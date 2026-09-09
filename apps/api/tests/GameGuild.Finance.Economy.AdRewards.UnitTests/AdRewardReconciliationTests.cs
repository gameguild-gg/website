using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.AdRewards.UnitTests;

public sealed class AdRewardReconciliationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-18T12:00:00Z");
    private static readonly byte[] ReportSecret = Enumerable.Range(70, 32).Select(value => (byte)value).ToArray();

    [Fact]
    public void Import_ReconcilesVarianceAndChangesFuturePolicyOnly()
    {
        var policies = StoreWithPolicy();
        var reports = new HmacAdProviderReportService("unity", ReportSecret);
        var reconciler = new AdRewardReconciler(policies, reports);
        var attributions = Attributions();
        var report = reports.Sign(
            "report-1", 1, "batch-1", Now, Now.AddHours(1),
            3_000_000, attributions.Select(item => item.SessionId).ToArray(),
            "provider-report-evidence", Now.AddHours(2));

        var imported = reconciler.Import(report, attributions, Now.AddHours(2).AddSeconds(1));

        imported.Reconciliation.EstimatedRevenueUsdNanos.Should().Be(4_000_000);
        imported.Reconciliation.ActualRevenueUsdNanos.Should().Be(3_000_000);
        imported.Reconciliation.VarianceUsdNanos.Should().Be(-1_000_000);
        imported.Reconciliation.HistoricalRewardSoftUnits.Should().Be(224);
        imported.FuturePolicy.Version.Should().Be(new PolicyVersion(2));
        imported.FuturePolicy.EstimatedNetEcpmUsdNanos.Should().Be(1_500_000_000);
        imported.FuturePolicy.SafetyBufferPpm.Should().Be(250_000);
        imported.FuturePolicy.Ranking.Should().Be(90);
        attributions.Select(item => item.RewardSoftUnits).Should().Equal(112, 112);
        reconciler.Import(report, attributions, Now.AddHours(2).AddSeconds(1)).Should().Be(imported);
    }

    [Fact]
    public void Import_AcceptsForwardCorrectionAndRejectsDuplicateOrRegressingVersions()
    {
        var policies = StoreWithPolicy();
        var reports = new HmacAdProviderReportService("unity", ReportSecret);
        var reconciler = new AdRewardReconciler(policies, reports);
        var attributions = Attributions();
        var first = reports.Sign(
            "report-1", 1, "batch-1", Now, Now.AddHours(1),
            3_000_000, attributions.Select(item => item.SessionId).ToArray(),
            "evidence-v1", Now.AddHours(2));
        reconciler.Import(first, attributions, Now.AddHours(2).AddSeconds(1));
        var correction = reports.Sign(
            "report-1", 2, "batch-1", Now, Now.AddHours(1),
            4_500_000, attributions.Select(item => item.SessionId).ToArray(),
            "evidence-v2", Now.AddHours(3));

        var corrected = reconciler.Import(correction, attributions, Now.AddHours(3).AddSeconds(1));

        corrected.Reconciliation.PreviousActualRevenueUsdNanos.Should().Be(3_000_000);
        corrected.Reconciliation.ActualDeltaUsdNanos.Should().Be(1_500_000);
        corrected.Reconciliation.VarianceUsdNanos.Should().Be(500_000);
        corrected.FuturePolicy.Version.Should().Be(new PolicyVersion(3));
        corrected.FuturePolicy.EstimatedNetEcpmUsdNanos.Should().Be(2_250_000_000);
        corrected.FuturePolicy.SafetyBufferPpm.Should().Be(240_000);
        corrected.FuturePolicy.Ranking.Should().Be(95);

        var conflicting = reports.Sign(
            "report-1", 2, "batch-1", Now, Now.AddHours(1),
            4_600_000, attributions.Select(item => item.SessionId).ToArray(),
            "conflicting-v2", Now.AddHours(3));
        FluentActions.Invoking(() => reconciler.Import(conflicting, attributions, Now.AddHours(3).AddSeconds(1)))
            .Should().Throw<AdProviderReportConflictException>();
        var skippedVersion = reports.Sign(
            "report-1", 4, "batch-1", Now, Now.AddHours(1), 4_700_000,
            attributions.Select(item => item.SessionId).ToArray(), "evidence-v4", Now.AddHours(4));
        FluentActions.Invoking(() => reconciler.Import(skippedVersion, attributions, Now.AddHours(4).AddSeconds(1)))
            .Should().Throw<AdProviderReportConflictException>();
    }

    [Fact]
    public void Import_RejectsInvalidSignatureAndBatchDoubleReconciliation()
    {
        var policies = StoreWithPolicy();
        var reports = new HmacAdProviderReportService("unity", ReportSecret);
        var reconciler = new AdRewardReconciler(policies, reports);
        var attributions = Attributions();
        var report = reports.Sign(
            "report-1", 1, "batch-1", Now, Now.AddHours(1), 3_000_000,
            attributions.Select(item => item.SessionId).ToArray(), "evidence", Now.AddHours(2));

        FluentActions.Invoking(() => reconciler.Import(
                report with { Signature = report.Signature + "x" }, attributions, Now.AddHours(2).AddSeconds(1)))
            .Should().Throw<AdProviderReportVerificationException>();
        reconciler.Import(report, attributions, Now.AddHours(2).AddSeconds(1));
        var alias = reports.Sign(
            "other-report", 1, "batch-1", Now, Now.AddHours(1), 3_000_000,
            attributions.Select(item => item.SessionId).ToArray(), "other", Now.AddHours(2));
        FluentActions.Invoking(() => reconciler.Import(alias, attributions, Now.AddHours(2).AddSeconds(1)))
            .Should().Throw<AdProviderReportConflictException>();
    }

    [Fact]
    public void VerifiedReport_ConfirmsDeferredClaimExactlyOnce()
    {
        var deferredPolicy = Policy(AdRewardIssuanceMode.DeferredReport);
        var harness = new AdRewardCoordinatorTests.Harness(policy: deferredPolicy);
        var pendingCommand = harness.Command(includeAuthorization: false, includeProof: false);
        harness.Coordinator.Complete(pendingCommand).State.Should().Be(AdRewardCompletionState.PendingProviderReport);
        var reportService = new HmacAdProviderReportService("unity", ReportSecret);
        var reconciler = new AdRewardReconciler(harness.Policies, reportService);
        var report = reportService.Sign(
            "deferred-report", 1, "deferred-batch", Now, Now.AddHours(1),
            2_000_000, [pendingCommand.Claims.SessionId], "verified-session", Now.AddHours(2));
        var imported = reconciler.Import(report, [], Now.AddHours(2).AddSeconds(1));
        var confirmation = harness.DeferredConfirmation(pendingCommand, imported.VerifiedReport);

        var issued = harness.Coordinator.ConfirmDeferred(confirmation);

        issued.State.Should().Be(AdRewardCompletionState.Issued);
        issued.Quote!.RewardSoftUnits.Should().Be(112);
        harness.Store.JournalEntries.Should().ContainSingle();
        harness.Coordinator.PendingClaims.Should().BeEmpty();
        harness.Coordinator.ConfirmDeferred(confirmation).Should().Be(issued);
    }

    private static AdNetworkPolicyStore StoreWithPolicy()
    {
        var store = new AdNetworkPolicyStore();
        store.Publish(Policy());
        return store;
    }

    private static AdNetworkPolicy Policy(
        AdRewardIssuanceMode mode = AdRewardIssuanceMode.ImmediateProviderProof) => new(
        "unity", new PolicyVersion(1), Now.AddHours(-1), Now.AddHours(1), mode,
        AdNetworkYieldState.Trailing, 2_000_000_000, 700_000, 200_000, 900_000,
        TimeSpan.FromSeconds(3), 1_000, Now, TimeSpan.FromHours(24), 100);

    private static AdRewardAttribution[] Attributions() =>
    [
        new(Guid.Parse("61000000-0000-0000-0000-000000000006"), "unity", new PolicyVersion(1),
            "batch-1", 2_000_000, 112, Now.AddMinutes(10)),
        new(Guid.Parse("62000000-0000-0000-0000-000000000006"), "unity", new PolicyVersion(1),
            "batch-1", 2_000_000, 112, Now.AddMinutes(20))
    ];
}
