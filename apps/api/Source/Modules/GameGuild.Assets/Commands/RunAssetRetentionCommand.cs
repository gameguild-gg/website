namespace GameGuild.Assets.Commands;

public sealed record RunAssetRetentionCommand(
    int GracePeriodHours = 24,
    int Limit = 100,
    bool DryRun = false) : IRequest<AssetRetentionRunResponse>;

public sealed record AssetRetentionRunResponse(
    int CandidatesFound,
    int Deleted,
    int Failed,
    bool DryRun,
    IReadOnlyList<string> Errors);

public sealed class RunAssetRetentionHandler(
    IAssetContentRepository contentRepository,
    IAssetStorageService storageService,
    ITransformedAssetRepository transformedRepository) : IRequestHandler<RunAssetRetentionCommand, AssetRetentionRunResponse>
{
    public async Task<AssetRetentionRunResponse> Handle(
        RunAssetRetentionCommand request,
        CancellationToken ct = default)
    {
        var gracePeriod = TimeSpan.FromHours(Math.Clamp(request.GracePeriodHours, 1, 24 * 365));
        var limit = Math.Clamp(request.Limit, 1, 10_000);
        var candidates = await contentRepository
            .GetGarbageCollectionCandidatesAsync(gracePeriod, limit, ct)
            .ConfigureAwait(false);

        if (request.DryRun)
        {
            return new AssetRetentionRunResponse(candidates.Count, 0, 0, true, []);
        }

        var deleted = 0;
        var failed = 0;
        var errors = new List<string>();

        foreach (var content in candidates)
        {
            try
            {
                await transformedRepository.DeleteBySourceAsync(content.Id, ct).ConfigureAwait(false);
                await storageService.DeleteAsync(content.BucketName, content.ObjectKey, ct).ConfigureAwait(false);
                content.AddIntegrationEvent(new AssetObjectDeletedEvent(
                    content.Id,
                    content.ContentHash,
                    content.SizeBytes,
                    "object-storage")
                {
                    TenantId = content.TenantId ?? DurableIntegrationEventTenants.Platform,
                    ActorId = DurableIntegrationEventActors.System,
                    AggregateType = nameof(AssetContent),
                    AggregateId = content.Id.ToString(),
                    CorrelationId = Guid.NewGuid()
                });
                await contentRepository.DeleteAsync(content.Id, ct).ConfigureAwait(false);
                deleted++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                errors.Add($"{content.Id}: {ex.Message}");
            }
        }

        return new AssetRetentionRunResponse(candidates.Count, deleted, failed, false, errors);
    }
}
