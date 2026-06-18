-- ============================================================
-- SP 名稱：usp_Feedback_GetByTrackingCode
-- 資料庫：Feedback_Test（Server: ymmistest）
-- 用途：依 TrackingCode 查詢單筆意見的完整資料，
--       供前台客戶查詢進度頁（/Feedback/Track）使用。
--       回傳欄位與 usp_Feedback_GetById 完全相同，
--       差異僅在於篩選條件改為 TrackingCode。
--       C# 層（FeedbackService.GetPublicByTrackingCodeAsync）
--       負責在回傳前過濾不公開欄位（AdminNote 等）。
-- 建立日期：2026-06-18
-- ============================================================

CREATE OR ALTER PROCEDURE dbo.usp_Feedback_GetByTrackingCode
    @TrackingCode NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        FeedbackId,
        TrackingCode,
        CustomerName,
        CustomerEmail,
        CustomerPhone,
        Category,
        Subject,
        Content,
        Status,
        Priority,
        AdminNote,
        LatestReplyContent,
        LatestReplyAt,
        ReplyCount,
        CreatedAt,
        UpdatedAt
    FROM
        dbo.Feedback
    WHERE
        TrackingCode = @TrackingCode;
    -- 若找不到資料則回傳空結果集（0 列），C# 端判斷 reader.ReadAsync() 回傳 false
END;
GO
