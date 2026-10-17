namespace GameGuild.Identity.Authorization;

/// <summary>
///     Result of a permission template mutation (create/update, issue #369).
/// </summary>
public sealed record PermissionTemplateMutationResult
{
    /// <summary>The template id.</summary>
    public Guid TemplateId { get; init; }

    /// <summary>The template name.</summary>
    public required string TemplateName { get; init; }

    /// <summary>Whether the template was created (as opposed to updated).</summary>
    public bool Created { get; init; }

    /// <summary>The template version number after the mutation.</summary>
    public int Version { get; init; }

    /// <summary>The template's raw permissions after the mutation.</summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];

    /// <summary>The template's role keys after the mutation (role templates).</summary>
    public IReadOnlyList<string> RoleKeys { get; init; } = [];

    /// <summary>Whether the template is active.</summary>
    public bool IsActive { get; init; }

    /// <summary>Whether the template is a system template.</summary>
    public bool IsSystemTemplate { get; init; }

    internal static PermissionTemplateMutationResult FromTemplate(PermissionTemplate template, int version, bool created) => new()
    {
        TemplateId = template.Id,
        TemplateName = template.Name,
        Created = created,
        Version = version,
        Permissions = template.Permissions,
        RoleKeys = template.RoleKeys,
        IsActive = template.IsActive,
        IsSystemTemplate = template.IsSystemTemplate
    };
}

/// <summary>
///     Per-target entry of a template instantiation result / dry-run preview.
/// </summary>
public sealed record PermissionTemplateInstantiationEntryResult
{
    /// <summary>The target user.</summary>
    public Guid UserId { get; init; }

    /// <summary>Permissions that would be (or were) granted from the template.</summary>
    public IReadOnlyList<string> PermissionsToGrant { get; init; } = [];

    /// <summary>Dynamic roles that would be (or were) assigned from the template.</summary>
    public IReadOnlyList<string> RolesToAssign { get; init; } = [];
}

/// <summary>
///     Result of instantiating a permission template into a tenant (or its dry-run
///     preview), issue #369.
/// </summary>
public sealed record PermissionTemplateInstantiationResult
{
    /// <summary>The instantiated template id.</summary>
    public Guid TemplateId { get; init; }

    /// <summary>The instantiated template name.</summary>
    public required string TemplateName { get; init; }

    /// <summary>The tenant scope (null in the result when it came from the actor context).</summary>
    public Guid? TenantId { get; init; }

    /// <summary>Whether the instantiation was applied (false for dry runs).</summary>
    public bool Applied { get; init; }

    /// <summary>The template version that was (or would be) applied.</summary>
    public int TemplateVersion { get; init; }

    /// <summary>Total targets considered.</summary>
    public int TotalTargets { get; init; }

    /// <summary>Targets with at least one change.</summary>
    public int AppliedTargets { get; init; }

    /// <summary>Targets already fully covered (no change needed).</summary>
    public int SkippedTargets { get; init; }

    /// <summary>Total permission grants in the plan.</summary>
    public int PermissionGrants { get; init; }

    /// <summary>Total role assignments in the plan.</summary>
    public int RoleAssignments { get; init; }

    /// <summary>Per-target plan detail.</summary>
    public IReadOnlyList<PermissionTemplateInstantiationEntryResult> Entries { get; init; } = [];
}

/// <summary>
///     Result of a permission template migration (or dry-run preview) / rollback,
///     issue #370.
/// </summary>
public sealed record PermissionTemplateMigrationResult
{
    /// <summary>The migration id (durable progress handle).</summary>
    public Guid MigrationId { get; init; }

    /// <summary>The migrated template id.</summary>
    public Guid TemplateId { get; init; }

    /// <summary>The migrated template name.</summary>
    public required string TemplateName { get; init; }

    /// <summary>The lowest source version among the targets.</summary>
    public int FromVersion { get; init; }

    /// <summary>The target version.</summary>
    public int ToVersion { get; init; }

    /// <summary>The migration status after the operation.</summary>
    public MigrationStatus Status { get; init; }

    /// <summary>Whether this was a dry run.</summary>
    public bool DryRun { get; init; }

    /// <summary>Total targets considered.</summary>
    public int TotalCount { get; init; }

    /// <summary>Successfully migrated targets.</summary>
    public int SuccessCount { get; init; }

    /// <summary>Skipped targets (unknown source version or no effective change).</summary>
    public int SkippedCount { get; init; }

    /// <summary>Failed targets.</summary>
    public int FailureCount { get; init; }

    /// <summary>Migration progress percentage (0 for dry runs).</summary>
    public double ProgressPercentage { get; init; }

    /// <summary>How many distinct tenants are affected.</summary>
    public int AffectedTenantCount { get; init; }

    /// <summary>Permissions the migration adds across targets.</summary>
    public IReadOnlyList<string> AddedPermissions { get; init; } = [];

    /// <summary>Permissions the migration removes across targets.</summary>
    public IReadOnlyList<string> RemovedPermissions { get; init; } = [];
}
