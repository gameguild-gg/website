using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Risk;

public sealed record EntityRiskLinkRequest(
    Guid TenantId,
    RiskEntityNode Left,
    RiskEntityNode Right,
    string Relationship,
    string EvidenceHash,
    DateTimeOffset ObservedAt);

public interface IEntityRiskGraphStore
{
    ValueTask<EntityRiskCluster> LinkAsync(EntityRiskLinkRequest request, CancellationToken cancellationToken);

    ValueTask<EntityRiskCluster> ClusterForAsync(
        Guid tenantId,
        RiskEntityNode seed,
        CancellationToken cancellationToken);
}

public sealed class PostgreSqlEntityRiskGraphStore : IEntityRiskGraphStore
{
    private readonly DbContext _db;

    public PostgreSqlEntityRiskGraphStore(IApplicationDbContext context) => _db = RequireRelationalContext(context);

    public async ValueTask<EntityRiskCluster> LinkAsync(
        EntityRiskLinkRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TenantId == Guid.Empty) throw new ArgumentException("Tenant ID cannot be empty.", nameof(request));
        if (request.Left == request.Right) throw new ArgumentException("A risk entity cannot link to itself.", nameof(request));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Relationship);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.EvidenceHash);

        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async _ =>
        {
        var nodes = await _db.Set<EconomyEntityGraphNodeRow>()
            .Where(row => row.TenantId == request.TenantId && row.SupersededAt == null)
            .ToListAsync(cancellationToken);
        var left = nodes.SingleOrDefault(row => row.Type == request.Left.Type && row.IdentityHash == request.Left.IdentifierHash);
        var right = nodes.SingleOrDefault(row => row.Type == request.Right.Type && row.IdentityHash == request.Right.IdentifierHash);
        var edges = await _db.Set<EconomyEntityGraphEdgeRow>()
            .Where(row => row.TenantId == request.TenantId && row.SupersededAt == null)
            .ToListAsync(cancellationToken);

        if (left is not null && right is not null)
        {
            var (leftId, rightId) = CanonicalPair(left.Id, right.Id);
            var existing = edges.SingleOrDefault(row =>
                row.LeftNodeId == leftId && row.RightNodeId == rightId &&
                row.Relationship == request.Relationship.Trim() &&
                row.EvidenceHash == request.EvidenceHash.Trim());
            if (existing is not null)
                return await ClusterForAsync(request.TenantId, request.Left, cancellationToken);
        }

        var currentVersion = Math.Max(
            nodes.Count == 0 ? 0 : nodes.Max(row => row.Version),
            edges.Count == 0 ? 0 : edges.Max(row => row.Version));
        var nextVersion = checked(currentVersion + 1);
        left ??= AddNode(request.TenantId, request.Left, request.EvidenceHash, nextVersion, request.ObservedAt);
        right ??= AddNode(request.TenantId, request.Right, request.EvidenceHash, nextVersion, request.ObservedAt);
        var pair = CanonicalPair(left.Id, right.Id);
        _db.Set<EconomyEntityGraphEdgeRow>().Add(new EconomyEntityGraphEdgeRow
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            LeftNodeId = pair.Left,
            RightNodeId = pair.Right,
            Relationship = request.Relationship.Trim(),
            Version = nextVersion,
            EvidenceHash = request.EvidenceHash.Trim(),
            RecordedAt = request.ObservedAt
        });
        await _db.SaveChangesAsync(cancellationToken);
        return await ClusterForAsync(request.TenantId, request.Left, cancellationToken);
        }, cancellationToken);
    }

    public async ValueTask<EntityRiskCluster> ClusterForAsync(
        Guid tenantId,
        RiskEntityNode seed,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        var rows = await _db.Set<EconomyEntityGraphNodeRow>()
            .AsNoTracking()
            .Where(row => row.TenantId == tenantId && row.SupersededAt == null)
            .ToListAsync(cancellationToken);
        var seedRow = rows.SingleOrDefault(row => row.Type == seed.Type && row.IdentityHash == seed.IdentifierHash);
        if (seedRow is null)
            return new EntityRiskCluster(HashNodeSet([seed]), 0, HashNodeSet([seed]), [seed]);

        var edges = await _db.Set<EconomyEntityGraphEdgeRow>()
            .AsNoTracking()
            .Where(row => row.TenantId == tenantId && row.SupersededAt == null)
            .ToListAsync(cancellationToken);
        var visited = new HashSet<Guid> { seedRow.Id };
        var pending = new Queue<Guid>();
        pending.Enqueue(seedRow.Id);
        while (pending.TryDequeue(out var current))
        {
            foreach (var edge in edges.Where(row => row.LeftNodeId == current || row.RightNodeId == current))
            {
                var neighbor = edge.LeftNodeId == current ? edge.RightNodeId : edge.LeftNodeId;
                if (visited.Add(neighbor)) pending.Enqueue(neighbor);
            }
        }

        var clusterRows = rows.Where(row => visited.Contains(row.Id)).ToArray();
        var clusterNodes = clusterRows
            .Select(row => new RiskEntityNode(row.Type, row.IdentityHash))
            .OrderBy(node => node.Type)
            .ThenBy(node => node.IdentifierHash, StringComparer.Ordinal)
            .ToArray();
        var clusterEdges = edges.Where(row => visited.Contains(row.LeftNodeId) && visited.Contains(row.RightNodeId)).ToArray();
        var version = Math.Max(
            clusterRows.Max(row => row.Version),
            clusterEdges.Length == 0 ? 0 : clusterEdges.Max(row => row.Version));
        var evidence = string.Join('|', clusterEdges
            .OrderBy(row => row.Version)
            .ThenBy(row => row.Id)
            .Select(row => string.Join(':', row.Version, row.Relationship, row.EvidenceHash, row.RecordedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))));
        return new EntityRiskCluster(HashNodeSet(clusterNodes), version, Hash(evidence), clusterNodes);
    }

    private EconomyEntityGraphNodeRow AddNode(
        Guid tenantId,
        RiskEntityNode node,
        string evidenceHash,
        long version,
        DateTimeOffset recordedAt)
    {
        var row = new EconomyEntityGraphNodeRow
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Type = node.Type, IdentityHash = node.IdentifierHash,
            Version = version, EvidenceHash = evidenceHash.Trim(), RecordedAt = recordedAt
        };
        _db.Set<EconomyEntityGraphNodeRow>().Add(row);
        return row;
    }

    private static (Guid Left, Guid Right) CanonicalPair(Guid left, Guid right) =>
        left.CompareTo(right) < 0 ? (left, right) : (right, left);

    private static string HashNodeSet(IEnumerable<RiskEntityNode> nodes) => Hash(string.Join('|', nodes
        .OrderBy(node => node.Type)
        .ThenBy(node => node.IdentifierHash, StringComparer.Ordinal)
        .Select(node => $"{(int)node.Type}:{node.IdentifierHash}")));

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    internal static DbContext RequireRelationalContext(IApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context as DbContext
            ?? throw new InvalidOperationException("Persistent Economy risk control requires the application's relational DbContext.");
    }
}

public enum RiskCounterReservationStatus
{
    Reserved = 1,
    Consumed = 2,
    Released = 3,
    Expired = 4
}

public sealed record DurableRiskCounterAllocation(
    Guid CounterId,
    RiskLimitKey Key,
    long CounterVersion,
    long Units,
    DateTimeOffset WindowStartedAt,
    DateTimeOffset WindowEndsAt);

public sealed record DurableAggregateRiskCounterReservation(
    Guid Id,
    Guid TenantId,
    Guid RiskDecisionId,
    string InputFingerprint,
    PostingTemplateKind Operation,
    CoinAmount Amount,
    IReadOnlyList<DurableRiskCounterAllocation> Allocations,
    DateTimeOffset ReservedAt,
    DateTimeOffset ExpiresAt,
    RiskCounterReservationStatus Status);

public interface IAggregateRiskCounterStore
{
    ValueTask<DurableAggregateRiskCounterReservation> ReserveAsync(
        Guid reservationId,
        Guid tenantId,
        Guid riskDecisionId,
        PostingTemplateKind operation,
        CoinAmount amount,
        IReadOnlyCollection<AggregateRiskLimit> limits,
        DateTimeOffset reservedAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken);

    ValueTask<DurableAggregateRiskCounterReservation> ConsumeAsync(
        Guid reservationId,
        DateTimeOffset consumedAt,
        CancellationToken cancellationToken);

    ValueTask<DurableAggregateRiskCounterReservation> ReleaseAsync(
        Guid reservationId,
        DateTimeOffset releasedAt,
        CancellationToken cancellationToken);
}

public sealed class PostgreSqlAggregateRiskCounterStore : IAggregateRiskCounterStore
{
    private readonly DbContext _db;

    public PostgreSqlAggregateRiskCounterStore(IApplicationDbContext context) =>
        _db = PostgreSqlEntityRiskGraphStore.RequireRelationalContext(context);

    public async ValueTask<DurableAggregateRiskCounterReservation> ReserveAsync(
        Guid reservationId,
        Guid tenantId,
        Guid riskDecisionId,
        PostingTemplateKind operation,
        CoinAmount amount,
        IReadOnlyCollection<AggregateRiskLimit> limits,
        DateTimeOffset reservedAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        ValidateReservationInputs(reservationId, tenantId, riskDecisionId, operation, amount, limits, reservedAt, expiresAt);
        var ordered = limits.OrderBy(limit => limit.Key.Dimension)
            .ThenBy(limit => limit.Key.SubjectHash, StringComparer.Ordinal)
            .ToArray();
        var fingerprint = Fingerprint(tenantId, riskDecisionId, operation, amount, ordered, reservedAt, expiresAt);

        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async _ =>
        {
        var replayRows = await _db.Set<EconomyRiskCounterReservationRow>()
            .Where(row => row.ReservationGroupId == reservationId)
            .ToListAsync(cancellationToken);
        if (replayRows.Count > 0)
        {
            if (replayRows.Any(row => row.InputFingerprint != fingerprint))
                throw new RiskDecisionReuseException("A counter reservation ID cannot be reused with different inputs.");
            return await MaterializeAsync(replayRows, cancellationToken);
        }

        var decisionBelongsToTenant = await (
                from decision in _db.Set<EconomyRiskDecisionRow>()
                join wallet in _db.Set<EconomyWalletRow>() on decision.SourceWalletId equals wallet.Id
                where decision.Id == riskDecisionId && wallet.TenantId == tenantId
                select decision.Id)
            .AnyAsync(cancellationToken);
        if (!decisionBelongsToTenant)
            throw new RiskDecisionBindingException("The counter reservation risk decision is not bound to this tenant.");

        var allocations = new List<(EconomyRiskCounterRow Counter, AggregateRiskLimit Limit)>();
        foreach (var limit in ordered)
        {
            var windowStartedAt = StartOfWindow(reservedAt, limit.Window);
            var windowEndsAt = windowStartedAt.Add(limit.Window);
            var counter = await _db.Set<EconomyRiskCounterRow>().SingleOrDefaultAsync(row =>
                row.TenantId == tenantId && row.Dimension == limit.Key.Dimension &&
                row.SubjectHash == limit.Key.SubjectHash && row.Operation == operation &&
                row.Currency == amount.Currency && row.WindowStartedAt == windowStartedAt,
                cancellationToken);
            if (counter is null)
            {
                counter = new EconomyRiskCounterRow
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, Dimension = limit.Key.Dimension,
                    SubjectHash = limit.Key.SubjectHash, Operation = operation, Currency = amount.Currency,
                    WindowStartedAt = windowStartedAt, WindowEndsAt = windowEndsAt,
                    CounterVersion = limit.CounterVersion, MaxUnits = limit.MaxUnits, UsedUnits = 0, UpdatedAt = reservedAt
                };
                _db.Set<EconomyRiskCounterRow>().Add(counter);
            }
            else if (limit.CounterVersion < counter.CounterVersion)
            {
                throw new StaleRiskCounterException("The aggregate risk counter version is stale.");
            }
            else if (limit.CounterVersion > counter.CounterVersion)
            {
                if (counter.UsedUnits > limit.MaxUnits)
                    throw new AggregateRiskLimitExceededException("The new aggregate risk limit is below already allocated capacity.");
                counter.CounterVersion = limit.CounterVersion;
                counter.MaxUnits = limit.MaxUnits;
            }

            if (amount.Units > counter.MaxUnits - counter.UsedUnits)
                throw new AggregateRiskLimitExceededException($"The {limit.Key.Dimension} aggregate risk limit was exceeded.");
            allocations.Add((counter, limit));
        }

        foreach (var allocation in allocations)
        {
            allocation.Counter.UsedUnits = checked(allocation.Counter.UsedUnits + amount.Units);
            allocation.Counter.UpdatedAt = reservedAt;
            _db.Set<EconomyRiskCounterReservationRow>().Add(new EconomyRiskCounterReservationRow
            {
                Id = Guid.NewGuid(), ReservationGroupId = reservationId, RiskDecisionId = riskDecisionId,
                RiskCounterId = allocation.Counter.Id, InputFingerprint = fingerprint, AmountUnits = amount.Units,
                ReservedAt = reservedAt, ExpiresAt = expiresAt, Status = RiskCounterReservationStatus.Reserved
            });
        }
        await _db.SaveChangesAsync(cancellationToken);
        return await ReadAsync(reservationId, cancellationToken);
        }, cancellationToken);
    }

    public ValueTask<DurableAggregateRiskCounterReservation> ConsumeAsync(
        Guid reservationId,
        DateTimeOffset consumedAt,
        CancellationToken cancellationToken) =>
        TransitionAsync(reservationId, consumedAt, consume: true, cancellationToken);

    public ValueTask<DurableAggregateRiskCounterReservation> ReleaseAsync(
        Guid reservationId,
        DateTimeOffset releasedAt,
        CancellationToken cancellationToken) =>
        TransitionAsync(reservationId, releasedAt, consume: false, cancellationToken);

    private async ValueTask<DurableAggregateRiskCounterReservation> TransitionAsync(
        Guid reservationId,
        DateTimeOffset occurredAt,
        bool consume,
        CancellationToken cancellationToken)
    {
        if (reservationId == Guid.Empty) throw new ArgumentException("Reservation ID cannot be empty.", nameof(reservationId));
        var status = await _db.Database.SqlQuery<int>($"""
                SELECT economy_private.transition_risk_counter_reservation_v1(
                    {reservationId}, {consume}, {occurredAt}) AS "Value"
                """)
            .SingleAsync(cancellationToken);
        if (consume && status == (int)RiskCounterReservationStatus.Expired)
            throw new InvalidOperationException("Expired risk counter capacity cannot be consumed.");
        return await ReadAsync(reservationId, cancellationToken);
    }

    private async ValueTask<DurableAggregateRiskCounterReservation> ReadAsync(
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        var rows = await _db.Set<EconomyRiskCounterReservationRow>().AsNoTracking()
            .Where(row => row.ReservationGroupId == reservationId)
            .ToListAsync(cancellationToken);
        if (rows.Count == 0) throw new KeyNotFoundException("Risk counter reservation was not found.");
        return await MaterializeAsync(rows, cancellationToken);
    }

    private async ValueTask<DurableAggregateRiskCounterReservation> MaterializeAsync(
        IReadOnlyList<EconomyRiskCounterReservationRow> rows,
        CancellationToken cancellationToken)
    {
        var counterIds = rows.Select(row => row.RiskCounterId).ToArray();
        var counters = await _db.Set<EconomyRiskCounterRow>().AsNoTracking()
            .Where(row => counterIds.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, cancellationToken);
        var first = rows[0];
        var firstCounter = counters[first.RiskCounterId];
        var allocations = rows.Select(row =>
        {
            var counter = counters[row.RiskCounterId];
            return new DurableRiskCounterAllocation(
                counter.Id, new RiskLimitKey(counter.Dimension, counter.SubjectHash), counter.CounterVersion,
                row.AmountUnits, counter.WindowStartedAt, counter.WindowEndsAt);
        }).ToArray();
        return new DurableAggregateRiskCounterReservation(
            first.ReservationGroupId, firstCounter.TenantId, first.RiskDecisionId, first.InputFingerprint,
            firstCounter.Operation, new CoinAmount(firstCounter.Currency, first.AmountUnits), allocations,
            first.ReservedAt, first.ExpiresAt, first.Status);
    }

    private static void ValidateReservationInputs(
        Guid reservationId,
        Guid tenantId,
        Guid riskDecisionId,
        PostingTemplateKind operation,
        CoinAmount amount,
        IReadOnlyCollection<AggregateRiskLimit> limits,
        DateTimeOffset reservedAt,
        DateTimeOffset expiresAt)
    {
        if (reservationId == Guid.Empty) throw new ArgumentException("Reservation ID cannot be empty.", nameof(reservationId));
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        if (riskDecisionId == Guid.Empty) throw new ArgumentException("Risk decision ID cannot be empty.", nameof(riskDecisionId));
        if (!Enum.IsDefined(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount.Units);
        ArgumentNullException.ThrowIfNull(limits);
        if (limits.Count == 0) throw new ArgumentException("At least one aggregate risk limit is required.", nameof(limits));
        if (limits.Select(limit => limit.Key).Distinct().Count() != limits.Count)
            throw new ArgumentException("Aggregate risk limit dimensions must be unique per subject.", nameof(limits));
        if (expiresAt <= reservedAt) throw new ArgumentException("Reservation expiry must follow creation.", nameof(expiresAt));
    }

    private static DateTimeOffset StartOfWindow(DateTimeOffset value, TimeSpan window)
    {
        var utcTicks = value.UtcTicks;
        return new DateTimeOffset(utcTicks - utcTicks % window.Ticks, TimeSpan.Zero);
    }

    private static string Fingerprint(
        Guid tenantId,
        Guid riskDecisionId,
        PostingTemplateKind operation,
        CoinAmount amount,
        IReadOnlyList<AggregateRiskLimit> limits,
        DateTimeOffset reservedAt,
        DateTimeOffset expiresAt)
    {
        var canonical = string.Join('|', tenantId.ToString("N"), riskDecisionId.ToString("N"), (int)operation,
            (int)amount.Currency, amount.Units, reservedAt.UtcTicks, expiresAt.UtcTicks,
            string.Join(';', limits.Select(limit => string.Join(':', (int)limit.Key.Dimension, limit.Key.SubjectHash,
                limit.CounterVersion, limit.MaxUnits, limit.Window.Ticks))));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}

public interface IProtectedChangeCooldownStore
{
    ValueTask<ProtectedChangeCooldown> RecordAsync(
        Guid tenantId,
        Guid subjectId,
        ProtectedChangeKind kind,
        string valueHash,
        DateTimeOffset changedAt,
        TimeSpan cooldown,
        CancellationToken cancellationToken);

    ValueTask<ProtectedChangeEvaluation> EvaluateAsync(
        Guid tenantId,
        Guid subjectId,
        ProtectedChangeKind kind,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<ProtectedChangeCooldown>> ForSubjectAsync(
        Guid tenantId,
        Guid subjectId,
        CancellationToken cancellationToken);
}

public sealed class PostgreSqlProtectedChangeCooldownStore : IProtectedChangeCooldownStore
{
    private readonly DbContext _db;

    public PostgreSqlProtectedChangeCooldownStore(IApplicationDbContext context) =>
        _db = PostgreSqlEntityRiskGraphStore.RequireRelationalContext(context);

    public async ValueTask<ProtectedChangeCooldown> RecordAsync(
        Guid tenantId,
        Guid subjectId,
        ProtectedChangeKind kind,
        string valueHash,
        DateTimeOffset changedAt,
        TimeSpan cooldown,
        CancellationToken cancellationToken)
    {
        ValidateKey(tenantId, subjectId, kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueHash);
        if (cooldown <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(cooldown));
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async _ =>
        {
        var latestVersion = await _db.Set<EconomyProtectedChangeCooldownRow>()
            .Where(row => row.TenantId == tenantId && row.SubjectId == subjectId && row.Kind == kind)
            .Select(row => (long?)row.Version)
            .MaxAsync(cancellationToken) ?? 0;
        var row = new EconomyProtectedChangeCooldownRow
        {
            Id = Guid.NewGuid(), TenantId = tenantId, SubjectId = subjectId, Kind = kind,
            ValueHash = valueHash.Trim(), Version = checked(latestVersion + 1), ChangedAt = changedAt,
            AvailableAt = changedAt.Add(cooldown)
        };
        _db.Set<EconomyProtectedChangeCooldownRow>().Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return Map(row);
        }, cancellationToken);
    }

    public async ValueTask<ProtectedChangeEvaluation> EvaluateAsync(
        Guid tenantId,
        Guid subjectId,
        ProtectedChangeKind kind,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ValidateKey(tenantId, subjectId, kind);
        var row = await _db.Set<EconomyProtectedChangeCooldownRow>().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.SubjectId == subjectId && item.Kind == kind)
            .OrderByDescending(item => item.Version)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("No protected change is registered for this subject.");
        var change = Map(row);
        return new ProtectedChangeEvaluation(change, now >= change.AvailableAt);
    }

    public async ValueTask<IReadOnlyList<ProtectedChangeCooldown>> ForSubjectAsync(
        Guid tenantId,
        Guid subjectId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        if (subjectId == Guid.Empty) throw new ArgumentException("Subject ID cannot be empty.", nameof(subjectId));
        return await _db.Set<EconomyProtectedChangeCooldownRow>().AsNoTracking()
            .Where(row => row.TenantId == tenantId && row.SubjectId == subjectId)
            .OrderBy(row => row.Kind).ThenBy(row => row.Version)
            .Select(row => new ProtectedChangeCooldown(
                row.SubjectId, row.Kind, row.ValueHash, row.Version, row.ChangedAt, row.AvailableAt))
            .ToArrayAsync(cancellationToken);
    }

    private static ProtectedChangeCooldown Map(EconomyProtectedChangeCooldownRow row) =>
        new(row.SubjectId, row.Kind, row.ValueHash, row.Version, row.ChangedAt, row.AvailableAt);

    private static void ValidateKey(Guid tenantId, Guid subjectId, ProtectedChangeKind kind)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        if (subjectId == Guid.Empty) throw new ArgumentException("Subject ID cannot be empty.", nameof(subjectId));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
    }
}

public interface IRiskReviewStore
{
    ValueTask<RiskReviewPage> ListAsync(
        Guid tenantId,
        RiskReviewStatus? status,
        int limit,
        string? cursor,
        CancellationToken cancellationToken);

    ValueTask<RiskReviewCase> SubmitAsync(
        Guid tenantId,
        Guid reviewId,
        Guid riskDecisionId,
        Guid submittedBy,
        IReadOnlyList<string> evidenceHashes,
        DateTimeOffset submittedAt,
        int requiredApprovals,
        CancellationToken cancellationToken);

    ValueTask<RiskReviewCase> ApproveAsync(
        Guid tenantId,
        Guid reviewId,
        Guid actorId,
        RiskManualDecisionCode decisionCode,
        string resolution,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken);

    ValueTask<RiskReviewCase> RejectAsync(
        Guid tenantId,
        Guid reviewId,
        Guid actorId,
        RiskManualDecisionCode decisionCode,
        string resolution,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken);

    ValueTask<RiskReviewCase> CurrentAsync(Guid tenantId, Guid reviewId, CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<RiskReviewEvent>> EventsAsync(
        Guid tenantId,
        Guid reviewId,
        CancellationToken cancellationToken);
}

public sealed record RiskReviewPage(
    IReadOnlyList<RiskReviewCase> Items,
    string? NextCursor);

public sealed class PostgreSqlRiskReviewStore : IRiskReviewStore
{
    private readonly DbContext _db;

    public PostgreSqlRiskReviewStore(IApplicationDbContext context) =>
        _db = PostgreSqlEntityRiskGraphStore.RequireRelationalContext(context);

    public async ValueTask<RiskReviewPage> ListAsync(
        Guid tenantId,
        RiskReviewStatus? status,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        if (status is not null && !Enum.IsDefined(status.Value))
            throw new ArgumentOutOfRangeException(nameof(status));
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        var position = DecodeCursor(cursor);
        var query = _db.Set<EconomyRiskReviewCaseRow>().AsNoTracking()
            .Where(row => row.TenantId == tenantId);
        if (status is not null) query = query.Where(row => row.Status == status.Value);
        if (position is not null)
        {
            var submittedAt = position.Value.SubmittedAt;
            var id = position.Value.Id;
            query = query.Where(row => row.SubmittedAt < submittedAt ||
                                       row.SubmittedAt == submittedAt && row.Id.CompareTo(id) > 0);
        }

        var rows = await query.OrderByDescending(row => row.SubmittedAt).ThenBy(row => row.Id)
            .Take(limit + 1).ToArrayAsync(cancellationToken);
        var pageRows = rows.Take(limit).ToArray();
        var reviewIds = pageRows.Select(row => row.Id).ToArray();
        var approvalEvents = reviewIds.Length == 0
            ? []
            : await _db.Set<EconomyRiskReviewEventRow>().AsNoTracking()
                .Where(row => reviewIds.Contains(row.RiskReviewCaseId) &&
                              (row.Kind == RiskReviewEventKind.ApprovalRecorded ||
                               row.Kind == RiskReviewEventKind.Approved))
                .OrderBy(row => row.Sequence)
                .ToArrayAsync(cancellationToken);
        var items = pageRows.Select(row => Map(
            row,
            approvalEvents.Where(item => item.RiskReviewCaseId == row.Id)
                .Select(item => item.ActorId).ToArray())).ToArray();
        var nextCursor = rows.Length > limit && pageRows.Length > 0
            ? EncodeCursor(pageRows[^1].SubmittedAt, pageRows[^1].Id)
            : null;
        return new RiskReviewPage(items, nextCursor);
    }

    public async ValueTask<RiskReviewCase> SubmitAsync(
        Guid tenantId,
        Guid reviewId,
        Guid riskDecisionId,
        Guid submittedBy,
        IReadOnlyList<string> evidenceHashes,
        DateTimeOffset submittedAt,
        int requiredApprovals,
        CancellationToken cancellationToken)
    {
        ValidateTenantReviewActor(tenantId, reviewId, submittedBy);
        if (riskDecisionId == Guid.Empty) throw new ArgumentException("Risk decision ID cannot be empty.", nameof(riskDecisionId));
        ArgumentNullException.ThrowIfNull(evidenceHashes);
        if (evidenceHashes.Count == 0 || evidenceHashes.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Review evidence hashes are required.", nameof(evidenceHashes));
        if (requiredApprovals is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(requiredApprovals));

        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async _ =>
        {
        var decisionIsReviewForTenant = await (
                from decision in _db.Set<EconomyRiskDecisionRow>()
                join wallet in _db.Set<EconomyWalletRow>() on decision.SourceWalletId equals wallet.Id
                where decision.Id == riskDecisionId && decision.Outcome == RiskOutcome.Review && wallet.TenantId == tenantId
                select decision.Id)
            .AnyAsync(cancellationToken);
        if (!decisionIsReviewForTenant)
            throw new RiskDecisionBindingException("Only a tenant-bound Review decision can create a review case.");
        if (await _db.Set<EconomyRiskReviewCaseRow>().AnyAsync(row => row.Id == reviewId, cancellationToken))
            throw new InvalidOperationException("Risk review case already exists.");

        var row = new EconomyRiskReviewCaseRow
        {
            Id = reviewId, TenantId = tenantId, RiskDecisionId = riskDecisionId, SubmittedBy = submittedBy,
            Status = RiskReviewStatus.Pending, SubmittedAt = submittedAt, RequiredApprovals = requiredApprovals
        };
        _db.Set<EconomyRiskReviewCaseRow>().Add(row);
        _db.Set<EconomyRiskReviewEventRow>().Add(new EconomyRiskReviewEventRow
        {
            Id = Guid.NewGuid(), RiskReviewCaseId = reviewId, Sequence = 1,
            Kind = RiskReviewEventKind.Submitted, ActorId = submittedBy,
            EvidenceHashes = JsonSerializer.Serialize(evidenceHashes.Select(value => value.Trim()).ToArray()),
            OccurredAt = submittedAt
        });
        await _db.SaveChangesAsync(cancellationToken);
        return await CurrentAsync(tenantId, reviewId, cancellationToken);
        }, cancellationToken);
    }

    public ValueTask<RiskReviewCase> ApproveAsync(
        Guid tenantId,
        Guid reviewId,
        Guid actorId,
        RiskManualDecisionCode decisionCode,
        string resolution,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken) =>
        ResolveAsync(tenantId, reviewId, actorId, decisionCode, resolution, occurredAt, approve: true, cancellationToken);

    public ValueTask<RiskReviewCase> RejectAsync(
        Guid tenantId,
        Guid reviewId,
        Guid actorId,
        RiskManualDecisionCode decisionCode,
        string resolution,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken) =>
        ResolveAsync(tenantId, reviewId, actorId, decisionCode, resolution, occurredAt, approve: false, cancellationToken);

    public async ValueTask<RiskReviewCase> CurrentAsync(
        Guid tenantId,
        Guid reviewId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        if (reviewId == Guid.Empty) throw new ArgumentException("Review ID cannot be empty.", nameof(reviewId));
        var row = await _db.Set<EconomyRiskReviewCaseRow>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.TenantId == tenantId && item.Id == reviewId, cancellationToken)
            ?? throw new KeyNotFoundException("Risk review case was not found.");
        var approvers = await ApprovalActorsAsync(reviewId, cancellationToken);
        return Map(row, approvers);
    }

    public async ValueTask<IReadOnlyList<RiskReviewEvent>> EventsAsync(
        Guid tenantId,
        Guid reviewId,
        CancellationToken cancellationToken)
    {
        _ = await CurrentAsync(tenantId, reviewId, cancellationToken);
        var rows = await _db.Set<EconomyRiskReviewEventRow>().AsNoTracking()
            .Where(row => row.RiskReviewCaseId == reviewId)
            .OrderBy(row => row.Sequence)
            .ToArrayAsync(cancellationToken);
        return rows.Select(row => new RiskReviewEvent(
            row.Sequence, row.RiskReviewCaseId, row.Kind, row.ActorId,
            JsonSerializer.Deserialize<string[]>(row.EvidenceHashes) ?? [], row.Resolution,
            row.DecisionCode, row.OccurredAt)).ToArray();
    }

    private async ValueTask<RiskReviewCase> ResolveAsync(
        Guid tenantId,
        Guid reviewId,
        Guid actorId,
        RiskManualDecisionCode decisionCode,
        string resolution,
        DateTimeOffset occurredAt,
        bool approve,
        CancellationToken cancellationToken)
    {
        ValidateTenantReviewActor(tenantId, reviewId, actorId);
        if (!Enum.IsDefined(decisionCode)) throw new ArgumentOutOfRangeException(nameof(decisionCode));
        ArgumentException.ThrowIfNullOrWhiteSpace(resolution);
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async _ =>
        {
        var row = await _db.Set<EconomyRiskReviewCaseRow>()
            .SingleOrDefaultAsync(item => item.TenantId == tenantId && item.Id == reviewId, cancellationToken)
            ?? throw new KeyNotFoundException("Risk review case was not found.");
        if (row.Status != RiskReviewStatus.Pending)
            throw new InvalidOperationException("Risk review case has already been resolved.");
        if (row.SubmittedBy == actorId)
            throw new InvalidOperationException("The submitter cannot resolve their own risk review.");
        if (occurredAt < row.SubmittedAt)
            throw new ArgumentException("Resolution cannot predate review submission.", nameof(occurredAt));
        var approvers = await ApprovalActorsAsync(reviewId, cancellationToken);
        if (approvers.Contains(actorId))
            throw new InvalidOperationException("A reviewer cannot approve the same case twice.");
        var sequence = await _db.Set<EconomyRiskReviewEventRow>()
            .Where(item => item.RiskReviewCaseId == reviewId)
            .MaxAsync(item => item.Sequence, cancellationToken) + 1;

        RiskReviewEventKind eventKind;
        if (approve)
        {
            approvers = [.. approvers, actorId];
            var complete = approvers.Count >= row.RequiredApprovals;
            eventKind = complete ? RiskReviewEventKind.Approved : RiskReviewEventKind.ApprovalRecorded;
            if (complete)
            {
                row.Status = RiskReviewStatus.Approved;
                row.ResolvedAt = occurredAt;
                row.ResolvedBy = actorId;
                row.Resolution = resolution.Trim();
            }
        }
        else
        {
            eventKind = RiskReviewEventKind.Rejected;
            row.Status = RiskReviewStatus.Rejected;
            row.ResolvedAt = occurredAt;
            row.ResolvedBy = actorId;
            row.Resolution = resolution.Trim();
        }

        _db.Set<EconomyRiskReviewEventRow>().Add(new EconomyRiskReviewEventRow
        {
            Id = Guid.NewGuid(), RiskReviewCaseId = reviewId, Sequence = sequence, Kind = eventKind,
            ActorId = actorId, EvidenceHashes = "[]", Resolution = resolution.Trim(),
            DecisionCode = decisionCode, OccurredAt = occurredAt
        });
        await _db.SaveChangesAsync(cancellationToken);
        return Map(row, approvers);
        }, cancellationToken);
    }

    private async Task<IReadOnlyList<Guid>> ApprovalActorsAsync(Guid reviewId, CancellationToken cancellationToken) =>
        await _db.Set<EconomyRiskReviewEventRow>().AsNoTracking()
            .Where(row => row.RiskReviewCaseId == reviewId &&
                          (row.Kind == RiskReviewEventKind.ApprovalRecorded || row.Kind == RiskReviewEventKind.Approved))
            .OrderBy(row => row.Sequence)
            .Select(row => row.ActorId)
            .ToArrayAsync(cancellationToken);

    private static RiskReviewCase Map(EconomyRiskReviewCaseRow row, IReadOnlyList<Guid> approvers) => new(
        row.Id, row.RiskDecisionId, row.SubmittedBy, row.Status, row.SubmittedAt, row.ResolvedAt,
        row.ResolvedBy, row.Resolution, row.RequiredApprovals, approvers, row.AppealOf);

    internal static string EncodeCursor(DateTimeOffset submittedAt, Guid id) =>
        $"{submittedAt.UtcTicks:X16}{id:N}";

    internal static (DateTimeOffset SubmittedAt, Guid Id)? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        if (cursor.Length != 48 ||
            !long.TryParse(cursor.AsSpan(0, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var ticks) ||
            !Guid.TryParseExact(cursor[16..], "N", out var id) ||
            ticks < DateTimeOffset.MinValue.UtcTicks || ticks > DateTimeOffset.MaxValue.UtcTicks)
            throw new ArgumentException("Risk review cursor is invalid.", nameof(cursor));
        return (new DateTimeOffset(ticks, TimeSpan.Zero), id);
    }

    private static void ValidateTenantReviewActor(Guid tenantId, Guid reviewId, Guid actorId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        if (reviewId == Guid.Empty) throw new ArgumentException("Review ID cannot be empty.", nameof(reviewId));
        if (actorId == Guid.Empty) throw new ArgumentException("Actor ID cannot be empty.", nameof(actorId));
    }
}
