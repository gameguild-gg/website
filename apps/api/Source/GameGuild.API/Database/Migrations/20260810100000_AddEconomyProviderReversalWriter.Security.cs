using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations;

public partial class AddEconomyProviderReversalWriter : Migration
{
    private static void InstallProviderReversalWriter(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION economy_private.append_provider_reversal_posting_v1(
                p_capability_id uuid,
                p_actor_id uuid,
                p_tenant_id uuid,
                p_posting_id uuid,
                p_idempotency_key text,
                p_template_kind integer,
                p_policy_version bigint,
                p_reserve_version bigint,
                p_root_source_stamp_id uuid,
                p_occurred_at timestamptz,
                p_lines jsonb,
                p_allocations jsonb,
                p_root_ranges jsonb)
            RETURNS TABLE(posting_id uuid, journal_sequence bigint, journal_hash text)
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
            DECLARE
                head record;
                entry_id uuid := gen_random_uuid();
                payload text;
                request_hash text;
            BEGIN
                IF p_posting_id IS NULL OR length(btrim(p_idempotency_key)) = 0
                   OR p_template_kind NOT IN (2, 3, 18, 19, 20)
                   OR p_policy_version <= 0 OR p_reserve_version <= 0
                   OR p_root_source_stamp_id IS NULL OR p_occurred_at IS NULL
                   OR jsonb_typeof(p_lines) <> 'array' OR jsonb_array_length(p_lines) < 2
                   OR jsonb_typeof(p_allocations) <> 'array' OR jsonb_typeof(p_root_ranges) <> 'array' THEN
                    RAISE EXCEPTION 'provider reversal posting arguments are invalid' USING ERRCODE = '22023';
                END IF;

                INSERT INTO public.economy_chain_head ("Id", "Sequence", "Hash", "UpdatedAt")
                VALUES (1, 0, repeat('0', 64), p_occurred_at)
                ON CONFLICT ("Id") DO NOTHING;
                SELECT * INTO head FROM public.economy_chain_head WHERE "Id" = 1 FOR UPDATE;
                journal_sequence := head."Sequence" + 1;
                request_hash := encode(public.digest(convert_to(jsonb_build_object(
                    'postingId', p_posting_id, 'idempotencyKey', btrim(p_idempotency_key),
                    'templateKind', p_template_kind, 'rootSourceStampId', p_root_source_stamp_id,
                    'lines', p_lines, 'allocations', p_allocations, 'ranges', p_root_ranges,
                    'occurredAt', p_occurred_at)::text, 'UTF8'), 'sha256'), 'hex');
                journal_hash := encode(public.digest(convert_to(concat_ws('|', head."Hash", p_posting_id::text,
                    journal_sequence::text, request_hash), 'UTF8'), 'sha256'), 'hex');

                INSERT INTO public.economy_posting_groups (
                    "Id", "IdempotencyKey", "TemplateKind", "TemplateVersion", "Authority", "Status", "CapabilityId",
                    "ActorId", "TenantId", "RiskDecisionId", "PolicyVersion", "ReserveVersion", "SourceStampId", "RecordedAt")
                VALUES (
                    p_posting_id, btrim(p_idempotency_key), p_template_kind, 1, 1, 1, p_capability_id,
                    p_actor_id, p_tenant_id, NULL, p_policy_version, p_reserve_version, p_root_source_stamp_id, p_occurred_at);
                INSERT INTO public.economy_journal_entries ("Id", "PostingGroupId", "Sequence", "PreviousHash", "Hash", "RecordedAt")
                VALUES (entry_id, p_posting_id, journal_sequence, head."Hash", journal_hash, p_occurred_at);
                INSERT INTO public.economy_journal_lines (
                    "Id", "JournalEntryId", "AccountId", "WalletId", "CreditLotId", "Sequence", "Side", "Currency", "AmountUnits", "Provenance")
                SELECT (line->>'id')::uuid, entry_id, (line->>'account_id')::uuid,
                       NULLIF(line->>'wallet_id', '')::uuid, NULLIF(line->>'credit_lot_id', '')::uuid,
                       ordinal::integer, (line->>'side')::integer, (line->>'currency')::integer,
                       (line->>'amount_units')::bigint, NULLIF(line->>'provenance', '')::integer
                FROM jsonb_array_elements(p_lines) WITH ORDINALITY AS item(line, ordinal);
                INSERT INTO public.economy_entry_allocations ("Id", "JournalLineId", "ParentLotId", "AmountUnits")
                SELECT (allocation->>'id')::uuid, (allocation->>'journal_line_id')::uuid,
                       (allocation->>'parent_lot_id')::uuid, (allocation->>'amount_units')::bigint
                FROM jsonb_array_elements(p_allocations) allocation;
                INSERT INTO public.economy_fragment_root_ranges (
                    "Id", "RootSourceStampId", "CreditLotId", "EntryAllocationId", "StartInclusive", "EndExclusive", "ReversalEpoch")
                SELECT (range_value->>'id')::uuid, (range_value->>'root_source_stamp_id')::uuid, NULL,
                       (range_value->>'entry_allocation_id')::uuid, (range_value->>'start_inclusive')::bigint,
                       (range_value->>'end_exclusive')::bigint, (range_value->>'reversal_epoch')::bigint
                FROM jsonb_array_elements(p_root_ranges) range_value;
                INSERT INTO public.economy_idempotency_records ("Id", "Key", "RequestHash", "PostingGroupId", "CreatedAt")
                VALUES (gen_random_uuid(), btrim(p_idempotency_key), request_hash, p_posting_id, p_occurred_at);
                UPDATE public.economy_chain_head
                SET "Sequence" = journal_sequence, "Hash" = journal_hash, "UpdatedAt" = p_occurred_at
                WHERE "Id" = 1;
                payload := json_build_object('PostingId', p_posting_id, 'Hash', journal_hash,
                    'RecordedAt', p_occurred_at)::text;
                INSERT INTO public.economy_outbox_messages ("Id", "PostingGroupId", "Type", "Payload", "PayloadHash", "OccurredAt")
                VALUES (gen_random_uuid(), p_posting_id, 'economy.provider-reversal.accepted.v1', payload,
                    encode(public.digest(convert_to(payload, 'UTF8'), 'sha256'), 'hex'), p_occurred_at);
                posting_id := p_posting_id;
                RETURN NEXT;
            END
            $function$;

            CREATE OR REPLACE FUNCTION economy_private.post_provider_reversal_v1(
                p_capability_id uuid,
                p_actor_id uuid,
                p_tenant_id uuid,
                p_operation_id uuid,
                p_idempotency_key text,
                p_root_source_stamp_id uuid,
                p_cumulative_hard_units bigint,
                p_irrecoverable_disposition integer,
                p_evidence_hash text,
                p_policy_version bigint,
                p_reserve_version bigint,
                p_occurred_at timestamptz)
            RETURNS TABLE(
                operation_id uuid,
                recovered_hard_units bigint,
                recovered_converted_soft_units bigint,
                responsible_debt_hard_units bigint,
                platform_loss_hard_units bigint,
                duplicate boolean)
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
            DECLARE
                existing_operation public.economy_provider_reversal_operations%ROWTYPE;
                claim record;
                reversal_state record;
                candidate record;
                source_event_sequence bigint;
                expected_trace_units bigint;
                recovered_trace_units bigint := 0;
                recovered_hard bigint := 0;
                recovered_soft bigint := 0;
                debt_hard bigint := 0;
                loss_hard bigint := 0;
                selected_trace bigint;
                selected_units bigint;
                account_liability uuid;
                account_soft_reserve uuid;
                account_hard_reserve uuid;
                account_clearing uuid;
                account_gap uuid;
                debit_line_id uuid;
                credit_line_id uuid;
                reserve_debit_line_id uuid;
                reserve_credit_line_id uuid;
                allocation_id uuid;
                posting_group_id uuid;
                target_ranges jsonb := '[]'::jsonb;
                request_hash text;
                is_full boolean;
                debt_id uuid;
            BEGIN
                IF p_capability_id IS NULL OR p_actor_id IS NULL OR p_tenant_id IS NULL OR p_operation_id IS NULL
                   OR p_root_source_stamp_id IS NULL OR p_cumulative_hard_units <= 0
                   OR p_irrecoverable_disposition NOT IN (1, 2) OR p_policy_version <= 0 OR p_reserve_version <= 0
                   OR p_occurred_at IS NULL OR length(btrim(p_idempotency_key)) = 0 OR length(btrim(p_evidence_hash)) = 0 THEN
                    RAISE EXCEPTION 'provider reversal arguments are invalid' USING ERRCODE = '22023';
                END IF;
                PERFORM pg_advisory_xact_lock(hashtextextended(btrim(p_idempotency_key), 0));
                PERFORM pg_advisory_xact_lock(hashtextextended(p_root_source_stamp_id::text, 0));
                request_hash := encode(public.digest(convert_to(jsonb_build_object(
                    'operationId', p_operation_id, 'idempotencyKey', btrim(p_idempotency_key),
                    'rootSourceStampId', p_root_source_stamp_id, 'cumulativeHardUnits', p_cumulative_hard_units,
                    'disposition', p_irrecoverable_disposition, 'evidenceHash', btrim(p_evidence_hash),
                    'policyVersion', p_policy_version, 'reserveVersion', p_reserve_version, 'occurredAt', p_occurred_at)::text, 'UTF8'), 'sha256'), 'hex');
                SELECT * INTO existing_operation FROM public.economy_provider_reversal_operations operation
                WHERE operation."IdempotencyKey" = btrim(p_idempotency_key) FOR UPDATE;
                IF FOUND THEN
                    IF existing_operation."Id" <> p_operation_id OR existing_operation."RequestHash" <> request_hash THEN
                        RAISE EXCEPTION 'provider reversal idempotency key is bound to another request' USING ERRCODE = '23505';
                    END IF;
                    operation_id := existing_operation."Id";
                    recovered_hard_units := existing_operation."RecoveredHardUnits";
                    recovered_converted_soft_units := existing_operation."RecoveredConvertedSoftUnits";
                    responsible_debt_hard_units := existing_operation."ResponsibleDebtHardUnits";
                    platform_loss_hard_units := existing_operation."PlatformLossHardUnits";
                    duplicate := true;
                    RETURN NEXT;
                    RETURN;
                END IF;
                PERFORM 1 FROM public.economy_registered_capabilities capability
                WHERE capability."Id" = p_capability_id AND capability."IsEnabled" AND capability."RevokedAt" IS NULL
                  AND capability."AllowedTemplateKinds" @> jsonb_build_array(2, 3, 18, 19, 20)
                FOR SHARE;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'provider reversal capability is absent, disabled, or unauthorized' USING ERRCODE = '42501';
                END IF;
                SELECT * INTO claim FROM public.economy_funding_claims funding
                WHERE funding."SourceStampId" = p_root_source_stamp_id FOR UPDATE;
                IF NOT FOUND OR claim."State" NOT IN (2, 5) OR claim."ConfirmedAt" IS NULL
                   OR p_cumulative_hard_units > claim."AuthoritativeUsdMinorUnits" THEN
                    RAISE EXCEPTION 'funding claim is absent, not confirmed, or exceeds the authoritative amount' USING ERRCODE = '23514';
                END IF;
                SELECT * INTO reversal_state FROM public.economy_root_reversal_states reversal
                WHERE reversal."RootSourceStampId" = p_root_source_stamp_id FOR UPDATE;
                IF NOT FOUND OR reversal_state."State" <> 'active'
                   OR reversal_state."CumulativeProviderUnits" <> claim."CumulativeProviderReversalUnits"
                   OR p_cumulative_hard_units <= reversal_state."CumulativeProviderUnits" THEN
                    RAISE EXCEPTION 'provider reversal is stale, non-monotonic, or already closed' USING ERRCODE = '40001';
                END IF;
                expected_trace_units := (p_cumulative_hard_units - reversal_state."CumulativeProviderUnits") * 1000;
                is_full := p_cumulative_hard_units = claim."AuthoritativeUsdMinorUnits";

                FOR candidate IN
                    WITH source_ranges AS (
                        SELECT lot."Id" AS lot_id, lot."WalletId" AS wallet_id, lot."Currency" AS currency,
                               lot."Provenance" AS provenance, lot."ConfirmedAt" AS confirmed_at,
                               ranges."StartInclusive" AS start_inclusive, ranges."EndExclusive" AS end_exclusive
                        FROM public.economy_credit_lots lot
                        JOIN public.economy_fragment_root_ranges ranges ON ranges."CreditLotId" = lot."Id"
                        WHERE lot."State" = 1 AND lot."ReversalEpoch" = reversal_state."Epoch"
                          AND ranges."RootSourceStampId" = p_root_source_stamp_id
                          AND ranges."ReversalEpoch" = reversal_state."Epoch"
                          AND lot."Currency" IN (1, 2)
                    ), free_ranges AS (
                        SELECT source_ranges.*, free_range
                        FROM source_ranges
                        CROSS JOIN LATERAL unnest(
                            int8multirange(int8range(source_ranges.start_inclusive, source_ranges.end_exclusive, '[)')) -
                            COALESCE((SELECT range_agg(int8range(used_range."StartInclusive", used_range."EndExclusive", '[)'))
                                FROM public.economy_fragment_root_ranges used_range
                                WHERE used_range."RootSourceStampId" = p_root_source_stamp_id
                                  AND used_range."EntryAllocationId" IS NOT NULL
                                  AND used_range."ReversalEpoch" = reversal_state."Epoch"
                                  AND int8range(used_range."StartInclusive", used_range."EndExclusive", '[)') &&
                                      int8range(source_ranges.start_inclusive, source_ranges.end_exclusive, '[)')), '{}'::int8multirange)
                        ) AS free_range
                    ), normalized AS (
                        SELECT lot_id, wallet_id, currency, provenance, confirmed_at,
                               lower(free_range)::bigint AS start_inclusive, upper(free_range)::bigint AS end_exclusive,
                               ((upper(free_range) - lower(free_range)) / 1000) * 1000 AS usable_trace_units
                        FROM free_ranges
                    ), ordered AS (
                        SELECT normalized.*, COALESCE(sum(usable_trace_units) OVER (
                            ORDER BY start_inclusive, confirmed_at, lot_id ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING), 0) AS prior_trace_units
                        FROM normalized WHERE usable_trace_units > 0
                    )
                    SELECT lot_id, wallet_id, currency, provenance, start_inclusive,
                           start_inclusive + LEAST(usable_trace_units, expected_trace_units - prior_trace_units) AS end_exclusive,
                           LEAST(usable_trace_units, expected_trace_units - prior_trace_units) AS trace_units
                    FROM ordered WHERE prior_trace_units < expected_trace_units
                    ORDER BY start_inclusive, confirmed_at, lot_id
                    FOR UPDATE OF public.economy_credit_lots
                LOOP
                    selected_trace := candidate.trace_units;
                    IF selected_trace <= 0 THEN CONTINUE; END IF;
                    selected_units := CASE WHEN candidate.currency = 1 THEN selected_trace / 1000 ELSE selected_trace END;
                    debit_line_id := gen_random_uuid();
                    credit_line_id := gen_random_uuid();
                    allocation_id := gen_random_uuid();
                    posting_group_id := gen_random_uuid();
                    IF candidate.currency = 1 THEN
                        SELECT "Id" INTO account_liability FROM public.economy_accounts
                        WHERE "WalletId" = candidate.wallet_id AND "Code" = 2 AND "Currency" = 1 AND "Provenance" = 1;
                        SELECT "Id" INTO account_clearing FROM public.economy_accounts
                        WHERE "WalletId" IS NULL AND "Code" = 1 AND "Currency" = 1 AND "Provenance" IS NULL;
                        IF account_liability IS NULL OR account_clearing IS NULL THEN
                            RAISE EXCEPTION 'provider reversal hard account partitions are absent' USING ERRCODE = '23503';
                        END IF;
                        PERFORM economy_private.append_provider_reversal_posting_v1(
                            p_capability_id, p_actor_id, p_tenant_id, posting_group_id,
                            btrim(p_idempotency_key) || ':hard:' || recovered_hard::text,
                            CASE WHEN is_full THEN 2 ELSE 3 END, p_policy_version, p_reserve_version,
                            p_root_source_stamp_id, p_occurred_at,
                            jsonb_build_array(
                                jsonb_build_object('id', debit_line_id, 'account_id', account_liability, 'wallet_id', candidate.wallet_id, 'credit_lot_id', NULL, 'side', 1, 'currency', 1, 'amount_units', selected_units, 'provenance', 1),
                                jsonb_build_object('id', credit_line_id, 'account_id', account_clearing, 'wallet_id', NULL, 'credit_lot_id', NULL, 'side', 2, 'currency', 1, 'amount_units', selected_units, 'provenance', NULL)),
                            jsonb_build_array(jsonb_build_object('id', allocation_id, 'journal_line_id', debit_line_id, 'parent_lot_id', candidate.lot_id, 'amount_units', selected_units)),
                            jsonb_build_array(jsonb_build_object('id', gen_random_uuid(), 'root_source_stamp_id', p_root_source_stamp_id, 'entry_allocation_id', allocation_id, 'start_inclusive', candidate.start_inclusive, 'end_exclusive', candidate.end_exclusive, 'reversal_epoch', reversal_state."Epoch")));
                        recovered_hard := recovered_hard + selected_units;
                    ELSE
                        SELECT "Id" INTO account_liability FROM public.economy_accounts
                        WHERE "WalletId" = candidate.wallet_id AND "Code" = 4 AND "Currency" = 2 AND "Provenance" = 3;
                        SELECT "Id" INTO account_soft_reserve FROM public.economy_accounts
                        WHERE "WalletId" IS NULL AND "Code" = 6 AND "Currency" = 2 AND "Provenance" IS NULL;
                        SELECT "Id" INTO account_hard_reserve FROM public.economy_accounts
                        WHERE "WalletId" IS NULL AND "Code" = 5 AND "Currency" = 1 AND "Provenance" IS NULL;
                        SELECT "Id" INTO account_clearing FROM public.economy_accounts
                        WHERE "WalletId" IS NULL AND "Code" = 1 AND "Currency" = 1 AND "Provenance" IS NULL;
                        IF account_liability IS NULL OR account_soft_reserve IS NULL OR account_hard_reserve IS NULL OR account_clearing IS NULL THEN
                            RAISE EXCEPTION 'provider reversal soft account partitions are absent' USING ERRCODE = '23503';
                        END IF;
                        reserve_debit_line_id := gen_random_uuid();
                        reserve_credit_line_id := gen_random_uuid();
                        PERFORM economy_private.append_provider_reversal_posting_v1(
                            p_capability_id, p_actor_id, p_tenant_id, posting_group_id,
                            btrim(p_idempotency_key) || ':soft:' || recovered_soft::text,
                            18, p_policy_version, p_reserve_version, p_root_source_stamp_id, p_occurred_at,
                            jsonb_build_array(
                                jsonb_build_object('id', debit_line_id, 'account_id', account_liability, 'wallet_id', candidate.wallet_id, 'credit_lot_id', NULL, 'side', 1, 'currency', 2, 'amount_units', selected_units, 'provenance', 3),
                                jsonb_build_object('id', credit_line_id, 'account_id', account_soft_reserve, 'wallet_id', NULL, 'credit_lot_id', NULL, 'side', 2, 'currency', 2, 'amount_units', selected_units, 'provenance', NULL),
                                jsonb_build_object('id', reserve_debit_line_id, 'account_id', account_hard_reserve, 'wallet_id', NULL, 'credit_lot_id', NULL, 'side', 1, 'currency', 1, 'amount_units', selected_units / 1000, 'provenance', NULL),
                                jsonb_build_object('id', reserve_credit_line_id, 'account_id', account_clearing, 'wallet_id', NULL, 'credit_lot_id', NULL, 'side', 2, 'currency', 1, 'amount_units', selected_units / 1000, 'provenance', NULL)),
                            jsonb_build_array(jsonb_build_object('id', allocation_id, 'journal_line_id', debit_line_id, 'parent_lot_id', candidate.lot_id, 'amount_units', selected_units)),
                            jsonb_build_array(jsonb_build_object('id', gen_random_uuid(), 'root_source_stamp_id', p_root_source_stamp_id, 'entry_allocation_id', allocation_id, 'start_inclusive', candidate.start_inclusive, 'end_exclusive', candidate.end_exclusive, 'reversal_epoch', reversal_state."Epoch")));
                        recovered_soft := recovered_soft + selected_units;
                    END IF;
                    INSERT INTO public.economy_provider_reversal_fragments (
                        "Id", "OperationId", "PostingGroupId", "ParentLotId", "WalletId", "Currency", "AmountUnits", "StartInclusive", "EndExclusive")
                    VALUES (gen_random_uuid(), p_operation_id, posting_group_id, candidate.lot_id, candidate.wallet_id,
                        candidate.currency, selected_units, candidate.start_inclusive, candidate.end_exclusive);
                    target_ranges := target_ranges || jsonb_build_array(jsonb_build_object(
                        'start', candidate.start_inclusive, 'endExclusive', candidate.end_exclusive, 'currency', candidate.currency));
                    recovered_trace_units := recovered_trace_units + selected_trace;
                END LOOP;
                IF recovered_trace_units > expected_trace_units THEN
                    RAISE EXCEPTION 'provider reversal over-consumed its source trace' USING ERRCODE = '23514';
                END IF;
                IF (expected_trace_units - recovered_trace_units) % 1000 <> 0 THEN
                    RAISE EXCEPTION 'provider reversal remainder violates hard-to-soft parity' USING ERRCODE = '23514';
                END IF;
                IF expected_trace_units > recovered_trace_units THEN
                    IF p_irrecoverable_disposition = 1 THEN debt_hard := (expected_trace_units - recovered_trace_units) / 1000;
                    ELSE loss_hard := (expected_trace_units - recovered_trace_units) / 1000;
                    END IF;
                    posting_group_id := gen_random_uuid();
                    debit_line_id := gen_random_uuid();
                    credit_line_id := gen_random_uuid();
                    SELECT "Id" INTO account_gap FROM public.economy_accounts
                    WHERE "WalletId" IS NULL AND "Code" = CASE WHEN debt_hard > 0 THEN 13 ELSE 15 END AND "Currency" = 1 AND "Provenance" IS NULL;
                    SELECT "Id" INTO account_clearing FROM public.economy_accounts
                    WHERE "WalletId" IS NULL AND "Code" = 1 AND "Currency" = 1 AND "Provenance" IS NULL;
                    IF account_gap IS NULL OR account_clearing IS NULL THEN
                        RAISE EXCEPTION 'provider reversal gap account partitions are absent' USING ERRCODE = '23503';
                    END IF;
                    PERFORM economy_private.append_provider_reversal_posting_v1(
                        p_capability_id, p_actor_id, p_tenant_id, posting_group_id, btrim(p_idempotency_key) || ':gap',
                        CASE WHEN debt_hard > 0 THEN 19 ELSE 20 END, p_policy_version, p_reserve_version,
                        p_root_source_stamp_id, p_occurred_at,
                        jsonb_build_array(
                            jsonb_build_object('id', debit_line_id, 'account_id', account_gap, 'wallet_id', NULL, 'credit_lot_id', NULL, 'side', 1, 'currency', 1, 'amount_units', COALESCE(NULLIF(debt_hard, 0), loss_hard), 'provenance', NULL),
                            jsonb_build_object('id', credit_line_id, 'account_id', account_clearing, 'wallet_id', NULL, 'credit_lot_id', NULL, 'side', 2, 'currency', 1, 'amount_units', COALESCE(NULLIF(debt_hard, 0), loss_hard), 'provenance', NULL)),
                        '[]'::jsonb, '[]'::jsonb);
                    IF debt_hard > 0 THEN
                        debt_id := gen_random_uuid();
                        INSERT INTO public.economy_wallet_debts ("Id", "WalletId", "RootSourceStampId", "Currency", "AmountUnits", "OutstandingUnits", "State", "CreatedAt", "UpdatedAt")
                        VALUES (debt_id, claim."WalletId", p_root_source_stamp_id, 1, debt_hard, debt_hard, 1, p_occurred_at, p_occurred_at);
                        INSERT INTO public.economy_wallet_debt_events ("Id", "DebtId", "OperationId", "Kind", "AmountUnits", "OccurredAt")
                        VALUES (gen_random_uuid(), debt_id, p_operation_id, 1, debt_hard, p_occurred_at);
                    END IF;
                END IF;
                INSERT INTO public.economy_provider_reversal_operations (
                    "Id", "IdempotencyKey", "RequestHash", "RootSourceStampId", "CumulativeHardUnits", "ReversalEpoch", "IrrecoverableDisposition",
                    "RecoveredHardUnits", "RecoveredConvertedSoftUnits", "ResponsibleDebtHardUnits", "PlatformLossHardUnits", "OccurredAt")
                VALUES (p_operation_id, btrim(p_idempotency_key), request_hash, p_root_source_stamp_id, p_cumulative_hard_units,
                    reversal_state."Epoch", p_irrecoverable_disposition, recovered_hard, recovered_soft, debt_hard, loss_hard, p_occurred_at);
                UPDATE public.economy_root_reversal_states
                SET "CumulativeProviderUnits" = p_cumulative_hard_units, "ReversedUnits" = p_cumulative_hard_units,
                    "State" = CASE WHEN is_full THEN 'reversed' ELSE 'active' END,
                    "TargetedRanges" = "TargetedRanges" || target_ranges, "UpdatedAt" = p_occurred_at
                WHERE "RootSourceStampId" = p_root_source_stamp_id;
                UPDATE public.economy_funding_claims
                SET "State" = CASE WHEN is_full THEN 6 ELSE 5 END,
                    "CumulativeProviderReversalUnits" = p_cumulative_hard_units,
                    "StateChangedAt" = p_occurred_at, "Version" = "Version" + 1
                WHERE "SourceStampId" = p_root_source_stamp_id;
                SELECT COALESCE(max("Sequence"), 0) + 1 INTO source_event_sequence
                FROM public.economy_source_stamp_events WHERE "SourceStampId" = p_root_source_stamp_id;
                INSERT INTO public.economy_source_stamp_events ("Id", "SourceStampId", "Sequence", "State", "EvidenceHash", "OccurredAt")
                VALUES (gen_random_uuid(), p_root_source_stamp_id, source_event_sequence,
                    CASE WHEN is_full THEN 6 ELSE 5 END, btrim(p_evidence_hash), p_occurred_at);
                PERFORM economy_private.rebuild_wallet_projection_v1(claim."WalletId", p_occurred_at);
                operation_id := p_operation_id;
                recovered_hard_units := recovered_hard;
                recovered_converted_soft_units := recovered_soft;
                responsible_debt_hard_units := debt_hard;
                platform_loss_hard_units := loss_hard;
                duplicate := false;
                RETURN NEXT;
            END
            $function$;

            ALTER FUNCTION economy_private.append_provider_reversal_posting_v1(uuid,uuid,uuid,uuid,text,integer,bigint,bigint,uuid,timestamptz,jsonb,jsonb,jsonb)
                OWNER TO gameguild_economy_procedure_owner;
            ALTER FUNCTION economy_private.post_provider_reversal_v1(uuid,uuid,uuid,uuid,text,uuid,bigint,integer,text,bigint,bigint,timestamptz)
                OWNER TO gameguild_economy_procedure_owner;
            REVOKE ALL ON FUNCTION economy_private.append_provider_reversal_posting_v1(uuid,uuid,uuid,uuid,text,integer,bigint,bigint,uuid,timestamptz,jsonb,jsonb,jsonb) FROM PUBLIC;
            REVOKE ALL ON FUNCTION economy_private.post_provider_reversal_v1(uuid,uuid,uuid,uuid,text,uuid,bigint,integer,text,bigint,bigint,timestamptz) FROM PUBLIC;
            GRANT EXECUTE ON FUNCTION economy_private.post_provider_reversal_v1(uuid,uuid,uuid,uuid,text,uuid,bigint,integer,text,bigint,bigint,timestamptz)
                TO gameguild_economy_writer;
            REVOKE ALL ON TABLE public.economy_provider_reversal_operations, public.economy_provider_reversal_fragments,
                public.economy_wallet_debts, public.economy_wallet_debt_events FROM PUBLIC;
            GRANT SELECT ON TABLE public.economy_provider_reversal_operations, public.economy_provider_reversal_fragments,
                public.economy_wallet_debts, public.economy_wallet_debt_events TO gameguild_economy_runtime;
            GRANT SELECT, INSERT, UPDATE ON TABLE public.economy_provider_reversal_operations, public.economy_provider_reversal_fragments,
                public.economy_wallet_debts, public.economy_wallet_debt_events TO gameguild_economy_procedure_owner;
            GRANT ALL ON TABLE public.economy_provider_reversal_operations, public.economy_provider_reversal_fragments,
                public.economy_wallet_debts, public.economy_wallet_debt_events TO gameguild_economy_migration;
            """);
    }

    private static void RemoveProviderReversalWriter(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP FUNCTION IF EXISTS economy_private.post_provider_reversal_v1(uuid,uuid,uuid,uuid,text,uuid,bigint,integer,text,bigint,bigint,timestamptz);
            DROP FUNCTION IF EXISTS economy_private.append_provider_reversal_posting_v1(uuid,uuid,uuid,uuid,text,integer,bigint,bigint,uuid,timestamptz,jsonb,jsonb,jsonb);
            """);
    }
}
