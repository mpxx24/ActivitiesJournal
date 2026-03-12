using System.Text.Json;
using ActivitiesJournal.Models;

namespace ActivitiesJournal.Services;

public class GoalsService
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    public GoalsService(IWebHostEnvironment env)
    {
        _filePath = Path.Combine(env.ContentRootPath, "App_Data", "goals.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
    }

    public GoalsData Load()
    {
        if (!File.Exists(_filePath)) return SeedDefaults();
        try
        {
            var text = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<GoalsData>(text, _json) ?? SeedDefaults();
        }
        catch { return SeedDefaults(); }
    }

    public void Save(GoalsData data)
        => File.WriteAllText(_filePath, JsonSerializer.Serialize(data, _json));

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
