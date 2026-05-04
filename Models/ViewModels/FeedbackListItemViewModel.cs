namespace FeedbackSystem.Models.ViewModels;

/// <summary>
/// 意見列表頁每一列的顯示資料 ViewModel
/// 對應 usp_Feedback_GetPagedList 回傳的欄位集合
/// </summary>
public class FeedbackListItemViewModel
{
    /// <summary>意見唯一識別碼（用於導向詳情／編輯頁的路由參數）</summary>
    public int FeedbackId { get; set; }

    /// <summary>意見追蹤代碼（格式：FB + yyyyMMdd + 6碼大寫亂數）</summary>
    public string TrackingCode { get; set; } = string.Empty;

    /// <summary>客戶姓名</summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>意見類別（產品 / 服務 / 建議 / 其他）</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>意見主旨</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// 處理狀態（0=待處理、1=處理中、2=已回覆、3=已關閉）
    /// 對應 DB tinyint，View 中轉換為中文標籤與 badge 顏色
    /// </summary>
    public byte Status { get; set; }

    /// <summary>
    /// 優先等級（1=一般、2=重要、3=緊急）
    /// 對應 DB tinyint，View 中轉換為顏色 badge
    /// </summary>
    public byte Priority { get; set; }

    /// <summary>累積回覆筆數（非正規化快取，由系統維護）</summary>
    public int ReplyCount { get; set; }

    /// <summary>意見建立時間（由資料庫自動填入，此處僅顯示）</summary>
    public DateTime CreatedAt { get; set; }
}
