---
name: aspnet-mvc-integration-test
description: "Use when writing integration tests for ASP.NET Core MVC projects. Covers Controller→Service→DB full chain testing, Stored Procedure (SP) call verification using real test database (reads ConnectionStrings from appsettings.json), HTTP API endpoint testing via WebApplicationFactory, and multi-component interaction tests. Triggers: integration test, write integration test, test SP, test stored procedure, test API endpoint, test full chain, WebApplicationFactory, test database, test with real DB. NOT for E2E/browser tests (Playwright, Selenium)."
argument-hint: "Specify what to test: e.g. 'FeedbackController.Index 整條鏈' or 'usp_Feedback_GetPagedList SP 呼叫'"
---

# ASP.NET Core MVC Integration Test Skill

## 目的

為 ASP.NET Core MVC (.NET 10) 專案產生 **Integration Test**，  
驗證「多個元件組合」與「API 端點是否正常運作」，範圍止於 HTTP 層的請求與回應驗證。

> **測試層次邊界（本 Skill 範疇一覽）**
>
> | 測試類型 | 範疇 | 依賴 | 本 Skill |
> |---------|------|------|----------|
> | Unit Test | 單一類別邏輯 | Mock 隔離所有外部依賴 | ❌ 請使用 `aspnet-mvc-unit-test` |
> | **Integration Test（本 Skill）** | Controller → Service → SP → 真實 DB | **不 Mock**，使用測試資料庫 | ✅ 涵蓋 |
> | E2E Test | 瀏覽器 UI 操作流程（Playwright / Selenium） | 真實瀏覽器 | ❌ **不在本 Skill 範疇** |
>
> **E2E 測試（Playwright、Selenium、瀏覽器自動化）請勿使用本 Skill**。  
> 本 Skill 只處理 in-process HTTP 請求（`TestServer`），不啟動真實瀏覽器。

> **本專案測試 DB**：Server=`ymmistest`，Database=`Feedback_Test`  
> 連線字串 Key：`ConnectionStrings:FeedbackDb`（讀取自 `appsettings.json`）

---

## 測試策略分層

本 Skill 支援以下三個測試層次，可依需求擇一或混合使用：

| 層次 | 說明 | 進入點 |
|------|------|--------|
| **Layer 1：Service + DB** | 直接實例化 Service，連接真實 DB，驗證 SP 呼叫結果 | [sp-test-template.md](./references/sp-test-template.md) |
| **Layer 2：HTTP 端點** | 使用 `WebApplicationFactory` 啟動完整應用程式，以 HttpClient 打 HTTP 請求 | [endpoint-test-template.md](./references/endpoint-test-template.md) |
| **Layer 3：跨層整合** | 同時驗證 HTTP 回應內容 + DB 狀態變化（例如：POST 新增後查 DB 確認） | 兩個範本組合使用 |

---

## 前置確認清單

### 步驟 1：確認測試專案是否存在

搜尋 `.sln` 同層是否有 `*.IntegrationTests.csproj` 或 `*.Tests.csproj`：

- **若不存在**，提示使用者執行以下指令建立：
  ```bash
  dotnet new xunit -n FeedbackSystem.IntegrationTests -o FeedbackSystem.IntegrationTests
  dotnet sln add FeedbackSystem.IntegrationTests/FeedbackSystem.IntegrationTests.csproj
  ```

- **若已存在 Unit Test 專案**，可在同一個測試專案內新增 `IntegrationTests/` 子資料夾，  
  但強烈建議分開，避免 Unit Test 被環境相依性污染。

### 步驟 2：確認 NuGet 套件

完整套件清單與版本說明詳見 [project-setup.md](./references/project-setup.md)。

最低必要套件如下：

```xml
<!-- 整合測試核心：提供 WebApplicationFactory / TestServer -->
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.*" />

<!-- 測試框架 -->
<PackageReference Include="xunit" Version="2.*" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.*" />

<!-- DB 存取：需與主專案版本一致 -->
<PackageReference Include="Microsoft.Data.SqlClient" Version="5.*" />

<!-- 組態讀取：從 appsettings.json 取得連線字串 -->
<PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="10.*" />
<PackageReference Include="Microsoft.Extensions.Configuration.EnvironmentVariables" Version="10.*" />

<!-- 選用：提升 Assert 可讀性 -->
<PackageReference Include="FluentAssertions" Version="6.*" />
```

另需在 `.csproj` 中加入對主專案的參考：

```xml
<ItemGroup>
  <ProjectReference Include="..\FeedbackSystem\FeedbackSystem.csproj" />
</ItemGroup>
```

### 步驟 3：識別測試目標

讀取以下檔案以了解待測範圍：

- `Controllers/FeedbackController.cs`：Action 方法與路由
- `Services/IFeedbackService.cs`：服務介面（SP 對應關係）
- `Services/FeedbackService.cs`：SP 名稱、參數、OUTPUT 參數
- `appsettings.json`：確認 `ConnectionStrings:FeedbackDb`

### 步驟 4：確認測試 DB 可連線

```csharp
// 快速連線驗證（放在測試的 Constructor 或 ClassFixture 初始化）
using var conn = new SqlConnection(connectionString);
await conn.OpenAsync(); // 若無法連線會立即拋出例外
```

---

## 撰寫程序

### 讀取連線字串的標準方式

Integration Test 不使用 Mock，必須讀取真實連線字串。有兩種方式：

**方式 A：直接讀取 appsettings.json（Service 層測試首選）**

```csharp
// 使用 ConfigurationBuilder 載入主專案的 appsettings.json
var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory()) // 測試執行目錄
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()                    // 允許 CI 環境覆寫
    .Build();

// 取得連線字串
var connectionString = config.GetConnectionString("FeedbackDb")
    ?? throw new InvalidOperationException("appsettings.json 缺少 ConnectionStrings:FeedbackDb");
```

> **重要**：需在 `.csproj` 將 `appsettings.json` 設為「Copy to Output Directory」，  
> 或在測試目錄下放置獨立的 `appsettings.json`（可覆寫測試 DB 設定）。  
> 詳細設定說明見 [project-setup.md](./references/project-setup.md)。

**方式 B：透過 WebApplicationFactory 讀取（HTTP 端點測試首選）**

```csharp
// WebApplicationFactory 會自動載入主專案的 appsettings.json
// 並透過 DI 注入到 FeedbackService
var factory = new WebApplicationFactory<Program>();
var client = factory.CreateClient();
// Service 內部的 _connectionString 已自動對應到真實 DB
```

---

## 測試撰寫規則

### SP 呼叫測試（Layer 1）

詳細範本見 [sp-test-template.md](./references/sp-test-template.md)。重點規則：

1. **直接實例化 Service**，不透過 WebApplicationFactory
2. **使用 `IClassFixture<IntegrationTestFixture>`** 共享連線字串，避免重複讀取設定檔
3. **每個測試後必須清理資料**（若測試有寫入操作），使用 `DELETE` 或呼叫刪除 SP
4. **驗證重點**：SP 回傳的資料欄位值、OUTPUT 參數、例外情況

### HTTP 端點測試（Layer 2）

詳細範本見 [endpoint-test-template.md](./references/endpoint-test-template.md)。重點規則：

1. 使用 `WebApplicationFactory<Program>` 啟動整個應用
2. `Program.cs` 必須可被測試專案存取（需使用 `partial class` 或 `InternalsVisibleTo`）
3. 驗證 **HTTP Status Code**、**Response 內容**、**Redirect 目標**
4. 對於 POST 操作，需驗證表單欄位完整性（包含 `__RequestVerificationToken` 若有啟用）

### 跨層整合測試（Layer 3）

1. 先透過 HttpClient 執行操作（POST 新增），取得回應
2. 再直接查詢 DB（透過 SqlConnection）確認資料已正確寫入
3. 測試後清理所有寫入的測試資料

---

## 測試隔離與資料清理策略

| 場景 | 建議策略 |
|------|---------|
| 只讀 SP（SELECT） | 無需清理，可直接驗證回傳資料 |
| 寫入 SP（INSERT/UPDATE） | 使用 `try/finally` 確保 `teardown` 一定執行刪除 |
| 大量資料測試 | 使用識別性前綴（如 `[TEST]`）並在 teardown 批次刪除 |
| 跨多表操作 | 依外鍵順序反向刪除，先刪子資料表再刪主資料表 |

---

## 命名慣例

| 元素 | 命名規則 | 範例 |
|------|---------|------|
| 測試類別 | `{被測元件}IntegrationTests` | `FeedbackServiceIntegrationTests` |
| HTTP 端點測試類別 | `{Controller名稱}EndpointTests` | `FeedbackEndpointTests` |
| 測試方法 | `{方法名稱}_{情境}_{預期結果}` | `GetPagedListAsync_WithValidParams_ReturnsFeedbackList` |
| Fixture 類別 | `IntegrationTestFixture` | `IntegrationTestFixture` |

---

## 輸出格式要求

產生程式碼時：

1. 先列出完整 `using` 區塊
2. 宣告 `namespace` 與測試類別
3. 在類別頂部說明「本測試對應的 SP 或 Action」
4. 使用 `IClassFixture<>` 共享昂貴的初始化資源（DB 連線設定）
5. 每個測試方法的三段式結構：`// Arrange` / `// Act` / `// Assert`
6. 寫入操作必須包含 `// Teardown` 區塊
7. 關鍵行加上行內註解，說明驗證意圖

---

## 快速參考

- 測試專案設定：[project-setup.md](./references/project-setup.md)
- SP 呼叫測試範本：[sp-test-template.md](./references/sp-test-template.md)
- HTTP 端點測試範本：[endpoint-test-template.md](./references/endpoint-test-template.md)
