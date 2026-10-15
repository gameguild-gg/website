using GameGuild.CQRS;
using GameGuild.CQRS.Models;
using GameGuild.Identity.Context.Actors;

using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Grants content-type scoped permissions (layer 2 of the 3-layer DAC model,
/// issues #360/#365). Guarded: only system administrators or tenant administrators of
/// the target tenant may grant; versioned and audited inside
/// <see cref="IContentTypePermissionGrantService"/>.
/// </summary>
public sealed record GrantContentTypePermissionGrantCommand : ICommand<Guid>
{
    /// <summary>Gets the tenant scope of the grant.</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>
    ///     Gets the grant subject. Null edits the tenant default row (every user of the
    ///     tenant for this content type).
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>Gets the content type name (e.g. <c>Project</c>).</summary>
    public required string ContentTypeName { get; init; }

    /// <summary>Gets the permissions to grant (e.g. ["Read", "Write"]).</summary>
    public required string[] Permissions { get; init; }

    /// <summary>Gets the optional expiration instant.</summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>Gets the optional reason (audit trail).</summary>
    public string? Reason { get; init; }
}

/// <summary>
///     Handler for <see cref="GrantContentTypePermissionGrantCommand"/>. The acting user
///     always comes from the authenticated actor context, never from the request body.
/// </summary>
public sealed class GrantContentTypePermissionGrantCommandHandler(
    IContentTypePermissionGrantService grantService,
    IActorContextAccessor actorContextAccessor,
    ILogger<GrantContentTypePermissionGrantCommandHandler> logger)
    : ICommandHandler<GrantContentTypePermissionGrantCommand, Guid>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<Guid> Handle(GrantContentTypePermissionGrantCommand request, CancellationToken cancellationToken)
    {
        Actor.EnsureTenantAdmin(request.TenantId.Value, "grant content-type permissions");
        var grantedBy = Actor.SubjectIdAsGuid ?? throw new UnauthorizedAccessException("Authenticated actor required");

        logger.LogInformation(
            "Granting content-type permissions {Permissions} on {ContentType} to user {UserId} in tenant {TenantId}",
            string.Join(", ", request.Permissions),
            request.ContentTypeName,
            request.UserId,
            request.TenantId.Value);

        var grant = await grantService.GrantAsync(
                request.UserId,
                request.TenantId.Value,
                request.ContentTypeName,
                request.Permissions,
                grantedBy,
                request.ExpiresAt,
                request.Reason,
                cancellationToken)
            .ConfigureAwait(false);

        return grant.Id;
    }
}

/// <summary>
///     Revokes content-type scoped permissions. Guarded like the grant command; the row
///     is soft-deleted once its payload is empty (permission history preservation).
/// </summary>
public sealed record RevokeContentTypePermissionGrantCommand : ICommand<bool>
{
    /// <summary>Gets the tenant scope of the grant.</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>Gets the grant subject (null addresses the tenant default row).</summary>
    public Guid? UserId { get; init; }

    /// <summary>Gets the content type name.</summary>
    public required string ContentTypeName { get; init; }

    /// <summary>Gets the permissions to revoke.</summary>
    public required string[] Permissions { get; init; }
}

/// <summary>Handler for <see cref="RevokeContentTypePermissionGrantCommand"/>.</summary>
public sealed class RevokeContentTypePermissionGrantCommandHandler(
    IContentTypePermissionGrantService grantService,
    IActorContextAccessor actorContextAccessor)
    : ICommandHandler<RevokeContentTypePermissionGrantCommand, bool>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<bool> Handle(RevokeContentTypePermissionGrantCommand request, CancellationToken cancellationToken)
    {
        Actor.EnsureTenantAdmin(request.TenantId.Value, "revoke content-type permissions");

        return await grantService.RevokeAsync(
                request.UserId,
                request.TenantId.Value,
                request.ContentTypeName,
                request.Permissions,
                cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>
///     Soft-deletes one content-type grant row by id (the CRUD delete). Tenant-isolated:
///     the row's tenant must match the requested tenant.
/// </summary>
public sealed record RevokeContentTypePermissionGrantByIdCommand : ICommand
{
    /// <summary>Gets the id of the grant row to delete.</summary>
    public required Guid GrantId { get; init; }

    /// <summary>Gets the tenant scope the row must belong to.</summary>
    public required TenantId TenantId { get; init; }
}

/// <summary>Handler for <see cref="RevokeContentTypePermissionGrantByIdCommand"/>.</summary>
public sealed class RevokeContentTypePermissionGrantByIdCommandHandler(
    IContentTypePermissionGrantRepository repository,
    IContentTypePermissionGrantService grantService,
    IActorContextAccessor actorContextAccessor)
    : ICommandHandler<RevokeContentTypePermissionGrantByIdCommand>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<Unit> Handle(RevokeContentTypePermissionGrantByIdCommand request, CancellationToken cancellationToken)
    {
        Actor.EnsureTenantAdmin(request.TenantId.Value, "delete a content-type permission grant");

        var grant = await repository.GetByIdAsync(request.GrantId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Content-type permission grant {request.GrantId} was not found.");

        // Fail-closed tenant isolation: a row of another tenant is never addressable.
        if (grant.TenantId != request.TenantId.Value)
        {
            throw new UnauthorizedAccessException(
                $"Content-type permission grant {request.GrantId} does not belong to tenant {request.TenantId.Value}.");
        }

        await grantService.RevokeAsync(
                grant.UserId,
                request.TenantId.Value,
                grant.ContentTypeName,
                grant.Permissions,
                cancellationToken)
            .ConfigureAwait(false);

        return Unit.Value;
    }
}

/// <summary>
///     Adds explicit DENY-WINS entries to a content-type grant row. Guarded like the
///     grant command.
/// </summary>
public sealed record DenyContentTypePermissionGrantCommand : ICommand<Guid>
{
    /// <summary>Gets the tenant scope of the deny.</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>Gets the deny subject (null addresses the tenant default row).</summary>
    public Guid? UserId { get; init; }

    /// <summary>Gets the content type name.</summary>
    public required string ContentTypeName { get; init; }

    /// <summary>Gets the permissions to deny.</summary>
    public required string[] Permissions { get; init; }

    /// <summary>Gets the optional reason (audit trail).</summary>
    public string? Reason { get; init; }
}

/// <summary>Handler for <see cref="DenyContentTypePermissionGrantCommand"/>.</summary>
public sealed class DenyContentTypePermissionGrantCommandHandler(
    IContentTypePermissionGrantService grantService,
    IActorContextAccessor actorContextAccessor)
    : ICommandHandler<DenyContentTypePermissionGrantCommand, Guid>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<Guid> Handle(DenyContentTypePermissionGrantCommand request, CancellationToken cancellationToken)
    {
        Actor.EnsureTenantAdmin(request.TenantId.Value, "deny content-type permissions");
        var deniedBy = Actor.SubjectIdAsGuid ?? throw new UnauthorizedAccessException("Authenticated actor required");

        var grant = await grantService.DenyAsync(
                request.UserId,
                request.TenantId.Value,
                request.ContentTypeName,
                request.Permissions,
                deniedBy,
                request.Reason,
                cancellationToken)
            .ConfigureAwait(false);

        return grant.Id;
    }
}

/// <summary>
///     Lists the content-type grant rows of one tenant (optionally narrowed to one
/// content type). Reads require an authenticated same-tenant actor (or system admin).
/// </summary>
public sealed record GetContentTypePermissionGrantsQuery : IQuery<GetContentTypePermissionGrantsResponse>
{
    /// <summary>Gets the tenant scope.</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>Gets an optional content-type name filter.</summary>
    public string? ContentTypeName { get; init; }
}

/// <summary>Response row for <see cref="GetContentTypePermissionGrantsQuery"/>.</summary>
public sealed record ContentTypePermissionGrantDto
{
    /// <summary>Gets the grant row id.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the grant subject (null = tenant default).</summary>
    public Guid? UserId { get; init; }

    /// <summary>Gets the tenant scope.</summary>
    public required Guid TenantId { get; init; }

    /// <summary>Gets the content type name.</summary>
    public required string ContentTypeName { get; init; }

    /// <summary>Gets the allowed permission entries.</summary>
    public required string[] Permissions { get; init; }

    /// <summary>Gets the denied permission entries.</summary>
    public required string[] DenyPermissions { get; init; }

    /// <summary>Gets the optional expiration instant.</summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>Gets whether the row is active.</summary>
    public bool IsActive { get; init; }

    /// <summary>Gets when the row was granted.</summary>
    public DateTime GrantedAt { get; init; }
}

/// <summary>Response envelope for <see cref="GetContentTypePermissionGrantsQuery"/>.</summary>
public sealed record GetContentTypePermissionGrantsResponse
{
    /// <summary>Gets the tenant scope.</summary>
    public required Guid TenantId { get; init; }

    /// <summary>Gets the matching grant rows.</summary>
    public required List<ContentTypePermissionGrantDto> Grants { get; init; }
}

/// <summary>Handler for <see cref="GetContentTypePermissionGrantsQuery"/>.</summary>
public sealed class GetContentTypePermissionGrantsQueryHandler(
    IContentTypePermissionGrantService grantService,
    IActorContextAccessor actorContextAccessor)
    : IQueryHandler<GetContentTypePermissionGrantsQuery, GetContentTypePermissionGrantsResponse>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<GetContentTypePermissionGrantsResponse> Handle(GetContentTypePermissionGrantsQuery request, CancellationToken cancellationToken)
    {
        if (!Actor.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("User not authenticated");
        }

        // Tenant from the request context, not the route: cross-tenant reads are denied
        // unless the caller is a system administrator.
        if (!Actor.IsSystemAdmin && Actor.TenantId != request.TenantId.Value)
        {
            throw new UnauthorizedAccessException("Content-type permission grants of another tenant are not readable.");
        }

        var grants = await grantService.GetGrantsAsync(request.TenantId.Value, request.ContentTypeName, cancellationToken).ConfigureAwait(false);

        return new GetContentTypePermissionGrantsResponse
        {
            TenantId = request.TenantId.Value,
            Grants = grants.Select(grant => new ContentTypePermissionGrantDto
            {
                Id = grant.Id,
                UserId = grant.UserId,
                TenantId = grant.TenantId ?? Guid.Empty,
                ContentTypeName = grant.ContentTypeName,
                Permissions = grant.Permissions,
                DenyPermissions = grant.DenyPermissions,
                ExpiresAt = grant.ExpiresAt,
                IsActive = grant.IsActive,
                GrantedAt = grant.GrantedAt
            }).ToList()
        };
    }
}

/// <summary>
///     Shared guard helpers for the content-type grant commands (issues #360/#365):
///     the actor must be authenticated and be a system administrator or a tenant
///     administrator of the target tenant (fail-closed, same contract as the tenant
///     permission command family).
/// </summary>
internal static class ContentTypePermissionGrantGuards
{
    /// <summary>Throws unless the actor is authenticated.</summary>
    public static void EnsureActor(this ActorContext actor, string operation)
    {
        if (!actor.IsAuthenticated)
        {
            throw new UnauthorizedAccessException($"An authenticated actor is required to {operation}.");
        }
    }

    /// <summary>
    ///     Throws unless the actor is a system administrator or a tenant administrator of
    ///     the target tenant (fail-closed; cross-tenant administration is denied).
    /// </summary>
    public static void EnsureTenantAdmin(this ActorContext actor, Guid tenantId, string operation)
    {
        actor.EnsureActor(operation);

        if (actor.IsSystemAdmin)
        {
            return;
        }

        if (!actor.IsTenantAdmin || actor.TenantId != tenantId)
        {
            throw new UnauthorizedAccessException(
                $"Only tenant or system administrators can {operation}.");
        }
    }
}
