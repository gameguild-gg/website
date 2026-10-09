namespace GameGuild.Identity.Authentication;

/// <summary>
/// Publishes API-key lifecycle events (create/rotate/revoke) without coupling the identity
/// module to a particular audit storage implementation. The compliance module bridges these
/// events to the central audit log.
/// </summary>
public interface IApiKeyAuditEventSink
{
    Task RecordAsync(ApiKeyAuditEvent auditEvent, CancellationToken cancellationToken);
}

/// <summary>
/// A security-relevant API-key lifecycle event. Secrets and key material must never be included;
/// keys are identified by id only.
/// </summary>
public sealed record ApiKeyAuditEvent(
    string ActionType,
    Guid ApiKeyId,
    Guid? UserId = null,
    Guid? TenantId = null,
    bool Success = true,
    string? Description = null,
    object? Metadata = null,
    string? ErrorMessage = null);

/// <summary>
/// Well-known <see cref="ApiKeyAuditEvent.ActionType"/> values.
/// </summary>
public static class ApiKeyAuditActions
{
    public const string Created = "ApiKey.Created";
    public const string Rotated = "ApiKey.Rotated";
    public const string Revoked = "ApiKey.Revoked";
}
