using FluentAssertions;
using GameGuild.Commerce.Subscriptions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Services;

/// <summary>
///     Verifies that the Stripe <c>invoice.payment_succeeded</c> success path materializes the local
///     invoice: the provider invoice id (the event's provider object id) is stamped as the external id
///     and the confirmed cycle is the subscription's next unprocessed cycle.
/// </summary>
public sealed class StripeInvoiceMaterializationRoutingTests
{
    private readonly Mock<IBillingWebhookRepository> _webhookRepository = new();
    private readonly Mock<ISubscriptionQueryService> _queryService = new();
    private readonly Mock<IInvoiceGenerationService> _invoiceGeneration = new();
    private readonly Subscription _subscription = new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        BillingCycle.Monthly,
        new Money(29.99m, "USD"),
        new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));

    public StripeInvoiceMaterializationRoutingTests()
    {
        _webhookRepository
            .Setup(repository => repository.GetByProviderScopeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);
        _webhookRepository
            .Setup(repository => repository.GetByExternalEventIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);
        _webhookRepository
            .Setup(repository => repository.CreateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent webhookEvent, CancellationToken _) => webhookEvent);
        _webhookRepository
            .Setup(repository => repository.TryClaimProcessingAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _webhookRepository
            .Setup(repository => repository.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent webhookEvent, CancellationToken _) => webhookEvent);

        _queryService
            .Setup(query => query.GetByExternalIdAsync("sub_inv", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_subscription);
    }

    private StripeBillingWebhookService CreateService() => new(
        _webhookRepository.Object,
        new TestStripeWebhookVerifier(),
        Mock.Of<IStripeProviderObjectBindingValidator>(),
        NullLogger<StripeBillingWebhookService>.Instance,
        Mock.Of<ISubscriptionLifecycleService>(),
        _queryService.Object,
        Mock.Of<ISubscriptionBillingService>(),
        Mock.Of<ISubscriptionExternalIdService>(),
        invoiceGeneration: _invoiceGeneration.Object);

    [Fact]
    public async Task InvoicePaymentSucceeded_MaterializesInvoiceWithProviderData()
    {
        var service = CreateService();
        var payload = "{\"data\":{\"object\":{\"id\":\"in_mat1\",\"subscription\":\"sub_inv\",\"amount_paid\":2999,\"currency\":\"usd\"}}}";

        var result = await service.ProcessStripeWebhookAsync(payload, "evt_inv1|invoice.payment_succeeded");

        result.Processed.Should().BeTrue();
        _invoiceGeneration.Verify(generation => generation.MaterializeForConfirmedCycleAsync(
            It.Is<ConfirmedCycleInvoiceRequest>(request =>
                request.SubscriptionId == _subscription.Id &&
                request.BillingCycleNumber == 1 &&
                request.Amount == 29.99m &&
                request.Currency == "USD" &&
                request.PaymentId == null &&
                request.ProviderInvoiceId == "in_mat1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvoicePaymentSucceeded_MaterializesNextCycle_AfterPreviousCycleProcessed()
    {
        _subscription.RecordPayment(29.99m, "USD", DateTime.UtcNow, "payment:first", forBillingCycle: 1);

        var service = CreateService();
        var payload = "{\"data\":{\"object\":{\"id\":\"in_mat2\",\"subscription\":\"sub_inv\",\"amount_paid\":2999,\"currency\":\"usd\"}}}";

        var result = await service.ProcessStripeWebhookAsync(payload, "evt_inv2|invoice.payment_succeeded");

        result.Processed.Should().BeTrue();
        _invoiceGeneration.Verify(generation => generation.MaterializeForConfirmedCycleAsync(
            It.Is<ConfirmedCycleInvoiceRequest>(request =>
                request.BillingCycleNumber == 2 &&
                request.ProviderInvoiceId == "in_mat2"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvoicePaymentSucceeded_StillRunsSubscriptionPaymentSync()
    {
        var billingService = new Mock<ISubscriptionBillingService>();
        var service = new StripeBillingWebhookService(
            _webhookRepository.Object,
            new TestStripeWebhookVerifier(),
            Mock.Of<IStripeProviderObjectBindingValidator>(),
            NullLogger<StripeBillingWebhookService>.Instance,
            Mock.Of<ISubscriptionLifecycleService>(),
            _queryService.Object,
            billingService.Object,
            Mock.Of<ISubscriptionExternalIdService>(),
            invoiceGeneration: _invoiceGeneration.Object);

        var payload = "{\"data\":{\"object\":{\"id\":\"in_sync\",\"subscription\":\"sub_inv\",\"amount_paid\":2999,\"currency\":\"usd\"}}}";

        var result = await service.ProcessStripeWebhookAsync(payload, "evt_inv3|invoice.payment_succeeded");

        result.Processed.Should().BeTrue();
        billingService.Verify(billing => billing.RecordPaymentAsync(
            _subscription.Id,
            29.99m,
            "USD",
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvoicePaymentSucceeded_WithoutGenerationService_StillProcesses()
    {
        var service = new StripeBillingWebhookService(
            _webhookRepository.Object,
            new TestStripeWebhookVerifier(),
            Mock.Of<IStripeProviderObjectBindingValidator>(),
            NullLogger<StripeBillingWebhookService>.Instance,
            Mock.Of<ISubscriptionLifecycleService>(),
            _queryService.Object,
            Mock.Of<ISubscriptionBillingService>(),
            Mock.Of<ISubscriptionExternalIdService>());

        var payload = "{\"data\":{\"object\":{\"id\":\"in_nogen\",\"subscription\":\"sub_inv\",\"amount_paid\":2999,\"currency\":\"usd\"}}}";

        var result = await service.ProcessStripeWebhookAsync(payload, "evt_inv4|invoice.payment_succeeded");

        result.Processed.Should().BeTrue();
        _invoiceGeneration.VerifyNoOtherCalls();
    }
}
