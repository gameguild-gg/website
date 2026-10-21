using FluentAssertions;
using GameGuild.Identity.Authorization;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.RefundAnalytics;

/// <summary>
///     Refund and dispute analytics for an inclusive window (issue #419 follow-up, the
///     deferred dispute statistics from the #810 spec): refund counts and amounts by
///     status/currency/UTC day, dispute resolution rates, average time-to-resolution, the
///     payments:admin permission contract, the tenant scope through the payment, and the
///     empty-state shape.
/// </summary>
public class GetRefundAnalyticsQueryTests
{
    private static async Task<PaymentsPersistenceTestDbContext> CreateContextAsync()
    {
        var context = new PaymentsPersistenceTestDbContext(
            new DbContextOptionsBuilder<PaymentsPersistenceTestDbContext>()
                .UseInMemoryDatabase($"refund-analytics-{Guid.NewGuid():N}")
                .Options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static Refund NewRefund(
        Guid tenantId,
        decimal amount,
        string currency,
        RefundStatus status,
        DateTime createdAtUtc)
    {
        var refund = Refund.Create(
            tenantId,
            Guid.NewGuid(),
            amount,
            currency,
            "duplicate",
            null,
            Guid.NewGuid(),
            requiresApproval: false,
            $"idem-{Guid.NewGuid():N}");
        refund.MarkApproved(Guid.NewGuid());
        refund.MarkProcessing();
        if (status == RefundStatus.Completed)
        {
            refund.MarkCompleted("re_123", "stripe");
        }
        else if (status == RefundStatus.Failed)
        {
            refund.MarkFailed("gateway rejected the test refund");
        }

        ForceCreatedAt(refund, createdAtUtc);
        return refund;
    }

    private static void ForceCreatedAt(Refund refund, DateTime createdAtUtc) =>
        refund.CreatedAt = createdAtUtc;

    private static PaymentDispute NewDispute(
        Guid paymentId,
        DisputeStatus status,
        DateTime createdAtUtc,
        DateTime? resolvedAtUtc = null,
        decimal amount = 50m) =>
        new()
        {
            PaymentId = paymentId,
            UserId = Guid.NewGuid(),
            Type = DisputeType.Fraudulent,
            Status = status,
            Amount = amount,
            Reason = "fraudulent",
            ResolvedAt = resolvedAtUtc,
            CreatedAt = createdAtUtc
        };

    private static Payment SucceededPayment(Guid tenantId)
    {
        var payment = Payment.Create(
            tenantId,
            50m,
            "USD",
            $"idem-{Guid.NewGuid():N}",
            provider: "stripe");
        payment.MarkAsProcessing();
        payment.MarkAsSucceeded("pi_analytics", "ch_analytics");
        return payment;
    }

    [Fact]
    public async Task Aggregates_RefundsByStatusCurrencyAndDay_WithDisputeResolutionRates()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var dayOne = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);
        var dayTwo = new DateTime(2026, 10, 2, 11, 30, 0, DateTimeKind.Utc);
        await using var context = await CreateContextAsync();

        var completedUsd = NewRefund(tenantId, 25m, "USD", RefundStatus.Completed, dayOne);
        var failedUsd = NewRefund(tenantId, 40m, "USD", RefundStatus.Failed, dayOne);
        var completedEur = NewRefund(tenantId, 10m, "EUR", RefundStatus.Completed, dayTwo);
        var otherTenant = NewRefund(otherTenantId, 99m, "USD", RefundStatus.Completed, dayTwo);
        context.Set<Refund>().AddRange(completedUsd, failedUsd, completedEur, otherTenant);

        var payment = Payment.Create(
            tenantId,
            50m,
            "USD",
            $"idem-{Guid.NewGuid():N}",
            provider: "stripe");
        context.Set<Payment>().Add(payment);

        var won = NewDispute(payment.Id, DisputeStatus.Won, dayOne, resolvedAtUtc: dayOne.AddHours(6));
        var lost = NewDispute(payment.Id, DisputeStatus.Lost, dayOne.AddHours(1), resolvedAtUtc: dayOne.AddHours(1).AddHours(18));
        var open = NewDispute(payment.Id, DisputeStatus.UnderReview, dayTwo);
        var otherTenantPayment = Payment.Create(
            otherTenantId,
            50m,
            "USD",
            $"idem-{Guid.NewGuid():N}",
            provider: "stripe");
        context.Set<Payment>().Add(otherTenantPayment);
        var foreignDispute = NewDispute(otherTenantPayment.Id, DisputeStatus.Won, dayTwo, resolvedAtUtc: dayTwo);
        context.Set<PaymentDispute>().AddRange(won, lost, open, foreignDispute);

        await context.SaveChangesAsync();

        var report = await new GetRefundAnalyticsQueryHandler(context).Handle(
            new GetRefundAnalyticsQuery(dayOne.Date, dayTwo.AddDays(1).AddTicks(-1), tenantId),
            CancellationToken.None);

        report.TotalRefundCount.Should().Be(3);
        report.RefundCountByStatus.Should().BeEquivalentTo(new Dictionary<RefundStatus, int>
        {
            [RefundStatus.Completed] = 2,
            [RefundStatus.Failed] = 1
        });
        report.RefundAmountByStatus.Should().BeEquivalentTo(new Dictionary<RefundStatus, decimal>
        {
            [RefundStatus.Completed] = 35m,
            [RefundStatus.Failed] = 40m
        });
        report.RefundCountByCurrency.Should().BeEquivalentTo(new Dictionary<string, int>
        {
            ["USD"] = 2,
            ["EUR"] = 1
        });
        report.CompletedRefundAmountByCurrency.Should().BeEquivalentTo(new Dictionary<string, decimal>
        {
            ["USD"] = 25m,
            ["EUR"] = 10m
        });
        report.RefundCountByReason.Should().BeEquivalentTo(new Dictionary<string, int>
        {
            ["duplicate"] = 3
        });
        report.CompletedRefundAmountByReason.Should().BeEquivalentTo(new Dictionary<string, decimal>
        {
            ["duplicate"] = 35m
        });

        report.RefundsByDay.Should().HaveCount(2);
        report.RefundsByDay[0].Should().BeEquivalentTo(new { Date = dayOne.Date, Count = 2, CompletedAmount = 25m });
        report.RefundsByDay[1].Should().BeEquivalentTo(new { Date = dayTwo.Date, Count = 1, CompletedAmount = 10m });

        report.TotalDisputeCount.Should().Be(3);
        report.DisputesWon.Should().Be(1);
        report.DisputesLost.Should().Be(1);
        report.DisputeWinRate.Should().Be(0.5);
        report.DisputeResolutionRate.Should().BeApproximately(2.0 / 3.0, 0.0001);
        report.AverageDisputeResolutionHours.Should().Be(12.0); // (6 + 18) / 2
    }

    [Fact]
    public async Task Rates_AreDenominatedBySucceededPaymentsOfTheSameWindow()
    {
        var tenantId = Guid.NewGuid();
        var day = SystemClock.UtcNow.AddHours(-1);
        var windowStart = SystemClock.UtcNow.AddDays(-1);
        var windowEnd = SystemClock.UtcNow.AddDays(1);
        await using var context = await CreateContextAsync();

        // Two succeeded payments (one refunded, one disputed) and one non-succeeded payment
        // that must stay out of the denominator.
        var refundedPayment = SucceededPayment(tenantId);
        var disputedPayment = SucceededPayment(tenantId);
        disputedPayment.MarkAsDisputed();
        var failedPayment = Payment.Create(
            tenantId,
            30m,
            "USD",
            $"idem-{Guid.NewGuid():N}",
            provider: "stripe");
        failedPayment.MarkAsProcessing();
        failedPayment.MarkAsFailed("declined", "card_declined");
        context.Set<Payment>().AddRange(refundedPayment, disputedPayment, failedPayment);

        var refund = NewRefund(tenantId, 25m, "USD", RefundStatus.Completed, day);
        refundedPayment.ProcessRefund(25m, "re_analytics", "duplicate");
        var dispute = NewDispute(disputedPayment.Id, DisputeStatus.UnderReview, day);
        context.Set<Refund>().Add(refund);
        context.Set<PaymentDispute>().Add(dispute);
        await context.SaveChangesAsync();

        var report = await new GetRefundAnalyticsQueryHandler(context).Handle(
            new GetRefundAnalyticsQuery(windowStart, windowEnd, tenantId),
            CancellationToken.None);

        report.RefundRatePerSucceededPayment.Should().BeApproximately(0.5, 0.0001);
        report.DisputeRatePerSucceededPayment.Should().BeApproximately(0.5, 0.0001);
    }

    [Fact]
    public async Task EmptyWindow_ReturnsZeroedAggregates_WithNullRates()
    {
        await using var context = await CreateContextAsync();

        var report = await new GetRefundAnalyticsQueryHandler(context).Handle(
            new GetRefundAnalyticsQuery(SystemClock.UtcNow.AddDays(-7), SystemClock.UtcNow, null),
            CancellationToken.None);

        report.TotalRefundCount.Should().Be(0);
        report.RefundCountByStatus.Should().BeEmpty();
        report.RefundAmountByStatus.Should().BeEmpty();
        report.RefundCountByCurrency.Should().BeEmpty();
        report.CompletedRefundAmountByCurrency.Should().BeEmpty();
        report.RefundsByDay.Should().BeEmpty();
        report.TotalDisputeCount.Should().Be(0);
        report.DisputeCountByStatus.Should().BeEmpty();
        report.DisputeWinRate.Should().BeNull();
        report.DisputeResolutionRate.Should().BeNull();
        report.AverageDisputeResolutionHours.Should().BeNull();
    }

    [Fact]
    public void Query_IsGuardedByThePaymentsAdminPermission()
    {
        var attribute = typeof(GetRefundAnalyticsQuery)
            .GetCustomAttributes(typeof(AuthorizeRequestAttribute), inherit: true)
            .Cast<AuthorizeRequestAttribute>()
            .Single();

        attribute.Permission.Should().Be(PaymentsPermission.Keys.Admin);
    }
}
