using ActivitiesJournal.Configuration;
using Microsoft.Extensions.Options;

namespace ActivitiesJournal.Services;

public class TrackMigrationService : IHostedService
{
    private readonly ITrackStorageService _trackStorage;
    private readonly TrackOwnerOptions _ownerOptions;
    private readonly ILogger<TrackMigrationService> _logger;

    public TrackMigrationService(
        ITrackStorageService trackStorage,
        IOptions<TrackOwnerOptions> ownerOptions,
        ILogger<TrackMigrationService> logger)
    {
        _trackStorage = trackStorage;
        _ownerOptions = ownerOptions.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_ownerOptions.OwnerAthleteId == 0)
        {
            _logger.LogWarning("TrackOwner:OwnerAthleteId not configured — skipping track migration");
            return;
        }

        try
        {
            await _trackStorage.MigrateToAthletePathsAsync(_ownerOptions.OwnerAthleteId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Track migration failed");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
