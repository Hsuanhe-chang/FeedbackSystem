using FeedbackSystem.Models.ViewModels;
using FeedbackSystem.Repositories;

namespace FeedbackSystem.Services;

/// <summary>
/// FeedbackService 的商業邏輯實作。
/// 透過注入 IFeedbackRepository 隔離 DB 依賴，
/// 本類別專注於商業邏輯（如唯一碼產生重試迴圈），
/// 所有 SP 呼叫委派給 Repository 執行。
/// </summary>
public class FeedbackService : IFeedbackService
{
    // 注入 Repository 介面，由 DI 容器提供 FeedbackRepository 實作
    // Unit Test 中以 NSubstitute Mock 替換，完全隔離 DB 依賴
    private readonly IFeedbackRepository _repository;

    /// <summary>
    /// 建構子：透過 DI 注入 IFeedbackRepository
    /// </summary>
    /// <param name="repository">資料存取層實作（生產環境為 FeedbackRepository，測試環境為 Mock）</param>
    public FeedbackService(IFeedbackRepository repository)
    {
        _repository = repository;
    }

    // ─────────────────────────────────────────────────────────────────
    // ─────────────────────────────────────────────────────────────────
    // 1. 取得分頁列表：直接委派 Repository
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task<(IEnumerable<FeedbackListItemViewModel> Items, int TotalCount)> GetPagedListAsync(
        byte? status, byte? priority, string? keyword, int page, int pageSize)
        // 無商業邏輯，直接轉給 Repository 執行 SP
        => _repository.GetPagedListAsync(status, priority, keyword, page, pageSize);

    // ─────────────────────────────────────────────────────────────────
    // 2. 確認 TrackingCode：直接委派 Repository
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task<bool> CheckTrackingCodeExistsAsync(string trackingCode)
        // 直接轉給 Repository 查詢 DB
        => _repository.CheckTrackingCodeExistsAsync(trackingCode);

    // ─────────────────────────────────────────────────────────────────
    // 3. 產生唯一 TrackingCode：商業邏輯（while 迴圈重試）在此
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<string> GenerateUniqueTrackingCodeAsync()
    {
        // 以迴圈持續產生候選碼，直到確認不重複為止
        // 此迴圈邏輯是本 Service 的核心商業邏輯，也是 Unit Test 的重點
        while (true)
        {
            // 格式：FB + yyyyMMdd + 6 碼大寫亂數英數字（避免易混淆字元）
            string candidate = "FB"
                + DateTime.Now.ToString("yyyyMMdd")
                + GenerateRandomUpperCode(6);

            // 委派 Repository 查詢 DB，確認此代碼是否已存在（Unit Test 中此呼叫被 Mock 替換）
            bool exists = await _repository.CheckTrackingCodeExistsAsync(candidate);

            // 若不存在則回傳；若已存在則繼續迴圈重試
            if (!exists)
                return candidate;
        }
    }

    /// <summary>
    /// 產生指定長度的大寫英數字亂數字串，用於組成 TrackingCode 的後六碼。
    /// 排除易混淆字元（O、0、I、1）確保追蹤碼易讀。
    /// </summary>
    /// <param name="length">字串長度（通常為 6）</param>
    private static string GenerateRandomUpperCode(int length)
    {
        // 可用字元集：大寫英文 + 數字，排除 O/0/I/1 避免閱讀混淆
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    // ─────────────────────────────────────────────────────────────────
    // 4. 新增意見：直接委派 Repository
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task<int> InsertFeedbackAsync(FeedbackCreateViewModel model)
        => _repository.InsertFeedbackAsync(model);

    // ─────────────────────────────────────────────────────────────────
    // 5. 取得單筆意見：直接委派 Repository
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task<FeedbackDetailViewModel?> GetByIdAsync(int feedbackId)
        => _repository.GetByIdAsync(feedbackId);

    // ─────────────────────────────────────────────────────────────────
    // 6. 取得回覆串：直接委派 Repository
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task<IEnumerable<FeedbackReplyViewModel>> GetRepliesByFeedbackIdAsync(int feedbackId)
        => _repository.GetRepliesByFeedbackIdAsync(feedbackId);

    // ─────────────────────────────────────────────────────────────────
    // 7. 更新意見：直接委派 Repository
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task UpdateFeedbackAsync(FeedbackEditViewModel model)
        => _repository.UpdateFeedbackAsync(model);

    // ─────────────────────────────────────────────────────────────────
    // 8. 新增回覆：直接委派 Repository
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task InsertReplyAsync(FeedbackReplyCreateViewModel model)
        => _repository.InsertReplyAsync(model);
}
