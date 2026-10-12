namespace GameGuild.Configuration.ApplicationLayer;

/// <summary>
///     Configuration options for JWT token generation and validation.
/// </summary>
public sealed class JwtOptions : BaseOptions
{
    /// <summary>
    ///     Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "Jwt";

    /// <summary>
    ///     The issuer of the JWT tokens (e.g., "GameGuild").
    /// </summary>
    public string Issuer { get; set; } = "GameGuild";

    /// <summary>
    ///     The audience for the JWT tokens (e.g., "GameGuild.Users").
    /// </summary>
    public string Audience { get; set; } = "GameGuild.Users";

    /// <summary>
    ///     Secret key for HS256 symmetric signing.
    ///     Must be at least 32 characters for HS256 algorithm.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    ///     Access token expiration time in minutes.
    ///     Default: 60 minutes (1 hour).
    /// </summary>
    public int AccessTokenExpirationMinutes { get; set; } = 60;

    /// <summary>
    ///     Refresh token expiration time in days.
    ///     Default: 30 days.
    /// </summary>
    public int RefreshTokenExpirationDays { get; set; } = 30;

    /// <summary>
    ///     Refresh token expiration time in days for persistent ("remember me") sessions.
    ///     Used when a sign-in request opts into a durable session.
    ///     Default: 30 days.
    /// </summary>
    public int PersistentRefreshTokenExpirationDays { get; set; } = 30;

    /// <summary>
    ///     Renew the refresh-token lifetime on rotation, within the session's absolute limit.
    ///     When false, rotation preserves the predecessor's expiration deadline.
    /// </summary>
    public bool RefreshTokenSlidingExpiration { get; set; } = true;

    /// <summary>Automatic replay containment; explicit revoke-all remains independent.</summary>
    public RefreshTokenReplayScope RefreshTokenReplayContainmentScope { get; set; } = RefreshTokenReplayScope.Family;

    /// <summary>
    ///     Clock skew tolerance for token expiration validation in seconds.
    ///     Default: 0 seconds (no tolerance).
    /// </summary>
    public int ClockSkewSeconds { get; set; } = 0;

    /// <summary>
    ///     Whether to validate the issuer claim.
    ///     Default: true.
    /// </summary>
    public bool ValidateIssuer { get; set; } = true;

    /// <summary>
    ///     Whether to validate the audience claim.
    ///     Default: true.
    /// </summary>
    public bool ValidateAudience { get; set; } = true;

    /// <summary>
    ///     Whether to validate token lifetime (expiration).
    ///     Default: true.
    /// </summary>
    public bool ValidateLifetime { get; set; } = true;

    /// <summary>
    ///     Whether to validate the issuer signing key.
    ///     Default: true.
    /// </summary>
    public bool ValidateIssuerSigningKey { get; set; } = true;

    /// <summary>
    ///     Returns true if the configuration is valid
    /// </summary>
    public bool IsValid { get => Validate().Count == 0; }

    /// <summary>
    ///     Validates the JWT options
    /// </summary>
    /// <returns>Validation errors, if any</returns>
    public new List<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Issuer)) { errors.Add("JWT Issuer is required"); }

        if (string.IsNullOrWhiteSpace(Audience)) { errors.Add("JWT Audience is required"); }

        if (string.IsNullOrWhiteSpace(SecretKey)) { errors.Add("JWT SecretKey is required"); }
        else if (SecretKey.Length < 32) { errors.Add("JWT SecretKey must be at least 32 characters long for HS256 algorithm"); }

        if (AccessTokenExpirationMinutes <= 0) { errors.Add("JWT AccessTokenExpirationMinutes must be greater than 0"); }

        if (RefreshTokenExpirationDays <= 0) { errors.Add("JWT RefreshTokenExpirationDays must be greater than 0"); }

        if (PersistentRefreshTokenExpirationDays <= 0) { errors.Add("JWT PersistentRefreshTokenExpirationDays must be greater than 0"); }

        if (!Enum.IsDefined(RefreshTokenReplayContainmentScope)) { errors.Add("JWT RefreshTokenReplayContainmentScope must be Family or Account"); }

        if (ClockSkewSeconds < 0) { errors.Add("JWT ClockSkewSeconds cannot be negative"); }

        return errors;
    }
}
