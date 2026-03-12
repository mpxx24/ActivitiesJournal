namespace ActivitiesJournal.Models;

public class SegmentBestTime
{
    public long SegmentId { get; set; }
    public string SegmentName { get; set; } = string.Empty;
    public float DistanceM { get; set; }
    public float AverageGrade { get; set; }
    public int BestElapsedSeconds { get; set; }
    public int? BestPrRank { get; set; }
    public DateTime BestDate { get; set; }
    public long BestActivityId { get; set; }
    public string BestActivityName { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public List<SegmentAttempt> AllAttempts { get; set; } = new();
    public double? StartLat { get; set; }
    public double? StartLng { get; set; }
    public double? EndLat { get; set; }
    public double? EndLng { get; set; }
    public string? Polyline { get; set; }
}

public class SegmentAttempt
{
    public DateTime Date { get; set; }
    public int ElapsedSeconds { get; set; }
    public long ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public int? PrRank { get; set; }
}

public class SegmentsViewModel
{
    public List<SegmentBestTime> Segments { get; set; } = new();
    public int RidesFetched { get; set; }
    public int TotalRidesAvailable { get; set; }
}

public class BestEffortsViewModel
{
    public List<BestEffortRow> Rows { get; set; } = new();
    public int RidesFetched { get; set; }
}

public class BestEffortRow
{
    public string DistanceName { get; set; } = string.Empty;
    public float DistanceM { get; set; }
    public int BestElapsedSeconds { get; set; }
    public DateTime BestDate { get; set; }
    public long BestActivityId { get; set; }
    public string BestActivityName { get; set; } = string.Empty;
    public List<(DateTime Date, int Seconds, long ActivityId, string ActivityName)> History { get; set; } = new();
}
