namespace GameGuild.Assets.Commands;

public sealed record SetAssetLegalHoldCommand(
    Guid AssetContentId,
    bool Enabled,
    string? Reason = null) : ICommand<AssetLegalHoldResponse?>;

public sealed record AssetLegalHoldResponse(
    Guid AssetContentId,
    bool IsDeletable,
    bool LegalHoldEnabled,
    string? Reason);

public sealed class SetAssetLegalHoldHandler(
    IAssetContentRepository contentRepository) : ICommandHandler<SetAssetLegalHoldCommand, AssetLegalHoldResponse?>
{
    public async Task<AssetLegalHoldResponse?> Handle(SetAssetLegalHoldCommand request, CancellationToken ct = default)
    {
        var content = await contentRepository.GetByIdAsync(request.AssetContentId, ct).ConfigureAwait(false);
        if (content is null)
        {
            return null;
        }

        if (request.Enabled)
        {
            content.MarkAsNonDeletable(request.Reason);
        }
        else
        {
            content.MarkAsDeletable();
        }

        await contentRepository.UpdateAsync(content, ct).ConfigureAwait(false);

        return new AssetLegalHoldResponse(
            content.Id,
            content.IsDeletable,
            !content.IsDeletable,
            request.Enabled ? request.Reason : null);
    }
}
