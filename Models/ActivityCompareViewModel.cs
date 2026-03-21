namespace ActivitiesJournal.Models;

public class ActivityCompareViewModel
{
    public StravaActivity? Strava { get; set; }
    public TrackSummary? Track { get; set; }
    public IReadOnlyList<GpxPoint> TrackPoints { get; set; } = Array.Empty<GpxPoint>();

    public bool HasBoth => Strava != null && Track != null;

    public double StravaDistanceKm => Strava != null ? Strava.Distance / 1000.0 : 0;
    public TimeSpan StravaDuration => Strava != null ? TimeSpan.FromSeconds(Strava.MovingTime) : TimeSpan.Zero;
    public double StravaAvgSpeedKmh => StravaDuration.TotalHours > 0 ? StravaDistanceKm / StravaDuration.TotalHours : 0;

    public double TrackAvgSpeedKmh => Track != null && Track.Duration.TotalHours > 0
        ? Track.DistanceKm / Track.Duration.TotalHours
        : 0;

    public double DistanceDeltaKm => HasBoth ? Track!.DistanceKm - StravaDistanceKm : 0;
    public TimeSpan DurationDelta => HasBoth ? Track!.Duration - StravaDuration : TimeSpan.Zero;
    public double SpeedDeltaKmh => HasBoth ? TrackAvgSpeedKmh - StravaAvgSpeedKmh : 0;
}
