using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.UnitTests.Risk;

public sealed class RiskDecisionAuthorizerTests
{
    private static readonly DateTimeOffset Time = new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AllowDecisionAuthorizesOnceAndSupportsOnlyExactIdempotentReplay()
    {
        var authorizer = new RiskDecisionAuthorizer();
        var context = Context();
        var decision = Decision(RiskOutcome.Allow, context);

        var first = authorizer.AuthorizeValueMovement(decision, context, Time);
        var replay = authorizer.AuthorizeValueMovement(decision, context, Time.AddSeconds(1));

        replay.Should().Be(first);
        authorizer.Authorizations.Should().ContainSingle();
        FluentActions.Invoking(() => authorizer.AuthorizeValueMovement(
                decision,
                context with { IdempotencyKey = new IdempotencyKey("different-replay") },
                Time.AddSeconds(2)))
            .Should().Throw<RiskDecisionReuseException>();
    }

    [Theory]
    [InlineData(RiskOutcome.Challenge)]
    [InlineData(RiskOutcome.Hold)]
    [InlineData(RiskOutcome.Review)]
    [InlineData(RiskOutcome.Deny)]
    public void OnlyAllowCanAuthorizeValueMovement(RiskOutcome outcome)
    {
        var context = Context();

        FluentActions.Invoking(() => new RiskDecisionAuthorizer().AuthorizeValueMovement(
                Decision(outcome, context), context, Time))
            .Should().Throw<RiskAuthorizationDeniedException>();
    }

    [Fact]
    public void MissingFutureExpiredAndBindingMismatchedDecisionsFailClosed()
    {
        var context = Context();
        var authorizer = new RiskDecisionAuthorizer();
        FluentActions.Invoking(() => authorizer.AuthorizeValueMovement(null!, context, Time))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => authorizer.AuthorizeValueMovement(
                Decision(RiskOutcome.Allow, context) with { IssuedAt = Time.AddSeconds(1) }, context, Time))
            .Should().Throw<RiskDecisionExpiredException>();
        FluentActions.Invoking(() => authorizer.AuthorizeValueMovement(
                Decision(RiskOutcome.Allow, context) with { ExpiresAt = Time }, context, Time))
            .Should().Throw<RiskDecisionExpiredException>();

        var mismatches = new[]
        {
            context with { ActorId = Guid.NewGuid() },
            context with { Amount = new CoinAmount(CurrencyCode.HardCoin, 11) },
            context with { DestinationWalletId = WalletId.New() },
            context with { ProviderReferenceHash = "provider-other" },
            context with { PolicyVersion = new PolicyVersion(2) },
            context with { ReserveVersion = new ReserveVersion(2) },
            context with { FeatureVersion = 2 },
            context with { KillSwitchEpoch = 2 },
            context with { CounterVersion = 2 },
            context with { EntityGraphVersion = 2 },
            context with { EntityGraphEvidenceHash = "graph-other" },
            context with { SourceRoots = [SourceStampId.New()] }
        };
        foreach (var mismatch in mismatches)
        {
            FluentActions.Invoking(() => new RiskDecisionAuthorizer().AuthorizeValueMovement(
                    Decision(RiskOutcome.Allow, context), mismatch, Time))
                .Should().Throw<RiskDecisionBindingException>();
        }
    }

    [Fact]
    public void HoldOutcomeCanOnlyCreateNonspendableHold()
    {
        var context = Context();
        var coordinator = new RiskHoldCoordinator();
        var result = coordinator.CreateHold(Decision(RiskOutcome.Hold, context), context, Time);

        result.Status.Should().Be(HoldStatus.Active);
        result.WalletId.Should().Be(context.SourceWalletId);
        FluentActions.Invoking(() => coordinator.CreateHold(
                Decision(RiskOutcome.Allow, context), context, Time))
            .Should().Throw<RiskAuthorizationDeniedException>();
    }

    private static ProtectedOperationContext Context() =>
        new(
            new IdempotencyKey("risk-command"),
            Guid.NewGuid(),
            PostingTemplateKind.Spend,
            WalletId.New(),
            WalletId.New(),
            new CoinAmount(CurrencyCode.HardCoin, 10),
            [new RiskCurrencyLeg(CurrencyCode.HardCoin, 10)],
            [SourceStampId.New()],
            "provider-hash",
            new PolicyVersion(1),
            new ReserveVersion(1),
            1,
            1,
            1,
            "graph-hash");

    private static RiskDecisionSnapshot Decision(RiskOutcome outcome, ProtectedOperationContext context) =>
        RiskDecisionSnapshot.Create(
            Guid.NewGuid(), outcome, context, Time.AddSeconds(-1), Time.AddMinutes(5),
            [RiskReasonCode.WithinLimits]);
}
