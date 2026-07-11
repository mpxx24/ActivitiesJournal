using System.Net;
using System.Security.Claims;
using ActivitiesJournal.Configuration;
using ActivitiesJournal.Models;
using ActivitiesJournal.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ActivitiesJournal.Tests;

[TestFixture]
public class StravaUploadTests
{
    private const long AthleteId = 42;
    private Mock<ITokenStore> _tokenStore = null!;
    private IMemoryCache _memoryCache = null!;
    private FakeHttpMessageHandler _httpHandler = null!;

    [SetUp]
    public void SetUp()
    {
        _tokenStore = new Mock<ITokenStore>();
        _tokenStore.Setup(t => t.Get(AthleteId)).Returns(("access_token", "refresh_token"));
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _httpHandler = new FakeHttpMessageHandler();
    }

    [TearDown]
    public void TearDown()
    {
        _memoryCache.Dispose();
        _httpHandler.Dispose();
    }

    private StravaService CreateSut()
    {
        var opts = Options.Create(new StravaOptions
        {
            ClientId = "123",
            ClientSecret = "secret",
            BaseUrl = "https://www.strava.com/api/v3/",
            AccessToken = "test_token",
            RefreshToken = "test_refresh"
        });

        var httpClient = new HttpClient(_httpHandler)
        {
            BaseAddress = new Uri("https://www.strava.com/api/v3/")
        };

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(a => a.HttpContext)
            .Returns(new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) });

        return new StravaService(
            opts,
            httpClient,
            _memoryCache,
            Mock.Of<ISegmentPolylineCacheService>(),
            Mock.Of<IActivityCacheService>(),
            _tokenStore.Object,
            httpContextAccessor.Object,
            NullLogger<StravaService>.Instance);
    }

    private static MemoryStream Gpx() => new("<gpx></gpx>"u8.ToArray());

    [Test]
    public async Task UploadActivityAsync_Success_PollsUntilActivityIdReady()
    {
        _httpHandler.Enqueue(HttpStatusCode.Created,
            """{"id": 111, "external_id": "track-abc", "error": null, "status": "processing", "activity_id": null}""");
        _httpHandler.Enqueue(HttpStatusCode.OK,
            """{"id": 111, "external_id": "track-abc", "error": null, "status": "processing", "activity_id": null}""");
        _httpHandler.Enqueue(HttpStatusCode.OK,
            """{"id": 111, "external_id": "track-abc", "error": null, "status": "ready", "activity_id": 456}""");

        var result = await CreateSut().UploadActivityAsync(
            AthleteId, Gpx(), "track-abc.gpx", ActivityType.Ride,
            "Recorded with Track", "track-abc", pollInterval: TimeSpan.Zero);

        Assert.That(result.Success, Is.True);
        Assert.That(result.StravaActivityId, Is.EqualTo(456));
        Assert.That(_httpHandler.RecordedUrls[0], Does.EndWith("uploads"));
        Assert.That(_httpHandler.RecordedUrls[1], Does.EndWith("uploads/111"));
    }

    [Test]
    public async Task UploadActivityAsync_SendsExternalIdTypeAndDescription()
    {
        _httpHandler.Enqueue(HttpStatusCode.Created,
            """{"id": 111, "error": null, "status": "ready", "activity_id": 456}""");

        await CreateSut().UploadActivityAsync(
            AthleteId, Gpx(), "track-abc.gpx", ActivityType.Football,
            "Recorded with Track", "track-abc", pollInterval: TimeSpan.Zero);

        var body = _httpHandler.RecordedBodies[0];
        Assert.That(body, Does.Contain("track-abc"));
        Assert.That(body, Does.Contain("soccer"));
        Assert.That(body, Does.Contain("Recorded with Track"));
        Assert.That(body, Does.Contain("gpx"));
    }

    [Test]
    public async Task UploadActivityAsync_Duplicate_ReportsDuplicateNotFailure()
    {
        _httpHandler.Enqueue(HttpStatusCode.Created,
            """{"id": 111, "error": null, "status": "processing", "activity_id": null}""");
        _httpHandler.Enqueue(HttpStatusCode.OK,
            """{"id": 111, "error": "track-abc.gpx duplicate of activity 999", "status": "error", "activity_id": null}""");

        var result = await CreateSut().UploadActivityAsync(
            AthleteId, Gpx(), "track-abc.gpx", ActivityType.Ride,
            null, "track-abc", pollInterval: TimeSpan.Zero);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Duplicate, Is.True);
    }

    [Test]
    public async Task UploadActivityAsync_ProcessingError_ReturnsFailure()
    {
        _httpHandler.Enqueue(HttpStatusCode.Created,
            """{"id": 111, "error": null, "status": "processing", "activity_id": null}""");
        _httpHandler.Enqueue(HttpStatusCode.OK,
            """{"id": 111, "error": "malformed GPX", "status": "error", "activity_id": null}""");

        var result = await CreateSut().UploadActivityAsync(
            AthleteId, Gpx(), "track-abc.gpx", ActivityType.Ride,
            null, "track-abc", pollInterval: TimeSpan.Zero);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Duplicate, Is.False);
        Assert.That(result.Error, Does.Contain("malformed"));
    }

    [Test]
    public async Task UploadActivityAsync_PostRejected_ReturnsFailure()
    {
        _httpHandler.Enqueue(HttpStatusCode.BadRequest, """{"message": "Bad Request"}""");

        var result = await CreateSut().UploadActivityAsync(
            AthleteId, Gpx(), "track-abc.gpx", ActivityType.Ride,
            null, "track-abc", pollInterval: TimeSpan.Zero);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.Not.Null);
    }

    [Test]
    public async Task UploadActivityAsync_NoToken_FailsWithoutHttpCall()
    {
        _tokenStore.Setup(t => t.Get(AthleteId)).Returns((string.Empty, string.Empty));

        var result = await CreateSut().UploadActivityAsync(
            AthleteId, Gpx(), "track-abc.gpx", ActivityType.Ride,
            null, "track-abc", pollInterval: TimeSpan.Zero);

        Assert.That(result.Success, Is.False);
        Assert.That(_httpHandler.RecordedUrls, Is.Empty);
    }

    [Test]
    public async Task UploadActivityAsync_Unauthorized_RefreshesTokenForAthleteAndRetries()
    {
        _httpHandler.Enqueue(HttpStatusCode.Unauthorized, """{"message": "Authorization Error"}""");
        _httpHandler.Enqueue(HttpStatusCode.OK,
            """{"access_token": "fresh_token", "refresh_token": "fresh_refresh"}""");
        _httpHandler.Enqueue(HttpStatusCode.Created,
            """{"id": 111, "error": null, "status": "ready", "activity_id": 456}""");

        var result = await CreateSut().UploadActivityAsync(
            AthleteId, Gpx(), "track-abc.gpx", ActivityType.Ride,
            null, "track-abc", pollInterval: TimeSpan.Zero);

        Assert.That(result.Success, Is.True);
        Assert.That(result.StravaActivityId, Is.EqualTo(456));
        _tokenStore.Verify(t => t.Set(AthleteId, "fresh_token", "fresh_refresh"), Times.Once);
    }

    [Test]
    public void GetAuthorizationUrl_RequestsWriteScope()
    {
        var url = CreateSut().GetAuthorizationUrl("state123");
        Assert.That(url, Does.Contain("activity:read_all"));
        Assert.That(url, Does.Contain("activity:write"));
    }
}
