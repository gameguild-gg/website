using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Configuration.ApplicationLayer;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authentication;

/// <summary>Limited enrollment uses the shared command transaction and existing TOTP provider.</summary>
public sealed class PostgreSqlSignInMfaEnrollmentPort(IApplicationDbContext context, ITotpMfaService totp,
    IEncryptionService encryption, MfaOptions options) : ISignInMfaEnrollmentPort
{
    private DbContext DatabaseContext => context is DbContext database &&
        database.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL" &&
        database.Database.CurrentTransaction is not null
        ? database : throw new InvalidOperationException("Limited MFA enrollment requires the owning PostgreSQL transaction.");

    public async Task AcquireSubjectLockAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        if (subjectId == Guid.Empty) { throw new ArgumentException("A subject is required.", nameof(subjectId)); }
        var database = DatabaseContext;
        var key = BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes("mfa-enrollment:" + subjectId.ToString("N"))));
        await database.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", cancellationToken).ConfigureAwait(false);
        // Existing MFA mutation paths also update this row; retain its lock through proof and credential commit.
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT 1 FROM "gameguild.authentication"."user_mfa_configuration"
            WHERE user_id = {subjectId} FOR UPDATE
            """, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SignInMfaEnrollmentSetup?> StartOrResumeAsync(SignInMfaChallenge challenge, string email,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        _ = DatabaseContext;
        if (!options.Enabled || challenge.Purpose != SignInMfaPurpose.EnrollFactor || challenge.ExpiresAt <= now) { return null; }
        if (challenge.EnrollmentConfigurationId.HasValue)
        {
            var existing = await ReadConfigurationAsync(challenge.SubjectId, cancellationToken).ConfigureAwait(false);
            if (!Matches(challenge, existing, enrolled: false, now)) { return null; }
            var secret = encryption.Decrypt(existing!.TotpSecretKey!);
            return new(existing.Id, Fingerprint(secret), secret,
                TotpMfaService.GenerateTotpUri(email, secret, options.TotpIssuer, options.TotpTimeStepSeconds),
                new DateTimeOffset(existing.SetupExpiresAt!.Value, TimeSpan.Zero));
        }
        if (await context.Set<SignInMfaChallenge>().AsNoTracking().AnyAsync(row =>
                row.SubjectId == challenge.SubjectId && row.Id != challenge.Id && row.Purpose == SignInMfaPurpose.EnrollFactor &&
                row.EnrollmentConfigurationId != null && row.ExpiresAt > now && row.CreatedAt <= now &&
                row.ConsumedAt == null && row.RevokedAt == null, cancellationToken).ConfigureAwait(false)) { return null; }
        var before = await ReadConfigurationAsync(challenge.SubjectId, cancellationToken).ConfigureAwait(false);
        if (before is { IsEnabled: true } || before?.LockedOutUntil > now.UtcDateTime) { return null; }
        var setup = await totp.SetupTotpAsync(challenge.SubjectId, email, cancellationToken).ConfigureAwait(false);
        var configuration = await context.Set<UserMfaConfiguration>().SingleAsync(row => row.UserId == challenge.SubjectId,
            cancellationToken).ConfigureAwait(false);
        if (configuration.IsEnabled || configuration.IsSetupComplete || configuration.SetupExpiresAt is not { } expires ||
            expires <= now.UtcDateTime || string.IsNullOrWhiteSpace(configuration.TotpSecretKey) ||
            !string.Equals(Fingerprint(encryption.Decrypt(configuration.TotpSecretKey)), Fingerprint(setup.SecretKey), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The TOTP provider did not establish a limited enrollment.");
        }
        configuration.SetupExpiresAt = expires < challenge.ExpiresAt.UtcDateTime ? expires : challenge.ExpiresAt.UtcDateTime;
        // Recovery codes are created only after the bound TOTP proof succeeds.
        configuration.BackupCodes = null;
        configuration.QrCodeSetupData = null;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new(configuration.Id, Fingerprint(setup.SecretKey), setup.SecretKey, setup.QrCodeUri,
            new DateTimeOffset(configuration.SetupExpiresAt.Value, TimeSpan.Zero));
    }

    public async Task<bool> MatchesAsync(SignInMfaChallenge challenge, bool enrolled, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        _ = DatabaseContext;
        return Matches(challenge, await ReadConfigurationAsync(challenge.SubjectId, cancellationToken).ConfigureAwait(false), enrolled, now);
    }

    private Task<UserMfaConfiguration?> ReadConfigurationAsync(Guid subjectId, CancellationToken cancellationToken) =>
        context.Set<UserMfaConfiguration>().AsNoTracking().SingleOrDefaultAsync(row => row.UserId == subjectId, cancellationToken);

    private bool Matches(SignInMfaChallenge challenge, UserMfaConfiguration? configuration, bool enrolled, DateTimeOffset now)
    {
        if (!options.Enabled || challenge.Purpose != SignInMfaPurpose.EnrollFactor || challenge.ExpiresAt <= now ||
            challenge.EnrollmentConfigurationId is not { } configurationId || configurationId == Guid.Empty ||
            challenge.EnrollmentInitializedAt is not { } initialized || initialized < challenge.CreatedAt ||
            initialized > now || initialized >= challenge.ExpiresAt ||
            !SignInMfaChallengeToken.IsDigest(challenge.EnrollmentSecretFingerprint ?? string.Empty) ||
            configuration is null || configuration.Id != configurationId ||
            configuration.IsEnabled != enrolled || configuration.IsSetupComplete != enrolled ||
            configuration.LockedOutUntil is { } lockedUntil && lockedUntil > now.UtcDateTime ||
            string.IsNullOrWhiteSpace(configuration.TotpSecretKey)) { return false; }
        if (!enrolled && (configuration.SetupExpiresAt is not { } expires || expires <= now.UtcDateTime ||
            expires > challenge.ExpiresAt.UtcDateTime)) { return false; }
        return string.Equals(Fingerprint(encryption.Decrypt(configuration.TotpSecretKey)),
            challenge.EnrollmentSecretFingerprint, StringComparison.Ordinal);
    }

    internal static string Fingerprint(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(
        secret.ToUpperInvariant().Replace(" ", "").Replace("-", "")))).ToLowerInvariant();
}
