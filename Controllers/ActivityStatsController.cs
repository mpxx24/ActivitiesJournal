using ActivitiesJournal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActivitiesJournal.Controllers;

[Authorize]
[Route("Activities/[action]")]
public class ActivityStatsController : Controller
{
    private readonly IStravaService _stravaService;
    private readonly IActivityStatsService _statsService;
    private readonly ILogger<ActivityStatsController> _logger;

    public ActivityStatsController(
        IStravaService stravaService,
        IActivityStatsService statsService,
        ILogger<ActivityStatsController> logger)
    {
        _stravaService = stravaService;
        _statsService = statsService;
        _logger = logger;
    }

    public async Task<IActionResult> Summary()
    {
        try
        {
            var year = DateTime.Now.Year;
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

    public async Task<IActionResult> Badges(string? type = null)
    {
        type ??= "Ride";
        ViewBag.ActivityType = type;
        ViewBag.ActivityTypeLabel = SportTypes.TypeLabel(type);

        try
        {
            var vm = await _statsService.GetBadgesAsync(type);
            return View(vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading badges");
            ViewBag.Error = "Failed to load badges.";
            return View(new Models.BadgesViewModel());
        }
    }

    public async Task<IActionResult> PersonalRecords(string? type = null)
    {
        type ??= "Ride";
        ViewBag.ActivityType = type;
        ViewBag.ActivityTypeLabel = SportTypes.TypeLabel(type);

        try
        {
            var vm = await _statsService.GetPersonalRecordsAsync(type);
            return View(vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading personal records");
            ViewBag.Error = "Failed to load personal records.";
            return View(new Models.PersonalRecordsViewModel());
        }
    }

    public async Task<IActionResult> Segments(int count = 30)
    {
        try
        {
            var (segVm, beVm) = await _statsService.GetSegmentsAsync(count);
            ViewBag.BestEfforts = beVm;
            ViewBag.FetchCount = count;
            return View(segVm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading segments");
            ViewBag.Error = "Failed to load segment data.";
            return View(new Models.SegmentsViewModel());
        }
    }

    public async Task<IActionResult> Fitness(int days = 365)
    {
        try
        {
            var vm = await _statsService.GetFitnessAsync(days);
            return View(vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading fitness curves");
            ViewBag.Error = "Failed to load fitness data.";
            return View(new Models.FitnessViewModel());
        }
    }

    public async Task<IActionResult> Analysis(int? year = null)
    {
        try
        {
            var vm = await _statsService.GetAnalysisAsync(year);
            return View(vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading analysis");
            ViewBag.Error = "Failed to load analysis data.";
            return View(new Models.AnalysisViewModel { Year = DateTime.Now.Year });
        }
    }

    public async Task<IActionResult> YearInReview(int? year = null)
    {
        try
        {
            var all = await _stravaService.GetAllActivitiesAsync();
            int selectedYear = year ?? DateTime.Now.Year;
            var availableYears = all.Select(a => a.StartDateLocal.Year).Distinct().OrderDescending().ToList();

            var rides = all.Where(a => a.StartDateLocal.Year == selectedYear &&
                SportTypes.IsRide(a.SportType)).ToList();
            var walks = all.Where(a => a.StartDateLocal.Year == selectedYear &&
                SportTypes.IsWalk(a.SportType)).ToList();
            var allYear = all.Where(a => a.StartDateLocal.Year == selectedYear).ToList();

            var longestRide  = rides.OrderByDescending(a => a.Distance).FirstOrDefault();
            var fastestRide  = rides.Where(a => a.Distance >= 20000).OrderByDescending(a => a.AverageSpeed).FirstOrDefault();
            var mostElevRide = rides.OrderByDescending(a => a.TotalElevationGain).FirstOrDefault();

            var rideDates = rides.Select(a => a.StartDateLocal.Date).Distinct().OrderDescending().ToList();
            int bestStreak = 0, cur = 0;
            DateTime? prev = null;
            foreach (var d in rideDates.OrderBy(d => d))
            {
                if (prev == null || d == prev.Value.AddDays(1)) { cur++; if (cur > bestStreak) bestStreak = cur; }
                else cur = 1;
                prev = d;
            }

            var activeMonths = allYear.Select(a => a.StartDateLocal.Month).Distinct().Count();
            var biggestMonth = Enumerable.Range(1, 12)
                .Select(m => (month: m, km: rides.Where(a => a.StartDateLocal.Month == m).Sum(a => a.Distance / 1000.0)))
                .OrderByDescending(x => x.km).First();

            ViewBag.SelectedYear   = selectedYear;
            ViewBag.AvailableYears = availableYears;
            ViewBag.RideCount      = rides.Count;
            ViewBag.RideKm         = rides.Sum(a => a.Distance / 1000.0);
            ViewBag.RideElevM      = (int)rides.Sum(a => (double)a.TotalElevationGain);
            ViewBag.RideHours      = rides.Sum(a => a.MovingTime / 3600.0);
            ViewBag.WalkCount      = walks.Count;
            ViewBag.WalkKm         = walks.Sum(a => a.Distance / 1000.0);
            ViewBag.LongestRide    = longestRide != null ? (longestRide.Distance / 1000.0).ToString("0.0") + " km" : "–";
            ViewBag.LongestRideName = longestRide?.Name ?? "–";
            ViewBag.FastestRide    = fastestRide != null ? (fastestRide.AverageSpeed * 3.6).ToString("0.0") + " km/h" : "–";
            ViewBag.FastestRideName = fastestRide?.Name ?? "–";
            ViewBag.MostElevRide   = mostElevRide != null ? ((int)mostElevRide.TotalElevationGain) + " m" : "–";
            ViewBag.MostElevRideName = mostElevRide?.Name ?? "–";
            ViewBag.BestStreak     = bestStreak;
            ViewBag.ActiveMonths   = activeMonths;
            ViewBag.BiggestMonth   = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(biggestMonth.month);
            ViewBag.BiggestMonthKm = biggestMonth.km.ToString("0.0");
            ViewBag.MonthlyKm      = Enumerable.Range(1, 12).Select(m =>
                Math.Round(rides.Where(a => a.StartDateLocal.Month == m).Sum(a => a.Distance / 1000.0), 1)).ToList();

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading year in review");
            ViewBag.Error = "Failed to load year in review data.";
            return View();
        }
    }

    public async Task<IActionResult> YearComparison(int? yearA = null, int? yearB = null, string? type = null)
    {
        try
        {
            type ??= "Ride";
            var all        = await _stravaService.GetAllActivitiesAsync();
            var activities = SportTypes.FilterByType(all, type);

            var availableYears = activities.Select(a => a.StartDateLocal.Year).Distinct().OrderDescending().ToList();
            int yA = yearA ?? DateTime.Now.Year;
            int yB = yearB ?? (yA - 1);

            double TotalKm(int y)    => activities.Where(a => a.StartDateLocal.Year == y).Sum(a => a.Distance / 1000.0);
            int    TotalCount(int y) => activities.Count(a => a.StartDateLocal.Year == y);
            double TotalElev(int y)  => activities.Where(a => a.StartDateLocal.Year == y).Sum(a => (double)a.TotalElevationGain);
            double TotalHours(int y) => activities.Where(a => a.StartDateLocal.Year == y).Sum(a => a.MovingTime / 3600.0);
            double AvgKm(int y)    { var c = TotalCount(y); return c > 0 ? TotalKm(y) / c : 0; }
            double AvgSpeed(int y) { var h = TotalHours(y); return h > 0 ? TotalKm(y) / h : 0; }
            double BestKm(int y)   => activities.Where(a => a.StartDateLocal.Year == y).Select(a => a.Distance / 1000.0).DefaultIfEmpty(0).Max();

            List<(int month, double km, int count)> MonthBreakdown(int y) =>
                Enumerable.Range(1, 12).Select(m =>
                {
                    var acts = activities.Where(a => a.StartDateLocal.Year == y && a.StartDateLocal.Month == m).ToList();
                    return (m, acts.Sum(a => a.Distance / 1000.0), acts.Count);
                }).ToList();

            ViewBag.ActivityType      = type;
            ViewBag.ActivityTypeLabel = SportTypes.TypeLabel(type);
            ViewBag.IsWalk            = type == "Walk";
            ViewBag.AvailableYears    = availableYears;
            ViewBag.YearA             = yA;
            ViewBag.YearB             = yB;
            ViewBag.TotalKmA          = TotalKm(yA);    ViewBag.TotalKmB    = TotalKm(yB);
            ViewBag.CountA            = TotalCount(yA); ViewBag.CountB      = TotalCount(yB);
            ViewBag.ElevA             = TotalElev(yA);  ViewBag.ElevB       = TotalElev(yB);
            ViewBag.HoursA            = TotalHours(yA); ViewBag.HoursB      = TotalHours(yB);
            ViewBag.AvgKmA            = AvgKm(yA);      ViewBag.AvgKmB      = AvgKm(yB);
            ViewBag.AvgSpeedA         = AvgSpeed(yA);   ViewBag.AvgSpeedB   = AvgSpeed(yB);
            ViewBag.BestKmA           = BestKm(yA);     ViewBag.BestKmB     = BestKm(yB);
            ViewBag.MonthsA           = MonthBreakdown(yA);
            ViewBag.MonthsB           = MonthBreakdown(yB);

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading year comparison");
            ViewBag.Error = "Failed to load comparison data.";
            return View();
        }
    }

    public async Task<IActionResult> LongestRide(string? type = null)
    {
        try
        {
            var all      = await _stravaService.GetAllActivitiesAsync();
            var actType  = type ?? "Ride";
            var filtered = SportTypes.FilterByType(all, actType == "Walk" ? "Walk" : "Ride");

            var longest = filtered
                .Where(a => a.Map?.SummaryPolyline != null)
                .OrderByDescending(a => a.Distance)
                .FirstOrDefault();

            ViewBag.ActivityType = actType;
            ViewBag.IsWalk       = actType == "Walk";

            if (longest != null)
            {
                var isWalk      = actType == "Walk";
                var distKm      = longest.Distance / 1000.0;
                var movingHours = longest.MovingTime / 3600.0;
                var speedKmh    = movingHours > 0 ? distKm / movingHours : 0;
                var paceMinKm   = distKm > 0 ? (longest.MovingTime / 60.0) / distKm : 0;

                ViewBag.DistanceKm       = distKm.ToString("0.0");
                ViewBag.Duration         = $"{(int)(longest.MovingTime / 3600)}h {(int)((longest.MovingTime % 3600) / 60)}m";
                ViewBag.SpeedOrPace      = isWalk
                    ? $"{(int)paceMinKm}:{(int)Math.Round((paceMinKm - (int)paceMinKm) * 60):D2} /km"
                    : $"{speedKmh:0.0} km/h";
                ViewBag.SpeedOrPaceLabel = isWalk ? "Avg Pace" : "Avg Speed";
                ViewBag.Elevation        = longest.TotalElevationGain.ToString("0") + " m";
                ViewBag.Date             = longest.StartDateLocal.ToString("d MMMM yyyy");
                ViewBag.RankInAll        = filtered.OrderByDescending(a => a.Distance).ToList().IndexOf(longest) + 1;
                ViewBag.TotalActivities  = filtered.Count;
            }

            return View(longest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading longest ride/walk data");
            ViewBag.Error = "Failed to load data.";
            return View((Models.StravaActivity?)null);
        }
    }
}
