namespace ActivitiesJournal.Models;

public class CalendarDayData
{
    public DateTime Date { get; set; }
    public int RideCount { get; set; }
    public double DistanceKm { get; set; }
    public double ElevationM { get; set; }
    public int Level { get; set; } // 0-4 intensity
}

public class CalendarViewModel
{
    public int Year { get; set; }
    public List<int> AvailableYears { get; set; } = new();
    public Dictionary<DateTime, CalendarDayData> DayData { get; set; } = new();
    public int TotalRides { get; set; }
    public double TotalDistanceKm { get; set; }
    public double TotalElevationM { get; set; }
    public int ActiveDays { get; set; }
}
