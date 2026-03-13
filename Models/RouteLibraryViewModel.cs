namespace ActivitiesJournal.Models;

public class RouteGroup
{
    public int RouteId { get; set; }
    public string Label { get; set; } = string.Empty;      // most common name or auto-generated
    public double DistanceKm { get; set; }
    public double StartLat { get; set; }
    public double StartLng { get; set; }
    public int Count { get; set; }
    public DateTime FirstDate { get; set; }
    public DateTime LastDate { get; set; }
    public double AvgSpeedKmh { get; set; }
    public double BestSpeedKmh { get; set; }
    public double AvgPaceMinKm { get; set; }   // for walks
    public double BestPaceMinKm { get; set; }
    public double AvgElevationM { get; set; }
    public List<StravaActivity> Activities { get; set; } = [];
}

public class RouteLibraryViewModel
{
    public string ActivityType { get; set; } = "Ride";
    public string ActivityTypeLabel { get; set; } = "Rides";
    public bool IsWalk { get; set; }
    public List<RouteGroup> Routes { get; set; } = [];    // sorted by count desc
    public int TotalActivities { get; set; }
    public int GroupedActivities { get; set; }
}
