using FluentValidation;

namespace GameGuild.Assets.Commands;

/// <summary>
/// Command to delete an asset reference.
/// </summary>
public sealed record DeleteAssetCommand(
    Guid AssetReferenceId,
    Guid UserId,
    bool ForceDelete = false) : ICommand<DeleteAssetResponse>;

public sealed record DeleteAssetResponse(
    bool Success,
    bool ContentMarkedForDeletion);

public sealed class DeleteAssetValidator : AbstractValidator<DeleteAssetCommand>
{
    public DeleteAssetValidator()
    {
        RuleFor(x => x.AssetReferenceId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public sealed class DeleteAssetHandler : ICommandHandler<DeleteAssetCommand, DeleteAssetResponse>
{
    private readonly IAssetReferenceRepository _referenceRepository;
    private readonly IAssetContentRepository _contentRepository;

    public DeleteAssetHandler(
        IAssetReferenceRepository referenceRepository,
        IAssetContentRepository contentRepository)
    {
        _referenceRepository = referenceRepository;
        _contentRepository = contentRepository;
    }

    public async Task<DeleteAssetResponse> Handle(
        DeleteAssetCommand request,
        CancellationToken ct = default)
    {
        var reference = await _referenceRepository.GetByIdAsync(request.AssetReferenceId, ct).ConfigureAwait(false);
        if (reference == null)
        {
            return new DeleteAssetResponse(false, false);
        }

        // Verify ownership (admin override with ForceDelete)
        if (!request.ForceDelete && 
            !await _referenceRepository.IsOwnedByUserAsync(request.AssetReferenceId, request.UserId, ct))
        {
            return new DeleteAssetResponse(false, false);
        }

        var contentId = reference.AssetContentId;

        reference.AddIntegrationEvent(new AssetReferenceRemovedEvent(reference.Id, contentId)
        {
            TenantId = reference.TenantId ?? DurableIntegrationEventTenants.Platform,
            ActorId = request.UserId,
            AggregateType = nameof(AssetReference),
            AggregateId = reference.Id.ToString(),
            CorrelationId = Guid.NewGuid()
        });

        // Soft delete the reference
        await _referenceRepository.DeleteAsync(request.AssetReferenceId, ct).ConfigureAwait(false);

        // Decrement content reference count (may mark for garbage collection)
        await _contentRepository.DecrementReferenceCountAsync(contentId, ct).ConfigureAwait(false);

        // Check if content is now marked for deletion
        var content = await _contentRepository.GetByIdAsync(contentId, ct).ConfigureAwait(false);
        var contentMarkedForDeletion = content?.MarkedForDeletionAt != null;

        return new DeleteAssetResponse(true, contentMarkedForDeletion);
    }
}
