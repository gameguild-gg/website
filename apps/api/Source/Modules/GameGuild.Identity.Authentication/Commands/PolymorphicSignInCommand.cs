using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Command for polymorphic sign-in supporting multiple credential types (email, phone, username)
/// </summary>
public class PolymorphicSignInCommand : ICommand<SignInResponse>
{
    /// <summary>
    ///     The credential identifier - can be email, phone number, or username
    /// </summary>
    public string Credential { get; init; } = string.Empty;

    /// <summary>
    ///     The credential type for explicit specification (optional, auto-detected if not provided)
    /// </summary>
    public CredentialType? CredentialType { get; init; }

    /// <summary>
    ///     Password for authentication
    /// </summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>
    ///     Optional tenant context
    /// </summary>
    public Guid? TenantId { get; init; }

    /// <summary>
    ///     Optional device fingerprint for trusted device tracking
    /// </summary>
    public string? DeviceFingerprint { get; init; }

    /// <summary>
    ///     When true, issues a persistent ("remember me") refresh token using the persistent lifetime.
    /// </summary>
    public bool? RememberMe { get; init; }
}
