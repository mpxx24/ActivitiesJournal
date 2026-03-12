using System.Text.Json;
using ActivitiesJournal.Models;
using Azure.Identity;
using Azure.Storage.Blobs;

namespace ActivitiesJournal.Services;

public class GoalsService
{
    private readonly string _filePath;
    private readonly BlobClient? _blobClient;
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    public GoalsService(IWebHostEnvironment env, IConfiguration config)
    {
        _filePath = Path.Combine(env.ContentRootPath, "App_Data", "goals.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

        var blobEndpoint = config["Storage:BlobEndpoint"];
        if (!string.IsNullOrEmpty(blobEndpoint))
        {
            var containerClient = new BlobContainerClient(
                new Uri($"{blobEndpoint.TrimEnd('/')}/goals"),
                new DefaultAzureCredential());
            containerClient.CreateIfNotExists();
            _blobClient = containerClient.GetBlobClient("goals.json");
        }
    }

    public GoalsData Load()
    {
        if (_blobClient != null)
        {
            try
            {
                var response = _blobClient.DownloadContent();
                return JsonSerializer.Deserialize<GoalsData>(response.Value.Content.ToString(), _json) ?? SeedDefaults();
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                return SeedDefaults();
            }
            catch { return SeedDefaults(); }
        }

        if (!File.Exists(_filePath)) return SeedDefaults();
        try
        {
            var text = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<GoalsData>(text, _json) ?? SeedDefaults();
        }
        catch { return SeedDefaults(); }
    }

    public void Save(GoalsData data)
    {
        var json = JsonSerializer.Serialize(data, _json);

        if (_blobClient != null)
        {
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            _blobClient.Upload(stream, overwrite: true);
            return;
        }

        File.WriteAllText(_filePath, json);
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
