using FluentAssertions;
using GameGuild.Commerce;
using GameGuild.Commerce.Subscriptions;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Services;

public class StripeBillingWebhookServiceTests
{
    [Fact]
    public async Task ProcessStripeWebhookAsync_Should_Return_AlreadyProcessed_For_Duplicate()
    {
        var repository = new Mock<IBillingWebhookRepository>();
        repository
            .Setup(r => r.GetByProviderScopeAsync(
                PaymentProviders.Stripe,
                "test",
                "platform",
                "we_test",
                "evt_1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BillingWebhookEvent
            {
                ExternalEventId = "evt_1",
                Provider = PaymentProviders.Stripe,
                IsProcessed = true,
                ProcessedAt = DateTime.UtcNow
            });

        var service = CreateService(repository, Mock.Of<ISubscriptionQueryService>(), Mock.Of<ISubscriptionBillingService>(), Mock.Of<ISubscriptionLifecycleService>(), Mock.Of<ISubscriptionExternalIdService>());

        var result = await service.ProcessStripeWebhookAsync(
            "{\"id\":\"evt_1\",\"type\":\"unknown.event\",\"data\":{\"object\":{\"id\":\"in_1\"}}}",
            "sig",
            CancellationToken.None);

        result.WasAlreadyProcessed.Should().BeTrue();
    }

    [Fact]
    public async Task ReplayStripeWebhookAsync_Should_Return_AlreadyProcessed_When_Row_Completed()
    {
        var repository = new Mock<IBillingWebhookRepository>();
        var service = CreateService(repository, Mock.Of<ISubscriptionQueryService>(), Mock.Of<ISubscriptionBillingService>(), Mock.Of<ISubscriptionLifecycleService>(), Mock.Of<ISubscriptionExternalIdService>());

        var row = new BillingWebhookEvent
        {
            ExternalEventId = "evt_done",
            Provider = PaymentProviders.Stripe,
            IsProcessed = true,
            ProcessedAt = DateTime.UtcNow,
            IsSignatureVerified = true
        };

        var result = await service.ReplayStripeWebhookAsync(row, CancellationToken.None);

        result.WasAlreadyProcessed.Should().BeTrue();
        repository.Verify(r => r.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReplayStripeWebhookAsync_Should_Refuse_Rows_Without_Verification_Proof()
    {
        // Fail closed: a legacy row with no retained verification proof must never be
        // re-routed by the retry pipeline (issue #408).
        var repository = new Mock<IBillingWebhookRepository>();
        repository
            .Setup(r => r.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);
        var service = CreateService(repository, Mock.Of<ISubscriptionQueryService>(), Mock.Of<ISubscriptionBillingService>(), Mock.Of<ISubscriptionLifecycleService>(), Mock.Of<ISubscriptionExternalIdService>());

        var legacyRow = new BillingWebhookEvent
        {
            ExternalEventId = "evt_legacy",
            Provider = PaymentProviders.Stripe,
            EventType = "customer.subscription.updated",
            Payload = "{}"
        };

        var result = await service.ReplayStripeWebhookAsync(legacyRow, CancellationToken.None);

        result.Processed.Should().BeFalse();
        result.RequiresRetry.Should().BeFalse();
        legacyRow.IsProcessed.Should().BeFalse();
    }

    [Fact]
    public async Task ReplayStripeWebhookAsync_Should_Reroute_The_Stored_Delivery()
    {
        // The retry pipeline's execution arm: a verified stored row is replayed through the
        // same event routing used for live deliveries and marked processed on success.
        var repository = new Mock<IBillingWebhookRepository>();
        repository
            .Setup(r => r.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);

        var subscription = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BillingCycle.Monthly, new Money(10m, "USD"), DateTime.UtcNow);
        var queryService = new Mock<ISubscriptionQueryService>();
        queryService
            .Setup(q => q.GetByExternalIdAsync("sub_replay", It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        var lifecycle = new Mock<ISubscriptionLifecycleService>();

        var service = CreateService(repository, queryService.Object, Mock.Of<ISubscriptionBillingService>(), lifecycle.Object, Mock.Of<ISubscriptionExternalIdService>());

        var row = new BillingWebhookEvent
        {
            ExternalEventId = "evt_replay",
            Provider = PaymentProviders.Stripe,
            ProviderEnvironment = "test",
            ProviderAccountId = "platform",
            WebhookEndpointId = "we_test",
            ProviderObjectId = "sub_replay",
            ProviderObjectType = "subscription",
            ProviderMonetaryLeg = "nonmonetary",
            EventType = "customer.subscription.updated",
            Payload = "{\"id\":\"evt_replay\",\"type\":\"customer.subscription.updated\",\"data\":{\"object\":{\"id\":\"sub_replay\",\"status\":\"active\"}}}",
            IsSignatureVerified = true,
            SignatureVerifiedAt = DateTime.UtcNow.AddMinutes(-10)
        };

        var result = await service.ReplayStripeWebhookAsync(row, CancellationToken.None);

        result.Processed.Should().BeTrue();
        result.WasAlreadyProcessed.Should().BeFalse();
        row.IsProcessed.Should().BeTrue();
        lifecycle.Verify(l => l.ActivateAsync(subscription.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessStripeWebhookAsync_Should_Retry_Failed_Durable_Event_On_Redelivery()
    {
        var failedEvent = new BillingWebhookEvent
        {
            ExternalEventId = "evt_retry",
            Provider = PaymentProviders.Stripe,
            ProviderEnvironment = "test",
            ProviderAccountId = "platform",
            WebhookEndpointId = "we_test",
            EventType = "unknown.event",
            Payload = "{}",
            IsFailed = true,
            ProcessingAttempts = 1
        };
        var repository = new Mock<IBillingWebhookRepository>();
        repository
            .Setup(r => r.GetByProviderScopeAsync(
                PaymentProviders.Stripe,
                "test",
                "platform",
                "we_test",
                "evt_retry",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(failedEvent);
        repository
            .Setup(r => r.UpdateAsync(failedEvent, It.IsAny<CancellationToken>()))
            .ReturnsAsync(failedEvent);
        var service = CreateService(
            repository,
            Mock.Of<ISubscriptionQueryService>(),
            Mock.Of<ISubscriptionBillingService>(),
            Mock.Of<ISubscriptionLifecycleService>(),
            Mock.Of<ISubscriptionExternalIdService>());

        var result = await service.ProcessStripeWebhookAsync(
            "{\"id\":\"evt_retry\",\"type\":\"unknown.event\",\"data\":{\"object\":{\"id\":\"obj\"}}}",
            "sig",
            CancellationToken.None);

        result.Processed.Should().BeTrue();
        result.WasAlreadyProcessed.Should().BeFalse();
        failedEvent.ProcessingAttempts.Should().Be(2);
        failedEvent.IsProcessed.Should().BeTrue();
        failedEvent.IsFailed.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessStripeWebhookAsync_Should_Activate_Subscription_On_Status_Change()
    {
        var repository = new Mock<IBillingWebhookRepository>();
        repository
            .Setup(r => r.GetByExternalEventIdAsync("evt_2", PaymentProviders.Stripe, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);
        repository
            .Setup(r => r.CreateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);
        repository
            .Setup(r => r.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);

        var subscription = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BillingCycle.Monthly, new Money(10m, "USD"), DateTime.UtcNow);
        var queryService = new Mock<ISubscriptionQueryService>();
        queryService
            .Setup(q => q.GetByExternalIdAsync("sub_123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        var lifecycle = new Mock<ISubscriptionLifecycleService>();

        var service = CreateService(repository, queryService.Object, Mock.Of<ISubscriptionBillingService>(), lifecycle.Object, Mock.Of<ISubscriptionExternalIdService>());

        var payload = "{\"id\":\"evt_2\",\"type\":\"customer.subscription.updated\",\"data\":{\"object\":{\"id\":\"sub_123\",\"status\":\"active\"}}}";

        var result = await service.ProcessStripeWebhookAsync(payload, "sig", CancellationToken.None);

        result.Processed.Should().BeTrue();
        lifecycle.Verify(l => l.ActivateAsync(subscription.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessStripeWebhookAsync_Should_Record_Payment_On_Payment_Succeeded()
    {
        var repository = new Mock<IBillingWebhookRepository>();
        repository
            .Setup(r => r.GetByExternalEventIdAsync("evt_3", PaymentProviders.Stripe, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);
        repository
            .Setup(r => r.CreateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);
        repository
            .Setup(r => r.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);

        var subscription = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BillingCycle.Monthly, new Money(50m, "USD"), DateTime.UtcNow);
        var queryService = new Mock<ISubscriptionQueryService>();
        queryService
            .Setup(q => q.GetByExternalIdAsync("sub_456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        var billingService = new Mock<ISubscriptionBillingService>();

        var service = CreateService(repository, queryService.Object, billingService.Object, Mock.Of<ISubscriptionLifecycleService>(), Mock.Of<ISubscriptionExternalIdService>());

        var payload = "{\"id\":\"evt_3\",\"type\":\"invoice.payment_succeeded\",\"data\":{\"object\":{\"subscription\":\"sub_456\",\"amount_paid\":5000,\"currency\":\"usd\"}}}";

        var result = await service.ProcessStripeWebhookAsync(payload, "sig", CancellationToken.None);

        result.Processed.Should().BeTrue();
        billingService.Verify(b => b.RecordPaymentAsync(subscription.Id, 50m, "USD", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

        [Fact]
        public async Task ProcessStripeWebhookAsync_Should_Record_Failure_On_Payment_Failed()
        {
            var repository = new Mock<IBillingWebhookRepository>();
            repository
                .Setup(r => r.GetByExternalEventIdAsync("evt_4", PaymentProviders.Stripe, It.IsAny<CancellationToken>()))
                .ReturnsAsync((BillingWebhookEvent?)null);
            repository
                .Setup(r => r.CreateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);
            repository
                .Setup(r => r.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);

            var subscription = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BillingCycle.Monthly, new Money(10m, "USD"), DateTime.UtcNow);
            var queryService = new Mock<ISubscriptionQueryService>();
            queryService
                .Setup(q => q.GetByExternalIdAsync("sub_456", It.IsAny<CancellationToken>()))
                .ReturnsAsync(subscription);

            var billingService = new Mock<ISubscriptionBillingService>();

            var service = CreateService(repository, queryService.Object, billingService.Object, Mock.Of<ISubscriptionLifecycleService>(), Mock.Of<ISubscriptionExternalIdService>());

            var payload = "{\"id\":\"evt_4\",\"type\":\"invoice.payment_failed\",\"data\":{\"object\":{\"subscription\":\"sub_456\",\"amount_due\":1000,\"currency\":\"usd\"}}}";

            var result = await service.ProcessStripeWebhookAsync(payload, "sig", CancellationToken.None);

            result.Processed.Should().BeTrue();
            billingService.Verify(b => b.RecordPaymentFailureAsync(subscription.Id, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ProcessStripeWebhookAsync_Should_Cancel_Subscription_On_Deleted()
        {
            var repository = new Mock<IBillingWebhookRepository>();
            repository
                .Setup(r => r.GetByExternalEventIdAsync("evt_5", PaymentProviders.Stripe, It.IsAny<CancellationToken>()))
                .ReturnsAsync((BillingWebhookEvent?)null);
            repository
                .Setup(r => r.CreateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);
            repository
                .Setup(r => r.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);

            var subscription = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BillingCycle.Monthly, new Money(10m, "USD"), DateTime.UtcNow);
            var queryService = new Mock<ISubscriptionQueryService>();
            queryService
                .Setup(q => q.GetByExternalIdAsync("sub_789", It.IsAny<CancellationToken>()))
                .ReturnsAsync(subscription);

            var lifecycle = new Mock<ISubscriptionLifecycleService>();

            var service = CreateService(repository, queryService.Object, Mock.Of<ISubscriptionBillingService>(), lifecycle.Object, Mock.Of<ISubscriptionExternalIdService>());

            var payload = "{\"id\":\"evt_5\",\"type\":\"customer.subscription.deleted\",\"data\":{\"object\":{\"id\":\"sub_789\"}}}";

            var result = await service.ProcessStripeWebhookAsync(payload, "sig", CancellationToken.None);

            result.Processed.Should().BeTrue();
            lifecycle.Verify(l => l.CancelAsync(subscription.Id, CancellationReason.Custom, It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ProcessStripeWebhookAsync_Should_Handle_Unhandled_Event_Type()
        {
            var repository = new Mock<IBillingWebhookRepository>();
            repository
                .Setup(r => r.GetByExternalEventIdAsync("evt_6", PaymentProviders.Stripe, It.IsAny<CancellationToken>()))
                .ReturnsAsync((BillingWebhookEvent?)null);
            repository
                .Setup(r => r.CreateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);
            repository
                .Setup(r => r.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);

            var service = CreateService(repository, Mock.Of<ISubscriptionQueryService>(), Mock.Of<ISubscriptionBillingService>(), Mock.Of<ISubscriptionLifecycleService>(), Mock.Of<ISubscriptionExternalIdService>());

            var payload = "{\"id\":\"evt_6\",\"type\":\"unknown.event\",\"data\":{\"object\":{}}}";
            var result = await service.ProcessStripeWebhookAsync(payload, "sig", CancellationToken.None);

            result.Processed.Should().BeTrue();
        }

    [Fact]
    public async Task ProcessStripeWebhookAsync_Should_Request_Retry_When_Another_Worker_Owns_Claim()
    {
        var activeEvent = new BillingWebhookEvent
        {
            ExternalEventId = "evt_active",
            Provider = PaymentProviders.Stripe,
            ProviderEnvironment = "test",
            ProviderAccountId = "platform",
            WebhookEndpointId = "we_test",
            EventType = "unknown.event",
            Payload = "{}",
            ProcessingAttempts = 1,
            UpdatedAt = DateTime.UtcNow
        };
        var repository = new Mock<IBillingWebhookRepository>();
        repository
            .Setup(candidate => candidate.GetByProviderScopeAsync(
                PaymentProviders.Stripe,
                "test",
                "platform",
                "we_test",
                "evt_active",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeEvent);
        repository
            .Setup(candidate => candidate.TryClaimProcessingAsync(
                activeEvent,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateService(
            repository,
            Mock.Of<ISubscriptionQueryService>(),
            Mock.Of<ISubscriptionBillingService>(),
            Mock.Of<ISubscriptionLifecycleService>(),
            Mock.Of<ISubscriptionExternalIdService>());

        var result = await service.ProcessStripeWebhookAsync(
            "{\"id\":\"evt_active\",\"type\":\"unknown.event\",\"data\":{\"object\":{\"id\":\"obj\"}}}",
            "sig",
            CancellationToken.None);

        result.Processed.Should().BeFalse();
        result.RequiresRetry.Should().BeTrue();
        repository.Verify(
            candidate => candidate.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProcessStripeWebhookAsync_OffersVerifiedEventsToRegisteredConsumers(bool handled)
    {
        var repository = new Mock<IBillingWebhookRepository>();
        repository
            .Setup(candidate => candidate.CreateAsync(
                It.IsAny<BillingWebhookEvent>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent webhookEvent, CancellationToken _) => webhookEvent);
        repository
            .Setup(candidate => candidate.UpdateAsync(
                It.IsAny<BillingWebhookEvent>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent webhookEvent, CancellationToken _) => webhookEvent);
        var consumer = new Mock<IStripeVerifiedEventConsumer>();
        consumer
            .Setup(candidate => candidate.TryConsumeAsync(
                It.IsAny<VerifiedStripeWebhookEvent>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(handled);
        var service = CreateService(
            repository,
            Mock.Of<ISubscriptionQueryService>(),
            Mock.Of<ISubscriptionBillingService>(),
            Mock.Of<ISubscriptionLifecycleService>(),
            Mock.Of<ISubscriptionExternalIdService>(),
            [consumer.Object]);

        var result = await service.ProcessStripeWebhookAsync(
            "{\"id\":\"evt_consumer\",\"type\":\"custom.verified\",\"data\":{\"object\":{\"id\":\"obj\"}}}",
            "sig",
            CancellationToken.None);

        result.Processed.Should().BeTrue();
        consumer.Verify(candidate => candidate.TryConsumeAsync(
            It.Is<VerifiedStripeWebhookEvent>(item => item.EventId == "evt_consumer"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static StripeBillingWebhookService CreateService(
        Mock<IBillingWebhookRepository> repository,
        ISubscriptionQueryService queryService,
        ISubscriptionBillingService billingService,
        ISubscriptionLifecycleService lifecycleService,
        ISubscriptionExternalIdService externalIdService,
        IEnumerable<IStripeVerifiedEventConsumer>? verifiedEventConsumers = null)
    {
        repository
            .Setup(candidate => candidate.TryClaimProcessingAsync(
                It.IsAny<BillingWebhookEvent>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent webhookEvent, DateTime staleBefore, CancellationToken _) =>
                webhookEvent.TryBeginProcessing(staleBefore));

        var verifier = new Mock<IStripeWebhookVerifier>();
        verifier
            .Setup(candidate => candidate.Verify(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string payload, string _) => CreateVerifiedEvent(payload));

        return new StripeBillingWebhookService(
            repository.Object,
            verifier.Object,
            CreateNoOpProviderObjectBindingValidator(),
            NullLogger<StripeBillingWebhookService>.Instance,
            lifecycleService,
            queryService,
            billingService,
            externalIdService,
            verifiedEventConsumers: verifiedEventConsumers);
    }

    private static IStripeProviderObjectBindingValidator CreateNoOpProviderObjectBindingValidator()
    {
        var validator = new Mock<IStripeProviderObjectBindingValidator>();
        validator
            .Setup(candidate => candidate.ValidateAsync(
                It.IsAny<VerifiedStripeWebhookEvent>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((StripeWebhookPaymentBinding?)null);
        return validator.Object;
    }

    private static VerifiedStripeWebhookEvent CreateVerifiedEvent(string payload)
    {
        using var document = System.Text.Json.JsonDocument.Parse(payload);
        var root = document.RootElement;
        var eventId = root.GetProperty("id").GetString()!;
        var eventType = root.GetProperty("type").GetString()!;
        var providerObject = root.GetProperty("data").GetProperty("object");
        var externalSubscriptionId = providerObject.TryGetProperty("subscription", out var subscriptionProperty)
            ? subscriptionProperty.GetString()
            : eventType.StartsWith("customer.subscription.", StringComparison.Ordinal) &&
              providerObject.TryGetProperty("id", out var objectIdProperty)
                ? objectIdProperty.GetString()
                : null;
        decimal? amount = providerObject.TryGetProperty("amount_paid", out var amountPaid)
            ? amountPaid.GetDecimal() / 100m
            : providerObject.TryGetProperty("amount_due", out var amountDue)
                ? amountDue.GetDecimal() / 100m
                : null;

        return new VerifiedStripeWebhookEvent
        {
            EventId = eventId,
            EventType = eventType,
            ProviderEnvironment = "test",
            ProviderAccountId = "platform",
            WebhookEndpointId = "we_test",
            EventSchemaVersion = "2023-10-16",
            ProviderObjectId = "obj_test",
            ProviderObjectType = "test_object",
            ProviderMonetaryLeg = "nonmonetary",
            ExternalSubscriptionId = externalSubscriptionId,
            Amount = amount,
            Currency = providerObject.TryGetProperty("currency", out var currencyProperty)
                ? currencyProperty.GetString()?.ToUpperInvariant()
                : null,
            VerifiedPayload = payload,
            RetainedPayload = payload,
            PayloadSha256 = new string('0', 64)
        };
    }
}
