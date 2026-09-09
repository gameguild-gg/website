using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Amazon.SimpleNotificationService.Util;
using GameGuild.CQRS;
using GameGuild.Notifications.Services.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Notifications.Controllers;

public sealed record ReceiveEmailEventsCommand(string Body) : ICommand<IActionResult>;

public sealed class ReceiveEmailEventsCommandHandler : ICommandHandler<ReceiveEmailEventsCommand, IActionResult>
{
    private const int MaxPayloadLength = 4000;

    /// <summary>Named HttpClient (registered in NotificationsModule) used to visit SubscribeURLs.</summary>
    public const string SubscriptionConfirmationClientName = "SnsSubscriptionConfirmation";

    private readonly ISnsMessageVerifier _verifier;
    private readonly IEmailEventProcessor _processor;
    private readonly IApplicationDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EmailEventsController> _logger;

    public ReceiveEmailEventsCommandHandler(
        ISnsMessageVerifier verifier,
        IEmailEventProcessor processor,
        IApplicationDbContext context,
        IHttpClientFactory httpClientFactory,
        ILogger<EmailEventsController> logger)
    {
        _verifier = verifier;
        _processor = processor;
        _context = context;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IActionResult> Handle(ReceiveEmailEventsCommand command, CancellationToken cancellationToken)
    {
        var body = command.Body;

        if (string.IsNullOrWhiteSpace(body))
        {
            return Malformed();
        }

        SnsVerificationResult verification;
        try
        {
            verification = _verifier.ValidateRequest(body);
        }
        catch
        {
            // The verifier contract is "never throws"; this guard ensures hostile input can
            // never surface as a 500 even if that contract is broken.
            return SignatureRejected();
        }

        if (verification is SnsVerificationResult.Invalid invalid)
        {
            return invalid.Rejection switch
            {
                SnsRejectionReason.TopicMismatch or SnsRejectionReason.TopicNotConfigured => Forbidden(),
                SnsRejectionReason.Malformed => Malformed(),
                _ => SignatureRejected()
            };
        }

        var envelope = ((SnsVerificationResult.Valid)verification).Envelope;
        return envelope.Type switch
        {
            "SubscriptionConfirmation" => await ConfirmSubscriptionAsync(envelope, cancellationToken).ConfigureAwait(false),
            "Notification" => await IngestNotificationAsync(envelope, cancellationToken).ConfigureAwait(false),
            _ => Malformed()
        };
    }

    private async Task<IActionResult> ConfirmSubscriptionAsync(Message envelope, CancellationToken cancellationToken)
    {
        // SSRF guard: the SubscribeURL is attacker-reachable input until proven otherwise —
        // host is pinned to sns.*.amazonaws.com BEFORE any fetch.
        if (!SnsMessageVerifier.IsTrustedAwsSnsUrl(envelope.SubscribeURL))
        {
            return Malformed();
        }

        var client = _httpClientFactory.CreateClient(SubscriptionConfirmationClientName);
        using var response = await client.GetAsync(envelope.SubscribeURL, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("SNS subscription confirmation fetch failed. Status: {Status}", (int)response.StatusCode);
            return ProblemResult(StatusCodes.Status502BadGateway, "SubscriptionConfirmationFailed", "The subscription confirmation could not be completed.");
        }

        _logger.LogInformation("SNS subscription confirmed. TopicArn: {TopicArn}", envelope.TopicArn);
        return new OkResult();
    }

    private async Task<IActionResult> IngestNotificationAsync(Message envelope, CancellationToken cancellationToken)
    {
        EmailDeliveryEvent emailEvent;
        try
        {
            emailEvent = BuildEvent(envelope);
        }
        catch (Exception)
        {
            // Inner SES payload unparseable or missing required fields.
            return Malformed();
        }

        var processorRan = false;
        try
        {
            var alreadyIngested = await _context.Set<EmailDeliveryEvent>()
                .AnyAsync(e => e.SnsMessageId == emailEvent.SnsMessageId, cancellationToken)
                .ConfigureAwait(false);

            if (!alreadyIngested)
            {
                _context.Set<EmailDeliveryEvent>().Add(emailEvent);
            }

            // The processor mutates tracked entities only (never saves); the single SaveChanges
            // below persists event + suppression + deadletters atomically. A failure before or
            // during it persists NOTHING → 500 → SNS retries the identical message.
            await _processor.ProcessAsync(emailEvent, cancellationToken).ConfigureAwait(false);
            processorRan = true;
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new OkResult();
        }
        catch (DbUpdateException)
        {
            // Unique-index race: a concurrent request ingested the same SnsMessageId between the
            // pre-check and the save. Drop the poisoned insert (detach pending entries), confirm
            // it really was a duplicate, re-run the idempotent processor (heals a delivery whose
            // side effects were lost) and ack — SNS must not retry an already-stored event.
            ResetContextState();
            var isDuplicate = await _context.Set<EmailDeliveryEvent>()
                .AnyAsync(e => e.SnsMessageId == emailEvent.SnsMessageId, cancellationToken)
                .ConfigureAwait(false);
            if (!isDuplicate)
            {
                // Genuine save failure (not a duplicate race): nothing persisted, SNS retries.
                return IngestFailed();
            }

            if (!processorRan)
            {
                await _processor.ProcessAsync(emailEvent, cancellationToken).ConfigureAwait(false);
            }

            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new OkResult();
        }
        catch (Exception)
        {
            // Nothing persisted (the single save never completed); no raw body in the log.
            return IngestFailed();
        }
    }

    /// <summary>
    /// Parses the inner SES event JSON into an <see cref="EmailDeliveryEvent"/>. Throws on any
    /// unparseable/missing field — the caller maps that to 400. Single-recipient pipeline:
    /// mail.destination[0]; switch to bounce.bouncedRecipients[0].emailAddress if multi-recipient
    /// sending ever lands.
    /// </summary>
    private static EmailDeliveryEvent BuildEvent(Message envelope)
    {
        using var document = JsonDocument.Parse(envelope.MessageText);
        var root = document.RootElement;

        var mail = root.GetProperty("mail");
        var providerMessageId = mail.GetProperty("messageId").GetString();
        var destination = mail.GetProperty("destination");
        var recipient = destination.GetArrayLength() > 0 ? destination[0].GetString() : null;
        var eventType = root.GetProperty("eventType").GetString();
        var occurredAt = DateTime.Parse(
            mail.GetProperty("timestamp").GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        string? bounceType = null;
        string? diagnosticCode = null;
        if (root.TryGetProperty("bounce", out var bounce))
        {
            if (bounce.TryGetProperty("bounceType", out var bounceTypeElement))
            {
                bounceType = bounceTypeElement.GetString();
            }

            // SES carries the SMTP diagnostic on each bounced recipient.
            if (bounce.TryGetProperty("bouncedRecipients", out var bouncedRecipients)
                && bouncedRecipients.GetArrayLength() > 0
                && bouncedRecipients[0].TryGetProperty("diagnosticCode", out var diagnostic))
            {
                diagnosticCode = diagnostic.GetString();
            }
        }

        if (string.IsNullOrWhiteSpace(providerMessageId) || string.IsNullOrWhiteSpace(recipient))
        {
            throw new InvalidOperationException("SES event is missing mail.messageId or mail.destination.");
        }

        if (eventType is null || !Enum.TryParse<EmailDeliveryEventType>(eventType, out var parsedEventType))
        {
            throw new InvalidOperationException("SES event carries an unsupported eventType.");
        }

        return EmailDeliveryEvent.Create(
            Truncate(providerMessageId, 100)!,
            recipient!,
            parsedEventType,
            occurredAt,
            Truncate(envelope.MessageId, 100)!,
            Truncate(bounceType, 30),
            Truncate(diagnosticCode, 200),
            BuildPrivacyStrippedPayload(root));
    }

    /// <summary>
    /// Re-serializes the event JSON with ipAddress/userAgent removed anywhere they appear
    /// (data minimization — GDPR/FERPA). If the stripped JSON still exceeds the jsonb budget,
    /// stores a VALID-JSON wrapper {"preview": "&lt;first 4000 chars&gt;"} — never a truncated
    /// raw JSON string, which the jsonb column would reject.
    /// </summary>
    private static string? BuildPrivacyStrippedPayload(JsonElement root)
    {
        var node = JsonSerializer.SerializeToNode(root);
        StripPrivacyFields(node);
        var serialized = JsonSerializer.Serialize(node);
        if (serialized.Length <= MaxPayloadLength)
        {
            return serialized;
        }

        return JsonSerializer.Serialize(new { preview = serialized[..MaxPayloadLength] });
    }

    private static void StripPrivacyFields(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                obj.Remove("ipAddress");
                obj.Remove("userAgent");
                foreach (var child in obj)
                {
                    StripPrivacyFields(child.Value);
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    StripPrivacyFields(item);
                }

                break;
        }
    }

    private void ResetContextState()
    {
        if (_context is not DbContext dbContext)
        {
            return; // Same escape hatch as EmailEventProcessor; production context always is one.
        }

        foreach (var entry in dbContext.ChangeTracker.Entries().ToList())
        {
            if (entry.State != EntityState.Unchanged)
            {
                entry.State = EntityState.Detached;
            }
        }
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];

    // Generic messages by design: the webhook must not reveal why a request failed (no oracle
    // for forged-message probing). Explicit ProblemDetails so direct unit invocation works
    // without ProblemDetailsFactory (NotificationUnsubscribeController pattern).
    private static ObjectResult Malformed()
        => ProblemResult(StatusCodes.Status400BadRequest, "InvalidBody", "The request body could not be processed.");

    private static ObjectResult SignatureRejected()
        => ProblemResult(StatusCodes.Status401Unauthorized, "Unauthorized", "The request could not be authenticated.");

    private static ObjectResult Forbidden()
        => ProblemResult(StatusCodes.Status403Forbidden, "TopicRejected", "The request was rejected.");

    private static ObjectResult IngestFailed()
        => ProblemResult(StatusCodes.Status500InternalServerError, "EmailEventIngestFailed", "The event could not be ingested.");

    private static ObjectResult ProblemResult(int status, string title, string detail) =>
        new(new ProblemDetails { Status = status, Title = title, Detail = detail }) { StatusCode = status };
}
