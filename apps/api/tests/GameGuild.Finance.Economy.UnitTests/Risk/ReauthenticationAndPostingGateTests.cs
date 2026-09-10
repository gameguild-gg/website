using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Reserves;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.UnitTests.Risk;

public sealed class ReauthenticationAndPostingGateTests
{
    private static readonly DateTimeOffset Time = new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FreshReauthenticationMustBindEveryProtectedOperationInput()
    {
        var actor = Guid.NewGuid();
        var evidence = Evidence(actor);

        ReauthenticationEvidenceValidator.RequireFresh(
            evidence, actor, ProtectedOperationKind.Payout, "transaction", ReauthenticationAssurance.MultiFactor, Time)
            .Should().Be(evidence);
        FluentActions.Invoking(() => ReauthenticationEvidenceValidator.RequireFresh(
                evidence, Guid.NewGuid(), ProtectedOperationKind.Payout, "transaction",
                ReauthenticationAssurance.MultiFactor, Time))
            .Should().Throw<ReauthenticationEvidenceException>();
        FluentActions.Invoking(() => ReauthenticationEvidenceValidator.RequireFresh(
                evidence, actor, ProtectedOperationKind.OwnershipTransfer, "transaction",
                ReauthenticationAssurance.MultiFactor, Time))
            .Should().Throw<ReauthenticationEvidenceException>();
        FluentActions.Invoking(() => ReauthenticationEvidenceValidator.RequireFresh(
                evidence, actor, ProtectedOperationKind.Payout, "different",
                ReauthenticationAssurance.MultiFactor, Time))
            .Should().Throw<ReauthenticationEvidenceException>();
        FluentActions.Invoking(() => ReauthenticationEvidenceValidator.RequireFresh(
                evidence, actor, ProtectedOperationKind.Payout, "transaction",
                ReauthenticationAssurance.HardwareBound, Time))
            .Should().Throw<ReauthenticationEvidenceException>();
        FluentActions.Invoking(() => ReauthenticationEvidenceValidator.RequireFresh(
                evidence with { ExpiresAt = Time }, actor, ProtectedOperationKind.Payout, "transaction",
                ReauthenticationAssurance.MultiFactor, Time))
            .Should().Throw<ReauthenticationEvidenceException>();
    }

    [Fact]
    public void CorePostingGateFailsClosedUntilSchemaAndCountersAreVerified()
    {
        var context = Context();
        var decision = Decision(context);
        var gate = new CoreProtectedPostingGate(new RiskDecisionAuthorizer());
        var command = new ProtectedPostingCommand(
            context.Operation, new RiskDecisionId(decision.Id), context);
        var reserveAuthorization = new ReservePostingAuthorization(
            context.ReserveVersion, context.ReserveAuthorizationEpoch, Time);

        FluentActions.Invoking(() => gate.Authorize(
                command, decision, RiskPersistenceReadiness.NotReady, reserveAuthorization, Time))
            .Should().Throw<RiskPersistenceNotReadyException>();
        FluentActions.Invoking(() => gate.Authorize(
                command, decision, new(true, true), null!, Time))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => gate.Authorize(
                command with { RiskDecisionId = null }, decision, new(true, true), reserveAuthorization, Time))
            .Should().Throw<MissingRiskDecisionException>();
        FluentActions.Invoking(() => gate.Authorize(
                command with { RiskDecisionId = new RiskDecisionId(Guid.NewGuid()) }, decision,
                new(true, true), reserveAuthorization, Time))
            .Should().Throw<RiskDecisionBindingException>();
        FluentActions.Invoking(() => gate.Authorize(
                command with { Operation = PostingTemplateKind.Burn }, decision, new(true, true),
                reserveAuthorization, Time))
            .Should().Throw<RiskDecisionBindingException>();
        FluentActions.Invoking(() => gate.Authorize(
                command, decision, new(true, true),
                new ReservePostingAuthorization(new ReserveVersion(2), context.ReserveAuthorizationEpoch, Time), Time))
            .Should().Throw<ReserveAuthorizationException>();
        FluentActions.Invoking(() => gate.Authorize(
                command, decision, new(true, true),
                new ReservePostingAuthorization(context.ReserveVersion, 2, Time), Time))
            .Should().Throw<ReserveAuthorizationEpochException>();

        gate.Authorize(command, decision, new(true, true), reserveAuthorization, Time)
            .DecisionId.Should().Be(decision.Id);
    }

    [Fact]
    public void RiskDecisionIdentifierRejectsEmptyValue()
    {
        FluentActions.Invoking(() => new RiskDecisionId(Guid.Empty)).Should().Throw<ArgumentException>();
        new RiskPersistenceReadiness(true, false).IsReady.Should().BeFalse();
    }

    private static ReauthenticationEvidence Evidence(Guid actor) =>
        new(
            actor, ProtectedOperationKind.Payout, "transaction", ReauthenticationAssurance.MultiFactor,
            Time.AddMinutes(-1), Time.AddMinutes(5), "evidence-hash");

    private static RiskDecisionSnapshot Decision(ProtectedOperationContext context) =>
        RiskDecisionSnapshot.Create(
            Guid.NewGuid(), RiskOutcome.Allow, context, Time.AddSeconds(-1), Time.AddMinutes(1),
            [RiskReasonCode.WithinLimits]);

    private static ProtectedOperationContext Context() =>
        new(
            new IdempotencyKey("protected-posting"), Guid.NewGuid(), PostingTemplateKind.PayoutReservation,
            WalletId.New(), WalletId.New(), new CoinAmount(CurrencyCode.HardCoin, 10),
            [new RiskCurrencyLeg(CurrencyCode.HardCoin, 10)], [SourceStampId.New()], "provider",
            new PolicyVersion(1), new ReserveVersion(1), 1, 1, 1, "graph", 1, 1);
}
