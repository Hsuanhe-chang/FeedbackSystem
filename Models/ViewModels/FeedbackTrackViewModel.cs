using System.ComponentModel.DataAnnotations;

namespace FeedbackSystem.Models.ViewModels;

/// <summary>
/// 客戶追蹤查詢頁面 ViewModel。
/// 同時承載：
///   ① 查詢表單輸入（TrackingCode）
///   ② 查詢完成後的結果資料（Result）
///   ③ 查詢後找不到資料的旗標（NotFound）
///
/// 設計說明：
///   - GET /Feedback/Track → 建立空白 ViewModel（Result=null, NotFound=false）
///   - POST /Feedback/Track，找到資料 → Result 被填入，NotFound=false
///   - POST /Feedback/Track，找不到資料 → Result=null, NotFound=true
/// </summary>
public class FeedbackTrackViewModel
{
    /// <summary>
    /// 客戶輸入的追蹤代碼。
    /// 格式：FB + yyyyMMdd + 6碼大寫亂數（例如 FB20260504A3F9K2）。
    /// 長度限制 20 字元符合 DB 欄位 nvarchar(20)。
    /// </summary>
    [Required(ErrorMessage = "請輸入追蹤代碼")]
    [StringLength(20, ErrorMessage = "追蹤代碼不得超過 20 個字元")]
    [Display(Name = "追蹤代碼")]
    public string TrackingCode { get; set; } = string.Empty;

    /// <summary>
    /// 查詢成功後填入的公開結果資料。
    /// null 表示尚未查詢，或查詢後找不到資料（請搭配 NotFound 判斷）。
    /// </summary>
    public FeedbackTrackResultViewModel? Result { get; set; }

    /// <summary>
    /// true = 已執行查詢但找不到對應的意見（TrackingCode 不存在或已關閉）。
    /// false（預設值）= 尚未查詢，或查詢成功。
    /// View 依此旗標決定是否顯示「找不到意見」的警示訊息。
    /// </summary>
    public bool NotFound { get; set; }
}
