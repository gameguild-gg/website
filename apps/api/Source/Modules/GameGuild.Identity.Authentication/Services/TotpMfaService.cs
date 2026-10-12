using System.Collections;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Configuration.ApplicationLayer;
using Microsoft.Extensions.Logging;
using QRCoder;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     TOTP (Time-based One-Time Password) MFA service.
///     Handles secret generation, QR code URIs, and TOTP code verification (RFC 6238).
/// </summary>
public sealed class TotpMfaService(
    ILogger<TotpMfaService> logger,
    IUserMfaConfigurationRepository mfaConfigRepository,
    IMfaAttemptTrackingService attemptTrackingService,
    IEncryptionService encryptionService,
    ITotpReplayStore replayStore,
    MfaOptions? mfaOptions = null,
    TimeProvider? timeProvider = null) : ITotpMfaService
{
    private readonly MfaOptions _mfaOptions = mfaOptions ?? new MfaOptions();
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    /// <summary>
    ///     Sets up TOTP-based MFA for a user. Returns QR code URI and secret key.
    /// </summary>
    public async Task<(string QrCodeUri, string SecretKey)> SetupTotpAsync(Guid userId, string userEmail, CancellationToken cancellationToken = default)
    {
        if (!_mfaOptions.Enabled)
        {
            throw new InvalidOperationException("Multi-factor authentication is disabled.");
        }

        logger.LogInformation("Setting up TOTP MFA for user: {UserId}", userId);

        try
        {
            // Check if MFA already enabled
            var existingConfig = await mfaConfigRepository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);

            if (existingConfig?.IsEnabled == true)
            {
                logger.LogWarning("MFA already enabled for user: {UserId}", userId);

                throw new InvalidOperationException("MFA is already enabled for this user");
            }

            // Generate secret key (Base32 encoded, 160 bits = 32 characters)
            var secretKey = GenerateBase32Secret();

            // Encrypt secret key before storing
            var encryptedSecret = encryptionService.Encrypt(secretKey);

            // Create or update MFA configuration
            var mfaConfig = existingConfig ?? new UserMfaConfiguration { Id = Guid.NewGuid(), UserId = userId };

            mfaConfig.PreferredMethod = MfaMethod.Totp;
            mfaConfig.TotpSecretKey = encryptedSecret;
            mfaConfig.IsEnabled = false; // Enabled after first successful verification
            mfaConfig.IsSetupComplete = false;
            mfaConfig.SetupExpiresAt = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(_mfaOptions.SetupSessionDurationMinutes);
            mfaConfig.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

            if (existingConfig == null) { await mfaConfigRepository.CreateAsync(mfaConfig, cancellationToken).ConfigureAwait(false); }
            else { await mfaConfigRepository.UpdateAsync(mfaConfig, cancellationToken).ConfigureAwait(false); }

            // Generate QR code URI (otpauth://totp/...)
            var qrCodeUri = GenerateTotpUri(userEmail, secretKey, _mfaOptions.TotpIssuer, _mfaOptions.TotpTimeStepSeconds);

            logger.LogInformation("TOTP setup successful for user: {UserId}", userId);

            return (qrCodeUri, secretKey);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting up TOTP for user: {UserId}", userId);

            throw;
        }
    }

    /// <summary>
    ///     Verifies TOTP code and enables MFA if first-time setup.
    /// </summary>
    public async Task<bool> VerifyTotpAsync(Guid userId, string totpCode, string? deviceId = null, CancellationToken cancellationToken = default)
    {
        if (!_mfaOptions.Enabled)
        {
            return false;
        }

        logger.LogInformation("Verifying TOTP code for user: {UserId}", userId);

        try
        {
            // Get MFA configuration
            var mfaConfig = await mfaConfigRepository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);

            if (mfaConfig == null || string.IsNullOrEmpty(mfaConfig.TotpSecretKey))
            {
                logger.LogWarning("No TOTP configuration found for user: {UserId}", userId);
                await attemptTrackingService.RecordMfaAttemptAsync(userId, MfaMethod.Totp, false, "No TOTP configuration", deviceId, cancellationToken).ConfigureAwait(false);

                return false;
            }

            var setupExpiresAt = mfaConfig.SetupExpiresAt
                ?? mfaConfig.UpdatedAt.AddMinutes(_mfaOptions.SetupSessionDurationMinutes);
            if (!mfaConfig.IsEnabled && _timeProvider.GetUtcNow().UtcDateTime >= setupExpiresAt)
            {
                mfaConfig.TotpSecretKey = null;
                mfaConfig.BackupCodes = null;
                mfaConfig.SetupExpiresAt = null;
                mfaConfig.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
                await mfaConfigRepository.UpdateAsync(mfaConfig, cancellationToken).ConfigureAwait(false);
                await attemptTrackingService.RecordMfaAttemptAsync(userId, MfaMethod.Totp, false, "Setup expired", deviceId, cancellationToken).ConfigureAwait(false);

                return false;
            }

            // Check lockout
            if (attemptTrackingService.IsLockedOut(mfaConfig))
            {
                logger.LogWarning("User is locked out due to failed MFA attempts: {UserId}", userId);
                await attemptTrackingService.RecordMfaAttemptAsync(userId, MfaMethod.Totp, false, "Account locked", deviceId, cancellationToken).ConfigureAwait(false);

                return false;
            }

            // Decrypt secret key
            var secretKey = encryptionService.Decrypt(mfaConfig.TotpSecretKey);

            var now = _timeProvider.GetUtcNow();
            var matchedStep = FindMatchingTotpStep(secretKey, totpCode, _mfaOptions.TotpClockSkew,
                _mfaOptions.TotpTimeStepSeconds, now);
            if (matchedStep is not null)
            {
                // Hash the canonical secret, not its randomized encrypted representation.
                var canonicalSecret = secretKey.ToUpperInvariant().Replace(" ", "").Replace("-", "");
                var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(canonicalSecret))).ToLowerInvariant();
                var accepted = await replayStore.TryAcceptAsync(mfaConfig.Id, fingerprint, matchedStep.Value, now,
                    async acceptedCancellation =>
                    {
                        if (!mfaConfig.IsEnabled)
                        {
                            mfaConfig.IsEnabled = true;
                            mfaConfig.IsSetupComplete = true;
                            mfaConfig.SetupExpiresAt = null;
                            mfaConfig.QrCodeSetupData = null;
                            mfaConfig.EnabledAt = now.UtcDateTime;
                        }
                        mfaConfig.FailedAttempts = 0;
                        mfaConfig.LockedOutUntil = null;
                        mfaConfig.LastUsedAt = now.UtcDateTime;
                        await mfaConfigRepository.UpdateAsync(mfaConfig, acceptedCancellation).ConfigureAwait(false);
                        await attemptTrackingService.RecordMfaAttemptAsync(userId, MfaMethod.Totp, true, null, deviceId,
                            acceptedCancellation).ConfigureAwait(false);
                    }, cancellationToken).ConfigureAwait(false);
                if (accepted)
                {
                    logger.LogInformation("TOTP verification successful for user: {UserId}", userId);
                    return true;
                }
            }

            await attemptTrackingService.RecordFailedMfaAttemptAsync(
                mfaConfig,
                MfaMethod.Totp,
                "Invalid or previously used code",
                deviceId,
                cancellationToken).ConfigureAwait(false);

            logger.LogWarning("Invalid TOTP code for user: {UserId}, Failed attempts: {FailedAttempts}", userId, mfaConfig.FailedAttempts);

            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error verifying TOTP for user: {UserId}", userId);
            await attemptTrackingService.RecordMfaAttemptAsync(userId, MfaMethod.Totp, false, "System error", deviceId, cancellationToken).ConfigureAwait(false);

            return false;
        }
    }

    /// <summary>
    ///     Generates a QR code image from QR code data.
    /// </summary>
    public Task<byte[]> GenerateQrCodeAsync(string qrCodeData, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(qrCodeData);
        cancellationToken.ThrowIfCancellationRequested();

        using var generator = new QRCodeGenerator();
        using var qrCodeDataModel = generator.CreateQrCode(qrCodeData, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrCodeDataModel);

        return Task.FromResult(qrCode.GetGraphic(20));
    }

    #region Private TOTP Helpers

    /// <summary>
    ///     Generates a Base32-encoded secret key for TOTP (160 bits = 32 characters).
    /// </summary>
    private static string GenerateBase32Secret()
    {
        const string base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var secret = new char[32];

        for (var i = 0; i < secret.Length; i++) { secret[i] = base32Chars[RandomNumberGenerator.GetInt32(base32Chars.Length)]; }

        return new string(secret);
    }

    /// <summary>
    ///     Generates TOTP URI for QR code (otpauth://totp/...).
    /// </summary>
    internal static string GenerateTotpUri(string userEmail, string secretKey, string issuer, int timeStepSeconds)
    {
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedEmail = Uri.EscapeDataString(userEmail);

        return $"otpauth://totp/{encodedIssuer}:{encodedEmail}?secret={secretKey}&issuer={encodedIssuer}&algorithm=SHA1&digits=6&period={timeStepSeconds}";
    }

    /// <summary>
    ///     Verifies TOTP code using time-based algorithm (RFC 6238).
    /// </summary>
    private static long? FindMatchingTotpStep(string secretKey, string totpCode, int window, int timeStepSeconds,
        DateTimeOffset now)
    {
        if (totpCode is not { Length: 6 } || !totpCode.All(char.IsAsciiDigit)) { return null; }
        var timeStep = now.ToUnixTimeSeconds() / timeStepSeconds;
        var supplied = Encoding.ASCII.GetBytes(totpCode);
        long? matchedStep = null;
        for (var offset = -window; offset <= window; offset++)
        {
            var candidateStep = timeStep + offset;
            if (candidateStep < 0) { continue; }
            var expected = Encoding.ASCII.GetBytes(GenerateTotpCode(secretKey, candidateStep));
            if (CryptographicOperations.FixedTimeEquals(expected, supplied)) { matchedStep = candidateStep; }
        }
        return matchedStep;
    }

    /// <summary>
    ///     Generates TOTP code for a given time step.
    /// </summary>
    private static string GenerateTotpCode(string secretKey, long timeStep)
    {
        // Convert Base32 secret to bytes
        var keyBytes = Base32Decode(secretKey);

        // Convert time step to bytes
        var timeBytes = BitConverter.GetBytes(timeStep);

        if (BitConverter.IsLittleEndian) { Array.Reverse(timeBytes); }

        // HMAC-SHA1
        using var hmac = new HMACSHA1(keyBytes);
        var hash = hmac.ComputeHash(timeBytes);

        // Dynamic truncation
        var offset = hash[^1] & 0x0F;
        var truncatedHash = (hash[offset] & 0x7F) << 24 | (hash[offset + 1] & 0xFF) << 16 | (hash[offset + 2] & 0xFF) << 8 | hash[offset + 3] & 0xFF;

        // Generate 6-digit code
        var code = truncatedHash % 1000000;

        return code.ToString("D6");
    }

    /// <summary>
    ///     Decodes Base32 string to bytes.
    /// </summary>
    private static byte[] Base32Decode(string base32)
    {
        const string base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        base32 = base32.ToUpperInvariant().Replace(" ", "").Replace("-", "");

        var bits = new BitArray(base32.Length * 5);
        var bitIndex = 0;

        foreach (var c in base32)
        {
            var value = base32Chars.IndexOf(c);

            if (value < 0) { throw new ArgumentException("Invalid Base32 character"); }

            for (var i = 4; i >= 0; i--) { bits[bitIndex++] = (value >> i & 1) == 1; }
        }

        var bytes = new byte[bitIndex / 8];

        for (var i = 0; i < bytes.Length; i++)
        {
            for (var j = 0; j < 8; j++)
            {
                if (bits[i * 8 + j]) { bytes[i] |= (byte)(1 << 7 - j); }
            }
        }

        return bytes;
    }

    #endregion
}
