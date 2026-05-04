namespace FeedbackSystem.Models.ViewModels;

/// <summary>
/// 意見詳情頁 ViewModel（所有欄位唯讀顯示）
/// 同時作為 Detail 頁面載入的主要資料模型，
/// 包含意見主體資訊與對應的回覆串清單
/// </summary>
public class FeedbackDetailViewModel
{
    // ─── 意見主體欄位 ─────────────────────────────────

    /// <summary>意見唯一識別碼</summary>
    public int FeedbackId { get; set; }

    /// <summary>意見追蹤代碼</summary>
    public string TrackingCode { get; set; } = string.Empty;

    /// <summary>客戶姓名</summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>客戶電子信箱</summary>
    public string CustomerEmail { get; set; } = string.Empty;

    /// <summary>客戶聯絡電話（可為 null）</summary>
    public string? CustomerPhone { get; set; }

    /// <summary>意見類別</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>意見主旨</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>意見詳細內容</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 處理狀態（0=待處理、1=處理中、2=已回覆、3=已關閉）
    /// </summary>
    public byte Status { get; set; }

    /// <summary>
    /// 優先等級（1=一般、2=重要、3=緊急）
    /// </summary>
    public byte Priority { get; set; }

    /// <summary>管理員內部備註（僅後台顯示，不對客戶公開）</summary>
    public string? AdminNote { get; set; }

    /// <summary>最新回覆內容快取（非正規化，由系統自動同步）</summary>
    public string? LatestReplyContent { get; set; }

    /// <summary>最新回覆時間快取（非正規化，由系統自動同步）</summary>
    public DateTime? LatestReplyAt { get; set; }

    /// <summary>累積回覆筆數</summary>
    public int ReplyCount { get; set; }

    /// <summary>意見建立時間（資料庫自動填入，僅顯示）</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>最後更新時間（資料庫自動填入，僅顯示）</summary>
    public DateTime UpdatedAt { get; set; }

    // ─── 回覆串 ───────────────────────────────────────

    /// <summary>
    /// 此意見對應的所有回覆清單，由 usp_FeedbackReply_GetByFeedbackId 取得
    /// 後台顯示全部（含 IsPublic=0 私密回覆）
    /// </summary>
    public List<FeedbackReplyViewModel> Replies { get; set; } = new();

    // ─── 新增回覆表單（嵌入於詳情頁底部） ─────────────

    /// <summary>
    /// 新增回覆的表單資料，供 Detail 頁底部快速回覆表單使用
    /// 預設帶入 FeedbackId，其餘欄位由使用者填寫
    /// </summary>
    public FeedbackReplyCreateViewModel NewReply { get; set; } = new();
}
