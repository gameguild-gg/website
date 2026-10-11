using System.Data.Common;
using FluentAssertions;
using GameGuild.Commerce.Subscriptions;
using GameGuild.Identity.Users;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using MockQueryable.Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Services;

public sealed class InvoiceGenerationServiceTests
{
    private readonly Mock<IApplicationDbContext> _context = new();
    private readonly Mock<ISubscriptionRepository> _subscriptionRepository = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly List<Invoice> _invoices = [];
    private readonly InvoiceGenerationService _service;

    public InvoiceGenerationServiceTests()
    {
        var invoiceSet = _invoices.AsQueryable().BuildMockDbSet();
        invoiceSet
            .Setup(set => set.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Invoice entity, CancellationToken _) =>
            {
                _invoices.Add(entity);
                return null!;
            });
        // The service persists through the synchronous DbSet.Add (EF's canonical path),
        // so the fake must capture that too or the in-memory invoice list stays empty.
        invoiceSet
            .Setup(set => set.Add(It.IsAny<Invoice>()))
            .Returns((Invoice entity) =>
            {
                _invoices.Add(entity);
                return entity;
            });
        invoiceSet
            .Setup(set => set.Remove(It.IsAny<Invoice>()))
            .Returns((Invoice entity) => null!);
        invoiceSet
            .Setup(set => set.Update(It.IsAny<Invoice>()))
            .Returns((Invoice entity) => null!);
        _context.Setup(context => context.Set<Invoice>()).Returns(invoiceSet.Object);
        _context
            .Setup(context => context.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _service = new InvoiceGenerationService(
            _context.Object,
            _subscriptionRepository.Object,
            _userRepository.Object,
            _notificationService.Object,
            NullLogger<InvoiceGenerationService>.Instance);
    }

    private static (Subscription Subscription, User User) ArrangeSubscriptionAndRecipient(decimal amount = 29.99m)
    {
        var subscription = new Subscription(
            tenantId: Guid.NewGuid(),
            planId: Guid.NewGuid(),
            createdByUserId: Guid.NewGuid(),
            billingCycle: BillingCycle.Monthly,
            amount: new Money(amount, "USD"),
            startDate: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));

        var user = User.CreateOAuthUser("subscriber@example.test", "Subscriber");
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, subscription.CreatedByUserId);

        return (subscription, user);
    }

    /// <summary>
    ///     Creates one subscription + recipient pair, wires the repositories to resolve them, and
    ///     returns the pair so tests build requests against the SAME subscription the mocks answer for.
    /// </summary>
    private (Subscription Subscription, User User) ArrangeHappyPath(decimal amount = 29.99m)
    {
        var (subscription, user) = ArrangeSubscriptionAndRecipient(amount);
        _subscriptionRepository
            .Setup(repository => repository.GetByIdAsync(subscription.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _userRepository
            .Setup(repository => repository.GetByIdAsync(subscription.CreatedByUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        return (subscription, user);
    }

    private ConfirmedCycleInvoiceRequest CreateRequest(
        Guid subscriptionId,
        int cycle,
        decimal amount = 29.99m,
        Guid? paymentId = null,
        string? providerInvoiceId = null) =>
        new(
            subscriptionId,
            cycle,
            amount,
            "USD",
            new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
            paymentId,
            providerInvoiceId);

    [Fact]
    public void BuildIdempotencyKey_ShouldUseSubscriptionCycleInvoiceFormat()
    {
        var subscriptionId = Guid.NewGuid();

        var key = InvoiceGenerationService.BuildIdempotencyKey(subscriptionId, 7);

        key.Should().Be($"subscription:{subscriptionId}:cycle:7:invoice");
    }

    [Fact]
    public void DeriveProviderPaymentId_ShouldBeDeterministicAndDistinct()
    {
        var first = InvoiceGenerationService.DeriveProviderPaymentId("in_first");
        var second = InvoiceGenerationService.DeriveProviderPaymentId("in_first");
        var other = InvoiceGenerationService.DeriveProviderPaymentId("in_other");

        first.Should().Be(second);
        first.Should().NotBe(other);
    }

    [Fact]
    public async Task Materialize_CreatesIssuedPaidInvoice_WhenPaymentIsConfirmed()
    {
        var (subscription, _) = ArrangeHappyPath();
        var paymentId = Guid.NewGuid();
        var request = CreateRequest(subscription.Id, 1, paymentId: paymentId);

        var result = await _service.MaterializeForConfirmedCycleAsync(request);

        result.Created.Should().BeTrue();
        var invoice = result.Invoice;
        invoice.SubscriptionId.Should().Be(subscription.Id);
        invoice.InvoiceNumber.Should().StartWith("INV-");
        invoice.Total.Should().Be(29.99m);
        invoice.Subtotal.Should().Be(29.99m);
        invoice.Currency.Should().Be("USD");
        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.PaymentId.Should().Be(paymentId);
        invoice.PaidAt.Should().Be(request.ProcessedAtUtc);
        invoice.DueDate.Should().Be(request.ProcessedAtUtc);
        invoice.IssuedAt.Should().NotBeNull();
        invoice.PeriodStart.Should().Be(subscription.CurrentPeriodStart);
        invoice.PeriodEnd.Should().Be(subscription.CurrentPeriodEnd);
        invoice.Description.Should().Be("Subscription billing cycle 1");
        invoice.IdempotencyKey.Should().Be($"subscription:{subscription.Id}:cycle:1:invoice");
        _invoices.Should().ContainSingle();
    }

    [Fact]
    public async Task Materialize_IsIdempotent_DoubleConfirmationYieldsOneInvoice()
    {
        var (subscription, _) = ArrangeHappyPath();
        var paymentId = Guid.NewGuid();
        var request = CreateRequest(subscription.Id, 1, paymentId: paymentId);

        var first = await _service.MaterializeForConfirmedCycleAsync(request);
        var second = await _service.MaterializeForConfirmedCycleAsync(request);

        second.Created.Should().BeFalse("double-confirming a cycle must not create a second invoice");
        second.Invoice.Id.Should().Be(first.Invoice.Id);
        _invoices.Should().ContainSingle();
    }

    [Fact]
    public async Task Materialize_IsIdempotent_WhenProviderPaymentKeyRepeats()
    {
        var (subscription, _) = ArrangeHappyPath();
        var request = CreateRequest(subscription.Id, 1, providerInvoiceId: "in_repeat");

        var first = await _service.MaterializeForConfirmedCycleAsync(request);
        var second = await _service.MaterializeForConfirmedCycleAsync(request);

        second.Created.Should().BeFalse();
        second.Invoice.Id.Should().Be(first.Invoice.Id);
        _invoices.Should().ContainSingle();
    }

    [Fact]
    public async Task Materialize_DistinctCycles_ProduceDistinctInvoices()
    {
        var (subscription, _) = ArrangeHappyPath();
        var paymentId = Guid.NewGuid();

        var cycle1 = await _service.MaterializeForConfirmedCycleAsync(CreateRequest(subscription.Id, 1, paymentId: paymentId));
        var cycle2 = await _service.MaterializeForConfirmedCycleAsync(CreateRequest(subscription.Id, 2, paymentId: paymentId));

        cycle1.Created.Should().BeTrue();
        cycle2.Created.Should().BeTrue();
        cycle2.Invoice.Id.Should().NotBe(cycle1.Invoice.Id);
        cycle2.Invoice.IdempotencyKey.Should().Be($"subscription:{subscription.Id}:cycle:2:invoice");
        _invoices.Should().HaveCount(2);
    }

    [Fact]
    public async Task Materialize_StampsProviderExternalId_AndRecordsDerivedPayment()
    {
        var (subscription, _) = ArrangeHappyPath();
        var request = CreateRequest(subscription.Id, 1, providerInvoiceId: "in_provider123");

        var result = await _service.MaterializeForConfirmedCycleAsync(request);

        result.Invoice.ExternalId.Should().Be("in_provider123");
        result.Invoice.PaymentId.Should().Be(InvoiceGenerationService.DeriveProviderPaymentId("in_provider123"));
        result.Invoice.Status.Should().Be(InvoiceStatus.Paid);
    }

    [Fact]
    public async Task Materialize_OnReplay_StampsProviderExternalIdWhenMissing()
    {
        var (subscription, _) = ArrangeHappyPath();
        var withoutProvider = CreateRequest(subscription.Id, 1, providerInvoiceId: "in_late");
        var first = await _service.MaterializeForConfirmedCycleAsync(withoutProvider);
        first.Invoice.ExternalId.Should().Be("in_late");

        // Simulate a materialization that happened before the provider id was known.
        _invoices.Clear();
        var bare = new Invoice(
            ((ISubscription)subscription).TenantId,
            subscription.Id,
            29.99m,
            "USD",
            $"subscription:{subscription.Id}:cycle:2:invoice");
        bare.SetDescription("Subscription billing cycle 2");
        bare.Issue(new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc));
        _invoices.Add(bare);

        var replay = await _service.MaterializeForConfirmedCycleAsync(CreateRequest(subscription.Id, 2, providerInvoiceId: "in_late2"));

        replay.Created.Should().BeFalse();
        replay.Invoice.Should().BeSameAs(bare);
        replay.Invoice.ExternalId.Should().Be("in_late2");
        replay.Invoice.PaymentId.Should().Be(InvoiceGenerationService.DeriveProviderPaymentId("in_late2"));
        replay.Invoice.Status.Should().Be(InvoiceStatus.Paid);
        _invoices.Should().ContainSingle();
    }

    [Fact]
    public async Task Materialize_Throws_WhenSubscriptionIsMissing()
    {
        var missingSubscriptionId = Guid.NewGuid();
        _subscriptionRepository
            .Setup(repository => repository.GetByIdAsync(missingSubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);

        var act = async () => await _service.MaterializeForConfirmedCycleAsync(CreateRequest(missingSubscriptionId, 1));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Materialize_ProducesRowReadableBySubscriptionInvoiceReadModel()
    {
        // SubscriptionInvoiceReadModel is a keyless SQL projection over the invoices table, so the
        // columns the GET subscriptions/{id}/invoices endpoint returns are exactly the persisted
        // invoice columns asserted here (see SubscriptionInvoiceReadModelConfiguration.ToSqlQuery).
        var (subscription, _) = ArrangeHappyPath();
        var paymentId = Guid.NewGuid();

        var result = await _service.MaterializeForConfirmedCycleAsync(CreateRequest(subscription.Id, 1, paymentId: paymentId));
        var invoice = result.Invoice;

        invoice.SubscriptionId.Should().Be(subscription.Id);
        invoice.InvoiceNumber.Should().NotBeNullOrWhiteSpace();
        invoice.Total.Should().BePositive();
        invoice.Currency.Should().Be("USD");
        invoice.CreatedAt.Should().BeOnOrBefore(DateTime.UtcNow);
        invoice.IssuedAt.Should().NotBeNull();
        invoice.DueDate.Should().NotBeNull();
        invoice.PaidAt.Should().NotBeNull();
        // Read-model Status is the raw enum int; 2 == "Paid" per GetSubscriptionInvoicesHandler.MapInvoiceStatus.
        ((int)invoice.Status).Should().Be(2);
        invoice.PaymentId.Should().Be(paymentId);
        invoice.ExternalId.Should().BeNull();
    }

    [Fact]
    public async Task Materialize_DispatchesInvoiceIssuedEmailNotification()
    {
        var (subscription, user) = ArrangeHappyPath();
        var paymentId = Guid.NewGuid();

        var result = await _service.MaterializeForConfirmedCycleAsync(CreateRequest(subscription.Id, 1, paymentId: paymentId));

        _notificationService.Verify(service => service.SendAsync(
            user.Id,
            NotificationType.InvoiceIssued,
            It.Is<string>(title => title.Contains(result.Invoice.InvoiceNumber)),
            It.IsAny<string>(),
            NotificationChannel.Email,
            ((ISubscription)subscription).TenantId,
            It.IsAny<string?>(),
            GameGuild.Notifications.NotificationPriority.Normal,
            result.Invoice.Id,
            nameof(Invoice),
            It.Is<string>(metadata =>
                metadata.Contains($"\"invoiceId\":\"{result.Invoice.Id}\"") &&
                metadata.Contains($"\"invoiceNumber\":\"{result.Invoice.InvoiceNumber}\"") &&
                metadata.Contains("\"billingCycleNumber\":1")),
            user.Email,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Materialize_DoesNotRedispatchEmail_OnIdempotentReplay()
    {
        var (subscription, _) = ArrangeHappyPath();
        var request = CreateRequest(subscription.Id, 1, paymentId: Guid.NewGuid());

        await _service.MaterializeForConfirmedCycleAsync(request);
        await _service.MaterializeForConfirmedCycleAsync(request);

        _notificationService.Verify(service => service.SendAsync(
            It.IsAny<Guid?>(),
            It.IsAny<NotificationType>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<NotificationChannel>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<GameGuild.Notifications.NotificationPriority>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Materialize_EmailDispatchFailure_DoesNotFailMaterialization()
    {
        var (subscription, _) = ArrangeHappyPath();
        _notificationService
            .Setup(service => service.SendAsync(
                It.IsAny<Guid?>(),
                It.IsAny<NotificationType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<NotificationChannel>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<GameGuild.Notifications.NotificationPriority>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("queue unavailable"));

        var result = await _service.MaterializeForConfirmedCycleAsync(CreateRequest(subscription.Id, 1, paymentId: Guid.NewGuid()));

        result.Created.Should().BeTrue("the invoice row is already persisted; email dispatch is best effort");
        _invoices.Should().ContainSingle();
    }

    [Fact]
    public async Task Materialize_SkipsEmail_WhenRecipientCannotReceiveMail()
    {
        var (subscription, user) = ArrangeSubscriptionAndRecipient();
        user.Deactivate();
        _subscriptionRepository
            .Setup(repository => repository.GetByIdAsync(subscription.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _userRepository
            .Setup(repository => repository.GetByIdAsync(subscription.CreatedByUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await _service.MaterializeForConfirmedCycleAsync(CreateRequest(subscription.Id, 1, paymentId: Guid.NewGuid()));

        result.Created.Should().BeTrue();
        _notificationService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Materialize_ConcurrentInsertBackstop_DegradesToIdempotentReplay()
    {
        // The unique IX_invoices_IdempotencyKey index is the concurrency backstop: a racing insert
        // loses with a 23505, detaches, re-fetches by key and reports an idempotent replay instead
        // of surfacing the constraint violation.
        var (subscription, _) = ArrangeHappyPath();

        _context
            .SetupSequence(context => context.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Throws(new DbUpdateException(
                "duplicate key value violates unique constraint \"IX_invoices_IdempotencyKey\"",
                new FakeUniqueViolationException()))
            .ReturnsAsync(1);

        var act = async () => await _service.MaterializeForConfirmedCycleAsync(CreateRequest(subscription.Id, 1, providerInvoiceId: "in_backstop"));

        var result = await act();
        result.Created.Should().BeFalse();
        result.Invoice.ExternalId.Should().Be("in_backstop");
        result.Invoice.Status.Should().Be(InvoiceStatus.Paid);
    }

    private sealed class FakeUniqueViolationException : DbException
    {
        public override string SqlState => "23505";

        public FakeUniqueViolationException()
            : base("duplicate key value violates unique constraint \"IX_invoices_IdempotencyKey\"")
        {
        }
    }
}
