using System.Globalization;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Operations;

public sealed class PostgreSqlEconomyReserveQueryReader : IEconomyReserveQueryReader
{
    private readonly DbContext _db;

    public PostgreSqlEconomyReserveQueryReader(IApplicationDbContext context) =>
        _db = PostgreSqlEntityRiskGraphStore.RequireRelationalContext(context);

    public async ValueTask<EconomyOperationalPage<EconomyCustodyObservationOperationalStatus>> ListCustodyAsync(
        Guid tenantId,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        ValidateTenantAndLimit(tenantId, limit);
        var position = DecodeDateCursor(cursor, "Custody observation");
        var query = _db.Set<EconomyCustodyObservationRow>().AsNoTracking();
        if (position is not null)
        {
            var observedAt = position.Value.At;
            var id = position.Value.Id;
            query = query.Where(row => row.ObservedAt < observedAt ||
                                       row.ObservedAt == observedAt && row.Id.CompareTo(id) > 0);
        }
        var rows = await query.OrderByDescending(row => row.ObservedAt).ThenBy(row => row.Id)
            .Take(limit + 1).ToArrayAsync(cancellationToken);
        var pageRows = rows.Take(limit).ToArray();
        return new EconomyOperationalPage<EconomyCustodyObservationOperationalStatus>(
            Array.AsReadOnly(pageRows.Select(MapCustody).ToArray()),
            rows.Length > limit && pageRows.Length > 0
                ? EncodeDateCursor(pageRows[^1].ObservedAt, pageRows[^1].Id)
                : null);
    }

    public async ValueTask<EconomyCustodyObservationOperationalStatus?> FindCustodyAsync(
        Guid tenantId,
        Guid observationId,
        CancellationToken cancellationToken)
    {
        ValidateTenantAndId(tenantId, observationId, nameof(observationId));
        var row = await _db.Set<EconomyCustodyObservationRow>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == observationId, cancellationToken);
        return row is null ? null : MapCustody(row);
    }

    public async ValueTask<EconomyOperationalPage<EconomyReserveProposalOperationalStatus>> ListProposalsAsync(
        Guid tenantId,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        ValidateTenantAndLimit(tenantId, limit);
        var position = DecodeVersionCursor(cursor);
        var query = _db.Set<EconomyReserveProposalRow>().AsNoTracking();
        if (position is not null)
        {
            var version = position.Value.Version;
            var id = position.Value.Id;
            query = query.Where(row => row.Version < version ||
                                       row.Version == version && row.Id.CompareTo(id) > 0);
        }
        var rows = await query.OrderByDescending(row => row.Version).ThenBy(row => row.Id)
            .Take(limit + 1).ToArrayAsync(cancellationToken);
        var pageRows = rows.Take(limit).ToArray();
        return new EconomyOperationalPage<EconomyReserveProposalOperationalStatus>(
            Array.AsReadOnly(pageRows.Select(MapProposal).ToArray()),
            rows.Length > limit && pageRows.Length > 0
                ? EncodeVersionCursor(pageRows[^1].Version, pageRows[^1].Id)
                : null);
    }

    public async ValueTask<EconomyReserveProposalOperationalStatus?> FindProposalAsync(
        Guid tenantId,
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        ValidateTenantAndId(tenantId, proposalId, nameof(proposalId));
        var row = await _db.Set<EconomyReserveProposalRow>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == proposalId, cancellationToken);
        return row is null ? null : MapProposal(row);
    }

    public async ValueTask<EconomyActiveReserveOperationalDetails?> ReadActiveHeadAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        ValidateTenant(tenantId);
        var head = await _db.Set<EconomyReserveHeadRow>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.IsActive, cancellationToken);
        if (head is null) return null;
        var allocations = await _db.Set<EconomyReserveAssetAllocationRow>().AsNoTracking()
            .Where(row => row.ReserveVersion == head.Version)
            .OrderBy(row => row.AssetKey)
            .ToArrayAsync(cancellationToken);
        var reconciliation = await _db.Set<EconomyCustodyReconciliationRow>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.ReserveVersion == head.Version, cancellationToken);
        return new EconomyActiveReserveOperationalDetails(
            new EconomyReserveOperationalStatus(
                head.Version, head.PolicyVersion, head.AuthorizationEpoch, head.Coverage,
                head.EvidenceHash, head.ObservedAt, head.ExpiresAt,
                reconciliation?.IsReconciled ?? false, reconciliation?.VarianceUsdNanos,
                reconciliation?.ReconciledAt),
            Array.AsReadOnly(allocations.Select(row => new EconomyReserveAssetAllocationOperationalStatus(
                row.Id, row.AssetKey, row.Purpose, row.EligibleUsdNanos)).ToArray()),
            reconciliation is null
                ? null
                : new EconomyCustodyReconciliationOperationalStatus(
                    reconciliation.Id, reconciliation.LiabilityUsdNanos,
                    reconciliation.EligibleAssetUsdNanos, reconciliation.VarianceUsdNanos,
                    reconciliation.IsReconciled, reconciliation.EvidenceHash,
                    reconciliation.ReconciledBy, reconciliation.ReconciledAt));
    }

    private static EconomyCustodyObservationOperationalStatus MapCustody(EconomyCustodyObservationRow row) =>
        new(row.Id, row.Provider, row.AssetKey, row.Purpose, row.Version, row.EligibleUsdNanos,
            row.ObservedAt, row.ExpiresAt, row.PayloadHash, row.KeyId);

    private static EconomyReserveProposalOperationalStatus MapProposal(EconomyReserveProposalRow row) =>
        new(row.Id, row.Version, row.PolicyVersion, row.ExpectedActiveVersion, row.AuthorizationEpoch,
            row.SnapshotHash, row.LiabilityUsdNanos, row.EligibleAssetUsdNanos, row.Coverage,
            row.ObservationIds, row.AssetAllocations, row.EvidenceHash, row.ProposedBy, row.ApprovedBy,
            row.ProposedAt, row.ApprovedAt, row.ObservedAt, row.ExpiresAt, row.Status);

    private static string EncodeDateCursor(DateTimeOffset at, Guid id) => $"{at.UtcTicks:X16}{id:N}";
    private static string EncodeVersionCursor(long version, Guid id) => $"{version:X16}{id:N}";

    private static (DateTimeOffset At, Guid Id)? DecodeDateCursor(string? cursor, string label)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        if (cursor.Length != 48 ||
            !long.TryParse(cursor.AsSpan(0, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                out var ticks) ||
            !Guid.TryParseExact(cursor[16..], "N", out var id) ||
            ticks < DateTimeOffset.MinValue.UtcTicks || ticks > DateTimeOffset.MaxValue.UtcTicks)
            throw new ArgumentException($"{label} cursor is invalid.", nameof(cursor));
        return (new DateTimeOffset(ticks, TimeSpan.Zero), id);
    }

    private static (long Version, Guid Id)? DecodeVersionCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        if (cursor.Length != 48 ||
            !long.TryParse(cursor.AsSpan(0, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                out var version) || version < 1 ||
            !Guid.TryParseExact(cursor[16..], "N", out var id))
            throw new ArgumentException("Reserve proposal cursor is invalid.", nameof(cursor));
        return (version, id);
    }

    private static void ValidateTenantAndLimit(Guid tenantId, int limit)
    {
        ValidateTenant(tenantId);
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
    }

    private static void ValidateTenantAndId(Guid tenantId, Guid id, string parameterName)
    {
        ValidateTenant(tenantId);
        if (id == Guid.Empty) throw new ArgumentException("Identifier cannot be empty.", parameterName);
    }

    private static void ValidateTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
    }
}
