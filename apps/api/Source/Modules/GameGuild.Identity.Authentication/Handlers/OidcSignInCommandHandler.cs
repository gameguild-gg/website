using GameGuild.CQRS;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handles the OidcSignInCommand by delegating to the OIDC federation service
///     to build the provider authorization URL from discovered metadata.
/// </summary>
public sealed class OidcSignInCommandHandler(
    IOidcFederationService oidcFederationService,
    ILogger<OidcSignInCommandHandler> logger
) : ICommandHandler<OidcSignInCommand, OidcSignInResponse>
{
    public async Task<OidcSignInResponse> Handle(OidcSignInCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Initiating OIDC federation sign-in for provider {Slug} with redirect to {RedirectUri}",
            request.Slug, request.RedirectUri);

        var state = Guid.NewGuid().ToString("N");
        var challenge = await oidcFederationService
            .BuildAuthorizationUrlAsync(request.Slug, request.RedirectUri, state, cancellationToken)
            .ConfigureAwait(false);

        return new OidcSignInResponse { AuthUrl = challenge.AuthUrl, State = state };
    }
}
