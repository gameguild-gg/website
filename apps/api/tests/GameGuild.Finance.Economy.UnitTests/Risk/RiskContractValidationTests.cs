using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.UnitTests.Risk;

public sealed class RiskContractValidationTests
{
    private static readonly DateTimeOffset Time = new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CurrencyLegAndEntityNodeRejectUnknownEnumValues()
    {
        FluentActions.Invoking(() => new RiskCurrencyLeg((CurrencyCode)99, 1))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new RiskEntityNode((RiskEntityType)99, "hash"))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void DecisionFactoryRejectsInvalidIdentityOutcomeLifetimeAndReasons()
    {
        var context = Context();
        FluentActions.Invoking(() => RiskDecisionSnapshot.Create(
                Guid.Empty, RiskOutcome.Allow, context, Time, Time.AddMinutes(1), [RiskReasonCode.WithinLimits]))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => RiskDecisionSnapshot.Create(
                Guid.NewGuid(), (RiskOutcome)99, context, Time, Time.AddMinutes(1), [RiskReasonCode.WithinLimits]))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => RiskDecisionSnapshot.Create(
                Guid.NewGuid(), RiskOutcome.Allow, context, Time, Time, [RiskReasonCode.WithinLimits]))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => RiskDecisionSnapshot.Create(
                Guid.NewGuid(), RiskOutcome.Allow, context, Time, Time.AddMinutes(1), []))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => RiskDecisionSnapshot.Create(
                Guid.NewGuid(), RiskOutcome.Allow, context, Time, Time.AddMinutes(1), [(RiskReasonCode)99]))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GraphRejectsSelfLinksAndUnlinkedClusterUsesDeterministicEvidence()
    {
        var graph = new EntityRiskGraph();
        var node = new RiskEntityNode(RiskEntityType.Account, "account-hash");

        FluentActions.Invoking(() => graph.Link(node, node, "evidence", Time))
            .Should().Throw<ArgumentException>();
        var cluster = graph.ClusterFor(node);
        cluster.Version.Should().Be(0);
        cluster.Id.Should().Be(cluster.EvidenceHash);
    }

    [Fact]
    public void ReservationRejectsInvalidIdentityOperationAndLifetime()
    {
        var store = new AggregateRiskLimitStore();
        var cluster = new EntityRiskCluster("cluster", 1, "evidence", []);
        var amount = new CoinAmount(CurrencyCode.HardCoin, 1);

        FluentActions.Invoking(() => store.Reserve(
                Guid.Empty, cluster, PostingTemplateKind.Spend, amount, 10, Time, Time.AddMinutes(1)))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.Reserve(
                Guid.NewGuid(), cluster, (PostingTemplateKind)99, amount, 10, Time, Time.AddMinutes(1)))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => store.Reserve(
                Guid.NewGuid(), cluster, PostingTemplateKind.Spend, amount, 10, Time, Time))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CooldownRejectsInvalidIdentityKindAndDuration()
    {
        var registry = new ProtectedChangeCooldownRegistry();
        FluentActions.Invoking(() => registry.Record(
                Guid.Empty, ProtectedChangeKind.PayoutDestination, "hash", Time, TimeSpan.FromHours(1)))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => registry.Record(
                Guid.NewGuid(), (ProtectedChangeKind)99, "hash", Time, TimeSpan.FromHours(1)))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => registry.Record(
                Guid.NewGuid(), ProtectedChangeKind.PayoutDestination, "hash", Time, TimeSpan.Zero))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ReviewLedgerRejectsInvalidIdentityEvidenceDuplicateAndReviewer()
    {
        var ledger = new RiskReviewLedger();
        var decision = ReviewDecision();
        var submitter = Guid.NewGuid();
        FluentActions.Invoking(() => ledger.Submit(Guid.Empty, decision, submitter, ["evidence"], Time))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => ledger.Submit(Guid.NewGuid(), decision, Guid.Empty, ["evidence"], Time))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => ledger.Submit(Guid.NewGuid(), decision, submitter, [], Time))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => ledger.Submit(Guid.NewGuid(), decision, submitter, [" "], Time))
            .Should().Throw<ArgumentException>();

        var id = Guid.NewGuid();
        ledger.Submit(id, decision, submitter, ["evidence"], Time);
        FluentActions.Invoking(() => ledger.Submit(id, decision, submitter, ["evidence"], Time))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => ledger.Approve(id, Guid.Empty, "approved", Time.AddMinutes(1)))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AuditRecordContainsOnlyHashesAndPublicDecisionIsRedacted()
    {
        var context = Context();
        var decision = RiskDecisionSnapshot.Create(
            Guid.NewGuid(), RiskOutcome.Review, context, Time, Time.AddMinutes(1),
            [RiskReasonCode.ManualReviewRequired]);

        var audit = RiskDecisionAuditRecord.Create(decision, context, Time);
        var publicView = audit.ToPublicView();

        audit.ActorHash.Contains(context.ActorId.ToString(), StringComparison.OrdinalIgnoreCase).Should().BeFalse();
        audit.SourceWalletHash.Contains(context.SourceWalletId.Value.ToString(), StringComparison.OrdinalIgnoreCase)
            .Should().BeFalse();
        audit.OperationFingerprint.Should().Be(context.Fingerprint());
        audit.ReasonCodes.Should().Equal(RiskReasonCode.ManualReviewRequired);
        audit.DestinationWalletHash.Should().HaveLength(64);
        audit.ProviderReferenceHash.Should().Be(context.ProviderReferenceHash);
        audit.EntityGraphEvidenceHash.Should().Be(context.EntityGraphEvidenceHash);
        publicView.Should().Be(new PublicRiskDecision(decision.Id, RiskOutcome.Review, Time));
    }

    private static RiskDecisionSnapshot ReviewDecision()
    {
        var context = Context();
        return RiskDecisionSnapshot.Create(
            Guid.NewGuid(), RiskOutcome.Review, context, Time, Time.AddMinutes(1),
            [RiskReasonCode.ManualReviewRequired]);
    }

    private static ProtectedOperationContext Context() =>
        new(
            new IdempotencyKey("validation"), Guid.NewGuid(), PostingTemplateKind.Spend,
            WalletId.New(), WalletId.New(), new CoinAmount(CurrencyCode.HardCoin, 1),
            [new RiskCurrencyLeg(CurrencyCode.HardCoin, 1)], [SourceStampId.New()], "provider-hash",
            new PolicyVersion(1), new ReserveVersion(1), 1, 1, 1, "graph-hash");
}
