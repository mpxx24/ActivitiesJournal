using System.Text.Json;
using ActivitiesJournal.Models;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;

namespace ActivitiesJournal.Services;

public class GoalsService : IGoalsService
{
    private readonly string _filePath;
    private readonly BlobClient? _blobClient;
    private readonly ILogger<GoalsService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public GoalsService(IWebHostEnvironment env, IOptions<StorageSettings> storageOptions, ILogger<GoalsService> logger)
    {
        _logger = logger;
        _filePath = Path.Combine(env.ContentRootPath, "App_Data", "goals.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

        var blobEndpoint = storageOptions.Value.BlobEndpoint;
        if (!string.IsNullOrEmpty(blobEndpoint))
        {
            var containerClient = new BlobContainerClient(
                new Uri($"{blobEndpoint.TrimEnd('/')}/goals"),
                new DefaultAzureCredential());
            containerClient.CreateIfNotExists();
            _blobClient = containerClient.GetBlobClient("goals.json");
        }
    }

    public async Task<GoalsData> LoadAsync()
    {
        if (_blobClient != null)
        {
            try
            {
                var response = await _blobClient.DownloadContentAsync();
                return JsonSerializer.Deserialize<GoalsData>(response.Value.Content.ToString(), JsonOptions)
                    ?? SeedDefaults();
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                return SeedDefaults();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load goals from blob storage, returning defaults");
                return SeedDefaults();
            }
        }

        if (!File.Exists(_filePath)) return SeedDefaults();
        try
        {
            var text = await File.ReadAllTextAsync(_filePath);
            return JsonSerializer.Deserialize<GoalsData>(text, JsonOptions) ?? SeedDefaults();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load goals from file, returning defaults");
            return SeedDefaults();
        }
    }

    public async Task SaveAsync(GoalsData data)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);

        if (_blobClient != null)
        {
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            await _blobClient.UploadAsync(stream, overwrite: true);
            return;
        }

        await File.WriteAllTextAsync(_filePath, json);
    }

    private static GoalsData SeedDefaults()
    {
        return new GoalsData
        {
            Challenges = new List<VirtualChallenge>
            {
                new() { Name = "Tour de France", TargetKm = 3_406, StartDate = DateTime.Today, IsPreset = true },
                new() { Name = "Giro d'Italia", TargetKm = 3_497, StartDate = DateTime.Today, IsPreset = true },
                new() { Name = "Vuelta a España", TargetKm = 3_270, StartDate = DateTime.Today, IsPreset = true },
                new() { Name = "Ride Across Poland (N–S)", TargetKm = 630, StartDate = DateTime.Today, IsPreset = true },
                new() { Name = "Warsaw → Paris", TargetKm = 1_430, StartDate = DateTime.Today, IsPreset = true },
                new() { Name = "Warsaw → Rome", TargetKm = 2_100, StartDate = DateTime.Today, IsPreset = true },
            }
        };
    }
}
