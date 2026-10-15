namespace GameGuild.Assets;

/// <summary>
/// Fail-closed default: without a host-registered classifier, every
/// classification is a failure and the decision policy routes items to
/// NeedsReview (never auto-approve).
/// </summary>
public sealed class UnresolvedModerationClassifier : IAssetModerationClassifier
{
    public Task<AssetModerationClassification> ClassifyAsync(
        AssetContent content, Stream imageBytes, CancellationToken ct = default) =>
        Task.FromResult(new AssetModerationClassification(true, [], "unresolved"));
}
