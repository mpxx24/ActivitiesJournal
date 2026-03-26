using System.Text;
using System.Text.Json;
using ActivitiesJournal.Configuration;
using ActivitiesJournal.Models;
using Microsoft.Extensions.Options;

namespace ActivitiesJournal.Services;

public class RoutingService : IRoutingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RoutingService> _logger;

    public RoutingService(
        IOptions<OpenRouteServiceOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<RoutingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<WaypointDto>?> GenerateRouteAsync(
        IReadOnlyList<WaypointDto> points,
        CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new
        {
            coordinates = points.Select(p => new[] { p.Lon, p.Lat }).ToArray()
        });

        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        var client = _httpClientFactory.CreateClient("ors");

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync("v2/directions/cycling-regular/geojson", content, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP error calling OpenRouteService");
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("OpenRouteService returned {StatusCode} for route generation", response.StatusCode);
            return null;
        }

        try
        {
            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            var coords = doc.RootElement
                .GetProperty("features")[0]
                .GetProperty("geometry")
                .GetProperty("coordinates");

            var waypoints = new List<WaypointDto>();
            foreach (var coord in coords.EnumerateArray())
            {
                var arr = coord.EnumerateArray().ToArray();
                waypoints.Add(new WaypointDto { Lat = arr[1].GetDouble(), Lon = arr[0].GetDouble() });
            }

            return waypoints;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse OpenRouteService response");
            return null;
        }
    }
}
