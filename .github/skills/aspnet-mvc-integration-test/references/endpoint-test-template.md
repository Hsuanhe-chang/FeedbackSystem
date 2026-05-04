# HTTP 端點測試範本（Controller → Service → DB 整條鏈）

本範本示範如何使用 `WebApplicationFactory<Program>` 啟動完整應用程式，  
以 `HttpClient` 打真實 HTTP 請求，驗證整條鏈（Controller → Service → SP → DB）的運作。

---

## 前置：修改主專案 Program.cs

在 `Program.cs` 最後加入以下一行，讓測試專案可存取 `Program` 類別：

```csharp
// 加在 Program.cs 最後一行
// 允許 WebApplicationFactory<Program> 存取此應用程式入口
public partial class Program { }
```

---

## 範本 A：GET 端點測試（HTTP 200 + 頁面內容驗證）

測試 `GET /Feedback/Index` 是否正常回傳 HTML 頁面，  
驗證整條鏈：HTTP 請求 → FeedbackController.Index → FeedbackService.GetPagedListAsync → SP → DB。

```csharp
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FeedbackSystem.IntegrationTests;

/// <summary>
/// FeedbackController HTTP 端點的 Integration Test
/// 使用 WebApplicationFactory 啟動真實應用程式（含真實 DB 連線）
/// 透過 HttpClient 發送請求，驗證 HTTP 回應狀態碼與頁面內容
/// </summary>
public class FeedbackEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    // WebApplicationFactory 啟動的測試應用程式 HttpClient
    private readonly HttpClient _client;

    /// <summary>
    /// 建構子：由 xUnit 注入 WebApplicationFactory，建立 HttpClient
    /// WebApplicationFactory 會自動讀取主專案的 appsettings.json（含真實 DB 連線字串）
    /// </summary>
    /// <param name="factory">WebApplicationFactory 實例，負責啟動測試用的 ASP.NET Core 應用程式</param>
    public FeedbackEndpointTests(WebApplicationFactory<Program> factory)
    {
        // AllowAutoRedirect = false：讓我們能驗證 302 Redirect 而非追蹤到最終頁面
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    // ─────────────────────────────────────────────────────────────────
    // GET /Feedback/Index
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 驗證：GET /Feedback/Index 應回傳 HTTP 200，且回應包含頁面標題
    /// 整條鏈：HTTP GET → FeedbackController.Index → FeedbackService → usp_Feedback_GetPagedList
    /// </summary>
    [Fact]
    public async Task Get_FeedbackIndex_ReturnsOkWithPageContent()
    {
        // Act
        var response = await _client.GetAsync("/Feedback/Index");

        // Assert
        // 驗證 HTTP 狀態碼
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK,
            "GET /Feedback/Index 應成功回傳頁面，若失敗代表 Controller 或 Service 有問題");

        // 讀取回應內容，驗證是否包含預期的 HTML 元素
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("意見回饋", "頁面應包含標題文字，確認 View 有正確渲染");
    }

    /// <summary>
    /// 驗證：GET /Feedback/Index?status=0 可正常篩選並回傳 200
    /// </summary>
    [Fact]
    public async Task Get_FeedbackIndex_WithStatusFilter_ReturnsOk()
    {
        // Act
        // 帶入 Query String 篩選條件
        var response = await _client.GetAsync("/Feedback/Index?status=0&page=1");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    // ─────────────────────────────────────────────────────────────────
    // GET /Feedback/Create
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 驗證：GET /Feedback/Create 應回傳 HTTP 200，且頁面包含表單
    /// </summary>
    [Fact]
    public async Task Get_FeedbackCreate_ReturnsOkWithForm()
    {
        // Act
        var response = await _client.GetAsync("/Feedback/Create");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        // 驗證頁面包含 HTML form 元素（表單存在）
        content.Should().Contain("<form", "Create 頁面應包含表單元素");
    }
}
```

---

## 範本 B：POST 端點測試（含 Anti-Forgery Token 處理）

> **重要**：本專案在 `Program.cs` 啟用了全域 `AutoValidateAntiforgeryToken`，  
> POST 請求必須攜帶合法的 `__RequestVerificationToken`，否則回傳 400。  
> 測試時需先從 GET 頁面擷取 Token。

```csharp
using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FeedbackSystem.IntegrationTests;

/// <summary>
/// 測試 FeedbackController 的 POST 端點（新增意見）
/// 包含 Anti-Forgery Token 的處理邏輯
/// </summary>
public class FeedbackCreateEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public FeedbackCreateEndpointTests(WebApplicationFactory<Program> factory)
    {
        // 啟用 Cookie 容器（Anti-Forgery Token 需要 Cookie 配合）
        // AllowAutoRedirect = true：讓 POST 成功後可追蹤 Redirect
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true,  // 追蹤成功後的 Redirect
            HandleCookies = true       // 自動管理 Cookie（Anti-Forgery 所需）
        });
    }

    /// <summary>
    /// 驗證：POST /Feedback/Create 帶合法資料，應成功新增並 Redirect
    /// 整條鏈：HTTP POST → FeedbackController.Create → FeedbackService → usp_Feedback_Insert → DB
    /// </summary>
    [Fact]
    public async Task Post_FeedbackCreate_WithValidData_RedirectsToSuccessPage()
    {
        // ─── Step 1：先 GET 頁面，取得 Anti-Forgery Token ───
        var getResponse = await _client.GetAsync("/Feedback/Create");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK, "必須先成功取得 Create 頁面才能執行 POST");

        var htmlContent = await getResponse.Content.ReadAsStringAsync();

        // 從 HTML 中用 Regex 擷取 __RequestVerificationToken 的值
        var tokenMatch = Regex.Match(htmlContent,
            @"<input[^>]+name=""__RequestVerificationToken""[^>]+value=""([^""]+)""");

        tokenMatch.Success.Should().BeTrue("頁面必須包含 Anti-Forgery Token，否則 POST 會被拒絕（400）");
        var antiForgeryToken = tokenMatch.Groups[1].Value;

        // ─── Step 2：準備 POST 表單資料 ───
        // 使用可識別前綴，便於在 DB 中辨識測試資料以利清理
        var formData = new Dictionary<string, string>
        {
            ["CustomerName"]             = "[整合測試] 自動化測試用戶",
            ["CustomerEmail"]            = "integration-test@example.com",
            ["CustomerPhone"]            = "",   // 選填，空字串
            ["Category"]                 = "其他",
            ["Subject"]                  = "[整合測試] POST 端點自動化測試",
            ["Content"]                  = "此為 Integration Test 自動新增的資料，測試後請清理。",
            ["__RequestVerificationToken"] = antiForgeryToken
        };

        // ─── Step 3：執行 POST ───
        // Act
        var postResponse = await _client.PostAsync("/Feedback/Create",
            new FormUrlEncodedContent(formData));

        // ─── Step 4：驗證 ───
        // Assert：成功後應 Redirect（AllowAutoRedirect=true，所以驗證最終頁面 200）
        postResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "POST 成功後應 Redirect 到確認頁或列表頁，最終回應應為 200");

        // 可選：驗證最終頁面包含成功訊息
        var resultContent = await postResponse.Content.ReadAsStringAsync();
        resultContent.Should().NotContain("400", "成功的 POST 不應出現 400 錯誤頁");

        // ─── Teardown：清理測試資料（見 sp-test-template.md 的 DeleteTestFeedbackAsync）───
        // 注意：此處需要知道新增的 FeedbackId，可從 Redirect URL 解析，
        // 或直接透過 SqlConnection 查詢最新一筆 Subject 符合的資料進行刪除
        // await DeleteTestFeedbackBySubjectAsync("[整合測試] POST 端點自動化測試");
    }

    /// <summary>
    /// 驗證：POST /Feedback/Create 帶無效資料（缺少必填欄位），應回傳 200（重新顯示表單）而非 Redirect
    /// ModelState 驗證失敗時，Controller 應重新 return View(model)
    /// </summary>
    [Fact]
    public async Task Post_FeedbackCreate_WithMissingRequiredFields_ReturnsCreateView()
    {
        // ─── Step 1：取得 Anti-Forgery Token ───
        var getResponse = await _client.GetAsync("/Feedback/Create");
        var htmlContent = await getResponse.Content.ReadAsStringAsync();
        var tokenMatch = Regex.Match(htmlContent,
            @"<input[^>]+name=""__RequestVerificationToken""[^>]+value=""([^""]+)""");
        var antiForgeryToken = tokenMatch.Groups[1].Value;

        // ─── Step 2：準備不完整表單（故意省略必填的 Subject 與 Content） ───
        var incompleteFormData = new Dictionary<string, string>
        {
            ["CustomerName"]               = "測試客戶",
            ["CustomerEmail"]              = "test@example.com",
            // Subject 與 Content 故意不填（必填欄位）
            ["Category"]                   = "其他",
            ["__RequestVerificationToken"] = antiForgeryToken
        };

        // Act
        var postResponse = await _client.PostAsync("/Feedback/Create",
            new FormUrlEncodedContent(incompleteFormData));

        // Assert
        // ModelState 驗證失敗時，Controller 應回傳 200（重新顯示表單）而非 Redirect
        postResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "表單驗證失敗時，Controller 應回傳 Create 表單頁（HTTP 200），而非 Redirect");

        var resultContent = await postResponse.Content.ReadAsStringAsync();
        // 驗證頁面仍是 Create 表單（包含 form 標籤）
        resultContent.Should().Contain("<form", "驗證失敗時應重新顯示 Create 表單");
    }
}
```

---

## 範本 C：自訂 WebApplicationFactory（覆寫設定）

當需要對特定測試**覆寫 appsettings 設定**（例如指向不同 DB）或**替換特定 Service** 時：

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FeedbackSystem.IntegrationTests;

/// <summary>
/// 自訂 WebApplicationFactory，可覆寫特定組態或替換 DI 服務
/// 用於需要特殊環境設定的 Integration Test
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// 覆寫 ConfigureWebHost，在啟動前修改應用程式設定
    /// </summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // 覆寫 appsettings.json 中的設定值
        // 優先順序高於 appsettings.json，可用於 CI 環境指定不同 DB
        builder.ConfigureAppConfiguration((context, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // 若 CI 環境需要不同的連線字串，在此覆寫
                // ["ConnectionStrings:FeedbackDb"] = Environment.GetEnvironmentVariable("TEST_DB_CONN")
            });
        });

        // 若需要替換某個 Service（例如用真實 FeedbackService 但 Mock 其他依賴）
        builder.ConfigureServices(services =>
        {
            // 範例：移除現有 IFeedbackService 並注入測試用實作
            // services.RemoveAll<IFeedbackService>();
            // services.AddScoped<IFeedbackService, TestFeedbackService>();
        });
    }
}
```

使用方式：

```csharp
// 將 WebApplicationFactory<Program> 替換為自訂 Factory
public class MyTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MyTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }
}
```
