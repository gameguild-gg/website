using FluentAssertions;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.GatewayRouting;

/// <summary>
///     Per-gateway 24h/7d/30d success-rate and latency aggregates computed from payment
///     records (issue #413): window membership, provider grouping, success-rate math and
///     the ProcessedAt-CreatedAt latency proxy.
/// </summary>
public class GatewayMetricsServiceTests
{
    private readonly Mock<IPaymentRepository> _repository = new();

    private static Payment NewPayment(
        string provider,
        PaymentStatus status,
        DateTime createdAt,
        DateTime? processedAt = null,
        decimal amount = 25m)
    {
        var payment = Payment.Create(
            tenantId: Guid.NewGuid(),
            amount: amount,
            currency: "USD",
            idempotencyKey: $"idem-{Guid.NewGuid():N}",
            provider: provider);
        payment.CreatedAt = createdAt;

        return payment;
    }

    private static void ForceStatus(Payment payment, PaymentStatus status, DateTime? processedAt)
    {
        // Drive the entity through its own state machine to keep the aggregates honest.
        payment.MarkAsProcessing();
        switch (status)
        {
            case PaymentStatus.Succeeded:
                payment.MarkAsSucceeded("ext-" + payment.Id.ToString("N"), "txn-" + payment.Id.ToString("N"));
                break;
            case PaymentStatus.Failed:
                payment.MarkAsFailed("test failure", "test_error");
                break;
        }

        if (processedAt.HasValue)
        {
            payment.GetType().GetProperty("ProcessedAt")!.SetValue(payment, processedAt);
        }
    }

    [Fact]
    public async Task Aggregates_AreComputed_PerWindowAndProvider()
    {
        var now = SystemClock.UtcNow;

        var recentStripeSucceeded = NewPayment("stripe", PaymentStatus.Succeeded, now.AddHours(-1));
        ForceStatus(recentStripeSucceeded, PaymentStatus.Succeeded, now.AddMinutes(-1));
        var recentStripeFailed = NewPayment("stripe", PaymentStatus.Failed, now.AddHours(-2));
        ForceStatus(recentStripeFailed, PaymentStatus.Failed, now.AddHours(-2).AddSeconds(30));
        var weekOldPayPalSucceeded = NewPayment("paypal", PaymentStatus.Succeeded, now.AddDays(-3));
        ForceStatus(weekOldPayPalSucceeded, PaymentStatus.Succeeded, now.AddDays(-3).AddMinutes(2));

        _repository
            .Setup(repository => repository.GetByDateRangeAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime start, DateTime end, Guid? _, CancellationToken _) =>
                new[]
                {
                    recentStripeSucceeded,
                    recentStripeFailed,
                    weekOldPayPalSucceeded
                }.Where(payment => payment.CreatedAt >= start && payment.CreatedAt <= end).ToList());

        var service = new GatewayMetricsService(_repository.Object);
        var metrics = await service.GetWindowMetricsAsync();

        var stripe24h = metrics.Should().ContainSingle(metric =>
            metric.ProviderId == "stripe" && metric.Window == "24h").Subject;
        stripe24h.Attempts.Should().Be(2);
        stripe24h.Succeeded.Should().Be(1);
        stripe24h.Failed.Should().Be(1);
        stripe24h.SuccessRate.Should().BeApproximately(0.5, 0.0001);

        var payPal7d = metrics.Should().ContainSingle(metric =>
            metric.ProviderId == "paypal" && metric.Window == "7d").Subject;
        payPal7d.Attempts.Should().Be(1);
        payPal7d.Succeeded.Should().Be(1);
        payPal7d.SuccessRate.Should().Be(1.0);

        metrics.Where(metric => metric.Window == "24h" && metric.ProviderId == "paypal")
            .Should().BeEmpty("providers without payments in a window are omitted, not zero-inflated");
        metrics.Where(metric => metric.Window == "30d").Should().HaveCount(2);
    }

    [Fact]
    public async Task Latency_IsTheMeanProcessingSpan_OfSucceededPayments()
    {
        var now = SystemClock.UtcNow;
        var fast = NewPayment("stripe", PaymentStatus.Succeeded, now.AddMinutes(-10));
        ForceStatus(fast, PaymentStatus.Succeeded, processedAt: now.AddMinutes(-9)); // 60_000 ms
        var slow = NewPayment("stripe", PaymentStatus.Succeeded, now.AddMinutes(-30));
        ForceStatus(slow, PaymentStatus.Succeeded, processedAt: now.AddMinutes(-28)); // 120_000 ms

        _repository
            .Setup(repository => repository.GetByDateRangeAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { fast, slow });

        var service = new GatewayMetricsService(_repository.Object);
        var metrics = await service.GetWindowMetricsAsync();

        var stripe24h = metrics.Single(metric => metric.ProviderId == "stripe" && metric.Window == "24h");
        stripe24h.AverageProcessingLatencyMs.Should().BeApproximately(90_000, 1);
    }

    [Fact]
    public async Task EmptyWindows_ProduceNoRows()
    {
        _repository
            .Setup(repository => repository.GetByDateRangeAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = new GatewayMetricsService(_repository.Object);
        var metrics = await service.GetWindowMetricsAsync();

        metrics.Should().BeEmpty();
    }

    [Fact]
    public async Task UnresolvedPayments_AreCountedSeparately()
    {
        var pending = NewPayment("stripe", PaymentStatus.Pending, SystemClock.UtcNow.AddHours(-1));

        _repository
            .Setup(repository => repository.GetByDateRangeAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { pending });

        var service = new GatewayMetricsService(_repository.Object);
        var metrics = await service.GetWindowMetricsAsync();

        var stripe24h = metrics.Single(metric => metric.ProviderId == "stripe" && metric.Window == "24h");
        stripe24h.Attempts.Should().Be(1);
        stripe24h.Unresolved.Should().Be(1);
        stripe24h.Succeeded.Should().Be(0);
        stripe24h.SuccessRate.Should().Be(0);
        stripe24h.AverageProcessingLatencyMs.Should().BeNull();
    }
}
