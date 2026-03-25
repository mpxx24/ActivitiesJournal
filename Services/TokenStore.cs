using System.Collections.Concurrent;
using System.Text.Json;
using ActivitiesJournal.Configuration;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;

namespace ActivitiesJournal.Services;

public class TokenStore : ITokenStore
{
    private readonly ConcurrentDictionary<long, (string AccessToken, string RefreshToken)> _tokens = new();
    private readonly ConcurrentDictionary<long, DateTime?> _timestamps = new();
    private readonly BlobContainerClient? _containerClient;
    private readonly ILogger<TokenStore> _logger;

    private record TokenEntry(string AccessToken, string RefreshToken);

    public TokenStore(
        IOptions<StravaOptions> stravaOptions,
        IOptions<TrackOwnerOptions> ownerOptions,
        IOptions<StorageOptions> storageOptions,
        ILogger<TokenStore> logger)
    {
        _logger = logger;
        var opts = stravaOptions.Value;
        var owner = ownerOptions.Value;
        if (!string.IsNullOrEmpty(opts.AccessToken) && owner.OwnerAthleteId != 0)
            _tokens[owner.OwnerAthleteId] = (opts.AccessToken, opts.RefreshToken ?? string.Empty);

        var blobEndpoint = storageOptions.Value.BlobEndpoint;
        if (!string.IsNullOrEmpty(blobEndpoint))
        {
            _containerClient = new BlobContainerClient(
                new Uri($"{blobEndpoint.TrimEnd('/')}/{BlobContainerNames.Tokens}"),
                new DefaultAzureCredential());
        }
    }

    public (string? AccessToken, string? RefreshToken) Get(long athleteId)
    {
        return _tokens.TryGetValue(athleteId, out var t) ? (t.AccessToken, t.RefreshToken) : (null, null);
    }

    public void Set(long athleteId, string accessToken, string refreshToken)
    {
        _tokens[athleteId] = (accessToken, refreshToken);
        _ = PersistToBlobAsync(athleteId, accessToken, refreshToken);
    }

    public DateTime? GetCacheTimestamp(long athleteId)
    {
        return _timestamps.TryGetValue(athleteId, out var ts) ? ts : null;
    }

    public void SetCacheTimestamp(long athleteId, DateTime? timestamp)
    {
        _timestamps[athleteId] = timestamp;
    }

    public async Task LoadAllFromBlobAsync()
    {
        if (_containerClient == null)
            return;

        try
        {
            await foreach (var blob in _containerClient.GetBlobsAsync())
            {
                if (!blob.Name.StartsWith("token-") || !blob.Name.EndsWith(".json"))
                    continue;

                var athleteIdStr = blob.Name["token-".Length..^".json".Length];
                if (!long.TryParse(athleteIdStr, out var athleteId))
                    continue;

                try
                {
                    var blobClient = _containerClient.GetBlobClient(blob.Name);
                    var response = await blobClient.DownloadContentAsync();
                    var entry = JsonSerializer.Deserialize<TokenEntry>(response.Value.Content.ToString());
                    if (entry != null)
                    {
                        _tokens[athleteId] = (entry.AccessToken, entry.RefreshToken);
                        _logger.LogInformation("Loaded token for athlete {AthleteId} from blob storage.", athleteId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load token for athlete {AthleteId} from blob.", athleteId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate tokens from blob storage.");
        }
    }

    private async Task PersistToBlobAsync(long athleteId, string accessToken, string refreshToken)
    {
        if (_containerClient == null)
            return;

        try
        {
            var json = JsonSerializer.Serialize(new TokenEntry(accessToken, refreshToken));
            var blobClient = _containerClient.GetBlobClient($"token-{athleteId}.json");
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            await blobClient.UploadAsync(stream, overwrite: true);
            _logger.LogInformation("Persisted token for athlete {AthleteId} to blob storage.", athleteId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist token for athlete {AthleteId} to blob storage.", athleteId);
        }
    }
}
