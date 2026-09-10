using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Finance.Economy.UnitTests.Risk;

public sealed class EconomyProtectedOperationOrchestratorTests
{
    private static readonly Guid TenantId = Guid.Parse("98000000-0000-0000-0000-000000000001");
    private static readonly Guid ActorId = Guid.Parse("98000000-0000-0000-0000-000000000002");
    private static readonly Guid DecisionId = Guid.Parse("98000000-0000-0000-0000-000000000003");
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExecuteAsync_DerivesAuthorityAndConsumesReceiptInsideTheTransaction()
    {
        var accessor = AuthenticatedActor();
        var issuer = new CapturingIssuer(new EconomyProtectedRiskDecision(
            DecisionId, RiskOutcome.Allow, EconomyProtectedOperationState.Ready, null, []));
        var capabilities = new CapturingCapabilityService();
        var transaction = new CapturingTransaction();
        var orchestrator = Orchestrator(
            accessor, new FixedJurisdictionResolver(), issuer, capabilities, transaction);
        var intent = Intent();

        var result = await orchestrator.ExecuteAsync(
            intent,
            (authorization, _) => Task.FromResult(authorization.JurisdictionCode),
            default);

        result.Should().Be("BRA");
        transaction.Committed.Should().BeTrue();
        issuer.Request.Should().NotBeNull();
        issuer.Request!.TenantId.Should().Be(TenantId);
        issuer.Request.ActorId.Should().Be(ActorId);
        issuer.Request.JurisdictionCode.Should().Be("BRA");
        issuer.Request.OperationFingerprint.Should().Be(
            EconomyProtectedOperationOrchestrator.Fingerprint(TenantId, ActorId, intent));
        capabilities.Context.Should().NotBeNull();
        capabilities.Context!.RiskDecisionId.Should().Be(DecisionId);
        capabilities.Context.OperationFingerprint.Should().Be(issuer.Request.OperationFingerprint);
        capabilities.Context.SourceRootHashes.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_UsesTheServerSelectedProtectedSubjectForJurisdictionAndEvidence()
    {
        var subjectId = Guid.Parse("98000000-0000-0000-0000-000000000020");
        var resolver = new FixedJurisdictionResolver();
        var issuer = new CapturingIssuer(new EconomyProtectedRiskDecision(
            DecisionId, RiskOutcome.Allow, EconomyProtectedOperationState.Ready, null, []));
        var capabilities = new CapturingCapabilityService();
        var orchestrator = Orchestrator(
            AuthenticatedActor(), resolver, issuer, capabilities, new CapturingTransaction());
        var intent = Intent() with { ProtectedSubjectId = subjectId };

        await orchestrator.ExecuteAsync(intent, (_, _) => Task.FromResult(true), default);

        resolver.SubjectId.Should().Be(subjectId);
        issuer.Request!.ActorId.Should().Be(ActorId);
        issuer.Request.SubjectReference.Should().Be(EconomySubjectReference.ForUser(TenantId, subjectId));
        capabilities.Context!.ActorId.Should().Be(ActorId);
        capabilities.Context.SubjectReference.Should().Be(EconomySubjectReference.ForUser(TenantId, subjectId));
        EconomyProtectedOperationOrchestrator.Fingerprint(TenantId, ActorId, intent)
            .Should().NotBe(EconomyProtectedOperationOrchestrator.Fingerprint(TenantId, ActorId, Intent()));
    }

    [Fact]
    public async Task ExecuteAsync_PersistsReviewOutcomeWithoutInvokingTheProtectedMutation()
    {
        var reviewId = Guid.NewGuid();
        var transaction = new CapturingTransaction();
        var orchestrator = Orchestrator(
            AuthenticatedActor(),
            new FixedJurisdictionResolver(),
            new CapturingIssuer(new EconomyProtectedRiskDecision(
                DecisionId, RiskOutcome.Review, EconomyProtectedOperationState.ReviewRequired,
                reviewId, ["A manual review is required."])),
            new CapturingCapabilityService(),
            transaction);
        var invoked = false;

        var action = () => orchestrator.ExecuteAsync(
            Intent(),
            (_, _) =>
            {
                invoked = true;
                return Task.FromResult(true);
            },
            default);

        var exception = await action.Should().ThrowAsync<EconomyProtectedOperationException>();
        exception.Which.State.Should().Be(EconomyProtectedOperationState.ReviewRequired);
        exception.Which.ReviewId.Should().Be(reviewId);
        invoked.Should().BeFalse();
        transaction.Committed.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_RejectsAnAnonymousActorBeforeOpeningATransaction()
    {
        var transaction = new CapturingTransaction();
        var orchestrator = Orchestrator(
            new ActorContextAccessor(),
            new FixedJurisdictionResolver(),
            new CapturingIssuer(null!),
            new CapturingCapabilityService(),
            transaction);

        var action = () => orchestrator.ExecuteAsync(
            Intent(), (_, _) => Task.FromResult(true), default);

        await action.Should().ThrowAsync<UnauthorizedAccessException>();
        transaction.Calls.Should().Be(0);
    }

    [Fact]
    public void Fingerprint_IsStableAndUnambiguousAcrossFieldBoundaries()
    {
        var intent = Intent();
        var first = EconomyProtectedOperationOrchestrator.Fingerprint(TenantId, ActorId, intent);
        var replay = EconomyProtectedOperationOrchestrator.Fingerprint(TenantId, ActorId, intent);
        var changed = EconomyProtectedOperationOrchestrator.Fingerprint(
            TenantId,
            ActorId,
            intent with { ProviderReferenceHash = intent.ProviderReferenceHash + "x" });

        first.Should().Be(replay).And.HaveLength(64);
        changed.Should().NotBe(first);
    }

    private static EconomyProtectedOperationOrchestrator Orchestrator(
        IActorContextAccessor accessor,
        IEconomyJurisdictionResolver resolver,
        IEconomyProtectedOperationRiskDecisionIssuer issuer,
        IEconomyCapabilityAuthorizationService capabilities,
        IEconomyProtectedOperationTransaction transaction) => new(
        accessor,
        new EconomyTrustedProtectedOperationAuthorizer(
            resolver,
            issuer,
            capabilities,
            transaction));

    [Fact]
    public void ValidationRejectsEveryUnboundProtectedOperationShape()
    {
        var intent = Intent();
        var invalid = new EconomyProtectedOperationIntent?[]
        {
            null,
            intent with { Capability = (EconomyValueMovementCapability)0 },
            intent with { TemplateKind = (PostingTemplateKind)0 },
            intent with { SourceWalletId = default },
            intent with { DestinationWalletId = default },
            intent with { ProtectedSubjectId = Guid.Empty },
            intent with { CurrencyLegs = null! },
            intent with { CurrencyLegs = [] },
            intent with { SourceRoots = null! },
            intent with { ProviderReferenceHash = " " },
            intent with { DestinationHash = " " }
        };

        foreach (var candidate in invalid)
            FluentActions.Invoking(() => EconomyProtectedOperationOrchestrator.Validate(candidate!))
                .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RejectionWithoutDiagnosticsUsesTheStructuredStateMessage()
    {
        var exception = new EconomyProtectedOperationException(
            EconomyProtectedOperationState.Denied, null, []);

        exception.Message.Should().Be(
            "The protected Economy operation was rejected with state Denied.");
        exception.Diagnostics.Should().BeEmpty();
    }

    private static EconomyProtectedOperationIntent Intent() => new(
        EconomyValueMovementCapability.BountyEscrow,
        PostingTemplateKind.BountyEscrow,
        new WalletId(Guid.Parse("98000000-0000-0000-0000-000000000010")),
        new WalletId(Guid.Parse("98000000-0000-0000-0000-000000000011")),
        new CoinAmount(CurrencyCode.HardCoin, 100),
        [new RiskCurrencyLeg(CurrencyCode.HardCoin, 100)],
        [
            new SourceStampId(Guid.Parse("98000000-0000-0000-0000-000000000012")),
            new SourceStampId(Guid.Parse("98000000-0000-0000-0000-000000000013"))
        ],
        "provider-hash",
        "destination-hash",
        new IdempotencyKey("protected-operation"),
        Now);

    private static ActorContextAccessor AuthenticatedActor()
    {
        var accessor = new ActorContextAccessor();
        accessor.SetActorContext(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = ActorId.ToString(),
            TenantId = TenantId,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            IsAuthenticated = true
        });
        return accessor;
    }

    private sealed class FixedJurisdictionResolver : IEconomyJurisdictionResolver
    {
        public Guid SubjectId { get; private set; }

        public ValueTask<EconomyJurisdictionResolution> ResolveAsync(
            Guid tenantId, Guid actorId, string? providerJurisdiction,
            string? destinationJurisdiction, DateTimeOffset evaluatedAt,
            CancellationToken cancellationToken)
        {
            SubjectId = actorId;
            return ValueTask.FromResult(
                new EconomyJurisdictionResolution("BRA", 3, 7, "kyc-evidence"));
        }
    }

    private sealed class CapturingIssuer(EconomyProtectedRiskDecision result)
        : IEconomyProtectedOperationRiskDecisionIssuer
    {
        public EconomyProtectedRiskDecisionRequest? Request { get; private set; }

        public ValueTask<EconomyProtectedRiskDecision> IssueAsync(
            EconomyProtectedRiskDecisionRequest request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class CapturingCapabilityService : IEconomyCapabilityAuthorizationService
    {
        public EconomyCapabilityEvaluationContext? Context { get; private set; }

        public ValueTask<CapabilityAuthorizationReceipt> AuthorizeAndConsumeAsync(
            EconomyCapabilityEvaluationContext context,
            CancellationToken cancellationToken)
        {
            Context = context;
            return ValueTask.FromResult(new CapabilityAuthorizationReceipt(
                Guid.NewGuid(), context.TenantId, context.ActorId, context.SubjectReference,
                context.JurisdictionCode, context.Capability, context.OperationFingerprint,
                1, 1, context.RiskDecisionId, 0, context.ProviderHash, context.DestinationHash,
                context.SourceRootHashes, [], context.EvaluatedAt, context.EvaluatedAt.AddMinutes(1),
                "receipt-hash", "key", "signature"));
        }
    }

    private sealed class CapturingTransaction : IEconomyProtectedOperationTransaction
    {
        public int Calls { get; private set; }
        public bool Committed { get; private set; }

        public async Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken)
        {
            Calls++;
            var result = await operation(cancellationToken);
            Committed = true;
            return result;
        }
    }
}
