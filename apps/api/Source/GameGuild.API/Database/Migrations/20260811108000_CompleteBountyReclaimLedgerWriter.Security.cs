using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations;

public partial class CompleteBountyReclaimLedgerWriter : Migration
{
    private static void InstallBountyReclaimLedgerWriter(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION economy_private.complete_bounty_reclaim_v1(
                p_bounty_id uuid,
                p_poster_id uuid,
                p_poster_wallet_id uuid,
                p_idempotency_key text,
                p_posting_id uuid,
                p_risk_decision_id uuid,
                p_reclaimed_at timestamptz)
            RETURNS void
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
            DECLARE
                bounty public.economy_bounties%ROWTYPE;
                existing public.economy_bounty_terminal_events%ROWTYPE;
                posting record;
                fragment record;
                escrow_lot public.economy_credit_lots%ROWTYPE;
                root_range record;
                output_lot_id uuid;
                first_output_lot_id uuid;
                output_lots jsonb := '[]'::jsonb;
                fee_units bigint;
                returned_units bigint;
                remaining_return bigint;
                return_segment bigint;
                fee_segment bigint;
                allocated_units bigint;
                total_allocated_units bigint;
                expected_sequence integer := 1;
                expected_account integer;
                expected_fee_account integer;
                trace_remaining bigint;
                trace_segment bigint;
                line_count integer;
            BEGIN
                IF p_bounty_id IS NULL OR p_poster_id IS NULL OR p_poster_wallet_id IS NULL
                   OR p_posting_id IS NULL OR p_risk_decision_id IS NULL
                   OR p_idempotency_key IS NULL OR length(btrim(p_idempotency_key)) = 0
                   OR p_reclaimed_at IS NULL THEN
                    RAISE EXCEPTION 'invalid durable bounty reclaim arguments' USING ERRCODE = '22023';
                END IF;

                SELECT * INTO bounty
                FROM public.economy_bounties
                WHERE "Id" = p_bounty_id
                FOR UPDATE;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'bounty escrow was not found' USING ERRCODE = 'P0002';
                END IF;

                SELECT * INTO existing
                FROM public.economy_bounty_terminal_events
                WHERE "BountyId" = p_bounty_id OR "IdempotencyKey" = btrim(p_idempotency_key)
                FOR UPDATE;
                IF FOUND THEN
                    IF existing."BountyId" <> p_bounty_id
                       OR existing."IdempotencyKey" <> btrim(p_idempotency_key)
                       OR existing."Status" <> 4
                       OR existing."ActorId" <> p_poster_id
                       OR existing."DestinationWalletId" <> p_poster_wallet_id
                       OR existing."RiskDecisionId" <> p_risk_decision_id THEN
                        RAISE EXCEPTION 'bounty reclaim idempotency key conflicts with immutable terminal outcome' USING ERRCODE = '23505';
                    END IF;
                    RETURN;
                END IF;

                IF bounty."Status" <> 1 THEN
                    RAISE EXCEPTION 'bounty already has a terminal outcome' USING ERRCODE = '23514';
                END IF;
                IF p_reclaimed_at < bounty."ExpiresAt" THEN
                    RAISE EXCEPTION 'bounty cannot be reclaimed before expiry' USING ERRCODE = '23514';
                END IF;
                IF bounty."PosterId" <> p_poster_id OR bounty."PosterWalletId" <> p_poster_wallet_id THEN
                    RAISE EXCEPTION 'only the bounty poster can reclaim this escrow' USING ERRCODE = '42501';
                END IF;

                SELECT group_record."Id", group_record."TemplateKind", group_record."Authority",
                       group_record."ActorId", group_record."TenantId", group_record."RiskDecisionId",
                       group_record."PolicyVersion", entry."Sequence"
                INTO posting
                FROM public.economy_posting_groups group_record
                JOIN public.economy_journal_entries entry ON entry."PostingGroupId" = group_record."Id"
                WHERE group_record."Id" = p_posting_id
                  AND group_record."IdempotencyKey" = btrim(p_idempotency_key)
                FOR SHARE;
                IF NOT FOUND OR posting."TemplateKind" <> 24 OR posting."Authority" <> 4
                   OR posting."ActorId" <> p_poster_id OR posting."RiskDecisionId" <> p_risk_decision_id THEN
                    RAISE EXCEPTION 'bounty reclaim posting does not bind the poster, authority, and risk decision' USING ERRCODE = '23514';
                END IF;
                IF NOT EXISTS (
                    SELECT 1
                    FROM public.economy_risk_decisions decision
                    WHERE decision."Id" = p_risk_decision_id
                      AND decision."Outcome" = 1
                      AND decision."TemplateKind" = 24
                      AND decision."AmountUnits" = bounty."AmountUnits"
                      AND decision."Currency" = bounty."Currency") THEN
                    RAISE EXCEPTION 'bounty reclaim risk decision is absent, denied, or mismatched' USING ERRCODE = '42501';
                END IF;

                fee_units := floor((bounty."AmountUnits"::numeric * bounty."ReclaimFeePpm"::numeric) / 1000000)::bigint;
                returned_units := bounty."AmountUnits" - fee_units;
                remaining_return := returned_units;
                expected_fee_account := CASE bounty."Currency" WHEN 1 THEN 14 WHEN 2 THEN 6 ELSE NULL END;
                IF expected_fee_account IS NULL OR returned_units <= 0 THEN
                    RAISE EXCEPTION 'bounty reclaim requires a supported coin currency and positive restored value' USING ERRCODE = '23514';
                END IF;

                FOR fragment IN
                    SELECT *
                    FROM public.economy_bounty_escrow_fragments
                    WHERE "BountyId" = p_bounty_id
                    ORDER BY "EscrowLotId"::text
                    FOR UPDATE
                LOOP
                    IF fragment."EscrowLotId" IS NULL THEN
                        RAISE EXCEPTION 'bounty reclaim requires materialized escrow lots' USING ERRCODE = '23514';
                    END IF;
                    SELECT * INTO escrow_lot
                    FROM public.economy_credit_lots lot
                    WHERE lot."Id" = fragment."EscrowLotId"
                    FOR UPDATE;
                    IF NOT FOUND OR escrow_lot."WalletId" <> bounty."EscrowWalletId"
                       OR escrow_lot."Currency" <> bounty."Currency"
                       OR escrow_lot."AmountUnits" <> fragment."AmountUnits"
                       OR escrow_lot."Provenance" <> fragment."Provenance"
                       OR escrow_lot."State" <> 1 THEN
                        RAISE EXCEPTION 'bounty escrow lot is not active and bound to the open bounty' USING ERRCODE = '23514';
                    END IF;

                    return_segment := LEAST(fragment."AmountUnits", remaining_return);
                    fee_segment := fragment."AmountUnits" - return_segment;
                    expected_account := CASE
                        WHEN bounty."Currency" = 1 AND fragment."Provenance" = 2 THEN 3
                        WHEN bounty."Currency" = 1 AND fragment."Provenance" = 1 THEN 2
                        WHEN bounty."Currency" = 2 AND fragment."Provenance" BETWEEN 3 AND 7 THEN 4
                        ELSE NULL
                    END;
                    IF expected_account IS NULL THEN
                        RAISE EXCEPTION 'bounty reclaim fragment provenance is not compatible with its currency' USING ERRCODE = '23514';
                    END IF;

                    IF return_segment > 0 THEN
                        IF NOT EXISTS (
                            SELECT 1
                            FROM public.economy_journal_entries entry
                            JOIN public.economy_journal_lines line ON line."JournalEntryId" = entry."Id"
                            JOIN public.economy_accounts account ON account."Id" = line."AccountId"
                            WHERE entry."PostingGroupId" = p_posting_id
                              AND line."Sequence" = expected_sequence
                              AND line."Side" = 1
                              AND account."Code" = CASE bounty."Currency" WHEN 1 THEN 9 ELSE 10 END
                              AND line."WalletId" IS NULL AND line."CreditLotId" IS NULL
                              AND line."Currency" = bounty."Currency" AND line."Provenance" IS NULL
                              AND line."AmountUnits" = return_segment) OR NOT EXISTS (
                            SELECT 1
                            FROM public.economy_journal_entries entry
                            JOIN public.economy_journal_lines line ON line."JournalEntryId" = entry."Id"
                            JOIN public.economy_accounts account ON account."Id" = line."AccountId"
                            WHERE entry."PostingGroupId" = p_posting_id
                              AND line."Sequence" = expected_sequence + 1
                              AND line."Side" = 2 AND account."Code" = expected_account
                              AND line."WalletId" = p_poster_wallet_id AND line."CreditLotId" IS NULL
                              AND line."Currency" = bounty."Currency" AND line."Provenance" = fragment."Provenance"
                              AND line."AmountUnits" = return_segment) THEN
                            RAISE EXCEPTION 'bounty reclaim return pair does not match the immutable escrow fragment' USING ERRCODE = '23514';
                        END IF;
                        SELECT COALESCE(sum(allocation."AmountUnits"), 0) INTO allocated_units
                        FROM public.economy_entry_allocations allocation
                        JOIN public.economy_journal_lines line ON line."Id" = allocation."JournalLineId"
                        JOIN public.economy_journal_entries entry ON entry."Id" = line."JournalEntryId"
                        WHERE entry."PostingGroupId" = p_posting_id
                          AND line."Sequence" = expected_sequence
                          AND allocation."ParentLotId" = fragment."EscrowLotId";
                        IF allocated_units <> return_segment THEN
                            RAISE EXCEPTION 'bounty reclaim return allocation is not exactly bound to the escrow lot' USING ERRCODE = '23514';
                        END IF;
                        expected_sequence := expected_sequence + 2;
                        remaining_return := remaining_return - return_segment;
                    END IF;

                    IF fee_segment > 0 THEN
                        IF NOT EXISTS (
                            SELECT 1
                            FROM public.economy_journal_entries entry
                            JOIN public.economy_journal_lines line ON line."JournalEntryId" = entry."Id"
                            JOIN public.economy_accounts account ON account."Id" = line."AccountId"
                            WHERE entry."PostingGroupId" = p_posting_id
                              AND line."Sequence" = expected_sequence AND line."Side" = 1
                              AND account."Code" = CASE bounty."Currency" WHEN 1 THEN 9 ELSE 10 END
                              AND line."WalletId" IS NULL AND line."CreditLotId" IS NULL
                              AND line."Currency" = bounty."Currency" AND line."Provenance" IS NULL
                              AND line."AmountUnits" = fee_segment) OR NOT EXISTS (
                            SELECT 1
                            FROM public.economy_journal_entries entry
                            JOIN public.economy_journal_lines line ON line."JournalEntryId" = entry."Id"
                            JOIN public.economy_accounts account ON account."Id" = line."AccountId"
                            WHERE entry."PostingGroupId" = p_posting_id
                              AND line."Sequence" = expected_sequence + 1 AND line."Side" = 2
                              AND account."Code" = expected_fee_account
                              AND line."WalletId" IS NULL AND line."CreditLotId" IS NULL
                              AND line."Currency" = bounty."Currency" AND line."Provenance" IS NULL
                              AND line."AmountUnits" = fee_segment) THEN
                            RAISE EXCEPTION 'bounty reclaim fee pair does not match the immutable escrow fragment' USING ERRCODE = '23514';
                        END IF;
                        SELECT COALESCE(sum(allocation."AmountUnits"), 0) INTO allocated_units
                        FROM public.economy_entry_allocations allocation
                        JOIN public.economy_journal_lines line ON line."Id" = allocation."JournalLineId"
                        JOIN public.economy_journal_entries entry ON entry."Id" = line."JournalEntryId"
                        WHERE entry."PostingGroupId" = p_posting_id
                          AND line."Sequence" = expected_sequence
                          AND allocation."ParentLotId" = fragment."EscrowLotId";
                        IF allocated_units <> fee_segment THEN
                            RAISE EXCEPTION 'bounty reclaim fee allocation is not exactly bound to the escrow lot' USING ERRCODE = '23514';
                        END IF;
                        expected_sequence := expected_sequence + 2;
                    END IF;
                END LOOP;

                IF remaining_return <> 0 THEN
                    RAISE EXCEPTION 'bounty reclaim return partition is incomplete' USING ERRCODE = '23514';
                END IF;
                SELECT count(*) INTO line_count
                FROM public.economy_journal_entries entry
                JOIN public.economy_journal_lines line ON line."JournalEntryId" = entry."Id"
                WHERE entry."PostingGroupId" = p_posting_id;
                IF line_count <> expected_sequence - 1 THEN
                    RAISE EXCEPTION 'bounty reclaim posting contains extra or omitted lines' USING ERRCODE = '23514';
                END IF;
                SELECT COALESCE(sum(allocation."AmountUnits"), 0) INTO total_allocated_units
                FROM public.economy_entry_allocations allocation
                JOIN public.economy_journal_lines line ON line."Id" = allocation."JournalLineId"
                JOIN public.economy_journal_entries entry ON entry."Id" = line."JournalEntryId"
                WHERE entry."PostingGroupId" = p_posting_id AND line."Side" = 1;
                IF total_allocated_units <> bounty."AmountUnits" THEN
                    RAISE EXCEPTION 'bounty reclaim allocations do not conserve the escrow amount' USING ERRCODE = '23514';
                END IF;

                remaining_return := returned_units;
                FOR fragment IN
                    SELECT *
                    FROM public.economy_bounty_escrow_fragments
                    WHERE "BountyId" = p_bounty_id
                    ORDER BY "EscrowLotId"::text
                    FOR UPDATE
                LOOP
                    SELECT * INTO escrow_lot
                    FROM public.economy_credit_lots lot
                    WHERE lot."Id" = fragment."EscrowLotId"
                    FOR UPDATE;
                    return_segment := LEAST(fragment."AmountUnits", remaining_return);
                    IF return_segment > 0 THEN
                        output_lot_id := gen_random_uuid();
                        INSERT INTO public.economy_credit_lots (
                            "Id", "WalletId", "RootSourceStampId", "Currency", "AmountUnits", "Provenance",
                            "CreditedAt", "ConfirmedAt", "OriginalMaturesAt", "CashOutEligible", "JournalSequence",
                            "State", "ReversalEpoch")
                        VALUES (
                            output_lot_id, p_poster_wallet_id, escrow_lot."RootSourceStampId", bounty."Currency",
                            return_segment, escrow_lot."Provenance", p_reclaimed_at, escrow_lot."ConfirmedAt",
                            escrow_lot."OriginalMaturesAt", escrow_lot."CashOutEligible", posting."Sequence", 1,
                            escrow_lot."ReversalEpoch");
                        INSERT INTO public.economy_lot_lineage_edges (
                            "Id", "ParentLotId", "ChildLotId", "Currency", "AmountUnits")
                        VALUES (
                            gen_random_uuid(), escrow_lot."Id", output_lot_id, bounty."Currency", return_segment);

                        trace_remaining := return_segment * fragment."TraceUnitsPerCoinUnit";
                        FOR root_range IN
                            SELECT * FROM public.economy_fragment_root_ranges
                            WHERE "CreditLotId" = escrow_lot."Id"
                            ORDER BY "RootSourceStampId"::text, "StartInclusive", "EndExclusive"
                        LOOP
                            EXIT WHEN trace_remaining = 0;
                            trace_segment := LEAST(root_range."EndExclusive" - root_range."StartInclusive", trace_remaining);
                            INSERT INTO public.economy_fragment_root_ranges (
                                "Id", "RootSourceStampId", "CreditLotId", "EntryAllocationId",
                                "StartInclusive", "EndExclusive", "ReversalEpoch")
                            VALUES (
                                gen_random_uuid(), root_range."RootSourceStampId", output_lot_id, NULL,
                                root_range."StartInclusive", root_range."StartInclusive" + trace_segment,
                                root_range."ReversalEpoch");
                            trace_remaining := trace_remaining - trace_segment;
                        END LOOP;
                        IF trace_remaining <> 0 THEN
                            RAISE EXCEPTION 'bounty reclaim root range partition is incomplete' USING ERRCODE = '23514';
                        END IF;
                        first_output_lot_id := COALESCE(first_output_lot_id, output_lot_id);
                        output_lots := output_lots || jsonb_build_array(jsonb_build_object(
                            'LotId', output_lot_id,
                            'WalletId', p_poster_wallet_id,
                            'Currency', bounty."Currency",
                            'AmountUnits', return_segment,
                            'Provenance', escrow_lot."Provenance",
                            'RootSourceStampId', escrow_lot."RootSourceStampId",
                            'ConfirmedAt', escrow_lot."ConfirmedAt",
                            'OriginalMaturesAt', escrow_lot."OriginalMaturesAt",
                            'CashOutEligible', escrow_lot."CashOutEligible"));
                        remaining_return := remaining_return - return_segment;
                    END IF;
                    UPDATE public.economy_credit_lots SET "State" = 3 WHERE "Id" = escrow_lot."Id";
                END LOOP;

                IF remaining_return <> 0 OR first_output_lot_id IS NULL OR jsonb_array_length(output_lots) = 0 THEN
                    RAISE EXCEPTION 'bounty reclaim did not materialize restored lots' USING ERRCODE = '23514';
                END IF;
                UPDATE public.economy_bounties
                SET "Status" = 4, "Version" = "Version" + 1
                WHERE "Id" = p_bounty_id;
                INSERT INTO public.economy_bounty_terminal_events (
                    "Id", "BountyId", "Status", "ActorId", "DestinationWalletId", "IdempotencyKey",
                    "RiskDecisionId", "ProceedsSourceStampId", "ProceedsLotId", "ReturnedUnits", "FeeUnits",
                    "FirstJournalSequence", "OutputLots", "OccurredAt")
                VALUES (
                    gen_random_uuid(), p_bounty_id, 4, p_poster_id, p_poster_wallet_id, btrim(p_idempotency_key),
                    p_risk_decision_id, NULL, NULL, returned_units, fee_units,
                    posting."Sequence", output_lots, p_reclaimed_at);
            END
            $function$;

            ALTER FUNCTION economy_private.complete_bounty_reclaim_v1(uuid,uuid,uuid,text,uuid,uuid,timestamptz)
                OWNER TO gameguild_economy_procedure_owner;
            REVOKE ALL ON FUNCTION economy_private.complete_bounty_reclaim_v1(uuid,uuid,uuid,text,uuid,uuid,timestamptz) FROM PUBLIC;
            GRANT EXECUTE ON FUNCTION economy_private.complete_bounty_reclaim_v1(uuid,uuid,uuid,text,uuid,uuid,timestamptz)
                TO gameguild_economy_writer;

            GRANT SELECT, INSERT, UPDATE ON TABLE public.economy_bounties,
                public.economy_bounty_escrow_fragments,
                public.economy_bounty_terminal_events,
                public.economy_credit_lots,
                public.economy_lot_lineage_edges,
                public.economy_fragment_root_ranges,
                public.economy_risk_decisions,
                public.economy_posting_groups,
                public.economy_journal_entries,
                public.economy_journal_lines,
                public.economy_entry_allocations,
                public.economy_accounts
                TO gameguild_economy_procedure_owner;
            """);
    }

    private static void RemoveBountyReclaimLedgerWriter(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP FUNCTION IF EXISTS economy_private.complete_bounty_reclaim_v1(uuid,uuid,uuid,text,uuid,uuid,timestamptz);
            """);
    }
}
