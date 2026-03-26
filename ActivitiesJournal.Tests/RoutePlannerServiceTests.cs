using ActivitiesJournal.Models;
using ActivitiesJournal.Services;

namespace ActivitiesJournal.Tests;

[TestFixture]
public class RoutePlannerServiceTests
{
    [Test]
    public void ComputeDistanceKm_EmptyList_ReturnsZero()
    {
        var result = RoutePlannerService.ComputeDistanceKm([]);
        Assert.That(result, Is.EqualTo(0.0));
    }

    [Test]
    public void ComputeDistanceKm_SingleWaypoint_ReturnsZero()
    {
        var result = RoutePlannerService.ComputeDistanceKm([new WaypointDto { Lat = 51.5, Lon = -0.1 }]);
        Assert.That(result, Is.EqualTo(0.0));
    }

    [Test]
    public void ComputeDistanceKm_TwoKnownPoints_CorrectHaversine()
    {
        // London (51.5074, -0.1278) to Paris (48.8566, 2.3522) ≈ 340 km
        var waypoints = new List<WaypointDto>
        {
            new() { Lat = 51.5074, Lon = -0.1278 },
            new() { Lat = 48.8566, Lon = 2.3522 }
        };

        var result = RoutePlannerService.ComputeDistanceKm(waypoints);

        Assert.That(result, Is.InRange(335.0, 345.0));
    }

    [Test]
    public void ComputeDistanceKm_MultipleWaypoints_SumsSegments()
    {
        // Three collinear points — total should equal sum of two segments
        var a = new WaypointDto { Lat = 51.5074, Lon = -0.1278 };
        var b = new WaypointDto { Lat = 48.8566, Lon = 2.3522 };
        var c = new WaypointDto { Lat = 48.8566, Lon = 4.0000 }; // further east from Paris

        var ab = RoutePlannerService.ComputeDistanceKm([a, b]);
        var bc = RoutePlannerService.ComputeDistanceKm([b, c]);
        var abc = RoutePlannerService.ComputeDistanceKm([a, b, c]);

        Assert.That(abc, Is.EqualTo(ab + bc).Within(0.001));
    }

    [Test]
    public void ComputeDistanceKm_IdenticalPoints_ReturnsZero()
    {
        var pt = new WaypointDto { Lat = 52.0, Lon = 4.0 };
        var result = RoutePlannerService.ComputeDistanceKm([pt, pt]);
        Assert.That(result, Is.EqualTo(0.0).Within(0.0001));
    }
}
