using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.RevenueAuditing;

public class RevenueAuditingPersistenceTests
{
    private static async Task<PaymentsPersistenceTestDbContext> CreateContextAsync()
    {
        var context = new PaymentsPersistenceTestDbContext(
            new DbContextOptionsBuilder<PaymentsPersistenceTestDbContext>()
                .UseInMemoryDatabase($"revenue-auditing-{Guid.NewGuid():N}")
                .Options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static RevenueEvent Event(
        string referenceId,
        decimal amount,
        DateTime timestamp,
        Guid? tenantId = null,
        RevenueEventType eventType = RevenueEventType.PaymentReceived,
        RevenueEventStatus status = RevenueEventStatus.Processed)
        => new()
        {
            ReferenceId = referenceId,
            Amount = amount,
            Currency = "USD",
            Source = RevenueSource.Subscription,
            EventType = eventType,
            Status = status,
            Timestamp = timestamp,
            TenantId = tenantId
        };

    [Fact]
    public async Task GetInPeriodAsync_FiltersByPeriodAndTenant()
    {
        await using var context = await CreateContextAsync();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        context.Set<RevenueEvent>().AddRange(
            Event("a-in", 10m, new DateTime(2026, 9, 10, 8, 0, 0, DateTimeKind.Utc), tenantA),
            Event("a-out", 10m, new DateTime(2026, 10, 2, 8, 0, 0, DateTimeKind.Utc), tenantA),
            Event("b-in", 10m, new DateTime(2026, 9, 11, 8, 0, 0, DateTimeKind.Utc), tenantB));
        await context.SaveChangesAsync();

        var repository = new RevenueEventRepository(context);
        var inPeriod = await repository.GetInPeriodAsync(
            new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 30, 23, 59, 59, DateTimeKind.Utc),
            tenantA);

        inPeriod.Select(revenueEvent => revenueEvent.ReferenceId).Should().Equal("a-in");

        var allTenants = await repository.GetInPeriodAsync(
            new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 31, 23, 59, 59, DateTimeKind.Utc),
            null);
        allTenants.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetGroupedTotalsAsync_GroupsByRequestedDimension()
    {
        await using var context = await CreateContextAsync();
        var day = new DateTime(2026, 9, 10, 8, 0, 0, DateTimeKind.Utc);
        context.Set<RevenueEvent>().AddRange(
            Event("r1", 10m, day, eventType: RevenueEventType.PaymentReceived),
            Event("r2", 30m, day, eventType: RevenueEventType.PaymentReceived),
            Event("r3", 5m, day, eventType: RevenueEventType.RefundProcessed));
        await context.SaveChangesAsync();

        var repository = new RevenueEventRepository(context);
        var byType = await repository.GetGroupedTotalsAsync(
            new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 30, 23, 59, 59, DateTimeKind.Utc),
            null,
            RevenueEventTotalGrouping.EventType);

        byType.Should().BeInAscendingOrder(total => total.Key);
        byType.Should().BeEquivalentTo(
        [
            new RevenueEventGroupTotal(nameof(RevenueEventType.PaymentReceived), 2, 40m),
            new RevenueEventGroupTotal(nameof(RevenueEventType.RefundProcessed), 1, 5m)
        ]);
    }

    [Fact]
    public async Task GetDailyTotalsAsync_ComputesCreditDebitAndNetTotals()
    {
        await using var context = await CreateContextAsync();
        var day = new DateTime(2026, 9, 10, 8, 0, 0, DateTimeKind.Utc);
        context.Set<RevenueEvent>().AddRange(
            Event("pay", 100m, day, eventType: RevenueEventType.PaymentReceived),
            Event("sub", 50m, day, eventType: RevenueEventType.SubscriptionRenewed),
            Event("refund", 30m, day, eventType: RevenueEventType.RefundProcessed),
            Event("fee", 5m, day, eventType: RevenueEventType.FeeCharged),
            Event("adjustment", 999m, day, eventType: RevenueEventType.Adjustment) // neutral: excluded from net
        );
        await context.SaveChangesAsync();

        var repository = new RevenueEventRepository(context);
        var totals = await repository.GetDailyTotalsAsync(
            new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 30, 23, 59, 59, DateTimeKind.Utc),
            null);

        var total = totals.Should().ContainSingle().Subject;
        total.DateUtc.Should().Be(new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc));
        total.CreditTotal.Should().Be(150m);
        total.DebitTotal.Should().Be(35m);
        total.NetTotal.Should().Be(115m);
        total.EventCount.Should().Be(5);
    }

    [Fact]
    public async Task RevenueReconciliationRepository_RunsAndDiscrepancies_RoundTrip()
    {
        await using var context = await CreateContextAsync();
        var repository = new RevenueReconciliationRepository(context);
        var run = new RevenueReconciliationRun
        {
            TenantId = Guid.NewGuid(),
            Source = "stripe-payouts",
            PeriodStartUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodEndUtc = new DateTime(2026, 9, 30, 23, 59, 59, DateTimeKind.Utc)
        };
        await repository.AddRunAsync(run);
        await repository.SaveChangesAsync();
        run.Complete(matchedCount: 1, discrepancyCount: 2, "{}");
        await repository.AddDiscrepancyAsync(new RevenueReconciliationDiscrepancy
        {
            RunId = run.Id,
            Kind = RevenueDiscrepancyKind.AmountMismatch,
            ExternalReference = "ref-1"
        });
        await repository.AddDiscrepancyAsync(new RevenueReconciliationDiscrepancy
        {
            RunId = run.Id,
            Kind = RevenueDiscrepancyKind.MissingInternal,
            ExternalReference = "ref-2"
        });
        await repository.UpdateRunAsync(run);
        await repository.SaveChangesAsync();

        var stored = await repository.GetRunByIdAsync(run.Id);
        stored.Should().NotBeNull();
        stored!.Status.Should().Be(RevenueReconciliationStatus.Completed);
        stored.MatchedCount.Should().Be(1);

        var runs = await repository.GetRunsAsync(run.TenantId, 0, 10);
        runs.TotalCount.Should().Be(1);

        var overlapping = await repository.GetRunsOverlappingPeriodAsync(
            new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc),
            run.TenantId);
        overlapping.Should().ContainSingle();

        var mismatchOnly = await repository.GetDiscrepanciesAsync(run.Id, RevenueDiscrepancyKind.AmountMismatch, 0, 10);
        mismatchOnly.TotalCount.Should().Be(1);
        mismatchOnly.Items.Single().ExternalReference.Should().Be("ref-1");

        var all = await repository.GetDiscrepanciesAsync(run.Id, null, 0, 10);
        all.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task RevenueAnomalyAlertRepository_AlertLifecycle_RoundTrip()
    {
        await using var context = await CreateContextAsync();
        var repository = new RevenueReconciliationRepository(context);
        var tenant = Guid.NewGuid();
        var day = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc);
        var alert = new RevenueAnomalyAlert
        {
            TenantId = tenant,
            Kind = RevenueAnomalyKind.Spike,
            DetectedForDateUtc = day
        };
        await repository.AddAsync(alert);
        await repository.SaveChangesAsync();

        (await repository.ExistsForDayAsync(RevenueAnomalyKind.Spike, day, tenant)).Should().BeTrue();
        (await repository.ExistsForDayAsync(RevenueAnomalyKind.Drop, day, tenant)).Should().BeFalse();
        (await repository.ExistsForDayAsync(RevenueAnomalyKind.Spike, day, null)).Should().BeFalse();

        var fetched = await repository.GetByIdAsync(alert.Id);
        fetched!.Acknowledge(Guid.NewGuid(), "handled");
        await repository.UpdateAsync(fetched);
        await repository.SaveChangesAsync();

        var acknowledged = await repository.GetAlertsAsync(tenant, RevenueAnomalyStatus.Acknowledged, 0, 10);
        acknowledged.TotalCount.Should().Be(1);
        var open = await repository.GetAlertsAsync(tenant, RevenueAnomalyStatus.Open, 0, 10);
        open.TotalCount.Should().Be(0);
    }
}
