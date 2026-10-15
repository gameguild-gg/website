using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using GameGuild.Assets.Openinary;

namespace GameGuild.Assets.BackgroundServices;

/// <summary>
/// Processes one moderation queue cycle: claims clean image content via CAS,
/// classifies it, applies the threshold policy, persists the decision, purges
/// rejected binaries, and prewarms approved variants. Serialized (one item at
/// a time) — the backing vLLM lane runs with a tiny concurrency budget.
/// </summary>
public interface IAssetModerationProcessor
{
    Task<int> ProcessPendingAsync(int batchSize, CancellationToken ct = default);
}

public sealed class ModerationClassificationProcessor(
    IAssetContentRepository contentRepository,
    IAssetModerationClassifier classifier,
    IAssetStorageService storageService,
    IOpeninaryClient openinaryClient,
    IAssetAccessService accessService,
    IOptions<AutoModerationOptions> options,
    ILogger<ModerationClassificationProcessor> logger) : IAssetModerationProcessor
{
    private readonly AutoModerationOptions _options = options.Value;
    private readonly ConcurrentDictionary<Guid, int> _consecutiveFailures = new();

    public async Task<int> ProcessPendingAsync(int batchSize, CancellationToken ct = default)
    {
        var candidates = await contentRepository
            .GetPendingModerationQueueAsync(Math.Max(1, batchSize), ct)
            .ConfigureAwait(false);
        var processed = 0;

        foreach (var content in candidates)
        {
            if (content.VirusScanStatus != VirusScanStatus.Clean)
            {
                continue;
            }

            if (!content.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                await HandleNonImageAsync(content, ct).ConfigureAwait(false);
                processed++;
                continue;
            }

            if (!await contentRepository.TryBeginModerationAsync(content.Id, ct).ConfigureAwait(false))
            {
                continue;
            }

            content.ModerationStatus = ModerationStatus.Processing;

            try
            {
                await ProcessItemAsync(content, ct).ConfigureAwait(false);
                processed++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Moderation cycle failed for content {ContentId}", content.Id);
                await HandleFailureAsync(content, ex.Message, ct).ConfigureAwait(false);
            }
        }

        return processed;
    }

    private async Task ProcessItemAsync(AssetContent content, CancellationToken ct)
    {
        AssetModerationClassification classification;
        using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.ClassificationTimeoutSeconds)));
            try
            {
                await using var imageBytes = await DownloadOriginalAsync(content, timeoutCts.Token).ConfigureAwait(false);
                classification = await classifier.ClassifyAsync(content, imageBytes, timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                classification = new AssetModerationClassification(true, [], "timeout");
            }
            catch (ModerationClassificationException ex)
            {
                classification = new AssetModerationClassification(true, [], "unparseable");
                logger.LogWarning("Classification unparseable for {ContentId}: {Message}", content.Id, ex.Message);
            }
        }

        var decision = ModerationDecisionPolicy.Decide(classification, _options, content.MimeType);
        ApplyDecision(content, classification, decision);
        await contentRepository.UpdateAsync(content, ct).ConfigureAwait(false);
        _consecutiveFailures.TryRemove(content.Id, out _);

        if (decision.Status == ModerationStatus.Approved)
        {
            await accessService.PrewarmDefaultVariantsAsync(content, ct).ConfigureAwait(false);
        }
        else if (decision.Status == ModerationStatus.Rejected)
        {
            await PurgeBinaryAsync(content, ct).ConfigureAwait(false);
        }
    }

    private async Task<Stream> DownloadOriginalAsync(AssetContent content, CancellationToken ct)
    {
        if (AssetStorageBackend.IsOpeninary(content))
        {
            var bytes = await openinaryClient.DownloadAsync(content.ObjectKey, ct).ConfigureAwait(false);
            return new MemoryStream(bytes);
        }

        return await storageService.DownloadAsync(content.BucketName, content.ObjectKey, ct).ConfigureAwait(false);
    }

    private void ApplyDecision(AssetContent content, AssetModerationClassification classification, ModerationDecision decision)
    {
        var scores = string.Join(",", classification.Labels.Select(l => $"{l.Name}:{l.Score:0.00}"));
        content.SetModerationStatus(decision.Status);
        content.SetModerationLabels(classification.Labels.Select(l => l.Name));
        content.ModerationReviewNotes = $"model={classification.Model};scores={scores};reason={decision.Reason}";
    }

    private async Task PurgeBinaryAsync(AssetContent content, CancellationToken ct)
    {
        try
        {
            if (AssetStorageBackend.IsOpeninary(content))
            {
                await openinaryClient.DeleteAsync(content.ObjectKey, ct).ConfigureAwait(false);
            }
            else
            {
                await storageService.DeleteAsync(content.BucketName, content.ObjectKey, ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Rejected-content binary purge failed for {ContentId} (status already persisted)", content.Id);
        }
    }

    private async Task HandleNonImageAsync(AssetContent content, CancellationToken ct)
    {
        content.SetModerationStatus(ModerationStatus.NeedsReview);
        content.SetModerationLabels(["manual_review_media_type"]);
        content.ModerationReviewNotes = $"model=none;scores=;reason=non-image media type '{content.MimeType}' requires manual review";
        await contentRepository.UpdateAsync(content, ct).ConfigureAwait(false);
    }

    private async Task HandleFailureAsync(AssetContent content, string message, CancellationToken ct)
    {
        var failures = _consecutiveFailures.AddOrUpdate(content.Id, 1, (_, count) => count + 1);
        if (failures >= Math.Max(1, _options.MaxConsecutiveFailures))
        {
            content.SetModerationStatus(ModerationStatus.NeedsReview);
            content.SetModerationLabels(["classifier_failure"]);
            content.ModerationReviewNotes = $"model=none;scores=;reason=classifier failed {failures} consecutive times: {message}";
            await contentRepository.UpdateAsync(content, ct).ConfigureAwait(false);
            _consecutiveFailures.TryRemove(content.Id, out _);
        }
        else
        {
            content.ModerationStatus = ModerationStatus.Pending;
            await contentRepository.UpdateAsync(content, ct).ConfigureAwait(false);
        }
    }
}
