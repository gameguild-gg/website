namespace GameGuild.Commerce.Subscriptions;

public sealed record MonthlyStatementEmailAttachment(string FileName, string ContentType, byte[] Content);

public sealed record MonthlyStatementEmailMessage(
    string ToEmail,
    string Subject,
    string PlainTextContent,
    string HtmlContent,
    IReadOnlyList<MonthlyStatementEmailAttachment> Attachments,
    string? ToName = null);

public interface IMonthlyStatementMailSender
{
    Task SendAsync(MonthlyStatementEmailMessage message, CancellationToken cancellationToken = default);
}
