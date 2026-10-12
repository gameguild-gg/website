using System.Security.Cryptography;
using System.Text;
using GameGuild.Configuration.ApplicationLayer;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Backup code MFA service.
///     Handles generation, hashing, and verification of single-use backup codes.
/// </summary>
public sealed class BackupCodeMfaService(
    ILogger<BackupCodeMfaService> logger,
    IUserMfaConfigurationRepository mfaConfigRepository,
    IMfaAttemptTrackingService attemptTrackingService,
    MfaOptions? mfaOptions = null) : IBackupCodeMfaService
{
    private readonly MfaOptions _mfaOptions = mfaOptions ?? new MfaOptions();

    /// <summary>
    ///     Generates backup codes for account recovery.
    /// </summary>
    public async Task<string[]> GenerateBackupCodesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (!_mfaOptions.Enabled)
        {
            throw new InvalidOperationException("Multi-factor authentication is disabled.");
        }

        logger.LogInformation("Generating backup codes for user: {UserId}", userId);

        try
        {
            var mfaConfig = await mfaConfigRepository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);

            if (mfaConfig is not { IsEnabled: true })
            {
                logger.LogWarning("MFA not enabled for user: {UserId}", userId);

                throw new InvalidOperationException("MFA must be enabled to generate backup codes");
            }

            var backupCodes = new HashSet<string>(StringComparer.Ordinal);

            while (backupCodes.Count < _mfaOptions.BackupCodesCount)
            {
                cancellationToken.ThrowIfCancellationRequested();
                backupCodes.Add(GenerateBackupCode());
            }

            // Hash backup codes before storing (like passwords)
            var hashedCodes = new List<string>();

            foreach (var code in backupCodes)
            {
                var hashedCode = await HashBackupCodeAsync(code, cancellationToken).ConfigureAwait(false);
                hashedCodes.Add(hashedCode);
            }

            // Store hashed codes
            mfaConfig.BackupCodes = new BackupCodeSet(hashedCodes.Count, hashedCodes).Serialize();
            mfaConfig.UpdatedAt = SystemClock.UtcNow;

            await mfaConfigRepository.UpdateAsync(mfaConfig, cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Backup codes generated for user: {UserId}", userId);

            // Return plain-text codes to user (only shown once)
            return backupCodes.ToArray();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating backup codes for user: {UserId}", userId);

            throw;
        }
    }

    /// <summary>
    ///     Verifies a backup code and invalidates it (single-use).
    /// </summary>
    public async Task<bool> VerifyBackupCodeAsync(Guid userId, string backupCode, string? deviceId = null, CancellationToken cancellationToken = default)
    {
        if (!_mfaOptions.Enabled)
        {
            return false;
        }

        logger.LogInformation("Verifying backup code for user: {UserId}", userId);

        try
        {
            for (var retry = 0; retry < 16; retry++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var mfaConfig = await mfaConfigRepository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);

                if (mfaConfig is not { IsEnabled: true } || string.IsNullOrEmpty(mfaConfig.BackupCodes))
                {
                    logger.LogWarning("No backup codes found for user: {UserId}", userId);
                    await attemptTrackingService.RecordMfaAttemptAsync(userId, MfaMethod.BackupCode, false, "No backup codes", deviceId, cancellationToken).ConfigureAwait(false);

                    return false;
                }

                // Check lockout
                if (attemptTrackingService.IsLockedOut(mfaConfig))
                {
                    logger.LogWarning("User is locked out due to failed MFA attempts: {UserId}", userId);
                    await attemptTrackingService.RecordMfaAttemptAsync(userId, MfaMethod.BackupCode, false, "Account locked", deviceId, cancellationToken).ConfigureAwait(false);

                    return false;
                }

                // Get all backup codes
                var codeSet = BackupCodeSet.Read(mfaConfig.BackupCodes);
                var hashedCodes = codeSet.Hashes;

                // Check if code matches any stored hashed code
                var codeFound = false;
                string? matchedHashedCode = null;

                foreach (var hashedCode in hashedCodes)
                {
                    if (await VerifyBackupCodeHashAsync(backupCode, hashedCode, cancellationToken))
                    {
                        codeFound = true;
                        matchedHashedCode = hashedCode;

                        break;
                    }
                }

                if (codeFound && matchedHashedCode != null)
                {
                    // Remove used code
                    hashedCodes.Remove(matchedHashedCode);
                    mfaConfig.BackupCodes = codeSet.Serialize();
                    mfaConfig.UpdatedAt = SystemClock.UtcNow;

                    // Reset failed attempts
                    mfaConfig.FailedAttempts = 0;
                    mfaConfig.LockedOutUntil = null;
                    mfaConfig.LastUsedAt = SystemClock.UtcNow;

                    try
                    {
                        await mfaConfigRepository.UpdateAsync(mfaConfig, cancellationToken).ConfigureAwait(false);
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        // Repository reloads the security state. Recheck the code and lockout before retrying.
                        continue;
                    }

                    await attemptTrackingService.RecordMfaAttemptAsync(userId, MfaMethod.BackupCode, true, null, deviceId, cancellationToken).ConfigureAwait(false);

                    logger.LogInformation("Backup code verification successful for user: {UserId}, Remaining codes: {RemainingCodes}", userId, hashedCodes.Count);

                    return true;
                }

                await attemptTrackingService.RecordFailedMfaAttemptAsync(
                    mfaConfig,
                    MfaMethod.BackupCode,
                    "Invalid code",
                    deviceId,
                    cancellationToken).ConfigureAwait(false);

                logger.LogWarning("Invalid backup code for user: {UserId}, Failed attempts: {FailedAttempts}", userId, mfaConfig.FailedAttempts);

                return false;
            }

            await attemptTrackingService.RecordMfaAttemptAsync(userId, MfaMethod.BackupCode, false, "Concurrent update limit", deviceId, cancellationToken).ConfigureAwait(false);
            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error verifying backup code for user: {UserId}", userId);
            await attemptTrackingService.RecordMfaAttemptAsync(userId, MfaMethod.BackupCode, false, "System error", deviceId, cancellationToken).ConfigureAwait(false);

            return false;
        }
    }

    /// <summary>
    ///     Generates a cryptographically random alphanumeric backup code.
    /// </summary>
    public string GenerateBackupCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Excludes similar-looking characters
        var code = new char[_mfaOptions.BackupCodeLength];

        for (var i = 0; i < code.Length; i++) { code[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)]; }

        return new string(code);
    }

    /// <summary>
    ///     Hashes a backup code (similar to password hashing).
    /// </summary>
    public Task<string> HashBackupCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        cancellationToken.ThrowIfCancellationRequested();
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(code, salt, 600000, HashAlgorithmName.SHA256, 32);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult($"pbkdf2-sha256$600000${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}");
    }

    /// <summary>
    ///     Verifies a backup code against its hash.
    /// </summary>
    private static Task<bool> VerifyBackupCodeHashAsync(string code, string hash, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(code) || code.Length > 64) { return Task.FromResult(false); }
        try
        {
            byte[] expected;
            byte[] computed;
            if (hash.StartsWith("pbkdf2-sha256$", StringComparison.Ordinal))
            {
                var parts = hash.Split('$');
                if (parts.Length != 4 || parts[1] != "600000") { return Task.FromResult(false); }
                var salt = Convert.FromBase64String(parts[2]);
                expected = Convert.FromBase64String(parts[3]);
                if (salt.Length != 16 || expected.Length != 32) { return Task.FromResult(false); }
                computed = Rfc2898DeriveBytes.Pbkdf2(code, salt, 600000, HashAlgorithmName.SHA256, 32);
            }
            else
            {
                expected = Convert.FromBase64String(hash);
                if (expected.Length != 32) { return Task.FromResult(false); }
                computed = SHA256.HashData(Encoding.UTF8.GetBytes(code));
            }

            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CryptographicOperations.FixedTimeEquals(expected, computed));
        }
        catch (FormatException) { return Task.FromResult(false); }
    }

    /// <summary>
    ///     Stores hashed backup codes for a user during MFA setup (before MFA is fully enabled).
    /// </summary>
    public async Task StoreBackupCodesForSetupAsync(Guid userId, IReadOnlyList<string> plainTextCodes, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Storing backup codes during MFA setup for user: {UserId}", userId);

        var mfaConfig = await mfaConfigRepository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (mfaConfig == null) { return; }

        var hashedCodes = new List<string>();
        foreach (var code in plainTextCodes)
        {
            var hashedCode = await HashBackupCodeAsync(code, cancellationToken).ConfigureAwait(false);
            hashedCodes.Add(hashedCode);
        }

        mfaConfig.BackupCodes = new BackupCodeSet(hashedCodes.Count, hashedCodes).Serialize();
        mfaConfig.UpdatedAt = SystemClock.UtcNow;
        await mfaConfigRepository.UpdateAsync(mfaConfig, cancellationToken).ConfigureAwait(false);
    }
}
