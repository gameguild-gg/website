using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations;

public partial class AddEconomyFifoReservationWriter : Migration
{
    private static void InstallFifoReservationWriter(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION economy_private.validate_fragment_root_range_v1()
            RETURNS trigger
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
            DECLARE
                authoritative_units bigint;
                current_epoch bigint;
                owner_units bigint;
                owner_root uuid;
                owner_currency integer;
                trace_scale bigint;
                root_trace_scale bigint;
            BEGIN
                PERFORM pg_advisory_xact_lock(hashtextextended(NEW."RootSourceStampId"::text, 0));
                IF NEW."CreditLotId" IS NOT NULL THEN
                    SELECT lot."AmountUnits", lot."RootSourceStampId", lot."Currency"
                    INTO owner_units, owner_root, owner_currency
                    FROM public.economy_credit_lots lot WHERE lot."Id" = NEW."CreditLotId" FOR SHARE;
                ELSE
                    SELECT allocation."AmountUnits", lot."RootSourceStampId", lot."Currency"
                    INTO owner_units, owner_root, owner_currency
                    FROM public.economy_entry_allocations allocation
                    JOIN public.economy_credit_lots lot ON lot."Id" = allocation."ParentLotId"
                    WHERE allocation."Id" = NEW."EntryAllocationId" FOR SHARE OF allocation, lot;
                END IF;
                trace_scale := CASE owner_currency WHEN 1 THEN 1000 WHEN 2 THEN 1 ELSE NULL END;
                SELECT CASE WHEN EXISTS (
                    SELECT 1 FROM public.economy_credit_lots root_lot
                    WHERE root_lot."RootSourceStampId" = NEW."RootSourceStampId" AND root_lot."Currency" = 1
                ) THEN 1000 ELSE 1 END INTO root_trace_scale;
                SELECT source."AuthoritativeUnits" * root_trace_scale INTO authoritative_units
                FROM public.economy_source_stamps source
                WHERE source."Id" = NEW."RootSourceStampId" FOR SHARE;
                SELECT COALESCE(reversal."Epoch", 0) INTO current_epoch
                FROM (SELECT NEW."RootSourceStampId" AS id) expected
                LEFT JOIN public.economy_root_reversal_states reversal
                    ON reversal."RootSourceStampId" = expected.id;

                IF authoritative_units IS NULL OR owner_units IS NULL OR owner_root <> NEW."RootSourceStampId"
                   OR NEW."ReversalEpoch" <> current_epoch
                   OR NEW."EndExclusive" > authoritative_units
                   OR NEW."EndExclusive" - NEW."StartInclusive" > owner_units * trace_scale THEN
                    RAISE EXCEPTION 'root range violates source, owner, amount, or reversal epoch' USING ERRCODE = '23514';
                END IF;
                RETURN NEW;
            END
            $function$;

            CREATE OR REPLACE FUNCTION economy_private.verify_fragment_root_range_conservation_v1()
            RETURNS trigger
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
            DECLARE
                owner_units bigint;
                range_units bigint;
                owner_currency integer;
                trace_scale bigint;
            BEGIN
                IF NEW."CreditLotId" IS NOT NULL THEN
                    SELECT "AmountUnits", "Currency" INTO owner_units, owner_currency
                    FROM public.economy_credit_lots WHERE "Id" = NEW."CreditLotId";
                    SELECT COALESCE(sum("EndExclusive" - "StartInclusive"), 0) INTO range_units
                    FROM public.economy_fragment_root_ranges WHERE "CreditLotId" = NEW."CreditLotId";
                ELSE
                    SELECT allocation."AmountUnits", lot."Currency" INTO owner_units, owner_currency
                    FROM public.economy_entry_allocations allocation
                    JOIN public.economy_credit_lots lot ON lot."Id" = allocation."ParentLotId"
                    WHERE allocation."Id" = NEW."EntryAllocationId";
                    SELECT COALESCE(sum("EndExclusive" - "StartInclusive"), 0) INTO range_units
                    FROM public.economy_fragment_root_ranges WHERE "EntryAllocationId" = NEW."EntryAllocationId";
                END IF;
                trace_scale := CASE owner_currency WHEN 1 THEN 1000 WHEN 2 THEN 1 ELSE NULL END;
                IF trace_scale IS NULL OR range_units <> owner_units * trace_scale THEN
                    RAISE EXCEPTION 'root-range ownership is not conserved' USING ERRCODE = '23514';
                END IF;
                RETURN NULL;
            END
            $function$;

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
                existing record;
            BEGIN
                IF p_operation_id IS NULL OR p_wallet_id IS NULL OR p_required_units <= 0
                   OR p_currency NOT IN (1, 2) OR p_provenance NOT IN (1, 2, 3, 4)
                   OR p_purpose NOT BETWEEN 1 AND 5 OR p_reserved_at IS NULL THEN
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

                FOR candidate IN
                    SELECT lot."Id", lot."RootSourceStampId", lot."ConfirmedAt", lot."JournalSequence"
                    FROM public.economy_credit_lots lot
                    WHERE lot."WalletId" = p_wallet_id
                      AND lot."Currency" = p_currency
                      AND lot."Provenance" = p_provenance
                      AND lot."State" = 1
                    ORDER BY lot."ConfirmedAt", lot."JournalSequence", lot."Id"
                    FOR UPDATE
                LOOP
                    EXIT WHEN remaining_trace = 0;

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
                                  AND reservation."Status" = 1
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

            CREATE OR REPLACE FUNCTION economy_private.transition_fifo_fragment_reservations_v1(
                p_operation_id uuid,
                p_expected_status integer,
                p_next_status integer,
                p_terminal_at timestamptz)
            RETURNS bigint
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
            DECLARE
                changed_count bigint;
            BEGIN
                IF p_operation_id IS NULL OR p_expected_status NOT BETWEEN 1 AND 3
                   OR p_next_status NOT BETWEEN 1 AND 3 OR p_terminal_at IS NULL
                   OR NOT ((p_expected_status = 1 AND p_next_status IN (2, 3))) THEN
                    RAISE EXCEPTION 'FIFO reservation transition is invalid' USING ERRCODE = '22023';
                END IF;

                UPDATE public.economy_fragment_reservations
                SET "Status" = p_next_status, "TerminalAt" = p_terminal_at
                WHERE "OperationId" = p_operation_id AND "Status" = p_expected_status;
                GET DIAGNOSTICS changed_count = ROW_COUNT;
                IF changed_count = 0 THEN
                    RAISE EXCEPTION 'FIFO reservation is stale or absent' USING ERRCODE = '40001';
                END IF;
                RETURN changed_count;
            END
            $function$;

            ALTER FUNCTION economy_private.reserve_fifo_fragments_v1(uuid,uuid,integer,integer,bigint,integer,timestamptz)
                OWNER TO gameguild_economy_procedure_owner;
            ALTER FUNCTION economy_private.transition_fifo_fragment_reservations_v1(uuid,integer,integer,timestamptz)
                OWNER TO gameguild_economy_procedure_owner;
            REVOKE ALL ON FUNCTION economy_private.reserve_fifo_fragments_v1(uuid,uuid,integer,integer,bigint,integer,timestamptz) FROM PUBLIC;
            REVOKE ALL ON FUNCTION economy_private.transition_fifo_fragment_reservations_v1(uuid,integer,integer,timestamptz) FROM PUBLIC;
            GRANT EXECUTE ON FUNCTION economy_private.reserve_fifo_fragments_v1(uuid,uuid,integer,integer,bigint,integer,timestamptz),
                economy_private.transition_fifo_fragment_reservations_v1(uuid,integer,integer,timestamptz)
                TO gameguild_economy_writer;

            REVOKE ALL ON TABLE public.economy_fragment_reservations FROM PUBLIC;
            REVOKE ALL ON TABLE public.economy_fragment_reservations FROM gameguild_economy_writer;
            GRANT SELECT ON TABLE public.economy_fragment_reservations TO gameguild_economy_runtime;
            GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.economy_fragment_reservations TO gameguild_economy_procedure_owner;
            GRANT ALL ON TABLE public.economy_fragment_reservations TO gameguild_economy_migration;

            CREATE TRIGGER deny_fragment_reservation_delete
                BEFORE DELETE ON public.economy_fragment_reservations
                FOR EACH ROW EXECUTE FUNCTION economy_private.deny_immutable_mutation_v1();
            """);
    }

    private static void RemoveFifoReservationWriter(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS deny_fragment_reservation_delete ON public.economy_fragment_reservations;
            DROP FUNCTION IF EXISTS economy_private.transition_fifo_fragment_reservations_v1(uuid,integer,integer,timestamptz);
            DROP FUNCTION IF EXISTS economy_private.reserve_fifo_fragments_v1(uuid,uuid,integer,integer,bigint,integer,timestamptz);
            """);
    }
}
