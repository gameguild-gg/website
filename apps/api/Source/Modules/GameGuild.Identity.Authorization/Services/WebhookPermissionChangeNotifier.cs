using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     The kind of permission change described by a <see cref="PermissionChangeEvent"/>.
/// </summary>
public enum PermissionChangeEventType
{
    /// <summary>Permissions were granted to a subject.</summary>
    Granted = 0,

    /// <summary>Permissions were revoked from a subject.</summary>
    Revoked = 1,

    /// <summary>Permissions were explicitly denied (DENY-WINS).</summary>
    Denied = 2,

    /// <summary>A permission set was replaced or updated.</summary>
    Updated = 3,

    /// <summary>A previously removed permission state was restored (issue #358).</summary>
    Restored = 4,

    /// <summary>Permissions were changed by an external-system synchronization import.</summary>
    Synced = 5
}

/// <summary>
///     An observed permission mutation, fanned out to registered
///     <see cref="IPermissionChangeNotifier"/> implementations after the change is
///     persisted, versioned and audited. Notification delivery never alters the outcome
///     of the mutation itself.
/// </summary>
/// <param name="EventType">The kind of change.</param>
/// <param name="TenantId">Tenant scope of the change (null for global defaults).</param>
/// <param name="UserId">Subject user the change applies to (null for tenant/global defaults).</param>
/// <param name="PermissionType">Free-form permission category, for example <c>Tenant</c>.</param>
/// <param name="Permissions">The permissions that changed.</param>
/// <param name="PerformedBy">The actor that performed the mutation.</param>
/// <param name="OccurredAtUtc">When the change occurred (defaults to now when unset).</param>
public sealed record PermissionChangeEvent(
    PermissionChangeEventType EventType,
    Guid? TenantId,
    Guid? UserId,
    string? PermissionType,
    IReadOnlyCollection<string> Permissions,
    Guid PerformedBy,
    DateTime OccurredAtUtc = default)
{
    /// <summary>Stable wire name for the event type.</summary>
    [JsonPropertyName("eventType")]
    public string EventTypeName => EventType switch
    {
        PermissionChangeEventType.Granted => "permission.granted",
        PermissionChangeEventType.Revoked => "permission.revoked",
        PermissionChangeEventType.Denied => "permission.denied",
        PermissionChangeEventType.Updated => "permission.updated",
        PermissionChangeEventType.Restored => "permission.restored",
        PermissionChangeEventType.Synced => "permission.synced",
        _ => "permission.changed"
    };
}

/// <summary>
///     Outbound fan-out for permission changes (issue #358). Implementations must never
///     throw; delivery problems are reported through the returned result and logging.
/// </summary>
public interface IPermissionChangeNotifier
{
    /// <summary>
    ///     Notifies subscribed external systems about one permission change.
    /// </summary>
    /// <param name="change">The change to announce.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Delivery outcome; never throws.</returns>
    Task<PermissionChangeNotificationResult> NotifyAsync(
        PermissionChangeEvent change,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Delivery outcome of one notification attempt series.
/// </summary>
/// <param name="Delivered">True when the endpoint acknowledged the delivery (2xx).</param>
/// <param name="Attempts">Number of attempts made.</param>
/// <param name="Skipped">True when notifications are disabled/unconfigured (no attempt was made).</param>
public sealed record PermissionChangeNotificationResult(
    bool Delivered,
    int Attempts,
    bool Skipped);

/// <summary>
///     HMAC-signed webhook implementation of <see cref="IPermissionChangeNotifier"/>
///     (issue #358). Config-gated through <c>PermissionEngine:Webhooks</c>: unless the
///     section is enabled with an endpoint and a secret, every notification is skipped
///     (fail-closed, no outbound calls).
/// </summary>
/// <remarks>
///     <para>
///         <b>Signature.</b> The payload is serialized to deterministic JSON and signed
///         with HMAC-SHA256 using the configured shared secret. The signature travels in
///         the <c>X-GameGuild-Signature</c> header as <c>sha256=&lt;lowercase hex&gt;</c>
///         over the exact request body bytes, so receivers can verify authenticity.
///     </para>
///     <para>
///         <b>Retry.</b> Failed or non-2xx deliveries are retried up to
///         <c>MaxAttempts</c> with exponential backoff
///         (<c>RetryBaseDelayMilliseconds * 2^(attempt-1)</c>). Every attempt and the
///         final failure are logged; the notifier never throws back into the permission
///         mutation path.
///     </para>
/// </remarks>
public sealed class WebhookPermissionChangeNotifier(
    HttpClient httpClient,
    IOptions<PermissionEngineOptions> engineOptions,
    ILogger<WebhookPermissionChangeNotifier> logger) : IPermissionChangeNotifier
{
    /// <summary>The signature header carrying the HMAC-SHA256 digest.</summary>
    public const string SignatureHeaderName = "X-GameGuild-Signature";

    private static readonly JsonSerializerOptions PayloadSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private readonly PermissionWebhookOptions _options = engineOptions.Value.Webhooks;

    /// <summary>
    ///     Serializes the webhook payload for one change. Public for tests and for
    ///     receivers that need the canonical byte representation.
    /// </summary>
    public static string SerializePayload(PermissionChangeEvent change)
    {
        change = change.OccurredAtUtc == default
            ? change with { OccurredAtUtc = SystemClock.UtcNow }
            : change;

        var payload = new WebhookPayload(
            change.EventTypeName,
            change.TenantId,
            change.UserId,
            change.PermissionType,
            change.Permissions,
            change.PerformedBy,
            change.OccurredAtUtc);
        return JsonSerializer.Serialize(payload, PayloadSerializerOptions);
    }

    /// <summary>
    ///     Computes the HMAC-SHA256 signature value for the given body bytes and secret,
    ///     in the <c>sha256=&lt;hex&gt;</c> header form.
    /// </summary>
    public static string ComputeSignature(ReadOnlySpan<byte> body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var digest = hmac.ComputeHash(body.ToArray());
        return $"sha256={Convert.ToHexString(digest).ToLowerInvariant()}";
    }

    /// <inheritdoc />
    public async Task<PermissionChangeNotificationResult> NotifyAsync(
        PermissionChangeEvent change,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (!_options.IsConfigured)
        {
            logger.LogDebug(
                "Permission change webhook for {EventType} skipped: webhooks disabled or not configured.",
                change.EventTypeName);
            return new PermissionChangeNotificationResult(Delivered: false, Attempts: 0, Skipped: true);
        }

        var payload = SerializePayload(change);
        var body = Encoding.UTF8.GetBytes(payload);
        var signature = ComputeSignature(body, _options.Secret!);
        var maxAttempts = Math.Max(1, _options.MaxAttempts);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint);
                request.Content = new ByteArrayContent(body);
                request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                request.Headers.Add(SignatureHeaderName, signature);

                using var response = await httpClient
                    .SendAsync(request, cancellationToken)
                    .ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    if (attempt > 1)
                    {
                        logger.LogInformation(
                            "Permission change webhook for {EventType} delivered on attempt {Attempt}.",
                            change.EventTypeName,
                            attempt);
                    }

                    return new PermissionChangeNotificationResult(Delivered: true, Attempts: attempt, Skipped: false);
                }

                logger.LogWarning(
                    "Permission change webhook for {EventType} attempt {Attempt}/{MaxAttempts} returned {StatusCode}.",
                    change.EventTypeName,
                    attempt,
                    maxAttempts,
                    (int)response.StatusCode);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Permission change webhook for {EventType} attempt {Attempt}/{MaxAttempts} failed.",
                    change.EventTypeName,
                    attempt,
                    maxAttempts);
            }

            if (attempt < maxAttempts)
            {
                var delay = TimeSpan.FromMilliseconds(
                    Math.Min(_options.RetryBaseDelayMilliseconds * (1 << (attempt - 1)), 30_000));
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        logger.LogError(
            "Permission change webhook for {EventType} was NOT delivered after {Attempts} attempts; the permission change itself is unaffected.",
            change.EventTypeName,
            maxAttempts);
        return new PermissionChangeNotificationResult(Delivered: false, Attempts: maxAttempts, Skipped: false);
    }

    private sealed record WebhookPayload(
        [property: JsonPropertyName("eventType")] string EventType,
        [property: JsonPropertyName("tenantId")] Guid? TenantId,
        [property: JsonPropertyName("userId")] Guid? UserId,
        [property: JsonPropertyName("permissionType")] string? PermissionType,
        [property: JsonPropertyName("permissions")] IReadOnlyCollection<string> Permissions,
        [property: JsonPropertyName("performedBy")] Guid PerformedBy,
        [property: JsonPropertyName("occurredAtUtc")] DateTime OccurredAtUtc);
}
