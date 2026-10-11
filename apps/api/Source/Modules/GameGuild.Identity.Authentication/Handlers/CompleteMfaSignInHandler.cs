using GameGuild.CQRS;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Http;

namespace GameGuild.Identity.Authentication;

public sealed class CompleteMfaSignInHandler(ISignInMfaService signInMfa, IUserRepository users,
    IHttpContextAccessor httpContextAccessor) : ICommandHandler<CompleteMfaSignInCommand, SignInResponse>
{
    public async Task<SignInResponse> Handle(CompleteMfaSignInCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var context = httpContextAccessor.HttpContext;
        var device = new DeviceInfo
        {
            Fingerprint = command.DeviceFingerprint ?? string.Empty,
            IpAddress = context?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = context?.Request.Headers.UserAgent.ToString()
        };
        var result = await signInMfa.CompleteCodeAsync(command.MfaToken, command.Code, command.Method,
            device, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        // Preserve the server-only failure marker so the command owner retains failed-attempt accounting.
        if (!result.Success) { return result; }
        return await result.ToDto(users, cancellationToken).ConfigureAwait(false);
    }
}
