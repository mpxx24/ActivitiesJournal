using ActivitiesJournal.Models;

namespace ActivitiesJournal.Services;

public class DashboardService : IDashboardService
{
    private readonly IStravaService _stravaService;

    public DashboardService(IStravaService stravaService)
    {
        _stravaService = stravaService;
    }

    public async Task<DashboardViewModel> BuildDashboardAsync()
    {
        var all = await _stravaService.GetAllActivitiesAsync();
        var today = DateTime.Today;
        var thisWeekStart = GetMondayOfWeek(today);

        var recent = all.OrderByDescending(a => a.StartDateLocal).Take(5).ToList();

        var weekRides = all.Where(a =>
            a.StartDateLocal.Date >= thisWeekStart && SportTypes.IsRide(a.SportType)).ToList();

        var weekWalks = all.Where(a =>
            a.StartDateLocal.Date >= thisWeekStart && SportTypes.IsWalk(a.SportType)).ToList();

        int rideStreak = ComputeCurrentStreak(all, today);

        var ytdRides = all.Where(a =>
            a.StartDateLocal.Year == today.Year && SportTypes.IsRide(a.SportType)).ToList();

        var ytdWalks = all.Where(a =>
            a.StartDateLocal.Year == today.Year && SportTypes.IsWalk(a.SportType)).ToList();

        var weeklyKm = BuildWeeklySummary(all, today);

        return new DashboardViewModel
        {
            RecentActivities = recent,
            WeekRideKm = weekRides.Sum(a => a.Distance / 1000.0),
            WeekRideCount = weekRides.Count,
            WeekWalkKm = weekWalks.Sum(a => a.Distance / 1000.0),
            WeekWalkCount = weekWalks.Count,
            RideStreak = rideStreak,
            YtdRideKm = ytdRides.Sum(a => a.Distance / 1000.0),
            YtdRideCount = ytdRides.Count,
            YtdWalkKm = ytdWalks.Sum(a => a.Distance / 1000.0),
            YtdWalkCount = ytdWalks.Count,
            WeeklyKm = weeklyKm,
        };
    }

    internal static DateTime GetMondayOfWeek(DateTime date)
    {
        return date.AddDays(-(int)date.DayOfWeek + (date.DayOfWeek == DayOfWeek.Sunday ? -6 : 1));
    }

    internal static int ComputeCurrentStreak(List<StravaActivity> all, DateTime today)
    {
        var rideDates = all
            .Where(a => SportTypes.IsRide(a.SportType))
            .Select(a => a.StartDateLocal.Date)
            .Distinct()
            .OrderDescending()
            .ToList();

        int streak = 0;
        var check = today;
        foreach (var d in rideDates)
        {
            if (d == check || d == check.AddDays(-1))
            {
                streak++;
                check = d;
            }
            else break;
        }
        return streak;
    }

    internal static List<double> BuildWeeklySummary(List<StravaActivity> all, DateTime today)
    {
        var mondayOfWeek = GetMondayOfWeek(today);
        return Enumerable.Range(0, 8).Select(w =>
        {
            var ws = mondayOfWeek.AddDays(-w * 7);
            var we = ws.AddDays(6);
            return all.Where(a => a.StartDateLocal.Date >= ws && a.StartDateLocal.Date <= we)
                      .Sum(a => a.Distance / 1000.0);
        }).Reverse().ToList();
    }
}
