using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations;

public partial class AddEconomyFifoTransferWriter : Migration
{
    private static void InstallFifoTransferWriter(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION economy_private.post_fifo_transfer_v1(
                p_capability_id uuid,
                p_actor_id uuid,
                p_tenant_id uuid,
                p_posting_id uuid,
                p_idempotency_key text,
                p_policy_version bigint,
                p_reserve_version bigint,
                p_risk_decision_id uuid,
                p_risk_operation_fingerprint text,
                p_expected_counter_version bigint,
                p_source_wallet_id uuid,
                p_destination_wallet_id uuid,
                p_currency integer,
                p_provenance integer,
                p_amount_units bigint,
                p_requested_at timestamptz,
                p_dispatch_snapshot_hash text)
            RETURNS TABLE(posting_id uuid, journal_sequence bigint, journal_hash text, duplicate boolean)
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
            DECLARE
                existing_operation public.economy_fifo_transfer_operations%ROWTYPE;
                existing_posting record;
                receipt record;
                reservation record;
                parent_lot record;
                account_code integer;
                source_account_id uuid;
                destination_account_id uuid;
                source_line_id uuid;
                destination_line_id uuid;
                child_lot_id uuid;
                allocation_id uuid;
                next_sequence bigint;
                trace_scale bigint;
                request_hash text;
                lines jsonb;
                allocations jsonb := '[]'::jsonb;
                root_ranges jsonb := '[]'::jsonb;
                expected_epochs jsonb := '[]'::jsonb;
                outbox_payload text;
            BEGIN
                IF p_capability_id IS NULL OR p_actor_id IS NULL OR p_tenant_id IS NULL OR p_posting_id IS NULL
                   OR p_risk_decision_id IS NULL OR p_source_wallet_id IS NULL OR p_destination_wallet_id IS NULL
                   OR p_source_wallet_id = p_destination_wallet_id OR p_amount_units <= 0 OR p_requested_at IS NULL
                   OR p_policy_version <= 0 OR p_reserve_version <= 0 OR p_expected_counter_version <= 0
                   OR p_currency NOT IN (1, 2) OR p_provenance NOT IN (1, 2, 3, 4, 5, 6, 7)
                   OR length(btrim(p_idempotency_key)) = 0 OR length(btrim(p_risk_operation_fingerprint)) = 0 THEN
                    RAISE EXCEPTION 'FIFO transfer arguments are invalid' USING ERRCODE = '22023';
                END IF;
                IF (p_currency = 1 AND p_provenance NOT IN (1, 2))
                   OR (p_currency = 2 AND p_provenance NOT IN (3, 4, 5, 6, 7)) THEN
                    RAISE EXCEPTION 'FIFO transfer currency and provenance are incompatible' USING ERRCODE = '23514';
                END IF;

                PERFORM pg_advisory_xact_lock(hashtextextended(btrim(p_idempotency_key), 0));
                PERFORM pg_advisory_xact_lock(hashtextextended(p_posting_id::text, 0));
                request_hash := encode(public.digest(convert_to(jsonb_build_object(
                    'postingId', p_posting_id,
                    'idempotencyKey', btrim(p_idempotency_key),
                    'capabilityId', p_capability_id,
                    'actorId', p_actor_id,
                    'tenantId', p_tenant_id,
                    'policyVersion', p_policy_version,
                    'reserveVersion', p_reserve_version,
                    'riskDecisionId', p_risk_decision_id,
                    'riskOperationFingerprint', btrim(p_risk_operation_fingerprint),
                    'expectedCounterVersion', p_expected_counter_version,
                    'sourceWalletId', p_source_wallet_id,
                    'destinationWalletId', p_destination_wallet_id,
                    'currency', p_currency,
                    'provenance', p_provenance,
                    'amountUnits', p_amount_units,
                    'requestedAt', p_requested_at,
                    'dispatchSnapshotHash', p_dispatch_snapshot_hash)::text, 'UTF8'), 'sha256'), 'hex');

                SELECT * INTO existing_operation
                FROM public.economy_fifo_transfer_operations operation
                WHERE operation."IdempotencyKey" = btrim(p_idempotency_key)
                FOR UPDATE;
                IF FOUND THEN
                    IF existing_operation."Id" <> p_posting_id
                       OR existing_operation."RequestHash" <> request_hash THEN
                        RAISE EXCEPTION 'FIFO transfer idempotency key is bound to another request' USING ERRCODE = '23505';
                    END IF;
                    SELECT entry."Sequence", entry."Hash"
                    INTO existing_posting
                    FROM public.economy_journal_entries entry
                    WHERE entry."PostingGroupId" = p_posting_id;
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'FIFO transfer operation has no immutable posting' USING ERRCODE = '23514';
                    END IF;
                    posting_id := p_posting_id;
                    journal_sequence := existing_posting."Sequence";
                    journal_hash := existing_posting."Hash";
                    duplicate := true;
                    RETURN NEXT;
                    RETURN;
                END IF;

                IF EXISTS (
                    SELECT 1 FROM public.economy_posting_groups posting
                    WHERE posting."IdempotencyKey" = btrim(p_idempotency_key) OR posting."Id" = p_posting_id
                ) THEN
                    RAISE EXCEPTION 'posting identity is already bound to another economy operation' USING ERRCODE = '23505';
                END IF;

                PERFORM 1
                FROM public.economy_wallets wallet
                WHERE wallet."Id" = p_destination_wallet_id AND wallet."State" = 1
                FOR SHARE;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'FIFO transfer destination wallet is absent or inactive' USING ERRCODE = '23503';
                END IF;
                PERFORM 1
                FROM public.economy_risk_decisions decision
                WHERE decision."Id" = p_risk_decision_id
                  AND decision."TemplateKind" = 4
                  AND decision."SourceWalletId" = p_source_wallet_id
                  AND decision."DestinationWalletId" = p_destination_wallet_id
                  AND decision."Currency" = p_currency
                  AND decision."AmountUnits" = p_amount_units
                FOR SHARE;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'FIFO transfer risk decision does not bind the requested wallet movement' USING ERRCODE = '42501';
                END IF;

                account_code := CASE p_provenance WHEN 1 THEN 2 WHEN 2 THEN 3 ELSE 4 END;
                SELECT account."Id" INTO source_account_id
                FROM public.economy_accounts account
                WHERE account."WalletId" = p_source_wallet_id
                  AND account."Code" = account_code
                  AND account."Currency" = p_currency
                  AND account."Provenance" = p_provenance;
                SELECT account."Id" INTO destination_account_id
                FROM public.economy_accounts account
                WHERE account."WalletId" = p_destination_wallet_id
                  AND account."Code" = account_code
                  AND account."Currency" = p_currency
                  AND account."Provenance" = p_provenance;
                IF source_account_id IS NULL OR destination_account_id IS NULL THEN
                    RAISE EXCEPTION 'FIFO transfer account partitions are not provisioned' USING ERRCODE = '23503';
                END IF;

                source_line_id := gen_random_uuid();
                destination_line_id := gen_random_uuid();
                lines := jsonb_build_array(
                    jsonb_build_object(
                        'id', source_line_id, 'account_id', source_account_id, 'account_code', account_code,
                        'wallet_id', p_source_wallet_id, 'credit_lot_id', NULL, 'side', 1, 'currency', p_currency,
                        'amount_units', p_amount_units, 'provenance', p_provenance),
                    jsonb_build_object(
                        'id', destination_line_id, 'account_id', destination_account_id, 'account_code', account_code,
                        'wallet_id', p_destination_wallet_id, 'credit_lot_id', NULL, 'side', 2, 'currency', p_currency,
                        'amount_units', p_amount_units, 'provenance', p_provenance));

                INSERT INTO public.economy_chain_head ("Id", "Sequence", "Hash", "UpdatedAt")
                VALUES (1, 0, repeat('0', 64), p_requested_at)
                ON CONFLICT ("Id") DO NOTHING;
                SELECT head."Sequence" + 1 INTO next_sequence
                FROM public.economy_chain_head head WHERE head."Id" = 1 FOR UPDATE;

                trace_scale := CASE p_currency WHEN 1 THEN 1000 ELSE 1 END;
                FOR reservation IN
                    SELECT * FROM economy_private.reserve_fifo_fragments_v1(
                        p_posting_id, p_source_wallet_id, p_currency, p_provenance, p_amount_units, 4, p_requested_at)
                LOOP
                    SELECT * INTO parent_lot
                    FROM public.economy_credit_lots lot
                    WHERE lot."Id" = reservation.parent_lot_id
                    FOR SHARE;
                    IF NOT FOUND
                       OR parent_lot."WalletId" <> p_source_wallet_id
                       OR parent_lot."Currency" <> p_currency
                       OR parent_lot."Provenance" <> p_provenance
                       OR parent_lot."State" <> 1 THEN
                        RAISE EXCEPTION 'FIFO reservation parent lot is no longer eligible' USING ERRCODE = '40001';
                    END IF;

                    child_lot_id := gen_random_uuid();
                    allocation_id := gen_random_uuid();
                    INSERT INTO public.economy_credit_lots (
                        "Id", "WalletId", "RootSourceStampId", "Currency", "AmountUnits", "Provenance",
                        "CreditedAt", "ConfirmedAt", "OriginalMaturesAt", "CashOutEligible", "JournalSequence",
                        "State", "ReversalEpoch")
                    VALUES (
                        child_lot_id, p_destination_wallet_id, reservation.root_source_stamp_id, p_currency,
                        reservation.amount_units, p_provenance, p_requested_at, parent_lot."ConfirmedAt",
                        parent_lot."OriginalMaturesAt", parent_lot."CashOutEligible", next_sequence, 1,
                        reservation.reversal_epoch);
                    INSERT INTO public.economy_lot_lineage_edges (
                        "Id", "ParentLotId", "ChildLotId", "Currency", "AmountUnits")
                    VALUES (
                        gen_random_uuid(), reservation.parent_lot_id, child_lot_id, p_currency, reservation.amount_units);
                    INSERT INTO public.economy_fragment_root_ranges (
                        "Id", "RootSourceStampId", "CreditLotId", "EntryAllocationId", "StartInclusive", "EndExclusive", "ReversalEpoch")
                    VALUES (
                        gen_random_uuid(), reservation.root_source_stamp_id, child_lot_id, NULL,
                        reservation.start_inclusive, reservation.end_exclusive, reservation.reversal_epoch);

                    allocations := allocations || jsonb_build_array(jsonb_build_object(
                        'id', allocation_id, 'journal_line_id', source_line_id,
                        'parent_lot_id', reservation.parent_lot_id, 'amount_units', reservation.amount_units));
                    root_ranges := root_ranges || jsonb_build_array(jsonb_build_object(
                        'id', gen_random_uuid(), 'root_source_stamp_id', reservation.root_source_stamp_id,
                        'credit_lot_id', NULL, 'entry_allocation_id', allocation_id,
                        'start_inclusive', reservation.start_inclusive, 'end_exclusive', reservation.end_exclusive,
                        'reversal_epoch', reservation.reversal_epoch));
                    IF NOT EXISTS (
                        SELECT 1 FROM jsonb_array_elements(expected_epochs) item
                        WHERE (item->>'root_source_stamp_id')::uuid = reservation.root_source_stamp_id
                    ) THEN
                        expected_epochs := expected_epochs || jsonb_build_array(jsonb_build_object(
                            'root_source_stamp_id', reservation.root_source_stamp_id,
                            'expected_epoch', reservation.reversal_epoch));
                    END IF;
                END LOOP;

                PERFORM economy_private.transition_fifo_fragment_reservations_v1(p_posting_id, 1, 3, p_requested_at);
                SELECT * INTO receipt
                FROM economy_private.post_registered_posting_v1(
                    p_capability_id, p_actor_id, p_tenant_id, p_posting_id, btrim(p_idempotency_key), 4, 1, 2,
                    p_policy_version, p_reserve_version, p_risk_decision_id, btrim(p_risk_operation_fingerprint),
                    p_expected_counter_version, NULL, NULL, p_requested_at, lines, allocations, root_ranges,
                    expected_epochs, p_dispatch_snapshot_hash);
                IF receipt.duplicate THEN
                    RAISE EXCEPTION 'FIFO transfer reached an unexpected duplicate posting' USING ERRCODE = '40001';
                END IF;

                INSERT INTO public.economy_fifo_transfer_operations (
                    "Id", "IdempotencyKey", "RequestHash", "SourceWalletId", "DestinationWalletId", "Currency",
                    "Provenance", "AmountUnits", "CreatedAt")
                VALUES (
                    p_posting_id, btrim(p_idempotency_key), request_hash, p_source_wallet_id, p_destination_wallet_id,
                    p_currency, p_provenance, p_amount_units, p_requested_at);

                PERFORM economy_private.rebuild_wallet_projection_v1(p_source_wallet_id, p_requested_at);
                PERFORM economy_private.rebuild_wallet_projection_v1(p_destination_wallet_id, p_requested_at);
                outbox_payload := json_build_object(
                    'PostingId', p_posting_id,
                    'Hash', receipt.journal_hash,
                    'RecordedAt', p_requested_at,
                    'JournalLineIds', jsonb_build_array(source_line_id, destination_line_id))::text;
                INSERT INTO public.economy_outbox_messages (
                    "Id", "PostingGroupId", "Type", "Payload", "PayloadHash", "OccurredAt")
                VALUES (
                    gen_random_uuid(), p_posting_id, 'economy.posting.accepted.v1', outbox_payload,
                    encode(public.digest(convert_to(outbox_payload, 'UTF8'), 'sha256'), 'hex'), p_requested_at);

                posting_id := receipt.posting_id;
                journal_sequence := receipt.journal_sequence;
                journal_hash := receipt.journal_hash;
                duplicate := false;
                RETURN NEXT;
            END
            $function$;

            ALTER FUNCTION economy_private.post_fifo_transfer_v1(
                uuid,uuid,uuid,uuid,text,bigint,bigint,uuid,text,bigint,uuid,uuid,integer,integer,bigint,timestamptz,text)
                OWNER TO gameguild_economy_procedure_owner;
            REVOKE ALL ON FUNCTION economy_private.post_fifo_transfer_v1(
                uuid,uuid,uuid,uuid,text,bigint,bigint,uuid,text,bigint,uuid,uuid,integer,integer,bigint,timestamptz,text)
                FROM PUBLIC;
            GRANT EXECUTE ON FUNCTION economy_private.post_fifo_transfer_v1(
                uuid,uuid,uuid,uuid,text,bigint,bigint,uuid,text,bigint,uuid,uuid,integer,integer,bigint,timestamptz,text)
                TO gameguild_economy_writer;

            REVOKE ALL ON TABLE public.economy_fifo_transfer_operations FROM PUBLIC;
            REVOKE ALL ON TABLE public.economy_fifo_transfer_operations FROM gameguild_economy_writer;
            GRANT SELECT ON TABLE public.economy_fifo_transfer_operations TO gameguild_economy_runtime;
            GRANT SELECT, INSERT, UPDATE ON TABLE public.economy_fifo_transfer_operations TO gameguild_economy_procedure_owner;
            GRANT ALL ON TABLE public.economy_fifo_transfer_operations TO gameguild_economy_migration;
            """);
    }

    private static void RemoveFifoTransferWriter(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP FUNCTION IF EXISTS economy_private.post_fifo_transfer_v1(
                uuid,uuid,uuid,uuid,text,bigint,bigint,uuid,text,bigint,uuid,uuid,integer,integer,bigint,timestamptz,text);
            """);
    }
}
