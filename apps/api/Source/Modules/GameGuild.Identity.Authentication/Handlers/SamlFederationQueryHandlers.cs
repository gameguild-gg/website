using GameGuild.CQRS;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handles the service-provider metadata query: emits the SAML 2.0 entity descriptor
///     (entity ID and POST-binding assertion consumer service) for exchange with the
///     identity provider. Unconfigured or disabled providers fail closed.
/// </summary>
public sealed class GetSamlServiceProviderMetadataQueryHandler(
    ISamlFederationService samlFederationService,
    ILogger<GetSamlServiceProviderMetadataQueryHandler> logger
) : IQueryHandler<GetSamlServiceProviderMetadataQuery, SamlMetadataResponse>
{
    public Task<SamlMetadataResponse> Handle(GetSamlServiceProviderMetadataQuery request, CancellationToken cancellationToken)
    {
        var metadataXml = samlFederationService.BuildServiceProviderMetadata(request.Slug);
        logger.LogInformation("SAML service provider metadata served for provider {Slug}", request.Slug);
        return Task.FromResult(new SamlMetadataResponse { MetadataXml = metadataXml });
    }
}

/// <summary>
///     Handles logout forwarding: builds the provider's front-channel single-logout
///     redirect (unsigned LogoutRequest, HTTP-Redirect binding) when the provider is
///     configured with a single logout service; null keeps logout local. Local refresh
///     revocation is unaffected.
/// </summary>
public sealed class GetSamlLogoutUrlQueryHandler(
    ISamlFederationService samlFederationService,
    ILogger<GetSamlLogoutUrlQueryHandler> logger
) : IQueryHandler<GetSamlLogoutUrlQuery, SamlLogoutUrlResponse>
{
    public Task<SamlLogoutUrlResponse> Handle(GetSamlLogoutUrlQuery request, CancellationToken cancellationToken)
    {
        var logoutUrl = samlFederationService.BuildLogoutRedirectUrl(request.Slug, request.PostLogoutRedirectUri);

        if (logoutUrl is null)
        {
            logger.LogInformation("SAML provider has no single logout service; logout stays local");
        }

        return Task.FromResult(new SamlLogoutUrlResponse { LogoutUrl = logoutUrl });
    }
}
