namespace ActivitiesJournal.Services;

public class TokenStoreInitializer : IHostedService
{
    private readonly ITokenStore _tokenStore;
    private readonly ILogger<TokenStoreInitializer> _logger;

    public TokenStoreInitializer(ITokenStore tokenStore, ILogger<TokenStoreInitializer> logger)
    {
        _tokenStore = tokenStore;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Loading persisted tokens from blob storage.");
        await _tokenStore.LoadAllFromBlobAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
