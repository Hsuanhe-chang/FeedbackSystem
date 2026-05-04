# Service 商業邏輯測試範本（xUnit + NSubstitute）

## 測試層次邊界（重要）

| 測試類型 | 測試目標 | 資料庫 | 範例 |
|---------|---------|--------|------|
| **Unit Test**（本範本） | Service 純商業邏輯 | ❌ 不碰 DB，以 Mock 隔離 | `GenerateUniqueTrackingCodeAsync` 重試迴圈邏輯 |
| **Integration Test** | Repository SP 呼叫 | ✅ 對真實 DB 執行 | `FeedbackRepository.InsertFeedbackAsync` → `usp_Feedback_Insert` |

> **原則**：Unit Test 只測 Service 的決策邏輯與流程控制；
> 任何「直接執行 SQL / 呼叫 SP」的程式碼，請放到 Integration Test。

---

## 前提：抽出 Repository 介面（架構要求）

現有 `FeedbackService` 直接使用 `SqlConnection + SqlCommand`，無法在 Unit Test 中隔離 DB。
**必須先完成以下架構調整**，才能對 Service 進行 Unit Test：

### 步驟 1：新增 `IFeedbackRepository` 介面

```csharp
// 路徑建議：Repositories/IFeedbackRepository.cs
namespace FeedbackSystem.Repositories;

/// <summary>
/// 資料存取層介面，封裝所有對 Stored Procedure 的呼叫
/// Unit Test 中以 NSubstitute Mock 此介面，隔離 DB 依賴
/// </summary>
public interface IFeedbackRepository
{
    Task<bool> CheckTrackingCodeExistsAsync(string trackingCode);
    Task<int> InsertFeedbackAsync(FeedbackCreateViewModel model);
    Task<(IEnumerable<FeedbackListItemViewModel> Items, int TotalCount)> GetPagedListAsync(
        byte? status, byte? priority, int page, int pageSize);
    Task<FeedbackDetailViewModel?> GetByIdAsync(int feedbackId);
    Task<IEnumerable<FeedbackReplyViewModel>> GetRepliesByFeedbackIdAsync(int feedbackId);
    Task UpdateFeedbackAsync(FeedbackEditViewModel model);
    Task InsertReplyAsync(FeedbackReplyCreateViewModel model);
}
```

### 步驟 2：將 `FeedbackService` 改為依賴 `IFeedbackRepository`

```csharp
// FeedbackService.cs：不再直接 new SqlConnection，改注入 Repository
public class FeedbackService : IFeedbackService
{
    // 注入 Repository 介面，由 DI 容器提供實作
    private readonly IFeedbackRepository _repository;

    public FeedbackService(IFeedbackRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// 純商業邏輯：持續產生候選碼，直到 Repository 確認不重複
    /// 此迴圈邏輯不依賴 DB，是 Unit Test 的理想對象
    /// </summary>
    public async Task<string> GenerateUniqueTrackingCodeAsync()
    {
        while (true)
        {
            string candidate = "FB"
                + DateTime.Now.ToString("yyyyMMdd")
                + GenerateRandomUpperCode(6);

            // 委派 DB 查詢給 Repository（Unit Test 中此行為 Mocked）
            bool exists = await _repository.CheckTrackingCodeExistsAsync(candidate);

            if (!exists)
                return candidate;
        }
    }

    // 其他方法改為直接委派 Repository，不再內嵌 SqlCommand 邏輯
    public Task<int> InsertFeedbackAsync(FeedbackCreateViewModel model)
        => _repository.InsertFeedbackAsync(model);
    // ... 其餘方法同理
}
```

### 步驟 3：註冊到 DI 容器（Program.cs）

```csharp
// 分別註冊 Repository 實作與 Service
builder.Services.AddScoped<IFeedbackRepository, FeedbackRepository>();
builder.Services.AddScoped<IFeedbackService, FeedbackService>();
```

---

## Unit Test 範本：Mock `IFeedbackRepository`，測試 Service 邏輯

> NSubstitute 官方建議：**只 Mock 介面**，不要直接 Mock `SqlConnection` 等具體類別。
> Service 的商業邏輯（迴圈、條件判斷、資料組合）才是 Unit Test 的對象。

```csharp
using FeedbackSystem.Models.ViewModels;
using FeedbackSystem.Repositories;
using FeedbackSystem.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace FeedbackSystem.Tests.Services;

/// <summary>
/// FeedbackService 商業邏輯 Unit Test
/// 以 NSubstitute Mock IFeedbackRepository，完全隔離 DB / SP 呼叫
/// 測試重點：Service 自身的流程控制與決策邏輯
/// </summary>
public class FeedbackServiceTests
{
    // ──────────────────────────────────────────
    // 欄位宣告：Mock 介面與 SUT（真實 Service 實體）
    // ──────────────────────────────────────────

    // Mock Repository 介面，所有 SP 呼叫皆由此替換
    private readonly IFeedbackRepository _mockRepository;

    // 被測試的真實 FeedbackService（注入 Mock Repository）
    private readonly FeedbackService _sut;

    public FeedbackServiceTests()
    {
        // 建立 NSubstitute 替代物件（僅支援介面，不支援 SqlConnection 等密封類別）
        _mockRepository = Substitute.For<IFeedbackRepository>();

        // 注入 Mock，讓 Service 執行真實邏輯，但 DB 呼叫由 Mock 接管
        _sut = new FeedbackService(_mockRepository);
    }

    // ══════════════════════════════════════════
    // GenerateUniqueTrackingCodeAsync
    // 測試重點：while 迴圈的重試邏輯，與代碼格式正確性
    // ══════════════════════════════════════════

    /// <summary>
    /// Happy Path：Repository 第一次就回傳「不重複」→ 應產生格式正確的唯一碼
    /// </summary>
    [Fact]
    public async Task GenerateUniqueTrackingCodeAsync_WhenFirstAttemptIsUnique_ReturnsFormattedCode()
    {
        // Arrange：讓 Repository 的 CheckTrackingCodeExistsAsync 每次都回傳 false
        // 代表該候選碼在 DB 中不存在，可直接使用
        _mockRepository
            .CheckTrackingCodeExistsAsync(Arg.Any<string>())
            .Returns(Task.FromResult(false));

        // Act：呼叫真實 Service 方法（實際執行 while 迴圈邏輯）
        var code = await _sut.GenerateUniqueTrackingCodeAsync();

        // Assert 1：格式必須符合 FB + 8碼日期 + 6碼大寫英數
        Assert.Matches(@"^FB\d{8}[A-Z2-9]{6}$", code);

        // Assert 2：Repository 應只被呼叫一次（第一次就成功）
        _mockRepository.Received(1).CheckTrackingCodeExistsAsync(Arg.Any<string>());
    }

    /// <summary>
    /// 邊界條件：前兩次碰撞（已存在），第三次才成功
    /// 測試 while 迴圈的重試邏輯是否正確運作
    /// </summary>
    [Fact]
    public async Task GenerateUniqueTrackingCodeAsync_WhenTwoCollisions_RetriesAndSucceeds()
    {
        // Arrange：使用 Queue 模擬「前兩次衝突，第三次可用」的情境
        var callQueue = new Queue<bool>([true, true, false]); // true=已存在, false=可用

        _mockRepository
            .CheckTrackingCodeExistsAsync(Arg.Any<string>())
            // Returns 的 callback 形式：每次呼叫從 Queue 取出下一個值
            .Returns(_ => Task.FromResult(callQueue.Dequeue()));

        // Act
        var code = await _sut.GenerateUniqueTrackingCodeAsync();

        // Assert 1：最終仍回傳有效代碼
        Assert.NotEmpty(code);
        Assert.StartsWith("FB", code);

        // Assert 2：Repository 應被呼叫三次（兩次碰撞 + 一次成功）
        _mockRepository.Received(3).CheckTrackingCodeExistsAsync(Arg.Any<string>());
    }

    /// <summary>
    /// Sad Path：Repository 拋出例外時，Service 不應吞掉例外，應向上傳遞
    /// </summary>
    [Fact]
    public async Task GenerateUniqueTrackingCodeAsync_WhenRepositoryThrows_PropagatesException()
    {
        // Arrange：讓 Repository 拋出 InvalidOperationException（模擬 DB 連線失敗）
        _mockRepository
            .CheckTrackingCodeExistsAsync(Arg.Any<string>())
            .ThrowsAsync(new InvalidOperationException("DB 連線中斷"));

        // Act & Assert：Service 不應吞掉例外，應讓它向上傳遞給 Controller
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.GenerateUniqueTrackingCodeAsync());
    }

    // ══════════════════════════════════════════
    // InsertFeedbackAsync
    // 測試重點：Service 是否正確將 ViewModel 傳給 Repository
    //           以及回傳值是否原封不動傳回給 Controller
    // ══════════════════════════════════════════

    /// <summary>
    /// Happy Path：Repository 成功新增後回傳 Id，Service 應原封傳回
    /// </summary>
    [Fact]
    public async Task InsertFeedbackAsync_WithValidModel_ReturnsRepositoryId()
    {
        // Arrange：設定 Repository Mock 回傳假 Id = 42
        var model = new FeedbackCreateViewModel
        {
            TrackingCode = "FB20260504ABC123",
            CustomerName = "測試客戶",
            CustomerEmail = "test@example.com",
            Category = "產品",
            Subject = "主旨",
            Content = "內容"
        };

        _mockRepository
            .InsertFeedbackAsync(model)
            .Returns(Task.FromResult(42));

        // Act
        var newId = await _sut.InsertFeedbackAsync(model);

        // Assert：Service 應回傳 Repository 的結果（不應擅自修改）
        Assert.Equal(42, newId);
    }

    /// <summary>
    /// Sad Path：Repository 拋出例外，Service 應讓例外向上傳遞
    /// </summary>
    [Fact]
    public async Task InsertFeedbackAsync_WhenRepositoryThrows_PropagatesException()
    {
        // Arrange
        var model = new FeedbackCreateViewModel { CustomerName = "測試" };

        _mockRepository
            .InsertFeedbackAsync(model)
            .ThrowsAsync(new InvalidOperationException("SP 執行失敗"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.InsertFeedbackAsync(model));
    }

    // ══════════════════════════════════════════
    // GetByIdAsync
    // 測試重點：id 不存在時是否正確回傳 null
    // ══════════════════════════════════════════

    /// <summary>
    /// Sad Path：Repository 回傳 null（id 不存在），Service 應直接回傳 null
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsNull()
    {
        // Arrange：模擬 DB 查無資料
        _mockRepository
            .GetByIdAsync(Arg.Any<int>())
            .Returns(Task.FromResult<FeedbackDetailViewModel?>(null));

        // Act
        var result = await _sut.GetByIdAsync(99999);

        // Assert
        Assert.Null(result);
    }
}
```

---

## Unit Test 應測 vs 不應測的邊界

| 情境 | Unit Test？ | 說明 |
|------|-----------|------|
| `GenerateUniqueTrackingCodeAsync` 重試迴圈 | ✅ | 純 Service 邏輯，Mock `CheckTrackingCodeExistsAsync` |
| Service 是否將正確參數傳給 Repository | ✅ | 用 `Received()` 驗證傳入值 |
| Service 是否原封回傳 Repository 結果 | ✅ | Assert 回傳值是否等於 Mock 設定值 |
| Repository 例外是否向上傳遞 | ✅ | Mock 拋出例外，Assert `ThrowsAsync` |
| `usp_Feedback_Insert` SP 執行結果正確 | ❌ | 屬於 Integration Test 範疇 |
| 分頁 SQL 語法是否正確 | ❌ | 屬於 Integration Test 範疇 |
| `@TotalCount` OUTPUT 參數是否正確讀取 | ❌ | 屬於 Integration Test 範疇 |

---

## ⚠️ Integration Test 範疇（不在本 Skill 內）

以下測試**需要真實 DB 連線**，應建立獨立的 `FeedbackSystem.IntegrationTests` 專案處理：

- `FeedbackRepository.InsertFeedbackAsync` → 實際執行 `usp_Feedback_Insert`
- `FeedbackRepository.GetPagedListAsync` → 驗證分頁、篩選、排序結果
- `FeedbackRepository.InsertReplyAsync` → 驗證 SP 內部 Transaction 正確性（回覆數 +1、Status 自動更新）
- `FeedbackRepository.CheckTrackingCodeExistsAsync` → 驗證 TrackingCode 重複偵測

Integration Test 建議工具：
- `Microsoft.AspNetCore.Mvc.Testing` — 啟動完整應用程式進行端對端測試
- `Testcontainers.MsSql` — 以 Docker 啟動隔離的 SQL Server 測試環境
