# Controller Action 測試範本（xUnit + NSubstitute）

## 完整範本

```csharp
using FeedbackSystem.Controllers;
using FeedbackSystem.Models.ViewModels;
using FeedbackSystem.Services;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace FeedbackSystem.Tests.Controllers;

/// <summary>
/// FeedbackController 單元測試
/// 使用 NSubstitute Mock IFeedbackService，隔離資料庫依賴
/// </summary>
public class FeedbackControllerTests
{
    // ──────────────────────────────────────────
    // 欄位宣告：Mock 物件與被測試系統（SUT）
    // ──────────────────────────────────────────

    // Mock IFeedbackService，避免實際呼叫資料庫
    private readonly IFeedbackService _mockFeedbackService;

    // 被測試的 Controller 實體（System Under Test）
    private readonly FeedbackController _sut;

    // ──────────────────────────────────────────
    // 建構子：在每個測試方法執行前初始化
    // ──────────────────────────────────────────
    public FeedbackControllerTests()
    {
        // 建立 NSubstitute 的替代物件（自動 Mock 所有介面方法）
        _mockFeedbackService = Substitute.For<IFeedbackService>();

        // 注入 Mock，建立 SUT
        _sut = new FeedbackController(_mockFeedbackService);
    }

    // ══════════════════════════════════════════
    // GET Create — 顯示新增表單
    // ══════════════════════════════════════════

    /// <summary>
    /// 正常情境：GET Create 應回傳 ViewResult，且 Model 為空的 FeedbackCreateViewModel
    /// </summary>
    [Fact]
    public async Task Create_Get_ReturnsViewWithEmptyModel()
    {
        // Arrange：不需要特殊設定，直接呼叫

        // Act：呼叫 GET Create Action
        var result = await _sut.Create();

        // Assert：必須回傳 ViewResult
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.IsType<FeedbackCreateViewModel>(viewResult.Model);
    }

    // ══════════════════════════════════════════
    // POST Create — 新增意見回饋
    // ══════════════════════════════════════════

    /// <summary>
    /// Happy Path：ModelState 合法時，應呼叫 Service 並導向詳情頁
    /// </summary>
    [Fact]
    public async Task Create_Post_WhenModelStateIsValid_CallsServiceAndRedirects()
    {
        // Arrange：建立合法的 ViewModel
        var model = new FeedbackCreateViewModel
        {
            CustomerName = "測試客戶",
            Email = "test@example.com",
            Category = "產品",
            Subject = "測試主旨",
            Content = "測試內容"
        };

        // 設定 Mock：InsertFeedbackAsync 回傳假 Id=1
        _mockFeedbackService
            .InsertFeedbackAsync(model)
            .Returns(Task.FromResult(1));

        // 設定 Mock：GenerateUniqueTrackingCodeAsync 回傳假追蹤碼
        _mockFeedbackService
            .GenerateUniqueTrackingCodeAsync()
            .Returns(Task.FromResult("FB20260504ABCDEF"));

        // Act：呼叫 POST Create Action
        var result = await _sut.Create(model);

        // Assert 1：必須重導向至 Detail 頁
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Detail", redirect.ActionName);

        // Assert 2：確認 Service 有被呼叫一次（驗證副作用）
        // NSubstitute 的 Received() 驗證是同步操作，不需要 await
        _mockFeedbackService.Received(1).InsertFeedbackAsync(model);
    }

    /// <summary>
    /// Sad Path：ModelState 無效時，應重新顯示表單（不呼叫 Service）
    /// </summary>
    [Fact]
    public async Task Create_Post_WhenModelStateIsInvalid_ReturnsViewWithModel()
    {
        // Arrange：加入 ModelState 錯誤，模擬驗證失敗
        _sut.ModelState.AddModelError("CustomerName", "客戶名稱為必填");
        var model = new FeedbackCreateViewModel();

        // Act
        var result = await _sut.Create(model);

        // Assert 1：必須回傳同一 View（表單重新顯示）
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal(model, viewResult.Model);

        // Assert 2：Service 不應被呼叫（因為驗證失敗就要提前返回）
        // NSubstitute 的 DidNotReceive() 驗證是同步操作，不需要 await
        _mockFeedbackService.DidNotReceive().InsertFeedbackAsync(Arg.Any<FeedbackCreateViewModel>());
    }

    /// <summary>
    /// Sad Path：Service 拋出例外時，Controller 應妥善處理（例如回傳錯誤頁或重新顯示表單）
    /// </summary>
    [Fact]
    public async Task Create_Post_WhenServiceThrows_HandlesException()
    {
        // Arrange
        var model = new FeedbackCreateViewModel
        {
            CustomerName = "測試",
            Email = "test@example.com",
            Category = "服務",
            Subject = "主旨",
            Content = "內容"
        };

        // 設定 Mock：InsertFeedbackAsync 拋出例外
        _mockFeedbackService
            .InsertFeedbackAsync(model)
            .ThrowsAsync(new InvalidOperationException("資料庫連線失敗"));

        // Act & Assert：視 Controller 錯誤處理策略調整
        // 若 Controller 會 re-throw → 使用 await Assert.ThrowsAsync<...>
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.Create(model));
    }
}
```

---

## 常用 NSubstitute 語法速查

| 情境 | 語法 |
|------|------|
| Mock 方法回傳值 | `mock.MethodAsync(arg).Returns(Task.FromResult(value))` |
| Mock 拋出例外 | `mock.MethodAsync(arg).ThrowsAsync(new Exception())` |
| 驗證方法被呼叫 N 次 | `mock.Received(n).MethodAsync(...)` （同步，勿加 await） |
| 驗證方法未被呼叫 | `mock.DidNotReceive().MethodAsync(...)` （同步，勿加 await） |
| 任意參數 Matcher | `Arg.Any<T>()` |
| 條件 Matcher | `Arg.Is<T>(x => x.Id == 1)` |
| 回調副作用 | `mock.Method(Arg.Any<T>()).Returns(x => { /* side effect */ return value; })` |

---

## ViewResult 常見斷言

```csharp
// 確認回傳 ViewResult
var viewResult = Assert.IsType<ViewResult>(result);

// 確認使用特定 View 名稱（空字串 = 預設）
Assert.Equal("Index", viewResult.ViewName);

// 確認 Model 型別與值
var model = Assert.IsType<FeedbackCreateViewModel>(viewResult.Model);
Assert.Equal("預期值", model.CustomerName);

// 確認 ViewData
Assert.Equal("預期標題", viewResult.ViewData["Title"]);

// 確認重導向目標
var redirect = Assert.IsType<RedirectToActionResult>(result);
Assert.Equal("Detail", redirect.ActionName);
Assert.Equal("Feedback", redirect.ControllerName);
Assert.Equal(1, redirect.RouteValues!["id"]);
```
