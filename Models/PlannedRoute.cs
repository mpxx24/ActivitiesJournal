namespace ActivitiesJournal.Models;

public class WaypointDto
{
    public double Lat { get; set; }
    public double Lon { get; set; }
}

public class PlannedRoute
{
    public string Id { get; set; } = string.Empty;
    public long AthleteId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public double DistanceKm { get; set; }
    public List<WaypointDto> Waypoints { get; set; } = [];
}
