using GameGuild.CQRS;

namespace GameGuild.Identity.Authorization.Queries;

/// <summary>
///     Query to build the permission graph of the caller's tenant scope (issue #334).
///     The tenant is resolved from the request context, never from the route/query string.
/// </summary>
public sealed record GetPermissionGraphQuery(bool IncludeUsers = true) : IQuery<Models.PermissionGraph>;

/// <summary>
///     Handler for <see cref="GetPermissionGraphQuery"/>.
/// </summary>
public sealed class GetPermissionGraphHandler(
    IPermissionGraphService graphService,
    IAuthorizationTenantContext tenantContext) : IQueryHandler<GetPermissionGraphQuery, Models.PermissionGraph>
{
    /// <inheritdoc />
    public Task<Models.PermissionGraph> Handle(GetPermissionGraphQuery request, CancellationToken cancellationToken)
        => graphService.BuildGraphAsync(tenantContext.TenantId, request.IncludeUsers, cancellationToken);
}

/// <summary>
///     Query to simulate the deletion of a dynamic role and report its impact (issue #334).
/// </summary>
public sealed record AnalyzeRoleDeletionQuery(Guid RoleId) : IQuery<Models.RoleDeletionImpact>;

/// <summary>
///     Handler for <see cref="AnalyzeRoleDeletionQuery"/>.
/// </summary>
public sealed class AnalyzeRoleDeletionHandler(
    IPermissionImpactAnalysisService impactService,
    IAuthorizationTenantContext tenantContext) : IQueryHandler<AnalyzeRoleDeletionQuery, Models.RoleDeletionImpact>
{
    /// <inheritdoc />
    public Task<Models.RoleDeletionImpact> Handle(AnalyzeRoleDeletionQuery request, CancellationToken cancellationToken)
        => impactService.AnalyzeRoleDeletionAsync(tenantContext.TenantId, request.RoleId, cancellationToken);
}

/// <summary>
///     Query to simulate removing a permission key from a role and report its impact (issue #334).
/// </summary>
public sealed record AnalyzePermissionRemovalQuery(Guid RoleId, string PermissionKey) : IQuery<Models.PermissionRemovalImpact>;

/// <summary>
///     Handler for <see cref="AnalyzePermissionRemovalQuery"/>.
/// </summary>
public sealed class AnalyzePermissionRemovalHandler(
    IPermissionImpactAnalysisService impactService,
    IAuthorizationTenantContext tenantContext) : IQueryHandler<AnalyzePermissionRemovalQuery, Models.PermissionRemovalImpact>
{
    /// <inheritdoc />
    public Task<Models.PermissionRemovalImpact> Handle(AnalyzePermissionRemovalQuery request, CancellationToken cancellationToken)
        => impactService.AnalyzePermissionRemovalAsync(tenantContext.TenantId, request.RoleId, request.PermissionKey, cancellationToken);
}
