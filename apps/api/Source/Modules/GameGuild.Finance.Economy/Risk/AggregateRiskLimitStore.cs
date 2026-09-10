using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.Risk;

public sealed record AggregateRiskReservation(
    Guid Id,
    string ClusterId,
    long ClusterVersion,
    PostingTemplateKind Operation,
    CoinAmount Amount,
    DateTimeOffset ReservedAt,
    DateTimeOffset ExpiresAt);

public sealed class AggregateRiskLimitStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, AggregateRiskReservation> _reservations = [];
    private readonly Dictionary<string, long> _latestClusterVersions = [];

    public IReadOnlyList<AggregateRiskReservation> Reservations
    {
        get
        {
            lock (_gate) return [.. _reservations.Values];
        }
    }

    public AggregateRiskReservation Reserve(
        Guid id,
        EntityRiskCluster cluster,
        PostingTemplateKind operation,
        CoinAmount amount,
        long limit,
        DateTimeOffset reservedAt,
        DateTimeOffset expiresAt)
    {
        if (id == Guid.Empty) throw new ArgumentException("Reservation ID cannot be empty.", nameof(id));
        ArgumentNullException.ThrowIfNull(cluster);
        if (!Enum.IsDefined(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount.Units);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        if (expiresAt <= reservedAt) throw new ArgumentException("Reservation expiry must follow creation.", nameof(expiresAt));

        lock (_gate)
        {
            var candidate = new AggregateRiskReservation(
                id, cluster.Id, cluster.Version, operation, amount, reservedAt, expiresAt);
            if (_reservations.TryGetValue(id, out var existing))
            {
                if (existing == candidate) return existing;
                throw new RiskDecisionReuseException("A reservation ID cannot be reused with different inputs.");
            }

            if (_latestClusterVersions.TryGetValue(cluster.Id, out var latest) && cluster.Version < latest)
                throw new StaleEntityGraphException("The entity graph version is stale.");

            var allocated = _reservations.Values
                .Where(item => item.ClusterId == cluster.Id && item.Operation == operation &&
                               item.Amount.Currency == amount.Currency && item.ExpiresAt > reservedAt)
                .Sum(item => item.Amount.Units);
            if (amount.Units > limit - allocated)
                throw new AggregateRiskLimitExceededException("The normalized entity cluster limit was exceeded.");

            _reservations.Add(id, candidate);
            _latestClusterVersions[cluster.Id] = Math.Max(latest, cluster.Version);
            return candidate;
        }
    }
}

public sealed class AggregateRiskLimitExceededException(string message) : InvalidOperationException(message);
public sealed class StaleEntityGraphException(string message) : InvalidOperationException(message);
