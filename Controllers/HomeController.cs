using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ActivitiesJournal.Models;
using ActivitiesJournal.Services;

namespace ActivitiesJournal.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IDashboardService _dashboardService;

    public HomeController(ILogger<HomeController> logger, IDashboardService dashboardService)
    {
        _logger = logger;
        _dashboardService = dashboardService;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var vm = await _dashboardService.BuildDashboardAsync();
            return View(vm);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load dashboard data");
            return View(new DashboardViewModel());
        }
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
