using ActivitiesJournal.Models;
using ActivitiesJournal.Services;
using Moq;

namespace ActivitiesJournal.Tests;

[TestFixture]
public class DashboardServiceTests
{
    private Mock<IStravaService> _stravaServiceMock = null!;
    private DashboardService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _stravaServiceMock = new Mock<IStravaService>();
        _sut = new DashboardService(_stravaServiceMock.Object);
    }

    [Test]
    public async Task BuildDashboardAsync_EmptyActivities_ReturnsEmptyViewModel()
    {
        _stravaServiceMock.Setup(s => s.GetAllActivitiesAsync())
            .ReturnsAsync(new List<StravaActivity>());

        var result = await _sut.BuildDashboardAsync();

        Assert.That(result.RecentActivities, Is.Empty);
        Assert.That(result.WeekRideKm, Is.EqualTo(0));
        Assert.That(result.RideStreak, Is.EqualTo(0));
        Assert.That(result.WeeklyKm, Has.Count.EqualTo(8));
    }

    [Test]
    public async Task BuildDashboardAsync_RecentActivities_LimitedToFive()
    {
        var activities = Enumerable.Range(1, 10).Select(i => new StravaActivity
        {
            Id = i,
            Name = $"Activity {i}",
            SportType = "Ride",
            StartDateLocal = DateTime.Today.AddDays(-i),
        }).ToList();

        _stravaServiceMock.Setup(s => s.GetAllActivitiesAsync())
            .ReturnsAsync(activities);

        var result = await _sut.BuildDashboardAsync();

        Assert.That(result.RecentActivities, Has.Count.EqualTo(5));
        Assert.That(result.RecentActivities[0].Id, Is.EqualTo(1)); // most recent first
    }

    [Test]
    public async Task BuildDashboardAsync_YtdTotals_SumsCurrentYearOnly()
    {
        var activities = new List<StravaActivity>
        {
            new() { SportType = "Ride", Distance = 10000, StartDateLocal = new DateTime(DateTime.Today.Year, 1, 15) },
            new() { SportType = "Ride", Distance = 20000, StartDateLocal = new DateTime(DateTime.Today.Year, 2, 10) },
            new() { SportType = "Ride", Distance = 50000, StartDateLocal = new DateTime(DateTime.Today.Year - 1, 6, 1) },
            new() { SportType = "Walk", Distance = 5000, StartDateLocal = new DateTime(DateTime.Today.Year, 3, 1) },
        };

        _stravaServiceMock.Setup(s => s.GetAllActivitiesAsync())
            .ReturnsAsync(activities);

        var result = await _sut.BuildDashboardAsync();

        Assert.That(result.YtdRideKm, Is.EqualTo(30.0).Within(0.01));
        Assert.That(result.YtdRideCount, Is.EqualTo(2));
        Assert.That(result.YtdWalkKm, Is.EqualTo(5.0).Within(0.01));
        Assert.That(result.YtdWalkCount, Is.EqualTo(1));
    }

    [Test]
    public void GetMondayOfWeek_ReturnsCorrectMonday()
    {
        // Wednesday 2026-03-18
        var wednesday = new DateTime(2026, 3, 18);
        Assert.That(DashboardService.GetMondayOfWeek(wednesday), Is.EqualTo(new DateTime(2026, 3, 16)));

        // Monday itself
        var monday = new DateTime(2026, 3, 16);
        Assert.That(DashboardService.GetMondayOfWeek(monday), Is.EqualTo(new DateTime(2026, 3, 16)));

        // Sunday
        var sunday = new DateTime(2026, 3, 22);
        Assert.That(DashboardService.GetMondayOfWeek(sunday), Is.EqualTo(new DateTime(2026, 3, 16)));
    }

    [Test]
    public void ComputeCurrentStreak_ConsecutiveDays_CountsCorrectly()
    {
        var today = new DateTime(2026, 3, 19);
        var activities = new List<StravaActivity>
        {
            new() { SportType = "Ride", StartDateLocal = new DateTime(2026, 3, 19) },
            new() { SportType = "Ride", StartDateLocal = new DateTime(2026, 3, 18) },
            new() { SportType = "Ride", StartDateLocal = new DateTime(2026, 3, 17) },
            new() { SportType = "Ride", StartDateLocal = new DateTime(2026, 3, 15) }, // gap
        };

        var streak = DashboardService.ComputeCurrentStreak(activities, today);

        Assert.That(streak, Is.EqualTo(3));
    }

    [Test]
    public void ComputeCurrentStreak_NoActivitiesToday_StartsFromYesterday()
    {
        var today = new DateTime(2026, 3, 19);
        var activities = new List<StravaActivity>
        {
            new() { SportType = "Ride", StartDateLocal = new DateTime(2026, 3, 18) },
            new() { SportType = "Ride", StartDateLocal = new DateTime(2026, 3, 17) },
        };

        var streak = DashboardService.ComputeCurrentStreak(activities, today);

        Assert.That(streak, Is.EqualTo(2));
    }

    [Test]
    public void ComputeCurrentStreak_NoRides_ReturnsZero()
    {
        var today = new DateTime(2026, 3, 19);
        var activities = new List<StravaActivity>
        {
            new() { SportType = "Walk", StartDateLocal = new DateTime(2026, 3, 19) },
        };

        var streak = DashboardService.ComputeCurrentStreak(activities, today);

        Assert.That(streak, Is.EqualTo(0));
    }

    [Test]
    public void BuildWeeklySummary_ReturnsEightWeeks()
    {
        var result = DashboardService.BuildWeeklySummary(new List<StravaActivity>(), DateTime.Today);

        Assert.That(result, Has.Count.EqualTo(8));
        Assert.That(result.All(km => km == 0), Is.True);
    }
}
