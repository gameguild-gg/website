using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Compliance.Audit;

/// <summary>
/// Server-side configuration for per-export webhooks. The request URL is only accepted when its exact host is
/// listed in <c>Audit:ExportWebhooks:AllowedHosts</c> and a signing secret of at least 32 characters is configured.
/// </summary>
public sealed class AuditExportWebhookOptions
{
    public const string ConfigurationSection = "Audit:ExportWebhooks";

    public string SigningSecret { get; set; } = string.Empty;

    public string[] AllowedHosts { get; set; } = [];

    public int MaxAttempts { get; set; } = 3;

    public int RetryDelayMilliseconds { get; set; } = 250;

    public int TimeoutSeconds { get; set; } = 5;
}

/// <summary>
/// Event payload delivered as JSON. The signature header is
/// <c>t={unixSeconds},v1={hex(HMAC-SHA256(secret, "{unixSeconds}.{rawBody}"))}</c>.
/// </summary>
public sealed record AuditExportWebhookNotification(
    string Id,
    string Type,
    DateTimeOffset OccurredAt,
    Guid ExportId,
    string Format,
    string Status,
    int TotalRecords,
    int RecordsWritten,
    string? ErrorCode);

public interface IAuditExportWebhookNotifier
{
    string? ValidateWebhookUrl(string? webhookUrl);

    Task NotifyAsync(
        string? webhookUrl,
        AuditExportWebhookNotification notification,
        CancellationToken cancellationToken);
}

public sealed class AuditExportWebhookNotifier(
    HttpClient httpClient,
    IOptions<AuditExportWebhookOptions> optionsAccessor,
    ILogger<AuditExportWebhookNotifier> logger) : IAuditExportWebhookNotifier
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AuditExportWebhookOptions _options = optionsAccessor.Value;

    public string? ValidateWebhookUrl(string? webhookUrl)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl)) { return null; }

        if (_options.SigningSecret.Length < 32 || _options.AllowedHosts.Length == 0)
        {
            return "Audit export webhooks are not configured by the server.";
        }

        if (!Uri.TryCreate(webhookUrl, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !uri.IsDefaultPort ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            !string.IsNullOrEmpty(uri.Query))
        {
            return "WebhookUrl must be an absolute HTTPS URL on the default port without user information, query parameters, or a fragment.";
        }

        var isAllowedHost = _options.AllowedHosts.Any(allowedHost =>
            string.Equals(allowedHost.Trim(), uri.IdnHost, StringComparison.OrdinalIgnoreCase));

        return isAllowedHost ? null : "WebhookUrl host is not allowed by the server configuration.";
    }

    public async Task NotifyAsync(
        string? webhookUrl,
        AuditExportWebhookNotification notification,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl)) { return; }

        var validationError = ValidateWebhookUrl(webhookUrl);
        if (validationError is not null)
        {
            logger.LogWarning("Skipping audit export webhook {EventId}: {Reason}", notification.Id, validationError);
            return;
        }

        var uri = new Uri(webhookUrl, UriKind.Absolute);
        var body = JsonSerializer.Serialize(notification, JsonOptions);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signedContent = Encoding.UTF8.GetBytes($"{timestamp}.{body}");
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(_options.SigningSecret), signedContent))
            .ToLowerInvariant();
        var attemptLimit = Math.Clamp(_options.MaxAttempts, 1, 5);
        var timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 1, 30));
        var retryDelay = TimeSpan.FromMilliseconds(Math.Clamp(_options.RetryDelayMilliseconds, 0, 5_000));

        for (var attempt = 1; attempt <= attemptLimit; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation("X-GameGuild-Audit-Event", notification.Type);
            request.Headers.TryAddWithoutValidation("X-GameGuild-Audit-Signature", $"t={timestamp},v1={signature}");
            request.Headers.TryAddWithoutValidation("Idempotency-Key", notification.Id);

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);

            try
            {
                using var response = await httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeoutSource.Token).ConfigureAwait(false);

                if (response.IsSuccessStatusCode) { return; }

                if (attempt == attemptLimit)
                {
                    logger.LogWarning(
                        "Audit export webhook {EventId} failed after {Attempts} attempts with HTTP {StatusCode} for host {Host}",
                        LogRedaction.Sanitize(notification.Id),
                        attempt,
                        (int)response.StatusCode,
                        LogRedaction.Sanitize(uri.IdnHost));
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt == attemptLimit)
                {
                    logger.LogWarning(
                        "Audit export webhook {EventId} timed out after {Attempts} attempts for host {Host}",
                        LogRedaction.Sanitize(notification.Id),
                        attempt,
                        LogRedaction.Sanitize(uri.IdnHost));
                }
            }
            catch (HttpRequestException exception)
            {
                if (attempt == attemptLimit)
                {
                    logger.LogWarning(
                        exception,
                        "Audit export webhook {EventId} could not be delivered after {Attempts} attempts for host {Host}",
                        LogRedaction.Sanitize(notification.Id),
                        attempt,
                        LogRedaction.Sanitize(uri.IdnHost));
                }
            }

            if (cancellationToken.IsCancellationRequested) { return; }
            if (attempt < attemptLimit && retryDelay > TimeSpan.Zero)
            {
                var delay = TimeSpan.FromMilliseconds(retryDelay.TotalMilliseconds * attempt);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

public static class AuditExportWebhookOptionsConfiguration
{
    public static AuditExportWebhookOptions BindFrom(IConfiguration configuration)
    {
        var section = configuration.GetSection(AuditExportWebhookOptions.ConfigurationSection);
        var options = new AuditExportWebhookOptions
        {
            SigningSecret = section[nameof(AuditExportWebhookOptions.SigningSecret)] ?? string.Empty,
            AllowedHosts = section.GetSection(nameof(AuditExportWebhookOptions.AllowedHosts))
                .GetChildren()
                .Select(child => child.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .ToArray()
        };

        if (int.TryParse(section[nameof(AuditExportWebhookOptions.MaxAttempts)], out var maxAttempts))
        {
            options.MaxAttempts = maxAttempts;
        }

        if (int.TryParse(section[nameof(AuditExportWebhookOptions.RetryDelayMilliseconds)], out var retryDelayMilliseconds))
        {
            options.RetryDelayMilliseconds = retryDelayMilliseconds;
        }

        if (int.TryParse(section[nameof(AuditExportWebhookOptions.TimeoutSeconds)], out var timeoutSeconds))
        {
            options.TimeoutSeconds = timeoutSeconds;
        }

        return options;
    }
}
