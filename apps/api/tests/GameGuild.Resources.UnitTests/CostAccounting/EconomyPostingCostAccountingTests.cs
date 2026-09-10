using FluentAssertions;
using GameGuild.Finance.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameGuild.Resources.UnitTests.CostAccounting;

public sealed class EconomyPostingCostAccountingTests
{
    [Fact]
    public async Task HandleAsync_RecordsOnePostingAndEveryJournalLine()
    {
        await using var context = new TestDbContext(
            new DbContextOptionsBuilder<TestDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var valuation = new InternalCostValuationService(
            context,
            TimeProvider.System,
            NullLogger<InternalCostValuationService>.Instance);
        var handler = new CostAccountingEventHandler(context, valuation);
        var tenantId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var postingId = Guid.NewGuid();
        var @event = new EconomyPostingAcceptedEventV1(
            postingId,
            "posting-hash",
            [
                new FinancePostingLineV1(1, "Debit", "Cash", "HardCoin", 40, null),
                new FinancePostingLineV1(2, "Credit", "Revenue", "HardCoin", 40, null)
            ])
        {
            EventId = eventId,
            TenantId = tenantId,
            ActorId = Guid.NewGuid(),
            AggregateType = "EconomyPosting",
            AggregateId = postingId.ToString(),
            CorrelationId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow
        };

        await ((IIntegrationEventHandler<EconomyPostingAcceptedEventV1>)handler).HandleAsync(@event);

        var entries = await context.Set<InternalUsageLedgerEntry>()
            .OrderBy(entry => entry.MetricCode)
            .ToListAsync();
        entries.Should().HaveCount(2);
        entries.Should().ContainSingle(entry =>
            entry.MetricCode == InternalCostMetrics.EconomyPosting &&
            entry.Quantity == 1 &&
            entry.Unit == "posting");
        entries.Should().ContainSingle(entry =>
            entry.MetricCode == InternalCostMetrics.EconomyJournalLine &&
            entry.Quantity == 2 &&
            entry.Unit == "line");
        entries.ShouldBelongToSingleEvent(eventId, tenantId);
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<InternalUsageLedgerEntry>();
            modelBuilder.Entity<CloudPriceRate>();
            modelBuilder.Entity<InternalCostValuation>();
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}

internal static class InternalUsageLedgerEntryAssertions
{
    public static void ShouldBelongToSingleEvent(
        this IEnumerable<InternalUsageLedgerEntry> entries,
        Guid eventId,
        Guid tenantId)
    {
        entries.Should().OnlyContain(entry => entry.EventId == eventId && entry.TenantId == tenantId);
    }
}
