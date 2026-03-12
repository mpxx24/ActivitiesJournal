using ActivitiesJournal.Models;

namespace ActivitiesJournal.Services;

public interface IStravaService
{
    Task<List<StravaActivity>> GetActivitiesAsync(int page = 1, int perPage = 30);
    Task<List<StravaActivity>> GetAllActivitiesAsync();
    Task<StravaActivity?> GetActivityByIdAsync(long activityId);
    Task<string> RefreshAccessTokenAsync();
    Task ExchangeCodeForTokenAsync(string code);
    string GetAuthorizationUrl();
    void InvalidateCache();
    Task<string?> GetSegmentPolylineAsync(long segmentId);
}
