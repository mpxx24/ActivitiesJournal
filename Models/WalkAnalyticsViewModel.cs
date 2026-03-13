namespace ActivitiesJournal.Models;

public class WalkAnalyticsViewModel
{
    public int Year { get; set; }
    public List<int> AvailableYears { get; set; } = [];

    // Totals
    public double TotalDistanceKm { get; set; }
    public TimeSpan TotalMovingTime { get; set; }
    public double TotalElevationM { get; set; }
    public int TotalWalks { get; set; }

    // Averages
    public double AvgDistanceKm { get; set; }
    public double AvgPaceSecPerKm { get; set; }  // seconds per km
    public double AvgElevationM { get; set; }

    // Records
    public StravaActivity? LongestWalk { get; set; }
    public StravaActivity? FastestPaceWalk { get; set; }   // best pace (min/km), min distance 3km
    public StravaActivity? MostElevationWalk { get; set; }
    public StravaActivity? LongestTimeWalk { get; set; }

    // Distribution by walk length category
    public int ShortWalks { get; set; }   // < 5 km
    public int MediumWalks { get; set; }  // 5–15 km
    public int LongWalks { get; set; }    // > 15 km

    // By hour of day (0–23) — how many walks started in that hour
    public int[] WalksByHour { get; set; } = new int[24];

    // By month (1–12)
    public double[] DistanceByMonth { get; set; } = new double[13]; // index 1–12

    // Walk type breakdown (Walk vs Hike)
    public int WalkCount { get; set; }
    public int HikeCount { get; set; }

    // Top 5 walks by distance
    public List<StravaActivity> Top5ByDistance { get; set; } = [];

    // Streaks (all-time, not year-filtered)
    public int CurrentStreakDays { get; set; }
    public int LongestStreakDays { get; set; }
    public DateTime LongestStreakStart { get; set; }
    public DateTime LongestStreakEnd { get; set; }
}
