// ──────────────────────────────────────────────────────────────────────────────
//  NUnit [SetUpFixture]：整個測試組件（Assembly）的前置 / 後置動作
//
//  功能：
//    1. 在所有測試開始前，以 Process 自動啟動 ASP.NET Core 應用程式
//    2. 輪詢 HTTP 端點直到應用程式就緒（避免競態條件）
//    3. 測試全部完成後，Kill 應用程式 Process（釋放 Port）
//
//  使用 HTTP（port 5104）而非 HTTPS，避免本機自簽憑證信任問題。
//  若 CI 環境已另外啟動應用程式，設定環境變數 E2E_BASE_URL 即可跳過自動啟動。
// ──────────────────────────────────────────────────────────────────────────────

using System.Diagnostics;

namespace FeedbackSystem.E2ETests;

/// <summary>
/// NUnit [SetUpFixture]（無 namespace 限制，套用至整個組件）。
/// 在任何測試執行前啟動主應用程式，測試完畢後自動關閉。
/// </summary>
[SetUpFixture]
public class AppStartupFixture
{
    // ─── Process 物件：持有執行中的 dotnet run 程序 ────────────────────────
    private static Process? _appProcess;

    /// <summary>
    /// 應用程式的基底 URL（HTTP，避免 HTTPS 憑證問題）。
    /// 優先讀取環境變數 E2E_BASE_URL（供 CI/CD 使用），
    /// 未設定則使用 launchSettings.json 的 "http" profile URL。
    /// </summary>
    public static string AppBaseUrl { get; private set; } =
        Environment.GetEnvironmentVariable("E2E_BASE_URL") ?? "http://localhost:5104";

    // ═══════════════════════════════════════════════════════════════════════
    //  OneTimeSetUp：測試組件啟動前執行一次
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 在整個測試組件的第一個測試執行前，自動啟動 ASP.NET Core 應用程式，
    /// 並等待 HTTP 端點就緒。
    /// </summary>
    [OneTimeSetUp]
    public async Task StartApplicationAsync()
    {
        // ── CI 環境：已由 pipeline 啟動，僅等待就緒 ──────────────────────
        if (Environment.GetEnvironmentVariable("E2E_BASE_URL") is not null)
        {
            // CI 環境中應用程式已在外部啟動，只需確認就緒即可
            await WaitForAppReadyAsync(AppBaseUrl, timeoutSeconds: 30);
            return;
        }

        // ── 本機環境：計算主專案路徑並啟動 ──────────────────────────────
        //
        // AppContext.BaseDirectory 範例：
        //   ...\FeedbackSystem\FeedbackSystem.E2ETests\bin\Debug\net10.0\
        //
        // 往上四層即為方案根目錄：
        //   net10.0 (1) → Debug (2) → bin (3) → FeedbackSystem.E2ETests (4) → FeedbackSystem
        //
        var testOutputDir = new DirectoryInfo(AppContext.BaseDirectory);
        var solutionDir   = testOutputDir.Parent!   // Debug
                                         .Parent!   // bin
                                         .Parent!   // FeedbackSystem.E2ETests
                                         .Parent!;  // FeedbackSystem（方案根）

        // 主專案的 .csproj 路徑
        var projectPath = Path.Combine(solutionDir.FullName, "FeedbackSystem.csproj");

        // 確認主專案存在，否則提前拋出明確錯誤
        if (!File.Exists(projectPath))
            throw new FileNotFoundException(
                $"找不到主專案檔案：{projectPath}\n" +
                "請確認測試輸出目錄結構正確（預期路徑：…/FeedbackSystem.E2ETests/bin/Debug/net10.0/）。");

        // ── 建立並啟動 dotnet run Process ────────────────────────────────
        _appProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName  = "dotnet",

                // --no-launch-profile：不讀取 launchSettings.json（手動指定 URL）
                // --urls：明確使用 HTTP 避免 HTTPS 憑證問題
                Arguments = $"run --project \"{projectPath}\" --no-launch-profile " +
                            $"--urls \"{AppBaseUrl}\"",

                UseShellExecute       = false,  // 必須 false 才能重導向輸出
                CreateNoWindow        = true,   // 不顯示額外視窗
                RedirectStandardOutput = true,  // 重導向 stdout（避免 buffer 阻塞）
                RedirectStandardError  = true,  // 重導向 stderr（避免 buffer 阻塞）
                WorkingDirectory      = solutionDir.FullName
            }
        };

        // 設定開發環境，確保 appsettings.Development.json、開發中間件正常載入
        _appProcess.StartInfo.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Development";

        // 附加非同步輸出處理器（必須在 BeginOutputReadLine 前附加，否則 NullReferenceException）
        // 此處捨棄輸出內容（若需偵錯可改為 Console.WriteLine）
        _appProcess.OutputDataReceived += (_, _) => { };
        _appProcess.ErrorDataReceived  += (_, _) => { };

        // 啟動 Process
        _appProcess.Start();

        // 開始非同步讀取，避免輸出 buffer 滿了導致 Process 死鎖
        _appProcess.BeginOutputReadLine();
        _appProcess.BeginErrorReadLine();

        // ── 等待應用程式 HTTP 端點就緒（最長 60 秒）─────────────────────
        await WaitForAppReadyAsync(AppBaseUrl, timeoutSeconds: 60);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  OneTimeTearDown：測試組件全部完成後執行一次
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 所有測試完成後，終止應用程式 Process 並釋放 Port。
    /// </summary>
    [OneTimeTearDown]
    public void StopApplication()
    {
        // 僅在本機自動啟動模式才需要關閉（CI 環境交由 pipeline 管理）
        if (_appProcess is null) return;

        // 確認 Process 尚未自行結束
        if (!_appProcess.HasExited)
        {
            // Kill 整個 Process Tree（包含子 Process，例如 dotnet build 子程序）
            _appProcess.Kill(entireProcessTree: true);
            // 等待最多 5 秒確認已完全結束
            _appProcess.WaitForExit(5000);
        }

        _appProcess.Dispose();
        _appProcess = null;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Private Helper：輪詢等待 HTTP 端點就緒
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 每秒輪詢 <paramref name="baseUrl"/>，直到收到非 5xx 的 HTTP 回應（或超時）。
    /// 任何非 5xx 回應（含 302 重導向）都代表應用程式已成功啟動。
    /// </summary>
    /// <param name="baseUrl">要輪詢的應用程式根 URL</param>
    /// <param name="timeoutSeconds">最長等待秒數（預設 60 秒）</param>
    /// <exception cref="TimeoutException">超時未能連線時拋出</exception>
    private static async Task WaitForAppReadyAsync(string baseUrl, int timeoutSeconds = 60)
    {
        // 使用 AllowAutoRedirect = false 避免重導向循環影響輪詢判斷
        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            // 忽略 HTTPS 憑證錯誤（萬一未來切換回 HTTPS 也能正常輪詢）
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };

        using var httpClient = new HttpClient(handler)
        {
            // 單次請求超時 5 秒，避免 TCP 連線等待拖慢輪詢節奏
            Timeout = TimeSpan.FromSeconds(5)
        };

        var deadline      = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        Exception? lastEx = null;  // 記錄最後一次例外，供超時訊息附上詳細原因

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await httpClient.GetAsync(baseUrl);

                // 任何非 5xx（Server Error）都視為應用程式已啟動就緒
                // 例如：200 OK、302 Found（重導向至 /Feedback 等）
                if ((int)response.StatusCode < 500)
                    return; // ✅ 應用程式已就緒，可以開始測試
            }
            catch (Exception ex)
            {
                // 連線被拒絕（ERR_CONNECTION_REFUSED）屬於正常等待狀態，繼續輪詢
                lastEx = ex;
            }

            // 等待 1 秒後再次輪詢，避免 CPU 忙等
            await Task.Delay(1000);
        }

        // ── 超時：拋出有意義的錯誤訊息 ──────────────────────────────────
        throw new TimeoutException(
            $"應用程式在 {timeoutSeconds} 秒內未能在 {baseUrl} 啟動。\n" +
            $"最後一次連線錯誤：{lastEx?.Message ?? "（無錯誤記錄）"}\n" +
            "排查建議：\n" +
            "  1. 確認 FeedbackSystem.csproj 路徑正確\n" +
            "  2. 確認 Port 5104 未被其他程序佔用\n" +
            "  3. 嘗試手動執行：dotnet run --project FeedbackSystem.csproj --urls http://localhost:5104");
    }
}
