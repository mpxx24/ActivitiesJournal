using System.Net;
using ActivitiesJournal.Configuration;
using ActivitiesJournal.Models;
using ActivitiesJournal.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace ActivitiesJournal.Tests;

[TestFixture]
public class RoutingServiceTests
{
    private Mock<HttpMessageHandler> _handlerMock = null!;
    private Mock<IHttpClientFactory> _factoryMock = null!;
    private RoutingService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _handlerMock = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(_handlerMock.Object)
        {
            BaseAddress = new Uri("https://api.openrouteservice.org/")
        };

        _factoryMock = new Mock<IHttpClientFactory>();
        _factoryMock.Setup(f => f.CreateClient("ors")).Returns(httpClient);

        var options = Options.Create(new OpenRouteServiceOptions { ApiKey = "test-key" });
        _sut = new RoutingService(options, _factoryMock.Object, NullLogger<RoutingService>.Instance);
    }

    private void SetupOrsResponse(HttpStatusCode statusCode, string body)
    {
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            });
    }

    [Test]
    public async Task GenerateRouteAsync_ValidResponse_ReturnsCorrectWaypoints()
    {
        const string orsResponse = """
            {
              "type": "FeatureCollection",
              "features": [{
                "type": "Feature",
                "geometry": {
                  "type": "LineString",
                  "coordinates": [
                    [14.5, 50.1, 200.0],
                    [14.51, 50.11, 210.0],
                    [14.52, 50.12, 220.0]
                  ]
                }
              }]
            }
            """;
        SetupOrsResponse(HttpStatusCode.OK, orsResponse);

        var result = await _sut.GenerateRouteAsync(
            new[] { new WaypointDto { Lat = 50.1, Lon = 14.5 }, new WaypointDto { Lat = 50.12, Lon = 14.52 } });

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(3));
        Assert.That(result[0].Lat, Is.EqualTo(50.1));
        Assert.That(result[0].Lon, Is.EqualTo(14.5));
        Assert.That(result[2].Lat, Is.EqualTo(50.12));
        Assert.That(result[2].Lon, Is.EqualTo(14.52));
    }

    [Test]
    public async Task GenerateRouteAsync_OrsReturnsUnauthorized_ReturnsNull()
    {
        SetupOrsResponse(HttpStatusCode.Unauthorized, """{"error": "Invalid API key"}""");

        var result = await _sut.GenerateRouteAsync(
            new[] { new WaypointDto { Lat = 50.1, Lon = 14.5 }, new WaypointDto { Lat = 50.12, Lon = 14.52 } });

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GenerateRouteAsync_OrsReturnsServerError_ReturnsNull()
    {
        SetupOrsResponse(HttpStatusCode.InternalServerError, """{"error": "Server error"}""");

        var result = await _sut.GenerateRouteAsync(
            new[] { new WaypointDto { Lat = 50.1, Lon = 14.5 }, new WaypointDto { Lat = 50.12, Lon = 14.52 } });

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GenerateRouteAsync_EmptyCoordinates_ReturnsEmptyList()
    {
        const string orsResponse = """
            {
              "type": "FeatureCollection",
              "features": [{
                "type": "Feature",
                "geometry": {
                  "type": "LineString",
                  "coordinates": []
                }
              }]
            }
            """;
        SetupOrsResponse(HttpStatusCode.OK, orsResponse);

        var result = await _sut.GenerateRouteAsync(
            new[] { new WaypointDto { Lat = 50.1, Lon = 14.5 }, new WaypointDto { Lat = 50.12, Lon = 14.52 } });

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Count, Is.EqualTo(0));
    }
}
