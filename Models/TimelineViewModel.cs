namespace ActivitiesJournal.Models;

public class TimelineWeekPoint
{
    public DateTime WeekStart { get; set; }
    public string Label { get; set; } = string.Empty;   // e.g. "Mar 10"
    public double DistanceKm { get; set; }
    public double TimeHours { get; set; }
    public int Count { get; set; }
}

public class TimelineMonthPoint
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Label { get; set; } = string.Empty;   // e.g. "Mar 2025"
    public double DistanceKm { get; set; }
    public double TimeHours { get; set; }
    public int Count { get; set; }
}

public class TimelineViewModel
{
    public string ActivityType { get; set; } = "Ride";
    public string ActivityTypeLabel { get; set; } = "Rides";
    public List<TimelineWeekPoint> Weeks { get; set; } = [];   // last 26 weeks
    public List<TimelineMonthPoint> Months { get; set; } = []; // last 18 months
}
