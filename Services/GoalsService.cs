using System.Text.Json;
using ActivitiesJournal.Configuration;
using ActivitiesJournal.Models;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;

namespace ActivitiesJournal.Services;

public class GoalsService : IGoalsService
{
    private readonly string _dataDirectory;
    private readonly BlobContainerClient? _containerClient;
    private readonly ILogger<GoalsService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public GoalsService(IWebHostEnvironment env, IOptions<StorageOptions> storageOptions, ILogger<GoalsService> logger)
    {
        _logger = logger;
        _dataDirectory = Path.Combine(env.ContentRootPath, "App_Data");

        var blobEndpoint = storageOptions.Value.BlobEndpoint;
        if (!string.IsNullOrEmpty(blobEndpoint))
        {
            _containerClient = new BlobContainerClient(
                new Uri($"{blobEndpoint.TrimEnd('/')}/{BlobContainerNames.Goals}"),
                new DefaultAzureCredential());
        }
    }

    public async Task<GoalsData> LoadAsync(long athleteId)
    {
        if (_containerClient != null)
        {
            try
            {
                var blobClient = _containerClient.GetBlobClient($"goals-{athleteId}.json");
                var response = await blobClient.DownloadContentAsync();
                var data = JsonSerializer.Deserialize<GoalsData>(response.Value.Content.ToString(), JsonOptions)
                    ?? SeedDefaults();
                NormalizePresetStartDates(data);
                return data;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                return SeedDefaults();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load goals from blob storage for athlete {AthleteId}, returning defaults", athleteId);
                return SeedDefaults();
            }
        }

        var filePath = GetLocalFilePath(athleteId);
        if (!File.Exists(filePath)) return SeedDefaults();
        try
        {
            var text = await File.ReadAllTextAsync(filePath);
            var data = JsonSerializer.Deserialize<GoalsData>(text, JsonOptions) ?? SeedDefaults();
            NormalizePresetStartDates(data);
            return data;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load goals from file for athlete {AthleteId}, returning defaults", athleteId);
            return SeedDefaults();
        }
    }

    public async Task SaveAsync(GoalsData data, long athleteId)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);

        if (_containerClient != null)
        {
            var blobClient = _containerClient.GetBlobClient($"goals-{athleteId}.json");
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            await blobClient.UploadAsync(stream, overwrite: true);
            return;
        }

        var filePath = GetLocalFilePath(athleteId);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(filePath, json);
    }

    private string GetLocalFilePath(long athleteId) =>
        Path.Combine(_dataDirectory, $"goals-{athleteId}.json");

    internal static void NormalizePresetStartDates(GoalsData data)
    {
        foreach (var c in data.Challenges.Where(c => c.IsPreset))
            c.StartDate = new DateTime(c.StartDate.Year, 1, 1);
    }

    private static GoalsData SeedDefaults()
    {
        var jan1 = new DateTime(DateTime.Today.Year, 1, 1);
        return new GoalsData
        {
            Challenges = new List<VirtualChallenge>
            {
                new() { Name = "Tour de France", TargetKm = 3_406, StartDate = jan1, IsPreset = true },
                new() { Name = "Giro d'Italia", TargetKm = 3_497, StartDate = jan1, IsPreset = true },
                new() { Name = "Vuelta a España", TargetKm = 3_270, StartDate = jan1, IsPreset = true },
                new() { Name = "Ride Across Poland (N–S)", TargetKm = 630, StartDate = jan1, IsPreset = true },
                new() { Name = "Warsaw → Paris", TargetKm = 1_430, StartDate = jan1, IsPreset = true },
                new() { Name = "Warsaw → Rome", TargetKm = 2_100, StartDate = jan1, IsPreset = true },
            }
        };
    }
}
