using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Commerce.Payments;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.Integrations;

public interface IStripeEconomyFundingAdapter
{
    ObserveHardCoinTopUpCommand CreateObservation(
        Payment payment,
        WalletId walletId,
        string evidence,
        DateTimeOffset observedAt);

    ObserveHardCoinTopUpCommand CreateObservation(
        EconomyTopUpPaymentFact payment,
        WalletId walletId,
        string evidence,
        DateTimeOffset observedAt);

    IdempotencyKey ConfirmationIdempotencyKey(Payment payment);

    IdempotencyKey ConfirmationIdempotencyKey(EconomyTopUpPaymentFact payment);

    ConfirmObservedTopUpCommand CreateConfirmation(
        Payment payment,
        HardCoinFundingClaim claim,
        ProtectedIssuanceAuthorization authorization,
        PolicyVersion policyVersion,
        string evidence,
        DateTimeOffset confirmedAt);

    PersistedDurableHardCoinFundingConfirmation CreateDurableConfirmation(
        EconomyTopUpPaymentFact payment,
        HardCoinFundingClaim claim,
        EconomyProtectedOperationAuthorization authorization,
        RegisteredPostingAuthority authority,
        string evidence,
        DateTimeOffset confirmedAt);

    FinalizeObservedTopUpCommand CreateTerminalFailure(
        Payment payment,
        SourceConfirmationState state,
        string evidence,
        DateTimeOffset occurredAt);

    ReverseTopUpCommand CreateReversal(
        Payment payment,
        decimal cumulativeRefundedAmount,
        decimal cumulativeDisputedAmount,
        ProviderReversalDisposition disposition,
        string evidence,
        ReserveVersion reserveVersion,
        PolicyVersion policyVersion,
        DateTimeOffset occurredAt);
}

public sealed class StripeEconomyFundingAdapter : IStripeEconomyFundingAdapter
{
    public ObserveHardCoinTopUpCommand CreateObservation(
        Payment payment,
        WalletId walletId,
        string evidence,
        DateTimeOffset observedAt)
    {
        ArgumentNullException.ThrowIfNull(payment);
        return CreateObservation(ToPaymentFact(payment), walletId, evidence, observedAt);
    }

    public ObserveHardCoinTopUpCommand CreateObservation(
        EconomyTopUpPaymentFact payment,
        WalletId walletId,
        string evidence,
        DateTimeOffset observedAt)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);
        var leg = ProviderLeg(payment);
        return new ObserveHardCoinTopUpCommand(
            SourceId(leg),
            walletId,
            leg,
            evidence,
            ToUsdMinorUnits(payment.Amount, nameof(payment)),
            observedAt);
    }

    public IdempotencyKey ConfirmationIdempotencyKey(Payment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);
        return ConfirmationIdempotencyKey(ToPaymentFact(payment));
    }

    public IdempotencyKey ConfirmationIdempotencyKey(EconomyTopUpPaymentFact payment)
    {
        ArgumentNullException.ThrowIfNull(payment);
        return Key("confirm", ProviderLeg(payment).Key);
    }

    public ConfirmObservedTopUpCommand CreateConfirmation(
        Payment payment,
        HardCoinFundingClaim claim,
        ProtectedIssuanceAuthorization authorization,
        PolicyVersion policyVersion,
        string evidence,
        DateTimeOffset confirmedAt)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);
        var leg = ProviderLeg(ToPaymentFact(payment));
        var sourceId = SourceId(leg);
        var units = ToUsdMinorUnits(payment.Amount, nameof(payment));
        if (claim.SourceId != sourceId || claim.ProviderLeg.Key != leg.Key || claim.Amount.Units != units)
            throw new InvalidOperationException("Payment provider fact does not match the observed Economy funding claim.");
        var key = Key("confirm", leg.Key);
        return new ConfirmObservedTopUpCommand(
            DeterministicPostingId(sourceId, "confirm"),
            key,
            sourceId,
            DeterministicCreditLotId(sourceId, "purchased-hard-root"),
            authorization.Reserve.Version,
            policyVersion,
            evidence,
            confirmedAt,
            authorization);
    }

    public PersistedDurableHardCoinFundingConfirmation CreateDurableConfirmation(
        EconomyTopUpPaymentFact payment,
        HardCoinFundingClaim claim,
        EconomyProtectedOperationAuthorization authorization,
        RegisteredPostingAuthority authority,
        string evidence,
        DateTimeOffset confirmedAt)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);
        var leg = ProviderLeg(payment);
        var sourceId = SourceId(leg);
        var units = ToUsdMinorUnits(payment.Amount, nameof(payment));
        if (claim.SourceId != sourceId || claim.ProviderLeg.Key != leg.Key || claim.Amount.Units != units)
            throw new InvalidOperationException("Payment provider fact does not match the observed Economy funding claim.");
        return new PersistedDurableHardCoinFundingConfirmation(
            DeterministicPostingId(sourceId, "confirm"),
            Key("confirm", leg.Key),
            sourceId,
            DeterministicCreditLotId(sourceId, "purchased-hard-root"),
            evidence,
            confirmedAt,
            authorization.Receipt,
            authority);
    }

    public FinalizeObservedTopUpCommand CreateTerminalFailure(
        Payment payment,
        SourceConfirmationState state,
        string evidence,
        DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);
        if (state is not SourceConfirmationState.Failed and not SourceConfirmationState.Expired)
            throw new ArgumentOutOfRangeException(nameof(state), "Only failed or expired payments can finalize an unconfirmed funding claim.");
        return new FinalizeObservedTopUpCommand(
            SourceId(ProviderLeg(ToPaymentFact(payment))), state, evidence, occurredAt);
    }

    public ReverseTopUpCommand CreateReversal(
        Payment payment,
        decimal cumulativeRefundedAmount,
        decimal cumulativeDisputedAmount,
        ProviderReversalDisposition disposition,
        string evidence,
        ReserveVersion reserveVersion,
        PolicyVersion policyVersion,
        DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);
        payment.ValidateProviderMonetaryBounds(
            payment.Amount,
            cumulativeRefundedAmount,
            cumulativeDisputedAmount);
        var leg = ProviderLeg(ToPaymentFact(payment));
        var cumulative = ToUsdMinorUnits(
            checked(cumulativeRefundedAmount + cumulativeDisputedAmount),
            nameof(cumulativeRefundedAmount));
        var sourceId = SourceId(leg);
        return new ReverseTopUpCommand(
            DeterministicPostingId(sourceId, $"reverse:{cumulative.ToString(CultureInfo.InvariantCulture)}"),
            Key("reverse", $"{leg.Key}:{cumulative.ToString(CultureInfo.InvariantCulture)}"),
            sourceId,
            cumulative,
            disposition,
            evidence,
            reserveVersion,
            policyVersion,
            occurredAt);
    }

    private static ProviderMonetaryLeg ProviderLeg(EconomyTopUpPaymentFact payment)
    {
        if (!string.Equals(payment.Provider, "stripe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Stripe Economy funding requires a Stripe payment.");
        if (!string.Equals(payment.Currency, "USD", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("HardCoin funding accepts authoritative USD amounts only.");
        if (string.IsNullOrWhiteSpace(payment.ProviderEnvironment) ||
            string.IsNullOrWhiteSpace(payment.ProviderAccountId) ||
            string.IsNullOrWhiteSpace(payment.ProviderObjectId) ||
            string.IsNullOrWhiteSpace(payment.ProviderObjectType) ||
            string.IsNullOrWhiteSpace(payment.ProviderMonetaryLeg))
            throw new InvalidOperationException("Payment must have a verified provider mapping before Economy funding.");
        return new ProviderMonetaryLeg(
            payment.Provider,
            payment.ProviderEnvironment,
            payment.ProviderAccountId,
            $"{payment.ProviderObjectType}:{payment.ProviderObjectId}",
            payment.ProviderMonetaryLeg);
    }

    private static EconomyTopUpPaymentFact ToPaymentFact(Payment payment) => new(
        payment.Id,
        payment.TenantId,
        payment.Amount,
        payment.Currency,
        payment.Provider,
        payment.ProviderEnvironment ?? string.Empty,
        payment.ProviderAccountId ?? string.Empty,
        payment.ProviderObjectId ?? string.Empty,
        payment.ProviderObjectType ?? string.Empty,
        payment.ProviderMonetaryLeg ?? string.Empty);

    private static long ToUsdMinorUnits(decimal amount, string parameterName)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(parameterName);
        var scaled = checked(amount * 100m);
        if (scaled != decimal.Truncate(scaled) || scaled > long.MaxValue)
            throw new ArgumentException("USD amount must be exactly representable in minor units.", parameterName);
        return decimal.ToInt64(scaled);
    }

    private static SourceStampId SourceId(ProviderMonetaryLeg leg) =>
        new(DeterministicGuid($"source:{leg.Key}"));

    private static PostingId DeterministicPostingId(SourceStampId sourceId, string purpose) =>
        new(DeterministicGuid($"posting:{sourceId.Value:N}:{purpose}"));

    private static CreditLotId DeterministicCreditLotId(SourceStampId sourceId, string purpose) =>
        new(DeterministicGuid($"lot:{sourceId.Value:N}:{purpose}"));

    private static IdempotencyKey Key(string operation, string providerIdentity) =>
        new($"economy:stripe:{operation}:{Hash(providerIdentity)}");

    private static Guid DeterministicGuid(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value))[..16]);

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
