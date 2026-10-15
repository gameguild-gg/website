namespace GameGuild.Identity.Authentication;

/// <summary>
///     Supported MFA methods
/// </summary>
public enum MfaMethod
{
    Totp = 1,
    BackupCode = 2,
    Sms = 3,
    Email = 4,
    WebAuthn = 5,
    Push = 6
}
