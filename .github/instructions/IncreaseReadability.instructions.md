---
description: "Use when writing or modifying code. Require high readability and clear comments for nearly every line or logical block so engineers can quickly understand intent, flow, and decisions."
applyTo: "**"
---
# Increase Code Readability Rule
當你在此專案中撰寫、修改或重構程式碼時，請將「可讀性」視為第一優先。

## 核心原則
- 盡可能在每一行關鍵程式碼或每個邏輯段落提供清楚註解。
- 註解要解釋「為什麼這樣做」與「這段在做什麼」，避免只重述語法。
- 若一段程式碼較長，先寫區塊註解，再在必要行補充行內註解。
- 命名需清楚表意，避免縮寫與模糊名稱。
- 優先拆分複雜邏輯為小函式，並在函式前加上用途說明。

## 註解要求
- 關鍵分支（if/else、switch）需註解判斷理由與預期行為。
- 迴圈需註解資料來源、停止條件、邊界條件。
- 非直覺運算、轉換、正規表示式、魔術數字都必須註解。
- 與安全性、權限、驗證、錯誤處理相關程式碼必須補充註解。
- 公開 API、Controller、Service、Utility 的主要方法需有摘要註解。

## 可讀性要求
- 維持一致縮排與格式，避免過長單行與巢狀過深。
- 變數作用域最小化，避免同一變數承擔多重語意。
- 使用早期返回（early return）降低巢狀層級。
- 變更既有程式碼時，補齊缺失註解，不只新增功能。

## 回覆風格
- 產生程式碼時，預設提供高註解版本。
- 若使用者特別要求精簡註解，才可降低註解密度。
- 維持註解與程式碼同步更新，避免註解失真。
