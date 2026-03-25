using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using ActivitiesJournal.Configuration;
using ActivitiesJournal.Models;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;

namespace ActivitiesJournal.Services;

public class ActivityCacheService : IActivityCacheService
{
    private readonly BlobContainerClient? _containerClient;
    private readonly string _dataDirectory;
    private readonly ILogger<ActivityCacheService> _logger;

    private readonly ConcurrentDictionary<long, AthleteActivityState> _athleteStates = new();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public ActivityCacheService(
        IWebHostEnvironment env,
        IOptions<StorageOptions> storageOptions,
        ILogger<ActivityCacheService> logger)
    {
        _logger = logger;
        _dataDirectory = Path.Combine(env.ContentRootPath, "App_Data");

        var blobEndpoint = storageOptions.Value.BlobEndpoint;
        if (!string.IsNullOrEmpty(blobEndpoint))
        {
            _containerClient = new BlobContainerClient(
                new Uri($"{blobEndpoint.TrimEnd('/')}/{BlobContainerNames.Activities}"),
                new DefaultAzureCredential());
        }
    }

    public async Task<List<StravaActivity>> GetAsync(long athleteId)
    {
        var state = GetOrCreateState(athleteId);
        await state.EnsureLoadedAsync(athleteId, _containerClient, _dataDirectory, _logger);
        return state.Activities.ToList();
    }

    public async Task SetAsync(long athleteId, List<StravaActivity> activities)
    {
        var state = GetOrCreateState(athleteId);
        state.Replace(activities);
        await state.PersistAsync(athleteId, _containerClient, _dataDirectory, _logger);
    }

    private AthleteActivityState GetOrCreateState(long athleteId) =>
        _athleteStates.GetOrAdd(athleteId, _ => new AthleteActivityState());

    private sealed class AthleteActivityState
    {
        private List<StravaActivity> _activities = new();
        private bool _loaded;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public IReadOnlyList<StravaActivity> Activities => _activities;

        public void Replace(List<StravaActivity> activities) => _activities = activities;

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
                    var blobClient = containerClient.GetBlobClient($"activities-{athleteId}.json");
                    var response = await blobClient.DownloadContentAsync();
                    var list = JsonSerializer.Deserialize<List<StravaActivity>>(
                        response.Value.Content.ToString(), JsonOptions) ?? new();
                    _activities = list;
                    logger.LogInformation("Loaded {Count} activities from blob storage for athlete {AthleteId}", _activities.Count, athleteId);
                    return;
                }
                catch (Azure.RequestFailedException ex) when (ex.Status == 404)
                {
                    logger.LogInformation("No activities blob found for athlete {AthleteId} — starting fresh", athleteId);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to load activities from blob storage for athlete {AthleteId}", athleteId);
                }
            }

            var filePath = Path.Combine(dataDirectory, $"activities-{athleteId}.json");
            if (File.Exists(filePath))
            {
                try
                {
                    var text = await File.ReadAllTextAsync(filePath);
                    _activities = JsonSerializer.Deserialize<List<StravaActivity>>(text, JsonOptions) ?? new();
                    logger.LogInformation("Loaded {Count} activities from local file for athlete {AthleteId}", _activities.Count, athleteId);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to load activities from local file for athlete {AthleteId}", athleteId);
                }
            }
        }

        public async Task PersistAsync(
            long athleteId,
            BlobContainerClient? containerClient,
            string dataDirectory,
            ILogger logger)
        {
            var json = JsonSerializer.Serialize(_activities, JsonOptions);

            if (containerClient != null)
            {
                try
                {
                    var blobClient = containerClient.GetBlobClient($"activities-{athleteId}.json");
                    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
                    await blobClient.UploadAsync(stream, overwrite: true);
                    logger.LogInformation("Persisted {Count} activities to blob storage for athlete {AthleteId}", _activities.Count, athleteId);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to persist activities to blob storage for athlete {AthleteId}", athleteId);
                }
                return;
            }

            try
            {
                var filePath = Path.Combine(dataDirectory, $"activities-{athleteId}.json");
                Directory.CreateDirectory(dataDirectory);
                await File.WriteAllTextAsync(filePath, json);
                logger.LogInformation("Persisted {Count} activities to local file for athlete {AthleteId}", _activities.Count, athleteId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to persist activities to local file for athlete {AthleteId}", athleteId);
            }
        }
    }
}
