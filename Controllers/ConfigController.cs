using ActivitiesJournal.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ActivitiesJournal.Controllers;

public class ConfigController : Controller
{
    private readonly StravaConfig _config;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigController> _logger;

    public ConfigController(IOptions<StravaConfig> config, IConfiguration configuration, ILogger<ConfigController> logger)
    {
        _config = config.Value;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index()
    {
        // This endpoint helps debug configuration issues
        // Remove or secure this in production!
        
        var configStatus = new
        {
            FromIOptions = new
            {
                ClientId = !string.IsNullOrEmpty(_config.ClientId) ? "✓ Set" : "❌ NOT SET",
                ClientSecret = !string.IsNullOrEmpty(_config.ClientSecret) ? "✓ Set" : "❌ NOT SET",
                AccessToken = !string.IsNullOrEmpty(_config.AccessToken) ? "✓ Set" : "❌ NOT SET",
                RefreshToken = !string.IsNullOrEmpty(_config.RefreshToken) ? "✓ Set" : "❌ NOT SET",
                BaseUrl = _config.BaseUrl
            },
            FromConfiguration = new
            {
                ClientId = _configuration["Strava:ClientId"] != null ? "✓ Found" : "❌ NOT FOUND",
                ClientSecret = _configuration["Strava:ClientSecret"] != null ? "✓ Found" : "❌ NOT FOUND",
                AccessToken = _configuration["Strava:AccessToken"] != null ? "✓ Found" : "❌ NOT FOUND",
                RefreshToken = _configuration["Strava:RefreshToken"] != null ? "✓ Found" : "❌ NOT FOUND",
                BaseUrl = _configuration["Strava:BaseUrl"] ?? "NOT SET"
            },
            EnvironmentVariables = new
            {
                Strava__ClientId = Environment.GetEnvironmentVariable("Strava__ClientId") != null ? "✓ Set" : "❌ NOT SET",
                Strava__ClientSecret = Environment.GetEnvironmentVariable("Strava__ClientSecret") != null ? "✓ Set" : "❌ NOT SET",
                Strava__AccessToken = Environment.GetEnvironmentVariable("Strava__AccessToken") != null ? "✓ Set" : "❌ NOT SET",
                Strava__RefreshToken = Environment.GetEnvironmentVariable("Strava__RefreshToken") != null ? "✓ Set" : "❌ NOT SET"
            }
        };

        return Json(configStatus);
    }
}
