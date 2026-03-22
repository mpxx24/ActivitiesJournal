using System.Collections.Concurrent;
using ActivitiesJournal.Configuration;
using Microsoft.Extensions.Options;

namespace ActivitiesJournal.Services;

public class TokenStore : ITokenStore
{
    private readonly ConcurrentDictionary<long, (string AccessToken, string RefreshToken)> _tokens = new();
    private readonly ConcurrentDictionary<long, DateTime?> _timestamps = new();

    public TokenStore(IOptions<StravaOptions> stravaOptions, IOptions<TrackOwnerOptions> ownerOptions)
    {
        var opts = stravaOptions.Value;
        var owner = ownerOptions.Value;
        if (!string.IsNullOrEmpty(opts.AccessToken) && owner.OwnerAthleteId != 0)
            _tokens[owner.OwnerAthleteId] = (opts.AccessToken, opts.RefreshToken ?? string.Empty);
    }

    public (string? AccessToken, string? RefreshToken) Get(long athleteId)
    {
        return _tokens.TryGetValue(athleteId, out var t) ? (t.AccessToken, t.RefreshToken) : (null, null);
    }

    public void Set(long athleteId, string accessToken, string refreshToken)
    {
        _tokens[athleteId] = (accessToken, refreshToken);
    }

    public DateTime? GetCacheTimestamp(long athleteId)
    {
        return _timestamps.TryGetValue(athleteId, out var ts) ? ts : null;
    }

    public void SetCacheTimestamp(long athleteId, DateTime? timestamp)
    {
        _timestamps[athleteId] = timestamp;
    }
}
