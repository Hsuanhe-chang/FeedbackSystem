using FeedbackSystem.Controllers;
using FeedbackSystem.Models.ViewModels;
using FeedbackSystem.Services;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FeedbackSystem.Tests.Controllers;

/// <summary>
/// FeedbackController 的 Unit Test。
/// 以 NSubstitute Mock IFeedbackService，完全隔離 DB 依賴。
/// 涵蓋所有 Action 的 Happy Path、Sad Path 與邊界條件。
/// </summary>
public class FeedbackControllerTests
{
    // ──────────────────────────────────────────
    // 欄位宣告：Mock 物件與被測試系統（SUT）
    // ──────────────────────────────────────────

    // Mock IFeedbackService，NSubstitute 自動替換所有介面方法
    private readonly IFeedbackService _mockFeedbackService;

    // 被測試的 Controller 實體（System Under Test）
    private readonly FeedbackController _sut;

    // ──────────────────────────────────────────
    // 建構子：每個測試方法執行前初始化
    // ──────────────────────────────────────────
    public FeedbackControllerTests()
    {
        // 建立 NSubstitute Mock，所有介面方法預設回傳 default 值
        _mockFeedbackService = Substitute.For<IFeedbackService>();

        // 注入 Mock，建立 Controller SUT
        _sut = new FeedbackController(_mockFeedbackService);
    }

    // ══════════════════════════════════════════
    // Index — 後台列表頁
    // ══════════════════════════════════════════

    /// <summary>
    /// Happy Path：Index 應呼叫 Service 取得分頁資料並回傳 ViewResult
    /// </summary>
    [Fact]
    public async Task Index_ReturnsViewWithItems()
    {
        // Arrange：設定 Mock 回傳假資料（2 筆、總計 2 筆）
        var fakeItems = new List<FeedbackListItemViewModel>
        {
            new() { FeedbackId = 1, CustomerName = "客戶A", TrackingCode = "FB001" },
            new() { FeedbackId = 2, CustomerName = "客戶B", TrackingCode = "FB002" }
        };
        _mockFeedbackService
            .GetPagedListAsync(null, null, null, 1, 10)
            .Returns((fakeItems.AsEnumerable(), 2));

        // Act：呼叫 GET Index，不傳篩選條件
        var result = await _sut.Index(null, null, null, 1);

        // Assert 1：必須回傳 ViewResult
        var viewResult = Assert.IsType<ViewResult>(result);

        // Assert 2：Model 應為 IEnumerable<FeedbackListItemViewModel>
        Assert.IsAssignableFrom<IEnumerable<FeedbackListItemViewModel>>(viewResult.Model);
    }

    // ══════════════════════════════════════════
    // Create — GET：前台新增表單
    // ══════════════════════════════════════════

    /// <summary>
    /// Happy Path：GET Create 應呼叫 Service 產生 TrackingCode，並回傳帶有 TrackingCode 的 ViewResult
    /// </summary>
    [Fact]
    public async Task Create_Get_ReturnsViewWithTrackingCode()
    {
        // Arrange：設定 Mock 的 TrackingCode 產生方法
        const string fakeCode = "FB20260504ABCDEF";
        _mockFeedbackService
            .GenerateUniqueTrackingCodeAsync()
            .Returns(fakeCode);

        // Act
        var result = await _sut.Create();

        // Assert 1：必須回傳 ViewResult
        var viewResult = Assert.IsType<ViewResult>(result);

        // Assert 2：Model 必須是 FeedbackCreateViewModel 且 TrackingCode 已帶入
        var model = Assert.IsType<FeedbackCreateViewModel>(viewResult.Model);
        Assert.Equal(fakeCode, model.TrackingCode);
    }

    // ══════════════════════════════════════════
    // Create — POST：儲存新增意見
    // ══════════════════════════════════════════

    /// <summary>
    /// Happy Path：ModelState 合法且 TrackingCode 不重複時，
    /// 應呼叫 InsertFeedbackAsync 並 Redirect 至 Detail 頁
    /// </summary>
    [Fact]
    public async Task Create_Post_WhenValidModel_InsertsAndRedirectsToDetail()
    {
        // Arrange：建立合法的提交 ViewModel
        var model = new FeedbackCreateViewModel
        {
            TrackingCode  = "FB20260504ABCDEF",
            CustomerName  = "測試客戶",
            CustomerEmail = "test@example.com",
            Category      = "產品",
            Subject       = "測試主旨",
            Content       = "測試內容"
        };

        // 設定 Mock：TrackingCode 不重複（回傳 false = 不存在）
        _mockFeedbackService
            .CheckTrackingCodeExistsAsync(model.TrackingCode)
            .Returns(false);

        // 設定 Mock：新增成功後回傳 FeedbackId = 99
        _mockFeedbackService
            .InsertFeedbackAsync(model)
            .Returns(99);

        // Act
        var result = await _sut.Create(model);

        // Assert 1：應重導向至 Detail 頁
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Detail", redirect.ActionName);

        // Assert 2：路由參數 id 應為新插入的 FeedbackId
        Assert.Equal(99, redirect.RouteValues!["id"]);

        // Assert 3：確認 InsertFeedbackAsync 確實被呼叫過一次（驗證副作用）
        _mockFeedbackService.Received(1).InsertFeedbackAsync(model);
    }

    /// <summary>
    /// Sad Path：ModelState 驗證失敗時，應重新顯示表單，且不呼叫 InsertFeedbackAsync
    /// </summary>
    [Fact]
    public async Task Create_Post_WhenModelStateInvalid_ReturnsViewWithModel()
    {
        // Arrange：加入 ModelState 錯誤，模擬 DataAnnotation 驗證失敗
        _sut.ModelState.AddModelError("CustomerName", "客戶姓名為必填");
        var model = new FeedbackCreateViewModel
        {
            TrackingCode = "FB20260504ABCDEF"
            // CustomerName 故意留空，觸發驗證失敗
        };

        // Act
        var result = await _sut.Create(model);

        // Assert 1：應重新顯示表單（回傳 ViewResult）
        var viewResult = Assert.IsType<ViewResult>(result);

        // Assert 2：Model 應為原本傳入的物件（保留使用者輸入）
        Assert.Equal(model, viewResult.Model);

        // Assert 3：驗證失敗應提前返回，不應呼叫 InsertFeedbackAsync
        _mockFeedbackService.DidNotReceive().InsertFeedbackAsync(Arg.Any<FeedbackCreateViewModel>());
    }

    /// <summary>
    /// Sad Path：TrackingCode 已重複時，應重新產生代碼並回傳表單（含錯誤訊息）
    /// </summary>
    [Fact]
    public async Task Create_Post_WhenTrackingCodeDuplicated_RegeneratesCodeAndReturnsView()
    {
        // Arrange：設定合法 Model
        var model = new FeedbackCreateViewModel
        {
            TrackingCode  = "FB20260504ABCDEF",   // 原始代碼（將被判定重複）
            CustomerName  = "測試客戶",
            CustomerEmail = "test@example.com",
            Category      = "產品",
            Subject       = "主旨",
            Content       = "內容"
        };

        // 設定 Mock：原始 TrackingCode 已存在（回傳 true = 重複）
        _mockFeedbackService
            .CheckTrackingCodeExistsAsync(model.TrackingCode)
            .Returns(true);

        // 設定 Mock：重新產生唯一代碼
        const string newCode = "FB20260504XYZABC";
        _mockFeedbackService
            .GenerateUniqueTrackingCodeAsync()
            .Returns(newCode);

        // Act
        var result = await _sut.Create(model);

        // Assert 1：應回傳 ViewResult（重新顯示表單讓使用者確認）
        Assert.IsType<ViewResult>(result);

        // Assert 2：model.TrackingCode 已被替換為新代碼
        Assert.Equal(newCode, model.TrackingCode);

        // Assert 3：不應呼叫 InsertFeedbackAsync（尚未儲存）
        _mockFeedbackService.DidNotReceive().InsertFeedbackAsync(Arg.Any<FeedbackCreateViewModel>());
    }

    // ══════════════════════════════════════════
    // Detail — 意見詳情頁
    // ══════════════════════════════════════════

    /// <summary>
    /// Happy Path：找到指定 id 的資料時，應回傳包含回覆串與新增回覆表單的 ViewResult
    /// </summary>
    [Fact]
    public async Task Detail_WhenFeedbackExists_ReturnsViewWithDetailAndReplies()
    {
        // Arrange：準備假的詳情資料
        const int targetId = 5;
        var fakeDetail = new FeedbackDetailViewModel
        {
            FeedbackId   = targetId,
            CustomerName = "測試客戶",
            TrackingCode = "FB20260504ABCDEF"
        };
        var fakeReplies = new List<FeedbackReplyViewModel>
        {
            new() { ReplyId = 1, Content = "回覆1", ReplierName = "客服" }
        };

        // 設定 Mock：GetByIdAsync 回傳假詳情
        _mockFeedbackService.GetByIdAsync(targetId).Returns(fakeDetail);

        // 設定 Mock：GetRepliesByFeedbackIdAsync 回傳假回覆串
        _mockFeedbackService
            .GetRepliesByFeedbackIdAsync(targetId)
            .Returns(fakeReplies.AsEnumerable());

        // Act
        var result = await _sut.Detail(targetId);

        // Assert 1：應回傳 ViewResult
        var viewResult = Assert.IsType<ViewResult>(result);

        // Assert 2：Model 應包含正確的 FeedbackId
        var model = Assert.IsType<FeedbackDetailViewModel>(viewResult.Model);
        Assert.Equal(targetId, model.FeedbackId);

        // Assert 3：Replies 清單應被帶入
        Assert.Single(model.Replies);

        // Assert 4：NewReply 初始值應帶入 FeedbackId 與預設 ReplyType
        Assert.Equal(targetId, model.NewReply.FeedbackId);
        Assert.Equal(1, model.NewReply.ReplyType);  // 預設官方回覆
    }

    /// <summary>
    /// Sad Path：找不到指定 id 的資料時，應回傳 404 NotFound
    /// </summary>
    [Fact]
    public async Task Detail_WhenFeedbackNotFound_ReturnsNotFound()
    {
        // Arrange：設定 Mock 回傳 null（資料不存在）
        _mockFeedbackService.GetByIdAsync(999).Returns((FeedbackDetailViewModel?)null);

        // Act
        var result = await _sut.Detail(999);

        // Assert：應回傳 NotFoundResult（HTTP 404）
        Assert.IsType<NotFoundResult>(result);
    }

    // ══════════════════════════════════════════
    // Edit — GET：後台編輯表單
    // ══════════════════════════════════════════

    /// <summary>
    /// Happy Path：找到資料時，應將 Detail ViewModel 轉換為 Edit ViewModel 並回傳表單
    /// </summary>
    [Fact]
    public async Task Edit_Get_WhenFeedbackExists_ReturnsViewWithEditModel()
    {
        // Arrange：建立假的詳情資料（編輯頁面的資料來源）
        const int targetId = 3;
        var fakeDetail = new FeedbackDetailViewModel
        {
            FeedbackId    = targetId,
            TrackingCode  = "FB20260504ABCDEF",
            CustomerName  = "測試客戶",
            CustomerEmail = "test@example.com",
            Category      = "產品",
            Subject       = "主旨",
            Content       = "內容",
            Status        = 0,
            Priority      = 1,
            CreatedAt     = new DateTime(2026, 5, 4)
        };
        _mockFeedbackService.GetByIdAsync(targetId).Returns(fakeDetail);

        // Act
        var result = await _sut.Edit(targetId);

        // Assert 1：應回傳 ViewResult
        var viewResult = Assert.IsType<ViewResult>(result);

        // Assert 2：Model 應為 FeedbackEditViewModel 且 FeedbackId 正確
        var model = Assert.IsType<FeedbackEditViewModel>(viewResult.Model);
        Assert.Equal(targetId, model.FeedbackId);
        Assert.Equal("產品", model.Category);
    }

    /// <summary>
    /// Sad Path：找不到資料時，GET Edit 應回傳 404
    /// </summary>
    [Fact]
    public async Task Edit_Get_WhenFeedbackNotFound_ReturnsNotFound()
    {
        // Arrange：設定 Mock 回傳 null
        _mockFeedbackService.GetByIdAsync(999).Returns((FeedbackDetailViewModel?)null);

        // Act
        var result = await _sut.Edit(999);

        // Assert：應回傳 404
        Assert.IsType<NotFoundResult>(result);
    }

    // ══════════════════════════════════════════
    // Edit — POST：儲存編輯結果
    // ══════════════════════════════════════════

    /// <summary>
    /// Happy Path：ModelState 合法且路由 id 與表單 id 一致時，應呼叫 Service 並 Redirect
    /// </summary>
    [Fact]
    public async Task Edit_Post_WhenValidModel_UpdatesAndRedirectsToDetail()
    {
        // Arrange：建立合法的編輯 ViewModel
        const int feedbackId = 7;
        var model = new FeedbackEditViewModel
        {
            FeedbackId = feedbackId,
            Category   = "服務",
            Subject    = "修改後的主旨",
            Content    = "修改後的內容",
            Status     = 1,
            Priority   = 2
        };

        // 設定 Mock：UpdateFeedbackAsync 為 void Task，預設不做任何事
        _mockFeedbackService.UpdateFeedbackAsync(model).Returns(Task.CompletedTask);

        // Act：id 與 model.FeedbackId 相同（合法請求）
        var result = await _sut.Edit(feedbackId, model);

        // Assert 1：應重導向至 Detail 頁
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Detail", redirect.ActionName);

        // Assert 2：路由參數 id 應為編輯的 FeedbackId
        Assert.Equal(feedbackId, redirect.RouteValues!["id"]);

        // Assert 3：確認 UpdateFeedbackAsync 被呼叫一次
        _mockFeedbackService.Received(1).UpdateFeedbackAsync(model);
    }

    /// <summary>
    /// Sad Path：路由 id 與表單 FeedbackId 不一致時，應回傳 400 BadRequest（防止偽造請求）
    /// </summary>
    [Fact]
    public async Task Edit_Post_WhenIdMismatch_ReturnsBadRequest()
    {
        // Arrange：路由 id = 1，但 model.FeedbackId = 99（不一致）
        var model = new FeedbackEditViewModel
        {
            FeedbackId = 99,    // 與路由 id 不同
            Category   = "產品",
            Subject    = "主旨",
            Content    = "內容",
            Status     = 0,
            Priority   = 1
        };

        // Act：路由 id = 1，但 model.FeedbackId = 99
        var result = await _sut.Edit(1, model);

        // Assert：應回傳 400 BadRequest（偽造請求防護）
        Assert.IsType<BadRequestResult>(result);
    }

    /// <summary>
    /// Sad Path：ModelState 驗證失敗時，應重新顯示表單，不呼叫 UpdateFeedbackAsync
    /// </summary>
    [Fact]
    public async Task Edit_Post_WhenModelStateInvalid_ReturnsViewWithModel()
    {
        // Arrange：加入 ModelState 錯誤，模擬欄位驗證失敗
        _sut.ModelState.AddModelError("Category", "請選擇意見類別");
        const int feedbackId = 5;
        var model = new FeedbackEditViewModel
        {
            FeedbackId = feedbackId,
            Category   = string.Empty,  // 空值觸發驗證失敗
            Subject    = "主旨",
            Content    = "內容",
            Status     = 0,
            Priority   = 1
        };

        // Act
        var result = await _sut.Edit(feedbackId, model);

        // Assert 1：應回傳 ViewResult 重新顯示表單
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal(model, viewResult.Model);

        // Assert 2：驗證失敗時不應呼叫 UpdateFeedbackAsync
        _mockFeedbackService.DidNotReceive().UpdateFeedbackAsync(Arg.Any<FeedbackEditViewModel>());
    }

    // ══════════════════════════════════════════
    // AddReply — POST：新增回覆
    // ══════════════════════════════════════════

    /// <summary>
    /// Happy Path：ModelState 合法時，應呼叫 InsertReplyAsync 並 Redirect 至 Detail 頁
    /// </summary>
    [Fact]
    public async Task AddReply_Post_WhenValidModel_InsertsAndRedirectsToDetail()
    {
        // Arrange：建立合法的回覆 ViewModel
        var model = new FeedbackReplyCreateViewModel
        {
            FeedbackId  = 10,
            Content     = "這是回覆內容",
            ReplierName = "客服人員",
            ReplyType   = 1,
            IsPublic    = true
        };

        // 設定 Mock：InsertReplyAsync 為 void Task
        _mockFeedbackService.InsertReplyAsync(model).Returns(Task.CompletedTask);

        // Act：使用 [Bind(Prefix = "NewReply")] 的 AddReply Action
        var result = await _sut.AddReply(model);

        // Assert 1：應重導向至 Detail 頁
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Detail", redirect.ActionName);

        // Assert 2：路由參數 id 應為 FeedbackId
        Assert.Equal(10, redirect.RouteValues!["id"]);

        // Assert 3：確認 InsertReplyAsync 被呼叫一次
        _mockFeedbackService.Received(1).InsertReplyAsync(model);
    }

    /// <summary>
    /// Sad Path：ModelState 驗證失敗時，應重新載入詳情頁（含回覆串）
    /// </summary>
    [Fact]
    public async Task AddReply_Post_WhenModelStateInvalid_ReloadsDetailView()
    {
        // Arrange：加入 ModelState 錯誤（回覆內容為空）
        _sut.ModelState.AddModelError("Content", "回覆內容為必填");
        var model = new FeedbackReplyCreateViewModel
        {
            FeedbackId  = 10,
            Content     = string.Empty, // 空值觸發錯誤
            ReplierName = "客服",
            ReplyType   = 1,
            IsPublic    = true
        };

        // 設定 Mock：重新載入詳情頁所需的資料
        var fakeDetail = new FeedbackDetailViewModel { FeedbackId = 10 };
        _mockFeedbackService.GetByIdAsync(10).Returns(fakeDetail);
        _mockFeedbackService
            .GetRepliesByFeedbackIdAsync(10)
            .Returns(Enumerable.Empty<FeedbackReplyViewModel>());

        // Act
        var result = await _sut.AddReply(model);

        // Assert 1：應回傳 ViewResult 顯示 Detail 頁（而非 Redirect）
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Detail", viewResult.ViewName);

        // Assert 2：不應呼叫 InsertReplyAsync（驗證失敗不應儲存）
        _mockFeedbackService.DidNotReceive().InsertReplyAsync(Arg.Any<FeedbackReplyCreateViewModel>());
    }

    /// <summary>
    /// Sad Path：AddReply 驗證失敗且找不到主意見時，應回傳 404
    /// </summary>
    [Fact]
    public async Task AddReply_Post_WhenModelStateInvalidAndFeedbackNotFound_ReturnsNotFound()
    {
        // Arrange：加入 ModelState 錯誤
        _sut.ModelState.AddModelError("Content", "回覆內容為必填");
        var model = new FeedbackReplyCreateViewModel
        {
            FeedbackId = 999,   // 不存在的 FeedbackId
            Content    = string.Empty,
            ReplierName = "客服",
            ReplyType  = 1
        };

        // 設定 Mock：主意見不存在
        _mockFeedbackService.GetByIdAsync(999).Returns((FeedbackDetailViewModel?)null);

        // Act
        var result = await _sut.AddReply(model);

        // Assert：主意見不存在時應回傳 404
        Assert.IsType<NotFoundResult>(result);
    }
}
