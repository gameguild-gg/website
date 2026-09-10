namespace GameGuild.Finance.Economy.Projections;

public enum WalletReviewState
{
    Healthy = 1,
    ReviewRequired = 2
}

public enum ProjectionReconciliationCode
{
    PendingClaimMismatch = 1,
    ConfirmedCompositionMismatch = 2,
    HardAvailabilityMismatch = 3,
    SoftAvailabilityMismatch = 4,
    WithdrawableMismatch = 5
}

public sealed record ProjectionReconciliationAlert(
    ProjectionReconciliationCode Code,
    string Message);

public sealed record ProjectionReconciliationResult(
    WalletReviewState State,
    WalletBalanceProjection Enforced,
    IReadOnlyList<ProjectionReconciliationAlert> Alerts);

public static class ProjectionReconciliationService
{
    public static ProjectionReconciliationResult Reconcile(
        WalletBalanceProjection live,
        WalletBalanceProjection rebuilt)
    {
        ArgumentNullException.ThrowIfNull(live);
        ArgumentNullException.ThrowIfNull(rebuilt);

        var alerts = new List<ProjectionReconciliationAlert>();
        if (live.PendingHard != rebuilt.PendingHard || live.PendingSoft != rebuilt.PendingSoft)
            Add(alerts, ProjectionReconciliationCode.PendingClaimMismatch,
                "Pending funding claims differ from their source-evidence rebuild.");
        if (live.PurchasedHard != rebuilt.PurchasedHard || live.EarnedHard != rebuilt.EarnedHard ||
            live.RestrictedHard != rebuilt.RestrictedHard || live.Soft != rebuilt.Soft ||
            live.ImmatureEarnedHard != rebuilt.ImmatureEarnedHard || live.HeldHard != rebuilt.HeldHard ||
            live.HeldSoft != rebuilt.HeldSoft)
            Add(alerts, ProjectionReconciliationCode.ConfirmedCompositionMismatch,
                "Confirmed lot composition differs from its immutable-fact rebuild.");
        if (live.AvailableHardToSpend != rebuilt.AvailableHardToSpend)
            Add(alerts, ProjectionReconciliationCode.HardAvailabilityMismatch,
                "Hard-coin availability differs from its immutable-fact rebuild.");
        if (live.AvailableSoftToSpend != rebuilt.AvailableSoftToSpend)
            Add(alerts, ProjectionReconciliationCode.SoftAvailabilityMismatch,
                "Soft-coin availability differs from its immutable-fact rebuild.");
        if (live.WithdrawableHard != rebuilt.WithdrawableHard)
            Add(alerts, ProjectionReconciliationCode.WithdrawableMismatch,
                "Withdrawable hard coin differs from its immutable-fact rebuild.");

        var enforced = new WalletBalanceProjection(
            rebuilt.PendingHard,
            rebuilt.PendingSoft,
            rebuilt.PurchasedHard,
            rebuilt.EarnedHard,
            rebuilt.RestrictedHard,
            rebuilt.Soft,
            rebuilt.ImmatureEarnedHard,
            rebuilt.HeldHard,
            rebuilt.HeldSoft,
            Math.Min(live.AvailableHardToSpend, rebuilt.AvailableHardToSpend),
            Math.Min(live.AvailableSoftToSpend, rebuilt.AvailableSoftToSpend),
            Math.Min(live.WithdrawableHard, rebuilt.WithdrawableHard));
        return new ProjectionReconciliationResult(
            alerts.Count == 0 ? WalletReviewState.Healthy : WalletReviewState.ReviewRequired,
            enforced,
            alerts);
    }

    private static void Add(
        ICollection<ProjectionReconciliationAlert> alerts,
        ProjectionReconciliationCode code,
        string message) => alerts.Add(new ProjectionReconciliationAlert(code, message));
}
