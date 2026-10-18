namespace GameGuild.Identity.Authentication;

public interface ISignInMfaChallengeStore
{
    Task AddAsync(SignInMfaChallenge challenge, CancellationToken cancellationToken);
    Task<SignInMfaChallenge?> FindActiveAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken);
    /// <summary>Call only after actual MFA verification; consume and session issuance share the command transaction.</summary>
    Task<bool> TryConsumeAsync(string tokenHash, SignInMfaChallengeBinding binding, MfaMethod method,
        DateTimeOffset now, CancellationToken cancellationToken);
    Task<int> RevokeSubjectAsync(Guid subjectId, DateTimeOffset now, CancellationToken cancellationToken);
}
