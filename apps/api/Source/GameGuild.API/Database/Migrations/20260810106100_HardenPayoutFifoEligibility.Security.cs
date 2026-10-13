using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations;

public partial class HardenPayoutFifoEligibility : Migration
{
    internal static void InstallHardenedPayoutFifoEligibility(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION economy_private.reserve_fifo_fragments_v1(
                            p_operation_id uuid,
                            p_wallet_id uuid,
                            p_currency integer,
                            p_provenance integer,
                            p_required_units bigint,
                            p_purpose integer,
                            p_reserved_at timestamptz)
                        RETURNS TABLE(
                            reservation_id uuid,
                            parent_lot_id uuid,
                            root_source_stamp_id uuid,
                            reversal_epoch bigint,
                            start_inclusive bigint,
                            end_exclusive bigint,
                            amount_units bigint)
                        LANGUAGE plpgsql
                        SECURITY DEFINER
                        SET search_path = pg_catalog, economy_private
                        AS $function$
                        DECLARE
                            candidate record;
                            source_range record;
                            free_range record;
                            trace_scale bigint;
                            required_trace bigint;
                            remaining_trace bigint;
                            selected_trace bigint;
                            candidate_held boolean;
                            existing record;
                        BEGIN
                            IF p_operation_id IS NULL OR p_wallet_id IS NULL OR p_required_units <= 0
                               OR p_currency NOT IN (1, 2) OR p_provenance NOT BETWEEN 1 AND 8
                               OR p_purpose NOT BETWEEN 1 AND 7 OR p_reserved_at IS NULL THEN
                                RAISE EXCEPTION 'FIFO reservation arguments are invalid' USING ERRCODE = '22023';
                            END IF;

                            SELECT COUNT(*) INTO selected_trace
                            FROM public.economy_fragment_reservations reservation
                            WHERE reservation."OperationId" = p_operation_id;
                            IF selected_trace > 0 THEN
                                IF EXISTS (
                                    SELECT 1 FROM public.economy_fragment_reservations reservation
                                    WHERE reservation."OperationId" = p_operation_id
                                      AND (reservation."WalletId" <> p_wallet_id OR reservation."Currency" <> p_currency
                                           OR reservation."Purpose" <> p_purpose)
                                ) THEN
                                    RAISE EXCEPTION 'FIFO reservation operation is bound to another request' USING ERRCODE = '23505';
                                END IF;
                                RETURN QUERY
                                SELECT reservation."Id", reservation."ParentLotId", reservation."RootSourceStampId",
                                       reservation."ReversalEpoch", reservation."StartInclusive", reservation."EndExclusive",
                                       (reservation."EndExclusive" - reservation."StartInclusive") /
                                       CASE WHEN reservation."Currency" = 1 THEN 1000 ELSE 1 END
                                FROM public.economy_fragment_reservations reservation
                                JOIN public.economy_credit_lots lot ON lot."Id" = reservation."ParentLotId"
                                WHERE reservation."OperationId" = p_operation_id
                                ORDER BY lot."ConfirmedAt", lot."JournalSequence", lot."Id", reservation."StartInclusive";
                                RETURN;
                            END IF;

                            trace_scale := CASE WHEN p_currency = 1 THEN 1000 ELSE 1 END;
                            required_trace := p_required_units * trace_scale;
                            remaining_trace := required_trace;

                            PERFORM 1
                            FROM public.economy_wallets wallet
                            WHERE wallet."Id" = p_wallet_id
                            FOR SHARE;
                            IF NOT FOUND THEN
                                RAISE EXCEPTION 'FIFO reservation wallet does not exist' USING ERRCODE = '23503';
                            END IF;

                            IF p_purpose IN (1, 2) AND (p_currency <> 1 OR p_provenance <> 2) THEN
                                RAISE EXCEPTION 'cash-out requires mature earned hard fragments' USING ERRCODE = '22023';
                            END IF;

                            IF p_purpose IN (1, 2) AND EXISTS (
                                SELECT 1
                                FROM public.economy_holds hold
                                WHERE hold."WalletId" = p_wallet_id
                                  AND hold."Currency" = 1
                                  AND hold."Status" = 1
                                  AND hold."EffectiveAt" <= p_reserved_at
                            ) THEN
                                RAISE EXCEPTION 'cash-out is blocked by an active hard-coin hold' USING ERRCODE = 'P0001';
                            END IF;

                            IF p_purpose IN (1, 2) AND EXISTS (
                                SELECT 1
                                FROM public.economy_wallet_debts debt
                                WHERE debt."WalletId" = p_wallet_id
                                  AND debt."OutstandingHardUnits" > 0
                            ) THEN
                                RAISE EXCEPTION 'cash-out is blocked by an outstanding hard-coin debt' USING ERRCODE = 'P0001';
                            END IF;

                            FOR candidate IN
                                SELECT lot."Id", lot."RootSourceStampId", lot."ConfirmedAt", lot."JournalSequence"
                                FROM public.economy_credit_lots lot
                                JOIN public.economy_source_stamps source_stamp
                                  ON source_stamp."Id" = lot."RootSourceStampId"
                                WHERE lot."WalletId" = p_wallet_id
                                  AND lot."Currency" = p_currency
                                  AND lot."Provenance" = p_provenance
                                  AND lot."State" = 1
                                  AND (
                                      p_purpose NOT IN (1, 2) OR (
                                          lot."CashOutEligible"
                                          AND lot."OriginalMaturesAt" <= p_reserved_at
                                          AND source_stamp."State" = 2
                                          AND source_stamp."ConfirmedAt" IS NOT NULL
                                          AND source_stamp."ConfirmedAt" <= p_reserved_at
                                      )
                                  )
                                ORDER BY lot."ConfirmedAt", lot."JournalSequence", lot."Id"
                                FOR UPDATE
                            LOOP
                                EXIT WHEN remaining_trace = 0;

                                -- Marketplace proceeds remain unavailable while their
                                -- refund-window hold is active. Resolve this relation
                                -- dynamically so a migration downgrade can retain the
                                -- hardened FIFO function after Marketplace tables leave.
                                candidate_held := false;
                                IF pg_catalog.to_regclass('public.economy_marketplace_settlement_credits') IS NOT NULL THEN
                                    EXECUTE $held$
                                        SELECT EXISTS (
                                            SELECT 1
                                            FROM public.economy_marketplace_settlement_credits credit
                                            JOIN public.economy_holds hold
                                              ON hold."Id" = credit."RefundHoldId"
                                            WHERE credit."CreditLotId" = $1
                                              AND hold."Status" = 1
                                              AND hold."EffectiveAt" <= $2)
                                    $held$ INTO candidate_held USING candidate."Id", p_reserved_at;
                                END IF;
                                IF candidate_held THEN
                                    CONTINUE;
                                END IF;

                                FOR source_range IN
                                    SELECT range_row."RootSourceStampId", range_row."ReversalEpoch",
                                           range_row."StartInclusive", range_row."EndExclusive"
                                    FROM public.economy_fragment_root_ranges range_row
                                    JOIN public.economy_root_reversal_states reversal
                                      ON reversal."RootSourceStampId" = range_row."RootSourceStampId"
                                     AND reversal."Epoch" = range_row."ReversalEpoch"
                                     AND reversal."State" = 'active'
                                    WHERE range_row."CreditLotId" = candidate."Id"
                                    ORDER BY range_row."RootSourceStampId", range_row."StartInclusive", range_row."EndExclusive"
                                LOOP
                                    EXIT WHEN remaining_trace = 0;

                                    FOR free_range IN
                                        WITH blocked AS (
                                            SELECT int8range(range_row."StartInclusive", range_row."EndExclusive", '[)') AS fragment
                                            FROM public.economy_entry_allocations allocation
                                            JOIN public.economy_fragment_root_ranges range_row
                                              ON range_row."EntryAllocationId" = allocation."Id"
                                            WHERE allocation."ParentLotId" = candidate."Id"
                                              AND range_row."RootSourceStampId" = source_range."RootSourceStampId"
                                              AND range_row."ReversalEpoch" = source_range."ReversalEpoch"
                                            UNION ALL
                                            SELECT int8range(reservation."StartInclusive", reservation."EndExclusive", '[)')
                                            FROM public.economy_fragment_reservations reservation
                                            WHERE reservation."ParentLotId" = candidate."Id"
                                              AND reservation."RootSourceStampId" = source_range."RootSourceStampId"
                                              AND reservation."ReversalEpoch" = source_range."ReversalEpoch"
                                              AND reservation."Status" IN (1, 4)
                                        )
                                        SELECT lower(fragment)::bigint AS start_inclusive, upper(fragment)::bigint AS end_exclusive
                                        FROM unnest(
                                            int8multirange(int8range(source_range."StartInclusive", source_range."EndExclusive", '[)')) -
                                            COALESCE((SELECT range_agg(fragment) FROM blocked), '{}'::int8multirange)
                                        ) AS fragment
                                        ORDER BY lower(fragment), upper(fragment)
                                    LOOP
                                        EXIT WHEN remaining_trace = 0;
                                        selected_trace := LEAST(remaining_trace, free_range.end_exclusive - free_range.start_inclusive);
                                        IF mod(selected_trace, trace_scale) <> 0 THEN
                                            selected_trace := selected_trace - mod(selected_trace, trace_scale);
                                        END IF;
                                        IF selected_trace <= 0 THEN
                                            CONTINUE;
                                        END IF;

                                        INSERT INTO public.economy_fragment_reservations (
                                            "Id", "OperationId", "ParentLotId", "WalletId", "Currency", "Purpose", "Status",
                                            "RootSourceStampId", "ReversalEpoch", "StartInclusive", "EndExclusive", "ReservedAt", "TerminalAt")
                                        VALUES (
                                            gen_random_uuid(), p_operation_id, candidate."Id", p_wallet_id, p_currency, p_purpose, 1,
                                            source_range."RootSourceStampId", source_range."ReversalEpoch", free_range.start_inclusive,
                                            free_range.start_inclusive + selected_trace, p_reserved_at, NULL);
                                        remaining_trace := remaining_trace - selected_trace;
                                    END LOOP;
                                END LOOP;
                            END LOOP;

                            IF remaining_trace <> 0 THEN
                                RAISE EXCEPTION 'FIFO reservation has insufficient confirmed fragments' USING ERRCODE = 'P0001';
                            END IF;

                            RETURN QUERY
                            SELECT reservation."Id", reservation."ParentLotId", reservation."RootSourceStampId",
                                   reservation."ReversalEpoch", reservation."StartInclusive", reservation."EndExclusive",
                                   (reservation."EndExclusive" - reservation."StartInclusive") / trace_scale
                            FROM public.economy_fragment_reservations reservation
                            JOIN public.economy_credit_lots lot ON lot."Id" = reservation."ParentLotId"
                            WHERE reservation."OperationId" = p_operation_id
                            ORDER BY lot."ConfirmedAt", lot."JournalSequence", lot."Id", reservation."StartInclusive";
                        END
                        $function$;

            ALTER FUNCTION economy_private.reserve_fifo_fragments_v1(uuid,uuid,integer,integer,bigint,integer,timestamptz)
                OWNER TO gameguild_economy_procedure_owner;
            DO $acl$
            BEGIN
                IF pg_catalog.to_regclass('public.economy_marketplace_settlement_credits') IS NOT NULL THEN
                    EXECUTE 'GRANT SELECT ON TABLE public.economy_marketplace_settlement_credits TO gameguild_economy_procedure_owner';
                END IF;
            END
            $acl$;
            REVOKE ALL ON FUNCTION economy_private.reserve_fifo_fragments_v1(uuid,uuid,integer,integer,bigint,integer,timestamptz) FROM PUBLIC;
            GRANT EXECUTE ON FUNCTION economy_private.reserve_fifo_fragments_v1(uuid,uuid,integer,integer,bigint,integer,timestamptz)
                TO gameguild_economy_writer;
            """);
    }
}
