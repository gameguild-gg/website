namespace GameGuild.Compliance.Audit;

/// <summary>
/// Service for creating and managing audit logs
/// </summary>
public interface IAuditService
{
    Task LogAsync(CreateAuditLogRequest request);

    /// <summary>
    /// Attempts to persist one audit log entry and reports whether the write was durable.
    /// Unlike <see cref="LogAsync"/>, the outcome is observable so callers that must not
    /// swallow audit delivery failures (for example permission evaluation logging) can
    /// surface them. Like <see cref="LogAsync"/>, this method never throws for
    /// persistence failures.
    /// </summary>
    /// <param name="request">The audit log entry to persist.</param>
    /// <returns><see langword="true"/> when the entry was persisted; <see langword="false"/> when the write failed.</returns>
    Task<bool> TryLogAsync(CreateAuditLogRequest request);

    Task LogPermissionGrantAsync(Guid userId, string permissionName, string resourceType, string? resourceId, Guid? tenantId = null);

    Task LogPermissionDenyAsync(Guid? userId, string permissionName, string resourceType, string? resourceId, string reason, Guid? tenantId = null);

    Task LogAuthenticationAsync(string actionType, Guid? userId, bool success, string? errorMessage = null);

    Task LogAdminActionAsync(Guid userId, string actionType, string description, object? metadata = null);

    Task LogSecurityViolationAsync(string violationType, string description, Guid? userId = null, object? metadata = null);

    Task<List<AuditLog>> GetAuditLogsAsync(AuditLogQuery query);

    IAsyncEnumerable<AuditLog> StreamAuditLogsAsync(AuditLogQuery query, CancellationToken cancellationToken);

    Task<int> GetAuditLogCountAsync(AuditLogQuery query);

    Task<List<AuditActivityBucket>> GetAuditActivityAsync(
        AuditLogQuery query,
        AuditActivityBucketSize bucketSize,
        CancellationToken cancellationToken = default);

    // Tenant-specific audit methods
    Task LogTenantOperationAsync(string actionType, Guid tenantId, Guid? userId = null, string? description = null, object? metadata = null, bool success = true);

    Task LogTenantIsolationBypassAsync(Guid userId, string reason, object? metadata = null);

    // Privacy audit methods
    Task LogPrivacyOperationAsync(string actionType, Guid userId, string? settingName = null, string? oldValue = null, string? newValue = null, Guid? tenantId = null, object? metadata = null);

    Task LogPrivacyViolationAsync(Guid? requestingUserId, Guid targetUserId, string attemptedField, string reason, Guid? tenantId = null);

    // Username normalization audit methods
    Task LogUsernameOperationAsync(string actionType, Guid userId, string? oldUsername = null, string? newUsername = null, string? reason = null, object? metadata = null);
}
