using System.Text;
using ActivitiesJournal.Configuration;
using ActivitiesJournal.Controllers;
using ActivitiesJournal.Models;
using ActivitiesJournal.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ActivitiesJournal.Tests;

[TestFixture]
public class TracksControllerUploadTests
{
    private const long OwnerAthleteId = 12345;
    private Mock<ITrackStorageService> _storage = null!;
    private Mock<ITrackParserService> _parser = null!;
    private Mock<IStravaService> _strava = null!;
    private TracksController _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _storage = new Mock<ITrackStorageService>();
        _parser = new Mock<ITrackParserService>();
        _strava = new Mock<IStravaService>();

        _parser.Setup(p => p.Parse(It.IsAny<Stream>())).Returns(new TrackParseResult
        {
            StartedAt = new DateTime(2026, 7, 11, 10, 0, 0),
            Duration = TimeSpan.FromMinutes(30),
            DistanceKm = 5.0,
            PointCount = 100
        });

        var ownerOptions = Options.Create(new TrackOwnerOptions
        {
            OwnerAthleteId = OwnerAthleteId,
            UploadApiKey = "key"
        });

        _sut = new TracksController(
            _storage.Object,
            _parser.Object,
            _strava.Object,
            ownerOptions,
            NullLogger<TracksController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [TearDown]
    public void TearDown() => _sut.Dispose();

    private static IFormFile GpxFile()
    {
        var bytes = Encoding.UTF8.GetBytes("<gpx></gpx>");
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "gpxFile", "activity.gpx");
    }

    [Test]
    public async Task Upload_WithoutStravaFlag_NeverCallsStrava()
    {
        var result = await _sut.Upload(GpxFile(), ActivityType.Ride, uploadToStrava: false, CancellationToken.None);

        Assert.That(result, Is.InstanceOf<JsonResult>());
        _strava.Verify(s => s.UploadActivityAsync(
            It.IsAny<long>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<ActivityType>(),
            It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _storage.Verify(s => s.UploadTrackAsync(
            It.IsAny<Stream>(), It.IsAny<TrackSummary>(), OwnerAthleteId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Upload_WithStravaFlag_UploadsForOwnerAndStoresStravaActivityId()
    {
        _strava.Setup(s => s.UploadActivityAsync(
                OwnerAthleteId, It.IsAny<Stream>(), It.IsAny<string>(), ActivityType.Run,
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StravaUploadResult { Success = true, StravaActivityId = 456 });

        TrackSummary? stored = null;
        _storage.Setup(s => s.UploadTrackAsync(
                It.IsAny<Stream>(), It.IsAny<TrackSummary>(), OwnerAthleteId, It.IsAny<CancellationToken>()))
            .Callback<Stream, TrackSummary, long, CancellationToken>((_, sum, _, _) => stored = sum)
            .ReturnsAsync((Stream _, TrackSummary sum, long _, CancellationToken _) => sum);

        await _sut.Upload(GpxFile(), ActivityType.Run, uploadToStrava: true, CancellationToken.None);

        Assert.That(stored, Is.Not.Null);
        Assert.That(stored!.StravaActivityId, Is.EqualTo(456));
        Assert.That(stored.StravaUploadStatus, Is.EqualTo("uploaded"));
        _strava.Verify(s => s.UploadActivityAsync(
            OwnerAthleteId, It.IsAny<Stream>(), It.IsAny<string>(), ActivityType.Run,
            It.IsAny<string?>(), It.Is<string>(e => e.StartsWith("track-")),
            It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Upload_StravaFails_TrackIsStillStored()
    {
        _strava.Setup(s => s.UploadActivityAsync(
                It.IsAny<long>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<ActivityType>(),
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StravaUploadResult { Success = false, Error = "rate limited" });

        TrackSummary? stored = null;
        _storage.Setup(s => s.UploadTrackAsync(
                It.IsAny<Stream>(), It.IsAny<TrackSummary>(), OwnerAthleteId, It.IsAny<CancellationToken>()))
            .Callback<Stream, TrackSummary, long, CancellationToken>((_, sum, _, _) => stored = sum)
            .ReturnsAsync((Stream _, TrackSummary sum, long _, CancellationToken _) => sum);

        var result = await _sut.Upload(GpxFile(), ActivityType.Ride, uploadToStrava: true, CancellationToken.None);

        Assert.That(result, Is.InstanceOf<JsonResult>());
        Assert.That(stored, Is.Not.Null);
        Assert.That(stored!.StravaActivityId, Is.Null);
        Assert.That(stored.StravaUploadStatus, Does.StartWith("failed"));
    }

    [Test]
    public async Task Upload_StravaDuplicate_ReportsDuplicateStatus()
    {
        _strava.Setup(s => s.UploadActivityAsync(
                It.IsAny<long>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<ActivityType>(),
                It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StravaUploadResult { Success = false, Duplicate = true, Error = "duplicate of 999" });

        TrackSummary? stored = null;
        _storage.Setup(s => s.UploadTrackAsync(
                It.IsAny<Stream>(), It.IsAny<TrackSummary>(), OwnerAthleteId, It.IsAny<CancellationToken>()))
            .Callback<Stream, TrackSummary, long, CancellationToken>((_, sum, _, _) => stored = sum)
            .ReturnsAsync((Stream _, TrackSummary sum, long _, CancellationToken _) => sum);

        await _sut.Upload(GpxFile(), ActivityType.Ride, uploadToStrava: true, CancellationToken.None);

        Assert.That(stored!.StravaUploadStatus, Is.EqualTo("duplicate"));
    }
}
