using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using FeedbackSystem.Models;

namespace FeedbackSystem.Controllers;

/// <summary>
/// 僅保留錯誤處理用途，Home 與 Privacy 頁面已移除。
/// </summary>
public class HomeController : Controller
{
    /// <summary>
    /// 全域例外處理的錯誤頁面，由 Program.cs 的 UseExceptionHandler 路由至此。
    /// </summary>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        // 取得目前請求的追蹤 ID，供錯誤頁面顯示
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
