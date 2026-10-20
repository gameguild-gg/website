using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.FraudDetection;

public class CardTestingPatternFraudSignalProviderTests : IAsyncDisposable
{
    private readonly PaymentsPersistenceTestDbContext _context = new(
        new DbContextOptionsBuilder<PaymentsPersistenceTestDbContext>()
            .UseInMemoryDatabase($"fraud-card-testing-{Guid.NewGuid():N}")
            .Options);

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _subscriptionId = Guid.NewGuid();
    private readonly Guid _currentPaymentId = Guid.NewGuid();
    private const string CustomerId = "cus_cardtest";

    [Fact]
    public async Task EvaluateAsync_Should_Return_No_Signal_When_Amount_Is_Not_Micro()
    {
        await SeedPaymentAsync(amount: 2m);
        await SeedPaymentAsync(amount: 2m);
        await SeedPaymentAsync(amount: 2m);
        await SeedPaymentAsync(amount: 2m);

        var provider = CreateProvider();
        var signals = await provider.EvaluateAsync(Context(amount: 120m), Options(), CancellationToken.None);

        signals.Should().BeEmpty("a normal-sized charge is not part of the card-testing pattern");
    }

    [Fact]
    public async Task EvaluateAsync_Should_Flag_MicroAmount_Burst_Above_Threshold()
    {
        for (var i = 0; i < 4; i++)
        {
            await SeedPaymentAsync(amount: 1m);
        }

        var provider = CreateProvider();
        var signals = await provider.EvaluateAsync(Context(amount: 1m), Options(), CancellationToken.None);

        var signal = signals.SingleOrDefault(item => item.SignalType == "Pattern.CardTesting");
        signal.Should().NotBeNull();
        signal!.Weight.Should().Be(40); // CardTestingWeight default, capped by MaxSignalScore 50
    }

    [Fact]
    public async Task EvaluateAsync_Should_Not_Flag_MicroAmount_Within_Threshold()
    {
        await SeedPaymentAsync(amount: 1m);
        await SeedPaymentAsync(amount: 1m);
        await SeedPaymentAsync(amount: 1m);

        var provider = CreateProvider();
        var signals = await provider.EvaluateAsync(Context(amount: 1m), Options(), CancellationToken.None);

        signals.Should().BeEmpty("three prior micro attempts equal the threshold, not exceed it");
    }

    [Fact]
    public async Task EvaluateAsync_Should_Not_Flag_When_Burst_Has_NormalAmounts()
    {
        for (var i = 0; i < 6; i++)
        {
            await SeedPaymentAsync(amount: 50m);
        }

        var provider = CreateProvider();
        var signals = await provider.EvaluateAsync(Context(amount: 3m), Options(), CancellationToken.None);

        signals.Should().BeEmpty("only micro-amount history counts toward the pattern");
    }

    [Fact]
    public async Task EvaluateAsync_Should_Exclude_Cancelled_And_Fraud_Rejected_Rows()
    {
        await SeedPaymentAsync(amount: 1m, status: PaymentStatus.Cancelled);
        await SeedPaymentAsync(amount: 1m, status: PaymentStatus.Failed, fraudDecision: FraudDecision.Block);
        await SeedPaymentAsync(amount: 1m, status: PaymentStatus.Failed, fraudDecision: FraudDecision.Review);

        var provider = CreateProvider();
        var signals = await provider.EvaluateAsync(Context(amount: 1m), Options(), CancellationToken.None);

        signals.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_Should_Stay_Tenant_And_Customer_Scoped()
    {
        for (var i = 0; i < 6; i++)
        {
            await SeedPaymentAsync(amount: 1m, tenantId: Guid.NewGuid());
            await SeedPaymentAsync(amount: 1m, externalCustomerId: "cus_other");
        }

        var provider = CreateProvider();
        var signals = await provider.EvaluateAsync(Context(amount: 1m), Options(), CancellationToken.None);

        signals.Should().BeEmpty();
    }

    private CardTestingPatternFraudSignalProvider CreateProvider() =>
        new(_context, NullLogger<CardTestingPatternFraudSignalProvider>.Instance);

    private static FraudAssessmentOptions Options() => new();

    private FraudSignalContext Context(string? externalCustomerId = CustomerId, decimal amount = 1m) => new(
        _tenantId,
        _currentPaymentId,
        _subscriptionId,
        externalCustomerId,
        null,
        amount,
        "USD");

    private async Task SeedPaymentAsync(
        decimal amount,
        PaymentStatus status = PaymentStatus.Succeeded,
        Guid? tenantId = null,
        string? externalCustomerId = CustomerId,
        FraudDecision? fraudDecision = null)
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

        if (status == PaymentStatus.Failed)
        {
            if (fraudDecision is { } decision)
            {
                payment.RecordFraudAssessment(decision, 50, "seeded", "{}");
            }
            else
            {
                payment.MarkAsFailed("seeded failure", "declined");
            }
        }
        else if (status == PaymentStatus.Succeeded)
        {
            payment.MarkAsSucceeded($"ch_{Guid.NewGuid():N}");
        }
        else if (status == PaymentStatus.Cancelled)
        {
            payment.Cancel("seeded cancellation");
        }

        _context.Set<Payment>().Add(payment);
        await _context.SaveChangesAsync();
    }

    public ValueTask DisposeAsync() => _context.DisposeAsync();
}
