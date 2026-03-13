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

    public async Task<IActionResult> Badges(string? type = null)
    {
        type ??= "Ride";
        ViewBag.ActivityType = type;
        ViewBag.ActivityTypeLabel = ActivityTypeLabel(type);

        try
        {
            var all = await _stravaService.GetAllActivitiesAsync();

            if (type == "Walk")
            {
                var walks = all.Where(a => a.SportType is "Walk" or "Hike" or "VirtualWalk")
                               .OrderBy(a => a.StartDateLocal).ToList();

                double wDistKm = walks.Sum(a => a.Distance) / 1000.0;
                double wElevM  = walks.Sum(a => (double)a.TotalElevationGain);
                int totalWalks = walks.Count;
                var walkDates = walks.Select(a => a.StartDateLocal.Date).Distinct().OrderBy(d => d).ToList();
                int wLongestStreak = ComputeLongestStreak(walkDates);

                var wHalfMarathons = walks.Where(a => a.Distance >= 21_000).ToList();
                var wMarathons     = walks.Where(a => a.Distance >= 42_000).ToList();
                var wBigClimbs     = walks.Where(a => a.TotalElevationGain >= 2_000).ToList();
                var wEarlyBird     = walks.Where(a => a.StartDateLocal.Hour < 7).ToList();
                var wEveningWalks  = walks.Where(a => a.StartDateLocal.Hour >= 20).ToList();

                var walkBadges = new List<Models.Badge>
                {
                    MilestoneBadge("First Walk",   "Complete your first walk or hike",    "bi-person-walking",      1,   totalWalks, walks.FirstOrDefault()),
                    MilestoneBadge("10 Walks",     "Complete 10 walks",                   "bi-person-walking",      10,  totalWalks, walks.Count >= 10  ? walks[9]  : null),
                    MilestoneBadge("50 Walks",     "Complete 50 walks",                   "bi-person-walking-fill", 50,  totalWalks, walks.Count >= 50  ? walks[49] : null),
                    MilestoneBadge("100 Walks",    "Complete 100 walks",                  "bi-person-walking-fill", 100, totalWalks, walks.Count >= 100 ? walks[99] : null),
                    MilestoneBadge("500 Walks",    "Complete 500 walks",                  "bi-person-walking-fill", 500, totalWalks, walks.Count >= 500 ? walks[499]: null),

                    DistanceBadge("Walker 100 km",   "Walk 100 km total",   "bi-signpost",        100,   wDistKm, walks),
                    DistanceBadge("Walker 1,000 km", "Walk 1,000 km total", "bi-signpost-2",      1_000, wDistKm, walks),
                    DistanceBadge("Walker 5,000 km", "Walk 5,000 km total", "bi-signpost-2-fill", 5_000, wDistKm, walks),

                    ElevationBadge("Hillwalker (500 m)", "Gain 500+ m in one walk",     "bi-triangle",      500,   wElevM, walks),
                    ElevationBadge("Everest Walker",     "Climb 8,849 m total walking", "bi-triangle-fill", 8_849, wElevM, walks),

                    new Models.Badge { Name = "Half-Marathon Walker", Description = "Walk 21+ km in a single outing", Icon = "bi-person-running",
                        Earned = wHalfMarathons.Any(), EarningActivity = wHalfMarathons.FirstOrDefault(), EarnedOn = wHalfMarathons.FirstOrDefault()?.StartDateLocal,
                        Progress = wHalfMarathons.Any() ? null : $"Longest: {(walks.Any() ? (walks.Max(a => a.Distance)/1000.0).ToString("0.0") : "0")} km" },

                    new Models.Badge { Name = "Marathon Walker", Description = "Walk 42+ km in a single outing", Icon = "bi-trophy",
                        Earned = wMarathons.Any(), EarningActivity = wMarathons.FirstOrDefault(), EarnedOn = wMarathons.FirstOrDefault()?.StartDateLocal,
                        Progress = wMarathons.Any() ? null : $"Longest: {(walks.Any() ? (walks.Max(a => a.Distance)/1000.0).ToString("0.0") : "0")} km" },

                    new Models.Badge { Name = "Mountain Goat (Walking)", Description = "Gain 2,000+ m elevation in one walk", Icon = "bi-sunrise",
                        Earned = wBigClimbs.Any(), EarningActivity = wBigClimbs.FirstOrDefault(), EarnedOn = wBigClimbs.FirstOrDefault()?.StartDateLocal,
                        Progress = wBigClimbs.Any() ? null : $"Best: {(walks.Any() ? walks.Max(a => a.TotalElevationGain).ToString("0") : "0")} m" },

                    new Models.Badge { Name = "Habit Walker", Description = "Walk 7 days in a row", Icon = "bi-fire",
                        Earned = wLongestStreak >= 7, Progress = wLongestStreak >= 7 ? null : $"Best streak: {wLongestStreak} day(s)" },

                    new Models.Badge { Name = "Habit Walker Pro", Description = "Walk 30 days in a row", Icon = "bi-stars",
                        Earned = wLongestStreak >= 30, Progress = wLongestStreak >= 30 ? null : $"Best streak: {wLongestStreak} day(s)" },

                    new Models.Badge { Name = "Early Bird Walker", Description = "Start a walk before 7 AM", Icon = "bi-sunrise-fill",
                        Earned = wEarlyBird.Any(), EarningActivity = wEarlyBird.FirstOrDefault(), EarnedOn = wEarlyBird.FirstOrDefault()?.StartDateLocal },

                    new Models.Badge { Name = "Evening Walker", Description = "Start a walk at or after 8 PM", Icon = "bi-moon-fill",
                        Earned = wEveningWalks.Any(), EarningActivity = wEveningWalks.FirstOrDefault(), EarnedOn = wEveningWalks.FirstOrDefault()?.StartDateLocal },

                    new Models.Badge { Name = "Year-Round Walker", Description = "Walk in all 12 calendar months in a year", Icon = "bi-calendar-check",
                        Earned = walks.GroupBy(a => a.StartDateLocal.Year).Any(g => g.Select(a => a.StartDateLocal.Month).Distinct().Count() == 12),
                        Progress = $"Best: {(walks.Any() ? walks.GroupBy(a => a.StartDateLocal.Year).Max(g => g.Select(a => a.StartDateLocal.Month).Distinct().Count()) : 0)} months" },
                };

                return View(new Models.BadgesViewModel { Badges = walkBadges });
            }

            var rides = all.Where(a => a.SportType is "Ride" or "VirtualRide" or "GravelRide" or "MountainBikeRide")
                           .OrderBy(a => a.StartDateLocal).ToList();

            double totalDistKm = rides.Sum(a => a.Distance) / 1000.0;
            double totalElevM = rides.Sum(a => (double)a.TotalElevationGain);
            int totalRides = rides.Count;

            var rideDates = rides.Select(a => a.StartDateLocal.Date).Distinct().OrderBy(d => d).ToList();
            int longestStreak = ComputeLongestStreak(rideDates);

            var centuries = rides.Where(a => a.Distance >= 100_000).ToList();
            var bigClimbs = rides.Where(a => a.TotalElevationGain >= 2000).ToList();
            var earlyBird = rides.Where(a => a.StartDateLocal.Hour < 7).ToList();
            var nightOwl = rides.Where(a => a.StartDateLocal.Hour >= 20).ToList();
            var fastRides = rides.Where(a => a.Distance >= 40_000 && a.AverageSpeed * 3.6 >= 35).ToList();

            var badges = new List<Models.Badge>
            {
                MilestoneBadge("First Ride", "Complete your first bike ride", "bi-bicycle", 1, totalRides, rides.FirstOrDefault()),
                MilestoneBadge("10 Rides", "Complete 10 rides", "bi-bicycle", 10, totalRides, rides.Count >= 10 ? rides[9] : null),
                MilestoneBadge("50 Rides", "Complete 50 rides", "bi-bicycle-fill", 50, totalRides, rides.Count >= 50 ? rides[49] : null),
                MilestoneBadge("100 Rides", "Complete 100 rides", "bi-bicycle-fill", 100, totalRides, rides.Count >= 100 ? rides[99] : null),
                MilestoneBadge("500 Rides", "Complete 500 rides", "bi-bicycle-fill", 500, totalRides, rides.Count >= 500 ? rides[499] : null),

                DistanceBadge("100 km Club", "Ride 100 km total", "bi-signpost", 100, totalDistKm, rides),
                DistanceBadge("1,000 km Club", "Ride 1,000 km total", "bi-signpost-2", 1_000, totalDistKm, rides),
                DistanceBadge("5,000 km Club", "Ride 5,000 km total", "bi-signpost-2-fill", 5_000, totalDistKm, rides),
                DistanceBadge("10,000 km Club", "Ride 10,000 km total", "bi-globe", 10_000, totalDistKm, rides),
                DistanceBadge("Moon Shot (384,400 km)", "Ride the distance to the Moon", "bi-moon-stars", 384_400, totalDistKm, rides),

                ElevationBadge("Everest (8,849 m)", "Climb as high as Mt Everest in total", "bi-triangle", 8_849, totalElevM, rides),
                ElevationBadge("Triple Everest", "Climb 3× Everest in total", "bi-triangle-fill", 26_547, totalElevM, rides),
                ElevationBadge("10× Everest", "Climb 10× Everest in total", "bi-triangle-fill", 88_490, totalElevM, rides),

                new Models.Badge { Name = "Century Ride", Description = "Complete a 100 km+ ride in a single session", Icon = "bi-c-circle",
                    Earned = centuries.Any(), EarningActivity = centuries.FirstOrDefault(),
                    EarnedOn = centuries.FirstOrDefault()?.StartDateLocal,
                    Progress = centuries.Any() ? null : $"Longest: {(rides.Any() ? (rides.Max(a => a.Distance) / 1000.0).ToString("0.0") : "0")} km" },

                new Models.Badge { Name = "Mountain Goat", Description = "Gain 2,000+ m elevation in a single ride", Icon = "bi-sunrise",
                    Earned = bigClimbs.Any(), EarningActivity = bigClimbs.FirstOrDefault(),
                    EarnedOn = bigClimbs.FirstOrDefault()?.StartDateLocal,
                    Progress = bigClimbs.Any() ? null : $"Best: {(rides.Any() ? rides.Max(a => a.TotalElevationGain).ToString("0") : "0")} m" },

                new Models.Badge { Name = "Speed Demon", Description = "Average 35+ km/h on a 40+ km ride", Icon = "bi-lightning-charge-fill",
                    Earned = fastRides.Any(), EarningActivity = fastRides.FirstOrDefault(),
                    EarnedOn = fastRides.FirstOrDefault()?.StartDateLocal,
                    Progress = fastRides.Any() ? null : "Avg 35 km/h on a 40+ km ride" },

                new Models.Badge { Name = "Week Warrior", Description = "Ride 7 days in a row", Icon = "bi-fire",
                    Earned = longestStreak >= 7,
                    Progress = longestStreak >= 7 ? null : $"Best streak: {longestStreak} day(s)" },

                new Models.Badge { Name = "Early Bird", Description = "Start a ride before 7 AM", Icon = "bi-sunrise-fill",
                    Earned = earlyBird.Any(), EarningActivity = earlyBird.FirstOrDefault(),
                    EarnedOn = earlyBird.FirstOrDefault()?.StartDateLocal },

                new Models.Badge { Name = "Night Owl", Description = "Start a ride at or after 8 PM", Icon = "bi-moon-fill",
                    Earned = nightOwl.Any(), EarningActivity = nightOwl.FirstOrDefault(),
                    EarnedOn = nightOwl.FirstOrDefault()?.StartDateLocal },

                new Models.Badge { Name = "Year-Round Rider", Description = "Ride in all 12 calendar months in a single year", Icon = "bi-calendar-check",
                    Earned = rides.GroupBy(a => a.StartDateLocal.Year).Any(g => g.Select(a => a.StartDateLocal.Month).Distinct().Count() == 12),
                    Progress = $"Best: {(rides.Any() ? rides.GroupBy(a => a.StartDateLocal.Year).Max(g => g.Select(a => a.StartDateLocal.Month).Distinct().Count()) : 0)} months" },
            };

            return View(new Models.BadgesViewModel { Badges = badges });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading badges");
            ViewBag.Error = "Failed to load badges.";
            return View(new Models.BadgesViewModel());
        }
    }

    private static List<Models.StravaActivity> FilterByActivityType(List<Models.StravaActivity> all, string type) => type switch
    {
        "Walk" => all.Where(a => a.SportType is "Walk" or "Hike" or "VirtualWalk").ToList(),
        "All"  => all,
        _      => all.Where(a => a.SportType is "Ride" or "VirtualRide" or "GravelRide" or "MountainBikeRide").ToList(),
    };

    private static string ActivityTypeLabel(string type) => type switch
    {
        "Walk" => "Walks & Hikes",
        "All"  => "All Activities",
        _      => "Rides",
    };

    private static int ComputeLongestStreak(List<DateTime> sortedDates)
    {
        if (!sortedDates.Any()) return 0;
        int longest = 1, current = 1;
        for (int i = 1; i < sortedDates.Count; i++)
        {
            current = (sortedDates[i] - sortedDates[i - 1]).Days == 1 ? current + 1 : 1;
            if (current > longest) longest = current;
        }
        return longest;
    }

    private static Models.Badge MilestoneBadge(string name, string desc, string icon, int target, int actual, Models.StravaActivity? earner)
        => new() { Name = name, Description = desc, Icon = icon, Earned = actual >= target,
            EarningActivity = earner, EarnedOn = earner?.StartDateLocal,
            Progress = actual < target ? $"{actual}/{target} rides" : null };

    private static Models.Badge DistanceBadge(string name, string desc, string icon, double targetKm, double actualKm, List<Models.StravaActivity> rides)
    {
        var earned = actualKm >= targetKm;
        Models.StravaActivity? earner = null;
        if (earned)
        {
            double cum = 0;
            foreach (var r in rides) { cum += r.Distance / 1000.0; if (cum >= targetKm) { earner = r; break; } }
        }
        return new() { Name = name, Description = desc, Icon = icon, Earned = earned,
            EarningActivity = earner, EarnedOn = earner?.StartDateLocal,
            Progress = earned ? null : $"{actualKm:0.0}/{targetKm:0} km" };
    }

    private static Models.Badge ElevationBadge(string name, string desc, string icon, double targetM, double actualM, List<Models.StravaActivity> rides)
    {
        var earned = actualM >= targetM;
        Models.StravaActivity? earner = null;
        if (earned)
        {
            double cum = 0;
            foreach (var r in rides) { cum += r.TotalElevationGain; if (cum >= targetM) { earner = r; break; } }
        }
        return new() { Name = name, Description = desc, Icon = icon, Earned = earned,
            EarningActivity = earner, EarnedOn = earner?.StartDateLocal,
            Progress = earned ? null : $"{actualM:0}/{targetM:0} m" };
    }

    public async Task<IActionResult> Calendar(int? year = null, string? type = null)
    {
        try
        {
            type ??= "Ride";
            var all = await _stravaService.GetAllActivitiesAsync();
            var rides = FilterByActivityType(all, type);
            ViewBag.ActivityType = type;
            ViewBag.ActivityTypeLabel = ActivityTypeLabel(type);

            var availableYears = rides.Select(a => a.StartDateLocal.Year).Distinct().OrderDescending().ToList();
            int selectedYear = year ?? DateTime.Now.Year;

            var yearRides = rides.Where(a => a.StartDateLocal.Year == selectedYear).ToList();

            var grouped = yearRides
                .GroupBy(a => a.StartDateLocal.Date)
                .ToDictionary(g => g.Key, g => new
                {
                    Count = g.Count(),
                    Dist = g.Sum(a => a.Distance) / 1000.0,
                    Elev = (double)g.Sum(a => a.TotalElevationGain),
                });

            double maxDist = grouped.Values.Any() ? grouped.Values.Max(v => v.Dist) : 1;

            var dayData = grouped.ToDictionary(kv => kv.Key, kv => new Models.CalendarDayData
            {
                Date = kv.Key,
                RideCount = kv.Value.Count,
                DistanceKm = Math.Round(kv.Value.Dist, 1),
                ElevationM = Math.Round(kv.Value.Elev, 0),
                Level = kv.Value.Dist <= 0 ? 0
                      : kv.Value.Dist < maxDist * 0.25 ? 1
                      : kv.Value.Dist < maxDist * 0.50 ? 2
                      : kv.Value.Dist < maxDist * 0.75 ? 3 : 4,
            });

            var vm = new Models.CalendarViewModel
            {
                Year = selectedYear,
                AvailableYears = availableYears,
                DayData = dayData,
                TotalRides = yearRides.Count,
                TotalDistanceKm = Math.Round(yearRides.Sum(a => a.Distance) / 1000.0, 1),
                TotalElevationM = Math.Round(yearRides.Sum(a => (double)a.TotalElevationGain), 0),
                ActiveDays = dayData.Count,
            };

            return View(vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading calendar");
            ViewBag.Error = "Failed to load calendar data.";
            return View(new Models.CalendarViewModel { Year = DateTime.Now.Year });
        }
    }

    public async Task<IActionResult> DayInHistory(string? type = null)
    {
        try
        {
            type ??= "Ride";
            var all = await _stravaService.GetAllActivitiesAsync();
            var rides = FilterByActivityType(all, type);
            ViewBag.ActivityType = type;
            ViewBag.ActivityTypeLabel = ActivityTypeLabel(type);

            var today = DateTime.Now;

            // Same day across years (excluding current year)
            var byYear = rides
                .Where(a => a.StartDateLocal.Month == today.Month && a.StartDateLocal.Day == today.Day && a.StartDateLocal.Year != today.Year)
                .GroupBy(a => a.StartDateLocal.Year)
                .OrderByDescending(g => g.Key)
                .Select(g => (g.Key, g.OrderByDescending(a => a.StartDateLocal).ToList()))
                .ToList();

            // Streak calculation: days that have at least one ride
            var rideDates = rides
                .Select(a => a.StartDateLocal.Date)
                .Distinct()
                .OrderByDescending(d => d)
                .ToHashSet();

            int currentStreak = 0;
            var check = today.Date;
            while (rideDates.Contains(check))
            {
                currentStreak++;
                check = check.AddDays(-1);
            }

            // Longest streak
            var allDates = rideDates.OrderBy(d => d).ToList();
            int longest = 0, streakLen = 1;
            DateTime streakStart = allDates.FirstOrDefault(), longestStart = allDates.FirstOrDefault(), longestEnd = allDates.FirstOrDefault();
            streakStart = allDates.FirstOrDefault();

            for (int i = 1; i < allDates.Count; i++)
            {
                if ((allDates[i] - allDates[i - 1]).Days == 1)
                {
                    streakLen++;
                }
                else
                {
                    if (streakLen > longest)
                    {
                        longest = streakLen;
                        longestStart = streakStart;
                        longestEnd = allDates[i - 1];
                    }
                    streakLen = 1;
                    streakStart = allDates[i];
                }
            }
            if (streakLen > longest)
            {
                longest = streakLen;
                longestStart = streakStart;
                longestEnd = allDates.LastOrDefault();
            }

            var vm = new Models.DayInHistoryViewModel
            {
                Today = today,
                ByYear = byYear,
                CurrentStreakDays = currentStreak,
                LongestStreakDays = longest,
                LongestStreakStart = longestStart,
                LongestStreakEnd = longestEnd,
            };

            return View(vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading day in history");
            ViewBag.Error = "Failed to load data.";
            return View(new Models.DayInHistoryViewModel { Today = DateTime.Now });
        }
    }

    public async Task<IActionResult> MonthComparison(int? year = null, string? type = null)
    {
        try
        {
            type ??= "Ride";
            var all = await _stravaService.GetAllActivitiesAsync();
            var rides = FilterByActivityType(all, type);
            ViewBag.ActivityType = type;
            ViewBag.ActivityTypeLabel = ActivityTypeLabel(type);

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

    public async Task<IActionResult> PersonalRecords(string? type = null)
    {
        try
        {
            type ??= "Ride";
            var all = await _stravaService.GetAllActivitiesAsync();
            var rides = FilterByActivityType(all, type);
            ViewBag.ActivityType = type;
            ViewBag.ActivityTypeLabel = ActivityTypeLabel(type);

            if (!rides.Any())
            {
                return View(new Models.PersonalRecordsViewModel());
            }

            var longestByDist = rides.MaxBy(a => a.Distance)!;
            var mostClimbing = rides.MaxBy(a => a.TotalElevationGain)!;
            var longestTime = rides.MaxBy(a => a.MovingTime)!;

            var records = new List<Models.PersonalRecord>
            {
                new() { Label = "Longest", Value = $"{longestByDist.Distance / 1000.0:0.00} km", Icon = "bi-rulers", Activity = longestByDist },
                new() { Label = "Longest Time", Value = TimeSpan.FromSeconds(longestTime.MovingTime).ToString(@"h\:mm\:ss"), Icon = "bi-clock", Activity = longestTime },
                new() { Label = "Most Elevation", Value = $"{mostClimbing.TotalElevationGain:0} m", Icon = "bi-triangle", Activity = mostClimbing },
            };

            if (type == "Walk")
            {
                // Best pace for walks (min/km), min 3 km
                var bestPace = rides.Where(a => a.Distance >= 3000 && a.MovingTime > 0)
                                    .MinBy(a => a.MovingTime / (a.Distance / 1000.0));
                if (bestPace != null)
                {
                    double secPerKm = bestPace.MovingTime / (bestPace.Distance / 1000.0);
                    int min = (int)(secPerKm / 60), sec = (int)(secPerKm % 60);
                    records.Add(new() { Label = "Best Pace", Value = $"{min}:{sec:D2} /km", Icon = "bi-lightning-charge", Activity = bestPace });
                }
            }
            else
            {
                var maxSpeed = rides.MaxBy(a => a.MaxSpeed)!;
                records.Add(new() { Label = "Top Speed (max)", Value = $"{maxSpeed.MaxSpeed * 3.6:0.0} km/h", Icon = "bi-lightning-charge", Activity = maxSpeed });
                var fastestAvg = rides.Where(a => a.Distance >= 20_000).MaxBy(a => a.AverageSpeed);
                if (fastestAvg != null)
                    records.Add(new() { Label = "Fastest Avg Speed (≥20 km)", Value = $"{fastestAvg.AverageSpeed * 3.6:0.0} km/h", Icon = "bi-speedometer2", Activity = fastestAvg });
            }

            var vm = new Models.PersonalRecordsViewModel
            {
                AllTimeRecords = records,
                Top10Longest = rides.OrderByDescending(a => a.Distance).Take(10).ToList(),
                Top10Fastest = type == "Walk"
                    ? rides.Where(a => a.Distance >= 3000 && a.MovingTime > 0)
                           .OrderBy(a => a.MovingTime / (a.Distance / 1000.0)).Take(10).ToList()
                    : rides.Where(a => a.Distance >= 20_000).OrderByDescending(a => a.AverageSpeed).Take(10).ToList(),
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

    public async Task<IActionResult> Segments(int count = 30)
    {
        try
        {
            var all = await _stravaService.GetAllActivitiesAsync();
            var rides = all.Where(a => a.SportType is "Ride" or "VirtualRide" or "GravelRide" or "MountainBikeRide")
                           .OrderByDescending(a => a.StartDateLocal).ToList();

            int fetchCount = Math.Min(count, rides.Count);
            var toFetch = rides.Take(fetchCount).ToList();

            // Parallel fetch with concurrency cap to respect rate limits
            var semaphore = new SemaphoreSlim(5, 5);
            var details = await Task.WhenAll(toFetch.Select(async r =>
            {
                await semaphore.WaitAsync();
                try { return await _stravaService.GetActivityByIdAsync(r.Id); }
                finally { semaphore.Release(); }
            }));

            // --- Segment leaderboard (Feature #5) ---
            var segmentMap = new Dictionary<long, Models.SegmentBestTime>();
            foreach (var detail in details.Where(d => d?.SegmentEfforts != null))
            {
                foreach (var effort in detail!.SegmentEfforts!)
                {
                    if (effort.Segment == null) continue;
                    if (!segmentMap.TryGetValue(effort.Segment.Id, out var seg))
                    {
                        seg = new Models.SegmentBestTime
                        {
                            SegmentId = effort.Segment.Id,
                            SegmentName = effort.Segment.Name,
                            DistanceM = effort.Segment.Distance,
                            AverageGrade = effort.Segment.AverageGrade,
                            StartLat = effort.Segment.StartLatlng?.Count >= 2 ? effort.Segment.StartLatlng[0] : null,
                            StartLng = effort.Segment.StartLatlng?.Count >= 2 ? effort.Segment.StartLatlng[1] : null,
                            EndLat = effort.Segment.EndLatlng?.Count >= 2 ? effort.Segment.EndLatlng[0] : null,
                            EndLng = effort.Segment.EndLatlng?.Count >= 2 ? effort.Segment.EndLatlng[1] : null,
                        };
                        segmentMap[effort.Segment.Id] = seg;
                    }
                    seg.AllAttempts.Add(new Models.SegmentAttempt
                    {
                        Date = detail.StartDateLocal,
                        ElapsedSeconds = effort.ElapsedTime,
                        ActivityId = detail.Id,
                        ActivityName = detail.Name,
                        PrRank = effort.PrRank,
                    });
                }
            }

            foreach (var seg in segmentMap.Values)
            {
                var best = seg.AllAttempts.MinBy(a => a.ElapsedSeconds)!;
                seg.BestElapsedSeconds = best.ElapsedSeconds;
                seg.BestPrRank = best.PrRank;
                seg.BestDate = best.Date;
                seg.BestActivityId = best.ActivityId;
                seg.BestActivityName = best.ActivityName;
                seg.AttemptCount = seg.AllAttempts.Count;
                seg.AllAttempts = seg.AllAttempts.OrderBy(a => a.Date).ToList();
            }

            // --- Best efforts over time (Feature #4) ---
            var effortMap = new Dictionary<string, Models.BestEffortRow>();
            foreach (var detail in details.Where(d => d?.BestEfforts != null))
            {
                foreach (var be in detail!.BestEfforts!)
                {
                    if (!effortMap.TryGetValue(be.Name, out var row))
                    {
                        row = new Models.BestEffortRow { DistanceName = be.Name, DistanceM = be.Distance };
                        effortMap[be.Name] = row;
                    }
                    row.History.Add((detail.StartDateLocal, be.ElapsedTime, detail.Id, detail.Name));
                }
            }
            foreach (var row in effortMap.Values)
            {
                var best = row.History.MinBy(h => h.Seconds);
                row.BestElapsedSeconds = best.Seconds;
                row.BestDate = best.Date;
                row.BestActivityId = best.ActivityId;
                row.BestActivityName = best.ActivityName;
                row.History = row.History.OrderBy(h => h.Date).ToList();
            }

            // Fetch segment polylines in parallel (cached permanently after first load)
            var polySem = new SemaphoreSlim(5);
            var polyTasks = segmentMap.Values
                .Where(s => s.StartLat.HasValue)
                .Select(async seg =>
                {
                    await polySem.WaitAsync();
                    try { seg.Polyline = await _stravaService.GetSegmentPolylineAsync(seg.SegmentId); }
                    finally { polySem.Release(); }
                });
            await Task.WhenAll(polyTasks);

            var segVm = new Models.SegmentsViewModel
            {
                Segments = segmentMap.Values.OrderByDescending(s => s.AttemptCount).ThenBy(s => s.SegmentName).ToList(),
                RidesFetched = fetchCount,
                TotalRidesAvailable = rides.Count,
            };
            var beVm = new Models.BestEffortsViewModel
            {
                Rows = effortMap.Values.OrderBy(r => r.DistanceM).ToList(),
                RidesFetched = fetchCount,
            };

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
            var all = await _stravaService.GetAllActivitiesAsync();
            var rides = all.Where(a => a.SportType is "Ride" or "VirtualRide" or "GravelRide" or "MountainBikeRide").ToList();

            // Build a daily load map.
            // Training load proxy: (distance_km * avg_speed_factor) scaled to roughly 0–100 TSS equivalent.
            // Simple formula: load = (moving_time_hrs * avg_speed_kmh) / 2  — produces ~50 for a steady 2h ride at 25 km/h
            var dailyLoad = rides
                .GroupBy(a => a.StartDateLocal.Date)
                .ToDictionary(g => g.Key, g => g.Sum(a =>
                {
                    double hrs = a.MovingTime / 3600.0;
                    double spd = a.AverageSpeed * 3.6;
                    return hrs * spd / 2.0;
                }));

            // Compute CTL/ATL from oldest date through today using EMA
            var today = DateTime.Today;
            var startDate = today.AddDays(-days);
            // Seed from earlier history for accuracy
            var seedStart = rides.Any() ? rides.Min(a => a.StartDateLocal.Date) : startDate;

            double ctl = 0, atl = 0;
            const double ctlDecay = 1.0 / 42.0;
            const double atlDecay = 1.0 / 7.0;

            // Pre-seed CTL/ATL from all history before our display window
            for (var d = seedStart; d < startDate; d = d.AddDays(1))
            {
                double load = dailyLoad.GetValueOrDefault(d, 0);
                ctl = ctl + (load - ctl) * ctlDecay;
                atl = atl + (load - atl) * atlDecay;
            }

            var points = new List<Models.FitnessDayPoint>();
            for (var d = startDate; d <= today; d = d.AddDays(1))
            {
                double load = dailyLoad.GetValueOrDefault(d, 0);
                ctl = ctl + (load - ctl) * ctlDecay;
                atl = atl + (load - atl) * atlDecay;
                points.Add(new Models.FitnessDayPoint
                {
                    Date = d,
                    Load = Math.Round(load, 1),
                    Ctl = Math.Round(ctl, 1),
                    Atl = Math.Round(atl, 1),
                    Tsb = Math.Round(ctl - atl, 1),
                });
            }

            var last = points.LastOrDefault();
            double tsb = last?.Tsb ?? 0;
            string status = tsb > 5 ? "Fresh — good day to race or go hard"
                          : tsb > -10 ? "Neutral — normal training"
                          : tsb > -30 ? "Tired — accumulated fatigue"
                          : "Very fatigued — consider rest";

            var vm = new Models.FitnessViewModel
            {
                Points = points,
                DaysShown = days,
                CurrentCtl = last?.Ctl ?? 0,
                CurrentAtl = last?.Atl ?? 0,
                CurrentTsb = tsb,
                TsbStatus = status,
            };

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
            var all = await _stravaService.GetAllActivitiesAsync();
            var rides = all.Where(a => a.SportType is "Ride" or "VirtualRide" or "GravelRide" or "MountainBikeRide").ToList();

            var availableYears = rides.Select(a => a.StartDateLocal.Year).Distinct().OrderDescending().ToList();
            int selectedYear = year ?? DateTime.Now.Year;
            var yearRides = rides.Where(a => a.StartDateLocal.Year == selectedYear).ToList();

            // ── Ride type clustering ─────────────────────────────────────────
            // Rules are applied in priority order (most specific first)
            static Models.RideType Classify(Models.StravaActivity a)
            {
                double distKm = a.Distance / 1000.0;
                double speedKmh = a.AverageSpeed * 3.6;
                if (distKm >= 130) return Models.RideType.Epic;
                if (distKm >= 50 && speedKmh >= 34) return Models.RideType.Race;
                if (speedKmh >= 30 || (distKm >= 40 && speedKmh >= 28)) return Models.RideType.Tempo;
                if (distKm >= 40) return Models.RideType.Endurance;
                return Models.RideType.Recovery;
            }

            var classified = yearRides.Select(a => new Models.ClassifiedRide { Activity = a, RideType = Classify(a) })
                                      .OrderByDescending(r => r.Activity.StartDateLocal).ToList();

            var typeCounts = classified.GroupBy(r => r.RideType)
                                       .ToDictionary(g => g.Key, g => g.Count());

            // ── Speed zones ──────────────────────────────────────────────────
            var zones = new[]
            {
                new Models.SpeedZoneData { Label = "< 20 km/h",    MinKmh = 0,  MaxKmh = 20,  Color = "#6c757d" },
                new Models.SpeedZoneData { Label = "20–25 km/h",   MinKmh = 20, MaxKmh = 25,  Color = "#17a2b8" },
                new Models.SpeedZoneData { Label = "25–30 km/h",   MinKmh = 25, MaxKmh = 30,  Color = "#28a745" },
                new Models.SpeedZoneData { Label = "30–35 km/h",   MinKmh = 30, MaxKmh = 35,  Color = "#ffc107" },
                new Models.SpeedZoneData { Label = "35–40 km/h",   MinKmh = 35, MaxKmh = 40,  Color = "#fd7e14" },
                new Models.SpeedZoneData { Label = "> 40 km/h",    MinKmh = 40, MaxKmh = 999, Color = "#dc3545" },
            }.ToList();

            foreach (var r in yearRides)
            {
                double spd = r.AverageSpeed * 3.6;
                var zone = zones.FirstOrDefault(z => spd >= z.MinKmh && spd < z.MaxKmh);
                if (zone != null)
                {
                    zone.RideCount++;
                    zone.TotalDistanceKm += r.Distance / 1000.0;
                    zone.TotalTimeHrs += r.MovingTime / 3600.0;
                }
            }

            var vm = new Models.AnalysisViewModel
            {
                Year = selectedYear,
                AvailableYears = availableYears,
                ClassifiedRides = classified,
                TypeCounts = typeCounts,
                SpeedZones = zones,
            };

            return View(vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading analysis");
            ViewBag.Error = "Failed to load analysis data.";
            return View(new Models.AnalysisViewModel { Year = DateTime.Now.Year });
        }
    }

    public async Task<IActionResult> CumulativeDistance(string? type = null, int? year = null)
    {
        try
        {
            type ??= "Ride";
            var all = await _stravaService.GetAllActivitiesAsync();
            var activities = FilterByActivityType(all, type);

            var availableYears = activities.Select(a => a.StartDateLocal.Year).Distinct().OrderDescending().ToList();
            int selectedYear = year ?? DateTime.Now.Year;
            int priorYear = selectedYear - 1;

            // Build day-by-day cumulative for each year
            // Returns list of (dayOfYear 1..365, cumulKm) for that year up to the min of yearEnd and today
            List<(int day, double cumKm)> BuildCumulative(int y)
            {
                var yearActs = activities.Where(a => a.StartDateLocal.Year == y)
                    .OrderBy(a => a.StartDateLocal).ToList();
                var result = new List<(int, double)>();
                double cum = 0;
                var start = new DateTime(y, 1, 1);
                var maxDay = y == DateTime.Today.Year ? DateTime.Today : new DateTime(y, 12, 31);
                var byDate = yearActs.ToLookup(a => a.StartDateLocal.Date);
                for (var d = start; d <= maxDay; d = d.AddDays(1))
                {
                    cum += byDate[d].Sum(a => a.Distance / 1000.0);
                    int doy = (d - start).Days + 1;
                    result.Add((doy, Math.Round(cum, 1)));
                }
                return result;
            }

            var currentCum  = BuildCumulative(selectedYear);
            var priorCum    = BuildCumulative(priorYear);

            // Only return every 7th point to keep chart responsive
            static List<(int day, double cumKm)> Downsample(List<(int day, double cumKm)> pts) =>
                pts.Where((_, i) => i % 3 == 0 || i == pts.Count - 1).ToList();

            ViewBag.ActivityType = type;
            ViewBag.ActivityTypeLabel = ActivityTypeLabel(type);
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

    public async Task<IActionResult> NameAnalysis(string? type = null)
    {
        try
        {
            type ??= "All";
            var all = await _stravaService.GetAllActivitiesAsync();
            var activities = FilterByActivityType(all, type);

            // Word frequency from activity names
            var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "a","an","the","and","or","in","on","at","to","for","of","with","by","from","my","i",
                "morning","afternoon","evening","night","ride","run","walk","hike","workout",
                "-","–","—","&","/","\\",
            };

            var wordCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in activities)
            {
                if (string.IsNullOrWhiteSpace(a.Name)) continue;
                foreach (var word in a.Name.Split(new[] { ' ', ',', '.', '!', '?', '(', ')', '[', ']', '#' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var clean = word.Trim('-').ToLowerInvariant();
                    if (clean.Length < 2 || stopWords.Contains(clean) || int.TryParse(clean, out _)) continue;
                    wordCounts[clean] = wordCounts.GetValueOrDefault(clean, 0) + 1;
                }
            }

            var top50 = wordCounts.OrderByDescending(kv => kv.Value).Take(50)
                .Select(kv => (Word: kv.Key, Count: kv.Value)).ToList();

            ViewBag.ActivityType = type;
            ViewBag.ActivityTypeLabel = ActivityTypeLabel(type);
            ViewBag.Top50 = top50;
            ViewBag.TotalActivities = activities.Count;

            // Also top name starters (first word)
            var starters = activities
                .Where(a => !string.IsNullOrWhiteSpace(a.Name))
                .Select(a => a.Name.Split(' ')[0].Trim())
                .Where(w => w.Length > 1)
                .GroupBy(w => w, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => (Word: g.Key, Count: g.Count()))
                .ToList();
            ViewBag.TopStarters = starters;

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading name analysis");
            ViewBag.Error = "Failed to load name analysis.";
            return View();
        }
    }

    public async Task<IActionResult> Histogram(string? type = null)
    {
        try
        {
            type ??= "Ride";
            var all = await _stravaService.GetAllActivitiesAsync();
            var activities = FilterByActivityType(all, type).Where(a => a.Distance > 0).ToList();

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
                ActivityTypeLabel = ActivityTypeLabel(type),
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
            var activities = FilterByActivityType(all, type)
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
                ActivityTypeLabel = ActivityTypeLabel(type),
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

    public async Task<IActionResult> Timeline(string? type = null)
    {
        try
        {
            type ??= "Ride";
            var all = await _stravaService.GetAllActivitiesAsync();
            var activities = FilterByActivityType(all, type);

            var today = DateTime.Today;

            // Weekly: last 26 weeks (Mon–Sun buckets)
            var weekStart = today.AddDays(-(int)today.DayOfWeek - 7 * 25);  // Monday of 26 weeks ago
            if (weekStart.DayOfWeek != DayOfWeek.Monday)
                weekStart = weekStart.AddDays(-(int)weekStart.DayOfWeek + 1);

            var byDate = activities.ToLookup(a => a.StartDateLocal.Date);
            var weeks = new List<Models.TimelineWeekPoint>();
            for (int w = 0; w < 26; w++)
            {
                var ws = weekStart.AddDays(w * 7);
                var we = ws.AddDays(6);
                double dist = 0, hrs = 0; int cnt = 0;
                for (var d = ws; d <= we && d <= today; d = d.AddDays(1))
                {
                    foreach (var a in byDate[d]) { dist += a.Distance / 1000.0; hrs += a.MovingTime / 3600.0; cnt++; }
                }
                weeks.Add(new Models.TimelineWeekPoint
                {
                    WeekStart = ws,
                    Label = ws.ToString("MMM d"),
                    DistanceKm = Math.Round(dist, 1),
                    TimeHours = Math.Round(hrs, 1),
                    Count = cnt,
                });
            }

            // Monthly: last 18 months
            var months = new List<Models.TimelineMonthPoint>();
            for (int m = 17; m >= 0; m--)
            {
                var monthDate = today.AddMonths(-m);
                var bucket = activities.Where(a => a.StartDateLocal.Year == monthDate.Year && a.StartDateLocal.Month == monthDate.Month).ToList();
                months.Add(new Models.TimelineMonthPoint
                {
                    Year = monthDate.Year,
                    Month = monthDate.Month,
                    Label = monthDate.ToString("MMM yy"),
                    DistanceKm = Math.Round(bucket.Sum(a => a.Distance) / 1000.0, 1),
                    TimeHours = Math.Round(bucket.Sum(a => a.MovingTime) / 3600.0, 1),
                    Count = bucket.Count,
                });
            }

            var vm = new Models.TimelineViewModel
            {
                ActivityType = type,
                ActivityTypeLabel = ActivityTypeLabel(type),
                Weeks = weeks,
                Months = months,
            };

            return View(vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading timeline");
            ViewBag.Error = "Failed to load timeline data.";
            return View(new Models.TimelineViewModel());
        }
    }

    public async Task<IActionResult> WalkAnalytics(int? year = null)
    {
        try
        {
            var all = await _stravaService.GetAllActivitiesAsync();
            var walks = all.Where(a => a.SportType is "Walk" or "Hike" or "VirtualWalk").ToList();

            var availableYears = walks.Select(a => a.StartDateLocal.Year).Distinct().OrderDescending().ToList();
            int selectedYear = year ?? DateTime.Now.Year;
            var yearWalks = walks.Where(a => a.StartDateLocal.Year == selectedYear).OrderBy(a => a.StartDateLocal).ToList();

            if (!yearWalks.Any())
            {
                return View(new Models.WalkAnalyticsViewModel
                {
                    Year = selectedYear,
                    AvailableYears = availableYears,
                });
            }

            double totalDistKm = yearWalks.Sum(a => a.Distance) / 1000.0;
            double totalElevM  = yearWalks.Sum(a => (double)a.TotalElevationGain);
            int    totalSec    = yearWalks.Sum(a => a.MovingTime);

            // Pace: seconds per km (lower = faster)
            double avgPaceSecPerKm = totalDistKm > 0 ? totalSec / totalDistKm : 0;

            var walksWithDist = yearWalks.Where(a => a.Distance >= 3000).ToList();
            var fastestPace = walksWithDist.Any()
                ? walksWithDist.MinBy(a => a.MovingTime / (a.Distance / 1000.0))
                : null;

            var distByMonth = new double[13];
            foreach (var a in yearWalks)
                distByMonth[a.StartDateLocal.Month] += a.Distance / 1000.0;

            var byHour = new int[24];
            foreach (var a in yearWalks)
                byHour[a.StartDateLocal.Hour]++;

            var vm = new Models.WalkAnalyticsViewModel
            {
                Year             = selectedYear,
                AvailableYears   = availableYears,
                TotalDistanceKm  = Math.Round(totalDistKm, 1),
                TotalMovingTime  = TimeSpan.FromSeconds(totalSec),
                TotalElevationM  = Math.Round(totalElevM, 0),
                TotalWalks       = yearWalks.Count,
                AvgDistanceKm    = Math.Round(totalDistKm / yearWalks.Count, 1),
                AvgPaceSecPerKm  = Math.Round(avgPaceSecPerKm, 0),
                AvgElevationM    = Math.Round(totalElevM / yearWalks.Count, 0),
                LongestWalk      = yearWalks.MaxBy(a => a.Distance),
                FastestPaceWalk  = fastestPace,
                MostElevationWalk = yearWalks.MaxBy(a => a.TotalElevationGain),
                LongestTimeWalk  = yearWalks.MaxBy(a => a.MovingTime),
                ShortWalks       = yearWalks.Count(a => a.Distance < 5000),
                MediumWalks      = yearWalks.Count(a => a.Distance >= 5000 && a.Distance <= 15000),
                LongWalks        = yearWalks.Count(a => a.Distance > 15000),
                WalksByHour      = byHour,
                DistanceByMonth  = distByMonth,
                WalkCount        = yearWalks.Count(a => a.SportType is "Walk" or "VirtualWalk"),
                HikeCount        = yearWalks.Count(a => a.SportType == "Hike"),
                Top5ByDistance   = yearWalks.OrderByDescending(a => a.Distance).Take(5).ToList(),
            };

            // All-time walk streaks
            var allWalkDates = walks.Select(a => a.StartDateLocal.Date).Distinct().OrderBy(d => d).ToList();
            var walkDateSet  = allWalkDates.ToHashSet();
            int curStreak = 0;
            for (var chk = DateTime.Today; walkDateSet.Contains(chk); chk = chk.AddDays(-1)) curStreak++;

            int longStreak = 0, sLen = allWalkDates.Any() ? 1 : 0;
            DateTime sStart = allWalkDates.FirstOrDefault(), lStart = sStart, lEnd = sStart;
            for (int i = 1; i < allWalkDates.Count; i++)
            {
                if ((allWalkDates[i] - allWalkDates[i - 1]).Days == 1) sLen++;
                else { if (sLen > longStreak) { longStreak = sLen; lStart = sStart; lEnd = allWalkDates[i - 1]; } sLen = 1; sStart = allWalkDates[i]; }
            }
            if (sLen > longStreak) { longStreak = sLen; lStart = sStart; lEnd = allWalkDates.LastOrDefault(); }

            vm.CurrentStreakDays  = curStreak;
            vm.LongestStreakDays  = longStreak;
            vm.LongestStreakStart = lStart;
            vm.LongestStreakEnd   = lEnd;

            return View(vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading walk analytics");
            ViewBag.Error = "Failed to load walk analytics.";
            return View(new Models.WalkAnalyticsViewModel { Year = DateTime.Now.Year });
        }
    }

    public async Task<IActionResult> Heatmap(int? year = null)
    {
        try
        {
            var all = await _stravaService.GetAllActivitiesAsync();

            var availableYears = all.Select(a => a.StartDateLocal.Year).Distinct().OrderDescending().ToList();
            ViewBag.AvailableYears = availableYears;
            ViewBag.SelectedYear = year;  // null = all-time

            var withPolylines = (year == null
                ? all
                : all.Where(a => a.StartDateLocal.Year == year))
                .Where(a => a.Map?.SummaryPolyline != null)
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
