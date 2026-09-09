using FluentAssertions;
using GameGuild.Finance.Economy.AdRewards.Persistence;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.AdRewards.UnitTests;

public sealed class PostgreSqlDurableAdRewardReportReaderTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 21, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.Parse("b1000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task ReadsOnlyTenantReportsWithTheirReconciliation()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("ad_report_reader");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var reportId = Seed(context, TenantId, "network-a", "report-a");
        Seed(context, Guid.NewGuid(), "network-a", "foreign");
        Seed(context, TenantId, "network-b", "other-network");
        await context.SaveChangesAsync();
        var reader = new PostgreSqlDurableAdRewardReportReader(context);

        var result = await reader.ListAsync(TenantId, "network-a", 20, CancellationToken.None);

        result.Should().ContainSingle();
        result[0].ProviderReportId.Should().Be(reportId);
        result[0].ReportId.Should().Be("report-a");
        result[0].Network.Should().Be("network-a");
        result[0].Version.Should().Be(1);
        result[0].BatchId.Should().Be("batch-report-a");
        result[0].PeriodStart.Should().Be(Now.AddHours(-2));
        result[0].PeriodEnd.Should().Be(Now.AddHours(-1));
        result[0].ActualRevenueUsdNanos.Should().Be(125);
        result[0].EvidenceHash.Should().Be("evidence");
        result[0].PayloadHash.Should().Be("payload");
        result[0].SignatureVerified.Should().BeTrue();
        result[0].ReceivedAt.Should().Be(Now.AddMinutes(-20));
        result[0].ProcessedAt.Should().Be(Now.AddMinutes(-20));
        result[0].ProcessingError.Should().BeNull();
        result[0].Reconciliation.Should().NotBeNull();
        var reconciliation = result[0].Reconciliation!;
        reconciliation.EstimatedRevenueUsdNanos.Should().Be(100);
        reconciliation.PreviousActualRevenueUsdNanos.Should().Be(0);
        reconciliation.ActualRevenueUsdNanos.Should().Be(125);
        reconciliation.ActualDeltaUsdNanos.Should().Be(125);
        reconciliation.VarianceUsdNanos.Should().Be(25);
        reconciliation.HistoricalRewardSoftUnits.Should().Be(10);
        reconciliation.ReconciledAt.Should().Be(Now.AddMinutes(-20));
    }

    [Fact]
    public async Task SupportsAllNetworksAndValidatesInputs()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("ad_report_reader_all");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        Seed(context, TenantId, "network-a", "report-a");
        var reportWithoutReconciliation = Seed(context, TenantId, "network-b", "report-b");
        context.Remove(context.Set<AdRewardReconciliationRow>().Local
            .Single(row => row.ProviderReportId == reportWithoutReconciliation));
        await context.SaveChangesAsync();
        var reader = new PostgreSqlDurableAdRewardReportReader(context);

        (await reader.ListAsync(TenantId, null, 1, CancellationToken.None)).Should().HaveCount(1);
        var all = await reader.ListAsync(TenantId, null, 20, CancellationToken.None);
        all.Should().Contain(item => item.ProviderReportId == reportWithoutReconciliation && item.Reconciliation == null);
        await FluentActions.Awaiting(() => reader.ListAsync(
                Guid.Empty, null, 10, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => reader.ListAsync(
                TenantId, " ", 10, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => reader.ListAsync(
                TenantId, null, 0, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new PostgreSqlDurableAdRewardReportReader(new StubContext()))
            .Should().Throw<InvalidOperationException>();
    }

    private static Guid Seed(ReportDbContext context, Guid tenantId, string network, string externalReportId)
    {
        var id = Guid.NewGuid();
        context.Set<AdProviderReportRow>().Add(new AdProviderReportRow
        {
            Id = id, TenantId = tenantId, Network = network, ReportId = externalReportId,
            Version = 1, BatchId = $"batch-{externalReportId}", PeriodStart = Now.AddHours(-2),
            PeriodEnd = Now.AddHours(-1), ActualRevenueUsdNanos = 125,
            VerifiedSessionIds = "[]", EvidenceHash = "evidence", ImportedAt = Now.AddMinutes(-30),
            Signature = "signature", PayloadHash = "payload", SignatureVerified = true,
            ReceivedAt = Now.AddMinutes(-20), ProcessedAt = Now.AddMinutes(-20)
        });
        context.Set<AdRewardReconciliationRow>().Add(new AdRewardReconciliationRow
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ProviderReportId = id, Network = network,
            ReportId = externalReportId, Version = 1, BatchId = $"batch-{externalReportId}",
            EstimatedRevenueUsdNanos = 100, PreviousActualRevenueUsdNanos = 0,
            ActualRevenueUsdNanos = 125, ActualDeltaUsdNanos = 125, VarianceUsdNanos = 25,
            HistoricalRewardSoftUnits = 10, ReconciledAt = Now.AddMinutes(-20)
        });
        return id;
    }

    private static ReportDbContext CreateContext(string connectionString) => new(
        new DbContextOptionsBuilder<ReportDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);

    private sealed class ReportDbContext(DbContextOptions<ReportDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            new AdRewardsModelConfiguration().Configure(modelBuilder);

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Database.BeginTransactionAsync(cancellationToken);
    }

    private sealed class StubContext : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
