using GameGuild.Configuration.ApplicationLayer;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     MFA orchestrator that coordinates TOTP, backup-code, and attempt-tracking sub-services.
///     Kept for backward compatibility with code that depends on IMfaService.
/// </summary>
public sealed class MfaService(
    ILogger<MfaService> logger,
    ITotpMfaService totpMfaService,
    IBackupCodeMfaService backupCodeMfaService,
    IMfaAttemptTrackingService attemptTrackingService,
    MfaOptions? mfaOptions = null) : IMfaService
{
    private readonly MfaOptions _mfaOptions = mfaOptions ?? new MfaOptions();

    /// <inheritdoc />
    public Task<MfaConfigurationResponse> GetMfaConfigurationAsync(Guid userId, CancellationToken cancellationToken = default) =>
        attemptTrackingService.GetMfaConfigurationAsync(userId, cancellationToken);

    /// <inheritdoc />
    public async Task<MfaSetupResult> InitiateMfaSetupAsync(Guid userId, string userEmail, CancellationToken cancellationToken = default)
    {
        (var qrCodeUri, var secretKey) = await totpMfaService.SetupTotpAsync(userId, userEmail, cancellationToken).ConfigureAwait(false);

        // Generate backup codes during setup
        var backupCodes = new List<string>();
        for (var i = 0; i < _mfaOptions.BackupCodesCount; i++)
        {
            backupCodes.Add(backupCodeMfaService.GenerateBackupCode());
        }

        // Store hashed backup codes (before MFA is fully enabled)
        await backupCodeMfaService.StoreBackupCodesForSetupAsync(userId, backupCodes, cancellationToken).ConfigureAwait(false);

        return new MfaSetupResult
        {
            Success = true,
            SecretKey = secretKey,
            QrCodeUri = qrCodeUri,
            QrCodeUrl = qrCodeUri,
            BackupCodes = backupCodes.ToArray(),
            Message = "MFA setup initiated successfully"
        };
    }

    /// <inheritdoc />
    public async Task<MfaVerificationResult> CompleteMfaSetupAsync(Guid userId, string totpCode, CancellationToken cancellationToken = default)
    {
        var isValid = await totpMfaService.VerifyTotpAsync(userId, totpCode, null, cancellationToken).ConfigureAwait(false);

        return new MfaVerificationResult { Success = isValid, Message = isValid ? "MFA setup completed successfully" : "Invalid TOTP code" };
    }

    /// <inheritdoc />
    public async Task<MfaVerificationResult> VerifyMfaAsync(Guid userId, string code, MfaMethod method = MfaMethod.Totp, CancellationToken cancellationToken = default)
    {
        try
        {
            bool isValid;

            if (method == MfaMethod.Totp) { isValid = await totpMfaService.VerifyTotpAsync(userId, code, null, cancellationToken).ConfigureAwait(false); }
            else if (method == MfaMethod.BackupCode) { isValid = await backupCodeMfaService.VerifyBackupCodeAsync(userId, code, null, cancellationToken).ConfigureAwait(false); }
            else { isValid = false; }

            return new MfaVerificationResult { Success = isValid, RequiresAdditionalVerification = false, Message = isValid ? "MFA verification successful" : "Invalid MFA code" };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to verify MFA for user: {UserId}", userId);

            return new MfaVerificationResult { Success = false, Message = ex.Message };
        }
    }

    /// <inheritdoc />
    public async Task<bool> DisableMfaAsync(Guid userId, string confirmationCode, CancellationToken cancellationToken = default)
    {
        // Verify the confirmation code before disabling
        var verificationResult = await VerifyMfaAsync(userId, confirmationCode, MfaMethod.Totp, cancellationToken).ConfigureAwait(false);

        if (!verificationResult.Success) { return false; }

        return await attemptTrackingService.DisableMfaAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<string[]> GenerateBackupCodesAsync(Guid userId, CancellationToken cancellationToken = default) =>
        backupCodeMfaService.GenerateBackupCodesAsync(userId, cancellationToken);

    /// <inheritdoc />
    public Task<bool> VerifyBackupCodeAsync(Guid userId, string backupCode, string? deviceId = null, CancellationToken cancellationToken = default) =>
        backupCodeMfaService.VerifyBackupCodeAsync(userId, backupCode, deviceId, cancellationToken);

    /// <inheritdoc />
    public Task<byte[]> GenerateQrCodeAsync(string qrCodeData, CancellationToken cancellationToken = default) =>
        totpMfaService.GenerateQrCodeAsync(qrCodeData, cancellationToken);

    /// <inheritdoc />
    public Task<bool> IsMfaEnabledAsync(Guid userId, CancellationToken cancellationToken = default) =>
        attemptTrackingService.GetMfaStatusAsync(userId, cancellationToken);

    /// <inheritdoc />
    public Task<bool> IsMfaRequiredAsync(Guid userId, CancellationToken cancellationToken = default) =>
        attemptTrackingService.IsMfaRequiredByPolicyAsync(userId, cancellationToken);

    /// <inheritdoc />
    public async Task ResetMfaFailedAttemptsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await attemptTrackingService.ResetFailedAttemptsAsync(userId, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task<bool> IsUserLockedOutAsync(Guid userId, CancellationToken cancellationToken = default) =>
        attemptTrackingService.IsUserLockedOutAsync(userId, cancellationToken);
}
