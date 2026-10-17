using System.Text.Json;
using FluentAssertions;
using GameGuild.Commerce.Payments;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Services;

/// <summary>
///     Stripe <c>payment_method.updated</c> reconciliation into the local payment-method
///     registry (issue #414 follow-up): verified webhook payloads map to card metadata
///     (brand, last4, expiry), registered methods are refreshed in place, unknown gateway
///     tokens are consumed as a no-op, and unrelated event types fall through.
/// </summary>
public class StripePaymentMethodUpdatedConsumerTests
{
    private readonly Mock<IPaymentMethodRepository> _paymentMethodRepository = new();
    private readonly Mock<ILogger<StripePaymentMethodUpdatedConsumer>> _logger = new();

    [Fact]
    public void PaymentMethodUpdated_Payload_MapsToCardMetadata()
    {
        var payload = CreatePayload("pm_123", brand: "visa", last4: "4242", expMonth: 12, expYear: 2030, fingerprint: "fp_1");

        var parsed = StripePaymentMethodUpdatedConsumer.ParsePaymentMethodPayload(payload);

        parsed.Should().NotBeNull();
        parsed!.PaymentMethodId.Should().Be("pm_123");
        parsed.Type.Should().Be("card");
        parsed.CardBrand.Should().Be("visa");
        parsed.CardLast4.Should().Be("4242");
        parsed.CardExpMonth.Should().Be(12);
        parsed.CardExpYear.Should().Be(2030);
        parsed.CardFingerprint.Should().Be("fp_1");
    }

    [Fact]
    public async Task Consumer_UpdatesRegisteredMethods_WithFreshExpiryAndBrand()
    {
        var method = new PaymentMethod(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "stripe",
            "pm_123",
            PaymentMethodType.Card,
            isDefault: true);
        method.UpdateGatewayMetadata(PaymentMethodType.Card, "mastercard", "1111", 5, 2026, "fp_old");
        _paymentMethodRepository
            .Setup(repository => repository.ListByGatewayIdAsync("stripe", "pm_123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { method });

        var consumed = await CreateConsumer().TryConsumeAsync(
            CreateEvent("payment_method.updated", CreatePayload("pm_123", brand: "visa", last4: "4242", expMonth: 12, expYear: 2030, fingerprint: "fp_1")),
            CancellationToken.None);

        consumed.Should().BeTrue();
        method.CardBrand.Should().Be("visa");
        method.CardLast4.Should().Be("4242");
        method.CardExpMonth.Should().Be(12);
        method.CardExpYear.Should().Be(2030);
        method.CardFingerprint.Should().Be("fp_1");
        method.ExpiresAt.Should().Be(PaymentMethod.ComputeExpiresAt(12, 2030));

        _paymentMethodRepository.Verify(
            repository => repository.UpdateAsync(method, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UnknownGatewayToken_IsConsumedAsNoOp()
    {
        _paymentMethodRepository
            .Setup(repository => repository.ListByGatewayIdAsync("stripe", "pm_unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PaymentMethod>());

        var consumed = await CreateConsumer().TryConsumeAsync(
            CreateEvent("payment_method.updated", CreatePayload("pm_unknown", brand: "visa", last4: "0000", expMonth: 1, expYear: 2028, fingerprint: null)),
            CancellationToken.None);

        consumed.Should().BeTrue("an unregistered gateway token must not fail the webhook inbox");
        _paymentMethodRepository.Verify(
            repository => repository.UpdateAsync(It.IsAny<PaymentMethod>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("payment_method.attached")]
    [InlineData("charge.dispute.created")]
    [InlineData("invoice.payment_succeeded")]
    public async Task OtherEventTypes_FallThrough(string eventType)
    {
        var consumed = await CreateConsumer().TryConsumeAsync(
            CreateEvent(eventType, CreatePayload("pm_123", brand: "visa", last4: "4242", expMonth: 12, expYear: 2030, fingerprint: null)),
            CancellationToken.None);

        consumed.Should().BeFalse("the consumer only claims payment-method update events");
        _paymentMethodRepository.Verify(
            repository => repository.ListByGatewayIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AutomaticallyUpdatedVariant_IsAlsoConsumed()
    {
        _paymentMethodRepository
            .Setup(repository => repository.ListByGatewayIdAsync("stripe", "pm_123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PaymentMethod>());

        var consumed = await CreateConsumer().TryConsumeAsync(
            CreateEvent("payment_method.automatically_updated", CreatePayload("pm_123", brand: "visa", last4: "4242", expMonth: 12, expYear: 2030, fingerprint: null)),
            CancellationToken.None);

        consumed.Should().BeTrue("the Account Updater variant carries the same metadata shape");
    }

    private StripePaymentMethodUpdatedConsumer CreateConsumer() =>
        new(_paymentMethodRepository.Object, _logger.Object);

    private static VerifiedStripeWebhookEvent CreateEvent(string eventType, string payload) =>
        new()
        {
            EventId = $"evt_{Guid.NewGuid():N}",
            EventType = eventType,
            ProviderEnvironment = "test",
            ProviderAccountId = "platform",
            WebhookEndpointId = "we_test",
            EventSchemaVersion = "2023-10-16",
            ProviderObjectId = "pm_123",
            ProviderObjectType = "payment_method",
            ProviderMonetaryLeg = "nonmonetary",
            VerifiedPayload = payload,
            RetainedPayload = "{}",
            PayloadSha256 = new string('0', 64)
        };

    private static string CreatePayload(
        string paymentMethodId,
        string brand,
        string last4,
        int expMonth,
        int expYear,
        string? fingerprint) =>
        JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["id"] = $"evt_{Guid.NewGuid():N}",
            ["type"] = "payment_method.updated",
            ["livemode"] = false,
            ["data"] = new Dictionary<string, object?>
            {
                ["object"] = new Dictionary<string, object?>
                {
                    ["id"] = paymentMethodId,
                    ["object"] = "payment_method",
                    ["type"] = "card",
                    ["card"] = new Dictionary<string, object?>
                    {
                        ["brand"] = brand,
                        ["last4"] = last4,
                        ["exp_month"] = expMonth,
                        ["exp_year"] = expYear,
                        ["fingerprint"] = fingerprint
                    }
                }
            }
        });
}
