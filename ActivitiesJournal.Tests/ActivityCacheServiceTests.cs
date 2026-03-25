using ActivitiesJournal.Configuration;
using ActivitiesJournal.Models;
using ActivitiesJournal.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ActivitiesJournal.Tests;

[TestFixture]
public class ActivityCacheServiceTests
{
    private string _tempDir = null!;
    private ActivityCacheService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ActivityCacheTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        var env = Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == _tempDir);
        var storageOptions = Options.Create(new StorageOptions { BlobEndpoint = null });
        _sut = new ActivityCacheService(env, storageOptions, NullLogger<ActivityCacheService>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public async Task GetAsync_NoFileExists_ReturnsEmptyList()
    {
        var result = await _sut.GetAsync(athleteId: 99);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task SetAsync_ThenGetAsync_RoundTripsActivities()
    {
        var activities = new List<StravaActivity>
        {
            new() { Id = 1, Name = "Morning Run", StartDate = new DateTime(2025, 3, 1, 8, 0, 0, DateTimeKind.Utc) },
            new() { Id = 2, Name = "Evening Ride", StartDate = new DateTime(2025, 3, 2, 18, 0, 0, DateTimeKind.Utc) },
        };

        await _sut.SetAsync(99, activities);
        var result = await _sut.GetAsync(99);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.Single(a => a.Id == 1).Name, Is.EqualTo("Morning Run"));
        Assert.That(result.Single(a => a.Id == 2).Name, Is.EqualTo("Evening Ride"));
    }

    [Test]
    public async Task SetAsync_OverwritesPreviousData()
    {
        await _sut.SetAsync(99, new List<StravaActivity> { new() { Id = 1 } });
        await _sut.SetAsync(99, new List<StravaActivity> { new() { Id = 1 }, new() { Id = 2 } });

        var result = await _sut.GetAsync(99);

        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetAsync_DifferentAthleteIds_ReturnSeparateData()
    {
        await _sut.SetAsync(100, new List<StravaActivity> { new() { Id = 10 } });
        await _sut.SetAsync(200, new List<StravaActivity> { new() { Id = 20 }, new() { Id = 21 } });

        var result100 = await _sut.GetAsync(100);
        var result200 = await _sut.GetAsync(200);

        Assert.That(result100, Has.Count.EqualTo(1));
        Assert.That(result200, Has.Count.EqualTo(2));
    }
}
