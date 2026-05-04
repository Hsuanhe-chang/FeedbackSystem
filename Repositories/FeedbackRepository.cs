using System.Data;
using FeedbackSystem.Models.ViewModels;
using Microsoft.Data.SqlClient;

namespace FeedbackSystem.Repositories;

/// <summary>
/// IFeedbackRepository 的 ADO.NET 實作。
/// 將所有 Stored Procedure 呼叫集中於此，
/// 讓 FeedbackService 專注於商業邏輯，不直接持有 SqlConnection。
/// 連線字串從 IConfiguration（appsettings.json）讀取。
/// </summary>
public class FeedbackRepository : IFeedbackRepository
{
    // 儲存連線字串，由 DI 注入的 IConfiguration 取得
    private readonly string _connectionString;

    /// <summary>
    /// 建構子：透過 DI 注入 IConfiguration，取得 FeedbackDb 連線字串
    /// </summary>
    /// <param name="configuration">ASP.NET Core 設定物件</param>
    /// <exception cref="InvalidOperationException">連線字串未設定時拋出例外</exception>
    public FeedbackRepository(IConfiguration configuration)
    {
        // 讀取 appsettings.json 中的 ConnectionStrings:FeedbackDb
        _connectionString = configuration.GetConnectionString("FeedbackDb")
            ?? throw new InvalidOperationException("ConnectionStrings:FeedbackDb 未設定，請檢查 appsettings.json");
    }

    // ─────────────────────────────────────────────────────────────────
    // 工具方法：建立並開啟 SqlConnection
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 建立並非同步開啟一個 SqlConnection。
    /// 呼叫端需以 await using 確保連線正確釋放。
    /// </summary>
    private async Task<SqlConnection> CreateOpenConnectionAsync()
    {
        var conn = new SqlConnection(_connectionString);
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
            CommandType = CommandType.StoredProcedure
        };

        // 傳入篩選參數（null 對應 SP 中的 nullable 參數）
        cmd.Parameters.AddWithValue("@Status",   (object?)status   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Priority", (object?)priority ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Page",     page);
        cmd.Parameters.AddWithValue("@PageSize", pageSize);

        // 宣告 OUTPUT 參數，接收 SP 回傳的總筆數
        var totalCountParam = new SqlParameter("@TotalCount", SqlDbType.Int)
        {
            Direction = ParameterDirection.Output
        };
        cmd.Parameters.Add(totalCountParam);

        // 執行 SP 並逐列讀取
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
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

        // 關閉 DataReader 後才可讀取 OUTPUT 參數值
        await reader.CloseAsync();
        totalCount = (int)(totalCountParam.Value ?? 0);

        return (items, totalCount);
    }

    // ─────────────────────────────────────────────────────────────────
    // 2. 確認 TrackingCode 是否存在（usp_Feedback_CheckTrackingCodeExists）
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<bool> CheckTrackingCodeExistsAsync(string trackingCode)
    {
        await using var conn = await CreateOpenConnectionAsync();
        await using var cmd = new SqlCommand("usp_Feedback_CheckTrackingCodeExists", conn)
        {
            CommandType = CommandType.StoredProcedure
        };

        cmd.Parameters.AddWithValue("@TrackingCode", trackingCode);

        // 宣告 OUTPUT 參數接收 SP 回傳的 0/1 結果
        var existsParam = new SqlParameter("@Exists", SqlDbType.Bit)
        {
            Direction = ParameterDirection.Output
        };
        cmd.Parameters.Add(existsParam);

        // SP 僅寫 OUTPUT，使用 ExecuteNonQueryAsync 而非 ExecuteScalarAsync
        await cmd.ExecuteNonQueryAsync();

        // true = TrackingCode 已存在，false = 可安全使用
        return existsParam.Value != DBNull.Value && (bool)existsParam.Value;
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

        // 以參數化方式傳入，防止 SQL Injection
        cmd.Parameters.AddWithValue("@TrackingCode",   model.TrackingCode);
        cmd.Parameters.AddWithValue("@CustomerName",   model.CustomerName);
        cmd.Parameters.AddWithValue("@CustomerEmail",  model.CustomerEmail);
        // CustomerPhone 為 nullable，null 轉換為 DBNull.Value
        cmd.Parameters.AddWithValue("@CustomerPhone",  (object?)model.CustomerPhone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Category",       model.Category);
        cmd.Parameters.AddWithValue("@Subject",        model.Subject);
        cmd.Parameters.AddWithValue("@Content",        model.Content);

        // OUTPUT 參數：接收 SP 回傳的新 FeedbackId（IDENTITY 值）
        var newIdParam = new SqlParameter("@FeedbackId", SqlDbType.Int)
        {
            Direction = ParameterDirection.Output
        };
        cmd.Parameters.Add(newIdParam);

        await cmd.ExecuteNonQueryAsync();

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

        // 將資料列對應到 ViewModel（nullable 欄位需先檢查 IsDBNull）
        return new FeedbackDetailViewModel
        {
            FeedbackId           = reader.GetInt32(reader.GetOrdinal("FeedbackId")),
            TrackingCode         = reader.GetString(reader.GetOrdinal("TrackingCode")),
            CustomerName         = reader.GetString(reader.GetOrdinal("CustomerName")),
            CustomerEmail        = reader.GetString(reader.GetOrdinal("CustomerEmail")),
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

        cmd.Parameters.AddWithValue("@FeedbackId", model.FeedbackId);
        cmd.Parameters.AddWithValue("@Category",   model.Category);
        cmd.Parameters.AddWithValue("@Subject",    model.Subject);
        cmd.Parameters.AddWithValue("@Content",    model.Content);
        cmd.Parameters.AddWithValue("@Status",     model.Status);
        cmd.Parameters.AddWithValue("@Priority",   model.Priority);
        // AdminNote 為 nullable，null 轉換為 DBNull.Value
        cmd.Parameters.AddWithValue("@AdminNote",  (object?)model.AdminNote ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
    }

    // ─────────────────────────────────────────────────────────────────
    // 7. 新增回覆（usp_FeedbackReply_Insert）
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task InsertReplyAsync(FeedbackReplyCreateViewModel model)
    {
        // Transaction 邏輯已封裝在 SP 內部，C# 端無需額外管理
        await using var conn = await CreateOpenConnectionAsync();
        await using var cmd = new SqlCommand("usp_FeedbackReply_Insert", conn)
        {
            CommandType = CommandType.StoredProcedure
        };

        cmd.Parameters.AddWithValue("@FeedbackId",  model.FeedbackId);
        cmd.Parameters.AddWithValue("@Content",     model.Content);
        cmd.Parameters.AddWithValue("@ReplierName", model.ReplierName);
        cmd.Parameters.AddWithValue("@ReplyType",   model.ReplyType);
        // bit 型別以 bool 傳入
        cmd.Parameters.AddWithValue("@IsPublic",    model.IsPublic);

        await cmd.ExecuteNonQueryAsync();
    }
}
