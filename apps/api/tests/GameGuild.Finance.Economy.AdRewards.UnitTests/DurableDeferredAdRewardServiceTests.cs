using FluentAssertions;
using GameGuild.Finance.Economy.AdRewards.Persistence;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Risk;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.AdRewards.UnitTests;

public sealed class DurableDeferredAdRewardServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Confirm_IssuesVerifiedClaimAndReturnsAnExactIdempotentReplay()
    {
        await using var fixture = await Fixture.CreateAsync("ad_deferred_confirm");
        var issuance = new Issuance();
        var service = fixture.Service(Policy(), issuance: issuance);
        var request = fixture.Request();

        var result = await service.ConfirmAsync(request);

        result.State.Should().Be(AdRewardCompletionState.Issued);
        result.RewardSoftUnits.Should().Be(45);
        result.PostingId.Should().NotBeNull();
        result.OutputLotId.Should().NotBeNull();
        result.IsDuplicate.Should().BeFalse();
        issuance.Requests.Should().ContainSingle();
        issuance.Requests[0].ProviderEventReference.Should()
            .Be($"report-a:1:{fixture.Session.Id:N}");
        (await fixture.Context.Set<AdRewardCapConsumptionRow>().CountAsync()).Should().Be(6);
        (await fixture.Context.Set<AdRewardBudgetConsumptionRow>().CountAsync()).Should().Be(1);
        (await fixture.Context.Set<AdRewardAttributionRow>().SingleAsync()).ProviderBatchId.Should().Be("batch-a");
        var pending = await fixture.Context.Set<AdRewardPendingClaimRow>().SingleAsync();
        pending.ConfirmationIdempotencyKeyHash.Should().NotBeNull();
        pending.ConfirmationRequestHash.Should().NotBeNull();
        (await fixture.Context.Set<AdRewardSessionRow>().SingleAsync()).State
            .Should().Be(DurableAdRewardSessionState.Posted);

        fixture.Context.ChangeTracker.Clear();
        var replay = await service.ConfirmAsync(request);
        replay.Should().BeEquivalentTo(result with { IsDuplicate = true });
        issuance.Requests.Should().ContainSingle();

        await FluentActions.Awaiting(() => service.ConfirmAsync(
                request with { IdempotencyKey = new IdempotencyKey("different") }).AsTask())
            .Should().ThrowAsync<AdRewardIdempotencyConflictException>();
    }

    [Fact]
    public async Task Confirm_AccumulatesRemainderWithoutAuthorizingValue()
    {
        await using var fixture = await Fixture.CreateAsync("ad_deferred_remainder");
        var capabilities = new Capabilities();
        var issuance = new Issuance();
        var result = await fixture.Service(Policy(ecpm: 1), capabilities, issuance)
            .ConfirmAsync(fixture.Request());

        result.State.Should().Be(AdRewardCompletionState.AccumulatedRemainder);
        result.RewardSoftUnits.Should().Be(0);
        capabilities.Requests.Should().BeEmpty();
        issuance.Requests.Should().BeEmpty();
        (await fixture.Context.Set<AdRewardAccumulatorRow>().SingleAsync()).RemainderNumerator
            .Should().NotBe("0");
    }

    [Fact]
    public async Task Confirm_UpdatesExistingAccumulatorAndPropagatesWriterReplay()
    {
        await using var fixture = await Fixture.CreateAsync("ad_deferred_accumulator");
        fixture.Context.Add(new AdRewardAccumulatorRow
        {
            TenantId = fixture.Session.TenantId, WalletId = fixture.Session.WalletId,
            Network = fixture.Session.Network, PolicyVersion = 1, RemainderNumerator = "1",
            CanonicalDenominator = AdRewardRationalAccumulator.CanonicalDenominator.ToString(),
            Version = 1, UpdatedAt = Now.AddMinutes(-1)
        });
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service(Policy(), issuance: new Issuance(isDuplicate: true))
            .ConfirmAsync(fixture.Request());

        result.IsDuplicate.Should().BeTrue();
        (await fixture.Context.Set<AdRewardAccumulatorRow>().SingleAsync()).Version.Should().Be(2);
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("session")]
    [InlineData("claim")]
    [InlineData("not-verified")]
    [InlineData("unbound-report")]
    public async Task Confirm_RequiresOwnedVerifiedReportBoundClaim(string scenario)
    {
        await using var fixture = await Fixture.CreateAsync("ad_deferred_binding");
        var request = fixture.Request();
        if (scenario == "actor") request = request with { ActorId = Guid.NewGuid() };
        if (scenario == "session") request = request with { SessionId = Guid.NewGuid() };
        if (scenario == "claim")
        {
            fixture.Context.Remove(await fixture.Context.Set<AdRewardPendingClaimRow>().SingleAsync());
            await fixture.Context.SaveChangesAsync();
            fixture.Context.ChangeTracker.Clear();
        }
        if (scenario == "not-verified")
        {
            var session = await fixture.Context.Set<AdRewardSessionRow>().SingleAsync();
            session.State = DurableAdRewardSessionState.Deferred;
            await fixture.Context.SaveChangesAsync();
            fixture.Context.ChangeTracker.Clear();
        }
        if (scenario == "unbound-report")
        {
            var pending = await fixture.Context.Set<AdRewardPendingClaimRow>().SingleAsync();
            pending.ProviderReportId = null;
            pending.ConfirmedAt = null;
            await fixture.Context.SaveChangesAsync();
            fixture.Context.ChangeTracker.Clear();
        }

        Func<Task> action = () => fixture.Service(Policy()).ConfirmAsync(request).AsTask();
        if (scenario == "actor") await action.Should().ThrowAsync<AdRewardRiskBindingException>();
        else await action.Should().ThrowAsync<AdRewardReplayException>();
    }

    [Theory]
    [InlineData("mode")]
    [InlineData("stale")]
    [InlineData("uncertified")]
    public async Task Confirm_FailsClosedForUnavailableBoundPolicyOrReport(string scenario)
    {
        await using var fixture = await Fixture.CreateAsync("ad_deferred_policy");
        var policy = scenario switch
        {
            "mode" => Policy(issuanceMode: AdRewardIssuanceMode.ImmediateProviderProof),
            "stale" => Policy(reportStaleAfter: TimeSpan.FromMinutes(1)),
            _ => Policy(certified: false)
        };

        await FluentActions.Awaiting(() => fixture.Service(policy).ConfirmAsync(fixture.Request()).AsTask())
            .Should().ThrowAsync<AdRewardDependencyUnavailableException>();
    }

    [Fact]
    public async Task Confirm_RejectsAnUnexpectedPostingIdentity()
    {
        await using var fixture = await Fixture.CreateAsync("ad_deferred_posting");
        await FluentActions.Awaiting(() => fixture.Service(Policy(), issuance: new Issuance(wrongPostingId: true))
                .ConfirmAsync(fixture.Request()).AsTask())
            .Should().ThrowAsync<RegisteredPostingRejectedException>();
    }

    [Fact]
    public async Task Confirm_RejectsAnExceededAtomicScopeCapWithExistingConsumptions()
    {
        await using var fixture = await Fixture.CreateAsync("ad_deferred_scope_cap");
        var auxiliary = AuxiliarySession(fixture.Session.TenantId);
        fixture.Context.Add(auxiliary);
        fixture.Context.AddRange(
            Consumption(fixture.Session.TenantId, fixture.Session.Id, AdRewardCapScope.User,
                KmsAdRewardSessionTokenProtector.HashOpaque(fixture.Session.UserId.ToString("N")), 10),
            Consumption(fixture.Session.TenantId, auxiliary.Id, AdRewardCapScope.User, "different-user", 10),
            Consumption(fixture.Session.TenantId, fixture.Session.Id, AdRewardCapScope.Device, "different-device", 10));
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        var budget = new AdRewardBudgetPolicy(54, 10_000, 10_000, 10_000,
            10_000_000_000, TimeSpan.FromDays(1));

        await FluentActions.Awaiting(() => fixture.Service(Policy(budget: budget))
                .ConfirmAsync(fixture.Request()).AsTask())
            .Should().ThrowAsync<AdRewardBudgetExceededException>();
    }

    [Fact]
    public async Task Confirm_RejectsAnExceededFundedLossBudget()
    {
        await using var fixture = await Fixture.CreateAsync("ad_deferred_loss_cap");
        var budget = new AdRewardBudgetPolicy(10_000, 10_000, 10_000, 10_000,
            1, TimeSpan.FromDays(1));

        await FluentActions.Awaiting(() => fixture.Service(Policy(budget: budget))
                .ConfirmAsync(fixture.Request()).AsTask())
            .Should().ThrowAsync<AdRewardBudgetExceededException>();
    }

    [Fact]
    public async Task Confirm_ValidatesEveryAuthorizationInput()
    {
        await using var fixture = await Fixture.CreateAsync("ad_deferred_validate");
        var service = fixture.Service(Policy());
        var request = fixture.Request();
        var invalid = new ConfirmDeferredAdRewardRequest?[]
        {
            null,
            request with { TenantId = Guid.Empty },
            request with { ActorId = Guid.Empty },
            request with { SessionId = Guid.Empty },
            request with { RiskDecisionId = Guid.Empty },
            request with { SubjectReference = " " },
            request with { JurisdictionCode = " " },
            request with { OperationFingerprint = " " }
        };
        foreach (var item in invalid)
        {
            await FluentActions.Awaiting(() => service.ConfirmAsync(item!).AsTask())
                .Should().ThrowAsync<ArgumentException>();
        }
    }

    [Fact]
    public void Constructor_RejectsEveryMissingOrNonRelationalDependency()
    {
        var context = new StubContext();
        var policies = new Policies(Policy());
        var capabilities = new Capabilities();
        var authority = new Authority();
        var issuance = new Issuance();
        Action<IApplicationDbContext, IDurableAdRewardPolicyReader, IEconomyCapabilityAuthorizationService,
            IRegisteredPostingCapabilityResolver, IAdRewardIssuanceGateway> create =
            (a, b, c, d, e) => _ = new DurableDeferredAdRewardService(a, b, c, d, e);

        FluentActions.Invoking(() => create(null!, policies, capabilities, authority, issuance)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => create(context, null!, capabilities, authority, issuance)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => create(context, policies, null!, authority, issuance)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => create(context, policies, capabilities, null!, issuance)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => create(context, policies, capabilities, authority, null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => create(context, policies, capabilities, authority, issuance)).Should().Throw<InvalidOperationException>();
    }

    private static AdRewardNetworkPolicySnapshot Policy(
        long ecpm = 1_000_000_000,
        bool certified = true,
        AdRewardIssuanceMode issuanceMode = AdRewardIssuanceMode.DeferredReport,
        TimeSpan? reportStaleAfter = null,
        AdRewardBudgetPolicy? budget = null,
        long ipMaximum = 10_000,
        long asnMaximum = 10_000)
    {
        var policy = new AdNetworkPolicy(
            "network-a", new PolicyVersion(1), Now.AddHours(-2), Now.AddHours(2),
            issuanceMode, AdNetworkYieldState.Trailing, ecpm, 500_000, 100_000, 800_000,
            TimeSpan.FromSeconds(2), 1_000, Now, reportStaleAfter ?? TimeSpan.FromHours(1), 1);
        return new AdRewardNetworkPolicySnapshot(
            Guid.Empty, policy, budget ?? new AdRewardBudgetPolicy(
                10_000, 10_000, 10_000, 10_000, 10_000_000_000, TimeSpan.FromDays(1)),
            ipMaximum, asnMaximum, "provider-hash", certified, "payload", "key", "signature");
    }

    private static AdRewardSessionRow AuxiliarySession(Guid tenantId) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, UserId = Guid.NewGuid(), WalletId = Guid.NewGuid(),
        Network = "network-a", PolicyVersion = 1, CreativeId = "auxiliary", DeviceRiskHash = "aux-device",
        IpRiskHash = "aux-ip", AsnRiskHash = "aux-asn", NonceHash = Guid.NewGuid().ToString("N"),
        TokenHash = Guid.NewGuid().ToString("N"), TokenKeyId = "key",
        RequiredDurationTicks = TimeSpan.FromSeconds(30).Ticks, State = DurableAdRewardSessionState.Posted,
        StartIdempotencyKeyHash = Guid.NewGuid().ToString("N"), StartRequestHash = "aux-request",
        IssuedAt = Now.AddMinutes(-2), ExpiresAt = Now.AddMinutes(3), UpdatedAt = Now, Version = 1
    };

    private static AdRewardCapConsumptionRow Consumption(
        Guid tenantId, Guid sessionId, AdRewardCapScope scope, string subject, long units) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, SessionId = sessionId, Scope = scope,
        SubjectHash = subject, WindowStartedAt = Now.AddHours(-1), WindowEndsAt = Now.AddHours(1),
        SoftUnits = units, LossBudgetUsdNanos = 0, ConsumedAt = Now.AddMinutes(-1)
    };

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(EconomyPostgreSqlTestDatabase database, TestDbContext context)
        {
            Database = database;
            Context = context;
            Session = SessionRow();
            Report = ReportRow(Session.TenantId);
        }

        private EconomyPostgreSqlTestDatabase Database { get; }
        public TestDbContext Context { get; }
        public AdRewardSessionRow Session { get; }
        public AdProviderReportRow Report { get; }

        public static async Task<Fixture> CreateAsync(string prefix)
        {
            var database = await EconomyPostgreSqlTestDatabase.CreateAsync(prefix);
            var context = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>()
                .UseNpgsql(database.ConnectionString)
                .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
                .Options);
            var fixture = new Fixture(database, context);
            context.AddRange(fixture.Session, fixture.Report);
            context.Add(new AdRewardReconciliationRow
            {
                Id = Guid.NewGuid(), TenantId = fixture.Session.TenantId, ProviderReportId = fixture.Report.Id,
                Network = "network-a", ReportId = "report-a", Version = 1, BatchId = "batch-a",
                EstimatedRevenueUsdNanos = 100, PreviousActualRevenueUsdNanos = 0,
                ActualRevenueUsdNanos = 125, ActualDeltaUsdNanos = 125, VarianceUsdNanos = 25,
                HistoricalRewardSoftUnits = 0, ReconciledAt = Now.AddMinutes(-1)
            });
            context.Add(new AdRewardPendingClaimRow
            {
                SessionId = fixture.Session.Id, TenantId = fixture.Session.TenantId,
                SourceStampId = Guid.NewGuid(), CompletionIdempotencyKeyHash = "completion-key",
                CompletionRequestHash = "completion-request", DeferredAt = Now.AddMinutes(-10),
                ProviderReportId = fixture.Report.Id, ConfirmedAt = Now.AddMinutes(-1)
            });
            context.Add(new AdRewardCompletionRow
            {
                SessionId = fixture.Session.Id, TenantId = fixture.Session.TenantId,
                UserId = fixture.Session.UserId, WalletId = fixture.Session.WalletId,
                Network = fixture.Session.Network, PolicyVersion = 1, IdempotencyKey = "completion-key",
                State = AdRewardCompletionState.PendingProviderReport, RewardSoftUnits = 0,
                EvidenceHashes = "[]", CompletedAt = Now.AddMinutes(-10), Version = 1
            });
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            return fixture;
        }

        public ConfirmDeferredAdRewardRequest Request() => new(
            Session.TenantId, Session.UserId, Session.Id, "subject", "br",
            new IdempotencyKey("confirm-deferred"), Guid.NewGuid(), "operation", Now);

        public DurableDeferredAdRewardService Service(
            AdRewardNetworkPolicySnapshot policy,
            Capabilities? capabilities = null,
            Issuance? issuance = null) => new(
            Context, new Policies(policy), capabilities ?? new Capabilities(), new Authority(), issuance ?? new Issuance());

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Database.DisposeAsync();
        }

        private static AdRewardSessionRow SessionRow() => new()
        {
            Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), UserId = Guid.NewGuid(), WalletId = Guid.NewGuid(),
            Network = "network-a", PolicyVersion = 1, CreativeId = "creative", DeviceRiskHash = "device",
            IpRiskHash = "ip", AsnRiskHash = "asn", NonceHash = "nonce", TokenHash = "token", TokenKeyId = "key",
            RequiredDurationTicks = TimeSpan.FromSeconds(30).Ticks, State = DurableAdRewardSessionState.Verified,
            StartIdempotencyKeyHash = Guid.NewGuid().ToString("N"), StartRequestHash = "request",
            IssuedAt = Now.AddHours(-1), ExpiresAt = Now.AddHours(1), UpdatedAt = Now.AddMinutes(-1), Version = 3
        };

        private static AdProviderReportRow ReportRow(Guid tenantId) => new()
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Network = "network-a", ReportId = "report-a",
            Version = 1, BatchId = "batch-a", PeriodStart = Now.AddHours(-1), PeriodEnd = Now.AddMinutes(-5),
            ActualRevenueUsdNanos = 125, VerifiedSessionIds = "[]", EvidenceHash = "report-evidence",
            ImportedAt = Now.AddMinutes(-2), Signature = "signature", PayloadHash = "payload",
            SignatureVerified = true, ReceivedAt = Now.AddMinutes(-1), ProcessedAt = Now.AddMinutes(-1)
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

    private sealed class Policies(AdRewardNetworkPolicySnapshot policy) : IDurableAdRewardPolicyReader
    {
        public ValueTask<AdRewardNetworkPolicySnapshot> GetEffectiveAsync(
            Guid tenantId, string network, DateTimeOffset at, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(policy with { TenantId = tenantId });
        public ValueTask<AdRewardNetworkPolicySnapshot> GetVersionAsync(
            Guid tenantId, string network, PolicyVersion version, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(policy with { TenantId = tenantId });
    }

    private sealed class Capabilities : IEconomyCapabilityAuthorizationService
    {
        public List<EconomyCapabilityEvaluationContext> Requests { get; } = [];
        public ValueTask<CapabilityAuthorizationReceipt> AuthorizeAndConsumeAsync(
            EconomyCapabilityEvaluationContext context, CancellationToken cancellationToken)
        {
            Requests.Add(context);
            return ValueTask.FromResult(new CapabilityAuthorizationReceipt(
                Guid.NewGuid(), context.TenantId, context.ActorId, context.SubjectReference,
                context.JurisdictionCode, context.Capability, context.OperationFingerprint,
                1, 1, context.RiskDecisionId, 1, context.ProviderHash, context.DestinationHash,
                context.SourceRootHashes, ["evidence"], context.EvaluatedAt, context.EvaluatedAt.AddMinutes(1),
                "receipt-hash", "key", "signature"));
        }
    }

    private sealed class Authority : IRegisteredPostingCapabilityResolver
    {
        public Task<RegisteredPostingCapability> ResolveAsync(
            string capabilityName, PostingTemplateKind templateKind,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RegisteredPostingCapability(Guid.NewGuid(), capabilityName, templateKind));
        public Task<RegisteredPostingAuthority> ResolveAuthorityAsync(
            string capabilityName, PostingTemplateKind templateKind, CapabilityAuthorizationReceipt receipt,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RegisteredPostingAuthority(
                Guid.NewGuid(), receipt.ActorId, receipt.TenantId, receipt.RiskDecisionId,
                receipt.OperationFingerprint, 1));
    }

    private sealed class Issuance(bool isDuplicate = false, bool wrongPostingId = false) : IAdRewardIssuanceGateway
    {
        public List<PersistedAdRewardIssuanceRequest> Requests { get; } = [];
        public RegisteredPostingReceipt Issue(PersistedAdRewardIssuanceRequest request)
        {
            Requests.Add(request);
            return new RegisteredPostingReceipt(
                wrongPostingId ? PostingId.New() : request.PostingId, 1, "journal", isDuplicate);
        }
    }

    private sealed class StubContext : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
