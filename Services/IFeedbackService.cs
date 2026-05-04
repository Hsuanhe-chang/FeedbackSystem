using FeedbackSystem.Models.ViewModels;

namespace FeedbackSystem.Services;

/// <summary>
/// FeedbackService 的服務介面
/// 定義所有與意見回饋相關的資料操作方法
/// 所有方法皆為非同步，全部透過 Stored Procedure 與資料庫溝通
/// </summary>
public interface IFeedbackService
{
    /// <summary>
    /// 取得分頁意見列表（對應 usp_Feedback_GetPagedList）
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
    /// 確認 TrackingCode 是否已存在（對應 usp_Feedback_CheckTrackingCodeExists）
    /// </summary>
    /// <param name="trackingCode">待確認的追蹤代碼</param>
    /// <returns>true=已存在；false=尚未使用</returns>
    Task<bool> CheckTrackingCodeExistsAsync(string trackingCode);

    /// <summary>
    /// 產生唯一 TrackingCode（反覆呼叫 CheckTrackingCodeExistsAsync 直到不重複）
    /// 格式：FB + yyyyMMdd + 6 碼大寫亂數（例如 FB20260504A3F9K2）
    /// </summary>
    /// <returns>唯一的追蹤代碼字串</returns>
    Task<string> GenerateUniqueTrackingCodeAsync();

    /// <summary>
    /// 新增一筆意見（對應 usp_Feedback_Insert）
    /// </summary>
    /// <param name="model">前台新增表單 ViewModel</param>
    /// <returns>資料庫自動產生的新 FeedbackId</returns>
    Task<int> InsertFeedbackAsync(FeedbackCreateViewModel model);

    /// <summary>
    /// 依 FeedbackId 取得單筆意見完整資料（對應 usp_Feedback_GetById）
    /// 同時包含唯讀顯示欄位，但不含回覆串（回覆串由 GetRepliesByFeedbackIdAsync 取得）
    /// </summary>
    /// <param name="feedbackId">意見識別碼</param>
    /// <returns>意見詳情 ViewModel；若不存在則回傳 null</returns>
    Task<FeedbackDetailViewModel?> GetByIdAsync(int feedbackId);

    /// <summary>
    /// 取得指定意見的所有回覆串（對應 usp_FeedbackReply_GetByFeedbackId）
    /// 後台顯示全部，包含 IsPublic=false 的私密回覆
    /// </summary>
    /// <param name="feedbackId">意見識別碼</param>
    /// <returns>回覆 ViewModel 集合</returns>
    Task<IEnumerable<FeedbackReplyViewModel>> GetRepliesByFeedbackIdAsync(int feedbackId);

    /// <summary>
    /// 更新意見可編輯欄位（對應 usp_Feedback_Update）
    /// 同步更新 UpdatedAt
    /// </summary>
    /// <param name="model">後台編輯表單 ViewModel</param>
    Task UpdateFeedbackAsync(FeedbackEditViewModel model);

    /// <summary>
    /// 新增回覆並同步更新快取欄位（對應 usp_FeedbackReply_Insert）
    /// SP 內部以 Transaction 確保：
    ///   ① INSERT FeedbackReply
    ///   ② UPDATE Feedback.LatestReplyContent / LatestReplyAt / ReplyCount+1
    ///   ③ 若 ReplyType=1（官方）且 Feedback.Status=0（待處理）→ Status 改為 2（已回覆）
    /// </summary>
    /// <param name="model">新增回覆表單 ViewModel</param>
    Task InsertReplyAsync(FeedbackReplyCreateViewModel model);
}
