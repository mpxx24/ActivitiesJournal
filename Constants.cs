using ActivitiesJournal.Models;

namespace ActivitiesJournal;

public static class SportTypes
{
    public static readonly string[] Ride = { "Ride", "VirtualRide", "GravelRide", "MountainBikeRide" };
    public static readonly string[] Walk = { "Walk", "Hike", "VirtualWalk" };
    public static readonly string[] Run = { "Run", "VirtualRun", "TrailRun" };
    public static readonly string[] Swim = { "Swim" };

    public static bool IsRide(string sportType) => Ride.Contains(sportType);
    public static bool IsWalk(string sportType) => Walk.Contains(sportType);
    public static bool IsRun(string sportType) => Run.Contains(sportType);
    public static bool IsSwim(string sportType) => Swim.Contains(sportType);

    public static List<StravaActivity> FilterByType(List<StravaActivity> activities, string type) => type switch
    {
        "Walk" => activities.Where(a => IsWalk(a.SportType)).ToList(),
        "Run" => activities.Where(a => IsRun(a.SportType)).ToList(),
        "Swim" => activities.Where(a => IsSwim(a.SportType)).ToList(),
        "All" => activities,
        _ => activities.Where(a => IsRide(a.SportType)).ToList(),
    };

    /// Strava's upload endpoint takes legacy lowercase activity types, not SportType.
    public static string ToStravaUploadType(ActivityType type) => type switch
    {
        ActivityType.Walk => "walk",
        ActivityType.Run => "run",
        ActivityType.Swim => "swim",
        ActivityType.Football => "soccer",
        _ => "ride",
    };

    public static string TypeLabel(string type) => type switch
    {
        "Walk" => "Walks & Hikes",
        "Run" => "Runs",
        "Swim" => "Swims",
        "All" => "All Activities",
        _ => "Rides",
    };
}
