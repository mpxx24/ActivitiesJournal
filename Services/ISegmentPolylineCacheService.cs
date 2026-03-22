namespace ActivitiesJournal.Services;

public interface ISegmentPolylineCacheService
{
    /// <summary>Returns the cached polyline, or null if this segment has not been persisted yet.</summary>
    Task<string?> GetAsync(long segmentId);

    /// <summary>Persists a successfully fetched polyline. Only call with non-null values.</summary>
    Task SetAsync(long segmentId, string polyline);
}
