namespace ActivitiesJournal.Models;

public class StravaUploadResult
{
    public bool Success { get; set; }
    public bool Duplicate { get; set; }
    public long? StravaActivityId { get; set; }
    public string? Error { get; set; }
}
