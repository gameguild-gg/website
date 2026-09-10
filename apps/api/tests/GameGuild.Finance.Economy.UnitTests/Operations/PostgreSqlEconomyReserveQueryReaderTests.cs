using FluentAssertions;
using GameGuild.Finance.Economy.Operations;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Reserves;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.UnitTests.Operations;

public sealed class PostgreSqlEconomyReserveQueryReaderTests
{
    private static readonly Guid TenantId = Guid.Parse("ad000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReadsCustodyAndReserveHistoryWithStableCursorsAndActiveHeadEvidence()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("reserve_queries");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var observations = Enumerable.Range(1, 3)
            .Select(index => Observation(index, Now.AddMinutes(-index)))
            .ToArray();
        context.Set<EconomyCustodyObservationRow>().AddRange(observations);
        var proposals = Enumerable.Range(1, 3).Select(Proposal).ToArray();
        context.Set<EconomyReserveProposalRow>().AddRange(proposals);
        context.Set<EconomyReserveHeadRow>().Add(new EconomyReserveHeadRow
        {
            Version = 3,
            IsActive = true,
            PolicyVersion = 7,
            AuthorizationEpoch = 9,
            ObservedAt = Now.AddMinutes(-5),
            ExpiresAt = Now.AddHours(1),
            Coverage = ReserveCoverageState.Covered,
            EvidenceHash = "reserve-evidence",
            ActivatedAt = Now.AddMinutes(-1)
        });
        context.Set<EconomyReserveAssetAllocationRow>().Add(new EconomyReserveAssetAllocationRow
        {
            Id = Guid.NewGuid(),
            ReserveVersion = 3,
            AssetKey = "usd:stripe",
            Purpose = ReserveBackingPurpose.HardCoin,
            EligibleUsdNanos = 1_000
        });
        context.Set<EconomyCustodyReconciliationRow>().Add(new EconomyCustodyReconciliationRow
        {
            Id = Guid.NewGuid(),
            ReserveVersion = 3,
            ObservationIds = $"[\"{observations[0].Id:N}\"]",
            LiabilityUsdNanos = 1_000,
            EligibleAssetUsdNanos = 1_000,
            VarianceUsdNanos = 0,
            IsReconciled = true,
            EvidenceHash = "reconciliation-evidence",
            ReconciledBy = Guid.NewGuid(),
            ReconciledAt = Now
        });
        await context.SaveChangesAsync();
        var reader = new PostgreSqlEconomyReserveQueryReader(context);

        var custodyFirst = await reader.ListCustodyAsync(TenantId, 2, null, default);
        var custodySecond = await reader.ListCustodyAsync(
            TenantId, 2, custodyFirst.NextCursor, default);
        var proposalFirst = await reader.ListProposalsAsync(TenantId, 2, null, default);
        var proposalSecond = await reader.ListProposalsAsync(
            TenantId, 2, proposalFirst.NextCursor, default);
        var active = await reader.ReadActiveHeadAsync(TenantId, default);

        custodyFirst.Items.Select(item => item.Version).Should().Equal(1, 2);
        custodySecond.Items.Select(item => item.Version).Should().Equal(3);
        proposalFirst.Items.Select(item => item.Version).Should().Equal(3, 2);
        proposalSecond.Items.Select(item => item.Version).Should().Equal(1);
        active.Should().NotBeNull();
        active!.Head.Version.Should().Be(3);
        active.Allocations.Should().ContainSingle();
        active.Reconciliation!.EvidenceHash.Should().Be("reconciliation-evidence");
    }

    [Fact]
    public async Task FindsDetailsAndRejectsInvalidInput()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("reserve_query_details");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var observation = Observation(1, Now);
        var proposal = Proposal(1);
        context.Set<EconomyCustodyObservationRow>().Add(observation);
        context.Set<EconomyReserveProposalRow>().Add(proposal);
        await context.SaveChangesAsync();
        var reader = new PostgreSqlEconomyReserveQueryReader(context);

        (await reader.FindCustodyAsync(TenantId, observation.Id, default))!.PayloadHash
            .Should().Be("payload-1");
        (await reader.FindProposalAsync(TenantId, proposal.Id, default))!.SnapshotHash
            .Should().Be("snapshot-1");
        await FluentActions.Awaiting(() => reader.ListCustodyAsync(
                TenantId, 20, "invalid", default).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => reader.ListProposalsAsync(
                Guid.Empty, 20, null, default).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => reader.ListProposalsAsync(
                TenantId, 20, "invalid", default).AsTask())
            .Should().ThrowAsync<ArgumentException>();

        var identifier = Guid.NewGuid().ToString("N");
        foreach (var invalidCursor in new[]
                 {
                     $"ZZZZZZZZZZZZZZZZ{identifier}",
                     $"FFFFFFFFFFFFFFFF{identifier}",
                     $"7FFFFFFFFFFFFFFF{identifier}",
                     $"0000000000000001{new string('Z', 32)}"
                 })
        {
            await FluentActions.Awaiting(() => reader.ListCustodyAsync(
                    TenantId, 20, invalidCursor, default).AsTask())
                .Should().ThrowAsync<ArgumentException>();
        }

        foreach (var invalidCursor in new[]
                 {
                     $"ZZZZZZZZZZZZZZZZ{identifier}",
                     $"0000000000000000{identifier}",
                     $"0000000000000001{new string('Z', 32)}"
                 })
        {
            await FluentActions.Awaiting(() => reader.ListProposalsAsync(
                    TenantId, 20, invalidCursor, default).AsTask())
                .Should().ThrowAsync<ArgumentException>();
        }

        await FluentActions.Awaiting(() => reader.ListCustodyAsync(
                TenantId, 0, null, default).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Awaiting(() => reader.ListCustodyAsync(
                TenantId, 101, null, default).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Awaiting(() => reader.FindCustodyAsync(
                TenantId, Guid.Empty, default).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => reader.FindCustodyAsync(
                Guid.Empty, observation.Id, default).AsTask())
            .Should().ThrowAsync<ArgumentException>();
    }

    private static EconomyCustodyObservationRow Observation(int version, DateTimeOffset observedAt) => new()
    {
        Id = Guid.NewGuid(),
        Provider = "stripe",
        AssetKey = $"usd:{version}",
        Purpose = ReserveBackingPurpose.HardCoin,
        Version = version,
        EligibleUsdNanos = 1_000,
        ObservedAt = observedAt,
        ExpiresAt = Now.AddHours(1),
        PayloadHash = $"payload-{version}",
        KeyId = "kms-key",
        Signature = "signature"
    };

    private static EconomyReserveProposalRow Proposal(int version) => new()
    {
        Id = Guid.NewGuid(),
        Version = version,
        PolicyVersion = 7,
        AuthorizationEpoch = version,
        SnapshotHash = $"snapshot-{version}",
        LiabilityUsdNanos = 1_000,
        EligibleAssetUsdNanos = 1_000,
        Coverage = ReserveCoverageState.Covered,
        ObservationIds = "[]",
        AssetAllocations = "[]",
        EvidenceHash = $"evidence-{version}",
        RequestHash = $"request-{version}",
        ProposedBy = Guid.NewGuid(),
        ObservedAt = Now.AddMinutes(-10),
        ProposedAt = Now.AddMinutes(-version),
        ExpiresAt = Now.AddHours(1),
        Status = "Proposed"
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
            new EconomyModelConfiguration().Configure(modelBuilder);

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Database.BeginTransactionAsync(cancellationToken);
    }
}
