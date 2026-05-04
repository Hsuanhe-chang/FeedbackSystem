using FeedbackSystem.Models.ViewModels;

namespace FeedbackSystem.Repositories;

/// <summary>
/// 資料存取層介面，封裝所有對 Stored Procedure 的直接呼叫。
/// 透過此介面隔離 DB 依賴，Unit Test 中以 NSubstitute Mock 替換，
/// 讓 FeedbackService 的商業邏輯可以被獨立測試。
/// </summary>
public interface IFeedbackRepository
{
    /// <summary>
    /// 取得分頁意見列表（usp_Feedback_GetPagedList）
    /// 支援依 Status、Priority 篩選
    /// </summary>
    /// <param name="status">處理狀態篩選（null=全部，0=待處理，1=處理中，2=已回覆，3=已關閉）</param>
    /// <param name="priority">優先等級篩選（null=全部，1=一般，2=重要，3=緊急）</param>
    /// <param name="page">目前頁碼（從 1 開始）</param>
    /// <param name="pageSize">每頁筆數</param>
    /// <returns>當頁資料集合 與 總筆數（用於計算分頁）</returns>
    Task<(IEnumerable<FeedbackListItemViewModel> Items, int TotalCount)> GetPagedListAsync(
        byte? status, byte? priority, int page, int pageSize);

    /// <summary>
    /// 確認 TrackingCode 是否已存在於資料庫（usp_Feedback_CheckTrackingCodeExists）
    /// </summary>
    /// <param name="trackingCode">待確認的追蹤代碼</param>
    /// <returns>true=已存在；false=尚未使用</returns>
    Task<bool> CheckTrackingCodeExistsAsync(string trackingCode);

    /// <summary>
    /// 新增一筆意見至資料庫（usp_Feedback_Insert）
    /// </summary>
    /// <param name="model">前台新增表單 ViewModel</param>
    /// <returns>資料庫自動產生的新 FeedbackId</returns>
    Task<int> InsertFeedbackAsync(FeedbackCreateViewModel model);

    /// <summary>
    /// 依 FeedbackId 取得單筆意見完整資料（usp_Feedback_GetById）
    /// </summary>
    /// <param name="feedbackId">意見識別碼</param>
    /// <returns>意見詳情 ViewModel；若不存在則回傳 null</returns>
    Task<FeedbackDetailViewModel?> GetByIdAsync(int feedbackId);

    /// <summary>
    /// 取得指定意見的所有回覆串（usp_FeedbackReply_GetByFeedbackId）
    /// </summary>
    /// <param name="feedbackId">意見識別碼</param>
    /// <returns>回覆 ViewModel 集合</returns>
    Task<IEnumerable<FeedbackReplyViewModel>> GetRepliesByFeedbackIdAsync(int feedbackId);

    /// <summary>
    /// 更新意見可編輯欄位（usp_Feedback_Update）
    /// </summary>
    /// <param name="model">後台編輯表單 ViewModel</param>
    Task UpdateFeedbackAsync(FeedbackEditViewModel model);

    /// <summary>
    /// 新增回覆並同步更新快取欄位（usp_FeedbackReply_Insert）
    /// SP 內部以 Transaction 確保資料一致性
    /// </summary>
    /// <param name="model">新增回覆表單 ViewModel</param>
    Task InsertReplyAsync(FeedbackReplyCreateViewModel model);
}
