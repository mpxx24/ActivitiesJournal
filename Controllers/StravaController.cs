using ActivitiesJournal.Services;
using Microsoft.AspNetCore.Mvc;

namespace ActivitiesJournal.Controllers;

public class StravaController : Controller
{
    private readonly IStravaService _stravaService;
    private readonly ILogger<StravaController> _logger;

    public StravaController(IStravaService stravaService, ILogger<StravaController> logger)
    {
        _stravaService = stravaService;
        _logger = logger;
    }

    [HttpPost]
    public IActionResult ClearCache(string? returnUrl = null)
    {
        _stravaService.InvalidateCache();
        _logger.LogInformation("Cache cleared manually");
        return Redirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
    }

    public IActionResult Authorize()
    {
        var authUrl = _stravaService.GetAuthorizationUrl();
        return Redirect(authUrl);
    }

    public async Task<IActionResult> Callback(string code, string state)
    {
        if (string.IsNullOrEmpty(code))
        {
            ViewBag.Error = "Authorization failed. No code received from Strava.";
            return View();
        }

        try
        {
            // Exchange authorization code for tokens and update Strava configuration in memory
            await _stravaService.ExchangeCodeForTokenAsync(code);

            // After successful exchange, redirect to activities list
            return RedirectToAction("Index", "Activities");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Strava callback");
            ViewBag.Error = "Failed to complete authorization.";
            return View();
        }
    }
}
