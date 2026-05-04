using FeedbackSystem.Models.ViewModels;
using FeedbackSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FeedbackSystem.Controllers;

/// <summary>
/// 意見回饋 Controller
/// 負責前台（客戶新增）與後台（列表、詳情、編輯、回覆）的所有 Action
/// 所有商業邏輯委派給 IFeedbackService，Controller 僅負責路由與 ModelState 檢查
/// </summary>
public class FeedbackController : Controller
{
    // 注入的服務介面，由 DI 容器提供 FeedbackService 實作
    private readonly IFeedbackService _feedbackService;

    // 靜態 Category 下拉清單（符合 Plan 規定：產品 / 服務 / 建議 / 其他）
    private static readonly List<SelectListItem> CategorySelectList = new()
    {
        new SelectListItem { Value = "產品", Text = "產品" },
        new SelectListItem { Value = "服務", Text = "服務" },
        new SelectListItem { Value = "建議", Text = "建議" },
        new SelectListItem { Value = "其他", Text = "其他" }
    };

    // Status 下拉清單（後台管理用）
    private static readonly List<SelectListItem> StatusSelectList = new()
    {
        new SelectListItem { Value = "0", Text = "待處理" },
        new SelectListItem { Value = "1", Text = "處理中" },
        new SelectListItem { Value = "2", Text = "已回覆" },
        new SelectListItem { Value = "3", Text = "已關閉" }
    };

    // Priority 下拉清單
    private static readonly List<SelectListItem> PrioritySelectList = new()
    {
        new SelectListItem { Value = "1", Text = "一般" },
        new SelectListItem { Value = "2", Text = "重要" },
        new SelectListItem { Value = "3", Text = "緊急" }
    };

    // ReplyType 下拉清單
    private static readonly List<SelectListItem> ReplyTypeSelectList = new()
    {
        new SelectListItem { Value = "0", Text = "客戶回覆" },
        new SelectListItem { Value = "1", Text = "官方回覆" }
    };

    /// <summary>
    /// 建構子：透過 DI 注入 IFeedbackService
    /// </summary>
    public FeedbackController(IFeedbackService feedbackService)
    {
        _feedbackService = feedbackService;
    }

    // ─────────────────────────────────────────────────────────────────
    // Index：後台意見列表
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// [GET] 後台意見列表頁
    /// 支援 Status / Priority 篩選與分頁（每頁 10 筆）
    /// </summary>
    /// <param name="status">處理狀態篩選（null=全部）</param>
    /// <param name="priority">優先等級篩選（null=全部）</param>
    /// <param name="page">目前頁碼（預設第 1 頁）</param>
    [HttpGet]
    public async Task<IActionResult> Index(byte? status, byte? priority, int page = 1)
    {
        // 每頁固定顯示 10 筆
        const int pageSize = 10;

        // 呼叫 Service 取得當頁資料與總筆數
        var (items, totalCount) = await _feedbackService.GetPagedListAsync(status, priority, page, pageSize);

        // 計算總頁數（無條件進位）
        int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        // 將篩選條件、分頁資訊傳到 View（使用 ViewBag 是因為這些屬於頁面狀態，非主要 ViewModel 資料）
        ViewBag.CurrentStatus   = status;
        ViewBag.CurrentPriority = priority;
        ViewBag.CurrentPage     = page;
        ViewBag.TotalPages      = totalPages;
        ViewBag.TotalCount      = totalCount;

        // 傳入篩選用下拉選單資料
        ViewBag.StatusList   = StatusSelectList;
        ViewBag.PriorityList = PrioritySelectList;

        return View(items);
    }

    // ─────────────────────────────────────────────────────────────────
    // Create：前台新增意見
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// [GET] 前台新增意見表單
    /// 自動產生唯一 TrackingCode 帶入表單
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        // 呼叫 Service 產生唯一 TrackingCode（反覆確認至不重複）
        string trackingCode = await _feedbackService.GenerateUniqueTrackingCodeAsync();

        // 建立空白表單 ViewModel，帶入自動產生的 TrackingCode
        var model = new FeedbackCreateViewModel
        {
            TrackingCode = trackingCode
        };

        // 傳入 Category 下拉清單
        ViewBag.CategoryList = CategorySelectList;

        return View(model);
    }

    /// <summary>
    /// [POST] 儲存新增意見
    /// 表單驗證通過後呼叫 Service 寫入 DB，成功後導向詳情頁
    /// </summary>
    /// <param name="model">前台新增表單 ViewModel（Model Binding 自動填入）</param>
    [HttpPost]
    public async Task<IActionResult> Create(FeedbackCreateViewModel model)
    {
        // 重新帶入下拉清單（POST 失敗需重新顯示表單時使用）
        ViewBag.CategoryList = CategorySelectList;

        // 若 ModelState 驗證失敗，回傳原表單並顯示錯誤訊息
        if (!ModelState.IsValid)
            return View(model);

        // 再次確認 TrackingCode 唯一性（防止極端情況下的重複），呼叫 SP usp_Feedback_CheckTrackingCodeExists
        bool exists = await _feedbackService.CheckTrackingCodeExistsAsync(model.TrackingCode);
        if (exists)
        {
            // TrackingCode 重複時重新產生並提示使用者重試
            model.TrackingCode = await _feedbackService.GenerateUniqueTrackingCodeAsync();
            ModelState.AddModelError(string.Empty, "追蹤代碼已重複，已自動重新產生，請確認後再提交。");
            return View(model);
        }

        // 呼叫 Service 新增意見，取得新產生的 FeedbackId
        int newFeedbackId = await _feedbackService.InsertFeedbackAsync(model);

        // 新增成功後導向詳情頁，讓使用者確認已建立的意見
        return RedirectToAction(nameof(Detail), new { id = newFeedbackId });
    }

    // ─────────────────────────────────────────────────────────────────
    // Detail：意見詳情（含回覆串）
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// [GET] 意見詳情頁
    /// 顯示意見全部欄位與回覆串，頁底嵌入新增回覆表單
    /// </summary>
    /// <param name="id">意見識別碼（路由參數）</param>
    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        // 取得單筆意見主體資料
        var detail = await _feedbackService.GetByIdAsync(id);

        // 若找不到資料則回傳 404
        if (detail == null)
            return NotFound();

        // 取得此意見的完整回覆串（含私密回覆）
        var replies = await _feedbackService.GetRepliesByFeedbackIdAsync(id);
        detail.Replies = replies.ToList();

        // 初始化新增回覆表單，預設帶入 FeedbackId 與回覆類型
        detail.NewReply = new FeedbackReplyCreateViewModel
        {
            FeedbackId = id,
            ReplyType  = 1,   // 預設官方回覆
            IsPublic   = true
        };

        // 傳入新增回覆表單所需的下拉清單
        ViewBag.ReplyTypeList = ReplyTypeSelectList;

        return View(detail);
    }

    // ─────────────────────────────────────────────────────────────────
    // Edit：後台編輯意見
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// [GET] 後台編輯意見表單
    /// 讀取現有資料帶入表單欄位
    /// </summary>
    /// <param name="id">意見識別碼（路由參數）</param>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        // 取得單筆意見資料（含唯讀顯示欄位）
        var detail = await _feedbackService.GetByIdAsync(id);

        // 找不到資料則回傳 404
        if (detail == null)
            return NotFound();

        // 將 Detail ViewModel 的欄位對應到 Edit ViewModel
        var model = new FeedbackEditViewModel
        {
            FeedbackId   = detail.FeedbackId,
            TrackingCode = detail.TrackingCode,   // 唯讀顯示
            CustomerName = detail.CustomerName,   // 唯讀顯示
            CustomerEmail = detail.CustomerEmail, // 唯讀顯示
            CreatedAt    = detail.CreatedAt,      // 唯讀顯示
            Category     = detail.Category,
            Subject      = detail.Subject,
            Content      = detail.Content,
            Status       = detail.Status,
            Priority     = detail.Priority,
            AdminNote    = detail.AdminNote
        };

        // 傳入下拉清單資料
        ViewBag.CategoryList = CategorySelectList;
        ViewBag.StatusList   = StatusSelectList;
        ViewBag.PriorityList = PrioritySelectList;

        return View(model);
    }

    /// <summary>
    /// [POST] 儲存編輯結果
    /// 驗證通過後呼叫 Service 更新 DB，成功後導向詳情頁
    /// </summary>
    /// <param name="id">路由中的 FeedbackId（用於防止偽造請求）</param>
    /// <param name="model">後台編輯表單 ViewModel</param>
    [HttpPost]
    public async Task<IActionResult> Edit(int id, FeedbackEditViewModel model)
    {
        // 防止路由 id 與表單 FeedbackId 不一致（防止偽造）
        if (id != model.FeedbackId)
            return BadRequest();

        // 重新帶入下拉清單（POST 失敗時需重新顯示）
        ViewBag.CategoryList = CategorySelectList;
        ViewBag.StatusList   = StatusSelectList;
        ViewBag.PriorityList = PrioritySelectList;

        // ModelState 驗證失敗則回傳原表單
        if (!ModelState.IsValid)
            return View(model);

        // 呼叫 Service 更新意見（SP 內部更新 UpdatedAt）
        await _feedbackService.UpdateFeedbackAsync(model);

        // 更新成功後導向詳情頁確認結果
        return RedirectToAction(nameof(Detail), new { id = model.FeedbackId });
    }

    // ─────────────────────────────────────────────────────────────────
    // AddReply：新增回覆（POST only，嵌入詳情頁表單）
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// [POST] 新增回覆
    /// 驗證通過後呼叫 Service 新增回覆（SP 內部同步快取欄位與狀態）
    /// 完成後 Redirect 回詳情頁
    /// </summary>
    /// <param name="model">新增回覆表單 ViewModel（由 Detail 頁底部表單送出）</param>
    [HttpPost]
    public async Task<IActionResult> AddReply(FeedbackReplyCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            // 驗證失敗時需重新載入詳情頁（含完整的回覆串與下拉清單）
            var detail = await _feedbackService.GetByIdAsync(model.FeedbackId);
            if (detail == null)
                return NotFound();

            var replies = await _feedbackService.GetRepliesByFeedbackIdAsync(model.FeedbackId);
            detail.Replies  = replies.ToList();
            detail.NewReply = model; // 保留使用者已輸入的內容
            ViewBag.ReplyTypeList = ReplyTypeSelectList;

            return View("Detail", detail);
        }

        // 呼叫 Service 新增回覆（SP 內部以 Transaction 確保快取欄位同步更新）
        await _feedbackService.InsertReplyAsync(model);

        // 新增成功後 Redirect 回詳情頁，避免重整頁面重複送出（PRG Pattern）
        return RedirectToAction(nameof(Detail), new { id = model.FeedbackId });
    }

}
