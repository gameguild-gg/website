namespace GameGuild.Commerce.Billing;

/// <summary>
///     Google Pay webhook verification configuration.
/// </summary>
/// <remarks>
///     Google Pay server notifications authenticate with a bearer JWT whose audience is the
///     receiving Google Cloud project. Verification is fail closed: when any verification key is
///     configured, every callback JWT must carry a valid RS256 signature made by one of the
///     configured keys, the expected audience, and non-expired timestamps. When no key is
///     configured the Google Pay webhook endpoint rejects all traffic rather than accept
///     unauthenticated events.
/// </remarks>
public class GooglePaySettings
{
    /// <summary>
    ///     Expected Google Cloud project identifier. Must match both the JWT audience claim and
    ///     the Google-Cloud-Project-Id callback header. Empty disables Google Pay processing.
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    ///     Provider public keys (RFC 7468 PEM or base64-encoded SPKI DER) accepted for callback
    ///     JWT signatures. Fail closed: with no key configured, verification always fails.
    /// </summary>
    public IReadOnlyList<string> VerificationKeys { get; set; } = [];

    /// <summary>
    ///     Maximum accepted JWT age in seconds (replay window).
    /// </summary>
    public long TimestampToleranceSeconds { get; set; } = 300;
}
