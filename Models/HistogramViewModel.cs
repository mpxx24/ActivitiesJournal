namespace ActivitiesJournal.Models;

public class HistogramBucket
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public double TotalDistanceKm { get; set; }
}

public class HistogramViewModel
{
    public string ActivityType { get; set; } = "Ride";
    public string ActivityTypeLabel { get; set; } = "Rides";
    public int TotalActivities { get; set; }

    // Distance buckets
    public List<HistogramBucket> DistanceBuckets { get; set; } = [];

    // Duration buckets
    public List<HistogramBucket> DurationBuckets { get; set; } = [];

    // Elevation buckets
    public List<HistogramBucket> ElevationBuckets { get; set; } = [];
}
