using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using ActivitiesJournal.Configuration;
using ActivitiesJournal.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ActivitiesJournal.Services;

public class StravaService : IStravaService
{
    private readonly StravaOptions _config;
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ISegmentPolylineCacheService _segmentPolylineCache;
    private readonly ITokenStore _tokenStore;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<StravaService> _logger;

    private string AllActivitiesCacheKey() => $"strava_all_activities_{GetCurrentAthleteId()}";
    private string PageCacheKey(int page, int perPage) => $"strava_page_{GetCurrentAthleteId()}_{page}_{perPage}";
    private string ActivityCacheKey(long id) => $"strava_activity_{GetCurrentAthleteId()}_{id}";
    private string SegmentPolyCacheKey(long id) => $"segment_poly_{GetCurrentAthleteId()}_{id}";

    private static readonly TimeSpan ListCacheDuration = TimeSpan.FromHours(1);
    private static readonly TimeSpan DetailCacheDuration = TimeSpan.FromHours(1);

    private record SegmentDetailResponse([property: System.Text.Json.Serialization.JsonPropertyName("map")] SegmentMapDetail? Map);
    private record SegmentMapDetail([property: System.Text.Json.Serialization.JsonPropertyName("polyline")] string? Polyline);

    public StravaService(
        IOptions<StravaOptions> config,
        HttpClient httpClient,
        IMemoryCache cache,
        ISegmentPolylineCacheService segmentPolylineCache,
        ITokenStore tokenStore,
        IHttpContextAccessor httpContextAccessor,
        ILogger<StravaService> logger)
    {
        _config = config.Value;
        _httpClient = httpClient;
        _cache = cache;
        _segmentPolylineCache = segmentPolylineCache;
        _tokenStore = tokenStore;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    private long GetCurrentAthleteId()
    {
        var value = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(value, out var id) ? id : 0;
    }

    private void SetAuthHeader()
    {
        var athleteId = GetCurrentAthleteId();
        var (accessToken, _) = _tokenStore.Get(athleteId);
        if (!string.IsNullOrEmpty(accessToken))
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    public void InvalidateCache()
    {
        var athleteId = GetCurrentAthleteId();
        _cache.Remove(AllActivitiesCacheKey());
        for (int p = 1; p <= 20; p++)
            _cache.Remove(PageCacheKey(p, 200));
        _tokenStore.SetCacheTimestamp(athleteId, null);
        _logger.LogInformation("Strava cache invalidated for athlete {AthleteId}", athleteId);
    }

    public DateTime? GetCacheTimestamp() => _tokenStore.GetCacheTimestamp(GetCurrentAthleteId());

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
            var athleteId = GetCurrentAthleteId();
            var (accessToken, _) = _tokenStore.Get(athleteId);
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogError("No access token found for athlete {AthleteId}. Please log in via Strava OAuth.", athleteId);
                throw new InvalidOperationException("Strava access token is not configured. Please log in via Strava OAuth.");
            }

            SetAuthHeader();
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
        var cacheKey = AllActivitiesCacheKey();
        if (_cache.TryGetValue(cacheKey, out List<StravaActivity>? cached) && cached != null)
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
        _cache.Set(cacheKey, all, ListCacheDuration);
        _tokenStore.SetCacheTimestamp(GetCurrentAthleteId(), DateTime.Now);
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
            var athleteId = GetCurrentAthleteId();
            var (accessToken, _) = _tokenStore.Get(athleteId);
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogError("No access token found for athlete {AthleteId}. Please log in via Strava OAuth.", athleteId);
                throw new InvalidOperationException("Strava access token is not configured. Please log in via Strava OAuth.");
            }

            SetAuthHeader();
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
            var athleteId = GetCurrentAthleteId();
            var (_, refreshToken) = _tokenStore.Get(athleteId);

            var requestBody = new Dictionary<string, string>
            {
                { "client_id", _config.ClientId },
                { "client_secret", _config.ClientSecret },
                { "refresh_token", refreshToken ?? _config.RefreshToken },
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
                _tokenStore.Set(athleteId, newAccessToken, newRefreshToken ?? string.Empty);
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", newAccessToken);
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

    public async Task<long> ExchangeCodeForTokenAsync(string code)
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

            long athleteId = 0;
            if (tokenResponse.TryGetProperty("athlete", out var athlete) &&
                athlete.TryGetProperty("id", out var idProp))
            {
                athleteId = idProp.GetInt64();
            }

            if (!string.IsNullOrEmpty(newAccessToken) && athleteId != 0)
            {
                _tokenStore.Set(athleteId, newAccessToken, newRefreshToken ?? string.Empty);
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", newAccessToken);
            }

            _logger.LogInformation("Successfully exchanged authorization code for access token. AthleteId: {AthleteId}", athleteId);
            return athleteId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exchanging authorization code for access token");
            throw;
        }
    }

    public async Task<string?> GetSegmentPolylineAsync(long segmentId)
    {
        var athleteId = GetCurrentAthleteId();
        var cacheKey = SegmentPolyCacheKey(segmentId);

        if (_cache.TryGetValue(cacheKey, out string? cached))
            return cached;

        var persisted = await _segmentPolylineCache.GetAsync(athleteId, segmentId);
        if (persisted != null)
        {
            _cache.Set(cacheKey, persisted, new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });
            return persisted;
        }

        try
        {
            SetAuthHeader();
            var response = await _httpClient.GetAsync($"segments/{segmentId}");
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await RefreshAccessTokenAsync();
                response = await _httpClient.GetAsync($"segments/{segmentId}");
            }
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Could not fetch segment {SegmentId}: {Status}", segmentId, response.StatusCode);
                _cache.Set(cacheKey, (string?)null, new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });
                return null;
            }
            var content = await response.Content.ReadAsStringAsync();
            var detail = JsonSerializer.Deserialize<SegmentDetailResponse>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var polyline = detail?.Map?.Polyline;

            _cache.Set(cacheKey, polyline, new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });
            if (polyline != null)
                await _segmentPolylineCache.SetAsync(athleteId, segmentId, polyline);

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
        var redirectUri = string.IsNullOrWhiteSpace(_config.RedirectUri)
            ? "http://localhost:5010/Strava/Callback"
            : _config.RedirectUri;

        return $"https://www.strava.com/oauth/authorize" +
               $"?client_id={_config.ClientId}" +
               $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
               $"&response_type=code" +
               $"&approval_prompt=auto" +
               $"&scope=activity:read_all";
    }
}
