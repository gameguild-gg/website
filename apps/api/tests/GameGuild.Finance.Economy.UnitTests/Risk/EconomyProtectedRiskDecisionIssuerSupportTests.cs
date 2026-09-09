using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.UnitTests.Risk;

public sealed class EconomyProtectedRiskDecisionIssuerSupportTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 16, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.Parse("9a000000-0000-0000-0000-000000000001");
    private static readonly Guid ActorId = Guid.Parse("9a000000-0000-0000-0000-000000000002");

    [Fact]
    public void AssessmentMapsEveryFailClosedEvidenceShape()
    {
        var allow = Evidence(ExternalRiskSource.FinancialCrime, ExternalRiskOutcome.Allow);
        var trust = Evidence(ExternalRiskSource.TrustSafety, ExternalRiskOutcome.Allow);

        EconomyProtectedRiskDecisionIssuerSupport.Assess([allow], Now).State
            .Should().Be(EconomyProtectedOperationState.ComplianceUnavailable);
        EconomyProtectedRiskDecisionIssuerSupport.Assess(
                [allow, allow with { EvidenceHash = "duplicate-source" }], Now).State
            .Should().Be(EconomyProtectedOperationState.ComplianceUnavailable);
        EconomyProtectedRiskDecisionIssuerSupport.Assess(
                [allow, trust with { ExpiresAt = Now }], Now).State
            .Should().Be(EconomyProtectedOperationState.ComplianceStale);

        var hold = EconomyProtectedRiskDecisionIssuerSupport.Assess(
            [allow, trust with { Outcome = ExternalRiskOutcome.Deny }], Now);
        hold.Outcome.Should().Be(RiskOutcome.Hold);
        hold.State.Should().Be(EconomyProtectedOperationState.Hold);

        var denied = EconomyProtectedRiskDecisionIssuerSupport.Assess(
            [allow, trust with { Outcome = (ExternalRiskOutcome)999 }], Now);
        denied.Outcome.Should().Be(RiskOutcome.Deny);
        denied.State.Should().Be(EconomyProtectedOperationState.Denied);

        EconomyProtectedRiskDecisionIssuerSupport.Assess([allow, trust], Now).State
            .Should().Be(EconomyProtectedOperationState.Ready);
    }

    [Fact]
    public void MaterializeLimitsCoversEveryServerDerivedSubject()
    {
        var rules = Enum.GetValues<EconomyRiskLimitSubject>()
            .Select((subject, index) => new EconomyProtectedRiskLimitRule(
                (RiskLimitDimension)(index + 1),
                subject,
                3,
                1_000,
                TimeSpan.FromHours(1)))
            .ToArray();
        var request = Request();

        var limits = EconomyProtectedRiskDecisionIssuerSupport.MaterializeLimits(
            new EconomyProtectedRiskPolicy(TimeSpan.FromMinutes(1), 1, TimeSpan.FromHours(1), 3, rules),
            request,
            new EntityRiskCluster("cluster-id", 4, "cluster-evidence", []));

        limits.Should().HaveCount(rules.Length + 1);
        limits.Select(limit => limit.Key).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void MaterializeLimitsRejectsEmptyDuplicateAndUnknownSubjects()
    {
        var request = Request();
        var cluster = new EntityRiskCluster("cluster-id", 4, "cluster-evidence", []);
        var noLimits = new EconomyProtectedRiskPolicy(
            TimeSpan.FromMinutes(1), 1, TimeSpan.FromHours(1), 3, []);
        var rootRule = new EconomyProtectedRiskLimitRule(
            RiskLimitDimension.SourceRoot,
            EconomyRiskLimitSubject.SourceRoot,
            3,
            1_000,
            TimeSpan.FromHours(1));
        var duplicateRoots = request with
        {
            Intent = request.Intent with { SourceRoots = [request.Intent.SourceRoots[0], request.Intent.SourceRoots[0]] }
        };
        var unknownSubject = new EconomyProtectedRiskPolicy(
            TimeSpan.FromMinutes(1),
            1,
            TimeSpan.FromHours(1),
            3,
            [rootRule with { Subject = (EconomyRiskLimitSubject)999 }]);

        FluentActions.Invoking(() => EconomyProtectedRiskDecisionIssuerSupport.MaterializeLimits(
                noLimits, request, cluster))
            .Should().Throw<EconomyProtectedRiskPolicyException>();
        FluentActions.Invoking(() => EconomyProtectedRiskDecisionIssuerSupport.MaterializeLimits(
                new EconomyProtectedRiskPolicy(
                    TimeSpan.FromMinutes(1), 1, TimeSpan.FromHours(1), 3, [rootRule]),
                duplicateRoots,
                cluster))
            .Should().Throw<EconomyProtectedRiskPolicyException>();
        FluentActions.Invoking(() => EconomyProtectedRiskDecisionIssuerSupport.MaterializeLimits(
                unknownSubject, request, cluster))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    private static ExternalRiskEvidence Evidence(
        ExternalRiskSource source,
        ExternalRiskOutcome outcome) => new(
        source,
        1,
        Now.AddMinutes(-1),
        Now.AddMinutes(5),
        outcome,
        $"evidence-{source}",
        true);

    private static EconomyProtectedRiskDecisionRequest Request() => new(
        TenantId,
        ActorId,
        EconomySubjectReference.ForUser(TenantId, ActorId),
        "BRA",
        "jurisdiction-evidence",
        "operation-fingerprint",
        new EconomyProtectedOperationIntent(
            EconomyValueMovementCapability.Transfer,
            PostingTemplateKind.Spend,
            WalletId.New(),
            WalletId.New(),
            new CoinAmount(CurrencyCode.HardCoin, 100),
            [new RiskCurrencyLeg(CurrencyCode.HardCoin, 100)],
            [SourceStampId.New(), SourceStampId.New()],
            "provider-hash",
            "destination-hash",
            new IdempotencyKey("support-tests"),
            Now));
}
