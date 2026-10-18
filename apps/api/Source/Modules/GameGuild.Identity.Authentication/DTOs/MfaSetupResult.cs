namespace GameGuild.Identity.Authentication;

/// <summary>
/// Result of MFA setup operation
/// </summary>
public class MfaSetupResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? QrCodeUrl { get; set; }
    public string? QrCodeUri { get => QrCodeUrl; set => QrCodeUrl = value; }
    public string? Secret { get; set; }
    public string? SecretKey { get => Secret; set => Secret = value; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string[]? BackupCodes { get; set; }
}
