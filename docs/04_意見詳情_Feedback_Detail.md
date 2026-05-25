# 畫面規格書：意見詳情

## 基本資訊

| 項目 | 內容 |
|------|------|
| 畫面名稱 | 意見詳情 |
| Controller | `FeedbackController` |
| Action | `Detail`（GET）、`AddReply`（POST） |
| View 路徑 | `Views/Feedback/Detail.cshtml` |
| 路由 URL | `/Feedback/Detail/{id}` |
| HTTP 方法 | GET（顯示詳情 + 回覆串）、POST（AddReply 新增回覆） |
| ViewModel | `FeedbackDetailViewModel` |
| 頁面標題 | 意見詳情 |

---

## 功能說明

顯示單筆意見的完整資訊，分為三個區塊：

1. **意見主體資訊**：顯示所有意見欄位（含管理員備註）
2. **回覆記錄串**：以對話氣泡方式呈現所有回覆，依回覆類型左右對齊並區分底色
3. **新增回覆表單**：頁面底部內嵌快速回覆表單

---

## 路由參數

| 參數 | 型別 | 必填 | 說明 |
|------|------|------|------|
| `id` | `int` | 是 | 意見唯一識別碼（FeedbackId） |

若 `id` 對應的意見不存在，回傳 **404 Not Found**。

---

## ViewModel：FeedbackDetailViewModel

### 意見主體欄位

| 欄位名稱 | 型別 | 說明 |
|---------|------|------|
| `FeedbackId` | `int` | 意見唯一識別碼 |
| `TrackingCode` | `string` | 追蹤代碼（`<code>` 標籤顯示） |
| `CustomerName` | `string` | 客戶姓名 |
| `CustomerEmail` | `string` | 客戶電子信箱 |
| `CustomerPhone` | `string?` | 客戶聯絡電話（null 時顯示「（未提供）」） |
| `Category` | `string` | 意見類別 |
| `Subject` | `string` | 意見主旨 |
| `Content` | `string` | 意見詳細內容（`pre-wrap` 保留換行） |
| `Status` | `byte` | 處理狀態（0~3，顯示於 card header badge） |
| `Priority` | `byte` | 優先等級（1~3，顯示於 card header badge） |
| `AdminNote` | `string?` | 管理員備註（僅後台顯示，有值才顯示藍色 alert 框） |
| `LatestReplyContent` | `string?` | 最新回覆內容快取 |
| `LatestReplyAt` | `DateTime?` | 最新回覆時間（有值才顯示） |
| `ReplyCount` | `int` | 累積回覆筆數 |
| `CreatedAt` | `DateTime` | 建立時間（格式：yyyy-MM-dd HH:mm:ss） |
| `UpdatedAt` | `DateTime` | 最後更新時間（格式：yyyy-MM-dd HH:mm:ss） |

### 回覆串欄位

| 欄位名稱 | 型別 | 說明 |
|---------|------|------|
| `Replies` | `List<FeedbackReplyViewModel>` | 所有回覆清單（含私密回覆） |

#### FeedbackReplyViewModel 欄位

| 欄位名稱 | 型別 | 說明 |
|---------|------|------|
| `ReplyId` | `int` | 回覆唯一識別碼 |
| `FeedbackId` | `int` | 所屬意見識別碼 |
| `Content` | `string` | 回覆內容（`pre-wrap` 保留換行） |
| `ReplierName` | `string` | 回覆者姓名 |
| `ReplyType` | `byte` | 回覆類型（0=客戶回覆、1=官方回覆） |
| `IsPublic` | `bool` | 是否公開（false 時顯示「私密」badge） |
| `CreatedAt` | `DateTime` | 回覆時間（格式：yyyy-MM-dd HH:mm） |

### 新增回覆表單

| 欄位名稱 | 型別 | 說明 |
|---------|------|------|
| `NewReply` | `FeedbackReplyCreateViewModel` | 嵌入的新增回覆表單資料（預設 ReplyType=1、IsPublic=true） |

---

## ViewBag 資料

| 鍵值 | 型別 | 說明 |
|------|------|------|
| `ReplyTypeList` | `List<SelectListItem>` | 回覆類型下拉清單（客戶回覆 / 官方回覆） |

---

## 表單驗證規則

### 新增回覆表單：FeedbackReplyCreateViewModel

| 欄位名稱 | 型別 | 必填 | 驗證規則 | 顯示名稱 | 說明 |
|---------|------|------|---------|---------|------|
| `FeedbackId` | `int` | 是 | `[Required]` | — | Hidden input 傳遞，確保回覆對應正確意見 |
| `ReplyType` | `byte` | 是 | `[Required]`、`[Range(0,1)]` | 回覆類型 | 下拉選單，預設 1（官方回覆） |
| `ReplierName` | `string` | 是 | `[Required]`、`[StringLength(100)]` | 回覆者姓名 | 最多 100 字元 |
| `Content` | `string` | 是 | `[Required]` | 回覆內容 | 長文字，不限長度 |
| `IsPublic` | `bool` | — | 無 | 公開顯示 | checkbox，預設勾選（公開） |

> **注意**：`ReplyId`、`CreatedAt` 不在此表單，由資料庫自動管理。

### 表單 Prefix 說明
表單欄位使用 `asp-for="NewReply.X"` 前綴，產生名稱如 `NewReply.Content`、`NewReply.FeedbackId`。
AddReply Action 使用 `[Bind(Prefix = "NewReply")]` 對應綁定。

### 前端 / 後端驗證
- 引入 `_ValidationScriptsPartial.cshtml`，啟用即時前端驗證
- 驗證失敗：重新載入詳情頁，保留使用者已輸入的回覆內容

---

## 畫面佈局

```
┌─────────────────────────────────────────────────────────────────────┐
│  意見詳情                    [編輯此意見 (btn-warning)] [返回列表]   │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│ ┌─ 意見資訊 (card) ─────────── [已回覆] [重要] ────────────────┐   │
│ │  追蹤代碼              建立時間              最後更新         │   │
│ │  FB20260504...         2026-05-04 10:00:00   2026-05-04 11:00│   │
│ │                                                               │   │
│ │  客戶姓名     電子信箱            聯絡電話                    │   │
│ │  王小明       abc@email.com       0912345678                  │   │
│ │                                                               │   │
│ │  意見類別   意見主旨                                          │   │
│ │  產品       這是意見主旨內容                                   │   │
│ │                                                               │   │
│ │  意見內容                                                     │   │
│ │  ┌─────────────────────────────────────────────────────┐     │   │
│ │  │ 這是詳細的意見內容，支援換行格式顯示...               │     │   │
│ │  └─────────────────────────────────────────────────────┘     │   │
│ │                                                               │   │
│ │  回覆總數：2 筆   最新回覆時間：2026-05-04 11:00              │   │
│ │                                                               │   │
│ │  [管理員備註：xxxxxx] (alert-info，有備註才顯示)              │   │
│ └──────────────────────────────────────────────────────────────┘   │
│                                                                      │
│  回覆記錄（2 筆）                                                    │
│                                                                      │
│  ┌─ 客戶回覆（靠左，灰底）────────────────────────────┐           │
│  │ [客戶回覆]             王小明 ｜ 2026-05-04 10:30   │           │
│  │ 這是客戶補充的內容...                               │           │
│  └─────────────────────────────────────────────────────┘           │
│                                                                      │
│           ┌─ 官方回覆（靠右，淡藍底）─────────────────────────┐    │
│           │ [官方回覆] [私密]     客服小李 ｜ 2026-05-04 11:00 │    │
│           │ 感謝您的反饋，我們已記錄...                        │    │
│           └───────────────────────────────────────────────────┘    │
│                                                                      │
│ ┌─ 新增回覆 (card) ──────────────────────────────────────────────┐ │
│ │  回覆類型  [官方回覆 ▼]   (max-width: 200px)                   │ │
│ │  回覆者姓名  [__________________]  (max-width: 300px)           │ │
│ │  回覆內容  ┌────────────────────────────────┐                  │ │
│ │            │                                │（4行高度）        │ │
│ │            └────────────────────────────────┘                  │ │
│ │  ☑ 公開顯示（取消勾選表示私密，僅後台可見）                    │ │
│ │  [送出回覆 (btn-success)]                                       │ │
│ └──────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 欄位顯示規則

### 回覆串顯示規則

| 屬性 | 客戶回覆（ReplyType=0） | 官方回覆（ReplyType=1） |
|------|------------------------|------------------------|
| 對齊方向 | 靠左（`justify-content-start`） | 靠右（`justify-content-end`） |
| 卡片底色 | `bg-light` | `bg-primary bg-opacity-10`（淡藍） |
| 邊框顏色 | 預設 | `border-primary` |
| 類型標籤 | `[客戶回覆]`（`bg-secondary`） | `[官方回覆]`（`bg-primary`） |
| 私密標籤 | `IsPublic=false` 時顯示 `[私密]`（`bg-warning text-dark`） | 同左 |
| 最大寬度 | 75% | 75% |

---

## 操作按鈕
| 返回列表 | `btn-outline-secondary` | `GET /Feedback` | 返回意見列表 |
| 送出回覆 | `btn-success` | `POST /Feedback/AddReply` | 提交新回覆 |

---

## 成功流程

```
使用者填寫回覆表單並提交
    → POST /Feedback/AddReply
    → [Bind(Prefix = "NewReply")] 綁定 FeedbackReplyCreateViewModel
    → ModelState 驗證
    → 驗證失敗：重新載入詳情頁（保留使用者已輸入內容）
    → 驗證通過：呼叫 InsertReplyAsync（SP 以 Transaction 同步快取欄位）
    → Redirect to GET /Feedback/Detail/{FeedbackId}（PRG Pattern 防重複送出）
```

---

## 資料來源

| 操作 | Service 方法 | Stored Procedure |
|------|------------|-----------------|
| 取得意見主體 | `GetByIdAsync(id)` | `usp_Feedback_GetById` |
| 取得回覆串 | `GetRepliesByFeedbackIdAsync(id)` | `usp_FeedbackReply_GetByFeedbackId` |
| 新增回覆 | `InsertReplyAsync(model)` | `usp_FeedbackReply_Insert`（含 Transaction 同步快取） |
