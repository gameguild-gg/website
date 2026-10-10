namespace GameGuild.Identity.Authentication;

/// <summary>
///     Password verification outcome combined with the credential-work classification used
///     for timing compensation: a fast structural rejection performed no cryptographic work
///     and must not be reported as completed credential verification.
/// </summary>
public readonly record struct PasswordVerificationResult(bool IsValid, bool PerformedCryptographicWork)
{
    /// <summary>Shorthand for a structural rejection that performed no cryptographic work.</summary>
    public static PasswordVerificationResult RejectedWithoutWork { get; } = new(IsValid: false, PerformedCryptographicWork: false);
}
