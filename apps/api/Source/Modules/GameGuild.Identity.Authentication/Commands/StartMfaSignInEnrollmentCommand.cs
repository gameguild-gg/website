using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

public sealed class StartMfaSignInEnrollmentCommand : ICommand<MfaSignInEnrollmentResponse>
{
    public string MfaToken { get; init; } = string.Empty;
}
