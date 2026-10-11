using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Outcome of one PayPal REST call performed by <see cref="IPayPalApiClient" />.
///     Transport failures, HTTP status and the raw body are surfaced verbatim so the
///     adapter can classify failures without the client guessing.
/// </summary>
/// <param name="Success">Whether PayPal answered 2xx.</param>
/// <param name="StatusCode">HTTP status code when a response arrived; null on transport failure.</param>
/// <param name="Body">Raw response body when present.</param>
/// <param name="ErrorCode">Best-effort error code extracted from the body.</param>
/// <param name="ErrorMessage">Best-effort error message extracted from the body.</param>
/// <param name="TransportFailed">True when the request never produced a response (DNS, connection, timeout).</param>
public sealed record PayPalApiResult(
    bool Success,
    int? StatusCode,
    string? Body,
    string? ErrorCode,
    string? ErrorMessage,
    bool TransportFailed)
{
    /// <summary>True when the call failed with an HTTP 408/429/5xx response.</summary>
    public bool IsServerError =>
        StatusCode is 408 or 429 or >= 500;

    /// <summary>True when the call failed with an HTTP 4xx response.</summary>
    public bool IsClientError =>
        StatusCode is >= 400 and < 500 && !IsServerError;
}

/// <summary>
///     Thin HTTP client over the PayPal REST API used by the PayPal processing adapter
///     (issue #413). Handles OAuth2 client-credentials authentication with token caching
///     and sends <c>PayPal-Request-Id</c> idempotency headers on mutating calls. All
///     failures come back as <see cref="PayPalApiResult" /> values — the client never throws
///     for transport or HTTP failures, so callers fail closed on values, not exceptions.
/// </summary>
public interface IPayPalApiClient
{
    /// <summary>
    ///     Creates and immediately captures a PayPal order charged to a stored payment
    ///     method token (vault/billing agreement). <paramref name="idempotencyKey" /> is
    ///     sent both as the order's invoice id and as the PayPal-Request-Id idempotency header.
    /// </summary>
    Task<PayPalApiResult> CreateAndCaptureOrderAsync(
        string idempotencyKey,
        decimal amount,
        string currency,
        string paymentMethodToken,
        string? description,
        CancellationToken cancellationToken = default);

    /// <summary>Refunds a captured payment, fully or partially.</summary>
    Task<PayPalApiResult> RefundCaptureAsync(
        string idempotencyKey,
        string captureId,
        decimal? amount,
        string currency,
        string? reason,
        CancellationToken cancellationToken = default);

    /// <summary>Retrieves the current state of a PayPal order.</summary>
    Task<PayPalApiResult> GetOrderAsync(string orderId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Verifies a webhook signature through PayPal's verify-webhook-signature endpoint.
    ///     The signature carries the five PayPal webhook headers joined with commas in
    ///     their documented order; <paramref name="webhookId" /> identifies the endpoint.
    /// </summary>
    Task<PayPalApiResult> VerifyWebhookSignatureAsync(
        string payload,
        string signature,
        string webhookId,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Default <see cref="IPayPalApiClient" /> implementation over <see cref="HttpClient" />
///     (registered through <c>IHttpClientFactory</c>). OAuth access tokens are cached per
///     client instance until shortly before expiry; a failing token request is a transport-style
///     failure value, never an exception.
/// </summary>
public sealed partial class PayPalApiClient : IPayPalApiClient
{
    private readonly HttpClient _httpClient;
    private readonly PayPalGatewayOptions _options;
    private readonly ILogger<PayPalApiClient> _logger;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _accessToken;
    private DateTime _accessTokenExpiresAtUtc;

    /// <summary>Creates the client bound to its options (called by the typed-client factory).</summary>
    public PayPalApiClient(HttpClient httpClient, IOptions<PayPalGatewayOptions> options, ILogger<PayPalApiClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _httpClient.BaseAddress = new Uri(_options.ResolveBaseUrl(), UriKind.Absolute);
        _httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(1, _options.RequestTimeoutSeconds));
    }

    /// <inheritdoc />
    public async Task<PayPalApiResult> CreateAndCaptureOrderAsync(
        string idempotencyKey,
        decimal amount,
        string currency,
        string paymentMethodToken,
        string? description,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentMethodToken);

        var orderPayload = new
        {
            intent = "CAPTURE",
            purchase_units = new[]
            {
                new
                {
                    amount = new
                    {
                        currency_code = currency.ToUpperInvariant(),
                        value = PayPalAmountFormatter.Format(amount, currency)
                    },
                    invoice_id = idempotencyKey,
                    custom_id = idempotencyKey,
                    description
                }
            },
            payment_source = new
            {
                token = new
                {
                    id = paymentMethodToken,
                    type = "BILLING_AGREEMENT"
                }
            }
        };

        var orderResult = await SendAsync(
            HttpMethod.Post,
            "/v2/checkout/orders",
            SerializeJson(orderPayload),
            idempotencyKey,
            cancellationToken).ConfigureAwait(false);

        if (!orderResult.Success)
        {
            return orderResult;
        }

        var orderId = ExtractString(orderResult.Body, "id");
        if (orderId is null)
        {
            return orderResult with
            {
                Success = false,
                ErrorCode = "paypal_unparseable_response",
                ErrorMessage = "The PayPal order creation response did not contain an order id."
            };
        }

        return await SendAsync(
            HttpMethod.Post,
            $"/v2/checkout/orders/{Uri.EscapeDataString(orderId)}/capture",
            content: "{}",
            idempotencyKey: $"{idempotencyKey}:capture",
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<PayPalApiResult> RefundCaptureAsync(
        string idempotencyKey,
        string captureId,
        decimal? amount,
        string currency,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(captureId);

        var payload = new
        {
            amount = amount.HasValue
                ? new
                {
                    currency_code = currency.ToUpperInvariant(),
                    value = PayPalAmountFormatter.Format(amount.Value, currency)
                }
                : null,
            note_to_payer = reason,
            invoice_id = idempotencyKey
        };

        return await SendAsync(
            HttpMethod.Post,
            $"/v2/payments/captures/{Uri.EscapeDataString(captureId)}/refund",
            SerializeJson(payload),
            idempotencyKey,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<PayPalApiResult> GetOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);

        return SendAsync(
            HttpMethod.Get,
            $"/v2/checkout/orders/{Uri.EscapeDataString(orderId)}",
            content: null,
            idempotencyKey: null,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PayPalApiResult> VerifyWebhookSignatureAsync(
        string payload,
        string signature,
        string webhookId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(webhookId);

        // PayPal's verification endpoint needs five webhook headers; the IPaymentGateway
        // contract carries one signature string, so callers pass them comma-joined in
        // the documented order: transmission id, transmission time, cert url, auth algo, signature.
        var parts = (signature ?? string.Empty).Split(',');
        var verifyPayload = new
        {
            auth_algo = parts.Length > 3 ? parts[3] : string.Empty,
            cert_url = parts.Length > 2 ? parts[2] : string.Empty,
            transmission_id = parts.Length > 0 ? parts[0] : string.Empty,
            transmission_sig = parts.Length > 4 ? parts[4] : string.Empty,
            transmission_time = parts.Length > 1 ? parts[1] : string.Empty,
            webhook_id = webhookId,
            webhook_event = JsonDocument.Parse(payload ?? "{}").RootElement.Clone()
        };

        return await SendAsync(
            HttpMethod.Post,
            "/v1/notifications/verify-webhook-signature",
            SerializeJson(verifyPayload),
            idempotencyKey: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static string SerializeJson(object payload) =>
        JsonSerializer.Serialize(payload, JsonSerializerOptions);

    private static string? ExtractString(string? body, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        using var document = JsonDocument.Parse(body);
        return document.RootElement.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private async Task<PayPalApiResult> SendAsync(
        HttpMethod method,
        string path,
        string? content,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                request.Headers.Add("PayPal-Request-Id", idempotencyKey);
            }

            if (content is not null)
            {
                request.Content = new StringContent(content, Encoding.UTF8, "application/json");
            }

            var accessToken = await AcquireAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            if (accessToken is null)
            {
                return new PayPalApiResult(
                    false,
                    null,
                    null,
                    "paypal_authentication_failed",
                    "PayPal OAuth2 token request failed; the adapter is not usable with the current credentials.",
                    TransportFailed: false);
            }

            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return new PayPalApiResult(true, (int)response.StatusCode, body, null, null, TransportFailed: false);
            }

            var (errorCode, errorMessage) = ParseErrorBody(body);
            LogHttpFailure((int)response.StatusCode, path, errorCode, errorMessage);
            return new PayPalApiResult(
                false,
                (int)response.StatusCode,
                body,
                errorCode,
                errorMessage,
                TransportFailed: false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            // No response arrived (connection failure or timeout): transport failure, the
            // safest classification surface for the adapter.
            LogTransportFailure(path, exception);
            return new PayPalApiResult(
                false,
                null,
                null,
                "paypal_transport_failure",
                exception.Message,
                TransportFailed: true);
        }
    }

    private async Task<string?> AcquireAccessTokenAsync(CancellationToken cancellationToken)
    {
        await _tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_accessToken is not null && SystemClock.UtcNow < _accessTokenExpiresAtUtc)
            {
                return _accessToken;
            }

            if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ClientSecret))
            {
                return null;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/oauth2/token")
            {
                Content = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded")
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}")));

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                LogTokenFailure((int)response.StatusCode, body);
                return null;
            }

            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("access_token", out var token) ||
                token.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var expiresIn = document.RootElement.TryGetProperty("expires_in", out var expires) &&
                            expires.ValueKind == JsonValueKind.Number
                ? expires.GetInt32()
                : 3600;

            _accessToken = token.GetString();
            // Refresh one minute before the server-side expiry.
            _accessTokenExpiresAtUtc = SystemClock.UtcNow.AddSeconds(Math.Max(60, expiresIn - 60));
            return _accessToken;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            LogTokenTransportFailure(exception);
            return null;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static (string? Code, string? Message) ParseErrorBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return (null, null);
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var name = root.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
                ? nameElement.GetString()
                : null;
            var message = root.TryGetProperty("message", out var messageElement) &&
                          messageElement.ValueKind == JsonValueKind.String
                ? messageElement.GetString()
                : null;

            if (root.TryGetProperty("details", out var details) &&
                details.ValueKind == JsonValueKind.Array &&
                details.GetArrayLength() > 0)
            {
                var first = details[0];
                var issue = first.TryGetProperty("issue", out var issueElement) &&
                            issueElement.ValueKind == JsonValueKind.String
                    ? issueElement.GetString()
                    : null;
                var description = first.TryGetProperty("description", out var descriptionElement) &&
                                  descriptionElement.ValueKind == JsonValueKind.String
                    ? descriptionElement.GetString()
                    : null;

                return (issue ?? name, description ?? message);
            }

            return (name, message);
        }
        catch (JsonException)
        {
            return (null, body.Length > 200 ? body[..200] : body);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "PayPal HTTP call to {Path} failed with {StatusCode}: {ErrorCode} - {ErrorMessage}")]
    private partial void LogHttpFailure(int statusCode, string path, string? errorCode, string? errorMessage);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "PayPal HTTP call to {Path} never produced a response")]
    private partial void LogTransportFailure(string path, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "PayPal OAuth2 token request failed with {StatusCode}: {Body}")]
    private partial void LogTokenFailure(int statusCode, string body);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "PayPal OAuth2 token request never produced a response")]
    private partial void LogTokenTransportFailure(Exception exception);
}

/// <summary>
///     Formats monetary amounts for the PayPal API, which expects plain decimal strings
///     with the currency's natural number of decimal places (two for most currencies,
///     none for zero-decimal currencies such as JPY).
/// </summary>
public static class PayPalAmountFormatter
{
    private static readonly HashSet<string> ZeroDecimalCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "JPY",
        "KRW",
        "VND",
        "CLP"
    };

    /// <summary>Formats <paramref name="amount" /> for PayPal requests.</summary>
    public static string Format(decimal amount, string currency) =>
        ZeroDecimalCurrencies.Contains(currency)
            ? decimal.Truncate(amount).ToString("0", System.Globalization.CultureInfo.InvariantCulture)
            : amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
}
