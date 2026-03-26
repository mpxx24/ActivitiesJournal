using ActivitiesJournal.Models;

namespace ActivitiesJournal.Services;

public interface IRoutePlannerService
{
    Task<PlannedRoute> SaveRouteAsync(long athleteId, string name, List<WaypointDto> waypoints, string? existingId, CancellationToken ct = default);
    Task<IReadOnlyList<PlannedRoute>> ListRoutesAsync(long athleteId, CancellationToken ct = default);
    Task<PlannedRoute?> GetRouteAsync(long athleteId, string id, CancellationToken ct = default);
    Task DeleteRouteAsync(long athleteId, string id, CancellationToken ct = default);
}
