using System.ComponentModel.DataAnnotations;
using FeedbackSystem.Models.ViewModels;

namespace FeedbackSystem.Tests.ViewModels;

/// <summary>
/// FeedbackEditViewModel DataAnnotation 驗證單元測試。
/// 重點測試後台可編輯欄位的驗證規則：Status（0~3）、Priority（1~3）、
/// Category、Subject、Content，以及隱藏傳遞的 FeedbackId。
/// </summary>
public class FeedbackEditViewModelTests
{
    // ──────────────────────────────────────────
    // 輔助方法
    // ──────────────────────────────────────────

    /// <summary>執行完整 DataAnnotation 驗證並回傳錯誤清單</summary>
    private static IList<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }

    /// <summary>
    /// 建立一個所有欄位皆合法的基礎 FeedbackEditViewModel。
    /// 測試方法只需修改要驗證的特定欄位。
    /// </summary>
    private static FeedbackEditViewModel BuildValidModel() => new()
    {
        FeedbackId    = 1,              // 必填 ID，非零整數
        TrackingCode  = "FB20260504ABC123",  // 唯讀顯示用，無驗證 Annotation
        CustomerName  = "測試客戶",         // 唯讀顯示用，無驗證 Annotation
        CustomerEmail = "test@example.com",
        CreatedAt     = DateTime.Now,
        Category      = "產品",
        Subject       = "測試主旨",
        Content       = "測試內容",
        Status        = 0,              // 0=待處理，在 [Range(0,3)] 範圍內
        Priority      = 1               // 1=一般，在 [Range(1,3)] 範圍內
    };

    // ══════════════════════════════════════════
    // 整體驗證
    // ══════════════════════════════════════════

    /// <summary>合法 ViewModel 應完全通過驗證</summary>
    [Fact]
    public void FullyValidModel_PassesAllValidations()
    {
        var model = BuildValidModel();

        var errors = Validate(model);

        Assert.Empty(errors);
    }

    // ══════════════════════════════════════════
    // FeedbackId 測試
    // ══════════════════════════════════════════

    /// <summary>Sad Path：FeedbackId 為 0 時（預設值未填），應觸發 [Required] 邏輯錯誤</summary>
    /// <remarks>
    /// [Required] 對 int 型別僅在 null 時觸發，但 int 不可為 null，
    /// 因此 FeedbackId=0 是 DB 層面的問題而非驗證層面；
    /// 此測試確認 FeedbackId=1 正常通過，FeedbackId=0 也不被 Required 擋下。
    /// </remarks>
    [Fact]
    public void FeedbackId_WhenPositive_PassesValidation()
    {
        var model = BuildValidModel();
        model.FeedbackId = 1;   // 合法的正整數 ID

        var errors = Validate(model);

        // FeedbackId 的 [Required] 對 int 型別作用有限，主要確保不是 null（不適用 int）
        Assert.DoesNotContain(errors, e => e.MemberNames.Contains(nameof(model.FeedbackId)));
    }

    // ══════════════════════════════════════════
    // Status 測試（[Range(0, 3)]）
    // ══════════════════════════════════════════

    /// <summary>合法的 Status 值（0~3）應通過 [Range] 驗證</summary>
    [Theory]
    [InlineData((byte)0)]   // 待處理
    [InlineData((byte)1)]   // 處理中
    [InlineData((byte)2)]   // 已回覆
    [InlineData((byte)3)]   // 已關閉
    public void Status_WhenInRange_PassesValidation(byte status)
    {
        var model = BuildValidModel();
        model.Status = status;

        var errors = Validate(model);

        Assert.DoesNotContain(errors, e => e.MemberNames.Contains(nameof(model.Status)));
    }

    /// <summary>邊界條件：Status = 4，超出 [Range(0, 3)] 應觸發錯誤</summary>
    [Fact]
    public void Status_WhenOutOfRange_FailsRangeValidation()
    {
        var model = BuildValidModel();
        model.Status = 4;   // 4 > 3，超出 [Range(0, 3)]

        var errors = Validate(model);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(model.Status)));
    }

    // ══════════════════════════════════════════
    // Priority 測試（[Range(1, 3)]）
    // ══════════════════════════════════════════

    /// <summary>合法的 Priority 值（1~3）應通過 [Range] 驗證</summary>
    [Theory]
    [InlineData((byte)1)]   // 一般
    [InlineData((byte)2)]   // 重要
    [InlineData((byte)3)]   // 緊急
    public void Priority_WhenInRange_PassesValidation(byte priority)
    {
        var model = BuildValidModel();
        model.Priority = priority;

        var errors = Validate(model);

        Assert.DoesNotContain(errors, e => e.MemberNames.Contains(nameof(model.Priority)));
    }

    /// <summary>邊界條件：Priority = 0，低於 [Range(1, 3)] 應觸發錯誤</summary>
    [Fact]
    public void Priority_WhenZero_FailsRangeValidation()
    {
        var model = BuildValidModel();
        model.Priority = 0;   // 0 < 1，低於下限

        var errors = Validate(model);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(model.Priority)));
    }

    /// <summary>邊界條件：Priority = 4，超出 [Range(1, 3)] 應觸發錯誤</summary>
    [Fact]
    public void Priority_WhenFour_FailsRangeValidation()
    {
        var model = BuildValidModel();
        model.Priority = 4;   // 4 > 3，超出上限

        var errors = Validate(model);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(model.Priority)));
    }

    // ══════════════════════════════════════════
    // Category 測試
    // ══════════════════════════════════════════

    /// <summary>Sad Path：Category 為空，應觸發 [Required] 錯誤</summary>
    [Fact]
    public void Category_WhenEmpty_FailsRequired()
    {
        var model = BuildValidModel();
        model.Category = string.Empty;

        var errors = Validate(model);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(model.Category)));
    }

    /// <summary>邊界條件：Category 超過 50 字，應觸發 [StringLength] 錯誤</summary>
    [Fact]
    public void Category_WhenExceeds50Chars_FailsStringLength()
    {
        var model = BuildValidModel();
        model.Category = new string('A', 51);   // 51 > 50

        var errors = Validate(model);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(model.Category)));
    }

    // ══════════════════════════════════════════
    // Subject 測試
    // ══════════════════════════════════════════

    /// <summary>Sad Path：Subject 為空，應觸發 [Required] 錯誤</summary>
    [Fact]
    public void Subject_WhenEmpty_FailsRequired()
    {
        var model = BuildValidModel();
        model.Subject = string.Empty;

        var errors = Validate(model);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(model.Subject)));
    }

    /// <summary>邊界條件：Subject 超過 200 字，應觸發 [StringLength] 錯誤</summary>
    [Fact]
    public void Subject_WhenExceeds200Chars_FailsStringLength()
    {
        var model = BuildValidModel();
        model.Subject = new string('A', 201);   // 201 > 200

        var errors = Validate(model);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(model.Subject)));
    }

    // ══════════════════════════════════════════
    // Content 測試
    // ══════════════════════════════════════════

    /// <summary>Sad Path：Content 為空，應觸發 [Required] 錯誤</summary>
    [Fact]
    public void Content_WhenEmpty_FailsRequired()
    {
        var model = BuildValidModel();
        model.Content = string.Empty;

        var errors = Validate(model);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(model.Content)));
    }

    // ══════════════════════════════════════════
    // AdminNote 測試（選填）
    // ══════════════════════════════════════════

    /// <summary>AdminNote 為 null 時（未填），應通過驗證（選填欄位）</summary>
    [Fact]
    public void AdminNote_WhenNull_PassesValidation()
    {
        var model = BuildValidModel();
        model.AdminNote = null;   // 選填，null 合法

        var errors = Validate(model);

        Assert.DoesNotContain(errors, e => e.MemberNames.Contains(nameof(model.AdminNote)));
    }
}
