using Microsoft.Extensions.Logging;

namespace GameGuild.Assets.BackgroundServices;

public interface IAssetVirusScanProcessor
{
    Task<int> ProcessPendingAsync(int batchSize, CancellationToken ct = default);
}

public sealed class AssetVirusScanProcessor(
    IAssetContentRepository contentRepository,
    IAssetStorageService storageService,
    IVirusScanService virusScanService,
    ILogger<AssetVirusScanProcessor> logger) : IAssetVirusScanProcessor
{
    public async Task<int> ProcessPendingAsync(int batchSize, CancellationToken ct = default)
    {
        var candidates = await contentRepository
            .GetPendingVirusScanAsync(Math.Max(1, batchSize), ct)
            .ConfigureAwait(false);
        var processed = 0;

        foreach (var content in candidates)
        {
            if (!await contentRepository.TryBeginVirusScanAsync(content.Id, ct).ConfigureAwait(false))
            {
                continue;
            }

            try
            {
                await using var stream = await storageService
                    .DownloadAsync(content.BucketName, content.ObjectKey, ct)
                    .ConfigureAwait(false);
                var result = await virusScanService
                    .ScanAsync(stream, content.ObjectKey, ct)
                    .ConfigureAwait(false);

                var status = result.IsClean
                    ? VirusScanStatus.Clean
                    : IsScannerFailure(result)
                        ? VirusScanStatus.ScanFailed
                        : VirusScanStatus.Infected;
                content.SetVirusScanStatus(status, result.Status);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Virus scan failed for asset content {ContentId}", content.Id);
                content.SetVirusScanStatus(VirusScanStatus.ScanFailed, ex.Message);
            }

            await contentRepository.UpdateAsync(content, ct).ConfigureAwait(false);
            processed++;
        }

        return processed;
    }

    private static bool IsScannerFailure(VirusScanResult result)
    {
        return string.Equals(result.ThreatType, "Configuration", StringComparison.OrdinalIgnoreCase)
            || result.Status.Contains("unavailable", StringComparison.OrdinalIgnoreCase)
            || result.Status.Contains("failed", StringComparison.OrdinalIgnoreCase);
    }
}
