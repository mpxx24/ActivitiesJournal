using ActivitiesJournal.Models;

namespace ActivitiesJournal.Services;

public interface IRoutingService
{
    Task<IReadOnlyList<WaypointDto>?> GenerateRouteAsync(
        IReadOnlyList<WaypointDto> points,
        CancellationToken ct = default);
}
