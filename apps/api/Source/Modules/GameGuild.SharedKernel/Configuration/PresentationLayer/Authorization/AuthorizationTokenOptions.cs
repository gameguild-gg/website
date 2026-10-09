namespace GameGuild.Configuration.PresentationLayer.Authorization;

/// <summary>
///     Configuration options for token-based authorization.
/// </summary>
public sealed class AuthorizationTokenOptions : BaseOptions
{
    /// <summary>
    ///     The configuration section name.
    /// </summary>
    public const string SectionName = "Authorization:Token";

    /// <summary>
    ///     Whether to require the token's tenant claim to match the resolved tenant.
    /// </summary>
    public bool RequireTenantClaimMatch { get; set; } = true;

    /// <summary>
    ///     The claim type used for tenant identification in tokens.
    /// </summary>
    public string TenantClaimType { get; set; } = "tenant_id";

    /// <summary>
    ///     The claim type used for user's default tenant.
    /// </summary>
    public string UserDefaultTenantClaimType { get; set; } = "udt";

    /// <summary>
    ///     The claim type used for permission claims.
    /// </summary>
    public string PermissionClaimType { get; set; } = "perm";

    /// <summary>
    ///     The claim type used for role ID claims.
    /// </summary>
    public string RoleIdClaimType { get; set; } = "role_id";

    /// <summary>
    ///     The claim type used for group ID claims.
    /// </summary>
    public string GroupIdClaimType { get; set; } = "group_id";

    /// <inheritdoc />
    public override void Validate()
    {
        base.Validate();
        
        if (string.IsNullOrWhiteSpace(TenantClaimType))
        {
            throw new InvalidOperationException("TenantClaimType cannot be null or empty.");
        }

        if (string.IsNullOrWhiteSpace(PermissionClaimType))
        {
            throw new InvalidOperationException("PermissionClaimType cannot be null or empty.");
        }
    }

    /// <summary>
    ///     Creates a default instance of AuthorizationTokenOptions.
    /// </summary>
    public static AuthorizationTokenOptions CreateDefault() => new();
}
