using ActivitiesJournal.Models;

namespace ActivitiesJournal.Tests;

[TestFixture]
public class SportTypesTests
{
    [TestCase("Ride", true)]
    [TestCase("VirtualRide", true)]
    [TestCase("GravelRide", true)]
    [TestCase("MountainBikeRide", true)]
    [TestCase("Walk", false)]
    [TestCase("Hike", false)]
    [TestCase("Run", false)]
    public void IsRide_ReturnsExpected(string sportType, bool expected)
    {
        Assert.That(SportTypes.IsRide(sportType), Is.EqualTo(expected));
    }

    [TestCase("Walk", true)]
    [TestCase("Hike", true)]
    [TestCase("VirtualWalk", true)]
    [TestCase("Ride", false)]
    [TestCase("Run", false)]
    public void IsWalk_ReturnsExpected(string sportType, bool expected)
    {
        Assert.That(SportTypes.IsWalk(sportType), Is.EqualTo(expected));
    }

    [Test]
    public void FilterByType_Ride_ReturnsOnlyRides()
    {
        var activities = new List<StravaActivity>
        {
            new() { SportType = "Ride", Name = "Morning Ride" },
            new() { SportType = "Walk", Name = "Evening Walk" },
            new() { SportType = "GravelRide", Name = "Gravel Fun" },
        };

        var result = SportTypes.FilterByType(activities, "Ride");

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.All(a => SportTypes.IsRide(a.SportType)), Is.True);
    }

    [Test]
    public void FilterByType_Walk_ReturnsOnlyWalks()
    {
        var activities = new List<StravaActivity>
        {
            new() { SportType = "Ride", Name = "Morning Ride" },
            new() { SportType = "Walk", Name = "Evening Walk" },
            new() { SportType = "Hike", Name = "Mountain Hike" },
        };

        var result = SportTypes.FilterByType(activities, "Walk");

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.All(a => SportTypes.IsWalk(a.SportType)), Is.True);
    }

    [Test]
    public void FilterByType_All_ReturnsEverything()
    {
        var activities = new List<StravaActivity>
        {
            new() { SportType = "Ride" },
            new() { SportType = "Walk" },
            new() { SportType = "Run" },
        };

        var result = SportTypes.FilterByType(activities, "All");

        Assert.That(result, Has.Count.EqualTo(3));
    }

    [TestCase("Ride", "Rides")]
    [TestCase("Walk", "Walks & Hikes")]
    [TestCase("All", "All Activities")]
    [TestCase("whatever", "Rides")]
    [TestCase("Run", "Runs")]
    [TestCase("Swim", "Swims")]
    public void TypeLabel_ReturnsExpected(string type, string expected)
    {
        Assert.That(SportTypes.TypeLabel(type), Is.EqualTo(expected));
    }

    [TestCase("Run", true)]
    [TestCase("VirtualRun", true)]
    [TestCase("TrailRun", true)]
    [TestCase("Ride", false)]
    [TestCase("Walk", false)]
    [TestCase("Swim", false)]
    public void IsRun_ReturnsExpected(string sportType, bool expected)
    {
        Assert.That(SportTypes.IsRun(sportType), Is.EqualTo(expected));
    }

    [TestCase("Swim", true)]
    [TestCase("Run", false)]
    [TestCase("Ride", false)]
    public void IsSwim_ReturnsExpected(string sportType, bool expected)
    {
        Assert.That(SportTypes.IsSwim(sportType), Is.EqualTo(expected));
    }

    [Test]
    public void FilterByType_Run_ReturnsOnlyRuns()
    {
        var activities = new List<StravaActivity>
        {
            new() { SportType = "Run", Name = "Morning Run" },
            new() { SportType = "TrailRun", Name = "Trail Run" },
            new() { SportType = "Ride", Name = "Morning Ride" },
        };

        var result = SportTypes.FilterByType(activities, "Run");

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.All(a => SportTypes.IsRun(a.SportType)), Is.True);
    }

    [Test]
    public void FilterByType_Swim_ReturnsOnlySwims()
    {
        var activities = new List<StravaActivity>
        {
            new() { SportType = "Swim", Name = "Open Water Swim" },
            new() { SportType = "Ride", Name = "Morning Ride" },
        };

        var result = SportTypes.FilterByType(activities, "Swim");

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].SportType, Is.EqualTo("Swim"));
    }

    [TestCase("Run")]
    [TestCase("Swim")]
    public void ActivityType_ParsesNewValues(string value)
    {
        Assert.That(Enum.TryParse<ActivityType>(value, out _), Is.True);
    }
}
