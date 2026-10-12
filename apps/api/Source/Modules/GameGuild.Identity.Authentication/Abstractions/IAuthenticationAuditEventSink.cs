namespace GameGuild.Identity.Authentication;

/// <summary>
/// Publishes authentication lifecycle events without coupling the identity module to a
/// particular audit storage implementation.
/// </summary>
public interface IAuthenticationAuditEventSink
{
    Task RecordAsync(AuthenticationAuditEvent auditEvent, CancellationToken cancellationToken);
}

/// <summary>
/// A security-relevant authentication event. Secrets and credential values must never be included.
/// </summary>
public sealed record AuthenticationAuditEvent(
    string ActionType,
    Guid? UserId,
    bool Success,
    string Method,
    string? IpAddress = null,
    string? UserAgent = null,
    Guid? SessionId = null,
    Guid? TenantId = null,
    string? ErrorMessage = null,
    object? Metadata = null,
    RiskLevel? AssessedRiskLevel = null);
