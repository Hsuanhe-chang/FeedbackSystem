---
description: "Use when designing or implementing web page features, forms, ViewModels, Controllers, and Views related to Feedback or FeedbackReply. Contains the complete DB schema for Feedback_Test database (Server: ymmistest) including column types, constraints, nullable rules, enum values, and table relationships."
applyTo: "**/*.cs,**/*.cshtml"
---

# Feedback_Test 資料庫 Schema 規範

資料來源：Server `ymmistest`，Database `Feedback_Test`

撰寫任何與意見回饋相關的功能畫面、ViewModel、Controller 或 Service 時，**必須嚴格對照此文件**定義欄位名稱、型別、驗證規則與列舉值。

---

## 資料表關係圖

```
Feedback (主表)
    └── FeedbackReply (子表，多筆回覆對應一筆意見)
        FeedbackReply.FeedbackId → Feedback.FeedbackId  (FK_FeedbackReply_Feedback)
```

---

## 一、dbo.Feedback（意見主表）

| # | 欄位名稱 | SQL 型別 | 可空 | 預設值 | 鍵 / 約束 | 說明 |
|---|---------|---------|------|--------|----------|------|
| 1 | `FeedbackId` | `int` | NOT NULL | IDENTITY(1,1) | **PK**，自動遞增 | 意見唯一識別碼 |
| 2 | `TrackingCode` | `nvarchar(20)` | NOT NULL | — | — | 意見追蹤代碼（供客戶查詢進度用） |
| 3 | `CustomerName` | `nvarchar(100)` | NOT NULL | — | — | 客戶姓名 |
| 4 | `CustomerEmail` | `nvarchar(200)` | NOT NULL | — | — | 客戶電子信箱（需符合 Email 格式） |
| 5 | `CustomerPhone` | `nvarchar(30)` | **NULL** | — | — | 客戶聯絡電話（選填） |
| 6 | `Category` | `nvarchar(50)` | NOT NULL | — | — | 意見類別（例如：產品、服務、建議等） |
| 7 | `Subject` | `nvarchar(200)` | NOT NULL | — | — | 意見主旨 |
| 8 | `Content` | `nvarchar(MAX)` | NOT NULL | — | — | 意見詳細內容（長文字，不限長度） |
| 9 | `Status` | `tinyint` | NOT NULL | `0` | CK: 0\|1\|2\|3 | **處理狀態**（見下方列舉說明） |
| 10 | `Priority` | `tinyint` | NOT NULL | `1` | CK: 1\|2\|3 | **優先等級**（見下方列舉說明） |
| 11 | `AdminNote` | `nvarchar(MAX)` | **NULL** | — | — | 管理員內部備註（選填，不對客戶顯示） |
| 12 | `LatestReplyContent` | `nvarchar(MAX)` | **NULL** | — | 非正規化快取 | 最新一筆回覆內容（由系統自動同步，勿手動賦值） |
| 13 | `LatestReplyAt` | `datetime2` | **NULL** | — | 非正規化快取 | 最新回覆時間（由系統自動同步，勿手動賦值） |
| 14 | `ReplyCount` | `int` | NOT NULL | `0` | 非正規化快取 | 累積回覆筆數（由系統自動維護，勿手動賦值） |
| 15 | `CreatedAt` | `datetime2` | NOT NULL | `getdate()` | — | 建立時間（由資料庫自動填入，ViewModel 僅顯示，不允許輸入） |
| 16 | `UpdatedAt` | `datetime2` | NOT NULL | `getdate()` | — | 最後更新時間（由資料庫自動填入，ViewModel 僅顯示，不允許輸入） |

### Status 列舉值（意見處理狀態）

| 數值 | 名稱 | 說明 | 建議顯示文字 |
|-----|------|------|------------|
| `0` | Pending | 待處理（新進意見，尚未有人受理） | 待處理 |
| `1` | Processing | 處理中（已有人受理但尚未回覆完畢） | 處理中 |
| `2` | Replied | 已回覆（至少有一筆正式回覆） | 已回覆 |
| `3` | Closed | 已關閉（意見處理完畢，不再受理） | 已關閉 |

### Priority 列舉值（優先等級）

| 數值 | 名稱 | 說明 | 建議顯示文字 | 建議顯示顏色 |
|-----|------|------|------------|------------|
| `1` | Low | 一般（預設值，非緊急事項） | 一般 | 灰色 / 預設 |
| `2` | High | 重要（需較快處理） | 重要 | 橙色 / 警告 |
| `3` | Urgent | 緊急（需立即處理） | 緊急 | 紅色 / 危險 |

---

## 二、dbo.FeedbackReply（回覆子表）

| # | 欄位名稱 | SQL 型別 | 可空 | 預設值 | 鍵 / 約束 | 說明 |
|---|---------|---------|------|--------|----------|------|
| 1 | `ReplyId` | `int` | NOT NULL | IDENTITY(1,1) | **PK**，自動遞增 | 回覆唯一識別碼 |
| 2 | `FeedbackId` | `int` | NOT NULL | — | **FK** → `Feedback.FeedbackId` | 所屬意見的識別碼（關聯主表） |
| 3 | `Content` | `nvarchar(MAX)` | NOT NULL | — | — | 回覆詳細內容（長文字，不限長度） |
| 4 | `ReplierName` | `nvarchar(100)` | NOT NULL | — | — | 回覆者姓名（客戶或管理員的名稱） |
| 5 | `ReplyType` | `tinyint` | NOT NULL | — | CK: 0\|1 | **回覆類型**（見下方列舉說明） |
| 6 | `IsPublic` | `bit` | NOT NULL | `1` | — | 是否公開顯示給客戶（`1`=公開，`0`=私密僅後台可見） |
| 7 | `CreatedAt` | `datetime2` | NOT NULL | `getdate()` | — | 回覆建立時間（由資料庫自動填入，ViewModel 僅顯示，不允許輸入） |

### ReplyType 列舉值（回覆類型）

| 數值 | 名稱 | 說明 | 建議顯示文字 |
|-----|------|------|------------|
| `0` | CustomerReply | 客戶追加回覆（客戶補充說明或追問） | 客戶回覆 |
| `1` | AdminReply | 管理員正式回覆（處理人員的官方答覆） | 官方回覆 |

---

## 三、撰寫功能畫面的強制規範

### 3.1 ViewModel 欄位對應規則

- **主鍵、非正規化快取欄位、時間戳記欄位**（FeedbackId、ReplyId、ReplyCount、LatestReplyContent、LatestReplyAt、CreatedAt、UpdatedAt）在**新增 ViewModel** 中**不得出現**（由資料庫或系統自動管理）
- **編輯/詳情 ViewModel** 中，`CreatedAt`、`UpdatedAt` 等時間戳記欄位僅供**唯讀顯示**，不可做為表單 input
- 所有 `tinyint` 列舉欄位（Status、Priority、ReplyType）在 ViewModel 必須使用 `byte` 型別，並搭配 `[Range]` Data Annotation 加上允許值範圍驗證
- `CustomerPhone` 為 nullable，ViewModel 對應使用 `string?`，並加上 `[Phone]` Data Annotation

### 3.2 表單驗證規範

| 欄位 | ViewModel 型別 | Data Annotation 範例 |
|------|--------------|---------------------|
| `TrackingCode` | `string` | `[Required][StringLength(20)]` |
| `CustomerName` | `string` | `[Required][StringLength(100)]` |
| `CustomerEmail` | `string` | `[Required][StringLength(200)][EmailAddress]` |
| `CustomerPhone` | `string?` | `[StringLength(30)][Phone]` |
| `Category` | `string` | `[Required][StringLength(50)]` |
| `Subject` | `string` | `[Required][StringLength(200)]` |
| `Content` | `string` | `[Required]` |
| `Status` | `byte` | `[Required][Range(0, 3)]` |
| `Priority` | `byte` | `[Required][Range(1, 3)]` |
| `AdminNote` | `string?` | （無特別限制） |
| `ReplierName` | `string` | `[Required][StringLength(100)]` |
| `ReplyType` | `byte` | `[Required][Range(0, 1)]` |
| `IsPublic` | `bool` | （無需額外 Annotation） |

### 3.3 頁面功能設計指引

#### 意見列表頁面（Feedback List）
- 顯示欄位：TrackingCode、CustomerName、Category、Subject、Status（轉換為中文標籤）、Priority（轉換為顏色標籤）、ReplyCount、CreatedAt
- 需支援依 Status、Priority 篩選
- 分頁顯示（避免一次載入全部資料）
- Status 與 Priority 需以視覺化標籤（badge）呈現，顏色對應上方列舉說明

#### 意見詳情頁面（Feedback Detail）
- 顯示意見主體所有欄位（含 AdminNote 供後台管理員閱覽）
- 下方顯示回覆串（FeedbackReply 清單）
  - 區分 ReplyType = 0（客戶）與 = 1（官方），UI 需有視覺差異
  - IsPublic = 0 的回覆僅後台可見，前台不顯示
- 提供新增回覆的快速表單

#### 意見新增頁面（Feedback Create）
- 表單欄位：TrackingCode（可自動產生）、CustomerName、CustomerEmail、CustomerPhone（選填）、Category（下拉選單）、Subject、Content
- Status、Priority、ReplyCount、LatestReplyContent、LatestReplyAt、CreatedAt、UpdatedAt **不出現在新增表單**

#### 意見編輯頁面（Feedback Edit，後台）
- 可編輯欄位：Category、Subject、Content、Status（下拉選單）、Priority（下拉選單）、AdminNote
- TrackingCode、CustomerName、CustomerEmail、CreatedAt 等識別性欄位建議唯讀顯示

#### 回覆新增表單（FeedbackReply Create）
- 表單欄位：Content、ReplierName、ReplyType（下拉選單）、IsPublic（核取方塊）
- FeedbackId 由路由或隱藏欄位傳遞，CreatedAt 不出現在表單

### 3.4 下拉選單資料來源

```csharp
// Status 下拉選單（後台管理用）
var statusList = new List<SelectListItem>
{
    new SelectListItem { Value = "0", Text = "待處理" },
    new SelectListItem { Value = "1", Text = "處理中" },
    new SelectListItem { Value = "2", Text = "已回覆" },
    new SelectListItem { Value = "3", Text = "已關閉" }
};

// Priority 下拉選單
var priorityList = new List<SelectListItem>
{
    new SelectListItem { Value = "1", Text = "一般" },
    new SelectListItem { Value = "2", Text = "重要" },
    new SelectListItem { Value = "3", Text = "緊急" }
};

// ReplyType 下拉選單
var replyTypeList = new List<SelectListItem>
{
    new SelectListItem { Value = "0", Text = "客戶回覆" },
    new SelectListItem { Value = "1", Text = "官方回覆" }
};
```
