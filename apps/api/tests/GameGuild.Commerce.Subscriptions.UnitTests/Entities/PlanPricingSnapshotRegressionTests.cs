using FluentAssertions;
using Xunit;

namespace GameGuild.Commerce.Subscriptions.UnitTests.Entities;

/// <summary>
///     Issue #416 audit gap 3: subscription-snapshot protection was implemented at the
///     entity level but never verified end-to-end. These tests create a subscription,
///     reprice its plan, and assert the existing subscription is billed at the snapshotted
///     amount — "Price updates only affect new subscriptions (not existing)".
/// </summary>
public sealed class PlanPricingSnapshotRegressionTests
{
    private static readonly DateTime PaymentDate = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void PlanPriceUpdate_DoesNotChangeExistingSubscriptionSnapshot()
    {
        // Arrange
        var plan = new SubscriptionPlan("Professional", "professional", 2999, "USD");
        var subscription = CreateActiveSubscription(plan.Id, 29.99m, "USD");

        // Act — the plan is repriced to $99.99 after the subscription was created.
        plan.UpdatePricing(9999, 99990);
        plan.UpdateLimits(maxUsers: 1);

        // Assert — the subscription's authoritative snapshot is untouched.
        subscription.Amount.Should().Be(new Money(29.99m, "USD"));
    }

    [Fact]
    public void PlanPriceUpdate_DoesNotChangeRenewalQuote()
    {
        // Arrange
        var plan = new SubscriptionPlan("Professional", "professional", 2999, "USD");
        var subscription = CreateActiveSubscription(plan.Id, 29.99m, "USD");
        subscription.RecordPayment(29.99m, "USD", PaymentDate, "payment-1", forBillingCycle: 1);

        // Act
        plan.UpdatePricing(9999, 99990);

        // Assert — the next renewal is quoted from the snapshot, never the plan.
        var renewal = subscription.PrepareRenewal("renewal-1");
        renewal.AmountDue.Should().Be(new Money(29.99m, "USD"));
    }

    [Fact]
    public void PlanPriceUpdate_NewPricePayment_IsRejectedForExistingSubscription()
    {
        // Arrange
        var plan = new SubscriptionPlan("Professional", "professional", 2999, "USD");
        var subscription = CreateActiveSubscription(plan.Id, 29.99m, "USD");

        // Act — the plan is repriced and a payment for the NEW price arrives.
        plan.UpdatePricing(9999);
        var result = subscription.RecordPayment(99.99m, "USD", PaymentDate, "payment-1", forBillingCycle: 1);

        // Assert — the snapshot rejects the repriced amount; the old amount still settles.
        result.IsSuccess.Should().BeFalse();
        subscription.BillingCycleCount.Should().Be(0);

        var oldPricePayment = subscription.RecordPayment(29.99m, "USD", PaymentDate, "payment-1", forBillingCycle: 1);
        oldPricePayment.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ArchivedPlan_KeepsExistingSubscriptionsBillable()
    {
        // Arrange
        var plan = new SubscriptionPlan("Professional", "professional", 2999, "USD");
        var subscription = CreateActiveSubscription(plan.Id, 29.99m, "USD");

        // Act — archive (deactivate) the plan: no new subscriptions, existing ones persist.
        plan.Deactivate();

        // Assert — the existing subscription stays active and keeps quoting its snapshot.
        plan.IsActive.Should().BeFalse();
        subscription.Status.Should().Be(SubscriptionStatus.Active);
        var renewal = subscription.PrepareRenewal("renewal-1");
        renewal.AmountDue.Should().Be(new Money(29.99m, "USD"));

        var payment = subscription.RecordPayment(29.99m, "USD", PaymentDate, "payment-1", forBillingCycle: 1);
        payment.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void PlanDiscontinuedEvent_IsRaisedOnArchive_ButNotOnRepricing()
    {
        var plan = new SubscriptionPlan("Professional", "professional", 2999, "USD");

        plan.UpdatePricing(9999);
        plan.DomainEvents.Should().NotContain(e => e is PlanDiscontinuedEvent);

        plan.Deactivate();
        plan.DomainEvents.Should().Contain(e => e is PlanDiscontinuedEvent);
    }

    private static Subscription CreateActiveSubscription(Guid planId, decimal amount, string currency)
    {
        var subscription = new Subscription(
            tenantId: Guid.NewGuid(),
            planId: planId,
            createdByUserId: Guid.NewGuid(),
            billingCycle: BillingCycle.Monthly,
            amount: new Money(amount, currency),
            startDate: PaymentDate.AddMonths(-1));
        subscription.Activate();
        return subscription;
    }
}
