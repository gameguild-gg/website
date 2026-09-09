using FluentValidation;

namespace GameGuild.Assets.Commands;

/// <summary>
/// Command to update an asset reference.
/// </summary>
public sealed record UpdateAssetCommand(
    Guid AssetReferenceId,
    Guid UserId,
    string? DisplayName = null,
    AssetAccessPolicy? AccessPolicy = null) : ICommand<UpdateAssetResponse?>;

public sealed record UpdateAssetResponse(
    Guid AssetReferenceId,
    string? DisplayName,
    AssetAccessPolicy AccessPolicy);

public sealed class UpdateAssetValidator : AbstractValidator<UpdateAssetCommand>
{
    public UpdateAssetValidator()
    {
        RuleFor(x => x.AssetReferenceId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.DisplayName).MaximumLength(255).When(x => x.DisplayName != null);
    }
}

public sealed class UpdateAssetHandler : ICommandHandler<UpdateAssetCommand, UpdateAssetResponse?>
{
    private readonly IAssetReferenceRepository _referenceRepository;

    public UpdateAssetHandler(IAssetReferenceRepository referenceRepository)
    {
        _referenceRepository = referenceRepository;
    }

    public async Task<UpdateAssetResponse?> Handle(
        UpdateAssetCommand request,
        CancellationToken ct = default)
    {
        var reference = await _referenceRepository.GetByIdAsync(request.AssetReferenceId, ct).ConfigureAwait(false);
        if (reference == null)
        {
            return null;
        }

        // Verify ownership
        if (!await _referenceRepository.IsOwnedByUserAsync(request.AssetReferenceId, request.UserId, ct))
        {
            return null;
        }

        // Apply updates
        if (request.DisplayName != null)
        {
            reference.UpdateDisplayName(request.DisplayName);
        }

        if (request.AccessPolicy.HasValue)
        {
            reference.UpdateAccessPolicy(request.AccessPolicy.Value);
        }

        await _referenceRepository.UpdateAsync(reference, ct).ConfigureAwait(false);

        return new UpdateAssetResponse(
            reference.Id,
            reference.DisplayName,
            reference.AccessPolicy);
    }
}
