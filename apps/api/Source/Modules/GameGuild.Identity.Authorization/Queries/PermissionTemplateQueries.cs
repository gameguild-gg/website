using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Lists permission templates with their role keys and current version number
///     (issue #369 template CRUD lifecycle).
/// </summary>
public sealed record ListPermissionTemplatesQuery : IQuery<IReadOnlyList<PermissionTemplateSummary>>
{
    /// <summary>Include inactive templates.</summary>
    public bool IncludeInactive { get; init; }

    /// <summary>Optional category filter.</summary>
    public string? Category { get; init; }
}

/// <summary>
///     Handler for <see cref="ListPermissionTemplatesQuery"/>.
/// </summary>
public sealed class ListPermissionTemplatesQueryHandler(
    IApplicationDbContext dbContext,
    IActorContextAccessor actorContextAccessor)
    : IQueryHandler<ListPermissionTemplatesQuery, IReadOnlyList<PermissionTemplateSummary>>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<IReadOnlyList<PermissionTemplateSummary>> Handle(
        ListPermissionTemplatesQuery request,
        CancellationToken cancellationToken)
    {
        // Read surface is admin-shaped: templates enumerate grantable permission sets.
        if (!Actor.IsAuthenticated || (!Actor.IsSystemAdmin && !Actor.IsTenantAdmin))
        {
            throw new UnauthorizedAccessException(
                "Listing permission templates requires tenant administration or system administration");
        }

        var query = dbContext.Set<PermissionTemplate>().AsNoTracking();
        if (!request.IncludeInactive)
        {
            query = query.Where(t => t.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            query = query.Where(t => t.Category == request.Category);
        }

        var templates = await query
            .OrderBy(t => t.Category)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var templateIds = templates.Select(t => t.Id).ToList();
        var latestVersions = await dbContext.Set<PermissionTemplateVersion>()
            .AsNoTracking()
            .Where(v => templateIds.Contains(v.TemplateId))
            .GroupBy(v => v.TemplateId)
            .Select(g => new { TemplateId = g.Key, Version = g.Max(v => v.VersionNumber) })
            .ToDictionaryAsync(x => x.TemplateId, x => x.Version, cancellationToken).ConfigureAwait(false);

        return templates
            .Select(t => new PermissionTemplateSummary
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                Permissions = t.Permissions,
                RoleKeys = t.RoleKeys,
                IsRoleTemplate = t.RoleKeys.Length > 0,
                IsSystemTemplate = t.IsSystemTemplate,
                IsActive = t.IsActive,
                Category = t.Category,
                MinimumTier = t.MinimumTier,
                CurrentVersion = latestVersions.GetValueOrDefault(t.Id, 1)
            })
            .ToList();
    }
}

/// <summary>
///     Lists the append-only version history of a permission template (issue #370).
/// </summary>
public sealed record GetPermissionTemplateVersionsQuery : IQuery<IReadOnlyList<PermissionTemplateVersionSummary>>
{
    /// <summary>The template id.</summary>
    public required Guid TemplateId { get; init; }
}

/// <summary>
///     Handler for <see cref="GetPermissionTemplateVersionsQuery"/>.
/// </summary>
public sealed class GetPermissionTemplateVersionsQueryHandler(
    IApplicationDbContext dbContext,
    IActorContextAccessor actorContextAccessor)
    : IQueryHandler<GetPermissionTemplateVersionsQuery, IReadOnlyList<PermissionTemplateVersionSummary>>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<IReadOnlyList<PermissionTemplateVersionSummary>> Handle(
        GetPermissionTemplateVersionsQuery request,
        CancellationToken cancellationToken)
    {
        if (!Actor.IsAuthenticated || (!Actor.IsSystemAdmin && !Actor.IsTenantAdmin))
        {
            throw new UnauthorizedAccessException(
                "Reading permission template versions requires tenant administration or system administration");
        }

        var versions = await dbContext.Set<PermissionTemplateVersion>()
            .AsNoTracking()
            .Where(v => v.TemplateId == request.TemplateId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return versions
            .Select(v => new PermissionTemplateVersionSummary
            {
                VersionNumber = v.VersionNumber,
                Name = v.Name,
                Permissions = v.Permissions,
                ChangeType = v.ChangeType,
                ChangeNotes = v.ChangeNotes,
                AddedPermissions = v.AddedPermissions ?? [],
                RemovedPermissions = v.RemovedPermissions ?? [],
                UnchangedPermissions = v.UnchangedPermissions ?? [],
                PermissionHash = v.PermissionHash,
                CreatedByUserId = v.CreatedByUserId,
                CreatedAt = v.CreatedAt
            })
            .ToList();
    }
}

/// <summary>
///     Dry-run preview of instantiating a template into a tenant (issue #369): what
///     permissions would be granted and which roles assigned, per target user, without
///     mutating anything.
/// </summary>
public sealed record PreviewPermissionTemplateInstantiationQuery : IQuery<PermissionTemplateInstantiationResult>
{
    /// <summary>The template to preview.</summary>
    public required Guid TemplateId { get; init; }

    /// <summary>The tenant that would receive the template. Defaults to the acting tenant.</summary>
    public Guid? TenantId { get; init; }

    /// <summary>The target users.</summary>
    public required Guid[] TargetUserIds { get; init; }
}

/// <summary>
///     Handler for <see cref="PreviewPermissionTemplateInstantiationQuery"/>: runs the
///     instantiation command with <c>DryRun = true</c> (no mutation, no audit).
/// </summary>
public sealed class PreviewPermissionTemplateInstantiationQueryHandler(
    InstantiatePermissionTemplateCommandHandler instantiateHandler)
    : IQueryHandler<PreviewPermissionTemplateInstantiationQuery, PermissionTemplateInstantiationResult>
{
    public async Task<PermissionTemplateInstantiationResult> Handle(
        PreviewPermissionTemplateInstantiationQuery request,
        CancellationToken cancellationToken)
    {
        var command = new InstantiatePermissionTemplateCommand
        {
            TemplateId = request.TemplateId,
            TenantId = request.TenantId,
            TargetUserIds = request.TargetUserIds,
            DryRun = true
        };

        return await instantiateHandler.Handle(command, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
///     Reads the durable progress of one template migration (issue #370).
/// </summary>
public sealed record GetPermissionTemplateMigrationQuery : IQuery<PermissionTemplateMigrationResult>
{
    /// <summary>The migration id.</summary>
    public required Guid MigrationId { get; init; }
}

/// <summary>
///     Handler for <see cref="GetPermissionTemplateMigrationQuery"/>.
/// </summary>
public sealed class GetPermissionTemplateMigrationQueryHandler(
    IApplicationDbContext dbContext,
    IActorContextAccessor actorContextAccessor)
    : IQueryHandler<GetPermissionTemplateMigrationQuery, PermissionTemplateMigrationResult>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<PermissionTemplateMigrationResult> Handle(
        GetPermissionTemplateMigrationQuery request,
        CancellationToken cancellationToken)
    {
        if (!Actor.IsAuthenticated || !Actor.IsSystemAdmin)
        {
            throw new UnauthorizedAccessException(
                "Reading permission template migrations requires system administrator privileges");
        }

        var migration = await dbContext.Set<PermissionTemplateMigration>()
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == request.MigrationId, cancellationToken).ConfigureAwait(false);
        if (migration is null)
        {
            throw new InvalidOperationException($"Template migration {request.MigrationId} not found");
        }

        var templateName = await dbContext.Set<PermissionTemplate>()
            .AsNoTracking()
            .Where(t => t.Id == migration.TemplateId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            ?? $"<deleted:{migration.TemplateId}>";

        return new PermissionTemplateMigrationResult
        {
            MigrationId = migration.Id,
            TemplateId = migration.TemplateId,
            TemplateName = templateName,
            FromVersion = migration.FromVersion,
            ToVersion = migration.ToVersion,
            Status = migration.Status,
            DryRun = migration.IsDryRun,
            TotalCount = migration.TotalCount,
            SuccessCount = migration.SuccessCount,
            SkippedCount = migration.SkippedCount,
            FailureCount = migration.FailureCount,
            ProgressPercentage = migration.GetProgressPercentage(),
            AffectedTenantCount = migration.AffectedTenantIds?.Length ?? 0
        };
    }
}

/// <summary>
///     Read model for one permission template (issue #369).
/// </summary>
public sealed record PermissionTemplateSummary
{
    /// <summary>The template id.</summary>
    public Guid Id { get; init; }

    /// <summary>The unique template name.</summary>
    public required string Name { get; init; }

    /// <summary>The template description.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Raw permissions the template grants.</summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];

    /// <summary>Dynamic role names the template grants (role templates).</summary>
    public IReadOnlyList<string> RoleKeys { get; init; } = [];

    /// <summary>True when the template grants at least one role (role template).</summary>
    public bool IsRoleTemplate { get; init; }

    /// <summary>Whether this is an immutable system template.</summary>
    public bool IsSystemTemplate { get; init; }

    /// <summary>Whether the template is active.</summary>
    public bool IsActive { get; init; }

    /// <summary>Template category.</summary>
    public string? Category { get; init; }

    /// <summary>Minimum tenant tier required to use the template.</summary>
    public string? MinimumTier { get; init; }

    /// <summary>Latest version number from the append-only history.</summary>
    public int CurrentVersion { get; init; }
}

/// <summary>
///     Read model for one permission template version snapshot (issue #370).
/// </summary>
public sealed record PermissionTemplateVersionSummary
{
    /// <summary>The version number.</summary>
    public int VersionNumber { get; init; }

    /// <summary>The template name at this version.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The full permission set at this version.</summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];

    /// <summary>The semantic change type.</summary>
    public TemplateChangeType ChangeType { get; init; }

    /// <summary>The change note.</summary>
    public string? ChangeNotes { get; init; }

    /// <summary>Permissions added relative to the previous version.</summary>
    public IReadOnlyList<string> AddedPermissions { get; init; } = [];

    /// <summary>Permissions removed relative to the previous version.</summary>
    public IReadOnlyList<string> RemovedPermissions { get; init; } = [];

    /// <summary>Permissions unchanged relative to the previous version.</summary>
    public IReadOnlyList<string> UnchangedPermissions { get; init; } = [];

    /// <summary>Checksum of the permission set.</summary>
    public string? PermissionHash { get; init; }

    /// <summary>Who created the version.</summary>
    public Guid CreatedByUserId { get; init; }

    /// <summary>When the version was created.</summary>
    public DateTime CreatedAt { get; init; }
}
