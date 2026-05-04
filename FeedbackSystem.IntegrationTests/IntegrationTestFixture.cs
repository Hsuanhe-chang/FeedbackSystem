using Microsoft.Extensions.Configuration;

namespace FeedbackSystem.IntegrationTests;

/// <summary>
/// 跨測試類別共用的 Integration Test Fixture。
/// 負責一次性讀取 appsettings.json，所有整合測試透過此 Fixture 取得連線字串，
/// 避免每個測試類別各自重複初始化設定，降低 I/O 開銷。
/// 實作 IDisposable 以便 xUnit 在測試集合結束後正確釋放資源。
/// </summary>
public class IntegrationTestFixture : IDisposable
{
    /// <summary>
    /// 從 appsettings.json 讀取的 FeedbackDb 連線字串。
    /// 供所有整合測試使用，對應 Server=ymmistest;Database=Feedback_Test 的真實測試 DB。
    /// </summary>
    public string ConnectionString { get; }

    /// <summary>
    /// 建構子：在 xUnit 第一次實例化此 Fixture 時執行一次，讀取組態並快取連線字串。
    /// </summary>
    public IntegrationTestFixture()
    {
        // 使用 ConfigurationBuilder 載入與測試輸出目錄並排的 appsettings.json
        // Directory.GetCurrentDirectory() 在執行期對應 bin/Debug/net10.0/
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())           // 測試執行目錄
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddEnvironmentVariables()  // 允許 CI 環境透過環境變數覆寫連線字串
            .Build();

        // 若連線字串未設定，立即拋出可識別的例外，避免測試以模糊錯誤失敗
        ConnectionString = config.GetConnectionString("FeedbackDb")
            ?? throw new InvalidOperationException(
                "appsettings.json 缺少 ConnectionStrings:FeedbackDb，" +
                "請確認測試輸出目錄（bin/Debug/net10.0/）中的 appsettings.json 已正確設定。");
    }

    /// <summary>
    /// 釋放資源（目前無長連線需關閉，保留介面供未來擴充）。
    /// </summary>
    public void Dispose() { }
}
