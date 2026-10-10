using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Command to handle GitHub OAuth callback and exchange code for tokens
/// </summary>
public class GitHubCallbackCommand : IRequest<SignInResponse>
{
    /// <summary>
    ///     OAuth authorization code from GitHub callback
    /// </summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>
    ///     OAuth state parameter for CSRF protection
    /// </summary>
    public string State { get; init; } = string.Empty;

    /// <summary>
    ///     Optional tenant context
    /// </summary>
    public Guid? TenantId { get; init; }

    /// <summary>
    ///     Device fingerprint for session tracking
    /// </summary>
    public string? DeviceFingerprint { get; init; }

    /// <summary>
    ///     When true, issues a persistent ("remember me") refresh token using the persistent lifetime.
    /// </summary>
    public bool? RememberMe { get; init; }
}
