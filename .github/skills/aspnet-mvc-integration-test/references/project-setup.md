# Integration Test 專案設定指南

## 1. 建立測試專案

```bash
# 在解決方案根目錄執行
dotnet new xunit -n FeedbackSystem.IntegrationTests -o FeedbackSystem.IntegrationTests
dotnet sln add FeedbackSystem.IntegrationTests/FeedbackSystem.IntegrationTests.csproj
```

---

## 2. 完整 .csproj 範本

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <!-- 與主專案使用相同的 .NET 版本 -->
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <!-- 測試執行時不產生 exe，僅產生 dll -->
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <!-- ★ 核心：提供 WebApplicationFactory / TestServer -->
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.*" />

    <!-- xUnit 測試框架 -->
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />

    <!-- DB 存取（需與主專案版本一致） -->
    <PackageReference Include="Microsoft.Data.SqlClient" Version="5.*" />

    <!-- 組態載入（讀取 appsettings.json） -->
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="10.*" />
    <PackageReference Include="Microsoft.Extensions.Configuration.EnvironmentVariables" Version="10.*" />

    <!-- 選用：提升 Assert 可讀性，例如 result.Should().Be(expected) -->
    <PackageReference Include="FluentAssertions" Version="6.*" />
  </ItemGroup>

  <ItemGroup>
    <!-- ★ 必要：參考主專案，才能使用 Controller / Service / ViewModel -->
    <ProjectReference Include="..\FeedbackSystem\FeedbackSystem.csproj" />
  </ItemGroup>

  <ItemGroup>
    <!-- ★ 必要：將 appsettings.json 複製到測試輸出目錄 -->
    <!-- 方法 A：複製主專案的 appsettings.json（使用真實 DB） -->
    <Content Include="..\FeedbackSystem\appsettings.json"
             CopyToOutputDirectory="PreserveNewest"
             Link="appsettings.json" />

    <!-- 方法 B（建議）：測試專案自有的 appsettings.json，可覆寫 DB 設定 -->
    <!-- 若選方法 B，請刪除方法 A，並在測試專案根目錄建立 appsettings.json -->
    <!-- <Content Include="appsettings.json" CopyToOutputDirectory="PreserveNewest" /> -->
  </ItemGroup>

</Project>
```

---

## 3. 讓 WebApplicationFactory 能使用 Program 類別

ASP.NET Core 的 `Program.cs` 預設為 top-level statement，編譯後類別是 internal。  
需要讓測試專案可以存取它，有兩種方式：

### 方式 A：在主專案 Program.cs 末尾加上（推薦）

```csharp
// 在 Program.cs 最後一行加入（讓測試專案可存取 Program 類別）
// 不影響正式環境，僅供 WebApplicationFactory<Program> 使用
public partial class Program { }
```

### 方式 B：在主專案 .csproj 加入 InternalsVisibleTo

```xml
<ItemGroup>
  <InternalsVisibleTo Include="FeedbackSystem.IntegrationTests" />
</ItemGroup>
```

---

## 4. 測試專案的 appsettings.json（方法 B）

在 `FeedbackSystem.IntegrationTests/appsettings.json` 建立，可指向測試 DB：

```json
{
  "ConnectionStrings": {
    // 指向測試資料庫（與主專案相同或獨立的測試 DB）
    "FeedbackDb": "Server=ymmistest;Database=Feedback_Test;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

---

## 5. 共用 Fixture 類別

為避免每個測試類別重複讀取設定檔，建立共用的 `IntegrationTestFixture`：

```csharp
using Microsoft.Extensions.Configuration;

namespace FeedbackSystem.IntegrationTests;

/// <summary>
/// 跨測試類別共用的 Integration Test Fixture
/// 負責一次性讀取 appsettings.json，供所有測試取得連線字串
/// 實作 IDisposable 以便在測試集合結束時釋放資源
/// </summary>
public class IntegrationTestFixture : IDisposable
{
    /// <summary>
    /// 從 appsettings.json 讀取的 FeedbackDb 連線字串
    /// 供所有 Integration Test 使用，避免重複讀取設定檔
    /// </summary>
    public string ConnectionString { get; }

    /// <summary>
    /// 建構子：初始化時一次性讀取組態
    /// </summary>
    public IntegrationTestFixture()
    {
        // 使用 ConfigurationBuilder 載入 appsettings.json
        // Directory.GetCurrentDirectory() 對應測試輸出目錄（bin/Debug/net10.0）
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddEnvironmentVariables() // 允許 CI 環境透過環境變數覆寫連線字串
            .Build();

        // 取得連線字串，若未設定則拋出明確例外
        ConnectionString = config.GetConnectionString("FeedbackDb")
            ?? throw new InvalidOperationException(
                "appsettings.json 缺少 ConnectionStrings:FeedbackDb，" +
                "請確認測試輸出目錄中的 appsettings.json 已正確設定。");
    }

    /// <summary>
    /// 釋放資源（目前無需特別清理，保留此介面供未來擴充）
    /// </summary>
    public void Dispose() { }
}
```

---

## 6. 驗證設定是否正確

執行以下指令確認測試專案可正常建置與執行：

```bash
# 建置測試專案
dotnet build FeedbackSystem.IntegrationTests

# 執行所有 Integration Test（--no-build 略過重複建置）
dotnet test FeedbackSystem.IntegrationTests --no-build --logger "console;verbosity=detailed"

# 只執行特定測試類別
dotnet test FeedbackSystem.IntegrationTests --filter "ClassName=FeedbackServiceIntegrationTests"
```
