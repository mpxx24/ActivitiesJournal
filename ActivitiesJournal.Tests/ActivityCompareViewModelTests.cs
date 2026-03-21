using ActivitiesJournal.Models;

namespace ActivitiesJournal.Tests;

[TestFixture]
public class ActivityCompareViewModelTests
{
    private static StravaActivity MakeStrava(float distanceM, int movingTimeSec) => new()
    {
        Distance = distanceM,
        MovingTime = movingTimeSec,
    };

    private static TrackSummary MakeTrack(double distanceKm, TimeSpan duration) => new()
    {
        DistanceKm = distanceKm,
        Duration = duration,
    };

    [Test]
    public void HasBoth_BothNull_ReturnsFalse()
    {
        var vm = new ActivityCompareViewModel();
        Assert.That(vm.HasBoth, Is.False);
    }

    [Test]
    public void HasBoth_OnlyStrava_ReturnsFalse()
    {
        var vm = new ActivityCompareViewModel { Strava = MakeStrava(10000, 3600) };
        Assert.That(vm.HasBoth, Is.False);
    }

    [Test]
    public void HasBoth_BothSet_ReturnsTrue()
    {
        var vm = new ActivityCompareViewModel
        {
            Strava = MakeStrava(10000, 3600),
            Track  = MakeTrack(10.5, TimeSpan.FromSeconds(3700)),
        };
        Assert.That(vm.HasBoth, Is.True);
    }

    [Test]
    public void StravaDistanceKm_ConvertsFromMetres()
    {
        var vm = new ActivityCompareViewModel { Strava = MakeStrava(12500, 3600) };
        Assert.That(vm.StravaDistanceKm, Is.EqualTo(12.5).Within(0.001));
    }

    [Test]
    public void StravaDuration_ConvertsFromSeconds()
    {
        var vm = new ActivityCompareViewModel { Strava = MakeStrava(10000, 5400) };
        Assert.That(vm.StravaDuration, Is.EqualTo(TimeSpan.FromSeconds(5400)));
    }

    [Test]
    public void StravaAvgSpeedKmh_ZeroDuration_ReturnsZero()
    {
        var vm = new ActivityCompareViewModel { Strava = MakeStrava(10000, 0) };
        Assert.That(vm.StravaAvgSpeedKmh, Is.EqualTo(0));
    }

    [Test]
    public void StravaAvgSpeedKmh_CalculatesCorrectly()
    {
        // 10 km in 1 hour = 10 km/h
        var vm = new ActivityCompareViewModel { Strava = MakeStrava(10000, 3600) };
        Assert.That(vm.StravaAvgSpeedKmh, Is.EqualTo(10.0).Within(0.001));
    }

    [Test]
    public void TrackAvgSpeedKmh_CalculatesCorrectly()
    {
        // 12 km in 1 hour = 12 km/h
        var vm = new ActivityCompareViewModel { Track = MakeTrack(12, TimeSpan.FromHours(1)) };
        Assert.That(vm.TrackAvgSpeedKmh, Is.EqualTo(12.0).Within(0.001));
    }

    [Test]
    public void DistanceDeltaKm_TrackLonger_IsPositive()
    {
        var vm = new ActivityCompareViewModel
        {
            Strava = MakeStrava(10000, 3600),   // 10 km
            Track  = MakeTrack(10.5, TimeSpan.FromHours(1)),
        };
        Assert.That(vm.DistanceDeltaKm, Is.EqualTo(0.5).Within(0.001));
    }

    [Test]
    public void DistanceDeltaKm_StravaCoverMoreGround_IsNegative()
    {
        var vm = new ActivityCompareViewModel
        {
            Strava = MakeStrava(11000, 3600),   // 11 km
            Track  = MakeTrack(10.0, TimeSpan.FromHours(1)),
        };
        Assert.That(vm.DistanceDeltaKm, Is.EqualTo(-1.0).Within(0.001));
    }

    [Test]
    public void DurationDelta_TrackLonger_IsPositive()
    {
        var vm = new ActivityCompareViewModel
        {
            Strava = MakeStrava(10000, 3600),
            Track  = MakeTrack(10, TimeSpan.FromSeconds(3700)),
        };
        Assert.That(vm.DurationDelta, Is.EqualTo(TimeSpan.FromSeconds(100)));
    }

    [Test]
    public void SpeedDeltaKmh_TrackFaster_IsPositive()
    {
        var vm = new ActivityCompareViewModel
        {
            Strava = MakeStrava(10000, 3600),  // 10 km/h
            Track  = MakeTrack(12, TimeSpan.FromHours(1)), // 12 km/h
        };
        Assert.That(vm.SpeedDeltaKmh, Is.EqualTo(2.0).Within(0.001));
    }

    [Test]
    public void DistanceDeltaKm_NotBothSet_ReturnsZero()
    {
        var vm = new ActivityCompareViewModel { Strava = MakeStrava(10000, 3600) };
        Assert.That(vm.DistanceDeltaKm, Is.EqualTo(0));
    }
}
