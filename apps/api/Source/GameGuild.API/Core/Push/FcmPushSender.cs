using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Push;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.API.Push;

/// <summary>
///     Firebase Cloud Messaging (HTTP v1) push adapter — config-gated: <c>PushDelivery:Provider =
///     Fcm</c> with a service-account key. Obtains OAuth 2.0 access tokens via a self-signed RS256
///     JWT assertion (no vendor SDK) and caches them until shortly before expiry. APNs delivery in
///     this product rides through FCM's APNs transport, so FCM is the single mobile adapter.
/// </summary>
public sealed class FcmPushSender : IPushSender
{
    public const string HttpClientName = "gameguild-push-fcm";

    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string Scope = "https://www.googleapis.com/auth/firebase.messaging";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory httpClientFactory;
    private readonly IOptions<PushDeliveryOptions> options;
    private readonly ILogger<FcmPushSender> logger;
    private readonly SemaphoreSlim tokenLock = new(1, 1);

    private volatile CachedAccessToken? cachedToken;

    public FcmPushSender(IHttpClientFactory httpClientFactory, IOptions<PushDeliveryOptions> options, ILogger<FcmPushSender> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.options = options;
        this.logger = logger;
    }

    public async Task<PushDeliveryReceipt> SendAsync(PushMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var currentOptions = options.Value;
        if (!currentOptions.Enabled)
        {
            return new PushDeliveryReceipt(Accepted: false, ProviderMessageId: null);
        }

        var fcm = currentOptions.Fcm;
        if (string.IsNullOrWhiteSpace(fcm?.ProjectId) || string.IsNullOrWhiteSpace(fcm.ClientEmail) || string.IsNullOrWhiteSpace(fcm.PrivateKey))
        {
            throw new InvalidOperationException("PushDelivery:Fcm:ProjectId, ClientEmail and PrivateKey are required to send push through FCM.");
        }

        var accessToken = await GetAccessTokenAsync(fcm, cancellationToken).ConfigureAwait(false);
        var client = httpClientFactory.CreateClient(HttpClientName);
        var baseUrl = string.IsNullOrWhiteSpace(fcm.ApiUrlBase)
            ? "https://fcm.googleapis.com"
            : fcm.ApiUrlBase!.TrimEnd('/');
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/projects/{fcm.ProjectId}/messages:send")
        {
            Content = JsonContent.Create(new
            {
                message = new
                {
                    token = message.DeviceToken,
                    notification = new { title = message.Title, body = message.Body },
                    data = message.Data
                }
            })
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var payload = await response.Content.ReadFromJsonAsync<FcmMessagePayload>(JsonOptions, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            // Declined/failed submissions are receipts, not exceptions: the notification sweep
            // retries and deadletters based on acceptance, keeping the pipeline observable.
            return new PushDeliveryReceipt(Accepted: false, ProviderMessageId: payload?.Name);
        }

        return new PushDeliveryReceipt(Accepted: true, ProviderMessageId: payload?.Name);
    }

    private async Task<string> GetAccessTokenAsync(PushDeliveryOptions.FcmOptions fcm, CancellationToken cancellationToken)
    {
        var existing = cachedToken;
        if (existing is not null && existing.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2))
        {
            return existing.Token;
        }

        await tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            existing = cachedToken;
            if (existing is not null && existing.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2))
            {
                return existing.Token;
            }

            var assertion = BuildJwtAssertion(fcm.ClientEmail!, fcm.PrivateKey!);
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                    ["assertion"] = assertion
                })
            };
            using var tokenResponse = await client.SendAsync(tokenRequest, cancellationToken).ConfigureAwait(false);
            tokenResponse.EnsureSuccessStatusCode();
            var tokenPayload = await tokenResponse.Content.ReadFromJsonAsync<OAuthTokenPayload>(JsonOptions, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The FCM OAuth token endpoint returned an unreadable payload.");

            cachedToken = new CachedAccessToken(tokenPayload.AccessToken, DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, tokenPayload.ExpiresIn - 120)));
            return tokenPayload.AccessToken;
        }
        finally
        {
            tokenLock.Release();
        }
    }

    /// <summary>Builds the RS256-signed JWT bearer assertion for the service account (no SDK dependency).</summary>
    internal static string BuildJwtAssertion(string clientEmail, string privateKeyPem)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var header = JsonSerializer.Serialize(new { alg = "RS256", typ = "JWT" });
        var claims = JsonSerializer.Serialize(new
        {
            iss = clientEmail,
            scope = Scope,
            aud = TokenEndpoint,
            iat = now,
            exp = now + 3600
        });

        var unsigned = $"{Base64UrlEncode(Encoding.UTF8.GetBytes(header))}.{Base64UrlEncode(Encoding.UTF8.GetBytes(claims))}";
        var signature = SignRs256(unsigned, privateKeyPem);
        return $"{unsigned}.{Base64UrlEncode(signature)}";
    }

    private static byte[] SignRs256(string unsignedInput, string privateKeyPem)
    {
        var normalizedKey = privateKeyPem.Replace("\\n", "\n", StringComparison.Ordinal);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(normalizedKey);
        return rsa.SignData(Encoding.UTF8.GetBytes(unsignedInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private sealed record CachedAccessToken(string Token, DateTimeOffset ExpiresAt);

    private sealed record OAuthTokenPayload(string AccessToken, int ExpiresIn);

    private sealed record FcmMessagePayload(string? Name, string? Error);
}
