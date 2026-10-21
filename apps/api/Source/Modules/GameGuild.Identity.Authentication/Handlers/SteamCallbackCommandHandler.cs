using GameGuild.CQRS;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handler for the Steam OpenID callback command: verifies the signed assertion
///     with Steam and completes sign-in (user resolution, auto-link policy, tenant context).
/// </summary>
public sealed class SteamCallbackCommandHandler(
    IOAuthAuthService oAuthAuthService,
    IUserRepository userRepository,
    ILogger<SteamCallbackCommandHandler> logger,
    FluentValidation.IValidator<SteamCallbackCommand> validator
) : ICommandHandler<SteamCallbackCommand, SignInResponse>
{
    public async Task<SignInResponse> Handle(SteamCallbackCommand command, CancellationToken cancellationToken)
    {
        // Validate command
        var validationResult = await validator.ValidateAsync(command, cancellationToken).ConfigureAwait(false);

        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => new ValidationError(e.PropertyName, e.ErrorMessage));

            throw new RequestValidationException(errors);
        }

        var signInRequest = new SteamCallbackSignInRequest { Code = command.Code, State = command.State, RedirectUri = command.RedirectUri, TenantId = command.TenantId, RememberMe = command.RememberMe };

        var domainResult = await oAuthAuthService.SteamCallbackSignInAsync(signInRequest, cancellationToken).ConfigureAwait(false);

        if (domainResult == null) { throw new InvalidOperationException("Authentication service returned null result"); }

        logger.LogInformation("Steam OpenID sign-in successful");

        // Map from Domain response to Application DTO
        return await domainResult.ToDto(userRepository, cancellationToken).ConfigureAwait(false);
    }
}
