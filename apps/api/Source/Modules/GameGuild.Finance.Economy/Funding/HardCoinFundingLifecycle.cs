using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;

namespace GameGuild.Finance.Economy.Funding;

public sealed record ObserveHardCoinTopUpCommand(
    SourceStampId SourceId,
    WalletId WalletId,
    ProviderMonetaryLeg ProviderLeg,
    string Evidence,
    long AuthoritativeUsdMinorUnits,
    DateTimeOffset ObservedAt);

public sealed record ConfirmObservedTopUpCommand(
    PostingId PostingId,
    IdempotencyKey IdempotencyKey,
    SourceStampId SourceId,
    CreditLotId CreditLotId,
    ReserveVersion ReserveVersion,
    PolicyVersion PolicyVersion,
    string Evidence,
    DateTimeOffset ConfirmedAt,
    ProtectedIssuanceAuthorization Authorization);

public sealed record FinalizeObservedTopUpCommand(
    SourceStampId SourceId,
    SourceConfirmationState State,
    string Evidence,
    DateTimeOffset OccurredAt);

public sealed record ConvertHardToSoftCommand(
    PostingId PrincipalPostingId,
    PostingId FeePostingId,
    IdempotencyKey IdempotencyKey,
    WalletId WalletId,
    CreditLotId OutputLotId,
    long PrincipalHardCoinUnits,
    long FeeHardCoinUnits,
    ReserveVersion ReserveVersion,
    PolicyVersion PolicyVersion,
    DateTimeOffset RequestedAt,
    ProtectedIssuanceAuthorization Authorization);

public sealed record HardToSoftConversionResult(
    PostingResult PrincipalPosting,
    PostingResult? FeePosting,
    CreditLot OutputLot);

public sealed record IssueSystemBackedGrantCommand(
    PostingId PostingId,
    IdempotencyKey IdempotencyKey,
    SourceStampId SourceId,
    WalletId WalletId,
    CreditLotId OutputLotId,
    long HardBackingUnits,
    ReserveVersion ReserveVersion,
    PolicyVersion PolicyVersion,
    string TreasuryEvidence,
    DateTimeOffset IssuedAt,
    ProtectedIssuanceAuthorization Authorization);

public sealed record SystemBackedGrantResult(PostingResult Posting, CreditLot OutputLot);

public sealed record IssueAdRewardCommand(
    PostingId PostingId,
    IdempotencyKey IdempotencyKey,
    SourceStampId SourceId,
    WalletId WalletId,
    CreditLotId OutputLotId,
    long SoftUnits,
    ReserveVersion ReserveVersion,
    PolicyVersion PolicyVersion,
    string ProviderEvidence,
    DateTimeOffset IssuedAt,
    ProtectedIssuanceAuthorization Authorization);

public sealed record AdRewardIssuanceResult(PostingResult Posting, CreditLot OutputLot);

public sealed record ProviderMonetaryLeg
{
    private const char Separator = '\u001f';
    private const int MaximumPersistentSourceReferenceLength = 256;

    public ProviderMonetaryLeg(
        string provider,
        string environment,
        string connectedAccount,
        string providerObject,
        string monetaryLeg)
    {
        Provider = Normalize(provider, nameof(provider));
        Environment = Normalize(environment, nameof(environment));
        ConnectedAccount = Normalize(connectedAccount, nameof(connectedAccount));
        ProviderObject = Normalize(providerObject, nameof(providerObject));
        MonetaryLeg = Normalize(monetaryLeg, nameof(monetaryLeg));
        Key = string.Join(Separator, Provider, Environment, ConnectedAccount, ProviderObject, MonetaryLeg);
        if (Key.Length > MaximumPersistentSourceReferenceLength)
            throw new ArgumentOutOfRangeException(nameof(provider), "Provider monetary leg identity exceeds the persistent source reference limit.");
    }

    public string Provider { get; }
    public string Environment { get; }
    public string ConnectedAccount { get; }
    public string ProviderObject { get; }
    public string MonetaryLeg { get; }
    public string Key { get; }

    private static string Normalize(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Provider identity values are required.", parameterName);
        return value.Trim();
    }
}

public static class HardCoinFundingAmount
{
    public static CoinAmount FromUsdMinorUnits(long usdMinorUnits)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(usdMinorUnits);
        return new CoinAmount(CurrencyCode.HardCoin, usdMinorUnits);
    }
}

public sealed record FundingSourceEvent(
    long Sequence,
    SourceConfirmationState State,
    string EvidenceHash,
    DateTimeOffset OccurredAt);

public sealed class HardCoinFundingClaim
{
    private readonly IReadOnlyList<FundingSourceEvent> _events;

    private HardCoinFundingClaim(
        SourceStampId sourceId,
        WalletId walletId,
        ProviderMonetaryLeg providerLeg,
        CoinAmount amount,
        SourceConfirmationState state,
        DateTimeOffset observedAt,
        DateTimeOffset? terminalAt,
        IReadOnlyList<FundingSourceEvent> events)
    {
        SourceId = sourceId;
        WalletId = walletId;
        ProviderLeg = providerLeg;
        Amount = amount;
        State = state;
        ObservedAt = observedAt;
        TerminalAt = terminalAt;
        _events = Array.AsReadOnly(events.ToArray());
    }

    public SourceStampId SourceId { get; }
    public WalletId WalletId { get; }
    public ProviderMonetaryLeg ProviderLeg { get; }
    public CoinAmount Amount { get; }
    public SourceConfirmationState State { get; }
    public DateTimeOffset ObservedAt { get; }
    public DateTimeOffset? TerminalAt { get; }
    public bool IsPending => State == SourceConfirmationState.Observed;
    public IReadOnlyList<FundingSourceEvent> Events => _events;

    public static HardCoinFundingClaim Observe(
        SourceStampId sourceId,
        WalletId walletId,
        ProviderMonetaryLeg providerLeg,
        string evidence,
        long authoritativeUsdMinorUnits,
        DateTimeOffset observedAt)
    {
        ArgumentNullException.ThrowIfNull(providerLeg);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);
        var amount = HardCoinFundingAmount.FromUsdMinorUnits(authoritativeUsdMinorUnits);
        var observed = Event(
            sourceId,
            providerLeg,
            1,
            SourceConfirmationState.Observed,
            evidence,
            observedAt);
        return new HardCoinFundingClaim(
            sourceId,
            walletId,
            providerLeg,
            amount,
            SourceConfirmationState.Observed,
            observedAt,
            null,
            [observed]);
    }

    public HardCoinFundingClaim Transition(
        SourceConfirmationState target,
        string evidence,
        DateTimeOffset occurredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);
        if (!CanTransition(State, target))
        {
            if (State != SourceConfirmationState.Observed)
                throw new FundingTerminalStateConflictException(State, target);
            throw new InvalidFundingStateTransitionException(State, target);
        }
        if (occurredAt < Events[^1].OccurredAt)
            throw new ArgumentException("Funding evidence cannot be backdated.", nameof(occurredAt));

        var nextEvent = Event(
            SourceId,
            ProviderLeg,
            checked(Events.Count + 1L),
            target,
            evidence,
            occurredAt);
        return new HardCoinFundingClaim(
            SourceId,
            WalletId,
            ProviderLeg,
            Amount,
            target,
            ObservedAt,
            occurredAt,
            [.. Events, nextEvent]);
    }

    private static bool CanTransition(SourceConfirmationState current, SourceConfirmationState target) =>
        (current, target) switch
        {
            (SourceConfirmationState.Observed, SourceConfirmationState.Confirmed) => true,
            (SourceConfirmationState.Observed, SourceConfirmationState.Failed) => true,
            (SourceConfirmationState.Observed, SourceConfirmationState.Expired) => true,
            (SourceConfirmationState.Confirmed, SourceConfirmationState.Disputed) => true,
            (SourceConfirmationState.Confirmed, SourceConfirmationState.Reversed) => true,
            (SourceConfirmationState.Disputed, SourceConfirmationState.Disputed) => true,
            (SourceConfirmationState.Disputed, SourceConfirmationState.Confirmed) => true,
            (SourceConfirmationState.Disputed, SourceConfirmationState.Reversed) => true,
            _ => false
        };

    private static FundingSourceEvent Event(
        SourceStampId sourceId,
        ProviderMonetaryLeg providerLeg,
        long sequence,
        SourceConfirmationState state,
        string evidence,
        DateTimeOffset occurredAt)
    {
        var canonical = new StringBuilder();
        Append(canonical, sourceId.Value.ToString("N"));
        Append(canonical, providerLeg.Key);
        Append(canonical, sequence.ToString(CultureInfo.InvariantCulture));
        Append(canonical, ((int)state).ToString(CultureInfo.InvariantCulture));
        Append(canonical, evidence.Trim());
        return new FundingSourceEvent(
            sequence,
            state,
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))),
            occurredAt);
    }

    private static void Append(StringBuilder builder, string value) =>
        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
}

public sealed class InvalidFundingStateTransitionException(
    SourceConfirmationState current,
    SourceConfirmationState target)
    : InvalidOperationException($"Funding cannot transition from {current} to {target}.");

public sealed class FundingTerminalStateConflictException(
    SourceConfirmationState current,
    SourceConfirmationState target)
    : InvalidOperationException($"Funding already reached {current} and cannot transition to {target}.");

public sealed class DuplicateProviderMonetaryLegException(ProviderMonetaryLeg leg)
    : InvalidOperationException($"Provider monetary leg '{leg.Key}' is already bound to a funding source.");
