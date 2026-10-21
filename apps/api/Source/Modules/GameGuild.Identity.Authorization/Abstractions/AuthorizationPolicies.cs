namespace GameGuild.Identity.Authorization;

/// <summary>
///     Constants for authorization policy names.
///     Use these constants instead of magic strings to prevent typos and enable refactoring.
/// </summary>
/// <remarks>
///     <para>
///         These policy names correspond to the authorization layers defined in
///         AUTHORIZATION_ARCHITECTURE.md:
///         <list type="bullet">
///             <item>Layer 1: Conditional Access Policies (security/context)</item>
///             <item>Layer 2: ABAC Policies (attribute-based)</item>
///             <item>Layer 3: Direct Permissions (fine-grained)</item>
///             <item>Layer 4: Role-Based Permissions (RBAC)</item>
///         </list>
///     </para>
///     <para>
///         Example usage in attribute:
///         <code>[Authorize(Policy = AuthorizationPolicies.RequireMfa)]</code>
///     </para>
///     <para>
///         Example usage in code:
///         <code>
///         services.AddAuthorizationBuilder()
///             .AddPolicy(AuthorizationPolicies.RequireMfa, policy => 
///                 policy.RequireClaim(AuthorizationClaims.MfaVerified));
///         </code>
///     </para>
/// </remarks>
public static class AuthorizationPolicies
{
    // ========================
    // LAYER 1: CONDITIONAL ACCESS
    // ========================

    /// <summary>
    ///     Policy requiring multi-factor authentication.
    /// </summary>
    public const string RequireMfa = "RequireMfa";

    /// <summary>
    ///     Policy requiring authentication from an allowed IP range.
    /// </summary>
    public const string RequireAllowedIp = "RequireAllowedIp";

    /// <summary>
    ///     Policy requiring access within business hours.
    /// </summary>
    public const string RequireBusinessHours = "RequireBusinessHours";

    /// <summary>
    ///     Policy requiring a trusted device.
    /// </summary>
    public const string RequireTrustedDevice = "RequireTrustedDevice";

    // ========================
    // LAYER 2: ABAC POLICIES
    // ========================

    /// <summary>
    ///     Policy for attribute-based access control evaluation.
    /// </summary>
    public const string AbacEvaluation = "AbacEvaluation";

    /// <summary>
    ///     Policy requiring a specific department.
    /// </summary>
    public const string RequireDepartment = "RequireDepartment";

    /// <summary>
    ///     Policy requiring a specific security clearance level.
    /// </summary>
    public const string RequireClearance = "RequireClearance";

    // ========================
    // LAYER 3: DIRECT PERMISSIONS
    // ========================

    /// <summary>
    ///     Policy for tenant-scoped access.
    /// </summary>
    public const string TenantMember = "TenantMember";

    /// <summary>
    ///     Policy for resource ownership verification.
    /// </summary>
    public const string ResourceOwner = "ResourceOwner";

    /// <summary>
    ///     Policy for checking resource-level ACL.
    /// </summary>
    public const string ResourceAcl = "ResourceAcl";

    // ========================
    // LAYER 4: ROLE-BASED (RBAC)
    // ========================

    /// <summary>
    ///     Policy requiring system administrator role.
    /// </summary>
    public const string SystemAdmin = "SystemAdmin";

    /// <summary>
    ///     Policy requiring tenant administrator role.
    /// </summary>
    public const string TenantAdmin = "TenantAdmin";

    /// <summary>
    ///     Policy requiring moderator role.
    /// </summary>
    public const string Moderator = "Moderator";

    // ========================
    // COMPOSITE POLICIES
    // ========================

    /// <summary>
    ///     Policy requiring both MFA and trusted device.
    /// </summary>
    public const string HighSecurity = "HighSecurity";

    /// <summary>
    ///     Policy for self-service operations (user can only access their own data).
    /// </summary>
    public const string SelfOrAdmin = "SelfOrAdmin";

    // ========================
    // GUEST (ANONYMOUS) ACCESS
    // ========================

    /// <summary>
    ///     Base guest-access policy: admits a well-formed guest principal (own claim/role
    ///     namespace, never a user id) to endpoints explicitly marked for guest access.
    ///     Applied together with the guest authentication scheme — it never weakens the
    ///     default <c>[Authorize]</c> surface, which rejects guest credentials outright.
    /// </summary>
    public const string GuestAccess = "GuestAccess";

    /// <summary>
    ///     Policy name admitting guests holding one specific enumerated guest permission
    ///     (e.g. <see cref="GuestPermission.Keys.BrowseCatalog"/>).
    ///     Permissions outside <c>GuestPermission.Allowlist</c> have no registered policy and
    ///     are denied to guests by construction.
    /// </summary>
    public static string GuestAccessFor(string guestPermissionKey)
    {
        if (string.IsNullOrWhiteSpace(guestPermissionKey))
        {
            throw new ArgumentException("A guest permission key is required.", nameof(guestPermissionKey));
        }

        if (!GuestPermission.Allowlist.Contains(guestPermissionKey))
        {
            throw new ArgumentOutOfRangeException(
                nameof(guestPermissionKey),
                guestPermissionKey,
                "Only enumerated GuestPermission keys have guest-access policies (fail closed).");
        }

        return $"{GuestAccess}:{guestPermissionKey}";
    }
}

/// <summary>
///     Constants for permission scope names.
///     Use these instead of string literals for permission checks.
/// </summary>
/// <remarks>
///     <para>
///         Permission format: &lt;resource&gt;:&lt;action&gt;
///     </para>
///     <para>
///         Example usage:
///         <code>
///         if (await permissionService.HasPermissionAsync(userId, tenantId, PermissionScopes.Users.Read))
///         </code>
///     </para>
/// </remarks>
public static class PermissionScopes
{
    /// <summary>
    ///     User-related permissions.
    /// </summary>
    public static class Users
    {
        public const string Read = "users:read";
        public const string Write = "users:write";
        public const string Delete = "users:delete";
        public const string All = "users:*";
    }

    /// <summary>
    ///     Tenant-related permissions.
    /// </summary>
    public static class Tenants
    {
        public const string Read = "tenants:read";
        public const string Write = "tenants:write";
        public const string Delete = "tenants:delete";
        public const string Manage = "tenants:manage";
        public const string All = "tenants:*";
    }

    /// <summary>
    ///     Permission management permissions.
    /// </summary>
    public static class Permissions
    {
        public const string Read = "permissions:read";
        public const string Grant = "permissions:grant";
        public const string Revoke = "permissions:revoke";
        public const string All = "permissions:*";
    }

    /// <summary>
    ///     Role management permissions.
    /// </summary>
    public static class Roles
    {
        public const string Read = "roles:read";
        public const string Write = "roles:write";
        public const string Assign = "roles:assign";
        public const string All = "roles:*";
    }

    /// <summary>
    ///     Audit log permissions.
    /// </summary>
    public static class Audit
    {
        public const string Read = "audit:read";
        public const string Export = "audit:export";
    }

    /// <summary>
    ///     Settings/configuration permissions.
    /// </summary>
    public static class Settings
    {
        public const string Read = "settings:read";
        public const string Write = "settings:write";
        public const string All = "settings:*";
    }
}

/// <summary>
///     Constants for authorization-related claim types.
/// </summary>
public static class AuthorizationClaims
{
    /// <summary>
    ///     JWT standard subject claim (user ID).
    /// </summary>
    public const string Sub = "sub";

    /// <summary>
    ///     Claim indicating MFA has been verified for this session.
    /// </summary>
    public const string MfaVerified = "mfa_verified";

    /// <summary>
    ///     Claim containing the tenant ID.
    /// </summary>
    public const string TenantId = "tenant_id";

    /// <summary>
    ///     Claim containing the user's permissions.
    /// </summary>
    public const string Permissions = "permissions";

    /// <summary>
    ///     Claim indicating the user is a system administrator.
    /// </summary>
    public const string SystemAdmin = "system_admin";

    /// <summary>
    ///     Claim indicating the user is a tenant administrator.
    /// </summary>
    public const string TenantAdmin = "tenant_admin";

    /// <summary>
    ///     Claim containing the device fingerprint.
    /// </summary>
    public const string DeviceFingerprint = "device_fingerprint";

    /// <summary>
    ///     Claim containing the IP address.
    /// </summary>
    public const string IpAddress = "ip_address";

    /// <summary>
    ///     Claim containing the authentication method.
    /// </summary>
    public const string AuthMethod = "amr";
}
