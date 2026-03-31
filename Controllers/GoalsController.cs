using System.Security.Claims;
using ActivitiesJournal.Models;
using ActivitiesJournal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActivitiesJournal.Controllers;

[Authorize]
public class GoalsController : Controller
{
    private readonly IGoalsService _goals;
    private readonly IGoalsAnalyticsService _analytics;
    private readonly ILogger<GoalsController> _logger;

    public GoalsController(IGoalsService goals, IGoalsAnalyticsService analytics, ILogger<GoalsController> logger)
    {
        _goals = goals;
        _analytics = analytics;
        _logger = logger;
    }

    private long GetAthleteId() =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public async Task<IActionResult> Index()
    {
        try
        {
            var vm = await _analytics.BuildGoalsViewModelAsync(GetAthleteId());
            return View(vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading goals");
            ViewBag.Error = "Failed to load goals.";
            return View(new GoalsViewModel { Year = DateTime.Now.Year });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveGoals(double? distanceGoalKm, double? elevationGoalM, int? ridesGoal)
    {
        var athleteId = GetAthleteId();
        int year = DateTime.Now.Year;
        var data = await _goals.LoadAsync(athleteId);
        var existing = data.AnnualGoals.FirstOrDefault(g => g.Year == year);
        if (existing == null)
        {
            existing = new AnnualGoals { Year = year };
            data.AnnualGoals.Add(existing);
        }
        existing.DistanceGoalKm = distanceGoalKm;
        existing.ElevationGoalM = elevationGoalM;
        existing.RidesGoal = ridesGoal;
        await _goals.SaveAsync(data, athleteId);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearGoal(string field)
    {
        var athleteId = GetAthleteId();
        int year = DateTime.Now.Year;
        var data = await _goals.LoadAsync(athleteId);
        var existing = data.AnnualGoals.FirstOrDefault(g => g.Year == year);
        if (existing != null)
        {
            if (field == "distance")  existing.DistanceGoalKm = null;
            if (field == "elevation") existing.ElevationGoalM = null;
            if (field == "rides")     existing.RidesGoal = null;
            await _goals.SaveAsync(data, athleteId);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddChallenge(string name, double targetKm, DateTime startDate)
    {
        var athleteId = GetAthleteId();
        var data = await _goals.LoadAsync(athleteId);
        data.Challenges.Add(new VirtualChallenge { Name = name, TargetKm = targetKm, StartDate = startDate });
        await _goals.SaveAsync(data, athleteId);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteChallenge(string id)
    {
        var athleteId = GetAthleteId();
        var data = await _goals.LoadAsync(athleteId);
        var ch = data.Challenges.FirstOrDefault(c => c.Id == id);
        if (ch != null && !ch.IsPreset)
            data.Challenges.Remove(ch);
        await _goals.SaveAsync(data, athleteId);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetChallenge(string id)
    {
        var athleteId = GetAthleteId();
        var data = await _goals.LoadAsync(athleteId);
        var ch = data.Challenges.FirstOrDefault(c => c.Id == id);
        if (ch != null)
            ch.StartDate = new DateTime(DateTime.Today.Year, 1, 1);
        await _goals.SaveAsync(data, athleteId);
        return RedirectToAction(nameof(Index));
    }
}
