using FluentAssertions;
using GameGuild.Finance.Economy.AdRewards.Persistence;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.AdRewards.UnitTests;

public sealed class DurableAdRewardReportServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Import_ReconcilesAttributionVerifiesDeferredClaimAndReplaysExactly()
    {
        await using var fixture = await Fixture.CreateAsync("ad_report_import", deferred: true);
        var service = fixture.Service();
        var request = fixture.Request();

        var imported = await service.ImportAsync(request);

        imported.IsDuplicate.Should().BeFalse();
        imported.VerifiedPendingSessions.Should().Equal(fixture.Session.Id);
        imported.Reconciliation.EstimatedRevenueUsdNanos.Should().Be(100);
        imported.Reconciliation.PreviousActualRevenueUsdNanos.Should().Be(0);
        imported.Reconciliation.ActualRevenueUsdNanos.Should().Be(125);
        imported.Reconciliation.ActualDeltaUsdNanos.Should().Be(125);
        imported.Reconciliation.VarianceUsdNanos.Should().Be(25);
        imported.Reconciliation.HistoricalRewardSoftUnits.Should().Be(45);
        var pending = await fixture.Context.Set<AdRewardPendingClaimRow>().SingleAsync();
        pending.ProviderReportId.Should().Be(imported.ProviderReportId);
        pending.ConfirmedAt.Should().Be(Now);
        (await fixture.Context.Set<AdRewardSessionRow>().SingleAsync()).State
            .Should().Be(DurableAdRewardSessionState.Verified);
        (await fixture.Context.Set<AdRewardProviderBatchClaimRow>().CountAsync()).Should().Be(1);
        (await fixture.Context.Set<AdRewardSessionEventRow>().SingleAsync()).State
            .Should().Be(DurableAdRewardSessionState.Verified);

        fixture.Context.ChangeTracker.Clear();
        var replay = await service.ImportAsync(request);

        replay.Should().BeEquivalentTo(imported with { IsDuplicate = true });
        (await fixture.Context.Set<AdProviderReportRow>().CountAsync()).Should().Be(1);
        (await fixture.Context.Set<AdRewardReconciliationRow>().CountAsync()).Should().Be(1);

        var conflict = request with
        {
            Report = request.Report with { ActualRevenueUsdNanos = 126 }
        };
        await FluentActions.Awaiting(() => service.ImportAsync(conflict).AsTask())
            .Should().ThrowAsync<AdProviderReportConflictException>();
    }

    [Fact]
    public async Task Import_AcceptsOnlyContiguousForwardVersionsAndUsesPreviousActual()
    {
        await using var fixture = await Fixture.CreateAsync("ad_report_versions", deferred: false);
        var service = fixture.Service();
        var first = await service.ImportAsync(fixture.Request());
        fixture.Context.ChangeTracker.Clear();
        var secondRequest = fixture.Request() with
        {
            Report = fixture.Report(version: 2, actual: 175, importedAt: Now.AddMinutes(1)),
            ReceivedAt = Now.AddMinutes(1)
        };

        var second = await service.ImportAsync(secondRequest);

        second.VerifiedPendingSessions.Should().BeEmpty();
        second.Reconciliation.PreviousActualRevenueUsdNanos.Should().Be(125);
        second.Reconciliation.ActualDeltaUsdNanos.Should().Be(50);
        second.ProviderReportId.Should().NotBe(first.ProviderReportId);

        fixture.Context.ChangeTracker.Clear();
        var skipped = secondRequest with
        {
            Report = fixture.Report(version: 4, actual: 200, importedAt: Now.AddMinutes(2)),
            ReceivedAt = Now.AddMinutes(2)
        };
        await FluentActions.Awaiting(() => service.ImportAsync(skipped).AsTask())
            .Should().ThrowAsync<AdProviderReportConflictException>();
    }

    [Fact]
    public async Task Import_RejectsAnUnverifiedReportOrForeignSession()
    {
        await using var fixture = await Fixture.CreateAsync("ad_report_reject", deferred: false);
        await FluentActions.Awaiting(() => fixture.Service(adapter: new Adapter("network-a") { ReportValid = false })
                .ImportAsync(fixture.Request()).AsTask())
            .Should().ThrowAsync<AdProviderReportVerificationException>();

        var foreign = fixture.Request() with
        {
            Report = fixture.Report(sessionIds: [Guid.NewGuid()])
        };
        await FluentActions.Awaiting(() => fixture.Service().ImportAsync(foreign).AsTask())
            .Should().ThrowAsync<AdProviderReportVerificationException>();
    }

    [Fact]
    public async Task Import_ValidatesEveryProviderPayloadInvariant()
    {
        await using var fixture = await Fixture.CreateAsync("ad_report_validate", deferred: false);
        var service = fixture.Service();
        var request = fixture.Request();
        var report = request.Report;
        var invalid = new ImportDurableAdProviderReportRequest?[]
        {
            null,
            new(request.TenantId, null!, request.ReceivedAt),
            request with { TenantId = Guid.Empty },
            request with { Report = report with { Network = " " } },
            request with { Report = report with { ReportId = " " } },
            request with { Report = report with { BatchId = " " } },
            request with { Report = report with { EvidenceHash = " " } },
            request with { Report = report with { Signature = " " } },
            request with { Report = report with { Version = 0 } },
            request with { Report = report with { PeriodEnd = report.PeriodStart } },
            request with { Report = report with { ImportedAt = report.PeriodEnd.AddTicks(-1) } },
            request with { Report = report with { ImportedAt = request.ReceivedAt.AddTicks(1) } },
            request with { Report = report with { ActualRevenueUsdNanos = -1 } },
            request with { Report = report with { VerifiedSessionIds = [] } },
            request with { Report = report with { VerifiedSessionIds = [Guid.Empty] } },
            request with { Report = report with { VerifiedSessionIds = [fixture.Session.Id, fixture.Session.Id] } }
        };

        foreach (var item in invalid)
        {
            await FluentActions.Awaiting(() => service.ImportAsync(item!).AsTask())
                .Should().ThrowAsync<Exception>();
        }
    }

    [Fact]
    public void Constructor_RejectsMissingOrNonRelationalDependencies()
    {
        var resolver = new AdRewardProviderAdapterResolver([new Adapter("network-a")]);
        FluentActions.Invoking(() => new DurableAdRewardReportService(null!, resolver))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new DurableAdRewardReportService(new StubContext(), null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new DurableAdRewardReportService(new StubContext(), resolver))
            .Should().Throw<InvalidOperationException>();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(EconomyPostgreSqlTestDatabase database, TestDbContext context)
        {
            Database = database;
            Context = context;
            TenantId = Guid.NewGuid();
            Session = SessionRow(TenantId);
        }

        private EconomyPostgreSqlTestDatabase Database { get; }
        public TestDbContext Context { get; }
        public Guid TenantId { get; }
        public AdRewardSessionRow Session { get; }

        public static async Task<Fixture> CreateAsync(string prefix, bool deferred)
        {
            var database = await EconomyPostgreSqlTestDatabase.CreateAsync(prefix);
            var context = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>()
                .UseNpgsql(database.ConnectionString)
                .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
                .Options);
            var fixture = new Fixture(database, context);
            fixture.Session.State = deferred
                ? DurableAdRewardSessionState.Deferred
                : DurableAdRewardSessionState.Posted;
            context.Add(fixture.Session);
            context.Add(new AdRewardAttributionRow
            {
                SessionId = fixture.Session.Id, TenantId = fixture.TenantId, Network = "network-a",
                PolicyVersion = 1, ProviderBatchId = "batch-a", EstimatedRevenueUsdNanos = 100,
                RewardSoftUnits = 45, CompletedAt = Now.AddHours(-1)
            });
            if (deferred)
            {
                context.Add(new AdRewardPendingClaimRow
                {
                    SessionId = fixture.Session.Id, TenantId = fixture.TenantId,
                    SourceStampId = Guid.NewGuid(), CompletionIdempotencyKeyHash = "completion-key",
                    CompletionRequestHash = "completion-request", DeferredAt = Now.AddHours(-1)
                });
                context.Add(new AdRewardCompletionRow
                {
                    SessionId = fixture.Session.Id, TenantId = fixture.TenantId, UserId = fixture.Session.UserId,
                    WalletId = fixture.Session.WalletId, Network = "network-a", PolicyVersion = 1,
                    IdempotencyKey = "completion-key", State = AdRewardCompletionState.PendingProviderReport,
                    RewardSoftUnits = 0, EvidenceHashes = "[]", CompletedAt = Now.AddHours(-1), Version = 1
                });
            }
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            return fixture;
        }

        public DurableAdRewardReportService Service(Adapter? adapter = null) => new(
            Context, new AdRewardProviderAdapterResolver([adapter ?? new Adapter("network-a")]));

        public ImportDurableAdProviderReportRequest Request() => new(TenantId, Report(), Now);

        public AdProviderReport Report(
            int version = 1,
            long actual = 125,
            DateTimeOffset? importedAt = null,
            IReadOnlyList<Guid>? sessionIds = null) => new(
            "network-a", "report-a", version, "batch-a", Now.AddHours(-2), Now.AddHours(-1),
            actual, sessionIds ?? [Session.Id], "evidence", importedAt ?? Now.AddMinutes(-1), "signature");

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Database.DisposeAsync();
        }

        private static AdRewardSessionRow SessionRow(Guid tenantId) => new()
        {
            Id = Guid.NewGuid(), TenantId = tenantId, UserId = Guid.NewGuid(), WalletId = Guid.NewGuid(),
            Network = "network-a", PolicyVersion = 1, CreativeId = "creative", DeviceRiskHash = "device",
            IpRiskHash = "ip", AsnRiskHash = "asn", NonceHash = "nonce", TokenHash = "token", TokenKeyId = "key",
            RequiredDurationTicks = TimeSpan.FromSeconds(30).Ticks, StartIdempotencyKeyHash = Guid.NewGuid().ToString("N"),
            StartRequestHash = "request", IssuedAt = Now.AddHours(-2), ExpiresAt = Now.AddHours(1),
            UpdatedAt = Now.AddHours(-1), Version = 2
        };
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            new AdRewardsModelConfiguration().Configure(modelBuilder);
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Database.BeginTransactionAsync(cancellationToken);
    }

    private sealed class Adapter(string network) : IAdRewardProviderAdapter
    {
        public string Network { get; } = network;
        public bool ReportValid { get; set; } = true;
        public ValueTask<AdRewardProviderProofVerification> VerifyCompletionAsync(
            DurableAdRewardSessionClaims session, ProviderCompletionProof proof,
            DateTimeOffset receivedAt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public ValueTask<bool> VerifyReportAsync(AdProviderReport report, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ReportValid);
    }

    private sealed class StubContext : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
