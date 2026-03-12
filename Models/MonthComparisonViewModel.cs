namespace ActivitiesJournal.Models;

public class MonthStats
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName => new DateTime(Year, Month, 1).ToString("MMMM yyyy");
    public int RideCount { get; set; }
    public double DistanceKm { get; set; }
    public double ElevationM { get; set; }
    public TimeSpan MovingTime { get; set; }
    public double AvgSpeedKmh { get; set; }
}

public class MonthComparisonViewModel
{
    public List<MonthStats> MonthlyStats { get; set; } = new();
    public int SelectedYear { get; set; }
    public List<int> AvailableYears { get; set; } = new();

    // Monthly data grouped by month number (1–12) for the current vs prior year chart
    public int CompareYear { get; set; }
    public MonthStats[] CurrentYearByMonth { get; set; } = new MonthStats[13];
    public MonthStats[] PriorYearByMonth { get; set; } = new MonthStats[13];
}
