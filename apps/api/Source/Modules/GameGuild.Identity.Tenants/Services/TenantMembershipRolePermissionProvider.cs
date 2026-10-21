using GameGuild.Identity.Authorization;

namespace GameGuild.Identity.Tenants;

/// <summary>
/// Projects administrative tenant membership roles into the dynamic permission pipeline.
/// </summary>
/// <remarks>
/// The membership lookup is scoped to the requested tenant and inactive or deleted
/// memberships never contribute permissions.
/// </remarks>
public sealed class TenantMembershipRolePermissionProvider(
    ITenantMemberRepository memberRepository) : IAuthorizationRolePermissionProvider
{
    public async Task<IReadOnlyCollection<string>> GetPermissionsAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var membership = await memberRepository
            .GetByUserAndTenantAsync(userId, tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (membership is null ||
            !membership.IsActive ||
            membership.DeletedAt is not null ||
            membership.UserId != userId ||
            membership.TenantId != tenantId ||
            !IsAdministrativeRole(membership.Role))
        {
            return [];
        }

        // Administrative-tier capabilities (issue #294): administrative membership roles
        // carry tenant administration plus member management, tenant settings and the
        // self-service audit-read tier. Role administration (roles:manage) and audit
        // exports (audit:export) stay delegation-only.
        //
        // Query-operations tier (issue #375): administrative roles also carry the
        // list/search/query operation keys so audit-query, security-event and
        // permission-query surfaces keep working; non-administrative members receive
        // nothing implicitly (fail-closed, explicit grants only).
        return
        [
            AdminPermission.Keys.TenantAdmin,
            ManageMembersPermission.Keys.Manage,
            ManageSettingsPermission.Keys.Read,
            ManageSettingsPermission.Keys.Manage,
            AuditPermission.Keys.Read,
            QueryPermission.Keys.List,
            QueryPermission.Keys.Search,
            QueryPermission.Keys.Query
        ];
    }

    private static bool IsAdministrativeRole(string? role) =>
        string.Equals(role, TenantRole.Owner.Value, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, TenantRole.Admin.Value, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, "TenantAdmin", StringComparison.OrdinalIgnoreCase);
}
