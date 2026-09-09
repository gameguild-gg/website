using System.Text.Json;
using FluentAssertions;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using System.Reflection;

namespace GameGuild.Finance.Economy.UnitTests.Risk;

public sealed class PostgreSqlPolicyAndKillSwitchStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 16, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.Parse("70000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task PolicyPublicationIsCanonicalSignedMonotonicAndDualControlled()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("policy_store");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var signer = new StubSigner();
        var store = new PostgreSqlEconomyCapabilityPolicyStore(context, signer, new FixedTimeProvider(Now));
        using var payload = JsonDocument.Parse("{\"z\":2,\"a\":{\"y\":true,\"x\":1}}");
        var proposer = Guid.NewGuid();
        var approver = Guid.NewGuid();
        var proposal = new EconomyCapabilityPolicyProposal(
            Guid.NewGuid(), TenantId, EconomyValueMovementCapability.PayoutExecution, "BR", 1,
            payload.RootElement.Clone(), proposer, Now, Now.AddMinutes(1), Now.AddHours(1), true);

        var pending = await store.ProposeAsync(proposal, CancellationToken.None);

        pending.State.Should().Be(EconomyCapabilityPolicyState.PendingApproval);
        pending.CanonicalPayload.Should().Be("{\"a\":{\"x\":1,\"y\":true},\"z\":2}");
        await FluentActions.Awaiting(() => store.ApproveAsync(
                proposal.Id, proposer, "reauth-proposer", Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<InvalidOperationException>();
        var approved = await store.ApproveAsync(
            proposal.Id, approver, "reauth-approver", Now, CancellationToken.None);

        approved.State.Should().Be(EconomyCapabilityPolicyState.Approved);
        approved.KeyId.Should().Be("kms-policy-key");
        signer.LastPayload.Should().Be(approved.CanonicalPayload);
        (await store.ActivateDueAsync(Now.AddMinutes(1), CancellationToken.None)).Should().Be(1);
        (await store.CurrentAsync(TenantId, EconomyValueMovementCapability.PayoutExecution, "BR", CancellationToken.None))!
            .State.Should().Be(EconomyCapabilityPolicyState.Active);

        var overlap = proposal with
        {
            Id = Guid.NewGuid(), Version = 2, ProposedBy = Guid.NewGuid(), ProposedAt = Now.AddMinutes(2),
            EffectiveAt = Now.AddMinutes(30), ExpiresAt = Now.AddHours(2)
        };
        await FluentActions.Awaiting(() => store.ProposeAsync(overlap, CancellationToken.None).AsTask())
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task KillSwitchActivatesImmediatelyAndReleaseRequiresProposalTwoAdminsAndReadiness()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("kill_switch_store");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var readiness = new StubReleaseReadiness();
        var store = new PostgreSqlEconomyKillSwitchStore(context, readiness);
        var scope = EconomyKillSwitchScope.ForCapability(TenantId, EconomyValueMovementCapability.PayoutExecution);
        var activationId = Guid.NewGuid();
        var activationActor = Guid.NewGuid();

        var active = await store.ActivateAsync(
            activationId, scope, "ledger mismatch", activationActor, Now, CancellationToken.None);
        var replay = await store.ActivateAsync(
            activationId, scope, "ledger mismatch", activationActor, Now, CancellationToken.None);

        active.Epoch.Should().Be(1);
        active.IsActive.Should().BeTrue();
        replay.Should().Be(active);
        await FluentActions.Awaiting(() => store.ActivateAsync(
                activationId, scope, "different reason", activationActor, Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<RiskDecisionReuseException>();

        var releaseProposer = Guid.NewGuid();
        await store.ProposeReleaseAsync(active.Id, releaseProposer, "reauth-release", Now.AddMinutes(1), CancellationToken.None);
        await FluentActions.Awaiting(() => store.ApproveReleaseAsync(
                active.Id, releaseProposer, "reauth-self", Now.AddMinutes(2), CancellationToken.None).AsTask())
            .Should().ThrowAsync<InvalidOperationException>();
        var reviewerOne = Guid.NewGuid();
        var reviewerTwo = Guid.NewGuid();
        (await store.ApproveReleaseAsync(
            active.Id, reviewerOne, "reauth-one", Now.AddMinutes(2), CancellationToken.None)).IsActive.Should().BeTrue();
        (await store.ApproveReleaseAsync(
            active.Id, reviewerTwo, "reauth-two", Now.AddMinutes(3), CancellationToken.None)).IsActive.Should().BeTrue();

        readiness.IsReady = true;
        var released = await store.TryReleaseAsync(active.Id, Now.AddMinutes(4), CancellationToken.None);
        released.IsActive.Should().BeFalse();
        readiness.Calls.Should().BeGreaterThan(0);
        var second = await store.ActivateAsync(
            Guid.NewGuid(), scope, "provider incident", Guid.NewGuid(), Now.AddMinutes(5), CancellationToken.None);
        second.Epoch.Should().Be(2);
    }

    [Fact]
    public async Task TransactionalStoresSupportTheRuntimeRetryingExecutionStrategy()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("policy_store_retry");
        await using var context = CreateContext(database.ConnectionString, enableRetryOnFailure: true);
        await context.Database.EnsureCreatedAsync();
        var policyStore = new PostgreSqlEconomyCapabilityPolicyStore(
            context, new StubSigner(), new FixedTimeProvider(Now));
        using var payload = JsonDocument.Parse("{\"enabled\":true}");
        var proposal = new EconomyCapabilityPolicyProposal(
            Guid.NewGuid(), TenantId, EconomyValueMovementCapability.PayoutExecution, "BR", 1,
            payload.RootElement.Clone(), Guid.NewGuid(), Now, Now.AddMinutes(1), Now.AddHours(1), true);

        await policyStore.ProposeAsync(proposal, CancellationToken.None);
        await policyStore.ApproveAsync(
            proposal.Id, Guid.NewGuid(), "reauth-policy", Now, CancellationToken.None);
        (await policyStore.ActivateDueAsync(Now.AddMinutes(1), CancellationToken.None)).Should().Be(1);

        var readiness = new StubReleaseReadiness { IsReady = true };
        var killSwitchStore = new PostgreSqlEconomyKillSwitchStore(context, readiness);
        var state = await killSwitchStore.ActivateAsync(
            Guid.NewGuid(), EconomyKillSwitchScope.ForCapability(
                TenantId, EconomyValueMovementCapability.PayoutExecution),
            "runtime retry smoke test", Guid.NewGuid(), Now.AddMinutes(2), CancellationToken.None);
        await killSwitchStore.ProposeReleaseAsync(
            state.Id, Guid.NewGuid(), "reauth-release", Now.AddMinutes(3), CancellationToken.None);
        await killSwitchStore.ApproveReleaseAsync(
            state.Id, Guid.NewGuid(), "reauth-first", Now.AddMinutes(4), CancellationToken.None);
        await killSwitchStore.ApproveReleaseAsync(
            state.Id, Guid.NewGuid(), "reauth-second", Now.AddMinutes(5), CancellationToken.None);

        (await killSwitchStore.TryReleaseAsync(
            state.Id, Now.AddMinutes(6), CancellationToken.None)).IsActive.Should().BeFalse();
    }

    [Fact]
    public void CanonicalJsonRejectsDuplicatePropertiesAndNormalizesPropertyOrder()
    {
        using var first = JsonDocument.Parse("{\"b\":[2,1],\"a\":1.00}");
        using var second = JsonDocument.Parse("{\"a\":1.00,\"b\":[2,1]}");
        EconomyCanonicalJson.Serialize(first.RootElement).Should().Be(EconomyCanonicalJson.Serialize(second.RootElement));

        using var duplicate = JsonDocument.Parse("{\"a\":1,\"a\":2}");
        FluentActions.Invoking(() => EconomyCanonicalJson.Serialize(duplicate.RootElement))
            .Should().Throw<ArgumentException>();

        using var everyScalar = JsonDocument.Parse("[\"value\",1e100,true,false,null]");
        EconomyCanonicalJson.Serialize(everyScalar.RootElement)
            .Should().Be("[\"value\",1E+100,true,false,null]");
        FluentActions.Invoking(() => EconomyCanonicalJson.Serialize(default))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PolicyProposalValidationScopeAndStateMappingCoverEveryBoundary()
    {
        using var payload = JsonDocument.Parse("{}");
        var valid = new EconomyCapabilityPolicyProposal(
            Guid.NewGuid(), TenantId, EconomyValueMovementCapability.PayoutExecution, "BR", 1,
            payload.RootElement.Clone(), Guid.NewGuid(), Now, Now, Now.AddHours(1), true);
        var validate = typeof(PostgreSqlEconomyCapabilityPolicyStore)
            .GetMethod("ValidateProposal", BindingFlags.NonPublic | BindingFlags.Static)!;
        void Validates(EconomyCapabilityPolicyProposal proposal) => validate.Invoke(null, [proposal]);
        void Rejects(EconomyCapabilityPolicyProposal? proposal, Type exceptionType)
        {
            var exception = FluentActions.Invoking(() => validate.Invoke(null, [proposal!]))
                .Should().Throw<TargetInvocationException>().Which.InnerException;
            exception.Should().NotBeNull();
            exception!.GetType().Should().Be(exceptionType);
        }

        Validates(valid);
        Rejects(null, typeof(ArgumentNullException));
        Rejects(valid with { Id = Guid.Empty }, typeof(ArgumentException));
        Rejects(valid with { Capability = (EconomyValueMovementCapability)999 }, typeof(ArgumentOutOfRangeException));
        Rejects(valid with { JurisdictionCode = " " }, typeof(ArgumentException));
        Rejects(valid with { JurisdictionCode = "*" }, typeof(ArgumentException));
        Rejects(valid with { JurisdictionCode = "ALL" }, typeof(ArgumentException));
        Rejects(valid with { Version = 0 }, typeof(ArgumentOutOfRangeException));
        Rejects(valid with { ProposedBy = Guid.Empty }, typeof(ArgumentException));
        Rejects(valid with { EffectiveAt = Now.AddTicks(-1) }, typeof(ArgumentException));
        Rejects(valid with { ExpiresAt = Now }, typeof(ArgumentException));

        var scopeKey = typeof(PostgreSqlEconomyCapabilityPolicyStore)
            .GetMethod("ScopeKey", BindingFlags.NonPublic | BindingFlags.Static)!;
        ((string)scopeKey.Invoke(null,
            [TenantId, EconomyValueMovementCapability.PayoutExecution, "BR"])!).Should().StartWith(TenantId.ToString("N"));
        ((string)scopeKey.Invoke(null,
            [null, EconomyValueMovementCapability.PayoutExecution, "BR"])!).Should().StartWith("global:");

        var map = typeof(PostgreSqlEconomyCapabilityPolicyStore)
            .GetMethod("Map", BindingFlags.NonPublic | BindingFlags.Static)!;
        var row = new EconomyCapabilityPolicyRow
        {
            Id = Guid.NewGuid(), ScopeKey = "scope", Capability = EconomyValueMovementCapability.PayoutExecution,
            JurisdictionCode = "BR", Version = 1, CanonicalPayload = "{}", PayloadHash = "hash",
            KeyId = "key", Signature = "signature", RequestHash = "request", ProposedBy = Guid.NewGuid(),
            ProposedAt = Now, EffectiveAt = Now, ExpiresAt = Now.AddHours(1)
        };
        EconomyCapabilityPolicy Map() => (EconomyCapabilityPolicy)map.Invoke(null, [row, Now])!;
        Map().State.Should().Be(EconomyCapabilityPolicyState.PendingApproval);
        row.ApprovedBy = Guid.NewGuid();
        Map().State.Should().Be(EconomyCapabilityPolicyState.Approved);
        row.IsActive = true;
        Map().State.Should().Be(EconomyCapabilityPolicyState.Active);
        row.ExpiresAt = Now;
        Map().State.Should().Be(EconomyCapabilityPolicyState.Expired);
    }

    [Fact]
    public async Task KillSwitchScopesAndCommandsRejectEveryInvalidAuthorityShapeBeforePersistence()
    {
        FluentActions.Invoking(() => EconomyKillSwitchScope.ForTenant(Guid.Empty))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => EconomyKillSwitchScope.ForCapability(
                Guid.Empty, EconomyValueMovementCapability.PayoutExecution))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => EconomyKillSwitchScope.ForCapability(
                TenantId, (EconomyValueMovementCapability)999))
            .Should().Throw<ArgumentOutOfRangeException>();
        EconomyKillSwitchScope.ForCapability(
                TenantId, EconomyValueMovementCapability.PayoutExecution)
            .Capability.Should().Be(EconomyValueMovementCapability.PayoutExecution);

        await using var context = CreateContext("Host=localhost;Database=unused;Username=unused;Password=unused");
        var store = new PostgreSqlEconomyKillSwitchStore(context, new StubReleaseReadiness());
        var actorId = Guid.NewGuid();
        var validScope = EconomyKillSwitchScope.ForTenant(TenantId);
        await FluentActions.Invoking(() => store.ActivateAsync(
                Guid.Empty, validScope, "reason", actorId, Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => store.ActivateAsync(
                Guid.NewGuid(), null!, "reason", actorId, Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentNullException>();
        await FluentActions.Invoking(() => store.ActivateAsync(
                Guid.NewGuid(), new EconomyKillSwitchScope("scope", TenantId, (EconomyValueMovementCapability)999),
                "reason", actorId, Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Invoking(() => store.ActivateAsync(
                Guid.NewGuid(), new EconomyKillSwitchScope("scope", null, EconomyValueMovementCapability.PayoutExecution),
                "reason", actorId, Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => store.ActivateAsync(
                Guid.NewGuid(), validScope, " ", actorId, Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => store.ActivateAsync(
                Guid.NewGuid(), validScope, "reason", Guid.Empty, Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => store.ProposeReleaseAsync(
                Guid.Empty, actorId, "reauth", Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => store.ProposeReleaseAsync(
                Guid.NewGuid(), Guid.Empty, "reauth", Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => store.ProposeReleaseAsync(
                Guid.NewGuid(), actorId, " ", Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => store.TryReleaseAsync(
                Guid.Empty, Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void ConstructorsRequireRelationalContextAndDependencies()
    {
        var context = new StubApplicationDbContext();
        FluentActions.Invoking(() => new PostgreSqlEconomyCapabilityPolicyStore(
                context, new StubSigner(), new FixedTimeProvider(Now)))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => new PostgreSqlEconomyKillSwitchStore(context, new StubReleaseReadiness()))
            .Should().Throw<InvalidOperationException>();
        using var relational = CreateContext("Host=localhost;Database=unused;Username=unused;Password=unused");
        FluentActions.Invoking(() => new PostgreSqlEconomyCapabilityPolicyStore(
                relational, null!, new FixedTimeProvider(Now)))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlEconomyCapabilityPolicyStore(
                relational, new StubSigner(), null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlEconomyKillSwitchStore(relational, null!))
            .Should().Throw<ArgumentNullException>();
    }

    private static PolicyDbContext CreateContext(
        string connectionString,
        bool enableRetryOnFailure = false) => new(
        new DbContextOptionsBuilder<PolicyDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                if (enableRetryOnFailure) npgsql.EnableRetryOnFailure();
            })
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);

    private sealed class PolicyDbContext(DbContextOptions<PolicyDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            new EconomyModelConfiguration().Configure(modelBuilder);

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Database.BeginTransactionAsync(cancellationToken);
    }

    private sealed class StubSigner : ICapabilityPolicySigner
    {
        public string? LastPayload { get; private set; }

        public ValueTask<CapabilityReceiptSignature> SignAsync(string canonicalPayload, CancellationToken cancellationToken)
        {
            LastPayload = canonicalPayload;
            return ValueTask.FromResult(new CapabilityReceiptSignature("kms-policy-key", "asymmetric-signature"));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StubReleaseReadiness : IKillSwitchReleaseReadinessGate
    {
        public bool IsReady { get; set; }
        public int Calls { get; private set; }

        public ValueTask<bool> IsReadyAsync(EconomyKillSwitchScope scope, CancellationToken cancellationToken)
        {
            Calls++;
            return ValueTask.FromResult(IsReady);
        }
    }

    private sealed class StubApplicationDbContext : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
