using GameGuild.CQRS;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handler for local sign-up command
/// </summary>
public sealed class LocalSignUpHandler(IAuthService authService, IUserRepository userRepository, ILogger<LocalSignUpHandler> logger) : ICommandHandler<LocalSignUpCommand, SignInResponse>
{
    public async Task<SignInResponse> Handle(LocalSignUpCommand command, CancellationToken cancellationToken)
    {
        var signUpRequest = new LocalSignUpRequest
        {
            Email = command.Email, Password = command.Password, Username = command.Username, TenantId = command.TenantId, FirstName = command.FirstName, LastName = command.LastName, PhoneNumber = command.PhoneNumber
        };

        var domainResult = await authService.LocalSignUpAsync(signUpRequest, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("User successfully signed up via local authentication");

        // Map from Domain response to Application DTO
        return await domainResult.ToDto(userRepository, cancellationToken).ConfigureAwait(false);
    }
}
