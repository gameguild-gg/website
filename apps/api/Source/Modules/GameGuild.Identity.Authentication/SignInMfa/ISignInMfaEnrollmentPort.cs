namespace GameGuild.Identity.Authentication;

/// <summary>All operations require the same PostgreSQL transaction as challenge consumption and issuance.</summary>
public interface ISignInMfaEnrollmentPort
{
    Task AcquireSubjectLockAsync(Guid subjectId, CancellationToken cancellationToken);
    Task<SignInMfaEnrollmentSetup?> StartOrResumeAsync(SignInMfaChallenge challenge, string email,
        DateTimeOffset now, CancellationToken cancellationToken);
    Task<bool> MatchesAsync(SignInMfaChallenge challenge, bool enrolled, DateTimeOffset now,
        CancellationToken cancellationToken);
}

/// <summary>Internal integration data; only the secret, provisioning URI and expiry are returned for enrollment.</summary>
public sealed record SignInMfaEnrollmentSetup(Guid ConfigurationId, string SecretFingerprint, string SecretKey,
    string QrCodeUri, DateTimeOffset ExpiresAt);
