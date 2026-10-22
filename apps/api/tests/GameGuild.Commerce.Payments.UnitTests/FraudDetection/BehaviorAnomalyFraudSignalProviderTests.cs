using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.FraudDetection;

public class BehaviorAnomalyFraudSignalProviderTests : IAsyncDisposable
{
    private readonly PaymentsPersistenceTestDbContext _context = new(
        new DbContextOptionsBuilder<PaymentsPersistenceTestDbContext>()
            .UseInMemoryDatabase($"fraud-behavior-{Guid.NewGuid():N}")
            .Options);

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _subscriptionId = Guid.NewGuid();
    private readonly Guid _currentPaymentId = Guid.NewGuid();
    private const string CustomerId = "cus_behavior";

    [Fact]
    public async Task EvaluateAsync_Should_Flag_Amount_Far_Above_The_Customer_Baseline()
    {
        // Baseline of two successful 20.00 charges; the attempt is 500 (25x the baseline).
        await SeedPaymentAsync(amount: 20m);
        await SeedPaymentAsync(amount: 20m);

        var provider = CreateProvider();
        var signals = await provider.EvaluateAsync(Context(amount: 500m), Options(), CancellationToken.None);

        var signal = signals.SingleOrDefault(item => item.SignalType == "Behavior.AmountAnomaly");
        signal.Should().NotBeNull();
        signal!.Weight.Should().Be(25); // BehaviorAnomalyWeight default
    }

    [Fact]
    public async Task EvaluateAsync_Should_Not_Flag_Amount_Within_The_Multiplier()
    {
        await SeedPaymentAsync(amount: 20m);
        await SeedPaymentAsync(amount: 20m);

        var provider = CreateProvider();
        var signals = await provider.EvaluateAsync(Context(amount: 60m), Options(), CancellationToken.None);

        signals.Should().BeEmpty("60 is only 3x the 20.00 baseline (multiplier is 5x)");
    }

    [Fact]
    public async Task EvaluateAsync_Should_Require_Minimum_History()
    {
        await SeedPaymentAsync(amount: 20m);

        var provider = CreateProvider();
        var signals = await provider.EvaluateAsync(Context(amount: 500m), Options(), CancellationToken.None);

        signals.Should().BeEmpty("a single prior charge does not establish a baseline");
    }

    [Fact]
    public async Task EvaluateAsync_Should_Only_Use_Successful_Charges_For_The_Baseline()
    {
        await SeedPaymentAsync(amount: 20m, status: PaymentStatus.Failed);
        await SeedPaymentAsync(amount: 20m, status: PaymentStatus.Failed);

        var provider = CreateProvider();
        var signals = await provider.EvaluateAsync(Context(amount: 500m), Options(), CancellationToken.None);

        signals.Should().BeEmpty("failed attempts do not describe normal customer behavior");
    }

    [Fact]
    public async Task EvaluateAsync_Should_Stay_Tenant_And_Customer_Scoped()
    {
        await SeedPaymentAsync(amount: 20m, tenantId: Guid.NewGuid());
        await SeedPaymentAsync(amount: 20m, externalCustomerId: "cus_other");

        var provider = CreateProvider();
        var signals = await provider.EvaluateAsync(Context(amount: 500m), Options(), CancellationToken.None);

        signals.Should().BeEmpty();
    }

    private BehaviorAnomalyFraudSignalProvider CreateProvider() =>
        new(_context, NullLogger<BehaviorAnomalyFraudSignalProvider>.Instance);

    private static FraudAssessmentOptions Options() => new();

    private FraudSignalContext Context(decimal amount) => new(
        _tenantId,
        _currentPaymentId,
        _subscriptionId,
        CustomerId,
        null,
        amount,
        "USD");

    private async Task SeedPaymentAsync(
        decimal amount,
        PaymentStatus status = PaymentStatus.Succeeded,
        Guid? tenantId = null,
        string? externalCustomerId = CustomerId)
    {
        var payment = Payment.Create(
            tenantId: tenantId ?? _tenantId,
            amount: amount,
            currency: "USD",
            idempotencyKey: $"seed-{Guid.NewGuid():N}",
            provider: "stripe",
            subscriptionId: _subscriptionId,
            externalCustomerId: externalCustomerId,
            paymentMethodId: null);

        payment.MarkAsProcessing();
        if (status == PaymentStatus.Succeeded)
        {
            payment.MarkAsSucceeded($"ch_{Guid.NewGuid():N}");
        }
        else
        {
            payment.MarkAsFailed("seeded failure", "declined");
        }

        _context.Set<Payment>().Add(payment);
        await _context.SaveChangesAsync();
    }

    public ValueTask DisposeAsync() => _context.DisposeAsync();
}
