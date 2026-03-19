namespace ActivitiesJournal.Models;

public class DashboardViewModel
{
    public List<StravaActivity> RecentActivities { get; set; } = new();
    public double WeekRideKm { get; set; }
    public int WeekRideCount { get; set; }
    public double WeekWalkKm { get; set; }
    public int WeekWalkCount { get; set; }
    public int RideStreak { get; set; }
    public double YtdRideKm { get; set; }
    public int YtdRideCount { get; set; }
    public double YtdWalkKm { get; set; }
    public int YtdWalkCount { get; set; }
    public List<double> WeeklyKm { get; set; } = new();
}
