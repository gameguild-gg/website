using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.AdRewards.UnitTests;

public sealed class AdRewardReconciliationValidationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-18T12:00:00Z");
    private static readonly byte[] Secret = Enumerable.Range(70, 32).Select(value => (byte)value).ToArray();
    private static readonly Guid Session = Guid.Parse("71000000-0000-0000-0000-000000000007");

    [Fact]
    public void ReportService_RejectsEveryInvalidSigningInput()
    {
        FluentActions.Invoking(() => new HmacAdProviderReportService(" ", Secret)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new HmacAdProviderReportService("unity", null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new HmacAdProviderReportService("unity", new byte[31])).Should().Throw<ArgumentException>();
        var service = Service();
        AssertSignFailure(() => service.Sign(" ", 1, "batch", Now, Now.AddMinutes(10), 1, [Session], "hash", Now.AddMinutes(11)));
        AssertSignFailure(() => service.Sign("report", 0, "batch", Now, Now.AddMinutes(10), 1, [Session], "hash", Now.AddMinutes(11)));
        AssertSignFailure(() => service.Sign("report", 1, " ", Now, Now.AddMinutes(10), 1, [Session], "hash", Now.AddMinutes(11)));
        AssertSignFailure(() => service.Sign("report", 1, "batch", Now, Now, 1, [Session], "hash", Now.AddMinutes(11)));
        AssertSignFailure(() => service.Sign("report", 1, "batch", Now, Now.AddMinutes(10), 1, [Session], "hash", Now.AddMinutes(9)));
        AssertSignFailure(() => service.Sign("report", 1, "batch", Now, Now.AddMinutes(10), -1, [Session], "hash", Now.AddMinutes(11)));
        AssertSignFailure(() => service.Sign("report", 1, "batch", Now, Now.AddMinutes(10), 1, null!, "hash", Now.AddMinutes(11)));
        AssertSignFailure(() => service.Sign("report", 1, "batch", Now, Now.AddMinutes(10), 1, [], "hash", Now.AddMinutes(11)));
        AssertSignFailure(() => service.Sign("report", 1, "batch", Now, Now.AddMinutes(10), 1, [Guid.Empty], "hash", Now.AddMinutes(11)));
        AssertSignFailure(() => service.Sign("report", 1, "batch", Now, Now.AddMinutes(10), 1, [Session, Session], "hash", Now.AddMinutes(11)));
        AssertSignFailure(() => service.Sign("report", 1, "batch", Now, Now.AddMinutes(10), 1, [Session], " ", Now.AddMinutes(11)));
    }

    [Fact]
    public void ReportService_VerifyRejectsEveryMalformedOrUnboundReport()
    {
        var service = Service();
        var report = Report(service);
        FluentActions.Invoking(() => service.Verify(null!, Now.AddMinutes(12))).Should().Throw<ArgumentNullException>();
        service.Verify(report, Now.AddMinutes(12)).Should().BeTrue();
        service.Verify(report with { Network = "other" }, Now.AddMinutes(12)).Should().BeFalse();
        service.Verify(report with { Version = 0 }, Now.AddMinutes(12)).Should().BeFalse();
        service.Verify(report with { PeriodEnd = report.PeriodStart }, Now.AddMinutes(12)).Should().BeFalse();
        service.Verify(report with { ImportedAt = report.PeriodEnd.AddTicks(-1) }, Now.AddMinutes(12)).Should().BeFalse();
        service.Verify(report with { ImportedAt = Now.AddMinutes(13) }, Now.AddMinutes(12)).Should().BeFalse();
        service.Verify(report with { ActualRevenueUsdNanos = -1 }, Now.AddMinutes(12)).Should().BeFalse();
        service.Verify(report with { VerifiedSessionIds = [] }, Now.AddMinutes(12)).Should().BeFalse();
        service.Verify(report with { VerifiedSessionIds = [Guid.Empty] }, Now.AddMinutes(12)).Should().BeFalse();
        service.Verify(report with { VerifiedSessionIds = [Session, Session] }, Now.AddMinutes(12)).Should().BeFalse();
        service.Verify(report with { Signature = "***" }, Now.AddMinutes(12)).Should().BeFalse();
        service.Verify(report with { Signature = Convert.ToBase64String(new byte[32]) }, Now.AddMinutes(12)).Should().BeFalse();
    }

    [Fact]
    public void Reconciler_ValidatesDependenciesArgumentsAndAuditProjection()
    {
        var policies = Store();
        var reports = Service();
        FluentActions.Invoking(() => new AdRewardReconciler(null!, reports)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new AdRewardReconciler(policies, null!)).Should().Throw<ArgumentNullException>();
        var reconciler = new AdRewardReconciler(policies, reports);
        reconciler.Reconciliations.Should().BeEmpty();
        FluentActions.Invoking(() => reconciler.Import(null!, [], Now)).Should().Throw<ArgumentNullException>();
        var report = Report(reports);
        FluentActions.Invoking(() => reconciler.Import(report, null!, Now.AddMinutes(12))).Should().Throw<ArgumentNullException>();

        var matching = Attribution(Session, "unity", "batch", 1_000, 10);
        var result = reconciler.Import(report, [
            matching,
            Attribution(Guid.NewGuid(), "other", "batch", 2_000, 20),
            Attribution(Guid.NewGuid(), "unity", "other-batch", 3_000, 30),
            Attribution(Guid.NewGuid(), "unity", "batch", 4_000, 40)
        ], Now.AddMinutes(12));

        result.VerifiedReport.Network.Should().Be("unity");
        result.VerifiedReport.ReportId.Should().Be("report");
        result.VerifiedReport.Version.Should().Be(1);
        result.VerifiedReport.BatchId.Should().Be("batch");
        result.VerifiedReport.PeriodEnd.Should().Be(Now.AddMinutes(10));
        result.VerifiedReport.ActualRevenueUsdNanos.Should().Be(1_000);
        result.VerifiedReport.VerifiedSessionIds.Should().Equal(Session);
        result.VerifiedReport.EvidenceHash.Should().Be("hash");
        result.VerifiedReport.ImportedAt.Should().Be(Now.AddMinutes(11));
        result.Reconciliation.Should().BeEquivalentTo(new AdRewardReconciliation(
            "unity", "report", 1, "batch", 1_000, 0, 1_000, 1_000, 0, 10, Now.AddMinutes(12)));
        result.Reconciliation.Network.Should().Be("unity");
        result.Reconciliation.ReportId.Should().Be("report");
        result.Reconciliation.Version.Should().Be(1);
        result.Reconciliation.BatchId.Should().Be("batch");
        reconciler.Reconciliations.Should().ContainSingle().Which.Should().Be(result.Reconciliation);
        matching.PolicyVersion.Should().Be(new PolicyVersion(1));
    }

    [Fact]
    public void Reconciler_HandlesNoMatchesAndUsesReportTimeBeforePolicyExpiry()
    {
        var policies = Store();
        var reports = Service();
        var reconciler = new AdRewardReconciler(policies, reports);
        var report = reports.Sign(
            "empty", 1, "empty-batch", Now, Now.AddMinutes(10), 0,
            [Session], "empty", Now.AddMinutes(20));

        var result = reconciler.Import(report, [], Now.AddMinutes(21));

        result.Reconciliation.EstimatedRevenueUsdNanos.Should().Be(0);
        result.Reconciliation.VarianceUsdNanos.Should().Be(0);
        result.FuturePolicy.EstimatedNetEcpmUsdNanos.Should().Be(2_000_000_000);
        result.FuturePolicy.SafetyBufferPpm.Should().Be(200_000);
        result.FuturePolicy.Ranking.Should().Be(100);
        result.FuturePolicy.EffectiveAt.Should().Be(Now.AddHours(1));
    }

    private static void AssertSignFailure(Action action) =>
        FluentActions.Invoking(action).Should().Throw<Exception>();

    private static HmacAdProviderReportService Service() => new("unity", Secret);

    private static AdProviderReport Report(HmacAdProviderReportService service) => service.Sign(
        "report", 1, "batch", Now, Now.AddMinutes(10), 1_000,
        [Session], "hash", Now.AddMinutes(11));

    private static AdRewardAttribution Attribution(
        Guid sessionId,
        string network,
        string batch,
        long revenue,
        long reward) => new(
        sessionId, network, new PolicyVersion(1), batch, revenue, reward, Now.AddMinutes(5));

    private static AdNetworkPolicyStore Store()
    {
        var store = new AdNetworkPolicyStore();
        store.Publish(new AdNetworkPolicy(
            "unity", new PolicyVersion(1), Now.AddHours(-1), Now.AddHours(1),
            AdRewardIssuanceMode.ImmediateProviderProof, AdNetworkYieldState.Trailing,
            2_000_000_000, 700_000, 200_000, 900_000, TimeSpan.FromSeconds(3),
            1_000, Now, TimeSpan.FromHours(24), 100));
        return store;
    }
}
