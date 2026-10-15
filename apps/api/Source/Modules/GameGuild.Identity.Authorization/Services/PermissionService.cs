using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Facade permission service that delegates to specialized services.
///     <b>DEPRECATED:</b> Prefer injecting <see cref="IPermissionGrantService"/>,
///     <see cref="IPermissionQueryService"/>, or <see cref="IPermissionBulkService"/> directly.
/// </summary>
/// <remarks>
///     <para>
///         This service now acts as a facade that delegates to:
///         <list type="bullet">
///             <item><see cref="IPermissionGrantService"/> - Grant/revoke mutations</item>
///             <item><see cref="IPermissionQueryService"/> - Permission checks and queries</item>
///             <item><see cref="IPermissionBulkService"/> - Bulk operations and tenant membership</item>
///         </list>
///     </para>
///     <para>
///         This facade is maintained for backward compatibility. New code should
///         inject the specific interface needed.
///     </para>
/// </remarks>
[Obsolete("Prefer IPermissionGrantService, IPermissionQueryService, or IPermissionBulkService for new code.")]
public class PermissionService(
    IPermissionGrantService grantService,
    IPermissionQueryService queryService,
    IPermissionBulkService bulkService,
    ILogger<PermissionService> logger
) : IPermissionService
{
    private readonly IPermissionGrantService _grantService = grantService;
    private readonly IPermissionQueryService _queryService = queryService;
    private readonly IPermissionBulkService _bulkService = bulkService;
    private readonly ILogger<PermissionService> _logger = logger;

    public Task<TenantPermission> GrantTenantPermissionAsync(
        Guid? userId,
        Guid? tenantId,
        string[] permissions,
        Guid? grantedBy = null,
        DateTime? expiresAt = null,
        string? reason = null,
        CancellationToken cancellationToken = default
    ) => _grantService.GrantTenantPermissionAsync(userId, tenantId, permissions, grantedBy, expiresAt, reason, cancellationToken);

    public Task<List<TenantPermission>> BulkGrantTenantPermissionAsync(
        Guid[] userIds,
        Guid tenantId,
        string[] permissions,
        Guid? grantedBy = null,
        DateTime? expiresAt = null,
        string? reason = null,
        CancellationToken cancellationToken = default
    ) => _bulkService.BulkGrantTenantPermissionAsync(userIds, tenantId, permissions, grantedBy, expiresAt, reason, cancellationToken);

    public Task<bool> RevokeTenantPermissionAsync(
        Guid? userId,
        Guid? tenantId,
        string[] permissions,
        CancellationToken cancellationToken = default
    ) => _grantService.RevokeTenantPermissionAsync(userId, tenantId, permissions, cancellationToken);

    public Task<bool> HasTenantPermissionAsync(
        Guid? userId,
        Guid? tenantId,
        string permission,
        CancellationToken cancellationToken = default
    ) => _queryService.HasTenantPermissionAsync(userId, tenantId, permission, cancellationToken);

    public Task<List<string>> GetTenantPermissionsAsync(
        Guid? userId,
        Guid? tenantId,
        CancellationToken cancellationToken = default
    ) => _queryService.GetTenantPermissionsAsync(userId, tenantId, cancellationToken);

    public Task<List<string>> GetEffectivePermissionsAsync(
        Guid userId,
        Guid? tenantId,
        CancellationToken cancellationToken = default
    ) => _queryService.GetEffectivePermissionsAsync(userId, tenantId, cancellationToken);

    public Task<TenantPermission> JoinTenantAsync(
        Guid userId,
        Guid tenantId,
        Guid? invitedBy = null,
        CancellationToken cancellationToken = default
    ) => _bulkService.JoinTenantAsync(userId, tenantId, invitedBy, cancellationToken);

    public Task<bool> LeaveTenantAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default
    ) => _bulkService.LeaveTenantAsync(userId, tenantId, cancellationToken);

    public Task<bool> IsUserInTenantAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default
    ) => _queryService.IsUserInTenantAsync(userId, tenantId, cancellationToken);

    public Task<List<string>> GetGlobalDefaultPermissionsAsync(
        CancellationToken cancellationToken = default
    ) => _queryService.GetGlobalDefaultPermissionsAsync(cancellationToken);

    public Task SetGlobalDefaultPermissionsAsync(
        string[] permissions,
        Guid? setBy = null,
        CancellationToken cancellationToken = default
    ) => _grantService.SetGlobalDefaultPermissionsAsync(permissions, setBy, cancellationToken);

    public Task<List<string>> GetTenantDefaultPermissionsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default
    ) => _queryService.GetTenantDefaultPermissionsAsync(tenantId, cancellationToken);

    public Task SetTenantDefaultPermissionsAsync(
        Guid tenantId,
        string[] permissions,
        Guid? setBy = null,
        CancellationToken cancellationToken = default
    ) => _grantService.SetTenantDefaultPermissionsAsync(tenantId, permissions, setBy, cancellationToken);
}
