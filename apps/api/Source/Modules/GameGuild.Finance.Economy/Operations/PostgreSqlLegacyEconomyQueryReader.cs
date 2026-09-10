using System.Globalization;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Operations;

public sealed record LegacyEconomyShadowBatchSummary(
    Guid Id,
    Guid TenantId,
    LegacyEconomyShadowState State,
    string JurisdictionCode,
    long PolicyVersion,
    int WalletCount,
    int TransactionCount,
    int FinancialLedgerEntryCount,
    long ExpectedHardUnits,
    long BackfilledHardUnits,
    long ReconciledHardUnits,
    string? FailureCode,
    DateTimeOffset CapturedAt,
    DateTimeOffset UpdatedAt,
    long Version);

public interface ILegacyEconomyQueryReader
{
    ValueTask<EconomyOperationalPage<LegacyEconomyShadowBatchSummary>> ListAsync(
        Guid tenantId,
        LegacyEconomyShadowState? state,
        int limit,
        string? cursor,
        CancellationToken cancellationToken);
}

public sealed class PostgreSqlLegacyEconomyQueryReader : ILegacyEconomyQueryReader
{
    private readonly DbContext _db;

    public PostgreSqlLegacyEconomyQueryReader(IApplicationDbContext context) =>
        _db = PostgreSqlEntityRiskGraphStore.RequireRelationalContext(context);

    public async ValueTask<EconomyOperationalPage<LegacyEconomyShadowBatchSummary>> ListAsync(
        Guid tenantId,
        LegacyEconomyShadowState? state,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        var position = DecodeCursor(cursor);
        var query = _db.Set<EconomyLegacyShadowBatchRow>().AsNoTracking()
            .Where(row => row.TenantId == tenantId);
        if (state is not null)
        {
            var storedState = ToStoredState(state.Value);
            query = query.Where(row => row.State == storedState);
        }
        if (position is not null)
        {
            var at = position.Value.At;
            var id = position.Value.Id;
            query = query.Where(row => row.UpdatedAt < at ||
                                       row.UpdatedAt == at && row.Id.CompareTo(id) > 0);
        }
        var rows = await query.OrderByDescending(row => row.UpdatedAt).ThenBy(row => row.Id)
            .Take(limit + 1).ToArrayAsync(cancellationToken);
        var pageRows = rows.Take(limit).ToArray();
        return new EconomyOperationalPage<LegacyEconomyShadowBatchSummary>(
            Array.AsReadOnly(pageRows.Select(Map).ToArray()),
            rows.Length > limit && pageRows.Length > 0
                ? EncodeCursor(pageRows[^1].UpdatedAt, pageRows[^1].Id)
                : null);
    }

    private static LegacyEconomyShadowBatchSummary Map(EconomyLegacyShadowBatchRow row) => new(
        row.Id, row.TenantId, ToPublicState(row.State), row.JurisdictionCode, row.PolicyVersion,
        row.WalletCount, row.TransactionCount, row.FinancialLedgerEntryCount, row.ExpectedHardUnits,
        row.BackfilledHardUnits, row.ReconciledHardUnits, row.FailureCode, row.CapturedAt,
        row.UpdatedAt, row.Version);

    internal static LegacyEconomyShadowState ToPublicState(EconomyLegacyShadowBatchState state) => state switch
    {
        EconomyLegacyShadowBatchState.Captured => LegacyEconomyShadowState.Captured,
        EconomyLegacyShadowBatchState.Backfilling => LegacyEconomyShadowState.Backfilling,
        EconomyLegacyShadowBatchState.Backfilled => LegacyEconomyShadowState.Backfilled,
        EconomyLegacyShadowBatchState.Reconciled => LegacyEconomyShadowState.Reconciled,
        EconomyLegacyShadowBatchState.CutoverProposed => LegacyEconomyShadowState.CutoverProposed,
        EconomyLegacyShadowBatchState.CutoverActive => LegacyEconomyShadowState.CutoverActive,
        EconomyLegacyShadowBatchState.RolledBack => LegacyEconomyShadowState.RolledBack,
        EconomyLegacyShadowBatchState.Failed => LegacyEconomyShadowState.Failed,
        _ => throw new InvalidOperationException("Unknown legacy Economy shadow state.")
    };

    private static EconomyLegacyShadowBatchState ToStoredState(LegacyEconomyShadowState state) => state switch
    {
        LegacyEconomyShadowState.Captured => EconomyLegacyShadowBatchState.Captured,
        LegacyEconomyShadowState.Backfilling => EconomyLegacyShadowBatchState.Backfilling,
        LegacyEconomyShadowState.Backfilled => EconomyLegacyShadowBatchState.Backfilled,
        LegacyEconomyShadowState.Reconciled => EconomyLegacyShadowBatchState.Reconciled,
        LegacyEconomyShadowState.CutoverProposed => EconomyLegacyShadowBatchState.CutoverProposed,
        LegacyEconomyShadowState.CutoverActive => EconomyLegacyShadowBatchState.CutoverActive,
        LegacyEconomyShadowState.RolledBack => EconomyLegacyShadowBatchState.RolledBack,
        LegacyEconomyShadowState.Failed => EconomyLegacyShadowBatchState.Failed,
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };

    private static string EncodeCursor(DateTimeOffset at, Guid id) => $"{at.UtcTicks:X16}{id:N}";

    private static (DateTimeOffset At, Guid Id)? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        if (cursor.Length != 48 ||
            !long.TryParse(cursor.AsSpan(0, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                out var ticks) || !Guid.TryParseExact(cursor[16..], "N", out var id) ||
            ticks < DateTimeOffset.MinValue.UtcTicks || ticks > DateTimeOffset.MaxValue.UtcTicks)
            throw new ArgumentException("Legacy Economy batch cursor is invalid.", nameof(cursor));
        return (new DateTimeOffset(ticks, TimeSpan.Zero), id);
    }
}
