using FluentAssertions;
using GameGuild.Commerce.Billing;
using GameGuild.Commerce.Subscriptions;
using GameGuild.Identity.Users;
using GameGuild.Notifications;
using MockQueryable.Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Services;

/// <summary>
///     Issue #418 invoice application: promo discounts linked to a subscription are carried
///     onto the materialized invoice (gross subtotal + discount, total stays the paid amount)
///     and the consuming promo usage rows are stamped with the invoice id.
/// </summary>
public sealed class InvoicePromoApplicationTests
{
    [Fact]
    public void ApplyPromoDiscount_GrossesUpSubtotal_AndKeepsTotalAtPaidAmount()
    {
        var invoice = new Invoice(Guid.NewGuid(), Guid.NewGuid(), 80m, "USD", idempotencyKey: "k");

        invoice.ApplyPromoDiscount(20m);

        invoice.Subtotal.Should().Be(100m);
        invoice.DiscountAmount.Should().Be(20m);
        invoice.Total.Should().Be(80m); // the confirmed payment amount
        invoice.AmountRemaining.Should().Be(80m);
    }

    [Fact]
    public void ApplyPromoDiscount_ZeroDiscount_IsNoOp()
    {
        var invoice = new Invoice(Guid.NewGuid(), Guid.NewGuid(), 80m, "USD", idempotencyKey: "k");

        invoice.ApplyPromoDiscount(0m);

        invoice.Subtotal.Should().Be(80m);
        invoice.DiscountAmount.Should().Be(0m);
        invoice.Total.Should().Be(80m);
    }

    [Fact]
    public void ApplyPromoDiscount_NegativeDiscount_Throws()
    {
        var invoice = new Invoice(Guid.NewGuid(), Guid.NewGuid(), 80m, "USD", idempotencyKey: "k");

        var act = () => invoice.ApplyPromoDiscount(-1m);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ApplyPromoDiscount_AfterIssue_Throws()
    {
        var invoice = new Invoice(Guid.NewGuid(), Guid.NewGuid(), 80m, "USD", idempotencyKey: "k");
        invoice.Issue(SystemClock.UtcNow);

        var act = () => invoice.ApplyPromoDiscount(20m);

        act.Should().Throw<InvalidOperationException>().WithMessage("*immutable*");
    }

    [Fact]
    public async Task MaterializeForConfirmedCycle_WithUninvoicedPromo_CarriesDiscountAndStampsUsage()
    {
        var harness = new Harness();
        var subscriptionId = harness.SeedSubscription(amount: 80m);
        var invoiceId = Guid.Empty;
        var usageIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        harness.PromoCoordinator
            .Setup(c => c.GetUninvoicedPromoContextAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameGuild.Commerce.SubscriptionPromoInvoiceContext(
                subscriptionId,
                20m,
                new[]
                {
                    new GameGuild.Commerce.SubscriptionPromoCodeApplication(Guid.NewGuid(), "SAVE20", 20m, "PercentageOff")
                },
                usageIds));
        harness.PromoCoordinator
            .Setup(c => c.MarkPromoUsesInvoicedAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback((IReadOnlyList<Guid> ids, Guid invoice, CancellationToken _) =>
            {
                invoiceId = invoice;
                ids.Should().BeEquivalentTo(usageIds);
            })
            .ReturnsAsync(usageIds.Count);

        var result = await harness.Service.MaterializeForConfirmedCycleAsync(new ConfirmedCycleInvoiceRequest(
            SubscriptionId: subscriptionId,
            BillingCycleNumber: 1,
            Amount: 80m,
            Currency: "USD",
            ProcessedAtUtc: SystemClock.UtcNow,
            PaymentId: Guid.NewGuid()));

        result.Created.Should().BeTrue();
        result.Invoice.Subtotal.Should().Be(100m);
        result.Invoice.DiscountAmount.Should().Be(20m);
        result.Invoice.Total.Should().Be(80m);
        result.Invoice.Status.Should().Be(InvoiceStatus.Paid);

        harness.PromoCoordinator.Verify(
            c => c.MarkPromoUsesInvoicedAsync(usageIds, result.Invoice.Id, It.IsAny<CancellationToken>()),
            Times.Once);
        invoiceId.Should().Be(result.Invoice.Id);
    }

    [Fact]
    public async Task MaterializeForConfirmedCycle_WithoutPromoContext_LeavesAmountsUntouched()
    {
        var harness = new Harness();
        var subscriptionId = harness.SeedSubscription(amount: 80m);
        harness.PromoCoordinator
            .Setup(c => c.GetUninvoicedPromoContextAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameGuild.Commerce.SubscriptionPromoInvoiceContext?)null);

        var result = await harness.Service.MaterializeForConfirmedCycleAsync(new ConfirmedCycleInvoiceRequest(
            SubscriptionId: subscriptionId,
            BillingCycleNumber: 1,
            Amount: 80m,
            Currency: "USD",
            ProcessedAtUtc: SystemClock.UtcNow,
            PaymentId: Guid.NewGuid()));

        result.Invoice.Subtotal.Should().Be(80m);
        result.Invoice.DiscountAmount.Should().Be(0m);
        result.Invoice.Total.Should().Be(80m);
        harness.PromoCoordinator.Verify(
            c => c.MarkPromoUsesInvoicedAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private sealed class Harness
    {
        public readonly Mock<IApplicationDbContext> Context = new();
        public readonly Mock<ISubscriptionRepository> SubscriptionRepository = new();
        public readonly Mock<GameGuild.Commerce.ISubscriptionPromoCoordinator> PromoCoordinator = new();
        public readonly List<Invoice> Invoices = [];
        public readonly InvoiceGenerationService Service;

        public Harness()
        {
            var invoiceSet = Invoices.AsQueryable().BuildMockDbSet();
            invoiceSet
                .Setup(set => set.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Invoice entity, CancellationToken _) =>
                {
                    Invoices.Add(entity);
                    return null!;
                });
            invoiceSet
                .Setup(set => set.Add(It.IsAny<Invoice>()))
                .Callback((Invoice entity) => Invoices.Add(entity));
            invoiceSet
                .Setup(set => set.Remove(It.IsAny<Invoice>()))
                .Returns((Invoice _) => null!);
            invoiceSet
                .Setup(set => set.Update(It.IsAny<Invoice>()))
                .Returns((Invoice _) => null!);
            Context.Setup(context => context.Set<Invoice>()).Returns(invoiceSet.Object);
            Context
                .Setup(context => context.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            Service = new InvoiceGenerationService(
                Context.Object,
                SubscriptionRepository.Object,
                Mock.Of<IUserRepository>(),
                Mock.Of<INotificationService>(),
                Mock.Of<GameGuild.Commerce.Payments.IReferralService>(),
                NullLogger<InvoiceGenerationService>.Instance,
                PromoCoordinator.Object);
        }

        public Guid SeedSubscription(decimal amount)
        {
            var subscription = new Subscription(
                tenantId: Guid.NewGuid(),
                planId: Guid.NewGuid(),
                createdByUserId: Guid.NewGuid(),
                billingCycle: BillingCycle.Monthly,
                amount: new Money(amount, "USD"),
                startDate: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));

            SubscriptionRepository
                .Setup(r => r.GetByIdAsync(subscription.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(subscription);

            return subscription.Id;
        }
    }
}
