using ActivitiesJournal.Models;
using ActivitiesJournal.Services;
using Moq;

namespace ActivitiesJournal.Tests;

[TestFixture]
public class GoalsAnalyticsServiceTests
{
    private Mock<IStravaService> _stravaMock = null!;
    private Mock<IGoalsService> _goalsMock = null!;
    private GoalsAnalyticsService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _stravaMock = new Mock<IStravaService>();
        _goalsMock = new Mock<IGoalsService>();
        _sut = new GoalsAnalyticsService(_stravaMock.Object, _goalsMock.Object);
    }

    [Test]
    public async Task BuildGoalsViewModelAsync_NoActivities_ReturnsZeroProgress()
    {
        _stravaMock.Setup(s => s.GetAllActivitiesAsync())
            .ReturnsAsync(new List<StravaActivity>());
        _goalsMock.Setup(g => g.LoadAsync(It.IsAny<long>()))
            .ReturnsAsync(new GoalsData());

        var vm = await _sut.BuildGoalsViewModelAsync(123);

        Assert.That(vm.CurrentDistanceKm, Is.EqualTo(0));
        Assert.That(vm.CurrentElevationM, Is.EqualTo(0));
        Assert.That(vm.CurrentRides, Is.EqualTo(0));
        Assert.That(vm.Year, Is.EqualTo(DateTime.Now.Year));
    }

    [Test]
    public async Task BuildGoalsViewModelAsync_WithRides_CalculatesCorrectTotals()
    {
        int year = DateTime.Now.Year;
        var activities = new List<StravaActivity>
        {
            new() { SportType = "Ride", Distance = 25000, TotalElevationGain = 300, StartDateLocal = new DateTime(year, 2, 1) },
            new() { SportType = "Ride", Distance = 30000, TotalElevationGain = 500, StartDateLocal = new DateTime(year, 3, 1) },
            new() { SportType = "Walk", Distance = 5000, TotalElevationGain = 50, StartDateLocal = new DateTime(year, 2, 15) },
            new() { SportType = "Ride", Distance = 40000, TotalElevationGain = 200, StartDateLocal = new DateTime(year - 1, 6, 1) },
        };

        _stravaMock.Setup(s => s.GetAllActivitiesAsync()).ReturnsAsync(activities);
        _goalsMock.Setup(g => g.LoadAsync(It.IsAny<long>())).ReturnsAsync(new GoalsData());

        var vm = await _sut.BuildGoalsViewModelAsync(123);

        Assert.That(vm.CurrentDistanceKm, Is.EqualTo(55.0).Within(0.1));
        Assert.That(vm.CurrentElevationM, Is.EqualTo(800));
        Assert.That(vm.CurrentRides, Is.EqualTo(2));
    }

    [Test]
    public async Task BuildGoalsViewModelAsync_WithGoals_IncludesProjections()
    {
        int year = DateTime.Now.Year;
        var activities = new List<StravaActivity>
        {
            new() { SportType = "Ride", Distance = 100000, TotalElevationGain = 1000, StartDateLocal = new DateTime(year, 1, 15) },
        };

        var goalsData = new GoalsData
        {
            AnnualGoals = new List<AnnualGoals>
            {
                new() { Year = year, DistanceGoalKm = 5000, ElevationGoalM = 50000, RidesGoal = 200 }
            }
        };

        _stravaMock.Setup(s => s.GetAllActivitiesAsync()).ReturnsAsync(activities);
        _goalsMock.Setup(g => g.LoadAsync(It.IsAny<long>())).ReturnsAsync(goalsData);

        var vm = await _sut.BuildGoalsViewModelAsync(123);

        Assert.That(vm.Goals.DistanceGoalKm, Is.EqualTo(5000));
        Assert.That(vm.DistanceProjection, Is.Not.Null);
        Assert.That(vm.ElevationProjection, Is.Not.Null);
        Assert.That(vm.RidesProjection, Is.Not.Null);
    }

    [Test]
    public async Task BuildGoalsViewModelAsync_ChallengeProgress_CalculatesKm()
    {
        int year = DateTime.Now.Year;
        var challengeStart = new DateTime(year, 1, 1);
        var activities = new List<StravaActivity>
        {
            new() { SportType = "Ride", Distance = 50000, StartDateLocal = new DateTime(year, 2, 1) },
            new() { SportType = "Ride", Distance = 30000, StartDateLocal = new DateTime(year - 1, 12, 15) }, // before challenge
        };

        var goalsData = new GoalsData
        {
            Challenges = new List<VirtualChallenge>
            {
                new() { Name = "Test Challenge", TargetKm = 100, StartDate = challengeStart }
            }
        };

        _stravaMock.Setup(s => s.GetAllActivitiesAsync()).ReturnsAsync(activities);
        _goalsMock.Setup(g => g.LoadAsync(It.IsAny<long>())).ReturnsAsync(goalsData);

        var vm = await _sut.BuildGoalsViewModelAsync(123);

        Assert.That(vm.ChallengeProgressList, Has.Count.EqualTo(1));
        Assert.That(vm.ChallengeProgressList[0].EarnedKm, Is.EqualTo(50.0).Within(0.1));
    }

    [TestCase(100, 5000, 0.5, 365, 182, "On pace for")]
    [TestCase(3000, 5000, 0.5, 365, 182, "On track!")]
    [TestCase(100, null, 0.5, 365, 182, null)]
    public void Project_ReturnsExpected(double current, double? target, double fraction, int daysInYear, int dayOfYear, string? expectedContains)
    {
        var result = GoalsAnalyticsService.Project(current, target, fraction, daysInYear, dayOfYear);

        if (expectedContains == null)
            Assert.That(result, Is.Null);
        else
            Assert.That(result, Does.Contain(expectedContains));
    }
}
