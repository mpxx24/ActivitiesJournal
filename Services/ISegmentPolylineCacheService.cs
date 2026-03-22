namespace ActivitiesJournal.Services;

public interface ISegmentPolylineCacheService
{
    /// <summary>Returns the cached polyline for the given athlete and segment, or null if not yet persisted.</summary>
    Task<string?> GetAsync(long athleteId, long segmentId);

    /// <summary>Persists a successfully fetched polyline. Only call with non-null values.</summary>
    Task SetAsync(long athleteId, long segmentId, string polyline);
}
