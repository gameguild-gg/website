using GameGuild.CQRS;
using Microsoft.AspNetCore.Http;

namespace GameGuild.Identity.Authentication;

public sealed class StartMfaSignInEnrollmentHandler(ISignInMfaService signInMfa, IHttpContextAccessor accessor)
    : ICommandHandler<StartMfaSignInEnrollmentCommand, MfaSignInEnrollmentResponse>
{
    public Task<MfaSignInEnrollmentResponse> Handle(StartMfaSignInEnrollmentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var context = accessor.HttpContext;
        return signInMfa.StartEnrollmentAsync(command.MfaToken, new DeviceInfo
        {
            IpAddress = context?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = context?.Request.Headers.UserAgent.ToString()
        }, cancellationToken);
    }
}
