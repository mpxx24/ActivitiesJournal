namespace ActivitiesJournal.Models;

public class StravaConfig
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://www.strava.com/api/v3";
    public string? RedirectUri { get; set; }
}
