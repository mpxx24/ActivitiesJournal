using System.Security.Claims;
using ActivitiesJournal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace ActivitiesJournal.Controllers;

[Authorize]
public class ActivitiesController : Controller
{
    private readonly IStravaService _stravaService;
    private readonly ITrackStorageService _trackStorage;
    private readonly ITrackParserService _trackParser;
    private readonly ILogger<ActivitiesController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _memoryCache;

    public ActivitiesController(IStravaService stravaService, ITrackStorageService trackStorage,
        ITrackParserService trackParser, ILogger<ActivitiesController> logger,
        IHttpClientFactory httpClientFactory, Microsoft.Extensions.Caching.Memory.IMemoryCache memoryCache)
    {
        _stravaService = stravaService;
        _trackStorage = trackStorage;
        _trackParser = trackParser;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _memoryCache = memoryCache;
    }

    private long GetAthleteId() =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public async Task<IActionResult> Index(int page = 1, int perPage = 30,
        string? q = null, string? sport = null,
        DateTime? dateFrom = null, DateTime? dateTo = null,
        double? minKm = null, double? maxKm = null)
    {
        try
        {
            var all = await _stravaService.GetAllActivitiesAsync();
            var filtered = all.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(q))
                filtered = filtered.Where(a => a.Name.Contains(q, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(sport))
                filtered = sport switch {
                    "Ride" => filtered.Where(a => SportTypes.IsRide(a.SportType)),
                    "Walk" => filtered.Where(a => SportTypes.IsWalk(a.SportType)),
                    _ => filtered.Where(a => a.SportType == sport)
                };

            if (dateFrom.HasValue) filtered = filtered.Where(a => a.StartDateLocal.Date >= dateFrom.Value.Date);
            if (dateTo.HasValue)   filtered = filtered.Where(a => a.StartDateLocal.Date <= dateTo.Value.Date);
            if (minKm.HasValue)    filtered = filtered.Where(a => a.Distance / 1000.0 >= minKm.Value);
            if (maxKm.HasValue)    filtered = filtered.Where(a => a.Distance / 1000.0 <= maxKm.Value);

            var list = filtered.OrderByDescending(a => a.StartDateLocal).ToList();
            int totalCount = list.Count;
            var paged = list.Skip((page - 1) * perPage).Take(perPage).ToList();

            ViewBag.CurrentPage = page;
            ViewBag.PerPage = perPage;
            ViewBag.HasMore = page * perPage < totalCount;
            ViewBag.TotalCount = totalCount;
            ViewBag.Q = q;
            ViewBag.Sport = sport;
            ViewBag.DateFrom = dateFrom?.ToString("yyyy-MM-dd");
            ViewBag.DateTo = dateTo?.ToString("yyyy-MM-dd");
            ViewBag.MinKm = minKm;
            ViewBag.MaxKm = maxKm;
            ViewBag.IsFiltered = !string.IsNullOrWhiteSpace(q) || !string.IsNullOrWhiteSpace(sport)
                || dateFrom.HasValue || dateTo.HasValue || minKm.HasValue || maxKm.HasValue;

            return View(paged);
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

            // Fetch weather data if activity has coordinates and is old enough for archive API
            if (activity.StartLatlng?.Count >= 2 && activity.StartDateLocal < DateTime.Now.AddDays(-2))
            {
                try
                {
                    await FetchWeatherAsync(activity);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Weather fetch failed for activity {Id}", id);
                }
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

    public async Task<IActionResult> Compare(long? stravaId, string? trackId, CancellationToken ct)
    {
        if (stravaId == null || string.IsNullOrEmpty(trackId))
            return View(new Models.ActivityCompareViewModel());

        try
        {
            var activity = await _stravaService.GetActivityByIdAsync(stravaId.Value);
            if (activity == null)
            {
                ViewBag.Error = $"Strava activity {stravaId} not found.";
                return View(new Models.ActivityCompareViewModel());
            }

            var summary = await _trackStorage.GetTrackSummaryAsync(GetAthleteId(), trackId, ct);
            if (summary == null)
            {
                ViewBag.Error = $"Track {trackId} not found.";
                return View(new Models.ActivityCompareViewModel());
            }

            IReadOnlyList<Models.GpxPoint> points = Array.Empty<Models.GpxPoint>();
            try
            {
                var gpxStream = await _trackStorage.GetTrackGpxAsync(GetAthleteId(), trackId, ct);
                var parsed = _trackParser.Parse(gpxStream);
                points = parsed.Points;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load GPX points for track {TrackId}", trackId);
            }

            return View(new Models.ActivityCompareViewModel
            {
                Strava = activity,
                Track = summary,
                TrackPoints = points
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading comparison stravaId={StravaId} trackId={TrackId}", stravaId, trackId);
            ViewBag.Error = "Failed to load comparison data.";
            return View(new Models.ActivityCompareViewModel());
        }
    }

    public async Task<IActionResult> WeatherInsights(string? type = null, int limit = 100)
    {
        try
        {
            type ??= "All";
            var all = await _stravaService.GetAllActivitiesAsync();
            var activities = SportTypes.FilterByType(all, type)
                .Where(a => a.StartLatlng?.Count >= 2 && a.StartDateLocal < DateTime.Now.AddDays(-2))
                .OrderByDescending(a => a.StartDateLocal)
                .Take(limit)
                .ToList();

            ViewBag.ActivityType = type;
            ViewBag.ActivityTypeLabel = SportTypes.TypeLabel(type);

            if (!activities.Any())
            {
                ViewBag.WeatherData = new List<(Models.StravaActivity, double temp, double wind, double precip, string desc)>();
                return View();
            }

            // Fetch weather for each activity in parallel with cache
            var sem = new SemaphoreSlim(5, 5);
            var results = await Task.WhenAll(activities.Select(async a =>
            {
                await sem.WaitAsync();
                try
                {
                    var cacheKey = $"weather_{a.StartDateLocal:yyyyMMdd}_{a.StartLatlng![0]:0.0}_{a.StartLatlng[1]:0.0}";
                    if (_memoryCache.TryGetValue<(double t, double w, double p, int code)>(cacheKey, out var cached))
                        return (a, cached.t, cached.w, cached.p, WmoCodeToDesc(cached.code), true);

                    var lat = a.StartLatlng![0].ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture);
                    var lon = a.StartLatlng[1].ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture);
                    var date = a.StartDateLocal.ToString("yyyy-MM-dd");
                    var url = $"v1/archive?latitude={lat}&longitude={lon}&start_date={date}&end_date={date}&hourly=temperature_2m,precipitation,windspeed_10m,weathercode&timezone=auto";

                    var client = _httpClientFactory.CreateClient("weather");
                    var resp = await client.GetAsync(url);
                    if (!resp.IsSuccessStatusCode) return (a, 0d, 0d, 0d, "Unknown", false);

                    var json = System.Text.Json.JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                    var h = json.RootElement.GetProperty("hourly");
                    int idx = Math.Min(a.StartDateLocal.Hour, 23);
                    double temp  = h.GetProperty("temperature_2m").EnumerateArray().ElementAtOrDefault(idx).GetDouble();
                    double wind  = h.GetProperty("windspeed_10m").EnumerateArray().ElementAtOrDefault(idx).GetDouble();
                    double prec  = h.GetProperty("precipitation").EnumerateArray().ElementAtOrDefault(idx).GetDouble();
                    int code     = h.GetProperty("weathercode").EnumerateArray().ElementAtOrDefault(idx).GetInt32();

                    _memoryCache.Set<(double, double, double, int)>(cacheKey, (temp, wind, prec, code), TimeSpan.FromDays(30));
                    return (a, temp, wind, prec, WmoCodeToDesc(code), true);
                }
                catch { return (a, 0d, 0d, 0d, "Unknown", false); }
                finally { sem.Release(); }
            }));

            var data = results.Where(r => r.Item6).ToList();

            // Group by temp range
            var tempGroups = new[] { (-20, 0, "< 0°C"), (0, 5, "0–5°C"), (5, 10, "5–10°C"),
                (10, 15, "10–15°C"), (15, 20, "15–20°C"), (20, 25, "20–25°C"), (25, 40, "> 25°C") };
            // Tuple layout: (activity, temp, wind, prec, desc, ok) = (Item1..Item6)
            var byTemp = tempGroups.Select(g =>
            {
                var bucket = data.Where(r => r.Item2 >= g.Item1 && r.Item2 < g.Item2).ToList();
                bool isWalk = type == "Walk";
                double avgVal = bucket.Any()
                    ? (isWalk ? bucket.Average(r => r.Item1.Distance > 0 ? r.Item1.MovingTime / (r.Item1.Distance / 1000.0) / 60.0 : 0)
                               : bucket.Average(r => r.Item1.AverageSpeed * 3.6))
                    : 0;
                return (Label: g.Item3, Count: bucket.Count, AvgValue: Math.Round(avgVal, 1));
            }).Where(g => g.Count > 0).ToList();

            // Dry vs wet (Item4 = precip)
            var dryCount = data.Count(r => r.Item4 < 0.5);
            var wetCount  = data.Count(r => r.Item4 >= 0.5);
            double dryAvgSpeed = data.Where(r => r.Item4 < 0.5 && r.Item1.AverageSpeed > 0).DefaultIfEmpty().Average(r => r == default ? 0 : r.Item1.AverageSpeed * 3.6);
            double wetAvgSpeed = data.Where(r => r.Item4 >= 0.5 && r.Item1.AverageSpeed > 0).DefaultIfEmpty().Average(r => r == default ? 0 : r.Item1.AverageSpeed * 3.6);

            // Best conditions: temp range with best avg performance (min 3 activities)
            bool isWalkMode = type == "Walk";
            var bestTempGroup = byTemp.Where(g => g.Count >= 3)
                .OrderBy(g => isWalkMode ? g.AvgValue : -g.AvgValue)  // lower pace = better; higher speed = better
                .FirstOrDefault();
            // Most frequent temp range
            var mostFreqTemp = byTemp.OrderByDescending(g => g.Count).FirstOrDefault();

            ViewBag.ActivityCount = activities.Count;
            ViewBag.FetchedCount = data.Count;
            ViewBag.ByTemp = byTemp;
            ViewBag.DryCount = dryCount;
            ViewBag.WetCount = wetCount;
            ViewBag.DryAvgSpeed = Math.Round(dryAvgSpeed, 1);
            ViewBag.WetAvgSpeed = Math.Round(wetAvgSpeed, 1);
            ViewBag.IsWalk = isWalkMode;
            ViewBag.Limit = limit;
            ViewBag.BestTempLabel = bestTempGroup.Label;
            ViewBag.BestTempValue = bestTempGroup.AvgValue;
            ViewBag.BestTempCount = bestTempGroup.Count;
            ViewBag.MostFreqTempLabel = mostFreqTemp.Label;
            ViewBag.MostFreqTempCount = mostFreqTemp.Count;
            ViewBag.PrefersDry = dryCount > wetCount * 2;

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading weather insights");
            ViewBag.Error = "Failed to load weather insights.";
            return View();
        }
    }

    private async Task FetchWeatherAsync(Models.StravaActivity activity)
    {
        var lat = activity.StartLatlng![0].ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture);
        var lon = activity.StartLatlng[1].ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture);
        var date = activity.StartDateLocal.ToString("yyyy-MM-dd");
        var url = $"v1/archive?latitude={lat}&longitude={lon}&start_date={date}&end_date={date}&hourly=temperature_2m,precipitation,windspeed_10m,weathercode&timezone=auto";

        var client = _httpClientFactory.CreateClient("weather");
        var resp = await client.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return;

        var json = System.Text.Json.JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var hourly = json.RootElement.GetProperty("hourly");
        var times  = hourly.GetProperty("time").EnumerateArray().Select(e => e.GetString()).ToList();
        var temps  = hourly.GetProperty("temperature_2m").EnumerateArray().Select(e => e.GetDouble()).ToList();
        var precip = hourly.GetProperty("precipitation").EnumerateArray().Select(e => e.GetDouble()).ToList();
        var wind   = hourly.GetProperty("windspeed_10m").EnumerateArray().Select(e => e.GetDouble()).ToList();
        var codes  = hourly.GetProperty("weathercode").EnumerateArray().Select(e => e.GetInt32()).ToList();

        // Find the hour closest to activity start
        var actHour = activity.StartDateLocal.Hour;
        int idx = Math.Min(actHour, times.Count - 1);

        ViewBag.WeatherTemp    = temps.Count > idx ? Math.Round(temps[idx], 1) : (double?)null;
        ViewBag.WeatherPrecip  = precip.Count > idx ? Math.Round(precip[idx], 1) : (double?)null;
        ViewBag.WeatherWind    = wind.Count > idx ? Math.Round(wind[idx], 1) : (double?)null;
        ViewBag.WeatherCode    = codes.Count > idx ? codes[idx] : (int?)null;
        ViewBag.WeatherDesc    = WmoCodeToDesc(codes.Count > idx ? codes[idx] : 0);
        ViewBag.WeatherIcon    = WmoCodeToIcon(codes.Count > idx ? codes[idx] : 0);
    }

    private static string WmoCodeToDesc(int code) => code switch
    {
        0 => "Clear sky", 1 or 2 or 3 => "Partly cloudy",
        45 or 48 => "Foggy",
        51 or 53 or 55 => "Drizzle",
        61 or 63 or 65 => "Rain",
        71 or 73 or 75 => "Snow",
        80 or 81 or 82 => "Rain showers",
        95 => "Thunderstorm",
        _ => "Cloudy"
    };

    private static string WmoCodeToIcon(int code) => code switch
    {
        0 => "bi-sun-fill text-warning",
        1 or 2 or 3 => "bi-cloud-sun text-warning",
        45 or 48 => "bi-cloud-fog2 text-secondary",
        51 or 53 or 55 => "bi-cloud-drizzle text-info",
        61 or 63 or 65 or 80 or 81 or 82 => "bi-cloud-rain text-info",
        71 or 73 or 75 => "bi-snow text-info",
        95 => "bi-cloud-lightning-rain text-warning",
        _ => "bi-clouds text-secondary"
    };
}
