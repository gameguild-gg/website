using GameGuild.Compliance.Audit;

namespace GameGuild.API.Database;

/// <summary>
/// Configurable filtering for automatic permission change auditing. An empty exclusion set
/// keeps the default coverage broad; entity names may be simple CLR names or full names.
/// </summary>
public sealed class PermissionAuditOptions
{
    public const string SectionName = "Audit:PermissionHooks";

    public HashSet<string> ExcludedEntityTypes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> ExcludedOperations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Permission changes persisted by a unit of work and sent to centralized audit sinks.
/// </summary>
public sealed record PermissionAuditChange(
    string ActionType,
    string Operation,
    string EntityType,
    string? ResourceId,
    Guid? TargetUserId,
    Guid? TenantId,
    Guid ActorId,
    string? CorrelationId,
    string? CommandType,
    string? BeforeState,
    string? AfterState);

/// <summary>
/// Context offered to optional domain-specific audit hooks after the primary audit attempt.
/// </summary>
public sealed record PermissionAuditHookContext(
    CreateAuditLogRequest AuditRequest,
    IReadOnlyList<PermissionAuditChange> Changes);

/// <summary>
/// Extension point for custom handling of persisted permission changes.
/// </summary>
public interface IPermissionAuditHook
{
    Task OnPermissionChangesAuditedAsync(
        PermissionAuditHookContext context,
        CancellationToken cancellationToken = default);
}
