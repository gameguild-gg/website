using System.ComponentModel.DataAnnotations;

namespace GameGuild.Identity.Authentication;

/// <summary>Completes the bound, expiring sign-in challenge. The server supplies identity and tenant.</summary>
public sealed class CompleteMfaSignInRequest
{
    [Required, StringLength(43, MinimumLength = 43)]
    public string MfaToken { get; init; } = string.Empty;

    [Required, StringLength(64)]
    public string Code { get; init; } = string.Empty;

    public MfaMethod Method { get; init; } = MfaMethod.Totp;

    [StringLength(64)]
    public string? DeviceFingerprint { get; init; }
}
