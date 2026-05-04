using System.ComponentModel.DataAnnotations;

namespace FeedbackSystem.Models.ViewModels;

/// <summary>
/// 新增回覆表單 ViewModel
/// 嵌入於 Detail 頁底部的快速回覆區塊
/// FeedbackId 由隱藏欄位傳遞，CreatedAt 由資料庫自動填入不在此 ViewModel
/// </summary>
public class FeedbackReplyCreateViewModel
{
    /// <summary>
    /// 所屬意見識別碼（由隱藏欄位傳遞，不顯示於表單）
    /// 確保回覆正確對應到指定意見
    /// </summary>
    [Required]
    public int FeedbackId { get; set; }

    /// <summary>回覆詳細內容（必填，長文字不限長度）</summary>
    [Required(ErrorMessage = "回覆內容為必填")]
    [Display(Name = "回覆內容")]
    public string Content { get; set; } = string.Empty;

    /// <summary>回覆者姓名（必填，最多 100 字）</summary>
    [Required(ErrorMessage = "回覆者姓名為必填")]
    [StringLength(100, ErrorMessage = "回覆者姓名不得超過 100 個字元")]
    [Display(Name = "回覆者姓名")]
    public string ReplierName { get; set; } = string.Empty;

    /// <summary>
    /// 回覆類型（必填，0=客戶回覆、1=官方回覆）
    /// 使用 byte 對應 DB tinyint，Range 限制允許值
    /// </summary>
    [Required(ErrorMessage = "請選擇回覆類型")]
    [Range(0, 1, ErrorMessage = "回覆類型值必須為 0 或 1")]
    [Display(Name = "回覆類型")]
    public byte ReplyType { get; set; } = 1; // 預設官方回覆

    /// <summary>
    /// 是否公開顯示（true=公開、false=私密僅後台可見）
    /// 預設 true（公開）
    /// </summary>
    [Display(Name = "公開顯示")]
    public bool IsPublic { get; set; } = true;
}
