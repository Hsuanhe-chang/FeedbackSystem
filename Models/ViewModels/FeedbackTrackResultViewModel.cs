namespace FeedbackSystem.Models.ViewModels;

/// <summary>
/// 客戶追蹤查詢結果 ViewModel（公開頁面專用）。
/// 僅包含可對客戶公開的欄位：
///   ✅ TrackingCode、CustomerName、Category、Subject、Content、Status、ReplyCount、LatestReplyAt、CreatedAt
///   ❌ 不包含 AdminNote（內部備註）、CustomerEmail / CustomerPhone（個資）、Priority（內部管理）
/// 回覆串只顯示 IsPublic = true 的公開回覆，私密回覆由 Service 過濾。
/// </summary>
public class FeedbackTrackResultViewModel
{
    // ─── 意見基本資訊（可公開欄位） ────────────────────────────────────

    /// <summary>意見追蹤代碼（客戶持有，用於查詢進度）</summary>
    public string TrackingCode { get; set; } = string.Empty;

    /// <summary>客戶姓名（用於打招呼確認身分）</summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>意見類別（產品 / 服務 / 建議 / 其他）</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>意見主旨</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>意見詳細內容</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 處理狀態（0=待處理、1=處理中、2=已回覆、3=已關閉）
    /// View 中轉換為中文標籤與 badge 顏色
    /// </summary>
    public byte Status { get; set; }

    /// <summary>累積回覆筆數（非正規化快取，由系統維護）</summary>
    public int ReplyCount { get; set; }

    /// <summary>最新回覆時間（公開回覆中最新的時間點）</summary>
    public DateTime? LatestReplyAt { get; set; }

    /// <summary>意見建立時間</summary>
    public DateTime CreatedAt { get; set; }

    // ─── 公開回覆串（IsPublic = true 的回覆，已由 Service 過濾） ────────

    /// <summary>
    /// 此意見的所有公開回覆清單（IsPublic = true），
    /// 包含客戶追加回覆（ReplyType=0）與官方回覆（ReplyType=1）。
    /// 私密回覆（IsPublic=false）已由 FeedbackService 在回傳前過濾掉。
    /// </summary>
    public List<FeedbackReplyViewModel> PublicReplies { get; set; } = new();
}
