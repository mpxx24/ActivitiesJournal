using ActivitiesJournal.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ActivitiesJournal.Controllers;

[Authorize]
public class ConfigController : Controller
{
    private readonly StravaOptions _config;
    private readonly ILogger<ConfigController> _logger;

    public ConfigController(IOptions<StravaOptions> config, ILogger<ConfigController> logger)
    {
        _config = config.Value;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var configStatus = new
        {
            ClientId = !string.IsNullOrEmpty(_config.ClientId) ? "Set" : "NOT SET",
            ClientSecret = !string.IsNullOrEmpty(_config.ClientSecret) ? "Set" : "NOT SET",
            AccessToken = !string.IsNullOrEmpty(_config.AccessToken) ? "Set" : "NOT SET",
            RefreshToken = !string.IsNullOrEmpty(_config.RefreshToken) ? "Set" : "NOT SET",
            _config.BaseUrl,
        };

        return Json(configStatus);
    }
}
