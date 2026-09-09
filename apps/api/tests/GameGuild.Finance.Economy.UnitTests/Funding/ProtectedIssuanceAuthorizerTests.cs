using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Reserves;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.UnitTests.Funding;

public sealed class ProtectedIssuanceAuthorizerTests
{
    private static readonly DateTimeOffset Time = DateTimeOffset.Parse("2026-07-18T12:00:00Z");

    [Fact]
    public void Authorize_SerializesFreshReserveRiskCountersRootsAndCooldownState()
    {
        var fixture = CreateFixture(10);

        var authorization = fixture.Authorizer.Authorize(fixture.Request);

        authorization.Operation.Should().Be(PostingTemplateKind.ConfirmedTopUpMint);
        authorization.Amount.Should().Be(fixture.Context.Amount);
        authorization.Reserve.Version.Should().Be(fixture.Context.ReserveVersion);
        authorization.Risk.DecisionId.Should().Be(fixture.Decision.Id);
        authorization.Counter.Allocations.Should().Contain(item =>
            item.Key.Dimension == RiskLimitDimension.SourceRoot);
        authorization.EnsureMatches(
            fixture.Context.Operation,
            fixture.Context.IdempotencyKey,
            fixture.Context.Amount,
            fixture.Context.ReserveVersion,
            Time);
    }

    [Fact]
    public void Authorize_RejectsMissingSourceRootExposureLimit()
    {
        var fixture = CreateFixture(10);
        var request = fixture.Request with
        {
            AggregateLimits = fixture.Request.AggregateLimits
                .Where(limit => limit.Key.Dimension != RiskLimitDimension.SourceRoot)
                .ToArray()
        };

        FluentActions.Invoking(() => fixture.Authorizer.Authorize(request))
            .Should().Throw<MissingSourceRootRiskLimitException>();
    }

    [Fact]
    public void Authorize_RejectsActiveProtectedChangeCooldown()
    {
        var fixture = CreateFixture(10);
        fixture.Cooldowns.Record(
            fixture.Request.CooldownSubjectId,
            ProtectedChangeKind.PasswordReset,
            "password-change",
            Time.AddMinutes(-1),
            TimeSpan.FromHours(1));

        FluentActions.Invoking(() => fixture.Authorizer.Authorize(fixture.Request))
            .Should().Throw<ProtectedChangeCooldownActiveException>();
    }

    [Fact]
    public void Authorize_RejectsStaleReserveHead()
    {
        var fixture = CreateFixture(10);

        FluentActions.Invoking(() => fixture.Authorizer.Authorize(
                fixture.Request with { RequestedAt = Time.AddMinutes(6) }))
            .Should().Throw<ReserveInputUnknownException>();
    }

    [Fact]
    public void Authorize_RejectsAggregateLimitExceeded()
    {
        var fixture = CreateFixture(6);
        fixture.Authorizer.Authorize(fixture.Request);
        var secondContext = fixture.Context with { IdempotencyKey = new IdempotencyKey("issuance-2") };
        var secondDecision = RiskDecisionSnapshot.Create(
            Guid.NewGuid(),
            RiskOutcome.Allow,
            secondContext,
            Time.AddSeconds(-1),
            Time.AddMinutes(1),
            [RiskReasonCode.WithinLimits]);
        var second = fixture.Request with
        {
            Context = secondContext,
            RiskDecisionId = new RiskDecisionId(secondDecision.Id),
            Decision = secondDecision,
            CounterReservationId = Guid.NewGuid()
        };

        FluentActions.Invoking(() => fixture.Authorizer.Authorize(second))
            .Should().Throw<AggregateRiskLimitExceededException>();
    }

    [Fact]
    public void Authorization_RejectsAnyMutationOrUseAfterReserveExpiry()
    {
        var fixture = CreateFixture(10);
        var authorization = fixture.Authorizer.Authorize(fixture.Request);

        FluentActions.Invoking(() => authorization.EnsureMatches(
                PostingTemplateKind.SystemBackedGrant,
                fixture.Context.IdempotencyKey,
                fixture.Context.Amount,
                fixture.Context.ReserveVersion,
                Time))
            .Should().Throw<IssuanceAuthorizationBindingException>();
        FluentActions.Invoking(() => authorization.EnsureMatches(
                fixture.Context.Operation,
                fixture.Context.IdempotencyKey,
                fixture.Context.Amount,
                fixture.Context.ReserveVersion,
                Time.AddMinutes(6)))
            .Should().Throw<IssuanceAuthorizationExpiredException>();
    }

    [Fact]
    public void Authorization_RejectsMismatchedRootsAndMalformedRequests()
    {
        var fixture = CreateFixture(10);
        var authorization = fixture.Authorizer.Authorize(fixture.Request);

        FluentActions.Invoking(() => authorization.EnsureSourceRoots([SourceStampId.New()]))
            .Should().Throw<IssuanceAuthorizationBindingException>();
        FluentActions.Invoking(() => authorization.EnsureSourceRoots(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => fixture.Authorizer.Authorize(
                fixture.Request with { CounterReservationId = Guid.Empty }))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => fixture.Authorizer.Authorize(
                fixture.Request with { CooldownSubjectId = Guid.Empty }))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AuthorizerConstructor_RejectsMissingSecurityDependencies()
    {
        var reserve = new CoreReserveAuthority();
        var gate = new CoreProtectedPostingGate(new RiskDecisionAuthorizer());
        var counters = new AggregateRiskCounterStore();
        var cooldowns = new ProtectedChangeCooldownRegistry();

        FluentActions.Invoking(() => new ProtectedIssuanceAuthorizer(null!, gate, counters, cooldowns))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new ProtectedIssuanceAuthorizer(reserve, null!, counters, cooldowns))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new ProtectedIssuanceAuthorizer(reserve, gate, null!, cooldowns))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new ProtectedIssuanceAuthorizer(reserve, gate, counters, null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AuthorizationValidity_UsesTheEarlierReserveExpiry()
    {
        var fixture = CreateFixture(10);
        var decision = RiskDecisionSnapshot.Create(
            Guid.NewGuid(), RiskOutcome.Allow, fixture.Context,
            Time.AddSeconds(-1), Time.AddMinutes(10), [RiskReasonCode.WithinLimits]);

        fixture.Authorizer.Authorize(fixture.Request with
        {
            RiskDecisionId = new RiskDecisionId(decision.Id),
            Decision = decision
        }).ValidUntil.Should().Be(Time.AddMinutes(5));
    }

    private static Fixture CreateFixture(long units)
    {
        var reserve = new CoreReserveAuthority();
        reserve.ValidateAndActivate(Proposal(), Time);
        var counters = new AggregateRiskCounterStore();
        var cooldowns = new ProtectedChangeCooldownRegistry();
        var authorizer = new ProtectedIssuanceAuthorizer(
            reserve,
            new CoreProtectedPostingGate(new RiskDecisionAuthorizer()),
            counters,
            cooldowns);
        var source = SourceStampId.New();
        var context = new ProtectedOperationContext(
            new IdempotencyKey("issuance-1"),
            Guid.NewGuid(),
            PostingTemplateKind.ConfirmedTopUpMint,
            WalletId.New(),
            WalletId.New(),
            new CoinAmount(CurrencyCode.HardCoin, units),
            [new RiskCurrencyLeg(CurrencyCode.HardCoin, units)],
            [source],
            "provider-reference-hash",
            new PolicyVersion(1),
            new ReserveVersion(1),
            1,
            1,
            1,
            "entity-graph-evidence",
            1,
            1);
        var decision = RiskDecisionSnapshot.Create(
            Guid.NewGuid(),
            RiskOutcome.Allow,
            context,
            Time.AddSeconds(-1),
            Time.AddMinutes(1),
            [RiskReasonCode.WithinLimits]);
        AggregateRiskLimit[] limits =
        [
            new(new RiskLimitKey(RiskLimitDimension.Wallet, context.SourceWalletId.Value.ToString("N")), 1, 10, TimeSpan.FromDays(1)),
            new(new RiskLimitKey(RiskLimitDimension.SourceRoot, source.Value.ToString("N")), 1, 10, TimeSpan.FromDays(1))
        ];
        var request = new ProtectedIssuanceRequest(
            context,
            new RiskDecisionId(decision.Id),
            decision,
            new RiskPersistenceReadiness(true, true),
            Guid.NewGuid(),
            limits,
            context.ActorId,
            Time);
        return new Fixture(authorizer, cooldowns, context, decision, request);
    }

    private static ReserveProposal Proposal() => new(
        new ReserveVersion(1),
        null,
        new PolicyVersion(1),
        1,
        Time.AddMinutes(-1),
        Time.AddMinutes(5),
        new ReserveLiabilityPosition(0, 0, 0, 0),
        new ReserveBufferPosition(0, 0, 0, 0, 0, 0, 0),
        [new ReserveServiceObservation("service", 1, 1, 1, 1, 0, true, Time.AddMinutes(-1), Time.AddMinutes(5))],
        [
            new ExternalReserveAsset("hard", ReserveBackingPurpose.HardCoin, 2_000_000_000),
            new ExternalReserveAsset("soft", ReserveBackingPurpose.SoftCoin, 2_000_000_000)
        ],
        "reserve-evidence");

    private sealed record Fixture(
        ProtectedIssuanceAuthorizer Authorizer,
        ProtectedChangeCooldownRegistry Cooldowns,
        ProtectedOperationContext Context,
        RiskDecisionSnapshot Decision,
        ProtectedIssuanceRequest Request);
}
