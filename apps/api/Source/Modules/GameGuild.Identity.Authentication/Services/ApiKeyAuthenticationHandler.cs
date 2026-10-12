using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Authentication handler for API key-based authentication
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly IAuthenticationAuditEventSink? _auditEventSink;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyRepository apiKeyRepository)
        : this(options, logger, encoder, apiKeyRepository, null)
    {
    }

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyRepository apiKeyRepository,
        IAuthenticationAuditEventSink? auditEventSink)
        : base(options, logger, encoder)
    {
        _apiKeyRepository = apiKeyRepository;
        _auditEventSink = auditEventSink;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var headerApiKey = Request.Headers[Options.HeaderName].FirstOrDefault();
        var queryApiKey = Options.AllowQueryString
            ? Request.Query[Options.QueryStringParameterName].FirstOrDefault()
            : null;
        string? customApiKey;
        try
        {
            customApiKey = Options.CustomKeyResolver?.Invoke(Request);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error reading custom API key source");
            return await FailAsync("Authentication error", "AuthenticationError").ConfigureAwait(false);
        }

        var configuredSources = new[] { headerApiKey, queryApiKey, customApiKey }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        if (configuredSources.Length > 1)
        {
            return await FailAsync(
                "Provide an API key through only one configured source.",
                "MultipleApiKeySources").ConfigureAwait(false);
        }

        var providedApiKey = configuredSources.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedApiKey))
        {
            return AuthenticateResult.NoResult();
        }

        if (!string.IsNullOrWhiteSpace(queryApiKey) && !Request.IsHttps)
        {
            return await FailAsync("API keys in query strings require HTTPS.", "ApiKeyQueryRequiresHttps").ConfigureAwait(false);
        }

        ApiKey? apiKey = null;
        try
        {
            // Compute hash of provided key
            var keyHash = ComputeHash(providedApiKey);

            // Look up API key through the credential repository
            apiKey = await _apiKeyRepository.GetByKeyHashAsync(keyHash, Context.RequestAborted).ConfigureAwait(false);

            if (apiKey == null)
            {
                Logger.LogWarning("Invalid API key provided");
                return await FailAsync("Invalid API key", "InvalidApiKey").ConfigureAwait(false);
            }

            // Validate key
            if (!apiKey.IsValid())
            {
                // Lazily finalize rotation: once the overlap window closes, record the
                // revocation so the lifecycle stays observable without a background job.
                await _apiKeyRepository.FinalizeRotationRevocationAsync(apiKey, Context.RequestAborted).ConfigureAwait(false);

                Logger.LogWarning("Inactive or expired API key used: {KeyId}", apiKey.Id);
                return await FailAsync("API key is inactive or expired", "InactiveOrExpiredApiKey", apiKey.UserId, apiKey.TenantId).ConfigureAwait(false);
            }

            // Check IP whitelist if configured
            if (!string.IsNullOrWhiteSpace(apiKey.IpWhitelist))
            {
                var clientIp = Context.Connection.RemoteIpAddress?.ToString();
                var allowedIps = apiKey.IpWhitelist.Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (clientIp == null || !allowedIps.Contains(clientIp))
                {
                    Logger.LogWarning("API key {KeyId} used from unauthorized IP: {ClientIp}", apiKey.Id, clientIp);
                    return await FailAsync("API key not authorized from this IP address", "ApiKeyIpNotAllowed", apiKey.UserId, apiKey.TenantId).ConfigureAwait(false);
                }
            }

            // Record usage
            await _apiKeyRepository.RecordUsageAsync(apiKey, Context.RequestAborted).ConfigureAwait(false);

            // Create claims
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, apiKey.UserId.ToString()),
                new("sub", apiKey.UserId.ToString()),
                new("tenant_id", apiKey.TenantId?.ToString() ?? string.Empty),
                new("api_key_id", apiKey.Id.ToString()),
                new("auth_method", "api_key")
            };

            // Add scope claims
            foreach (var scope in apiKey.GetScopes())
            {
                claims.Add(new Claim("scope", scope));
            }

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            Logger.LogInformation("API key authentication successful for user {UserId}, key {KeyId}",
                apiKey.UserId, apiKey.Id);

            await RecordAuthenticationAuditEventAsync(success: true, apiKey.UserId, apiKey.TenantId).ConfigureAwait(false);

            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during API key authentication");
            return await FailAsync("Authentication error", "AuthenticationError", apiKey?.UserId, apiKey?.TenantId).ConfigureAwait(false);
        }
    }

    private async Task<AuthenticateResult> FailAsync(string message, string auditReason, Guid? userId = null, Guid? tenantId = null)
    {
        await RecordAuthenticationAuditEventAsync(false, userId, tenantId, auditReason).ConfigureAwait(false);
        return AuthenticateResult.Fail(message);
    }

    private async Task RecordAuthenticationAuditEventAsync(
        bool success,
        Guid? userId,
        Guid? tenantId,
        string? auditReason = null)
    {
        if (_auditEventSink is null)
        {
            return;
        }

        try
        {
            await _auditEventSink.RecordAsync(new AuthenticationAuditEvent(
                success ? "Authentication.Succeeded" : "Authentication.Failed",
                userId,
                success,
                "ApiKey",
                Context.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString(),
                TenantId: tenantId,
                ErrorMessage: auditReason),
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Could not record API key authentication audit event");
        }
    }

    private static string ComputeHash(string plaintext)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

/// <summary>
///     Options for API key authentication
/// </summary>
public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "ApiKey";

    public string HeaderName { get; set; } = "X-API-Key";

    public bool AllowQueryString { get; set; }

    public string QueryStringParameterName { get; set; } = "api_key";

    /// <summary>
    ///     Optional programmatic source for deployments that retrieve API keys from a custom request location.
    /// </summary>
    public Func<HttpRequest, string?>? CustomKeyResolver { get; set; }

    public override void Validate()
    {
        base.Validate();

        if (string.IsNullOrWhiteSpace(HeaderName))
        {
            throw new InvalidOperationException("API key header name must not be empty.");
        }

        if (AllowQueryString && string.IsNullOrWhiteSpace(QueryStringParameterName))
        {
            throw new InvalidOperationException("API key query parameter name must not be empty when query authentication is enabled.");
        }
    }
}

/// <summary>
///     Extension methods for API key authentication
/// </summary>
public static class ApiKeyAuthenticationExtensions
{
    public static AuthenticationBuilder AddApiKeyAuthentication(this AuthenticationBuilder builder) =>
        AddApiKeyAuthentication(builder, configure: null);

    public static AuthenticationBuilder AddApiKeyAuthentication(
        this AuthenticationBuilder builder,
        Action<ApiKeyAuthenticationOptions>? configure)
    {
        return builder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationOptions.SchemeName,
            options =>
            {
                configure?.Invoke(options);
                options.Validate();
            });
    }
}
