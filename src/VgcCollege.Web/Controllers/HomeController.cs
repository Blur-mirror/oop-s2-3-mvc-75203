using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using VgcCollege.Web.Models;

namespace VgcCollege.Web.Controllers;

/// Public-facing controller. Authenticated users are redirected to their
/// role-specific dashboard so they land in the right area immediately.
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        // Redirect logged-in users to their dashboard
        if (User.Identity?.IsAuthenticated == true)
        {
            if (User.IsInRole("Administrator")) return RedirectToAction("Index", "Admin");
            if (User.IsInRole("Faculty"))       return RedirectToAction("Index", "Faculty");
            if (User.IsInRole("Student"))       return RedirectToAction("Index", "Student");
        }
        return View();
    }

    public IActionResult Privacy() => View();


    /// Global error handler – shown for unhandled exceptions in production.
    /// Does NOT expose stack traces or inner exception details.
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }
}
