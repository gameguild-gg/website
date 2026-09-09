using System.Globalization;
using GameGuild.Finance.Economy.AdRewards.Persistence;
using GameGuild.Finance.Economy.Operations;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.AdRewards;

public sealed class PostgreSqlAdRewardOperationalQueryReader : IAdRewardOperationalQueryReader
{
    private readonly DbContext _db;

    public PostgreSqlAdRewardOperationalQueryReader(IApplicationDbContext context) =>
        _db = context as DbContext ?? throw new InvalidOperationException(
            "Ad reward operational queries require the application's relational DbContext.");

    public async ValueTask<EconomyOperationalPage<AdRewardSessionOperationalSummary>> ListSessionsAsync(
        Guid tenantId,
        DurableAdRewardSessionState? state,
        string? network,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        ValidateQuery(tenantId, network, limit);
        if (state is not null && !Enum.IsDefined(state.Value))
            throw new ArgumentOutOfRangeException(nameof(state));
        var position = DecodeCursor(cursor, "Ad reward session");
        var query = _db.Set<AdRewardSessionRow>().AsNoTracking()
            .Where(row => row.TenantId == tenantId);
        if (state is not null) query = query.Where(row => row.State == state.Value);
        if (network is not null)
        {
            var normalized = network.Trim();
            query = query.Where(row => row.Network == normalized);
        }
        if (position is not null)
        {
            var at = position.Value.At;
            var id = position.Value.Id;
            query = query.Where(row => row.IssuedAt < at ||
                                       row.IssuedAt == at && row.Id.CompareTo(id) > 0);
        }
        var rows = await query.OrderByDescending(row => row.IssuedAt).ThenBy(row => row.Id)
            .Take(limit + 1).ToArrayAsync(cancellationToken);
        var pageRows = rows.Take(limit).ToArray();
        return new EconomyOperationalPage<AdRewardSessionOperationalSummary>(
            Array.AsReadOnly(pageRows.Select(MapSession).ToArray()),
            rows.Length > limit && pageRows.Length > 0
                ? EncodeCursor(pageRows[^1].IssuedAt, pageRows[^1].Id)
                : null);
    }

    public async ValueTask<AdRewardSessionOperationalDetails?> FindSessionAsync(
        Guid tenantId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        ValidateTenantAndId(tenantId, sessionId);
        var session = await _db.Set<AdRewardSessionRow>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == sessionId && row.TenantId == tenantId,
                cancellationToken);
        if (session is null) return null;
        var milestones = await _db.Set<AdRewardPlaybackMilestoneRow>().AsNoTracking()
            .Where(row => row.SessionId == sessionId)
            .OrderBy(row => row.Sequence)
            .ToArrayAsync(cancellationToken);
        var events = await _db.Set<AdRewardSessionEventRow>().AsNoTracking()
            .Where(row => row.SessionId == sessionId)
            .OrderBy(row => row.Sequence)
            .ToArrayAsync(cancellationToken);
        var completion = await _db.Set<AdRewardCompletionRow>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.SessionId == sessionId && row.TenantId == tenantId,
                cancellationToken);
        return new AdRewardSessionOperationalDetails(
            MapSession(session),
            Array.AsReadOnly(milestones.Select(row => new AdRewardMilestoneOperationalStatus(
                row.Id, row.Sequence, row.Percentage, row.ObservedAt, row.EvidenceHash)).ToArray()),
            Array.AsReadOnly(events.Select(row => new AdRewardSessionEventOperationalStatus(
                row.Id, row.Sequence, row.State, row.EvidenceHash, row.OccurredAt)).ToArray()),
            completion is null
                ? null
                : new AdRewardCompletionOperationalStatus(
                    completion.State, completion.RewardSoftUnits, completion.PostingId,
                    completion.ProviderEventId, completion.ReserveVersion,
                    completion.JurisdictionCode, completion.CompletedAt));
    }

    public async ValueTask<EconomyOperationalPage<AdRewardPendingClaimOperationalStatus>> ListPendingClaimsAsync(
        Guid tenantId,
        bool? confirmed,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        ValidateQuery(tenantId, null, limit);
        var position = DecodeCursor(cursor, "Ad reward pending claim");
        var query = _db.Set<AdRewardPendingClaimRow>().AsNoTracking()
            .Where(row => row.TenantId == tenantId);
        if (confirmed is true) query = query.Where(row => row.ConfirmedAt != null);
        else if (confirmed is false) query = query.Where(row => row.ConfirmedAt == null);
        if (position is not null)
        {
            var at = position.Value.At;
            var id = position.Value.Id;
            query = query.Where(row => row.DeferredAt < at ||
                                       row.DeferredAt == at && row.SessionId.CompareTo(id) > 0);
        }
        var rows = await query.OrderByDescending(row => row.DeferredAt).ThenBy(row => row.SessionId)
            .Take(limit + 1).ToArrayAsync(cancellationToken);
        var pageRows = rows.Take(limit).ToArray();
        return new EconomyOperationalPage<AdRewardPendingClaimOperationalStatus>(
            Array.AsReadOnly(pageRows.Select(row => new AdRewardPendingClaimOperationalStatus(
                row.SessionId, row.TenantId, row.SourceStampId, row.DeferredAt,
                row.ProviderReportId, row.ConfirmedAt)).ToArray()),
            rows.Length > limit && pageRows.Length > 0
                ? EncodeCursor(pageRows[^1].DeferredAt, pageRows[^1].SessionId)
                : null);
    }

    public async ValueTask<EconomyOperationalPage<AdRewardReconciliationOperationalStatus>> ListReconciliationsAsync(
        Guid tenantId,
        string? network,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        ValidateQuery(tenantId, network, limit);
        var position = DecodeCursor(cursor, "Ad reward reconciliation");
        var query = _db.Set<AdRewardReconciliationRow>().AsNoTracking()
            .Where(row => row.TenantId == tenantId);
        if (network is not null)
        {
            var normalized = network.Trim();
            query = query.Where(row => row.Network == normalized);
        }
        if (position is not null)
        {
            var at = position.Value.At;
            var id = position.Value.Id;
            query = query.Where(row => row.ReconciledAt < at ||
                                       row.ReconciledAt == at && row.Id.CompareTo(id) > 0);
        }
        var rows = await query.OrderByDescending(row => row.ReconciledAt).ThenBy(row => row.Id)
            .Take(limit + 1).ToArrayAsync(cancellationToken);
        var pageRows = rows.Take(limit).ToArray();
        return new EconomyOperationalPage<AdRewardReconciliationOperationalStatus>(
            Array.AsReadOnly(pageRows.Select(row => new AdRewardReconciliationOperationalStatus(
                row.Id, row.TenantId, row.ProviderReportId, row.Network, row.ReportId, row.Version,
                row.BatchId, row.EstimatedRevenueUsdNanos, row.ActualRevenueUsdNanos,
                row.VarianceUsdNanos, row.HistoricalRewardSoftUnits, row.ReconciledAt)).ToArray()),
            rows.Length > limit && pageRows.Length > 0
                ? EncodeCursor(pageRows[^1].ReconciledAt, pageRows[^1].Id)
                : null);
    }

    private static AdRewardSessionOperationalSummary MapSession(AdRewardSessionRow row) => new(
        row.Id, row.TenantId, row.UserId, row.Network, row.CreativeId, row.PolicyVersion,
        row.State, row.IssuedAt, row.ExpiresAt, row.UpdatedAt);

    private static string EncodeCursor(DateTimeOffset at, Guid id) => $"{at.UtcTicks:X16}{id:N}";

    internal static (DateTimeOffset At, Guid Id)? DecodeCursor(string? cursor, string label)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        if (cursor.Length != 48 ||
            !long.TryParse(cursor.AsSpan(0, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                out var ticks) || !Guid.TryParseExact(cursor[16..], "N", out var id) ||
            ticks < DateTimeOffset.MinValue.UtcTicks || ticks > DateTimeOffset.MaxValue.UtcTicks)
            throw new ArgumentException($"{label} cursor is invalid.", nameof(cursor));
        return (new DateTimeOffset(ticks, TimeSpan.Zero), id);
    }

    private static void ValidateQuery(Guid tenantId, string? network, int limit)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        if (network is not null && string.IsNullOrWhiteSpace(network))
            throw new ArgumentException("Network cannot be blank.", nameof(network));
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
    }

    private static void ValidateTenantAndId(Guid tenantId, Guid sessionId)
    {
        if (tenantId == Guid.Empty || sessionId == Guid.Empty)
            throw new ArgumentException("Tenant and session IDs are required.");
    }
}
