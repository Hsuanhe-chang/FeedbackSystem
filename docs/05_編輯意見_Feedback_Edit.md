# 畫面規格書：編輯意見（後台）

## 基本資訊

| 項目 | 內容 |
|------|------|
| 畫面名稱 | 編輯意見（後台管理） |
| Controller | `FeedbackController` |
| Action | `Edit` |
| View 路徑 | `Views/Feedback/Edit.cshtml` |
| 路由 URL | `/Feedback/Edit/{id}` |
| HTTP 方法 | GET（載入編輯表單）、POST（儲存變更） |
| ViewModel | `FeedbackEditViewModel` |
| 頁面標題 | 編輯意見 |

---

## 功能說明

後台管理員修改指定意見的內容、狀態與優先等級。頁面分為兩個區塊：

1. **唯讀識別資訊**（卡片區塊）：顯示追蹤代碼、客戶姓名、電子信箱、建立時間，**不可修改**
2. **可編輯表單**：意見類別、處理狀態、優先等級、意見主旨、意見內容、管理員備註

---

## 路由參數

| 參數 | 型別 | 必填 | 說明 |
|------|------|------|------|
| `id` | `int` | 是 | 意見唯一識別碼（FeedbackId） |

若 `id` 對應的意見不存在，回傳 **404 Not Found**。

---

## ViewModel：FeedbackEditViewModel

### 唯讀顯示欄位（非表單 input）

| 欄位名稱 | 型別 | 顯示位置 | 說明 |
|---------|------|---------|------|
| `TrackingCode` | `string` | 唯讀識別資訊卡片 | 追蹤代碼（`<code>` 標籤顯示） |
| `CustomerName` | `string` | 唯讀識別資訊卡片 | 客戶姓名 |
| `CustomerEmail` | `string` | 唯讀識別資訊卡片 | 客戶電子信箱 |
| `CreatedAt` | `DateTime` | 唯讀識別資訊卡片 | 建立時間（格式：yyyy-MM-dd HH:mm:ss） |

### 可編輯欄位（表單 input）

| 欄位名稱 | 型別 | 必填 | 驗證規則 | 顯示名稱 | 說明 |
|---------|------|------|---------|---------|------|
| `FeedbackId` | `int` | 是 | `[Required]` | — | Hidden input 傳遞，防止偽造請求 |
| `Category` | `string` | 是 | `[Required]`、`[StringLength(50)]` | 意見類別 | 下拉選單：產品 / 服務 / 建議 / 其他 |
| `Status` | `byte` | 是 | `[Required]`、`[Range(0,3)]` | 處理狀態 | 下拉選單：待處理 / 處理中 / 已回覆 / 已關閉 |
| `Priority` | `byte` | 是 | `[Required]`、`[Range(1,3)]` | 優先等級 | 下拉選單：一般 / 重要 / 緊急 |
| `Subject` | `string` | 是 | `[Required]`、`[StringLength(200)]` | 意見主旨 | 最多 200 字元 |
| `Content` | `string` | 是 | `[Required]` | 意見內容 | 長文字，不限長度（文字方塊 6 行高） |
| `AdminNote` | `string?` | 否 | 無 | 管理員備註 | 選填，不對客戶顯示（文字方塊 3 行高） |

> **注意**：`CustomerPhone`、`LatestReplyContent`、`LatestReplyAt`、`ReplyCount`、`UpdatedAt` 等欄位**不出現在此頁面**，由系統自動管理。

---

## ViewBag 資料

| 鍵值 | 型別 | 說明 |
|------|------|------|
| `CategoryList` | `List<SelectListItem>` | 意見類別下拉清單 |
| `StatusList` | `List<SelectListItem>` | 處理狀態下拉清單 |
| `PriorityList` | `List<SelectListItem>` | 優先等級下拉清單 |

### 下拉選單選項

**CategoryList（意見類別）**

| Value | Text |
|-------|------|
| `產品` | 產品 |
| `服務` | 服務 |
| `建議` | 建議 |
| `其他` | 其他 |

**StatusList（處理狀態）**

| Value | Text |
|-------|------|
| `0` | 待處理 |
| `1` | 處理中 |
| `2` | 已回覆 |
| `3` | 已關閉 |

**PriorityList（優先等級）**

| Value | Text |
|-------|------|
| `1` | 一般 |
| `2` | 重要 |
| `3` | 緊急 |

---

## 表單驗證規則

### 前端驗證（jQuery Validation）
- 引入 `_ValidationScriptsPartial.cshtml`，啟用即時前端驗證
- 各欄位標示下方顯示紅色錯誤訊息（`text-danger small`）
- 整體錯誤摘要顯示於表單頂部（`asp-validation-summary="ModelOnly"`）

### 後端驗證
- `ModelState.IsValid` 驗證所有 Data Annotations
- 驗證失敗：回傳原表單，保留使用者已輸入的內容與錯誤訊息
- 路由 `id` 與表單 `FeedbackId` 不一致時：回傳 **400 BadRequest**（防止偽造請求）

---

## 畫面佈局

```
┌──────────────────────────────────────────────────────────────┐
│  編輯意見                              [返回詳情 (btn-secondary)]│
├──────────────────────────────────────────────────────────────┤
│  [驗證錯誤摘要區（ModelOnly）]                                │
│                                                              │
│ ┌─ 識別資訊（唯讀，不可修改）─────────────────────────────┐  │
│ │  追蹤代碼              客戶姓名          電子信箱         │  │
│ │  FB20260504A3F9K2      王小明            abc@email.com   │  │
│ │                                                          │  │
│ │  建立時間                                                │  │
│ │  2026-05-04 10:00:00                                    │  │
│ └──────────────────────────────────────────────────────────┘  │
│                                                              │
│ ┌─ 可編輯表單 ─────────────────────────────────────────────┐  │
│ │  意見類別       處理狀態          優先等級                │  │
│ │  [產品 ▼]      [已回覆 ▼]        [重要 ▼]               │  │
│ │                                                          │  │
│ │  意見主旨                                                │  │
│ │  [__________________________________________________]   │  │
│ │                                                          │  │
│ │  意見內容                                                │  │
│ │  ┌──────────────────────────────────────────────────┐   │  │
│ │  │                                                  │   │  │
│ │  │（6行高度）                                        │   │  │
│ │  └──────────────────────────────────────────────────┘   │  │
│ │                                                          │  │
│ │  管理員備註 (選填，不對客戶顯示)                         │  │
│ │  ┌──────────────────────────────────────────────────┐   │  │
│ │  │（3行高度）                                        │   │  │
│ │  └──────────────────────────────────────────────────┘   │  │
│ │                                                          │  │
│ │  [儲存變更 (btn-primary)]  [取消 (btn-outline-secondary)]│  │
│ └──────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────┘
```

---

## 操作按鈕

| 按鈕 | 樣式 | 目標 | 說明 |
|------|------|------|------|
| 返回詳情 | `btn-outline-secondary`（右上角） | `GET /Feedback/Detail/{id}` | 放棄編輯，返回詳情頁 |
| 儲存變更 | `btn-primary` | `POST /Feedback/Edit/{id}` | 送出表單，驗證通過後更新 DB |
| 取消 | `btn-outline-secondary` | `GET /Feedback/Detail/{id}` | 放棄編輯，返回詳情頁 |

---

## 成功流程

```
GET /Feedback/Edit/{id}
    → 取得意見資料（usp_Feedback_GetById）
    → 對應至 FeedbackEditViewModel
    → 顯示表單（唯讀區塊 + 可編輯欄位帶入現有值）

管理員修改並提交
    → POST /Feedback/Edit/{id}
    → 驗證路由 id == FeedbackId（防偽造）
    → ModelState 驗證
    → 驗證失敗：回傳表單並顯示錯誤
    → 驗證通過：呼叫 UpdateFeedbackAsync（usp_Feedback_Update，SP 內自動更新 UpdatedAt）
    → Redirect to GET /Feedback/Detail/{FeedbackId}
```

---

## 資料來源

| 操作 | Service 方法 | Stored Procedure |
|------|------------|-----------------|
| 讀取意見資料 | `GetByIdAsync(id)` | `usp_Feedback_GetById` |
| 更新意見 | `UpdateFeedbackAsync(model)` | `usp_Feedback_Update` |
