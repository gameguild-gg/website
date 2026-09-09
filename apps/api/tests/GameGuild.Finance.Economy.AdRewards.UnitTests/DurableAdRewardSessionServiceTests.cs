using FluentAssertions;
using GameGuild.Finance.Economy.AdRewards.Persistence;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.AdRewards.UnitTests;

public sealed class DurableAdRewardSessionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Start_PersistsSignedSessionAndRefreshesAnExactDuplicate()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("ad_session_start");
        await using var context = Context(database.ConnectionString);
        var tokens = new Tokens();
        var entropy = new Entropy();
        var service = Service(context, Policy(), tokens, entropy);
        var request = Request();

        var created = await service.StartAsync(request);

        created.IsDuplicate.Should().BeFalse();
        created.Claims.SessionId.Should().Be(entropy.SessionId);
        created.Claims.Nonce.Should().Be("nonce-1");
        created.Claims.ExpiresAt.Should().Be(Now.AddMinutes(5));
        created.Token.Value.Should().Contain(".key.");
        var row = await context.Set<AdRewardSessionRow>().SingleAsync();
        row.State.Should().Be(DurableAdRewardSessionState.Issued);
        row.Version.Should().Be(1);
        (await context.Set<AdRewardSessionEventRow>().SingleAsync()).State
            .Should().Be(DurableAdRewardSessionState.Issued);

        context.ChangeTracker.Clear();
        var duplicate = await service.StartAsync(request with { RequestedAt = Now.AddSeconds(1) });

        duplicate.IsDuplicate.Should().BeTrue();
        duplicate.Claims.SessionId.Should().Be(created.Claims.SessionId);
        duplicate.Claims.Nonce.Should().Be("nonce-2");
        duplicate.Token.Should().NotBe(created.Token);
        row = await context.Set<AdRewardSessionRow>().SingleAsync();
        row.Version.Should().Be(2);
        row.UpdatedAt.Should().Be(Now.AddSeconds(1));
        (await context.Set<AdRewardSessionEventRow>().CountAsync()).Should().Be(1);

        await FluentActions.Awaiting(() => service.StartAsync(
                request with { CreativeId = "different", RequestedAt = Now.AddSeconds(2) }).AsTask())
            .Should().ThrowAsync<AdRewardIdempotencyConflictException>();
    }

    [Fact]
    public async Task Start_UsesThePolicyExpiryWhenItPrecedesTheSessionLifetime()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("ad_session_expiry");
        await using var context = Context(database.ConnectionString);
        var policy = Policy(expiresAt: Now.AddMinutes(2));

        var result = await Service(context, policy).StartAsync(Request());

        result.Claims.ExpiresAt.Should().Be(Now.AddMinutes(2));
    }

    [Theory]
    [InlineData("uncertified")]
    [InlineData("disabled")]
    [InlineData("stale")]
    public async Task Start_FailsClosedWhenProviderPolicyOrReportsAreUnavailable(string scenario)
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("ad_session_closed");
        await using var context = Context(database.ConnectionString);
        var policy = Policy(
            certified: scenario != "uncertified",
            issuanceMode: scenario == "disabled" ? AdRewardIssuanceMode.Disabled : AdRewardIssuanceMode.ImmediateProviderProof,
            reportsCurrentThrough: scenario == "stale" ? Now.AddHours(-2) : Now);
        var service = Service(context, policy);

        Func<Task> action = () => service.StartAsync(Request()).AsTask();

        if (scenario == "stale")
            await action.Should().ThrowAsync<AdNetworkReportStaleException>();
        else
            await action.Should().ThrowAsync<AdRewardIssuanceDisabledException>();
        (await context.Set<AdRewardSessionRow>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Start_RequiresACertifiedConfiguredAdapter()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("ad_session_adapter");
        await using var context = Context(database.ConnectionString);
        var service = Service(context, Policy(), resolver: new AdRewardProviderAdapterResolver([]));

        await FluentActions.Awaiting(() => service.StartAsync(Request()).AsTask())
            .Should().ThrowAsync<AdRewardProviderUnavailableException>();
    }

    [Fact]
    public async Task Start_ValidatesEveryRequiredInputBeforeDatabaseWork()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("ad_session_validation");
        await using var context = Context(database.ConnectionString);
        var service = Service(context, Policy());
        var request = Request();
        var invalid = new StartDurableAdRewardSessionRequest?[]
        {
            null,
            request with { TenantId = Guid.Empty },
            request with { UserId = Guid.Empty },
            request with { WalletId = default },
            request with { Network = " " },
            request with { CreativeId = " " },
            request with { DeviceRiskHash = " " },
            request with { IpRiskHash = " " },
            request with { AsnRiskHash = " " },
            request with { RequiredDuration = TimeSpan.Zero }
        };

        foreach (var item in invalid)
        {
            await FluentActions.Awaiting(() => service.StartAsync(item!).AsTask())
                .Should().ThrowAsync<ArgumentException>();
        }
    }

    [Fact]
    public void Constructor_RejectsEveryMissingOrNonRelationalDependency()
    {
        var context = new StubContext();
        var policy = new Policies(Policy());
        var tokens = new Tokens();
        var entropy = new Entropy();
        var resolver = Resolver();

        FluentActions.Invoking(() => new DurableAdRewardSessionService(null!, policy, tokens, entropy, resolver))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new DurableAdRewardSessionService(context, null!, tokens, entropy, resolver))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new DurableAdRewardSessionService(context, policy, null!, entropy, resolver))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new DurableAdRewardSessionService(context, policy, tokens, null!, resolver))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new DurableAdRewardSessionService(context, policy, tokens, entropy, null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new DurableAdRewardSessionService(context, policy, tokens, entropy, resolver))
            .Should().Throw<InvalidOperationException>();
    }

    private static DurableAdRewardSessionService Service(
        TestDbContext context,
        AdRewardNetworkPolicySnapshot policy,
        Tokens? tokens = null,
        Entropy? entropy = null,
        IAdRewardProviderAdapterResolver? resolver = null) => new(
        context,
        new Policies(policy),
        tokens ?? new Tokens(),
        entropy ?? new Entropy(),
        resolver ?? Resolver());

    private static AdRewardProviderAdapterResolver Resolver() => new([new Adapter("network-a")]);

    private static StartDurableAdRewardSessionRequest Request() => new(
        Guid.NewGuid(), Guid.NewGuid(), WalletId.New(), " network-a ", " creative ",
        " device ", " ip ", " asn ", TimeSpan.FromSeconds(30),
        new IdempotencyKey("start-session"), Now);

    private static AdRewardNetworkPolicySnapshot Policy(
        bool certified = true,
        AdRewardIssuanceMode issuanceMode = AdRewardIssuanceMode.ImmediateProviderProof,
        DateTimeOffset? reportsCurrentThrough = null,
        DateTimeOffset? expiresAt = null)
    {
        var policy = new AdNetworkPolicy(
            "network-a", new PolicyVersion(1), Now.AddHours(-1), expiresAt ?? Now.AddHours(1),
            issuanceMode, AdNetworkYieldState.Trailing, 1_000_000_000, 500_000, 100_000,
            800_000, TimeSpan.FromSeconds(2), 1_000, reportsCurrentThrough ?? Now,
            TimeSpan.FromHours(1), 1);
        return new AdRewardNetworkPolicySnapshot(
            Guid.Empty, policy, new AdRewardBudgetPolicy(10_000, 10_000, 10_000, 10_000,
                10_000_000_000, TimeSpan.FromDays(1)),
            10_000, 10_000, "provider-hash", certified, "payload", "key", "signature");
    }

    private static TestDbContext Context(string connectionString) => new(
        new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options)
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

    private sealed class Policies(AdRewardNetworkPolicySnapshot policy) : IDurableAdRewardPolicyReader
    {
        public ValueTask<AdRewardNetworkPolicySnapshot> GetEffectiveAsync(
            Guid tenantId, string network, DateTimeOffset at, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(policy with { TenantId = tenantId });
        public ValueTask<AdRewardNetworkPolicySnapshot> GetVersionAsync(
            Guid tenantId, string network, PolicyVersion version, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(policy with { TenantId = tenantId });
    }

    private sealed class Tokens : IAdRewardSessionTokenProtector
    {
        public ValueTask<SignedAdRewardSession> ProtectAsync(
            DurableAdRewardSessionClaims claims, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new SignedAdRewardSession($"{claims.SessionId:N}-{claims.Nonce}.key.signature"));
        public ValueTask<DurableAdRewardSessionClaims> UnprotectAsync(
            SignedAdRewardSession token, DateTimeOffset now, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class Entropy : IAdRewardSessionEntropy
    {
        private int _nonce;
        public Guid SessionId { get; } = Guid.NewGuid();
        public Guid CreateSessionId() => SessionId;
        public string CreateNonce() => $"nonce-{++_nonce}";
    }

    private sealed class Adapter(string network) : IAdRewardProviderAdapter
    {
        public string Network { get; } = network;
        public ValueTask<AdRewardProviderProofVerification> VerifyCompletionAsync(
            DurableAdRewardSessionClaims session, ProviderCompletionProof proof,
            DateTimeOffset receivedAt, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new AdRewardProviderProofVerification(true, proof.EvidenceHash, "payload", receivedAt));
        public ValueTask<bool> VerifyReportAsync(AdProviderReport report, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(true);
    }
}
