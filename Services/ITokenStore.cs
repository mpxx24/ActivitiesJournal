namespace ActivitiesJournal.Services;

public interface ITokenStore
{
    (string? AccessToken, string? RefreshToken) Get(long athleteId);
    void Set(long athleteId, string accessToken, string refreshToken);
    DateTime? GetCacheTimestamp(long athleteId);
    void SetCacheTimestamp(long athleteId, DateTime? timestamp);
    Task LoadAllFromBlobAsync();
}
