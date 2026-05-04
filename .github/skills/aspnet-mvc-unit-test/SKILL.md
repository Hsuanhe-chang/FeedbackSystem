---
name: aspnet-mvc-unit-test
description: "Use when writing unit tests for ASP.NET Core MVC projects. Covers Controller Action testing (ViewResult, RedirectToAction, ModelState), Service business logic testing (boundary conditions, exception handling), and ViewModel DataAnnotation validation. Uses xUnit + NSubstitute. Triggers: unit test, write test, test controller, test service, test viewmodel, NSubstitute mock, xUnit."
argument-hint: "Specify what to test: e.g. 'FeedbackController.Create' or 'FeedbackService.InsertFeedbackAsync'"
---

# ASP.NET Core MVC Unit Test Skill

## 目的
為 ASP.NET Core MVC (.NET 10) 專案快速、正確地產生高品質 Unit Test，
涵蓋 Controller、Service、ViewModel 三個層次，
遵循 **xUnit + NSubstitute** 組合，符合 AAA（Arrange / Act / Assert）模式。

> **測試層次邊界**
> - **Unit Test（本 Skill 範疇）**：測試單一類別的邏輯，以 Mock 隔離所有外部依賴（DB、HTTP、第三方 API）
> - **Integration Test（不在本 Skill 範疇）**：測試 Controller → Service → SP → 真實 DB 的完整鏈路（請使用 `aspnet-mvc-integration-test`）
> - **E2E Test（不在本 Skill 範疇）**：透過真實瀏覽器模擬使用者操作流程（請使用 `aspnet-mvc-e2e-test`）

---

## 前置確認清單

在開始撰寫測試前，依序確認以下事項：

### 步驟 1：確認測試專案是否存在
- 搜尋 `.sln` 同層是否有 `*.Tests.csproj` 或 `*.UnitTests.csproj`
- **若不存在**，提示使用者執行下方指令建立並加入解決方案：
  ```bash
  dotnet new xunit -n FeedbackSystem.Tests -o FeedbackSystem.Tests
  dotnet sln add FeedbackSystem.Tests/FeedbackSystem.Tests.csproj
  ```

### 步驟 2：確認 NuGet 套件安裝
在 `*.Tests.csproj` 中必須包含：
```xml
<PackageReference Include="xunit" Version="2.*" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
<PackageReference Include="NSubstitute" Version="5.*" />
<PackageReference Include="NSubstitute.Analyzers.CSharp" Version="1.*" />  <!-- 編譯期錯誤偵測 -->
<PackageReference Include="FluentAssertions" Version="6.*" />  <!-- 選用，提升可讀性 -->
```
若缺少任何套件，告知使用者執行對應的 `dotnet add package` 指令。

### 步驟 3：識別測試對象
讀取目標 `.cs` 檔，確認：
- **Controller**：注入的介面（`IXxxService`）、每個 Action 的回傳型別與路由
- **Service**：是否依賴 `IXxxRepository`（若直接依賴 `SqlConnection`，需先完成 Repository 抽象化）
  - 可測試部分：迴圈邏輯、條件判斷、資料組合、例外傳遞
  - 不可測試部分（需移至 Repository）：任何 `SqlCommand`、`SqlConnection`、SP 呼叫
- **ViewModel**：`[Required]`、`[MaxLength]`、`[Range]` 等 DataAnnotation 規則

---

## 測試撰寫程序

### Controller Action 測試
遵循 [controller-test-template.md](./references/controller-test-template.md)

重點規則：
1. 以 `NSubstitute.Substitute.For<IXxxService>()` 建立 Mock
2. 使用 `controller.ModelState.AddModelError(...)` 模擬驗證失敗
3. 必測 Happy Path（正常流程）與 Sad Path（ModelState invalid / service 拋出例外）
4. Assert 重點：`ViewResult`、`RedirectToActionResult`、`ViewData`、`ModelState.IsValid`

### Service 商業邏輯測試
遵循 [service-test-template.md](./references/service-test-template.md)

重點規則：
1. Service 必須依賴 `IFeedbackRepository` 介面，**不可直接持有 SqlConnection**
2. 以 `NSubstitute.Substitute.For<IFeedbackRepository>()` Mock 所有 DB 操作
3. 測試 Service 的商業邏輯：重試迴圈、條件判斷、資料轉換、例外是否向上傳遞
4. **SP 呼叫、SQL 執行結果 → 不屬於 Unit Test，改寫至 Integration Test**

### ViewModel DataAnnotation 驗證測試
遵循 [viewmodel-test-template.md](./references/viewmodel-test-template.md)

重點規則：
1. 使用 `System.ComponentModel.DataAnnotations.Validator.TryValidateObject(...)` 進行驗證
2. 每個帶有 Annotation 的屬性至少覆蓋一個合法值、一個非法值
3. 驗證 `ValidationResult.MemberNames` 確認錯誤指向正確欄位

---

## 命名慣例

| 元素 | 命名規則 | 範例 |
|------|---------|------|
| 測試類別 | `{被測類別}Tests` | `FeedbackControllerTests` |
| 測試方法 | `{方法名稱}_{情境}_{預期結果}` | `Create_WhenModelStateIsValid_ReturnsRedirect` |
| Mock 變數 | `_mock{介面名稱}` | `_mockFeedbackService` |
| SUT 變數 | `_sut` 或 `_{類別名稱Camel}` | `_sut` |

---

## 程式碼品質要求

- 每個測試方法必須標記 `[Fact]` 或 `[Theory]`
- `[Theory]` + `[InlineData]` 用於邊界值測試（避免重複測試方法）
- 使用 `// Arrange / // Act / // Assert` 三段式註解區隔
- 每段 Assert 只驗證一個行為（避免 Over-specification）
- Mock 的 `Received()` 驗證僅用於確認「副作用」，不過度驗證所有呼叫

---

## 輸出格式

產生程式碼時：
1. 先列出完整的 `using` 區塊
2. 宣告 `namespace` 與 `public class XxxTests`
3. 在類別頂部集中宣告所有 Mock 與 SUT 欄位
4. 使用 `public XxxTests()` 建構子初始化（或 `IClassFixture<>` 如需跨測試共享狀態）
5. 每個測試方法附上 `/// <summary>` 說明測試意圖

---

## 快速參考

- Controller 測試範本：[controller-test-template.md](./references/controller-test-template.md)
- Service 測試範本：[service-test-template.md](./references/service-test-template.md)
- ViewModel 驗證測試範本：[viewmodel-test-template.md](./references/viewmodel-test-template.md)
