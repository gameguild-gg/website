using FluentValidation;
using GameGuild.CQRS;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handler for the OIDC federation callback command: exchanges the authorization
///     code for a signed-in session (ID-token validation, user resolution, auto-link/JIT
///     policy, fail-closed MFA policy, tenant context).
/// </summary>
public sealed class OidcCallbackCommandHandler(
    IOAuthAuthService oAuthAuthService,
    IUserRepository userRepository,
    ILogger<OidcCallbackCommandHandler> logger,
    IValidator<OidcCallbackCommand> validator
) : ICommandHandler<OidcCallbackCommand, SignInResponse>
{
    public async Task<SignInResponse> Handle(OidcCallbackCommand command, CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(command, cancellationToken).ConfigureAwait(false);

        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => new ValidationError(e.PropertyName, e.ErrorMessage));

            throw new RequestValidationException(errors);
        }

        var signInRequest = new OidcSignInRequest
        {
            Slug = command.Slug,
            Code = command.Code,
            State = command.State,
            RedirectUri = command.RedirectUri,
            TenantId = command.TenantId,
            RememberMe = command.RememberMe
        };

        var domainResult = await oAuthAuthService.OidcSignInAsync(signInRequest, cancellationToken).ConfigureAwait(false);

        if (domainResult == null) { throw new InvalidOperationException("Authentication service returned null result"); }

        logger.LogInformation("OIDC federation sign-in successful for provider {Slug}", command.Slug);

        return await domainResult.ToDto(userRepository, cancellationToken).ConfigureAwait(false);
    }
}
