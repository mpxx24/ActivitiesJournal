namespace ActivitiesJournal.Models;

public class GpxPoint
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Elevation { get; set; }
    public DateTime? Time { get; set; }
}
