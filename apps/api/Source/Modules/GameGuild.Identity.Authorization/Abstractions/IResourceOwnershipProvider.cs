namespace GameGuild.Identity.Authorization;

/// <summary>
///     A resolved resource-ownership designation (issue #372). Minimal v1 ownership is
///     recorded on resource creation as the <see cref="ResourceUserPermission"/> row with
///     <c>IsOwner = true</c> (the creator auto-grant — the entity's
///     <c>CreatedBy => GrantedByUserId</c> mapping), and it moves only through the
///     audited <c>TransferOwnershipCommand</c>.
/// </summary>
/// <param name="OwnerUserId">The designated owner (creator by default, transfer target after a transfer).</param>
/// <param name="TenantId">Tenant scope of the ownership row.</param>
/// <param name="ResourceType">Resource type of the owned resource.</param>
/// <param name="ResourceId">Resource id of the owned resource.</param>
/// <param name="OwnerPermissions">The permission set recorded on the owner row.</param>
/// <param name="ExpiresAt">Optional expiry of the ownership designation (temporal precedence, issue #363).</param>
/// <param name="OwnerGrantId">The id of the underlying owner grant row (audit/restore reference).</param>
public sealed record ResourceOwnership(
    Guid OwnerUserId,
    Guid TenantId,
    string ResourceType,
    string ResourceId,
    IReadOnlyList<string> OwnerPermissions,
    DateTime? ExpiresAt,
    Guid OwnerGrantId);

/// <summary>
///     Read-side ownership lookup for the effective-permission resolution engine
///     (issue #372). Implementations must be deterministic and tenant-isolated: an
///     ownership record only ever matches the exact (tenant, user, resource type,
///     resource id) tuple it was recorded for.
/// </summary>
public interface IResourceOwnershipProvider
{
    /// <summary>
    ///     Returns the unrevoked ownership designation for the exact (tenant, user,
    ///     resource) tuple, or null when this user does not own this resource. An
    ///     <see cref="ResourceOwnership.ExpiresAt"/> in the past is still returned (the
    ///     caller enforces the temporal rule — an expired time-bound ownership must fail
    ///     closed); a revoked row is never returned.
    /// </summary>
    Task<ResourceOwnership?> GetOwnerAsync(
        Guid tenantId,
        Guid userId,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default);
}
