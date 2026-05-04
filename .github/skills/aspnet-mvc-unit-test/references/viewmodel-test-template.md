# ViewModel DataAnnotation 驗證測試範本（xUnit）

## 測試原理

使用 `System.ComponentModel.DataAnnotations.Validator.TryValidateObject()` 
在不需要 HTTP 請求的情況下，直接驗證 ViewModel 的 DataAnnotation 規則。

---

## 完整範本

```csharp
using System.ComponentModel.DataAnnotations;
using FeedbackSystem.Models.ViewModels;
using Xunit;

namespace FeedbackSystem.Tests.ViewModels;

/// <summary>
/// FeedbackCreateViewModel DataAnnotation 驗證單元測試
/// 不依賴 HTTP 管線，直接呼叫 Validator API 進行驗證
/// </summary>
public class FeedbackCreateViewModelTests
{
    // ──────────────────────────────────────────
    // 輔助方法：執行 DataAnnotation 驗證並回傳結果清單
    // ──────────────────────────────────────────

    /// <summary>
    /// 對指定 ViewModel 執行完整 DataAnnotation 驗證
    /// </summary>
    /// <param name="model">待驗證的物件</param>
    /// <returns>驗證結果清單（空集合 = 全部通過）</returns>
    private static IList<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(model, null, null);

        // validateAllProperties: true → 驗證所有屬性（不只 [Required]）
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);

        return results;
    }

    // ══════════════════════════════════════════
    // CustomerName 測試
    // ══════════════════════════════════════════

    /// <summary>合法輸入：姓名在長度限制內，應通過驗證</summary>
    [Fact]
    public void CustomerName_WhenValid_PassesValidation()
    {
        // Arrange：建立最小合法 ViewModel
        var model = BuildValidModel();
        model.CustomerName = "正常客戶名稱";

        // Act
        var errors = Validate(model);

        // Assert：CustomerName 欄位不應有錯誤
        Assert.DoesNotContain(errors, e => e.MemberNames.Contains(nameof(model.CustomerName)));
    }

    /// <summary>Sad Path：姓名為空字串，應觸發 [Required] 錯誤</summary>
    [Fact]
    public void CustomerName_WhenEmpty_FailsRequiredValidation()
    {
        // Arrange
        var model = BuildValidModel();
        model.CustomerName = string.Empty;  // 違反 [Required]

        // Act
        var errors = Validate(model);

        // Assert：必須有針對 CustomerName 的驗證錯誤
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(model.CustomerName)));
    }

    /// <summary>邊界條件：姓名超過 100 字，應觸發 [StringLength] 錯誤</summary>
    [Fact]
    public void CustomerName_WhenExceeds100Chars_FailsStringLengthValidation()
    {
        // Arrange：產生 101 字的字串（剛好超過限制）
        var model = BuildValidModel();
        model.CustomerName = new string('A', 101);  // 101 > 100，違反 [StringLength(100)]

        // Act
        var errors = Validate(model);

        // Assert
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(model.CustomerName)));
    }

    // ══════════════════════════════════════════
    // CustomerEmail 測試
    // ══════════════════════════════════════════

    /// <summary>合法 Email 格式應通過驗證</summary>
    [Theory]
    [InlineData("valid@example.com")]       // 標準格式
    [InlineData("user.name+tag@domain.co")] // 含特殊字元
    public void CustomerEmail_WhenValidFormat_PassesValidation(string email)
    {
        // Arrange
        var model = BuildValidModel();
        model.CustomerEmail = email;

        // Act
        var errors = Validate(model);

        // Assert
        Assert.DoesNotContain(errors, e => e.MemberNames.Contains(nameof(model.CustomerEmail)));
    }

    /// <summary>無效 Email 格式應觸發驗證錯誤</summary>
    [Theory]
    [InlineData("not-an-email")]        // 缺少 @ 符號
    [InlineData("missing@")]            // 缺少網域
    [InlineData("@nodomain.com")]       // 缺少使用者名稱
    [InlineData("")]                    // 空字串（[Required]）
    public void CustomerEmail_WhenInvalidFormat_FailsValidation(string email)
    {
        // Arrange
        var model = BuildValidModel();
        model.CustomerEmail = email;

        // Act
        var errors = Validate(model);

        // Assert
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(model.CustomerEmail)));
    }

    // ══════════════════════════════════════════
    // CustomerPhone 測試（選填，但若填寫需符合格式）
    // ══════════════════════════════════════════

    /// <summary>合法電話格式（或 null）應通過驗證</summary>
    [Theory]
    [InlineData(null)]              // 選填 → null 合法
    [InlineData("0912345678")]      // 手機格式
    [InlineData("0212345678")]      // 市話（台北）
    [InlineData("0422123456")]      // 市話（台中）
    public void CustomerPhone_WhenValidOrNull_PassesValidation(string? phone)
    {
        // Arrange
        var model = BuildValidModel();
        model.CustomerPhone = phone;

        // Act
        var errors = Validate(model);

        // Assert
        Assert.DoesNotContain(errors, e => e.MemberNames.Contains(nameof(model.CustomerPhone)));
    }

    /// <summary>無效電話格式應觸發 [RegularExpression] 錯誤</summary>
    [Theory]
    [InlineData("0912-345-678")]    // 含連字符（不符合純數字規則）
    [InlineData("091234")]          // 位數不足
    [InlineData("12345678")]        // 不以 0 開頭
    public void CustomerPhone_WhenInvalidFormat_FailsRegexValidation(string phone)
    {
        // Arrange
        var model = BuildValidModel();
        model.CustomerPhone = phone;

        // Act
        var errors = Validate(model);

        // Assert
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(model.CustomerPhone)));
    }

    // ══════════════════════════════════════════
    // 輔助方法：建立「所有欄位合法」的基礎 Model
    // ══════════════════════════════════════════

    /// <summary>
    /// 建立一個所有必填欄位皆合法的 FeedbackCreateViewModel
    /// 作為各測試的基礎（只修改要測試的欄位）
    /// </summary>
    private static FeedbackCreateViewModel BuildValidModel() => new()
    {
        TrackingCode = "FB20260504ABC123",   // 符合格式：FB + 8碼日期 + 6碼英數
        CustomerName = "測試客戶",
        CustomerEmail = "test@example.com",
        CustomerPhone = null,                // 選填，預設不填
        Category = "產品",
        Subject = "測試主旨",
        Content = "這是測試內容，用於驗證 ViewModel。"
    };
}
```

---

## 常見 DataAnnotation 對應測試表

| Annotation | 測試邊界值 |
|------------|-----------|
| `[Required]` | `""`, `null`, `" "`（純空白） |
| `[StringLength(n)]` | `n` 字（剛好合法）、`n+1` 字（剛好超過） |
| `[EmailAddress]` | 合法格式、缺少 @、缺少網域、空字串 |
| `[RegularExpression(pattern)]` | 符合 pattern 的範例、不符合的邊界值 |
| `[Range(min, max)]` | `min-1`、`min`、`max`、`max+1` |
| `[MaxLength(n)]` | 長度 = n（合法）、長度 = n+1（超過） |
| `[MinLength(n)]` | 長度 = n（合法）、長度 = n-1（不足） |

---

## 驗證結果斷言速查

```csharp
// 驗證全部通過（無錯誤）
Assert.Empty(Validate(model));

// 指定欄位有錯誤
Assert.Contains(errors, e => e.MemberNames.Contains("欄位名稱"));

// 指定欄位無錯誤
Assert.DoesNotContain(errors, e => e.MemberNames.Contains("欄位名稱"));

// 確認錯誤訊息內容
Assert.Contains(errors, e =>
    e.MemberNames.Contains("CustomerName") &&
    e.ErrorMessage!.Contains("必填"));

// 確認錯誤數量
Assert.Single(errors);            // 只有一個錯誤
Assert.Equal(3, errors.Count);    // 精確 3 個錯誤
```
