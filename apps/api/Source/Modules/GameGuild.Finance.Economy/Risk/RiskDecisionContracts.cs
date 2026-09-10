using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.Risk;

public enum RiskOutcome
{
    Allow = 1,
    Challenge = 2,
    Hold = 3,
    Review = 4,
    Deny = 5
}

public enum RiskReasonCode
{
    WithinLimits = 1,
    ManualReviewRequired = 2,
    AggregateLimitExceeded = 3,
    ExternalEvidenceDenied = 4,
    ProtectedChangeCooldown = 5,
    StaleEntityGraph = 6
}

public readonly record struct RiskCurrencyLeg
{
    public RiskCurrencyLeg(CurrencyCode currency, long units)
    {
        if (!Enum.IsDefined(currency)) throw new ArgumentOutOfRangeException(nameof(currency));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(units);
        Currency = currency;
        Units = units;
    }

    public CurrencyCode Currency { get; }
    public long Units { get; }
}

public sealed record ProtectedOperationContext(
    IdempotencyKey IdempotencyKey,
    Guid ActorId,
    PostingTemplateKind Operation,
    WalletId SourceWalletId,
    WalletId DestinationWalletId,
    CoinAmount Amount,
    IReadOnlyList<RiskCurrencyLeg> CurrencyLegs,
    IReadOnlyList<SourceStampId> SourceRoots,
    string ProviderReferenceHash,
    PolicyVersion PolicyVersion,
    ReserveVersion ReserveVersion,
    long FeatureVersion,
    long KillSwitchEpoch,
    long EntityGraphVersion,
    string EntityGraphEvidenceHash,
    long CounterVersion = 1,
    long ReserveAuthorizationEpoch = 1)
{
    public string Fingerprint()
    {
        var legs = string.Join(',', CurrencyLegs.Select(leg =>
            $"{(int)leg.Currency}:{leg.Units.ToString(CultureInfo.InvariantCulture)}"));
        var roots = string.Join(',', SourceRoots.Select(root => root.Value.ToString("N")));
        var canonical = string.Join('|',
            IdempotencyKey.Value,
            ActorId.ToString("N"),
            ((int)Operation).ToString(CultureInfo.InvariantCulture),
            SourceWalletId.Value.ToString("N"),
            DestinationWalletId.Value.ToString("N"),
            ((int)Amount.Currency).ToString(CultureInfo.InvariantCulture),
            Amount.Units.ToString(CultureInfo.InvariantCulture),
            legs,
            roots,
            ProviderReferenceHash,
            PolicyVersion.Value.ToString(CultureInfo.InvariantCulture),
            ReserveVersion.Value.ToString(CultureInfo.InvariantCulture),
            FeatureVersion.ToString(CultureInfo.InvariantCulture),
            KillSwitchEpoch.ToString(CultureInfo.InvariantCulture),
            EntityGraphVersion.ToString(CultureInfo.InvariantCulture),
            EntityGraphEvidenceHash,
            CounterVersion.ToString(CultureInfo.InvariantCulture),
            ReserveAuthorizationEpoch.ToString(CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}

public sealed record RiskDecisionSnapshot(
    Guid Id,
    RiskOutcome Outcome,
    string OperationFingerprint,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<RiskReasonCode> ReasonCodes)
{
    public static RiskDecisionSnapshot Create(
        Guid id,
        RiskOutcome outcome,
        ProtectedOperationContext context,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        IReadOnlyList<RiskReasonCode> reasonCodes)
    {
        if (id == Guid.Empty) throw new ArgumentException("Risk decision ID cannot be empty.", nameof(id));
        if (!Enum.IsDefined(outcome)) throw new ArgumentOutOfRangeException(nameof(outcome));
        ArgumentNullException.ThrowIfNull(context);
        if (expiresAt <= issuedAt) throw new ArgumentException("Risk decision expiry must follow issuance.", nameof(expiresAt));
        ArgumentNullException.ThrowIfNull(reasonCodes);
        if (reasonCodes.Count == 0 || reasonCodes.Any(code => !Enum.IsDefined(code)))
            throw new ArgumentException("At least one valid risk reason code is required.", nameof(reasonCodes));

        return new RiskDecisionSnapshot(
            id, outcome, context.Fingerprint(), issuedAt, expiresAt, [.. reasonCodes]);
    }
}

public sealed record RiskAuthorization(
    Guid DecisionId,
    string OperationFingerprint,
    IdempotencyKey IdempotencyKey,
    DateTimeOffset AuthorizedAt);

public sealed class RiskAuthorizationDeniedException(string message) : InvalidOperationException(message);
public sealed class RiskDecisionExpiredException(string message) : InvalidOperationException(message);
public sealed class RiskDecisionBindingException(string message) : InvalidOperationException(message);
public sealed class RiskDecisionReuseException(string message) : InvalidOperationException(message);
