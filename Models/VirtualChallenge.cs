namespace ActivitiesJournal.Models;

public class VirtualChallenge
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public double TargetKm { get; set; }
    public DateTime StartDate { get; set; }
    public bool IsPreset { get; set; }
}
