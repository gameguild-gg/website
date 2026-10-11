using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

public sealed class CompleteMfaSignInCommand : ICommand<SignInResponse>
{
    public string MfaToken { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public MfaMethod Method { get; init; } = MfaMethod.Totp;
    public string? DeviceFingerprint { get; init; }
}
