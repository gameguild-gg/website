namespace GameGuild.Identity.Authentication;

/// <summary>Coordinates a persisted, single-use TOTP step and its successful verification effects.</summary>
public interface ITotpReplayStore
{
    Task<bool> TryAcceptAsync(Guid configurationId, string secretFingerprint, long matchedStep,
        DateTimeOffset acceptedAt, Func<CancellationToken, Task> persistSuccessfulVerification) =>
        TryAcceptAsync(configurationId, secretFingerprint, matchedStep, acceptedAt, persistSuccessfulVerification, CancellationToken.None);

    /// <summary>
    /// Accepts only a step newer than the enrollment's persisted watermark. The successful effects
    /// execute in the same database transaction; an existing caller transaction remains caller-owned.
    /// </summary>
    Task<bool> TryAcceptAsync(Guid configurationId, string secretFingerprint, long matchedStep,
        DateTimeOffset acceptedAt, Func<CancellationToken, Task> persistSuccessfulVerification,
        CancellationToken cancellationToken);
}
