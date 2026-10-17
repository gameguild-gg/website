namespace GameGuild.Sms;

/// <summary>
///     Host-level configuration for SMS delivery. The default (disabled, no provider) is the
///     fail-closed state: the platform records SMS notifications durably but no message leaves
///     the system until a host configures a concrete provider.
/// </summary>
public sealed class SmsDeliveryOptions
{
    /// <summary>Section name used by the composition root.</summary>
    public const string SectionName = "SmsDelivery";

    /// <summary>Whether an SMS provider is configured. Disabled (default) means delivery is unavailable.</summary>
    public bool Enabled { get; set; }

    /// <summary>Provider identifier: <c>Twilio</c> (HTTP adapter in the API host) or empty for the no-op default.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Alphanumeric sender id or E.164 origin number required by the provider.</summary>
    public string? From { get; set; }

    /// <summary>Twilio REST credentials (Account SID + auth token); only read when Provider is <c>Twilio</c>.</summary>
    public TwilioOptions? Twilio { get; set; }

    /// <summary>Twilio-specific credentials.</summary>
    public sealed class TwilioOptions
    {
        /// <summary>Twilio account SID.</summary>
        public string? AccountSid { get; set; }

        /// <summary>Twilio auth token.</summary>
        public string? AuthToken { get; set; }

        /// <summary>Optional override of the Twilio API base URL (tests/proxies).</summary>
        public string? ApiUrlBase { get; set; }
    }
}

/// <summary>An outbound SMS: a destination phone number and a plain-text body.</summary>
/// <param name="To">Destination phone number in E.164 format.</param>
/// <param name="Body">Message body (plain text).</param>
public sealed record SmsMessage(string To, string Body);

/// <summary>Provider acceptance is independent of the optional provider message identifier.</summary>
/// <param name="Accepted">Whether the provider accepted the message for delivery.</param>
/// <param name="ProviderMessageId">Optional provider-side message identifier.</param>
public sealed record SmsDeliveryReceipt(bool Accepted, string? ProviderMessageId);

/// <summary>
///     Outbound SMS port. Platform modules depend on this abstraction; the composition root
///     plugs in a concrete provider (Twilio HTTP adapter) or the no-op default. Implementations
///     must be thread-safe. A disabled configuration must return <c>Accepted = false</c> rather
///     than throwing so notification sweeps can retry/deadletter observably.
/// </summary>
public interface ISmsSender
{
    /// <summary>Sends one SMS and returns the provider acceptance receipt.</summary>
    Task<SmsDeliveryReceipt> SendAsync(SmsMessage message, CancellationToken cancellationToken = default);
}

/// <summary>
///     Default no-op SMS sender: delivery is unavailable and every send is declined without side
///     effects. Registered as the fallback so SMS-backed features (for example SMS MFA) degrade
///     fail-closed until a host configures a real provider.
/// </summary>
public sealed class NoOpSmsSender : ISmsSender
{
    /// <summary>Shared stateless instance.</summary>
    public static readonly NoOpSmsSender Instance = new();

    private NoOpSmsSender() { }

    /// <inheritdoc />
    public Task<SmsDeliveryReceipt> SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new SmsDeliveryReceipt(Accepted: false, ProviderMessageId: null));
    }
}
