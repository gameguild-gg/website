using System.Text.Json;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Bounties;

public interface IBountyPostableLotReader
{
    IReadOnlyList<CreditLot> Read(WalletId walletId, CurrencyCode currency, DateTimeOffset asOf);
}

/// <summary>
/// Reads only active, confirmed fragments that are not already allocated or reserved. The
/// returned lots retain their immutable source ranges so the bounty writer can prove FIFO use.
/// </summary>
public sealed class PostgreSqlBountyPostableLotReader : IBountyPostableLotReader
{
    private readonly DbContext _db;

    public PostgreSqlBountyPostableLotReader(IApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "PostgreSQL bounty lot reads require the application's relational DbContext.");
    }

    public IReadOnlyList<CreditLot> Read(WalletId walletId, CurrencyCode currency, DateTimeOffset asOf)
    {
        if (!Enum.IsDefined(currency)) throw new ArgumentOutOfRangeException(nameof(currency));

        return _db.Database.SqlQuery<BountyPostableLotProjection>($"""
            WITH available_ranges AS (
                SELECT lot."Id", lot."WalletId", lot."Currency", lot."Provenance", lot."ConfirmedAt",
                       lot."OriginalMaturesAt", lot."JournalSequence", lot."ReversalEpoch",
                       root_range."RootSourceStampId", root_range."ReversalEpoch" AS "RangeEpoch",
                       lower(free_range.fragment)::bigint AS "StartInclusive",
                       upper(free_range.fragment)::bigint AS "EndExclusive"
                FROM public.economy_credit_lots lot
                JOIN public.economy_fragment_root_ranges root_range
                  ON root_range."CreditLotId" = lot."Id"
                JOIN public.economy_root_reversal_states reversal
                  ON reversal."RootSourceStampId" = root_range."RootSourceStampId"
                 AND reversal."Epoch" = root_range."ReversalEpoch"
                 AND reversal."State" = 'active'
                CROSS JOIN LATERAL (
                    SELECT fragment
                    FROM unnest(
                        int8multirange(int8range(root_range."StartInclusive", root_range."EndExclusive", '[)')) -
                        COALESCE((
                            SELECT range_agg(blocked.fragment)
                            FROM (
                                SELECT int8range(allocation_range."StartInclusive", allocation_range."EndExclusive", '[)') AS fragment
                                FROM public.economy_entry_allocations allocation
                                JOIN public.economy_fragment_root_ranges allocation_range
                                  ON allocation_range."EntryAllocationId" = allocation."Id"
                                WHERE allocation."ParentLotId" = lot."Id"
                                  AND allocation_range."RootSourceStampId" = root_range."RootSourceStampId"
                                  AND allocation_range."ReversalEpoch" = root_range."ReversalEpoch"
                                UNION ALL
                                SELECT int8range(reservation."StartInclusive", reservation."EndExclusive", '[)') AS fragment
                                FROM public.economy_fragment_reservations reservation
                                WHERE reservation."ParentLotId" = lot."Id"
                                  AND reservation."RootSourceStampId" = root_range."RootSourceStampId"
                                  AND reservation."ReversalEpoch" = root_range."ReversalEpoch"
                                  AND reservation."Status" = 1
                            ) AS blocked
                        ), int8multirange())
                    ) AS fragment
                ) AS free_range
                WHERE lot."WalletId" = {walletId.Value}
                  AND lot."Currency" = {(int)currency}
                  AND lot."State" = 1
                  AND lot."ConfirmedAt" <= {asOf}
            )
            SELECT "Id", "WalletId", "Currency", "Provenance", "ConfirmedAt", "OriginalMaturesAt", "JournalSequence",
                   sum("EndExclusive" - "StartInclusive") /
                       CASE WHEN "Currency" = 1 THEN 1000 ELSE 1 END AS "AmountUnits",
                   jsonb_agg(jsonb_build_object(
                       'RootSourceStampId', "RootSourceStampId",
                       'StartInclusive', "StartInclusive",
                       'EndExclusive', "EndExclusive",
                       'ReversalEpoch', "RangeEpoch")
                       ORDER BY "RootSourceStampId", "StartInclusive", "EndExclusive")::text AS "RootRanges"
            FROM available_ranges
            GROUP BY "Id", "WalletId", "Currency", "Provenance", "ConfirmedAt", "OriginalMaturesAt", "JournalSequence"
            HAVING sum("EndExclusive" - "StartInclusive") > 0
            ORDER BY "ConfirmedAt", "JournalSequence", "Id"
            """)
            .AsEnumerable()
            .Select(ToCreditLot)
            .ToArray();
    }

    private static CreditLot ToCreditLot(BountyPostableLotProjection row)
    {
        var ranges = JsonSerializer.Deserialize<RootRangeProjection[]>(row.RootRanges)
            ?? throw new InvalidOperationException("Bounty lot root ranges are missing.");
        var currency = (CurrencyCode)row.Currency;
        var provenance = (ProvenanceKind)row.Provenance;
        if (!Enum.IsDefined(currency) || !Enum.IsDefined(provenance))
            throw new InvalidOperationException("Bounty lot has an unknown currency or provenance.");
        var scale = CurrencyTraceScale.For(currency);
        if (row.AmountUnits <= 0 || ranges.Length == 0 ||
            ranges.Sum(range => checked(range.EndExclusive - range.StartInclusive)) != checked(row.AmountUnits * scale))
            throw new InvalidOperationException("Bounty lot root ranges do not conserve the available amount.");

        return new CreditLot(
            new CreditLotId(row.Id),
            new WalletId(row.WalletId),
            new CoinAmount(currency, row.AmountUnits),
            provenance,
            row.ConfirmedAt,
            row.OriginalMaturesAt,
            row.JournalSequence,
            CreditLotState.Active,
            ranges.Select(range => new RootTraceRange(
                new SourceStampId(range.RootSourceStampId),
                range.StartInclusive,
                checked(range.EndExclusive - range.StartInclusive),
                range.ReversalEpoch)).ToArray(),
            scale);
    }

    private sealed class BountyPostableLotProjection
    {
        public Guid Id { get; init; }
        public Guid WalletId { get; init; }
        public int Currency { get; init; }
        public int Provenance { get; init; }
        public DateTimeOffset ConfirmedAt { get; init; }
        public DateTimeOffset OriginalMaturesAt { get; init; }
        public long JournalSequence { get; init; }
        public long AmountUnits { get; init; }
        public string RootRanges { get; init; } = "[]";
    }

    private sealed record RootRangeProjection(
        Guid RootSourceStampId,
        long StartInclusive,
        long EndExclusive,
        long ReversalEpoch);
}
