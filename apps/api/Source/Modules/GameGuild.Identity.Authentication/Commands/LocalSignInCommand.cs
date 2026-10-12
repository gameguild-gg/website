using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Command to handle local user sign-in
/// </summary>
public class LocalSignInCommand : ICommand<SignInResponse>
{
    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public Guid? TenantId { get; init; }

    public string? DeviceFingerprint { get; init; }

    /// <summary>When true, issues a persistent ("remember me") refresh token using the persistent lifetime.</summary>
    public bool? RememberMe { get; init; }
}
