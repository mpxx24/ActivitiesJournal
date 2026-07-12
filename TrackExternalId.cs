namespace ActivitiesJournal;

/// <summary>
/// Formats and parses the Strava upload <c>external_id</c> that ties a Strava activity
/// back to the local Track it was uploaded from. Format: <c>track-&lt;trackId&gt;</c>.
/// </summary>
public static class TrackExternalId
{
    public const string Prefix = "track-";

    public static string ForTrack(string trackId)
    {
        ArgumentException.ThrowIfNullOrEmpty(trackId);
        return Prefix + trackId;
    }

    public static bool TryParseTrackId(string? externalId, out string trackId)
    {
        trackId = string.Empty;

        if (string.IsNullOrEmpty(externalId) || !externalId.StartsWith(Prefix, StringComparison.Ordinal))
            return false;

        var id = externalId[Prefix.Length..];
        if (string.IsNullOrEmpty(id))
            return false;

        trackId = id;
        return true;
    }
}
