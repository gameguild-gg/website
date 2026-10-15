using FluentValidation;

namespace GameGuild.Assets.Commands;

/// <summary>
/// Emitted when an asset reference is attached to or detached from a parent resource.
/// </summary>
public sealed record AssetReferenceReboundEvent(
    [property: NonPersonalEventData] Guid AssetReferenceId = default,
    [property: NonPersonalEventData] string? ParentResourceType = null,
    [property: NonPersonalEventData] Guid? ParentResourceId = null) : DurableIntegrationEventBase
{
    public override string EventName => "assets.reference.rebound.v1";
    public override string SourceModule => "Assets";
}

/// <summary>
/// Command to attach an asset reference to a parent resource, or detach it (nulls).
/// </summary>
public sealed record RebindAssetParentCommand(
    Guid AssetReferenceId,
    Guid UserId,
    Guid? TenantId,
    string? ParentResourceType = null,
    Guid? ParentResourceId = null) : ICommand<RebindAssetParentResponse>;

public sealed record RebindAssetParentResponse(
    Guid AssetReferenceId,
    string? ParentResourceType,
    Guid? ParentResourceId);

public sealed class RebindAssetParentValidator : AbstractValidator<RebindAssetParentCommand>
{
    public RebindAssetParentValidator()
    {
        RuleFor(x => x.AssetReferenceId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.ParentResourceType).MaximumLength(100).When(x => x.ParentResourceType != null);
    }
}

public sealed class RebindAssetParentHandler : ICommandHandler<RebindAssetParentCommand, RebindAssetParentResponse?>
{
    private const int ParentResourceTypeMaxLength = 100;

    private readonly IAssetReferenceRepository _referenceRepository;
    private readonly IUseCaseOperationContextAccessor? _operationContextAccessor;

    public RebindAssetParentHandler(
        IAssetReferenceRepository referenceRepository,
        IUseCaseOperationContextAccessor? operationContextAccessor = null)
    {
        _referenceRepository = referenceRepository;
        _operationContextAccessor = operationContextAccessor;
    }

    public async Task<RebindAssetParentResponse?> Handle(
        RebindAssetParentCommand request,
        CancellationToken ct = default)
    {
        var reference = await _referenceRepository.GetByIdAsync(request.AssetReferenceId, ct).ConfigureAwait(false);
        if (reference == null)
        {
            return null;
        }

        // Cross-tenant rebind denied; tenant comes from the actor context, not the route.
        if (reference.TenantId.HasValue &&
            request.TenantId.HasValue &&
            reference.TenantId.Value != request.TenantId.Value)
        {
            return null;
        }

        // Verify ownership
        if (!await _referenceRepository.IsOwnedByUserAsync(request.AssetReferenceId, request.UserId, ct))
        {
            return null;
        }

        // Normalize: whitespace-only type is a detach; trim otherwise.
        var parentResourceType = request.ParentResourceType?.Trim();
        if (string.IsNullOrEmpty(parentResourceType))
        {
            parentResourceType = null;
        }

        reference.ParentResourceType = parentResourceType;
        reference.ParentResourceId = parentResourceType == null ? null : request.ParentResourceId;

        var operation = _operationContextAccessor?.Current;
        reference.AddIntegrationEvent(new AssetReferenceReboundEvent(reference.Id, parentResourceType, reference.ParentResourceId)
        {
            TenantId = reference.TenantId ?? DurableIntegrationEventTenants.Platform,
            ActorId = request.UserId,
            AggregateType = nameof(AssetReference),
            AggregateId = reference.Id.ToString(),
            CorrelationId = operation?.CorrelationId ?? Guid.NewGuid(),
            CausationId = operation?.CausationId,
        });

        await _referenceRepository.UpdateAsync(reference, ct).ConfigureAwait(false);

        return new RebindAssetParentResponse(reference.Id, reference.ParentResourceType, reference.ParentResourceId);
    }
}
