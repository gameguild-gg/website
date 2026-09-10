using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;

namespace GameGuild.Finance.Economy.Policy;

public static class CreditLotMaturity
{
    public static DateTimeOffset Assign(
        CurrencyCode currency,
        ProvenanceKind provenance,
        DateTimeOffset confirmedAt) =>
        currency == CurrencyCode.HardCoin && provenance == ProvenanceKind.EarnedHard
            ? confirmedAt.Add(EconomyParity.EarnedHardMaturity)
            : confirmedAt;

    public static void EnsureExactEarnedHard(
        CurrencyCode currency,
        ProvenanceKind provenance,
        DateTimeOffset confirmedAt,
        DateTimeOffset maturesAt)
    {
        if (currency == CurrencyCode.HardCoin && provenance == ProvenanceKind.EarnedHard &&
            maturesAt != Assign(currency, provenance, confirmedAt))
            throw new ArgumentException("Earned hard currency must mature exactly 120 days after confirmation.", nameof(maturesAt));
    }
}

public enum ProtectedValueOperation
{
    Spend = 1,
    Transfer = 2,
    Convert = 3,
    Escrow = 4,
    Payout = 5
}

public sealed record WalletRestrictionSnapshot
{
    public WalletRestrictionSnapshot(WalletId walletId, WalletLifecycleState lifecycleState, long hardDebtUnits)
    {
        if (!Enum.IsDefined(lifecycleState)) throw new ArgumentOutOfRangeException(nameof(lifecycleState));
        ArgumentOutOfRangeException.ThrowIfNegative(hardDebtUnits);
        WalletId = walletId;
        LifecycleState = lifecycleState;
        HardDebtUnits = hardDebtUnits;
    }

    public WalletId WalletId { get; }
    public WalletLifecycleState LifecycleState { get; }
    public long HardDebtUnits { get; }
}

public enum WalletRestrictionReason
{
    WalletNotActive = 1,
    OutstandingDebt = 2
}

public sealed record WalletRestrictionDecision(bool IsAllowed, IReadOnlyList<WalletRestrictionReason> Reasons);

public static class WalletRestrictionEvaluator
{
    public static WalletRestrictionDecision Evaluate(
        WalletRestrictionSnapshot snapshot,
        ProtectedValueOperation operation)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!Enum.IsDefined(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
        var reasons = new List<WalletRestrictionReason>();
        if (snapshot.LifecycleState != WalletLifecycleState.Active)
            reasons.Add(WalletRestrictionReason.WalletNotActive);
        if (snapshot.HardDebtUnits > 0)
            reasons.Add(WalletRestrictionReason.OutstandingDebt);
        return new WalletRestrictionDecision(reasons.Count == 0, reasons);
    }
}

public enum PayoutIneligibilityReason
{
    NonCashableCurrency = 1,
    NonCashableProvenance = 2,
    LotNotActive = 3,
    Immature = 4,
    ActiveHold = 5,
    AccountRestricted = 6,
    OutstandingDebt = 7
}

public sealed record PayoutEligibilityDecision(
    bool IsEligible,
    IReadOnlyList<PayoutIneligibilityReason> Reasons);

public static class PayoutEligibilityEvaluator
{
    public static PayoutEligibilityDecision Evaluate(
        CreditLot lot,
        DateTimeOffset asOf,
        IReadOnlyCollection<HoldContract> holds,
        WalletRestrictionSnapshot restriction)
    {
        ArgumentNullException.ThrowIfNull(lot);
        ArgumentNullException.ThrowIfNull(holds);
        ArgumentNullException.ThrowIfNull(restriction);
        if (restriction.WalletId != lot.WalletId)
            throw new ArgumentException("Restriction snapshot must belong to the credit-lot wallet.", nameof(restriction));

        var reasons = new List<PayoutIneligibilityReason>();
        if (lot.Amount.Currency != CurrencyCode.HardCoin)
            reasons.Add(PayoutIneligibilityReason.NonCashableCurrency);
        if (lot.Provenance != ProvenanceKind.EarnedHard)
            reasons.Add(PayoutIneligibilityReason.NonCashableProvenance);
        if (lot.State != CreditLotState.Active)
            reasons.Add(PayoutIneligibilityReason.LotNotActive);
        if (asOf < lot.OriginalMaturesAt)
            reasons.Add(PayoutIneligibilityReason.Immature);
        if (holds.Any(hold => hold.WalletId == lot.WalletId && hold.Status == HoldStatus.Active &&
                              hold.EffectiveAt <= asOf &&
                              hold.Amount.Currency == lot.Amount.Currency))
            reasons.Add(PayoutIneligibilityReason.ActiveHold);
        if (restriction.LifecycleState != WalletLifecycleState.Active)
            reasons.Add(PayoutIneligibilityReason.AccountRestricted);
        if (restriction.HardDebtUnits > 0)
            reasons.Add(PayoutIneligibilityReason.OutstandingDebt);
        return new PayoutEligibilityDecision(reasons.Count == 0, reasons);
    }
}
