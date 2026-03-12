using ActivitiesJournal.Services;
using Microsoft.AspNetCore.Mvc;

namespace ActivitiesJournal.Controllers;

public class ActivitiesController : Controller
{
    private readonly IStravaService _stravaService;
    private readonly ILogger<ActivitiesController> _logger;

    public ActivitiesController(IStravaService stravaService, ILogger<ActivitiesController> logger)
    {
        _stravaService = stravaService;
        _logger = logger;
    }

    public async Task<IActionResult> Index(int page = 1, int perPage = 30)
    {
        try
        {
            var activities = await _stravaService.GetActivitiesAsync(page, perPage);
            ViewBag.CurrentPage = page;
            ViewBag.PerPage = perPage;
            ViewBag.HasMore = activities.Count == perPage;
            
            return View(activities);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Unauthorized access to Strava API");
            ViewBag.Error = ex.Message;
            return View(new List<Models.StravaActivity>());
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Configuration error");
            ViewBag.Error = ex.Message + " See SETUP.md for instructions on configuring User Secrets.";
            return View(new List<Models.StravaActivity>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading activities");
            ViewBag.Error = $"Failed to load activities: {ex.Message}. Please check your Strava API configuration and logs for details.";
            return View(new List<Models.StravaActivity>());
        }
    }

    public async Task<IActionResult> Details(long id)
    {
        try
        {
            var activity = await _stravaService.GetActivityByIdAsync(id);
            
            if (activity == null)
            {
                return NotFound();
            }

            return View(activity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading activity {ActivityId}", id);
            ViewBag.Error = "Failed to load activity details.";
            return View();
        }
    }

    public async Task<IActionResult> Summary()
    {
        try
        {
            var year = DateTime.Now.Year;
            // Fetch a larger page to approximate full year; adjust if needed.
            var activities = await _stravaService.GetActivitiesAsync(1, 200);
            var yearActivities = activities
                .Where(a => a.StartDateLocal.Year == year)
                .OrderByDescending(a => a.StartDateLocal)
                .ToList();

            var byType = yearActivities
                .GroupBy(a => a.SportType)
                .Select(g => new Models.ActivityTypeSummary
                {
                    SportType = g.Key,
                    Count = g.Count(),
                    TotalDistanceKm = g.Sum(a => a.Distance) / 1000.0,
                    TotalMovingTime = TimeSpan.FromSeconds(g.Sum(a => a.MovingTime)),
                    TotalElevationGain = g.Sum(a => a.TotalElevationGain),
                    LongestByDistance = g.OrderByDescending(a => a.Distance).FirstOrDefault()
                })
                .OrderByDescending(t => t.TotalDistanceKm)
                .ToList();

            var vm = new Models.YearSummaryViewModel
            {
                Year = year,
                Activities = yearActivities,
                ByType = byType
            };

            return View(vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building yearly summary");
            ViewBag.Error = "Failed to load yearly summary.";
            return View(new Models.YearSummaryViewModel { Year = DateTime.Now.Year });
        }
    }

    public async Task<IActionResult> PersonalRecords()
    {
        try
        {
            var all = await _stravaService.GetAllActivitiesAsync();
            var rides = all.Where(a => a.SportType is "Ride" or "VirtualRide" or "GravelRide" or "MountainBikeRide").ToList();

            if (!rides.Any())
            {
                return View(new Models.PersonalRecordsViewModel());
            }

            var longestByDist = rides.MaxBy(a => a.Distance)!;
            var fastestAvg = rides.Where(a => a.Distance >= 20_000).MaxBy(a => a.AverageSpeed);
            var mostClimbing = rides.MaxBy(a => a.TotalElevationGain)!;
            var longestTime = rides.MaxBy(a => a.MovingTime)!;
            var maxSpeed = rides.MaxBy(a => a.MaxSpeed)!;

            var records = new List<Models.PersonalRecord>
            {
                new() { Label = "Longest Ride", Value = $"{longestByDist.Distance / 1000.0:0.00} km", Icon = "bi-rulers", Activity = longestByDist },
                new() { Label = "Most Time in Saddle", Value = TimeSpan.FromSeconds(longestTime.MovingTime).ToString(@"h\:mm\:ss"), Icon = "bi-clock", Activity = longestTime },
                new() { Label = "Most Elevation", Value = $"{mostClimbing.TotalElevationGain:0} m", Icon = "bi-triangle", Activity = mostClimbing },
                new() { Label = "Top Speed (max)", Value = $"{maxSpeed.MaxSpeed * 3.6:0.0} km/h", Icon = "bi-lightning-charge", Activity = maxSpeed },
            };

            if (fastestAvg != null)
                records.Insert(1, new() { Label = "Fastest Avg Speed (≥20 km)", Value = $"{fastestAvg.AverageSpeed * 3.6:0.0} km/h", Icon = "bi-speedometer2", Activity = fastestAvg });

            var vm = new Models.PersonalRecordsViewModel
            {
                AllTimeRecords = records,
                Top10Longest = rides.OrderByDescending(a => a.Distance).Take(10).ToList(),
                Top10Fastest = rides.Where(a => a.Distance >= 20_000).OrderByDescending(a => a.AverageSpeed).Take(10).ToList(),
                Top10MostClimbing = rides.OrderByDescending(a => a.TotalElevationGain).Take(10).ToList(),
                TotalRides = rides.Count,
                TotalDistanceKm = rides.Sum(a => a.Distance) / 1000.0,
            };

            return View(vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading personal records");
            ViewBag.Error = "Failed to load personal records.";
            return View(new Models.PersonalRecordsViewModel());
        }
    }

    public async Task<IActionResult> Heatmap()
    {
        try
        {
            var year = DateTime.Now.Year;
            var activities = await _stravaService.GetActivitiesAsync(1, 200);
            var withPolylines = activities
                .Where(a => a.StartDateLocal.Year == year && a.Map?.SummaryPolyline != null)
                .ToList();

            return View(withPolylines);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading heatmap data");
            ViewBag.Error = "Failed to load heatmap data.";
            return View(new List<Models.StravaActivity>());
        }
    }
}
