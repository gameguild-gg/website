using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using GameGuild.Email;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;
using SendGridEmailAddress = SendGrid.Helpers.Mail.EmailAddress;

namespace GameGuild.API.Email;

public sealed class EmailSender : IEmailSender
{
    private readonly Func<string, ISendGridClient> sendGridClientFactory;
    private readonly IOptions<EmailDeliveryOptions> options;
    private readonly ILogger<EmailSender> logger;
    private readonly SesEmailSender? sesEmailSender;

    public EmailSender(IOptions<EmailDeliveryOptions> options, ILogger<EmailSender> logger, SesEmailSender sesEmailSender)
        : this(options, logger, apiKey => new SendGridClient(apiKey), sesEmailSender)
    {
    }

    public EmailSender(
        IOptions<EmailDeliveryOptions> options,
        ILogger<EmailSender> logger)
        : this(options, logger, apiKey => new SendGridClient(apiKey))
    {
    }

    internal EmailSender(
        IOptions<EmailDeliveryOptions> options,
        ILogger<EmailSender> logger,
        Func<string, ISendGridClient> sendGridClientFactory,
        SesEmailSender? sesEmailSender = null)
    {
        this.options = options;
        this.logger = logger;
        this.sendGridClientFactory = sendGridClientFactory;
        this.sesEmailSender = sesEmailSender;
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

        var provider = ResolveProvider(currentOptions);
        if (provider.Equals("Ses", StringComparison.OrdinalIgnoreCase))
        {
            return await (sesEmailSender ?? throw new InvalidOperationException("The SES email sender is not registered."))
                .SendAsync(message, cancellationToken).ConfigureAwait(false);
        }
        if (provider.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
        {
            return await SendWithSmtpAsync(message, currentOptions, cancellationToken).ConfigureAwait(false);
        }

        if (provider.Equals("SendGrid", StringComparison.OrdinalIgnoreCase))
        {
            return await SendWithSendGridAsync(message, currentOptions, cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException(
            $"EmailDelivery:Provider '{currentOptions.Provider}' is not supported. Use 'Ses', 'SendGrid' or 'Smtp'.");
    }

    private static string ResolveProvider(EmailDeliveryOptions currentOptions)
    {
        if (!string.IsNullOrWhiteSpace(currentOptions.Provider))
        {
            return currentOptions.Provider.Trim();
        }

        if (!string.IsNullOrWhiteSpace(currentOptions.Ses.Region)) return "Ses";
        return string.IsNullOrWhiteSpace(currentOptions.SendGridApiKey) ? "Smtp" : "SendGrid";
    }

    private async Task<string?> SendWithSendGridAsync(
        EmailMessage message,
        EmailDeliveryOptions currentOptions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currentOptions.SendGridApiKey))
        {
            throw new InvalidOperationException("EmailDelivery:SendGridApiKey is required when email delivery uses SendGrid.");
        }

        var client = sendGridClientFactory(currentOptions.SendGridApiKey);
        var from = new SendGridEmailAddress(
            currentOptions.FromEmail,
            string.IsNullOrWhiteSpace(currentOptions.FromName) ? currentOptions.FromEmail : currentOptions.FromName);
        var to = new SendGridEmailAddress(
            message.ToEmail,
            string.IsNullOrWhiteSpace(message.ToName) ? message.ToEmail : message.ToName);
        var providerMessage = MailHelper.CreateSingleEmail(
            from,
            to,
            message.Subject,
            message.PlainTextContent,
            message.HtmlContent);

        foreach (var attachment in message.Attachments ?? [])
        {
            providerMessage.AddAttachment(
                attachment.FileName,
                Convert.ToBase64String(attachment.Content),
                attachment.ContentType);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var response = await client.SendEmailAsync(providerMessage, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"SendGrid rejected email delivery with status code {(int)response.StatusCode}.");
        }

        logger.LogInformation(
            "Delivered email to {RecipientEmail} with {AttachmentCount} attachments.",
            message.ToEmail,
            message.Attachments?.Count ?? 0);
        return response.Headers.TryGetValues("X-Message-Id", out var messageIds)
            ? messageIds.FirstOrDefault()
            : null;
    }

    private async Task<string?> SendWithSmtpAsync(
        EmailMessage message,
        EmailDeliveryOptions currentOptions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currentOptions.SmtpHost))
        {
            throw new InvalidOperationException("EmailDelivery:SmtpHost is required when email delivery uses SMTP.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var smtpClient = new SmtpClient(currentOptions.SmtpHost, currentOptions.SmtpPort)
        {
            EnableSsl = currentOptions.SmtpUseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(currentOptions.SmtpUsername))
        {
            smtpClient.Credentials = new NetworkCredential(
                currentOptions.SmtpUsername,
                currentOptions.SmtpPassword ?? string.Empty);
        }
        else
        {
            smtpClient.UseDefaultCredentials = false;
        }

        using var mailMessage = new MailMessage
        {
            From = new MailAddress(
                currentOptions.FromEmail!,
                string.IsNullOrWhiteSpace(currentOptions.FromName) ? currentOptions.FromEmail : currentOptions.FromName),
            Subject = message.Subject,
            SubjectEncoding = Encoding.UTF8,
            Body = message.PlainTextContent,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false
        };

        mailMessage.To.Add(new MailAddress(
            message.ToEmail,
            string.IsNullOrWhiteSpace(message.ToName) ? message.ToEmail : message.ToName));
        mailMessage.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            message.PlainTextContent,
            Encoding.UTF8,
            MediaTypeNames.Text.Plain));
        mailMessage.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            message.HtmlContent,
            Encoding.UTF8,
            MediaTypeNames.Text.Html));

        foreach (var attachment in message.Attachments ?? [])
        {
            var stream = new MemoryStream(attachment.Content, writable: false);
            mailMessage.Attachments.Add(
                new System.Net.Mail.Attachment(stream, attachment.FileName, attachment.ContentType));
        }

        var messageId = $"<{Guid.NewGuid():N}@{new MailAddress(currentOptions.FromEmail!).Host}>";
        mailMessage.Headers.Add("Message-ID", messageId);
        await smtpClient.SendMailAsync(mailMessage, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Delivered email to {RecipientEmail} through SMTP host {SmtpHost}:{SmtpPort} with {AttachmentCount} attachments.",
            message.ToEmail,
            currentOptions.SmtpHost,
            currentOptions.SmtpPort,
            message.Attachments?.Count ?? 0);
        return messageId;
    }
}
