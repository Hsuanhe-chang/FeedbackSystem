-- =============================================
-- 異動說明：新增 @Keyword 參數，支援依客戶姓名、主旨、追蹤代碼進行模糊搜尋
-- 執行前提：Server=ymmistest, Database=Feedback_Test
-- 執行方式：直接在 SSMS 對 Feedback_Test 資料庫執行此腳本
-- =============================================

USE [Feedback_Test];
GO

ALTER PROCEDURE [dbo].[usp_Feedback_GetPagedList]
    -- 篩選參數：null 表示不篩選該欄位
    @Status     TINYINT       = NULL,
    @Priority   TINYINT       = NULL,
    -- 關鍵字搜尋：null 表示不套用關鍵字篩選
    -- 模糊比對 CustomerName、Subject、TrackingCode 任一欄位
    @Keyword    NVARCHAR(100) = NULL,
    -- 分頁參數
    @Page       INT           = 1,
    @PageSize   INT           = 10,
    -- OUTPUT：符合篩選條件的總筆數（供前端計算總頁數）
    @TotalCount INT           OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- ── 步驟一：計算符合條件的總筆數 ──────────────────────────────────
    -- 同時套用 Status、Priority、Keyword 三個篩選條件（null 時略過）
    SELECT @TotalCount = COUNT(*)
    FROM   dbo.Feedback
    WHERE  (@Status   IS NULL OR Status   = @Status)
      AND  (@Priority IS NULL OR Priority = @Priority)
      -- 關鍵字模糊比對：CustomerName、Subject、TrackingCode 任一包含即符合
      AND  (
               @Keyword IS NULL
               OR CustomerName  LIKE N'%' + @Keyword + N'%'
               OR Subject       LIKE N'%' + @Keyword + N'%'
               OR TrackingCode  LIKE N'%' + @Keyword + N'%'
           );

    -- ── 步驟二：取出當頁資料 ──────────────────────────────────────────
    -- 使用 OFFSET/FETCH 實作分頁，依建立時間倒序排列（最新在上）
    SELECT
        FeedbackId,
        TrackingCode,
        CustomerName,
        Category,
        Subject,
        Status,
        Priority,
        ReplyCount,
        CreatedAt
    FROM   dbo.Feedback
    WHERE  (@Status   IS NULL OR Status   = @Status)
      AND  (@Priority IS NULL OR Priority = @Priority)
      AND  (
               @Keyword IS NULL
               OR CustomerName  LIKE N'%' + @Keyword + N'%'
               OR Subject       LIKE N'%' + @Keyword + N'%'
               OR TrackingCode  LIKE N'%' + @Keyword + N'%'
           )
    ORDER BY CreatedAt DESC
    OFFSET  (@Page - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END
GO
