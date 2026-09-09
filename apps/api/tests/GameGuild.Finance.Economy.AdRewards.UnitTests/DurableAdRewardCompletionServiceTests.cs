using FluentAssertions;
using GameGuild.Finance.Economy.AdRewards.Persistence;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Identity.Context.Actors;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.AdRewards.UnitTests;

public sealed class DurableAdRewardCompletionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CompletionRequestExposesOnlyPlaybackIntent()
    {
        typeof(CompleteDurableAdRewardSessionRequest).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo([
                "Token", "Playback", "ProviderProof", "IdempotencyKey", "CompletedAt"
            ]);
    }

    [Fact]
    public async Task ImmediateCompletion_PersistsProofCapsPostingAttributionAndExactReplay()
    {
        await using var fixture = await Fixture.CreateAsync("ad_complete_immediate");
        var issuance = new Issuance();
        var service = fixture.Service(Policy(), issuance: issuance);
        var request = fixture.Request();

        var result = await service.CompleteAsync(request);

        result.State.Should().Be(AdRewardCompletionState.Issued);
        result.RewardSoftUnits.Should().Be(45);
        result.PostingId.Should().NotBeNull();
        result.OutputLotId.Should().NotBeNull();
        result.IsDuplicate.Should().BeFalse();
        issuance.Requests.Should().ContainSingle();
        issuance.Requests[0].SoftUnits.Should().Be(45);
        issuance.Requests[0].Network.Should().Be("network-a");
        (await fixture.Context.Set<AdRewardPlaybackMilestoneRow>().CountAsync()).Should().Be(5);
        (await fixture.Context.Set<AdRewardProviderProofInboxRow>().CountAsync()).Should().Be(1);
        (await fixture.Context.Set<AdRewardCapConsumptionRow>().CountAsync()).Should().Be(6);
        (await fixture.Context.Set<AdRewardBudgetConsumptionRow>().CountAsync()).Should().Be(1);
        (await fixture.Context.Set<AdRewardAttributionRow>().CountAsync()).Should().Be(1);
        (await fixture.Context.Set<AdRewardAccumulatorRow>().SingleAsync()).Version.Should().Be(1);
        var session = await fixture.Context.Set<AdRewardSessionRow>().SingleAsync();
        session.State.Should().Be(DurableAdRewardSessionState.Posted);
        session.Version.Should().Be(2);

        fixture.Context.ChangeTracker.Clear();
        var replay = await service.CompleteAsync(request);

        replay.Should().BeEquivalentTo(result with { IsDuplicate = true });
        issuance.Requests.Should().ContainSingle();
        await FluentActions.Awaiting(() => service.CompleteAsync(
                request with { IdempotencyKey = new IdempotencyKey("different") }).AsTask())
            .Should().ThrowAsync<AdRewardReplayException>();
    }

    [Fact]
    public async Task ImmediateCompletion_AccumulatesRemainderWithoutAuthorizingMovement()
    {
        await using var fixture = await Fixture.CreateAsync("ad_complete_remainder");
        var orchestrator = new ProtectedOperationOrchestrator(fixture.Claims);
        var issuance = new Issuance();
        var service = fixture.Service(Policy(ecpm: 1), orchestrator: orchestrator, issuance: issuance);

        var result = await service.CompleteAsync(fixture.Request());

        result.State.Should().Be(AdRewardCompletionState.AccumulatedRemainder);
        result.RewardSoftUnits.Should().Be(0);
        result.PostingId.Should().BeNull();
        orchestrator.Intents.Should().BeEmpty();
        issuance.Requests.Should().BeEmpty();
        (await fixture.Context.Set<AdRewardCapConsumptionRow>().CountAsync()).Should().Be(0);
        (await fixture.Context.Set<AdRewardAccumulatorRow>().SingleAsync()).RemainderNumerator
            .Should().NotBe("0");
        (await fixture.Context.Set<AdRewardAttributionRow>().SingleAsync()).RewardSoftUnits.Should().Be(0);
    }

    [Fact]
    public async Task ImmediateCompletion_UpdatesAnExistingAccumulatorAndPropagatesPostingReplay()
    {
        await using var fixture = await Fixture.CreateAsync("ad_complete_accumulator");
        fixture.Context.Add(new AdRewardAccumulatorRow
        {
            TenantId = fixture.Claims.TenantId, WalletId = fixture.Claims.WalletId.Value,
            Network = fixture.Claims.Network, PolicyVersion = 1, RemainderNumerator = "1",
            CanonicalDenominator = AdRewardRationalAccumulator.CanonicalDenominator.ToString(),
            Version = 1, UpdatedAt = Now.AddMinutes(-1)
        });
        await fixture.Context.SaveChangesAsync();
        var service = fixture.Service(Policy(), issuance: new Issuance(isDuplicate: true));

        var result = await service.CompleteAsync(fixture.Request());

        result.IsDuplicate.Should().BeTrue();
        (await fixture.Context.Set<AdRewardAccumulatorRow>().SingleAsync()).Version.Should().Be(2);
    }

    [Fact]
    public async Task DeferredCompletion_RecordsPendingClaimWithoutCapabilityOrPosting()
    {
        await using var fixture = await Fixture.CreateAsync("ad_complete_deferred");
        var orchestrator = new ProtectedOperationOrchestrator(fixture.Claims);
        var issuance = new Issuance();
        var service = fixture.Service(
            Policy(issuanceMode: AdRewardIssuanceMode.DeferredReport),
            orchestrator: orchestrator,
            issuance: issuance);
        var request = fixture.Request() with { ProviderProof = null };

        var result = await service.CompleteAsync(request);

        result.State.Should().Be(AdRewardCompletionState.PendingProviderReport);
        result.RewardSoftUnits.Should().Be(0);
        orchestrator.Intents.Should().BeEmpty();
        issuance.Requests.Should().BeEmpty();
        (await fixture.Context.Set<AdRewardPendingClaimRow>().SingleAsync()).ProviderReportId.Should().BeNull();
        (await fixture.Context.Set<AdRewardSessionRow>().SingleAsync()).State
            .Should().Be(DurableAdRewardSessionState.Deferred);
    }

    [Theory]
    [InlineData("not-effective")]
    [InlineData("stale")]
    [InlineData("uncertified")]
    [InlineData("disabled")]
    public async Task Completion_FailsClosedForUnavailablePolicyDependencies(string scenario)
    {
        await using var fixture = await Fixture.CreateAsync("ad_complete_policy");
        var policy = scenario switch
        {
            "not-effective" => Policy(effectiveAt: Now.AddMinutes(1)),
            "stale" => Policy(reportsCurrentThrough: Now.AddHours(-2)),
            "uncertified" => Policy(certified: false),
            _ => Policy(issuanceMode: AdRewardIssuanceMode.Disabled)
        };

        await FluentActions.Awaiting(() => fixture.Service(policy).CompleteAsync(fixture.Request()).AsTask())
            .Should().ThrowAsync<AdRewardDependencyUnavailableException>();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("invalid")]
    [InlineData("mismatch")]
    public async Task ImmediateCompletion_RequiresValidBoundProviderProof(string scenario)
    {
        await using var fixture = await Fixture.CreateAsync("ad_complete_proof");
        var adapter = new Adapter("network-a")
        {
            Verification = new AdRewardProviderProofVerification(
                scenario != "invalid", scenario == "mismatch" ? "other" : "evidence", "payload", Now)
        };
        var request = fixture.Request();
        if (scenario == "missing") request = request with { ProviderProof = null };

        Func<Task> action = () => fixture.Service(Policy(), adapter: adapter)
            .CompleteAsync(request).AsTask();

        if (scenario == "missing") await action.Should().ThrowAsync<AdProviderProofRequiredException>();
        else await action.Should().ThrowAsync<AdPlaybackVerificationException>();
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("actor")]
    public async Task Completion_RequiresTheTenantActorToOwnTheSignedSession(string scenario)
    {
        await using var fixture = await Fixture.CreateAsync("ad_complete_scope");
        fixture.ActorContexts.SetActorContext(Actor(
            scenario == "tenant" ? Guid.NewGuid() : fixture.Claims.TenantId,
            scenario == "actor" ? Guid.NewGuid() : fixture.Claims.UserId));

        await FluentActions.Awaiting(() => fixture.Service(Policy()).CompleteAsync(fixture.Request()).AsTask())
            .Should().ThrowAsync<AdRewardRiskBindingException>();
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("actor")]
    [InlineData("jurisdiction")]
    [InlineData("policy")]
    [InlineData("provider")]
    public async Task Completion_RejectsMismatchedProtectedOperationAuthorization(string scenario)
    {
        await using var fixture = await Fixture.CreateAsync("ad_complete_authorization");
        var orchestrator = new ProtectedOperationOrchestrator(fixture.Claims, scenario);

        await FluentActions.Awaiting(() => fixture.Service(Policy(), orchestrator: orchestrator)
                .CompleteAsync(fixture.Request()).AsTask())
            .Should().ThrowAsync<AdRewardRiskBindingException>();
    }

    [Fact]
    public async Task Completion_RejectsMissingSessionNonConsumableStateAndWrongDurableBinding()
    {
        await using var fixture = await Fixture.CreateAsync("ad_complete_binding");
        fixture.Context.Remove(await fixture.Context.Set<AdRewardSessionRow>().SingleAsync());
        await fixture.Context.SaveChangesAsync();
        await FluentActions.Awaiting(() => fixture.Service(Policy()).CompleteAsync(fixture.Request()).AsTask())
            .Should().ThrowAsync<AdRewardReplayException>();

        await fixture.SeedSessionAsync();
        var row = await fixture.Context.Set<AdRewardSessionRow>().SingleAsync();
        row.State = DurableAdRewardSessionState.Active;
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        await FluentActions.Awaiting(() => fixture.Service(Policy()).CompleteAsync(fixture.Request()).AsTask())
            .Should().ThrowAsync<AdRewardReplayException>();

        row = await fixture.Context.Set<AdRewardSessionRow>().SingleAsync();
        row.State = DurableAdRewardSessionState.Issued;
        row.CreativeId = "tampered";
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        await FluentActions.Awaiting(() => fixture.Service(Policy()).CompleteAsync(fixture.Request()).AsTask())
            .Should().ThrowAsync<AdRewardRiskBindingException>();
    }

    [Fact]
    public async Task CompletionRequiresAuthenticatedTenantActor()
    {
        await using var fixture = await Fixture.CreateAsync("ad_complete_auth");
        fixture.ActorContexts.SetActorContext(Actor(
            fixture.Claims.TenantId, fixture.Claims.UserId, authenticated: false));

        await FluentActions.Awaiting(() => fixture.Service(Policy()).CompleteAsync(fixture.Request()).AsTask())
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Issuance_RejectsAnUnexpectedPostingIdentity()
    {
        await using var fixture = await Fixture.CreateAsync("ad_complete_posting_id");
        var issuance = new Issuance(wrongPostingId: true);

        await FluentActions.Awaiting(() => fixture.Service(Policy(), issuance: issuance)
                .CompleteAsync(fixture.Request()).AsTask())
            .Should().ThrowAsync<RegisteredPostingRejectedException>();
    }

    [Fact]
    public async Task Issuance_RejectsAnExceededAtomicScopeCapWithExistingConsumptions()
    {
        await using var fixture = await Fixture.CreateAsync("ad_complete_scope_cap");
        var auxiliary = AuxiliarySession(fixture.Claims.TenantId);
        fixture.Context.Add(auxiliary);
        fixture.Context.AddRange(
            Consumption(fixture.Claims.TenantId, fixture.Claims.SessionId, AdRewardCapScope.User,
                KmsAdRewardSessionTokenProtector.HashOpaque(fixture.Claims.UserId.ToString("N")), 10),
            Consumption(fixture.Claims.TenantId, auxiliary.Id, AdRewardCapScope.User, "different-user", 10),
            Consumption(fixture.Claims.TenantId, fixture.Claims.SessionId, AdRewardCapScope.Device, "different-device", 10));
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        var budget = new AdRewardBudgetPolicy(54, 10_000, 10_000, 10_000,
            10_000_000_000, TimeSpan.FromDays(1));

        await FluentActions.Awaiting(() => fixture.Service(Policy(budget: budget))
                .CompleteAsync(fixture.Request()).AsTask())
            .Should().ThrowAsync<AdRewardBudgetExceededException>();
    }

    [Fact]
    public async Task Issuance_RejectsAnExceededFundedLossBudget()
    {
        await using var fixture = await Fixture.CreateAsync("ad_complete_loss_cap");
        var budget = new AdRewardBudgetPolicy(10_000, 10_000, 10_000, 10_000,
            1, TimeSpan.FromDays(1));

        await FluentActions.Awaiting(() => fixture.Service(Policy(budget: budget))
                .CompleteAsync(fixture.Request()).AsTask())
            .Should().ThrowAsync<AdRewardBudgetExceededException>();
    }

    [Fact]
    public async Task Completion_ValidatesTheRequestAndAllPlaybackInvariants()
    {
        await using var fixture = await Fixture.CreateAsync("ad_complete_validation");
        var service = fixture.Service(Policy());
        var request = fixture.Request();
        await FluentActions.Awaiting(() => service.CompleteAsync(null!).AsTask())
            .Should().ThrowAsync<ArgumentNullException>();
        await FluentActions.Awaiting(() => service.CompleteAsync(request with { Playback = null! }).AsTask())
            .Should().ThrowAsync<ArgumentNullException>();

        var evidence = request.Playback;
        var invalid = new[]
        {
            evidence with { StartedAt = fixture.Claims.IssuedAt.AddTicks(-1) },
            evidence with { CompletedAt = Now.AddTicks(1) },
            evidence with { CompletedAt = evidence.StartedAt.AddTicks(-1) },
            evidence with { PlaybackDuration = fixture.Claims.RequiredDuration - TimeSpan.FromTicks(1) },
            evidence with { VisibleDuration = TimeSpan.FromTicks(-1) },
            evidence with { VisibleDuration = evidence.PlaybackDuration + TimeSpan.FromTicks(1) },
            evidence with { FocusLoss = TimeSpan.FromTicks(-1) },
            evidence with { FocusLoss = TimeSpan.FromSeconds(3) },
            evidence with { VisibleDuration = TimeSpan.FromSeconds(1) },
            evidence with { Milestones = [0] },
            evidence with { Milestones = [1, 100] },
            evidence with { Milestones = [0, 99] },
            evidence with { Milestones = [0, 101, 100] },
            evidence with { Milestones = [0, 50, 50, 100] }
        };
        foreach (var playback in invalid)
        {
            await FluentActions.Awaiting(() => service.CompleteAsync(request with { Playback = playback }).AsTask())
                .Should().ThrowAsync<AdPlaybackVerificationException>();
        }
    }

    [Fact]
    public void Constructor_RejectsEveryMissingOrNonRelationalDependency()
    {
        var context = new StubContext();
        var policy = new Policies(Policy());
        var tokens = new Tokens(new Dictionary<string, DurableAdRewardSessionClaims>());
        var resolver = new AdRewardProviderAdapterResolver([new Adapter("network-a")]);
        var actorContexts = new TestActorContextAccessor();
        var jurisdictions = new JurisdictionResolver();
        var orchestrator = new ProtectedOperationOrchestrator(new DurableAdRewardSessionClaims(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), WalletId.New(), "network-a", "creative",
            "device", "ip", "asn", "nonce", TimeSpan.FromSeconds(30), new PolicyVersion(1),
            Now.AddMinutes(-2), Now.AddMinutes(3)));
        var authority = new Authority();
        var issuance = new Issuance();

        Action<IApplicationDbContext, IDurableAdRewardPolicyReader, IAdRewardSessionTokenProtector,
            IAdRewardProviderAdapterResolver, IActorContextAccessor, IEconomyJurisdictionResolver,
            IEconomyProtectedOperationOrchestrator, IRegisteredPostingCapabilityResolver,
            IAdRewardIssuanceGateway> create =
            (a, b, c, d, e, f, g, h, i) =>
                _ = new DurableAdRewardCompletionService(a, b, c, d, e, f, g, h, i);
        FluentActions.Invoking(() => create(null!, policy, tokens, resolver, actorContexts, jurisdictions, orchestrator, authority, issuance)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => create(context, null!, tokens, resolver, actorContexts, jurisdictions, orchestrator, authority, issuance)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => create(context, policy, null!, resolver, actorContexts, jurisdictions, orchestrator, authority, issuance)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => create(context, policy, tokens, null!, actorContexts, jurisdictions, orchestrator, authority, issuance)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => create(context, policy, tokens, resolver, null!, jurisdictions, orchestrator, authority, issuance)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => create(context, policy, tokens, resolver, actorContexts, null!, orchestrator, authority, issuance)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => create(context, policy, tokens, resolver, actorContexts, jurisdictions, null!, authority, issuance)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => create(context, policy, tokens, resolver, actorContexts, jurisdictions, orchestrator, null!, issuance)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => create(context, policy, tokens, resolver, actorContexts, jurisdictions, orchestrator, authority, null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => create(context, policy, tokens, resolver, actorContexts, jurisdictions, orchestrator, authority, issuance)).Should().Throw<InvalidOperationException>();
    }

    private static AdRewardNetworkPolicySnapshot Policy(
        long ecpm = 1_000_000_000,
        bool certified = true,
        AdRewardIssuanceMode issuanceMode = AdRewardIssuanceMode.ImmediateProviderProof,
        DateTimeOffset? effectiveAt = null,
        DateTimeOffset? reportsCurrentThrough = null,
        AdRewardBudgetPolicy? budget = null,
        long ipMaximum = 10_000,
        long asnMaximum = 10_000)
    {
        var policy = new AdNetworkPolicy(
            "network-a", new PolicyVersion(1), effectiveAt ?? Now.AddHours(-1), Now.AddHours(1),
            issuanceMode, AdNetworkYieldState.Trailing, ecpm, 500_000, 100_000, 800_000,
            TimeSpan.FromSeconds(2), 1_000, reportsCurrentThrough ?? Now,
            TimeSpan.FromHours(1), 1);
        return new AdRewardNetworkPolicySnapshot(
            Guid.Empty, policy, budget ?? new AdRewardBudgetPolicy(
                10_000, 10_000, 10_000, 10_000, 10_000_000_000, TimeSpan.FromDays(1)),
            ipMaximum, asnMaximum, "provider-hash", certified, "payload", "key", "signature");
    }

    private static ActorContext Actor(Guid tenantId, Guid actorId, bool authenticated = true) => new()
    {
        ActorKind = ActorKind.User,
        SubjectId = actorId.ToString(),
        TenantId = tenantId,
        Roles = new HashSet<string>(),
        Permissions = new HashSet<string>(),
        IsAuthenticated = authenticated
    };

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
            Claims = new DurableAdRewardSessionClaims(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), WalletId.New(), "network-a", "creative",
                "device", "ip", "asn", "nonce", TimeSpan.FromSeconds(30), new PolicyVersion(1),
                Now.AddMinutes(-2), Now.AddMinutes(3));
            Token = new SignedAdRewardSession("token-" + Claims.SessionId.ToString("N"));
            ActorContexts = new TestActorContextAccessor();
            ActorContexts.SetActorContext(Actor(Claims.TenantId, Claims.UserId));
        }

        private EconomyPostgreSqlTestDatabase Database { get; }
        public TestDbContext Context { get; }
        public DurableAdRewardSessionClaims Claims { get; }
        public SignedAdRewardSession Token { get; }
        public TestActorContextAccessor ActorContexts { get; }

        public static async Task<Fixture> CreateAsync(string prefix)
        {
            var database = await EconomyPostgreSqlTestDatabase.CreateAsync(prefix);
            var context = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>()
                .UseNpgsql(database.ConnectionString)
                .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
                .Options);
            await context.Database.EnsureCreatedAsync();
            var fixture = new Fixture(database, context);
            await fixture.SeedSessionAsync();
            return fixture;
        }

        public async Task SeedSessionAsync()
        {
            Context.Add(new AdRewardSessionRow
            {
                Id = Claims.SessionId, TenantId = Claims.TenantId, UserId = Claims.UserId,
                WalletId = Claims.WalletId.Value, Network = Claims.Network, PolicyVersion = Claims.PolicyVersion.Value,
                CreativeId = Claims.CreativeId, DeviceRiskHash = Claims.DeviceRiskHash,
                IpRiskHash = Claims.IpRiskHash, AsnRiskHash = Claims.AsnRiskHash,
                NonceHash = KmsAdRewardSessionTokenProtector.HashOpaque(Claims.Nonce),
                TokenHash = KmsAdRewardSessionTokenProtector.HashToken(Token.Value), TokenKeyId = "key",
                RequiredDurationTicks = Claims.RequiredDuration.Ticks, State = DurableAdRewardSessionState.Issued,
                StartIdempotencyKeyHash = Guid.NewGuid().ToString("N"), StartRequestHash = "request",
                IssuedAt = Claims.IssuedAt, ExpiresAt = Claims.ExpiresAt, UpdatedAt = Claims.IssuedAt, Version = 1
            });
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
        }

        public CompleteDurableAdRewardSessionRequest Request() => new(
            Token,
            new AdPlaybackEvidence(Now.AddSeconds(-30), Now, TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30), TimeSpan.Zero, [0, 25, 50, 75, 100]),
            new ProviderCompletionProof("network-a", "event-1", Claims.SessionId,
                Claims.CreativeId, Now, "evidence", "signature"),
            new IdempotencyKey("complete-session"), Now);

        public DurableAdRewardCompletionService Service(
            AdRewardNetworkPolicySnapshot policy,
            Adapter? adapter = null,
            ProtectedOperationOrchestrator? orchestrator = null,
            Issuance? issuance = null) => new(
            Context,
            new Policies(policy),
            new Tokens(new Dictionary<string, DurableAdRewardSessionClaims> { [Token.Value] = Claims }),
            new AdRewardProviderAdapterResolver([adapter ?? new Adapter("network-a")]),
            ActorContexts,
            new JurisdictionResolver(),
            orchestrator ?? new ProtectedOperationOrchestrator(Claims),
            new Authority(),
            issuance ?? new Issuance());

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Database.DisposeAsync();
        }
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

    private sealed class Tokens(IReadOnlyDictionary<string, DurableAdRewardSessionClaims> claims)
        : IAdRewardSessionTokenProtector
    {
        public ValueTask<SignedAdRewardSession> ProtectAsync(
            DurableAdRewardSessionClaims value, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public ValueTask<DurableAdRewardSessionClaims> UnprotectAsync(
            SignedAdRewardSession token, DateTimeOffset now, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(claims[token.Value]);
    }

    private sealed class Adapter(string network) : IAdRewardProviderAdapter
    {
        public string Network { get; } = network;
        public AdRewardProviderProofVerification Verification { get; set; } =
            new(true, "evidence", "payload", Now);
        public ValueTask<AdRewardProviderProofVerification> VerifyCompletionAsync(
            DurableAdRewardSessionClaims session, ProviderCompletionProof proof,
            DateTimeOffset receivedAt, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Verification);
        public ValueTask<bool> VerifyReportAsync(AdProviderReport report, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(true);
    }

    private sealed class TestActorContextAccessor : IActorContextAccessor
    {
        public ActorContext ActorContext { get; private set; } = ActorContext.Anonymous;
        public void SetActorContext(ActorContext context) => ActorContext = context;
        public void ClearActorContext() => ActorContext = ActorContext.Anonymous;
    }

    private sealed class JurisdictionResolver : IEconomyJurisdictionResolver
    {
        public ValueTask<EconomyJurisdictionResolution> ResolveAsync(
            Guid tenantId,
            Guid actorId,
            string? providerJurisdiction,
            string? destinationJurisdiction,
            DateTimeOffset evaluatedAt,
            CancellationToken cancellationToken) => ValueTask.FromResult(
            new EconomyJurisdictionResolution("BR", 1, 1, "jurisdiction-evidence"));
    }

    private sealed class ProtectedOperationOrchestrator(
        DurableAdRewardSessionClaims claims,
        string? mismatch = null) : IEconomyProtectedOperationOrchestrator
    {
        public List<EconomyProtectedOperationIntent> Intents { get; } = [];

        public async Task<TResult> ExecuteAsync<TResult>(
            EconomyProtectedOperationIntent intent,
            Func<EconomyProtectedOperationAuthorization, CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken)
        {
            Intents.Add(intent);
            var riskDecisionId = Guid.NewGuid();
            var fingerprint = $"server-{intent.IdempotencyKey.Value}";
            var tenantId = mismatch == "tenant" ? Guid.NewGuid() : claims.TenantId;
            var actorId = mismatch == "actor" ? Guid.NewGuid() : claims.UserId;
            var jurisdictionCode = mismatch == "jurisdiction" ? "US" : "BR";
            var policyVersion = mismatch == "policy" ? 2 : 1;
            var providerHash = mismatch == "provider" ? "other-provider" : intent.ProviderReferenceHash;
            var receipt = new CapabilityAuthorizationReceipt(
                Guid.NewGuid(), tenantId, actorId,
                EconomySubjectReference.ForUser(claims.TenantId, claims.UserId),
                jurisdictionCode, intent.Capability, fingerprint, policyVersion, 1, riskDecisionId, 1,
                providerHash, intent.DestinationHash,
                intent.SourceRoots.Select(root => root.Value.ToString("N")).ToArray(),
                ["evidence"], intent.RequestedAt, intent.RequestedAt.AddMinutes(1),
                "receipt-hash", "key", "signature");
            return await operation(new EconomyProtectedOperationAuthorization(
                tenantId, actorId, jurisdictionCode, riskDecisionId, fingerprint, receipt),
                cancellationToken);
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
