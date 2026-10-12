namespace GameGuild.Compliance.Audit;

/// <summary>
///     Service responsible for querying and collecting audit log entries
///     from multiple sources (authentication, permission, general).
/// </summary>
public interface IAuditLogQueryService
{
    /// <summary>
    ///     Get unified audit logs from all sources with filtering, search, and pagination.
    /// </summary>
    Task<UnifiedSecurityAuditResponse> GetUnifiedAuditLogsAsync(
        UnifiedSecurityAuditRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get authentication-specific audit logs.
    /// </summary>
    Task<AuthenticationAuditResponse> GetAuthenticationLogsAsync(
        AuthenticationAuditRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get permission-specific audit logs.
    /// </summary>
    Task<PermissionAuditResponse> GetPermissionLogsAsync(
        PermissionAuditRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Collect unified audit entries from the underlying stores for a bounded time window,
    ///     optionally restricted to a single source. Used by the audit query DSL executor, which
    ///     applies its own filter tree in memory. Each source contributes at most
    ///     <paramref name="rowLimit" /> rows.
    /// </summary>
    Task<IReadOnlyList<UnifiedSecurityAuditEntry>> CollectUnifiedEntriesAsync(
        DateTime startDateUtc,
        DateTime endDateUtc,
        SecurityAuditSourceType? restrictToSource,
        int rowLimit,
        CancellationToken cancellationToken = default);
}
