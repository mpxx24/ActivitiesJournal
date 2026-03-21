using ActivitiesJournal.Configuration;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;

namespace ActivitiesJournal.Services;

public class AzureBlobStorageInitializer : IHostedService
{
    private readonly StorageOptions _options;
    private readonly ILogger<AzureBlobStorageInitializer> _logger;

    public AzureBlobStorageInitializer(IOptions<StorageOptions> options, ILogger<AzureBlobStorageInitializer> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_options.BlobEndpoint))
        {
            _logger.LogInformation("No BlobEndpoint configured — skipping container initialisation (local dev mode).");
            return;
        }

        await EnsureContainerAsync(BlobContainerNames.Goals, cancellationToken);
        await EnsureContainerAsync(BlobContainerNames.Tracks, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task EnsureContainerAsync(string containerName, CancellationToken ct)
    {
        var endpoint = _options.BlobEndpoint!.TrimEnd('/');
        var uri = new Uri($"{endpoint}/{containerName}");
        var client = new BlobContainerClient(uri, new DefaultAzureCredential());
        await client.CreateIfNotExistsAsync(cancellationToken: ct);
        _logger.LogInformation("Blob container '{Container}' is ready.", containerName);
    }
}
