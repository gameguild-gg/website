using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.AdRewards.UnitTests;

public sealed class AdRewardDeferredFailureTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-18T12:00:00Z");
    private static readonly byte[] Secret = Enumerable.Range(70, 32).Select(value => (byte)value).ToArray();

    [Fact]
    public void ConfirmDeferred_RejectsNullMissingAndConflictingIdempotency()
    {
        var missingHarness = DeferredHarness();
        FluentActions.Invoking(() => missingHarness.Coordinator.ConfirmDeferred(null!))
            .Should().Throw<ArgumentNullException>();
        var neverPending = missingHarness.Command(includeAuthorization: false, includeProof: false);
        var missingReport = Verified("unity", [neverPending.Claims.SessionId]);
        FluentActions.Invoking(() => missingHarness.Coordinator.ConfirmDeferred(
                missingHarness.DeferredConfirmation(neverPending, missingReport)))
            .Should().Throw<AdRewardReplayException>();

        var conflictHarness = DeferredHarness();
        var first = conflictHarness.Command(idempotency: "used", includeAuthorization: false, includeProof: false);
        var second = conflictHarness.Command(idempotency: "pending", includeAuthorization: false, includeProof: false);
        conflictHarness.Coordinator.Complete(first);
        conflictHarness.Coordinator.Complete(second);
        var report = Verified("unity", [second.Claims.SessionId]);
        var confirmation = conflictHarness.DeferredConfirmation(second, report) with
        {
            IdempotencyKey = first.IdempotencyKey
        };
        FluentActions.Invoking(() => conflictHarness.Coordinator.ConfirmDeferred(confirmation))
            .Should().Throw<AdRewardIdempotencyConflictException>();
    }

    [Fact]
    public void ConfirmDeferred_RejectsWrongNetworkMissingSessionAndFutureImport()
    {
        AssertReportRejected((command, _) => Verified("other", [command.Claims.SessionId]));
        AssertReportRejected((_, _) => Verified("unity", [Guid.NewGuid()]));
        AssertReportRejected((command, _) => Verified(
            "unity", [command.Claims.SessionId], importedAt: Now.AddHours(3)));
    }

    [Fact]
    public void ConfirmDeferred_RejectsStaleVerifiedReport()
    {
        var harness = DeferredHarness();
        var pending = harness.Command(includeAuthorization: false, includeProof: false);
        harness.Coordinator.Complete(pending);
        var report = Verified("unity", [pending.Claims.SessionId]);
        var staleAt = report.PeriodEnd.AddHours(24).AddTicks(1);
        var confirmation = harness.DeferredConfirmation(pending, report) with
        {
            ConfirmedAt = staleAt,
            Dependencies = AdRewardDependencySnapshot.Healthy(staleAt.AddMinutes(-1), staleAt.AddMinutes(1))
        };

        FluentActions.Invoking(() => harness.Coordinator.ConfirmDeferred(confirmation))
            .Should().Throw<AdRewardDependencyUnavailableException>();
    }

    [Fact]
    public void ConfirmDeferred_AccumulatesSubUnitRewardWithoutPosting()
    {
        var policy = Policy("unity", AdRewardIssuanceMode.DeferredReport, ecpm: 1, share: 1, buffer: 999_998);
        var harness = new AdRewardCoordinatorTests.Harness(policy: policy);
        var pending = harness.Command(includeAuthorization: false, includeProof: false);
        harness.Coordinator.Complete(pending);
        var report = Verified("unity", [pending.Claims.SessionId]);

        var result = harness.Coordinator.ConfirmDeferred(harness.DeferredConfirmation(pending, report));

        result.State.Should().Be(AdRewardCompletionState.AccumulatedRemainder);
        result.Quote!.RewardSoftUnits.Should().Be(0);
        result.Issuance.Should().BeNull();
        harness.Store.JournalEntries.Should().BeEmpty();
        harness.Coordinator.PendingClaims.Should().BeEmpty();
        harness.Coordinator.Attributions.Should().ContainSingle().Which.ProviderBatchId.Should().Be(report.BatchId);
    }

    private static void AssertReportRejected(
        Func<AdRewardCompletionCommand, AdRewardCoordinatorTests.Harness, VerifiedAdProviderReport> reportFactory)
    {
        var harness = DeferredHarness();
        var pending = harness.Command(includeAuthorization: false, includeProof: false);
        harness.Coordinator.Complete(pending);
        var confirmation = harness.DeferredConfirmation(pending, reportFactory(pending, harness));
        FluentActions.Invoking(() => harness.Coordinator.ConfirmDeferred(confirmation))
            .Should().Throw<AdProviderReportVerificationException>();
    }

    private static AdRewardCoordinatorTests.Harness DeferredHarness() =>
        new(policy: Policy("unity", AdRewardIssuanceMode.DeferredReport));

    private static VerifiedAdProviderReport Verified(
        string network,
        IReadOnlyList<Guid> sessions,
        DateTimeOffset? importedAt = null)
    {
        var reports = new HmacAdProviderReportService(network, Secret);
        var store = new AdNetworkPolicyStore();
        store.Publish(Policy(network, AdRewardIssuanceMode.DeferredReport));
        var reconciler = new AdRewardReconciler(store, reports);
        var report = reports.Sign(
            $"report-{Guid.NewGuid():N}", 1, $"batch-{Guid.NewGuid():N}",
            Now, Now.AddHours(1), 1_000, sessions, "evidence", importedAt ?? Now.AddHours(2));
        return reconciler.Import(report, [], (importedAt ?? Now.AddHours(2)).AddTicks(1)).VerifiedReport;
    }

    private static AdNetworkPolicy Policy(
        string network,
        AdRewardIssuanceMode mode,
        long ecpm = 2_000_000_000,
        int share = 700_000,
        int buffer = 200_000) => new(
        network, new PolicyVersion(1), Now.AddHours(-1), Now.AddHours(1), mode,
        AdNetworkYieldState.Trailing, ecpm, share, buffer, 900_000,
        TimeSpan.FromSeconds(3), 1_000, Now, TimeSpan.FromHours(24), 100);
}
