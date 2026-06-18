using FeedbackSystem.Repositories;
using FeedbackSystem.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// 加入 MVC 服務，並設定全域防偽驗證（AutoValidateAntiforgeryToken）
// 讓所有 POST 請求自動驗證 Anti-Forgery Token，無需在每個 Action 個別加 Attribute
builder.Services.AddControllersWithViews(options =>
{
    // 全域套用 AutoValidateAntiforgeryTokenAttribute，防範 CSRF 攻擊
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

// 以 Scoped 生命週期分別註冊 Repository 與 Service
// Repository：封裝所有 SP 呼叫（ADO.NET 實作）
// Service：負責商業邏輯，依賴 IFeedbackRepository
builder.Services.AddScoped<IFeedbackRepository, FeedbackRepository>();
builder.Services.AddScoped<IFeedbackService, FeedbackService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

// 預設路由改為導向意見回饋列表（Feedback/Index）
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Feedback}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

// 讓整合測試專案的 WebApplicationFactory<Program> 可存取此應用程式入口點。
// top-level statement 編譯後為 internal class，需宣告 partial 使其對外可見。
// 此宣告不影響正式環境的任何行為。
public partial class Program { }
