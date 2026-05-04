using System.ComponentModel.DataAnnotations;
using FeedbackSystem.Models.ViewModels;

namespace FeedbackSystem.Tests.ViewModels;

/// <summary>
/// FeedbackCreateViewModel DataAnnotation 驗證單元測試。
/// 使用 Validator.TryValidateObject 直接驗證規則，
/// 不依賴 HTTP 管線或 ModelBinder。
/// </summary>
public class FeedbackCreateViewModelTests
{
    // ──────────────────────────────────────────
    // 輔助方法：執行 DataAnnotation 驗證並回傳結果清單
    // ──────────────────────────────────────────

    /// <summary>
    /// 對指定 ViewModel 執行完整的 DataAnnotation 驗證。
    /// validateAllProperties: true → 驗證所有屬性（包含 StringLength、EmailAddress 等）
    /// </summary>
    /// <param name="model">待驗證的物件</param>
    /// <returns>驗證結果清單（空集合 = 全部通過）</returns>
    private static IList<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(model, null, null);
        // validateAllProperties: true 才會驗證非 Required 的 Annotation
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }

    /// <summary>
    /// 建立一個所有必填欄位皆合法的基礎 ViewModel。
    /// 各測試方法只需修改「被測欄位」，其餘維持合法值以確保孤立性。
    /// </summary>
    private static FeedbackCreateViewModel BuildValidModel() => new()
    {
        // 符合格式 FB + 8 碼日期 + 6 碼大寫英數
        TrackingCode  = "FB20260504ABC123",
        CustomerName  = "測試客戶",
        CustomerEmail = "test@example.com",
        CustomerPhone = null,       // 選填欄位，預設不填
        Category      = "產品",
        Subject       = "測試主旨",
        Content       = "這是測試內容"
    };

    // ══════════════════════════════════════════
    // 整體驗證：合法 Model 應全部通過
    // ══════════════════════════════════════════

    /// <summary>合法 ViewModel 應完全通過驗證，無任何錯誤</summary>
    [Fact]
    public void FullyValidModel_PassesAllValidations()
    {
        // Arrange：建立完全合法的 Model
        var model = BuildValidModel();

        // Act
        var errors = Validate(model);

        // Assert：驗證清單應為空
        Assert.Empty(errors);
    }

    // ══════════════════════════════════════════
    // TrackingCode 測試
    // ══════════════════════════════════════════

    /// <summary>Sad Path：TrackingCode 為空，應觸發 [Required] 錯誤</summary>
    [Fact]
    public void TrackingCode_WhenEmpty_FailsRequired()
    {
        // Arrange：清空 TrackingCode
        var model = BuildValidModel();
        model.TrackingCode = string.Empty;

        // Act
        var errors = Validate(model);

        // Assert：應有針對 TrackingCode 的驗證錯誤
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(model.TrackingCode)));
    }

    /// <summary>邊界條件：TrackingCode 超過 20 字，應觸發 [StringLength] 錯誤</summary>
    [Fact]
    public void TrackingCode_WhenExceeds20Chars_FailsStringLength()
    {
        // Arrange：21 碼字串，剛好超過限制
        var model = BuildValidModel();
        model.TrackingCode = new string('A', 21);   // 21 > 20

        // Act
        var errors = Validate(model);

        // Assert
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(model.TrackingCode)));
    }

    /// <summary>邊界條件：TrackingCode 剛好 20 字，應通過驗證</summary>
    [Fact]
    public void TrackingCode_WhenExactly20Chars_PassesValidation()
    {
        // Arrange：20 碼字串，剛好在邊界上
        var model = BuildValidModel();
        model.TrackingCode = new string('A', 20);   // 20 = 20，合法

        // Act
        var errors = Validate(model);

        // Assert：不應有 TrackingCode 的錯誤
        Assert.DoesNotContain(errors, e => e.MemberNames.Contains(nameof(model.TrackingCode)));
    }

    // ══════════════════════════════════════════
    // CustomerName 測試
    // ══════════════════════════════════════════

    /// <summary>Sad Path：CustomerName 為空，應觸發 [Required] 錯誤</summary>
    [Fact]
    public void CustomerName_WhenEmpty_FailsRequired()
    {
        var model = BuildValidModel();
        model.CustomerName = string.Empty;

        var errors = Validate(model);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(model.CustomerName)));
    }

    /// <summary>邊界條件：CustomerName 超過 100 字，應觸發 [StringLength] 錯誤</summary>
    [Fact]
    public void CustomerName_WhenExceeds100Chars_FailsStringLength()
    {
        // Arrange：101 碼，剛好超過 StringLength(100)
        var model = BuildValidModel();
        model.CustomerName = new string('A', 101);

        var errors = Validate(model);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(model.CustomerName)));
    }

    /// <summary>邊界條件：CustomerName 剛好 100 字，應通過驗證</summary>
    [Fact]
    public void CustomerName_WhenExactly100Chars_PassesValidation()
    {
        var model = BuildValidModel();
        model.CustomerName = new string('A', 100);  // 等於上限，合法

        var errors = Validate(model);

        Assert.DoesNotContain(errors, e => e.MemberNames.Contains(nameof(model.CustomerName)));
    }

    // ══════════════════════════════════════════
    // CustomerEmail 測試
    // ══════════════════════════════════════════

    /// <summary>合法 Email 格式，應通過驗證</summary>
    [Theory]
    [InlineData("valid@example.com")]           // 標準格式
    [InlineData("user.name+tag@domain.co.tw")]  // 含點與加號
    [InlineData("a@b.c")]                       // 最短合法格式
    public void CustomerEmail_WhenValidFormat_PassesValidation(string email)
    {
        var model = BuildValidModel();
        model.CustomerEmail = email;

        var errors = Validate(model);

        // 確認 Email 欄位無錯誤
        Assert.DoesNotContain(errors, e => e.MemberNames.Contains(nameof(model.CustomerEmail)));
    }

    /// <summary>無效 Email 格式，應觸發驗證錯誤</summary>
    [Theory]
    [InlineData("")]                // [Required] 觸發
    [InlineData("not-an-email")]    // 缺少 @ 符號
    [InlineData("missing@")]        // 缺少網域部分
    [InlineData("@nodomain.com")]   // 缺少使用者名稱
    public void CustomerEmail_WhenInvalidFormat_FailsValidation(string email)
    {
        var model = BuildValidModel();
        model.CustomerEmail = email;

        var errors = Validate(model);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(model.CustomerEmail)));
    }

    // ══════════════════════════════════════════
    // CustomerPhone 測試（選填但若填寫需符合格式）
    // ══════════════════════════════════════════

    /// <summary>null 或合法電話號碼應通過驗證</summary>
    [Theory]
    [InlineData(null)]              // 選填，null 合法
    [InlineData("0912345678")]      // 手機 09 開頭 10 碼
    [InlineData("0212345678")]      // 台北市話 02 開頭
    [InlineData("0422123456")]      // 台中市話 04 開頭
    public void CustomerPhone_WhenValidOrNull_PassesValidation(string? phone)
    {
        var model = BuildValidModel();
        model.CustomerPhone = phone;

        var errors = Validate(model);

        Assert.DoesNotContain(errors, e => e.MemberNames.Contains(nameof(model.CustomerPhone)));
    }

    /// <summary>不符合電話正規格式，應觸發 [RegularExpression] 錯誤</summary>
    [Theory]
    [InlineData("0912-345-678")]    // 含連字符（不符合純數字規則）
    [InlineData("091234")]          // 位數不足（少於 8 碼）
    [InlineData("12345678")]        // 不以 0 開頭
    [InlineData("0112345678")]      // 01 開頭不在市話或手機範圍
    public void CustomerPhone_WhenInvalidFormat_FailsRegexValidation(string phone)
    {
        var model = BuildValidModel();
        model.CustomerPhone = phone;

        var errors = Validate(model);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(model.CustomerPhone)));
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

    /// <summary>Happy Path：Content 有值（不限長度），應通過驗證</summary>
    [Fact]
    public void Content_WhenHasValue_PassesValidation()
    {
        var model = BuildValidModel();
        // 測試長文字也能通過（Content 沒有 StringLength 限制）
        model.Content = new string('A', 5000);

        var errors = Validate(model);

        Assert.DoesNotContain(errors, e => e.MemberNames.Contains(nameof(model.Content)));
    }
}
