namespace GameGuild.Identity.Context.Actors;

/// <summary>
///     Strongly-typed actor attributes/claims for ABAC (Attribute-Based Access Control).
///     Replaces stringly-typed dictionary for compile-time safety.
/// </summary>
/// <remarks>
///     <para>
///         This record provides type-safe access to common actor attributes while still
///         allowing extensibility via the <see cref="Custom"/> dictionary for application-specific claims.
///     </para>
///     <para>
///         All properties are nullable since not all actors will have all attributes.
///         Check for null before using attribute values in authorization decisions.
///     </para>
/// </remarks>
public sealed record ActorAttributes
{
    /// <summary>
    ///     Empty attributes instance for anonymous/minimal actors.
    /// </summary>
    public static readonly ActorAttributes Empty = new();

    #region Identity Attributes

    /// <summary>
    ///     Gets the actor's email address.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    ///     Gets whether the actor's email has been verified.
    /// </summary>
    public bool EmailVerified { get; init; }

    /// <summary>
    ///     Gets the actor's display name.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    ///     Gets the actor's first name.
    /// </summary>
    public string? FirstName { get; init; }

    /// <summary>
    ///     Gets the actor's last name.
    /// </summary>
    public string? LastName { get; init; }

    /// <summary>
    ///     Gets the actor's username/handle.
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    ///     Gets the actor's profile picture URL.
    /// </summary>
    public string? PictureUrl { get; init; }

    #endregion

    #region Security Attributes

    /// <summary>
    ///     Gets whether MFA (Multi-Factor Authentication) has been verified for this session.
    /// </summary>
    public bool MfaVerified { get; init; }

    /// <summary>
    ///     Gets the MFA method used (e.g., "totp", "sms", "webauthn").
    /// </summary>
    public string? MfaMethod { get; init; }

    /// <summary>
    ///     Gets the IP address of the request.
    /// </summary>
    public string? IpAddress { get; init; }

    /// <summary>
    ///     Gets the user agent string of the request.
    /// </summary>
    public string? UserAgent { get; init; }

    /// <summary>
    ///     Gets the device fingerprint for the session.
    /// </summary>
    public string? DeviceFingerprint { get; init; }

    /// <summary>
    ///     Gets whether this is a trusted device.
    /// </summary>
    public bool TrustedDevice { get; init; }

    /// <summary>
    ///     Gets the session ID.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    ///     Gets the JWT ID (jti claim) for token revocation tracking.
    /// </summary>
    public string? TokenId { get; init; }

    /// <summary>
    ///     Gets when the authentication occurred (auth_time claim).
    /// </summary>
    public DateTimeOffset? AuthenticatedAt { get; init; }

    /// <summary>
    ///     Gets when the token expires.
    /// </summary>
    public DateTimeOffset? TokenExpiresAt { get; init; }

    #endregion

    #region Organization Attributes

    /// <summary>
    ///     Gets the actor's department within the organization.
    /// </summary>
    public string? Department { get; init; }

    /// <summary>
    ///     Gets the actor's job title.
    /// </summary>
    public string? JobTitle { get; init; }

    /// <summary>
    ///     Gets the actor's manager/supervisor ID.
    /// </summary>
    public Guid? ManagerId { get; init; }

    /// <summary>
    ///     Gets the organization unit path (e.g., "/Engineering/Backend").
    /// </summary>
    public string? OrganizationUnit { get; init; }

    /// <summary>
    ///     Gets the actor's employee ID.
    /// </summary>
    public string? EmployeeId { get; init; }

    /// <summary>
    ///     Gets the actor's cost center.
    /// </summary>
    public string? CostCenter { get; init; }

    #endregion

    #region Tenant Membership Attributes

    /// <summary>
    ///     Gets the actor's role within the current tenant (e.g., "Owner", "Admin", "Member").
    /// </summary>
    public string? TenantRole { get; init; }

    /// <summary>
    ///     Gets when the actor joined the current tenant.
    /// </summary>
    public DateTimeOffset? TenantJoinedAt { get; init; }

    /// <summary>
    ///     Gets the actor's membership status in the current tenant.
    /// </summary>
    public string? TenantMembershipStatus { get; init; }

    #endregion

    #region OAuth/External Provider Attributes

    /// <summary>
    ///     Gets the external identity provider (e.g., "google", "github", "microsoft").
    /// </summary>
    public string? IdentityProvider { get; init; }

    /// <summary>
    ///     Gets the external provider's subject ID.
    /// </summary>
    public string? ExternalSubjectId { get; init; }

    #endregion

    #region Locale Attributes

    /// <summary>
    ///     Gets the actor's preferred locale (e.g., "en-US", "pt-BR").
    /// </summary>
    public string? Locale { get; init; }

    /// <summary>
    ///     Gets the actor's timezone (e.g., "America/New_York").
    /// </summary>
    public string? Timezone { get; init; }

    #endregion

    #region Extensibility

    /// <summary>
    ///     Gets custom/application-specific attributes not covered by the typed properties.
    /// </summary>
    /// <remarks>
    ///     Use this for domain-specific claims. Consider promoting frequently-used
    ///     custom attributes to typed properties in future versions.
    /// </remarks>
    public IReadOnlyDictionary<string, string> Custom { get; init; } =
        new Dictionary<string, string>();

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Gets the full name by combining first and last name.
    /// </summary>
    public string? FullName => string.IsNullOrEmpty(FirstName) && string.IsNullOrEmpty(LastName)
        ? null
        : $"{FirstName} {LastName}".Trim();

    /// <summary>
    ///     Gets a custom attribute by key.
    /// </summary>
    /// <param name="key">The attribute key.</param>
    /// <returns>The attribute value, or null if not found.</returns>
    public string? GetCustomAttribute(string key)
    {
        return Custom.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    ///     Creates a new instance with an additional custom attribute.
    /// </summary>
    /// <param name="key">The attribute key.</param>
    /// <param name="value">The attribute value.</param>
    /// <returns>A new ActorAttributes instance with the added attribute.</returns>
    public ActorAttributes WithCustomAttribute(string key, string value)
    {
        var newCustom = new Dictionary<string, string>(Custom) { [key] = value };
        return this with { Custom = newCustom };
    }

    /// <summary>
    ///     Converts to a legacy dictionary format for backward compatibility.
    /// </summary>
    /// <returns>Dictionary of all non-null attributes.</returns>
    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        var result = new Dictionary<string, string>();

        if (Email != null)
        {
            result["email"] = Email;
        }

        if (EmailVerified)
        {
            result["email_verified"] = "true";
        }

        if (DisplayName != null)
        {
            result["name"] = DisplayName;
        }

        if (FirstName != null)
        {
            result["given_name"] = FirstName;
        }

        if (LastName != null)
        {
            result["family_name"] = LastName;
        }

        if (Username != null)
        {
            result["preferred_username"] = Username;
        }

        if (PictureUrl != null)
        {
            result["picture"] = PictureUrl;
        }

        if (MfaVerified)
        {
            result["mfa_verified"] = "true";
        }

        if (MfaMethod != null)
        {
            result["mfa_method"] = MfaMethod;
        }

        if (IpAddress != null)
        {
            result["ip_address"] = IpAddress;
        }

        if (UserAgent != null)
        {
            result["user_agent"] = UserAgent;
        }

        if (DeviceFingerprint != null)
        {
            result["device_fingerprint"] = DeviceFingerprint;
        }

        if (TrustedDevice)
        {
            result["trusted_device"] = "true";
        }

        if (SessionId != null)
        {
            result["session_id"] = SessionId;
        }

        if (TokenId != null)
        {
            result["jti"] = TokenId;
        }

        if (AuthenticatedAt.HasValue)
        {
            result["auth_time"] = AuthenticatedAt.Value.ToUnixTimeSeconds().ToString();
        }

        if (TokenExpiresAt.HasValue)
        {
            result["exp"] = TokenExpiresAt.Value.ToUnixTimeSeconds().ToString();
        }

        if (Department != null)
        {
            result["department"] = Department;
        }

        if (JobTitle != null)
        {
            result["job_title"] = JobTitle;
        }

        if (ManagerId.HasValue)
        {
            result["manager_id"] = ManagerId.Value.ToString();
        }

        if (OrganizationUnit != null)
        {
            result["org_unit"] = OrganizationUnit;
        }

        if (EmployeeId != null)
        {
            result["employee_id"] = EmployeeId;
        }

        if (CostCenter != null)
        {
            result["cost_center"] = CostCenter;
        }

        if (TenantRole != null)
        {
            result["tenant_role"] = TenantRole;
        }

        if (TenantJoinedAt.HasValue)
        {
            result["tenant_joined_at"] = TenantJoinedAt.Value.ToString("O");
        }

        if (TenantMembershipStatus != null)
        {
            result["tenant_membership_status"] = TenantMembershipStatus;
        }

        if (IdentityProvider != null)
        {
            result["idp"] = IdentityProvider;
        }

        if (ExternalSubjectId != null)
        {
            result["external_sub"] = ExternalSubjectId;
        }

        if (Locale != null)
        {
            result["locale"] = Locale;
        }

        if (Timezone != null)
        {
            result["zoneinfo"] = Timezone;
        }

        // Add custom attributes
        foreach (var kvp in Custom)
        {
            result[kvp.Key] = kvp.Value;
        }

        return result;
    }

    /// <summary>
    ///     Creates an ActorAttributes instance from a legacy dictionary.
    /// </summary>
    /// <param name="dictionary">The dictionary of attributes.</param>
    /// <returns>A typed ActorAttributes instance.</returns>
    public static ActorAttributes FromDictionary(IReadOnlyDictionary<string, string>? dictionary)
    {
        if (dictionary == null || dictionary.Count == 0)
        {
            return Empty;
        }

        var custom = new Dictionary<string, string>();
        
        var attrs = new ActorAttributes
        {
            Email = dictionary.TryGetValue("email", out var email) ? email : null,
            EmailVerified = dictionary.TryGetValue("email_verified", out var emailVerified) && 
                           bool.TryParse(emailVerified, out var ev) && ev,
            DisplayName = dictionary.TryGetValue("name", out var name) ? name : null,
            FirstName = dictionary.TryGetValue("given_name", out var givenName) ? givenName : null,
            LastName = dictionary.TryGetValue("family_name", out var familyName) ? familyName : null,
            Username = dictionary.TryGetValue("preferred_username", out var username) ? username : null,
            PictureUrl = dictionary.TryGetValue("picture", out var picture) ? picture : null,
            MfaVerified = dictionary.TryGetValue("mfa_verified", out var mfaVerified) && 
                         bool.TryParse(mfaVerified, out var mv) && mv,
            MfaMethod = dictionary.TryGetValue("mfa_method", out var mfaMethod) ? mfaMethod : null,
            IpAddress = dictionary.TryGetValue("ip_address", out var ip) ? ip : null,
            UserAgent = dictionary.TryGetValue("user_agent", out var ua) ? ua : null,
            DeviceFingerprint = dictionary.TryGetValue("device_fingerprint", out var df) ? df : null,
            TrustedDevice = dictionary.TryGetValue("trusted_device", out var trusted) && 
                           bool.TryParse(trusted, out var td) && td,
            SessionId = dictionary.TryGetValue("session_id", out var sessionId) ? sessionId : null,
            TokenId = dictionary.TryGetValue("jti", out var jti) ? jti : null,
            AuthenticatedAt = dictionary.TryGetValue("auth_time", out var authTime) && 
                             long.TryParse(authTime, out var at) 
                ? DateTimeOffset.FromUnixTimeSeconds(at) 
                : null,
            TokenExpiresAt = dictionary.TryGetValue("exp", out var exp) && 
                            long.TryParse(exp, out var expVal) 
                ? DateTimeOffset.FromUnixTimeSeconds(expVal) 
                : null,
            Department = dictionary.TryGetValue("department", out var dept) ? dept : null,
            JobTitle = dictionary.TryGetValue("job_title", out var jobTitle) ? jobTitle : null,
            ManagerId = dictionary.TryGetValue("manager_id", out var managerId) && 
                       Guid.TryParse(managerId, out var mid) 
                ? mid 
                : null,
            OrganizationUnit = dictionary.TryGetValue("org_unit", out var orgUnit) ? orgUnit : null,
            EmployeeId = dictionary.TryGetValue("employee_id", out var empId) ? empId : null,
            CostCenter = dictionary.TryGetValue("cost_center", out var costCenter) ? costCenter : null,
            TenantRole = dictionary.TryGetValue("tenant_role", out var tenantRole) ? tenantRole : null,
            TenantJoinedAt = dictionary.TryGetValue("tenant_joined_at", out var joinedAt) && 
                            DateTimeOffset.TryParse(joinedAt, out var ja) 
                ? ja 
                : null,
            TenantMembershipStatus = dictionary.TryGetValue("tenant_membership_status", out var status) 
                ? status 
                : null,
            IdentityProvider = dictionary.TryGetValue("idp", out var idp) ? idp : null,
            ExternalSubjectId = dictionary.TryGetValue("external_sub", out var extSub) ? extSub : null,
            Locale = dictionary.TryGetValue("locale", out var locale) ? locale : null,
            Timezone = dictionary.TryGetValue("zoneinfo", out var tz) ? tz : null
        };

        // Collect unknown keys into Custom
        var knownKeys = new HashSet<string>
        {
            "email", "email_verified", "name", "given_name", "family_name", 
            "preferred_username", "picture", "mfa_verified", "mfa_method",
            "ip_address", "user_agent", "device_fingerprint", "trusted_device",
            "session_id", "jti", "auth_time", "exp", "department", "job_title",
            "manager_id", "org_unit", "employee_id", "cost_center", "tenant_role",
            "tenant_joined_at", "tenant_membership_status", "idp", "external_sub",
            "locale", "zoneinfo"
        };

        foreach (var kvp in dictionary)
        {
            if (!knownKeys.Contains(kvp.Key))
            {
                custom[kvp.Key] = kvp.Value;
            }
        }

        return attrs with { Custom = custom };
    }

    #endregion
}
