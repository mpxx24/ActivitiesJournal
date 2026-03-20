using System.Security.Claims;
using ActivitiesJournal.Configuration;
using ActivitiesJournal.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ActivitiesJournal.Controllers;

public class StravaController : Controller
{
    private readonly IStravaService _stravaService;
    private readonly TrackOwnerOptions _ownerOptions;
    private readonly ILogger<StravaController> _logger;

    public StravaController(
        IStravaService stravaService,
        IOptions<TrackOwnerOptions> ownerOptions,
        ILogger<StravaController> logger)
    {
        _stravaService = stravaService;
        _ownerOptions = ownerOptions.Value;
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
            var athleteId = await _stravaService.ExchangeCodeForTokenAsync(code);

            if (_ownerOptions.OwnerAthleteId != 0 && athleteId == _ownerOptions.OwnerAthleteId)
            {
                var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, athleteId.ToString()) };
                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(identity),
                    new AuthenticationProperties { IsPersistent = true });
                _logger.LogInformation("Owner signed in via Strava OAuth. AthleteId: {AthleteId}", athleteId);
            }

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
