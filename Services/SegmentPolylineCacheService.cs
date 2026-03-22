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
    private readonly BlobClient? _blobClient;
    private readonly string _localFilePath;
    private readonly ILogger<SegmentPolylineCacheService> _logger;

    private readonly ConcurrentDictionary<long, string> _polylines = new();
    private bool _loaded;
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public SegmentPolylineCacheService(
        IWebHostEnvironment env,
        IOptions<StorageOptions> storageOptions,
        ILogger<SegmentPolylineCacheService> logger)
    {
        _logger = logger;
        _localFilePath = Path.Combine(env.ContentRootPath, "App_Data", "segment-polylines.json");

        var blobEndpoint = storageOptions.Value.BlobEndpoint;
        if (!string.IsNullOrEmpty(blobEndpoint))
        {
            var containerClient = new BlobContainerClient(
                new Uri($"{blobEndpoint.TrimEnd('/')}/{BlobContainerNames.Segments}"),
                new DefaultAzureCredential());
            _blobClient = containerClient.GetBlobClient("polylines.json");
        }
    }

    public async Task<string?> GetAsync(long segmentId)
    {
        await EnsureLoadedAsync();
        return _polylines.TryGetValue(segmentId, out var poly) ? poly : null;
    }

    public async Task SetAsync(long segmentId, string polyline)
    {
        await EnsureLoadedAsync();
        _polylines[segmentId] = polyline;
        await PersistAsync();
    }

    private async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        await _loadLock.WaitAsync();
        try
        {
            if (_loaded) return;
            await LoadAsync();
            _loaded = true;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private async Task LoadAsync()
    {
        if (_blobClient != null)
        {
            try
            {
                var response = await _blobClient.DownloadContentAsync();
                var dict = JsonSerializer.Deserialize<Dictionary<long, string>>(
                    response.Value.Content.ToString(), JsonOptions) ?? new();
                foreach (var (k, v) in dict)
                    _polylines[k] = v;
                _logger.LogInformation("Loaded {Count} segment polylines from blob storage", _polylines.Count);
                return;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogInformation("No segment polylines blob found — starting fresh");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load segment polylines from blob storage");
            }
        }

        if (File.Exists(_localFilePath))
        {
            try
            {
                var text = await File.ReadAllTextAsync(_localFilePath);
                var dict = JsonSerializer.Deserialize<Dictionary<long, string>>(text, JsonOptions) ?? new();
                foreach (var (k, v) in dict)
                    _polylines[k] = v;
                _logger.LogInformation("Loaded {Count} segment polylines from local file", _polylines.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load segment polylines from local file");
            }
        }
    }

    private async Task PersistAsync()
    {
        var snapshot = new Dictionary<long, string>(_polylines);
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);

        if (_blobClient != null)
        {
            try
            {
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
                await _blobClient.UploadAsync(stream, overwrite: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist segment polylines to blob storage");
            }
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_localFilePath)!);
            await File.WriteAllTextAsync(_localFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist segment polylines to local file");
        }
    }
}
