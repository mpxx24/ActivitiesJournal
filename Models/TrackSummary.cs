namespace ActivitiesJournal.Models;

public class TrackSummary
{
    public string Id { get; set; } = string.Empty;
    public long AthleteId { get; set; }
    public ActivityType ActivityType { get; set; }
    public DateTime StartedAt { get; set; }
    public double DistanceKm { get; set; }
    public TimeSpan Duration { get; set; }
    public int PointCount { get; set; }
}
