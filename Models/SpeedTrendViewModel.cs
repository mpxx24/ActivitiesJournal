namespace ActivitiesJournal.Models;

public class SpeedTrendPoint
{
    public DateTime Date { get; set; }
    public double Value { get; set; }   // km/h for rides, min/km (as decimal) for walks
    public double DistanceKm { get; set; }
    public long ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;
}

public class SpeedTrendViewModel
{
    public string ActivityType { get; set; } = "Ride";
    public string ActivityTypeLabel { get; set; } = "Rides";
    public bool IsWalk { get; set; }
    public string YAxisLabel { get; set; } = "km/h";
    public List<SpeedTrendPoint> Points { get; set; } = [];
    public List<SpeedTrendPoint> RollingAvg { get; set; } = [];  // 10-activity rolling avg
    public List<int> AvailableYears { get; set; } = [];
    public int? SelectedYear { get; set; }   // null = all time
}
