using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ActivitiesJournal.Configuration;
using ActivitiesJournal.Filters;
using ActivitiesJournal.Models;
using ActivitiesJournal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ActivitiesJournal.Controllers;

[Authorize]
public class RoutePlannerController : Controller
{
    private readonly IRoutePlannerService _routePlanner;
    private readonly TrackOwnerOptions _ownerOptions;
    private readonly ILogger<RoutePlannerController> _logger;

    public RoutePlannerController(
        IRoutePlannerService routePlanner,
        IOptions<TrackOwnerOptions> ownerOptions,
        ILogger<RoutePlannerController> logger)
    {
        _routePlanner = routePlanner;
        _ownerOptions = ownerOptions.Value;
        _logger = logger;
    }

    private long GetAthleteId() =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var routes = await _routePlanner.ListRoutesAsync(GetAthleteId(), ct);
        return View(new RoutePlannerIndexViewModel { Routes = routes });
    }

    public async Task<IActionResult> Detail(string id, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(id))
            return BadRequest();

        var route = await _routePlanner.GetRouteAsync(GetAthleteId(), id, ct);
        if (route == null)
            return NotFound();

        return View(new RoutePlannerDetailViewModel { Route = route });
    }

    [HttpGet]
    public async Task<IActionResult> Points(string id, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(id))
            return BadRequest();

        var route = await _routePlanner.GetRouteAsync(GetAthleteId(), id, ct);
        if (route == null)
            return NotFound();

        return Json(route.Waypoints.Select(w => new { lat = w.Lat, lon = w.Lon }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(string name, string waypointsJson, string? existingId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Route name is required.";
            return RedirectToAction("Index");
        }

        List<WaypointDto>? waypoints;
        try
        {
            waypoints = JsonSerializer.Deserialize<List<WaypointDto>>(waypointsJson ?? "[]");
        }
        catch
        {
            TempData["Error"] = "Invalid waypoint data.";
            return RedirectToAction("Index");
        }

        if (waypoints == null || waypoints.Count < 2)
        {
            TempData["Error"] = "A route needs at least 2 waypoints.";
            return RedirectToAction("Index");
        }

        await _routePlanner.SaveRouteAsync(GetAthleteId(), name.Trim(), waypoints, existingId, ct);
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(id))
            return BadRequest();

        await _routePlanner.DeleteRouteAsync(GetAthleteId(), id, ct);
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> Download(string id, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(id))
            return BadRequest();

        var route = await _routePlanner.GetRouteAsync(GetAthleteId(), id, ct);
        if (route == null)
            return NotFound();

        var gpx = BuildGpx(route);
        var bytes = Encoding.UTF8.GetBytes(gpx);
        var fileName = $"{SanitizeFileName(route.Name)}.gpx";
        return File(bytes, "application/gpx+xml", fileName);
    }

    [AllowAnonymous]
    [TypeFilter(typeof(RequireApiKeyFilter))]
    [HttpGet]
    public async Task<IActionResult> ApiList(CancellationToken ct)
    {
        var routes = await _routePlanner.ListRoutesAsync(_ownerOptions.OwnerAthleteId, ct);
        return Json(routes.Select(r => new
        {
            r.Id,
            r.Name,
            r.DistanceKm,
            r.CreatedAt,
            waypointCount = r.Waypoints.Count
        }));
    }

    [AllowAnonymous]
    [TypeFilter(typeof(RequireApiKeyFilter))]
    [HttpGet]
    public async Task<IActionResult> ApiPoints(string id, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(id))
            return BadRequest();

        var route = await _routePlanner.GetRouteAsync(_ownerOptions.OwnerAthleteId, id, ct);
        if (route == null)
            return NotFound();

        return Json(route.Waypoints.Select(w => new { lat = w.Lat, lon = w.Lon }));
    }

    private static string BuildGpx(PlannedRoute route)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<gpx version=\"1.1\" creator=\"ActivitiesJournal\" xmlns=\"http://www.topografix.com/GPX/1/1\">");
        sb.AppendLine($"  <metadata><name>{EscapeXml(route.Name)}</name></metadata>");
        sb.AppendLine("  <rte>");
        sb.AppendLine($"    <name>{EscapeXml(route.Name)}</name>");
        foreach (var w in route.Waypoints)
            sb.AppendLine($"    <rtept lat=\"{w.Lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}\" lon=\"{w.Lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}\"/>");
        sb.AppendLine("  </rte>");
        sb.AppendLine("</gpx>");
        return sb.ToString();
    }

    private static string EscapeXml(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static string SanitizeFileName(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
