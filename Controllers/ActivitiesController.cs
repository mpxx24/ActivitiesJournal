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

    public async Task<IActionResult> MonthComparison(int? year = null)
    {
        try
        {
            var all = await _stravaService.GetAllActivitiesAsync();
            var rides = all.Where(a => a.SportType is "Ride" or "VirtualRide" or "GravelRide" or "MountainBikeRide").ToList();

            var availableYears = rides.Select(a => a.StartDateLocal.Year).Distinct().OrderDescending().ToList();
            int selectedYear = year ?? DateTime.Now.Year;
            int priorYear = selectedYear - 1;

            Models.MonthStats StatsFor(IEnumerable<Models.StravaActivity> src, int y, int m)
            {
                var bucket = src.Where(a => a.StartDateLocal.Year == y && a.StartDateLocal.Month == m).ToList();
                var totalDist = bucket.Sum(a => a.Distance);
                var totalTime = bucket.Sum(a => a.MovingTime);
                return new Models.MonthStats
                {
                    Year = y, Month = m,
                    RideCount = bucket.Count,
                    DistanceKm = totalDist / 1000.0,
                    ElevationM = bucket.Sum(a => a.TotalElevationGain),
                    MovingTime = TimeSpan.FromSeconds(totalTime),
                    AvgSpeedKmh = totalTime > 0 ? (totalDist / totalTime) * 3.6 : 0,
                };
            }

            var currentByMonth = new Models.MonthStats[13];
            var priorByMonth = new Models.MonthStats[13];
            for (int m = 1; m <= 12; m++)
            {
                currentByMonth[m] = StatsFor(rides, selectedYear, m);
                priorByMonth[m] = StatsFor(rides, priorYear, m);
            }

            var monthly = rides
                .GroupBy(a => new { a.StartDateLocal.Year, a.StartDateLocal.Month })
                .Select(g => StatsFor(rides, g.Key.Year, g.Key.Month))
                .OrderByDescending(s => s.Year).ThenByDescending(s => s.Month)
                .ToList();

            var vm = new Models.MonthComparisonViewModel
            {
                MonthlyStats = monthly,
                SelectedYear = selectedYear,
                CompareYear = priorYear,
                AvailableYears = availableYears,
                CurrentYearByMonth = currentByMonth,
                PriorYearByMonth = priorByMonth,
            };

            return View(vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading month comparison");
            ViewBag.Error = "Failed to load comparison data.";
            return View(new Models.MonthComparisonViewModel { SelectedYear = DateTime.Now.Year });
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
