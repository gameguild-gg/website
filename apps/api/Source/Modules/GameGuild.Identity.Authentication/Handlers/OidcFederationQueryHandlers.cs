using GameGuild.CQRS;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handles domain-to-provider discovery for the login page: resolves the configured
///     federation providers serving the requested email domain.
/// </summary>
public sealed class DiscoverOidcProvidersQueryHandler(
    IOidcFederationService oidcFederationService,
    ILogger<DiscoverOidcProvidersQueryHandler> logger
) : IQueryHandler<DiscoverOidcProvidersQuery, OidcDiscoverProviderResponse>
{
    public Task<OidcDiscoverProviderResponse> Handle(DiscoverOidcProvidersQuery request, CancellationToken cancellationToken)
    {
        var domain = ExtractDomain(request.Email);
        var providers = oidcFederationService.FindProvidersForEmailDomain(domain);

        logger.LogInformation("OIDC domain discovery matched {Count} provider(s) for domain {Domain}", providers.Count, domain);

        return Task.FromResult(new OidcDiscoverProviderResponse { Providers = providers });
    }

    private static string ExtractDomain(string email)
    {
        var trimmed = email.Trim();
        var atIndex = trimmed.LastIndexOf('@');
        return atIndex >= 0 && atIndex < trimmed.Length - 1 ? trimmed[(atIndex + 1)..] : trimmed;
    }
}

/// <summary>
///     Handles logout forwarding: resolves the provider's discovered end_session_endpoint and
///     appends the caller's post-logout redirect. Local refresh revocation is unaffected.
/// </summary>
public sealed class GetOidcEndSessionUrlQueryHandler(
    IOidcFederationService oidcFederationService,
    ILogger<GetOidcEndSessionUrlQueryHandler> logger
) : IQueryHandler<GetOidcEndSessionUrlQuery, OidcEndSessionUrlResponse>
{
    public async Task<OidcEndSessionUrlResponse> Handle(GetOidcEndSessionUrlQuery request, CancellationToken cancellationToken)
    {
        var endSessionEndpoint = await oidcFederationService
            .GetEndSessionEndpointAsync(request.Slug, cancellationToken)
            .ConfigureAwait(false);

        if (endSessionEndpoint is null)
        {
            logger.LogInformation("OIDC provider {Slug} does not advertise an end_session_endpoint; logout stays local", request.Slug);
            return new OidcEndSessionUrlResponse { EndSessionUrl = null };
        }

        var url = string.IsNullOrWhiteSpace(request.PostLogoutRedirectUri)
            ? endSessionEndpoint
            : $"{endSessionEndpoint}?post_logout_redirect_uri={Uri.EscapeDataString(request.PostLogoutRedirectUri)}";

        return new OidcEndSessionUrlResponse { EndSessionUrl = url };
    }
}
