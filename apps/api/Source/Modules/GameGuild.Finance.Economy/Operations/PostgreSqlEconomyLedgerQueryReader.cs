using System.Globalization;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Operations;

public sealed class PostgreSqlEconomyLedgerQueryReader : IEconomyLedgerQueryReader
{
    private readonly DbContext _db;

    public PostgreSqlEconomyLedgerQueryReader(IApplicationDbContext context) =>
        _db = PostgreSqlEntityRiskGraphStore.RequireRelationalContext(context);

    public async ValueTask<EconomyOperationalPage<EconomyJournalVerificationRunDetails>> ListVerificationsAsync(
        Guid tenantId, int limit, string? cursor, CancellationToken cancellationToken)
    {
        ValidateTenantAndLimit(tenantId, limit);
        var position = DecodeDateCursor(cursor, "Journal verification");
        var query = _db.Set<EconomyJournalVerificationCheckpointRow>().AsNoTracking();
        if (position is not null)
        {
            var at = position.Value.At;
            var id = position.Value.Id;
            query = query.Where(row => row.CompletedAt < at ||
                                       row.CompletedAt == at && row.Id.CompareTo(id) > 0);
        }
        var rows = await query.OrderByDescending(row => row.CompletedAt).ThenBy(row => row.Id)
            .Take(limit + 1).ToArrayAsync(cancellationToken);
        var pageRows = rows.Take(limit).ToArray();
        return Page(pageRows.Select(MapVerification).ToArray(), rows.Length > limit,
            pageRows.Length == 0 ? null : EncodeDateCursor(pageRows[^1].CompletedAt, pageRows[^1].Id));
    }

    public async ValueTask<EconomyJournalVerificationRunDetails?> FindVerificationAsync(
        Guid tenantId, Guid verificationId, CancellationToken cancellationToken)
    {
        ValidateTenantAndId(tenantId, verificationId, nameof(verificationId));
        var row = await _db.Set<EconomyJournalVerificationCheckpointRow>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == verificationId, cancellationToken);
        return row is null ? null : MapVerification(row);
    }

    public async ValueTask<EconomyOperationalPage<EconomyAnchorOperationalDetails>> ListAnchorsAsync(
        Guid tenantId, int limit, string? cursor, CancellationToken cancellationToken)
    {
        ValidateTenantAndLimit(tenantId, limit);
        var position = DecodeDateCursor(cursor, "Anchor");
        var query = _db.Set<EconomyExternalAnchorRow>().AsNoTracking();
        if (position is not null)
        {
            var at = position.Value.At;
            var id = position.Value.Id;
            query = query.Where(row => row.AnchoredAt < at ||
                                       row.AnchoredAt == at && row.Id.CompareTo(id) > 0);
        }
        var rows = await query.OrderByDescending(row => row.AnchoredAt).ThenBy(row => row.Id)
            .Take(limit + 1).ToArrayAsync(cancellationToken);
        var pageRows = rows.Take(limit).ToArray();
        var verifications = await ReadLatestAnchorVerificationsAsync(
            pageRows.Select(row => row.Id).ToArray(), cancellationToken);
        var items = pageRows.Select(row => MapAnchor(
            row, verifications.GetValueOrDefault(row.Id))).ToArray();
        return Page(items, rows.Length > limit,
            pageRows.Length == 0 ? null : EncodeDateCursor(pageRows[^1].AnchoredAt, pageRows[^1].Id));
    }

    public async ValueTask<EconomyAnchorOperationalDetails?> FindAnchorAsync(
        Guid tenantId, Guid anchorId, CancellationToken cancellationToken)
    {
        ValidateTenantAndId(tenantId, anchorId, nameof(anchorId));
        var row = await _db.Set<EconomyExternalAnchorRow>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == anchorId, cancellationToken);
        if (row is null) return null;
        var latest = await _db.Set<EconomyAnchorVerificationRow>().AsNoTracking()
            .Where(item => item.ExternalAnchorId == anchorId)
            .OrderByDescending(item => item.VerifiedAt)
            .ThenBy(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return MapAnchor(row, latest);
    }

    public async ValueTask<IReadOnlyList<EconomyAnchorVerificationOperationalStatus>> ReadAnchorVerificationsAsync(
        Guid tenantId, Guid anchorId, CancellationToken cancellationToken)
    {
        ValidateTenantAndId(tenantId, anchorId, nameof(anchorId));
        var exists = await _db.Set<EconomyExternalAnchorRow>().AsNoTracking()
            .AnyAsync(item => item.Id == anchorId, cancellationToken);
        if (!exists) return Array.Empty<EconomyAnchorVerificationOperationalStatus>();
        var rows = await _db.Set<EconomyAnchorVerificationRow>().AsNoTracking()
            .Where(item => item.ExternalAnchorId == anchorId)
            .OrderBy(item => item.VerifiedAt)
            .ThenBy(item => item.Id)
            .ToArrayAsync(cancellationToken);
        return Array.AsReadOnly(rows.Select(MapAnchorVerification).ToArray());
    }

    public async ValueTask<EconomyOperationalPage<EconomyProjectionGenerationOperationalDetails>> ListProjectionsAsync(
        Guid tenantId, int limit, string? cursor, CancellationToken cancellationToken)
    {
        ValidateTenantAndLimit(tenantId, limit);
        var position = DecodeVersionCursor(cursor, "Projection generation");
        var query = _db.Set<EconomyProjectionGenerationRow>().AsNoTracking();
        if (position is not null)
        {
            var version = position.Value.Version;
            var id = position.Value.Id;
            query = query.Where(row => row.Generation < version ||
                                       row.Generation == version && row.Id.CompareTo(id) > 0);
        }
        var rows = await query.OrderByDescending(row => row.Generation).ThenBy(row => row.Id)
            .Take(limit + 1).ToArrayAsync(cancellationToken);
        var pageRows = rows.Take(limit).ToArray();
        return Page(pageRows.Select(MapProjection).ToArray(), rows.Length > limit,
            pageRows.Length == 0 ? null : EncodeVersionCursor(pageRows[^1].Generation, pageRows[^1].Id));
    }

    public async ValueTask<EconomyProjectionGenerationOperationalDetails?> FindProjectionAsync(
        Guid tenantId, long generation, CancellationToken cancellationToken)
    {
        ValidateTenant(tenantId);
        if (generation < 1) throw new ArgumentOutOfRangeException(nameof(generation));
        var row = await _db.Set<EconomyProjectionGenerationRow>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Generation == generation, cancellationToken);
        return row is null ? null : MapProjection(row);
    }

    public async ValueTask<IReadOnlyList<EconomyProjectionApprovalAuditEntry>> ReadProjectionAuditAsync(
        Guid tenantId, long generation, CancellationToken cancellationToken)
    {
        ValidateTenant(tenantId);
        if (generation < 1) throw new ArgumentOutOfRangeException(nameof(generation));
        var rows = await _db.Set<EconomyProjectionGenerationApprovalRow>().AsNoTracking()
            .Where(item => item.Generation == generation)
            .OrderBy(item => item.ApprovedAt)
            .ThenBy(item => item.Id)
            .ToArrayAsync(cancellationToken);
        return Array.AsReadOnly(rows.Select(item => new EconomyProjectionApprovalAuditEntry(
            item.Id, item.ActorId, item.ReauthenticationHash, item.ApprovedAt)).ToArray());
    }

    private async Task<IReadOnlyDictionary<Guid, EconomyAnchorVerificationRow>> ReadLatestAnchorVerificationsAsync(
        Guid[] anchorIds, CancellationToken cancellationToken)
    {
        if (anchorIds.Length == 0) return new Dictionary<Guid, EconomyAnchorVerificationRow>();
        var rows = await _db.Set<EconomyAnchorVerificationRow>().AsNoTracking()
            .Where(row => anchorIds.Contains(row.ExternalAnchorId))
            .OrderByDescending(row => row.VerifiedAt)
            .ThenBy(row => row.Id)
            .ToArrayAsync(cancellationToken);
        return rows.GroupBy(row => row.ExternalAnchorId).ToDictionary(group => group.Key, group => group.First());
    }

    private static EconomyJournalVerificationRunDetails MapVerification(EconomyJournalVerificationCheckpointRow row) =>
        new(row.Id, row.FromSequence, row.ToSequence, row.PreviousHash, row.CurrentHash, row.IsValid,
            row.FailureCode, row.FencingToken, row.StartedAt, row.CompletedAt);

    private static EconomyAnchorOperationalDetails MapAnchor(
        EconomyExternalAnchorRow row, EconomyAnchorVerificationRow? verification) => new(
        new EconomyAnchorOperationalStatus(
            row.Id, row.JournalSequence, row.JournalHash, row.Provider, row.AnchoredAt,
            verification?.SignatureValid ?? false, verification?.ObjectMatches ?? false,
            verification?.RetainUntil, verification?.VerifiedAt),
        row.DispatchSnapshotHash);

    private static EconomyAnchorVerificationOperationalStatus MapAnchorVerification(EconomyAnchorVerificationRow row) =>
        new(row.Id, row.KeyId, row.ObjectVersion, row.ETag, row.RetainUntil, row.ObjectHash,
            row.SignatureValid, row.ObjectMatches, row.VerifiedAt);

    private static EconomyProjectionGenerationOperationalDetails MapProjection(EconomyProjectionGenerationRow row) =>
        new(new EconomyProjectionOperationalStatus(
                row.Generation, row.ToSequence, row.JournalHash, row.ProjectionHash, row.MismatchCount,
                row.State, row.ProposedBy, row.ApprovedBy, row.SecondApprovedBy, row.ActivatedAt),
            row.FromSequence, row.IsActive, row.StartedAt, row.CompletedAt);

    private static EconomyOperationalPage<T> Page<T>(T[] items, bool hasMore, string? cursor) =>
        new(Array.AsReadOnly(items), hasMore ? cursor : null);

    private static string EncodeDateCursor(DateTimeOffset at, Guid id) => $"{at.UtcTicks:X16}{id:N}";
    private static string EncodeVersionCursor(long version, Guid id) => $"{version:X16}{id:N}";

    private static (DateTimeOffset At, Guid Id)? DecodeDateCursor(string? cursor, string label)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        if (cursor.Length != 48 ||
            !long.TryParse(cursor.AsSpan(0, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                out var ticks) || !Guid.TryParseExact(cursor[16..], "N", out var id) ||
            ticks < DateTimeOffset.MinValue.UtcTicks || ticks > DateTimeOffset.MaxValue.UtcTicks)
            throw new ArgumentException($"{label} cursor is invalid.", nameof(cursor));
        return (new DateTimeOffset(ticks, TimeSpan.Zero), id);
    }

    private static (long Version, Guid Id)? DecodeVersionCursor(string? cursor, string label)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        if (cursor.Length != 48 ||
            !long.TryParse(cursor.AsSpan(0, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                out var version) || version < 1 || !Guid.TryParseExact(cursor[16..], "N", out var id))
            throw new ArgumentException($"{label} cursor is invalid.", nameof(cursor));
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
