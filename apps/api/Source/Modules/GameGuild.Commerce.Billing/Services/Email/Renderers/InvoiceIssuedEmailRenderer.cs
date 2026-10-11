using System.Globalization;
using System.Text;
using System.Text.Json;
using GameGuild.Email;
using GameGuild.Notifications;
using GameGuild.Notifications.Services.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GameGuild.Commerce.Billing.Services.Email.Renderers;

/// <summary>
///     Renders the invoice-issued email at send time. The invoice row is RE-READ from the invoices
///     table via <see cref="IApplicationDbContext"/> (REGEN-IS-CANONICAL, mirroring the monthly
///     statement renderer in Commerce.Subscriptions): the attachment and amounts reflect the persisted
///     invoice, while <see cref="Notification.Metadata"/> carries the recipient contract written by
///     <c>InvoiceGenerationService</c>. A render failure surfaces as an exception, which the email
///     dispatcher routes through its normal retry/backoff/deadletter path.
/// </summary>
/// <remarks>
///     Lives in Commerce.Billing (not the Notifications module) because it depends on the Invoice
///     entity defined here; the Notifications module cannot reference commerce modules (circular).
///     Registered as <see cref="IEmailRenderer"/> in the Billing module DI.
/// </remarks>
public sealed class InvoiceIssuedEmailRenderer(
    IApplicationDbContext context,
    IConfiguration configuration,
    IEmailFooterService footerService) : EmailRendererBase, IEmailRenderer
{
    private static readonly JsonSerializerOptions MetadataOptions = new(JsonSerializerDefaults.Web);
    private static readonly CultureInfo InvoiceCurrencyCulture = CultureInfo.GetCultureInfo("en-US");

    public NotificationType Type => NotificationType.InvoiceIssued;

    public async Task<EmailMessage?> RenderAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        var metadata = ParseMetadata(notification.Metadata);

        var invoice = await context.Set<Invoice>()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == metadata.InvoiceId, cancellationToken)
            .ConfigureAwait(false);

        if (invoice is null)
        {
            throw new InvalidOperationException(
                $"Invoice {metadata.InvoiceId} referenced by notification {notification.Id} no longer exists.");
        }

        var consoleBaseUrl = ResolveConsoleBaseUrl();
        var invoicesPageAbsoluteUrl = BuildAbsoluteUrl(consoleBaseUrl, $"subscriptions/{metadata.SubscriptionId}/invoices");

        var periodLabel = metadata.PeriodStart.HasValue && metadata.PeriodEnd.HasValue
            ? $"{metadata.PeriodStart:yyyy-MM-dd} to {metadata.PeriodEnd:yyyy-MM-dd}"
            : "current billing cycle";

        var subject = $"Your invoice {invoice.InvoiceNumber} is ready";

        var plainTextBody =
            $"Invoice {invoice.InvoiceNumber} for billing cycle {metadata.BillingCycleNumber} is attached as PDF.\n\n" +
            $"Period: {periodLabel}\n" +
            $"Total: {FormatInvoiceAmount(invoice.Total)} {invoice.Currency}\n" +
            $"Status: {DescribeStatus(invoice)}\n\n" +
            $"Review your invoice history: {invoicesPageAbsoluteUrl}";

        var htmlBody = $"""
            <p>Invoice <strong>{invoice.InvoiceNumber}</strong> for billing cycle {metadata.BillingCycleNumber} is attached as PDF.</p>
            <p>
                <strong>Period:</strong> {periodLabel}<br />
                <strong>Total:</strong> {FormatInvoiceAmount(invoice.Total)} {invoice.Currency}<br />
                <strong>Status:</strong> {DescribeStatus(invoice)}
            </p>
            <p>
                Review your invoice history:
                <a href="{invoicesPageAbsoluteUrl}">{invoicesPageAbsoluteUrl}</a>
            </p>
            """;

        var (plain, html) = MergeFooter(plainTextBody, htmlBody, footerService.Build(notification));

        var attachments = new List<EmailAttachment>
        {
            new($"invoice-{invoice.InvoiceNumber}.pdf", "application/pdf", InvoicePdfComposer.Compose(invoice, metadata.BillingCycleNumber))
        };

        return new EmailMessage(
            metadata.RecipientEmail,
            subject,
            plain,
            html,
            metadata.RecipientName,
            attachments);
    }

    private static string DescribeStatus(Invoice invoice) => invoice.Status switch
    {
        InvoiceStatus.Paid => "Paid",
        InvoiceStatus.Open => "Open",
        InvoiceStatus.PastDue => "Past due",
        InvoiceStatus.Void => "Void",
        InvoiceStatus.Uncollectible => "Uncollectible",
        _ => "Draft"
    };

    private static InvoiceIssuedEmailMetadata ParseMetadata(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            throw new InvalidOperationException("Invoice issued notification is missing metadata.");
        }

        try
        {
            return JsonSerializer.Deserialize<InvoiceIssuedEmailMetadata>(metadataJson, MetadataOptions)
                ?? throw new InvalidOperationException("Invoice issued notification metadata could not be deserialized.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Invoice issued notification metadata is malformed.", ex);
        }
    }

    private string ResolveConsoleBaseUrl()
    {
        var configured = configuration["InvoiceEmails:ConsoleBaseUrl"]
            ?? configuration["StatementEmails:ConsoleBaseUrl"]
            ?? configuration["NEXTAUTH_URL"]
            ?? configuration["NEXT_PUBLIC_URL"]
            ?? "http://localhost:3000";

        return configured.Trim().TrimEnd('/');
    }

    private static string BuildAbsoluteUrl(string baseUrl, string relativePath)
        => new Uri(new Uri(baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/", UriKind.Absolute), relativePath.TrimStart('/')).ToString();

    private static string FormatInvoiceAmount(decimal amount)
        => amount.ToString("F2", InvoiceCurrencyCulture);
}

/// <summary>
///     Composes the minimal single-page invoice PDF receipt. Text-layout PDF built with the same
///     hand-rolled approach as the MonthlyStatement artifact composer (no external PDF dependency);
///     full invoice PDF generation (itemized layout, provider branding) is a tracked follow-up.
/// </summary>
public static class InvoicePdfComposer
{
    public static byte[] Compose(Invoice invoice, int billingCycleNumber)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var periodLabel = invoice.PeriodStart.HasValue && invoice.PeriodEnd.HasValue
            ? $"{invoice.PeriodStart:yyyy-MM-dd} to {invoice.PeriodEnd:yyyy-MM-dd}"
            : "current billing cycle";

        var lines = new List<string>
        {
            $"Invoice {invoice.InvoiceNumber}",
            $"Billing cycle: {billingCycleNumber}",
            $"Period: {periodLabel}",
            $"Issued: {invoice.IssuedAt:yyyy-MM-dd HH:mm:ss} UTC",
            invoice.DueDate.HasValue ? $"Due: {invoice.DueDate:yyyy-MM-dd}" : "Due: -",
            string.Empty,
            $"Description: {invoice.Description}",
            $"Subtotal: {invoice.Subtotal:F2} {invoice.Currency}",
            $"Discount: {invoice.DiscountAmount:F2} {invoice.Currency}",
            $"Tax: {invoice.TaxAmount:F2} {invoice.Currency}",
            $"Total: {invoice.Total:F2} {invoice.Currency}",
            $"Amount paid: {invoice.AmountPaid:F2} {invoice.Currency}",
            invoice.PaidAt.HasValue ? $"Paid at: {invoice.PaidAt:yyyy-MM-dd HH:mm:ss} UTC" : "Paid at: -",
            string.Empty,
            invoice.PaymentId.HasValue ? $"Payment reference: {invoice.PaymentId}" : "Payment reference: -",
            string.IsNullOrWhiteSpace(invoice.ExternalId) ? "Provider invoice: -" : $"Provider invoice: {invoice.ExternalId}"
        };

        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine("BT");
        contentBuilder.AppendLine("/F1 11 Tf");
        contentBuilder.AppendLine("50 780 Td");
        contentBuilder.AppendLine("14 TL");

        foreach (var line in lines)
        {
            contentBuilder.AppendLine($"({EscapePdfLiteral(line)}) Tj");
            contentBuilder.AppendLine("T*");
        }

        contentBuilder.AppendLine("ET");

        var content = contentBuilder.ToString();
        var objects = new[]
        {
            "1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n",
            "2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj\n",
            "3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >> endobj\n",
            "4 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj\n",
            $"5 0 obj << /Length {Encoding.ASCII.GetByteCount(content)} >> stream\n{content}endstream\nendobj\n"
        };

        var documentBuilder = new StringBuilder();
        documentBuilder.Append("%PDF-1.4\n");

        var offsets = new List<int>();
        foreach (var obj in objects)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(documentBuilder.ToString()));
            documentBuilder.Append(obj);
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(documentBuilder.ToString());
        documentBuilder.Append($"xref\n0 {objects.Length + 1}\n");
        documentBuilder.Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            documentBuilder.Append($"{offset:D10} 00000 n \n");
        }

        documentBuilder.Append($"trailer << /Size {objects.Length + 1} /Root 1 0 R >>\n");
        documentBuilder.Append($"startxref\n{xrefOffset}\n%%EOF");

        return Encoding.ASCII.GetBytes(documentBuilder.ToString());
    }

    private static string EscapePdfLiteral(string value)
        => (value ?? string.Empty)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
}
