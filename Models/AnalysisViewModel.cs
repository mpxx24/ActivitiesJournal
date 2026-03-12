namespace ActivitiesJournal.Models;

public enum RideType
{
    Recovery,    // short, low speed
    Endurance,   // moderate distance, moderate speed
    Tempo,       // higher speed or effort
    Race,        // high speed, relatively high distance
    Epic,        // very long distance
}

public class ClassifiedRide
{
    public StravaActivity Activity { get; set; } = null!;
    public RideType RideType { get; set; }
    public string TypeLabel => RideType.ToString();
    public string TypeColor => RideType switch
    {
        RideType.Recovery  => "secondary",
        RideType.Endurance => "info",
        RideType.Tempo     => "warning",
        RideType.Race      => "danger",
        RideType.Epic      => "success",
        _                  => "secondary",
    };
}

public class SpeedZoneData
{
    public string Label { get; set; } = string.Empty;
    public double MinKmh { get; set; }
    public double MaxKmh { get; set; }
    public int RideCount { get; set; }       // rides whose avg speed falls in this zone
    public double TotalDistanceKm { get; set; }
    public double TotalTimeHrs { get; set; }
    public string Color { get; set; } = string.Empty;
}

public class AnalysisViewModel
{
    public int Year { get; set; }
    public List<int> AvailableYears { get; set; } = new();
    public List<ClassifiedRide> ClassifiedRides { get; set; } = new();
    public Dictionary<RideType, int> TypeCounts { get; set; } = new();
    public List<SpeedZoneData> SpeedZones { get; set; } = new();
}
