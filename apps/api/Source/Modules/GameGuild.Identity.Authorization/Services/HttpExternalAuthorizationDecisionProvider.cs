using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     OAuth2 client-credentials implementation of
/// <see cref="IExternalAuthorizationDecisionProvider"/> (issue #146), gated by
/// <c>Authorization:ExternalDecision</c>. Disabled by default: with
/// <see cref="ExternalAuthorizationOptions.Enabled"/> = false every evaluation returns
/// <c>null</c> without a single outbound call.
/// </summary>
/// <remarks>
///     <para>
///         <b>Wire contract.</b> The provider acquires a bearer token from
/// <see cref="ExternalAuthorizationOptions.TokenEndpoint"/> using the
/// client-credentials grant (form-urlencoded <c>grant_type=client_credentials</c>,
/// <c>client_id</c>, <c>client_secret</c>; JSON response
/// <c>{ "access_token": "...", "expires_in": 3600 }</c>) and caches it until shortly
/// before expiry. Each decision is one POST of
/// <c>{ "userId", "tenantId", "permission", "resourceType?", "resourceId?" }</c> to
/// <see cref="ExternalAuthorizationOptions.Endpoint"/> expecting
/// <c>{ "decision": "allow" | "deny" | "not_applicable", "reasons": [...] }</c>
/// (casing and <c>-</c>/<c>_</c> separators are tolerated).
///     </para>
///     <para>
///         <b>Fail-closed.</b> Token-acquisition failures, network errors, timeouts,
/// non-2xx responses and unparseable payloads follow the configured
/// <see cref="ExternalAuthorizationOptions.FailMode"/>:
/// <see cref="ExternalAuthorizationFailMode.Enforce"/> returns a deny for the queried
/// permission; <see cref="ExternalAuthorizationFailMode.Observe"/> returns no decision
/// (local resolution applies) and logs a warning. The provider never throws back into
/// the resolution path (caller cancellation is the only rethrown exception).
///     </para>
///     <para>
///         <b>Decision cache.</b> Decisions (including denials) are reused per exact
/// query for <see cref="ExternalAuthorizationOptions.CacheTtl"/> (zero disables
/// caching), bounding the call volume the external service sees per resolution. The
/// bearer token is cached independently of the decision cache.
///     </para>
/// </remarks>
public sealed class HttpExternalAuthorizationDecisionProvider(
    HttpClient httpClient,
    IOptions<ExternalAuthorizationOptions> options,
    ILogger<HttpExternalAuthorizationDecisionProvider> logger) : IExternalAuthorizationDecisionProvider
{
    private const string UnavailableReason = "external-authorization-unavailable";

    private static readonly Task<ExternalAuthorizationDecision?> NoDecision =
        Task.FromResult<ExternalAuthorizationDecision?>(null);

    private static readonly JsonSerializerOptions WireJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ExternalAuthorizationOptions _options = options.Value;

    private readonly ConcurrentDictionary<string, CachedDecision> _decisionCache = new(StringComparer.Ordinal);

    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private string? _accessToken;
    private DateTime _accessTokenValidUntilUtc = DateTime.MinValue;

    /// <inheritdoc />
    public async Task<ExternalAuthorizationDecision?> EvaluateAsync(
        ExternalAuthorizationQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return await NoDecision.ConfigureAwait(false);
        }

        if (!IsConfigured())
        {
            logger.LogError(
                "External authorization decisions are enabled but the provider is not fully configured (endpoint/token endpoint/client credentials) - failing {FailMode}.",
                _options.FailMode);
            return Unavailable("external-authorization-misconfigured");
        }

        var cacheKey = BuildCacheKey(query);
        if (TryGetCachedDecision(cacheKey, out var cached))
        {
            return cached;
        }

        var token = await AcquireAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (token is null)
        {
            return Unavailable("external-authorization-token-unavailable");
        }

        var response = await SendDecisionRequestAsync(query, token, cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            return Unavailable("external-authorization-unusable-response");
        }

        var decision = MapResponse(response);
        if (decision is null)
        {
            logger.LogWarning(
                "External authorization decision endpoint returned an unknown decision value '{DecisionValue}' for permission {Permission} - failing {FailMode}.",
                response.Decision, query.Permission, _options.FailMode);
            return Unavailable("external-authorization-unknown-decision");
        }

        CacheDecision(cacheKey, decision);
        if (decision.Outcome == ExternalAuthorizationOutcome.Deny)
        {
            logger.LogInformation(
                "External authorization decision endpoint denied permission {Permission} for user {UserId} in tenant {TenantId}: {Reasons}",
                query.Permission, query.UserId, query.TenantId, string.Join("; ", decision.Reasons)); // codeql[cs/cleartext-storage-of-sensitive-information] intentional user/tenant Guid audit logging
        }

        return decision;
    }

    private bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(_options.Endpoint)
        && !string.IsNullOrWhiteSpace(_options.TokenEndpoint)
        && !string.IsNullOrWhiteSpace(_options.ClientId)
        && !string.IsNullOrWhiteSpace(_options.ClientSecret);

    private async Task<DecisionResponse?> SendDecisionRequestAsync(
        ExternalAuthorizationQuery query,
        string token,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(
                        new DecisionRequest(query.UserId, query.TenantId, query.Permission, query.ResourceType, query.ResourceId),
                        WireJsonOptions),
                    Encoding.UTF8,
                    "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "External authorization decision endpoint answered {StatusCode} for permission {Permission} - failing {FailMode}.",
                    (int)response.StatusCode, query.Permission, _options.FailMode);
                return null;
            }

            return await response.Content
                .ReadFromJsonAsync<DecisionResponse>(WireJsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "External authorization decision request for permission {Permission} failed - failing {FailMode}.",
                query.Permission, _options.FailMode);
            return null;
        }
    }

    /// <summary>
    ///     Acquires (and caches) a bearer token via the OAuth2 client-credentials grant.
    ///     A single-flight lock prevents a token stampede when many resolutions race.
    /// </summary>
    private async Task<string?> AcquireAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_accessToken is not null && _accessTokenValidUntilUtc > SystemClock.UtcNow)
        {
            return _accessToken;
        }

        await _tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_accessToken is not null && _accessTokenValidUntilUtc > SystemClock.UtcNow)
            {
                return _accessToken;
            }

            var tokenRequest = new HttpRequestMessage(HttpMethod.Post, _options.TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = _options.ClientId!,
                    ["client_secret"] = _options.ClientSecret!
                })
            };

            using var response = await httpClient.SendAsync(tokenRequest, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "External authorization token endpoint answered {StatusCode} - failing {FailMode}.",
                    (int)response.StatusCode, _options.FailMode);
                return null;
            }

            var token = await response.Content
                .ReadFromJsonAsync<TokenResponse>(WireJsonOptions, cancellationToken)
                .ConfigureAwait(false);
            if (token?.AccessToken is not { Length: > 0 } accessToken)
            {
                logger.LogWarning(
                    "External authorization token endpoint returned a payload without an access_token - failing {FailMode}.",
                    _options.FailMode);
                return null;
            }

            // Cache until shortly before expiry; clamp the usable lifetime so a missing
            // or tiny expires_in never produces a token trusted for minutes.
            var lifetimeSeconds = token.ExpiresInSeconds is > 60 ? token.ExpiresInSeconds.Value - 30 : 30;
            _accessToken = accessToken;
            _accessTokenValidUntilUtc = SystemClock.UtcNow + TimeSpan.FromSeconds(lifetimeSeconds);
            return accessToken;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "External authorization token acquisition failed - failing {FailMode}.",
                _options.FailMode);
            return null;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static string BuildCacheKey(ExternalAuthorizationQuery query) =>
        string.Join(
            '|',
            query.UserId.ToString("N"),
            query.TenantId.ToString("N"),
            query.ResourceType ?? string.Empty,
            query.ResourceId ?? string.Empty,
            query.Permission.ToUpperInvariant());

    private bool TryGetCachedDecision(string cacheKey, out ExternalAuthorizationDecision decision)
    {
        decision = null!;
        if (_options.CacheTtl <= TimeSpan.Zero
            || !_decisionCache.TryGetValue(cacheKey, out var cached))
        {
            return false;
        }

        if (cached.ExpiresAtUtc <= SystemClock.UtcNow)
        {
            _decisionCache.TryRemove(cacheKey, out _);
            return false;
        }

        decision = cached.Decision;
        return true;
    }

    private void CacheDecision(string cacheKey, ExternalAuthorizationDecision decision)
    {
        if (_options.CacheTtl > TimeSpan.Zero)
        {
            _decisionCache[cacheKey] = new CachedDecision(decision, SystemClock.UtcNow + _options.CacheTtl);
        }
    }

    /// <summary>Maps a wire response to a decision; unknown values map to <c>null</c> (fail mode applies).</summary>
    private static ExternalAuthorizationDecision? MapResponse(DecisionResponse response)
    {
        var normalized = (response.Decision ?? string.Empty)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToLowerInvariant();

        var reasons = response.Reasons is { Length: > 0 } responseReasons ? responseReasons : Array.Empty<string>();
        return normalized switch
        {
            "allow" => new ExternalAuthorizationDecision { Outcome = ExternalAuthorizationOutcome.Allow, Reasons = reasons },
            "deny" => ExternalAuthorizationDecision.Deny(reasons),
            "notapplicable" => ExternalAuthorizationDecision.NotApplicable,
            _ => null
        };
    }

    private ExternalAuthorizationDecision? Unavailable(string reason) =>
        _options.FailMode switch
        {
            ExternalAuthorizationFailMode.Enforce => ExternalAuthorizationDecision.Deny(UnavailableReason, reason),
            _ => null
        };

    private readonly record struct CachedDecision(ExternalAuthorizationDecision Decision, DateTime ExpiresAtUtc);

    private sealed record DecisionRequest(
        Guid UserId,
        Guid TenantId,
        string Permission,
        string? ResourceType,
        string? ResourceId);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] long? ExpiresInSeconds);

    private sealed record DecisionResponse(
        [property: JsonPropertyName("decision")] string? Decision,
        [property: JsonPropertyName("reasons")] string[]? Reasons);
}
