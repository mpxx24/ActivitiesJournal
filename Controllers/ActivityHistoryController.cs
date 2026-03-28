using ActivitiesJournal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActivitiesJournal.Controllers;

[Authorize]
[Route("Activities/[action]")]
public class ActivityHistoryController : Controller
{
    private readonly IStravaService _stravaService;
    private readonly ILogger<ActivityHistoryController> _logger;

    public ActivityHistoryController(IStravaService stravaService, ILogger<ActivityHistoryController> logger)
    {
        _stravaService = stravaService;
        _logger = logger;
    }

    public async Task<IActionResult> Calendar(int? year = null, string? type = null)
    {
        try
        {
            type ??= "Ride";
            var all = await _stravaService.GetAllActivitiesAsync();
            var rides = SportTypes.FilterByType(all, type);
            ViewBag.ActivityType = type;
            ViewBag.ActivityTypeLabel = SportTypes.TypeLabel(type);

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
            var rides = SportTypes.FilterByType(all, type);
            ViewBag.ActivityType = type;
            ViewBag.ActivityTypeLabel = SportTypes.TypeLabel(type);

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
            var rides = SportTypes.FilterByType(all, type);
            ViewBag.ActivityType = type;
            ViewBag.ActivityTypeLabel = SportTypes.TypeLabel(type);

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

    public async Task<IActionResult> Timeline(string? type = null)
    {
        try
        {
            type ??= "Ride";
            var all = await _stravaService.GetAllActivitiesAsync();
            var activities = SportTypes.FilterByType(all, type);

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
                ActivityTypeLabel = SportTypes.TypeLabel(type),
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
            var walks = all.Where(a => SportTypes.IsWalk(a.SportType)).ToList();

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

    public async Task<IActionResult> ActiveMinutes(int weeks = 26)
    {
        try
        {
            var all = await _stravaService.GetAllActivitiesAsync();
            var today = DateTime.Today;
            const int whoTarget = 150; // minutes/week

            // Build weekly buckets going back `weeks` weeks
            var weekData = Enumerable.Range(0, weeks).Select(w =>
            {
                var weekStart = today.AddDays(-w * 7 - (int)today.DayOfWeek + (today.DayOfWeek == DayOfWeek.Sunday ? -6 : 1));
                var weekEnd = weekStart.AddDays(6);
                var acts = all.Where(a => a.StartDateLocal.Date >= weekStart && a.StartDateLocal.Date <= weekEnd).ToList();
                int rideMin = acts.Where(a => SportTypes.IsRide(a.SportType)).Sum(a => a.MovingTime / 60);
                int walkMin = acts.Where(a => SportTypes.IsWalk(a.SportType)).Sum(a => a.MovingTime / 60);
                int otherMin = acts.Where(a => !SportTypes.IsRide(a.SportType) && !SportTypes.IsWalk(a.SportType)).Sum(a => a.MovingTime / 60);
                return new { WeekStart = weekStart, Label = weekStart.ToString("MMM d"), RideMin = rideMin, WalkMin = walkMin, OtherMin = otherMin, TotalMin = rideMin + walkMin + otherMin };
            }).Reverse().ToList();

            // Average of last 8 full weeks (excluding current partial week)
            var completedWeeks = weekData.SkipLast(1).TakeLast(8).ToList();
            double avgMin = completedWeeks.Any() ? completedWeeks.Average(w => w.TotalMin) : 0;

            // Current week
            var currentWeek = weekData.Last();
            int weeksAboveTarget = weekData.Count(w => w.TotalMin >= whoTarget);

            ViewBag.Weeks = weekData;
            ViewBag.WhoTarget = whoTarget;
            ViewBag.AvgMin = (int)avgMin;
            ViewBag.CurrentMin = currentWeek.TotalMin;
            ViewBag.WeeksAboveTarget = weeksAboveTarget;
            ViewBag.TotalWeeks = weeks;
            ViewBag.CompliancePct = weeks > 0 ? (int)(weeksAboveTarget * 100.0 / weeks) : 0;

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading active minutes");
            ViewBag.Error = "Failed to load active minutes data.";
            return View();
        }
    }

    public async Task<IActionResult> NameAnalysis(string? type = null)
    {
        try
        {
            type ??= "All";
            var all = await _stravaService.GetAllActivitiesAsync();
            var activities = SportTypes.FilterByType(all, type);

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
            ViewBag.ActivityTypeLabel = SportTypes.TypeLabel(type);
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

    public async Task<IActionResult> ThisTimeLastYear(string? type = null)
    {
        try
        {
            type ??= "Ride";
            var all = await _stravaService.GetAllActivitiesAsync();
            var activities = SportTypes.FilterByType(all, type);

            var today = DateTime.Today;
            int thisYear = today.Year;
            int lastYear = thisYear - 1;
            // Same calendar date but previous year — include all up to that date
            var cutoffLastYear = new DateTime(lastYear, today.Month, today.Day);

            var ytd = activities.Where(a => a.StartDateLocal.Year == thisYear).ToList();
            var ytdLast = activities.Where(a => a.StartDateLocal.Year == lastYear && a.StartDateLocal.Date <= cutoffLastYear).ToList();

            double KmSum(List<Models.StravaActivity> acts) => acts.Sum(a => a.Distance / 1000.0);
            double ElevSum(List<Models.StravaActivity> acts) => acts.Sum(a => (double)a.TotalElevationGain);
            double HoursSum(List<Models.StravaActivity> acts) => acts.Sum(a => a.MovingTime / 3600.0);

            // Weekly distance for last 12 weeks (both years aligned by week-of-year offset)
            var weeklyThis = new List<(string label, double km)>();
            var weeklyLast = new List<(string label, double km)>();
            for (int w = 11; w >= 0; w--)
            {
                var weekStart = today.AddDays(-w * 7 - (int)today.DayOfWeek);
                var weekEnd = weekStart.AddDays(6);
                var label = weekStart.ToString("MMM d");
                weeklyThis.Add((label, activities.Where(a => a.StartDateLocal.Date >= weekStart && a.StartDateLocal.Date <= weekEnd && a.StartDateLocal.Year == thisYear).Sum(a => a.Distance / 1000.0)));
                var lws = weekStart.AddYears(-1); var lwe = weekEnd.AddYears(-1);
                weeklyLast.Add((label, activities.Where(a => a.StartDateLocal.Date >= lws && a.StartDateLocal.Date <= lwe).Sum(a => a.Distance / 1000.0)));
            }

            ViewBag.ActivityType = type;
            ViewBag.IsWalk = type == "Walk";
            ViewBag.ThisYear = thisYear;
            ViewBag.LastYear = lastYear;
            ViewBag.Today = today.ToString("d MMMM");
            ViewBag.KmThis = KmSum(ytd); ViewBag.KmLast = KmSum(ytdLast);
            ViewBag.CountThis = ytd.Count; ViewBag.CountLast = ytdLast.Count;
            ViewBag.ElevThis = ElevSum(ytd); ViewBag.ElevLast = ElevSum(ytdLast);
            ViewBag.HoursThis = HoursSum(ytd); ViewBag.HoursLast = HoursSum(ytdLast);
            ViewBag.WeeklyThis = weeklyThis;
            ViewBag.WeeklyLast = weeklyLast;

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading this time last year");
            ViewBag.Error = "Failed to load data.";
            return View();
        }
    }
}
