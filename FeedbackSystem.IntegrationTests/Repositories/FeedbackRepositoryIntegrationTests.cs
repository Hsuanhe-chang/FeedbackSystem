using System.Data;
using FluentAssertions;
using FeedbackSystem.Models.ViewModels;
using FeedbackSystem.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace FeedbackSystem.IntegrationTests.Repositories;

/// <summary>
/// FeedbackRepository 的 Integration Test（Layer 1：Repository → SP → 真實 DB）。
///
/// 測試範圍：
///   - usp_Feedback_GetPagedList   → GetPagedListAsync
///   - usp_Feedback_CheckTrackingCodeExists → CheckTrackingCodeExistsAsync
///   - usp_Feedback_Insert         → InsertFeedbackAsync
///   - usp_Feedback_GetById        → GetByIdAsync
///
/// 前提：測試 DB（Server=ymmistest, Database=Feedback_Test）可連線，
///       且已執行過建表與 SP 的 DDL 腳本。
/// </summary>
public class FeedbackRepositoryIntegrationTests : IClassFixture<IntegrationTestFixture>
{
    // 從 Fixture 取得的 Repository 實例（已注入真實連線字串）
    private readonly FeedbackRepository _sut;

    // 保留連線字串供 Teardown 的 SqlConnection 直接使用
    private readonly string _connectionString;

    /// <summary>
    /// 建構子：由 xUnit 自動注入 Fixture，建立 FeedbackRepository 實例。
    /// </summary>
    /// <param name="fixture">共用 Fixture，包含從 appsettings.json 讀取的連線字串</param>
    public FeedbackRepositoryIntegrationTests(IntegrationTestFixture fixture)
    {
        _connectionString = fixture.ConnectionString;

        // 建立真實 IConfiguration，供 FeedbackRepository 建構子取得連線字串
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // 直接使用 Fixture 快取的連線字串，避免再次讀取 JSON 檔
                ["ConnectionStrings:FeedbackDb"] = _connectionString
            })
            .Build();

        // 直接實例化 Repository（被測目標），繞過 DI，模擬真實 SP 呼叫
        _sut = new FeedbackRepository(config);
    }

    // ═════════════════════════════════════════════════════════════════
    // 1. GetPagedListAsync → usp_Feedback_GetPagedList
    // ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// 不帶任何篩選條件取得第一頁時，SP 應回傳清單與非負的 TotalCount。
    /// 前提：測試 DB 至少有一筆資料（或空清單也可驗證結構正確）。
    /// </summary>
    [Fact]
    public async Task GetPagedListAsync_NoFilter_ReturnsItemsAndNonNegativeTotalCount()
    {
        // Arrange：不篩選 Status / Priority，取第 1 頁、每頁 10 筆
        byte? status = null;
        byte? priority = null;
        int page = 1;
        int pageSize = 10;

        // Act：呼叫 Repository，觸發 usp_Feedback_GetPagedList
        var (items, totalCount) = await _sut.GetPagedListAsync(status, priority, page, pageSize);

        // Assert
        items.Should().NotBeNull("SP 應永遠回傳清單物件，即使是空清單也不應為 null");
        totalCount.Should().BeGreaterThanOrEqualTo(0, "TotalCount OUTPUT 參數不應為負數");

        // 驗證每筆資料的必填欄位與值域
        foreach (var item in items)
        {
            item.FeedbackId.Should().BeGreaterThan(0, "FeedbackId 為 IDENTITY 欄位，不應 <= 0");
            item.TrackingCode.Should().NotBeNullOrEmpty("TrackingCode 為 NOT NULL 欄位");
            item.CustomerName.Should().NotBeNullOrEmpty("CustomerName 為 NOT NULL 欄位");
            item.Category.Should().NotBeNullOrEmpty("Category 為 NOT NULL 欄位");
            item.Subject.Should().NotBeNullOrEmpty("Subject 為 NOT NULL 欄位");
            // Status 允許值：0=待處理, 1=處理中, 2=已回覆, 3=已關閉
            item.Status.Should().BeInRange(0, 3, "Status 只允許 0~3");
            // Priority 允許值：1=一般, 2=重要, 3=緊急
            item.Priority.Should().BeInRange(1, 3, "Priority 只允許 1~3");
            item.CreatedAt.Should().NotBe(default, "CreatedAt 為 NOT NULL 欄位，不應為 DateTime.MinValue");
        }
    }

    /// <summary>
    /// 以特定 Status 篩選時，SP 回傳的每筆資料 Status 都應符合篩選值。
    /// </summary>
    [Theory]
    [InlineData((byte)0)]  // 待處理
    [InlineData((byte)1)]  // 處理中
    [InlineData((byte)2)]  // 已回覆
    [InlineData((byte)3)]  // 已關閉
    public async Task GetPagedListAsync_WithStatusFilter_ReturnsOnlyMatchingRows(byte filterStatus)
    {
        // Arrange：以特定 Status 篩選，取前 100 筆（確保能驗證篩選結果）
        int page = 1;
        int pageSize = 100;

        // Act
        var (items, _) = await _sut.GetPagedListAsync(filterStatus, null, page, pageSize);

        // Assert：每筆資料的 Status 都應等於篩選值
        foreach (var item in items)
        {
            item.Status.Should().Be(filterStatus,
                $"以 Status={filterStatus} 篩選後，回傳每筆資料的 Status 均應為 {filterStatus}");
        }
    }

    /// <summary>
    /// 以特定 Priority 篩選時，SP 回傳的每筆資料 Priority 都應符合篩選值。
    /// </summary>
    [Theory]
    [InlineData((byte)1)]  // 一般
    [InlineData((byte)2)]  // 重要
    [InlineData((byte)3)]  // 緊急
    public async Task GetPagedListAsync_WithPriorityFilter_ReturnsOnlyMatchingRows(byte filterPriority)
    {
        // Arrange
        int page = 1;
        int pageSize = 100;

        // Act
        var (items, _) = await _sut.GetPagedListAsync(null, filterPriority, page, pageSize);

        // Assert：每筆資料的 Priority 都應等於篩選值
        foreach (var item in items)
        {
            item.Priority.Should().Be(filterPriority,
                $"以 Priority={filterPriority} 篩選後，回傳每筆資料的 Priority 均應為 {filterPriority}");
        }
    }

    /// <summary>
    /// 分頁功能驗證：第 2 頁回傳的資料數量不超過 pageSize。
    /// </summary>
    [Fact]
    public async Task GetPagedListAsync_SecondPage_ReturnsAtMostPageSizeItems()
    {
        // Arrange：設定每頁 5 筆，取第 2 頁
        int page = 2;
        int pageSize = 5;

        // Act
        var (items, totalCount) = await _sut.GetPagedListAsync(null, null, page, pageSize);

        // Assert：回傳筆數不超過 pageSize（可能不足 pageSize 但不應超過）
        items.Count().Should().BeLessOrEqualTo(pageSize,
            $"第 {page} 頁最多回傳 {pageSize} 筆，實際不應超過此值");

        totalCount.Should().BeGreaterThanOrEqualTo(0, "TotalCount 不應為負數");
    }

    // ═════════════════════════════════════════════════════════════════
    // 2. CheckTrackingCodeExistsAsync → usp_Feedback_CheckTrackingCodeExists
    // ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// 傳入顯然不存在的 TrackingCode（含 GUID 確保唯一），SP 應回傳 false。
    /// </summary>
    [Fact]
    public async Task CheckTrackingCodeExistsAsync_NonExistentCode_ReturnsFalse()
    {
        // Arrange：使用含 GUID 的代碼，確保資料庫中不可能存在
        var nonExistentCode = $"TEST_{Guid.NewGuid():N}"[..16];

        // Act：呼叫 usp_Feedback_CheckTrackingCodeExists
        var exists = await _sut.CheckTrackingCodeExistsAsync(nonExistentCode);

        // Assert：剛產生的 GUID 代碼必定不存在
        exists.Should().BeFalse("此 TrackingCode 剛產生，資料庫中不應存在對應資料");
    }

    // ═════════════════════════════════════════════════════════════════
    // 3. InsertFeedbackAsync → usp_Feedback_Insert
    // ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// 傳入合法的建立 ViewModel，SP 應回傳正整數的新 FeedbackId（IDENTITY 值）。
    /// 測試後無論成功或失敗，均執行 Teardown 清理測試資料，避免污染測試 DB。
    /// </summary>
    [Fact]
    public async Task InsertFeedbackAsync_ValidModel_ReturnsNewPositiveFeedbackId()
    {
        // Arrange：準備合法的測試資料，TrackingCode 以 GUID 確保唯一性
        var model = new FeedbackCreateViewModel
        {
            // 截取前 16 字元確保不超過欄位長度（DB 欄位 nvarchar(20)）
            TrackingCode  = $"ITST{Guid.NewGuid():N}"[..16],
            CustomerName  = "[整合測試] 測試客戶",
            CustomerEmail = "integration-test@example.com",
            CustomerPhone = null,           // 選填欄位，測試 null 值的 DBNull 轉換
            Category      = "其他",
            Subject       = "[整合測試] InsertFeedbackAsync 自動化測試主旨",
            Content       = "此為 Integration Test 自動新增的測試資料，測試完成後將自動清除。"
        };

        int newFeedbackId = 0;

        try
        {
            // Act：呼叫 usp_Feedback_Insert
            newFeedbackId = await _sut.InsertFeedbackAsync(model);

            // Assert：SP 應回傳正整數（DB IDENTITY 新值）
            newFeedbackId.Should().BeGreaterThan(0,
                "usp_Feedback_Insert 應回傳新產生的 FeedbackId（IDENTITY 值），必為正整數");
        }
        finally
        {
            // Teardown：無論測試成功或失敗，均刪除本次新增的測試資料，確保 DB 不殘留測試污染
            if (newFeedbackId > 0)
                await DeleteTestFeedbackAsync(newFeedbackId);
        }
    }

    /// <summary>
    /// 新增後立即以 GetByIdAsync 查詢，驗證 SP 寫入的資料可完整讀回。
    /// 此為 Layer 3 的跨層整合驗證：Insert SP → GetById SP → 資料一致性。
    /// </summary>
    [Fact]
    public async Task InsertThenGetById_DataRoundTrip_ReturnsMatchingDetail()
    {
        // Arrange：準備一筆完整測試資料
        var trackingCode = $"ITST{Guid.NewGuid():N}"[..16];
        var model = new FeedbackCreateViewModel
        {
            TrackingCode  = trackingCode,
            CustomerName  = "[整合測試] Round-Trip 測試客戶",
            CustomerEmail = "round-trip@example.com",
            CustomerPhone = "0912345678",   // 手機格式，測試非 null 的 Phone 欄位
            Category      = "產品",
            Subject       = "[整合測試] Round-Trip 測試主旨",
            Content       = "驗證 Insert 後 GetById 資料一致性的整合測試資料。"
        };

        int newFeedbackId = 0;

        try
        {
            // Act：新增資料
            newFeedbackId = await _sut.InsertFeedbackAsync(model);
            newFeedbackId.Should().BeGreaterThan(0, "Insert 應成功並回傳正整數 FeedbackId");

            // Act：立即以 FeedbackId 讀回剛新增的資料
            var detail = await _sut.GetByIdAsync(newFeedbackId);

            // Assert：讀回的資料應與寫入時完全一致
            detail.Should().NotBeNull("剛新增的資料應可被 GetById 查詢到");
            detail!.FeedbackId.Should().Be(newFeedbackId, "FeedbackId 應與 Insert 回傳的值一致");
            detail.TrackingCode.Should().Be(model.TrackingCode, "TrackingCode 應與寫入值一致");
            detail.CustomerName.Should().Be(model.CustomerName, "CustomerName 應與寫入值一致");
            detail.CustomerEmail.Should().Be(model.CustomerEmail, "CustomerEmail 應與寫入值一致");
            detail.CustomerPhone.Should().Be(model.CustomerPhone, "CustomerPhone 應與寫入值一致");
            detail.Category.Should().Be(model.Category, "Category 應與寫入值一致");
            detail.Subject.Should().Be(model.Subject, "Subject 應與寫入值一致");
            detail.Content.Should().Be(model.Content, "Content 應與寫入值一致");
            // 新增時 Status 預設為 0（待處理）、Priority 預設為 1（一般）
            detail.Status.Should().Be(0, "新增的意見 Status 預設應為 0（待處理）");
            detail.Priority.Should().Be(1, "新增的意見 Priority 預設應為 1（一般）");
        }
        finally
        {
            // Teardown：清理測試資料，遵守外鍵順序（先刪回覆再刪意見）
            if (newFeedbackId > 0)
                await DeleteTestFeedbackAsync(newFeedbackId);
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // 4. GetByIdAsync → usp_Feedback_GetById
    // ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// 傳入不存在的 FeedbackId，SP 應回傳 null（查無資料）。
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        // Arrange：使用一個極大的 ID，確保資料庫中不存在
        // int.MaxValue（2,147,483,647）不可能為真實 IDENTITY 值
        int nonExistentId = int.MaxValue;

        // Act：呼叫 usp_Feedback_GetById
        var result = await _sut.GetByIdAsync(nonExistentId);

        // Assert：查無資料時 Repository 應回傳 null
        result.Should().BeNull("不存在的 FeedbackId 應讓 SP 回傳空結果集，Repository 應回傳 null");
    }

    // ═════════════════════════════════════════════════════════════════
    // 5. OUTPUT 參數低層驗證（直接呼叫 SP，繞過 Repository 抽象）
    // ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// 直接以 SqlCommand 呼叫 usp_Feedback_GetPagedList，
    /// 驗證 OUTPUT 參數 @TotalCount 不為負數。
    /// 此測試為低層驗證，確認 SP 本身的 OUTPUT 參數行為符合預期。
    /// </summary>
    [Fact]
    public async Task Usp_GetPagedList_OutputTotalCount_IsNonNegative()
    {
        // Arrange：建立直接的 SqlConnection，繞過 Repository 抽象層
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand("usp_Feedback_GetPagedList", conn)
        {
            CommandType = CommandType.StoredProcedure
        };

        // 傳入分頁參數（不篩選，取第 1 頁 10 筆）
        cmd.Parameters.AddWithValue("@Status",   DBNull.Value);
        cmd.Parameters.AddWithValue("@Priority", DBNull.Value);
        cmd.Parameters.AddWithValue("@Page",     1);
        cmd.Parameters.AddWithValue("@PageSize", 10);

        // 宣告 OUTPUT 參數接收 SP 回傳的 TotalCount
        var totalCountParam = new SqlParameter("@TotalCount", SqlDbType.Int)
        {
            Direction = ParameterDirection.Output
        };
        cmd.Parameters.Add(totalCountParam);

        // Act：執行 SP 並讀完結果集（必須先關閉 Reader 才能讀 OUTPUT 參數）
        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.CloseAsync();

        int totalCount = (int)(totalCountParam.Value ?? 0);

        // Assert：TotalCount OUTPUT 參數值不應為負數
        totalCount.Should().BeGreaterThanOrEqualTo(0,
            "@TotalCount OUTPUT 參數為筆數統計，不應出現負數");
    }

    // ═════════════════════════════════════════════════════════════════
    // 私有工具方法
    // ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// Teardown 工具方法：依 FeedbackId 刪除測試資料。
    /// 遵守外鍵約束順序：先刪 FeedbackReply（子資料表）再刪 Feedback（主資料表）。
    /// 使用參數化 SQL，防止 SQL Injection。
    /// </summary>
    /// <param name="feedbackId">要清理的測試資料 FeedbackId</param>
    private async Task DeleteTestFeedbackAsync(int feedbackId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // 先刪除 FeedbackReply 子資料（若有），否則外鍵約束阻止主資料表刪除
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
