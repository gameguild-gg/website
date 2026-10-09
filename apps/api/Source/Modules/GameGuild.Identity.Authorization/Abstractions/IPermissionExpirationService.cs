namespace GameGuild.Identity.Authorization;

/// <summary>
///     Temporal permission lifecycle operations (issue #331).
///     Implements automatic expiration of permission grants, upcoming-expiration
///     notifications, and bulk administrative expiration management.
/// </summary>
/// <remarks>
///     <para>
///         <b>SECURITY - Mutation invariants:</b> every mutation deactivates or re-times
///         grants, increments the tenant security version
///         (<see cref="ITenantSecurityVersionStore"/>) so cached permission data is
///         invalidated, and writes a <see cref="PermissionAuditLog"/> entry.
///     </para>
///     <para>
///         <b>SECURITY - Fail closed:</b> expired grants already contribute nothing to
///         permission evaluation (see <c>EffectivePermissionResolverService</c> and
///         <c>PermissionQueryService</c>); the cleanup performed here is durable
///         revocation plus notification/audit trail, not the enforcement boundary.
///     </para>
/// </remarks>
public interface IPermissionExpirationService
{
    /// <summary>
    ///     Deactivates grants whose <c>ExpiresAt</c> has passed: publishes an
    ///     <see cref="PermissionExpirationNotification"/> with kind
    ///     <see cref="PermissionExpirationKind.Exired"/>, writes an audit entry with
    ///     operation <see cref="PermissionOperationType.Expire"/>, and bumps the tenant
    ///     security version. Idempotent.
    /// </summary>
    /// <returns>The number of grants processed.</returns>
    Task<int> ProcessExpiredAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Publishes <see cref="PermissionExpirationNotification"/> with kind
    ///     <see cref="PermissionExpirationKind.Upcoming"/> for active grants expiring
    ///     within the configured window. Reminders are deduplicated per grant via the
    ///     <c>Metadata["expirationReminderAt"]</c> stamp and
    ///     <c>MinimumReminderInterval</c>.
    /// </summary>
    /// <returns>The number of reminders published.</returns>
    Task<int> SendUpcomingExpirationRemindersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Bulk-sets an absolute expiration for a set of grants within one tenant.
    ///     A <c>null</c> <paramref name="expiresAt"/> clears the expiration (grant
    ///     becomes permanent again). A non-null value must be in the future.
    /// </summary>
    /// <returns>The updated grants (only those found in the tenant).</returns>
    Task<List<TenantPermission>> SetExpirationAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> permissionIds,
        DateTime? expiresAt,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Bulk-extends the expiration of a set of grants within one tenant by a
    ///     positive <paramref name="extension"/> period. Grants without an expiration
    ///     are treated as expiring now. Extending reactivates grants whose new
    ///     expiration is in the future.
    /// </summary>
    /// <returns>The updated grants (only those found in the tenant).</returns>
    Task<List<TenantPermission>> ExtendExpirationAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> permissionIds,
        TimeSpan extension,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets active grants in a tenant that expire no later than
    ///     <paramref name="cutoff"/> (administrative visibility for upcoming
    ///     expirations).
    /// </summary>
    Task<List<TenantPermission>> GetExpiringAsync(
        Guid tenantId,
        DateTime? cutoff = null,
        CancellationToken cancellationToken = default);
}
