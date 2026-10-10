using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Events;

public sealed class BillingIntegrationEventPublisherTests
{
    private readonly Mock<IDurableEventProducer> _producer = new();
    private readonly Mock<IUseCaseOperationContextAccessor> _operationContexts = new();

    private BillingIntegrationEventPublisher CreatePublisher() => new(
        _producer.Object,
        _operationContexts.Object,
        NullLogger<BillingIntegrationEventPublisher>.Instance);

    [Fact]
    public async Task PublishWebhookProcessedAsync_RecordsNamedEventWithInboxAggregate()
    {
        var webhookEvent = new BillingWebhookEvent
        {
            Provider = PaymentProviders.Stripe,
            ExternalEventId = "evt_1",
            EventType = "invoice.payment_succeeded",
            ProcessingAttempts = 2
        };

        await CreatePublisher().PublishWebhookProcessedAsync(webhookEvent);

        var recorded = _producer.Invocations
            .Single(invocation => invocation.Method.Name == nameof(IDurableEventProducer.RecordAsync))
            .Arguments[0] as BillingWebhookProcessedV1;

        recorded.Should().NotBeNull();
        recorded!.EventName.Should().Be("commerce.billing.webhook-processed.v1");
        recorded.SourceModule.Should().Be("Commerce.Billing");
        recorded.AggregateType.Should().Be("BillingWebhookEvent");
        recorded.AggregateId.Should().Be(webhookEvent.Id.ToString());
        recorded.Provider.Should().Be(PaymentProviders.Stripe);
        recorded.ExternalEventId.Should().Be("evt_1");
        recorded.AttemptNumber.Should().Be(2);
        recorded.TenantId.Should().Be(DurableIntegrationEventTenants.Platform,
            "webhook rows without a tenant resolve to the platform tenant");
    }

    [Fact]
    public async Task PublishWebhookFailedAsync_RecordsFailedAttemptEvent()
    {
        var tenantId = Guid.NewGuid();
        var webhookEvent = new BillingWebhookEvent
        {
            Provider = PaymentProviders.PayPal,
            ExternalEventId = "txn_1",
            EventType = "PAYMENT.SALE.COMPLETED",
            TenantId = tenantId,
            ErrorMessage = "downstream unavailable"
        };
        webhookEvent.MarkAsFailed("downstream unavailable");
        webhookEvent.IncrementAttempts();

        await CreatePublisher().PublishWebhookFailedAsync(webhookEvent);

        var recorded = _producer.Invocations
            .Single(invocation => invocation.Method.Name == nameof(IDurableEventProducer.RecordAsync))
            .Arguments[0] as BillingWebhookFailedV1;

        recorded.Should().NotBeNull();
        recorded!.EventName.Should().Be("commerce.billing.webhook-failed.v1");
        recorded.TenantId.Should().Be(tenantId);
        recorded.ErrorMessage.Should().Be("downstream unavailable");
    }

    [Fact]
    public async Task PublishInvoicePaidAsync_RecordsSubscriptionAggregateEvent()
    {
        var subscriptionId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        await CreatePublisher().PublishInvoicePaidAsync(
            subscriptionId, tenantId, PaymentProviders.Stripe, "evt_9", 42m, "USD");

        var recorded = _producer.Invocations
            .Single(invocation => invocation.Method.Name == nameof(IDurableEventProducer.RecordAsync))
            .Arguments[0] as BillingInvoicePaidV1;

        recorded.Should().NotBeNull();
        recorded!.EventName.Should().Be("commerce.billing.invoice-paid.v1");
        recorded.AggregateType.Should().Be("Subscription");
        recorded.AggregateId.Should().Be(subscriptionId.ToString());
        recorded.Amount.Should().Be(42m);
        recorded.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task RecordAsync_CorrelatesToAmbientUseCaseOperation()
    {
        var correlationId = Guid.NewGuid();
        var causationId = Guid.NewGuid();
        _operationContexts
            .Setup(accessor => accessor.Current)
            .Returns(new UseCaseOperationContext("commerce.billing.process-stripe-webhook",
                nameof(ProcessStripeWebhookCommand), Guid.NewGuid(), Guid.NewGuid(), correlationId, causationId));

        await CreatePublisher().PublishSubscriptionRenewedAsync(
            Guid.NewGuid(), Guid.NewGuid(), PaymentProviders.Stripe, "evt_renewal");

        var recorded = _producer.Invocations
            .Single(invocation => invocation.Method.Name == nameof(IDurableEventProducer.RecordAsync))
            .Arguments[0] as BillingSubscriptionRenewedV1;

        recorded.Should().NotBeNull();
        recorded!.CorrelationId.Should().Be(correlationId,
            "named events must correlate with the emitting use-case operation");
        recorded.CausationId.Should().Be(causationId);
    }

    [Fact]
    public async Task RecordAsync_SwallowsProducerFailures()
    {
        _producer
            .Setup(producer => producer.RecordAsync(It.IsAny<IDurableIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("outbox unavailable"));

        var act = async () => await CreatePublisher()
            .PublishSubscriptionCancelledAsync(Guid.NewGuid(), Guid.NewGuid(), PaymentProviders.Stripe, "evt_x");

        await act.Should().NotThrowAsync(
            "billing event publication is best-effort and must never change the webhook processing outcome");
    }

    [Fact]
    public async Task RecordAsync_PropagatesCancellation()
    {
        _producer
            .Setup(producer => producer.RecordAsync(It.IsAny<IDurableIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var act = async () => await CreatePublisher()
            .PublishSubscriptionCancelledAsync(Guid.NewGuid(), Guid.NewGuid(), PaymentProviders.Stripe, "evt_x");

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
