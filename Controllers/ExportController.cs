using System.Text;
using System.Text.Json;
using ActivitiesJournal.Services;
using Microsoft.AspNetCore.Mvc;

namespace ActivitiesJournal.Controllers;

public class ExportController : Controller
{
    private readonly IStravaService _stravaService;
    private readonly ILogger<ExportController> _logger;

    public ExportController(IStravaService stravaService, ILogger<ExportController> logger)
    {
        _stravaService = stravaService;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var all = await _stravaService.GetAllActivitiesAsync();
            ViewBag.SportTypes = all.Select(a => a.SportType).Distinct().OrderBy(t => t).ToList();
            ViewBag.Years = all.Select(a => a.StartDateLocal.Year).Distinct().OrderByDescending(y => y).ToList();
            ViewBag.TotalCount = all.Count;
            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading activities for export");
            ViewBag.Error = $"Failed to load activities: {ex.Message}";
            ViewBag.SportTypes = new List<string>();
            ViewBag.Years = new List<int>();
            ViewBag.TotalCount = 0;
            return View();
        }
    }

    [HttpGet]
    public async Task<IActionResult> Count([FromQuery] string[]? sports, [FromQuery] int[]? years)
    {
        var all = await _stravaService.GetAllActivitiesAsync();
        var count = ApplyFilters(all, sports, years).Count();
        return Json(new { count });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Download([FromForm] string[]? sports, [FromForm] int[]? years)
    {
        var all = await _stravaService.GetAllActivitiesAsync();
        var filtered = ApplyFilters(all, sports, years)
            .OrderByDescending(a => a.StartDateLocal)
            .ToList();

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(filtered, options);
        var bytes = Encoding.UTF8.GetBytes(json);
        var filename = $"activities_{DateTime.Now:yyyyMMdd}.json";
        return File(bytes, "application/json", filename);
    }

    public async Task<IActionResult> DownloadActivity(long id)
    {
        var activity = await _stravaService.GetActivityByIdAsync(id);
        if (activity == null)
            return NotFound();

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(activity, options);
        var bytes = Encoding.UTF8.GetBytes(json);
        var filename = $"activity_{id}.json";
        return File(bytes, "application/json", filename);
    }

    private static IEnumerable<ActivitiesJournal.Models.StravaActivity> ApplyFilters(
        IEnumerable<ActivitiesJournal.Models.StravaActivity> activities,
        string[]? sports,
        int[]? years)
    {
        var filtered = activities.AsEnumerable();
        if (sports?.Length > 0)
            filtered = filtered.Where(a => sports.Contains(a.SportType));
        if (years?.Length > 0)
            filtered = filtered.Where(a => years.Contains(a.StartDateLocal.Year));
        return filtered;
    }
}
