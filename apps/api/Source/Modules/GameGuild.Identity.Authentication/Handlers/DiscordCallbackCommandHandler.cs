using GameGuild.CQRS;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handler for the Discord OAuth callback command: exchanges the authorization
///     code for a signed-in session (user resolution, auto-link policy, tenant context).
/// </summary>
public sealed class DiscordCallbackCommandHandler(
    IOAuthAuthService oAuthAuthService,
    IUserRepository userRepository,
    ILogger<DiscordCallbackCommandHandler> logger,
    FluentValidation.IValidator<DiscordCallbackCommand> validator
) : ICommandHandler<DiscordCallbackCommand, SignInResponse>
{
    public async Task<SignInResponse> Handle(DiscordCallbackCommand command, CancellationToken cancellationToken)
    {
        // Validate command
        var validationResult = await validator.ValidateAsync(command, cancellationToken).ConfigureAwait(false);

        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => new ValidationError(e.PropertyName, e.ErrorMessage));

            throw new RequestValidationException(errors);
        }

        var signInRequest = new DiscordSignInRequest { Code = command.Code, State = command.State, RedirectUri = command.RedirectUri, TenantId = command.TenantId };

        var domainResult = await oAuthAuthService.DiscordSignInAsync(signInRequest, cancellationToken).ConfigureAwait(false);

        if (domainResult == null) { throw new InvalidOperationException("Authentication service returned null result"); }

        logger.LogInformation("Discord OAuth sign-in successful");

        // Map from Domain response to Application DTO
        return await domainResult.ToDto(userRepository, cancellationToken).ConfigureAwait(false);
    }
}
