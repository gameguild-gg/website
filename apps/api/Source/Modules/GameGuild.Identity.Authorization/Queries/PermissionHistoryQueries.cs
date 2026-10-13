using GameGuild.CQRS;
using GameGuild.CQRS.Models;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization.Queries;

// ==================== POINT-IN-TIME RECONSTRUCTION (issue #189) ====================

/// <summary>
///     Query reconstructing a subject's effective permission state at a past instant
///     (issue #189: time-based permission state reconstruction).
/// </summary>
public sealed record GetPermissionHistoryAtQuery : IQuery<PermissionHistoryAtSnapshot>
{
    /// <summary>
    ///     Gets the requested instant (UTC, inclusive: changes recorded at exactly this
    ///     timestamp are included).
    /// </summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    ///     Gets the subject user (defaults to the caller).
    /// </summary>
    public Guid? UserId { get; init; }
}

/// <summary>
///     Handler for <see cref="GetPermissionHistoryAtQuery"/>.
/// </summary>
public sealed class GetPermissionHistoryAtQueryHandler(
    IPermissionHistoryReconstructionService reconstructionService,
    IActorContextAccessor actorContextAccessor,
    IAuthorizationTenantContext tenantContext,
    ILogger<GetPermissionHistoryAtQueryHandler> logger)
    : IQueryHandler<GetPermissionHistoryAtQuery, PermissionHistoryAtSnapshot>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    /// <inheritdoc />
    public async Task<PermissionHistoryAtSnapshot> Handle(
        GetPermissionHistoryAtQuery request,
        CancellationToken cancellationToken)
    {
        var targetUserId = ResolveTargetUser(request.UserId);
        await EnsureAuthorizedAsync(targetUserId, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Reconstructing permission history for user {UserId} at {Timestamp}",
            targetUserId,
            request.Timestamp);

        return await reconstructionService
            .ReconstructAtAsync(tenantContext.TenantId, targetUserId, request.Timestamp, cancellationToken)
            .ConfigureAwait(false);
    }

    private Guid ResolveTargetUser(Guid? requestedUserId)
    {
        if (requestedUserId is { } target && target != Guid.Empty)
        {
            return target;
        }

        return Actor.SubjectIdAsGuid
            ?? throw new UnauthorizedAccessException("User not authenticated");
    }

    private async Task EnsureAuthorizedAsync(Guid targetUserId, CancellationToken cancellationToken)
    {
        if (Actor.IsSystemAdmin || Actor.SubjectIdAsGuid == targetUserId)
        {
            return;
        }

        // Fail closed: reconstructing another member's effective permissions at a past
        // instant is a compliance-administration capability only.
        _ = cancellationToken;
        await Task.CompletedTask.ConfigureAwait(false);
        throw new UnauthorizedAccessException(
            "Viewing another user's permission history requires system administration.");
    }
}

// ==================== INHERITANCE HISTORY (issue #189) ====================

/// <summary>
///     Query listing role-inheritance change events (issue #189: inheritance history
///     tracking showing parent-child relationship changes over time). Each event carries
///     the pre/post hierarchy state and the cascading effects recorded at mutation time.
/// </summary>
public sealed record GetInheritanceHistoryQuery : IQuery<IReadOnlyList<InheritanceHistoryEvent>>
{
    /// <summary>
    ///     Gets the window start (inclusive), if bounded.
    /// </summary>
    public DateTime? FromTimestamp { get; init; }

    /// <summary>
    ///     Gets the window end (inclusive), if bounded.
    /// </summary>
    public DateTime? ToTimestamp { get; init; }
}

/// <summary>
///     Handler for <see cref="GetInheritanceHistoryQuery"/>.
/// </summary>
public sealed class GetInheritanceHistoryQueryHandler(
    IPermissionHistoryReconstructionService reconstructionService,
    IActorContextAccessor actorContextAccessor,
    IAuthorizationTenantContext tenantContext)
    : IQueryHandler<GetInheritanceHistoryQuery, IReadOnlyList<InheritanceHistoryEvent>>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    /// <inheritdoc />
    public Task<IReadOnlyList<InheritanceHistoryEvent>> Handle(
        GetInheritanceHistoryQuery request,
        CancellationToken cancellationToken)
    {
        if (!Actor.IsSystemAdmin)
        {
            throw new UnauthorizedAccessException(
                "Role-inheritance history requires system administration.");
        }

        return reconstructionService.GetInheritanceHistoryAsync(
            tenantContext.TenantId,
            request.FromTimestamp,
            request.ToTimestamp,
            cancellationToken);
    }
}

// ==================== POLICY VERSION HISTORY (issue #188) ====================

/// <summary>
///     Query listing the append-only version history of one permission policy
///     (issue #188: policy change tracking with complete version history).
/// </summary>
public sealed record GetPolicyVersionHistoryQuery : IQuery<List<PolicyVersionHistory>>
{
    /// <summary>
    ///     Gets the policy whose versions are requested.
    /// </summary>
    public required Guid PolicyId { get; init; }

    /// <summary>
    ///     Gets the policy family to disambiguate ids shared across kinds.
    /// </summary>
    public PolicyVersionKind? Kind { get; init; }
}

/// <summary>
///     Handler for <see cref="GetPolicyVersionHistoryQuery"/>.
/// </summary>
public sealed class GetPolicyVersionHistoryQueryHandler(
    IPolicyVersionHistoryRepository historyRepository,
    IActorContextAccessor actorContextAccessor,
    IAuthorizationTenantContext tenantContext)
    : IQueryHandler<GetPolicyVersionHistoryQuery, List<PolicyVersionHistory>>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    /// <inheritdoc />
    public async Task<List<PolicyVersionHistory>> Handle(
        GetPolicyVersionHistoryQuery request,
        CancellationToken cancellationToken)
    {
        if (!Actor.IsSystemAdmin)
        {
            throw new UnauthorizedAccessException(
                "Permission-policy version history requires system administration.");
        }

        return await historyRepository
            .ListAsync(request.PolicyId, request.Kind, tenantContext.TenantId, cancellationToken)
            .ConfigureAwait(false);
    }
}
