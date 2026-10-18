namespace GameGuild.Identity.Authentication;

/// <summary>All operations require the same PostgreSQL transaction as challenge consumption and issuance.</summary>
public interface ISignInMfaEnrollmentPort
{
    Task AcquireSubjectLockAsync(Guid subjectId, CancellationToken cancellationToken);
    Task<bool> MatchesAsync(SignInMfaChallenge challenge, bool enrolled, DateTimeOffset now,
        CancellationToken cancellationToken);
}
