using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ActivitiesJournal.Models;
using ActivitiesJournal.Services;

namespace ActivitiesJournal.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IStravaService _stravaService;

    public HomeController(ILogger<HomeController> logger, IStravaService stravaService)
    {
        _logger = logger;
        _stravaService = stravaService;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var all = await _stravaService.GetAllActivitiesAsync();
            var today = DateTime.Today;
            var thisWeekStart = today.AddDays(-(int)today.DayOfWeek + (today.DayOfWeek == DayOfWeek.Sunday ? -6 : 1));

            // Recent activities (last 5)
            var recent = all.OrderByDescending(a => a.StartDateLocal).Take(5).ToList();
            ViewBag.Recent = recent;

            // This week's rides
            var weekRides = all.Where(a =>
                a.StartDateLocal.Date >= thisWeekStart &&
                a.SportType is "Ride" or "VirtualRide" or "GravelRide" or "MountainBikeRide").ToList();
            ViewBag.WeekRideKm = weekRides.Sum(a => a.Distance / 1000.0);
            ViewBag.WeekRideCount = weekRides.Count;

            // This week's walks
            var weekWalks = all.Where(a =>
                a.StartDateLocal.Date >= thisWeekStart &&
                a.SportType is "Walk" or "Hike" or "VirtualWalk").ToList();
            ViewBag.WeekWalkKm = weekWalks.Sum(a => a.Distance / 1000.0);
            ViewBag.WeekWalkCount = weekWalks.Count;

            // Current ride streak (consecutive days with a ride activity)
            var rideDates = all
                .Where(a => a.SportType is "Ride" or "VirtualRide" or "GravelRide" or "MountainBikeRide")
                .Select(a => a.StartDateLocal.Date).Distinct().OrderDescending().ToList();
            int rideStreak = 0;
            var check = today;
            foreach (var d in rideDates)
            {
                if (d == check || d == check.AddDays(-1)) { rideStreak++; check = d; }
                else break;
            }
            ViewBag.RideStreak = rideStreak;

            // YTD totals
            var ytdRides = all.Where(a => a.StartDateLocal.Year == today.Year &&
                a.SportType is "Ride" or "VirtualRide" or "GravelRide" or "MountainBikeRide").ToList();
            ViewBag.YtdRideKm = ytdRides.Sum(a => a.Distance / 1000.0);
            ViewBag.YtdRideCount = ytdRides.Count;

            var ytdWalks = all.Where(a => a.StartDateLocal.Year == today.Year &&
                a.SportType is "Walk" or "Hike" or "VirtualWalk").ToList();
            ViewBag.YtdWalkKm = ytdWalks.Sum(a => a.Distance / 1000.0);
            ViewBag.YtdWalkCount = ytdWalks.Count;

            // Last 8 weeks activity summary (for mini sparkline)
            var weeks = Enumerable.Range(0, 8).Select(w =>
            {
                var ws = today.AddDays(-w * 7 - (int)today.DayOfWeek + (today.DayOfWeek == DayOfWeek.Sunday ? -6 : 1));
                var we = ws.AddDays(6);
                return all.Where(a => a.StartDateLocal.Date >= ws && a.StartDateLocal.Date <= we).Sum(a => a.Distance / 1000.0);
            }).Reverse().ToList();
            ViewBag.WeeklyKm = weeks;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load dashboard data");
            ViewBag.Recent = new List<StravaActivity>();
        }

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
