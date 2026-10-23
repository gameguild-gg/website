using GameGuild.CQRS;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handles the SamlSignInCommand by delegating to the SAML federation service to
///     issue an AuthnRequest and build the provider redirect URL (HTTP-Redirect binding).
/// </summary>
public sealed class SamlSignInCommandHandler(
    ISamlFederationService samlFederationService,
    ILogger<SamlSignInCommandHandler> logger
) : ICommandHandler<SamlSignInCommand, SamlSignInResponse>
{
    public async Task<SamlSignInResponse> Handle(SamlSignInCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Initiating SAML federation sign-in for provider {Slug}", request.Slug);

        var challenge = await samlFederationService
            .BuildSignInRedirectAsync(request.Slug, request.RelayState, cancellationToken)
            .ConfigureAwait(false);

        return new SamlSignInResponse { RedirectUrl = challenge.RedirectUrl, RelayState = challenge.RelayState };
    }
}
