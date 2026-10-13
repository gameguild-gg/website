using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations;

public partial class RepairHardToSoftConversionRiskReservationAlias : Migration
{
    private static void InstallHardToSoftConversionRiskReservationAliasRepair(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION economy_private.post_hard_to_soft_conversion_v1(
                p_capability_id uuid,
                p_actor_id uuid,
                p_tenant_id uuid,
                p_principal_posting_id uuid,
                p_fee_posting_id uuid,
                p_idempotency_key text,
                p_policy_version bigint,
                p_reserve_version bigint,
                p_risk_decision_id uuid,
                p_risk_operation_fingerprint text,
                p_expected_counter_version bigint,
                p_wallet_id uuid,
                p_output_lot_id uuid,
                p_principal_hard_units bigint,
                p_fee_hard_units bigint,
                p_requested_at timestamptz,
                p_dispatch_snapshot_hash text)
            RETURNS TABLE(posting_id uuid, journal_sequence bigint, journal_hash text, duplicate boolean)
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
            DECLARE
                existing_operation public.economy_hard_to_soft_conversion_operations%ROWTYPE;
                existing_receipt record;
                principal_receipt record;
                reservation record;
                parent_lot record;
                risk_record record;
                customer_hard_account_id uuid;
                hard_reserve_account_id uuid;
                soft_reserve_account_id uuid;
                customer_soft_account_id uuid;
                fee_revenue_account_id uuid;
                principal_debit_line_id uuid := gen_random_uuid();
                principal_credit_hard_line_id uuid := gen_random_uuid();
                principal_debit_soft_line_id uuid := gen_random_uuid();
                principal_credit_soft_line_id uuid := gen_random_uuid();
                fee_debit_line_id uuid := gen_random_uuid();
                fee_credit_line_id uuid := gen_random_uuid();
                child_lot_id uuid;
                allocation_id uuid;
                fee_allocation_id uuid;
                fee_entry_id uuid;
                next_sequence bigint;
                fee_hash text;
                fee_request_hash text;
                request_hash text;
                principal_lines jsonb;
                principal_allocations jsonb := '[]'::jsonb;
                principal_root_ranges jsonb := '[]'::jsonb;
                fee_allocations jsonb := '[]'::jsonb;
                fee_root_ranges jsonb := '[]'::jsonb;
                expected_epochs jsonb := '[]'::jsonb;
                remaining_principal bigint := p_principal_hard_units;
                remaining_fee bigint := p_fee_hard_units;
                principal_segment bigint;
                fee_segment bigint;
                soft_segment bigint;
                output_lot_created boolean := false;
                fee_outbox_payload text;
            BEGIN
                IF p_capability_id IS NULL OR p_actor_id IS NULL OR p_tenant_id IS NULL
                   OR p_principal_posting_id IS NULL OR p_wallet_id IS NULL OR p_output_lot_id IS NULL
                   OR p_risk_decision_id IS NULL OR p_principal_hard_units <= 0 OR p_fee_hard_units < 0
                   OR p_requested_at IS NULL OR p_policy_version <= 0 OR p_reserve_version <= 0
                   OR p_expected_counter_version <= 0 OR length(btrim(p_idempotency_key)) = 0
                   OR length(btrim(p_risk_operation_fingerprint)) = 0
                   OR (p_fee_hard_units = 0 AND p_fee_posting_id IS NOT NULL)
                   OR (p_fee_hard_units > 0 AND p_fee_posting_id IS NULL) THEN
                    RAISE EXCEPTION 'hard-to-soft conversion arguments are invalid' USING ERRCODE = '22023';
                END IF;

                PERFORM pg_advisory_xact_lock(hashtextextended(btrim(p_idempotency_key), 0));
                PERFORM pg_advisory_xact_lock(hashtextextended(p_principal_posting_id::text, 0));
                request_hash := encode(public.digest(convert_to(jsonb_build_object(
                    'capabilityId', p_capability_id,
                    'actorId', p_actor_id,
                    'tenantId', p_tenant_id,
                    'principalPostingId', p_principal_posting_id,
                    'feePostingId', p_fee_posting_id,
                    'idempotencyKey', btrim(p_idempotency_key),
                    'policyVersion', p_policy_version,
                    'reserveVersion', p_reserve_version,
                    'riskDecisionId', p_risk_decision_id,
                    'riskOperationFingerprint', btrim(p_risk_operation_fingerprint),
                    'expectedCounterVersion', p_expected_counter_version,
                    'walletId', p_wallet_id,
                    'outputLotId', p_output_lot_id,
                    'principalHardUnits', p_principal_hard_units,
                    'feeHardUnits', p_fee_hard_units,
                    'requestedAt', p_requested_at,
                    'dispatchSnapshotHash', p_dispatch_snapshot_hash)::text, 'UTF8'), 'sha256'), 'hex');

                SELECT * INTO existing_operation
                FROM public.economy_hard_to_soft_conversion_operations operation
                WHERE operation."IdempotencyKey" = btrim(p_idempotency_key)
                FOR UPDATE;
                IF FOUND THEN
                    IF existing_operation."Id" <> p_principal_posting_id
                       OR existing_operation."RequestHash" <> request_hash THEN
                        RAISE EXCEPTION 'hard-to-soft conversion idempotency key is bound to another request' USING ERRCODE = '23505';
                    END IF;
                    SELECT entry."Sequence", entry."Hash" INTO existing_receipt
                    FROM public.economy_journal_entries entry
                    WHERE entry."PostingGroupId" = p_principal_posting_id;
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'hard-to-soft conversion has no immutable principal posting' USING ERRCODE = '23514';
                    END IF;
                    posting_id := p_principal_posting_id;
                    journal_sequence := existing_receipt."Sequence";
                    journal_hash := existing_receipt."Hash";
                    duplicate := true;
                    RETURN NEXT;
                    RETURN;
                END IF;

                PERFORM 1 FROM public.economy_wallets wallet
                WHERE wallet."Id" = p_wallet_id AND wallet."State" = 1
                FOR SHARE;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'conversion wallet is absent or inactive' USING ERRCODE = '23503';
                END IF;

                PERFORM 1 FROM public.economy_registered_capabilities capability
                WHERE capability."Id" = p_capability_id
                  AND capability."IsEnabled"
                  AND capability."RevokedAt" IS NULL
                  AND capability."AllowedTemplateKinds" @> jsonb_build_array(5)
                  AND (p_fee_hard_units = 0 OR capability."AllowedTemplateKinds" @> jsonb_build_array(17))
                FOR SHARE;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'conversion capability is absent, disabled, or unauthorized' USING ERRCODE = '42501';
                END IF;

                SELECT * INTO risk_record
                FROM public.economy_risk_decisions decision
                WHERE decision."Id" = p_risk_decision_id
                  AND decision."Outcome" = 1
                  AND decision."TemplateKind" = 5
                  AND decision."OperationFingerprint" = btrim(p_risk_operation_fingerprint)
                  AND decision."PolicyVersion" = p_policy_version
                  AND decision."ReserveVersion" = p_reserve_version
                  AND decision."CounterVersion" = p_expected_counter_version
                  AND decision."Currency" = 1
                  AND decision."AmountUnits" = p_principal_hard_units + p_fee_hard_units
                  AND decision."IssuedAt" <= p_requested_at
                  AND decision."ExpiresAt" > p_requested_at
                FOR UPDATE;
                IF NOT FOUND OR EXISTS (
                    SELECT 1 FROM public.economy_risk_decision_consumptions consumption
                    WHERE consumption."RiskDecisionId" = p_risk_decision_id) OR NOT EXISTS (
                    SELECT 1 FROM public.economy_risk_counter_reservations counter_reservation
                    JOIN public.economy_risk_counters counter ON counter."Id" = counter_reservation."RiskCounterId"
                    WHERE counter_reservation."RiskDecisionId" = p_risk_decision_id
                      AND counter_reservation."AmountUnits" = risk_record."AmountUnits"
                      AND counter."CounterVersion" = p_expected_counter_version) THEN
                    RAISE EXCEPTION 'conversion risk authorization is missing, stale, or consumed' USING ERRCODE = '42501';
                END IF;

                SELECT "Id" INTO customer_hard_account_id FROM public.economy_accounts
                WHERE "WalletId" = p_wallet_id AND "Code" = 2 AND "Currency" = 1 AND "Provenance" = 1;
                SELECT "Id" INTO hard_reserve_account_id FROM public.economy_accounts
                WHERE "WalletId" IS NULL AND "Code" = 5 AND "Currency" = 1 AND "Provenance" IS NULL;
                SELECT "Id" INTO soft_reserve_account_id FROM public.economy_accounts
                WHERE "WalletId" IS NULL AND "Code" = 6 AND "Currency" = 2 AND "Provenance" IS NULL;
                SELECT "Id" INTO customer_soft_account_id FROM public.economy_accounts
                WHERE "WalletId" = p_wallet_id AND "Code" = 4 AND "Currency" = 2 AND "Provenance" = 3;
                SELECT "Id" INTO fee_revenue_account_id FROM public.economy_accounts
                WHERE "WalletId" IS NULL AND "Code" = 14 AND "Currency" = 1 AND "Provenance" IS NULL;
                IF customer_hard_account_id IS NULL OR hard_reserve_account_id IS NULL
                   OR soft_reserve_account_id IS NULL OR customer_soft_account_id IS NULL
                   OR (p_fee_hard_units > 0 AND fee_revenue_account_id IS NULL) THEN
                    RAISE EXCEPTION 'conversion account partitions are not provisioned' USING ERRCODE = '23503';
                END IF;

                INSERT INTO public.economy_hard_to_soft_conversion_operations (
                    "Id", "IdempotencyKey", "RequestHash", "WalletId", "OutputLotId", "PrincipalHardUnits",
                    "FeeHardUnits", "PrincipalPostingId", "FeePostingId", "CreatedAt")
                VALUES (
                    p_principal_posting_id, btrim(p_idempotency_key), request_hash, p_wallet_id, p_output_lot_id,
                    p_principal_hard_units, p_fee_hard_units, p_principal_posting_id, p_fee_posting_id, p_requested_at);

                FOR reservation IN
                    SELECT * FROM economy_private.reserve_fifo_fragments_v1(
                        p_principal_posting_id, p_wallet_id, 1, 1,
                        p_principal_hard_units + p_fee_hard_units, 3, p_requested_at)
                LOOP
                    SELECT * INTO parent_lot FROM public.economy_credit_lots lot
                    WHERE lot."Id" = reservation.parent_lot_id
                    FOR SHARE;
                    IF NOT FOUND OR parent_lot."WalletId" <> p_wallet_id OR parent_lot."Currency" <> 1
                       OR parent_lot."Provenance" <> 1 OR parent_lot."State" <> 1 THEN
                        RAISE EXCEPTION 'conversion reservation parent lot is no longer eligible' USING ERRCODE = '40001';
                    END IF;

                    principal_segment := LEAST(reservation.amount_units, remaining_principal);
                    IF principal_segment > 0 THEN
                        soft_segment := principal_segment * 1000;
                        child_lot_id := CASE WHEN output_lot_created THEN gen_random_uuid() ELSE p_output_lot_id END;
                        output_lot_created := true;
                        allocation_id := gen_random_uuid();
                        INSERT INTO public.economy_credit_lots (
                            "Id", "WalletId", "RootSourceStampId", "Currency", "AmountUnits", "Provenance",
                            "CreditedAt", "ConfirmedAt", "OriginalMaturesAt", "CashOutEligible", "JournalSequence", "State", "ReversalEpoch")
                        VALUES (
                            child_lot_id, p_wallet_id, reservation.root_source_stamp_id, 2, soft_segment, 3,
                            p_requested_at, p_requested_at, p_requested_at, false, 0, 1, reservation.reversal_epoch);
                        INSERT INTO public.economy_lot_lineage_edges ("Id", "ParentLotId", "ChildLotId", "Currency", "AmountUnits")
                        VALUES (gen_random_uuid(), reservation.parent_lot_id, child_lot_id, 2, soft_segment);
                        INSERT INTO public.economy_fragment_root_ranges (
                            "Id", "RootSourceStampId", "CreditLotId", "EntryAllocationId", "StartInclusive", "EndExclusive", "ReversalEpoch")
                        VALUES (
                            gen_random_uuid(), reservation.root_source_stamp_id, child_lot_id, NULL,
                            reservation.start_inclusive, reservation.start_inclusive + soft_segment, reservation.reversal_epoch);
                        principal_allocations := principal_allocations || jsonb_build_array(jsonb_build_object(
                            'id', allocation_id, 'journal_line_id', principal_debit_line_id,
                            'parent_lot_id', reservation.parent_lot_id, 'amount_units', principal_segment));
                        principal_root_ranges := principal_root_ranges || jsonb_build_array(jsonb_build_object(
                            'id', gen_random_uuid(), 'root_source_stamp_id', reservation.root_source_stamp_id,
                            'credit_lot_id', NULL, 'entry_allocation_id', allocation_id,
                            'start_inclusive', reservation.start_inclusive, 'end_exclusive', reservation.start_inclusive + soft_segment,
                            'reversal_epoch', reservation.reversal_epoch));
                        remaining_principal := remaining_principal - principal_segment;
                    END IF;

                    fee_segment := reservation.amount_units - principal_segment;
                    IF fee_segment > 0 THEN
                        fee_allocation_id := gen_random_uuid();
                        fee_allocations := fee_allocations || jsonb_build_array(jsonb_build_object(
                            'id', fee_allocation_id, 'journal_line_id', fee_debit_line_id,
                            'parent_lot_id', reservation.parent_lot_id, 'amount_units', fee_segment));
                        fee_root_ranges := fee_root_ranges || jsonb_build_array(jsonb_build_object(
                            'id', gen_random_uuid(), 'root_source_stamp_id', reservation.root_source_stamp_id,
                            'credit_lot_id', NULL, 'entry_allocation_id', fee_allocation_id,
                            'start_inclusive', reservation.start_inclusive + principal_segment, 'end_exclusive', reservation.end_exclusive,
                            'reversal_epoch', reservation.reversal_epoch));
                        remaining_fee := remaining_fee - fee_segment;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM jsonb_array_elements(expected_epochs) item
                        WHERE (item->>'root_source_stamp_id')::uuid = reservation.root_source_stamp_id) THEN
                        expected_epochs := expected_epochs || jsonb_build_array(jsonb_build_object(
                            'root_source_stamp_id', reservation.root_source_stamp_id, 'expected_epoch', reservation.reversal_epoch));
                    END IF;
                END LOOP;
                IF remaining_principal <> 0 OR remaining_fee <> 0 OR NOT output_lot_created THEN
                    RAISE EXCEPTION 'conversion fragment partition is incomplete' USING ERRCODE = '40001';
                END IF;

                principal_lines := jsonb_build_array(
                    jsonb_build_object('id', principal_debit_line_id, 'account_id', customer_hard_account_id, 'account_code', 2,
                        'wallet_id', p_wallet_id, 'credit_lot_id', NULL, 'side', 1, 'currency', 1,
                        'amount_units', p_principal_hard_units, 'provenance', 1),
                    jsonb_build_object('id', principal_credit_hard_line_id, 'account_id', hard_reserve_account_id, 'account_code', 5,
                        'wallet_id', NULL, 'credit_lot_id', NULL, 'side', 2, 'currency', 1,
                        'amount_units', p_principal_hard_units, 'provenance', NULL),
                    jsonb_build_object('id', principal_debit_soft_line_id, 'account_id', soft_reserve_account_id, 'account_code', 6,
                        'wallet_id', NULL, 'credit_lot_id', NULL, 'side', 1, 'currency', 2,
                        'amount_units', p_principal_hard_units * 1000, 'provenance', NULL),
                    jsonb_build_object('id', principal_credit_soft_line_id, 'account_id', customer_soft_account_id, 'account_code', 4,
                        'wallet_id', p_wallet_id, 'credit_lot_id', p_output_lot_id, 'side', 2, 'currency', 2,
                        'amount_units', p_principal_hard_units * 1000, 'provenance', 3));
                SELECT * INTO principal_receipt FROM economy_private.post_registered_posting_v1(
                    p_capability_id, p_actor_id, p_tenant_id, p_principal_posting_id, btrim(p_idempotency_key),
                    5, 1, 2, p_policy_version, p_reserve_version, p_risk_decision_id,
                    btrim(p_risk_operation_fingerprint), p_expected_counter_version, NULL, NULL, p_requested_at,
                    principal_lines, principal_allocations, principal_root_ranges, expected_epochs, p_dispatch_snapshot_hash);
                IF principal_receipt.duplicate THEN
                    RAISE EXCEPTION 'conversion reached an unexpected duplicate principal posting' USING ERRCODE = '40001';
                END IF;

                IF p_fee_hard_units > 0 THEN
                    INSERT INTO public.economy_chain_head ("Id", "Sequence", "Hash", "UpdatedAt")
                    VALUES (1, 0, repeat('0', 64), p_requested_at) ON CONFLICT ("Id") DO NOTHING;
                    SELECT head."Sequence" + 1, head."Hash" INTO next_sequence, fee_hash
                    FROM public.economy_chain_head head WHERE head."Id" = 1 FOR UPDATE;
                    fee_request_hash := encode(public.digest(convert_to(jsonb_build_object(
                        'conversionPostingId', p_principal_posting_id, 'feePostingId', p_fee_posting_id,
                        'idempotencyKey', btrim(p_idempotency_key) || ':fee', 'amountUnits', p_fee_hard_units,
                        'riskDecisionId', p_risk_decision_id, 'requestedAt', p_requested_at)::text, 'UTF8'), 'sha256'), 'hex');
                    fee_hash := encode(public.digest(convert_to(concat_ws('|', fee_hash, p_fee_posting_id::text,
                        next_sequence::text, fee_request_hash), 'UTF8'), 'sha256'), 'hex');
                    INSERT INTO public.economy_posting_groups (
                        "Id", "IdempotencyKey", "TemplateKind", "TemplateVersion", "Authority", "Status", "CapabilityId",
                        "ActorId", "TenantId", "RiskDecisionId", "PolicyVersion", "ReserveVersion", "SourceStampId", "RecordedAt")
                    VALUES (p_fee_posting_id, btrim(p_idempotency_key) || ':fee', 17, 1, 2, 1, p_capability_id,
                        p_actor_id, p_tenant_id, p_risk_decision_id, p_policy_version, p_reserve_version, NULL, p_requested_at);
                    fee_entry_id := gen_random_uuid();
                    INSERT INTO public.economy_journal_entries ("Id", "PostingGroupId", "Sequence", "PreviousHash", "Hash", "RecordedAt")
                    VALUES (fee_entry_id, p_fee_posting_id, next_sequence,
                        (SELECT "Hash" FROM public.economy_chain_head WHERE "Id" = 1), fee_hash, p_requested_at);
                    INSERT INTO public.economy_journal_lines (
                        "Id", "JournalEntryId", "AccountId", "WalletId", "CreditLotId", "Sequence", "Side", "Currency", "AmountUnits", "Provenance")
                    VALUES
                        (fee_debit_line_id, fee_entry_id, customer_hard_account_id, p_wallet_id, NULL, 1, 1, 1, p_fee_hard_units, 1),
                        (fee_credit_line_id, fee_entry_id, fee_revenue_account_id, NULL, NULL, 2, 2, 1, p_fee_hard_units, NULL);
                    INSERT INTO public.economy_entry_allocations ("Id", "JournalLineId", "ParentLotId", "AmountUnits")
                    SELECT (item->>'id')::uuid, (item->>'journal_line_id')::uuid,
                           (item->>'parent_lot_id')::uuid, (item->>'amount_units')::bigint
                    FROM jsonb_array_elements(fee_allocations) item;
                    INSERT INTO public.economy_fragment_root_ranges (
                        "Id", "RootSourceStampId", "CreditLotId", "EntryAllocationId", "StartInclusive", "EndExclusive", "ReversalEpoch")
                    SELECT (item->>'id')::uuid, (item->>'root_source_stamp_id')::uuid, NULL,
                           (item->>'entry_allocation_id')::uuid, (item->>'start_inclusive')::bigint,
                           (item->>'end_exclusive')::bigint, (item->>'reversal_epoch')::bigint
                    FROM jsonb_array_elements(fee_root_ranges) item;
                    INSERT INTO public.economy_idempotency_records ("Id", "Key", "RequestHash", "PostingGroupId", "CreatedAt")
                    VALUES (gen_random_uuid(), btrim(p_idempotency_key) || ':fee', fee_request_hash, p_fee_posting_id, p_requested_at);
                    UPDATE public.economy_chain_head SET "Sequence" = next_sequence, "Hash" = fee_hash, "UpdatedAt" = p_requested_at WHERE "Id" = 1;
                    fee_outbox_payload := json_build_object('PostingId', p_fee_posting_id, 'Hash', fee_hash,
                        'RecordedAt', p_requested_at, 'JournalLineIds', jsonb_build_array(fee_debit_line_id, fee_credit_line_id))::text;
                    INSERT INTO public.economy_outbox_messages ("Id", "PostingGroupId", "Type", "Payload", "PayloadHash", "OccurredAt")
                    VALUES (gen_random_uuid(), p_fee_posting_id, 'economy.posting.accepted.v1', fee_outbox_payload,
                        encode(public.digest(convert_to(fee_outbox_payload, 'UTF8'), 'sha256'), 'hex'), p_requested_at);
                END IF;

                PERFORM economy_private.transition_fifo_fragment_reservations_v1(p_principal_posting_id, 1, 3, p_requested_at);
                PERFORM economy_private.rebuild_wallet_projection_v1(p_wallet_id, p_requested_at);
                posting_id := principal_receipt.posting_id;
                journal_sequence := principal_receipt.journal_sequence;
                journal_hash := principal_receipt.journal_hash;
                duplicate := false;
                RETURN NEXT;
            END
            $function$;

            ALTER FUNCTION economy_private.post_hard_to_soft_conversion_v1(
                uuid,uuid,uuid,uuid,uuid,text,bigint,bigint,uuid,text,bigint,uuid,uuid,bigint,bigint,timestamptz,text)
                OWNER TO gameguild_economy_procedure_owner;
            REVOKE ALL ON FUNCTION economy_private.post_hard_to_soft_conversion_v1(
                uuid,uuid,uuid,uuid,uuid,text,bigint,bigint,uuid,text,bigint,uuid,uuid,bigint,bigint,timestamptz,text) FROM PUBLIC;
            GRANT EXECUTE ON FUNCTION economy_private.post_hard_to_soft_conversion_v1(
                uuid,uuid,uuid,uuid,uuid,text,bigint,bigint,uuid,text,bigint,uuid,uuid,bigint,bigint,timestamptz,text)
                TO gameguild_economy_writer;

            REVOKE ALL ON TABLE public.economy_hard_to_soft_conversion_operations FROM PUBLIC;
            REVOKE ALL ON TABLE public.economy_hard_to_soft_conversion_operations FROM gameguild_economy_writer;
            GRANT SELECT ON TABLE public.economy_hard_to_soft_conversion_operations TO gameguild_economy_runtime;
            GRANT SELECT, INSERT, UPDATE ON TABLE public.economy_hard_to_soft_conversion_operations TO gameguild_economy_procedure_owner;
            GRANT ALL ON TABLE public.economy_hard_to_soft_conversion_operations TO gameguild_economy_migration;
            """);
    }

    private static void RemoveHardToSoftConversionRiskReservationAliasRepair(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP FUNCTION IF EXISTS economy_private.post_hard_to_soft_conversion_v1(
                uuid,uuid,uuid,uuid,uuid,text,bigint,bigint,uuid,text,bigint,uuid,uuid,bigint,bigint,timestamptz,text);
            """);
    }
}
