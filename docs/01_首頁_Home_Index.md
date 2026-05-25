# 畫面規格書：首頁

## 基本資訊

| 項目 | 內容 |
|------|------|
| 畫面名稱 | 首頁 |
| Controller | `HomeController` |
| Action | `Index` |
| View 路徑 | `Views/Home/Index.cshtml` |
| 路由 URL | `/` 或 `/Home/Index` |
| HTTP 方法 | GET |
| ViewModel | 無（靜態頁面） |
| 頁面標題 | Home Page |

---

## 功能說明

系統的入口頁面，目前為 ASP.NET Core 預設範本頁面，顯示歡迎訊息與 ASP.NET Core 學習連結。
此頁面不含任何業務功能，作為系統起點供後續導航使用。

---

## 畫面佈局

```
┌──────────────────────────────────────────────┐
│  [Navbar] FeedbackSystem                     │
├──────────────────────────────────────────────┤
│                                              │
│              Welcome                         │
│   Learn about building Web apps with         │
│   ASP.NET Core  [連結]                       │
│                                              │
└──────────────────────────────────────────────┘
```

---

## 操作按鈕

| 元件 | 目標 | 說明 |
|------|------|------|
| Navbar 導覽列連結 | `/Feedback` | 導向意見列表（後台） |
| Navbar 導覽列連結 | `/Feedback/Create` | 導向提交意見（前台） |
| 頁面文字連結 | ASP.NET Core 官方文件 | 外部連結 |
