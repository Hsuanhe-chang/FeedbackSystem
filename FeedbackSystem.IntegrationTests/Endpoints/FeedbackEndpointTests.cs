using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace FeedbackSystem.IntegrationTests.Endpoints;

/// <summary>
/// FeedbackController HTTP 端點的 Integration Test（Layer 2：HTTP → Controller → Service → SP → DB）。
///
/// 使用 WebApplicationFactory&lt;Program&gt; 在 in-process 中啟動完整應用程式（含真實 DB 連線），
/// 透過 HttpClient 發送 HTTP 請求，驗證整條鏈的狀態碼與回應內容。
///
/// 注意：POST 端點啟用了全域 AutoValidateAntiforgeryToken，
///       必須先 GET 頁面取得 Token 才能成功送出表單。
/// </summary>
public class FeedbackEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    // WebApplicationFactory 啟動的測試用應用程式所提供的 HttpClient
    private readonly HttpClient _client;

    // 保留連線字串，供寫入測試的 Teardown 直接執行 SQL 清理
    private readonly string _connectionString;

    /// <summary>
    /// 建構子：由 xUnit 注入 WebApplicationFactory，並讀取連線字串供 Teardown 使用。
    /// </summary>
    /// <param name="factory">啟動真實應用程式的工廠（自動讀取 appsettings.json）</param>
    public FeedbackEndpointTests(WebApplicationFactory<Program> factory)
    {
        // AllowAutoRedirect = false：讓測試可精確驗證 302 Redirect 而非追蹤最終頁面
        // （GET 端點使用；POST 端點另建 client，見各測試方法）
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // 讀取連線字串（與 IntegrationTestFixture 相同方式，但此處 WebApplicationFactory 已處理組態）
        // 直接讀取部署後的 appsettings.json 取得連線字串，供 Teardown SQL 指令使用
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        _connectionString = config.GetConnectionString("FeedbackDb")
            ?? throw new InvalidOperationException("appsettings.json 缺少 ConnectionStrings:FeedbackDb");
    }

    // ═════════════════════════════════════════════════════════════════
    // GET /Feedback/Index
    // ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// 驗證：GET /Feedback/Index 應回傳 HTTP 200，且頁面包含「意見」關鍵字。
    /// 整條鏈：HTTP GET → FeedbackController.Index → FeedbackService → usp_Feedback_GetPagedList → DB。
    /// </summary>
    [Fact]
    public async Task Get_FeedbackIndex_ReturnsOkWithPageContent()
    {
        // Act：發送 GET 請求至列表頁
        var response = await _client.GetAsync("/Feedback/Index");

        // Assert：狀態碼應為 200 OK
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /Feedback/Index 應成功回傳 HTML 頁面，若失敗代表 Controller 或 SP 有問題");

        // 讀取回應內容，驗證 HTML 包含列表頁預期的關鍵字
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("意見", "頁面應包含「意見」關鍵字，確認 View 有正確渲染");
    }

    /// <summary>
    /// 驗證：GET /Feedback/Index?status=0 帶 Status 篩選條件，應回傳 HTTP 200。
    /// </summary>
    [Fact]
    public async Task Get_FeedbackIndex_WithStatusFilter_ReturnsOk()
    {
        // Act：帶 Query String 篩選 Status=0（待處理）
        var response = await _client.GetAsync("/Feedback/Index?status=0&page=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "帶篩選條件的 GET /Feedback/Index 仍應回傳 200");
    }

    /// <summary>
    /// 驗證：GET /Feedback/Index?priority=3 帶 Priority 篩選條件，應回傳 HTTP 200。
    /// </summary>
    [Fact]
    public async Task Get_FeedbackIndex_WithPriorityFilter_ReturnsOk()
    {
        // Act：帶 Query String 篩選 Priority=3（緊急）
        var response = await _client.GetAsync("/Feedback/Index?priority=3&page=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "帶 Priority 篩選條件的 GET /Feedback/Index 仍應回傳 200");
    }

    // ═════════════════════════════════════════════════════════════════
    // GET /Feedback/Create
    // ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// 驗證：GET /Feedback/Create 應回傳 HTTP 200，且頁面包含 HTML 表單元素。
    /// 整條鏈：HTTP GET → FeedbackController.Create（GET） → GenerateUniqueTrackingCodeAsync → SP → DB。
    /// </summary>
    [Fact]
    public async Task Get_FeedbackCreate_ReturnsOkWithForm()
    {
        // Act
        var response = await _client.GetAsync("/Feedback/Create");

        // Assert：狀態碼應為 200 OK
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /Feedback/Create 應成功回傳建立表單頁");

        var content = await response.Content.ReadAsStringAsync();

        // 驗證頁面包含 HTML form 元素
        content.Should().Contain("<form", "Create 頁面應包含表單元素，確認 View 有正確渲染");

        // 驗證頁面已自動帶入 TrackingCode（由 GenerateUniqueTrackingCodeAsync 產生）
        content.Should().Contain("TrackingCode", "Create 表單應包含 TrackingCode 欄位");
    }

    // ═════════════════════════════════════════════════════════════════
    // POST /Feedback/Create（含 Anti-Forgery Token 處理）
    // ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// 驗證：POST /Feedback/Create 帶合法資料，成功後應 Redirect（302）到詳情頁。
    /// 整條鏈：HTTP POST → FeedbackController.Create（POST） → InsertFeedbackAsync → SP → DB。
    ///
    /// 步驟：
    ///   1. GET Create 頁面，擷取 Anti-Forgery Token
    ///   2. POST 表單，攜帶 Token
    ///   3. 驗證回應為 302 Redirect 到 /Feedback/Detail/{id}
    ///   4. Teardown：清理測試寫入的資料
    /// </summary>
    [Fact]
    public async Task Post_FeedbackCreate_WithValidData_RedirectsToDetailPage()
    {
        // ─── 使用啟用 Cookie 容器的 Client（Anti-Forgery 需要 Cookie 配合） ───
        // AllowAutoRedirect = false：在 302 階段停住，可驗證 Redirect 目標 URL
        var antiForgerySupportedFactory = new WebApplicationFactory<Program>();
        var clientWithCookies = antiForgerySupportedFactory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false, // 停在 302，驗證 Location header
                HandleCookies     = true   // 自動管理 Cookie（Anti-Forgery 所需）
            });

        int newFeedbackId = 0;
        // trackingCode 必須宣告在 try 外部，才能在 finally 的備援清理中使用。
        // 若 Regex 解析 Location URL 失敗導致 newFeedbackId = 0，
        // 仍可透過 TrackingCode 找到並清除已寫入的測試資料，防止 DB 污染。
        string trackingCode = string.Empty;

        try
        {
            // ─── Step 1：GET Create 頁面，取得 Anti-Forgery Token ───
            var getResponse = await clientWithCookies.GetAsync("/Feedback/Create");
            getResponse.StatusCode.Should().Be(HttpStatusCode.OK,
                "必須先成功取得 Create 頁面才能擷取 Anti-Forgery Token");

            var htmlContent = await getResponse.Content.ReadAsStringAsync();

            // 從 HTML 中用 Regex 擷取 __RequestVerificationToken 的 hidden input 值
            var tokenMatch = Regex.Match(htmlContent,
                @"<input[^>]+name=""__RequestVerificationToken""[^>]+value=""([^""]+)""",
                RegexOptions.IgnoreCase);

            tokenMatch.Success.Should().BeTrue(
                "GET Create 頁面必須包含 Anti-Forgery Token hidden input，否則 POST 會被拒絕（400）");

            var antiForgeryToken = tokenMatch.Groups[1].Value;

            // 同時擷取頁面中已產生的 TrackingCode（由後端帶入 hidden input）
            var trackingCodeMatch = Regex.Match(htmlContent,
                @"<input[^>]+name=""TrackingCode""[^>]+value=""([^""]+)""",
                RegexOptions.IgnoreCase);

            trackingCodeMatch.Success.Should().BeTrue(
                "Create 表單應包含 TrackingCode hidden input（由後端預先產生）");

            // 賦值給 try 外的變數，使 finally 的備援 Teardown 可取得 TrackingCode
            trackingCode = trackingCodeMatch.Groups[1].Value;

            // ─── Step 2：準備 POST 表單資料 ───
            var formData = new Dictionary<string, string>
            {
                ["TrackingCode"]               = trackingCode,           // 使用頁面產生的 TrackingCode
                ["CustomerName"]               = "[整合測試] POST 端點測試客戶",
                ["CustomerEmail"]              = "endpoint-test@example.com",
                ["CustomerPhone"]              = "",                      // 選填，空字串
                ["Category"]                   = "其他",
                ["Subject"]                    = "[整合測試] HTTP POST 端點自動化測試主旨",
                ["Content"]                    = "此為 HTTP 端點整合測試自動新增的資料，測試後將自動清除。",
                ["__RequestVerificationToken"] = antiForgeryToken         // 防偽 Token
            };

            // ─── Step 3：執行 POST ───
            var postResponse = await clientWithCookies.PostAsync(
                "/Feedback/Create",
                new FormUrlEncodedContent(formData));

            // Assert：成功新增後應回傳 302 Redirect 到 Detail 頁
            postResponse.StatusCode.Should().Be(HttpStatusCode.Found,
                "POST 表單合法資料後，Controller 應 RedirectToAction(Detail)，回傳 302 Found");

            // 驗證 Location header 指向 Detail 頁（/Feedback/Detail/{id}）
            var location = postResponse.Headers.Location?.ToString() ?? string.Empty;
            location.Should().Contain("/Feedback/Detail",
                "Redirect 目標應為 /Feedback/Detail/{新 FeedbackId}");

            // 從 Location URL 解析出新建的 FeedbackId，用於 Teardown
            var idMatch = Regex.Match(location, @"/Feedback/Detail/(\d+)", RegexOptions.IgnoreCase);
            if (idMatch.Success)
                newFeedbackId = int.Parse(idMatch.Groups[1].Value);

            // Assert 2：直接向 DB 查詢（raw SELECT，繞過 HTTP 與 SP 抽象層），
            // 確認 HTTP POST 觸發的寫入操作確實將資料持久化到資料庫。
            // 連線字串由此類建構子從 appsettings.json 讀取。
            if (newFeedbackId > 0)
            {
                var dbRow = await QueryFeedbackDirectFromDbAsync(newFeedbackId);
                dbRow.Exists.Should().BeTrue(
                    $"HTTP POST 成功後， FeedbackId={newFeedbackId} 應在 Feedback 資料表中確實存在");
                dbRow.CustomerName.Should().Be("[整合測試] POST 端點測試客戶",
                    "DB 實際存入的 CustomerName 應與 POST 表單資料一致");
                dbRow.TrackingCode.Should().Be(trackingCode,
                    "DB 實際存入的 TrackingCode 應與頁面產生的值一致");
                dbRow.CustomerEmail.Should().Be("endpoint-test@example.com",
                    "DB 實際存入的 CustomerEmail 應與 POST 表單資料一致");
                dbRow.Category.Should().Be("其他",
                    "DB 實際存入的 Category 應與 POST 表單資料一致");
                dbRow.Status.Should().Be(0, "HTTP POST 新增的意見 Status 預設應為 0（待處理）");
                dbRow.Priority.Should().Be(1, "HTTP POST 新增的意見 Priority 預設應為 1（一般）");
            }
        }
        finally
        {
            // Teardown：清理本次 POST 寫入的測試資料，不論測試成功或失敗都執行
            if (newFeedbackId > 0)
            {
                // 主要清理路徑：以 FeedbackId（從 Location URL 解析）精確刪除
                await DeleteTestFeedbackAsync(newFeedbackId);
            }
            else if (!string.IsNullOrEmpty(trackingCode))
            {
                // 備援清理路徑：當 Location URL Regex 解析失敗導致 newFeedbackId = 0 時，
                // 改以 TrackingCode 查找並清除可能已寫入的資料，確保不殘留測試污染
                await DeleteTestFeedbackByTrackingCodeAsync(trackingCode);
            }

            clientWithCookies.Dispose();
            antiForgerySupportedFactory.Dispose();
        }
    }

    /// <summary>
    /// 驗證：POST /Feedback/Create 缺少必填欄位（ModelState 驗證失敗），
    /// Controller 應重新顯示 Create 表單（HTTP 200），而非 Redirect。
    /// </summary>
    [Fact]
    public async Task Post_FeedbackCreate_MissingRequiredFields_ReturnsCreateView()
    {
        // ─── 建立支援 Cookie 的 Client ───
        var factory = new WebApplicationFactory<Program>();
        var clientWithCookies = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true,  // 允許追蹤 Redirect（驗證失敗時應回到同頁，無 Redirect）
            HandleCookies     = true
        });

        try
        {
            // ─── Step 1：GET Create 頁面取得 Anti-Forgery Token ───
            var getResponse = await clientWithCookies.GetAsync("/Feedback/Create");
            var htmlContent = await getResponse.Content.ReadAsStringAsync();

            var tokenMatch = Regex.Match(htmlContent,
                @"<input[^>]+name=""__RequestVerificationToken""[^>]+value=""([^""]+)""",
                RegexOptions.IgnoreCase);
            tokenMatch.Success.Should().BeTrue("必須取得 Anti-Forgery Token");
            var antiForgeryToken = tokenMatch.Groups[1].Value;

            var trackingCodeMatch = Regex.Match(htmlContent,
                @"<input[^>]+name=""TrackingCode""[^>]+value=""([^""]+)""",
                RegexOptions.IgnoreCase);
            var trackingCode = trackingCodeMatch.Success ? trackingCodeMatch.Groups[1].Value : "FBTEST12345678";

            // ─── Step 2：準備不完整表單（故意省略必填的 Subject 與 Content） ───
            var incompleteFormData = new Dictionary<string, string>
            {
                ["TrackingCode"]               = trackingCode,
                ["CustomerName"]               = "[整合測試] 驗證失敗測試客戶",
                ["CustomerEmail"]              = "invalid-test@example.com",
                // 故意省略 Category、Subject、Content（必填欄位）
                ["__RequestVerificationToken"] = antiForgeryToken
            };

            // ─── Step 3：執行 POST ───
            var postResponse = await clientWithCookies.PostAsync(
                "/Feedback/Create",
                new FormUrlEncodedContent(incompleteFormData));

            // Assert：ModelState 驗證失敗時，Controller 應回傳 HTTP 200（重新顯示表單）
            postResponse.StatusCode.Should().Be(HttpStatusCode.OK,
                "表單驗證失敗時，Controller 應回傳 200（重新顯示 Create 表單），而非 Redirect");

            var resultContent = await postResponse.Content.ReadAsStringAsync();

            // 驗證頁面仍包含 form 標籤（確認是 Create 表單，而非其他頁面）
            resultContent.Should().Contain("<form", "驗證失敗時應重新顯示包含表單的 Create 頁面");
        }
        finally
        {
            clientWithCookies.Dispose();
            factory.Dispose();
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // GET /Feedback/Detail/{id}
    // ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// 驗證：GET /Feedback/Detail/{不存在的 id} 應回傳 HTTP 404 Not Found。
    /// </summary>
    [Fact]
    public async Task Get_FeedbackDetail_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange：使用不可能存在的 ID
        int nonExistentId = int.MaxValue;

        // Act
        var response = await _client.GetAsync($"/Feedback/Detail/{nonExistentId}");

        // Assert：查無資料時 Controller 應回傳 404
        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            $"FeedbackId={nonExistentId} 不存在，Controller 應回傳 404 Not Found");
    }

    // ═════════════════════════════════════════════════════════════════
    // 私有工具方法
    // ═════════════════════════════════════════════════════════════════

    /// <summary>    /// DB 直查結果容器：用於 QueryFeedbackDirectFromDbAsync 回傳。
    /// </summary>
    private record FeedbackDbRow(
        bool    Exists,
        string  TrackingCode,
        string  CustomerName,
        string  CustomerEmail,
        string? CustomerPhone,
        string  Category,
        string  Subject,
        string  Content,
        byte    Status,
        byte    Priority);

    /// <summary>
    /// 直接以 SqlConnection + raw SELECT 查詢 Feedback 資料表，
    /// 完全繞過 HTTP 層、Controller、SP 與 Repository，
    /// 獨立驗證 HTTP POST 操作對資料庫的實際寫入結果。
    /// 連線字串由建構子從 appsettings.json 讀取。
    /// </summary>
    /// <param name="feedbackId">要查詢的 FeedbackId</param>
    /// <returns>查詢結果；若查無資料則 Exists = false</returns>
    private async Task<FeedbackDbRow> QueryFeedbackDirectFromDbAsync(int feedbackId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // raw SELECT：繞過所有 SP/Repository 抽象，直接讀取資料表
        await using var cmd = new SqlCommand(
            @"SELECT TrackingCode, CustomerName, CustomerEmail, CustomerPhone,
                     Category, Subject, Content, Status, Priority
              FROM   Feedback
              WHERE  FeedbackId = @FeedbackId", conn);
        cmd.Parameters.AddWithValue("@FeedbackId", feedbackId);

        await using var reader = await cmd.ExecuteReaderAsync();

        // 查無資料：表示 HTTP POST 未寫入 DB
        if (!await reader.ReadAsync())
            return new FeedbackDbRow(false, "", "", "", null, "", "", "", 0, 0);

        return new FeedbackDbRow(
            Exists:        true,
            TrackingCode:  reader.GetString(reader.GetOrdinal("TrackingCode")),
            CustomerName:  reader.GetString(reader.GetOrdinal("CustomerName")),
            CustomerEmail: reader.GetString(reader.GetOrdinal("CustomerEmail")),
            // CustomerPhone 為 nullable，需先判斷 IsDBNull 再讀取
            CustomerPhone: reader.IsDBNull(reader.GetOrdinal("CustomerPhone"))
                               ? null
                               : reader.GetString(reader.GetOrdinal("CustomerPhone")),
            Category:      reader.GetString(reader.GetOrdinal("Category")),
            Subject:       reader.GetString(reader.GetOrdinal("Subject")),
            Content:       reader.GetString(reader.GetOrdinal("Content")),
            Status:        reader.GetByte(reader.GetOrdinal("Status")),
            Priority:      reader.GetByte(reader.GetOrdinal("Priority")));
    }

    /// <summary>    /// Teardown 工具方法：依 FeedbackId 刪除測試資料。
    /// 遵守外鍵約束順序：先刪 FeedbackReply 子資料，再刪 Feedback 主資料。
    /// 使用參數化 SQL，防止 SQL Injection。
    /// </summary>
    /// <param name="feedbackId">要清理的測試資料 FeedbackId</param>
    private async Task DeleteTestFeedbackAsync(int feedbackId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // 先刪除 FeedbackReply 子資料（若有），外鍵約束要求先刪子表
        await using var deleteRepliesCmd = new SqlCommand(
            "DELETE FROM FeedbackReply WHERE FeedbackId = @FeedbackId", conn);
        deleteRepliesCmd.Parameters.AddWithValue("@FeedbackId", feedbackId);
        await deleteRepliesCmd.ExecuteNonQueryAsync();

        // 再刪除 Feedback 主資料
        await using var deleteFeedbackCmd = new SqlCommand(
            "DELETE FROM Feedback WHERE FeedbackId = @FeedbackId", conn);
        deleteFeedbackCmd.Parameters.AddWithValue("@FeedbackId", feedbackId);
        await deleteFeedbackCmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Teardown 備援工具方法：依 TrackingCode 查找並刪除測試資料。
    /// 當無法從 Location URL 解析出 FeedbackId 時作為備援使用，
    /// 確保即使主要清理路徑失效，測試資料仍不會殘留在測試 DB 中。
    /// TrackingCode 在 DB 中具唯一性（UNIQUE constraint），可安全用作查詢鍵。
    /// 使用參數化 SQL，防止 SQL Injection。
    /// </summary>
    /// <param name="trackingCode">要清理的測試資料 TrackingCode（唯一識別碼）</param>
    private async Task DeleteTestFeedbackByTrackingCodeAsync(string trackingCode)
    {
        if (string.IsNullOrEmpty(trackingCode))
            return; // 無 TrackingCode 代表資料從未被送出，無需清理

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // 以 TrackingCode 查出 FeedbackId（TrackingCode 為 UNIQUE，最多一筆）
        int feedbackId;
        await using (var selectCmd = new SqlCommand(
            "SELECT TOP 1 FeedbackId FROM Feedback WHERE TrackingCode = @TrackingCode", conn))
        {
            selectCmd.Parameters.AddWithValue("@TrackingCode", trackingCode);
            var result = await selectCmd.ExecuteScalarAsync();

            // 若查無資料，代表 POST 本來就未寫入，直接返回
            if (result == null || result == DBNull.Value)
                return;

            feedbackId = (int)result;
        }

        // 先刪除 FeedbackReply 子資料（遵守外鍵約束順序）
        await using var deleteRepliesCmd = new SqlCommand(
            "DELETE FROM FeedbackReply WHERE FeedbackId = @FeedbackId", conn);
        deleteRepliesCmd.Parameters.AddWithValue("@FeedbackId", feedbackId);
        await deleteRepliesCmd.ExecuteNonQueryAsync();

        // 再刪除 Feedback 主資料
        await using var deleteFeedbackCmd = new SqlCommand(
            "DELETE FROM Feedback WHERE FeedbackId = @FeedbackId", conn);
        deleteFeedbackCmd.Parameters.AddWithValue("@FeedbackId", feedbackId);
        await deleteFeedbackCmd.ExecuteNonQueryAsync();
    }

}
