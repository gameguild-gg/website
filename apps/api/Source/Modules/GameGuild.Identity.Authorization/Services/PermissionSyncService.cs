using System.Text.Json.Serialization;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Import/export contract for external system permission synchronization (issue #358).
///     DTOs are JSON-serializable and deliberately portable: roles reference their
///     parents by name so a document produced by one environment can be imported into
///     another.
/// </summary>
/// <param name="SchemaVersion">Contract version; the only supported value is <c>1.0</c>.</param>
/// <param name="ExportedAtUtc">When the document was produced.</param>
/// <param name="TenantId">Tenant scope of the document (null = global, system-admin only).</param>
/// <param name="Roles">Role definitions to synchronize.</param>
/// <param name="Permissions">Permission entries (tenant defaults and user grants) to synchronize.</param>
public sealed record ExternalPermissionSyncDocument(
    [property: JsonPropertyName("schemaVersion")] string SchemaVersion,
    [property: JsonPropertyName("exportedAtUtc")] DateTime ExportedAtUtc,
    [property: JsonPropertyName("tenantId")] Guid? TenantId,
    [property: JsonPropertyName("roles")] List<ExternalRoleDefinition> Roles,
    [property: JsonPropertyName("permissions")] List<ExternalPermissionEntry> Permissions)
{
    /// <summary>The only schema version accepted by the importer.</summary>
    public const string SupportedSchemaVersion = "1.0";
}

/// <summary>Role definition in the sync contract.</summary>
/// <param name="Name">Unique role name within the tenant scope.</param>
/// <param name="DisplayName">Display name.</param>
/// <param name="Description">Optional description.</param>
/// <param name="Permissions">Directly assigned allow permissions.</param>
/// <param name="DenyPermissions">Directly assigned deny permissions (DENY-WINS).</param>
/// <param name="ParentRoleName">Primary parent role name (optional).</param>
/// <param name="AdditionalParentRoleNames">Additional parent role names (multi-parent inheritance, issue #358).</param>
/// <param name="BlockedInheritedPermissions">Inherited permissions the role opts out of (selective blocking, issue #358).</param>
/// <param name="Priority">Conflict-resolution priority.</param>
/// <param name="IsActive">Whether the role is active.</param>
public sealed record ExternalRoleDefinition(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("displayName")] string? DisplayName,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("permissions")] string[] Permissions,
    [property: JsonPropertyName("denyPermissions")] string[] DenyPermissions,
    [property: JsonPropertyName("parentRoleName")] string? ParentRoleName,
    [property: JsonPropertyName("additionalParentRoleNames")] string[]? AdditionalParentRoleNames,
    [property: JsonPropertyName("blockedInheritedPermissions")] string[]? BlockedInheritedPermissions,
    [property: JsonPropertyName("priority")] int Priority,
    [property: JsonPropertyName("isActive")] bool IsActive);

/// <summary>Permission entry in the sync contract (one TenantPermission row).</summary>
/// <param name="UserId">Subject user (null = tenant defaults).</param>
/// <param name="Permissions">Allowed permissions.</param>
/// <param name="DenyPermissions">Denied permissions (DENY-WINS).</param>
/// <param name="IsActive">Whether the entry is active.</param>
public sealed record ExternalPermissionEntry(
    [property: JsonPropertyName("userId")] Guid? UserId,
    [property: JsonPropertyName("permissions")] string[] Permissions,
    [property: JsonPropertyName("denyPermissions")] string[] DenyPermissions,
    [property: JsonPropertyName("isActive")] bool IsActive);

/// <summary>
///     One planned change of a synchronization import.
/// </summary>
/// <param name="Kind">Change kind: <c>role.create</c>, <c>role.update</c>, <c>permissions.grant</c>, <c>permissions.revoke</c>, <c>permissions.deny</c>, <c>permissions.removeDeny</c>.</param>
/// <param name="Target">Human-readable target description (role name or user id).</param>
/// <param name="Payload">The permissions or role fields involved.</param>
public sealed record PermissionSyncChange(
    string Kind,
    string Target,
    IReadOnlyDictionary<string, string> Payload);

/// <summary>
///     Result of a synchronization import (or dry-run preview).
/// </summary>
/// <param name="IsValid">False when validation failed — nothing was applied (fail-closed).</param>
/// <param name="ValidationErrors">Validation problems (empty when valid).</param>
/// <param name="Changes">The planned changes (the full plan when valid).</param>
/// <param name="Applied">True when the plan was actually applied (false for dry-runs and invalid documents).</param>
/// <param name="DryRun">True when this was a dry-run.</param>
public sealed record PermissionSyncImportResult(
    bool IsValid,
    IReadOnlyList<string> ValidationErrors,
    IReadOnlyList<PermissionSyncChange> Changes,
    bool Applied,
    bool DryRun);

/// <summary>
///     External system permission synchronization (issue #358): exports the permission
///     state of a tenant as a portable JSON document and imports (or dry-runs) a
///     document back into the platform.
/// </summary>
/// <remarks>
///     <para>
///         <b>Fail-closed import.</b> A document that fails any validation rule
///         (unsupported schema version, size caps, unknown/dangling parent references,
///         parent cycles, cross-tenant scope) is rejected in full — imports are atomic at
///         the document level and never partially applied.
///     </para>
///     <para>
///         <b>Guarded, versioned, audited.</b> Every applied change goes through the
///         guarded mutation paths (grant service for permission rows, role repository for
///         roles with cycle validation), bumps the tenant security version, writes a
///         <see cref="PermissionOperationType.SyncImport"/> audit entry and fans out a
///         <c>permission.synced</c> webhook notification.
///     </para>
/// </remarks>
public interface IPermissionSyncService
{
    /// <summary>Exports the permission state of a tenant (or the global state for system admins).</summary>
    Task<ExternalPermissionSyncDocument> ExportAsync(Guid? tenantId, CancellationToken cancellationToken = default);

    /// <summary>Validates a document and computes the change plan without applying anything.</summary>
    Task<PermissionSyncImportResult> PreviewImportAsync(
        ExternalPermissionSyncDocument document,
        Guid? targetTenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Validates and (unless dry-run) applies a document. Invalid documents apply nothing.</summary>
    Task<PermissionSyncImportResult> ImportAsync(
        ExternalPermissionSyncDocument document,
        Guid? targetTenantId,
        bool dryRun,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Default <see cref="IPermissionSyncService"/> implementation.
/// </summary>
public sealed class PermissionSyncService(
    IDynamicRoleRepository roleRepository,
    ITenantPermissionRepository permissionRepository,
    IPermissionGrantService grantService,
    IRoleInheritanceEngine inheritanceEngine,
    IPermissionAuditService auditService,
    IActorContextAccessor actorContextAccessor,
    IEnumerable<IPermissionChangeNotifier> changeNotifiers,
    IOptions<PermissionEngineOptions> engineOptions,
    ILogger<PermissionSyncService> logger) : IPermissionSyncService
{
    private readonly PermissionEngineOptions _options = engineOptions.Value;

    private ActorContext Actor => actorContextAccessor.ActorContext;

    /// <inheritdoc />
    public async Task<ExternalPermissionSyncDocument> ExportAsync(Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var roles = await roleRepository.GetByTenantAsync(tenantId, includeGlobal: false, cancellationToken).ConfigureAwait(false);
        var roleNames = roles.ToDictionary(role => role.Id, role => role.Name);

        string? NameOf(Guid? roleId)
            => roleId.HasValue && roleNames.TryGetValue(roleId.Value, out var name) ? name : null;

        var externalRoles = roles
            .Select(role => new ExternalRoleDefinition(
                role.Name,
                role.DisplayName,
                role.Description,
                role.Permissions,
                role.DenyPermissions,
                NameOf(role.ParentRoleId),
                role.AdditionalParentRoleIds.Select(id => NameOf(id)).Where(name => name is not null).Select(name => name!).ToArray(),
                role.BlockedInheritedPermissions,
                role.Priority,
                role.IsActive))
            .ToList();

        var permissions = new List<ExternalPermissionEntry>();
        if (tenantId.HasValue)
        {
            var rows = await permissionRepository.GetByTenantAsync(tenantId.Value, cancellationToken).ConfigureAwait(false);
            permissions.AddRange(rows.Select(row => new ExternalPermissionEntry(
                row.UserId,
                row.Permissions,
                row.DenyPermissions,
                row.IsActive)));
        }

        logger.LogInformation(
            "Exported permission sync document for tenant {TenantId}: {RoleCount} roles, {PermissionCount} permission entries.",
            tenantId,
            externalRoles.Count,
            permissions.Count);

        return new ExternalPermissionSyncDocument(
            ExternalPermissionSyncDocument.SupportedSchemaVersion,
            SystemClock.UtcNow,
            tenantId,
            externalRoles,
            permissions);
    }

    /// <inheritdoc />
    public Task<PermissionSyncImportResult> PreviewImportAsync(
        ExternalPermissionSyncDocument document,
        Guid? targetTenantId,
        CancellationToken cancellationToken = default)
        => ImportAsync(document, targetTenantId, dryRun: true, cancellationToken);

    /// <inheritdoc />
    public async Task<PermissionSyncImportResult> ImportAsync(
        ExternalPermissionSyncDocument document,
        Guid? targetTenantId,
        bool dryRun,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var (errors, changes) = await ValidateAndPlanAsync(document, targetTenantId, cancellationToken).ConfigureAwait(false);
        if (errors.Count > 0)
        {
            logger.LogWarning(
                "Permission sync import rejected with {ErrorCount} validation error(s); nothing applied (fail-closed).",
                errors.Count);
            return new PermissionSyncImportResult(false, errors, changes, Applied: false, dryRun);
        }

        if (dryRun)
        {
            logger.LogInformation(
                "Permission sync dry-run for tenant {TenantId}: {ChangeCount} planned change(s), nothing applied.",
                targetTenantId,
                changes.Count);
            return new PermissionSyncImportResult(true, errors, changes, Applied: false, DryRun: true);
        }

        await ApplyAsync(document, targetTenantId, changes, cancellationToken).ConfigureAwait(false);
        logger.LogInformation(
            "Permission sync import applied {ChangeCount} change(s) for tenant {TenantId}.",
            changes.Count,
            targetTenantId);
        return new PermissionSyncImportResult(true, errors, changes, Applied: true, DryRun: false);
    }

    /// <summary>
    ///     Validates the document against the target tenant and computes the change plan.
    ///     Any error aborts the whole import (fail-closed, no partial apply).
    /// </summary>
    private async Task<(List<string> Errors, List<PermissionSyncChange> Changes)> ValidateAndPlanAsync(
        ExternalPermissionSyncDocument document,
        Guid? targetTenantId,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var changes = new List<PermissionSyncChange>();

        if (!string.Equals(document.SchemaVersion, ExternalPermissionSyncDocument.SupportedSchemaVersion, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Unsupported schema version '{document.SchemaVersion}'; supported: '{ExternalPermissionSyncDocument.SupportedSchemaVersion}'.");
            return (errors, changes);
        }

        if (document.Roles is null || document.Permissions is null)
        {
            errors.Add("Document roles and permissions collections are required.");
            return (errors, changes);
        }

        if (document.Roles.Count > _options.ExternalSync.MaxRolesPerImport)
        {
            errors.Add($"Document contains {document.Roles.Count} roles; the configured cap is {_options.ExternalSync.MaxRolesPerImport}.");
        }

        if (document.Permissions.Count > _options.ExternalSync.MaxPermissionEntriesPerImport)
        {
            errors.Add($"Document contains {document.Permissions.Count} permission entries; the configured cap is {_options.ExternalSync.MaxPermissionEntriesPerImport}.");
        }

        // Cross-tenant guard: a tenant-scoped document can only be imported into its own
        // tenant scope; global documents only into the global scope.
        if (document.TenantId.HasValue && targetTenantId.HasValue && document.TenantId.Value != targetTenantId.Value)
        {
            errors.Add($"Document tenant {document.TenantId} does not match the import target tenant {targetTenantId}.");
        }

        if (document.TenantId.HasValue && targetTenantId is null)
        {
            errors.Add("A tenant-scoped document cannot be imported into the global scope.");
        }

        var duplicateNames = document.Roles
            .Where(role => !string.IsNullOrWhiteSpace(role.Name))
            .GroupBy(role => role.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        foreach (var duplicate in duplicateNames)
        {
            errors.Add($"Duplicate role name in document: '{duplicate}'.");
        }

        foreach (var role in document.Roles)
        {
            if (string.IsNullOrWhiteSpace(role.Name))
            {
                errors.Add("Every role definition requires a non-empty name.");
                continue;
            }

            if (role.Permissions is null || role.DenyPermissions is null)
            {
                errors.Add($"Role '{role.Name}': permissions and denyPermissions are required.");
            }

            if (role.Permissions?.Contains("admin:*", StringComparer.OrdinalIgnoreCase) == true)
            {
                errors.Add($"Role '{role.Name}': the non-delegable wildcard 'admin:*' cannot be imported.");
            }
        }

        // Parent references must resolve (inside the document or already present) and must
        // not create cycles.
        var documentNames = document.Roles
            .Where(role => !string.IsNullOrWhiteSpace(role.Name))
            .Select(role => role.Name!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var existingRoles = await roleRepository.GetByTenantAsync(targetTenantId, includeGlobal: true, cancellationToken).ConfigureAwait(false);
        var existingByName = existingRoles
            .Where(role => !string.IsNullOrWhiteSpace(role.Name))
            .GroupBy(role => role.Name!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var role in document.Roles)
        {
            if (string.IsNullOrWhiteSpace(role.Name))
            {
                continue;
            }

            var parentNames = new List<string>();
            if (!string.IsNullOrWhiteSpace(role.ParentRoleName))
            {
                parentNames.Add(role.ParentRoleName!);
            }

            if (role.AdditionalParentRoleNames is not null)
            {
                parentNames.AddRange(role.AdditionalParentRoleNames.Where(name => !string.IsNullOrWhiteSpace(name))!);
            }

            foreach (var parentName in parentNames)
            {
                if (!documentNames.Contains(parentName) && !existingByName.ContainsKey(parentName))
                {
                    errors.Add($"Role '{role.Name}' references unknown parent role '{parentName}'.");
                }
            }

            if (parentNames.Contains(role.Name, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add($"Role '{role.Name}' cannot inherit from itself.");
            }
        }

        // Cycle validation: first inside the document graph itself (new roles may only
        // cycle among document roles), then against persisted roles for updates.
        var documentGraph = documentRolesWithParents(document)
            .ToDictionary(role => role.name, role => role.parentNames, StringComparer.OrdinalIgnoreCase);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visitedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Visit(string name)
        {
            if (visitedNames.Contains(name))
            {
                return;
            }

            if (!visiting.Add(name))
            {
                errors.Add($"Role '{name}': the document role parents would create an inheritance cycle.");
                return;
            }

            if (documentGraph.TryGetValue(name, out var parents))
            {
                foreach (var parent in parents)
                {
                    if (documentGraph.ContainsKey(parent))
                    {
                        Visit(parent);
                    }
                }
            }

            visiting.Remove(name);
            visitedNames.Add(name);
        }

        foreach (var name in documentGraph.Keys)
        {
            Visit(name);
        }

        foreach (var (name, parentNames) in documentGraph)
        {
            if (!existingByName.TryGetValue(name, out var existing))
            {
                continue;
            }

            // Updating an existing role's parents: check the candidate parent set that
            // resolves to persisted roles (document-new parents cannot close a DB cycle).
            var candidateParents = ResolveParentIds(parentNames, existingByName);
            if (candidateParents.Count > 0
                && await inheritanceEngine.WouldCreateCycleAsync(existing.Id, candidateParents, cancellationToken).ConfigureAwait(false))
            {
                errors.Add($"Role '{name}': the requested parents would create an inheritance cycle.");
            }
        }

        // Permission entries: user rows and tenant defaults.
        var existingTenantRows = targetTenantId.HasValue
            ? await permissionRepository.GetByTenantAsync(targetTenantId.Value, cancellationToken).ConfigureAwait(false)
            : new List<TenantPermission>();
        var existingRowsByUser = existingTenantRows.ToDictionary(row => row.UserId ?? Guid.Empty);

        foreach (var entry in document.Permissions)
        {
            if (entry.Permissions is null || entry.DenyPermissions is null)
            {
                errors.Add("Every permission entry requires permissions and denyPermissions collections.");
                continue;
            }

            if (entry.Permissions.Contains("admin:*", StringComparer.OrdinalIgnoreCase))
            {
                errors.Add($"Permission entry for user {entry.UserId}: the non-delegable wildcard 'admin:*' cannot be imported.");
            }
        }

        if (errors.Count > 0)
        {
            return (errors, changes);
        }

        // ---- Plan (validated) ----
        foreach (var role in document.Roles)
        {
            var exists = existingByName.ContainsKey(role.Name!);
            changes.Add(new PermissionSyncChange(
                exists ? "role.update" : "role.create",
                $"role:{role.Name}",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["permissions"] = string.Join(",", role.Permissions),
                    ["denyPermissions"] = string.Join(",", role.DenyPermissions),
                    ["parents"] = string.Join(",", ParentNamesOf(role))
                }));
        }

        foreach (var entry in document.Permissions)
        {
            var target = entry.UserId.HasValue ? $"user:{entry.UserId}" : "tenant-defaults";
            var existingRow = existingRowsByUser.TryGetValue(entry.UserId ?? Guid.Empty, out var row) ? row : null;
            var existingAllows = existingRow?.Permissions ?? Array.Empty<string>();
            var existingDenies = existingRow?.DenyPermissions ?? Array.Empty<string>();

            var toGrant = entry.Permissions.Except(existingAllows, StringComparer.OrdinalIgnoreCase).ToArray();
            var toRevoke = existingAllows.Except(entry.Permissions, StringComparer.OrdinalIgnoreCase).ToArray();
            var toDeny = entry.DenyPermissions.Except(existingDenies, StringComparer.OrdinalIgnoreCase).ToArray();
            var toUnDeny = existingDenies.Except(entry.DenyPermissions, StringComparer.OrdinalIgnoreCase).ToArray();

            if (toGrant.Length > 0)
            {
                changes.Add(new PermissionSyncChange("permissions.grant", target, new Dictionary<string, string> { ["permissions"] = string.Join(",", toGrant) }));
            }

            if (toRevoke.Length > 0)
            {
                changes.Add(new PermissionSyncChange("permissions.revoke", target, new Dictionary<string, string> { ["permissions"] = string.Join(",", toRevoke) }));
            }

            if (toDeny.Length > 0)
            {
                changes.Add(new PermissionSyncChange("permissions.deny", target, new Dictionary<string, string> { ["permissions"] = string.Join(",", toDeny) }));
            }

            if (toUnDeny.Length > 0)
            {
                changes.Add(new PermissionSyncChange("permissions.removeDeny", target, new Dictionary<string, string> { ["permissions"] = string.Join(",", toUnDeny) }));
            }
        }

        return (errors, changes);
    }

    /// <summary>
    ///     Applies a validated plan. Roles first (parents exist before children rely on
    ///     them), then permission entries through the guarded grant service.
    /// </summary>
    private async Task ApplyAsync(
        ExternalPermissionSyncDocument document,
        Guid? targetTenantId,
        IReadOnlyList<PermissionSyncChange> changes,
        CancellationToken cancellationToken)
    {
        var performedBy = Actor.SubjectIdAsGuid ?? Guid.Empty;
        var existingRoles = await roleRepository.GetByTenantAsync(targetTenantId, includeGlobal: false, cancellationToken).ConfigureAwait(false);
        var existingByName = existingRoles
            .GroupBy(role => role.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var resolvedIdsByName = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in existingByName)
        {
            resolvedIdsByName[pair.Key] = pair.Value.Id;
        }

        // Pass 1: create or update roles.
        foreach (var definition in document.Roles)
        {
            var parentNames = ParentNamesOf(definition);
            Guid? primaryParentId = null;
            var additionalParentIds = new List<Guid>();

            foreach (var parentName in parentNames)
            {
                if (resolvedIdsByName.TryGetValue(parentName, out var parentId))
                {
                    if (primaryParentId is null)
                    {
                        primaryParentId = parentId;
                    }
                    else
                    {
                        additionalParentIds.Add(parentId);
                    }
                }
            }

            if (existingByName.TryGetValue(definition.Name!, out var existing))
            {
                existing.DisplayName = definition.DisplayName ?? existing.DisplayName;
                existing.Description = definition.Description ?? existing.Description;
                existing.Permissions = definition.Permissions;
                existing.DenyPermissions = definition.DenyPermissions;
                existing.BlockedInheritedPermissions = definition.BlockedInheritedPermissions ?? Array.Empty<string>();
                existing.ParentRoleId = primaryParentId;
                existing.AdditionalParentRoleIds = additionalParentIds.ToArray();
                existing.Priority = definition.Priority;
                existing.IsActive = definition.IsActive;
                await roleRepository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var created = new DynamicRole
                {
                    Name = definition.Name!,
                    DisplayName = definition.DisplayName ?? definition.Name!,
                    Description = definition.Description,
                    TenantId = targetTenantId,
                    Permissions = definition.Permissions,
                    DenyPermissions = definition.DenyPermissions,
                    BlockedInheritedPermissions = definition.BlockedInheritedPermissions ?? Array.Empty<string>(),
                    ParentRoleId = primaryParentId,
                    AdditionalParentRoleIds = additionalParentIds.ToArray(),
                    Priority = definition.Priority,
                    IsActive = definition.IsActive
                };
                var result = await roleRepository.CreateAsync(created, cancellationToken).ConfigureAwait(false);
                resolvedIdsByName[definition.Name!] = result.Id;
            }

            await auditService.LogPermissionChangeAsync(
                PermissionOperationType.SyncImport,
                null,
                performedBy,
                targetTenantId,
                permissionType: "Role",
                resourceType: "DynamicRole",
                oldValue: null,
                newValue: $"role:{definition.Name}",
                reason: "External system permission synchronization (issue #358)",
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        // Pass 2: permission entries through the guarded, versioned, audited grant service.
        foreach (var entry in document.Permissions)
        {
            var existingRow = targetTenantId.HasValue
                ? await permissionRepository.GetByUserAndTenantAsync(entry.UserId, targetTenantId, cancellationToken).ConfigureAwait(false)
                : await permissionRepository.GetByUserAndTenantAsync(entry.UserId, null, cancellationToken).ConfigureAwait(false);
            var existingAllows = existingRow?.Permissions ?? Array.Empty<string>();
            var existingDenies = existingRow?.DenyPermissions ?? Array.Empty<string>();

            var toGrant = entry.Permissions.Except(existingAllows, StringComparer.OrdinalIgnoreCase).ToArray();
            var toRevoke = existingAllows.Except(entry.Permissions, StringComparer.OrdinalIgnoreCase).ToArray();
            var toDeny = entry.DenyPermissions.Except(existingDenies, StringComparer.OrdinalIgnoreCase).ToArray();
            var toUnDeny = existingDenies.Except(entry.DenyPermissions, StringComparer.OrdinalIgnoreCase).ToArray();

            if (toGrant.Length > 0)
            {
                await grantService.GrantTenantPermissionAsync(
                    entry.UserId,
                    targetTenantId,
                    toGrant,
                    performedBy,
                    reason: "External system permission synchronization (issue #358)",
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            if (toRevoke.Length > 0)
            {
                await grantService.RevokeTenantPermissionAsync(entry.UserId, targetTenantId, toRevoke, cancellationToken).ConfigureAwait(false);
            }

            if (toDeny.Length > 0)
            {
                await grantService.DenyTenantPermissionAsync(
                    entry.UserId,
                    targetTenantId,
                    toDeny,
                    performedBy,
                    reason: "External system permission synchronization (issue #358)",
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            if (toUnDeny.Length > 0)
            {
                await grantService.RemoveDenyPermissionsAsync(entry.UserId, targetTenantId, toUnDeny, cancellationToken).ConfigureAwait(false);
            }
        }

        // Notify webhook subscribers about the synchronization.
        var change = new PermissionChangeEvent(
            PermissionChangeEventType.Synced,
            targetTenantId,
            null,
            "Sync",
            changes.Select(item => item.Kind).Distinct().ToArray(),
            performedBy);
        foreach (var notifier in changeNotifiers)
        {
            try
            {
                await notifier.NotifyAsync(change, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Permission change notifier {NotifierType} failed during a sync import; the import is unaffected.",
                    notifier.GetType().Name);
            }
        }
    }

    private static IEnumerable<(string name, IReadOnlyList<string> parentNames)> documentRolesWithParents(ExternalPermissionSyncDocument document)
    {
        foreach (var role in document.Roles)
        {
            if (string.IsNullOrWhiteSpace(role.Name))
            {
                continue;
            }

            yield return (role.Name!, ParentNamesOf(role));
        }
    }

    private static List<string> ParentNamesOf(ExternalRoleDefinition role)
    {
        var parents = new List<string>();
        if (!string.IsNullOrWhiteSpace(role.ParentRoleName))
        {
            parents.Add(role.ParentRoleName!);
        }

        if (role.AdditionalParentRoleNames is not null)
        {
            parents.AddRange(role.AdditionalParentRoleNames.Where(name => !string.IsNullOrWhiteSpace(name))!);
        }

        return parents;
    }

    private static List<Guid> ResolveParentIds(IEnumerable<string> parentNames, Dictionary<string, DynamicRole> existingByName)
    {
        var ids = new List<Guid>();
        foreach (var name in parentNames)
        {
            if (existingByName.TryGetValue(name, out var role))
            {
                ids.Add(role.Id);
            }
        }

        return ids;
    }
}
