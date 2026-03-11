using System.Text.Json.Serialization;

namespace ActivitiesJournal.Models;

public class StravaActivity
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("distance")]
    public float Distance { get; set; }

    [JsonPropertyName("moving_time")]
    public int MovingTime { get; set; }

    [JsonPropertyName("elapsed_time")]
    public int ElapsedTime { get; set; }

    [JsonPropertyName("total_elevation_gain")]
    public float TotalElevationGain { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("sport_type")]
    public string SportType { get; set; } = string.Empty;

    [JsonPropertyName("start_date")]
    public DateTime StartDate { get; set; }

    [JsonPropertyName("start_date_local")]
    public DateTime StartDateLocal { get; set; }

    [JsonPropertyName("timezone")]
    public string Timezone { get; set; } = string.Empty;

    [JsonPropertyName("utc_offset")]
    public float UtcOffset { get; set; }

    [JsonPropertyName("location_city")]
    public string? LocationCity { get; set; }

    [JsonPropertyName("location_state")]
    public string? LocationState { get; set; }

    [JsonPropertyName("location_country")]
    public string? LocationCountry { get; set; }

    [JsonPropertyName("achievement_count")]
    public int AchievementCount { get; set; }

    [JsonPropertyName("kudos_count")]
    public int KudosCount { get; set; }

    [JsonPropertyName("comment_count")]
    public int CommentCount { get; set; }

    [JsonPropertyName("athlete_count")]
    public int AthleteCount { get; set; }

    [JsonPropertyName("photo_count")]
    public int PhotoCount { get; set; }

    [JsonPropertyName("trainer")]
    public bool Trainer { get; set; }

    [JsonPropertyName("commute")]
    public bool Commute { get; set; }

    [JsonPropertyName("manual")]
    public bool Manual { get; set; }

    [JsonPropertyName("private")]
    public bool Private { get; set; }

    [JsonPropertyName("visibility")]
    public string Visibility { get; set; } = string.Empty;

    [JsonPropertyName("flagged")]
    public bool Flagged { get; set; }

    [JsonPropertyName("gear_id")]
    public string? GearId { get; set; }

    [JsonPropertyName("start_latlng")]
    public List<float>? StartLatlng { get; set; }

    [JsonPropertyName("end_latlng")]
    public List<float>? EndLatlng { get; set; }

    [JsonPropertyName("average_speed")]
    public float AverageSpeed { get; set; }

    [JsonPropertyName("max_speed")]
    public float MaxSpeed { get; set; }

    [JsonPropertyName("average_cadence")]
    public float? AverageCadence { get; set; }

    [JsonPropertyName("average_watts")]
    public float? AverageWatts { get; set; }

    [JsonPropertyName("weighted_average_watts")]
    public int? WeightedAverageWatts { get; set; }

    [JsonPropertyName("kilojoules")]
    public float? Kilojoules { get; set; }

    [JsonPropertyName("device_watts")]
    public bool DeviceWatts { get; set; }

    [JsonPropertyName("has_heartrate")]
    public bool HasHeartrate { get; set; }

    [JsonPropertyName("average_heartrate")]
    public float? AverageHeartrate { get; set; }

    [JsonPropertyName("max_heartrate")]
    public int? MaxHeartrate { get; set; }

    [JsonPropertyName("heartrate_opt_out")]
    public bool HeartrateOptOut { get; set; }

    [JsonPropertyName("display_hide_heartrate_option")]
    public bool DisplayHideHeartrateOption { get; set; }

    [JsonPropertyName("elev_high")]
    public float? ElevHigh { get; set; }

    [JsonPropertyName("elev_low")]
    public float? ElevLow { get; set; }

    [JsonPropertyName("upload_id")]
    public long? UploadId { get; set; }

    [JsonPropertyName("upload_id_str")]
    public string? UploadIdStr { get; set; }

    [JsonPropertyName("external_id")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("from_accepted_tag")]
    public bool FromAcceptedTag { get; set; }

    [JsonPropertyName("pr_count")]
    public int PrCount { get; set; }

    [JsonPropertyName("total_photo_count")]
    public int TotalPhotoCount { get; set; }

    [JsonPropertyName("has_kudoed")]
    public bool HasKudoed { get; set; }

    [JsonPropertyName("workout_type")]
    public int? WorkoutType { get; set; }

    [JsonPropertyName("suffer_score")]
    public int? SufferScore { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("calories")]
    public float? Calories { get; set; }

    [JsonPropertyName("segment_efforts")]
    public List<SegmentEffort>? SegmentEfforts { get; set; }

    [JsonPropertyName("splits_metric")]
    public List<Split>? SplitsMetric { get; set; }

    [JsonPropertyName("splits_standard")]
    public List<Split>? SplitsStandard { get; set; }

    [JsonPropertyName("laps")]
    public List<Lap>? Laps { get; set; }

    [JsonPropertyName("best_efforts")]
    public List<BestEffort>? BestEfforts { get; set; }

    [JsonPropertyName("gear")]
    public Gear? Gear { get; set; }

    [JsonPropertyName("photos")]
    public Photos? Photos { get; set; }

    [JsonPropertyName("map")]
    public Map? Map { get; set; }

    [JsonPropertyName("athlete")]
    public Athlete? Athlete { get; set; }
}

public class SegmentEffort
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("elapsed_time")]
    public int ElapsedTime { get; set; }

    [JsonPropertyName("distance")]
    public float Distance { get; set; }

    [JsonPropertyName("start_index")]
    public int StartIndex { get; set; }

    [JsonPropertyName("end_index")]
    public int EndIndex { get; set; }

    [JsonPropertyName("average_cadence")]
    public float? AverageCadence { get; set; }

    [JsonPropertyName("average_watts")]
    public float? AverageWatts { get; set; }

    [JsonPropertyName("device_watts")]
    public bool DeviceWatts { get; set; }

    [JsonPropertyName("average_heartrate")]
    public float? AverageHeartrate { get; set; }

    [JsonPropertyName("max_heartrate")]
    public int? MaxHeartrate { get; set; }

    [JsonPropertyName("segment")]
    public Segment? Segment { get; set; }

    [JsonPropertyName("kom_rank")]
    public int? KomRank { get; set; }

    [JsonPropertyName("pr_rank")]
    public int? PrRank { get; set; }
}

public class Segment
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("activity_type")]
    public string ActivityType { get; set; } = string.Empty;

    [JsonPropertyName("distance")]
    public float Distance { get; set; }

    [JsonPropertyName("average_grade")]
    public float AverageGrade { get; set; }

    [JsonPropertyName("maximum_grade")]
    public float MaximumGrade { get; set; }

    [JsonPropertyName("elevation_high")]
    public float ElevationHigh { get; set; }

    [JsonPropertyName("elevation_low")]
    public float ElevationLow { get; set; }

    [JsonPropertyName("start_latlng")]
    public List<float>? StartLatlng { get; set; }

    [JsonPropertyName("end_latlng")]
    public List<float>? EndLatlng { get; set; }
}

public class Split
{
    [JsonPropertyName("distance")]
    public float Distance { get; set; }

    [JsonPropertyName("elapsed_time")]
    public int ElapsedTime { get; set; }

    [JsonPropertyName("elevation_difference")]
    public float ElevationDifference { get; set; }

    [JsonPropertyName("moving_time")]
    public int MovingTime { get; set; }

    [JsonPropertyName("split")]
    public int SplitNumber { get; set; }

    [JsonPropertyName("average_speed")]
    public float AverageSpeed { get; set; }

    [JsonPropertyName("average_heartrate")]
    public float? AverageHeartrate { get; set; }
}

public class Lap
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("distance")]
    public float Distance { get; set; }

    [JsonPropertyName("moving_time")]
    public int MovingTime { get; set; }

    [JsonPropertyName("elapsed_time")]
    public int ElapsedTime { get; set; }

    [JsonPropertyName("total_elevation_gain")]
    public float TotalElevationGain { get; set; }

    [JsonPropertyName("average_speed")]
    public float AverageSpeed { get; set; }

    [JsonPropertyName("max_speed")]
    public float MaxSpeed { get; set; }

    [JsonPropertyName("average_cadence")]
    public float? AverageCadence { get; set; }

    [JsonPropertyName("average_watts")]
    public float? AverageWatts { get; set; }

    [JsonPropertyName("average_heartrate")]
    public float? AverageHeartrate { get; set; }

    [JsonPropertyName("max_heartrate")]
    public int? MaxHeartrate { get; set; }

    [JsonPropertyName("lap_index")]
    public int LapIndex { get; set; }

    [JsonPropertyName("split")]
    public int Split { get; set; }
}

public class BestEffort
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("elapsed_time")]
    public int ElapsedTime { get; set; }

    [JsonPropertyName("distance")]
    public float Distance { get; set; }

    [JsonPropertyName("start_index")]
    public int StartIndex { get; set; }

    [JsonPropertyName("end_index")]
    public int EndIndex { get; set; }

    [JsonPropertyName("pr_rank")]
    public int? PrRank { get; set; }
}

public class Gear
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("primary")]
    public bool Primary { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("distance")]
    public float Distance { get; set; }
}

public class Photos
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("primary")]
    public Photo? Primary { get; set; }
}

public class Photo
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("unique_id")]
    public string UniqueId { get; set; } = string.Empty;

    [JsonPropertyName("urls")]
    public Dictionary<string, string> Urls { get; set; } = new();

    [JsonPropertyName("source")]
    public int Source { get; set; }
}

public class Map
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("summary_polyline")]
    public string? SummaryPolyline { get; set; }

    [JsonPropertyName("resource_state")]
    public int ResourceState { get; set; }
}

public class Athlete
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("resource_state")]
    public int ResourceState { get; set; }
}
