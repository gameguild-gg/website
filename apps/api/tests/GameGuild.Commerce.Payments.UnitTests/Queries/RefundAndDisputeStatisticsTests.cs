using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.RefundDisputeStats;

/// <summary>
///     Refund and dispute statistics aggregation (issue #419) over the in-memory refund and
///     dispute ledgers.
/// </summary>
public class RefundAndDisputeStatisticsTests
{
    [Fact]
    public async Task RefundStatistics_AggregatesCountsAmountsAndReasons()
    {
        await using var db = NewDatabase();
        var tenantId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        db.Add(RefundRow(tenantId, paymentId, 30m, RefundStatus.Completed, reason: "duplicate", createdAt: SystemClock.UtcNow.AddDays(-2)));
        db.Add(RefundRow(tenantId, paymentId, 50m, RefundStatus.Completed, reason: "goodwill", createdAt: SystemClock.UtcNow.AddDays(-1)));
        db.Add(RefundRow(tenantId, paymentId, 75m, RefundStatus.Pending, reason: "goodwill", createdAt: SystemClock.UtcNow));
        db.Add(RefundRow(Guid.NewGuid(), paymentId, 99m, RefundStatus.Completed, reason: "duplicate", createdAt: SystemClock.UtcNow));
        await db.SaveChangesAsync();

        var handler = new GetRefundStatisticsQueryHandler(db);
        var stats = await handler.Handle(
            new GetRefundStatisticsQuery(SystemClock.UtcNow.AddDays(-7), SystemClock.UtcNow, tenantId),
            CancellationToken.None);

        stats.TotalCount.Should().Be(3);
        stats.CountByStatus[RefundStatus.Completed].Should().Be(2);
        stats.CountByStatus[RefundStatus.Pending].Should().Be(1);
        stats.CompletedAmountByCurrency["USD"].Should().Be(80m);
        stats.CompletedCountByReason["duplicate"].Should().Be(1);
        stats.CompletedCountByReason["goodwill"].Should().Be(1);
    }

    [Fact]
    public async Task RefundStatistics_EmptyWindow_ReturnsZeroedAggregates()
    {
        await using var db = NewDatabase();
        await db.SaveChangesAsync();

        var handler = new GetRefundStatisticsQueryHandler(db);
        var stats = await handler.Handle(
            new GetRefundStatisticsQuery(SystemClock.UtcNow.AddDays(-1), SystemClock.UtcNow),
            CancellationToken.None);

        stats.TotalCount.Should().Be(0);
        stats.CountByStatus.Should().BeEmpty();
        stats.CompletedAmountByCurrency.Should().BeEmpty();
        stats.CompletedCountByReason.Should().BeEmpty();
    }

    [Fact]
    public async Task DisputeStatistics_ComputesWinLossRatioAndResolutionTime()
    {
        await using var db = NewDatabase();
        var now = SystemClock.UtcNow;
        var tenantId = Guid.NewGuid();
        AddDispute(db, Dispute(tenantId, 100m, DisputeStatus.Won, resolvedAt: now.AddHours(-10), createdAt: now.AddDays(-2)));
        AddDispute(db, Dispute(tenantId, 200m, DisputeStatus.Won, resolvedAt: null, createdAt: now.AddDays(-1)));
        AddDispute(db, Dispute(tenantId, 300m, DisputeStatus.Lost, resolvedAt: now.AddHours(-30), createdAt: now.AddDays(-3)));
        AddDispute(db, Dispute(tenantId, 400m, DisputeStatus.UnderReview, resolvedAt: null, createdAt: now));
        await db.SaveChangesAsync();

        var handler = new GetDisputeStatisticsQueryHandler(db);
        var stats = await handler.Handle(
            new GetDisputeStatisticsQuery(now.AddDays(-7), now, tenantId),
            CancellationToken.None);

        stats.TotalCount.Should().Be(4);
        stats.WonCount.Should().Be(2);
        stats.LostCount.Should().Be(1);
        stats.WinLossRatio.Should().BeApproximately(2.0 / 3.0, 0.0001);
        stats.AverageResolutionHours.Should().BeApproximately(20.0, 0.5); // (10 + 30) / 2
        stats.TotalDisputedAmount.Should().Be(1000m);
    }

    [Fact]
    public async Task DisputeStatistics_NoDecidedDisputes_RatioAndResolutionAreNull()
    {
        await using var db = NewDatabase();
        var now = SystemClock.UtcNow;
        var tenantId = Guid.NewGuid();
        AddDispute(db, Dispute(tenantId, 100m, DisputeStatus.UnderReview, resolvedAt: null, createdAt: now));
        await db.SaveChangesAsync();

        var handler = new GetDisputeStatisticsQueryHandler(db);
        var stats = await handler.Handle(
            new GetDisputeStatisticsQuery(now.AddDays(-7), now, tenantId),
            CancellationToken.None);

        stats.WonCount.Should().Be(0);
        stats.LostCount.Should().Be(0);
        stats.WinLossRatio.Should().BeNull();
        stats.AverageResolutionHours.Should().BeNull();
    }

    private static Refund RefundRow(
        Guid tenantId,
        Guid paymentId,
        decimal amount,
        RefundStatus status,
        string reason,
        DateTime createdAt)
    {
        var refund = Refund.Create(
            tenantId,
            paymentId,
            amount,
            "USD",
            reason,
            null,
            Guid.NewGuid(),
            requiresApproval: status == RefundStatus.Pending,
            idempotencyKey: $"refund_{Guid.NewGuid():N}");
        refund.CreatedAt = createdAt;
        if (status == RefundStatus.Completed)
        {
            refund.MarkProcessing();
            refund.MarkCompleted($"re_{Guid.NewGuid():N}", "stripe");
        }

        return refund;
    }

    private static (Payment Payment, PaymentDispute Dispute) Dispute(
        Guid tenantId,
        decimal amount,
        DisputeStatus status,
        DateTime? resolvedAt,
        DateTime createdAt)
    {
        var payment = Payment.Create(tenantId, amount, "USD", $"idem-{Guid.NewGuid():N}");
        payment.MarkAsProcessing();
        payment.MarkAsSucceeded("pi_stats", "ch_stats");

        return (payment, new PaymentDispute
        {
            PaymentId = payment.Id,
            UserId = Guid.NewGuid(),
            Type = DisputeType.Fraudulent,
            Amount = amount,
            Reason = "fraudulent",
            Status = status,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            ResolvedAt = resolvedAt
        });
    }

    private static void AddDispute(DisputesStatsTestDbContext db, (Payment Payment, PaymentDispute Dispute) pair)
    {
        db.Add(pair.Payment);
        db.Add(pair.Dispute);
    }

    private static DisputesStatsTestDbContext NewDatabase()
    {
        var options = new DbContextOptionsBuilder<DisputesStatsTestDbContext>()
            .UseInMemoryDatabase($"stats-{Guid.NewGuid():N}")
            .Options;
        return new DisputesStatsTestDbContext(options);
    }

    private sealed class DisputesStatsTestDbContext(
        Microsoft.EntityFrameworkCore.DbContextOptions<DisputesStatsTestDbContext> options)
        : Microsoft.EntityFrameworkCore.DbContext(options), IApplicationDbContext
    {
        public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Database.BeginTransactionAsync(cancellationToken);

        protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Payment>();
            modelBuilder.Entity<Refund>();
            modelBuilder.Entity<PaymentDispute>(builder =>
            {
                builder.Property(dispute => dispute.Status).HasConversion<string>().IsRequired();
                builder.Property(dispute => dispute.Type).HasConversion<string>().IsRequired();
                builder.Property(dispute => dispute.Resolution).HasConversion<string>();
            });
            modelBuilder.Entity<DisputeEvidence>();
            base.OnModelCreating(modelBuilder);
        }
    }
}
