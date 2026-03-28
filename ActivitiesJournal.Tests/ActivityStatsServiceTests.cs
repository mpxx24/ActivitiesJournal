using ActivitiesJournal.Models;
using ActivitiesJournal.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ActivitiesJournal.Tests;

[TestFixture]
public class ActivityStatsServiceTests
{
    private Mock<IStravaService> _stravaMock = null!;
    private ActivityStatsService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _stravaMock = new Mock<IStravaService>();
        _sut = new ActivityStatsService(_stravaMock.Object, Mock.Of<ILogger<ActivityStatsService>>());
    }

    // ── ComputeLongestStreak ─────────────────────────────────────────────────

    [Test]
    public void ComputeLongestStreak_EmptyList_ReturnsZero()
    {
        var result = ActivityStatsService.ComputeLongestStreak([]);
        Assert.That(result, Is.EqualTo(0));
    }

    [Test]
    public void ComputeLongestStreak_SingleDate_ReturnsOne()
    {
        var result = ActivityStatsService.ComputeLongestStreak([new DateTime(2024, 1, 1)]);
        Assert.That(result, Is.EqualTo(1));
    }

    [Test]
    public void ComputeLongestStreak_ConsecutiveDays_ReturnsLength()
    {
        var dates = new List<DateTime>
        {
            new(2024, 1, 1), new(2024, 1, 2), new(2024, 1, 3),
            new(2024, 1, 5), new(2024, 1, 6),
        };
        var result = ActivityStatsService.ComputeLongestStreak(dates);
        Assert.That(result, Is.EqualTo(3));
    }

    [Test]
    public void ComputeLongestStreak_NoConsecutiveDays_ReturnsOne()
    {
        var dates = new List<DateTime>
        {
            new(2024, 1, 1), new(2024, 1, 3), new(2024, 1, 5),
        };
        var result = ActivityStatsService.ComputeLongestStreak(dates);
        Assert.That(result, Is.EqualTo(1));
    }

    // ── GetFitnessAsync ──────────────────────────────────────────────────────

    [Test]
    public async Task GetFitnessAsync_NoActivities_ReturnsZeroCtlAtl()
    {
        _stravaMock.Setup(s => s.GetAllActivitiesAsync()).ReturnsAsync([]);

        var vm = await _sut.GetFitnessAsync(30);

        Assert.That(vm.CurrentCtl, Is.EqualTo(0).Within(0.01));
        Assert.That(vm.CurrentAtl, Is.EqualTo(0).Within(0.01));
        Assert.That(vm.CurrentTsb, Is.EqualTo(0).Within(0.01));
        Assert.That(vm.Points, Has.Count.EqualTo(31)); // 0..30 days inclusive
    }

    [Test]
    public async Task GetFitnessAsync_WithRides_CtlIncreasesOverTime()
    {
        var today = DateTime.Today;
        var rides = Enumerable.Range(1, 30)
            .Select(i => new StravaActivity
            {
                SportType = "Ride",
                Distance = 50000,
                MovingTime = 7200,
                AverageSpeed = 7f,
                StartDateLocal = today.AddDays(-30 + i),
            })
            .ToList();

        _stravaMock.Setup(s => s.GetAllActivitiesAsync()).ReturnsAsync(rides);

        var vm = await _sut.GetFitnessAsync(30);

        Assert.That(vm.CurrentCtl, Is.GreaterThan(0));
        Assert.That(vm.CurrentAtl, Is.GreaterThan(0));
        Assert.That(vm.DaysShown, Is.EqualTo(30));
    }

    [Test]
    public async Task GetFitnessAsync_TsbStatus_FreshWhenPositiveTsb()
    {
        // Long rest period → CTL decays more slowly than ATL → TSB positive
        var pastRides = Enumerable.Range(1, 60)
            .Select(i => new StravaActivity
            {
                SportType = "Ride",
                Distance = 80000,
                MovingTime = 10800,
                AverageSpeed = 8f,
                StartDateLocal = DateTime.Today.AddDays(-90 + i), // all > 30 days ago
            })
            .ToList();

        _stravaMock.Setup(s => s.GetAllActivitiesAsync()).ReturnsAsync(pastRides);

        var vm = await _sut.GetFitnessAsync(30);

        // After 30 days rest, ATL should be near zero, CTL slightly positive → TSB >= 0
        Assert.That(vm.CurrentTsb, Is.GreaterThanOrEqualTo(0));
        Assert.That(vm.TsbStatus, Does.Contain("Fresh").Or.Contain("Neutral"));
    }

    // ── GetAnalysisAsync ─────────────────────────────────────────────────────

    [Test]
    public async Task GetAnalysisAsync_ClassifiesEpicRide()
    {
        var year = DateTime.Now.Year;
        _stravaMock.Setup(s => s.GetAllActivitiesAsync()).ReturnsAsync([
            new StravaActivity { SportType = "Ride", Distance = 140000, MovingTime = 18000, AverageSpeed = 7.78f, StartDateLocal = new DateTime(year, 6, 1) }
        ]);

        var vm = await _sut.GetAnalysisAsync(year);

        Assert.That(vm.ClassifiedRides, Has.Count.EqualTo(1));
        Assert.That(vm.ClassifiedRides[0].RideType, Is.EqualTo(RideType.Epic));
    }

    [Test]
    public async Task GetAnalysisAsync_ClassifiesRecoveryRide()
    {
        var year = DateTime.Now.Year;
        _stravaMock.Setup(s => s.GetAllActivitiesAsync()).ReturnsAsync([
            new StravaActivity { SportType = "Ride", Distance = 20000, MovingTime = 5000, AverageSpeed = 4f, StartDateLocal = new DateTime(year, 6, 1) }
        ]);

        var vm = await _sut.GetAnalysisAsync(year);

        Assert.That(vm.ClassifiedRides[0].RideType, Is.EqualTo(RideType.Recovery));
    }

    [Test]
    public async Task GetAnalysisAsync_SpeedZones_BucketsCorrectly()
    {
        var year = DateTime.Now.Year;
        _stravaMock.Setup(s => s.GetAllActivitiesAsync()).ReturnsAsync([
            new StravaActivity { SportType = "Ride", Distance = 30000, MovingTime = 6000, AverageSpeed = 5.0f /* 18 km/h */, StartDateLocal = new DateTime(year, 6, 1) },
            new StravaActivity { SportType = "Ride", Distance = 30000, MovingTime = 3600, AverageSpeed = 7.5f /* 27 km/h */, StartDateLocal = new DateTime(year, 6, 2) },
        ]);

        var vm = await _sut.GetAnalysisAsync(year);

        var zoneSub20 = vm.SpeedZones.Single(z => z.Label == "< 20 km/h");
        var zone2530  = vm.SpeedZones.Single(z => z.Label == "25–30 km/h");
        Assert.That(zoneSub20.RideCount, Is.EqualTo(1));
        Assert.That(zone2530.RideCount, Is.EqualTo(1));
    }

    [Test]
    public async Task GetAnalysisAsync_FiltersByYear()
    {
        var year = DateTime.Now.Year;
        _stravaMock.Setup(s => s.GetAllActivitiesAsync()).ReturnsAsync([
            new StravaActivity { SportType = "Ride", Distance = 30000, MovingTime = 3600, AverageSpeed = 8f, StartDateLocal = new DateTime(year, 6, 1) },
            new StravaActivity { SportType = "Ride", Distance = 30000, MovingTime = 3600, AverageSpeed = 8f, StartDateLocal = new DateTime(year - 1, 6, 1) },
        ]);

        var vm = await _sut.GetAnalysisAsync(year);

        Assert.That(vm.ClassifiedRides, Has.Count.EqualTo(1));
        Assert.That(vm.Year, Is.EqualTo(year));
    }

    // ── GetPersonalRecordsAsync ──────────────────────────────────────────────

    [Test]
    public async Task GetPersonalRecordsAsync_NoActivities_ReturnsEmpty()
    {
        _stravaMock.Setup(s => s.GetAllActivitiesAsync()).ReturnsAsync([]);

        var vm = await _sut.GetPersonalRecordsAsync("Ride");

        Assert.That(vm.AllTimeRecords, Is.Empty);
        Assert.That(vm.TotalRides, Is.EqualTo(0));
    }

    [Test]
    public async Task GetPersonalRecordsAsync_PicksLongestAndMostClimbing()
    {
        _stravaMock.Setup(s => s.GetAllActivitiesAsync()).ReturnsAsync([
            new StravaActivity { SportType = "Ride", Distance = 100000, MovingTime = 14400, TotalElevationGain = 500, AverageSpeed = 7f, MaxSpeed = 15f, StartDateLocal = DateTime.Today },
            new StravaActivity { SportType = "Ride", Distance = 50000,  MovingTime = 7200,  TotalElevationGain = 2000, AverageSpeed = 7f, MaxSpeed = 18f, StartDateLocal = DateTime.Today.AddDays(-1) },
        ]);

        var vm = await _sut.GetPersonalRecordsAsync("Ride");

        var longest = vm.AllTimeRecords.Single(r => r.Label == "Longest");
        Assert.That(longest.Value, Does.Contain("100") & Does.Contain("km"));

        var mostClimb = vm.AllTimeRecords.Single(r => r.Label == "Most Elevation");
        Assert.That(mostClimb.Value, Does.StartWith("2000"));
    }

    [Test]
    public async Task GetPersonalRecordsAsync_Walk_IncludesBestPace()
    {
        _stravaMock.Setup(s => s.GetAllActivitiesAsync()).ReturnsAsync([
            new StravaActivity { SportType = "Walk", Distance = 10000, MovingTime = 5400, TotalElevationGain = 100, StartDateLocal = DateTime.Today },
        ]);

        var vm = await _sut.GetPersonalRecordsAsync("Walk");

        Assert.That(vm.AllTimeRecords.Any(r => r.Label == "Best Pace"), Is.True);
    }

    // ── GetBadgesAsync ───────────────────────────────────────────────────────

    [Test]
    public async Task GetBadgesAsync_Ride_FirstRideBadgeEarned()
    {
        _stravaMock.Setup(s => s.GetAllActivitiesAsync()).ReturnsAsync([
            new StravaActivity { SportType = "Ride", Distance = 30000, MovingTime = 3600, StartDateLocal = DateTime.Today },
        ]);

        var vm = await _sut.GetBadgesAsync("Ride");

        var badge = vm.Badges.Single(b => b.Name == "First Ride");
        Assert.That(badge.Earned, Is.True);
    }

    [Test]
    public async Task GetBadgesAsync_Ride_FirstRideBadgeNotEarned_WhenNoRides()
    {
        _stravaMock.Setup(s => s.GetAllActivitiesAsync()).ReturnsAsync([]);

        var vm = await _sut.GetBadgesAsync("Ride");

        var badge = vm.Badges.Single(b => b.Name == "First Ride");
        Assert.That(badge.Earned, Is.False);
    }

    [Test]
    public async Task GetBadgesAsync_Walk_FirstWalkBadgeEarned()
    {
        _stravaMock.Setup(s => s.GetAllActivitiesAsync()).ReturnsAsync([
            new StravaActivity { SportType = "Walk", Distance = 5000, MovingTime = 3600, StartDateLocal = DateTime.Today },
        ]);

        var vm = await _sut.GetBadgesAsync("Walk");

        var badge = vm.Badges.Single(b => b.Name == "First Walk");
        Assert.That(badge.Earned, Is.True);
    }

    [Test]
    public async Task GetBadgesAsync_Ride_CenturyBadgeEarned()
    {
        _stravaMock.Setup(s => s.GetAllActivitiesAsync()).ReturnsAsync([
            new StravaActivity { SportType = "Ride", Distance = 105000, MovingTime = 14400, StartDateLocal = DateTime.Today },
        ]);

        var vm = await _sut.GetBadgesAsync("Ride");

        var badge = vm.Badges.Single(b => b.Name == "Century Ride");
        Assert.That(badge.Earned, Is.True);
    }

    [Test]
    public async Task GetBadgesAsync_Ride_StreakBadge_EarnedAfter7ConsecutiveDays()
    {
        var rides = Enumerable.Range(0, 7)
            .Select(i => new StravaActivity
            {
                SportType = "Ride",
                Distance = 30000,
                MovingTime = 3600,
                StartDateLocal = DateTime.Today.AddDays(-6 + i),
            })
            .ToList();

        _stravaMock.Setup(s => s.GetAllActivitiesAsync()).ReturnsAsync(rides);

        var vm = await _sut.GetBadgesAsync("Ride");

        var badge = vm.Badges.Single(b => b.Name == "Week Warrior");
        Assert.That(badge.Earned, Is.True);
    }
}
