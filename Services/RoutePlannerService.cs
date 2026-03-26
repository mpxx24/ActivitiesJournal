using System.Text;
using System.Text.Json;
using ActivitiesJournal.Configuration;
using ActivitiesJournal.Models;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;

namespace ActivitiesJournal.Services;

public class RoutePlannerService : IRoutePlannerService
{
    private readonly BlobContainerClient? _containerClient;
    private readonly ILogger<RoutePlannerService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public RoutePlannerService(IOptions<StorageOptions> storageOptions, ILogger<RoutePlannerService> logger)
    {
        _logger = logger;
        var blobEndpoint = storageOptions.Value.BlobEndpoint;
        if (!string.IsNullOrEmpty(blobEndpoint))
        {
            _containerClient = new BlobContainerClient(
                new Uri($"{blobEndpoint.TrimEnd('/')}/{BlobContainerNames.PlannedRoutes}"),
                new DefaultAzureCredential());
        }
    }

    // Test-only constructor
    internal RoutePlannerService(BlobContainerClient containerClient, ILogger<RoutePlannerService> logger)
    {
        _containerClient = containerClient;
        _logger = logger;
    }

    private static string RouteBlobPath(long athleteId, string routeId) => $"{athleteId}/{routeId}.json";

    public async Task<PlannedRoute> SaveRouteAsync(long athleteId, string name, List<WaypointDto> waypoints, string? existingId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(waypoints);

        if (_containerClient == null)
            throw new InvalidOperationException("Blob storage is not configured.");

        PlannedRoute? existing = null;
        if (!string.IsNullOrEmpty(existingId))
            existing = await GetRouteAsync(athleteId, existingId, ct);

        var route = new PlannedRoute
        {
            Id = existing?.Id ?? Guid.NewGuid().ToString(),
            AthleteId = athleteId,
            Name = name,
            CreatedAt = existing?.CreatedAt ?? DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            DistanceKm = ComputeDistanceKm(waypoints),
            Waypoints = waypoints
        };

        var json = JsonSerializer.Serialize(route, JsonOptions);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var blobClient = _containerClient.GetBlobClient(RouteBlobPath(athleteId, route.Id));
        await blobClient.UploadAsync(stream, overwrite: true, cancellationToken: ct);

        _logger.LogInformation("Saved planned route {RouteId} '{Name}' for athlete {AthleteId} ({DistanceKm:F1} km, {WaypointCount} waypoints)",
            route.Id, route.Name, athleteId, route.DistanceKm, route.Waypoints.Count);

        return route;
    }

    public async Task<IReadOnlyList<PlannedRoute>> ListRoutesAsync(long athleteId, CancellationToken ct = default)
    {
        if (_containerClient == null)
            return Array.Empty<PlannedRoute>();

        var prefix = $"{athleteId}/";
        var routes = new List<PlannedRoute>();

        await foreach (var blob in _containerClient.GetBlobsAsync(
            traits: Azure.Storage.Blobs.Models.BlobTraits.None,
            states: Azure.Storage.Blobs.Models.BlobStates.None,
            prefix: prefix,
            cancellationToken: ct))
        {
            try
            {
                var blobClient = _containerClient.GetBlobClient(blob.Name);
                var response = await blobClient.DownloadContentAsync(cancellationToken: ct);
                var route = JsonSerializer.Deserialize<PlannedRoute>(response.Value.Content.ToString(), JsonOptions);
                if (route != null)
                    routes.Add(route);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load planned route from {BlobName}", blob.Name);
            }
        }

        return routes.OrderByDescending(r => r.CreatedAt).ToList();
    }

    public async Task<PlannedRoute?> GetRouteAsync(long athleteId, string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        if (_containerClient == null)
            return null;

        try
        {
            var blobClient = _containerClient.GetBlobClient(RouteBlobPath(athleteId, id));
            var response = await blobClient.DownloadContentAsync(cancellationToken: ct);
            return JsonSerializer.Deserialize<PlannedRoute>(response.Value.Content.ToString(), JsonOptions);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get planned route {RouteId}", id);
            throw;
        }
    }

    public async Task DeleteRouteAsync(long athleteId, string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        if (_containerClient == null)
            throw new InvalidOperationException("Blob storage is not configured.");

        await _containerClient.GetBlobClient(RouteBlobPath(athleteId, id)).DeleteIfExistsAsync(cancellationToken: ct);

        _logger.LogInformation("Deleted planned route {RouteId} for athlete {AthleteId}", id, athleteId);
    }

    internal static double ComputeDistanceKm(List<WaypointDto> waypoints)
    {
        if (waypoints.Count < 2)
            return 0.0;

        const double R = 6371.0;
        var total = 0.0;

        for (var i = 0; i < waypoints.Count - 1; i++)
        {
            var lat1 = waypoints[i].Lat * Math.PI / 180.0;
            var lat2 = waypoints[i + 1].Lat * Math.PI / 180.0;
            var dLat = (waypoints[i + 1].Lat - waypoints[i].Lat) * Math.PI / 180.0;
            var dLon = (waypoints[i + 1].Lon - waypoints[i].Lon) * Math.PI / 180.0;

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                  + Math.Cos(lat1) * Math.Cos(lat2)
                  * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            total += 2 * R * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        return total;
    }
}
