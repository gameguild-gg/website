using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Policy;

namespace GameGuild.Finance.Economy.Ledger;

public sealed class SourceEvidence
{
    private SourceEvidence(
        SourceStampId id,
        string provider,
        string providerReference,
        string evidenceHash,
        SourceConfirmationState state,
        DateTimeOffset observedAt,
        DateTimeOffset? confirmedAt,
        DateTimeOffset? reversedAt)
    {
        Id = id;
        Provider = provider;
        ProviderReference = providerReference;
        EvidenceHash = evidenceHash;
        State = state;
        ObservedAt = observedAt;
        ConfirmedAt = confirmedAt;
        ReversedAt = reversedAt;
    }

    public SourceStampId Id { get; }
    public string Provider { get; }
    public string ProviderReference { get; }
    public string EvidenceHash { get; }
    public SourceConfirmationState State { get; }
    public DateTimeOffset ObservedAt { get; }
    public DateTimeOffset? ConfirmedAt { get; }
    public DateTimeOffset? ReversedAt { get; }

    public static SourceEvidence Observe(
        SourceStampId id,
        string provider,
        string providerReference,
        string evidence,
        DateTimeOffset observedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);

        provider = provider.Trim();
        providerReference = providerReference.Trim();
        return new SourceEvidence(
            id,
            provider,
            providerReference,
            ComputeEvidenceHash(provider, providerReference, evidence),
            SourceConfirmationState.Observed,
            observedAt,
            null,
            null);
    }

    public SourceEvidence Confirm(DateTimeOffset confirmedAt)
    {
        if (State != SourceConfirmationState.Observed)
            throw new InvalidOperationException("Only observed source evidence can be confirmed.");
        if (confirmedAt < ObservedAt)
            throw new ArgumentException("Confirmation cannot precede observation.", nameof(confirmedAt));

        return new SourceEvidence(
            Id, Provider, ProviderReference, EvidenceHash,
            SourceConfirmationState.Confirmed, ObservedAt, confirmedAt, null);
    }

    public SourceEvidence Fail(DateTimeOffset failedAt) =>
        CompleteObserved(SourceConfirmationState.Failed, failedAt);

    public SourceEvidence Expire(DateTimeOffset expiredAt) =>
        CompleteObserved(SourceConfirmationState.Expired, expiredAt);

    public SourceEvidence Dispute(DateTimeOffset disputedAt)
    {
        if (State is not (SourceConfirmationState.Confirmed or SourceConfirmationState.Disputed))
            throw new InvalidOperationException("Only confirmed or disputed source evidence can be disputed.");
        if (disputedAt < ConfirmedAt!.Value)
            throw new ArgumentException("Dispute cannot precede confirmation.", nameof(disputedAt));

        return new SourceEvidence(
            Id, Provider, ProviderReference, EvidenceHash,
            SourceConfirmationState.Disputed, ObservedAt, ConfirmedAt, disputedAt);
    }

    public SourceEvidence ResolveDispute(DateTimeOffset resolvedAt)
    {
        if (State != SourceConfirmationState.Disputed)
            throw new InvalidOperationException("Only disputed source evidence can be resolved.");
        if (resolvedAt < ReversedAt!.Value)
            throw new ArgumentException("Resolution cannot precede the dispute.", nameof(resolvedAt));

        return new SourceEvidence(
            Id,
            Provider,
            ProviderReference,
            EvidenceHash,
            SourceConfirmationState.Confirmed,
            ObservedAt,
            ConfirmedAt,
            null);
    }

    public SourceEvidence Reverse(DateTimeOffset reversedAt)
    {
        if (State is not (SourceConfirmationState.Confirmed or SourceConfirmationState.Disputed))
            throw new InvalidOperationException("Only confirmed or disputed source evidence can be reversed.");
        if (reversedAt < ConfirmedAt!.Value)
            throw new ArgumentException("Reversal cannot precede confirmation.", nameof(reversedAt));

        return new SourceEvidence(
            Id, Provider, ProviderReference, EvidenceHash,
            SourceConfirmationState.Reversed, ObservedAt, ConfirmedAt, reversedAt);
    }

    private SourceEvidence CompleteObserved(SourceConfirmationState target, DateTimeOffset occurredAt)
    {
        if (State != SourceConfirmationState.Observed)
            throw new InvalidOperationException("Only observed source evidence can fail or expire.");
        if (occurredAt < ObservedAt)
            throw new ArgumentException("Terminal evidence cannot precede observation.", nameof(occurredAt));

        return new SourceEvidence(
            Id, Provider, ProviderReference, EvidenceHash,
            target, ObservedAt, null, occurredAt);
    }

    private static string ComputeEvidenceHash(string provider, string reference, string evidence)
    {
        var builder = new StringBuilder();
        Append(builder, provider);
        Append(builder, reference);
        Append(builder, evidence);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static void Append(StringBuilder builder, string value) =>
        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
}

public static class ConfirmedCreditFactory
{
    public static CreditLot CreateRootLot(
        CreditLotId lotId,
        WalletId walletId,
        CoinAmount amount,
        ProvenanceKind provenance,
        SourceEvidence source,
        long journalSequence)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.State != SourceConfirmationState.Confirmed || source.ConfirmedAt is null)
            throw new InvalidOperationException("Root credits require confirmed source evidence.");
        return CreateRootLot(
            lotId,
            walletId,
            amount,
            provenance,
            source,
            CreditLotMaturity.Assign(amount.Currency, provenance, source.ConfirmedAt.Value),
            journalSequence);
    }

    public static CreditLot CreateRootLot(
        CreditLotId lotId,
        WalletId walletId,
        CoinAmount amount,
        ProvenanceKind provenance,
        SourceEvidence source,
        DateTimeOffset originalMaturesAt,
        long journalSequence)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.State != SourceConfirmationState.Confirmed || source.ConfirmedAt is null)
            throw new InvalidOperationException("Root credits require confirmed source evidence.");
        CreditLotMaturity.EnsureExactEarnedHard(
            amount.Currency,
            provenance,
            source.ConfirmedAt.Value,
            originalMaturesAt);

        var traceUnitsPerCoinUnit = CurrencyTraceScale.For(amount.Currency);
        return new CreditLot(
            lotId,
            walletId,
            amount,
            provenance,
            source.ConfirmedAt.Value,
            originalMaturesAt,
            journalSequence,
            CreditLotState.Active,
            [new RootTraceRange(source.Id, 0, checked(amount.Units * traceUnitsPerCoinUnit), 0)],
            traceUnitsPerCoinUnit);
    }
}
