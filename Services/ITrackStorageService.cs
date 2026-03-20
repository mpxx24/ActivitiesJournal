using ActivitiesJournal.Models;

namespace ActivitiesJournal.Services;

public interface ITrackStorageService
{
    Task<TrackSummary> UploadTrackAsync(Stream gpxStream, TrackSummary summary, CancellationToken ct = default);
    Task<IReadOnlyList<TrackSummary>> ListTracksAsync(CancellationToken ct = default);
    Task<TrackSummary?> GetTrackSummaryAsync(string id, CancellationToken ct = default);
    Task<Stream> GetTrackGpxAsync(string id, CancellationToken ct = default);
}
