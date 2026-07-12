using ActivitiesJournal.Models;

namespace ActivitiesJournal.Services;

public interface ITrackStorageService
{
    Task<TrackSummary> UploadTrackAsync(Stream gpxStream, TrackSummary summary, long athleteId, CancellationToken ct = default);
    Task UpdateTrackSummaryAsync(TrackSummary summary, long athleteId, CancellationToken ct = default);
    Task<IReadOnlyList<TrackSummary>> ListTracksAsync(long athleteId, CancellationToken ct = default);
    Task<TrackSummary?> GetTrackSummaryAsync(long athleteId, string id, CancellationToken ct = default);
    Task<Stream> GetTrackGpxAsync(long athleteId, string id, CancellationToken ct = default);
    Task DeleteTrackAsync(long athleteId, string id, CancellationToken ct = default);
    Task MigrateToAthletePathsAsync(long ownerAthleteId, CancellationToken ct = default);
}
