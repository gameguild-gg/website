using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Command to handle Google ID token sign-in (for NextAuth.js integration)
/// </summary>
public class GoogleIdTokenSignInCommand : ICommand<SignInResponse>
{
    public string IdToken { get; set; } = string.Empty;

    public Guid? TenantId { get; set; }

    public string? DeviceFingerprint { get; set; }
}
