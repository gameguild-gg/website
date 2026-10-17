namespace GameGuild.Learning;

/// <summary>
///     Publishes learning-domain mutation events (courses, enrollments, certificates) without
///     coupling the learning modules to a particular audit storage implementation. The central
///     implementation persists each event in the same transaction as the mutation and fails
///     closed: a mutation whose audit event cannot be persisted must not commit.
/// </summary>
public interface ILearningAuditEventSink
{
    /// <summary>
    ///     Records one required learning mutation event. Implementations must throw when the event
    ///     cannot be persisted durably so the owning command transaction rolls back.
    /// </summary>
    Task RecordAsync(LearningAuditEvent auditEvent, CancellationToken cancellationToken);
}

/// <summary>
///     A learning mutation performed through the learning modules. Metadata must never contain
///     credentials, submission bodies, or graded answers.
/// </summary>
public sealed record LearningAuditEvent(
    string ActionType,
    string ResourceType,
    string? ResourceId,
    Guid? ActorUserId = null,
    Guid? TenantId = null,
    string? Description = null,
    bool Success = true,
    string? ErrorMessage = null,
    object? Metadata = null);
