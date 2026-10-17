namespace GameGuild.Identity.Authentication;

/// <summary>
///     Response of MFA setup operation
/// </summary>
public class MfaSetupResponse
{
    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public string? SecretKey { get; set; }

    public string? QrCodeData { get; set; }
    
    public string? QrCodeUri { get; set; }
    
    /// <summary>
    ///     When the pending TOTP setup session expires
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }
    
    public string[] BackupCodes { get; set; } = [];

    public static MfaSetupResponse Success(string secretKey, string qrCodeData) { return new MfaSetupResponse { IsSuccess = true, SecretKey = secretKey, QrCodeData = qrCodeData }; }

    public static MfaSetupResponse Failure(string errorMessage) { return new MfaSetupResponse { IsSuccess = false, ErrorMessage = errorMessage }; }
}
