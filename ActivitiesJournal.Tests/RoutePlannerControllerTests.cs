using System.Security.Claims;
using ActivitiesJournal.Configuration;
using ActivitiesJournal.Controllers;
using ActivitiesJournal.Models;
using ActivitiesJournal.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ActivitiesJournal.Tests;

[TestFixture]
public class RoutePlannerControllerTests
{
    private const long TestAthleteId = 99999;
    private Mock<IRoutePlannerService> _service = null!;
    private RoutePlannerController _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new Mock<IRoutePlannerService>();
        var ownerOptions = Options.Create(new TrackOwnerOptions { OwnerAthleteId = TestAthleteId, UploadApiKey = "test-key" });
        _sut = new RoutePlannerController(
            _service.Object,
            ownerOptions,
            NullLogger<RoutePlannerController>.Instance);

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, TestAthleteId.ToString()) };
        var identity = new ClaimsIdentity(claims, "Test");
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        _sut.TempData = new TempDataDictionary(
            new DefaultHttpContext(),
            Mock.Of<ITempDataProvider>());
    }

    [TearDown]
    public void TearDown() => _sut.Dispose();

    // --- Index ---

    [Test]
    public async Task Index_ListsRoutesForCurrentAthlete()
    {
        _service.Setup(s => s.ListRoutesAsync(TestAthleteId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<PlannedRoute>());

        var result = await _sut.Index(CancellationToken.None);

        _service.Verify(s => s.ListRoutesAsync(TestAthleteId, It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(result, Is.InstanceOf<ViewResult>());
    }

    // --- Detail ---

    [Test]
    public async Task Detail_ValidId_ReturnsView()
    {
        var route = new PlannedRoute { Id = "r1", Name = "Test", AthleteId = TestAthleteId };
        _service.Setup(s => s.GetRouteAsync(TestAthleteId, "r1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(route);

        var result = await _sut.Detail("r1", CancellationToken.None);

        Assert.That(result, Is.InstanceOf<ViewResult>());
    }

    [Test]
    public async Task Detail_EmptyId_ReturnsBadRequest()
    {
        var result = await _sut.Detail("", CancellationToken.None);
        Assert.That(result, Is.InstanceOf<BadRequestResult>());
    }

    [Test]
    public async Task Detail_NotFound_ReturnsNotFound()
    {
        _service.Setup(s => s.GetRouteAsync(TestAthleteId, "missing", It.IsAny<CancellationToken>()))
                .ReturnsAsync((PlannedRoute?)null);

        var result = await _sut.Detail("missing", CancellationToken.None);

        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    // --- Save ---

    [Test]
    public async Task Save_ValidPayload_CallsServiceAndRedirects()
    {
        var waypointsJson = "[{\"Lat\":51.5,\"Lon\":-0.1},{\"Lat\":52.0,\"Lon\":0.0}]";
        _service.Setup(s => s.SaveRouteAsync(TestAthleteId, "My Route", It.IsAny<List<WaypointDto>>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PlannedRoute { Id = "new1", Name = "My Route" });

        var result = await _sut.Save("My Route", waypointsJson, null, CancellationToken.None);

        _service.Verify(s => s.SaveRouteAsync(TestAthleteId, "My Route", It.Is<List<WaypointDto>>(w => w.Count == 2), null, It.IsAny<CancellationToken>()), Times.Once);
        var redirect = result as RedirectToActionResult;
        Assert.That(redirect?.ActionName, Is.EqualTo("Index"));
    }

    [Test]
    public async Task Save_EmptyName_RedirectsWithoutCallingService()
    {
        var result = await _sut.Save("", "[{\"Lat\":1,\"Lon\":1},{\"Lat\":2,\"Lon\":2}]", null, CancellationToken.None);

        _service.Verify(s => s.SaveRouteAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<List<WaypointDto>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
    }

    [Test]
    public async Task Save_SingleWaypoint_RedirectsWithoutCallingService()
    {
        var result = await _sut.Save("Name", "[{\"Lat\":1,\"Lon\":1}]", null, CancellationToken.None);

        _service.Verify(s => s.SaveRouteAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<List<WaypointDto>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
    }

    // --- Delete ---

    [Test]
    public async Task Delete_ValidId_DeletesAndRedirectsToIndex()
    {
        _service.Setup(s => s.DeleteRouteAsync(TestAthleteId, "r1", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        var result = await _sut.Delete("r1", CancellationToken.None);

        _service.Verify(s => s.DeleteRouteAsync(TestAthleteId, "r1", It.IsAny<CancellationToken>()), Times.Once);
        var redirect = result as RedirectToActionResult;
        Assert.That(redirect?.ActionName, Is.EqualTo("Index"));
    }

    [Test]
    public async Task Delete_EmptyId_ReturnsBadRequest()
    {
        var result = await _sut.Delete("", CancellationToken.None);
        Assert.That(result, Is.InstanceOf<BadRequestResult>());
        _service.Verify(s => s.DeleteRouteAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- ApiList ---

    [Test]
    public async Task ApiList_ReturnsJsonRouteList()
    {
        var routes = new List<PlannedRoute>
        {
            new() { Id = "r1", Name = "Morning Ride", DistanceKm = 12.5, CreatedAt = DateTime.UtcNow, Waypoints = [new(), new()] }
        };
        _service.Setup(s => s.ListRoutesAsync(TestAthleteId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(routes);

        var result = await _sut.ApiList(CancellationToken.None);

        Assert.That(result, Is.InstanceOf<JsonResult>());
    }

    // --- ApiPoints ---

    [Test]
    public async Task ApiPoints_ValidId_ReturnsWaypointArray()
    {
        var route = new PlannedRoute
        {
            Id = "r1",
            Waypoints = [new() { Lat = 51.5, Lon = -0.1 }, new() { Lat = 52.0, Lon = 0.0 }]
        };
        _service.Setup(s => s.GetRouteAsync(TestAthleteId, "r1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(route);

        var result = await _sut.ApiPoints("r1", CancellationToken.None);

        Assert.That(result, Is.InstanceOf<JsonResult>());
    }

    [Test]
    public async Task ApiPoints_UnknownId_ReturnsNotFound()
    {
        _service.Setup(s => s.GetRouteAsync(TestAthleteId, "nope", It.IsAny<CancellationToken>()))
                .ReturnsAsync((PlannedRoute?)null);

        var result = await _sut.ApiPoints("nope", CancellationToken.None);

        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    [Test]
    public async Task ApiPoints_EmptyId_ReturnsBadRequest()
    {
        var result = await _sut.ApiPoints("", CancellationToken.None);
        Assert.That(result, Is.InstanceOf<BadRequestResult>());
    }
}
