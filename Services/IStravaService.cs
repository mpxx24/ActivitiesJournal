using ActivitiesJournal.Models;

namespace ActivitiesJournal.Services;

public interface IStravaService
{
    Task<List<StravaActivity>> GetActivitiesAsync(int page = 1, int perPage = 30);
    Task<List<StravaActivity>> GetAllActivitiesAsync();
    Task<StravaActivity?> GetActivityByIdAsync(long activityId);
    Task<string> RefreshAccessTokenAsync();
    Task<long> ExchangeCodeForTokenAsync(string code);
    string GetAuthorizationUrl(string state);
    void InvalidateCache();
    DateTime? GetCacheTimestamp();
    Task<string?> GetSegmentPolylineAsync(long segmentId);

    /// <summary>
    /// Uploads a GPX file to Strava for the given athlete (explicit id — this is
    /// also called from the API-key-protected Track upload, where there is no
    /// authenticated HttpContext user). Polls Strava's async upload endpoint
    /// until an activity id or an error is available.
    /// </summary>
    Task<StravaUploadResult> UploadActivityAsync(
        long athleteId,
        Stream gpxStream,
        string fileName,
        ActivityType activityType,
        string? description,
        string externalId,
        TimeSpan? pollInterval = null,
        CancellationToken ct = default);
}
