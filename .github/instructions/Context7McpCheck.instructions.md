---
description: "Use when writing or modifying code for any framework or language. Identify project version first, then use Context7 MCP to query matching docs. Final step must output connection status."
applyTo: "**"
---
# Context7 MCP Universal Version-Aware Check Rule

每次協助撰寫或修改程式碼時，必須嚴格遵循以下「版本對齊與驗證」流程，最後再進行 Context7 MCP 的連線狀態回報。

## 執行流程
1. **環境與版本識別 (Identify)**：
   - 動作：在給出程式碼之前，必須先從使用者的提問、上下文，或是專案設定檔（如 `package.json`, `.csproj`, `requirements.txt` 等）中，判斷出當前的「語言、框架與版本」（例如：Vue 3, .NET 10, React 18, C# 10）。
2. **精準文件檢索 (Query)**：
   - 動作：透過 Context7 MCP 查詢時，必須將上述「框架與版本」加入搜尋條件。
3. **版本相容性審查 (Validate)**：
   - 動作：確保產出的程式碼**完全符合該專案版本**的官方規範。
   - 限制：絕不可使用該版本尚未支援的新語法，也應避免使用在該版本中已被標記為廢棄（Deprecated/Obsolete）的老舊寫法。

## 強制規則 (Final Step)
回覆的最後一步，必須嘗試連接 Context7 MCP 進行檢查，並強制輸出以下對應的文字（不可加入其他推測或廢話）：

- 若連接或查詢成功，在回覆最後必須輸出：
  > ✅ Context7McpCheck instructions loaded.
- 若連接或查詢失敗，在回覆最後必須輸出：
  > ❌ 尚未設定 Context7 MCP