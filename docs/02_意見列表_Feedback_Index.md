# 畫面規格書：意見列表（後台）

## 基本資訊

| 項目 | 內容 |
|------|------|
| 畫面名稱 | 意見管理（後台列表） |
| Controller | `FeedbackController` |
| Action | `Index` |
| View 路徑 | `Views/Feedback/Index.cshtml` |
| 路由 URL | `/Feedback` 或 `/Feedback/Index` |
| HTTP 方法 | GET |
| ViewModel | `IEnumerable<FeedbackListItemViewModel>` |
| 頁面標題 | 意見管理 |

---

## 功能說明

後台管理員查看所有意見的列表頁面，提供以下功能：
1. **篩選**：可依「處理狀態」與「優先等級」篩選意見
2. **分頁**：每頁顯示 10 筆，支援多頁切換（顯示頁碼，過多時以省略號收合）
3. **操作**：每筆意見提供「詳情」與「編輯」兩個操作按鈕
4. **新增入口**：頁面右上角提供「提交新意見」按鈕，導向前台新增頁面

---

## Query String 參數

| 參數名稱 | 型別 | 必填 | 預設值 | 說明 |
|---------|------|------|--------|------|
| `status` | `byte?` | 否 | null（全部） | 依處理狀態篩選（0=待處理、1=處理中、2=已回覆、3=已關閉） |
| `priority` | `byte?` | 否 | null（全部） | 依優先等級篩選（1=一般、2=重要、3=緊急） |
| `page` | `int` | 否 | 1 | 目前頁碼 |

---

## ViewModel：FeedbackListItemViewModel

| 欄位名稱 | 型別 | 說明 |
|---------|------|------|
| `FeedbackId` | `int` | 意見唯一識別碼（用於操作按鈕的路由參數） |
| `TrackingCode` | `string` | 意見追蹤代碼（格式：FB + yyyyMMdd + 6碼大寫亂數） |
| `CustomerName` | `string` | 客戶姓名 |
| `Category` | `string` | 意見類別 |
| `Subject` | `string` | 意見主旨（超過 30 字時截斷並加「…」） |
| `Status` | `byte` | 處理狀態（0~3，View 轉換為中文標籤與 badge 顏色） |
| `Priority` | `byte` | 優先等級（1~3，View 轉換為顏色 badge） |
| `ReplyCount` | `int` | 累積回覆筆數 |
| `CreatedAt` | `DateTime` | 建立時間（格式：yyyy-MM-dd HH:mm） |

---

## ViewBag 資料

| 鍵值 | 型別 | 說明 |
|------|------|------|
| `CurrentStatus` | `byte?` | 目前篩選的狀態值 |
| `CurrentPriority` | `byte?` | 目前篩選的優先等級值 |
| `CurrentPage` | `int` | 目前頁碼 |
| `TotalPages` | `int` | 總頁數 |
| `TotalCount` | `int` | 符合篩選條件的總筆數 |
| `StatusList` | `List<SelectListItem>` | 處理狀態下拉清單 |
| `PriorityList` | `List<SelectListItem>` | 優先等級下拉清單 |

---

## 畫面佈局

```
┌──────────────────────────────────────────────────────────────────────┐
│  意見管理                                     [＋ 提交新意見] (btn-success)│
├──────────────────────────────────────────────────────────────────────┤
│ ┌─ 篩選區塊 (card) ─────────────────────────────────────────────┐   │
│ │  處理狀態: [全部狀態 ▼]   優先等級: [全部等級 ▼]  [篩選] [清除] │   │
│ └──────────────────────────────────────────────────────────────┘   │
│                                                                      │
│  共 N 筆意見                                                          │
│ ┌────────────────────────────────────────────────────────────────┐  │
│ │ 追蹤代碼 │ 客戶姓名 │ 類別 │ 主旨 │ 狀態 │ 優先等級 │回覆數│建立時間│操作│  │
│ ├────────────────────────────────────────────────────────────────┤  │
│ │ FB20260504... │ 王小明 │ 產品 │ 主旨... │ [已回覆] │ [重要] │ 2 │ 2026-05-04 │ [詳情][編輯] │  │
│ │ ...           │ ...    │ ... │ ...     │ [待處理] │ [一般] │ 0 │ ...          │ ...          │  │
│ └────────────────────────────────────────────────────────────────┘  │
│                                                                      │
│         [上一頁]  [1] [2] [3] ... [N]  [下一頁]                      │
└──────────────────────────────────────────────────────────────────────┘
```

---

## 欄位顯示規則

### 處理狀態（Status）Badge 顏色

| 值 | 顯示文字 | Bootstrap Badge Class |
|----|---------|----------------------|
| 0 | 待處理 | `bg-secondary`（灰色） |
| 1 | 處理中 | `bg-primary`（藍色） |
| 2 | 已回覆 | `bg-success`（綠色） |
| 3 | 已關閉 | `bg-dark`（黑色） |

### 優先等級（Priority）Badge 顏色

| 值 | 顯示文字 | Bootstrap Badge Class |
|----|---------|----------------------|
| 1 | 一般 | `bg-secondary`（灰色） |
| 2 | 重要 | `bg-warning text-dark`（橙色） |
| 3 | 緊急 | `bg-danger`（紅色） |

### 主旨截斷規則
- 主旨長度 > 30 字元時，顯示前 30 字元並補上「…」
- 完整主旨請至詳情頁查看

---

## 操作按鈕

| 按鈕 | 樣式 | 目標 | 說明 |
|------|------|------|------|
| ＋ 提交新意見 | `btn-success`（右上角） | `GET /Feedback/Create` | 導向前台新增意見頁 |
| 篩選 | `btn-primary` | `GET /Feedback?status=...&priority=...` | 送出篩選條件，重置為第 1 頁 |
| 清除 | `btn-outline-secondary` | `GET /Feedback` | 清除所有篩選條件 |
| 詳情 | `btn-sm btn-outline-info` | `GET /Feedback/Detail/{id}` | 導向意見詳情頁 |
| 編輯 | `btn-sm btn-outline-warning` | `GET /Feedback/Edit/{id}` | 導向後台編輯頁 |

---

## 分頁邏輯

- 每頁固定顯示 **10** 筆
- 頁碼顯示規則：顯示第 1 頁、最後 1 頁，以及當前頁碼前後 2 頁，其餘以「…」替代
- 第 1 頁時「上一頁」按鈕停用（disabled）
- 最後 1 頁時「下一頁」按鈕停用（disabled）
- 分頁連結保留目前的 `status` 與 `priority` 篩選參數

---

## 無資料提示

當篩選結果為空（或資料庫無任何資料）時，表格顯示一列提示：

```
目前尚無意見資料
```
（置中，文字淡灰色 `text-muted`）

---

## 資料來源

- Service 方法：`IFeedbackService.GetPagedListAsync(status, priority, page, pageSize)`
- 對應 Stored Procedure：`usp_Feedback_GetPagedList`
