using ActivitiesJournal.Configuration;
using ActivitiesJournal.Models;
using ActivitiesJournal.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ActivitiesJournal.Tests;

[TestFixture]
public class TrackLinkServiceTests
{
    private const long OwnerAthleteId = 12345;
    private Mock<ITrackStorageService> _storage = null!;
    private TrackLinkService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _storage = new Mock<ITrackStorageService>();
        var ownerOptions = Options.Create(new TrackOwnerOptions
        {
            OwnerAthleteId = OwnerAthleteId,
            UploadApiKey = "key"
        });
        _sut = new TrackLinkService(_storage.Object, ownerOptions, NullLogger<TrackLinkService>.Instance);
    }

    private static StravaActivity Activity(long id, string? externalId) =>
        new() { Id = id, ExternalId = externalId };

    private static TrackSummary Track(string id, long? stravaActivityId = null) =>
        new() { Id = id, StravaActivityId = stravaActivityId };

    [Test]
    public async Task ReconcileAsync_MatchingTrack_BackfillsStravaActivityId()
    {
        var track = Track("abc");
        _storage.Setup(s => s.GetTrackSummaryAsync(OwnerAthleteId, "abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(track);

        var linked = await _sut.ReconcileAsync(OwnerAthleteId, new[] { Activity(456, "track-abc") });

        Assert.That(linked, Is.EqualTo(1));
        Assert.That(track.StravaActivityId, Is.EqualTo(456));
        _storage.Verify(s => s.UpdateTrackSummaryAsync(
            It.Is<TrackSummary>(t => t.Id == "abc" && t.StravaActivityId == 456),
            OwnerAthleteId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ReconcileAsync_AlreadyLinked_DoesNotRewrite()
    {
        _storage.Setup(s => s.GetTrackSummaryAsync(OwnerAthleteId, "abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Track("abc", stravaActivityId: 456));

        var linked = await _sut.ReconcileAsync(OwnerAthleteId, new[] { Activity(456, "track-abc") });

        Assert.That(linked, Is.EqualTo(0));
        _storage.Verify(s => s.UpdateTrackSummaryAsync(
            It.IsAny<TrackSummary>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ReconcileAsync_NoLocalTrack_DoesNotThrowOrWrite()
    {
        _storage.Setup(s => s.GetTrackSummaryAsync(OwnerAthleteId, "missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrackSummary?)null);

        var linked = await _sut.ReconcileAsync(OwnerAthleteId, new[] { Activity(456, "track-missing") });

        Assert.That(linked, Is.EqualTo(0));
        _storage.Verify(s => s.UpdateTrackSummaryAsync(
            It.IsAny<TrackSummary>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ReconcileAsync_NonTrackExternalId_IsIgnored()
    {
        var linked = await _sut.ReconcileAsync(OwnerAthleteId,
            new[] { Activity(1, "garmin-999"), Activity(2, null) });

        Assert.That(linked, Is.EqualTo(0));
        _storage.Verify(s => s.GetTrackSummaryAsync(
            It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ReconcileAsync_NonOwnerAthlete_SkipsWithoutStorageAccess()
    {
        var linked = await _sut.ReconcileAsync(OwnerAthleteId + 1, new[] { Activity(456, "track-abc") });

        Assert.That(linked, Is.EqualTo(0));
        _storage.Verify(s => s.GetTrackSummaryAsync(
            It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ReconcileAsync_EmptyList_ReturnsZero()
    {
        var linked = await _sut.ReconcileAsync(OwnerAthleteId, Array.Empty<StravaActivity>());
        Assert.That(linked, Is.EqualTo(0));
    }

    [Test]
    public async Task ReconcileAsync_MultipleActivities_LinksEachMatch()
    {
        var t1 = Track("a");
        var t2 = Track("b");
        _storage.Setup(s => s.GetTrackSummaryAsync(OwnerAthleteId, "a", It.IsAny<CancellationToken>())).ReturnsAsync(t1);
        _storage.Setup(s => s.GetTrackSummaryAsync(OwnerAthleteId, "b", It.IsAny<CancellationToken>())).ReturnsAsync(t2);

        var linked = await _sut.ReconcileAsync(OwnerAthleteId, new[]
        {
            Activity(10, "track-a"),
            Activity(20, "track-b"),
            Activity(30, "manual-entry"),
        });

        Assert.That(linked, Is.EqualTo(2));
        Assert.That(t1.StravaActivityId, Is.EqualTo(10));
        Assert.That(t2.StravaActivityId, Is.EqualTo(20));
    }

    [Test]
    public async Task ReconcileAsync_StorageThrows_ContinuesAndDoesNotBubble()
    {
        _storage.Setup(s => s.GetTrackSummaryAsync(OwnerAthleteId, "a", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("blob down"));
        var t2 = Track("b");
        _storage.Setup(s => s.GetTrackSummaryAsync(OwnerAthleteId, "b", It.IsAny<CancellationToken>())).ReturnsAsync(t2);

        var linked = await _sut.ReconcileAsync(OwnerAthleteId, new[]
        {
            Activity(10, "track-a"),
            Activity(20, "track-b"),
        });

        Assert.That(linked, Is.EqualTo(1));
        Assert.That(t2.StravaActivityId, Is.EqualTo(20));
    }
}
