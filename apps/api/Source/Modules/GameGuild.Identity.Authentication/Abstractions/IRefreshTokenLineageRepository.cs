namespace GameGuild.Identity.Authentication;

/// <summary>Persists ownership-checked links between refresh credentials and authentication sessions.</summary>
public interface IRefreshTokenLineageRepository
{
    /// <summary>Returns false only for a session without a corresponding stored refresh credential.</summary>
    Task<bool> BindSessionAsync(Guid userId, string tokenHash, Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Records the exact predecessor after its one-time rotation claim succeeded.</summary>
    Task RecordRotationAsync(Guid userId, Guid parentTokenId, string replacementTokenHash, Guid sessionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Terminates the persisted owned family and revokes its credentials. Null means
    /// no family can be proven; the caller must retain account containment.
    /// </summary>
    Task<Guid?> RevokeFamilyAsync(Guid userId, Guid tokenId, string? revokedByIp, CancellationToken cancellationToken);
}
