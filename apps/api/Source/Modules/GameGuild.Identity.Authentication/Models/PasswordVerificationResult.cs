namespace GameGuild.Identity.Authentication;

/// <summary>Validity and completed credential work are independent; no secret or credential is retained.</summary>
public readonly record struct PasswordVerificationResult(bool IsValid, bool WorkPerformed)
{
    /// <summary>Compatibility name for the credential classification contract.</summary>
    public bool PerformedCryptographicWork => WorkPerformed;

    public static PasswordVerificationResult RejectedWithoutWork => default;
}
