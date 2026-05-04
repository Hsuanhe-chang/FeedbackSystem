using System.ComponentModel.DataAnnotations;

namespace FeedbackSystem.Models.ViewModels;

/// <summary>
/// 前台「提交意見」表單的 ViewModel
/// 不包含 Status、Priority 等系統欄位（由 DB 預設值決定）
/// 不包含 FeedbackId、CreatedAt 等系統管理欄位
/// </summary>
public class FeedbackCreateViewModel
{
    /// <summary>
    /// 追蹤代碼（由後端自動產生後帶入 hidden input + 唯讀顯示）
    /// 格式：FB + yyyyMMdd + 6碼大寫亂數（例如 FB20260504A3F9K2）
    /// 不允許使用者自行輸入
    /// </summary>
    [Required(ErrorMessage = "追蹤代碼為必填")]
    [StringLength(20, ErrorMessage = "追蹤代碼不得超過 20 個字元")]
    public string TrackingCode { get; set; } = string.Empty;

    /// <summary>客戶姓名（必填，最多 100 字）</summary>
    [Required(ErrorMessage = "客戶姓名為必填")]
    [StringLength(100, ErrorMessage = "姓名不得超過 100 個字元")]
    [Display(Name = "客戶姓名")]
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>客戶電子信箱（必填，需符合 Email 格式，最多 200 字）</summary>
    [Required(ErrorMessage = "電子信箱為必填")]
    [StringLength(200, ErrorMessage = "電子信箱不得超過 200 個字元")]
    [EmailAddress(ErrorMessage = "請輸入有效的電子信箱格式")]
    [Display(Name = "電子信箱")]
    public string CustomerEmail { get; set; } = string.Empty;

    /// <summary>
    /// 客戶聯絡電話（選填，nullable）
    /// 若填寫，僅接受純數字（不需輸入「-」）、最多 30 字。
    /// 支援市話格式（純數字）：0212345678、0422123456
    /// 支援手機格式（純數字）：0912345678
    /// 注意：[Phone] 屬性不產生 jQuery Validate 客戶端規則，
    ///       改用 [RegularExpression] 確保前後端驗證皆生效。
    /// </summary>
    [StringLength(30, ErrorMessage = "聯絡電話不得超過 30 個字元")]
    [RegularExpression(
        // 市話：0[2-8] 開頭，共 8~10 碼純數字
        // 手機：09 開頭，共 10 碼純數字
        @"^(0[2-8]\d{6,8}|09\d{8})$",
        ErrorMessage = "請輸入純數字電話號碼（市話如 0212345678，手機如 0912345678）")]
    [Display(Name = "聯絡電話")]
    public string? CustomerPhone { get; set; }

    /// <summary>
    /// 意見類別（必填，下拉靜態清單：產品 / 服務 / 建議 / 其他）
    /// 最多 50 字
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

    /// <summary>意見詳細內容（必填，長文字不限長度）</summary>
    [Required(ErrorMessage = "意見內容為必填")]
    [Display(Name = "意見內容")]
    public string Content { get; set; } = string.Empty;
}
