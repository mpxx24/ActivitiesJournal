using ActivitiesJournal.Configuration;
using ActivitiesJournal.Models;
using Microsoft.Extensions.Options;

namespace ActivitiesJournal.Services;

public class TrackLinkService : ITrackLinkService
{
    private readonly ITrackStorageService _trackStorage;
    private readonly TrackOwnerOptions _ownerOptions;
    private readonly ILogger<TrackLinkService> _logger;

    public TrackLinkService(
        ITrackStorageService trackStorage,
        IOptions<TrackOwnerOptions> ownerOptions,
        ILogger<TrackLinkService> logger)
    {
        _trackStorage = trackStorage;
        _ownerOptions = ownerOptions.Value;
        _logger = logger;
    }

    public async Task<int> ReconcileAsync(long athleteId, IReadOnlyList<StravaActivity> activities, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(activities);

        // Track uploads are always stored under the owner's athlete id, and only the owner's
        // Strava feed can contain track-* external ids. Skip everyone else — no blob I/O.
        var ownerId = _ownerOptions.OwnerAthleteId;
        if (athleteId != ownerId || activities.Count == 0)
            return 0;

        var linked = 0;
        foreach (var activity in activities)
        {
            if (!TrackExternalId.TryParseTrackId(activity.ExternalId, out var trackId))
                continue;

            try
            {
                var summary = await _trackStorage.GetTrackSummaryAsync(ownerId, trackId, ct);
                if (summary == null)
                {
                    _logger.LogInformation(
                        "Synced Strava activity {StravaId} references track {TrackId} but no local track metadata was found",
                        activity.Id, trackId);
                    continue;
                }

                if (summary.StravaActivityId == activity.Id)
                    continue; // already linked — idempotent, nothing to write

                summary.StravaActivityId = activity.Id;
                await _trackStorage.UpdateTrackSummaryAsync(summary, ownerId, ct);
                linked++;

                _logger.LogInformation("Linked track {TrackId} to synced Strava activity {StravaId}",
                    trackId, activity.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to link track {TrackId} to Strava activity {StravaId}",
                    trackId, activity.Id);
            }
        }

        if (linked > 0)
            _logger.LogInformation("Linked {Count} track(s) to synced Strava activities for athlete {AthleteId}",
                linked, ownerId);

        return linked;
    }
}
