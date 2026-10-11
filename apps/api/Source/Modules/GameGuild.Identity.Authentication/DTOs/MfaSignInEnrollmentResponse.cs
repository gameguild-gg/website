namespace GameGuild.Identity.Authentication;

/// <summary>Provisioning data only; this response never grants an authenticated session.</summary>
public sealed class MfaSignInEnrollmentResponse
{
    public bool Success { get; init; }
    public string SecretKey { get; init; } = string.Empty;
    public string QrCodeUri { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
}
