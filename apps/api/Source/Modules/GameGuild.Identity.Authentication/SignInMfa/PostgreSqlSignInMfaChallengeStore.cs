using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authentication;

public sealed class PostgreSqlSignInMfaChallengeStore(IApplicationDbContext context) : ISignInMfaChallengeStore
{
    private DbSet<SignInMfaChallenge> Challenges => context.Set<SignInMfaChallenge>();

    public async Task AddAsync(SignInMfaChallenge challenge, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(challenge);
        challenge.ValidateForPersistence();
        await Challenges.AddAsync(challenge, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<SignInMfaChallenge?> FindActiveAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        RequireDigest(tokenHash);
        return Challenges.AsNoTracking().SingleOrDefaultAsync(challenge => challenge.TokenHash == tokenHash &&
            challenge.CreatedAt <= now && challenge.ExpiresAt > now &&
            challenge.RevokedAt == null && challenge.ConsumedAt == null, cancellationToken);
    }

    public async Task<bool> TryConsumeAsync(string tokenHash, SignInMfaChallengeBinding binding, MfaMethod method,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireDigest(tokenHash);
        ArgumentNullException.ThrowIfNull(binding);
        binding.Validate();
        if (method is not (MfaMethod.Totp or MfaMethod.BackupCode or MfaMethod.WebAuthn) ||
            binding.Purpose == SignInMfaPurpose.EnrollFactor && method != MfaMethod.Totp)
        {
            throw new ArgumentOutOfRangeException(nameof(method));
        }
        var changed = await Challenges.Where(challenge => challenge.TokenHash == tokenHash &&
                challenge.SubjectId == binding.SubjectId && challenge.TenantId == binding.TenantId &&
                challenge.SubjectTokenVersion == binding.SubjectTokenVersion &&
                challenge.PolicyFingerprint == binding.PolicyFingerprint && challenge.Purpose == binding.Purpose &&
                challenge.CreatedAt <= now && challenge.ExpiresAt > now &&
                challenge.ConsumedAt == null && challenge.RevokedAt == null &&
                (challenge.Purpose == SignInMfaPurpose.VerifyFactor ||
                 challenge.EnrollmentConfigurationId != null && challenge.EnrollmentSecretFingerprint != null && challenge.EnrollmentInitializedAt != null))
            .ExecuteUpdateAsync(setters => setters.SetProperty(challenge => challenge.ConsumedAt, now)
                .SetProperty(challenge => challenge.VerificationMethod, method), cancellationToken).ConfigureAwait(false);
        return changed == 1;
    }

    public async Task<bool> TryBindEnrollmentAsync(string tokenHash, SignInMfaChallengeBinding binding, Guid configurationId,
        string secretFingerprint, DateTimeOffset now, CancellationToken cancellationToken)
    {
        RequireDigest(tokenHash);
        ArgumentNullException.ThrowIfNull(binding);
        binding.Validate();
        RequireDigest(secretFingerprint);
        if (configurationId == Guid.Empty || binding.Purpose != SignInMfaPurpose.EnrollFactor || now.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("A current enrollment binding is required.");
        }
        var changed = await Challenges.Where(challenge => challenge.TokenHash == tokenHash &&
                challenge.SubjectId == binding.SubjectId && challenge.TenantId == binding.TenantId &&
                challenge.SubjectTokenVersion == binding.SubjectTokenVersion && challenge.PolicyFingerprint == binding.PolicyFingerprint &&
                challenge.Purpose == binding.Purpose && challenge.CreatedAt <= now && challenge.ExpiresAt > now &&
                challenge.ConsumedAt == null && challenge.RevokedAt == null && challenge.EnrollmentConfigurationId == null &&
                challenge.EnrollmentSecretFingerprint == null && challenge.EnrollmentInitializedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(challenge => challenge.EnrollmentConfigurationId, configurationId)
                .SetProperty(challenge => challenge.EnrollmentSecretFingerprint, secretFingerprint)
                .SetProperty(challenge => challenge.EnrollmentInitializedAt, now), cancellationToken).ConfigureAwait(false);
        return changed == 1;
    }

    public Task<int> RevokeSubjectAsync(Guid subjectId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (subjectId == Guid.Empty) { throw new ArgumentException("A subject is required.", nameof(subjectId)); }
        return Challenges.Where(challenge => challenge.SubjectId == subjectId && challenge.ConsumedAt == null &&
                challenge.RevokedAt == null && challenge.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(challenge => challenge.RevokedAt, now), cancellationToken);
    }

    private static void RequireDigest(string tokenHash)
    {
        if (!SignInMfaChallengeToken.IsDigest(tokenHash)) { throw new ArgumentException("Invalid challenge digest.", nameof(tokenHash)); }
    }
}
