namespace GameGuild.Identity.Authentication;

/// <summary>Password sign-in using one email, username or canonical international phone identifier.</summary>
public sealed class PolymorphicSignInRequest
{
    public string Credential { get; init; } = string.Empty;
    public CredentialType? CredentialType { get; init; }
    public string Password { get; init; } = string.Empty;
    public Guid? TenantId { get; init; }
    public string? DeviceFingerprint { get; init; }

    /// <summary>When true, issues a persistent ("remember me") refresh token using the persistent lifetime.</summary>
    public bool? RememberMe { get; init; }
}
