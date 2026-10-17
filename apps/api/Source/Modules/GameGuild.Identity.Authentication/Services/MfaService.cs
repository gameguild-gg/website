using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     MFA orchestrator that coordinates TOTP, backup-code, SMS-OTP, push-challenge and
///     attempt-tracking sub-services. Kept for backward compatibility with code that depends
///     on IMfaService.
/// </summary>
public sealed class MfaService(
    ILogger<MfaService> logger,
    ITotpMfaService totpMfaService,
    IBackupCodeMfaService backupCodeMfaService,
    IMfaAttemptTrackingService attemptTrackingService,
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ISmsMfaService? smsMfaService = null,
    IPushMfaService? pushMfaService = null,
    MfaOptions? mfaOptions = null) : IMfaService
{
    private readonly MfaOptions _mfaOptions = mfaOptions ?? new MfaOptions();

    /// <inheritdoc />
    public Task<MfaConfigurationResponse> GetMfaConfigurationAsync(Guid userId, CancellationToken cancellationToken = default) =>
        attemptTrackingService.GetMfaConfigurationAsync(userId, cancellationToken);

    /// <inheritdoc />
    public async Task<MfaSetupResult> InitiateMfaSetupAsync(Guid userId, string userEmail, CancellationToken cancellationToken = default)
    {
        MfaEnrollmentMetrics.RecordEnrollment(MfaMethod.Totp, "started");
        (var qrCodeUri, var secretKey, var expiresAt) = await totpMfaService.SetupTotpAsync(userId, userEmail, cancellationToken).ConfigureAwait(false);

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
            ExpiresAt = expiresAt,
            BackupCodes = backupCodes.ToArray(),
            Message = "MFA setup initiated successfully"
        };
    }

    /// <inheritdoc />
    public async Task<MfaVerificationResult> CompleteMfaSetupAsync(Guid userId, string totpCode, CancellationToken cancellationToken = default)
    {
        var isValid = await totpMfaService.VerifyTotpAsync(userId, totpCode, null, cancellationToken).ConfigureAwait(false);
        MfaEnrollmentMetrics.RecordEnrollment(MfaMethod.Totp, isValid ? "enrolled" : "verify-failed");

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
            else if (method == MfaMethod.Sms)
            {
                if (smsMfaService is null) { isValid = false; }
                else { isValid = await smsMfaService.VerifyCodeAsync(userId, code, cancellationToken).ConfigureAwait(false); }
            }
            else if (method == MfaMethod.Push)
            {
                // Push verification is challenge-id driven (no typed code); delegate to the
                // push service which resolves the user's outstanding challenge and its TTL.
                if (pushMfaService is null) { isValid = false; }
                else { return await pushMfaService.VerifyAsync(userId, cancellationToken).ConfigureAwait(false); }
            }
            else { isValid = false; }

            MfaEnrollmentMetrics.RecordVerification(method, isValid);
            return new MfaVerificationResult
            {
                Success = isValid,
                RequiresAdditionalVerification = false,
                Message = isValid ? "MFA verification successful" : "Invalid MFA code"
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to verify MFA for user: {UserId}", userId);

            return new MfaVerificationResult { Success = false, Message = ex.Message };
        }
    }

    /// <inheritdoc />
    public async Task<bool> DisableMfaAsync(Guid userId, string password, CancellationToken cancellationToken = default)
    {
        // Password re-verification before disabling (issue #244): possession of a live TOTP code
        // alone must not be enough to strip the account's factors — the primary credential is
        // required, and passwordless accounts (no stored hash) cannot disable MFA this way.
        var user = await userRepository.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user?.PasswordHash is not { } passwordHash)
        {
            logger.LogWarning("MFA disable refused for user {UserId}: no password credential on file", userId);
            MfaEnrollmentMetrics.RecordEnrollment(MfaMethod.Totp, "disable-refused");
            return false;
        }

        if (!passwordHasher.VerifyPassword(passwordHash, password))
        {
            logger.LogWarning("MFA disable refused for user {UserId}: password verification failed", userId);
            MfaEnrollmentMetrics.RecordEnrollment(MfaMethod.Totp, "disable-refused");
            return false;
        }

        var disabled = await attemptTrackingService.DisableMfaAsync(userId, cancellationToken).ConfigureAwait(false);
        if (disabled)
        {
            MfaEnrollmentMetrics.RecordEnrollment(MfaMethod.Totp, "disabled");
        }

        return disabled;
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
