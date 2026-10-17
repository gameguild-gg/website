namespace GameGuild.Content.Pages;

/// <summary>
///     Publishes content-page mutation events without coupling this module to a particular audit
///     storage implementation. The central implementation persists each event in the same
///     transaction as the mutation and fails closed: a mutation whose audit event cannot be
///     persisted must not commit.
/// </summary>
public interface IContentPagesAuditEventSink
{
    /// <summary>
    ///     Records one required content mutation event. Implementations must throw when the event
    ///     cannot be persisted durably so the owning command transaction rolls back.
    /// </summary>
    Task RecordAsync(ContentPagesAuditEvent auditEvent, CancellationToken cancellationToken);
}

/// <summary>
///     A content mutation performed through this module. Resource identifiers and metadata must
///     never contain credentials or secrets.
/// </summary>
public sealed record ContentPagesAuditEvent(
    string ActionType,
    string ResourceType,
    string? ResourceId,
    Guid? ActorUserId = null,
    Guid? TenantId = null,
    string? Description = null,
    bool Success = true,
    string? ErrorMessage = null,
    object? Metadata = null);
