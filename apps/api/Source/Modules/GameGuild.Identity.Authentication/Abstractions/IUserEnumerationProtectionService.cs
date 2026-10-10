namespace GameGuild.Identity.Authentication;

/// <summary>
///     Service for protecting against user enumeration attacks.
///     Prevents attackers from discovering valid usernames/emails through timing attacks or error messages.
/// </summary>
public interface IUserEnumerationProtectionService
{
    /// <summary>
    ///     Captures the server-owned monotonic origin for an authentication attempt. Must be
    ///     called before any account resolution so that lookups and credential verification are
    ///     inside the compensated window.
    /// </summary>
    AuthenticationTimingScope BeginAuthenticationTiming();

    /// <summary>
    ///     Adds compensation to an authentication response so the total window (account
    ///     resolution + credential work + this call) is indistinguishable across outcomes.
    ///     When the window completed no usable credential verification (missing account,
    ///     passwordless account, unusable credential), equivalent dummy verification runs at
    ///     the configured BCrypt work factor before topping the window up to the target time.
    ///     There is no existence-dependent jitter: both classifications follow the same
    ///     deterministic compensation structure.
    /// </summary>
    /// <param name="timingScope">Server-owned scope created before account resolution</param>
    /// <param name="credentialWork">Classification of the credential work completed in the window</param>
    Task AddTimingProtectionDelayAsync(AuthenticationTimingScope timingScope, CredentialWorkClassification credentialWork);

    /// <summary>
    ///     Generates a consistent, generic error message that doesn't reveal if user exists.
    /// </summary>
    /// <param name="context">Context of the authentication attempt</param>
    /// <returns>Generic error message</returns>
    string GetGenericErrorMessage(string context);

    /// <summary>
    ///     Checks if an IP address or identifier should be throttled due to enumeration attempts.
    /// </summary>
    /// <param name="identifier">IP address, email, or other identifier</param>
    /// <returns>Throttle decision with delay duration if applicable</returns>
    Task<ThrottleDecision> ShouldThrottleAsync(string identifier);

    /// <summary>
    ///     Records a potential enumeration attempt for monitoring and blocking.
    /// </summary>
    /// <param name="identifier">The identifier making enumeration attempts</param>
    /// <param name="attemptType">Type of enumeration (login, password reset, etc.)</param>
    Task RecordEnumerationAttemptAsync(string identifier, string attemptType);
}
