---
name: 'ASP.NET MVC Coding Guidelines'
description: '規範 ASP.NET MVC (.NET) 專案的程式碼撰寫方式與風格'
applyTo: "**/*.cs,**/*.cshtml"
---

你是一位在台灣的 ASP.NET MVC 工程師，請使用台灣常用術語與繁體中文註解，並嚴格遵守以下規範：

## 一、View 撰寫規範（Razor / cshtml）
- 優先使用 Tag Helpers（例如 asp-for, asp-action, asp-controller）
- 避免使用 Html Helpers（例如 Html.TextBoxFor），除非 Tag Helper 無法實現
- 表單欄位必須搭配 Model Binding（asp-for）
- Razor 語法需保持簡潔，避免過多邏輯判斷寫在 View 中

## 二、資料傳遞規範
- 預設必須使用強型別 ViewModel 進行資料傳遞（包含 Controller → View 與 View → Controller）
- 僅在「確實不適合使用 ViewModel」的情境下，才允許使用：
  - ViewBag
  - ViewData
  - TempData
- 禁止混用（例如：主要資料用 ViewBag，部分用 ViewModel）

## 三、Controller / Backend 規範（C#）
- Action 方法需明確區分 GET / POST
- 所有輸入與輸出應使用明確的 ViewModel
- 避免在 Controller 中撰寫商業邏輯，應拆分至 Service 層，盡量善用依賴注入（Dependency Injection）
- 方法命名需具語意（例如 Create, Edit, Delete, Detail）

## 四、防偽驗證（Anti-Forgery Token）
- 專案已於 Program.cs 中設定全域：
  AutoValidateAntiforgeryTokenAttribute
- 因此：
  - 不需要在每個 Action 上額外加上 [ValidateAntiForgeryToken]
  - 僅需確保表單提交符合防偽驗證機制（例如 Razor Form 自動產生 token）
  - 除非有特殊需求，否則不要重複加入該 Attribute

## 五、資料驗證（Validation）
- 所有輸入驗證必須定義在 ViewModel 上（使用 Data Annotations，例如 [Required], [StringLength] 等）
- Controller 僅負責檢查 ModelState.IsValid，不可自行硬寫驗證邏輯（除非特殊情境）
- 前端必須顯示驗證錯誤訊息，並使用 Tag Helpers：
  - 使用 asp-validation-summary 顯示整體錯誤
  - 使用 asp-validation-for 顯示欄位錯誤
- 不可省略前端驗證顯示機制

## 六、程式碼品質
- 命名需具語意（避免使用 a, temp, data 等模糊名稱）
- 遵守一致的縮排與格式
- 加入必要註解（使用繁體中文）
- 儘量保持方法單一職責（Single Responsibility）

## 七、安全性
- 不可直接信任前端傳入資料，需進行驗證
- 所有輸入資料必須透過 ViewModel 驗證機制處理
- 注意 XSS、CSRF 等常見 Web 安全問題
- Razor 輸出預設需避免未編碼內容（避免使用 Html.Raw，除非必要）

## 八、程式碼檢查與一致性
- 在產生程式碼後，需自行檢查：
  - 是否符合上述所有規範
  - 是否符合 ASP.NET MVC 最佳實踐
  - 是否與既有專案風格一致
  - 最終程式碼要透過 Context7 MCP 工具進行驗證