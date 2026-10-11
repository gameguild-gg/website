using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Subscriptions.UnitTests.Services;

public sealed class PaymentSubscriptionSyncServiceTests
{
    private readonly Mock<ISubscriptionRepository> _repository = new();
    private readonly Mock<ISubscriptionInvoiceMaterializer> _invoiceMaterializer = new();
    private readonly PaymentSubscriptionSyncService _service;

    public PaymentSubscriptionSyncServiceTests()
    {
        _service = new PaymentSubscriptionSyncService(
            _repository.Object,
            _invoiceMaterializer.Object,
            Mock.Of<ILogger<PaymentSubscriptionSyncService>>());
    }

    [Fact]
    public async Task SyncSuccessfulPaymentAsync_ShouldSkip_WhenPaymentHasNoSubscription()
    {
        await _service.SyncSuccessfulPaymentAsync(
            Guid.NewGuid(),
            subscriptionId: null,
            amount: 29.99m,
            currency: "USD",
            billingCycleNumber: 1,
            processedAt: DateTime.UtcNow);

        _repository.Verify(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(repository => repository.UpdateAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SyncSuccessfulPaymentAsync_ShouldSkip_WhenSubscriptionIsMissing()
    {
        var subscriptionId = Guid.NewGuid();
        _repository
            .Setup(repository => repository.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);

        await _service.SyncSuccessfulPaymentAsync(
            Guid.NewGuid(),
            subscriptionId,
            29.99m,
            "USD",
            billingCycleNumber: 1,
            processedAt: DateTime.UtcNow);

        _repository.Verify(repository => repository.UpdateAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SyncSuccessfulPaymentAsync_ShouldRecordPayment_WhenSubscriptionExists()
    {
        var paymentId = Guid.NewGuid();
        var subscription = CreateActiveSubscription(Guid.NewGuid());
        var processedAt = DateTime.UtcNow;

        _repository
            .Setup(repository => repository.GetByIdAsync(subscription.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _repository
            .Setup(repository => repository.UpdateAsync(subscription, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        await _service.SyncSuccessfulPaymentAsync(
            paymentId,
            subscription.Id,
            29.99m,
            "USD",
            billingCycleNumber: 1,
            processedAt: processedAt);

        subscription.LastPaymentIdempotencyKey.Should().Be($"payment:{paymentId}");
        subscription.LastPaymentAt.Should().Be(processedAt);
        _repository.Verify(repository => repository.UpdateAsync(subscription, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncSuccessfulPaymentAsync_ShouldActivatePendingSubscription()
    {
        var paymentId = Guid.NewGuid();
        var subscription = CreatePendingSubscription(Guid.NewGuid());

        _repository
            .Setup(repository => repository.GetByIdAsync(subscription.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _repository
            .Setup(repository => repository.UpdateAsync(subscription, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        await _service.SyncSuccessfulPaymentAsync(
            paymentId,
            subscription.Id,
            29.99m,
            "USD",
            billingCycleNumber: 1,
            processedAt: DateTime.UtcNow);

        subscription.Status.Should().Be(SubscriptionStatus.Active);
        _repository.Verify(repository => repository.UpdateAsync(subscription, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncSuccessfulPaymentAsync_ShouldNotUpdateAgain_WhenPaymentWasAlreadyProcessed()
    {
        var paymentId = Guid.NewGuid();
        var subscription = CreateActiveSubscription(Guid.NewGuid());
        subscription.RecordPayment(29.99m, "USD", DateTime.UtcNow, $"payment:{paymentId}", forBillingCycle: 1);

        _repository
            .Setup(repository => repository.GetByIdAsync(subscription.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        await _service.SyncSuccessfulPaymentAsync(
            paymentId,
            subscription.Id,
            29.99m,
            "USD",
            billingCycleNumber: 1,
            processedAt: DateTime.UtcNow.AddMinutes(5));

        _repository.Verify(repository => repository.UpdateAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(29.98, "USD")]
    [InlineData(30.00, "USD")]
    [InlineData(29.99, "EUR")]
    public async Task SyncSuccessfulPaymentAsync_ShouldNotActivatePendingSubscription_WhenMoneyIsNotExact(
        decimal amount,
        string currency)
    {
        var subscription = CreatePendingSubscription(Guid.NewGuid());
        _repository
            .Setup(repository => repository.GetByIdAsync(subscription.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        await _service.SyncSuccessfulPaymentAsync(
            Guid.NewGuid(),
            subscription.Id,
            amount,
            currency,
            billingCycleNumber: 1,
            processedAt: DateTime.UtcNow);

        subscription.Status.Should().Be(SubscriptionStatus.PendingActivation);
        subscription.BillingCycleCount.Should().Be(0);
        _repository.Verify(repository => repository.UpdateAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SyncSuccessfulPaymentAsync_WithNullBillingCycle_ShouldFailClosed()
    {
        var subscription = CreatePendingSubscription(Guid.NewGuid());
        _repository
            .Setup(repository => repository.GetByIdAsync(subscription.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        await _service.SyncSuccessfulPaymentAsync(
            Guid.NewGuid(),
            subscription.Id,
            29.99m,
            "USD",
            billingCycleNumber: null,
            processedAt: DateTime.UtcNow);

        subscription.Status.Should().Be(SubscriptionStatus.PendingActivation);
        subscription.BillingCycleCount.Should().Be(0);
        _repository.Verify(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(repository => repository.UpdateAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SyncSuccessfulPaymentAsync_ShouldMaterializeInvoice_OnSuccess()
    {
        var paymentId = Guid.NewGuid();
        var subscription = CreatePendingSubscription(Guid.NewGuid());
        var processedAt = DateTime.UtcNow;

        _repository
            .Setup(repository => repository.GetByIdAsync(subscription.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _repository
            .Setup(repository => repository.UpdateAsync(subscription, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        await _service.SyncSuccessfulPaymentAsync(
            paymentId,
            subscription.Id,
            29.99m,
            "USD",
            billingCycleNumber: 1,
            processedAt: processedAt);

        _invoiceMaterializer.Verify(materializer => materializer.MaterializeForConfirmedCycleAsync(
            subscription.Id,
            1,
            29.99m,
            "USD",
            processedAt,
            paymentId,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncSuccessfulPaymentAsync_ShouldMaterializeInvoice_OnIdempotentReplay()
    {
        var paymentId = Guid.NewGuid();
        var subscription = CreateActiveSubscription(Guid.NewGuid());
        subscription.RecordPayment(29.99m, "USD", DateTime.UtcNow.AddMinutes(-5), $"payment:{paymentId}", forBillingCycle: 1);
        var processedAt = DateTime.UtcNow;

        _repository
            .Setup(repository => repository.GetByIdAsync(subscription.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        await _service.SyncSuccessfulPaymentAsync(
            paymentId,
            subscription.Id,
            29.99m,
            "USD",
            billingCycleNumber: 1,
            processedAt: processedAt);

        _repository.Verify(repository => repository.UpdateAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()), Times.Never);
        _invoiceMaterializer.Verify(materializer => materializer.MaterializeForConfirmedCycleAsync(
            subscription.Id,
            1,
            29.99m,
            "USD",
            processedAt,
            paymentId,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(29.98, "USD")]
    [InlineData(29.99, "EUR")]
    public async Task SyncSuccessfulPaymentAsync_ShouldNotMaterializeInvoice_WhenSyncIsRejected(decimal amount, string currency)
    {
        var subscription = CreatePendingSubscription(Guid.NewGuid());

        _repository
            .Setup(repository => repository.GetByIdAsync(subscription.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        await _service.SyncSuccessfulPaymentAsync(
            Guid.NewGuid(),
            subscription.Id,
            amount,
            currency,
            billingCycleNumber: 1,
            processedAt: DateTime.UtcNow);

        _invoiceMaterializer.Verify(materializer => materializer.MaterializeForConfirmedCycleAsync(
            It.IsAny<Guid>(),
            It.IsAny<int>(),
            It.IsAny<decimal>(),
            It.IsAny<string>(),
            It.IsAny<DateTime>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SyncSuccessfulPaymentAsync_ShouldNotFail_WhenInvoiceMaterializationThrows()
    {
        var subscription = CreatePendingSubscription(Guid.NewGuid());

        _repository
            .Setup(repository => repository.GetByIdAsync(subscription.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _repository
            .Setup(repository => repository.UpdateAsync(subscription, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _invoiceMaterializer
            .Setup(materializer => materializer.MaterializeForConfirmedCycleAsync(
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("storage unavailable"));

        var act = async () => await _service.SyncSuccessfulPaymentAsync(
            Guid.NewGuid(),
            subscription.Id,
            29.99m,
            "USD",
            billingCycleNumber: 1,
            processedAt: DateTime.UtcNow);

        await act.Should().NotThrowAsync("payment success must not be rolled back by invoice materialization");
    }

    private static Subscription CreateActiveSubscription(Guid id)
    {
        var subscription = CreatePendingSubscription(id);
        subscription.Activate();

        return subscription;
    }

    private static Subscription CreatePendingSubscription(Guid id)
    {
        var subscription = new Subscription(
            tenantId: Guid.NewGuid(),
            planId: Guid.NewGuid(),
            createdByUserId: Guid.NewGuid(),
            billingCycle: BillingCycle.Monthly,
            amount: new Money(29.99m, "USD"),
            startDate: DateTime.UtcNow,
            trialEndDate: null);

        typeof(Subscription).GetProperty(nameof(Subscription.Id))!.SetValue(subscription, id);

        return subscription;
    }
}
