namespace GameGuild.Identity.Authentication;

/// <summary>Bounded retention writes for the trusted background cleanup operation.</summary>
public interface IRefreshTokenCleanupRepository
{
    Task<int> DeleteExpiredAndRevokedBatchAsync(DateTime cutoffUtc, int batchSize, CancellationToken cancellationToken);
}

/// <summary>Retains sessions referenced by any surviving refresh token.</summary>
public interface IUserSessionCleanupRepository
{
    Task<int> DeleteRetainedSessionBatchAsync(DateTime cutoffUtc, int batchSize, CancellationToken cancellationToken);
}
