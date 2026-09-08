using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Command to handle token revocation
/// </summary>
public class RevokeTokenCommand : ICommand
{
    /// <summary>
    ///     The refresh token to revoke
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    ///     IP address for audit logging
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    ///     User ID initiating the revocation
    /// </summary>
    public Guid? UserId { get; set; }
}
