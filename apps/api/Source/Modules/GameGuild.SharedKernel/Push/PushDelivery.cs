namespace GameGuild.Push;

/// <summary>
///     Host-level configuration for mobile push delivery (FCM for Android, APNs for iOS). The
///     default (disabled, no provider) is the fail-closed state: push notifications are recorded
///     durably but nothing reaches a device until a host configures a concrete adapter.
/// </summary>
public sealed class PushDeliveryOptions
{
    /// <summary>Section name used by the composition root.</summary>
    public const string SectionName = "PushDelivery";

    /// <summary>Whether a push provider is configured. Disabled (default) means delivery is unavailable.</summary>
    public bool Enabled { get; set; }

    /// <summary>Provider identifier: <c>Fcm</c> (HTTP v1 adapter in the API host) or empty for the no-op default.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>FCM v1 credentials (OAuth 2.0 service account); only read when Provider is <c>Fcm</c>.</summary>
    public FcmOptions? Fcm { get; set; }

    /// <summary>FCM HTTP v1 credentials.</summary>
    public sealed class FcmOptions
    {
        /// <summary>Sender project id (FCM v1 messages:send endpoint is project-scoped).</summary>
        public string? ProjectId { get; set; }

        /// <summary>Service-account client email used for the OAuth 2.0 JWT bearer assertion.</summary>
        public string? ClientEmail { get; set; }

        /// <summary>Service-account private key (PEM, RS256) used to sign the JWT bearer assertion.</summary>
        public string? PrivateKey { get; set; }

        /// <summary>Optional override of the FCM API base URL (tests/proxies).</summary>
        public string? ApiUrlBase { get; set; }
    }
}

/// <summary>An outbound push notification targeted at one device registration token.</summary>
/// <param name="DeviceToken">Provider device registration token.</param>
/// <param name="Title">Notification title.</param>
/// <param name="Body">Notification body.</param>
/// <param name="Data">Optional structured data payload (string key/value).</param>
public sealed record PushMessage(
    string DeviceToken,
    string Title,
    string Body,
    IReadOnlyDictionary<string, string>? Data = null);

/// <summary>Provider acceptance is independent of the optional provider message identifier.</summary>
/// <param name="Accepted">Whether the provider accepted the message for delivery.</param>
/// <param name="ProviderMessageId">Optional provider-side message identifier.</param>
public sealed record PushDeliveryReceipt(bool Accepted, string? ProviderMessageId);

/// <summary>
///     Mobile push port (FCM/APNs). Platform modules depend on this abstraction; the composition
///     root plugs in a concrete adapter or the no-op default. Implementations must be thread-safe.
///     A disabled configuration must return <c>Accepted = false</c> rather than throwing so
///     notification sweeps can retry/deadletter observably.
/// </summary>
public interface IPushSender
{
    /// <summary>Sends one push message and returns the provider acceptance receipt.</summary>
    Task<PushDeliveryReceipt> SendAsync(PushMessage message, CancellationToken cancellationToken = default);
}

/// <summary>
///     Default no-op push sender: delivery is unavailable and every send is declined without side
///     effects. Registered as the fallback so push-backed features (for example push MFA) degrade
///     fail-closed until a host configures a real adapter.
/// </summary>
public sealed class NoOpPushSender : IPushSender
{
    /// <summary>Shared stateless instance.</summary>
    public static readonly NoOpPushSender Instance = new();

    private NoOpPushSender() { }

    /// <inheritdoc />
    public Task<PushDeliveryReceipt> SendAsync(PushMessage message, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PushDeliveryReceipt(Accepted: false, ProviderMessageId: null));
    }
}
