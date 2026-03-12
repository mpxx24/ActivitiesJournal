namespace ActivitiesJournal.Models;

public class PersonalRecord
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public StravaActivity? Activity { get; set; }
}

public class PersonalRecordsViewModel
{
    public List<PersonalRecord> AllTimeRecords { get; set; } = new();
    public List<StravaActivity> Top10Longest { get; set; } = new();
    public List<StravaActivity> Top10Fastest { get; set; } = new();
    public List<StravaActivity> Top10MostClimbing { get; set; } = new();
    public int TotalRides { get; set; }
    public double TotalDistanceKm { get; set; }
}
