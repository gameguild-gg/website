namespace GameGuild.Features;

/// <summary>
/// Service interface for managing tenant capability entitlements.
/// Provides fail-closed capability checks with audit logging.
/// </summary>
public interface ICapabilityService
{
    /// <summary>
    /// Checks if a capability is enabled for a tenant using fail-closed behavior.
    /// 1. Checks explicit tenant overrides (highest priority)
    /// 2. Falls back to subscription plan entitlements
    /// 3. Returns false on any error or missing data (fail-closed)
    /// </summary>
    /// <param name="tenantId">The tenant to check.</param>
    /// <param name="capability">The capability key (e.g., "lxp.discovery").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the capability is enabled; false otherwise (including on error).</returns>
    Task<bool> IsCapabilityEnabledAsync(Guid tenantId, string capability, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all enabled capabilities for a tenant as a dictionary.
    /// Returns the capability key and enabled state.
    /// </summary>
    /// <param name="tenantId">The tenant to query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of capability keys to enabled states.</returns>
    Task<IDictionary<string, bool>> GetTenantCapabilitiesAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets or overrides a capability for a tenant.
    /// Creates an audit log entry for the change.
    /// Requires a resolved tenant or system administrator; audit identity comes from that actor.
    /// </summary>
    /// <param name="tenantId">The tenant to modify.</param>
    /// <param name="capability">The capability key.</param>
    /// <param name="isEnabled">Whether to enable or disable the capability.</param>
    /// <param name="source">The source of this change (e.g., "override:admin").</param>
    /// <param name="userId">Legacy caller identity; ignored in favor of the resolved actor (null for non-user actors).</param>
    /// <param name="reason">The reason for the change.</param>
    /// <param name="expiresAt">Optional expiration for time-limited capabilities.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetCapabilityOverrideAsync(
        Guid tenantId,
        string capability,
        bool isEnabled,
        string source,
        Guid? userId,
        string? reason,
        DateTimeOffset? expiresAt = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a capability override, reverting to subscription plan default.
    /// Requires a resolved tenant or system administrator.
    /// </summary>
    /// <param name="tenantId">The tenant to modify.</param>
    /// <param name="capability">The capability key.</param>
    /// <param name="userId">Legacy caller identity; ignored in favor of the resolved actor.</param>
    /// <param name="reason">The reason for removing the override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveCapabilityOverrideAsync(
        Guid tenantId,
        string capability,
        Guid? userId,
        string? reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Syncs capabilities from the tenant's subscription plan.
    /// Called after subscription changes to update entitlements.
    /// Background callers must establish an authenticated privileged system actor.
    /// Preserves explicit overrides and audits each new or changed plan row with the resolved actor.
    /// </summary>
    /// <param name="tenantId">The tenant to sync.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SyncCapabilitiesFromPlanAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the audit log for a tenant's capability changes.
    /// Requires a resolved tenant or system administrator.
    /// </summary>
    /// <param name="tenantId">The tenant to query.</param>
    /// <param name="capability">Optional capability filter.</param>
    /// <param name="fromDate">Optional start date filter.</param>
    /// <param name="toDate">Optional end date filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of audit log entries.</returns>
    Task<IEnumerable<CapabilityAuditLog>> GetAuditLogAsync(
        Guid tenantId,
        string? capability = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default);
}
