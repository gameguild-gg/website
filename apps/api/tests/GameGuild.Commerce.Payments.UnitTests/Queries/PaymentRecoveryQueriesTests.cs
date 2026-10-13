using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.PaymentRecoveryQueries;

public class PaymentRecoveryQueriesTests
{
    private static async Task<PaymentsPersistenceTestDbContext> CreateContextAsync()
    {
        var context = new PaymentsPersistenceTestDbContext(
            new DbContextOptionsBuilder<PaymentsPersistenceTestDbContext>()
                .UseInMemoryDatabase($"payment-recovery-{Guid.NewGuid():N}")
                .Options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    [Fact]
    public async Task RecoveryMetrics_ComputeSuccessAndRetryRecoveryRates()
    {
        await using var context = await CreateContextAsync();
        var tenantId = Guid.NewGuid();
        // 2 payments that succeeded on first attempt.
        context.Set<Payment>().AddRange(
            Succeeded(tenantId, 100m),
            Succeeded(tenantId, 50m));
        // 1 payment that failed once, was retried, then succeeded (recovered, 25 USD).
        var recovered = Failed(tenantId, 25m);
        recovered.PrepareForRetry();
        recovered.MarkAsProcessing();
        recovered.MarkAsSucceeded("pi_recovered", "ch_recovered");
        context.Set<Payment>().Add(recovered);
        // 1 payment that failed once, retried, failed again and exhausted its budget (MaxRetries = 1).
        var exhausted = Failed(tenantId, 75m, maxRetries: 1);
        exhausted.PrepareForRetry();
        exhausted.MarkAsProcessing();
        exhausted.MarkAsFailed("final failure", maxRetries: 1);
        context.Set<Payment>().Add(exhausted);
        // 1 payment outside the tenant scope.
        context.Set<Payment>().Add(Succeeded(Guid.NewGuid(), 999m));
        await context.SaveChangesAsync();

        var handler = new GetPaymentRecoveryMetricsQueryHandler(context);
        var metrics = await handler.Handle(
            new GetPaymentRecoveryMetricsQuery(SystemClock.UtcNow.AddDays(-7), SystemClock.UtcNow.AddMinutes(1), tenantId),
            CancellationToken.None);

        metrics.Attempts.TotalPayments.Should().Be(4);
        metrics.Attempts.SucceededPayments.Should().Be(3);
        metrics.Attempts.FailedPayments.Should().Be(1);
        metrics.Attempts.SuccessRate.Should().Be(75m);

        metrics.Retries.EverFailedPayments.Should().Be(2);
        metrics.Retries.RetriedPayments.Should().Be(2);
        metrics.Retries.RecoveredPayments.Should().Be(1);
        metrics.Retries.RetryRecoveryRate.Should().Be(50m);
        metrics.Retries.RecoveredAmounts.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new PaymentCurrencyAmount("USD", 25m, 1));

        metrics.Dunning.ExhaustedPayments.Should().Be(1);
    }

    [Fact]
    public async Task RecoveryMetrics_SeparateRecoveredAmountsByCurrency()
    {
        await using var context = await CreateContextAsync();
        var tenantId = Guid.NewGuid();

        var recoveredUsd = Failed(tenantId, 25m);
        recoveredUsd.PrepareForRetry();
        recoveredUsd.MarkAsProcessing();
        recoveredUsd.MarkAsSucceeded("pi_usd", "ch_usd");

        var recoveredEur = Failed(tenantId, 40m, currency: "EUR");
        recoveredEur.PrepareForRetry();
        recoveredEur.MarkAsProcessing();
        recoveredEur.MarkAsSucceeded("pi_eur", "ch_eur");

        context.Set<Payment>().AddRange(recoveredUsd, recoveredEur);
        await context.SaveChangesAsync();

        var handler = new GetPaymentRecoveryMetricsQueryHandler(context);
        var metrics = await handler.Handle(
            new GetPaymentRecoveryMetricsQuery(SystemClock.UtcNow.AddDays(-7), SystemClock.UtcNow.AddMinutes(1), null),
            CancellationToken.None);

        metrics.Retries.RecoveredAmounts.Should().HaveCount(2);
        metrics.Retries.RecoveredAmounts.Should().Contain(new PaymentCurrencyAmount("EUR", 40m, 1));
        metrics.Retries.RecoveredAmounts.Should().Contain(new PaymentCurrencyAmount("USD", 25m, 1));
    }

    [Fact]
    public async Task RecoveryMetrics_EmptyWindow_ReportsZeroRatesWithoutDividingByZero()
    {
        await using var context = await CreateContextAsync();
        var handler = new GetPaymentRecoveryMetricsQueryHandler(context);

        var metrics = await handler.Handle(
            new GetPaymentRecoveryMetricsQuery(SystemClock.UtcNow.AddDays(-1), SystemClock.UtcNow, null),
            CancellationToken.None);

        metrics.Attempts.TotalPayments.Should().Be(0);
        metrics.Attempts.SuccessRate.Should().Be(0m);
        metrics.Retries.RetryRecoveryRate.Should().Be(0m);
        metrics.Retries.RecoveredAmounts.Should().BeEmpty();
    }

    [Fact]
    public async Task RecoveryMetrics_InvertedWindow_Throws()
    {
        await using var context = await CreateContextAsync();
        var handler = new GetPaymentRecoveryMetricsQueryHandler(context);
        var from = SystemClock.UtcNow;
        var to = SystemClock.UtcNow.AddDays(-1);

        var act = () => handler.Handle(new GetPaymentRecoveryMetricsQuery(from, to, null), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RetryingCandidates_ReturnOnlyDueFailedPaymentsWithBudgetLeft_SoonestFirst()
    {
        await using var context = await CreateContextAsync();
        var tenantId = Guid.NewGuid();
        var now = SystemClock.UtcNow;

        var dueSoon = Failed(tenantId, 10m);
        var dueEarlier = Failed(tenantId, 20m);
        var notDueYet = Failed(tenantId, 30m);
        var exhausted = Failed(tenantId, 40m, maxRetries: 1);

        context.Set<Payment>().AddRange(dueSoon, dueEarlier, notDueYet, exhausted);

        // Schedule explicit next-retry times through EF so due/not-due is deterministic.
        ScheduleRetryAt(context, dueSoon, now.AddMinutes(-10));
        ScheduleRetryAt(context, dueEarlier, now.AddHours(-2));
        ScheduleRetryAt(context, notDueYet, now.AddMinutes(10));
        ScheduleRetryAt(context, exhausted, now.AddHours(-3)); // due, but RetryCount == MaxRetries below

        // Exhaust the retry budget of the fourth payment.
        exhausted.PrepareForRetry();
        exhausted.MarkAsProcessing();
        exhausted.MarkAsFailed("final failure", maxRetries: 1);
        ScheduleRetryAt(context, exhausted, now.AddHours(-1));

        await context.SaveChangesAsync();

        var handler = new GetRetryingPaymentCandidatesQueryHandler(context);
        var candidates = await handler.Handle(
            new GetRetryingPaymentCandidatesQuery(tenantId, 50),
            CancellationToken.None);

        candidates.Should().HaveCount(2);
        candidates[0].PaymentId.Should().Be(dueEarlier.Id.ToString());
        candidates[1].PaymentId.Should().Be(dueSoon.Id.ToString());
    }

    [Fact]
    public async Task RetryingCandidates_CapsTake()
    {
        await using var context = await CreateContextAsync();
        var tenantId = Guid.NewGuid();
        for (var i = 0; i < 3; i++)
        {
            var payment = Failed(tenantId, 10m + i);
            context.Set<Payment>().Add(payment);
            ScheduleRetryAt(context, payment, SystemClock.UtcNow.AddMinutes(-5));
        }
        await context.SaveChangesAsync();

        var handler = new GetRetryingPaymentCandidatesQueryHandler(context);
        var candidates = await handler.Handle(
            new GetRetryingPaymentCandidatesQuery(tenantId, 2),
            CancellationToken.None);

        candidates.Should().HaveCount(2);
    }

    /// <summary>Sets an explicit next-retry time through EF's property API (private setter).</summary>
    private static void ScheduleRetryAt(PaymentsPersistenceTestDbContext context, Payment payment, DateTime nextRetryAt)
        => context.Entry(payment).Property(p => p.NextRetryAt).CurrentValue = nextRetryAt;

    private static Payment Succeeded(Guid tenantId, decimal amount, string currency = "USD")
    {
        var payment = Payment.Create(tenantId, amount, currency, $"idem-{Guid.NewGuid():N}");
        payment.MarkAsProcessing();
        payment.MarkAsSucceeded("pi_ok", "ch_ok");
        return payment;
    }

    private static Payment Failed(Guid tenantId, decimal amount, string currency = "USD", int? maxRetries = null)
    {
        var payment = Payment.Create(tenantId, amount, currency, $"idem-{Guid.NewGuid():N}", maxRetries: maxRetries);
        payment.MarkAsProcessing();
        payment.MarkAsFailed("test failure");
        return payment;
    }
}
