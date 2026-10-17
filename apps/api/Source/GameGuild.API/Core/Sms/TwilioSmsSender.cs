using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GameGuild.Sms;
using Microsoft.Extensions.Options;

namespace GameGuild.API.Sms;

/// <summary>
///     Twilio REST SMS adapter (config-gated: <c>SmsDelivery:Provider = Twilio</c> with account
///     credentials). Sends through the plain REST API — no vendor SDK — so the adapter stays a
///     small, auditable HTTP client owned by the composition root.
/// </summary>
public sealed class TwilioSmsSender : ISmsSender
{
    public const string HttpClientName = "gameguild-sms-twilio";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory httpClientFactory;
    private readonly IOptions<SmsDeliveryOptions> options;

    public TwilioSmsSender(IHttpClientFactory httpClientFactory, IOptions<SmsDeliveryOptions> options)
    {
        this.httpClientFactory = httpClientFactory;
        this.options = options;
    }

    public async Task<SmsDeliveryReceipt> SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var currentOptions = options.Value;
        if (!currentOptions.Enabled)
        {
            return new SmsDeliveryReceipt(Accepted: false, ProviderMessageId: null);
        }

        var twilio = currentOptions.Twilio;
        if (string.IsNullOrWhiteSpace(twilio?.AccountSid) || string.IsNullOrWhiteSpace(twilio.AuthToken))
        {
            throw new InvalidOperationException("SmsDelivery:Twilio:AccountSid and AuthToken are required to send SMS through Twilio.");
        }

        if (string.IsNullOrWhiteSpace(currentOptions.From))
        {
            throw new InvalidOperationException("SmsDelivery:From is required to send SMS.");
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        var authToken = twilio.AuthToken!;
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{twilio.AccountSid}:{authToken}"));
        using var request = new HttpRequestMessage(HttpMethod.Post, $"Accounts/{twilio.AccountSid}/Messages.json")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["To"] = message.To,
                ["From"] = currentOptions.From!,
                ["Body"] = message.Body
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var payload = await response.Content.ReadFromJsonAsync<TwilioMessagePayload>(JsonOptions, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            // Declined/failed submissions are receipts, not exceptions: the notification sweep
            // retries and deadletters based on acceptance, keeping the pipeline observable.
            return new SmsDeliveryReceipt(Accepted: false, ProviderMessageId: payload?.Sid);
        }

        return new SmsDeliveryReceipt(Accepted: true, ProviderMessageId: payload?.Sid);
    }

    private sealed record TwilioMessagePayload(string? Sid, string? Status, int? ErrorCode);
}
