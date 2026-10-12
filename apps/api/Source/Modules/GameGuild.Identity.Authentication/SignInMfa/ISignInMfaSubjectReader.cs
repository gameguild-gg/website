using GameGuild.Identity.Users;

namespace GameGuild.Identity.Authentication;

/// <summary>Fresh server-owned account and enrollment state, independent of a tracked entity or caller claims.</summary>
public interface ISignInMfaSubjectReader
{
    Task<SignInMfaSubjectState?> ReadCurrentAsync(Guid subjectId, CancellationToken cancellationToken);
}

public sealed record SignInMfaSubjectState(User User, bool HasEnrolledMfa, DateTime? LockedOutUntil);
