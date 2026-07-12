using System.Text;
using System.Text.Json;
using ActivitiesJournal.Configuration;
using ActivitiesJournal.Models;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;

namespace ActivitiesJournal.Services;

public class TrackStorageService : ITrackStorageService
{
    private readonly BlobContainerClient? _containerClient;
    private readonly ILogger<TrackStorageService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public TrackStorageService(IOptions<StorageOptions> storageOptions, ILogger<TrackStorageService> logger)
    {
        _logger = logger;
        var blobEndpoint = storageOptions.Value.BlobEndpoint;
        if (!string.IsNullOrEmpty(blobEndpoint))
        {
            _containerClient = new BlobContainerClient(
                new Uri($"{blobEndpoint.TrimEnd('/')}/{BlobContainerNames.Tracks}"),
                new DefaultAzureCredential());
        }
    }

    private static string GpxBlobPath(long athleteId, string trackId) => $"{athleteId}/{trackId}.gpx";
    private static string MetaBlobPath(long athleteId, string trackId) => $"{athleteId}/{trackId}.json";

    public async Task<TrackSummary> UploadTrackAsync(Stream gpxStream, TrackSummary summary, long athleteId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(gpxStream);
        ArgumentNullException.ThrowIfNull(summary);

        if (_containerClient == null)
            throw new InvalidOperationException("Blob storage is not configured.");

        summary.AthleteId = athleteId;

        var gpxBlob = _containerClient.GetBlobClient(GpxBlobPath(athleteId, summary.Id));
        await gpxBlob.UploadAsync(gpxStream, overwrite: true, cancellationToken: ct);

        var metaBlob = _containerClient.GetBlobClient(MetaBlobPath(athleteId, summary.Id));
        var json = JsonSerializer.Serialize(summary, JsonOptions);
        using var metaStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await metaBlob.UploadAsync(metaStream, overwrite: true, cancellationToken: ct);

        _logger.LogInformation("Uploaded track {TrackId} for athlete {AthleteId} ({ActivityType}, {DistanceKm:F1} km)",
            summary.Id, athleteId, summary.ActivityType, summary.DistanceKm);

        return summary;
    }

    public async Task UpdateTrackSummaryAsync(TrackSummary summary, long athleteId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentException.ThrowIfNullOrEmpty(summary.Id);

        if (_containerClient == null)
            throw new InvalidOperationException("Blob storage is not configured.");

        summary.AthleteId = athleteId;

        var metaBlob = _containerClient.GetBlobClient(MetaBlobPath(athleteId, summary.Id));
        var json = JsonSerializer.Serialize(summary, JsonOptions);
        using var metaStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await metaBlob.UploadAsync(metaStream, overwrite: true, cancellationToken: ct);

        _logger.LogInformation("Updated track metadata {TrackId} for athlete {AthleteId}", summary.Id, athleteId);
    }

    public async Task<IReadOnlyList<TrackSummary>> ListTracksAsync(long athleteId, CancellationToken ct = default)
    {
        if (_containerClient == null)
            return Array.Empty<TrackSummary>();

        var prefix = $"{athleteId}/";
        var summaries = new List<TrackSummary>();
        await foreach (var blob in _containerClient.GetBlobsAsync(traits: Azure.Storage.Blobs.Models.BlobTraits.None, states: Azure.Storage.Blobs.Models.BlobStates.None, prefix: prefix, cancellationToken: ct))
        {
            if (!blob.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                var blobClient = _containerClient.GetBlobClient(blob.Name);
                var response = await blobClient.DownloadContentAsync(cancellationToken: ct);
                var summary = JsonSerializer.Deserialize<TrackSummary>(response.Value.Content.ToString(), JsonOptions);
                if (summary != null)
                    summaries.Add(summary);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load track metadata from {BlobName}", blob.Name);
            }
        }

        return summaries.OrderByDescending(s => s.StartedAt).ToList();
    }

    public async Task<TrackSummary?> GetTrackSummaryAsync(long athleteId, string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        if (_containerClient == null)
            return null;

        try
        {
            var blobClient = _containerClient.GetBlobClient(MetaBlobPath(athleteId, id));
            var response = await blobClient.DownloadContentAsync(cancellationToken: ct);
            return JsonSerializer.Deserialize<TrackSummary>(response.Value.Content.ToString(), JsonOptions);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get track summary for {TrackId}", id);
            throw;
        }
    }

    public async Task DeleteTrackAsync(long athleteId, string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        if (_containerClient == null)
            throw new InvalidOperationException("Blob storage is not configured.");

        await _containerClient.GetBlobClient(GpxBlobPath(athleteId, id)).DeleteIfExistsAsync(cancellationToken: ct);
        await _containerClient.GetBlobClient(MetaBlobPath(athleteId, id)).DeleteIfExistsAsync(cancellationToken: ct);

        _logger.LogInformation("Deleted track {TrackId} for athlete {AthleteId}", id, athleteId);
    }

    public async Task<Stream> GetTrackGpxAsync(long athleteId, string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        if (_containerClient == null)
            throw new InvalidOperationException("Blob storage is not configured.");

        var blobClient = _containerClient.GetBlobClient(GpxBlobPath(athleteId, id));
        var response = await blobClient.DownloadStreamingAsync(cancellationToken: ct);
        return response.Value.Content;
    }

    public async Task MigrateToAthletePathsAsync(long ownerAthleteId, CancellationToken ct = default)
    {
        if (_containerClient == null)
        {
            _logger.LogInformation("Blob storage not configured — skipping track migration");
            return;
        }

        var migratedCount = 0;
        var rootJsonBlobs = new List<string>();

        await foreach (var blob in _containerClient.GetBlobsAsync(cancellationToken: ct))
        {
            if (blob.Name.Contains('/'))
                continue;
            if (blob.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                rootJsonBlobs.Add(blob.Name);
        }

        if (rootJsonBlobs.Count == 0)
        {
            _logger.LogInformation("No root-level track blobs to migrate");
            return;
        }

        _logger.LogInformation("Found {Count} root-level track blobs to migrate to athlete {AthleteId}", rootJsonBlobs.Count, ownerAthleteId);

        foreach (var jsonBlobName in rootJsonBlobs)
        {
            var trackId = jsonBlobName[..^5]; // strip .json
            var gpxBlobName = $"{trackId}.gpx";

            try
            {
                // Migrate JSON metadata — update AthleteId field
                var jsonSource = _containerClient.GetBlobClient(jsonBlobName);
                var jsonResponse = await jsonSource.DownloadContentAsync(cancellationToken: ct);
                var summary = JsonSerializer.Deserialize<TrackSummary>(jsonResponse.Value.Content.ToString(), JsonOptions);
                if (summary != null)
                {
                    summary.AthleteId = ownerAthleteId;
                    var updatedJson = JsonSerializer.Serialize(summary, JsonOptions);
                    var jsonDest = _containerClient.GetBlobClient(MetaBlobPath(ownerAthleteId, trackId));
                    using var metaStream = new MemoryStream(Encoding.UTF8.GetBytes(updatedJson));
                    await jsonDest.UploadAsync(metaStream, overwrite: true, cancellationToken: ct);
                    await jsonSource.DeleteIfExistsAsync(cancellationToken: ct);
                }

                // Migrate GPX file via copy
                var gpxSource = _containerClient.GetBlobClient(gpxBlobName);
                if (await gpxSource.ExistsAsync(ct))
                {
                    var gpxDest = _containerClient.GetBlobClient(GpxBlobPath(ownerAthleteId, trackId));
                    await gpxDest.StartCopyFromUriAsync(gpxSource.Uri, cancellationToken: ct);
                    await gpxSource.DeleteIfExistsAsync(cancellationToken: ct);
                }

                migratedCount++;
                _logger.LogInformation("Migrated track {TrackId} to athlete path {AthleteId}/{TrackId}", trackId, ownerAthleteId, trackId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to migrate track {TrackId}", trackId);
            }
        }

        _logger.LogInformation("Track migration complete. Migrated {Count} tracks to athlete {AthleteId}", migratedCount, ownerAthleteId);
    }
}
