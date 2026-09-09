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
    /// <summary>Sends the message and returns the provider message id, or null when email delivery is disabled (skip).</summary>
    Task<string?> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
