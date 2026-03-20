namespace ActivitiesJournal.Models;

public class TrackParseResult
{
    public DateTime StartedAt { get; set; }
    public TimeSpan Duration { get; set; }
    public double DistanceKm { get; set; }
    public int PointCount { get; set; }
    public List<GpxPoint> Points { get; set; } = new();
}
