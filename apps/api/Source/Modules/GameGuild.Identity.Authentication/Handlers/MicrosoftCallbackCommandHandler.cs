using GameGuild.CQRS;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handler for the Microsoft OAuth callback command: exchanges the authorization
///     code for a signed-in session (user resolution, auto-link policy, tenant context).
/// </summary>
public sealed class MicrosoftCallbackCommandHandler(
    IOAuthAuthService oAuthAuthService,
    IUserRepository userRepository,
    ILogger<MicrosoftCallbackCommandHandler> logger,
    FluentValidation.IValidator<MicrosoftCallbackCommand> validator
) : ICommandHandler<MicrosoftCallbackCommand, SignInResponse>
{
    public async Task<SignInResponse> Handle(MicrosoftCallbackCommand command, CancellationToken cancellationToken)
    {
        // Validate command
        var validationResult = await validator.ValidateAsync(command, cancellationToken).ConfigureAwait(false);

        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => new ValidationError(e.PropertyName, e.ErrorMessage));

            throw new RequestValidationException(errors);
        }

        var signInRequest = new MicrosoftCallbackSignInRequest { Code = command.Code, State = command.State, RedirectUri = command.RedirectUri, TenantId = command.TenantId, RememberMe = command.RememberMe };

        var domainResult = await oAuthAuthService.MicrosoftCallbackSignInAsync(signInRequest, cancellationToken).ConfigureAwait(false);

        if (domainResult == null) { throw new InvalidOperationException("Authentication service returned null result"); }

        logger.LogInformation("Microsoft OAuth sign-in successful");

        // Map from Domain response to Application DTO
        return await domainResult.ToDto(userRepository, cancellationToken).ConfigureAwait(false);
    }
}
