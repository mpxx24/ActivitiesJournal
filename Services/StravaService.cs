using System.Net.Http.Headers;
using System.Text.Json;
using ActivitiesJournal.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ActivitiesJournal.Services;

public class StravaService : IStravaService
{
    private readonly StravaConfig _config;
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<StravaService> _logger;

    private const string AllActivitiesCacheKey = "strava_all_activities";
    private static string PageCacheKey(int page, int perPage) => $"strava_page_{page}_{perPage}";
    private static string ActivityCacheKey(long id) => $"strava_activity_{id}";

    private static readonly TimeSpan ListCacheDuration = TimeSpan.FromHours(1);
    private static readonly TimeSpan DetailCacheDuration = TimeSpan.FromHours(1);
    private static string SegmentPolyCacheKey(long id) => $"segment_poly_{id}";

    private record SegmentDetailResponse([property: System.Text.Json.Serialization.JsonPropertyName("map")] SegmentMapDetail? Map);
    private record SegmentMapDetail([property: System.Text.Json.Serialization.JsonPropertyName("polyline")] string? Polyline);

    public StravaService(IOptions<StravaConfig> config, HttpClient httpClient, IMemoryCache cache, ILogger<StravaService> logger)
    {
        _config = config.Value;
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        
        // Log configuration status for debugging
        _logger.LogInformation("Strava Configuration Status:");
        _logger.LogInformation("  ClientId: {HasClientId}", !string.IsNullOrEmpty(_config.ClientId) ? "Set" : "NOT SET");
        _logger.LogInformation("  ClientSecret: {HasSecret}", !string.IsNullOrEmpty(_config.ClientSecret) ? "Set" : "NOT SET");
        _logger.LogInformation("  AccessToken: {HasToken}", !string.IsNullOrEmpty(_config.AccessToken) ? "Set" : "NOT SET");
        _logger.LogInformation("  RefreshToken: {HasRefresh}", !string.IsNullOrEmpty(_config.RefreshToken) ? "Set" : "NOT SET");
        _logger.LogInformation("  BaseUrl: {BaseUrl}", _config.BaseUrl);
        
        // Ensure BaseUrl ends with a slash for proper path combination
        var baseUrl = _config.BaseUrl.TrimEnd('/');
        _httpClient.BaseAddress = new Uri(baseUrl + "/");
        
        if (string.IsNullOrEmpty(_config.AccessToken))
        {
            _logger.LogWarning("⚠️  Strava AccessToken is not configured!");
            _logger.LogWarning("   Set environment variables: Strava__AccessToken, Strava__ClientId, Strava__ClientSecret, Strava__RefreshToken");
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", _config.AccessToken);
            _logger.LogInformation("✓ Access token configured, ready to make API calls");
        }
    }

    public void InvalidateCache()
    {
        // Remove the all-activities aggregate key
        _cache.Remove(AllActivitiesCacheKey);
        // Remove paginated list keys (pages 1–20 cover any realistic dataset)
        for (int p = 1; p <= 20; p++)
            _cache.Remove(PageCacheKey(p, 200));
        _logger.LogInformation("Strava cache invalidated");
    }

    public async Task<List<StravaActivity>> GetActivitiesAsync(int page = 1, int perPage = 30)
    {
        var cacheKey = PageCacheKey(page, perPage);
        if (_cache.TryGetValue(cacheKey, out List<StravaActivity>? cached) && cached != null)
            return cached;

        var result = await FetchActivitiesAsync(page, perPage);
        _cache.Set(cacheKey, result, ListCacheDuration);
        return result;
    }

    private async Task<List<StravaActivity>> FetchActivitiesAsync(int page, int perPage)
    {
        try
        {
            if (string.IsNullOrEmpty(_config.AccessToken))
            {
                _logger.LogError("Access token is not configured. Please set Strava:AccessToken in User Secrets.");
                throw new InvalidOperationException("Strava access token is not configured. Please configure it in User Secrets.");
            }

            var response = await _httpClient.GetAsync(
                $"athlete/activities?page={page}&per_page={perPage}");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Strava API error: Status {StatusCode}, Response: {ErrorContent}", 
                    response.StatusCode, errorContent);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Access token expired, attempting to refresh...");
                    await RefreshAccessTokenAsync();
                    response = await _httpClient.GetAsync(
                        $"athlete/activities?page={page}&per_page={perPage}");
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("Strava API error after refresh: Status {StatusCode}, Response: {ErrorContent}", 
                            response.StatusCode, errorContent);
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    _logger.LogError("Access forbidden (403). Possible causes: Invalid access token, insufficient permissions, or token doesn't have 'activity:read_all' scope.");
                    throw new UnauthorizedAccessException(
                        "Access forbidden. Please check that your access token is valid and has the 'activity:read_all' scope. " +
                        "You may need to re-authorize the application through the OAuth flow.");
                }

                response.EnsureSuccessStatusCode();
            }

            var content = await response.Content.ReadAsStringAsync();
            var activities = JsonSerializer.Deserialize<List<StravaActivity>>(
                content, 
                new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

            return activities ?? new List<StravaActivity>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching activities from Strava");
            throw;
        }
    }

    public async Task<List<StravaActivity>> GetAllActivitiesAsync()
    {
        if (_cache.TryGetValue(AllActivitiesCacheKey, out List<StravaActivity>? cached) && cached != null)
            return cached;

        var all = new List<StravaActivity>();
        int page = 1;
        const int perPage = 200;
        while (true)
        {
            var batch = await FetchActivitiesAsync(page, perPage);
            all.AddRange(batch);
            if (batch.Count < perPage) break;
            page++;
        }
        _cache.Set(AllActivitiesCacheKey, all, ListCacheDuration);
        return all;
    }

    public async Task<StravaActivity?> GetActivityByIdAsync(long activityId)
    {
        var cacheKey = ActivityCacheKey(activityId);
        if (_cache.TryGetValue(cacheKey, out StravaActivity? cached) && cached != null)
            return cached;

        var result = await FetchActivityByIdAsync(activityId);
        if (result != null)
            _cache.Set(cacheKey, result, DetailCacheDuration);
        return result;
    }

    private async Task<StravaActivity?> FetchActivityByIdAsync(long activityId)
    {
        try
        {
            if (string.IsNullOrEmpty(_config.AccessToken))
            {
                _logger.LogError("Access token is not configured. Please set Strava:AccessToken in User Secrets.");
                throw new InvalidOperationException("Strava access token is not configured. Please configure it in User Secrets.");
            }

            var response = await _httpClient.GetAsync($"activities/{activityId}");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Strava API error: Status {StatusCode}, Response: {ErrorContent}", 
                    response.StatusCode, errorContent);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Access token expired, attempting to refresh...");
                    await RefreshAccessTokenAsync();
                    response = await _httpClient.GetAsync($"activities/{activityId}");
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("Strava API error after refresh: Status {StatusCode}, Response: {ErrorContent}", 
                            response.StatusCode, errorContent);
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    _logger.LogError("Access forbidden (403). Possible causes: Invalid access token, insufficient permissions, or token doesn't have 'activity:read_all' scope.");
                    throw new UnauthorizedAccessException(
                        "Access forbidden. Please check that your access token is valid and has the 'activity:read_all' scope.");
                }

                response.EnsureSuccessStatusCode();
            }

            var content = await response.Content.ReadAsStringAsync();
            var activity = JsonSerializer.Deserialize<StravaActivity>(
                content,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            return activity;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching activity {ActivityId} from Strava", activityId);
            throw;
        }
    }

    public async Task<string> RefreshAccessTokenAsync()
    {
        try
        {
            var requestBody = new Dictionary<string, string>
            {
                { "client_id", _config.ClientId },
                { "client_secret", _config.ClientSecret },
                { "refresh_token", _config.RefreshToken },
                { "grant_type", "refresh_token" }
            };

            var content = new FormUrlEncodedContent(requestBody);
            var response = await _httpClient.PostAsync("https://www.strava.com/oauth/token", content);

            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

            var newAccessToken = tokenResponse.GetProperty("access_token").GetString();
            var newRefreshToken = tokenResponse.GetProperty("refresh_token").GetString();

            if (!string.IsNullOrEmpty(newAccessToken))
            {
                _config.AccessToken = newAccessToken;
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", newAccessToken);
            }

            if (!string.IsNullOrEmpty(newRefreshToken))
            {
                _config.RefreshToken = newRefreshToken;
            }

            InvalidateCache();
            return newAccessToken ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing access token");
            throw;
        }
    }

    public async Task ExchangeCodeForTokenAsync(string code)
    {
        try
        {
            var requestBody = new Dictionary<string, string>
            {
                { "client_id", _config.ClientId },
                { "client_secret", _config.ClientSecret },
                { "code", code },
                { "grant_type", "authorization_code" }
            };

            using var content = new FormUrlEncodedContent(requestBody);
            using var response = await _httpClient.PostAsync("https://www.strava.com/oauth/token", content);

            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to exchange authorization code for token. Status {StatusCode}, Response: {Response}",
                    response.StatusCode, responseContent);
                response.EnsureSuccessStatusCode();
            }

            var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

            var newAccessToken = tokenResponse.GetProperty("access_token").GetString();
            var newRefreshToken = tokenResponse.GetProperty("refresh_token").GetString();

            if (!string.IsNullOrEmpty(newAccessToken))
            {
                _config.AccessToken = newAccessToken;
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", newAccessToken);
            }

            if (!string.IsNullOrEmpty(newRefreshToken))
            {
                _config.RefreshToken = newRefreshToken;
            }

            _logger.LogInformation("Successfully exchanged authorization code for access token.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exchanging authorization code for access token");
            throw;
        }
    }

    public async Task<string?> GetSegmentPolylineAsync(long segmentId)
    {
        var cacheKey = SegmentPolyCacheKey(segmentId);
        if (_cache.TryGetValue(cacheKey, out string? cached))
            return cached;

        try
        {
            var response = await _httpClient.GetAsync($"segments/{segmentId}");
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await RefreshAccessTokenAsync();
                response = await _httpClient.GetAsync($"segments/{segmentId}");
            }
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Could not fetch segment {SegmentId}: {Status}", segmentId, response.StatusCode);
                return null;
            }
            var content = await response.Content.ReadAsStringAsync();
            var detail = JsonSerializer.Deserialize<SegmentDetailResponse>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var polyline = detail?.Map?.Polyline;
            // Cache permanently — segment shapes never change
            _cache.Set(cacheKey, polyline, new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });
            return polyline;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching segment polyline for {SegmentId}", segmentId);
            return null;
        }
    }

    public string GetAuthorizationUrl()
    {
        // Use configured redirect URI if available, otherwise fall back to default dev URL.
        var redirectUri = string.IsNullOrWhiteSpace(_config.RedirectUri)
            ? "http://localhost:5010/Strava/Callback"
            : _config.RedirectUri;
        var scope = "activity:read_all";
        var state = Guid.NewGuid().ToString();

        return $"https://www.strava.com/oauth/authorize?" +
               $"client_id={_config.ClientId}&" +
               $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
               $"response_type=code&" +
               $"scope={scope}&" +
               $"state={state}";
    }
}
