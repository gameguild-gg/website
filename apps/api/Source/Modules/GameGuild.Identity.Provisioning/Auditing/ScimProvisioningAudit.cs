using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Provisioning;

/// <summary>
///     Audit event raised for every SCIM provisioning mutation. The module only defines
/// the contract; hosts register a sink (the compliance audit module provides the
/// central implementation) so provisioning actions leave a durable trail.
/// </summary>
public sealed record ScimProvisioningAuditEvent(
    string Action,
    Guid? TargetUserId,
    Guid? TargetRoleId,
    Guid TenantId,
    string Actor,
    string? ExternalId = null,
    string? Detail = null,
    IReadOnlyDictionary<string, object?>? Metadata = null);

public static class ScimProvisioningAuditActions
{
    public const string UserCreated = "Scim.UserCreated";
    public const string UserReplaced = "Scim.UserReplaced";
    public const string UserPatched = "Scim.UserPatched";
    public const string UserDeprovisioned = "Scim.UserDeprovisioned";
    public const string GroupCreated = "Scim.GroupCreated";
    public const string GroupReplaced = "Scim.GroupReplaced";
    public const string GroupPatched = "Scim.GroupPatched";
    public const string GroupDeleted = "Scim.GroupDeleted";
    public const string MembersChanged = "Scim.GroupMembersChanged";
    public const string TokenIssued = "Scim.TokenIssued";
    public const string TokenRotated = "Scim.TokenRotated";
    public const string TokenRevoked = "Scim.TokenRevoked";
}

public interface IScimProvisioningAuditSink
{
    Task RecordAsync(ScimProvisioningAuditEvent auditEvent, CancellationToken cancellationToken = default);
}

/// <summary>Null-object sink used when no host sink is registered.</summary>
public sealed class NullScimProvisioningAuditSink : IScimProvisioningAuditSink
{
    public static readonly NullScimProvisioningAuditSink Instance = new();

    private NullScimProvisioningAuditSink()
    {
    }

    public Task RecordAsync(ScimProvisioningAuditEvent auditEvent, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

/// <summary>
///     Resilient audit helper: sink failures are logged, never propagated, so auditing
///     cannot take down a provisioning request that already succeeded.
/// </summary>
public static class ScimProvisioningAudit
{
    public static async Task RecordAsync(
        IScimProvisioningAuditSink? sink,
        ScimProvisioningAuditEvent auditEvent,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (sink is null)
        {
            return;
        }

        try
        {
            await sink.RecordAsync(auditEvent, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not record SCIM provisioning audit event {Action}", auditEvent.Action);
        }
    }
}
