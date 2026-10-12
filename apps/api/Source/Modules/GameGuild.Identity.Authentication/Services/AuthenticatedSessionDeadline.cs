namespace GameGuild.Identity.Authentication;

/// <summary>Returns the verified persisted lifetime of an issued authentication session.</summary>
internal static class AuthenticatedSessionDeadline
{
    public static DateTime Require(
        UserSession? session,
        Guid userId,
        Guid sessionId,
        string tokenHash,
        DateTime requestedDeadline)
    {
        if (session is null || session.Id != sessionId || session.UserId != userId ||
            !session.IsActive || session.TerminatedAt is not null ||
            session.ExpiresAt <= SystemClock.UtcNow || session.ExpiresAt > requestedDeadline ||
            !string.Equals(session.RefreshToken, tokenHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The refresh credential has no valid persisted session lifetime.");
        }

        return session.ExpiresAt;
    }
}
