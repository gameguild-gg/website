using FluentAssertions;
using GameGuild.Finance.Economy.AdRewards.Persistence;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.AdRewards.UnitTests;

public sealed class PostgreSqlAdRewardOperationalQueryReaderTests
{
    private static readonly Guid TenantId = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ListsOnlyActorTenantSessionsClaimsAndReconciliationsWithStableCursors()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("ad_operational_queries");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var sessions = Enumerable.Range(1, 3).Select(index => Session(TenantId, index)).ToArray();
        var foreign = Session(Guid.NewGuid(), 4);
        var report = Report(TenantId, 1);
        var foreignReport = Report(foreign.TenantId, 2);
        context.Set<AdRewardSessionRow>().AddRange(sessions.Append(foreign));
        context.Set<AdProviderReportRow>().AddRange(report, foreignReport);
        context.Set<AdRewardPendingClaimRow>().AddRange(
            Claim(TenantId, sessions[0], report.Id),
            Claim(foreign.TenantId, foreign, foreignReport.Id));
        context.Set<AdRewardReconciliationRow>().AddRange(
            Reconciliation(TenantId, report, 1),
            Reconciliation(foreign.TenantId, foreignReport, 2));
        await context.SaveChangesAsync();
        var reader = new PostgreSqlAdRewardOperationalQueryReader(context);

        var first = await reader.ListSessionsAsync(TenantId, null, null, 2, null, default);
        var second = await reader.ListSessionsAsync(TenantId, null, null, 2, first.NextCursor, default);
        var claims = await reader.ListPendingClaimsAsync(TenantId, false, 20, null, default);
        var reconciliations = await reader.ListReconciliationsAsync(TenantId, null, 20, null, default);
        var filtered = await reader.ListSessionsAsync(
            TenantId, null, " google-ad-manager ", 20, null, default);

        first.Items.Select(item => item.Id).Should().Equal(sessions[0].Id, sessions[1].Id);
        second.Items.Select(item => item.Id).Should().Equal(sessions[2].Id);
        claims.Items.Should().ContainSingle().Which.TenantId.Should().Be(TenantId);
        reconciliations.Items.Should().ContainSingle().Which.ProviderReportId.Should().Be(report.Id);
        filtered.Items.Should().HaveCount(3);

        await FluentActions.Awaiting(() => reader.ListSessionsAsync(
                Guid.Empty, null, null, 20, null, default).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => reader.ListSessionsAsync(
                TenantId, null, " ", 20, null, default).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => reader.ListSessionsAsync(
                TenantId, null, null, 0, null, default).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Awaiting(() => reader.ListSessionsAsync(
                TenantId, null, null, 101, null, default).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task SessionDetailIncludesAppendOnlyEventsMilestonesAndCompletionWithoutRiskHashes()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("ad_operational_detail");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var session = Session(TenantId, 1);
        context.Set<AdRewardSessionRow>().Add(session);
        context.Set<AdRewardPlaybackMilestoneRow>().Add(new AdRewardPlaybackMilestoneRow
        {
            Id = Guid.NewGuid(), SessionId = session.Id, Sequence = 1, Percentage = 25,
            ObservedAt = Now, EvidenceHash = "milestone-evidence"
        });
        context.Set<AdRewardSessionEventRow>().Add(new AdRewardSessionEventRow
        {
            Id = Guid.NewGuid(), SessionId = session.Id, Sequence = 1,
            State = DurableAdRewardSessionState.Deferred, EvidenceHash = "event-evidence", OccurredAt = Now
        });
        context.Set<AdRewardCompletionRow>().Add(new AdRewardCompletionRow
        {
            SessionId = session.Id, TenantId = TenantId, UserId = session.UserId, WalletId = session.WalletId,
            Network = session.Network, PolicyVersion = 1, IdempotencyKey = "completion-1",
            State = AdRewardCompletionState.PendingProviderReport, RewardSoftUnits = 0,
            EvidenceHashes = "[]", CompletedAt = Now, Version = 1
        });
        await context.SaveChangesAsync();
        var reader = new PostgreSqlAdRewardOperationalQueryReader(context);

        var detail = await reader.FindSessionAsync(TenantId, session.Id, default);

        detail.Should().NotBeNull();
        detail!.Milestones.Should().ContainSingle().Which.Percentage.Should().Be(25);
        detail.Events.Should().ContainSingle().Which.State.Should().Be(DurableAdRewardSessionState.Deferred);
        detail.Completion!.State.Should().Be(AdRewardCompletionState.PendingProviderReport);
        detail.Summary.GetType().GetProperties().Select(property => property.Name)
            .Should().NotContain(["DeviceRiskHash", "IpRiskHash", "AsnRiskHash", "TokenHash", "NonceHash"]);
        (await reader.FindSessionAsync(Guid.NewGuid(), session.Id, default)).Should().BeNull();
        await FluentActions.Awaiting(() => reader.FindSessionAsync(
                Guid.Empty, session.Id, default).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => reader.FindSessionAsync(
                TenantId, Guid.Empty, default).AsTask())
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void ConstructorAndCursorCodecRejectInvalidInfrastructureAndComponents()
    {
        FluentActions.Invoking(() => new PostgreSqlAdRewardOperationalQueryReader(new NonDbContext()))
            .Should().Throw<InvalidOperationException>();

        var identifier = Guid.NewGuid().ToString("N");
        PostgreSqlAdRewardOperationalQueryReader.DecodeCursor(null, "Session").Should().BeNull();
        PostgreSqlAdRewardOperationalQueryReader.DecodeCursor("   ", "Session").Should().BeNull();
        PostgreSqlAdRewardOperationalQueryReader.DecodeCursor(
            $"{Now.UtcTicks:X16}{identifier}", "Session").Should().NotBeNull();

        foreach (var cursor in new[]
                 {
                     "invalid",
                     $"ZZZZZZZZZZZZZZZZ{identifier}",
                     $"0000000000000001{new string('Z', 32)}",
                     $"FFFFFFFFFFFFFFFF{identifier}",
                     $"7FFFFFFFFFFFFFFF{identifier}"
                 })
        {
            FluentActions.Invoking(() =>
                    PostgreSqlAdRewardOperationalQueryReader.DecodeCursor(cursor, "Session"))
                .Should().Throw<ArgumentException>();
        }
    }

    private static AdRewardSessionRow Session(Guid tenantId, int index) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, UserId = Guid.NewGuid(), WalletId = Guid.NewGuid(),
        Network = "google-ad-manager", PolicyVersion = 1, CreativeId = $"creative-{index}",
        DeviceRiskHash = "device", IpRiskHash = "ip", AsnRiskHash = "asn", NonceHash = $"nonce-{index}",
        TokenHash = $"token-{index}", TokenKeyId = "kms", RequiredDurationTicks = TimeSpan.FromSeconds(30).Ticks,
        State = DurableAdRewardSessionState.Deferred, StartIdempotencyKeyHash = $"start-{index}",
        StartRequestHash = $"request-{index}", IssuedAt = Now.AddMinutes(-index),
        ExpiresAt = Now.AddHours(1), UpdatedAt = Now, Version = 1
    };

    private static AdProviderReportRow Report(Guid tenantId, int index) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, Network = "google-ad-manager",
        ReportId = $"report-{index}", Version = 1, BatchId = $"batch-{index}",
        PeriodStart = Now.AddDays(-1), PeriodEnd = Now, ActualRevenueUsdNanos = 100,
        VerifiedSessionIds = "[]", EvidenceHash = $"evidence-{index}", ImportedAt = Now,
        Signature = "signature", PayloadHash = $"payload-{index}", SignatureVerified = true,
        ReceivedAt = Now, ProcessedAt = Now
    };

    private static AdRewardPendingClaimRow Claim(Guid tenantId, AdRewardSessionRow session, Guid reportId) => new()
    {
        SessionId = session.Id, TenantId = tenantId, SourceStampId = Guid.NewGuid(),
        CompletionIdempotencyKeyHash = $"completion-{session.Id:N}",
        CompletionRequestHash = $"request-{session.Id:N}", DeferredAt = session.UpdatedAt,
        ProviderReportId = reportId
    };

    private static AdRewardReconciliationRow Reconciliation(
        Guid tenantId, AdProviderReportRow report, int index) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, ProviderReportId = report.Id,
        Network = report.Network, ReportId = report.ReportId, Version = report.Version,
        BatchId = report.BatchId, EstimatedRevenueUsdNanos = 90,
        PreviousActualRevenueUsdNanos = 0, ActualRevenueUsdNanos = 100,
        ActualDeltaUsdNanos = 100, VarianceUsdNanos = 10,
        HistoricalRewardSoftUnits = index, ReconciledAt = Now
    };

    private static QueryDbContext CreateContext(string connectionString) => new(
        new DbContextOptionsBuilder<QueryDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);

    private sealed class QueryDbContext(DbContextOptions<QueryDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            new AdRewardsModelConfiguration().Configure(modelBuilder);

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Database.BeginTransactionAsync(cancellationToken);
    }

    private sealed class NonDbContext : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => throw new NotSupportedException();

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
