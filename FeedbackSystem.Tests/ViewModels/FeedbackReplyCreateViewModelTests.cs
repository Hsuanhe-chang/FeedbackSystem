using System.ComponentModel.DataAnnotations;
using FeedbackSystem.Models.ViewModels;

namespace FeedbackSystem.Tests.ViewModels;

/// <summary>
/// FeedbackReplyCreateViewModel DataAnnotation 驗證單元測試。
/// 重點測試：Content（必填）、ReplierName（必填最多 100 字）、
/// ReplyType（[Range(0,1)]）等規則。
/// </summary>
public class FeedbackReplyCreateViewModelTests
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
    /// 建立一個所有欄位皆合法的基礎 FeedbackReplyCreateViewModel。
    /// </summary>
    private static FeedbackReplyCreateViewModel BuildValidModel() => new()
    {
        FeedbackId  = 1,            // 必填，大於 0 的正整數
        Content     = "這是回覆內容",
        ReplierName = "客服人員",
        ReplyType   = 1,            // 1=官方回覆，在 [Range(0,1)] 範圍內
        IsPublic    = true          // 預設公開，無驗證 Annotation
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

    /// <summary>Happy Path：Content 有任意長度文字，應通過驗證（無 StringLength 限制）</summary>
    [Fact]
    public void Content_WhenHasLongValue_PassesValidation()
    {
        var model = BuildValidModel();
        // Content 沒有 StringLength 限制，長文字也應通過
        model.Content = new string('A', 5000);

        var errors = Validate(model);

        Assert.DoesNotContain(errors, e => e.MemberNames.Contains(nameof(model.Content)));
    }

    // ══════════════════════════════════════════
    // ReplierName 測試
    // ══════════════════════════════════════════

    /// <summary>Sad Path：ReplierName 為空，應觸發 [Required] 錯誤</summary>
    [Fact]
    public void ReplierName_WhenEmpty_FailsRequired()
    {
        var model = BuildValidModel();
        model.ReplierName = string.Empty;

        var errors = Validate(model);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(model.ReplierName)));
    }

    /// <summary>邊界條件：ReplierName 超過 100 字，應觸發 [StringLength] 錯誤</summary>
    [Fact]
    public void ReplierName_WhenExceeds100Chars_FailsStringLength()
    {
        var model = BuildValidModel();
        model.ReplierName = new string('A', 101);   // 101 > 100

        var errors = Validate(model);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(model.ReplierName)));
    }

    /// <summary>邊界條件：ReplierName 剛好 100 字，應通過驗證</summary>
    [Fact]
    public void ReplierName_WhenExactly100Chars_PassesValidation()
    {
        var model = BuildValidModel();
        model.ReplierName = new string('A', 100);   // 等於上限，合法

        var errors = Validate(model);

        Assert.DoesNotContain(errors, e => e.MemberNames.Contains(nameof(model.ReplierName)));
    }

    // ══════════════════════════════════════════
    // ReplyType 測試（[Range(0, 1)]）
    // ══════════════════════════════════════════

    /// <summary>合法的 ReplyType 值（0 或 1）應通過 [Range] 驗證</summary>
    [Theory]
    [InlineData((byte)0)]   // 0=客戶回覆
    [InlineData((byte)1)]   // 1=官方回覆
    public void ReplyType_WhenInRange_PassesValidation(byte replyType)
    {
        var model = BuildValidModel();
        model.ReplyType = replyType;

        var errors = Validate(model);

        Assert.DoesNotContain(errors, e => e.MemberNames.Contains(nameof(model.ReplyType)));
    }

    /// <summary>邊界條件：ReplyType = 2，超出 [Range(0, 1)] 應觸發錯誤</summary>
    [Fact]
    public void ReplyType_WhenTwo_FailsRangeValidation()
    {
        var model = BuildValidModel();
        model.ReplyType = 2;   // 2 > 1，超出允許範圍

        var errors = Validate(model);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(model.ReplyType)));
    }

    // ══════════════════════════════════════════
    // IsPublic 測試
    // ══════════════════════════════════════════

    /// <summary>IsPublic 不論 true 或 false，都應通過驗證（無額外限制）</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsPublic_WhenAnyBoolValue_PassesValidation(bool isPublic)
    {
        var model = BuildValidModel();
        model.IsPublic = isPublic;

        var errors = Validate(model);

        Assert.DoesNotContain(errors, e => e.MemberNames.Contains(nameof(model.IsPublic)));
    }
}
