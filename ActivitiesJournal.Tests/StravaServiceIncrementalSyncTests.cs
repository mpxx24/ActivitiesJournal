using System.Net;
using System.Security.Claims;
using System.Text.Json;
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
public class StravaServiceIncrementalSyncTests
{
    private Mock<IActivityCacheService> _activityCache = null!;
    private Mock<ITokenStore> _tokenStore = null!;
    private Mock<IHttpContextAccessor> _httpContextAccessor = null!;
    private IMemoryCache _memoryCache = null!;
    private FakeHttpMessageHandler _httpHandler = null!;

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

        return new StravaService(
            opts,
            httpClient,
            _memoryCache,
            Mock.Of<ISegmentPolylineCacheService>(),
            _activityCache.Object,
            _tokenStore.Object,
            _httpContextAccessor.Object,
            NullLogger<StravaService>.Instance);
    }

    [SetUp]
    public void SetUp()
    {
        _activityCache = new Mock<IActivityCacheService>();

        _tokenStore = new Mock<ITokenStore>();
        _tokenStore.Setup(t => t.Get(It.IsAny<long>())).Returns(("access_token", "refresh_token"));

        var user = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, "42") }));
        var httpContext = new DefaultHttpContext { User = user };
        _httpContextAccessor = new Mock<IHttpContextAccessor>();
        _httpContextAccessor.Setup(a => a.HttpContext).Returns(httpContext);

        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _httpHandler = new FakeHttpMessageHandler();
    }

    [TearDown]
    public void TearDown()
    {
        _memoryCache.Dispose();
        _httpHandler.Dispose();
    }

    [Test]
    public async Task GetAllActivitiesAsync_EmptyBlobCache_DoesFullFetchWithoutAfterParam()
    {
        _activityCache.Setup(c => c.GetAsync(42)).ReturnsAsync(new List<StravaActivity>());

        var activity = new StravaActivity
        {
            Id = 1,
            Name = "Run",
            StartDate = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc)
        };
        _httpHandler.Enqueue(HttpStatusCode.OK, JsonSerializer.Serialize(new[] { activity }));
        // Default: returns [] for subsequent pages

        var sut = CreateSut();
        var result = await sut.GetAllActivitiesAsync();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Id, Is.EqualTo(1));
        Assert.That(_httpHandler.RecordedUrls, Has.None.Contains("after="));
        _activityCache.Verify(c => c.SetAsync(42, It.Is<List<StravaActivity>>(l => l.Count == 1)), Times.Once);
    }

    [Test]
    public async Task GetAllActivitiesAsync_BlobHasActivities_FetchesWithAfterParam()
    {
        var existing = new StravaActivity
        {
            Id = 1,
            Name = "Old Run",
            StartDate = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc)
        };
        _activityCache.Setup(c => c.GetAsync(42)).ReturnsAsync(new List<StravaActivity> { existing });
        // No new activities from incremental fetch (default returns [])

        var sut = CreateSut();
        var result = await sut.GetAllActivitiesAsync();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(_httpHandler.RecordedUrls, Has.Some.Contains("after="));
    }

    [Test]
    public async Task GetAllActivitiesAsync_BlobHasActivitiesAndNewOnesExist_MergesAndSaves()
    {
        var existing = new StravaActivity
        {
            Id = 1,
            Name = "Old Run",
            StartDate = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc)
        };
        _activityCache.Setup(c => c.GetAsync(42)).ReturnsAsync(new List<StravaActivity> { existing });

        var newActivity = new StravaActivity
        {
            Id = 2,
            Name = "New Run",
            StartDate = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc)
        };
        _httpHandler.Enqueue(HttpStatusCode.OK, JsonSerializer.Serialize(new[] { newActivity }));
        // Default: [] for page 2

        var sut = CreateSut();
        var result = await sut.GetAllActivitiesAsync();

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.Select(a => a.Id), Is.EquivalentTo(new[] { 1L, 2L }));
        _activityCache.Verify(
            c => c.SetAsync(42, It.Is<List<StravaActivity>>(l => l.Count == 2)),
            Times.Once);
    }

    [Test]
    public async Task GetAllActivitiesAsync_CachedInMemory_DoesNotCallBlobOrApi()
    {
        var activities = new List<StravaActivity> { new() { Id = 99 } };
        _memoryCache.Set("strava_all_activities_42", activities);

        var sut = CreateSut();
        var result = await sut.GetAllActivitiesAsync();

        Assert.That(result, Has.Count.EqualTo(1));
        _activityCache.Verify(c => c.GetAsync(It.IsAny<long>()), Times.Never);
        Assert.That(_httpHandler.RecordedUrls, Is.Empty);
    }

    [Test]
    public void GetAllActivitiesAsync_StravaReturns429_ThrowsWithClearMessage()
    {
        _activityCache.Setup(c => c.GetAsync(42)).ReturnsAsync(new List<StravaActivity>());
        _httpHandler.SetDefault(HttpStatusCode.TooManyRequests, "Rate limit exceeded");

        var sut = CreateSut();

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetAllActivitiesAsync());
        Assert.That(ex!.Message, Does.Contain("rate limit").IgnoreCase);
    }

    [Test]
    public void InvalidateCache_ClearsInMemoryCache_DoesNotTouchBlob()
    {
        var activities = new List<StravaActivity> { new() { Id = 1 } };
        _memoryCache.Set("strava_all_activities_42", activities);

        var sut = CreateSut();
        sut.InvalidateCache();

        Assert.That(_memoryCache.TryGetValue("strava_all_activities_42", out _), Is.False);
        _activityCache.Verify(
            c => c.SetAsync(It.IsAny<long>(), It.IsAny<List<StravaActivity>>()),
            Times.Never);
    }
}

internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<(HttpStatusCode StatusCode, string Content)> _queue = new();
    private (HttpStatusCode StatusCode, string Content) _default = (HttpStatusCode.OK, "[]");
    public List<string> RecordedUrls { get; } = new();

    public void Enqueue(HttpStatusCode statusCode, string content)
        => _queue.Enqueue((statusCode, content));

    public void SetDefault(HttpStatusCode statusCode, string content)
        => _default = (statusCode, content);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri?.ToString() ?? string.Empty;
        RecordedUrls.Add(url);

        var (statusCode, content) = _queue.TryDequeue(out var queued)
            ? queued
            : _default;

        return Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content)
        });
    }
}
