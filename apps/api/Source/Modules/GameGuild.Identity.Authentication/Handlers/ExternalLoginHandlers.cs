using GameGuild.CQRS;
using GameGuild.Identity.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

public sealed class GetExternalLoginsQueryHandler(IExternalLoginRepository externalLoginRepository)
    : IQueryHandler<GetExternalLoginsQuery, List<ExternalLoginDto>>
{
    public async Task<List<ExternalLoginDto>> Handle(GetExternalLoginsQuery request, CancellationToken cancellationToken)
    {
        var logins = await externalLoginRepository.GetByUserIdAsync(request.UserId, cancellationToken).ConfigureAwait(false);

        return logins
            .Select(l => new ExternalLoginDto
            {
                Provider = l.Provider,
                CreatedAt = l.CreatedAt,
                GrantedScopes = ExternalLoginGrants.Deserialize(l.GrantedScopes),
                ConsentedAt = l.ConsentedAt,
                ConsentVersion = l.ConsentVersion
            })
            .ToList();
    }
}

public sealed class GetExternalLoginLinkPreviewQueryHandler(
    IOAuthService oAuthService
) : IQueryHandler<GetExternalLoginLinkPreviewQuery, ExternalLoginLinkPreviewResponse>
{
    private static readonly HashSet<string> LinkableProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "google",
        "discord",
        "github",
        "microsoft"
    };

    public Task<ExternalLoginLinkPreviewResponse> Handle(GetExternalLoginLinkPreviewQuery request, CancellationToken cancellationToken)
    {
        if (!LinkableProviders.Contains(request.Provider))
        {
            throw new NotSupportedException($"Provider not supported for link preview: {request.Provider}");
        }

        // The exact scope list the authorization request will carry — the same resolution
        // GetAuthorizationUrlAsync embeds in the URL, shown to the user before continuing.
        var requestedScopes = oAuthService.ResolveAuthorizationScopes(request.Provider);

        return Task.FromResult(new ExternalLoginLinkPreviewResponse
        {
            Provider = request.Provider.ToLowerInvariant(),
            RequestedScopes = requestedScopes
        });
    }
}

public sealed class LinkGoogleAccountCommandHandler(
    IGoogleIdTokenVerifier googleIdTokenVerifier,
    IExternalLoginRepository externalLoginRepository,
    IOAuthService oAuthService,
    ILogger<LinkGoogleAccountCommandHandler> logger
) : ICommandHandler<LinkGoogleAccountCommand>
{
    public async Task<Unit> Handle(LinkGoogleAccountCommand request, CancellationToken cancellationToken)
    {
        var verified = await googleIdTokenVerifier.VerifyAsync(request.IdToken, cancellationToken).ConfigureAwait(false);

        // GIS implicit-flow grants are configured on the client; record the server-resolved
        // scope set (configured or Google safe defaults) as the granted scopes.
        var grantedScopes = oAuthService.ResolveAuthorizationScopes("google");

        await ExternalLoginLinking.LinkAsync(externalLoginRepository, "google", verified.Sub, request.UserId, grantedScopes, logger, cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}

public sealed class DiscordLinkAuthorizeCommandHandler(
    IOAuthService oAuthService,
    ILogger<DiscordLinkAuthorizeCommandHandler> logger
) : ICommandHandler<DiscordLinkAuthorizeCommand, DiscordLinkAuthorizeResponse>
{
    public async Task<DiscordLinkAuthorizeResponse> Handle(DiscordLinkAuthorizeCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Initiating Discord OAuth link flow with redirect to {RedirectUri}", request.RedirectUri);

        var state = Guid.NewGuid().ToString("N");
        var authUrl = await oAuthService.GetAuthorizationUrlAsync(
            "discord",
            request.RedirectUri,
            state
        ).ConfigureAwait(false);

        return new DiscordLinkAuthorizeResponse { AuthUrl = authUrl, State = state };
    }
}

public sealed class LinkDiscordAccountCommandHandler(
    IOAuthService oAuthService,
    IExternalLoginRepository externalLoginRepository,
    ILogger<LinkDiscordAccountCommandHandler> logger
) : ICommandHandler<LinkDiscordAccountCommand>
{
    public async Task<Unit> Handle(LinkDiscordAccountCommand request, CancellationToken cancellationToken)
    {
        var profile = await oAuthService.HandleCallbackAsync("discord", request.Code, request.State, request.RedirectUri).ConfigureAwait(false);

        // The authorization request that produced this code asked for exactly the scopes
        // ResolveAuthorizationScopes returns (the same list the authorize step embedded in
        // the URL) — persist that as the granted scope set.
        var grantedScopes = oAuthService.ResolveAuthorizationScopes("discord");

        await ExternalLoginLinking.LinkAsync(externalLoginRepository, "discord", profile.ProviderId, request.UserId, grantedScopes, logger, cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}

public sealed class UnlinkExternalLoginCommandHandler(
    IExternalLoginRepository externalLoginRepository,
    IUserRepository userRepository
) : ICommandHandler<UnlinkExternalLoginCommand>
{
    public async Task<Unit> Handle(UnlinkExternalLoginCommand request, CancellationToken cancellationToken)
    {
        var logins = await externalLoginRepository.GetByUserIdAsync(request.UserId, cancellationToken).ConfigureAwait(false);

        if (logins.All(l => l.Provider != request.Provider))
        {
            throw new ExternalLoginNotFoundException($"No {request.Provider} login linked to this account");
        }

        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken).ConfigureAwait(false);

        if (user?.PasswordHash is null && logins.Count == 1)
        {
            throw new LastSignInMethodException("Cannot remove the last sign-in method");
        }

        await externalLoginRepository.DeleteAsync(request.Provider, request.UserId, cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}

/// <summary>
///     Revokes individual scope grants on a linked provider. Whole-provider unlink remains
///     the full revocation path (DELETE /external-logins/{provider}); this command only
///     narrows the recorded <see cref="ExternalLogin.GrantedScopes" /> and never removes
///     the link itself. Audit: structured log line with user, provider, revoked scopes,
///     and the remaining set.
/// </summary>
public sealed class RevokeExternalLoginScopesCommandHandler(
    IExternalLoginRepository externalLoginRepository,
    ILogger<RevokeExternalLoginScopesCommandHandler> logger
) : ICommandHandler<RevokeExternalLoginScopesCommand, RevokeExternalLoginScopesResponse>
{
    public async Task<RevokeExternalLoginScopesResponse> Handle(RevokeExternalLoginScopesCommand request, CancellationToken cancellationToken)
    {
        var invalidScope = request.Scopes.FirstOrDefault(scope => !ExternalLoginGrants.IsValidScopeToken(scope));
        if (invalidScope is not null)
        {
            throw new InvalidOAuthScopeException($"'{invalidScope}' is not a valid OAuth scope token");
        }

        var logins = await externalLoginRepository.GetByUserIdAsync(request.UserId, cancellationToken).ConfigureAwait(false);
        var link = logins.FirstOrDefault(l => l.Provider.Equals(request.Provider, StringComparison.OrdinalIgnoreCase));

        if (link is null)
        {
            throw new ExternalLoginNotFoundException($"No {request.Provider} login linked to this account");
        }

        var current = ExternalLoginGrants.Deserialize(link.GrantedScopes);
        var revokeSet = new HashSet<string>(request.Scopes, StringComparer.OrdinalIgnoreCase);
        var remaining = current.Where(scope => !revokeSet.Contains(scope)).ToList();

        if (!ExternalLoginGrants.SameScopeSet(current, remaining))
        {
            await externalLoginRepository.UpdateGrantedScopesAsync(link.Provider, request.UserId, remaining, cancellationToken).ConfigureAwait(false);

            logger.LogInformation(
                "OAuth scope grants revoked for user {UserId} on provider {Provider}: revoked {RevokedScopes}, remaining {RemainingScopes}",
                request.UserId,
                link.Provider,
                request.Scopes,
                remaining);
        }

        return new RevokeExternalLoginScopesResponse
        {
            Provider = link.Provider,
            GrantedScopes = remaining,
            ConsentedAt = link.ConsentedAt,
            ConsentVersion = link.ConsentVersion
        };
    }
}

/// <summary>
///     Shared three-way link rule for authenticated account linking (plan B1):
///     no existing row → insert; same user → idempotent no-op; different user → 409 conflict.
///     The insert uses insert-only AddAsync — UpsertAsync is FORBIDDEN here because its internal
///     read-then-update path silently reassigns row ownership when a concurrent request committed
///     the same (Provider, ProviderKey) between our pre-check and the write.
///     Scope grants (issue #250): a fresh insert records the granted scopes with a consent stamp;
///     a same-user re-link re-records consent only when the granted scope set changed (or the
///     legacy row never had one), so unchanged re-authorizations keep the first-consent stamp.
/// </summary>
internal static class ExternalLoginLinking
{
    public static async Task LinkAsync(
        IExternalLoginRepository externalLoginRepository,
        string provider,
        string providerKey,
        Guid userId,
        string[] grantedScopes,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var existing = await externalLoginRepository.GetByProviderKeyAsync(provider, providerKey, cancellationToken).ConfigureAwait(false);

        if (existing == null)
        {
            try
            {
                await externalLoginRepository.AddAsync(
                    new ExternalLogin
                    {
                        UserId = userId,
                        Provider = provider,
                        ProviderKey = providerKey,
                        GrantedScopes = ExternalLoginGrants.Serialize(grantedScopes),
                        ConsentedAt = SystemClock.UtcNow,
                        ConsentVersion = OAuthConsentVersions.Current
                    },
                    cancellationToken).ConfigureAwait(false);

                logger.LogInformation(
                    "OAuth scope consent recorded for user {UserId} on provider {Provider}: granted {GrantedScopes} (consent version {ConsentVersion})",
                    userId,
                    provider,
                    grantedScopes,
                    OAuthConsentVersions.Current);

                return;
            }
            catch (DbUpdateException)
            {
                // Unique-index race: a concurrent request inserted the same (Provider, ProviderKey).
                // Refetch and apply the same three-way rule below.
                existing = await externalLoginRepository.GetByProviderKeyAsync(provider, providerKey, cancellationToken).ConfigureAwait(false);

                if (existing == null) { throw; }
            }
        }

        if (existing.UserId == userId)
        {
            await RefreshConsentWhenScopeSetChangedAsync(externalLoginRepository, existing, grantedScopes, logger, cancellationToken).ConfigureAwait(false);
            return;
        }

        throw new ExternalLoginConflictException("Social account already linked to another user");
    }

    private static async Task RefreshConsentWhenScopeSetChangedAsync(
        IExternalLoginRepository externalLoginRepository,
        ExternalLogin existing,
        string[] grantedScopes,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var recorded = ExternalLoginGrants.Deserialize(existing.GrantedScopes);
        if (existing.ConsentedAt is not null && ExternalLoginGrants.SameScopeSet(recorded, grantedScopes))
        {
            // Unchanged re-authorization: keep the original first-consent record.
            return;
        }

        await externalLoginRepository.RecordConsentAsync(existing.Provider, existing.UserId, grantedScopes, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "OAuth scope consent re-recorded for user {UserId} on provider {Provider}: granted {GrantedScopes} (consent version {ConsentVersion})",
            existing.UserId,
            existing.Provider,
            grantedScopes,
            OAuthConsentVersions.Current);
    }
}
