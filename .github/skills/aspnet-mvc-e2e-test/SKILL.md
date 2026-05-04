---
name: aspnet-mvc-e2e-test
description: "Use when writing E2E (end-to-end) browser tests for ASP.NET Core MVC projects using Playwright .NET. Simulates real user browser interactions: page navigation, form filling, button clicks, UI assertions. Reads app base URL from launchSettings.json and DB connection from appsettings.json. Triggers: E2E test, end-to-end test, browser test, Playwright, UI test, simulate user, page navigation test, form submit test. NOT for unit tests (use aspnet-mvc-unit-test) or integration tests without browser (use aspnet-mvc-integration-test)."
argument-hint: "Specify what to test: e.g. '新增 Feedback 完整流程' or 'Feedback 列表頁搜尋與分頁'"
---

# ASP.NET Core MVC E2E Test Skill（Playwright .NET）

## 目的

為 ASP.NET Core MVC (.NET 10) 專案產生 **E2E Test**，  
使用 **Playwright .NET** 模擬真實使用者在瀏覽器中的操作流程，  
驗證從「頁面載入 → 使用者互動 → UI 狀態變化」的完整路徑。

> **測試層次邊界（三層一覽）**
>
> | 測試類型 | 範疇 | 依賴 | 本 Skill |
> |---------|------|------|----------|
> | Unit Test | 單一類別邏輯 | Mock 隔離 | ❌ 請使用 `aspnet-mvc-unit-test` |
> | Integration Test | Controller → Service → SP → 真實 DB | 測試 DB，無瀏覽器 | ❌ 請使用 `aspnet-mvc-integration-test` |
> | **E2E Test（本 Skill）** | 使用者 UI 操作完整流程 | **真實瀏覽器 + 執行中的應用程式** | ✅ 涵蓋 |
>
> **E2E Test 啟動真實瀏覽器（Chromium / Firefox / WebKit），不使用 Mock 或 TestServer。**  
> 應用程式必須以 `dotnet run` 或 `dotnet watch` 方式事先在本機或 CI 環境中啟動。

> **本專案測試 DB**：Server=`ymmistest`，Database=`Feedback_Test`  
> 連線字串 Key：`ConnectionStrings:FeedbackDb`（讀取自 `appsettings.json`）  
> 應用程式基底 URL：從 `Properties/launchSettings.json` 的 `applicationUrl` 取得（預設 `https://localhost:7xxx`）

---

## 前置確認清單

### 步驟 1：確認測試專案是否存在

搜尋 `.sln` 同層是否有 `*.E2ETests.csproj` 或 `*.PlaywrightTests.csproj`：

- **若不存在**，提示使用者執行以下指令建立並加入解決方案：
  ```bash
  # 建立 NUnit 測試專案（Playwright 官方建議使用 NUnit 或 MSTest）
  dotnet new nunit -n FeedbackSystem.E2ETests -o FeedbackSystem.E2ETests
  dotnet sln add FeedbackSystem.E2ETests/FeedbackSystem.E2ETests.csproj
  ```

- **為何使用 NUnit？**  
  Playwright .NET 為 NUnit / MSTest 提供 `PageTest` 基底類別，可自動管理 Browser、BrowserContext、Page 的生命週期。  
  若使用 xUnit，需手動管理這些物件（可行但較繁瑣）。

### 步驟 2：確認 NuGet 套件

完整套件清單須包含：

```xml
<!-- Playwright .NET 核心套件 -->
<PackageReference Include="Microsoft.Playwright" Version="1.*" />

<!-- NUnit 整合：提供 PageTest 基底類別，自動管理瀏覽器生命週期 -->
<PackageReference Include="Microsoft.Playwright.NUnit" Version="1.*" />

<!-- NUnit 框架 -->
<PackageReference Include="NUnit" Version="3.*" />
<PackageReference Include="NUnit3TestAdapter" Version="4.*" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />

<!-- 組態讀取：從 appsettings.json 取得 DB 連線字串（用於測試後資料清理） -->
<PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="10.*" />
<PackageReference Include="Microsoft.Extensions.Configuration.EnvironmentVariables" Version="10.*" />

<!-- DB 直接操作：驗證寫入結果或執行 teardown 清理 -->
<PackageReference Include="Microsoft.Data.SqlClient" Version="5.*" />

<!-- 選用：提升 Assert 可讀性 -->
<PackageReference Include="FluentAssertions" Version="6.*" />
```

> 安裝 NuGet 後，**必須執行一次瀏覽器安裝指令**：
> ```bash
> # 進入測試專案目錄後執行（安裝 Chromium，也可選 firefox / webkit）
> pwsh bin/Debug/net10.0/playwright.ps1 install chromium
> ```

### 步驟 3：讀取應用程式基底 URL

E2E Test 需要知道應用程式的執行網址，從 `Properties/launchSettings.json` 讀取：

```csharp
// 標準方式：直接硬寫或從環境變數取得 Base URL
// 建議定義為 const 或讀取環境變數，方便 CI/CD 切換
private const string BaseUrl = "https://localhost:7xxx"; // 從 launchSettings.json applicationUrl 取得

// 或使用環境變數（CI 環境推薦）
private static readonly string BaseUrl =
    Environment.GetEnvironmentVariable("E2E_BASE_URL") ?? "https://localhost:7xxx";
```

### 步驟 4：讀取 DB 連線字串（用於 Teardown）

E2E Test 寫入操作後，需直接查詢 DB 清理測試資料：

```csharp
// 讀取主專案 appsettings.json 的連線字串（供 Teardown 使用）
var config = new ConfigurationBuilder()
    // 設定基底路徑為主專案的輸出目錄，或直接讀取測試目錄內的 appsettings.json
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables() // 允許 CI 環境變數覆寫
    .Build();

var connectionString = config.GetConnectionString("FeedbackDb")
    ?? throw new InvalidOperationException("找不到 ConnectionStrings:FeedbackDb，請確認 appsettings.json 已複製至輸出目錄");
```

> **重要**：需在 `.csproj` 中將 `appsettings.json` 設為「複製到輸出目錄」：
> ```xml
> <ItemGroup>
>   <!-- 複製主專案的 appsettings.json 以供讀取連線字串 -->
>   <Content Include="..\FeedbackSystem\appsettings.json">
>     <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
>     <Link>appsettings.json</Link>
>   </Content>
> </ItemGroup>
> ```

### 步驟 5：確認應用程式已啟動

E2E Test **不會自動啟動應用程式**，執行前必須確認：

```bash
# 在另一個終端機視窗啟動應用程式
dotnet run --project FeedbackSystem/FeedbackSystem.csproj

# 或使用 watch 模式（開發時）
dotnet watch --project FeedbackSystem/FeedbackSystem.csproj
```

> CI/CD 環境中，應在 pipeline 的前置步驟啟動應用程式，  
> 並等待健康檢查通過後再執行 E2E Test。

---

## E2E 測試撰寫程序

### 測試類別架構（使用 PageTest 基底類別）

```csharp
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace FeedbackSystem.E2ETests;

/// <summary>
/// FeedbackController 的 E2E 測試：繼承 PageTest 自動取得已初始化的 Page 物件，
/// 模擬使用者在瀏覽器中操作 Feedback 相關頁面的完整流程。
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)] // 每個 TestFixture 使用獨立 BrowserContext，可安全並行
public class FeedbackE2ETests : PageTest
{
    // ─── 應用程式設定 ───────────────────────────────────────────────
    // 從環境變數取得 Base URL，未設定時使用 launchSettings.json 預設值
    private static readonly string BaseUrl =
        Environment.GetEnvironmentVariable("E2E_BASE_URL") ?? "https://localhost:7001";

    // ─── DB 連線（用於 Teardown 清理測試資料）──────────────────────
    private string _connectionString = string.Empty;

    /// <summary>
    /// 每個測試類別初始化一次：讀取 appsettings.json 的 DB 連線字串。
    /// </summary>
    [OneTimeSetUp]
    public void ReadConfiguration()
    {
        // 從輸出目錄讀取 appsettings.json
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        // 取得連線字串，若缺少則提前失敗以避免靜默錯誤
        _connectionString = config.GetConnectionString("FeedbackDb")
            ?? throw new InvalidOperationException(
                "appsettings.json 缺少 ConnectionStrings:FeedbackDb");
    }

    // ─── 測試方法範例（見下方章節）──────────────────────────────────
}
```

---

## 測試情境撰寫規則

### 1. 頁面導航與 UI 狀態驗證

```csharp
/// <summary>
/// 驗證 Feedback 列表頁能正常載入，且頁面標題與表格標頭符合預期。
/// </summary>
[Test]
public async Task FeedbackIndex_PageLoads_ShowsTableWithExpectedHeaders()
{
    // ── Arrange ──────────────────────────────────────────────────────
    // 導航至 Feedback 列表頁
    await Page.GotoAsync($"{BaseUrl}/Feedback");

    // ── Act ──────────────────────────────────────────────────────────
    // 等待頁面主要內容元素出現（Playwright 會自動 retry until visible）
    var heading = Page.GetByRole(AriaRole.Heading, new() { Name = "意見回饋清單" });
    var table = Page.Locator("table.feedback-list");

    // ── Assert ───────────────────────────────────────────────────────
    // 驗證標題可見
    await Expect(heading).ToBeVisibleAsync();
    // 驗證表格存在
    await Expect(table).ToBeVisibleAsync();
}
```

### 2. 表單填寫與送出流程

```csharp
/// <summary>
/// 驗證使用者填寫並送出新增 Feedback 表單後，能跳轉至列表頁並顯示成功訊息。
/// 測試完成後透過 DB 刪除測試資料（Teardown）。
/// </summary>
[Test]
public async Task FeedbackCreate_FillAndSubmitForm_RedirectsToIndexWithSuccessMessage()
{
    // ── Arrange ──────────────────────────────────────────────────────
    // 使用唯一前綴標記測試資料，方便 Teardown 識別並清理
    var testTitle = $"[E2E_TEST] {Guid.NewGuid():N}";

    // 記錄測試資料 ID，供 Teardown 清理使用
    int createdFeedbackId = 0;

    try
    {
        // ── Act ──────────────────────────────────────────────────────
        // 導航至新增頁面
        await Page.GotoAsync($"{BaseUrl}/Feedback/Create");

        // 等待表單載入完成
        await Page.WaitForSelectorAsync("form");

        // 填寫表單欄位（使用 GetByLabel 定位，語意明確且不依賴 DOM 結構）
        await Page.GetByLabel("標題").FillAsync(testTitle);
        await Page.GetByLabel("內容").FillAsync("這是 E2E 測試自動填寫的內容");
        await Page.GetByLabel("分類").SelectOptionAsync("建議");

        // 按下送出按鈕
        await Page.GetByRole(AriaRole.Button, new() { Name = "送出" }).ClickAsync();

        // ── Assert ───────────────────────────────────────────────────
        // 驗證已跳轉至列表頁（URL 應變更為 /Feedback）
        await Expect(Page).ToHaveURLAsync(new Regex($"{Regex.Escape(BaseUrl)}/Feedback$"));

        // 驗證成功訊息可見
        await Expect(Page.Locator(".alert-success")).ToBeVisibleAsync();

        // （選用）驗證列表中出現剛新增的資料
        await Expect(Page.GetByText(testTitle)).ToBeVisibleAsync();
    }
    finally
    {
        // ── Teardown ─────────────────────────────────────────────────
        // 無論測試成功或失敗，都執行清理以避免污染測試 DB
        await CleanupTestFeedbackByTitleAsync(testTitle);
    }
}
```

### 3. 搜尋與篩選操作

```csharp
/// <summary>
/// 驗證使用者在列表頁輸入關鍵字後，搜尋結果只顯示符合條件的資料。
/// </summary>
[Test]
public async Task FeedbackIndex_SearchByKeyword_FiltersResultsCorrectly()
{
    // ── Arrange ──────────────────────────────────────────────────────
    await Page.GotoAsync($"{BaseUrl}/Feedback");

    // ── Act ──────────────────────────────────────────────────────────
    // 在搜尋框輸入關鍵字
    await Page.GetByPlaceholder("搜尋標題或內容").FillAsync("測試關鍵字");

    // 按下搜尋按鈕（或按 Enter）
    await Page.GetByRole(AriaRole.Button, new() { Name = "搜尋" }).ClickAsync();

    // 等待搜尋結果更新（等待網路請求完成或特定元素重新渲染）
    await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

    // ── Assert ───────────────────────────────────────────────────────
    // 驗證結果區域中所有顯示的標題都包含關鍵字
    var resultRows = Page.Locator("table tbody tr");
    var count = await resultRows.CountAsync();

    // 至少有一筆結果（若已知測試資料存在）
    Assert.That(count, Is.GreaterThan(0), "搜尋結果不應為空");
}
```

### 4. 驗證錯誤訊息（表單驗證）

```csharp
/// <summary>
/// 驗證使用者送出空白表單時，頁面顯示欄位必填的驗證錯誤訊息。
/// </summary>
[Test]
public async Task FeedbackCreate_SubmitEmptyForm_ShowsValidationErrors()
{
    // ── Arrange ──────────────────────────────────────────────────────
    await Page.GotoAsync($"{BaseUrl}/Feedback/Create");
    await Page.WaitForSelectorAsync("form");

    // ── Act ──────────────────────────────────────────────────────────
    // 不填任何欄位，直接按下送出
    await Page.GetByRole(AriaRole.Button, new() { Name = "送出" }).ClickAsync();

    // ── Assert ───────────────────────────────────────────────────────
    // 驗證表單並未跳轉（仍在 Create 頁）
    await Expect(Page).ToHaveURLAsync(new Regex($"{Regex.Escape(BaseUrl)}/Feedback/Create"));

    // 驗證必填欄位的錯誤訊息出現（使用 Bootstrap 的 field-validation-error class）
    var titleError = Page.Locator("[data-valmsg-for='Title']");
    await Expect(titleError).ToBeVisibleAsync();
    await Expect(titleError).ToHaveTextAsync(new Regex(".+"), new() { UseInnerText = true });
}
```

---

## Locator 選擇策略（優先順序）

| 優先序 | 方式 | 範例 | 建議情境 |
|--------|------|------|---------|
| 1 | `GetByRole` | `Page.GetByRole(AriaRole.Button, new() { Name = "送出" })` | 按鈕、連結、標題等語意元素 |
| 2 | `GetByLabel` | `Page.GetByLabel("標題")` | 表單欄位（關聯 `<label>` 的 `<input>`） |
| 3 | `GetByPlaceholder` | `Page.GetByPlaceholder("搜尋...")` | 帶有 placeholder 的輸入框 |
| 4 | `GetByText` | `Page.GetByText("儲存成功")` | 頁面上的靜態文字 |
| 5 | `GetByTestId` | `Page.GetByTestId("feedback-list")` | 有設 `data-testid` 屬性的元素 |
| 6 | CSS Selector | `Page.Locator(".alert-success")` | 語意方式無法定位時的備選 |
| ❌ 禁止 | XPath / ID | `Page.Locator("#id123")` | 脆弱、耦合 DOM 結構，避免使用 |

> **原則**：優先使用「使用者可感知的語意」定位元素，避免依賴實作細節（ID、class 名稱）。

---

## 測試隔離與資料清理策略

| 場景 | 建議策略 |
|------|---------|
| 只讀操作（瀏覽列表、搜尋） | 無需清理，直接驗證 UI 狀態 |
| 寫入操作（新增、編輯） | 使用 `[E2E_TEST]` 前綴標記，在 `finally` 區塊以 DB 直連清理 |
| 跨頁面流程（新增後編輯再刪除） | 以 `try/finally` 包覆整個流程，確保任何步驟失敗都能 teardown |
| 多測試共用前置資料 | 在 `[OneTimeSetUp]` 建立，在 `[OneTimeTearDown]` 刪除 |

### 標準 Teardown 輔助方法

```csharp
/// <summary>
/// 透過 DB 直連刪除標題包含測試前綴的 Feedback 測試資料。
/// 使用獨立的 SqlConnection，不依賴 Playwright 瀏覽器狀態。
/// </summary>
private async Task CleanupTestFeedbackByTitleAsync(string titlePrefix)
{
    await using var conn = new SqlConnection(_connectionString);
    await conn.OpenAsync();

    // 先刪除子資料表（FeedbackReply），再刪除主資料表（Feedback）
    // 避免因外鍵約束造成刪除失敗
    const string deleteReplySql = @"
        DELETE fr FROM FeedbackReply fr
        INNER JOIN Feedback f ON fr.FeedbackId = f.Id
        WHERE f.Title LIKE @TitlePrefix + '%'";

    await using var deleteReplyCmd = new SqlCommand(deleteReplySql, conn);
    deleteReplyCmd.Parameters.AddWithValue("@TitlePrefix", titlePrefix);
    await deleteReplyCmd.ExecuteNonQueryAsync();

    const string deleteFeedbackSql = "DELETE FROM Feedback WHERE Title LIKE @TitlePrefix + '%'";
    await using var deleteFeedbackCmd = new SqlCommand(deleteFeedbackSql, conn);
    deleteFeedbackCmd.Parameters.AddWithValue("@TitlePrefix", titlePrefix);
    await deleteFeedbackCmd.ExecuteNonQueryAsync();
}
```

---

## 瀏覽器設定與 Playwright 選項

### 覆寫 PageTest 預設瀏覽器

Playwright 預設使用 Chromium。可透過環境變數或覆寫 `BrowserType` 屬性切換瀏覽器：

```bash
# 使用環境變數切換（執行測試前設定）
$env:BROWSER = "firefox"   # 或 "webkit"
dotnet test
```

### 覆寫 BrowserContext 選項（例如忽略 HTTPS 憑證）

本機開發環境通常使用自簽憑證，需在 `ContextOptions` 中設定忽略：

```csharp
/// <summary>
/// 覆寫 PageTest 的 ContextOptions，忽略本機開發環境的 HTTPS 自簽憑證錯誤。
/// 在 CI 環境中若使用正式憑證，則此設定不影響測試。
/// </summary>
public override BrowserNewContextOptions ContextOptions()
{
    return new BrowserNewContextOptions
    {
        // 忽略 HTTPS 憑證錯誤（適用本機 dotnet dev-cert）
        IgnoreHTTPSErrors = true,
        // 設定一致的視窗大小，避免 RWD 導致版面差異影響選取
        ViewportSize = new ViewportSize { Width = 1280, Height = 720 },
        // 設定語系，避免語言偵測影響 UI 文字比對
        Locale = "zh-TW"
    };
}
```

---

## 命名慣例

| 元素 | 命名規則 | 範例 |
|------|---------|------|
| 測試類別 | `{功能模組}E2ETests` | `FeedbackE2ETests` |
| 測試方法 | `{頁面或操作}_{情境}_{預期結果}` | `FeedbackCreate_FillAndSubmitForm_RedirectsToIndex` |
| 測試資料前綴 | `[E2E_TEST]` + GUID | `[E2E_TEST] a1b2c3d4...` |
| Teardown 輔助方法 | `CleanupXxx` | `CleanupTestFeedbackByTitleAsync` |

---

## 輸出格式要求

產生程式碼時：

1. 先列出完整 `using` 區塊（包含 `Microsoft.Playwright.NUnit`、`Microsoft.Data.SqlClient`、`Microsoft.Extensions.Configuration`）
2. 宣告 `namespace` 與 `[TestFixture]` 類別，繼承 `PageTest`
3. 在類別頂部說明「本測試涵蓋的使用者操作流程」
4. `[OneTimeSetUp]` 負責讀取 `appsettings.json` 取得 DB 連線字串
5. 覆寫 `ContextOptions()` 加入 `IgnoreHTTPSErrors = true`
6. 每個測試方法的四段式結構：`// Arrange` / `// Act` / `// Assert` / `// Teardown`（若有寫入）
7. 寫入操作以 `try/finally` 包覆，確保 Teardown 一定執行
8. Locator 優先使用 `GetByRole`、`GetByLabel`、`GetByPlaceholder`
9. 關鍵行加上行內繁體中文註解，說明驗證意圖

---

## 執行測試指令

```bash
# 執行所有 E2E Test
dotnet test FeedbackSystem.E2ETests/FeedbackSystem.E2ETests.csproj

# 執行特定測試類別
dotnet test --filter "FullyQualifiedName~FeedbackE2ETests"

# 執行時顯示詳細輸出（方便排查失敗）
dotnet test --logger "console;verbosity=detailed"

# 有頭瀏覽器模式（可視化偵錯，預設為 Headless）
$env:HEADED = "1"; dotnet test

# 指定 Base URL（CI 環境）
$env:E2E_BASE_URL = "https://staging.example.com"; dotnet test
```

---

## 快速參考

| 主題 | 說明 |
|------|------|
| Playwright .NET 官方文件 | https://playwright.dev/dotnet/docs/intro |
| PageTest 基底類別 | 繼承後自動取得 `Page`、`Browser`、`BrowserContext`，無需手動管理生命週期 |
| `Expect(locator)` 斷言 | 內建自動 retry（預設 5 秒），避免時序問題造成的 flaky test |
| `WaitForLoadStateAsync` | `NetworkIdle`（等待網路靜止）或 `DOMContentLoaded`（DOM 載入完成） |
| DB 連線字串來源 | `appsettings.json` → `ConnectionStrings:FeedbackDb` → Server=`ymmistest`，Database=`Feedback_Test` |
| 瀏覽器安裝 | `pwsh bin/Debug/net10.0/playwright.ps1 install chromium` |
