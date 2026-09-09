using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Reserves;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.Funding;

public sealed record ProtectedIssuanceRequest(
    ProtectedOperationContext Context,
    RiskDecisionId RiskDecisionId,
    RiskDecisionSnapshot Decision,
    RiskPersistenceReadiness PersistenceReadiness,
    Guid CounterReservationId,
    IReadOnlyCollection<AggregateRiskLimit> AggregateLimits,
    Guid CooldownSubjectId,
    DateTimeOffset RequestedAt,
    CoinAmount? ReserveLiabilityIncrease = null);

public sealed class ProtectedIssuanceAuthorization
{
    private readonly CoreReserveAuthority _reserveAuthority;

    internal ProtectedIssuanceAuthorization(
        PostingTemplateKind operation,
        CoinAmount amount,
        IdempotencyKey idempotencyKey,
        ReservePostingAuthorization reserve,
        RiskAuthorization risk,
        AggregateRiskCounterReservation counter,
        DateTimeOffset validUntil,
        CoreReserveAuthority reserveAuthority,
        IReadOnlyCollection<SourceStampId> sourceRoots)
    {
        Operation = operation;
        Amount = amount;
        IdempotencyKey = idempotencyKey;
        Reserve = reserve;
        Risk = risk;
        Counter = counter;
        ValidUntil = validUntil;
        _reserveAuthority = reserveAuthority;
        SourceRoots = Array.AsReadOnly(sourceRoots.OrderBy(root => root.Value).ToArray());
    }

    public PostingTemplateKind Operation { get; }
    public CoinAmount Amount { get; }
    public IdempotencyKey IdempotencyKey { get; }
    public ReservePostingAuthorization Reserve { get; }
    public RiskAuthorization Risk { get; }
    public AggregateRiskCounterReservation Counter { get; }
    public DateTimeOffset ValidUntil { get; }
    public IReadOnlyList<SourceStampId> SourceRoots { get; }

    public void EnsureMatches(
        PostingTemplateKind operation,
        IdempotencyKey idempotencyKey,
        CoinAmount amount,
        ReserveVersion reserveVersion,
        DateTimeOffset now)
    {
        if (Operation != operation || IdempotencyKey != idempotencyKey || Amount != amount ||
            Reserve.Version != reserveVersion)
            throw new IssuanceAuthorizationBindingException("Issuance authorization does not match the posting.");
        if (now > ValidUntil)
            throw new IssuanceAuthorizationExpiredException("Issuance authorization is no longer current.");
        _reserveAuthority.Authorize(Reserve.Version, Reserve.AuthorizationEpoch, now);
    }

    public void EnsureSourceRoots(IReadOnlyCollection<SourceStampId> sourceRoots)
    {
        ArgumentNullException.ThrowIfNull(sourceRoots);
        if (!SourceRoots.SequenceEqual(sourceRoots.OrderBy(root => root.Value)))
            throw new IssuanceAuthorizationBindingException("Issuance authorization source roots do not match the posting.");
    }
}

public sealed class ProtectedIssuanceAuthorizer
{
    private readonly object _gate = new();
    private readonly CoreReserveAuthority _reserveAuthority;
    private readonly CoreProtectedPostingGate _postingGate;
    private readonly AggregateRiskCounterStore _counterStore;
    private readonly ProtectedChangeCooldownRegistry _cooldowns;

    public ProtectedIssuanceAuthorizer(
        CoreReserveAuthority reserveAuthority,
        CoreProtectedPostingGate postingGate,
        AggregateRiskCounterStore counterStore,
        ProtectedChangeCooldownRegistry cooldowns)
    {
        _reserveAuthority = reserveAuthority ?? throw new ArgumentNullException(nameof(reserveAuthority));
        _postingGate = postingGate ?? throw new ArgumentNullException(nameof(postingGate));
        _counterStore = counterStore ?? throw new ArgumentNullException(nameof(counterStore));
        _cooldowns = cooldowns ?? throw new ArgumentNullException(nameof(cooldowns));
    }

    public ProtectedIssuanceAuthorization Authorize(ProtectedIssuanceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Context);
        ArgumentNullException.ThrowIfNull(request.Decision);
        ArgumentNullException.ThrowIfNull(request.PersistenceReadiness);
        ArgumentNullException.ThrowIfNull(request.AggregateLimits);
        if (request.CounterReservationId == Guid.Empty)
            throw new ArgumentException("Counter reservation ID cannot be empty.", nameof(request));
        if (request.CooldownSubjectId == Guid.Empty)
            throw new ArgumentException("Cooldown subject ID cannot be empty.", nameof(request));
        EnsureSourceRootLimits(request.Context.SourceRoots, request.AggregateLimits);

        lock (_gate)
        {
            EnsureCooldownsElapsed(request.CooldownSubjectId, request.RequestedAt);
            var liabilityIncrease = request.ReserveLiabilityIncrease ?? request.Context.Amount;
            var reserve = _reserveAuthority.AuthorizeIssuance(
                request.Context.ReserveVersion,
                request.Context.ReserveAuthorizationEpoch,
                liabilityIncrease,
                request.RequestedAt);
            var risk = _postingGate.Authorize(
                new ProtectedPostingCommand(
                    request.Context.Operation,
                    request.RiskDecisionId,
                    request.Context),
                request.Decision,
                request.PersistenceReadiness,
                reserve,
                request.RequestedAt);
            var counter = _counterStore.Reserve(
                request.CounterReservationId,
                request.Context.Operation,
                request.Context.Amount,
                request.AggregateLimits,
                request.RequestedAt);
            var validUntil = request.Decision.ExpiresAt < _reserveAuthority.ActiveHead!.ExpiresAt
                ? request.Decision.ExpiresAt
                : _reserveAuthority.ActiveHead.ExpiresAt;
            return new ProtectedIssuanceAuthorization(
                request.Context.Operation,
                request.Context.Amount,
                request.Context.IdempotencyKey,
                reserve,
                risk,
                counter,
                validUntil,
                _reserveAuthority,
                request.Context.SourceRoots);
        }
    }

    private static void EnsureSourceRootLimits(
        IReadOnlyCollection<SourceStampId> roots,
        IReadOnlyCollection<AggregateRiskLimit> limits)
    {
        foreach (var root in roots)
        {
            if (!limits.Any(limit => limit.Key.Dimension == RiskLimitDimension.SourceRoot &&
                                     StringComparer.Ordinal.Equals(
                                         limit.Key.SubjectHash,
                                         root.Value.ToString("N"))))
                throw new MissingSourceRootRiskLimitException(
                    $"Source root {root.Value:N} is missing an aggregate exposure limit.");
        }
    }

    private void EnsureCooldownsElapsed(Guid subjectId, DateTimeOffset now)
    {
        var active = _cooldowns.ForSubject(subjectId).FirstOrDefault(change => now < change.AvailableAt);
        if (active is not null)
            throw new ProtectedChangeCooldownActiveException(
                $"Protected change {active.Kind} remains in cooldown until {active.AvailableAt:O}.");
    }
}

public sealed class MissingSourceRootRiskLimitException(string message) : InvalidOperationException(message);
public sealed class ProtectedChangeCooldownActiveException(string message) : InvalidOperationException(message);
public sealed class IssuanceAuthorizationBindingException(string message) : InvalidOperationException(message);
public sealed class IssuanceAuthorizationExpiredException(string message) : InvalidOperationException(message);
