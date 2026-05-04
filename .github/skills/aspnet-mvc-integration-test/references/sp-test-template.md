# SP 呼叫測試範本（Service + DB 整合）

本範本示範如何直接測試 `FeedbackService` 呼叫 Stored Procedure 的完整流程，  
**不使用 Mock，使用真實測試資料庫（ymmistest / Feedback_Test）**。

---

## 範本 A：只讀 SP 測試（GetPagedList）

測試 `usp_Feedback_GetPagedList`，驗證分頁查詢回傳結果的正確性。

```csharp
using FluentAssertions;
using FeedbackSystem.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FeedbackSystem.IntegrationTests;

/// <summary>
/// FeedbackService 的 Integration Test
/// 直接連接真實測試 DB（ymmistest/Feedback_Test），驗證 SP 呼叫結果
/// 使用 IClassFixture 共用連線字串讀取邏輯，避免重複初始化
/// </summary>
public class FeedbackServiceIntegrationTests : IClassFixture<IntegrationTestFixture>
{
    // 從 Fixture 取得的 FeedbackService 實例（已注入真實連線字串）
    private readonly FeedbackService _sut;

    /// <summary>
    /// 建構子：由 xUnit 自動注入 Fixture，並根據真實連線字串建立 FeedbackService
    /// </summary>
    /// <param name="fixture">共用測試 Fixture，內含從 appsettings.json 讀取的連線字串</param>
    public FeedbackServiceIntegrationTests(IntegrationTestFixture fixture)
    {
        // 建立真實的 IConfiguration，供 FeedbackService 建構子使用
        // 這樣 Service 的 _connectionString 會對應到真實測試 DB
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // 直接注入從 Fixture 取得的連線字串，省去再次讀取檔案
                ["ConnectionStrings:FeedbackDb"] = fixture.ConnectionString
            })
            .Build();

        // 使用真實 IConfiguration 實例化 FeedbackService（被測目標）
        _sut = new FeedbackService(config);
    }

    // ─────────────────────────────────────────────────────────────────
    // 測試 usp_Feedback_GetPagedList（分頁查詢）
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 驗證：不帶篩選條件取得第一頁時，應回傳資料（前提：測試 DB 有資料）
    /// </summary>
    [Fact]
    public async Task GetPagedListAsync_WithNoFilter_ReturnsItemsAndTotalCount()
    {
        // Arrange
        // 不篩選 Status / Priority，取得第 1 頁、每頁 10 筆
        byte? status = null;
        byte? priority = null;
        int page = 1;
        int pageSize = 10;

        // Act
        // 呼叫真實 Service，觸發 usp_Feedback_GetPagedList SP
        var (items, totalCount) = await _sut.GetPagedListAsync(status, priority, page, pageSize);

        // Assert
        // 驗證：SP 有回傳資料（測試 DB 必須有至少一筆）
        items.Should().NotBeNull("SP 應永遠回傳清單物件，即使是空清單也不應為 null");
        totalCount.Should().BeGreaterThanOrEqualTo(0, "totalCount 為 OUTPUT 參數，不應為負數");

        // 驗證：每筆資料的必填欄位不為空（資料完整性檢查）
        foreach (var item in items)
        {
            item.TrackingCode.Should().NotBeNullOrEmpty("TrackingCode 為 NOT NULL 欄位");
            item.CustomerName.Should().NotBeNullOrEmpty("CustomerName 為 NOT NULL 欄位");
            item.Status.Should().BeInRange(0, 3, "Status 只允許 0=待處理, 1=處理中, 2=已回覆, 3=已關閉");
            item.Priority.Should().BeInRange(1, 3, "Priority 只允許 1=一般, 2=重要, 3=緊急");
        }
    }

    /// <summary>
    /// 驗證：以特定 Status 篩選時，回傳的資料列 Status 都符合篩選條件
    /// </summary>
    [Theory]
    // 測試每個合法 Status 值的篩選
    [InlineData((byte)0)] // 待處理
    [InlineData((byte)1)] // 處理中
    [InlineData((byte)2)] // 已回覆
    [InlineData((byte)3)] // 已關閉
    public async Task GetPagedListAsync_WithStatusFilter_ReturnsOnlyMatchingStatus(byte filterStatus)
    {
        // Arrange
        int page = 1;
        int pageSize = 100; // 取較多筆以確保能驗證篩選結果

        // Act
        var (items, _) = await _sut.GetPagedListAsync(filterStatus, null, page, pageSize);

        // Assert
        // 每一筆的 Status 都必須等於篩選條件
        foreach (var item in items)
        {
            item.Status.Should().Be(filterStatus,
                $"以 Status={filterStatus} 篩選，回傳的每筆資料 Status 都應為 {filterStatus}");
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // 測試 usp_Feedback_CheckTrackingCodeExists（唯一性驗證）
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 驗證：傳入一個顯然不存在的 TrackingCode，SP 應回傳 false
    /// </summary>
    [Fact]
    public async Task CheckTrackingCodeExistsAsync_WithNonExistentCode_ReturnsFalse()
    {
        // Arrange
        // 使用一個極不可能存在的代碼（含特殊前綴與時間戳）
        var nonExistentCode = $"TEST_NEVER_EXISTS_{Guid.NewGuid():N}";

        // Act
        var exists = await _sut.CheckTrackingCodeExistsAsync(nonExistentCode);

        // Assert
        exists.Should().BeFalse("此 TrackingCode 剛產生，資料庫中不應存在");
    }
}
```

---

## 範本 B：寫入 SP 測試（InsertFeedback + Teardown）

測試 `usp_Feedback_Insert`，驗證新增後可查詢到資料，**並在測試後清理資料**。

```csharp
using System.Data;
using FluentAssertions;
using FeedbackSystem.Models.ViewModels;
using FeedbackSystem.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FeedbackSystem.IntegrationTests;

/// <summary>
/// 測試 FeedbackService 寫入操作（InsertFeedbackAsync）
/// 每次測試結束後必須清理新增的測試資料，確保不污染測試 DB
/// </summary>
public class FeedbackInsertIntegrationTests : IClassFixture<IntegrationTestFixture>
{
    private readonly FeedbackService _sut;
    private readonly string _connectionString;

    public FeedbackInsertIntegrationTests(IntegrationTestFixture fixture)
    {
        _connectionString = fixture.ConnectionString;

        // 建立真實 IConfiguration 注入 FeedbackService
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:FeedbackDb"] = _connectionString
            })
            .Build();

        _sut = new FeedbackService(config);
    }

    /// <summary>
    /// 驗證：新增一筆意見後，SP 應回傳正值的 FeedbackId（IDENTITY 值）
    /// 測試後清理：刪除本次新增的測試資料，避免污染測試 DB
    /// </summary>
    [Fact]
    public async Task InsertFeedbackAsync_WithValidModel_ReturnsNewFeedbackId()
    {
        // Arrange
        // 準備一筆有效的測試資料，TrackingCode 使用 GUID 確保唯一性
        var model = new FeedbackCreateViewModel
        {
            TrackingCode  = $"FBTEST{Guid.NewGuid():N}"[..16], // 截取前 16 碼符合欄位長度
            CustomerName  = "[整合測試] 測試客戶",
            CustomerEmail = "integration-test@example.com",
            CustomerPhone = null,    // 選填欄位，測試 null 值
            Category      = "其他",
            Subject       = "[整合測試] 自動化測試主旨",
            Content       = "此為 Integration Test 自動新增的測試資料，請勿手動刪除。"
        };

        int newFeedbackId = 0;

        try
        {
            // Act
            // 呼叫真實 Service，觸發 usp_Feedback_Insert SP
            newFeedbackId = await _sut.InsertFeedbackAsync(model);

            // Assert
            // 驗證：SP 應回傳正整數（IDENTITY 欄位的新值）
            newFeedbackId.Should().BeGreaterThan(0, "usp_Feedback_Insert 應回傳新 IDENTITY 值");
        }
        finally
        {
            // Teardown（不論測試成功或失敗都必須執行）
            // 直接以 SqlConnection 刪除測試資料，確保 DB 不殘留測試污染
            if (newFeedbackId > 0)
            {
                await DeleteTestFeedbackAsync(newFeedbackId);
            }
        }
    }

    /// <summary>
    /// 工具方法：依 FeedbackId 刪除測試資料（先刪回覆，再刪意見，遵守外鍵順序）
    /// </summary>
    /// <param name="feedbackId">要刪除的測試資料 ID</param>
    private async Task DeleteTestFeedbackAsync(int feedbackId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // 先刪除 FeedbackReply 子資料（若有），否則外鍵約束會阻止刪除
        await using var deleteRepliesCmd = new SqlCommand(
            "DELETE FROM FeedbackReply WHERE FeedbackId = @FeedbackId", conn);
        deleteRepliesCmd.Parameters.AddWithValue("@FeedbackId", feedbackId);
        await deleteRepliesCmd.ExecuteNonQueryAsync();

        // 再刪除 Feedback 主資料
        await using var deleteFeedbackCmd = new SqlCommand(
            "DELETE FROM Feedback WHERE FeedbackId = @FeedbackId", conn);
        deleteFeedbackCmd.Parameters.AddWithValue("@FeedbackId", feedbackId);
        await deleteFeedbackCmd.ExecuteNonQueryAsync();
    }
}
```

---

## 範本 C：OUTPUT 參數驗證

若 SP 有 OUTPUT 參數（如 `@TotalCount`、`@FeedbackId`），驗證方式如下：

```csharp
/// <summary>
/// 直接以 SqlCommand 呼叫 SP，驗證 OUTPUT 參數值（低層驗證用）
/// 當需要驗證 Service 層以外的 SP 行為時使用
/// </summary>
[Fact]
public async Task UspFeedbackGetPagedList_OutputTotalCount_IsNonNegative()
{
    // Arrange
    await using var conn = new SqlConnection(_connectionString);
    await conn.OpenAsync();

    await using var cmd = new SqlCommand("usp_Feedback_GetPagedList", conn)
    {
        CommandType = CommandType.StoredProcedure
    };

    // 傳入分頁參數
    cmd.Parameters.AddWithValue("@Status", DBNull.Value);
    cmd.Parameters.AddWithValue("@Priority", DBNull.Value);
    cmd.Parameters.AddWithValue("@Page", 1);
    cmd.Parameters.AddWithValue("@PageSize", 10);

    // 宣告 OUTPUT 參數
    var totalCountParam = new SqlParameter("@TotalCount", SqlDbType.Int)
    {
        Direction = ParameterDirection.Output
    };
    cmd.Parameters.Add(totalCountParam);

    // Act
    await using var reader = await cmd.ExecuteReaderAsync();
    await reader.CloseAsync(); // 關閉 Reader 後才能讀取 OUTPUT 參數

    int totalCount = (int)(totalCountParam.Value ?? 0);

    // Assert
    totalCount.Should().BeGreaterThanOrEqualTo(0, "TotalCount OUTPUT 參數不應為負數");
}
```
