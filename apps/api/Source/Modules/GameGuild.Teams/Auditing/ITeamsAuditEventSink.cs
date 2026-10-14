namespace GameGuild.Teams;

/// <summary>
///     Publishes team mutation events without coupling the teams module to a particular audit
///     storage implementation. The central implementation persists each event in the same
///     transaction as the mutation and fails closed: a mutation whose audit event cannot be
///     persisted must not commit.
/// </summary>
public interface ITeamsAuditEventSink
{
    /// <summary>
    ///     Records one required team mutation event. Implementations must throw when the event
    ///     cannot be persisted durably so the owning command transaction rolls back.
    /// </summary>
    Task RecordAsync(TeamsAuditEvent auditEvent, CancellationToken cancellationToken);
}

/// <summary>
///     A team mutation performed through this module. Resource identifiers and metadata must
///     never contain credentials, invitation tokens, or personal contact details.
/// </summary>
public sealed record TeamsAuditEvent(
    string ActionType,
    string ResourceType,
    string? ResourceId,
    Guid? ActorUserId = null,
    Guid? TenantId = null,
    string? Description = null,
    bool Success = true,
    string? ErrorMessage = null,
    object? Metadata = null);
