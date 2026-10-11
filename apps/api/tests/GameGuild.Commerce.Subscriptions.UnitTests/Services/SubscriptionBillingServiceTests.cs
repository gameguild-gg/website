using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Subscriptions.UnitTests.Services;

public sealed class SubscriptionBillingServiceTests
{
    private static readonly DateTime PeriodStart = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<ISubscriptionRepository> _repository = new();
    private readonly Mock<ISubscriptionPlanService> _planService = new();
    private readonly SubscriptionBillingService _service;

    public SubscriptionBillingServiceTests()
    {
        _service = new SubscriptionBillingService(
            _repository.Object,
            _planService.Object,
            Mock.Of<ISubscriptionNotificationService>(),
            Mock.Of<ILogger<SubscriptionBillingService>>());
    }

    [Fact]
    public async Task ProcessRenewalAsync_ShouldPreparePaymentWithoutAdvancingOrClaimingRevenue()
    {
        var subscription = CreatePaidActiveSubscription(Guid.NewGuid());
        var initialPeriodStart = subscription.CurrentPeriodStart;
        var initialPeriodEnd = subscription.CurrentPeriodEnd;
        var initialNextBillingDate = subscription.NextBillingDate;
        _repository
            .Setup(repository => repository.GetByIdAsync(subscription.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _planService
            .Setup(service => service.GetByIdAsync(subscription.PlanId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionPlan("Plan", "plan", 2999));

        var result = await _service.ProcessRenewalAsync(subscription.Id);

        result.Success.Should().BeFalse();
        result.PaymentRequired.Should().BeTrue();
        result.RequiredBillingCycle.Should().Be(2);
        result.AmountDue.Should().Be(subscription.Amount);
        result.ChargedAmount.Should().BeNull();
        subscription.BillingCycleCount.Should().Be(1);
        subscription.CurrentPeriodStart.Should().Be(initialPeriodStart);
        subscription.CurrentPeriodEnd.Should().Be(initialPeriodEnd);
        subscription.NextBillingDate.Should().Be(initialNextBillingDate);
        _planService.Verify(
            service => service.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _repository.Verify(
            repository => repository.UpdateAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()),
            Times.Never);

        var paymentResult = subscription.RecordPayment(
            subscription.Amount.Amount,
            subscription.Amount.Currency,
            initialNextBillingDate,
            "provider-payment-cycle-2",
            forBillingCycle: 2);

        paymentResult.IsSuccess.Should().BeTrue();
        subscription.BillingCycleCount.Should().Be(2);
        subscription.CurrentPeriodStart.Should().Be(initialNextBillingDate);
    }

    [Fact]
    public async Task ProcessBulkRenewalsAsync_ShouldPrepareAllWithoutAdvancingOrRecognizingRevenue()
    {
        var first = CreatePaidActiveSubscription(Guid.NewGuid());
        var second = CreatePaidActiveSubscription(Guid.NewGuid());
        var subscriptions = new[] { first, second };
        var initialPeriods = subscriptions.ToDictionary(
            subscription => subscription.Id,
            subscription => (subscription.CurrentPeriodStart, subscription.CurrentPeriodEnd, subscription.NextBillingDate));

        foreach (var subscription in subscriptions)
        {
            _repository
                .Setup(repository => repository.GetByIdAsync(subscription.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(subscription);
        }

        _planService
            .Setup(service => service.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionPlan("Plan", "plan", 2999));

        var result = await _service.ProcessBulkRenewalsAsync(subscriptions.Select(subscription => subscription.Id));

        result.TotalProcessed.Should().Be(2);
        result.SuccessfulRenewals.Should().Be(0);
        result.FailedRenewals.Should().Be(2);
        result.TotalRevenue.Should().Be(Money.Zero());
        result.RenewalAttempts.Should().HaveCount(2);
        result.RenewalAttempts.Should().OnlyContain(attempt =>
            !attempt.Success &&
            attempt.Amount == Money.Zero() &&
            attempt.ErrorMessage != null &&
            attempt.ErrorMessage.Contains("payment confirmation", StringComparison.OrdinalIgnoreCase));

        foreach (var subscription in subscriptions)
        {
            var initialPeriod = initialPeriods[subscription.Id];
            subscription.BillingCycleCount.Should().Be(1);
            subscription.CurrentPeriodStart.Should().Be(initialPeriod.CurrentPeriodStart);
            subscription.CurrentPeriodEnd.Should().Be(initialPeriod.CurrentPeriodEnd);
            subscription.NextBillingDate.Should().Be(initialPeriod.NextBillingDate);
        }

        _planService.Verify(
            service => service.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _repository.Verify(
            repository => repository.UpdateAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RecordPaymentAsync_ShouldRecordTheNextExpectedCycle()
    {
        // Provider-confirmed payments arrive without an explicit local cycle number; the service
        // must pass the subscription's next unprocessed cycle so the entity accepts the recording
        // (a cycle-less recording is rejected) and Stripe invoice.payment_succeeded can complete.
        var subscription = CreatePaidActiveSubscription(Guid.NewGuid());
        _repository
            .Setup(repository => repository.GetByIdAsync(subscription.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _repository
            .Setup(repository => repository.UpdateAsync(subscription, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        await _service.RecordPaymentAsync(subscription.Id, 29.99m, "USD", PeriodStart.AddMonths(1));

        subscription.LastProcessedBillingCycle.Should().Be(2);
        subscription.BillingCycleCount.Should().Be(2);
        _repository.Verify(
            repository => repository.UpdateAsync(subscription, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static Subscription CreatePaidActiveSubscription(Guid id)
    {
        var subscription = new Subscription(
            tenantId: Guid.NewGuid(),
            planId: Guid.NewGuid(),
            createdByUserId: Guid.NewGuid(),
            billingCycle: BillingCycle.Monthly,
            amount: new Money(29.99m, "USD"),
            startDate: PeriodStart);
        typeof(Subscription).GetProperty(nameof(Subscription.Id))!.SetValue(subscription, id);
        subscription.Activate();
        subscription.RecordPayment(
            29.99m,
            "USD",
            PeriodStart,
            $"provider-payment-{id}",
            forBillingCycle: 1);

        return subscription;
    }
}
