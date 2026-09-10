using FluentAssertions;
using GameGuild.Finance.Economy.Projections;

namespace GameGuild.Finance.Economy.UnitTests.Projections;

public sealed class ProjectionReconciliationTests
{
    [Fact]
    public void MismatchEnforcesLowerAvailabilityAndMovesWalletToReview()
    {
        var live = Projection(availableHard: 90, availableSoft: 70, withdrawable: 50, purchased: 100);
        var rebuilt = Projection(availableHard: 80, availableSoft: 75, withdrawable: 40, purchased: 95);

        var result = ProjectionReconciliationService.Reconcile(live, rebuilt);

        result.State.Should().Be(WalletReviewState.ReviewRequired);
        result.Enforced.AvailableHardToSpend.Should().Be(80);
        result.Enforced.AvailableSoftToSpend.Should().Be(70);
        result.Enforced.WithdrawableHard.Should().Be(40);
        result.Alerts.Select(alert => alert.Code).Should().BeEquivalentTo(
        [
            ProjectionReconciliationCode.ConfirmedCompositionMismatch,
            ProjectionReconciliationCode.HardAvailabilityMismatch,
            ProjectionReconciliationCode.SoftAvailabilityMismatch,
            ProjectionReconciliationCode.WithdrawableMismatch
        ]);
    }

    [Fact]
    public void MatchingProjectionRemainsHealthyWithoutAlerts()
    {
        var projection = Projection(20, 30, 10, 40);

        var result = ProjectionReconciliationService.Reconcile(projection, projection);

        result.State.Should().Be(WalletReviewState.Healthy);
        result.Enforced.Should().Be(projection);
        result.Alerts.Should().BeEmpty();
    }

    [Fact]
    public void PendingMismatchIsReportedAndNullInputsAreRejected()
    {
        var live = Projection(20, 30, 10, 40, pendingHard: 5);
        var rebuilt = Projection(20, 30, 10, 40, pendingHard: 4);

        ProjectionReconciliationService.Reconcile(live, rebuilt).Alerts
            .Should().ContainSingle(alert => alert.Code == ProjectionReconciliationCode.PendingClaimMismatch);
        FluentActions.Invoking(() => ProjectionReconciliationService.Reconcile(null!, rebuilt))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => ProjectionReconciliationService.Reconcile(live, null!))
            .Should().Throw<ArgumentNullException>();
    }

    private static WalletBalanceProjection Projection(
        long availableHard,
        long availableSoft,
        long withdrawable,
        long purchased,
        long pendingHard = 0) =>
        new(pendingHard, 0, purchased, 0, 0, 0, 0, 0, 0, availableHard, availableSoft, withdrawable);
}
