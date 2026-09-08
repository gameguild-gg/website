using GameGuild.Identity.Tenants;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Resolves the tenant-scoped roles emitted into an access token. SystemAdmin is
///     the only platform-global role and is trusted only from the default tenant.
/// </summary>
internal static class TenantAccessContextResolver
{
    private const string SystemAdminRole = "SystemAdmin";
    private const string UserRole = "User";

    public static TenantAccessContext Resolve(GetUserMembershipsResponse memberships, Guid? requestedTenantId)
    {
        var activeMemberships = memberships.Memberships
            .Where(membership => membership.IsActive)
            .ToList();
        if (activeMemberships.Count == 0)
        {
            return new TenantAccessContext(null, null, [UserRole]);
        }

        var availableMemberships = activeMemberships
            .GroupBy(membership => membership.TenantId)
            .Select(group => group.First())
            .ToList();
        var availableTenants = availableMemberships
            .Select(membership => new global::GameGuild.TenantInfo(
                membership.TenantId,
                membership.TenantName,
                membership.TenantSlug,
                membership.TenantIsActive))
            .ToList();

        var defaultMembership = availableMemberships.FirstOrDefault(membership => membership.TenantIsDefault);
        Guid? selectedTenantId;
        if (requestedTenantId.HasValue)
        {
            selectedTenantId = availableTenants
                .FirstOrDefault(tenant => tenant.Id == requestedTenantId.Value && tenant.IsActive)
                ?.Id;
        }
        else
        {
            selectedTenantId = defaultMembership is { TenantIsActive: true }
                ? defaultMembership.TenantId
                : null;
            selectedTenantId ??= availableTenants.FirstOrDefault(tenant => tenant.IsActive)?.Id;
        }

        if (!selectedTenantId.HasValue)
        {
            return new TenantAccessContext(null, availableTenants, [UserRole]);
        }

        var roles = activeMemberships
            .Where(membership => membership.TenantId == selectedTenantId)
            .Where(membership => !IsSystemAdminRole(membership.Role) || membership.TenantIsDefault)
            .Select(membership => membership.Role)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .ToList();

        var isSystemAdmin = activeMemberships.Any(membership =>
            membership.TenantIsDefault &&
            membership.TenantIsActive &&
            IsSystemAdminRole(membership.Role));
        if (isSystemAdmin)
        {
            roles.Add(SystemAdminRole);
        }

        roles.Add(UserRole);
        return new TenantAccessContext(
            selectedTenantId,
            availableTenants,
            roles.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static bool IsSystemAdminRole(string? role) =>
        string.Equals(role, SystemAdminRole, StringComparison.OrdinalIgnoreCase);
}

internal sealed record TenantAccessContext(
    Guid? TenantId,
    IReadOnlyList<global::GameGuild.TenantInfo>? AvailableTenants,
    IReadOnlyList<string> Roles);
