namespace GameGuild.Email;

public sealed class EmailDeliveryOptions
{
    public bool Enabled { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string? SendGridApiKey { get; set; }

    public string? FromEmail { get; set; }

    public string? FromName { get; set; }

    public string? SmtpHost { get; set; }

    public int SmtpPort { get; set; } = 1025;

    public string? SmtpUsername { get; set; }

    public string? SmtpPassword { get; set; }

    public bool SmtpUseSsl { get; set; }

    public SesOptions Ses { get; set; } = new();

    public EventsOptions Events { get; set; } = new();

    public sealed class SesOptions
    {
        public string? Region { get; set; }

        public string? ConfigurationSetName { get; set; }
    }

    public sealed class EventsOptions
    {
        public string? TopicArn { get; set; }
    }
}

public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

public sealed record EmailMessage(
    string ToEmail,
    string Subject,
    string PlainTextContent,
    string HtmlContent,
    string? ToName = null,
    IReadOnlyList<EmailAttachment>? Attachments = null);

public interface IEmailSender
{
    /// <summary>Returns the optional provider message identifier. A null identifier alone does not establish acceptance; use IConfirmedEmailSender when confirmation is required.</summary>
    Task<string?> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

/// <summary>Provider acceptance is independent of the optional provider message identifier.</summary>
public sealed record EmailDeliveryReceipt(bool Accepted, string? ProviderMessageId);

/// <summary>Optional capability for notifications that require explicit provider acceptance.</summary>
public interface IConfirmedEmailSender : IEmailSender
{
    Task<EmailDeliveryReceipt> SendWithReceiptAsync(EmailMessage message) => SendWithReceiptAsync(message, CancellationToken.None);

    Task<EmailDeliveryReceipt> SendWithReceiptAsync(EmailMessage message, CancellationToken cancellationToken);
}
