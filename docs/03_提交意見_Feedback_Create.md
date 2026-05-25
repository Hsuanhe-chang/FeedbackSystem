# 畫面規格書：提交意見（前台）

## 基本資訊

| 項目 | 內容 |
|------|------|
| 畫面名稱 | 提交意見（前台新增） |
| Controller | `FeedbackController` |
| Action | `Create` |
| View 路徑 | `Views/Feedback/Create.cshtml` |
| 路由 URL | `/Feedback/Create` |
| HTTP 方法 | GET（顯示表單）、POST（送出意見） |
| ViewModel | `FeedbackCreateViewModel` |
| 頁面標題 | 提交意見 |

---

## 功能說明

前台供客戶填寫並提交意見的表單頁面：

1. **GET**：後端自動產生唯一追蹤代碼（TrackingCode），以唯讀方式顯示在表單頂部，客戶需記錄此代碼以便日後查詢進度
2. **POST**：驗證表單資料，再次確認 TrackingCode 唯一性後寫入資料庫，成功後自動導向「意見詳情頁」

### TrackingCode 格式
`FB` + `yyyyMMdd` + `6碼大寫亂數英數字`
範例：`FB20260504A3F9K2`

---

## ViewModel：FeedbackCreateViewModel

### 欄位定義

| 欄位名稱 | 型別 | 必填 | 驗證規則 | 顯示名稱 | 說明 |
|---------|------|------|---------|---------|------|
| `TrackingCode` | `string` | 是 | `[Required]`、`[StringLength(20)]` | 追蹤代碼 | 由後端自動產生，hidden input 傳遞 + 唯讀顯示，不允許使用者修改 |
| `CustomerName` | `string` | 是 | `[Required]`、`[StringLength(100)]` | 客戶姓名 | 最多 100 字元 |
| `CustomerEmail` | `string` | 是 | `[Required]`、`[StringLength(200)]`、`[EmailAddress]` | 電子信箱 | 需符合 Email 格式，最多 200 字元 |
| `CustomerPhone` | `string?` | 否 | `[StringLength(30)]`、`[RegularExpression]` | 聯絡電話 | 選填；若填寫須為純數字，市話（0212345678）或手機（0912345678）格式 |
| `Category` | `string` | 是 | `[Required]`、`[StringLength(50)]` | 意見類別 | 下拉選單，選項：產品 / 服務 / 建議 / 其他 |
| `Subject` | `string` | 是 | `[Required]`、`[StringLength(200)]` | 意見主旨 | 最多 200 字元 |
| `Content` | `string` | 是 | `[Required]` | 意見內容 | 長文字，不限長度 |

> **注意**：`Status`、`Priority`、`ReplyCount`、`LatestReplyContent`、`LatestReplyAt`、`CreatedAt`、`UpdatedAt` 等系統欄位**不出現在此表單**，由資料庫預設值管理。

---

## ViewBag 資料

| 鍵值 | 型別 | 說明 |
|------|------|------|
| `CategoryList` | `List<SelectListItem>` | 意見類別下拉清單 |

### CategoryList 選項

| Value | Text |
|-------|------|
| `產品` | 產品 |
| `服務` | 服務 |
| `建議` | 建議 |
| `其他` | 其他 |

---

## 表單驗證規則

### 前端驗證（jQuery Validation）
- 引入 `_ValidationScriptsPartial.cshtml`，啟用即時前端驗證
- 各欄位標示下方顯示紅色錯誤訊息（`<span asp-validation-for>` + `text-danger small`）

### 後端驗證
- `ModelState.IsValid` 驗證所有 Data Annotations
- 驗證失敗：回傳原表單，顯示各欄位錯誤訊息
- TrackingCode 重複檢查：呼叫 `usp_Feedback_CheckTrackingCodeExists`
  - 若重複：自動重新產生 TrackingCode，於表單頂部顯示提示訊息「追蹤代碼已重複，已自動重新產生，請確認後再提交。」

### 電話號碼正規表示式
```
^(0[2-8]\d{6,8}|09\d{8})$
```
- 市話：`0[2-8]` 開頭，共 8~10 碼純數字（例如 0212345678）
- 手機：`09` 開頭，共 10 碼純數字（例如 0912345678）

---

## 畫面佈局

```
┌────────────────────────────────────────────────┐
│  提交意見                                        │
│  [驗證錯誤摘要區（ModelOnly）]                   │
│                                                  │
│  追蹤代碼                                        │
│  [FB20260504A3F9K2            ] (唯讀，bg-light) │
│  系統自動產生，請記錄此代碼以便日後查詢意見進度。   │
│                                                  │
│  客戶姓名 *                                      │
│  [__________________________]                    │
│                                                  │
│  電子信箱 *                                      │
│  [__________________________]                    │
│                                                  │
│  聯絡電話 (選填)                                 │
│  [__________________________]                    │
│  純數字即可，例如：0212345678 或 0912345678       │
│                                                  │
│  意見類別 *                                      │
│  [-- 請選擇類別 --          ▼]                   │
│                                                  │
│  意見主旨 *                                      │
│  [__________________________]                    │
│                                                  │
│  意見內容 *                                      │
│  ┌──────────────────────────┐                   │
│  │                          │（6行高度）         │
│  └──────────────────────────┘                   │
│                                                  │
│  [提交意見 (btn-primary)]  [取消 (btn-outline-secondary)] │
└────────────────────────────────────────────────┘
```

---

## 操作按鈕

| 按鈕 | 樣式 | 目標 | 說明 |
|------|------|------|------|
| 提交意見 | `btn-primary` | `POST /Feedback/Create` | 送出表單，驗證通過後寫入 DB |
| 取消 | `btn-outline-secondary` | `GET /Home/Index` | 放棄填寫，返回首頁 |

---

## 成功流程

```
GET /Feedback/Create
    → 後端產生 TrackingCode
    → 顯示空白表單

使用者填寫並提交
    → POST /Feedback/Create
    → ModelState 驗證
    → 確認 TrackingCode 唯一性
    → 呼叫 InsertFeedbackAsync（usp_Feedback_Insert）
    → 取得新 FeedbackId
    → Redirect to GET /Feedback/Detail/{newFeedbackId}
```

---

## 資料來源

| 操作 | Service 方法 | Stored Procedure |
|------|------------|-----------------|
| 產生追蹤代碼 | `GenerateUniqueTrackingCodeAsync()` | `usp_Feedback_CheckTrackingCodeExists` |
| 確認唯一性 | `CheckTrackingCodeExistsAsync(code)` | `usp_Feedback_CheckTrackingCodeExists` |
| 新增意見 | `InsertFeedbackAsync(model)` | `usp_Feedback_Insert` |
