using ActivitiesJournal.Models;

namespace ActivitiesJournal.Services;

public interface ITrackLinkService
{
    /// <summary>
    /// Links freshly-synced Strava activities back to the local Track upload they originated
    /// from, by matching Strava's <c>external_id</c> (<c>track-&lt;trackId&gt;</c>). Non-destructive:
    /// only backfills <see cref="TrackSummary.StravaActivityId"/>; never deletes or skips either copy.
    /// Returns the number of tracks newly linked.
    /// </summary>
    Task<int> ReconcileAsync(long athleteId, IReadOnlyList<StravaActivity> activities, CancellationToken ct = default);
}
