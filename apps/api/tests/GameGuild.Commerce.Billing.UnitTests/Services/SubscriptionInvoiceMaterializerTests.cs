using FluentAssertions;
using GameGuild.Commerce.Subscriptions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Services;

public sealed class SubscriptionInvoiceMaterializerTests
{
    private readonly Mock<IInvoiceGenerationService> _inner = new();
    private readonly SubscriptionInvoiceMaterializer _materializer;

    public SubscriptionInvoiceMaterializerTests()
    {
        _materializer = new SubscriptionInvoiceMaterializer(
            _inner.Object,
            NullLogger<SubscriptionInvoiceMaterializer>.Instance);
    }

    [Fact]
    public async Task Materialize_DelegatesToGenerationService_WithCycleRequest()
    {
        var subscriptionId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var processedAt = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        _inner
            .Setup(service => service.MaterializeForConfirmedCycleAsync(
                It.Is<ConfirmedCycleInvoiceRequest>(request =>
                    request.SubscriptionId == subscriptionId &&
                    request.BillingCycleNumber == 3 &&
                    request.Amount == 19m &&
                    request.Currency == "EUR" &&
                    request.ProcessedAtUtc == processedAt &&
                    request.PaymentId == paymentId &&
                    request.ProviderInvoiceId == "in_delegate"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvoiceGenerationResult(new Invoice(subscriptionId, subscriptionId, 19m, "EUR"), Created: true));

        await _materializer.MaterializeForConfirmedCycleAsync(subscriptionId, 3, 19m, "EUR", processedAt, paymentId, "in_delegate");

        _inner.VerifyAll();
    }

    [Fact]
    public async Task Materialize_SwallowsFailures_PaymentSuccessMustNotBeAffected()
    {
        _inner
            .Setup(service => service.MaterializeForConfirmedCycleAsync(It.IsAny<ConfirmedCycleInvoiceRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("storage unavailable"));

        var act = async () => await _materializer.MaterializeForConfirmedCycleAsync(Guid.NewGuid(), 1, 10m, "USD", DateTime.UtcNow);

        await act.Should().NotThrowAsync();
    }
}
