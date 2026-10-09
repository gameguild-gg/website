using FluentValidation;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Identity.Authorization.Commands;

// ============================================================================
// Delegated Administration Commands
// ============================================================================

/// <summary>
///     Command to grant delegated admin scope
/// </summary>
public sealed record GrantDelegatedAdminCommand(
    Guid AdminUserId,
    Guid? TenantId,
    string Name,
    string Description,
    string[] ManagedResourceTypes,
    Guid[] ManagedUserIds,
    string[] AllowedOperations,
    Guid? OrganizationalUnitId = null
) : ICommand<DelegatedAdminScope>;

public sealed class GrantDelegatedAdminValidator : AbstractValidator<GrantDelegatedAdminCommand>
{
    public GrantDelegatedAdminValidator()
    {
        RuleFor(x => x.AdminUserId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.ManagedResourceTypes)
            .NotEmpty()
            .When(x => x.ManagedUserIds == null || x.ManagedUserIds.Length == 0)
            .WithMessage("Either managed resource types or managed user IDs must be provided");
        RuleFor(x => x.AllowedOperations).NotEmpty();
    }
}

public sealed class GrantDelegatedAdminHandler(
    IDelegatedAdminService service,
    IActorContextAccessor actorContextAccessor,
    ITenantSecurityVersionStore securityVersionStore,
    IPermissionAuditService auditService
) : ICommandHandler<GrantDelegatedAdminCommand, DelegatedAdminScope>
{
    private readonly IDelegatedAdminService _service =
        service ?? throw new ArgumentNullException(nameof(service));

    public async Task<DelegatedAdminScope> Handle(
        GrantDelegatedAdminCommand request,
        CancellationToken cancellationToken
    )
    {
        var actor = actorContextAccessor.ActorContext;

        // Fail closed: delegated administration is a permission mutation and must be
        // guarded. The tenant is taken from the actor's request context, never trusted
        // from the command payload. System admins may grant tenant-scoped or global
        // scopes; tenant admins may only grant scopes inside their own tenant.
        if (!actor.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Authenticated actor required to grant delegated admin scopes");
        }

        if (!actor.IsSystemAdmin)
        {
            if (!actor.IsTenantAdmin)
            {
                throw new UnauthorizedAccessException(
                    "Only system or tenant administrators can grant delegated admin scopes");
            }

            if (request.TenantId is null || actor.TenantId != request.TenantId)
            {
                throw new UnauthorizedAccessException(
                    "Tenant admins can only grant delegated admin scopes within their own tenant");
            }
        }

        var scope = new DelegatedAdminScope
        {
            AdminUserId = request.AdminUserId,
            TenantId = request.TenantId,
            Name = request.Name,
            Description = request.Description,
            AllowedResourceTypes = System.Text.Json.JsonSerializer.Serialize(request.ManagedResourceTypes),
            AllowedUserIds = System.Text.Json.JsonSerializer.Serialize(request.ManagedUserIds),
            GrantablePermissions = System.Text.Json.JsonSerializer.Serialize(request.AllowedOperations),
            AllowedDepartments = request.OrganizationalUnitId?.ToString(),
            IsActive = true,
            CreatedBy = actor.SubjectIdAsGuid ?? Guid.Empty
        };

        var created = await _service.GrantDelegatedAdminAsync(scope, cancellationToken).ConfigureAwait(false);

        var tenantKey = created.TenantId?.ToString() ?? "global";
        await securityVersionStore.IncrementVersionAsync(tenantKey, cancellationToken).ConfigureAwait(false);
        await auditService.LogPermissionChangeAsync(
            PermissionOperationType.Grant,
            created.AdminUserId,
            actor.SubjectIdAsGuid ?? Guid.Empty,
            created.TenantId,
            permissionType: "DelegatedAdminScope",
            resourceId: created.Id,
            resourceType: "DelegatedAdminScope",
            newValue: $"{created.Name}:{string.Join(",", request.AllowedOperations)}",
            reason: "Delegated admin scope granted",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return created;
    }
}

/// <summary>
///     Command to revoke delegated admin scope
/// </summary>
public sealed record RevokeDelegatedAdminCommand(Guid ScopeId) : ICommand<bool>;

public sealed class RevokeDelegatedAdminValidator : AbstractValidator<RevokeDelegatedAdminCommand>
{
    public RevokeDelegatedAdminValidator()
    {
        RuleFor(x => x.ScopeId).NotEmpty();
    }
}

public sealed class RevokeDelegatedAdminHandler(
    IDelegatedAdminService service,
    IActorContextAccessor actorContextAccessor,
    ITenantSecurityVersionStore securityVersionStore,
    IPermissionAuditService auditService
) : ICommandHandler<RevokeDelegatedAdminCommand, bool>
{
    private readonly IDelegatedAdminService _service =
        service ?? throw new ArgumentNullException(nameof(service));

    public async Task<bool> Handle(
        RevokeDelegatedAdminCommand request,
        CancellationToken cancellationToken
    )
    {
        var actor = actorContextAccessor.ActorContext;

        // Fail closed: revocation is a permission mutation. Guard against the scope's
        // own tenant — tenant admins may only revoke scopes in their own tenant.
        if (!actor.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Authenticated actor required to revoke delegated admin scopes");
        }

        var scope = await _service.GetScopeByIdAsync(request.ScopeId, cancellationToken).ConfigureAwait(false);
        if (scope == null)
        {
            return false;
        }

        if (!actor.IsSystemAdmin)
        {
            if (!actor.IsTenantAdmin)
            {
                throw new UnauthorizedAccessException(
                    "Only system or tenant administrators can revoke delegated admin scopes");
            }

            var scopeTenantId = scope.TenantId?.Value;
            if (scopeTenantId is null || actor.TenantId != scopeTenantId)
            {
                throw new UnauthorizedAccessException(
                    "Tenant admins can only revoke delegated admin scopes within their own tenant");
            }
        }

        var oldValue = $"{scope.Name}:{scope.GrantablePermissions}";
        var revoked = await _service.RevokeDelegatedAdminAsync(request.ScopeId, cancellationToken).ConfigureAwait(false);
        if (!revoked)
        {
            return false;
        }

        var tenantKey = scope.TenantId?.ToString() ?? "global";
        await securityVersionStore.IncrementVersionAsync(tenantKey, cancellationToken).ConfigureAwait(false);
        await auditService.LogPermissionChangeAsync(
            PermissionOperationType.Revoke,
            scope.AdminUserId,
            actor.SubjectIdAsGuid ?? Guid.Empty,
            scope.TenantId,
            permissionType: "DelegatedAdminScope",
            resourceId: scope.Id,
            resourceType: "DelegatedAdminScope",
            oldValue: oldValue,
            newValue: null,
            reason: "Delegated admin scope revoked",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return true;
    }
}
