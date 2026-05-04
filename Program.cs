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

// 以 Scoped 生命週期註冊 IFeedbackService 介面與其實作 FeedbackService
// 每個 HTTP Request 共用同一個實例，結束後自動釋放
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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
