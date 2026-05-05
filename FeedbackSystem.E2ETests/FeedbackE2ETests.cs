// ──────────────────────────────────────────────────────────────────────────────
//  使用 Playwright .NET + NUnit 進行 E2E 測試
//  涵蓋的使用者操作流程：
//    1. 新增意見（Create）：填寫表單 → 選擇下拉類別 → 提交 → 驗證導向詳情頁
//    2. 查詢列表（Index）：篩選 Status / Priority 下拉 → 驗證結果
//    3. 編輯意見（Edit）：修改類別、狀態、優先等級下拉 → 儲存 → 驗證 DB
//    4. 新增回覆（AddReply）：選擇回覆類型下拉 → 送出回覆 → 驗證 DB 回覆數
//    5. Teardown：所有測試後透過 DB 直連清除 [E2E_TEST] 標記資料
// ──────────────────────────────────────────────────────────────────────────────

using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace FeedbackSystem.E2ETests;

/// <summary>
/// FeedbackController 的 E2E 測試類別。
/// 繼承 PageTest 以自動取得已初始化的 Page 物件（Chromium），
/// 模擬使用者在瀏覽器中的完整操作流程。
///
/// 前置條件：
///   - 請先在另一個終端機執行：dotnet run --project ..\FeedbackSystem.csproj
///   - 確認應用程式在 https://localhost:7233 正常回應後，再執行本測試
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)] // 每個 TestFixture 使用獨立 BrowserContext，可安全並行
public class FeedbackE2ETests : PageTest
{
    // ─── 應用程式設定 ───────────────────────────────────────────────────────
    // 統一從 AppStartupFixture 取得 BaseUrl（已處理環境變數與預設值）
    // AppStartupFixture 的 [SetUpFixture][OneTimeSetUp] 會在第一個測試前自動啟動應用程式，
    // 因此此處不需要再讀取環境變數，直接引用已解析好的 AppBaseUrl。
    private static string BaseUrl => AppStartupFixture.AppBaseUrl;

    // 每個瀏覽器操作之間加入的人工延遲（毫秒），模擬真實使用者速度
    private const int HumanDelayMs = 600;

    // ─── DB 連線字串（Teardown 清理測試資料用）──────────────────────────────
    private string _connectionString = string.Empty;

    // ─── 用於跨測試方法共用的資料記錄（e.g. 新增後的 ID）──────────────────
    // 記錄由 Create 流程建立的 FeedbackId，供後續 Edit / Reply 流程使用
    private int _createdFeedbackId = 0;

    // 用於新增測試的唯一標題前綴（含 GUID 確保不重複）
    private string _testTitlePrefix = string.Empty;

    // ═══════════════════════════════════════════════════════════════════════
    //  初始化
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 測試類別啟動一次：讀取 appsettings.json 取得 DB 連線字串，
    /// 並初始化本次測試共用的唯一資料前綴。
    /// </summary>
    [OneTimeSetUp]
    public void ReadConfiguration()
    {
        // 從輸出目錄讀取 appsettings.json（已設定 CopyToOutputDirectory）
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables() // 允許 CI 環境變數覆寫連線字串
            .Build();

        // 取得 Feedback_Test 資料庫連線字串，缺少時提前失敗
        _connectionString = config.GetConnectionString("FeedbackDb")
            ?? throw new InvalidOperationException(
                "appsettings.json 缺少 ConnectionStrings:FeedbackDb，請確認已正確複製至輸出目錄");

        // 每次測試執行使用唯一前綴，避免不同執行次的資料互相干擾
        _testTitlePrefix = $"[E2E_TEST] {Guid.NewGuid():N}";
    }

    /// <summary>
    /// 測試類別結束後清理所有以 [E2E_TEST] 前綴標記的資料，
    /// 無論任何測試失敗都能確保 DB 不殘留髒資料。
    /// </summary>
    [OneTimeTearDown]
    public async Task CleanupAllTestDataAsync()
    {
        // 以 [E2E_TEST] 開頭的 Title 為識別標準，統一清除本次執行建立的資料
        await CleanupTestFeedbackByTitlePrefixAsync("[E2E_TEST]");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  覆寫 BrowserContext 選項
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 覆寫 PageTest 預設的 ContextOptions：
    ///   - 忽略本機開發環境的 HTTPS 自簽憑證錯誤
    ///   - 固定 1280×720 視窗大小，避免 RWD 版型差異影響 Locator 選取
    ///   - 設定語系為 zh-TW，確保 UI 文字與測試斷言一致
    /// </summary>
    public override BrowserNewContextOptions ContextOptions()
    {
        return new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,                          // 忽略本機自簽憑證
            ViewportSize      = new ViewportSize { Width = 1280, Height = 720 },
            Locale            = "zh-TW"                        // 語系設定
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Test 1：新增意見（Create）
    //  操作流程：Index 頁 → 點擊「＋ 提交新意見」→ 填表 → 選類別「服務」→ 提交
    //  驗證：導向到詳情頁、DB 中存有此筆資料、Category 為「服務」
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 模擬使用者從列表頁點擊新增按鈕，填寫表單（含下拉選擇「服務」類別），
    /// 提交後驗證導向詳情頁，並直接查詢 DB 確認資料正確寫入。
    /// </summary>
    [Test]
    [Order(1)] // 先執行新增，取得 ID 供後續測試使用
    public async Task Test01_Create_FillFormAndSubmit_VerifyInDb()
    {
        // ── Arrange ─────────────────────────────────────────────────────────
        // 使用本次唯一前綴作為主旨，方便 Teardown 識別
        string subject = $"{_testTitlePrefix} 新增測試主旨";
        string customerName  = "E2E 測試使用者";
        string customerEmail = "e2e_test@example.com";
        string customerPhone = "0912345678";

        // ── Act：瀏覽器操作 ──────────────────────────────────────────────────

        // Step 1：前往意見管理列表頁
        await Page.GotoAsync($"{BaseUrl}/Feedback");
        // 等待頁面 DOM 載入完成，確保按鈕已渲染
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await Task.Delay(HumanDelayMs); // 人工延遲，模擬使用者瀏覽頁面

        // Step 2：點擊「＋ 提交新意見」按鈕，前往新增頁
        await Page.GetByRole(AriaRole.Link, new() { Name = "＋ 提交新意見" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await Task.Delay(HumanDelayMs);

        // Step 3：驗證已在新增頁，確認表單存在
        await Expect(Page).ToHaveURLAsync(new Regex($"{Regex.Escape(BaseUrl)}/Feedback/Create"));

        // Step 4：填寫客戶姓名（等待欄位可見後再填入）
        var nameInput = Page.GetByLabel("客戶姓名");
        await nameInput.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await nameInput.FillAsync(customerName);
        await Task.Delay(HumanDelayMs); // 每個欄位之間延遲，模擬打字速度

        // Step 5：填寫電子信箱
        await Page.GetByLabel("電子信箱").FillAsync(customerEmail);
        await Task.Delay(HumanDelayMs);

        // Step 6：填寫聯絡電話（選填）
        await Page.GetByLabel("聯絡電話").FillAsync(customerPhone);
        await Task.Delay(HumanDelayMs);

        // Step 7：選擇意見類別下拉為「服務」（測試非預設選項）
        // 使用 SelectOptionAsync 並指定 label 值，語意最明確
        var categorySelect = Page.GetByLabel("意見類別");
        await categorySelect.SelectOptionAsync(new SelectOptionValue { Label = "服務" });
        await Task.Delay(HumanDelayMs);

        // Step 8：填寫意見主旨
        await Page.GetByLabel("意見主旨").FillAsync(subject);
        await Task.Delay(HumanDelayMs);

        // Step 9：填寫意見內容
        await Page.GetByLabel("意見內容").FillAsync("這是 E2E 自動化測試填入的詳細內容，請勿在正式環境中顯示。");
        await Task.Delay(HumanDelayMs);

        // Step 10：點擊「提交意見」按鈕送出表單
        await Page.GetByRole(AriaRole.Button, new() { Name = "提交意見" }).ClickAsync();

        // 等待頁面導向完成（詳情頁載入）
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await Task.Delay(HumanDelayMs);

        // ── Assert：UI 驗證 ──────────────────────────────────────────────────

        // 驗證已成功導向詳情頁（URL 應為 /Feedback/Detail/{id}）
        await Expect(Page).ToHaveURLAsync(
            new Regex($"{Regex.Escape(BaseUrl)}/Feedback/Detail/\\d+"));

        // 驗證頁面標題為「意見詳情」
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "意見詳情" })).ToBeVisibleAsync();

        // 驗證詳情頁中顯示剛填入的主旨文字
        await Expect(Page.GetByText(subject)).ToBeVisibleAsync();

        // 驗證意見類別顯示為「服務」
        await Expect(Page.GetByText("服務")).ToBeVisibleAsync();

        // ── 從 URL 取得新建立的 FeedbackId，供後續測試使用 ──────────────────
        var urlMatch = Regex.Match(Page.Url, @"/Feedback/Detail/(\d+)");
        Assert.That(urlMatch.Success, Is.True, "無法從 URL 解析 FeedbackId");
        _createdFeedbackId = int.Parse(urlMatch.Groups[1].Value);

        // ── Assert：DB 驗證 ──────────────────────────────────────────────────
        // 直接查詢資料庫，確認資料確實寫入且欄位值正確
        var dbRecord = await GetFeedbackFromDbAsync(_createdFeedbackId);

        Assert.That(dbRecord, Is.Not.Null,
            $"DB 中找不到 FeedbackId={_createdFeedbackId} 的資料");
        Assert.That(dbRecord!.Category,     Is.EqualTo("服務"),
            "DB 中 Category 應為「服務」");
        Assert.That(dbRecord.Subject,       Is.EqualTo(subject),
            "DB 中 Subject 應與填入值相符");
        Assert.That(dbRecord.CustomerName,  Is.EqualTo(customerName),
            "DB 中 CustomerName 應與填入值相符");
        Assert.That(dbRecord.CustomerEmail, Is.EqualTo(customerEmail),
            "DB 中 CustomerEmail 應與填入值相符");
        Assert.That(dbRecord.Status,        Is.EqualTo((byte)0),
            "新增意見的預設狀態應為 0（待處理）");
        Assert.That(dbRecord.Priority,      Is.EqualTo((byte)1),
            "新增意見的預設優先等級應為 1（一般）");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Test 2：查詢列表篩選（Index Filter）
    //  操作流程：前往列表頁 → 選擇 Status=「待處理」、Priority=「一般」→ 篩選
    //  驗證：結果表格中的每筆資料均顯示符合的狀態/優先等級
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 模擬使用者在列表頁操作篩選下拉（Status、Priority），
    /// 驗證篩選後表格中顯示的資料均符合篩選條件。
    /// </summary>
    [Test]
    [Order(2)]
    public async Task Test02_Index_FilterByStatusAndPriority_ShowsMatchingRows()
    {
        // ── Arrange ─────────────────────────────────────────────────────────
        // 前往意見管理列表頁
        await Page.GotoAsync($"{BaseUrl}/Feedback");
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await Task.Delay(HumanDelayMs);

        // ── Act：選擇篩選條件 ────────────────────────────────────────────────

        // Step 1：選擇「處理狀態」下拉為「待處理」（value = 0）
        // Index 頁的 select name="status"，使用 Locator by name 屬性
        await Page.Locator("select[name='status']").SelectOptionAsync(
            new SelectOptionValue { Value = "0" });
        await Task.Delay(HumanDelayMs);

        // Step 2：選擇「優先等級」下拉為「一般」（value = 1）
        await Page.Locator("select[name='priority']").SelectOptionAsync(
            new SelectOptionValue { Value = "1" });
        await Task.Delay(HumanDelayMs);

        // Step 3：點擊「篩選」按鈕送出篩選表單
        await Page.GetByRole(AriaRole.Button, new() { Name = "篩選" }).ClickAsync();

        // 等待篩選結果頁面載入完成（含網路請求）
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Task.Delay(HumanDelayMs);

        // ── Assert ───────────────────────────────────────────────────────────

        // 驗證 URL 帶有篩選參數（status=0 且 priority=1）
        await Expect(Page).ToHaveURLAsync(
            new Regex($"{Regex.Escape(BaseUrl)}/Feedback\\?.*status=0.*priority=1"));

        // 取得表格中所有資料列（排除 header）
        var rows = Page.Locator("table tbody tr");
        int rowCount = await rows.CountAsync();

        // 若有結果，驗證第一筆資料列包含「待處理」與「一般」的 badge 文字
        if (rowCount > 0)
        {
            // 驗證第一列中有「待處理」文字（Status badge）
            await Expect(rows.First.GetByText("待處理")).ToBeVisibleAsync();
            // 驗證第一列中有「一般」文字（Priority badge）
            await Expect(rows.First.GetByText("一般")).ToBeVisibleAsync();
        }
        else
        {
            // 無結果時應顯示「目前尚無意見資料」提示
            await Expect(Page.GetByText("目前尚無意見資料")).ToBeVisibleAsync();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Test 3：編輯意見（Edit）
    //  操作流程：從 Test01 建立的詳情頁 → 點擊「編輯此意見」→
    //            修改類別為「建議」/ 狀態為「處理中」/ 優先等級為「重要」→ 儲存
    //  驗證：DB 中欄位已更新
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 模擬管理員進入編輯頁，修改下拉欄位（Category / Status / Priority），
    /// 儲存後直接查詢 DB 確認欄位值已更新。
    /// 本測試依賴 Test01 建立的 FeedbackId（_createdFeedbackId）。
    /// </summary>
    [Test]
    [Order(3)]
    public async Task Test03_Edit_ChangeDropdowns_VerifyUpdatedInDb()
    {
        // ── Arrange ─────────────────────────────────────────────────────────
        // 確認前置測試已建立資料
        Assert.That(_createdFeedbackId, Is.GreaterThan(0),
            "Test01 必須先執行以取得 FeedbackId，請確認測試執行順序");

        // 直接導向該筆意見的詳情頁
        await Page.GotoAsync($"{BaseUrl}/Feedback/Detail/{_createdFeedbackId}");
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await Task.Delay(HumanDelayMs);

        // ── Act：瀏覽器操作 ──────────────────────────────────────────────────

        // Step 1：點擊「編輯此意見」按鈕（詳情頁右上角）
        await Page.GetByRole(AriaRole.Link, new() { Name = "編輯此意見" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await Task.Delay(HumanDelayMs);

        // 驗證已導向編輯頁
        await Expect(Page).ToHaveURLAsync(
            new Regex($"{Regex.Escape(BaseUrl)}/Feedback/Edit/{_createdFeedbackId}"));

        // Step 2：修改「意見類別」下拉為「建議」（原為「服務」，測試變更）
        var categorySelect = Page.Locator("select[id='Category']");
        await categorySelect.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await categorySelect.SelectOptionAsync(new SelectOptionValue { Label = "建議" });
        await Task.Delay(HumanDelayMs);

        // Step 3：修改「處理狀態」下拉為「處理中」（value = 1）
        var statusSelect = Page.Locator("select[id='Status']");
        await statusSelect.SelectOptionAsync(new SelectOptionValue { Value = "1" });
        await Task.Delay(HumanDelayMs);

        // Step 4：修改「優先等級」下拉為「重要」（value = 2）
        var prioritySelect = Page.Locator("select[id='Priority']");
        await prioritySelect.SelectOptionAsync(new SelectOptionValue { Value = "2" });
        await Task.Delay(HumanDelayMs);

        // Step 5：修改管理員備註（確認此欄位也能正確寫入）
        var adminNoteTextarea = Page.Locator("textarea[id='AdminNote']");
        await adminNoteTextarea.FillAsync("E2E 測試自動修改的管理員備註");
        await Task.Delay(HumanDelayMs);

        // Step 6：點擊「儲存變更」送出表單
        await Page.GetByRole(AriaRole.Button, new() { Name = "儲存變更" }).ClickAsync();

        // 等待導向回詳情頁
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await Task.Delay(HumanDelayMs);

        // ── Assert：UI 驗證 ──────────────────────────────────────────────────

        // 驗證已回到詳情頁
        await Expect(Page).ToHaveURLAsync(
            new Regex($"{Regex.Escape(BaseUrl)}/Feedback/Detail/{_createdFeedbackId}"));

        // 驗證詳情頁顯示更新後的類別「建議」
        await Expect(Page.GetByText("建議")).ToBeVisibleAsync();

        // 驗證詳情頁顯示更新後的狀態 badge「處理中」
        await Expect(Page.Locator(".badge").Filter(new() { HasText = "處理中" })).ToBeVisibleAsync();

        // 驗證詳情頁顯示更新後的優先等級 badge「重要」
        await Expect(Page.Locator(".badge").Filter(new() { HasText = "重要" })).ToBeVisibleAsync();

        // ── Assert：DB 驗證 ──────────────────────────────────────────────────
        var dbRecord = await GetFeedbackFromDbAsync(_createdFeedbackId);

        Assert.That(dbRecord, Is.Not.Null);
        Assert.That(dbRecord!.Category, Is.EqualTo("建議"),
            "DB 中 Category 應已更新為「建議」");
        Assert.That(dbRecord.Status, Is.EqualTo((byte)1),
            "DB 中 Status 應已更新為 1（處理中）");
        Assert.That(dbRecord.Priority, Is.EqualTo((byte)2),
            "DB 中 Priority 應已更新為 2（重要）");
        Assert.That(dbRecord.AdminNote, Is.EqualTo("E2E 測試自動修改的管理員備註"),
            "DB 中 AdminNote 應與填入值相符");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Test 4：新增回覆（AddReply）
    //  操作流程：前往詳情頁 → 選擇「客戶回覆」類型（非預設官方回覆）→ 填寫內容 → 送出
    //  驗證：頁面回覆列表增加一筆、DB 的 ReplyCount = 1
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 模擬使用者在詳情頁下方的「新增回覆」表單，
    /// 選擇回覆類型「客戶回覆」（測試非預設值），填入回覆內容後送出，
    /// 驗證 UI 顯示新回覆，並直接查詢 DB 確認 ReplyCount 已增加。
    /// </summary>
    [Test]
    [Order(4)]
    public async Task Test04_AddReply_SelectCustomerReplyType_VerifyInDb()
    {
        // ── Arrange ─────────────────────────────────────────────────────────
        Assert.That(_createdFeedbackId, Is.GreaterThan(0),
            "Test01 必須先執行以取得 FeedbackId");

        string replyContent  = "這是 E2E 測試填入的客戶回覆內容";
        string replierName   = "E2E 客戶";

        // 前往詳情頁
        await Page.GotoAsync($"{BaseUrl}/Feedback/Detail/{_createdFeedbackId}");
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await Task.Delay(HumanDelayMs);

        // ── Act：瀏覽器操作 ──────────────────────────────────────────────────

        // Step 1：頁面捲動到「新增回覆」區塊（確保表單在可視範圍內）
        var replyForm = Page.Locator("form[action*='AddReply']");
        // 等待回覆表單存在
        await replyForm.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await replyForm.ScrollIntoViewIfNeededAsync();
        await Task.Delay(HumanDelayMs);

        // Step 2：選擇「回覆類型」下拉為「客戶回覆」（value = 0，測試非預設的「官方回覆」）
        // Detail 頁的 select name="NewReply.ReplyType"
        var replyTypeSelect = Page.Locator("select[id='NewReply_ReplyType']");
        await replyTypeSelect.SelectOptionAsync(new SelectOptionValue { Value = "0" });
        await Task.Delay(HumanDelayMs);

        // Step 3：填寫回覆者姓名
        var replierNameInput = Page.Locator("input[id='NewReply_ReplierName']");
        await replierNameInput.FillAsync(replierName);
        await Task.Delay(HumanDelayMs);

        // Step 4：填寫回覆內容
        var contentTextarea = Page.Locator("textarea[id='NewReply_Content']");
        await contentTextarea.FillAsync(replyContent);
        await Task.Delay(HumanDelayMs);

        // Step 5：確認「公開顯示」核取方塊已勾選（預設應為勾選，這裡明確確認）
        var isPublicCheckbox = Page.Locator("input[id='NewReply_IsPublic']");
        bool isChecked = await isPublicCheckbox.IsCheckedAsync();
        if (!isChecked)
        {
            // 若未勾選則點擊勾選（保持公開）
            await isPublicCheckbox.CheckAsync();
        }
        await Task.Delay(HumanDelayMs);

        // Step 6：點擊「送出回覆」按鈕
        await Page.GetByRole(AriaRole.Button, new() { Name = "送出回覆" }).ClickAsync();

        // 等待頁面 redirect 回詳情頁完成
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await Task.Delay(HumanDelayMs);

        // ── Assert：UI 驗證 ──────────────────────────────────────────────────

        // 驗證仍在詳情頁（AddReply 成功後 Redirect 回 Detail）
        await Expect(Page).ToHaveURLAsync(
            new Regex($"{Regex.Escape(BaseUrl)}/Feedback/Detail/{_createdFeedbackId}"));

        // 驗證回覆列表中出現「客戶回覆」badge
        await Expect(Page.Locator(".badge").Filter(new() { HasText = "客戶回覆" }).First)
            .ToBeVisibleAsync();

        // 驗證回覆內容文字出現在頁面上
        await Expect(Page.GetByText(replyContent)).ToBeVisibleAsync();

        // ── Assert：DB 驗證 ──────────────────────────────────────────────────
        // 驗證 Feedback 主表的 ReplyCount 已更新為 1
        var dbRecord = await GetFeedbackFromDbAsync(_createdFeedbackId);
        Assert.That(dbRecord, Is.Not.Null);
        Assert.That(dbRecord!.ReplyCount, Is.EqualTo(1),
            "DB 中 Feedback.ReplyCount 應為 1（已新增一筆回覆）");

        // 驗證 FeedbackReply 子表中確實存在對應的回覆記錄
        var dbReply = await GetLatestReplyFromDbAsync(_createdFeedbackId);
        Assert.That(dbReply, Is.Not.Null,
            "DB 中應存在對應 FeedbackId 的回覆記錄");
        Assert.That(dbReply!.Content, Is.EqualTo(replyContent),
            "DB 中回覆 Content 應與填入值相符");
        Assert.That(dbReply.ReplyType, Is.EqualTo((byte)0),
            "DB 中回覆 ReplyType 應為 0（客戶回覆）");
        Assert.That(dbReply.ReplierName, Is.EqualTo(replierName),
            "DB 中回覆 ReplierName 應與填入值相符");
        Assert.That(dbReply.IsPublic, Is.True,
            "DB 中回覆 IsPublic 應為 true（公開顯示）");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Test 5：新增第二筆回覆（官方回覆）並驗證 ReplyCount 累加
    //  操作流程：同 Test04，但選擇「官方回覆」類型
    //  驗證：DB ReplyCount = 2
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 新增第二筆官方回覆，驗證 DB ReplyCount 累加至 2，
    /// 並確認 LatestReplyContent 快取欄位也已更新。
    /// </summary>
    [Test]
    [Order(5)]
    public async Task Test05_AddSecondReply_AdminType_VerifyReplyCountIncremented()
    {
        // ── Arrange ─────────────────────────────────────────────────────────
        Assert.That(_createdFeedbackId, Is.GreaterThan(0),
            "Test01 必須先執行以取得 FeedbackId");

        string adminReplyContent = "這是 E2E 測試的官方回覆，感謝您的意見！";
        string adminReplierName  = "E2E 客服人員";

        // 前往詳情頁
        await Page.GotoAsync($"{BaseUrl}/Feedback/Detail/{_createdFeedbackId}");
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await Task.Delay(HumanDelayMs);

        // ── Act ──────────────────────────────────────────────────────────────

        // 捲動到回覆表單
        var replyForm = Page.Locator("form[action*='AddReply']");
        await replyForm.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await replyForm.ScrollIntoViewIfNeededAsync();
        await Task.Delay(HumanDelayMs);

        // 選擇「官方回覆」（value = 1，這次測試預設選項之外的確認）
        await Page.Locator("select[id='NewReply_ReplyType']")
                  .SelectOptionAsync(new SelectOptionValue { Value = "1" });
        await Task.Delay(HumanDelayMs);

        // 填寫回覆者姓名
        await Page.Locator("input[id='NewReply_ReplierName']").FillAsync(adminReplierName);
        await Task.Delay(HumanDelayMs);

        // 填寫回覆內容
        await Page.Locator("textarea[id='NewReply_Content']").FillAsync(adminReplyContent);
        await Task.Delay(HumanDelayMs);

        // 取消勾選「公開顯示」（測試私密回覆功能）
        var isPublicCheckbox = Page.Locator("input[id='NewReply_IsPublic']");
        bool isChecked = await isPublicCheckbox.IsCheckedAsync();
        if (isChecked)
        {
            // 點擊取消勾選，設為私密回覆
            await isPublicCheckbox.UncheckAsync();
        }
        await Task.Delay(HumanDelayMs);

        // 送出回覆
        await Page.GetByRole(AriaRole.Button, new() { Name = "送出回覆" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await Task.Delay(HumanDelayMs);

        // ── Assert：UI 驗證 ──────────────────────────────────────────────────

        // 驗證頁面顯示「2 筆」回覆（回覆串 header 文字）
        await Expect(Page.GetByText(new Regex("回覆記錄（2 筆）"))).ToBeVisibleAsync();

        // 驗證官方回覆內容出現在頁面上
        await Expect(Page.GetByText(adminReplyContent)).ToBeVisibleAsync();

        // 驗證「私密」badge 存在（IsPublic = false 時顯示）
        await Expect(Page.Locator(".badge").Filter(new() { HasText = "私密" }).First)
            .ToBeVisibleAsync();

        // ── Assert：DB 驗證 ──────────────────────────────────────────────────
        var dbRecord = await GetFeedbackFromDbAsync(_createdFeedbackId);
        Assert.That(dbRecord, Is.Not.Null);
        Assert.That(dbRecord!.ReplyCount, Is.EqualTo(2),
            "DB 中 Feedback.ReplyCount 應累加為 2");
        Assert.That(dbRecord.LatestReplyContent, Is.EqualTo(adminReplyContent),
            "DB 中 LatestReplyContent 快取應更新為最新一筆回覆內容");

        // 驗證最新回覆記錄的 IsPublic = false
        var dbReply = await GetLatestReplyFromDbAsync(_createdFeedbackId);
        Assert.That(dbReply, Is.Not.Null);
        Assert.That(dbReply!.IsPublic, Is.False,
            "DB 中最新回覆的 IsPublic 應為 false（私密回覆）");
        Assert.That(dbReply.ReplyType, Is.EqualTo((byte)1),
            "DB 中最新回覆 ReplyType 應為 1（官方回覆）");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Private Helpers：DB 查詢輔助方法
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 從 DB 查詢指定 FeedbackId 的意見主表資料。
    /// 使用獨立 SqlConnection，不依賴任何 Playwright 或 App 狀態。
    /// </summary>
    /// <param name="feedbackId">要查詢的意見 ID</param>
    /// <returns>匿名物件含常用欄位，若找不到則回傳 null</returns>
    private async Task<FeedbackDbRecord?> GetFeedbackFromDbAsync(int feedbackId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // 查詢意見主表的關鍵欄位，使用參數化查詢防止 SQL Injection
        const string sql = @"
            SELECT FeedbackId, Category, Subject, Content,
                   CustomerName, CustomerEmail, CustomerPhone,
                   Status, Priority, AdminNote,
                   ReplyCount, LatestReplyContent
            FROM   Feedback
            WHERE  FeedbackId = @FeedbackId";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@FeedbackId", feedbackId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null; // 找不到資料

        // 將 DataReader 結果對應至記錄物件
        return new FeedbackDbRecord
        {
            FeedbackId          = reader.GetInt32(reader.GetOrdinal("FeedbackId")),
            Category            = reader.GetString(reader.GetOrdinal("Category")),
            Subject             = reader.GetString(reader.GetOrdinal("Subject")),
            Content             = reader.GetString(reader.GetOrdinal("Content")),
            CustomerName        = reader.GetString(reader.GetOrdinal("CustomerName")),
            CustomerEmail       = reader.GetString(reader.GetOrdinal("CustomerEmail")),
            CustomerPhone       = reader.IsDBNull(reader.GetOrdinal("CustomerPhone"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("CustomerPhone")),
            Status              = reader.GetByte(reader.GetOrdinal("Status")),
            Priority            = reader.GetByte(reader.GetOrdinal("Priority")),
            AdminNote           = reader.IsDBNull(reader.GetOrdinal("AdminNote"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("AdminNote")),
            ReplyCount          = reader.GetInt32(reader.GetOrdinal("ReplyCount")),
            LatestReplyContent  = reader.IsDBNull(reader.GetOrdinal("LatestReplyContent"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("LatestReplyContent"))
        };
    }

    /// <summary>
    /// 從 DB 查詢指定 FeedbackId 的最新一筆回覆記錄（依 CreatedAt DESC）。
    /// </summary>
    /// <param name="feedbackId">要查詢的意見 ID</param>
    /// <returns>匿名物件含常用欄位，若找不到則回傳 null</returns>
    private async Task<FeedbackReplyDbRecord?> GetLatestReplyFromDbAsync(int feedbackId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // 取最新一筆（CreatedAt 最大的）回覆，使用參數化查詢
        const string sql = @"
            SELECT TOP 1
                   ReplyId, FeedbackId, Content, ReplierName,
                   ReplyType, IsPublic, CreatedAt
            FROM   FeedbackReply
            WHERE  FeedbackId = @FeedbackId
            ORDER BY CreatedAt DESC";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@FeedbackId", feedbackId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null; // 無回覆記錄

        return new FeedbackReplyDbRecord
        {
            ReplyId     = reader.GetInt32(reader.GetOrdinal("ReplyId")),
            FeedbackId  = reader.GetInt32(reader.GetOrdinal("FeedbackId")),
            Content     = reader.GetString(reader.GetOrdinal("Content")),
            ReplierName = reader.GetString(reader.GetOrdinal("ReplierName")),
            ReplyType   = reader.GetByte(reader.GetOrdinal("ReplyType")),
            IsPublic    = reader.GetBoolean(reader.GetOrdinal("IsPublic"))
        };
    }

    /// <summary>
    /// Teardown 用：刪除標題包含指定前綴的所有測試 Feedback 資料。
    /// 先刪子表 FeedbackReply（FK 約束），再刪主表 Feedback。
    /// </summary>
    /// <param name="titlePrefix">要清除的 Subject 前綴字串（例如 "[E2E_TEST]"）</param>
    private async Task CleanupTestFeedbackByTitlePrefixAsync(string titlePrefix)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // Step 1：先刪除子表回覆資料（避免外鍵約束造成主表刪除失敗）
        const string deleteReplySql = @"
            DELETE fr
            FROM   FeedbackReply fr
            INNER JOIN Feedback f ON fr.FeedbackId = f.FeedbackId
            WHERE  f.Subject LIKE @TitlePrefix + '%'";

        await using var deleteReplyCmd = new SqlCommand(deleteReplySql, conn);
        deleteReplyCmd.Parameters.AddWithValue("@TitlePrefix", titlePrefix);
        await deleteReplyCmd.ExecuteNonQueryAsync();

        // Step 2：再刪除主表資料
        const string deleteFeedbackSql = @"
            DELETE FROM Feedback
            WHERE  Subject LIKE @TitlePrefix + '%'";

        await using var deleteFeedbackCmd = new SqlCommand(deleteFeedbackSql, conn);
        deleteFeedbackCmd.Parameters.AddWithValue("@TitlePrefix", titlePrefix);
        await deleteFeedbackCmd.ExecuteNonQueryAsync();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Private Record Types：DB 查詢結果的輕量資料物件
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Feedback 主表 DB 查詢結果的輕量物件（僅含 E2E 驗證所需欄位）</summary>
    private sealed class FeedbackDbRecord
    {
        public int     FeedbackId         { get; init; }
        public string  Category           { get; init; } = string.Empty;
        public string  Subject            { get; init; } = string.Empty;
        public string  Content            { get; init; } = string.Empty;
        public string  CustomerName       { get; init; } = string.Empty;
        public string  CustomerEmail      { get; init; } = string.Empty;
        public string? CustomerPhone      { get; init; }
        public byte    Status             { get; init; }
        public byte    Priority           { get; init; }
        public string? AdminNote          { get; init; }
        public int     ReplyCount         { get; init; }
        public string? LatestReplyContent { get; init; }
    }

    /// <summary>FeedbackReply 子表 DB 查詢結果的輕量物件</summary>
    private sealed class FeedbackReplyDbRecord
    {
        public int    ReplyId    { get; init; }
        public int    FeedbackId { get; init; }
        public string Content    { get; init; } = string.Empty;
        public string ReplierName{ get; init; } = string.Empty;
        public byte   ReplyType  { get; init; }
        public bool   IsPublic   { get; init; }
    }
}
