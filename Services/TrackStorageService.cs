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
                new Uri($"{blobEndpoint.TrimEnd('/')}/gps-tracks"),
                new DefaultAzureCredential());
            _containerClient.CreateIfNotExists();
        }
    }

    public async Task<TrackSummary> UploadTrackAsync(Stream gpxStream, TrackSummary summary, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(gpxStream);
        ArgumentNullException.ThrowIfNull(summary);

        if (_containerClient == null)
            throw new InvalidOperationException("Blob storage is not configured.");

        var gpxBlob = _containerClient.GetBlobClient($"{summary.Id}.gpx");
        await gpxBlob.UploadAsync(gpxStream, overwrite: true, cancellationToken: ct);

        var metaBlob = _containerClient.GetBlobClient($"{summary.Id}.json");
        var json = JsonSerializer.Serialize(summary, JsonOptions);
        using var metaStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await metaBlob.UploadAsync(metaStream, overwrite: true, cancellationToken: ct);

        _logger.LogInformation("Uploaded track {TrackId} ({ActivityType}, {DistanceKm:F1} km)",
            summary.Id, summary.ActivityType, summary.DistanceKm);

        return summary;
    }

    public async Task<IReadOnlyList<TrackSummary>> ListTracksAsync(CancellationToken ct = default)
    {
        if (_containerClient == null)
            return Array.Empty<TrackSummary>();

        var summaries = new List<TrackSummary>();
        await foreach (var blob in _containerClient.GetBlobsAsync(cancellationToken: ct))
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

    public async Task<TrackSummary?> GetTrackSummaryAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        if (_containerClient == null)
            return null;

        try
        {
            var blobClient = _containerClient.GetBlobClient($"{id}.json");
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

    public async Task DeleteTrackAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        if (_containerClient == null)
            throw new InvalidOperationException("Blob storage is not configured.");

        await _containerClient.GetBlobClient($"{id}.gpx").DeleteIfExistsAsync(cancellationToken: ct);
        await _containerClient.GetBlobClient($"{id}.json").DeleteIfExistsAsync(cancellationToken: ct);

        _logger.LogInformation("Deleted track {TrackId}", id);
    }

    public async Task<Stream> GetTrackGpxAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        if (_containerClient == null)
            throw new InvalidOperationException("Blob storage is not configured.");

        var blobClient = _containerClient.GetBlobClient($"{id}.gpx");
        var response = await blobClient.DownloadStreamingAsync(cancellationToken: ct);
        return response.Value.Content;
    }
}
