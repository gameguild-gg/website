using FluentValidation;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization.Commands;

// ============================================================================
// JIT Elevation Commands
// ============================================================================
//
// SECURITY (issue #327, adversarial finding F-3): every acting identity on this
// surface is taken from the authenticated actor (IActorContextAccessor), never
// from the request body. Before this hardening, a member could approve their own
// elevation request by naming an arbitrary reviewer id in the body — the only
// guard (reviewer != requester) was trivially bypassable and the activated
// elevation then contributed its permission through the effective-permission
// resolver (privilege escalation).

/// <summary>
///     Command to request a Just-in-Time permission elevation
/// </summary>
public sealed record RequestJitElevationCommand(
    Guid RequesterId,
    Guid? TenantId,
    string Permission,
    string Justification,
    int DurationMinutes,
    Guid? ResourceId = null,
    string? ResourceType = null,
    DateTime? StartsAt = null
) : ICommand<JitElevationRequest>;

public sealed class RequestJitElevationValidator : AbstractValidator<RequestJitElevationCommand>
{
    public RequestJitElevationValidator()
    {
        RuleFor(x => x.RequesterId).NotEmpty();
        RuleFor(x => x.Permission).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Justification).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.DurationMinutes).GreaterThan(0).LessThanOrEqualTo(1440); // Max 24 hours
        RuleFor(x => x.ResourceType).MaximumLength(128).When(x => x.ResourceType != null);
    }
}

public sealed class RequestJitElevationHandler(
    IJitElevationService service,
    IActorContextAccessor actorContextAccessor,
    ILogger<RequestJitElevationHandler> logger)
    : ICommandHandler<RequestJitElevationCommand, JitElevationRequest>
{
    public async Task<JitElevationRequest> Handle(
        RequestJitElevationCommand request,
        CancellationToken cancellationToken
    )
    {
        // SECURITY: the requester is the authenticated actor; a client-supplied
        // requester id naming anyone else is an impersonation attempt — denied.
        var actor = actorContextAccessor.ActorContext;
        var requesterId = actor.SubjectIdAsGuid
            ?? throw new UnauthorizedAccessException("An authenticated actor is required to request a JIT elevation");
        if (request.RequesterId != requesterId)
        {
            logger.LogWarning(
                "Actor {ActorId} attempted to record a JIT elevation request as requester {ForgedRequesterId}",
                requesterId,
                request.RequesterId);
            throw new UnauthorizedAccessException("JIT elevation requests can only be recorded for the authenticated actor");
        }

        return await service.RequestElevationAsync(
            requesterId,
            request.TenantId,
            request.Permission,
            request.Justification,
            request.DurationMinutes,
            request.ResourceId,
            request.ResourceType,
            request.StartsAt,
            cancellationToken
        );
    }
}

/// <summary>
///     Command to approve a JIT elevation request
/// </summary>
public sealed record ApproveJitElevationCommand(
    Guid RequestId,
    Guid ReviewerId,
    string? Comments = null
) : ICommand<JitElevationRequest>;

public sealed class ApproveJitElevationValidator : AbstractValidator<ApproveJitElevationCommand>
{
    public ApproveJitElevationValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.ReviewerId).NotEmpty();
        RuleFor(x => x.Comments).MaximumLength(2000).When(x => x.Comments != null);
    }
}

public sealed class ApproveJitElevationHandler(
    IJitElevationService service,
    IJitElevationRequestRepository repository,
    IActorContextAccessor actorContextAccessor,
    ILogger<ApproveJitElevationHandler> logger)
    : ICommandHandler<ApproveJitElevationCommand, JitElevationRequest>
{
    public async Task<JitElevationRequest> Handle(
        ApproveJitElevationCommand request,
        CancellationToken cancellationToken
    )
    {
        // SECURITY (issue #327, finding F-3): approving an elevation is a permission
        // mutation, so the reviewing identity is the authenticated actor — never the
        // body-supplied reviewer — and only system or same-tenant administrators may
        // approve. Without these guards any member could self-approve with a spoofed
        // reviewer id and hold the elevated permission for its duration.
        var actor = actorContextAccessor.ActorContext;
        var reviewerId = actor.SubjectIdAsGuid
            ?? throw new UnauthorizedAccessException("An authenticated reviewer is required to approve JIT elevations");
        if (request.ReviewerId != reviewerId)
        {
            logger.LogWarning(
                "Actor {ActorId} attempted to approve JIT elevation {RequestId} as reviewer {SpoofedReviewerId}",
                reviewerId,
                request.RequestId,
                request.ReviewerId);
            throw new UnauthorizedAccessException("JIT elevations can only be approved as the authenticated actor");
        }

        var elevation = await repository.GetByIdAsync(request.RequestId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Elevation request {request.RequestId} not found");

        if (!actor.IsSystemAdmin)
        {
            if (!actor.IsTenantAdmin || elevation.TenantId is null || actor.TenantId != elevation.TenantId.Value)
            {
                logger.LogWarning(
                    "Actor {ActorId} attempted to approve JIT elevation {RequestId} without same-tenant admin privileges",
                    reviewerId,
                    request.RequestId);
                throw new UnauthorizedAccessException(
                    "Only system administrators or administrators of the elevation's tenant can approve JIT elevations");
            }
        }

        return await service.ApproveRequestAsync(
            request.RequestId,
            reviewerId,
            request.Comments,
            cancellationToken
        ).ConfigureAwait(false);
    }
}

/// <summary>
///     Command to deny a JIT elevation request
/// </summary>
public sealed record DenyJitElevationCommand(
    Guid RequestId,
    Guid ReviewerId,
    string Comments
) : ICommand<JitElevationRequest>;

public sealed class DenyJitElevationValidator : AbstractValidator<DenyJitElevationCommand>
{
    public DenyJitElevationValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.ReviewerId).NotEmpty();
        RuleFor(x => x.Comments).NotEmpty().MaximumLength(2000);
    }
}

public sealed class DenyJitElevationHandler(
    IJitElevationService service,
    IActorContextAccessor actorContextAccessor,
    ILogger<DenyJitElevationHandler> logger)
    : ICommandHandler<DenyJitElevationCommand, JitElevationRequest>
{
    public async Task<JitElevationRequest> Handle(
        DenyJitElevationCommand request,
        CancellationToken cancellationToken
    )
    {
        // SECURITY: the reviewing identity is the authenticated actor; denying is
        // privilege-reducing so no admin guard is required, but attribution spoofing
        // (denying as someone else) is still rejected.
        var actor = actorContextAccessor.ActorContext;
        var reviewerId = actor.SubjectIdAsGuid
            ?? throw new UnauthorizedAccessException("An authenticated reviewer is required to deny JIT elevations");
        if (request.ReviewerId != reviewerId)
        {
            logger.LogWarning(
                "Actor {ActorId} attempted to deny JIT elevation {RequestId} as reviewer {SpoofedReviewerId}",
                reviewerId,
                request.RequestId,
                request.ReviewerId);
            throw new UnauthorizedAccessException("JIT elevations can only be denied as the authenticated actor");
        }

        return await service.DenyRequestAsync(
            request.RequestId,
            reviewerId,
            request.Comments,
            cancellationToken
        ).ConfigureAwait(false);
    }
}

/// <summary>
///     Command to revoke an active JIT elevation
/// </summary>
public sealed record RevokeJitElevationCommand(
    Guid RequestId,
    Guid RevokedBy,
    string Reason
) : ICommand<bool>;

public sealed class RevokeJitElevationValidator : AbstractValidator<RevokeJitElevationCommand>
{
    public RevokeJitElevationValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.RevokedBy).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000);
    }
}

public sealed class RevokeJitElevationHandler(
    IJitElevationService service,
    IActorContextAccessor actorContextAccessor,
    ILogger<RevokeJitElevationHandler> logger)
    : ICommandHandler<RevokeJitElevationCommand, bool>
{
    public async Task<bool> Handle(
        RevokeJitElevationCommand request,
        CancellationToken cancellationToken
    )
    {
        // SECURITY: the revoking identity is the authenticated actor; revoking is
        // privilege-reducing (and self-service revocation is legitimate), but the
        // identity must still never come from the request body.
        var actor = actorContextAccessor.ActorContext;
        var revokedBy = actor.SubjectIdAsGuid
            ?? throw new UnauthorizedAccessException("An authenticated actor is required to revoke JIT elevations");
        if (request.RevokedBy != revokedBy)
        {
            logger.LogWarning(
                "Actor {ActorId} attempted to revoke JIT elevation {RequestId} as {SpoofedRevokerId}",
                revokedBy,
                request.RequestId,
                request.RevokedBy);
            throw new UnauthorizedAccessException("JIT elevations can only be revoked as the authenticated actor");
        }

        return await service.RevokeElevationAsync(
            request.RequestId,
            revokedBy,
            request.Reason,
            cancellationToken
        ).ConfigureAwait(false);
    }
}

/// <summary>
///     Command to cleanup expired JIT elevations
/// </summary>
public sealed record CleanupExpiredElevationsCommand : ICommand<int>;

public sealed class CleanupExpiredElevationsHandler(IJitElevationService service)
    : ICommandHandler<CleanupExpiredElevationsCommand, int>
{
    public async Task<int> Handle(
        CleanupExpiredElevationsCommand request,
        CancellationToken cancellationToken
    )
    {
        return await service.CleanupExpiredElevationsAsync(cancellationToken).ConfigureAwait(false);
    }
}
