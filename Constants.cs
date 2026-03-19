using ActivitiesJournal.Models;

namespace ActivitiesJournal;

public static class SportTypes
{
    public static readonly string[] Ride = { "Ride", "VirtualRide", "GravelRide", "MountainBikeRide" };
    public static readonly string[] Walk = { "Walk", "Hike", "VirtualWalk" };

    public static bool IsRide(string sportType) => Ride.Contains(sportType);
    public static bool IsWalk(string sportType) => Walk.Contains(sportType);

    public static List<StravaActivity> FilterByType(List<StravaActivity> activities, string type) => type switch
    {
        "Walk" => activities.Where(a => IsWalk(a.SportType)).ToList(),
        "All" => activities,
        _ => activities.Where(a => IsRide(a.SportType)).ToList(),
    };

    public static string TypeLabel(string type) => type switch
    {
        "Walk" => "Walks & Hikes",
        "All" => "All Activities",
        _ => "Rides",
    };
}
