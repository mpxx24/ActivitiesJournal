using System.Security.Claims;
using System.Security.Cryptography;
using ActivitiesJournal.Configuration;
using ActivitiesJournal.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
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

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ClearCache(string? returnUrl = null)
    {
        _stravaService.InvalidateCache();
        _logger.LogInformation("Cache cleared manually");
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return Redirect("/");
    }

    public IActionResult Authorize()
    {
        var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        TempData["OAuthState"] = state;
        var authUrl = _stravaService.GetAuthorizationUrl(state);
        return Redirect(authUrl);
    }

    public async Task<IActionResult> Callback(string code, string state)
    {
        var expectedState = TempData["OAuthState"]?.ToString();
        if (string.IsNullOrEmpty(expectedState) || state != expectedState)
        {
            _logger.LogWarning("OAuth state mismatch. Expected: {Expected}, Received: {Received}",
                expectedState != null ? "[present]" : "[missing]",
                !string.IsNullOrEmpty(state) ? "[present]" : "[missing]");
            ViewBag.Error = "Authorization failed. Invalid state parameter.";
            return View();
        }

        if (string.IsNullOrEmpty(code))
        {
            ViewBag.Error = "Authorization failed. No code received from Strava.";
            return View();
        }

        try
        {
            var athleteId = await _stravaService.ExchangeCodeForTokenAsync(code);

            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, athleteId.ToString()) };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = true });
            _logger.LogInformation("Athlete {AthleteId} signed in via Strava OAuth", athleteId);

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
