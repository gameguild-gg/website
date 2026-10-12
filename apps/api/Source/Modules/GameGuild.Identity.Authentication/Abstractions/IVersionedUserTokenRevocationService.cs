namespace GameGuild.Identity.Authentication;

/// <summary>Records a user cutoff with the persisted token version reached by the same revocation operation.</summary>
public interface IVersionedUserTokenRevocationService
{
    Task RevokeAllUserTokensAsync(Guid userId, int minimumTokenVersion, string? reason, CancellationToken cancellationToken);

    Task<bool> IsUserTokenRevokedAsync(Guid userId, DateTime tokenIssuedAt, int? tokenVersion, CancellationToken cancellationToken);
}

internal sealed record UserTokenRevocationBoundary(DateTime RevokedAt, int? MinimumTokenVersion = null)
{
    public bool IsRevoked(DateTime issuedAt, int? tokenVersion)
    {
        var currentVersion = MinimumTokenVersion is > 0 && tokenVersion.HasValue && tokenVersion.Value >= MinimumTokenVersion.Value;
        return issuedAt.ToUniversalTime() < RevokedAt.ToUniversalTime() && !currentVersion;
    }
}
