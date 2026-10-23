using FluentValidation;
using GameGuild.CQRS;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handler for the SAML federation callback command (Assertion Consumer Service):
///     validates the posted SAMLResponse (provider signature, issuer, audience,
///     destination, lifetimes, correlation, replay), then completes sign-in with the
///     shared external-identity auto-link/JIT policy, fail-closed MFA policy, and the
///     provider's tenant allowlist.
/// </summary>
public sealed class SamlCallbackCommandHandler(
    IOAuthAuthService oAuthAuthService,
    IUserRepository userRepository,
    ILogger<SamlCallbackCommandHandler> logger,
    IValidator<SamlCallbackCommand> validator
) : ICommandHandler<SamlCallbackCommand, SignInResponse>
{
    public async Task<SignInResponse> Handle(SamlCallbackCommand command, CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(command, cancellationToken).ConfigureAwait(false);

        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => new ValidationError(e.PropertyName, e.ErrorMessage));

            throw new RequestValidationException(errors);
        }

        var signInRequest = new SamlSignInRequest
        {
            Slug = command.Slug,
            SamlResponse = command.SamlResponse,
            RelayState = command.RelayState,
            TenantId = command.TenantId,
            RememberMe = command.RememberMe
        };

        var domainResult = await oAuthAuthService.SamlSignInAsync(signInRequest, cancellationToken).ConfigureAwait(false);

        if (domainResult == null) { throw new InvalidOperationException("Authentication service returned null result"); }

        logger.LogInformation("SAML federation sign-in successful for provider {Slug}", command.Slug);

        return await domainResult.ToDto(userRepository, cancellationToken).ConfigureAwait(false);
    }
}
