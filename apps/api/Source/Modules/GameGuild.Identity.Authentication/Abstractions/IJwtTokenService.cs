using System.Security.Claims;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Service for generating and validating JWT tokens
/// </summary>
public interface IJwtTokenService
{
    // Synchronous methods
    string GenerateAccessToken(Guid userId, string email, string[ ] roles);

    string GenerateAccessToken(Guid userId, string email, string[ ] roles, IEnumerable<Claim> additionalClaims);

    string GenerateRefreshToken();

    ClaimsPrincipal GetPrincipalFromExpiredToken(string token);

    ClaimsPrincipal ValidateToken(string token);

    // Async methods
    Task<string> GenerateAccessTokenAsync(Guid userId, string email, string[ ] roles, Guid? tenantId, int tokenVersion = 1, CancellationToken cancellationToken = default);

    Task<string> GenerateAccessTokenAsync(
        Guid userId,
        string email,
        string[ ] roles,
        Guid? tenantId,
        int tokenVersion,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<string> GenerateAccessTokenAsync(
        Guid userId,
        string email,
        string[ ] roles,
        Guid? tenantId,
        int tokenVersion,
        DateTimeOffset authenticatedAt,
        CancellationToken cancellationToken = default);

    Task<string> GenerateAccessTokenAsync(
        Guid userId,
        string email,
        string[ ] roles,
        Guid? tenantId,
        int tokenVersion,
        DateTimeOffset authenticatedAt,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<string> GenerateRefreshTokenAsync(Guid userId, DeviceInfo deviceInfo, CancellationToken cancellationToken = default);

    Task<string> GenerateRefreshTokenAsync(
        Guid userId,
        DeviceInfo deviceInfo,
        DateTimeOffset authenticatedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Generates and persists a refresh token whose stored deadline is the caller-resolved
    ///     session policy deadline (persistent "remember me" vs standard lifetime).
    /// </summary>
    /// <param name="userId">User the token is issued for.</param>
    /// <param name="deviceInfo">Device the token is bound to.</param>
    /// <param name="authenticatedAt">Authentication instant recorded as the token's creation time.</param>
    /// <param name="expiresAt">Resolved expiration for the persisted token row; null falls back to the standard lifetime.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<string> GenerateRefreshTokenAsync(
        Guid userId,
        DeviceInfo deviceInfo,
        DateTimeOffset authenticatedAt,
        DateTime? expiresAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Generates an access token for a service account (client_credentials flow).
    /// </summary>
    /// <param name="serviceAccountId">The service account ID.</param>
    /// <param name="clientId">The client ID.</param>
    /// <param name="serviceName">The human-readable service name.</param>
    /// <param name="scopes">The granted scopes.</param>
    /// <param name="tenantId">Optional tenant ID the service account belongs to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The access token and expiration time.</returns>
    Task<(string Token, DateTime ExpiresAt)> GenerateServiceAccountTokenAsync(
        string serviceAccountId,
        string clientId,
        string serviceName,
        IReadOnlySet<string> scopes,
        Guid? tenantId,
        CancellationToken cancellationToken = default);
}
