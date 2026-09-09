using System.Globalization;
using GameGuild.Finance.Economy.Marketplace.Persistence;
using GameGuild.Finance.Economy.Operations;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Marketplace;

public sealed class PostgreSqlMarketplaceOperationalQueryReader : IMarketplaceOperationalQueryReader
{
    private readonly DbContext _db;

    public PostgreSqlMarketplaceOperationalQueryReader(IApplicationDbContext context) =>
        _db = context as DbContext ?? throw new InvalidOperationException(
            "Marketplace operational queries require the application's relational DbContext.");

    public async ValueTask<EconomyOperationalPage<MarketplaceSettlementOperationalSummary>> ListSettlementsAsync(
        Guid tenantId,
        MarketplaceSettlementStatus? status,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        ValidateTenantAndLimit(tenantId, limit);
        if (status is not null && !Enum.IsDefined(status.Value))
            throw new ArgumentOutOfRangeException(nameof(status));
        var position = DecodeCursor(cursor, "Marketplace settlement");
        var query = _db.Set<MarketplaceSettlementRow>().AsNoTracking()
            .Where(row => row.TenantId == tenantId);
        if (status is not null) query = query.Where(row => row.Status == status.Value);
        if (position is not null)
        {
            var at = position.Value.At;
            var id = position.Value.Id;
            query = query.Where(row => row.SettledAt < at ||
                                       row.SettledAt == at && row.Id.CompareTo(id) > 0);
        }
        var rows = await query.OrderByDescending(row => row.SettledAt).ThenBy(row => row.Id)
            .Take(limit + 1).ToArrayAsync(cancellationToken);
        var pageRows = rows.Take(limit).ToArray();
        return new EconomyOperationalPage<MarketplaceSettlementOperationalSummary>(
            Array.AsReadOnly(pageRows.Select(MapSettlement).ToArray()),
            rows.Length > limit && pageRows.Length > 0
                ? EncodeCursor(pageRows[^1].SettledAt, pageRows[^1].Id)
                : null);
    }

    public async ValueTask<MarketplaceSettlementOperationalDetails?> FindSettlementAsync(
        Guid tenantId,
        Guid settlementId,
        CancellationToken cancellationToken)
    {
        ValidateTenantAndId(tenantId, settlementId, nameof(settlementId));
        var settlement = await _db.Set<MarketplaceSettlementRow>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == settlementId && row.TenantId == tenantId,
                cancellationToken);
        if (settlement is null) return null;
        var legs = await _db.Set<MarketplaceSettlementLegRow>().AsNoTracking()
            .Where(row => row.SettlementId == settlementId)
            .OrderBy(row => row.Currency)
            .ToArrayAsync(cancellationToken);
        var events = await _db.Set<MarketplaceEventRow>().AsNoTracking()
            .Where(row => row.SettlementId == settlementId && row.TenantId == tenantId)
            .OrderBy(row => row.Sequence)
            .ToArrayAsync(cancellationToken);
        var refunds = await _db.Set<MarketplaceRefundRow>().AsNoTracking()
            .Where(row => row.SettlementId == settlementId && row.TenantId == tenantId)
            .OrderBy(row => row.RefundedAt)
            .ThenBy(row => row.Id)
            .ToArrayAsync(cancellationToken);
        var outbox = await _db.Set<MarketplaceOutboxRow>().AsNoTracking()
            .Where(row => row.SettlementId == settlementId && row.TenantId == tenantId)
            .OrderBy(row => row.OccurredAt)
            .ThenBy(row => row.Id)
            .ToArrayAsync(cancellationToken);
        return new MarketplaceSettlementOperationalDetails(
            MapSettlement(settlement),
            Array.AsReadOnly(legs.Select(row => new MarketplaceSettlementLegOperationalStatus(
                row.Currency, row.Units, row.SellerUnits, row.PlatformFeeUnits, row.RefundedUnits)).ToArray()),
            Array.AsReadOnly(events.Select(row => new MarketplaceEventOperationalStatus(
                row.Id, row.Sequence, row.EventKind, row.EvidenceHash, row.OccurredAt)).ToArray()),
            Array.AsReadOnly(refunds.Select(MapRefund).ToArray()),
            Array.AsReadOnly(outbox.Select(MapOutbox).ToArray()));
    }

    public async ValueTask<EconomyOperationalPage<MarketplaceRefundOperationalStatus>> ListRefundsAsync(
        Guid tenantId,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        ValidateTenantAndLimit(tenantId, limit);
        var position = DecodeCursor(cursor, "Marketplace refund");
        var query = _db.Set<MarketplaceRefundRow>().AsNoTracking()
            .Where(row => row.TenantId == tenantId);
        if (position is not null)
        {
            var at = position.Value.At;
            var id = position.Value.Id;
            query = query.Where(row => row.RefundedAt < at ||
                                       row.RefundedAt == at && row.Id.CompareTo(id) > 0);
        }
        var rows = await query.OrderByDescending(row => row.RefundedAt).ThenBy(row => row.Id)
            .Take(limit + 1).ToArrayAsync(cancellationToken);
        var pageRows = rows.Take(limit).ToArray();
        return new EconomyOperationalPage<MarketplaceRefundOperationalStatus>(
            Array.AsReadOnly(pageRows.Select(MapRefund).ToArray()),
            rows.Length > limit && pageRows.Length > 0
                ? EncodeCursor(pageRows[^1].RefundedAt, pageRows[^1].Id)
                : null);
    }

    public async ValueTask<MarketplaceRefundOperationalStatus?> FindRefundAsync(
        Guid tenantId,
        Guid refundId,
        CancellationToken cancellationToken)
    {
        ValidateTenantAndId(tenantId, refundId, nameof(refundId));
        var row = await _db.Set<MarketplaceRefundRow>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == refundId && item.TenantId == tenantId,
                cancellationToken);
        return row is null ? null : MapRefund(row);
    }

    public async ValueTask<EconomyOperationalPage<MarketplaceOutboxOperationalStatus>> ListOutboxAsync(
        Guid tenantId,
        bool? published,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        ValidateTenantAndLimit(tenantId, limit);
        var position = DecodeCursor(cursor, "Marketplace outbox");
        var query = _db.Set<MarketplaceOutboxRow>().AsNoTracking()
            .Where(row => row.TenantId == tenantId);
        if (published is true) query = query.Where(row => row.PublishedAt != null);
        else if (published is false) query = query.Where(row => row.PublishedAt == null);
        if (position is not null)
        {
            var at = position.Value.At;
            var id = position.Value.Id;
            query = query.Where(row => row.OccurredAt < at ||
                                       row.OccurredAt == at && row.Id.CompareTo(id) > 0);
        }
        var rows = await query.OrderByDescending(row => row.OccurredAt).ThenBy(row => row.Id)
            .Take(limit + 1).ToArrayAsync(cancellationToken);
        var pageRows = rows.Take(limit).ToArray();
        return new EconomyOperationalPage<MarketplaceOutboxOperationalStatus>(
            Array.AsReadOnly(pageRows.Select(MapOutbox).ToArray()),
            rows.Length > limit && pageRows.Length > 0
                ? EncodeCursor(pageRows[^1].OccurredAt, pageRows[^1].Id)
                : null);
    }

    private static MarketplaceSettlementOperationalSummary MapSettlement(MarketplaceSettlementRow row) => new(
        row.Id, row.TenantId, row.OrderId, row.OrderLineItemId, row.ProductId, row.Quantity,
        row.RefundedQuantity, row.BuyerId, row.SellerId, row.PolicyVersion, row.CurrencyMode,
        row.Status, row.EntitlementId, row.EntitlementStatus, row.JournalSequence, row.ReserveVersion,
        row.JurisdictionCode, row.RefundHoldUntil, row.SettledAt, row.UpdatedAt);

    private static MarketplaceRefundOperationalStatus MapRefund(MarketplaceRefundRow row) => new(
        row.Id, row.TenantId, row.SettlementId, row.BuyerId, row.IsFullRefund,
        row.EntitlementRevoked, row.ReasonCode, row.Quantity, row.RefundedQuantity,
        row.FirstJournalSequence, row.RefundedAt);

    private static MarketplaceOutboxOperationalStatus MapOutbox(MarketplaceOutboxRow row) => new(
        row.Id, row.TenantId, row.SettlementId, row.MessageType, row.PayloadHash, row.OccurredAt,
        row.PublishedAt, row.AttemptCount, row.LeaseExpiresAt, !string.IsNullOrWhiteSpace(row.LastError));

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

    private static void ValidateTenantAndLimit(Guid tenantId, int limit)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
    }

    private static void ValidateTenantAndId(Guid tenantId, Guid id, string parameterName)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        if (id == Guid.Empty) throw new ArgumentException("Identifier cannot be empty.", parameterName);
    }
}
