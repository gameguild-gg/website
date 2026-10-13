namespace GameGuild.Identity.Authentication;

/// <summary>
///     Service for protecting against user enumeration attacks.
///     Prevents attackers from discovering valid usernames/emails through timing attacks or error messages.
/// </summary>
public interface IUserEnumerationProtectionService
{
    /// <summary>
    ///     Adds artificial delay to authentication responses to prevent timing attacks.
    ///     Ensures both successful and failed attempts take similar time.
    /// </summary>
    /// <param name="isValidUser">Whether the user exists (internal use only)</param>
    /// <param name="startTime">When the authentication attempt started</param>
    Task AddTimingProtectionDelayAsync(bool isValidUser, DateTime startTime);

    /// <summary>Server-created window; the native implementation shares the monotonic origin.</summary>
    AuthenticationTimingScope BeginAuthenticationTiming() => new();

    /// <summary>Compatibility for existing custom providers; native compensation is monotonic and cancellable.</summary>
    Task AddTimingProtectionDelayAsync(AuthenticationTimingScope timingScope, CredentialWorkClassification credentialWork) =>
        AddTimingProtectionDelayAsync(credentialWork == CredentialWorkClassification.Completed, timingScope.Origin.StartedAtUtc);

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
