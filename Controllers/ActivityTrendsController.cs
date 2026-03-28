using ActivitiesJournal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActivitiesJournal.Controllers;

[Authorize]
[Route("Activities/[action]")]
public class ActivityTrendsController : Controller
{
    private readonly IStravaService _stravaService;
    private readonly ILogger<ActivityTrendsController> _logger;

    public ActivityTrendsController(IStravaService stravaService, ILogger<ActivityTrendsController> logger)
    {
        _stravaService = stravaService;
        _logger = logger;
    }

    public async Task<IActionResult> RouteLibrary(string? type = null)
    {
        try
        {
            type ??= "Ride";
            bool isWalk = type == "Walk";
            var all = await _stravaService.GetAllActivitiesAsync();
            var activities = SportTypes.FilterByType(all, type)
                .Where(a => a.StartLatlng?.Count >= 2 && a.Distance > 500)
                .OrderBy(a => a.StartDateLocal)
                .ToList();

            // Haversine distance in meters between two lat/lng points
            static double HavDist(double lat1, double lon1, double lat2, double lon2)
            {
                const double R = 6371000;
                double dLat = (lat2 - lat1) * Math.PI / 180;
                double dLon = (lon2 - lon1) * Math.PI / 180;
                double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                         + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
                         * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
                return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            }

            const double startThresholdM = 400;   // start within 400m
            const double distTolerancePct = 0.12; // distance within 12%

            // Greedy grouping
            var groups = new List<List<Models.StravaActivity>>();
            var assigned = new HashSet<long>();

            foreach (var act in activities)
            {
                if (assigned.Contains(act.Id)) continue;
                double lat = act.StartLatlng![0];
                double lon = act.StartLatlng[1];
                double dist = act.Distance;

                // Try to find an existing group whose representative matches
                bool found = false;
                foreach (var grp in groups)
                {
                    var rep = grp[0];
                    double rLat = rep.StartLatlng![0];
                    double rLon = rep.StartLatlng[1];
                    double rDist = rep.Distance;
                    if (HavDist(lat, lon, rLat, rLon) <= startThresholdM
                        && Math.Abs(dist - rDist) / Math.Max(rDist, 1) <= distTolerancePct)
                    {
                        grp.Add(act);
                        assigned.Add(act.Id);
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    groups.Add(new List<Models.StravaActivity> { act });
                    assigned.Add(act.Id);
                }
            }

            // Only show routes done at least 2 times
            var routeGroups = groups.Where(g => g.Count >= 2)
                .OrderByDescending(g => g.Count)
                .Select((g, idx) =>
                {
                    var rep = g.First();
                    double avgSpd = g.Average(a => a.AverageSpeed * 3.6);
                    double bestSpd = g.Max(a => a.AverageSpeed * 3.6);
                    double avgPace = g.Average(a => a.Distance > 0 ? a.MovingTime / (a.Distance / 1000.0) / 60.0 : 0);
                    double bestPace = g.Where(a => a.Distance > 0).Min(a => a.MovingTime / (a.Distance / 1000.0) / 60.0);
                    string label = g.GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                        .OrderByDescending(gg => gg.Count()).First().Key;
                    return new Models.RouteGroup
                    {
                        RouteId = idx + 1,
                        Label = label,
                        DistanceKm = Math.Round(g.Average(a => a.Distance) / 1000.0, 1),
                        StartLat = rep.StartLatlng![0],
                        StartLng = rep.StartLatlng[1],
                        Count = g.Count,
                        FirstDate = g.Min(a => a.StartDateLocal),
                        LastDate = g.Max(a => a.StartDateLocal),
                        AvgSpeedKmh = Math.Round(avgSpd, 1),
                        BestSpeedKmh = Math.Round(bestSpd, 1),
                        AvgPaceMinKm = Math.Round(avgPace, 2),
                        BestPaceMinKm = Math.Round(bestPace, 2),
                        AvgElevationM = Math.Round(g.Average(a => a.TotalElevationGain), 0),
                        Activities = g.OrderByDescending(a => a.StartDateLocal).ToList(),
                    };
                }).ToList();

            return View(new Models.RouteLibraryViewModel
            {
                ActivityType = type,
                ActivityTypeLabel = SportTypes.TypeLabel(type),
                IsWalk = isWalk,
                Routes = routeGroups,
                TotalActivities = activities.Count,
                GroupedActivities = routeGroups.Sum(r => r.Count),
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading route library");
            ViewBag.Error = "Failed to load route library.";
            return View(new Models.RouteLibraryViewModel());
        }
    }

    public async Task<IActionResult> CumulativeDistance(string? type = null, int? year = null)
    {
        try
        {
            type ??= "Ride";
            var all = await _stravaService.GetAllActivitiesAsync();
            var activities = SportTypes.FilterByType(all, type);

            var availableYears = activities.Select(a => a.StartDateLocal.Year).Distinct().OrderDescending().ToList();
            int selectedYear = year ?? DateTime.Now.Year;
            int priorYear = selectedYear - 1;

            // Build day-by-day cumulative for each year
            // Returns list of (dayOfYear 1..365, cumulKm) for that year up to the min of yearEnd and today
            // limitToDoy caps the result at that day-of-year (used for "same period" prior year comparison)
            List<(int day, double cumKm)> BuildCumulative(int y, int? limitToDoy = null)
            {
                var yearActs = activities.Where(a => a.StartDateLocal.Year == y)
                    .OrderBy(a => a.StartDateLocal).ToList();
                var result = new List<(int, double)>();
                double cum = 0;
                var start = new DateTime(y, 1, 1);
                var maxDay = y == DateTime.Today.Year ? DateTime.Today : new DateTime(y, 12, 31);
                if (limitToDoy.HasValue)
                {
                    var capped = start.AddDays(limitToDoy.Value - 1);
                    if (capped < maxDay) maxDay = capped;
                }
                var byDate = yearActs.ToLookup(a => a.StartDateLocal.Date);
                for (var d = start; d <= maxDay; d = d.AddDays(1))
                {
                    cum += byDate[d].Sum(a => a.Distance / 1000.0);
                    int doy = (d - start).Days + 1;
                    result.Add((doy, Math.Round(cum, 1)));
                }
                return result;
            }

            int todayDoy = (DateTime.Today - new DateTime(DateTime.Today.Year, 1, 1)).Days + 1;
            var currentCum  = BuildCumulative(selectedYear);
            // Cap prior year at same day-of-year as today for a true "same period" comparison
            var priorCum    = BuildCumulative(priorYear, selectedYear == DateTime.Today.Year ? todayDoy : (int?)null);

            // Only return every 7th point to keep chart responsive
            static List<(int day, double cumKm)> Downsample(List<(int day, double cumKm)> pts) =>
                pts.Where((_, i) => i % 3 == 0 || i == pts.Count - 1).ToList();

            ViewBag.ActivityType = type;
            ViewBag.ActivityTypeLabel = SportTypes.TypeLabel(type);
            ViewBag.SelectedYear = selectedYear;
            ViewBag.PriorYear = priorYear;
            ViewBag.AvailableYears = availableYears;
            ViewBag.CurrentCum = Downsample(currentCum);
            ViewBag.PriorCum   = Downsample(priorCum);

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading cumulative distance");
            ViewBag.Error = "Failed to load cumulative distance data.";
            return View();
        }
    }

    public async Task<IActionResult> Histogram(string? type = null)
    {
        try
        {
            type ??= "Ride";
            var all = await _stravaService.GetAllActivitiesAsync();
            var activities = SportTypes.FilterByType(all, type).Where(a => a.Distance > 0).ToList();

            var distRanges = new (double, double?, string)[]
            {
                (0, 5, "< 5 km"), (5, 10, "5–10 km"), (10, 20, "10–20 km"),
                (20, 30, "20–30 km"), (30, 50, "30–50 km"), (50, 75, "50–75 km"),
                (75, 100, "75–100 km"), (100, null, "100+ km"),
            };
            var distBuckets = distRanges.Select(r =>
            {
                double distKm(Models.StravaActivity a) => a.Distance / 1000.0;
                var bucket = activities.Where(a => distKm(a) >= r.Item1 && (r.Item2 == null || distKm(a) < r.Item2.Value)).ToList();
                return new Models.HistogramBucket { Label = r.Item3, Count = bucket.Count, TotalDistanceKm = Math.Round(bucket.Sum(a => a.Distance) / 1000.0, 1) };
            }).ToList();

            var durRanges = new (int, int?, string)[]
            {
                (0, 1800, "< 30 min"), (1800, 3600, "30–60 min"), (3600, 7200, "1–2 h"),
                (7200, 10800, "2–3 h"), (10800, 18000, "3–5 h"), (18000, null, "5+ h"),
            };
            var durBuckets = durRanges.Select(r =>
            {
                var bucket = activities.Where(a => a.MovingTime >= r.Item1 && (r.Item2 == null || a.MovingTime < r.Item2.Value)).ToList();
                return new Models.HistogramBucket { Label = r.Item3, Count = bucket.Count };
            }).ToList();

            var elevRanges = new (float, float?, string)[]
            {
                (0, 100, "< 100 m"), (100, 300, "100–300 m"), (300, 500, "300–500 m"),
                (500, 1000, "500–1 000 m"), (1000, 2000, "1 000–2 000 m"), (2000, null, "2 000+ m"),
            };
            var elevBuckets = elevRanges.Select(r =>
            {
                var bucket = activities.Where(a => a.TotalElevationGain >= r.Item1 && (r.Item2 == null || a.TotalElevationGain < r.Item2.Value)).ToList();
                return new Models.HistogramBucket { Label = r.Item3, Count = bucket.Count };
            }).ToList();

            return View(new Models.HistogramViewModel
            {
                ActivityType = type,
                ActivityTypeLabel = SportTypes.TypeLabel(type),
                TotalActivities = activities.Count,
                DistanceBuckets = distBuckets,
                DurationBuckets = durBuckets,
                ElevationBuckets = elevBuckets,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading histogram");
            ViewBag.Error = "Failed to load histogram data.";
            return View(new Models.HistogramViewModel());
        }
    }

    public async Task<IActionResult> SpeedTrend(string? type = null, int? year = null)
    {
        try
        {
            type ??= "Ride";
            bool isWalk = type == "Walk";
            var all = await _stravaService.GetAllActivitiesAsync();
            var activities = SportTypes.FilterByType(all, type)
                .Where(a => a.Distance > 0 && a.MovingTime > 0)
                .OrderBy(a => a.StartDateLocal)
                .ToList();

            var availableYears = activities.Select(a => a.StartDateLocal.Year).Distinct().OrderDescending().ToList();

            if (year.HasValue)
                activities = activities.Where(a => a.StartDateLocal.Year == year.Value).ToList();

            var points = activities.Select(a =>
            {
                double distKm = a.Distance / 1000.0;
                double val = isWalk
                    ? a.MovingTime / distKm / 60.0          // pace in min/km (decimal)
                    : a.AverageSpeed * 3.6;                  // speed in km/h
                return new Models.SpeedTrendPoint
                {
                    Date = a.StartDateLocal,
                    Value = Math.Round(val, 2),
                    DistanceKm = Math.Round(distKm, 1),
                    ActivityId = a.Id,
                    ActivityName = a.Name,
                };
            }).ToList();

            // 10-activity rolling average
            const int window = 10;
            var rolling = new List<Models.SpeedTrendPoint>();
            for (int i = window - 1; i < points.Count; i++)
            {
                double avg = points.Skip(i - window + 1).Take(window).Average(p => p.Value);
                rolling.Add(new Models.SpeedTrendPoint
                {
                    Date = points[i].Date,
                    Value = Math.Round(avg, 2),
                });
            }

            var vm = new Models.SpeedTrendViewModel
            {
                ActivityType = type,
                ActivityTypeLabel = SportTypes.TypeLabel(type),
                IsWalk = isWalk,
                YAxisLabel = isWalk ? "min/km" : "km/h",
                Points = points,
                RollingAvg = rolling,
                AvailableYears = availableYears,
                SelectedYear = year,
            };

            return View(vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading speed trend");
            ViewBag.Error = "Failed to load speed trend data.";
            return View(new Models.SpeedTrendViewModel());
        }
    }

    public async Task<IActionResult> CadenceTrend(int? year = null)
    {
        try
        {
            var all = await _stravaService.GetAllActivitiesAsync();
            var rides = all
                .Where(a => SportTypes.IsRide(a.SportType))
                .Where(a => a.AverageCadence.HasValue && a.AverageCadence > 0)
                .OrderBy(a => a.StartDateLocal)
                .ToList();

            var availableYears = rides.Select(r => r.StartDateLocal.Year).Distinct().OrderDescending().ToList();
            var filtered = year.HasValue ? rides.Where(r => r.StartDateLocal.Year == year.Value).ToList() : rides;

            // Build points: date, cadence, rolling 10-ride average
            var points = filtered.Select(a => new {
                Date = a.StartDateLocal.ToString("yyyy-MM-dd"),
                Cadence = (double)a.AverageCadence!.Value,
                DistKm = a.Distance / 1000.0,
                Name = a.Name,
                Id = a.Id,
            }).ToList();

            // Rolling 10-ride average of cadence
            var rollingAvg = points.Select((p, i) =>
            {
                var windowPts = points.Skip(Math.Max(0, i - 9)).Take(Math.Min(10, i + 1));
                return Math.Round(windowPts.Average(x => x.Cadence), 1);
            }).ToList();

            // Monthly averages
            var monthlyAvg = filtered
                .GroupBy(a => new { a.StartDateLocal.Year, a.StartDateLocal.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new {
                    Label = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yy"),
                    Avg = Math.Round(g.Average(a => (double)a.AverageCadence!.Value), 1),
                    Count = g.Count(),
                }).ToList();

            double overallAvg = filtered.Any() ? filtered.Average(a => (double)a.AverageCadence!.Value) : 0;
            double maxCad = filtered.Any() ? filtered.Max(a => (double)a.AverageCadence!.Value) : 0;
            double minCad = filtered.Any() ? filtered.Min(a => (double)a.AverageCadence!.Value) : 0;
            var bestCad = filtered.Any() ? filtered.OrderByDescending(a => a.AverageCadence).First() : null;

            ViewBag.AvailableYears = availableYears;
            ViewBag.SelectedYear = year;
            ViewBag.Points = points;
            ViewBag.RollingAvg = rollingAvg;
            ViewBag.MonthlyAvg = monthlyAvg;
            ViewBag.OverallAvg = overallAvg;
            ViewBag.MaxCad = maxCad;
            ViewBag.MinCad = minCad;
            ViewBag.BestCadActivity = bestCad?.Name ?? "–";
            ViewBag.BestCadId = bestCad?.Id;
            ViewBag.TotalRides = filtered.Count;

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading cadence trend");
            ViewBag.Error = "Failed to load cadence data.";
            return View();
        }
    }

    public async Task<IActionResult> DistanceProjection(string? type = null)
    {
        try
        {
            type ??= "Ride";
            var all = await _stravaService.GetAllActivitiesAsync();
            var activities = SportTypes.FilterByType(all, type);

            var today = DateTime.Today;
            int currentYear = today.Year;
            int lastYearNum = currentYear - 1;
            var yearStart = new DateTime(currentYear, 1, 1);
            var yearEnd = new DateTime(currentYear, 12, 31);
            int daysRemaining = (yearEnd - today).Days;

            // Build day-by-day actual cumulative for current year
            var currentYearActs = activities.Where(a => a.StartDateLocal.Year == currentYear).ToList();
            var byDate = currentYearActs.ToLookup(a => a.StartDateLocal.Date);

            var actual = new List<Models.DistanceProjectionPoint>();
            double cum = 0;
            for (var d = yearStart; d <= today; d = d.AddDays(1))
            {
                cum += byDate[d].Sum(a => a.Distance / 1000.0);
                actual.Add(new() { Date = d.ToString("yyyy-MM-dd"), Km = Math.Round(cum, 1) });
            }
            double currentTotalKm = cum;

            // Compute average and stddev of daily km over the last 30 days
            const int windowDays = 30;
            var windowStart = today.AddDays(-windowDays + 1);
            var dailyKmsInWindow = new List<double>();
            for (var d = windowStart; d <= today; d = d.AddDays(1))
                dailyKmsInWindow.Add(byDate[d].Sum(a => a.Distance / 1000.0));

            double avgDaily = dailyKmsInWindow.Count > 0 ? dailyKmsInWindow.Average() : 0;
            double stddev = 0;
            if (dailyKmsInWindow.Count > 1)
            {
                double variance = dailyKmsInWindow.Sum(x => Math.Pow(x - avgDaily, 2)) / dailyKmsInWindow.Count;
                stddev = Math.Sqrt(variance);
            }

            // Project from today to Dec 31 (today is the anchor point for all three lines)
            var projection = new List<Models.DistanceProjectionPoint>();
            var upperBand = new List<Models.DistanceProjectionPoint>();
            var lowerBand = new List<Models.DistanceProjectionPoint>();

            projection.Add(new() { Date = today.ToString("yyyy-MM-dd"), Km = Math.Round(currentTotalKm, 1) });
            upperBand.Add(new() { Date = today.ToString("yyyy-MM-dd"), Km = Math.Round(currentTotalKm, 1) });
            lowerBand.Add(new() { Date = today.ToString("yyyy-MM-dd"), Km = Math.Round(currentTotalKm, 1) });

            double projCum = currentTotalKm, upperCum = currentTotalKm, lowerCum = currentTotalKm;
            for (var d = today.AddDays(1); d <= yearEnd; d = d.AddDays(1))
            {
                projCum += avgDaily;
                upperCum += avgDaily + stddev;
                lowerCum += Math.Max(0, avgDaily - stddev);
                projection.Add(new() { Date = d.ToString("yyyy-MM-dd"), Km = Math.Round(projCum, 1) });
                upperBand.Add(new() { Date = d.ToString("yyyy-MM-dd"), Km = Math.Round(upperCum, 1) });
                lowerBand.Add(new() { Date = d.ToString("yyyy-MM-dd"), Km = Math.Round(lowerCum, 1) });
            }

            // Build last year's cumulative, mapped to current year dates for overlay
            var lastYearActs = activities.Where(a => a.StartDateLocal.Year == lastYearNum).ToList();
            var lastYearByDate = lastYearActs.ToLookup(a => a.StartDateLocal.Date);
            var lastYear = new List<Models.DistanceProjectionPoint>();
            double lastCum = 0;
            for (var d = new DateTime(lastYearNum, 1, 1); d.Year == lastYearNum; d = d.AddDays(1))
            {
                lastCum += lastYearByDate[d].Sum(a => a.Distance / 1000.0);
                var equiv = yearStart.AddDays(d.DayOfYear - 1);
                if (equiv <= yearEnd)
                    lastYear.Add(new() { Date = equiv.ToString("yyyy-MM-dd"), Km = Math.Round(lastCum, 1) });
            }

            var vm = new Models.DistanceProjectionViewModel
            {
                ActivityType = type,
                ActivityTypeLabel = SportTypes.TypeLabel(type),
                CurrentYear = currentYear,
                Actual = actual,
                Projection = projection,
                UpperBand = upperBand,
                LowerBand = lowerBand,
                LastYear = lastYear,
                CurrentTotalKm = Math.Round(currentTotalKm, 1),
                ProjectedTotalKm = Math.Round(projCum, 1),
                UpperProjectedKm = Math.Round(upperCum, 1),
                LowerProjectedKm = Math.Round(lowerCum, 1),
                LastYearTotalKm = Math.Round(lastCum, 1),
                AvgDailyKm = Math.Round(avgDaily, 2),
                DaysRemaining = daysRemaining,
                ProjectionWindowDays = windowDays,
            };

            return View(vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading distance projection");
            ViewBag.Error = "Failed to load distance projection data.";
            return View(new Models.DistanceProjectionViewModel());
        }
    }

    public async Task<IActionResult> Heatmap(int? year = null, string? mode = null)
    {
        try
        {
            var all = await _stravaService.GetAllActivitiesAsync();

            var availableYears = all.Select(a => a.StartDateLocal.Year).Distinct().OrderDescending().ToList();
            ViewBag.AvailableYears = availableYears;
            ViewBag.SelectedYear = year;
            ViewBag.Mode = mode ?? "all";  // "all" or "new"

            var withPolylines = (year == null
                ? all
                : all.Where(a => a.StartDateLocal.Year == year))
                .Where(a => a.Map?.SummaryPolyline != null)
                .OrderBy(a => a.StartDateLocal)
                .ToList();

            // Pass activity metadata: polyline, date, sport type for coloring
            var today = DateTime.Today;
            var activityMeta = withPolylines.Select(a => new
            {
                p = a.Map!.SummaryPolyline!,
                y = a.StartDateLocal.Year,
                daysAgo = (today - a.StartDateLocal.Date).Days,
                isNew = (today - a.StartDateLocal.Date).Days <= 180,
                sport = a.SportType,
            }).ToList();

            ViewBag.ActivityMeta = System.Text.Json.JsonSerializer.Serialize(activityMeta);
            ViewBag.NewCount = activityMeta.Count(a => a.isNew);
            ViewBag.OldCount = activityMeta.Count(a => !a.isNew);

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
