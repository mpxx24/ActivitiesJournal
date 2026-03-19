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
    public void TypeLabel_ReturnsExpected(string type, string expected)
    {
        Assert.That(SportTypes.TypeLabel(type), Is.EqualTo(expected));
    }
}
