using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Command to handle token refresh
/// </summary>
public class RefreshTokenCommand : ICommand<SignInResponse>
{
    /// <summary>
    ///     The refresh token to use for generating new access/refresh tokens
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    ///     Optional tenant ID to generate tenant-specific claims
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    ///     IP address for audit logging
    /// </summary>
    public string? IpAddress { get; set; }
}
