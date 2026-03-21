using ActivitiesJournal.Filters;
using ActivitiesJournal.Models;
using ActivitiesJournal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActivitiesJournal.Controllers;

[Authorize]
public class TracksController : Controller
{
    private readonly ITrackStorageService _trackStorage;
    private readonly ITrackParserService _trackParser;
    private readonly ILogger<TracksController> _logger;

    public TracksController(
        ITrackStorageService trackStorage,
        ITrackParserService trackParser,
        ILogger<TracksController> logger)
    {
        _trackStorage = trackStorage;
        _trackParser = trackParser;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var tracks = await _trackStorage.ListTracksAsync(ct);
        return View(tracks);
    }

    public async Task<IActionResult> Detail(string id, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(id))
            return BadRequest();

        var summary = await _trackStorage.GetTrackSummaryAsync(id, ct);
        if (summary == null)
            return NotFound();

        return View(summary);
    }

    [HttpGet]
    public async Task<IActionResult> Points(string id, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(id))
            return BadRequest();

        try
        {
            var gpxStream = await _trackStorage.GetTrackGpxAsync(id, ct);
            var parsed = _trackParser.Parse(gpxStream);
            return Json(parsed.Points.Select(p => new { lat = p.Latitude, lon = p.Longitude, ele = p.Elevation }));
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return NotFound();
        }
    }

    [HttpPost]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(id))
            return BadRequest();

        await _trackStorage.DeleteTrackAsync(id, ct);
        return RedirectToAction("Index");
    }

    [AllowAnonymous]
    [TypeFilter(typeof(RequireApiKeyFilter))]
    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile gpxFile, ActivityType activityType, CancellationToken ct)
    {
        if (gpxFile == null || gpxFile.Length == 0)
            return BadRequest(new { error = "No GPX file provided." });

        using var buffer = new MemoryStream();
        await gpxFile.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        TrackParseResult parsed;
        try
        {
            parsed = _trackParser.Parse(buffer);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse uploaded GPX file");
            return BadRequest(new { error = "Invalid GPX file." });
        }

        if (parsed.PointCount == 0)
            return BadRequest(new { error = "GPX file contains no track points." });

        var summary = new TrackSummary
        {
            Id = Guid.NewGuid().ToString(),
            ActivityType = activityType,
            StartedAt = parsed.StartedAt,
            DistanceKm = parsed.DistanceKm,
            Duration = parsed.Duration,
            PointCount = parsed.PointCount
        };

        buffer.Position = 0;
        await _trackStorage.UploadTrackAsync(buffer, summary, ct);

        return Json(summary);
    }
}
