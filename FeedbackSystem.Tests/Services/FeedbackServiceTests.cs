using FeedbackSystem.Models.ViewModels;
using FeedbackSystem.Repositories;
using FeedbackSystem.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FeedbackSystem.Tests.Services;

/// <summary>
/// FeedbackService 商業邏輯 Unit Test。
/// 以 NSubstitute Mock IFeedbackRepository，完全隔離 DB / SP 呼叫。
/// 測試重點：GenerateUniqueTrackingCodeAsync 的 while 迴圈重試邏輯、
///           委派方法是否正確傳遞參數與回傳值、以及例外向上傳遞的行為。
/// </summary>
public class FeedbackServiceTests
{
    // ──────────────────────────────────────────
    // 欄位宣告：Mock Repository 介面與 SUT
    // ──────────────────────────────────────────

    // Mock IFeedbackRepository，所有 SP 呼叫皆由此替換
    private readonly IFeedbackRepository _mockRepository;

    // 被測試的真實 FeedbackService（注入 Mock Repository）
    private readonly FeedbackService _sut;

    // ──────────────────────────────────────────
    // 建構子：每個測試方法執行前初始化
    // ──────────────────────────────────────────
    public FeedbackServiceTests()
    {
        // 建立 NSubstitute Mock（僅支援介面，不支援密封類別）
        _mockRepository = Substitute.For<IFeedbackRepository>();

        // 注入 Mock，讓 Service 執行真實商業邏輯，但 DB 呼叫由 Mock 接管
        _sut = new FeedbackService(_mockRepository);
    }

    // ══════════════════════════════════════════
    // GenerateUniqueTrackingCodeAsync
    // 測試重點：while 迴圈的重試邏輯、代碼格式正確性
    // ══════════════════════════════════════════

    /// <summary>
    /// Happy Path：Repository 第一次就回傳「不重複」（false），
    /// 應直接回傳格式正確的唯一代碼
    /// </summary>
    [Fact]
    public async Task GenerateUniqueTrackingCodeAsync_WhenFirstAttemptIsUnique_ReturnsFormattedCode()
    {
        // Arrange：CheckTrackingCodeExistsAsync 每次都回傳 false（代碼不存在，可使用）
        _mockRepository
            .CheckTrackingCodeExistsAsync(Arg.Any<string>())
            .Returns(false);

        // Act：呼叫真實 Service 方法（實際執行 while 迴圈）
        var code = await _sut.GenerateUniqueTrackingCodeAsync();

        // Assert 1：格式必須符合 FB + 8 碼日期 + 6 碼大寫英數（排除 O/0/I/1）
        Assert.Matches(@"^FB\d{8}[A-HJ-NP-Z2-9]{6}$", code);

        // Assert 2：Repository 應只被呼叫一次（第一次就成功不需重試）
        _mockRepository.Received(1).CheckTrackingCodeExistsAsync(Arg.Any<string>());
    }

    /// <summary>
    /// 邊界條件：前兩次發生碰撞（已存在），第三次才成功。
    /// 測試 while 迴圈能否正確重試並最終回傳唯一代碼。
    /// </summary>
    [Fact]
    public async Task GenerateUniqueTrackingCodeAsync_WhenTwoCollisions_RetriesAndSucceeds()
    {
        // Arrange：用 Queue 模擬「前兩次衝突，第三次可用」的情境
        // true=代碼已存在（需重試），false=代碼可用（可回傳）
        var callQueue = new Queue<bool>([true, true, false]);

        _mockRepository
            .CheckTrackingCodeExistsAsync(Arg.Any<string>())
            // 每次呼叫從 Queue 取出下一個回傳值
            .Returns(_ => callQueue.Dequeue());

        // Act
        var code = await _sut.GenerateUniqueTrackingCodeAsync();

        // Assert 1：最終仍回傳有效的唯一代碼
        Assert.NotEmpty(code);
        Assert.StartsWith("FB", code);
        Assert.Equal(16, code.Length);  // FB(2) + 日期(8) + 亂數(6) = 16 碼

        // Assert 2：Repository 應被呼叫三次（兩次碰撞 + 一次成功）
        _mockRepository.Received(3).CheckTrackingCodeExistsAsync(Arg.Any<string>());
    }

    /// <summary>
    /// Sad Path：Repository 拋出例外時（DB 連線失敗），
    /// Service 不應吞掉例外，應讓它向上傳遞給 Controller
    /// </summary>
    [Fact]
    public async Task GenerateUniqueTrackingCodeAsync_WhenRepositoryThrows_PropagatesException()
    {
        // Arrange：讓 Repository 拋出例外，模擬 DB 連線中斷
        _mockRepository
            .CheckTrackingCodeExistsAsync(Arg.Any<string>())
            .ThrowsAsync(new InvalidOperationException("DB 連線中斷"));

        // Act & Assert：Service 不應吞掉例外，應讓它向上傳遞
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.GenerateUniqueTrackingCodeAsync());
    }

    // ══════════════════════════════════════════
    // CheckTrackingCodeExistsAsync
    // 測試重點：Service 正確委派並原封傳回 Repository 結果
    // ══════════════════════════════════════════

    /// <summary>
    /// Happy Path：Repository 回傳 true 時，Service 應原封傳回 true
    /// </summary>
    [Theory]
    [InlineData(true)]    // 代碼已存在
    [InlineData(false)]   // 代碼不存在
    public async Task CheckTrackingCodeExistsAsync_ReturnsRepositoryResult(bool exists)
    {
        // Arrange：設定 Mock 回傳指定值
        const string code = "FB20260504ABCDEF";
        _mockRepository
            .CheckTrackingCodeExistsAsync(code)
            .Returns(exists);

        // Act
        var result = await _sut.CheckTrackingCodeExistsAsync(code);

        // Assert：Service 應原封傳回 Repository 結果（不應修改）
        Assert.Equal(exists, result);
    }

    // ══════════════════════════════════════════
    // InsertFeedbackAsync
    // 測試重點：Service 正確傳遞 Model 給 Repository，並原封傳回 ID
    // ══════════════════════════════════════════

    /// <summary>
    /// Happy Path：Repository 新增成功後回傳 Id，Service 應原封傳回（不應擅自修改）
    /// </summary>
    [Fact]
    public async Task InsertFeedbackAsync_WithValidModel_ReturnsRepositoryId()
    {
        // Arrange：建立要新增的 ViewModel
        var model = new FeedbackCreateViewModel
        {
            TrackingCode  = "FB20260504ABCDEF",
            CustomerName  = "測試客戶",
            CustomerEmail = "test@example.com",
            Category      = "產品",
            Subject       = "主旨",
            Content       = "內容"
        };

        // 設定 Mock：Repository 新增成功後回傳假 ID = 42
        _mockRepository
            .InsertFeedbackAsync(model)
            .Returns(42);

        // Act
        var newId = await _sut.InsertFeedbackAsync(model);

        // Assert：Service 應回傳 Repository 的結果（不應修改）
        Assert.Equal(42, newId);
    }

    /// <summary>
    /// Sad Path：Repository 拋出例外時，Service 應讓例外向上傳遞
    /// </summary>
    [Fact]
    public async Task InsertFeedbackAsync_WhenRepositoryThrows_PropagatesException()
    {
        // Arrange：設定 Mock 拋出例外
        var model = new FeedbackCreateViewModel { CustomerName = "測試" };
        _mockRepository
            .InsertFeedbackAsync(model)
            .ThrowsAsync(new InvalidOperationException("SP 執行失敗"));

        // Act & Assert：例外應向上傳遞
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.InsertFeedbackAsync(model));
    }

    // ══════════════════════════════════════════
    // GetByIdAsync
    // 測試重點：資料不存在時是否正確回傳 null
    // ══════════════════════════════════════════

    /// <summary>
    /// Happy Path：Repository 找到資料時，Service 應原封回傳 ViewModel
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_WhenFound_ReturnsViewModel()
    {
        // Arrange：準備假的詳情 ViewModel
        const int targetId = 7;
        var fakeDetail = new FeedbackDetailViewModel
        {
            FeedbackId   = targetId,
            CustomerName = "測試客戶"
        };
        _mockRepository.GetByIdAsync(targetId).Returns(fakeDetail);

        // Act
        var result = await _sut.GetByIdAsync(targetId);

        // Assert：應回傳 Repository 的結果
        Assert.NotNull(result);
        Assert.Equal(targetId, result!.FeedbackId);
    }

    /// <summary>
    /// Sad Path：Repository 回傳 null（資料不存在）時，Service 應直接回傳 null
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsNull()
    {
        // Arrange：設定 Mock 回傳 null，模擬資料不存在
        _mockRepository
            .GetByIdAsync(Arg.Any<int>())
            .Returns((FeedbackDetailViewModel?)null);

        // Act
        var result = await _sut.GetByIdAsync(99999);

        // Assert：應回傳 null
        Assert.Null(result);
    }

    // ══════════════════════════════════════════
    // GetPagedListAsync
    // 測試重點：Service 正確委派，並傳遞正確的篩選參數給 Repository
    // ══════════════════════════════════════════

    /// <summary>
    /// Happy Path：Service 應正確轉發分頁查詢給 Repository，並回傳結果
    /// </summary>
    [Fact]
    public async Task GetPagedListAsync_DelegatesToRepositoryAndReturnsResult()
    {
        // Arrange：準備假的分頁資料
        var fakeItems = new List<FeedbackListItemViewModel>
        {
            new() { FeedbackId = 1, CustomerName = "客戶A" }
        };
        const int totalCount = 1;

        // 設定 Mock：指定篩選條件下的回傳值
        _mockRepository
            .GetPagedListAsync(null, null, null, 1, 10)
            .Returns((fakeItems.AsEnumerable(), totalCount));

        // Act
        var (items, count) = await _sut.GetPagedListAsync(null, null, null, 1, 10);

        // Assert 1：確認結果筆數正確
        Assert.Single(items);
        Assert.Equal(totalCount, count);

        // Assert 2：確認 Repository 被呼叫並傳入正確的參數
        _mockRepository.Received(1).GetPagedListAsync(null, null, null, 1, 10);
    }

    // ══════════════════════════════════════════
    // UpdateFeedbackAsync
    // 測試重點：Service 正確委派 Update 給 Repository
    // ══════════════════════════════════════════

    /// <summary>
    /// Happy Path：更新成功時，Service 應正確委派給 Repository
    /// </summary>
    [Fact]
    public async Task UpdateFeedbackAsync_DelegatesToRepository()
    {
        // Arrange
        var model = new FeedbackEditViewModel
        {
            FeedbackId = 5,
            Category   = "服務",
            Subject    = "主旨",
            Content    = "內容",
            Status     = 1,
            Priority   = 2
        };
        _mockRepository.UpdateFeedbackAsync(model).Returns(Task.CompletedTask);

        // Act
        await _sut.UpdateFeedbackAsync(model);

        // Assert：確認 Repository 的 UpdateFeedbackAsync 被呼叫一次
        _mockRepository.Received(1).UpdateFeedbackAsync(model);
    }

    // ══════════════════════════════════════════
    // InsertReplyAsync
    // 測試重點：Service 正確委派 InsertReply 給 Repository
    // ══════════════════════════════════════════

    /// <summary>
    /// Happy Path：新增回覆成功時，Service 應正確委派給 Repository
    /// </summary>
    [Fact]
    public async Task InsertReplyAsync_DelegatesToRepository()
    {
        // Arrange
        var model = new FeedbackReplyCreateViewModel
        {
            FeedbackId  = 10,
            Content     = "回覆內容",
            ReplierName = "客服",
            ReplyType   = 1,
            IsPublic    = true
        };
        _mockRepository.InsertReplyAsync(model).Returns(Task.CompletedTask);

        // Act
        await _sut.InsertReplyAsync(model);

        // Assert：確認 Repository 的 InsertReplyAsync 被呼叫一次
        _mockRepository.Received(1).InsertReplyAsync(model);
    }
}
