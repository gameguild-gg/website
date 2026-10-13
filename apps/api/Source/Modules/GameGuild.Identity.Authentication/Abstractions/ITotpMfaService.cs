namespace GameGuild.Identity.Authentication;

/// <summary>
///     Service for TOTP (Time-based One-Time Password) MFA operations.
///     Handles secret generation, QR code URIs, and TOTP code verification.
/// </summary>
public interface ITotpMfaService
{
    /// <summary>
    ///     Sets up TOTP-based MFA for a user. Returns QR code URI, secret key, and setup expiry.
    /// </summary>
    Task<(string QrCodeUri, string SecretKey, DateTimeOffset ExpiresAt)> SetupTotpAsync(Guid userId, string userEmail, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Verifies TOTP code and enables MFA if first-time setup.
    /// </summary>
    Task<bool> VerifyTotpAsync(Guid userId, string totpCode, string? deviceId = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Generates a QR code image from QR code data.
    /// </summary>
    Task<byte[]> GenerateQrCodeAsync(string qrCodeData, CancellationToken cancellationToken = default);
}
