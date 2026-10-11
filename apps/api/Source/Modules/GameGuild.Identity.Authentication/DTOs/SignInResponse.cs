namespace GameGuild.Identity.Authentication;

/// <summary>
///     DTO for sign-in response
/// </summary>
public class SignInResponse
{
    /// <summary>
    ///     Whether sign-in was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    ///     Response message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    ///     JWT access token
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    ///     Refresh token
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    ///     Backward compatible field: originally represented refresh token expiry (or conflated); prefer using AccessTokenExpiresAt / RefreshTokenExpiresAt.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    ///     When the access token expires (short-lived)
    /// </summary>
    public DateTime AccessTokenExpiresAt { get; set; }

    /// <summary>
    ///     When the refresh token expires (long-lived)
    /// </summary>
    public DateTime RefreshTokenExpiresAt { get; set; }

    /// <summary>
    ///     Expiration in seconds
    /// </summary>
    public int ExpiresIn { get; set; }

    /// <summary>
    ///     User ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    ///     User email
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    ///     Session ID
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    ///     Temporary token for MFA flows
    /// </summary>
    public string? TempToken { get; set; }

    /// <summary>
    ///     MFA token
    /// </summary>
    public string? MfaToken { get; set; }

    /// <summary>Recovery codes returned once, only when a limited enrollment finishes with verified TOTP.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string[]? MfaEnrollmentBackupCodes { get; set; }

    /// <summary>
    ///     User information
    /// </summary>
    public UserDto User { get; set; } = new UserDto();

    /// <summary>
    ///     Current tenant ID
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    ///     List of tenants the user has access to
    /// </summary>
    public IEnumerable<global::GameGuild.TenantInfo>? AvailableTenants { get; set; }

    /// <summary>
    ///     Whether MFA is required
    /// </summary>
    public bool RequiresMfa { get; set; }

    /// <summary>
    ///     MFA session ID if MFA is required
    /// </summary>
    public string? MfaSessionId { get; set; }

    /// <summary>
    ///     Whether step-up authentication is required due to high-risk login
    /// </summary>
    public bool RequiresStepUp { get; set; }

    /// <summary>
    ///     Temporary token for completing step-up authentication
    /// </summary>
    public string? StepUpToken { get; set; }

    /// <summary>
    ///     When the step-up token expires
    /// </summary>
    public DateTime? StepUpExpiresAt { get; set; }

    /// <summary>
    ///     Risk level detected during authentication
    /// </summary>
    public RiskLevel? RiskLevel { get; set; }

    /// <summary>
    ///     List of risk factors detected
    /// </summary>
    public List<string>? RiskFactors { get; set; }

    /// <summary>
    ///     Available step-up authentication methods
    /// </summary>
    public List<string>? AvailableMethods { get; set; }

    /// <summary>
    ///     Authentication method references (OIDC <c>amr</c>) attested by the federated
    /// identity provider for this sign-in. Null for non-federated flows.
    /// </summary>
    public IReadOnlyList<string>? AuthenticationMethodReferences { get; set; }

    /// <summary>
    ///     Authentication context class reference (OIDC <c>acr</c>) attested by the federated
    /// identity provider for this sign-in. Null for non-federated flows.
    /// </summary>
    public string? AuthenticationContextClassReference { get; set; }

    /// <summary>
    ///     Whether the federated identity provider attested multi-factor authentication
    /// (<c>amr</c> containing "mfa") for this sign-in.
    /// </summary>
    public bool MfaVerifiedByProvider { get; set; }
}
