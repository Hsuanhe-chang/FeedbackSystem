using System.ComponentModel.DataAnnotations;

namespace FeedbackSystem.Models.ViewModels;

/// <summary>
/// 後台「編輯意見」表單 ViewModel
/// 僅包含後台管理員可修改的欄位
/// 識別欄位（TrackingCode, CustomerName 等）唯讀顯示，不作為表單 input
/// </summary>
public class FeedbackEditViewModel
{
    // ─── 隱藏傳遞（路由識別用）────────────────────────

    /// <summary>意見唯一識別碼（hidden input 傳遞，不顯示於表單）</summary>
    [Required]
    public int FeedbackId { get; set; }

    // ─── 唯讀顯示欄位（非 input，僅供頁面顯示參考）──────

    /// <summary>追蹤代碼（唯讀顯示，不可修改）</summary>
    public string TrackingCode { get; set; } = string.Empty;

    /// <summary>客戶姓名（唯讀顯示，不可修改）</summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>客戶電子信箱（唯讀顯示，不可修改）</summary>
    public string CustomerEmail { get; set; } = string.Empty;

    /// <summary>建立時間（唯讀顯示，不可修改）</summary>
    public DateTime CreatedAt { get; set; }

    // ─── 可編輯欄位 ───────────────────────────────────

    /// <summary>
    /// 意見類別（下拉選單：產品 / 服務 / 建議 / 其他）
    /// </summary>
    [Required(ErrorMessage = "請選擇意見類別")]
    [StringLength(50, ErrorMessage = "類別不得超過 50 個字元")]
    [Display(Name = "意見類別")]
    public string Category { get; set; } = string.Empty;

    /// <summary>意見主旨（必填，最多 200 字）</summary>
    [Required(ErrorMessage = "意見主旨為必填")]
    [StringLength(200, ErrorMessage = "主旨不得超過 200 個字元")]
    [Display(Name = "意見主旨")]
    public string Subject { get; set; } = string.Empty;

    /// <summary>意見詳細內容（必填）</summary>
    [Required(ErrorMessage = "意見內容為必填")]
    [Display(Name = "意見內容")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 處理狀態（必填，0=待處理、1=處理中、2=已回覆、3=已關閉）
    /// 使用 byte 對應 DB tinyint，Range 限制允許值
    /// </summary>
    [Required(ErrorMessage = "請選擇處理狀態")]
    [Range(0, 3, ErrorMessage = "狀態值必須介於 0 到 3 之間")]
    [Display(Name = "處理狀態")]
    public byte Status { get; set; }

    /// <summary>
    /// 優先等級（必填，1=一般、2=重要、3=緊急）
    /// 使用 byte 對應 DB tinyint，Range 限制允許值
    /// </summary>
    [Required(ErrorMessage = "請選擇優先等級")]
    [Range(1, 3, ErrorMessage = "優先等級值必須介於 1 到 3 之間")]
    [Display(Name = "優先等級")]
    public byte Priority { get; set; }

    /// <summary>管理員內部備註（選填，不對客戶顯示）</summary>
    [Display(Name = "管理員備註")]
    public string? AdminNote { get; set; }
}
