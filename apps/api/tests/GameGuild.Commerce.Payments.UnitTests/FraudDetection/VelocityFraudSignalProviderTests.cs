using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.FraudDetection;

public class VelocityFraudSignalProviderTests : IAsyncDisposable
{
    private readonly PaymentsPersistenceTestDbContext _context = new(
        new DbContextOptionsBuilder<PaymentsPersistenceTestDbContext>()
            .UseInMemoryDatabase($"fraud-velocity-{Guid.NewGuid():N}")
            .Options);

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _subscriptionId = Guid.NewGuid();
    private readonly Guid _currentPaymentId = Guid.NewGuid();
    private const string CustomerId = "cus_velocity";
    private const string PaymentMethod = "pm_seen";

    [Fact]
    public async Task EvaluateAsync_ShouldReturnNoSignals_WhenHistoryIsWithinThresholds()
    {
        await SeedPaymentAsync(status: PaymentStatus.Succeeded);
        await SeedPaymentAsync(status: PaymentStatus.Succeeded);
        var provider = CreateProvider();

        var signals = await provider.EvaluateAsync(Context(), Options(), CancellationToken.None);

        signals.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_ShouldFlagPaymentFrequency_WhenAboveThreshold()
    {
        for (var i = 0; i < 4; i++)
        {
            await SeedPaymentAsync(status: PaymentStatus.Succeeded, paymentMethod: PaymentMethod);
        }

        var provider = CreateProvider();
        var signals = await provider.EvaluateAsync(Context(), Options(), CancellationToken.None);

        var frequency = signals.SingleOrDefault(signal => signal.SignalType == "Velocity.PaymentFrequency");
        frequency.Should().NotBeNull();
        frequency!.Weight.Should().Be(15); // (4 attempts - threshold 3) * weight 15
    }

    [Fact]
    public async Task EvaluateAsync_ShouldFlagFailedAttempts_WhenAboveThreshold()
    {
        await SeedPaymentAsync(status: PaymentStatus.Failed, paymentMethod: PaymentMethod);
        await SeedPaymentAsync(status: PaymentStatus.Failed, paymentMethod: PaymentMethod);
        await SeedPaymentAsync(status: PaymentStatus.Failed, paymentMethod: PaymentMethod);

        var provider = CreateProvider();
        var signals = await provider.EvaluateAsync(Context(), Options(), CancellationToken.None);

        var failed = signals.SingleOrDefault(signal => signal.SignalType == "Velocity.FailedAttempts");
        failed.Should().NotBeNull();
        failed!.Weight.Should().Be(20); // (3 failures - threshold 2) * weight 20
    }

    [Fact]
    public async Task EvaluateAsync_ShouldFlagNewPaymentMethod_WhenMethodWasNeverSeen()
    {
        await SeedPaymentAsync(status: PaymentStatus.Succeeded, paymentMethod: "pm_other");

        var provider = CreateProvider();
        var signals = await provider.EvaluateAsync(Context(), Options(), CancellationToken.None);

        var newMethod = signals.SingleOrDefault(signal => signal.SignalType == "Velocity.NewPaymentMethod");
        newMethod.Should().NotBeNull();
        newMethod!.Weight.Should().Be(25);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldNotFlagPaymentMethod_WhenAlreadySeen()
    {
        await SeedPaymentAsync(status: PaymentStatus.Succeeded, paymentMethod: PaymentMethod);

        var provider = CreateProvider();
        var signals = await provider.EvaluateAsync(Context(), Options(), CancellationToken.None);

        signals.Should().NotContain(signal => signal.SignalType == "Velocity.NewPaymentMethod");
    }

    [Fact]
    public async Task EvaluateAsync_ShouldFlagDisputedPayments()
    {
        await SeedPaymentAsync(status: PaymentStatus.Disputed, paymentMethod: PaymentMethod);

        var provider = CreateProvider();
        var signals = await provider.EvaluateAsync(Context(), Options(), CancellationToken.None);

        var disputed = signals.SingleOrDefault(signal => signal.SignalType == "Velocity.DisputedPayments");
        disputed.Should().NotBeNull();
        disputed!.Weight.Should().Be(35);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldExcludeCancelledAndFraudRejectedRows()
    {
        await SeedPaymentAsync(status: PaymentStatus.Cancelled, paymentMethod: PaymentMethod);
        await SeedPaymentAsync(status: PaymentStatus.Failed, paymentMethod: PaymentMethod, fraudDecision: FraudDecision.Block);
        await SeedPaymentAsync(status: PaymentStatus.Failed, paymentMethod: PaymentMethod, fraudDecision: FraudDecision.Review);

        var provider = CreateProvider();
        var signals = await provider.EvaluateAsync(Context(), Options(), CancellationToken.None);

        signals.Should().NotContain(signal => signal.SignalType == "Velocity.PaymentFrequency");
        signals.Should().NotContain(signal => signal.SignalType == "Velocity.FailedAttempts");
        signals.Should().NotContain(signal => signal.SignalType == "Velocity.DisputedPayments");
    }

    [Fact]
    public async Task EvaluateAsync_ShouldExcludeOwnRowFromVelocityCounts()
    {
        // A previously rejected attempt re-scored under the same payment row must not
        // count itself (issue #409 reassessment rule).
        await SeedPaymentAsync(status: PaymentStatus.Failed, id: _currentPaymentId, paymentMethod: PaymentMethod);

        var provider = CreateProvider();
        var signals = await provider.EvaluateAsync(Context(), Options(), CancellationToken.None);

        signals.Should().NotContain(signal => signal.SignalType == "Velocity.FailedAttempts");
    }

    [Fact]
    public async Task EvaluateAsync_ShouldIgnoreAttemptsOutsideTheWindow()
    {
        await SeedPaymentAsync(status: PaymentStatus.Succeeded, createdAt: SystemClock.UtcNow.AddMinutes(-30));

        var provider = CreateProvider();
        var signals = await provider.EvaluateAsync(Context(), Options(), CancellationToken.None);

        signals.Should().NotContain(signal => signal.SignalType == "Velocity.PaymentFrequency");
    }

    [Fact]
    public async Task EvaluateAsync_ShouldStayTenantScoped()
    {
        for (var i = 0; i < 5; i++)
        {
            await SeedPaymentAsync(status: PaymentStatus.Succeeded, tenantId: Guid.NewGuid());
        }

        var provider = CreateProvider();
        var signals = await provider.EvaluateAsync(Context(), Options(), CancellationToken.None);

        signals.Should().NotContain(signal => signal.SignalType == "Velocity.PaymentFrequency");
    }

    [Fact]
    public async Task EvaluateAsync_ShouldMatchOtherCustomersOnlyForTheSameCustomer()
    {
        for (var i = 0; i < 5; i++)
        {
            await SeedPaymentAsync(status: PaymentStatus.Succeeded, externalCustomerId: "cus_other");
        }

        var provider = CreateProvider();
        var signals = await provider.EvaluateAsync(Context(), Options(), CancellationToken.None);

        signals.Should().NotContain(signal => signal.SignalType == "Velocity.PaymentFrequency");
    }

    [Fact]
    public async Task EvaluateAsync_ShouldFallBackToSubscriptionScope_ForUnboundAttempts()
    {
        for (var i = 0; i < 4; i++)
        {
            await SeedPaymentAsync(status: PaymentStatus.Succeeded, externalCustomerId: null, paymentMethod: PaymentMethod);
        }

        var provider = CreateProvider();
        var signals = await provider.EvaluateAsync(
            Context(externalCustomerId: null),
            Options(),
            CancellationToken.None);

        signals.Should().Contain(signal => signal.SignalType == "Velocity.PaymentFrequency");
    }

    private VelocityFraudSignalProvider CreateProvider() =>
        new(_context, NullLogger<VelocityFraudSignalProvider>.Instance);

    private static FraudAssessmentOptions Options() => new();

    private FraudSignalContext Context(string? externalCustomerId = CustomerId) => new(
        _tenantId,
        _currentPaymentId,
        _subscriptionId,
        externalCustomerId,
        PaymentMethod,
        25m,
        "USD");

    private async Task SeedPaymentAsync(
        PaymentStatus status,
        Guid? id = null,
        Guid? tenantId = null,
        string? externalCustomerId = CustomerId,
        string? paymentMethod = PaymentMethod,
        FraudDecision? fraudDecision = null,
        DateTime? createdAt = null)
    {
        var payment = Payment.Create(
            tenantId: tenantId ?? _tenantId,
            amount: 25m,
            currency: "USD",
            idempotencyKey: $"seed-{Guid.NewGuid():N}",
            provider: "stripe",
            subscriptionId: _subscriptionId,
            externalCustomerId: externalCustomerId,
            paymentMethodId: paymentMethod);

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
        else if (status == PaymentStatus.Disputed)
        {
            payment.MarkAsSucceeded($"ch_{Guid.NewGuid():N}");
            payment.MarkAsDisputed();
        }
        else if (status == PaymentStatus.Cancelled)
        {
            payment.Cancel("seeded cancellation");
        }

        if (createdAt.HasValue)
        {
            payment.CreatedAt = createdAt.Value;
        }

        _context.Set<Payment>().Add(payment);
        await _context.SaveChangesAsync();
    }

    public ValueTask DisposeAsync() => _context.DisposeAsync();
}
