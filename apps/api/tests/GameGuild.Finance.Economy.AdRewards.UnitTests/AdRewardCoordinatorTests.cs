using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Reserves;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.AdRewards.UnitTests;

public sealed class AdRewardCoordinatorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-18T12:00:00Z");

    [Fact]
    public void Complete_AtomicallyIssuesVerifiedRewardAndPersistsEveryProtectionFact()
    {
        var harness = new Harness();
        var command = harness.Command();

        var result = harness.Coordinator.Complete(command);

        result.State.Should().Be(AdRewardCompletionState.Issued);
        result.Quote!.RewardSoftUnits.Should().Be(112);
        result.Issuance!.OutputLot.Provenance.Should().Be(ProvenanceKind.AdRewardSoft);
        harness.Store.JournalEntries.Should().ContainSingle();
        harness.Coordinator.Completions.Should().ContainSingle();
        harness.Coordinator.BudgetConsumptions.Should().ContainSingle().Which.Should().Match<AdRewardBudgetConsumption>(item =>
            item.SessionId == command.Claims.SessionId &&
            item.UserId == harness.UserId &&
            item.DeviceRiskHash == harness.DeviceRiskHash &&
            item.Network == "unity" &&
            item.LossBudgetUsdNanos == 1_120_000);
    }

    [Fact]
    public void Complete_IsIdempotentButRejectsTokenOrProviderProofReplay()
    {
        var harness = new Harness();
        var command = harness.Command();
        var first = harness.Coordinator.Complete(command);

        harness.Coordinator.Complete(command).Should().Be(first);
        FluentActions.Invoking(() => harness.Coordinator.Complete(
                harness.Command(idempotency: "other", token: command.Token, proof: harness.Proof(command.Claims))))
            .Should().Throw<AdRewardReplayException>();

        var next = harness.Command(idempotency: "next");
        FluentActions.Invoking(() => harness.Coordinator.Complete(
                next with { Proof = command.Proof }))
            .Should().Throw<AdProviderProofReplayException>();
        harness.Store.JournalEntries.Should().ContainSingle();
    }

    public static TheoryData<AdRewardDependencySnapshot> UnavailableDependencies => new()
    {
        Healthy() with { FraudDecisionAvailable = false },
        Healthy() with { CounterStoreAvailable = false },
        Healthy() with { RevenueReportsCurrent = false },
        Healthy() with { LossBudgetAvailable = false },
        Healthy() with { ReserveSnapshotAvailable = false },
        Healthy() with { ProviderProofServiceAvailable = false },
        Healthy() with { ExpiresAt = Now.AddSeconds(1) }
    };

    [Theory]
    [MemberData(nameof(UnavailableDependencies))]
    public void Complete_FailsClosedWhenAnyIssuanceDependencyIsUnavailable(AdRewardDependencySnapshot dependencies)
    {
        var harness = new Harness();

        FluentActions.Invoking(() => harness.Coordinator.Complete(
                harness.Command(dependencies: dependencies)))
            .Should().Throw<AdRewardDependencyUnavailableException>();
        harness.Store.JournalEntries.Should().BeEmpty();
    }

    [Fact]
    public void Complete_RequiresMatchingRiskDecisionGraphAndAggregateExposureDimensions()
    {
        var harness = new Harness();
        var command = harness.Command();

        FluentActions.Invoking(() => harness.Coordinator.Complete(
                command with { RiskDecisionId = new RiskDecisionId(Guid.NewGuid()) }))
            .Should().Throw<AdRewardRiskBindingException>();
        FluentActions.Invoking(() => harness.Coordinator.Complete(
                command with { EntityCluster = command.EntityCluster with { Version = command.EntityCluster.Version + 1 } }))
            .Should().Throw<AdRewardRiskBindingException>();

        var incomplete = harness.Command(idempotency: "missing-limit", omitDimension: RiskLimitDimension.DeviceIpAsnCluster);
        FluentActions.Invoking(() => harness.Coordinator.Complete(incomplete))
            .Should().Throw<AdRewardRiskBindingException>();
    }

    public static TheoryData<AdRewardBudgetPolicy> ExhaustedBudgets => new()
    {
        Budget(maxUser: 150),
        Budget(maxDevice: 150),
        Budget(maxNetwork: 150),
        Budget(maxGlobal: 150),
        Budget(lossBudgetUsdNanos: 1_500_000)
    };

    [Theory]
    [MemberData(nameof(ExhaustedBudgets))]
    public void Complete_AtomicallyEnforcesVelocityQuotaAndFundedLossBudget(AdRewardBudgetPolicy budget)
    {
        var harness = new Harness(budget: budget);
        harness.Coordinator.Complete(harness.Command());

        FluentActions.Invoking(() => harness.Coordinator.Complete(harness.Command(idempotency: "second")))
            .Should().Throw<AdRewardBudgetExceededException>();
        harness.Store.JournalEntries.Should().ContainSingle();
        harness.Coordinator.BudgetConsumptions.Should().ContainSingle();
    }

    [Fact]
    public void Complete_EnforcesPolicyFreshnessAndBothKillSwitches()
    {
        var staleHarness = new Harness(policy: Policy(reportsCurrentThrough: Now.AddDays(-2)));
        FluentActions.Invoking(() => staleHarness.Coordinator.Complete(staleHarness.Command()))
            .Should().Throw<AdRewardDependencyUnavailableException>();

        var networkHarness = new Harness();
        networkHarness.Controls.DisableNetwork("unity", 1, "variance");
        FluentActions.Invoking(() => networkHarness.Coordinator.Complete(networkHarness.Command()))
            .Should().Throw<AdRewardIssuanceDisabledException>();

        var globalHarness = new Harness();
        globalHarness.Controls.DisableGlobally(1, "loss-budget");
        FluentActions.Invoking(() => globalHarness.Coordinator.Complete(globalHarness.Command()))
            .Should().Throw<AdRewardIssuanceDisabledException>();
    }

    [Fact]
    public void Complete_RecordsNonMonetaryPendingClaimForDeferredNetworks()
    {
        var harness = new Harness(policy: Policy(mode: AdRewardIssuanceMode.DeferredReport));
        var command = harness.Command(includeAuthorization: false, includeProof: false);

        var result = harness.Coordinator.Complete(command);

        result.State.Should().Be(AdRewardCompletionState.PendingProviderReport);
        result.Quote.Should().BeNull();
        result.Issuance.Should().BeNull();
        harness.Store.JournalEntries.Should().BeEmpty();
        harness.Coordinator.PendingClaims.Should().ContainSingle().Which.SessionId.Should().Be(command.Claims.SessionId);
        harness.Coordinator.BudgetConsumptions.Should().BeEmpty();

        var pending = harness.Coordinator.PendingClaims.Single();
        pending.UserId.Should().Be(harness.UserId);
        pending.WalletId.Should().Be(harness.WalletId);
        pending.Network.Should().Be("unity");
        pending.PolicyVersion.Should().Be(new PolicyVersion(1));
        pending.SourceId.Should().Be(command.SourceId);
        pending.IdempotencyKey.Should().Be(command.IdempotencyKey);
        pending.CompletedAt.Should().Be(command.CompletedAt);
    }

    [Fact]
    public void Complete_RejectsIdempotencyClaimsAndPolicyBindingConflicts()
    {
        var harness = new Harness();
        var first = harness.Command();
        harness.Coordinator.Complete(first);
        FluentActions.Invoking(() => harness.Coordinator.Complete(harness.Command()))
            .Should().Throw<AdRewardIdempotencyConflictException>();

        var claimsHarness = new Harness();
        var claimsCommand = claimsHarness.Command();
        FluentActions.Invoking(() => claimsHarness.Coordinator.Complete(
                claimsCommand with { Claims = claimsCommand.Claims with { DeviceRiskHash = "tampered" } }))
            .Should().Throw<AdRewardRiskBindingException>();

        var policyHarness = new Harness();
        var policyCommand = policyHarness.Command();
        var wrongPolicyClaims = policyCommand.Claims with { PolicyVersion = new PolicyVersion(2) };
        FluentActions.Invoking(() => policyHarness.Coordinator.Complete(policyCommand with
            {
                Claims = wrongPolicyClaims,
                Token = policyHarness.Sign(wrongPolicyClaims)
            }))
            .Should().Throw<AdRewardRiskBindingException>();
    }

    [Fact]
    public void Complete_AccumulatesSubUnitRewardWithoutLedgerPosting()
    {
        var harness = new Harness(policy: Policy(ecpm: 1, share: 1, buffer: 999_998));
        var result = harness.Coordinator.Complete(harness.Command(includeAuthorization: false));

        result.State.Should().Be(AdRewardCompletionState.AccumulatedRemainder);
        result.Quote!.RewardSoftUnits.Should().Be(0);
        result.Issuance.Should().BeNull();
        harness.Store.JournalEntries.Should().BeEmpty();
        harness.Coordinator.Attributions.Should().ContainSingle();
    }

    [Fact]
    public void Coordinator_ValidatesConstructorAndBudgetWindow()
    {
        var policies = new AdNetworkPolicyStore();
        var controls = new AdRewardControlState();
        var tokens = new AdRewardSessionTokenService(new byte[32], TimeSpan.FromMinutes(5));
        var playback = new AdPlaybackVerifier(new HmacProviderCompletionProofService("unity", new byte[32]));
        var accumulator = new AdRewardRationalAccumulator();
        var posting = new TransactionalPostingService(new InMemoryLedgerKernelStore());
        FluentActions.Invoking(() => new AdRewardCoordinator(null!, controls, tokens, playback, accumulator, posting)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new AdRewardCoordinator(policies, null!, tokens, playback, accumulator, posting)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new AdRewardCoordinator(policies, controls, null!, playback, accumulator, posting)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new AdRewardCoordinator(policies, controls, tokens, null!, accumulator, posting)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new AdRewardCoordinator(policies, controls, tokens, playback, null!, posting)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new AdRewardCoordinator(policies, controls, tokens, playback, accumulator, null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new AdRewardBudgetPolicy(1, 1, 1, 1, 1, TimeSpan.Zero))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Complete_RejectsMissingAccountAndDeviceGraphNodes()
    {
        var accountHarness = new Harness();
        var accountCommand = accountHarness.Command();
        var withoutAccount = accountCommand.EntityCluster with
        {
            Nodes = accountCommand.EntityCluster.Nodes
                .Where(node => node.Type != RiskEntityType.Account).ToArray()
        };
        FluentActions.Invoking(() => accountHarness.Coordinator.Complete(
                accountCommand with { EntityCluster = withoutAccount }))
            .Should().Throw<AdRewardRiskBindingException>();

        var deviceHarness = new Harness();
        var deviceCommand = deviceHarness.Command();
        var withoutDevice = deviceCommand.EntityCluster with
        {
            Nodes = deviceCommand.EntityCluster.Nodes
                .Where(node => node.Type != RiskEntityType.DeviceRiskToken).ToArray()
        };
        FluentActions.Invoking(() => deviceHarness.Coordinator.Complete(
                deviceCommand with { EntityCluster = withoutDevice }))
            .Should().Throw<AdRewardRiskBindingException>();
    }

    private static AdRewardDependencySnapshot Healthy() =>
        AdRewardDependencySnapshot.Healthy(Now.AddSeconds(30), Now.AddMinutes(2));

    private static AdRewardBudgetPolicy Budget(
        long maxUser = 10_000,
        long maxDevice = 10_000,
        long maxNetwork = 10_000,
        long maxGlobal = 10_000,
        long lossBudgetUsdNanos = 100_000_000) => new(
        maxUser, maxDevice, maxNetwork, maxGlobal, lossBudgetUsdNanos, TimeSpan.FromDays(1));

    private static AdNetworkPolicy Policy(
        AdRewardIssuanceMode mode = AdRewardIssuanceMode.ImmediateProviderProof,
        DateTimeOffset? reportsCurrentThrough = null,
        long ecpm = 2_000_000_000,
        int share = 700_000,
        int buffer = 200_000) => new(
        "unity",
        new PolicyVersion(1),
        Now.AddHours(-1),
        Now.AddHours(1),
        mode,
        AdNetworkYieldState.Trailing,
        ecpm,
        share,
        buffer,
        900_000,
        TimeSpan.FromSeconds(3),
        1_000,
        reportsCurrentThrough ?? Now,
        TimeSpan.FromHours(24),
        100);

    internal sealed class Harness
    {
        private readonly byte[] _sessionSecret = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
        private readonly HmacProviderCompletionProofService _proofs = new(
            "unity", Enumerable.Range(33, 32).Select(value => (byte)value).ToArray());
        private readonly AdRewardSessionTokenService _tokens;
        private readonly AdNetworkPolicy _policy;
        private readonly AdRewardBudgetPolicy _budget;

        internal Harness(AdNetworkPolicy? policy = null, AdRewardBudgetPolicy? budget = null)
        {
            _policy = policy ?? Policy();
            _budget = budget ?? Budget();
            Policies.Publish(_policy);
            _tokens = new AdRewardSessionTokenService(_sessionSecret, TimeSpan.FromMinutes(5));
            Coordinator = new AdRewardCoordinator(
                Policies,
                Controls,
                _tokens,
                new AdPlaybackVerifier(_proofs),
                new AdRewardRationalAccumulator(),
                new TransactionalPostingService(Store));
        }

        internal Guid UserId { get; } = Guid.Parse("51000000-0000-0000-0000-000000000005");
        internal WalletId WalletId { get; } = new(Guid.Parse("52000000-0000-0000-0000-000000000005"));
        internal string DeviceRiskHash { get; } = "device-risk-hash";
        internal InMemoryLedgerKernelStore Store { get; } = new();
        internal AdNetworkPolicyStore Policies { get; } = new();
        internal AdRewardControlState Controls { get; } = new();
        internal AdRewardCoordinator Coordinator { get; }

        internal ProviderCompletionProof Proof(AdRewardSessionClaims claims) =>
            _proofs.Sign($"event-{claims.SessionId:N}", claims.SessionId, claims.CreativeId,
                Now.AddSeconds(31), "provider-evidence");

        internal SignedAdRewardSession Sign(AdRewardSessionClaims claims) => _tokens.Issue(claims, Now);

        internal AdRewardCompletionCommand Command(
            string idempotency = "reward-1",
            SignedAdRewardSession? token = null,
            ProviderCompletionProof? proof = null,
            AdRewardDependencySnapshot? dependencies = null,
            RiskLimitDimension? omitDimension = null,
            bool includeAuthorization = true,
            bool includeProof = true)
        {
            var sessionId = Guid.NewGuid();
            var claims = new AdRewardSessionClaims(
                sessionId,
                UserId,
                WalletId,
                "unity",
                "creative-1",
                DeviceRiskHash,
                $"nonce-{sessionId:N}",
                TimeSpan.FromSeconds(30),
                _policy.Version,
                Now,
                Now.AddMinutes(5));
            var signed = token ?? _tokens.Issue(claims, Now);
            if (token is not null) claims = _tokens.Validate(token.Value, Now.AddSeconds(30));
            var sourceId = SourceStampId.New();
            var key = new IdempotencyKey(idempotency);
            var cluster = Cluster();
            var context = Context(key, sourceId, cluster);
            var authorization = includeAuthorization
                ? Authorize(context, sourceId, cluster, omitDimension)
                : null;
            return new AdRewardCompletionCommand(
                key,
                signed,
                claims,
                new AdPlaybackEvidence(
                    Now, Now.AddSeconds(31), TimeSpan.FromSeconds(30),
                    TimeSpan.FromSeconds(29), TimeSpan.FromSeconds(1), [0, 25, 50, 75, 100]),
                includeProof ? proof ?? Proof(claims) : null,
                sourceId,
                PostingId.New(),
                CreditLotId.New(),
                context,
                authorization is null ? null : new RiskDecisionId(authorization.Risk.DecisionId),
                authorization,
                cluster,
                dependencies ?? Healthy(),
                _budget,
                Now.AddSeconds(32));
        }

        internal DeferredAdRewardConfirmationCommand DeferredConfirmation(
            AdRewardCompletionCommand pending,
            VerifiedAdProviderReport report)
        {
            var key = new IdempotencyKey($"{pending.IdempotencyKey.Value}:report:{report.ReportId}:{report.Version}");
            var context = Context(key, pending.SourceId, pending.EntityCluster);
            var authorization = Authorize(
                context, pending.SourceId, pending.EntityCluster, null, Now.AddHours(2));
            return new DeferredAdRewardConfirmationCommand(
                pending.Claims.SessionId,
                key,
                report,
                PostingId.New(),
                CreditLotId.New(),
                context,
                new RiskDecisionId(authorization.Risk.DecisionId),
                authorization,
                pending.EntityCluster,
                AdRewardDependencySnapshot.Healthy(Now.AddHours(2), Now.AddHours(3)),
                _budget,
                Now.AddHours(2).AddSeconds(1));
        }

        private EntityRiskCluster Cluster()
        {
            var graph = new EntityRiskGraph();
            var account = new RiskEntityNode(RiskEntityType.Account, UserId.ToString("N"));
            var device = new RiskEntityNode(RiskEntityType.DeviceRiskToken, DeviceRiskHash);
            var referral = new RiskEntityNode(RiskEntityType.Referral, "referral-cluster");
            graph.Link(account, device, "account-device", Now.AddMinutes(-1));
            graph.Link(device, referral, "device-referral", Now.AddMinutes(-1));
            return graph.ClusterFor(account);
        }

        private ProtectedOperationContext Context(
            IdempotencyKey key,
            SourceStampId sourceId,
            EntityRiskCluster cluster) => new(
            key,
            UserId,
            PostingTemplateKind.AdRewardIssuance,
            WalletId,
            WalletId,
            new CoinAmount(CurrencyCode.SoftCoin, 112),
            [new RiskCurrencyLeg(CurrencyCode.SoftCoin, 112)],
            [sourceId],
            "provider-proof",
            _policy.Version,
            new ReserveVersion(1),
            1,
            1,
            cluster.Version,
            cluster.EvidenceHash,
            1,
            1);

        private static ProtectedIssuanceAuthorization Authorize(
            ProtectedOperationContext context,
            SourceStampId sourceId,
            EntityRiskCluster cluster,
            RiskLimitDimension? omitDimension,
            DateTimeOffset? requestedAt = null)
        {
            var at = requestedAt ?? Now.AddSeconds(30);
            var reserve = new CoreReserveAuthority();
            reserve.ValidateAndActivate(new ReserveProposal(
                new ReserveVersion(1), null, context.PolicyVersion, 1,
                at.AddMinutes(-1), at.AddMinutes(5),
                new ReserveLiabilityPosition(0, 0, 0, 0),
                new ReserveBufferPosition(0, 0, 0, 100_000_000, 100_000_000, 0, 0),
                [new ReserveServiceObservation("ad-reward", 1, 1, 1, 1, 0, true, at.AddMinutes(-1), at.AddMinutes(5))],
                [
                    new ExternalReserveAsset("hard", ReserveBackingPurpose.HardCoin, 2_000_000_000_000),
                    new ExternalReserveAsset("soft", ReserveBackingPurpose.SoftCoin, 2_000_000_000_000)
                ],
                "reserve-evidence"), at);
            var decision = RiskDecisionSnapshot.Create(
                Guid.NewGuid(), RiskOutcome.Allow, context,
                at.AddSeconds(-1), at.AddMinutes(1), [RiskReasonCode.WithinLimits]);
            var limits = new List<AggregateRiskLimit>
            {
                Limit(RiskLimitDimension.Wallet, context.SourceWalletId.Value.ToString("N")),
                Limit(RiskLimitDimension.IdentityCluster, cluster.Id),
                Limit(RiskLimitDimension.DeviceIpAsnCluster, "device-risk-hash"),
                Limit(RiskLimitDimension.ProviderAccount, "unity"),
                Limit(RiskLimitDimension.GlobalLossBudget, "ad-rewards-global"),
                Limit(RiskLimitDimension.SourceRoot, sourceId.Value.ToString("N"))
            };
            if (omitDimension.HasValue)
                limits.RemoveAll(limit => limit.Key.Dimension == omitDimension.Value);
            return new ProtectedIssuanceAuthorizer(
                    reserve,
                    new CoreProtectedPostingGate(new RiskDecisionAuthorizer()),
                    new AggregateRiskCounterStore(),
                    new ProtectedChangeCooldownRegistry())
                .Authorize(new ProtectedIssuanceRequest(
                    context,
                    new RiskDecisionId(decision.Id),
                    decision,
                    new RiskPersistenceReadiness(true, true),
                    Guid.NewGuid(),
                    limits,
                    context.ActorId,
                    at,
                    context.Amount));
        }

        private static AggregateRiskLimit Limit(RiskLimitDimension dimension, string subject) =>
            new(new RiskLimitKey(dimension, subject), 1, 10_000, TimeSpan.FromDays(1));
    }
}
