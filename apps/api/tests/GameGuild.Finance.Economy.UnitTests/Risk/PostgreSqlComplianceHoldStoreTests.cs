using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.UnitTests.Risk;

public sealed class PostgreSqlComplianceHoldStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ActivationReleaseAndScopeMatchingAreDurableAndIdempotent()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("compliance_holds");
        await using var context = CreateContext(database.ConnectionString);
        var store = new PostgreSqlComplianceHoldStore(context);
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var activation = Activation(tenant, actor);

        var created = await store.ActivateAsync(activation, CancellationToken.None);
        var replay = await store.ActivateAsync(activation, CancellationToken.None);

        created.Should().Be(replay);
        created.Scope.Key.Should().Be($"{tenant:N}:subject-hash:all");
        created.IsActive(Now).Should().BeTrue();
        created.IsActive(Now.AddHours(-2)).Should().BeFalse();
        created.IsActive(Now.AddHours(3)).Should().BeFalse();
        (await store.IsActiveAsync(
            new ComplianceHoldScope(tenant, "subject-hash", EconomyValueMovementCapability.PayoutExecution),
            Now, CancellationToken.None)).Should().BeTrue("a global subject hold covers every capability");
        (await store.IsActiveAsync(
            new ComplianceHoldScope(Guid.NewGuid(), "subject-hash", EconomyValueMovementCapability.PayoutExecution),
            Now, CancellationToken.None)).Should().BeFalse();

        var released = await store.ReleaseAsync(created.Id, actor, " release-evidence ", Now.AddMinutes(1), CancellationToken.None);
        var releaseReplay = await store.ReleaseAsync(created.Id, actor, "release-evidence", Now.AddMinutes(2), CancellationToken.None);
        released.Should().Be(releaseReplay);
        released.IsActive(Now.AddMinutes(1)).Should().BeFalse();
        (await store.IsActiveAsync(activation.Scope, Now.AddMinutes(1), CancellationToken.None)).Should().BeFalse();
        (await context.Set<EconomyComplianceHoldEventRow>().CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task ConflictingReplaysOverlappingHoldsAndInvalidReleasesFailClosed()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("compliance_hold_conflicts");
        await using var context = CreateContext(database.ConnectionString);
        var store = new PostgreSqlComplianceHoldStore(context);
        var activation = Activation(Guid.NewGuid(), Guid.NewGuid());
        await store.ActivateAsync(activation, CancellationToken.None);

        await FluentActions.Awaiting(() => store.ActivateAsync(
                activation with { ReasonCode = "different" }, CancellationToken.None).AsTask())
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*different inputs*");
        await FluentActions.Awaiting(() => store.ActivateAsync(
                activation with { Id = Guid.NewGuid(), IdempotencyKey = "other-key" }, CancellationToken.None).AsTask())
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*already exists*");
        await FluentActions.Awaiting(() => store.ReleaseAsync(
                Guid.NewGuid(), activation.ActorId, "evidence", Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<KeyNotFoundException>();
        await FluentActions.Awaiting(() => store.ReleaseAsync(
                activation.Id, activation.ActorId, "evidence", Now.AddHours(-2), CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>().WithParameterName("releasedAt");

        await store.ReleaseAsync(activation.Id, activation.ActorId, "evidence", Now, CancellationToken.None);
        await FluentActions.Awaiting(() => store.ReleaseAsync(
                activation.Id, Guid.NewGuid(), "evidence", Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*different inputs*");
        await FluentActions.Awaiting(() => store.ReleaseAsync(
                activation.Id, activation.ActorId, "different", Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*different inputs*");
    }

    [Fact]
    public async Task CapabilityScopedHoldMatchesOnlyItsCapability()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("compliance_capability_hold");
        await using var context = CreateContext(database.ConnectionString);
        var store = new PostgreSqlComplianceHoldStore(context);
        var tenant = Guid.NewGuid();
        var scope = new ComplianceHoldScope(tenant, "subject", EconomyValueMovementCapability.PayoutExecution);
        await store.ActivateAsync(
            Activation(tenant, Guid.NewGuid()) with { Scope = scope }, CancellationToken.None);

        scope.Key.Should().EndWith($":{(int)EconomyValueMovementCapability.PayoutExecution}");
        (await store.IsActiveAsync(scope, Now, CancellationToken.None)).Should().BeTrue();
        (await store.IsActiveAsync(
            scope with { Capability = EconomyValueMovementCapability.MarketplaceSettlement },
            Now, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task AdministrativeReleaseIsTenantScopedPolicyBoundAndDualControlled()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("compliance_hold_admin");
        await using var context = CreateContext(database.ConnectionString);
        var tenant = Guid.NewGuid();
        var activator = Guid.NewGuid();
        var proposer = Guid.NewGuid();
        var firstApprover = Guid.NewGuid();
        var secondApprover = Guid.NewGuid();
        var holdStore = new PostgreSqlComplianceHoldStore(context);
        var hold = await holdStore.ActivateAsync(
            Activation(tenant, activator),
            CancellationToken.None);
        var policy = new StubReleasePolicy(
            new ComplianceHoldReleasePolicyAuthorization(2, "signed-policy-evidence"));
        var administration = new PostgreSqlComplianceHoldAdministrationStore(context, policy);

        var page = await administration.ListAsync(
            tenant,
            true,
            EconomyValueMovementCapability.PayoutExecution,
            1,
            null,
            Now,
            CancellationToken.None);
        page.Items.Should().ContainSingle();
        page.Items[0].Hold.Id.Should().Be(hold.Id);
        page.Items[0].ReleaseProposedBy.Should().BeNull();
        page.NextCursor.Should().BeNull();
        await FluentActions.Awaiting(() => administration.CurrentAsync(
                Guid.NewGuid(), hold.Id, CancellationToken.None).AsTask())
            .Should().ThrowAsync<KeyNotFoundException>();

        var proposed = await administration.ProposeReleaseAsync(
            tenant,
            hold.Id,
            proposer,
            "proposal-step-up",
            Now,
            CancellationToken.None);
        proposed.RequiredReleaseApprovals.Should().Be(2);
        proposed.ReleasePolicyEvidenceHash.Should().Be("signed-policy-evidence");
        proposed.ReleaseProposedBy.Should().Be(proposer);
        policy.LastTenantId.Should().Be(tenant);
        policy.LastCapability.Should().BeNull();

        var firstApproval = await administration.ApproveReleaseAsync(
            tenant,
            hold.Id,
            firstApprover,
            "approval-one-step-up",
            Now.AddMinutes(1),
            CancellationToken.None);
        firstApproval.Hold.IsActive(Now.AddMinutes(1)).Should().BeTrue();
        firstApproval.ReleaseApprovers.Should().Equal(firstApprover);

        var released = await administration.ApproveReleaseAsync(
            tenant,
            hold.Id,
            secondApprover,
            "approval-two-step-up",
            Now.AddMinutes(2),
            CancellationToken.None);
        released.Hold.IsActive(Now.AddMinutes(2)).Should().BeFalse();
        released.Hold.ReleasedBy.Should().Be(secondApprover);
        released.ReleaseApprovers.Should().Equal(firstApprover, secondApprover);
        var audit = await administration.EventsAsync(tenant, hold.Id, CancellationToken.None);
        audit.Select(item => item.Sequence).Should().Equal(1, 2, 3, 4, 5);
        audit.Select(item => item.Kind).Should().Equal(
            ComplianceHoldEventKinds.Activated,
            ComplianceHoldEventKinds.ReleaseProposed,
            ComplianceHoldEventKinds.ReleaseApproved,
            ComplianceHoldEventKinds.ReleaseApproved,
            ComplianceHoldEventKinds.Released);
    }

    [Fact]
    public async Task AdministrativeReleaseRejectsActorReuseAndInvalidPolicyAuthorization()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("compliance_hold_admin_failures");
        await using var context = CreateContext(database.ConnectionString);
        var tenant = Guid.NewGuid();
        var activator = Guid.NewGuid();
        var proposer = Guid.NewGuid();
        var approver = Guid.NewGuid();
        var holdStore = new PostgreSqlComplianceHoldStore(context);
        var hold = await holdStore.ActivateAsync(Activation(tenant, activator), CancellationToken.None);
        var invalidPolicy = new StubReleasePolicy(
            new ComplianceHoldReleasePolicyAuthorization(0, "invalid"));
        var administration = new PostgreSqlComplianceHoldAdministrationStore(context, invalidPolicy);

        await FluentActions.Awaiting(() => administration.ProposeReleaseAsync(
                tenant, hold.Id, activator, "step-up", Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*activator*");
        await FluentActions.Awaiting(() => administration.ProposeReleaseAsync(
                tenant, hold.Id, proposer, "step-up", Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*invalid authorization*");

        var validAdministration = new PostgreSqlComplianceHoldAdministrationStore(
            context,
            new StubReleasePolicy(new ComplianceHoldReleasePolicyAuthorization(2, "policy")));
        await FluentActions.Awaiting(() => validAdministration.ApproveReleaseAsync(
                tenant, hold.Id, approver, "step-up", Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*must be proposed*");
        await validAdministration.ProposeReleaseAsync(
            tenant, hold.Id, proposer, "step-up", Now, CancellationToken.None);
        await FluentActions.Awaiting(() => validAdministration.ProposeReleaseAsync(
                tenant, hold.Id, Guid.NewGuid(), "step-up", Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*already been proposed*");
        await FluentActions.Awaiting(() => validAdministration.ApproveReleaseAsync(
                tenant, hold.Id, proposer, "step-up", Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*must be distinct*");
        await validAdministration.ApproveReleaseAsync(
            tenant, hold.Id, approver, "step-up", Now.AddMinutes(1), CancellationToken.None);
        await FluentActions.Awaiting(() => validAdministration.ApproveReleaseAsync(
                tenant, hold.Id, approver, "step-up", Now.AddMinutes(2), CancellationToken.None).AsTask())
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*approve twice*");
    }

    [Fact]
    public async Task ReleasePolicyResolverUsesTheStrongestCurrentSignedPolicy()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("compliance_hold_policy");
        await using var context = CreateContext(database.ConnectionString);
        var tenant = Guid.NewGuid();
        context.Set<EconomyCapabilityPolicyRow>().AddRange(
            Policy(Guid.NewGuid(), null, EconomyValueMovementCapability.PayoutExecution, 1),
            Policy(Guid.NewGuid(), tenant, EconomyValueMovementCapability.PayoutExecution, 2),
            Policy(Guid.NewGuid(), tenant, EconomyValueMovementCapability.Transfer, 1));
        await context.SaveChangesAsync();
        var verifier = new StubSignatureVerifier(true);
        var resolver = new PostgreSqlComplianceHoldReleasePolicyResolver(context, verifier);

        var result = await resolver.ResolveAsync(
            tenant,
            EconomyValueMovementCapability.PayoutExecution,
            Now,
            CancellationToken.None);

        result.RequiredApprovals.Should().Be(2);
        result.EvidenceHash.Should().HaveLength(64);
        verifier.VerifiedPayloads.Should().HaveCount(2);
        var globalResult = await resolver.ResolveAsync(
            tenant,
            null,
            Now,
            CancellationToken.None);
        globalResult.RequiredApprovals.Should().Be(2);
        verifier.VerifiedPayloads.Should().HaveCount(5);
        await FluentActions.Awaiting(() => resolver.ResolveAsync(
                Guid.NewGuid(),
                EconomyValueMovementCapability.MarketplaceSettlement,
                Now,
                CancellationToken.None).AsTask())
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*No active signed policy*");
    }

    [Fact]
    public async Task ReleasePolicyResolverRejectsUnsignedOrInvalidlySignedPolicies()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("compliance_hold_policy_invalid");
        await using var context = CreateContext(database.ConnectionString);
        var tenant = Guid.NewGuid();
        var policy = Policy(Guid.NewGuid(), tenant, EconomyValueMovementCapability.Transfer, 1);
        policy.KeyId = " ";
        context.Set<EconomyCapabilityPolicyRow>().Add(policy);
        await context.SaveChangesAsync();

        var accepting = new PostgreSqlComplianceHoldReleasePolicyResolver(
            context,
            new StubSignatureVerifier(true));
        await FluentActions.Awaiting(() => accepting.ResolveAsync(
                tenant, EconomyValueMovementCapability.Transfer, Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*invalid signature*");

        policy.KeyId = "key";
        policy.Signature = " ";
        await context.SaveChangesAsync();
        await FluentActions.Awaiting(() => accepting.ResolveAsync(
                tenant, EconomyValueMovementCapability.Transfer, Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*invalid signature*");

        policy.Signature = "signature";
        await context.SaveChangesAsync();
        var rejecting = new PostgreSqlComplianceHoldReleasePolicyResolver(
            context,
            new StubSignatureVerifier(false));
        await FluentActions.Awaiting(() => rejecting.ResolveAsync(
                tenant, EconomyValueMovementCapability.Transfer, Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*invalid signature*");
    }

    [Fact]
    public async Task AdministrationPaginationStatusFiltersAndCursorsAreStable()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("compliance_hold_admin_page");
        await using var context = CreateContext(database.ConnectionString);
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var store = new PostgreSqlComplianceHoldStore(context);
        var firstActivation = Activation(tenant, actor) with
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000001")
        };
        firstActivation = firstActivation with
        {
            Scope = firstActivation.Scope with { SubjectHash = "subject-one" },
            IdempotencyKey = "page-one"
        };
        var secondActivation = firstActivation with
        {
            Id = Guid.Parse("20000000-0000-0000-0000-000000000001"),
            Scope = firstActivation.Scope with
            {
                SubjectHash = "subject-two",
                Capability = EconomyValueMovementCapability.Transfer
            },
            IdempotencyKey = "page-two"
        };
        await store.ActivateAsync(firstActivation, CancellationToken.None);
        await store.ActivateAsync(secondActivation, CancellationToken.None);
        await store.ReleaseAsync(
            firstActivation.Id,
            actor,
            "system-evidence",
            Now,
            CancellationToken.None);
        var administration = new PostgreSqlComplianceHoldAdministrationStore(
            context,
            new StubReleasePolicy(new ComplianceHoldReleasePolicyAuthorization(1, "policy")));

        var firstPage = await administration.ListAsync(
            tenant, null, null, 1, null, Now, CancellationToken.None);
        firstPage.Items.Should().ContainSingle();
        firstPage.NextCursor.Should().NotBeNull();
        var secondPage = await administration.ListAsync(
            tenant, null, null, 1, firstPage.NextCursor, Now, CancellationToken.None);
        secondPage.Items.Should().ContainSingle();
        secondPage.Items[0].Hold.Id.Should().NotBe(firstPage.Items[0].Hold.Id);
        secondPage.NextCursor.Should().BeNull();

        var inactive = await administration.ListAsync(
            tenant, false, null, 100, null, Now, CancellationToken.None);
        inactive.Items.Select(item => item.Hold.Id).Should().Contain(firstActivation.Id);
        var transfer = await administration.ListAsync(
            tenant,
            true,
            EconomyValueMovementCapability.Transfer,
            100,
            null,
            Now,
            CancellationToken.None);
        transfer.Items.Select(item => item.Hold.Id).Should().Contain(secondActivation.Id);
    }

    [Fact]
    public async Task AdministrationConstructionBoundariesAndCursorValidationFailClosed()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("compliance_hold_admin_boundaries");
        await using var context = CreateContext(database.ConnectionString);
        var policy = new StubReleasePolicy(new ComplianceHoldReleasePolicyAuthorization(1, "policy"));
        Action nullPolicy = () => new PostgreSqlComplianceHoldAdministrationStore(context, null!);
        Action nullVerifier = () => new PostgreSqlComplianceHoldReleasePolicyResolver(context, null!);
        nullPolicy.Should().Throw<ArgumentNullException>();
        nullVerifier.Should().Throw<ArgumentNullException>();
        var administration = new PostgreSqlComplianceHoldAdministrationStore(context, policy);
        var tenant = Guid.NewGuid();

        await FluentActions.Awaiting(() => administration.ListAsync(
                Guid.Empty, null, null, 1, null, Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>().WithParameterName("tenantId");
        await FluentActions.Awaiting(() => administration.ListAsync(
                tenant,
                null,
                (EconomyValueMovementCapability)int.MaxValue,
                1,
                null,
                Now,
                CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Awaiting(() => administration.ListAsync(
                tenant, null, null, 0, null, Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Awaiting(() => administration.CurrentAsync(
                tenant, Guid.Empty, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>().WithParameterName("holdId");
        await FluentActions.Awaiting(() => administration.ProposeReleaseAsync(
                tenant, Guid.NewGuid(), Guid.Empty, "evidence", Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>().WithParameterName("actorId");
        await FluentActions.Awaiting(() => administration.ProposeReleaseAsync(
                tenant, Guid.NewGuid(), Guid.NewGuid(), " ", Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>();

        var id = Guid.NewGuid();
        var valid = PostgreSqlComplianceHoldAdministrationStore.EncodeCursor(Now, id);
        PostgreSqlComplianceHoldAdministrationStore.DecodeCursor(valid)
            .Should().Be((Now, id));
        PostgreSqlComplianceHoldAdministrationStore.DecodeCursor(null).Should().BeNull();
        PostgreSqlComplianceHoldAdministrationStore.DecodeCursor(" ").Should().BeNull();
        var invalid = new[]
        {
            "short",
            $"ZZZZZZZZZZZZZZZZ{id:N}",
            $"0000000000000001{new string('z', 32)}",
            $"FFFFFFFFFFFFFFFF{id:N}",
            $"7FFFFFFFFFFFFFFF{id:N}"
        };
        foreach (var cursor in invalid)
            FluentActions.Invoking(() => PostgreSqlComplianceHoldAdministrationStore.DecodeCursor(cursor))
                .Should().Throw<ArgumentException>().WithParameterName("cursor");

        var resolver = new PostgreSqlComplianceHoldReleasePolicyResolver(
            context,
            new StubSignatureVerifier(true));
        await FluentActions.Awaiting(() => resolver.ResolveAsync(
                Guid.Empty, null, Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>().WithParameterName("tenantId");
        await FluentActions.Awaiting(() => resolver.ResolveAsync(
                tenant,
                (EconomyValueMovementCapability)int.MaxValue,
                Now,
                CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task BoundaryValidationAndConstructionRejectInvalidInputs()
    {
        Action nullContext = () => new PostgreSqlComplianceHoldStore(null!);
        Action nonRelational = () => new PostgreSqlComplianceHoldStore(new StubApplicationDbContext());
        nullContext.Should().Throw<ArgumentNullException>();
        nonRelational.Should().Throw<InvalidOperationException>().WithMessage("*relational DbContext*");

        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("compliance_hold_validation");
        await using var context = CreateContext(database.ConnectionString);
        var store = new PostgreSqlComplianceHoldStore(context);
        var valid = Activation(Guid.NewGuid(), Guid.NewGuid());
        var invalidCapability = (EconomyValueMovementCapability)int.MaxValue;
        Func<ComplianceHoldActivation, Task> activate = value =>
            store.ActivateAsync(value, CancellationToken.None).AsTask();

        await FluentActions.Awaiting(() => activate(null!)).Should().ThrowAsync<ArgumentNullException>();
        await FluentActions.Awaiting(() => activate(valid with { Id = Guid.Empty })).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => activate(valid with { Scope = null! })).Should().ThrowAsync<ArgumentNullException>();
        await FluentActions.Awaiting(() => activate(valid with { Scope = valid.Scope with { TenantId = Guid.Empty } })).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => activate(valid with { Scope = valid.Scope with { SubjectHash = " " } })).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => activate(valid with { Scope = valid.Scope with { Capability = invalidCapability } })).Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Awaiting(() => activate(valid with { CaseReferenceHash = " " })).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => activate(valid with { ReasonCode = " " })).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => activate(valid with { EvidenceHash = " " })).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => activate(valid with { IdempotencyKey = " " })).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => activate(valid with { ActorId = Guid.Empty })).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => activate(valid with { ExpiresAt = valid.ActivatedAt })).Should().ThrowAsync<ArgumentException>();

        await FluentActions.Awaiting(() => store.ReleaseAsync(Guid.Empty, Guid.NewGuid(), "e", Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>().WithParameterName("holdId");
        await FluentActions.Awaiting(() => store.ReleaseAsync(Guid.NewGuid(), Guid.Empty, "e", Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>().WithParameterName("actorId");
        await FluentActions.Awaiting(() => store.ReleaseAsync(Guid.NewGuid(), Guid.NewGuid(), " ", Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => store.IsActiveAsync(null!, Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentNullException>();
    }

    private static ComplianceHoldActivation Activation(Guid tenantId, Guid actorId) => new(
        Guid.NewGuid(),
        new ComplianceHoldScope(tenantId, "subject-hash", null),
        " case-hash ",
        " sanctions ",
        " evidence ",
        " idempotency ",
        actorId,
        Now.AddHours(-1),
        Now.AddHours(2));

    private static EconomyCapabilityPolicyRow Policy(
        Guid id,
        Guid? tenantId,
        EconomyValueMovementCapability capability,
        int approvals)
    {
        var payload =
            $"{{\"riskDecisionLifetimeSeconds\":300,\"riskReviewRequiredApprovals\":{approvals}," +
            "\"complianceHoldSeconds\":86400,\"riskLimits\":[{\"dimension\":\"Wallet\"," +
            "\"subject\":\"SourceWallet\",\"counterVersion\":1,\"maximumUnits\":1000," +
            "\"windowSeconds\":86400}]}";
        return new EconomyCapabilityPolicyRow
        {
            Id = id,
            ScopeKey = $"{tenantId?.ToString("N") ?? "global"}:{(int)capability}:US",
            TenantId = tenantId,
            Capability = capability,
            JurisdictionCode = "US",
            Version = 1,
            CanonicalPayload = payload,
            PayloadHash = $"payload-{id:N}",
            RequestHash = $"request-{id:N}",
            KeyId = "kms-key",
            Signature = "signature",
            ProposedBy = Guid.NewGuid(),
            ApprovedBy = Guid.NewGuid(),
            ProposedAt = Now.AddHours(-2),
            ApprovedAt = Now.AddHours(-1),
            EffectiveAt = Now.AddHours(-1),
            ExpiresAt = Now.AddHours(1),
            ProviderReady = true,
            IsActive = true
        };
    }

    private static ApplicationDbContext CreateContext(string connectionString) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connectionString).Options);

    private sealed class StubApplicationDbContext : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => throw new NotSupportedException();

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubReleasePolicy(ComplianceHoldReleasePolicyAuthorization authorization)
        : IComplianceHoldReleasePolicyResolver
    {
        public Guid LastTenantId { get; private set; }
        public EconomyValueMovementCapability? LastCapability { get; private set; }

        public ValueTask<ComplianceHoldReleasePolicyAuthorization> ResolveAsync(
            Guid tenantId,
            EconomyValueMovementCapability? capability,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            LastTenantId = tenantId;
            LastCapability = capability;
            return ValueTask.FromResult(authorization);
        }
    }

    private sealed class StubSignatureVerifier(bool result) : ICapabilityPolicySignatureVerifier
    {
        public List<string> VerifiedPayloads { get; } = [];

        public ValueTask<bool> VerifyAsync(
            string canonicalPayload,
            string keyId,
            string signature,
            CancellationToken cancellationToken)
        {
            VerifiedPayloads.Add(canonicalPayload);
            return ValueTask.FromResult(result);
        }
    }
}
