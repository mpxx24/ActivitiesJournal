namespace ActivitiesJournal.Tests;

[TestFixture]
public class TrackExternalIdTests
{
    [Test]
    public void ForTrack_PrependsPrefix()
    {
        Assert.That(TrackExternalId.ForTrack("abc"), Is.EqualTo("track-abc"));
    }

    [Test]
    public void ForTrack_NullOrEmpty_Throws()
    {
        Assert.Catch<ArgumentException>(() => TrackExternalId.ForTrack(""));
        Assert.Catch<ArgumentException>(() => TrackExternalId.ForTrack(null!));
    }

    [Test]
    public void TryParseTrackId_ValidTrackExternalId_ReturnsTrueAndId()
    {
        var ok = TrackExternalId.TryParseTrackId("track-abc-123", out var id);
        Assert.That(ok, Is.True);
        Assert.That(id, Is.EqualTo("abc-123"));
    }

    [Test]
    public void TryParseTrackId_NonTrackPrefix_ReturnsFalse()
    {
        Assert.That(TrackExternalId.TryParseTrackId("garmin-1", out var id), Is.False);
        Assert.That(id, Is.Empty);
    }

    [Test]
    public void TryParseTrackId_NullOrEmpty_ReturnsFalse()
    {
        Assert.That(TrackExternalId.TryParseTrackId(null, out _), Is.False);
        Assert.That(TrackExternalId.TryParseTrackId("", out _), Is.False);
    }

    [Test]
    public void TryParseTrackId_PrefixOnly_ReturnsFalse()
    {
        Assert.That(TrackExternalId.TryParseTrackId("track-", out _), Is.False);
    }

    [Test]
    public void RoundTrip_ForTrackThenParse_YieldsOriginalId()
    {
        var ext = TrackExternalId.ForTrack("guid-42");
        Assert.That(TrackExternalId.TryParseTrackId(ext, out var id), Is.True);
        Assert.That(id, Is.EqualTo("guid-42"));
    }
}
