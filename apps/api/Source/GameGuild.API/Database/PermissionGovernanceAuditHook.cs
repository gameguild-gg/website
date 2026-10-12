using GameGuild.Identity.Authorization;

namespace GameGuild.API.Database;

/// <summary>
///     Bridges the EF permission-audit capture pipeline into the authorization module's
///     governance audit surface (issues #188/#189): role-hierarchy mutations,
///     dynamic-role assignments and permission-policy mutations are turned into
///     <see cref="PermissionAuditLog"/> entries with cascading-effect visibility through
///     <see cref="IRoleHierarchyAuditService"/>.
/// </summary>
/// <remarks>
///     The hook runs after the permission mutation has committed. Any failure is logged
///     and swallowed by the caller-side contract (audit isolation): it can never fail or
///     roll back the audited mutation.
/// </remarks>
public sealed class PermissionGovernanceAuditHook(
    IRoleHierarchyAuditService roleHierarchyAuditService,
    ILogger<PermissionGovernanceAuditHook> logger) : IPermissionAuditHook
{
    private static readonly HashSet<string> GovernanceEntityTypes =
    [
        "DynamicRole",
        "DynamicRoleAssignment",
        "ConditionalPolicy",
        "PolicyDefinitionEntity"
    ];

    /// <inheritdoc />
    public async Task OnPermissionChangesAuditedAsync(
        PermissionAuditHookContext context,
        CancellationToken cancellationToken = default)
    {
        var governanceChanges = context.Changes
            .Where(change => GovernanceEntityTypes.Contains(change.EntityType))
            .Select(change => new GovernancePermissionChange(
                change.EntityType,
                change.Operation,
                change.ResourceId,
                change.TargetUserId,
                change.TenantId,
                change.ActorId,
                change.CorrelationId,
                change.CommandType,
                change.BeforeState,
                change.AfterState))
            .ToArray();

        if (governanceChanges.Length == 0)
        {
            return;
        }

        try
        {
            await roleHierarchyAuditService
                .WriteGovernanceAuditEntriesAsync(governanceChanges, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Governance audit hook failed for {ChangeCount} captured change(s); the audited permission mutations remain committed",
                governanceChanges.Length);
        }
    }
}
