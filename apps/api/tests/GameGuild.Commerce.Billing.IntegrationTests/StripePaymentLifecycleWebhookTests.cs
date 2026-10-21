using System.Text.Json;
using FluentAssertions;
using GameGuild.Commerce.Payments;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.IntegrationTests;

/// <summary>
///     Stripe payment lifecycle webhook reconciliation (issue #415): payload parsing and the
///     payment-intent outcome, refund outcome and charge-refunded paths of
///     <see cref="StripePaymentLifecycleEventConsumer" />.
/// </summary>
public class StripePaymentLifecycleWebhookTests
{
    [Fact]
    public void ParseLifecyclePayload_ExtractsTheProviderObjectFields()
    {
        var payload = LifecycleEventPayload(
            eventType: "payment_intent.succeeded",
            objectId: "pi_1",
            status: "succeeded",
            chargeId: "ch_1");

        var parsed = StripePaymentLifecycleEventConsumer.ParseLifecyclePayload(payload);

        parsed.Should().NotBeNull();
        parsed!.ObjectId.Should().Be("pi_1");
        parsed.Status.Should().Be("succeeded");
        parsed.ChargeId.Should().Be("ch_1");
    }

    [Fact]
    public void ParseLifecyclePayload_RejectsMalformedPayloads()
    {
        StripePaymentLifecycleEventConsumer.ParseLifecyclePayload("{ not json").Should().BeNull();
        StripePaymentLifecycleEventConsumer.ParseLifecyclePayload("""{"data":{"object":{"object":"refund"}}}""").Should().BeNull();
    }

    [Fact]
    public async Task TryConsumeAsync_UnrelatedEvents_AreReleased()
    {
        var consumer = CreateConsumer();

        var handled = await consumer.TryConsumeAsync(
            VerifiedEvent("invoice.payment_succeeded", "in_1"),
            CancellationToken.None);

        handled.Should().BeFalse();
    }

    [Fact]
    public async Task TryConsumeAsync_SucceededIntent_CompletesPendingPayment()
    {
        var consumer = CreateConsumer(out var paymentRepository, out _, out _);
        var payment = ProcessingPayment();
        paymentRepository
            .Setup(repo => repo.GetByProviderMappingAsync("stripe", "test", "acct_platform", "pi_ok", "payment_intent", "capture", It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var handled = await consumer.TryConsumeAsync(
            VerifiedEvent("payment_intent.succeeded", "pi_ok", status: "succeeded", chargeId: "ch_ok"),
            CancellationToken.None);

        handled.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Succeeded);
        payment.ExternalPaymentId.Should().Be("pi_ok");
        paymentRepository.Verify(repo => repo.UpdateAsync(payment, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TryConsumeAsync_SucceededIntent_IsIdempotentForSettledPayments()
    {
        var consumer = CreateConsumer(out var paymentRepository, out _, out _);
        var payment = SucceededPayment();
        paymentRepository
            .Setup(repo => repo.GetByProviderMappingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        var handled = await consumer.TryConsumeAsync(
            VerifiedEvent("payment_intent.succeeded", "pi_ok"),
            CancellationToken.None);

        handled.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Succeeded);
        paymentRepository.Verify(repo => repo.UpdateAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryConsumeAsync_UnknownPayment_IsReleasedToSiblingConsumers()
    {
        var consumer = CreateConsumer(out var paymentRepository, out _, out _);
        paymentRepository
            .Setup(repo => repo.GetByProviderMappingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);
        paymentRepository
            .Setup(repo => repo.GetByExternalPaymentIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);

        var handled = await consumer.TryConsumeAsync(
            VerifiedEvent("payment_intent.succeeded", "pi_unknown"),
            CancellationToken.None);

        handled.Should().BeFalse();
    }

    [Fact]
    public async Task TryConsumeAsync_FailedIntent_FailsProcessingPaymentAndSchedulesRetry()
    {
        var consumer = CreateConsumer(out var paymentRepository, out _, out var retryPolicy);
        var payment = ProcessingPayment();
        paymentRepository
            .Setup(repo => repo.GetByProviderMappingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        var nextRetryAt = SystemClock.UtcNow.AddDays(1);
        retryPolicy
            .Setup(resolver => resolver.ComputeSchedule(null, payment.RetryCount, null))
            .Returns(new PaymentRetrySchedule(true, nextRetryAt, 4, "default"));

        var handled = await consumer.TryConsumeAsync(
            VerifiedEvent("payment_intent.payment_failed", "pi_bad", status: "failed"),
            CancellationToken.None);

        handled.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Failed);
        payment.NextRetryAt.Should().Be(nextRetryAt);
        payment.IsRetriable.Should().BeTrue();
        paymentRepository.Verify(repo => repo.UpdateAsync(payment, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TryConsumeAsync_RefundSucceeded_CompletesTheLocalRefundThroughTheCore()
    {
        var consumer = CreateConsumer(out _, out var refundRepository, out _, out var reconciledRefunds);
        var refund = ProcessingRefund();
        refundRepository
            .Setup(repo => repo.GetByGatewayRefundIdAsync("re_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(refund);

        var handled = await consumer.TryConsumeAsync(
            VerifiedEvent("refund.updated", "re_1", status: "succeeded", objectKind: "refund", paymentIntent: "pi_ok"),
            CancellationToken.None);

        handled.Should().BeTrue();
        reconciledRefunds.Should().ContainSingle()
            .Which.Should().Be(("re_1", refund));
    }

    [Fact]
    public async Task TryConsumeAsync_RefundSucceeded_UnknownGatewayRefund_IsReleased()
    {
        var consumer = CreateConsumer(out _, out var refundRepository, out _);
        refundRepository
            .Setup(repo => repo.GetByGatewayRefundIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Refund?)null);

        var handled = await consumer.TryConsumeAsync(
            VerifiedEvent("refund.created", "re_external", status: "succeeded", objectKind: "refund", paymentIntent: "pi_ok"),
            CancellationToken.None);

        handled.Should().BeFalse();
    }

    [Fact]
    public async Task TryConsumeAsync_RefundFailed_FailsTheInFlightRefundRow()
    {
        var consumer = CreateConsumer(out _, out var refundRepository, out _);
        var refund = ProcessingRefund();
        refundRepository
            .Setup(repo => repo.GetByGatewayRefundIdAsync("re_2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(refund);

        var handled = await consumer.TryConsumeAsync(
            VerifiedEvent("refund.updated", "re_2", status: "failed", objectKind: "refund", paymentIntent: "pi_ok"),
            CancellationToken.None);

        handled.Should().BeTrue();
        refund.Status.Should().Be(RefundStatus.Failed);
        refundRepository.Verify(repo => repo.UpdateAsync(refund, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TryConsumeAsync_RefundPending_LeavesTheRefundInFlight()
    {
        var consumer = CreateConsumer(out _, out var refundRepository, out _);
        var refund = ProcessingRefund();
        refundRepository
            .Setup(repo => repo.GetByGatewayRefundIdAsync("re_3", It.IsAny<CancellationToken>()))
            .ReturnsAsync(refund);

        var handled = await consumer.TryConsumeAsync(
            VerifiedEvent("refund.created", "re_3", status: "pending", objectKind: "refund", paymentIntent: "pi_ok"),
            CancellationToken.None);

        handled.Should().BeTrue();
        refund.Status.Should().Be(RefundStatus.Processing);
        refundRepository.Verify(repo => repo.UpdateAsync(It.IsAny<Refund>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryConsumeAsync_ChargeRefunded_CompletesTheSingleInFlightRefund()
    {
        var consumer = CreateConsumer(out var paymentRepository, out var refundRepository, out _, out var reconciledRefunds);
        var payment = SucceededPayment();
        var refund = ProcessingRefund(payment.Id);
        paymentRepository
            .Setup(repo => repo.GetByProviderMappingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        refundRepository
            .Setup(repo => repo.GetOpenRefundForPaymentAsync(payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(refund);

        var handled = await consumer.TryConsumeAsync(
            VerifiedEvent("charge.refunded", "pi_ok", status: "partially_refunded", objectKind: "charge"),
            CancellationToken.None);

        handled.Should().BeTrue();
        reconciledRefunds.Should().Contain(("charge_refund_pi_ok", refund));
    }

    [Fact]
    public async Task TryConsumeAsync_ChargeRefunded_WithoutInFlightRefund_DoesNothing()
    {
        var consumer = CreateConsumer(out var paymentRepository, out var refundRepository, out _, out var reconciledRefunds);
        var payment = SucceededPayment();
        paymentRepository
            .Setup(repo => repo.GetByProviderMappingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        refundRepository
            .Setup(repo => repo.GetOpenRefundForPaymentAsync(payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Refund?)null);

        var handled = await consumer.TryConsumeAsync(
            VerifiedEvent("charge.refunded", "pi_ok", status: "refunded", objectKind: "charge"),
            CancellationToken.None);

        handled.Should().BeTrue();
        reconciledRefunds.Should().BeEmpty();
    }

    private static StripePaymentLifecycleEventConsumer CreateConsumer(
        out Mock<IPaymentRepository> paymentRepository,
        out Mock<IRefundRepository> refundRepository,
        out Mock<IPaymentRetryPolicyResolver> retryPolicy,
        List<(string GatewayRefundId, Refund Refund)>? reconciledRefunds = null)
    {
        paymentRepository = new Mock<IPaymentRepository>();
        refundRepository = new Mock<IRefundRepository>();
        retryPolicy = new Mock<IPaymentRetryPolicyResolver>();
        var refundCore = new Mock<IRefundProcessingService>();
        refundCore
            .Setup(core => core.CompleteFromGatewayAsync(It.IsAny<Refund>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Refund refund, string gatewayRefundId, string gatewayName, CancellationToken _) =>
            {
                reconciledRefunds?.Add((gatewayRefundId, refund));
                return new RefundExecutionOutcome { Success = true, Refund = refund };
            });

        return new StripePaymentLifecycleEventConsumer(
            paymentRepository.Object,
            refundRepository.Object,
            refundCore.Object,
            retryPolicy.Object,
            Options.Create(new PaymentRetryOptions()),
            NullLogger<StripePaymentLifecycleEventConsumer>.Instance);
    }

    private static StripePaymentLifecycleEventConsumer CreateConsumer() =>
        CreateConsumer(out _, out _, out _);

    private static Payment ProcessingPayment()
    {
        var payment = Payment.Create(Guid.NewGuid(), 50m, "USD", $"idem-{Guid.NewGuid():N}");
        payment.MarkAsProcessing();
        return payment;
    }

    private static Payment SucceededPayment()
    {
        var payment = ProcessingPayment();
        payment.MarkAsSucceeded("pi_ok", "ch_ok");
        return payment;
    }

    private static Refund ProcessingRefund(Guid? paymentId = null)
    {
        var refund = Refund.Create(
            Guid.NewGuid(),
            paymentId ?? Guid.NewGuid(),
            50m,
            "USD",
            "customer_request",
            null,
            Guid.NewGuid(),
            requiresApproval: false,
            $"idem-{Guid.NewGuid():N}");
        refund.MarkProcessing();
        return refund;
    }

    private static VerifiedStripeWebhookEvent VerifiedEvent(
        string eventType,
        string objectId,
        string status = "succeeded",
        string objectKind = "payment_intent",
        string chargeId = "ch_ok",
        string paymentIntent = "pi_ok") =>
        new()
        {
            EventId = $"evt_{Guid.NewGuid():N}",
            EventType = eventType,
            ProviderEnvironment = "test",
            ProviderAccountId = "acct_platform",
            WebhookEndpointId = "we_1",
            EventSchemaVersion = "2024-06-20",
            ProviderObjectId = paymentIntent,
            ProviderObjectType = objectKind,
            ProviderMonetaryLeg = "capture",
            VerifiedPayload = LifecycleEventPayload(eventType, objectId, status, chargeId, objectKind),
            RetainedPayload = "{}",
            PayloadSha256 = "sha",
            OccurredAt = DateTimeOffset.UtcNow,
            IsLiveMode = false,
            Amount = 50m,
            Currency = "USD"
        };

    private static string LifecycleEventPayload(
        string eventType,
        string objectId,
        string status,
        string chargeId,
        string objectKind = "payment_intent")
    {
        var providerObject = new Dictionary<string, object?>
        {
            ["id"] = objectId,
            ["object"] = objectKind,
            ["status"] = status,
            ["amount"] = 5000,
            ["currency"] = "usd"
        };
        if (objectKind == "payment_intent")
        {
            providerObject["charge"] = chargeId;
        }
        else
        {
            providerObject["payment_intent"] = "pi_ok";
        }

        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["id"] = $"evt_{Guid.NewGuid():N}",
            ["type"] = eventType,
            ["data"] = new Dictionary<string, object?> { ["object"] = providerObject }
        });
    }
}
