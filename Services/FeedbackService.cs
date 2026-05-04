using System.Data;
using FeedbackSystem.Models.ViewModels;
using Microsoft.Data.SqlClient;

namespace FeedbackSystem.Services;

/// <summary>
/// FeedbackService 的 ADO.NET 實作
/// 所有資料操作皆透過 Stored Procedure，不在程式碼中撰寫 T-SQL
/// 連線字串從 IConfiguration（appsettings.json）讀取
/// </summary>
public class FeedbackService : IFeedbackService
{
    // 儲存連線字串，由 DI 注入的 IConfiguration 取得
    private readonly string _connectionString;

    /// <summary>
    /// 建構子：透過 DI 注入 IConfiguration，取得 FeedbackDb 連線字串
    /// </summary>
    /// <param name="configuration">ASP.NET Core 設定物件</param>
    /// <exception cref="InvalidOperationException">若連線字串不存在則拋出例外</exception>
    public FeedbackService(IConfiguration configuration)
    {
        // 讀取 appsettings.json 中的 ConnectionStrings:FeedbackDb
        _connectionString = configuration.GetConnectionString("FeedbackDb")
            ?? throw new InvalidOperationException("ConnectionStrings:FeedbackDb 未設定，請檢查 appsettings.json");
    }

    // ─────────────────────────────────────────────────────────────────
    // 工具方法：建立並開啟 SqlConnection
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 建立並開啟一個 SqlConnection，供各方法使用
    /// 呼叫端需以 await using 確保連線正確釋放
    /// </summary>
    private async Task<SqlConnection> CreateOpenConnectionAsync()
    {
        // 建立連線物件，注入預先讀取的連線字串
        var conn = new SqlConnection(_connectionString);
        // 非同步開啟連線，避免阻塞執行緒
        await conn.OpenAsync();
        return conn;
    }

    // ─────────────────────────────────────────────────────────────────
    // 1. 取得分頁列表（usp_Feedback_GetPagedList）
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<(IEnumerable<FeedbackListItemViewModel> Items, int TotalCount)> GetPagedListAsync(
        byte? status, byte? priority, int page, int pageSize)
    {
        var items = new List<FeedbackListItemViewModel>();
        int totalCount = 0;

        await using var conn = await CreateOpenConnectionAsync();
        await using var cmd = new SqlCommand("usp_Feedback_GetPagedList", conn)
        {
            // 指定以 SP 模式執行，而非直接執行 T-SQL
            CommandType = CommandType.StoredProcedure
        };

        // 傳入篩選參數（可為 null，對應 SP 中 nullable 的 @Status / @Priority）
        cmd.Parameters.AddWithValue("@Status", (object?)status ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Priority", (object?)priority ?? DBNull.Value);

        // 傳入分頁參數
        cmd.Parameters.AddWithValue("@Page", page);
        cmd.Parameters.AddWithValue("@PageSize", pageSize);

        // 宣告 OUTPUT 參數，用於接收 SP 回傳的總筆數
        var totalCountParam = new SqlParameter("@TotalCount", SqlDbType.Int)
        {
            Direction = ParameterDirection.Output
        };
        cmd.Parameters.Add(totalCountParam);

        // 執行 SP 並逐列讀取資料
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            // 將每列資料對應到 ViewModel
            items.Add(new FeedbackListItemViewModel
            {
                FeedbackId   = reader.GetInt32(reader.GetOrdinal("FeedbackId")),
                TrackingCode = reader.GetString(reader.GetOrdinal("TrackingCode")),
                CustomerName = reader.GetString(reader.GetOrdinal("CustomerName")),
                Category     = reader.GetString(reader.GetOrdinal("Category")),
                Subject      = reader.GetString(reader.GetOrdinal("Subject")),
                Status       = reader.GetByte(reader.GetOrdinal("Status")),
                Priority     = reader.GetByte(reader.GetOrdinal("Priority")),
                ReplyCount   = reader.GetInt32(reader.GetOrdinal("ReplyCount")),
                CreatedAt    = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
            });
        }

        // 關閉 DataReader 後才能讀取 OUTPUT 參數值
        await reader.CloseAsync();
        totalCount = (int)(totalCountParam.Value ?? 0);

        return (items, totalCount);
    }

    // ─────────────────────────────────────────────────────────────────
    // 2. 產生唯一 TrackingCode（usp_Feedback_CheckTrackingCodeExists）
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<string> GenerateUniqueTrackingCodeAsync()
    {
        // 以迴圈持續產生並確認唯一性，直到找到不重複的代碼
        while (true)
        {
            // 格式：FB + yyyyMMdd + 6 碼大寫亂數英數字
            string candidate = "FB"
                + DateTime.Now.ToString("yyyyMMdd")
                + GenerateRandomUpperCode(6);

            // 呼叫 SP 確認此代碼是否已存在
            bool exists = await CheckTrackingCodeExistsAsync(candidate);

            // 若不存在則回傳此代碼；若已存在則繼續迴圈重新產生
            if (!exists)
                return candidate;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> CheckTrackingCodeExistsAsync(string trackingCode)
    {
        await using var conn = await CreateOpenConnectionAsync();
        await using var cmd = new SqlCommand("usp_Feedback_CheckTrackingCodeExists", conn)
        {
            CommandType = CommandType.StoredProcedure
        };

        // 傳入待查詢的 TrackingCode（輸入參數）
        cmd.Parameters.AddWithValue("@TrackingCode", trackingCode);

        // 宣告 @Exists OUTPUT 參數，用於接收 SP 回傳的 0/1 結果
        // SP 設計為 OUTPUT 而非 SELECT，需明確加入才不會拋出「必須有參數 @Exists」錯誤
        var existsParam = new SqlParameter("@Exists", SqlDbType.Bit)
        {
            Direction = ParameterDirection.Output
        };
        cmd.Parameters.Add(existsParam);

        // SP 只寫 OUTPUT，不回傳結果集，使用 ExecuteNonQueryAsync 而非 ExecuteScalarAsync
        await cmd.ExecuteNonQueryAsync();

        // 讀取 OUTPUT 參數值：true = TrackingCode 已被佔用，false = 可安全使用
        return existsParam.Value != DBNull.Value && (bool)existsParam.Value;
    }

    /// <summary>
    /// 產生指定長度的大寫英數字亂數字串
    /// 用於組成 TrackingCode 的後六碼
    /// </summary>
    /// <param name="length">字串長度</param>
    private static string GenerateRandomUpperCode(int length)
    {
        // 可用字元集：大寫英文 + 數字，排除易混淆字元（O、0、I、1）
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    // ─────────────────────────────────────────────────────────────────
    // 3. 新增意見（usp_Feedback_Insert）
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<int> InsertFeedbackAsync(FeedbackCreateViewModel model)
    {
        await using var conn = await CreateOpenConnectionAsync();
        await using var cmd = new SqlCommand("usp_Feedback_Insert", conn)
        {
            CommandType = CommandType.StoredProcedure
        };

        // 傳入意見表單所有欄位，以參數化方式防止 SQL Injection
        cmd.Parameters.AddWithValue("@TrackingCode", model.TrackingCode);
        cmd.Parameters.AddWithValue("@CustomerName", model.CustomerName);
        cmd.Parameters.AddWithValue("@CustomerEmail", model.CustomerEmail);
        // CustomerPhone 為 nullable，null 需轉換為 DBNull.Value
        cmd.Parameters.AddWithValue("@CustomerPhone", (object?)model.CustomerPhone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Category", model.Category);
        cmd.Parameters.AddWithValue("@Subject", model.Subject);
        cmd.Parameters.AddWithValue("@Content", model.Content);

        // OUTPUT 參數：接收 SP 回傳的新 FeedbackId（IDENTITY 值）
        var newIdParam = new SqlParameter("@FeedbackId", SqlDbType.Int)
        {
            Direction = ParameterDirection.Output
        };
        cmd.Parameters.Add(newIdParam);

        await cmd.ExecuteNonQueryAsync();

        // 取得 SP 輸出的新 FeedbackId 並回傳
        return (int)newIdParam.Value;
    }

    // ─────────────────────────────────────────────────────────────────
    // 4. 取得單筆意見（usp_Feedback_GetById）
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<FeedbackDetailViewModel?> GetByIdAsync(int feedbackId)
    {
        await using var conn = await CreateOpenConnectionAsync();
        await using var cmd = new SqlCommand("usp_Feedback_GetById", conn)
        {
            CommandType = CommandType.StoredProcedure
        };

        cmd.Parameters.AddWithValue("@FeedbackId", feedbackId);

        await using var reader = await cmd.ExecuteReaderAsync();

        // 若查無資料則回傳 null
        if (!await reader.ReadAsync())
            return null;

        // 將資料列對應到 FeedbackDetailViewModel
        return new FeedbackDetailViewModel
        {
            FeedbackId           = reader.GetInt32(reader.GetOrdinal("FeedbackId")),
            TrackingCode         = reader.GetString(reader.GetOrdinal("TrackingCode")),
            CustomerName         = reader.GetString(reader.GetOrdinal("CustomerName")),
            CustomerEmail        = reader.GetString(reader.GetOrdinal("CustomerEmail")),
            // nullable 欄位需檢查 IsDBNull 後再取值
            CustomerPhone        = reader.IsDBNull(reader.GetOrdinal("CustomerPhone"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("CustomerPhone")),
            Category             = reader.GetString(reader.GetOrdinal("Category")),
            Subject              = reader.GetString(reader.GetOrdinal("Subject")),
            Content              = reader.GetString(reader.GetOrdinal("Content")),
            Status               = reader.GetByte(reader.GetOrdinal("Status")),
            Priority             = reader.GetByte(reader.GetOrdinal("Priority")),
            AdminNote            = reader.IsDBNull(reader.GetOrdinal("AdminNote"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("AdminNote")),
            LatestReplyContent   = reader.IsDBNull(reader.GetOrdinal("LatestReplyContent"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("LatestReplyContent")),
            LatestReplyAt        = reader.IsDBNull(reader.GetOrdinal("LatestReplyAt"))
                                    ? null
                                    : reader.GetDateTime(reader.GetOrdinal("LatestReplyAt")),
            ReplyCount           = reader.GetInt32(reader.GetOrdinal("ReplyCount")),
            CreatedAt            = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            UpdatedAt            = reader.GetDateTime(reader.GetOrdinal("UpdatedAt"))
        };
    }

    // ─────────────────────────────────────────────────────────────────
    // 5. 取得回覆串（usp_FeedbackReply_GetByFeedbackId）
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IEnumerable<FeedbackReplyViewModel>> GetRepliesByFeedbackIdAsync(int feedbackId)
    {
        var replies = new List<FeedbackReplyViewModel>();

        await using var conn = await CreateOpenConnectionAsync();
        await using var cmd = new SqlCommand("usp_FeedbackReply_GetByFeedbackId", conn)
        {
            CommandType = CommandType.StoredProcedure
        };

        cmd.Parameters.AddWithValue("@FeedbackId", feedbackId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            replies.Add(new FeedbackReplyViewModel
            {
                ReplyId     = reader.GetInt32(reader.GetOrdinal("ReplyId")),
                FeedbackId  = reader.GetInt32(reader.GetOrdinal("FeedbackId")),
                Content     = reader.GetString(reader.GetOrdinal("Content")),
                ReplierName = reader.GetString(reader.GetOrdinal("ReplierName")),
                ReplyType   = reader.GetByte(reader.GetOrdinal("ReplyType")),
                // DB bit 欄位以 GetBoolean 讀取
                IsPublic    = reader.GetBoolean(reader.GetOrdinal("IsPublic")),
                CreatedAt   = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
            });
        }

        return replies;
    }

    // ─────────────────────────────────────────────────────────────────
    // 6. 更新意見（usp_Feedback_Update）
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task UpdateFeedbackAsync(FeedbackEditViewModel model)
    {
        await using var conn = await CreateOpenConnectionAsync();
        await using var cmd = new SqlCommand("usp_Feedback_Update", conn)
        {
            CommandType = CommandType.StoredProcedure
        };

        // 傳入識別碼與所有可編輯欄位
        cmd.Parameters.AddWithValue("@FeedbackId", model.FeedbackId);
        cmd.Parameters.AddWithValue("@Category", model.Category);
        cmd.Parameters.AddWithValue("@Subject", model.Subject);
        cmd.Parameters.AddWithValue("@Content", model.Content);
        cmd.Parameters.AddWithValue("@Status", model.Status);
        cmd.Parameters.AddWithValue("@Priority", model.Priority);
        // AdminNote 為 nullable
        cmd.Parameters.AddWithValue("@AdminNote", (object?)model.AdminNote ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
    }

    // ─────────────────────────────────────────────────────────────────
    // 7. 新增回覆（usp_FeedbackReply_Insert）
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task InsertReplyAsync(FeedbackReplyCreateViewModel model)
    {
        // 注意：快取欄位同步與 Status 自動切換的 Transaction 邏輯
        // 已封裝在 usp_FeedbackReply_Insert SP 內部，
        // 此處 C# 程式碼無需另外管理 Transaction
        await using var conn = await CreateOpenConnectionAsync();
        await using var cmd = new SqlCommand("usp_FeedbackReply_Insert", conn)
        {
            CommandType = CommandType.StoredProcedure
        };

        // 傳入回覆所有欄位
        cmd.Parameters.AddWithValue("@FeedbackId", model.FeedbackId);
        cmd.Parameters.AddWithValue("@Content", model.Content);
        cmd.Parameters.AddWithValue("@ReplierName", model.ReplierName);
        cmd.Parameters.AddWithValue("@ReplyType", model.ReplyType);
        // bit 型別以 bool 傳入
        cmd.Parameters.AddWithValue("@IsPublic", model.IsPublic);

        await cmd.ExecuteNonQueryAsync();
    }
}
