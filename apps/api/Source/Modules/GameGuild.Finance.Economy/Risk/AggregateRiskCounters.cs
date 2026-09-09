using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.Risk;

public enum RiskLimitDimension
{
    Wallet = 1,
    IdentityCluster = 2,
    SourceRoot = 3,
    Destination = 4,
    CounterpartyPair = 5,
    Product = 6,
    Tenant = 7,
    ProviderAccount = 8,
    DeviceIpAsnCluster = 9,
    GlobalLossBudget = 10
}

public readonly record struct RiskLimitKey
{
    public RiskLimitKey(RiskLimitDimension dimension, string subjectHash)
    {
        if (!Enum.IsDefined(dimension)) throw new ArgumentOutOfRangeException(nameof(dimension));
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectHash);
        Dimension = dimension;
        SubjectHash = subjectHash.Trim();
    }

    public RiskLimitDimension Dimension { get; }
    public string SubjectHash { get; }
}

public sealed record AggregateRiskLimit
{
    public AggregateRiskLimit(RiskLimitKey key, long counterVersion, long maxUnits, TimeSpan window)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(counterVersion);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxUnits);
        if (window <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(window));
        Key = key;
        CounterVersion = counterVersion;
        MaxUnits = maxUnits;
        Window = window;
    }

    public RiskLimitKey Key { get; }
    public long CounterVersion { get; }
    public long MaxUnits { get; }
    public TimeSpan Window { get; }
}

public sealed record AggregateRiskCounterAllocation(
    RiskLimitKey Key,
    long CounterVersion,
    long Units,
    TimeSpan Window);

public sealed record AggregateRiskCounterReservation(
    Guid Id,
    string InputFingerprint,
    PostingTemplateKind Operation,
    CoinAmount Amount,
    IReadOnlyList<AggregateRiskCounterAllocation> Allocations,
    DateTimeOffset ReservedAt);

public sealed class AggregateRiskCounterStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, AggregateRiskCounterReservation> _reservations = [];
    private readonly Dictionary<RiskLimitKey, long> _counterVersions = [];

    public IReadOnlyList<AggregateRiskCounterReservation> Reservations
    {
        get
        {
            lock (_gate) return [.. _reservations.Values];
        }
    }

    public AggregateRiskCounterReservation Reserve(
        Guid id,
        PostingTemplateKind operation,
        CoinAmount amount,
        IReadOnlyCollection<AggregateRiskLimit> limits,
        DateTimeOffset reservedAt)
    {
        if (id == Guid.Empty) throw new ArgumentException("Reservation ID cannot be empty.", nameof(id));
        if (!Enum.IsDefined(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount.Units);
        ArgumentNullException.ThrowIfNull(limits);
        if (limits.Count == 0) throw new ArgumentException("At least one aggregate risk limit is required.", nameof(limits));
        if (limits.Select(limit => limit.Key).Distinct().Count() != limits.Count)
            throw new ArgumentException("Aggregate risk limit dimensions must be unique per subject.", nameof(limits));

        var ordered = limits.OrderBy(limit => limit.Key.Dimension)
            .ThenBy(limit => limit.Key.SubjectHash, StringComparer.Ordinal)
            .ToArray();
        var fingerprint = Fingerprint(operation, amount, ordered, reservedAt);

        lock (_gate)
        {
            if (_reservations.TryGetValue(id, out var existing))
            {
                if (existing.InputFingerprint == fingerprint) return existing;
                throw new RiskDecisionReuseException("A counter reservation ID cannot be reused with different inputs.");
            }

            foreach (var limit in ordered)
            {
                if (_counterVersions.TryGetValue(limit.Key, out var currentVersion) &&
                    limit.CounterVersion < currentVersion)
                    throw new StaleRiskCounterException("The aggregate risk counter version is stale.");

                var allocated = _reservations.Values
                    .Where(reservation => reservation.Operation == operation &&
                                          reservation.Amount.Currency == amount.Currency &&
                                          reservation.ReservedAt > reservedAt - limit.Window)
                    .SelectMany(reservation => reservation.Allocations)
                    .Where(allocation => allocation.Key == limit.Key)
                    .Sum(allocation => allocation.Units);
                if (amount.Units > limit.MaxUnits - allocated)
                    throw new AggregateRiskLimitExceededException(
                        $"The {limit.Key.Dimension} aggregate risk limit was exceeded.");
            }

            var allocations = ordered.Select(limit => new AggregateRiskCounterAllocation(
                limit.Key, limit.CounterVersion, amount.Units, limit.Window)).ToArray();
            var reservation = new AggregateRiskCounterReservation(
                id, fingerprint, operation, amount, allocations, reservedAt);
            _reservations.Add(id, reservation);
            foreach (var limit in ordered)
                _counterVersions[limit.Key] = Math.Max(
                    _counterVersions.GetValueOrDefault(limit.Key), limit.CounterVersion);
            return reservation;
        }
    }

    private static string Fingerprint(
        PostingTemplateKind operation,
        CoinAmount amount,
        IReadOnlyList<AggregateRiskLimit> limits,
        DateTimeOffset reservedAt)
    {
        var dimensions = string.Join('|', limits.Select(limit => string.Join(':',
            (int)limit.Key.Dimension,
            limit.Key.SubjectHash,
            limit.CounterVersion.ToString(CultureInfo.InvariantCulture),
            limit.MaxUnits.ToString(CultureInfo.InvariantCulture),
            limit.Window.Ticks.ToString(CultureInfo.InvariantCulture))));
        var canonical = string.Join('|',
            (int)operation,
            (int)amount.Currency,
            amount.Units.ToString(CultureInfo.InvariantCulture),
            reservedAt.UtcTicks.ToString(CultureInfo.InvariantCulture),
            dimensions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}

public sealed class StaleRiskCounterException(string message) : InvalidOperationException(message);
