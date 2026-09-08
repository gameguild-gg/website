using System.Text;
using Amazon;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using GameGuild.Email;
using Microsoft.Extensions.Options;
using SesContent = Amazon.SimpleEmailV2.Model.Content;

namespace GameGuild.API.Email;

/// <summary>Amazon SES provider used by the shared, configurable email sender.</summary>
public sealed class SesEmailSender : IEmailSender
{
    private readonly Func<string, IAmazonSimpleEmailServiceV2> sesClientFactory;
    private readonly IOptions<EmailDeliveryOptions> options;
    private readonly ILogger<SesEmailSender> logger;

    public SesEmailSender(
        IOptions<EmailDeliveryOptions> options,
        ILogger<SesEmailSender> logger)
        : this(options, logger, region => new AmazonSimpleEmailServiceV2Client(RegionEndpoint.GetBySystemName(region)))
    {
    }

    internal SesEmailSender(
        IOptions<EmailDeliveryOptions> options,
        ILogger<SesEmailSender> logger,
        Func<string, IAmazonSimpleEmailServiceV2> sesClientFactory)
    {
        this.options = options;
        this.logger = logger;
        this.sesClientFactory = sesClientFactory;
    }

    public async Task<string?> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var currentOptions = options.Value;
        if (!currentOptions.Enabled)
        {
            logger.LogInformation("Email delivery is disabled. Skipping email to {RecipientEmail}.", message.ToEmail);
            return null;
        }

        if (string.IsNullOrWhiteSpace(currentOptions.FromEmail))
        {
            throw new InvalidOperationException("EmailDelivery:FromEmail is required to send email.");
        }

        var region = currentOptions.Ses.Region?.Trim();
        if (string.IsNullOrEmpty(region))
        {
            throw new InvalidOperationException("EmailDelivery:Ses:Region is required when email delivery is enabled.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        foreach (var value in new[] { currentOptions.FromEmail, currentOptions.FromName, message.ToEmail, message.ToName, message.Subject })
            ValidateHeaderValue(value);
        foreach (var attachment in message.Attachments ?? [])
        {
            ValidateHeaderValue(attachment.FileName);
            ValidateHeaderValue(attachment.ContentType);
        }

        using var client = sesClientFactory(region);
        var configurationSetName = currentOptions.Ses.ConfigurationSetName?.Trim();
        var request = new SendEmailRequest
        {
            FromEmailAddress = FormatAddress(currentOptions.FromEmail, currentOptions.FromName),
            Destination = new Destination
            {
                ToAddresses = [FormatAddress(message.ToEmail, message.ToName)]
            },
            Content = message.Attachments is { Count: > 0 }
                ? BuildRawContent(message, currentOptions)
                : BuildSimpleContent(message),
            ConfigurationSetName = string.IsNullOrWhiteSpace(configurationSetName) ? null : configurationSetName
        };

        SendEmailResponse response;
        try
        {
            response = await client.SendEmailAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (AmazonSimpleEmailServiceV2Exception ex)
        {
            throw new InvalidOperationException(
                $"Amazon SES rejected email delivery with status code {(int)ex.StatusCode}: {ex.Message}", ex);
        }

        logger.LogInformation(
            "Delivered email to {RecipientEmail} with {AttachmentCount} attachments.",
            message.ToEmail,
            message.Attachments?.Count ?? 0);

        return response.MessageId;
    }

    /// <summary>SES v2 simple content does not support attachments, so attachment sends use a raw MIME message.</summary>
    private static EmailContent BuildSimpleContent(EmailMessage message) => new()
    {
        Simple = new Message
        {
            Subject = CreateContent(message.Subject),
            Body = new Body
            {
                Html = CreateContent(message.HtmlContent),
                Text = CreateContent(message.PlainTextContent)
            }
        }
    };

    private static EmailContent BuildRawContent(EmailMessage message, EmailDeliveryOptions currentOptions)
    {
        var mixedBoundary = $"=_mixed_{Guid.NewGuid():N}";
        var alternativeBoundary = $"=_alt_{Guid.NewGuid():N}";
        var builder = new StringBuilder();
        AppendHeader(builder, "From: " + FormatAddress(currentOptions.FromEmail!, currentOptions.FromName));
        AppendHeader(builder, "To: " + FormatAddress(message.ToEmail, message.ToName));
        AppendHeader(builder, "Subject: " + EncodeHeaderValue(message.Subject));
        AppendHeader(builder, "MIME-Version: 1.0");
        AppendHeader(builder, $"Content-Type: multipart/mixed; boundary=\"{mixedBoundary}\"");
        builder.Append("\r\n");
        AppendBoundary(builder, mixedBoundary);
        AppendHeader(builder, $"Content-Type: multipart/alternative; boundary=\"{alternativeBoundary}\"");
        builder.Append("\r\n");
        AppendMimePart(builder, alternativeBoundary, "text/plain", message.PlainTextContent);
        AppendMimePart(builder, alternativeBoundary, "text/html", message.HtmlContent);
        AppendClosingBoundary(builder, alternativeBoundary);

        foreach (var attachment in message.Attachments!)
        {
            AppendBoundary(builder, mixedBoundary);
            AppendHeader(builder, $"Content-Type: {attachment.ContentType}; name=\"{EscapeQuotedValue(attachment.FileName)}\"");
            AppendHeader(builder, "Content-Transfer-Encoding: base64");
            AppendHeader(builder, $"Content-Disposition: attachment; filename=\"{EscapeQuotedValue(attachment.FileName)}\"");
            builder.Append("\r\n");
            builder.Append(WrapBase64(Convert.ToBase64String(attachment.Content)));
            builder.Append("\r\n");
        }

        AppendClosingBoundary(builder, mixedBoundary);

        return new EmailContent
        {
            Raw = new RawMessage { Data = new MemoryStream(Encoding.UTF8.GetBytes(builder.ToString()), writable: false) }
        };
    }

    private static SesContent CreateContent(string data) => new() { Data = data, Charset = "UTF-8" };

    private static void AppendMimePart(StringBuilder builder, string boundary, string contentType, string body)
    {
        AppendBoundary(builder, boundary);
        AppendHeader(builder, $"Content-Type: {contentType}; charset=UTF-8");
        AppendHeader(builder, "Content-Transfer-Encoding: base64");
        builder.Append("\r\n");
        builder.Append(WrapBase64(Convert.ToBase64String(Encoding.UTF8.GetBytes(body))));
        builder.Append("\r\n");
    }

    private static void AppendHeader(StringBuilder builder, string header) => builder.Append(header).Append("\r\n");

    private static void AppendBoundary(StringBuilder builder, string boundary) =>
        AppendHeader(builder, $"--{boundary}");

    private static void AppendClosingBoundary(StringBuilder builder, string boundary) =>
        AppendHeader(builder, $"--{boundary}--");

    private static string WrapBase64(string base64)
    {
        var wrapped = new StringBuilder(base64.Length + (base64.Length / 76 * 2) + 2);
        for (var offset = 0; offset < base64.Length; offset += 76)
        {
            wrapped.Append(base64, offset, Math.Min(76, base64.Length - offset)).Append("\r\n");
        }

        return wrapped.ToString();
    }

    private static string EncodeHeaderValue(string value) =>
        value.All(c => c < 128)
            ? value
            : $"=?UTF-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(value))}?=";

    private static string FormatAddress(string email, string? name) =>
        string.IsNullOrWhiteSpace(name) || name == email ? email : $"\"{EscapeQuotedValue(name)}\" <{email}>";

    private static string EscapeQuotedValue(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static void ValidateHeaderValue(string? value)
    {
        if (value?.Any(character => char.IsControl(character)) == true)
            throw new ArgumentException("Email headers must not contain control characters.");
    }
}
