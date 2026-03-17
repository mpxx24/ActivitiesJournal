namespace ActivitiesJournal.Models;

public class DistanceProjectionPoint
{
    public string Date { get; set; } = "";
    public double Km { get; set; }
}

public class DistanceProjectionViewModel
{
    public string ActivityType { get; set; } = "Ride";
    public string ActivityTypeLabel { get; set; } = "Rides";
    public int CurrentYear { get; set; } = DateTime.Now.Year;

    public List<DistanceProjectionPoint> Actual { get; set; } = [];
    public List<DistanceProjectionPoint> Projection { get; set; } = [];
    public List<DistanceProjectionPoint> UpperBand { get; set; } = [];
    public List<DistanceProjectionPoint> LowerBand { get; set; } = [];
    public List<DistanceProjectionPoint> LastYear { get; set; } = [];

    public double CurrentTotalKm { get; set; }
    public double ProjectedTotalKm { get; set; }
    public double UpperProjectedKm { get; set; }
    public double LowerProjectedKm { get; set; }
    public double LastYearTotalKm { get; set; }
    public double AvgDailyKm { get; set; }
    public int DaysRemaining { get; set; }
    public int ProjectionWindowDays { get; set; } = 30;
}
