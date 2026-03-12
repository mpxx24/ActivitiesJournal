using ActivitiesJournal.Models;
using ActivitiesJournal.Services;
using Microsoft.AspNetCore.Mvc;

namespace ActivitiesJournal.Controllers;

public class GoalsController : Controller
{
    private readonly IStravaService _strava;
    private readonly GoalsService _goals;
    private readonly ILogger<GoalsController> _logger;

    public GoalsController(IStravaService strava, GoalsService goals, ILogger<GoalsController> logger)
    {
        _strava = strava;
        _goals = goals;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var all = await _strava.GetAllActivitiesAsync();
            var rides = all.Where(a => a.SportType is "Ride" or "VirtualRide" or "GravelRide" or "MountainBikeRide").ToList();

            int year = DateTime.Now.Year;
            var yearRides = rides.Where(a => a.StartDateLocal.Year == year).ToList();
            double distKm = yearRides.Sum(a => a.Distance) / 1000.0;
            double elevM = yearRides.Sum(a => (double)a.TotalElevationGain);
            int rideCount = yearRides.Count;

            var data = _goals.Load();
            var goals = data.AnnualGoals.FirstOrDefault(g => g.Year == year) ?? new AnnualGoals { Year = year };

            // Projection: extrapolate based on day of year
            int dayOfYear = DateTime.Now.DayOfYear;
            int daysInYear = DateTime.IsLeapYear(year) ? 366 : 365;
            double fraction = dayOfYear / (double)daysInYear;

            string? Project(double current, double? target)
            {
                if (target == null || fraction <= 0) return null;
                double projected = current / fraction;
                if (projected >= target) return "On track!";
                double needed = target.Value - current;
                double daysLeft = daysInYear - dayOfYear;
                return $"On pace for {projected:0} — need {needed:0} more in {daysLeft:0} days";
            }

            // Virtual challenges: sum km from rides since challenge StartDate
            var progressList = data.Challenges.Select(c =>
            {
                var km = rides.Where(a => a.StartDateLocal.Date >= c.StartDate.Date)
                              .Sum(a => a.Distance) / 1000.0;
                return new ChallengeProgress { Challenge = c, EarnedKm = Math.Round(km, 1) };
            }).ToList();

            var vm = new GoalsViewModel
            {
                Year = year,
                Goals = goals,
                CurrentDistanceKm = Math.Round(distKm, 1),
                CurrentElevationM = Math.Round(elevM, 0),
                CurrentRides = rideCount,
                DistanceProjection = Project(distKm, goals.DistanceGoalKm),
                ElevationProjection = Project(elevM, goals.ElevationGoalM),
                RidesProjection = Project(rideCount, goals.RidesGoal),
                ChallengeProgressList = progressList,
            };

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
    public IActionResult SaveGoals(double? distanceGoalKm, double? elevationGoalM, int? ridesGoal)
    {
        int year = DateTime.Now.Year;
        var data = _goals.Load();
        var existing = data.AnnualGoals.FirstOrDefault(g => g.Year == year);
        if (existing == null)
        {
            existing = new AnnualGoals { Year = year };
            data.AnnualGoals.Add(existing);
        }
        existing.DistanceGoalKm = distanceGoalKm;
        existing.ElevationGoalM = elevationGoalM;
        existing.RidesGoal = ridesGoal;
        _goals.Save(data);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult AddChallenge(string name, double targetKm, DateTime startDate)
    {
        var data = _goals.Load();
        data.Challenges.Add(new VirtualChallenge { Name = name, TargetKm = targetKm, StartDate = startDate });
        _goals.Save(data);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult DeleteChallenge(string id)
    {
        var data = _goals.Load();
        var ch = data.Challenges.FirstOrDefault(c => c.Id == id);
        if (ch != null && !ch.IsPreset)
            data.Challenges.Remove(ch);
        _goals.Save(data);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult ResetChallenge(string id)
    {
        var data = _goals.Load();
        var ch = data.Challenges.FirstOrDefault(c => c.Id == id);
        if (ch != null)
            ch.StartDate = DateTime.Today;
        _goals.Save(data);
        return RedirectToAction(nameof(Index));
    }
}
