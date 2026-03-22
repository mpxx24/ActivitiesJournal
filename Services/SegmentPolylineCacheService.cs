using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using ActivitiesJournal.Configuration;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;

namespace ActivitiesJournal.Services;

public class SegmentPolylineCacheService : ISegmentPolylineCacheService
{
    private readonly BlobContainerClient? _containerClient;
    private readonly string _dataDirectory;
    private readonly ILogger<SegmentPolylineCacheService> _logger;

    private readonly ConcurrentDictionary<long, AthletePolylineState> _athleteStates = new();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public SegmentPolylineCacheService(
        IWebHostEnvironment env,
        IOptions<StorageOptions> storageOptions,
        ILogger<SegmentPolylineCacheService> logger)
    {
        _logger = logger;
        _dataDirectory = Path.Combine(env.ContentRootPath, "App_Data");

        var blobEndpoint = storageOptions.Value.BlobEndpoint;
        if (!string.IsNullOrEmpty(blobEndpoint))
        {
            _containerClient = new BlobContainerClient(
                new Uri($"{blobEndpoint.TrimEnd('/')}/{BlobContainerNames.Segments}"),
                new DefaultAzureCredential());
        }
    }

    public async Task<string?> GetAsync(long athleteId, long segmentId)
    {
        var state = GetOrCreateState(athleteId);
        await state.EnsureLoadedAsync(athleteId, _containerClient, _dataDirectory, _logger);
        return state.Polylines.TryGetValue(segmentId, out var poly) ? poly : null;
    }

    public async Task SetAsync(long athleteId, long segmentId, string polyline)
    {
        var state = GetOrCreateState(athleteId);
        await state.EnsureLoadedAsync(athleteId, _containerClient, _dataDirectory, _logger);
        state.Polylines[segmentId] = polyline;
        await state.PersistAsync(athleteId, _containerClient, _dataDirectory, _logger);
    }

    private AthletePolylineState GetOrCreateState(long athleteId) =>
        _athleteStates.GetOrAdd(athleteId, _ => new AthletePolylineState());

    private sealed class AthletePolylineState
    {
        public ConcurrentDictionary<long, string> Polylines { get; } = new();
        private bool _loaded;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public async Task EnsureLoadedAsync(
            long athleteId,
            BlobContainerClient? containerClient,
            string dataDirectory,
            ILogger logger)
        {
            if (_loaded) return;
            await _lock.WaitAsync();
            try
            {
                if (_loaded) return;
                await LoadAsync(athleteId, containerClient, dataDirectory, logger);
                _loaded = true;
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task LoadAsync(
            long athleteId,
            BlobContainerClient? containerClient,
            string dataDirectory,
            ILogger logger)
        {
            if (containerClient != null)
            {
                try
                {
                    var blobClient = containerClient.GetBlobClient($"polylines-{athleteId}.json");
                    var response = await blobClient.DownloadContentAsync();
                    var dict = JsonSerializer.Deserialize<Dictionary<long, string>>(
                        response.Value.Content.ToString(), JsonOptions) ?? new();
                    foreach (var (k, v) in dict)
                        Polylines[k] = v;
                    logger.LogInformation("Loaded {Count} segment polylines from blob storage for athlete {AthleteId}", Polylines.Count, athleteId);
                    return;
                }
                catch (Azure.RequestFailedException ex) when (ex.Status == 404)
                {
                    logger.LogInformation("No segment polylines blob found for athlete {AthleteId} — starting fresh", athleteId);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to load segment polylines from blob storage for athlete {AthleteId}", athleteId);
                }
            }

            var filePath = Path.Combine(dataDirectory, $"segment-polylines-{athleteId}.json");
            if (File.Exists(filePath))
            {
                try
                {
                    var text = await File.ReadAllTextAsync(filePath);
                    var dict = JsonSerializer.Deserialize<Dictionary<long, string>>(text, JsonOptions) ?? new();
                    foreach (var (k, v) in dict)
                        Polylines[k] = v;
                    logger.LogInformation("Loaded {Count} segment polylines from local file for athlete {AthleteId}", Polylines.Count, athleteId);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to load segment polylines from local file for athlete {AthleteId}", athleteId);
                }
            }
        }

        public async Task PersistAsync(
            long athleteId,
            BlobContainerClient? containerClient,
            string dataDirectory,
            ILogger logger)
        {
            var snapshot = new Dictionary<long, string>(Polylines);
            var json = JsonSerializer.Serialize(snapshot, JsonOptions);

            if (containerClient != null)
            {
                try
                {
                    var blobClient = containerClient.GetBlobClient($"polylines-{athleteId}.json");
                    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
                    await blobClient.UploadAsync(stream, overwrite: true);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to persist segment polylines to blob storage for athlete {AthleteId}", athleteId);
                }
                return;
            }

            try
            {
                var filePath = Path.Combine(dataDirectory, $"segment-polylines-{athleteId}.json");
                Directory.CreateDirectory(dataDirectory);
                await File.WriteAllTextAsync(filePath, json);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to persist segment polylines to local file for athlete {AthleteId}", athleteId);
            }
        }
    }
}
