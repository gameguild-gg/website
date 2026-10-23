using FluentAssertions;
using Xunit;

namespace GameGuild.Commerce.Subscriptions.UnitTests.Entities;

/// <summary>
///     Issue #418: consumption of promo grants on the subscription aggregate —
///     <see cref="Subscription.ExtendTrial"/> (FreeTrialExtensionDays codes) and
///     <see cref="Subscription.SkipBillingPeriods"/> (FreeMonths codes).
/// </summary>
public class SubscriptionTrialExtensionTests
{
    private static Subscription CreatePendingSubscription(decimal amount = 29.99m)
    {
        var startDate = SystemClock.UtcNow;
        return new Subscription(
            tenantId: Guid.NewGuid(),
            planId: Guid.NewGuid(),
            createdByUserId: Guid.NewGuid(),
            billingCycle: BillingCycle.Monthly,
            amount: new Money(amount, "USD"),
            startDate: startDate);
    }

    [Fact]
    public void ExtendTrial_OnPendingSubscription_TransitionsToTrialingAndGrantsTrial()
    {
        var subscription = CreatePendingSubscription();

        var newEnd = subscription.ExtendTrial(14);

        subscription.Status.Should().Be(SubscriptionStatus.Trialing);
        subscription.TrialEndDate.Should().Be(newEnd);
        newEnd.Should().BeAfter(SystemClock.UtcNow);
        subscription.DomainEvents.OfType<TrialExtendedEvent>()
            .Should().ContainSingle().Which.AdditionalDays.Should().Be(14);
    }

    [Fact]
    public void ExtendTrial_OnTrialingSubscription_ExtendsFromCurrentTrialEnd()
    {
        var startDate = SystemClock.UtcNow;
        var trialEnd = startDate.AddDays(10);
        var subscription = new Subscription(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BillingCycle.Monthly,
            new Money(29.99m, "USD"), startDate, trialEnd);

        var newEnd = subscription.ExtendTrial(5);

        newEnd.Date.Should().Be(trialEnd.AddDays(5).Date);
        subscription.Status.Should().Be(SubscriptionStatus.Trialing);
    }

    [Fact]
    public void ExtendTrial_PushesNextBillingDateBeyondTheExtendedTrial()
    {
        var subscription = CreatePendingSubscription();
        var originalNextBilling = subscription.NextBillingDate;

        var newEnd = subscription.ExtendTrial(45);

        // The first charge cannot land inside the trial: the next billing date moves to
        // the new trial end and the period end shifts by the same amount.
        subscription.NextBillingDate.Should().BeOnOrAfter(newEnd);
        subscription.CurrentPeriodEnd.Should().BeAfter(originalNextBilling);
    }

    [Fact]
    public void ExtendTrial_WithNonPositiveDays_Throws()
    {
        var subscription = CreatePendingSubscription();

        var act = () => subscription.ExtendTrial(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ExtendTrial_OnCancelledSubscription_Throws()
    {
        var subscription = CreatePendingSubscription();
        subscription.Cancel(CancellationReason.UserRequested);

        var act = () => subscription.ExtendTrial(7);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SkipBillingPeriods_AdvancesNextBillingDateAndPeriodEnd()
    {
        var subscription = CreatePendingSubscription();
        var originalNextBilling = subscription.NextBillingDate;
        var originalPeriodEnd = subscription.CurrentPeriodEnd;

        var newNextBilling = subscription.SkipBillingPeriods(2);

        newNextBilling.Should().Be(originalNextBilling.AddMonths(2));
        subscription.NextBillingDate.Should().Be(newNextBilling);
        subscription.CurrentPeriodEnd.Should().Be(originalPeriodEnd.AddMonths(2));
        subscription.DomainEvents.OfType<BillingPeriodsSkippedEvent>()
            .Should().ContainSingle().Which.SkippedPeriods.Should().Be(2);
    }

    [Fact]
    public void SkipBillingPeriods_WithNonPositivePeriods_Throws()
    {
        var subscription = CreatePendingSubscription();

        var act = () => subscription.SkipBillingPeriods(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
