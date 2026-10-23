using FluentAssertions;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Result = GameGuild.Result;

namespace GameGuild.Commerce.Payments.UnitTests.Services;

/// <summary>
///     Dispute notifications and fee recording (issue #419): the team alert on dispute creation,
///     the evidence-deadline query, deadline reminder emails, and the
///     <see cref="PaymentDispute.RecordDisputeFee"/> lifecycle rules.
/// </summary>
public class DisputeNotificationAndFeeTests
{
    private readonly Mock<INotificationService> _notifications = new();
    private readonly Mock<IRevenueAuditService> _revenueAudit = new();

    private static async Task<PaymentsPersistenceTestDbContext> CreateContextAsync()
    {
        var context = new PaymentsPersistenceTestDbContext(
            new DbContextOptionsBuilder<PaymentsPersistenceTestDbContext>()
                .UseInMemoryDatabase($"disputes-{Guid.NewGuid():N}")
                .Options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    [Fact]
    public async Task SendDisputeCreatedAlertAsync_WithoutRecipients_SendsNothing()
    {
        var service = CreateService(await CreateContextAsync(), emails: []);
        var dispute = OpenDispute();

        await service.SendDisputeCreatedAlertAsync(dispute, payment: null);

        _notifications.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SendDisputeCreatedAlertAsync_EmailsEveryConfiguredRecipient()
    {
        var service = CreateService(await CreateContextAsync(), emails: ["support@example.com", "billing@example.com"]);
        var dispute = OpenDispute();

        await service.SendDisputeCreatedAlertAsync(dispute, payment: null);

        foreach (var recipient in new[] { "support@example.com", "billing@example.com" })
        {
            _notifications.Verify(notificationService => notificationService.SendAsync(
                It.IsAny<Guid?>(),
                NotificationType.DisputeCreated,
                It.IsAny<string>(),
                It.Is<string>(message => message.Contains(dispute.Reason) && message.Contains(dispute.PaymentId.ToString())),
                NotificationChannel.Email,
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                NotificationPriority.High,
                dispute.Id,
                nameof(PaymentDispute),
                It.IsAny<string?>(),
                recipient,
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [Fact]
    public async Task SendDisputeCreatedAlertAsync_IsBestEffort_DeliveryFailuresAreSwallowed()
    {
        var service = CreateService(await CreateContextAsync(), emails: ["support@example.com"]);
        _notifications
            .Setup(notificationService => notificationService.SendAsync(
                It.IsAny<Guid?>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<NotificationChannel>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<NotificationPriority>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("provider unavailable"));

        var act = () => service.SendDisputeCreatedAlertAsync(OpenDispute(), payment: null);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ListOpenDisputesApproachingDeadlineAsync_ReturnsOnlyOpenDisputesWithinTheWindow()
    {
        await using var context = await CreateContextAsync();
        var dueSoon = OpenDispute(evidenceDueBy: SystemClock.UtcNow.AddDays(3));
        var dueLater = OpenDispute(evidenceDueBy: SystemClock.UtcNow.AddDays(30));
        var resolvedDueSoon = OpenDispute(evidenceDueBy: SystemClock.UtcNow.AddDays(3), status: DisputeStatus.Won);
        var noDeadline = OpenDispute(evidenceDueBy: null);
        context.Set<PaymentDispute>().AddRange(dueSoon, dueLater, resolvedDueSoon, noDeadline);
        await context.SaveChangesAsync();
        var service = CreateService(context, emails: []);

        var approaching = await service.ListOpenDisputesApproachingDeadlineAsync(daysWithin: 7);

        approaching.Should().ContainSingle(dispute => dispute.Id == dueSoon.Id);
    }

    [Fact]
    public async Task SendDeadlineReminderAsync_EmailsTheConfiguredRecipients()
    {
        var service = CreateService(await CreateContextAsync(), emails: ["support@example.com"]);
        var dispute = OpenDispute(evidenceDueBy: SystemClock.UtcNow.AddDays(2));

        await service.SendDeadlineReminderAsync(dispute);

        _notifications.Verify(notificationService => notificationService.SendAsync(
            It.IsAny<Guid?>(),
            NotificationType.DisputeDeadlineReminder,
            It.IsAny<string>(),
            It.Is<string>(message => message.Contains(dispute.Id.ToString())),
            NotificationChannel.Email,
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            NotificationPriority.High,
            dispute.Id,
            nameof(PaymentDispute),
            It.IsAny<string?>(),
            "support@example.com",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void RecordDisputeFee_RecordsPositiveFeesOnUnresolvedDisputes()
    {
        var dispute = OpenDispute();

        dispute.RecordDisputeFee(15.5m);

        dispute.DisputeFee.Should().Be(15.5m);
    }

    [Fact]
    public void RecordDisputeFee_RejectsNegativeFees()
    {
        var dispute = OpenDispute();
        var act = () => dispute.RecordDisputeFee(-1m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RecordDisputeFee_TerminalDisputesKeepTheRecordedFee()
    {
        var dispute = OpenDispute();
        dispute.RecordDisputeFee(15m);
        dispute.MarkAsWon("merchant evidence accepted", Guid.NewGuid());

        dispute.RecordDisputeFee(99m);

        dispute.DisputeFee.Should().Be(15m);
    }

    [Fact]
    public void RefundKind_DefaultsToFull_AndRecordsProratedKinds()
    {
        var refund = Refund.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            10m,
            "USD",
            "goodwill",
            null,
            Guid.NewGuid(),
            requiresApproval: false,
            $"idem-{Guid.NewGuid():N}");

        refund.Kind.Should().Be(RefundKind.Full);

        var prorated = Refund.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            10m,
            "USD",
            "subscription_cancellation",
            null,
            Guid.NewGuid(),
            requiresApproval: false,
            $"idem-{Guid.NewGuid():N}",
            kind: RefundKind.Prorated);

        prorated.Kind.Should().Be(RefundKind.Prorated);
    }

    [Fact]
    public void EvidenceType_IncludesInvoiceAndSubscriptionRecordKinds()
    {
        ((int)EvidenceType.Invoice).Should().Be(9);
        ((int)EvidenceType.SubscriptionRecord).Should().Be(10);
    }

    [Fact]
    public async Task ResolveDisputeAsync_BooksTheFeeInThePaymentCurrency()
    {
        await using var context = await CreateContextAsync();
        var payment = Payment.Create(Guid.NewGuid(), 100m, "EUR", $"idem-{Guid.NewGuid():N}");
        payment.MarkAsProcessing();
        payment.MarkAsSucceeded("pi_1", "ch_1");
        context.Set<Payment>().Add(payment);
        var dispute = OpenDispute();
        dispute.PaymentId = payment.Id;
        dispute.RecordDisputeFee(15m);
        context.Set<PaymentDispute>().Add(dispute);
        await context.SaveChangesAsync();
        var service = new DisputeService(context, NullLogger<DisputeService>.Instance, _revenueAudit.Object);

        await service.ResolveDisputeAsync(dispute.Id, DisputeResolution.Lost, "merchant evidence");

        _revenueAudit.Verify(audit => audit.CreateLedgerEntryAsync(
            LedgerEntryType.Fee,
            It.IsAny<string>(),
            It.IsAny<string>(),
            15m,
            "EUR",
            It.IsAny<string>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveDisputeAsync_MissingPayment_FallsBackToUsdWithTheFeeStillBooked()
    {
        await using var context = await CreateContextAsync();
        var dispute = OpenDispute();
        dispute.RecordDisputeFee(15m);
        context.Set<PaymentDispute>().Add(dispute);
        await context.SaveChangesAsync();
        var service = new DisputeService(context, NullLogger<DisputeService>.Instance, _revenueAudit.Object);

        await service.ResolveDisputeAsync(dispute.Id, DisputeResolution.Lost, "merchant evidence");

        _revenueAudit.Verify(audit => audit.CreateLedgerEntryAsync(
            LedgerEntryType.Fee,
            It.IsAny<string>(),
            It.IsAny<string>(),
            15m,
            "USD",
            It.IsAny<string>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private DisputeNotificationService CreateService(
        PaymentsPersistenceTestDbContext context,
        string[] emails) =>
        new(
            context,
            _notifications.Object,
            Options.Create(new DisputeNotificationOptions { TeamAlertEmails = emails }),
            NullLogger<DisputeNotificationService>.Instance);

    private static PaymentDispute OpenDispute(
        DateTime? evidenceDueBy = null,
        DisputeStatus status = DisputeStatus.Submitted)
    {
        var dispute = new PaymentDispute
        {
            PaymentId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Type = DisputeType.Fraudulent,
            Status = status,
            Amount = 50m,
            Reason = "fraudulent"
        };
        if (evidenceDueBy.HasValue)
        {
            dispute.BindGatewayIdentity("stripe", $"dp_{Guid.NewGuid():N}", evidenceDueBy);
        }

        return dispute;
    }
}
