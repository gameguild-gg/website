namespace GameGuild.Identity.Authentication;

/// <summary>
///     Issues and verifies one-time email sign-in codes (the code modality that
///     complements the magic link). Codes are six digits, generated with the
///     cryptographic RNG, and only their SHA-256 digest is ever stored. These
///     guarantees apply to the configured in-process cache, not a distributed
///     token store.
/// </summary>
public interface IEmailCodeService
{
    /// <summary>
    ///     Generates a one-time six-digit sign-in code for the given user.
    ///     Enforces the resend throttle: returns <c>null</c> when a code was
    ///     issued for the same address within the throttle window.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="email">The user's email address</param>
    /// <returns>The plaintext code to deliver by email, or <c>null</c> when throttled</returns>
    Task<string?> GenerateEmailCodeAsync(Guid userId, string email);

    /// <summary>
    ///     Verifies and consumes a one-time sign-in code for an email address.
    ///     Comparison is constant-time on the code digest; each wrong code
    ///     consumes one of the limited verification attempts.
    /// </summary>
    /// <param name="email">The email address the code belongs to</param>
    /// <param name="code">The six-digit code received by email</param>
    /// <returns>Validated code information when successful</returns>
    Task<TokenValidationResult> VerifyEmailCodeAsync(string email, string code);
}
