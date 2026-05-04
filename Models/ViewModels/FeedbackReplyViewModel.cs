namespace FeedbackSystem.Models.ViewModels;

/// <summary>
/// 單筆回覆的顯示 ViewModel
/// 對應 usp_FeedbackReply_GetByFeedbackId 回傳的欄位集合
/// 供 Detail 頁面的回覆串列表使用
/// </summary>
public class FeedbackReplyViewModel
{
    /// <summary>回覆唯一識別碼</summary>
    public int ReplyId { get; set; }

    /// <summary>所屬意見的識別碼</summary>
    public int FeedbackId { get; set; }

    /// <summary>回覆詳細內容</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>回覆者姓名（客戶或管理員）</summary>
    public string ReplierName { get; set; } = string.Empty;

    /// <summary>
    /// 回覆類型（0=客戶回覆、1=官方回覆）
    /// View 中依此值套用不同的 UI 樣式（左對齊灰底 vs 右對齊藍底）
    /// </summary>
    public byte ReplyType { get; set; }

    /// <summary>
    /// 是否公開顯示（true=公開、false=私密，僅後台可見）
    /// View 中 IsPublic=false 時顯示「私密」badge
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>回覆建立時間（資料庫自動填入，僅顯示）</summary>
    public DateTime CreatedAt { get; set; }
}
