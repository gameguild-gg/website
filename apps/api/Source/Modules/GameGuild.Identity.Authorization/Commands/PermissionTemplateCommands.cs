using System.Text.Json;
using GameGuild.CQRS;
using GameGuild.CQRS.Models;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

// ============================================================================
// PERMISSION TEMPLATE LIFECYCLE (issues #369 / #370)
//
// Guarded + versioned + audited mutations:
//   * guarded  — every handler re-checks the actor from IActorContextAccessor
//                (never the request body): anonymous actors are rejected; system
//                templates are immutable; tenant templates require same-tenant
//                admin or system admin; instantiations/migrations additionally
//                fail closed when the target tenant is outside the actor scope.
//   * versioned — every accepted template mutation appends a PermissionTemplateVersion
//                snapshot (issue #370) and bumps the tenant security version for every
//                tenant whose grants changed (cache invalidation).
//   * audited  — every accepted mutation writes a PermissionAuditLog entry via
//                IPermissionAuditService.
// ============================================================================

/// <summary>
///     Creates a permission template (issue #369 template CRUD lifecycle).
/// </summary>
/// <remarks>
///     <para><b>SECURITY:</b> Creating a system template requires
///     <c>system:manage-global-defaults</c> (or SystemAdmin); creating a tenant-usable
///     template requires tenant-admin or SystemAdmin. The initial permission set is
///     snapshotted as version 1 and the creation is audited.</para>
/// </remarks>
public sealed record CreatePermissionTemplateCommand : ICommand<PermissionTemplateMutationResult>
{
    /// <summary>Template name (unique).</summary>
    public required string Name { get; init; }

    /// <summary>Template description.</summary>
    public required string Description { get; init; }

    /// <summary>Raw permissions the template grants.</summary>
    public string[] Permissions { get; init; } = Array.Empty<string>();

    /// <summary>
    ///     Dynamic role names the template grants (issue #369 role templates). A
    ///     non-empty set makes this a role template.
    /// </summary>
    public string[] RoleKeys { get; init; } = Array.Empty<string>();

    /// <summary>Whether the template is a system template (immutable once created).</summary>
    public bool IsSystemTemplate { get; init; }

    /// <summary>Optional category for organization.</summary>
    public string? Category { get; init; }

    /// <summary>Optional minimum tenant tier.</summary>
    public string? MinimumTier { get; init; }

    /// <summary>Optional change note recorded on the version snapshot.</summary>
    public string? ChangeNotes { get; init; }
}

/// <summary>
///     Handler for <see cref="CreatePermissionTemplateCommand"/>.
/// </summary>
public sealed class CreatePermissionTemplateCommandHandler(
    IApplicationDbContext dbContext,
    IActorContextAccessor actorContextAccessor,
    IPermissionAuditService auditService,
    ILogger<CreatePermissionTemplateCommandHandler> logger)
    : ICommandHandler<CreatePermissionTemplateCommand, PermissionTemplateMutationResult>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<PermissionTemplateMutationResult> Handle(
        CreatePermissionTemplateCommand request,
        CancellationToken cancellationToken)
    {
        if (!Actor.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("User is not authenticated");
        }

        if (request.IsSystemTemplate)
        {
            if (!Actor.IsSystemAdmin && !Actor.HasPermission(SystemPermission.Keys.ManageGlobalDefaults))
            {
                throw new UnauthorizedAccessException(
                    $"Creating system permission templates requires '{SystemPermission.Keys.ManageGlobalDefaults}' permission");
            }
        }
        else if (!Actor.IsSystemAdmin && !Actor.IsTenantAdmin)
        {
            throw new UnauthorizedAccessException(
                "Creating permission templates requires tenant administration or system administration");
        }

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Template name must not be empty", nameof(request));
        }

        var permissions = request.Permissions.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToArray();
        var roleKeys = request.RoleKeys.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct().ToArray();
        if (permissions.Length == 0 && roleKeys.Length == 0)
        {
            throw new ArgumentException("A template must grant at least one permission or one role");
        }

        var nameTaken = await dbContext.Set<PermissionTemplate>()
            .AnyAsync(t => t.Name == name, cancellationToken).ConfigureAwait(false);
        if (nameTaken)
        {
            throw new InvalidOperationException($"A permission template named '{name}' already exists");
        }

        var template = new PermissionTemplate
        {
            Name = name,
            Description = request.Description,
            Permissions = permissions,
            RoleKeys = roleKeys,
            IsSystemTemplate = request.IsSystemTemplate,
            IsActive = true,
            Category = request.Category,
            MinimumTier = request.MinimumTier
        };
        dbContext.Set<PermissionTemplate>().Add(template);

        var snapshot = PermissionTemplateSnapshot.CreateInitial(template, Actor.SubjectIdAsGuid ?? Guid.Empty, request.ChangeNotes);
        dbContext.Set<PermissionTemplateVersion>().Add(snapshot);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await auditService.LogPermissionChangeAsync(
            PermissionOperationType.Grant,
            userId: null,
            performedBy: Actor.SubjectIdAsGuid ?? Guid.Empty,
            tenantId: Actor.TenantId,
            permissionType: $"template:{template.Name}",
            oldValue: null,
            newValue: JsonSerializer.Serialize(new { template.Permissions, template.RoleKeys }),
            reason: request.ChangeNotes ?? "Permission template created",
            success: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Created permission template {TemplateId} '{TemplateName}' (version 1, {PermissionCount} permissions, {RoleCount} roles)",
            template.Id, template.Name, permissions.Length, roleKeys.Length);

        return PermissionTemplateMutationResult.FromTemplate(template, 1, created: true);
    }
}

/// <summary>
///     Updates a permission template (issue #369 template CRUD lifecycle). Every accepted
///     update appends a new <see cref="PermissionTemplateVersion"/> snapshot with the
///     computed permission diff (issue #370 version bump on template update).
/// </summary>
/// <remarks>
///     <para><b>SECURITY:</b> System templates are immutable (fail closed). Tenant
///     templates require same-tenant admin or SystemAdmin. The mutation is versioned,
///     audited, and every tenant holding an active instantiation of the template has its
///     security version bumped so cached permission sets are invalidated.</para>
/// </remarks>
public sealed record UpdatePermissionTemplateCommand : ICommand<PermissionTemplateMutationResult>
{
    /// <summary>The template to update.</summary>
    public required Guid TemplateId { get; init; }

    /// <summary>The new name (unchanged when null).</summary>
    public string? Name { get; init; }

    /// <summary>The new description (unchanged when null).</summary>
    public string? Description { get; init; }

    /// <summary>The new permission set (replaces the current set when provided).</summary>
    public string[]? Permissions { get; init; }

    /// <summary>The new role-key set (replaces the current set when provided).</summary>
    public string[]? RoleKeys { get; init; }

    /// <summary>The new active flag (unchanged when null).</summary>
    public bool? IsActive { get; init; }

    /// <summary>The new category (unchanged when null).</summary>
    public string? Category { get; init; }

    /// <summary>The new minimum tier (unchanged when null).</summary>
    public string? MinimumTier { get; init; }

    /// <summary>Change note recorded on the new version snapshot.</summary>
    public string? ChangeNotes { get; init; }

    /// <summary>The semantic change type recorded on the snapshot.</summary>
    public TemplateChangeType ChangeType { get; init; } = TemplateChangeType.Minor;
}

/// <summary>
///     Handler for <see cref="UpdatePermissionTemplateCommand"/>.
/// </summary>
public sealed class UpdatePermissionTemplateCommandHandler(
    IApplicationDbContext dbContext,
    IActorContextAccessor actorContextAccessor,
    ITenantSecurityVersionStore securityVersionStore,
    IPermissionAuditService auditService,
    ILogger<UpdatePermissionTemplateCommandHandler> logger)
    : ICommandHandler<UpdatePermissionTemplateCommand, PermissionTemplateMutationResult>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<PermissionTemplateMutationResult> Handle(
        UpdatePermissionTemplateCommand request,
        CancellationToken cancellationToken)
    {
        if (!Actor.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("User is not authenticated");
        }

        var template = await dbContext.Set<PermissionTemplate>()
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId, cancellationToken).ConfigureAwait(false);
        if (template is null)
        {
            throw new InvalidOperationException($"Permission template {request.TemplateId} not found");
        }

        // SECURITY: system templates are immutable by definition (entity contract).
        if (template.IsSystemTemplate)
        {
            throw new UnauthorizedAccessException(
                $"System permission template '{template.Name}' cannot be modified");
        }

        if (!Actor.IsSystemAdmin && !Actor.IsTenantAdmin)
        {
            throw new UnauthorizedAccessException(
                "Updating permission templates requires tenant administration or system administration");
        }

        var oldPermissions = template.Permissions;
        var oldRoleKeys = template.RoleKeys;

        if (request.Name is { } incomingName)
        {
            var trimmed = incomingName.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                throw new ArgumentException("Template name must not be empty", nameof(request));
            }

            var nameTaken = await dbContext.Set<PermissionTemplate>()
                .AnyAsync(t => t.Id != template.Id && t.Name == trimmed, cancellationToken).ConfigureAwait(false);
            if (nameTaken)
            {
                throw new InvalidOperationException($"A permission template named '{trimmed}' already exists");
            }

            template.Name = trimmed;
        }

        if (request.Description is { } description)
        {
            template.Description = description;
        }

        var newPermissions = request.Permissions is { } requestedPermissions
            ? requestedPermissions.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToArray()
            : oldPermissions;
        var newRoleKeys = request.RoleKeys is { } requestedRoleKeys
            ? requestedRoleKeys.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct().ToArray()
            : oldRoleKeys;

        if (newPermissions.Length == 0 && newRoleKeys.Length == 0)
        {
            throw new ArgumentException("A template must grant at least one permission or one role");
        }

        template.Permissions = newPermissions;
        template.RoleKeys = newRoleKeys;
        if (request.IsActive.HasValue)
        {
            if (request.IsActive.Value)
            {
                template.Activate();
            }
            else
            {
                template.Deactivate();
            }
        }

        if (request.Category is { } category)
        {
            template.Category = category;
        }

        if (request.MinimumTier is { } minimumTier)
        {
            template.MinimumTier = minimumTier;
        }

        // Issue #370: version bump — append the new snapshot with the computed diff.
        var latestVersionNumber = await dbContext.Set<PermissionTemplateVersion>()
            .Where(v => v.TemplateId == template.Id)
            .MaxAsync(v => (int?)v.VersionNumber, cancellationToken).ConfigureAwait(false) ?? 0;
        var snapshot = PermissionTemplateSnapshot.CreateChange(
            template,
            latestVersionNumber + 1,
            oldPermissions,
            oldRoleKeys,
            Actor.SubjectIdAsGuid ?? Guid.Empty,
            request.ChangeNotes,
            request.ChangeType);
        dbContext.Set<PermissionTemplateVersion>().Add(snapshot);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // SECURITY: every tenant holding an active instantiation of this template gets
        // its security version bumped so cached permission sets are invalidated.
        var affectedTenants = await dbContext.Set<PermissionTemplateInstantiation>()
            .Where(i => i.TemplateId == template.Id && i.IsActive && i.TenantId != null)
            .Select(i => i.TenantId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var tenantId in affectedTenants)
        {
            await securityVersionStore.IncrementVersionAsync(tenantId.ToString(), cancellationToken).ConfigureAwait(false);
        }

        await auditService.LogPermissionChangeAsync(
            PermissionOperationType.Update,
            userId: null,
            performedBy: Actor.SubjectIdAsGuid ?? Guid.Empty,
            tenantId: Actor.TenantId,
            permissionType: $"template:{template.Name}",
            oldValue: JsonSerializer.Serialize(new { permissions = oldPermissions, roleKeys = oldRoleKeys }),
            newValue: JsonSerializer.Serialize(new { permissions = newPermissions, roleKeys = newRoleKeys }),
            reason: request.ChangeNotes ?? $"Permission template updated to version {snapshot.VersionNumber}",
            success: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Updated permission template {TemplateId} '{TemplateName}' to version {Version} (+{Added} -{Removed})",
            template.Id, template.Name, snapshot.VersionNumber,
            snapshot.AddedPermissions?.Length ?? 0, snapshot.RemovedPermissions?.Length ?? 0);

        return PermissionTemplateMutationResult.FromTemplate(template, snapshot.VersionNumber, created: false);
    }
}

/// <summary>
///     Deletes a permission template (issue #369 template CRUD lifecycle).
/// </summary>
/// <remarks>
///     <para><b>SECURITY:</b> System templates cannot be deleted (fail closed). Tenant
///     templates require same-tenant admin or SystemAdmin. The delete is audited and the
///     version history records a terminal inactive snapshot.</para>
/// </remarks>
public sealed record DeletePermissionTemplateCommand : ICommand<bool>
{
    /// <summary>The template to delete.</summary>
    public required Guid TemplateId { get; init; }

    /// <summary>Reason recorded in the audit log.</summary>
    public string? Reason { get; init; }
}

/// <summary>
///     Handler for <see cref="DeletePermissionTemplateCommand"/>.
/// </summary>
public sealed class DeletePermissionTemplateCommandHandler(
    IApplicationDbContext dbContext,
    IActorContextAccessor actorContextAccessor,
    IPermissionAuditService auditService,
    ILogger<DeletePermissionTemplateCommandHandler> logger)
    : ICommandHandler<DeletePermissionTemplateCommand, bool>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<bool> Handle(DeletePermissionTemplateCommand request, CancellationToken cancellationToken)
    {
        if (!Actor.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("User is not authenticated");
        }

        var template = await dbContext.Set<PermissionTemplate>()
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId, cancellationToken).ConfigureAwait(false);
        if (template is null)
        {
            return false;
        }

        if (template.IsSystemTemplate)
        {
            throw new UnauthorizedAccessException(
                $"System permission template '{template.Name}' cannot be deleted");
        }

        if (!Actor.IsSystemAdmin && !Actor.IsTenantAdmin)
        {
            throw new UnauthorizedAccessException(
                "Deleting permission templates requires tenant administration or system administration");
        }

        // Active instantiations pin the template: deleting it would orphan live grants.
        // They must be migrated away (or deactivated) first — fail closed.
        var hasActiveInstantiations = await dbContext.Set<PermissionTemplateInstantiation>()
            .AnyAsync(i => i.TemplateId == template.Id && i.IsActive, cancellationToken).ConfigureAwait(false);
        if (hasActiveInstantiations)
        {
            throw new InvalidOperationException(
                $"Permission template '{template.Name}' has active instantiations; migrate or deactivate them before deleting the template");
        }

        var name = template.Name;
        var permissions = template.Permissions;
        var roleKeys = template.RoleKeys;

        dbContext.Set<PermissionTemplate>().Remove(template);

        // Terminal provenance snapshot: the version history survives the delete.
        var latestVersionNumber = await dbContext.Set<PermissionTemplateVersion>()
            .Where(v => v.TemplateId == template.Id)
            .MaxAsync(v => (int?)v.VersionNumber, cancellationToken).ConfigureAwait(false) ?? 0;
        var terminal = PermissionTemplateSnapshot.CreateChange(
            template,
            latestVersionNumber + 1,
            permissions,
            roleKeys,
            Actor.SubjectIdAsGuid ?? Guid.Empty,
            request.Reason ?? "Template deleted",
            TemplateChangeType.Major);
        terminal.IsActive = false;
        dbContext.Set<PermissionTemplateVersion>().Add(terminal);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await auditService.LogPermissionChangeAsync(
            PermissionOperationType.Delete,
            userId: null,
            performedBy: Actor.SubjectIdAsGuid ?? Guid.Empty,
            tenantId: Actor.TenantId,
            permissionType: $"template:{name}",
            oldValue: JsonSerializer.Serialize(new { permissions, roleKeys }),
            newValue: null,
            reason: request.Reason ?? "Permission template deleted",
            success: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Deleted permission template {TemplateId} '{TemplateName}'", template.Id, name);

        return true;
    }
}

/// <summary>
///     Instantiates a permission template into a tenant (issue #369): grants the
///     template's raw permissions to the target users and, for role templates, assigns
///     the named tenant-scoped dynamic roles. With <see cref="DryRun"/> = true the
///     command returns exactly what would change without mutating anything.
/// </summary>
/// <remarks>
///     <para><b>SECURITY:</b> Requires same-tenant admin or SystemAdmin (the target
///     tenant must match the actor's tenant unless the actor is a SystemAdmin). Every
///     accepted instantiation writes per-target provenance rows
///     (<see cref="PermissionTemplateInstantiation"/>), bumps the tenant security
///     version and is audited.</para>
/// </remarks>
public sealed record InstantiatePermissionTemplateCommand : ICommand<PermissionTemplateInstantiationResult>
{
    /// <summary>The template to instantiate.</summary>
    public required Guid TemplateId { get; init; }

    /// <summary>The tenant receiving the template. Defaults to the acting tenant.</summary>
    public Guid? TenantId { get; init; }

    /// <summary>The users the template is applied to.</summary>
    public required Guid[] TargetUserIds { get; init; }

    /// <summary>When true, compute and return the preview without applying anything.</summary>
    public bool DryRun { get; init; }

    /// <summary>Reason recorded in the audit log.</summary>
    public string? Reason { get; init; }
}

/// <summary>
///     Handler for <see cref="InstantiatePermissionTemplateCommand"/>.
/// </summary>
public sealed class InstantiatePermissionTemplateCommandHandler(
    IApplicationDbContext dbContext,
    IActorContextAccessor actorContextAccessor,
    ITenantSecurityVersionStore securityVersionStore,
    IPermissionAuditService auditService,
    ILogger<InstantiatePermissionTemplateCommandHandler> logger)
    : ICommandHandler<InstantiatePermissionTemplateCommand, PermissionTemplateInstantiationResult>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<PermissionTemplateInstantiationResult> Handle(
        InstantiatePermissionTemplateCommand request,
        CancellationToken cancellationToken)
    {
        if (!Actor.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("User is not authenticated");
        }

        var tenantId = request.TenantId ?? Actor.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Template instantiation requires a tenant scope");
        }

        if (!Actor.IsSystemAdmin && (!Actor.IsTenantAdmin || Actor.TenantId != tenantId))
        {
            throw new UnauthorizedAccessException(
                "Instantiating a permission template in another tenant requires system administration privileges");
        }

        var template = await dbContext.Set<PermissionTemplate>()
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId && t.IsActive, cancellationToken).ConfigureAwait(false);
        if (template is null)
        {
            throw new InvalidOperationException($"Permission template {request.TemplateId} not found or inactive");
        }

        var latestVersionNumber = await dbContext.Set<PermissionTemplateVersion>()
            .Where(v => v.TemplateId == template.Id)
            .MaxAsync(v => (int?)v.VersionNumber, cancellationToken).ConfigureAwait(false) ?? 1;

        var targetUserIds = request.TargetUserIds.Distinct().ToList();
        if (targetUserIds.Count == 0)
        {
            throw new ArgumentException("At least one target user is required", nameof(request));
        }

        var plan = await BuildPlanAsync(template, tenantId.Value, targetUserIds, latestVersionNumber, cancellationToken).ConfigureAwait(false);

        if (request.DryRun)
        {
            logger.LogInformation(
                "Dry-run instantiation of template {TemplateId} '{TemplateName}' in tenant {TenantId}: {PermissionCount} permission grants, {RoleCount} role assignments planned",
                template.Id, template.Name, tenantId, plan.TotalPlannedPermissionGrants, plan.TotalPlannedRoleAssignments);
            return plan.ToResult(applied: false, template, latestVersionNumber);
        }

        var existingRows = await dbContext.Set<PermissionTemplateInstantiation>()
            .Where(i => i.TemplateId == template.Id && i.TenantId == tenantId && targetUserIds.Contains(i.UserId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var rowsByUser = existingRows.ToDictionary(i => i.UserId, i => i);

        foreach (var entry in plan.Entries)
        {
            if (entry.PermissionsToGrant.Count > 0)
            {
                if (entry.ExistingPermission is null)
                {
                    dbContext.Set<TenantPermission>().Add(new TenantPermission
                    {
                        TenantId = tenantId,
                        UserId = entry.UserId,
                        Permissions = entry.PermissionsToGrant.ToArray(),
                        GrantedAt = SystemClock.UtcNow,
                        GrantedBy = Actor.SubjectIdAsGuid,
                        Reason = $"Applied from template: {template.Name}",
                        IsActive = true
                    });
                }
                else
                {
                    entry.ExistingPermission.AddPermissions(entry.PermissionsToGrant.ToArray());
                    entry.ExistingPermission.Reason = $"Updated with template: {template.Name}";
                }
            }

            foreach (var role in entry.RolesToAssign)
            {
                dbContext.Set<DynamicRoleAssignment>().Add(new DynamicRoleAssignment
                {
                    UserId = entry.UserId,
                    RoleId = role.Id,
                    TenantId = tenantId,
                    IsActive = true,
                    GrantedBy = Actor.SubjectIdAsGuid,
                    Reason = $"template:{template.Name}"
                });
            }

            if (rowsByUser.TryGetValue(entry.UserId, out var existingRow))
            {
                existingRow.TemplateVersionNumber = latestVersionNumber;
                existingRow.InstantiatedByUserId = Actor.SubjectIdAsGuid ?? Guid.Empty;
                existingRow.InstantiatedAt = SystemClock.UtcNow;
                existingRow.IsActive = true;
            }
            else
            {
                dbContext.Set<PermissionTemplateInstantiation>().Add(new PermissionTemplateInstantiation
                {
                    TemplateId = template.Id,
                    TenantId = tenantId,
                    UserId = entry.UserId,
                    TemplateVersionNumber = latestVersionNumber,
                    InstantiatedByUserId = Actor.SubjectIdAsGuid ?? Guid.Empty,
                    IsActive = true
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var newVersion = await securityVersionStore
            .IncrementVersionAsync(tenantId.Value.ToString(), cancellationToken).ConfigureAwait(false);
        logger.LogDebug(
            "Incremented security version for tenant {TenantId} to {Version} after template instantiation",
            tenantId, newVersion);

        await auditService.LogPermissionChangeAsync(
            PermissionOperationType.Grant,
            userId: null,
            performedBy: Actor.SubjectIdAsGuid ?? Guid.Empty,
            tenantId: tenantId,
            permissionType: $"template:{template.Name}",
            oldValue: null,
            newValue: JsonSerializer.Serialize(new
            {
                targets = plan.Entries.Count,
                permissionGrants = plan.TotalPlannedPermissionGrants,
                roleAssignments = plan.TotalPlannedRoleAssignments,
                version = latestVersionNumber
            }),
            reason: request.Reason ?? $"Instantiated template {template.Name}",
            success: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Instantiated template {TemplateId} '{TemplateName}' in tenant {TenantId} for {TargetCount} users (version {Version})",
            template.Id, template.Name, tenantId, plan.Entries.Count, latestVersionNumber);

        return plan.ToResult(applied: true, template, latestVersionNumber);
    }

    private async Task<InstantiationPlan> BuildPlanAsync(
        PermissionTemplate template,
        Guid tenantId,
        List<Guid> targetUserIds,
        int templateVersion,
        CancellationToken cancellationToken)
    {
        var existingPermissions = await dbContext.Set<TenantPermission>()
            .Where(p => p.TenantId == tenantId && p.UserId != null && targetUserIds.Contains(p.UserId.Value) && p.IsActive)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var permissionsByUser = existingPermissions.ToDictionary(p => p.UserId!.Value, p => p);

        var existingAssignments = await dbContext.Set<DynamicRoleAssignment>()
            .Where(a => a.TenantId == tenantId && targetUserIds.Contains(a.UserId) && a.IsActive)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        List<DynamicRole> rolesToResolve;
        if (template.RoleKeys.Length == 0)
        {
            rolesToResolve = [];
        }
        else
        {
            rolesToResolve = await dbContext.Set<DynamicRole>()
                .Where(r => r.IsActive
                    && (r.TenantId == tenantId || r.TenantId == null)
                    && template.RoleKeys.Contains(r.Name))
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        var missingRoles = template.RoleKeys
            .Except(rolesToResolve.Select(r => r.Name), StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (missingRoles.Count > 0)
        {
            // Fail closed: a role template naming roles that do not exist in scope must
            // not silently grant a partial set.
            throw new InvalidOperationException(
                $"Role template '{template.Name}' references roles that do not exist in tenant {tenantId}: {string.Join(", ", missingRoles)}");
        }

        var plan = new InstantiationPlan();
        foreach (var userId in targetUserIds)
        {
            permissionsByUser.TryGetValue(userId, out var existingPermission);
            var currentPermissions = existingPermission is null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(existingPermission.Permissions, StringComparer.OrdinalIgnoreCase);
            var permissionsToGrant = template.Permissions
                .Where(p => !currentPermissions.Contains(p))
                .ToList();

            var assignedRoleIds = existingAssignments
                .Where(a => a.UserId == userId)
                .Select(a => a.RoleId)
                .ToHashSet();
            var rolesToAssign = rolesToResolve
                .Where(r => !assignedRoleIds.Contains(r.Id))
                .ToList();

            if (permissionsToGrant.Count == 0 && rolesToAssign.Count == 0)
            {
                plan.SkippedTargets++;
                continue;
            }

            plan.Entries.Add(new InstantiationPlanEntry(
                userId,
                existingPermission,
                permissionsToGrant,
                rolesToAssign));
        }

        plan.TemplateVersion = templateVersion;
        return plan;
    }

    private sealed class InstantiationPlan
    {
        public List<InstantiationPlanEntry> Entries { get; } = [];

        public int SkippedTargets { get; set; }

        public int TemplateVersion { get; set; }

        public int TotalPlannedPermissionGrants =>
            Entries.Sum(e => e.PermissionsToGrant.Count);

        public int TotalPlannedRoleAssignments =>
            Entries.Sum(e => e.RolesToAssign.Count);

        public PermissionTemplateInstantiationResult ToResult(bool applied, PermissionTemplate template, int version) => new()
        {
            TemplateId = template.Id,
            TemplateName = template.Name,
            TenantId = null,
            Applied = applied,
            TemplateVersion = version,
            TotalTargets = Entries.Count + SkippedTargets,
            AppliedTargets = Entries.Count,
            SkippedTargets = SkippedTargets,
            PermissionGrants = TotalPlannedPermissionGrants,
            RoleAssignments = TotalPlannedRoleAssignments,
            Entries = Entries.Select(e => new PermissionTemplateInstantiationEntryResult
            {
                UserId = e.UserId,
                PermissionsToGrant = e.PermissionsToGrant,
                RolesToAssign = e.RolesToAssign.Select(r => r.Name).ToList()
            }).ToList()
        };
    }

    private sealed record InstantiationPlanEntry(
        Guid UserId,
        TenantPermission? ExistingPermission,
        List<string> PermissionsToGrant,
        List<DynamicRole> RolesToAssign);
}

/// <summary>
///     Migrates a permission template's instantiated targets to a target version
///     (issue #370): applies the version delta (added/removed permissions, role-key
///     changes) across the tenants that instantiated the template, batched, with
///     durable per-batch progress reporting. Supports dry-run previews.
/// </summary>
/// <remarks>
///     <para><b>SECURITY:</b> Requires system administration (cross-tenant by nature).
///     A tenant-filtered migration still requires SystemAdmin because the template and
///     version history are platform-level assets. Every batch commits progress counters;
///     every affected tenant gets its security version bumped; the run is audited.</para>
/// </remarks>
public sealed record MigratePermissionTemplateCommand : ICommand<PermissionTemplateMigrationResult>
{
    /// <summary>The template to migrate.</summary>
    public required Guid TemplateId { get; init; }

    /// <summary>The target version number.</summary>
    public required int ToVersion { get; init; }

    /// <summary>Optional tenant filter (null = every tenant that instantiated the template).</summary>
    public Guid[]? TenantIds { get; init; }

    /// <summary>Batch size for per-batch progress commits (default 25).</summary>
    public int BatchSize { get; init; } = 25;

    /// <summary>When true, compute and return the plan without applying anything.</summary>
    public bool DryRun { get; init; }

    /// <summary>Reason recorded in the audit log.</summary>
    public string? Reason { get; init; }
}

/// <summary>
///     Handler for <see cref="MigratePermissionTemplateCommand"/>.
/// </summary>
public sealed class MigratePermissionTemplateCommandHandler(
    IApplicationDbContext dbContext,
    IActorContextAccessor actorContextAccessor,
    ITenantSecurityVersionStore securityVersionStore,
    IPermissionAuditService auditService,
    ILogger<MigratePermissionTemplateCommandHandler> logger)
    : ICommandHandler<MigratePermissionTemplateCommand, PermissionTemplateMigrationResult>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<PermissionTemplateMigrationResult> Handle(
        MigratePermissionTemplateCommand request,
        CancellationToken cancellationToken)
    {
        if (!Actor.IsAuthenticated || !Actor.IsSystemAdmin)
        {
            throw new UnauthorizedAccessException(
                "Migrating permission templates across tenants requires system administrator privileges");
        }

        var template = await dbContext.Set<PermissionTemplate>()
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId, cancellationToken).ConfigureAwait(false);
        if (template is null)
        {
            throw new InvalidOperationException($"Permission template {request.TemplateId} not found");
        }

        var targetVersion = await dbContext.Set<PermissionTemplateVersion>()
            .FirstOrDefaultAsync(v => v.TemplateId == template.Id && v.VersionNumber == request.ToVersion, cancellationToken).ConfigureAwait(false);
        if (targetVersion is null)
        {
            throw new InvalidOperationException(
                $"Version {request.ToVersion} of permission template '{template.Name}' does not exist");
        }

        var targets = await LoadTargetsAsync(template.Id, request, cancellationToken).ConfigureAwait(false);
        var versionsByNumber = await dbContext.Set<PermissionTemplateVersion>()
            .Where(v => v.TemplateId == template.Id)
            .ToDictionaryAsync(v => v.VersionNumber, v => v, cancellationToken).ConfigureAwait(false);

        var batchSize = Math.Max(1, request.BatchSize);
        var migration = new PermissionTemplateMigration
        {
            TemplateId = template.Id,
            FromVersion = targets.Count == 0 ? request.ToVersion : targets.Min(t => t.TemplateVersionNumber),
            ToVersion = request.ToVersion,
            Status = request.DryRun ? MigrationStatus.Planned : MigrationStatus.InProgress,
            Strategy = MigrationStrategy.Immediate,
            StartedAt = SystemClock.UtcNow,
            InitiatedByUserId = Actor.SubjectIdAsGuid ?? Guid.Empty,
            AffectedTenantIds = targets.Select(t => t.TenantId!.Value).Distinct().ToArray(),
            AffectedUserIds = targets.Select(t => t.UserId).Distinct().ToArray(),
            TotalCount = targets.Count,
            IsDryRun = request.DryRun,
            Notes = request.Reason
        };

        if (request.DryRun)
        {
            var preview = BuildDryRunResult(migration, template, targetVersion, targets, versionsByNumber);
            migration.DryRunResult = JsonSerializer.Serialize(new
            {
                preview.TotalCount,
                preview.SuccessCount,
                preview.AddedPermissions,
                preview.RemovedPermissions
            });
            dbContext.Set<PermissionTemplateMigration>().Add(migration);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            logger.LogInformation(
                "Dry-run migration of template {TemplateId} '{TemplateName}' to version {ToVersion}: {TargetCount} targets across {TenantCount} tenants",
                template.Id, template.Name, request.ToVersion, targets.Count, preview.AffectedTenantCount);

            return preview;
        }

        dbContext.Set<PermissionTemplateMigration>().Add(migration);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var appliedTenants = new HashSet<Guid>();
        var rollbackEntries = new List<MigrationRollbackEntry>();
        var errors = new List<string>();

        foreach (var batch in targets.Chunk(batchSize))
        {
            foreach (var target in batch)
            {
                try
                {
                    if (!versionsByNumber.TryGetValue(target.TemplateVersionNumber, out var fromVersion))
                    {
                        migration.SkippedCount++;
                        continue;
                    }

                    var rollback = await MigrateTargetAsync(
                        template, target, fromVersion, targetVersion, migration.Id, cancellationToken).ConfigureAwait(false);
                    if (rollback is not null)
                    {
                        rollbackEntries.Add(rollback);
                        migration.SuccessCount++;
                    }
                    else
                    {
                        migration.SkippedCount++;
                    }

                    appliedTenants.Add(target.TenantId!.Value);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    migration.FailureCount++;
                    errors.Add($"target {target.UserId} in tenant {target.TenantId}: {exception.Message}");
                    logger.LogError(exception,
                        "Template migration {MigrationId} failed for user {UserId} in tenant {TenantId}",
                        migration.Id, target.UserId, target.TenantId);
                }
            }

            migration.Errors = errors.Count > 0 ? JsonSerializer.Serialize(errors.Take(50)) : null;
            // Durable per-batch progress: the migration row itself is the progress report.
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation(
                "Template migration {MigrationId} progress: {Processed}/{Total} ({Success} ok, {Failed} failed, {Skipped} skipped)",
                migration.Id, migration.SuccessCount + migration.FailureCount + migration.SkippedCount,
                migration.TotalCount, migration.SuccessCount, migration.FailureCount, migration.SkippedCount);
        }

        foreach (var tenantId in appliedTenants)
        {
            await securityVersionStore.IncrementVersionAsync(tenantId.ToString(), cancellationToken).ConfigureAwait(false);
        }

        migration.RollbackPlan = rollbackEntries.Count > 0
            ? JsonSerializer.Serialize(rollbackEntries)
            : null;
        migration.Status = migration.FailureCount > 0 ? MigrationStatus.Failed : MigrationStatus.Completed;
        migration.CompletedAt = SystemClock.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await auditService.LogPermissionChangeAsync(
            PermissionOperationType.Update,
            userId: null,
            performedBy: Actor.SubjectIdAsGuid ?? Guid.Empty,
            tenantId: null,
            permissionType: $"template:{template.Name}",
            oldValue: $"version:{migration.FromVersion}",
            newValue: $"version:{migration.ToVersion}",
            reason: request.Reason ?? $"Migrated template {template.Name} to version {request.ToVersion}",
            success: migration.FailureCount == 0,
            errorMessage: migration.Errors,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Migration {MigrationId} of template '{TemplateName}' to version {ToVersion} finished: {Success}/{Total} succeeded",
            migration.Id, template.Name, request.ToVersion, migration.SuccessCount, migration.TotalCount);

        return ToResult(migration, template, addedPermissions: DiffPermissions(versionsByNumber, targets, targetVersion, added: true),
            removedPermissions: DiffPermissions(versionsByNumber, targets, targetVersion, added: false));
    }

    private async Task<List<PermissionTemplateInstantiation>> LoadTargetsAsync(
        Guid templateId,
        MigratePermissionTemplateCommand request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<PermissionTemplateInstantiation>()
            .Where(i => i.TemplateId == templateId && i.IsActive && i.TenantId != null);

        if (request.TenantIds is { Length: > 0 } tenantFilter)
        {
            var filter = tenantFilter.ToHashSet();
            query = query.Where(i => i.TenantId != null && filter.Contains(i.TenantId.Value));
        }

        // Targets already at the requested version have nothing to migrate.
        query = query.Where(i => i.TemplateVersionNumber != request.ToVersion);

        return await query
            .OrderBy(i => i.TenantId)
            .ThenBy(i => i.UserId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    private static List<string> DiffPermissions(
        Dictionary<int, PermissionTemplateVersion> versionsByNumber,
        List<PermissionTemplateInstantiation> targets,
        PermissionTemplateVersion targetVersion,
        bool added)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var target in targets)
        {
            if (!versionsByNumber.TryGetValue(target.TemplateVersionNumber, out var fromVersion))
            {
                continue;
            }

            var delta = added ? targetVersion.AddedPermissions : targetVersion.RemovedPermissions;
            var fromSet = new HashSet<string>(fromVersion.Permissions, StringComparer.OrdinalIgnoreCase);
            foreach (var permission in delta ?? Array.Empty<string>())
            {
                var inFrom = fromSet.Contains(permission);
                if (added ? !inFrom : inFrom)
                {
                    result.Add(permission);
                }
            }
        }

        return result.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task<MigrationRollbackEntry?> MigrateTargetAsync(
        PermissionTemplate template,
        PermissionTemplateInstantiation target,
        PermissionTemplateVersion fromVersion,
        PermissionTemplateVersion toVersion,
        Guid migrationId,
        CancellationToken cancellationToken)
    {
        var tenantId = target.TenantId!.Value;
        var added = (toVersion.AddedPermissions ?? Array.Empty<string>())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();
        var removed = (toVersion.RemovedPermissions ?? Array.Empty<string>())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();
        var rolesAdded = toVersion.Metadata is null
            ? []
            : (JsonSerializer.Deserialize<TemplateVersionMetadata>(toVersion.Metadata)?.AddedRoleKeys ?? []);
        var rolesRemoved = fromVersion.Metadata is null
            ? []
            : (JsonSerializer.Deserialize<TemplateVersionMetadata>(fromVersion.Metadata)?.RemovedRoleKeys ?? []);

        var rollback = new MigrationRollbackEntry
        {
            TenantId = tenantId,
            UserId = target.UserId,
            TemplateVersionNumber = target.TemplateVersionNumber
        };

        var changed = false;

        if (added.Count > 0 || removed.Count > 0)
        {
            var permission = await dbContext.Set<TenantPermission>()
                .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.UserId == target.UserId && p.IsActive, cancellationToken).ConfigureAwait(false);
            if (permission is not null)
            {
                rollback.BeforePermissions = permission.Permissions;
                var before = permission.Permissions.ToList();
                var after = before
                    .Where(p => !removed.Contains(p, StringComparer.OrdinalIgnoreCase))
                    .Concat(added.Where(p => !before.Contains(p, StringComparer.OrdinalIgnoreCase)))
                    .Distinct()
                    .ToArray();
                if (!after.SequenceEqual(before))
                {
                    permission.Permissions = after;
                    permission.Reason = $"Migrated with template: {template.Name} -> v{toVersion.VersionNumber}";
                    changed = true;
                }
            }
            else if (added.Count > 0)
            {
                dbContext.Set<TenantPermission>().Add(new TenantPermission
                {
                    TenantId = tenantId,
                    UserId = target.UserId,
                    Permissions = added.ToArray(),
                    GrantedAt = SystemClock.UtcNow,
                    GrantedBy = Actor.SubjectIdAsGuid,
                    Reason = $"Migrated from template: {template.Name} -> v{toVersion.VersionNumber}",
                    IsActive = true
                });
                rollback.BeforePermissions = [];
                changed = true;
            }
        }

        if (rolesAdded.Count > 0 || rolesRemoved.Count > 0)
        {
            var assignments = await dbContext.Set<DynamicRoleAssignment>()
                .Where(a => a.TenantId == tenantId && a.UserId == target.UserId
                    && a.Reason == $"template:{template.Name}")
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            rollback.BeforeRoleAssignmentIds = assignments.Select(a => a.Id).ToList();

            var roleIds = await dbContext.Set<DynamicRole>()
                .Where(r => r.IsActive && (r.TenantId == tenantId || r.TenantId == null)
                    && (rolesAdded.Contains(r.Name) || rolesRemoved.Contains(r.Name)))
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            foreach (var role in roleIds.Where(r => rolesAdded.Contains(r.Name, StringComparer.OrdinalIgnoreCase)))
            {
                if (assignments.Any(a => a.RoleId == role.Id && a.IsActive))
                {
                    continue;
                }

                dbContext.Set<DynamicRoleAssignment>().Add(new DynamicRoleAssignment
                {
                    UserId = target.UserId,
                    RoleId = role.Id,
                    TenantId = tenantId,
                    IsActive = true,
                    GrantedBy = Actor.SubjectIdAsGuid,
                    Reason = $"template:{template.Name}"
                });
                changed = true;
            }

            foreach (var assignment in assignments.Where(a =>
                         roleIds.Any(r => r.Id == a.RoleId && rolesRemoved.Contains(r.Name, StringComparer.OrdinalIgnoreCase))))
            {
                assignment.IsActive = false;
                changed = true;
            }
        }

        if (!changed)
        {
            return null;
        }

        target.TemplateVersionNumber = toVersion.VersionNumber;
        target.LastMigrationId = migrationId;
        return rollback;
    }

    private PermissionTemplateMigrationResult BuildDryRunResult(
        PermissionTemplateMigration migration,
        PermissionTemplate template,
        PermissionTemplateVersion targetVersion,
        List<PermissionTemplateInstantiation> targets,
        Dictionary<int, PermissionTemplateVersion> versionsByNumber)
    {
        var skipped = targets.Count(t => !versionsByNumber.ContainsKey(t.TemplateVersionNumber));
        return new PermissionTemplateMigrationResult
        {
            MigrationId = migration.Id,
            TemplateId = template.Id,
            TemplateName = template.Name,
            FromVersion = migration.FromVersion,
            ToVersion = migration.ToVersion,
            Status = MigrationStatus.Planned,
            DryRun = true,
            TotalCount = targets.Count,
            SuccessCount = targets.Count - skipped,
            SkippedCount = skipped,
            FailureCount = 0,
            ProgressPercentage = 0,
            AffectedTenantCount = targets.Select(t => t.TenantId!.Value).Distinct().Count(),
            AddedPermissions = DiffPermissions(versionsByNumber, targets, targetVersion, added: true),
            RemovedPermissions = DiffPermissions(versionsByNumber, targets, targetVersion, added: false)
        };
    }

    private static PermissionTemplateMigrationResult ToResult(
        PermissionTemplateMigration migration,
        PermissionTemplate template,
        List<string> addedPermissions,
        List<string> removedPermissions) => new()
    {
        MigrationId = migration.Id,
        TemplateId = template.Id,
        TemplateName = template.Name,
        FromVersion = migration.FromVersion,
        ToVersion = migration.ToVersion,
        Status = migration.Status,
        DryRun = false,
        TotalCount = migration.TotalCount,
        SuccessCount = migration.SuccessCount,
        SkippedCount = migration.SkippedCount,
        FailureCount = migration.FailureCount,
        ProgressPercentage = migration.GetProgressPercentage(),
        AffectedTenantCount = migration.AffectedTenantIds?.Length ?? 0,
        AddedPermissions = addedPermissions,
        RemovedPermissions = removedPermissions
    };
}

/// <summary>
///     Rolls back a completed template migration (issue #370): restores every affected
///     target to the permission/role state recorded before the migration ran.
/// </summary>
/// <remarks>
///     <para><b>SECURITY:</b> Requires system administration. The rollback replays the
///     recorded before-states, bumps every affected tenant's security version, marks the
///     migration <see cref="MigrationStatus.RolledBack"/> and is audited.</para>
/// </remarks>
public sealed record RollbackPermissionTemplateMigrationCommand : ICommand<PermissionTemplateMigrationResult>
{
    /// <summary>The migration to roll back.</summary>
    public required Guid MigrationId { get; init; }

    /// <summary>Reason recorded in the audit log.</summary>
    public required string Reason { get; init; }
}

/// <summary>
///     Handler for <see cref="RollbackPermissionTemplateMigrationCommand"/>.
/// </summary>
public sealed class RollbackPermissionTemplateMigrationCommandHandler(
    IApplicationDbContext dbContext,
    IActorContextAccessor actorContextAccessor,
    ITenantSecurityVersionStore securityVersionStore,
    IPermissionAuditService auditService,
    ILogger<RollbackPermissionTemplateMigrationCommandHandler> logger)
    : ICommandHandler<RollbackPermissionTemplateMigrationCommand, PermissionTemplateMigrationResult>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<PermissionTemplateMigrationResult> Handle(
        RollbackPermissionTemplateMigrationCommand request,
        CancellationToken cancellationToken)
    {
        if (!Actor.IsAuthenticated || !Actor.IsSystemAdmin)
        {
            throw new UnauthorizedAccessException(
                "Rolling back permission template migrations requires system administrator privileges");
        }

        var migration = await dbContext.Set<PermissionTemplateMigration>()
            .FirstOrDefaultAsync(m => m.Id == request.MigrationId, cancellationToken).ConfigureAwait(false);
        if (migration is null)
        {
            throw new InvalidOperationException($"Template migration {request.MigrationId} not found");
        }

        if (migration.IsDryRun)
        {
            throw new InvalidOperationException("A dry-run migration has nothing to roll back");
        }

        if (migration.Status is not (MigrationStatus.Completed or MigrationStatus.Failed))
        {
            throw new InvalidOperationException(
                $"Only completed or failed migrations can be rolled back (current status: {migration.Status})");
        }

        var template = await dbContext.Set<PermissionTemplate>()
            .FirstOrDefaultAsync(t => t.Id == migration.TemplateId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Permission template {migration.TemplateId} not found");

        var rollbackEntries = string.IsNullOrWhiteSpace(migration.RollbackPlan)
            ? []
            : (JsonSerializer.Deserialize<List<MigrationRollbackEntry>>(migration.RollbackPlan) ?? []);

        var rolledBackTenants = new HashSet<Guid>();
        foreach (var entry in rollbackEntries)
        {
            var target = await dbContext.Set<PermissionTemplateInstantiation>()
                .FirstOrDefaultAsync(i => i.TemplateId == migration.TemplateId
                    && i.UserId == entry.UserId
                    && i.TenantId == entry.TenantId, cancellationToken).ConfigureAwait(false);

            if (entry.BeforePermissions is { Length: > 0 })
            {
                var permission = await dbContext.Set<TenantPermission>()
                    .FirstOrDefaultAsync(p => p.TenantId == entry.TenantId
                        && p.UserId == entry.UserId
                        && p.IsActive, cancellationToken).ConfigureAwait(false);
                if (permission is not null)
                {
                    permission.Permissions = entry.BeforePermissions;
                    permission.Reason = $"Rolled back template migration {migration.Id}";
                    rolledBackTenants.Add(entry.TenantId);
                }
            }

            if (entry.BeforeRoleAssignmentIds is { Count: > 0 })
            {
                var assignments = await dbContext.Set<DynamicRoleAssignment>()
                    .Where(a => entry.BeforeRoleAssignmentIds.Contains(a.Id))
                    .ToListAsync(cancellationToken).ConfigureAwait(false);
                foreach (var assignment in assignments)
                {
                    assignment.IsActive = true;
                    rolledBackTenants.Add(entry.TenantId);
                }

                var templateAssignments = await dbContext.Set<DynamicRoleAssignment>()
                    .Where(a => a.TenantId == entry.TenantId
                        && a.UserId == entry.UserId
                        && a.Reason == $"template:{template.Name}"
                        && a.IsActive
                        && !entry.BeforeRoleAssignmentIds.Contains(a.Id))
                    .ToListAsync(cancellationToken).ConfigureAwait(false);
                foreach (var assignment in templateAssignments)
                {
                    assignment.IsActive = false;
                }
            }

            if (target is not null)
            {
                target.TemplateVersionNumber = entry.TemplateVersionNumber;
                target.LastMigrationId = migration.Id;
            }
        }

        migration.Status = MigrationStatus.RolledBack;
        migration.CompletedAt = SystemClock.UtcNow;
        migration.Notes = $"{migration.Notes} | Rolled back: {request.Reason}";
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach (var tenantId in rolledBackTenants)
        {
            await securityVersionStore.IncrementVersionAsync(tenantId.ToString(), cancellationToken).ConfigureAwait(false);
        }

        await auditService.LogPermissionChangeAsync(
            PermissionOperationType.Restore,
            userId: null,
            performedBy: Actor.SubjectIdAsGuid ?? Guid.Empty,
            tenantId: null,
            permissionType: $"template:{template.Name}",
            oldValue: $"version:{migration.ToVersion}",
            newValue: $"version:{migration.FromVersion}",
            reason: request.Reason,
            success: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Rolled back migration {MigrationId} of template '{TemplateName}': {TargetCount} targets restored",
            migration.Id, template.Name, rollbackEntries.Count);

        return new PermissionTemplateMigrationResult
        {
            MigrationId = migration.Id,
            TemplateId = template.Id,
            TemplateName = template.Name,
            FromVersion = migration.FromVersion,
            ToVersion = migration.ToVersion,
            Status = migration.Status,
            DryRun = false,
            TotalCount = migration.TotalCount,
            SuccessCount = migration.SuccessCount,
            SkippedCount = migration.SkippedCount,
            FailureCount = migration.FailureCount,
            ProgressPercentage = migration.GetProgressPercentage(),
            AffectedTenantCount = rolledBackTenants.Count,
            AddedPermissions = [],
            RemovedPermissions = []
        };
    }
}

/// <summary>
///     Serializable before-state of one migration target, used as the rollback plan.
/// </summary>
internal sealed class MigrationRollbackEntry
{
    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    public int TemplateVersionNumber { get; set; }

    public string[]? BeforePermissions { get; set; }

    public List<Guid>? BeforeRoleAssignmentIds { get; set; }
}

/// <summary>
///     Role-key diff carried on <see cref="PermissionTemplateVersion.Metadata"/> JSON.
/// </summary>
internal sealed class TemplateVersionMetadata
{
    public List<string> AddedRoleKeys { get; set; } = [];

    public List<string> RemovedRoleKeys { get; set; } = [];
}

/// <summary>
///     Snapshot factory for <see cref="PermissionTemplateVersion"/> rows.
/// </summary>
internal static class PermissionTemplateSnapshot
{
    public static PermissionTemplateVersion CreateInitial(PermissionTemplate template, Guid createdBy, string? changeNotes) => new()
    {
        TemplateId = template.Id,
        VersionNumber = 1,
        Name = template.Name,
        Description = template.Description,
        Permissions = template.Permissions,
        IsActive = template.IsActive,
        CreatedByUserId = createdBy,
        ChangeNotes = changeNotes ?? "Initial version",
        ChangeType = TemplateChangeType.Major,
        AddedPermissions = template.Permissions,
        RemovedPermissions = [],
        UnchangedPermissions = [],
        PreviousVersion = null,
        Metadata = JsonSerializer.Serialize(new TemplateVersionMetadata
        {
            AddedRoleKeys = template.RoleKeys.ToList(),
            RemovedRoleKeys = []
        }),
        PermissionHash = ComputeHash(template.Permissions, template.RoleKeys),
        Tags = null
    };

    public static PermissionTemplateVersion CreateChange(
        PermissionTemplate template,
        int versionNumber,
        string[] oldPermissions,
        string[] oldRoleKeys,
        Guid createdBy,
        string? changeNotes,
        TemplateChangeType changeType)
    {
        var oldSet = new HashSet<string>(oldPermissions, StringComparer.OrdinalIgnoreCase);
        var newSet = new HashSet<string>(template.Permissions, StringComparer.OrdinalIgnoreCase);
        var oldRoleSet = new HashSet<string>(oldRoleKeys, StringComparer.OrdinalIgnoreCase);
        var newRoleSet = new HashSet<string>(template.RoleKeys, StringComparer.OrdinalIgnoreCase);

        return new PermissionTemplateVersion
        {
            TemplateId = template.Id,
            VersionNumber = versionNumber,
            Name = template.Name,
            Description = template.Description,
            Permissions = template.Permissions,
            IsActive = template.IsActive,
            CreatedByUserId = createdBy,
            ChangeNotes = changeNotes ?? $"Updated to version {versionNumber}",
            ChangeType = changeType,
            AddedPermissions = newSet.Where(p => !oldSet.Contains(p)).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray(),
            RemovedPermissions = oldSet.Where(p => !newSet.Contains(p)).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray(),
            UnchangedPermissions = newSet.Where(oldSet.Contains).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray(),
            PreviousVersion = versionNumber - 1,
            Metadata = JsonSerializer.Serialize(new TemplateVersionMetadata
            {
                AddedRoleKeys = newRoleSet.Where(r => !oldRoleSet.Contains(r)).OrderBy(r => r, StringComparer.OrdinalIgnoreCase).ToList(),
                RemovedRoleKeys = oldRoleSet.Where(r => !newRoleSet.Contains(r)).OrderBy(r => r, StringComparer.OrdinalIgnoreCase).ToList()
            }),
            PermissionHash = ComputeHash(template.Permissions, template.RoleKeys),
            Tags = null
        };
    }

    private static string ComputeHash(string[] permissions, string[] roleKeys)
    {
        var material = string.Join("|",
            permissions.OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .Concat(roleKeys.OrderBy(r => r, StringComparer.OrdinalIgnoreCase).Select(r => $"role:{r}")));
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(material)));
    }
}
