using System.Text.Json;
using FluentAssertions;
using GameGuild.Commerce.Billing.Services.Email.Renderers;
using GameGuild.Notifications;
using GameGuild.Notifications.Services.Email;
using MockQueryable.Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Services.Email;

public sealed class InvoiceIssuedEmailRendererTests
{
    private readonly Mock<IApplicationDbContext> _context = new();
    private readonly Mock<IEmailFooterService> _footerService = new();
    private readonly InvoiceIssuedEmailRenderer _renderer;

    public InvoiceIssuedEmailRendererTests()
    {
        var configuration = new Mock<IConfiguration>();
        configuration
            .Setup(config => config["InvoiceEmails:ConsoleBaseUrl"])
            .Returns("https://console.example.test");
        _footerService
            .Setup(service => service.Build(It.IsAny<Notification>()))
            .Returns((EmailFooter?)null);

        _renderer = new InvoiceIssuedEmailRenderer(
            _context.Object,
            configuration.Object,
            _footerService.Object);
    }

    private static Invoice CreatePaidInvoice(Guid invoiceId, Guid subscriptionId)
    {
        var invoice = new Invoice(Guid.NewGuid(), subscriptionId, 29.99m, "USD", $"subscription:{subscriptionId}:cycle:1:invoice");
        invoice.SetDescription("Subscription billing cycle 1");
        invoice.SetBillingPeriod(
            new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc));
        invoice.SetExternalId("in_renderer");
        invoice.Issue(new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc));
        invoice.RecordPayment(Guid.NewGuid(), 29.99m, new DateTime(2026, 10, 1, 12, 5, 0, DateTimeKind.Utc));
        typeof(Invoice).GetProperty(nameof(Invoice.Id))!.SetValue(invoice, invoiceId);
        return invoice;
    }

    private static Notification CreateNotification(Invoice invoice)
    {
        var metadata = JsonSerializer.Serialize(new InvoiceIssuedEmailMetadata(
            TenantId: Guid.NewGuid(),
            SubscriptionId: invoice.SubscriptionId,
            UserId: Guid.NewGuid(),
            InvoiceId: invoice.Id,
            InvoiceNumber: invoice.InvoiceNumber,
            BillingCycleNumber: 1,
            Amount: 29.99m,
            Currency: "USD",
            PeriodStart: invoice.PeriodStart,
            PeriodEnd: invoice.PeriodEnd,
            RecipientEmail: "subscriber@example.test",
            RecipientName: "Subscriber"), new JsonSerializerOptions(JsonSerializerDefaults.Web));

        return Notification.Create(
            recipientId: Guid.NewGuid(),
            type: NotificationType.InvoiceIssued,
            channel: NotificationChannel.Email,
            title: $"Invoice {invoice.InvoiceNumber} issued",
            message: "Invoice issued",
            tenantId: Guid.NewGuid(),
            referenceEntityId: invoice.Id,
            referenceEntityType: nameof(Invoice),
            metadata: metadata,
            recipientEmail: "subscriber@example.test");
    }

    [Fact]
    public void Type_ShouldBeInvoiceIssued()
    {
        _renderer.Type.Should().Be(NotificationType.InvoiceIssued);
    }

    [Fact]
    public async Task Render_LoadsInvoiceAtSendTime_AndBuildsPdfAttachment()
    {
        var subscriptionId = Guid.NewGuid();
        var invoice = CreatePaidInvoice(Guid.NewGuid(), subscriptionId);
        _context
            .Setup(context => context.Set<Invoice>())
            .Returns(new[] { invoice }.AsQueryable().BuildMockDbSet().Object);
        var notification = CreateNotification(invoice);

        var message = await _renderer.RenderAsync(notification);

        message.Should().NotBeNull();
        message!.ToEmail.Should().Be("subscriber@example.test");
        message.ToName.Should().Be("Subscriber");
        message.Subject.Should().Contain(invoice.InvoiceNumber);
        message.PlainTextContent.Should().Contain("billing cycle 1");
        message.PlainTextContent.Should().Contain("2026-09-01 to 2026-09-30");
        message.PlainTextContent.Should().Contain($"subscriptions/{subscriptionId}/invoices");
        message.HtmlContent.Should().Contain(invoice.InvoiceNumber);
        message.Attachments.Should().ContainSingle();
        var attachment = message.Attachments!.Single();
        attachment.FileName.Should().Be($"invoice-{invoice.InvoiceNumber}.pdf");
        attachment.ContentType.Should().Be("application/pdf");
        attachment.Content.Should().NotBeEmpty();
        System.Text.Encoding.ASCII.GetString(attachment.Content).StartsWith("%PDF-1.4", StringComparison.Ordinal).Should().BeTrue();
    }

    [Fact]
    public async Task Render_Throws_WhenInvoiceNoLongerExists()
    {
        _context
            .Setup(context => context.Set<Invoice>())
            .Returns(Array.Empty<Invoice>().AsQueryable().BuildMockDbSet().Object);
        var orphan = CreatePaidInvoice(Guid.NewGuid(), Guid.NewGuid());
        var notification = CreateNotification(orphan);

        var act = async () => await _renderer.RenderAsync(notification);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Render_Throws_WhenMetadataIsMissing()
    {
        var notification = Notification.Create(
            recipientId: Guid.NewGuid(),
            type: NotificationType.InvoiceIssued,
            channel: NotificationChannel.Email,
            title: "Invoice issued",
            message: "Invoice issued",
            recipientEmail: "subscriber@example.test");

        var act = async () => await _renderer.RenderAsync(notification);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
