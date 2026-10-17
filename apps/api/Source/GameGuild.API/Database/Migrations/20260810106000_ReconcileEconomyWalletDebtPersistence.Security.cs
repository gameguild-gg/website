using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations;

public partial class ReconcileEconomyWalletDebtPersistence : Migration
{
    private static void RestoreWalletDebtPersistence(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TABLE IF EXISTS public.economy_wallet_debt_events;
            DROP TABLE IF EXISTS public.economy_wallet_debts;
            ALTER TABLE public.economy_wallet_debt_events_legacy RENAME TO economy_wallet_debt_events;
            ALTER TABLE public.economy_wallet_debts_legacy RENAME TO economy_wallet_debts;
            """);

        HardenEconomyProviderReversalWriter.InstallHardenedProviderReversalWriter(migrationBuilder);
    }

    private static void ReconcileWalletDebtPersistence(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE public.economy_wallet_debt_events RENAME TO economy_wallet_debt_events_legacy;
            ALTER TABLE public.economy_wallet_debts RENAME TO economy_wallet_debts_legacy;

            CREATE TABLE public.economy_wallet_debts (
                "WalletId" uuid NOT NULL,
                "OutstandingHardUnits" bigint NOT NULL,
                "UpdatedAt" timestamptz NOT NULL,
                "Version" bigint NOT NULL,
                CONSTRAINT "PK_economy_wallet_debts_current" PRIMARY KEY ("WalletId"),
                CONSTRAINT "FK_economy_wallet_debts_current_economy_wallets_WalletId"
                    FOREIGN KEY ("WalletId") REFERENCES public.economy_wallets("Id") ON DELETE RESTRICT,
                CONSTRAINT ck_economy_wallet_debts_nonnegative CHECK ("OutstandingHardUnits" >= 0),
                CONSTRAINT ck_economy_wallet_debts_version_positive CHECK ("Version" > 0)
            );

            CREATE TABLE public.economy_wallet_debt_events (
                "Id" uuid NOT NULL,
                "WalletId" uuid NOT NULL,
                "SourceStampId" uuid NOT NULL,
                "Sequence" bigint NOT NULL,
                "DeltaHardUnits" bigint NOT NULL,
                "OutstandingHardUnits" bigint NOT NULL,
                "OccurredAt" timestamptz NOT NULL,
                CONSTRAINT "PK_economy_wallet_debt_events_current" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_economy_wallet_debt_events_current_economy_wallet_debts_WalletId"
                    FOREIGN KEY ("WalletId") REFERENCES public.economy_wallet_debts("WalletId") ON DELETE RESTRICT,
                CONSTRAINT "FK_economy_wallet_debt_events_current_economy_source_stamps_SourceStampId"
                    FOREIGN KEY ("SourceStampId") REFERENCES public.economy_source_stamps("Id") ON DELETE RESTRICT,
                CONSTRAINT ck_economy_wallet_debt_events_sequence_positive CHECK ("Sequence" > 0),
                CONSTRAINT ck_economy_wallet_debt_events_delta_nonzero
                    CHECK ("DeltaHardUnits" <> 0 AND "OutstandingHardUnits" >= 0)
            );
            CREATE UNIQUE INDEX ux_economy_wallet_debt_events_wallet_sequence
                ON public.economy_wallet_debt_events ("WalletId", "Sequence");

            INSERT INTO public.economy_wallet_debts (
                "WalletId", "OutstandingHardUnits", "UpdatedAt", "Version")
            SELECT legacy."WalletId", sum(legacy."OutstandingUnits"), max(legacy."UpdatedAt"), 1
            FROM public.economy_wallet_debts_legacy legacy
            WHERE legacy."State" = 1 AND legacy."OutstandingUnits" > 0
            GROUP BY legacy."WalletId";

            INSERT INTO public.economy_wallet_debt_events (
                "Id", "WalletId", "SourceStampId", "Sequence", "DeltaHardUnits", "OutstandingHardUnits", "OccurredAt")
            SELECT
                legacy."Id",
                legacy."WalletId",
                legacy."RootSourceStampId",
                row_number() OVER (
                    PARTITION BY legacy."WalletId"
                    ORDER BY legacy."CreatedAt", legacy."UpdatedAt", legacy."Id"),
                legacy."OutstandingUnits",
                sum(legacy."OutstandingUnits") OVER (
                    PARTITION BY legacy."WalletId"
                    ORDER BY legacy."CreatedAt", legacy."UpdatedAt", legacy."Id"
                    ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW),
                legacy."UpdatedAt"
            FROM public.economy_wallet_debts_legacy legacy
            WHERE legacy."State" = 1 AND legacy."OutstandingUnits" > 0;

            REVOKE ALL ON TABLE public.economy_wallet_debts, public.economy_wallet_debt_events FROM PUBLIC;
            REVOKE ALL ON TABLE public.economy_wallet_debts, public.economy_wallet_debt_events FROM gameguild_economy_writer;
            GRANT SELECT ON TABLE public.economy_wallet_debts, public.economy_wallet_debt_events TO gameguild_economy_runtime;
            GRANT SELECT, INSERT, UPDATE ON TABLE public.economy_wallet_debts, public.economy_wallet_debt_events
                TO gameguild_economy_procedure_owner;
            GRANT ALL ON TABLE public.economy_wallet_debts, public.economy_wallet_debt_events TO gameguild_economy_migration;
            """);

        migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION economy_private.post_provider_reversal_v2(
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
                            p_risk_decision_id uuid,
                            p_risk_operation_fingerprint text,
                            p_expected_counter_version bigint,
                            p_occurred_at timestamptz,
                            p_dispatch_snapshot_hash text)
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
                            risk_record record;
                            chain_head record;
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
                            template_kind integer;
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
                            entry_id uuid := gen_random_uuid();
                            debt_outstanding_hard_units bigint;
                            debt_event_sequence bigint;
                            line_count integer := 0;
                            all_lines jsonb := '[]'::jsonb;
                            all_allocations jsonb := '[]'::jsonb;
                            all_ranges jsonb := '[]'::jsonb;
                            all_fragments jsonb := '[]'::jsonb;
                            targeted_ranges jsonb := '[]'::jsonb;
                            request_hash text;
                            journal_hash text;
                            outbox_payload text;
                            is_full boolean;
                        BEGIN
                            IF p_capability_id IS NULL OR p_actor_id IS NULL OR p_tenant_id IS NULL OR p_operation_id IS NULL
                               OR p_root_source_stamp_id IS NULL OR p_risk_decision_id IS NULL
                               OR p_cumulative_hard_units <= 0 OR p_irrecoverable_disposition NOT IN (1, 2)
                               OR p_policy_version <= 0 OR p_reserve_version <= 0 OR p_expected_counter_version <= 0
                               OR p_occurred_at IS NULL OR length(btrim(p_idempotency_key)) = 0
                               OR length(btrim(p_evidence_hash)) = 0 OR length(btrim(p_risk_operation_fingerprint)) = 0 THEN
                                RAISE EXCEPTION 'hardened provider reversal arguments are invalid' USING ERRCODE = '22023';
                            END IF;
                            PERFORM pg_advisory_xact_lock(hashtextextended(btrim(p_idempotency_key), 0));
                            PERFORM pg_advisory_xact_lock(hashtextextended(p_root_source_stamp_id::text, 0));
                            request_hash := encode(public.digest(convert_to(jsonb_build_object(
                                'capabilityId', p_capability_id, 'actorId', p_actor_id, 'tenantId', p_tenant_id,
                                'operationId', p_operation_id, 'idempotencyKey', btrim(p_idempotency_key),
                                'rootSourceStampId', p_root_source_stamp_id, 'cumulativeHardUnits', p_cumulative_hard_units,
                                'disposition', p_irrecoverable_disposition, 'evidenceHash', btrim(p_evidence_hash),
                                'policyVersion', p_policy_version, 'reserveVersion', p_reserve_version,
                                'riskDecisionId', p_risk_decision_id, 'riskOperationFingerprint', btrim(p_risk_operation_fingerprint),
                                'counterVersion', p_expected_counter_version, 'occurredAt', p_occurred_at,
                                'dispatchSnapshotHash', p_dispatch_snapshot_hash)::text, 'UTF8'), 'sha256'), 'hex');
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
                            SELECT * INTO claim FROM public.economy_funding_claims funding
                            WHERE funding."SourceStampId" = p_root_source_stamp_id FOR UPDATE;
                            IF NOT FOUND OR claim."State" NOT IN (2, 5) OR claim."ConfirmedAt" IS NULL
                               OR p_cumulative_hard_units > claim."AuthoritativeUsdMinorUnits" THEN
                                RAISE EXCEPTION 'funding claim is absent, not confirmed, or exceeds its authoritative value' USING ERRCODE = '23514';
                            END IF;                PERFORM pg_advisory_xact_lock(hashtextextended(claim."WalletId"::text, 0));
                            SELECT * INTO reversal_state FROM public.economy_root_reversal_states reversal
                            WHERE reversal."RootSourceStampId" = p_root_source_stamp_id FOR UPDATE;
                            IF NOT FOUND OR reversal_state."State" <> 'active'
                               OR reversal_state."CumulativeProviderUnits" <> claim."CumulativeProviderReversalUnits"
                               OR p_cumulative_hard_units <= reversal_state."CumulativeProviderUnits" THEN
                                RAISE EXCEPTION 'provider reversal is stale, non-monotonic, or closed' USING ERRCODE = '40001';
                            END IF;
                            expected_trace_units := (p_cumulative_hard_units - reversal_state."CumulativeProviderUnits") * 1000;
                            is_full := p_cumulative_hard_units = claim."AuthoritativeUsdMinorUnits";
                            template_kind := CASE WHEN is_full THEN 2 ELSE 3 END;
                            PERFORM 1 FROM public.economy_registered_capabilities capability
                            WHERE capability."Id" = p_capability_id AND capability."IsEnabled" AND capability."RevokedAt" IS NULL
                              AND capability."AllowedTemplateKinds" @> jsonb_build_array(template_kind)
                            FOR SHARE;
                            IF NOT FOUND THEN
                                RAISE EXCEPTION 'provider reversal capability is absent, disabled, or unauthorized' USING ERRCODE = '42501';
                            END IF;
                            SELECT * INTO risk_record FROM public.economy_risk_decisions risk
                            WHERE risk."Id" = p_risk_decision_id
                              AND risk."Outcome" = 1
                              AND risk."OperationFingerprint" = btrim(p_risk_operation_fingerprint)
                              AND risk."TemplateKind" = template_kind
                              AND risk."PolicyVersion" = p_policy_version
                              AND risk."ReserveVersion" = p_reserve_version
                              AND risk."CounterVersion" = p_expected_counter_version
                              AND risk."Currency" = 1
                              AND risk."AmountUnits" = expected_trace_units / 1000
                              AND risk."IssuedAt" <= p_occurred_at
                              AND risk."ExpiresAt" > p_occurred_at
                            FOR UPDATE;
                            IF NOT FOUND OR EXISTS (
                                SELECT 1 FROM public.economy_risk_decision_consumptions consumption
                                WHERE consumption."RiskDecisionId" = p_risk_decision_id) OR NOT EXISTS (
                                SELECT 1 FROM public.economy_risk_counter_reservations reservation
                                JOIN public.economy_risk_counters counter ON counter."Id" = reservation."RiskCounterId"
                                WHERE reservation."RiskDecisionId" = p_risk_decision_id
                                  AND reservation."AmountUnits" = expected_trace_units / 1000
                                  AND counter."CounterVersion" = p_expected_counter_version) THEN
                                RAISE EXCEPTION 'provider reversal risk authorization is missing, stale, or consumed' USING ERRCODE = '42501';
                            END IF;

                            FOR candidate IN
                                WITH source_ranges AS (
                                    SELECT lot."Id" AS lot_id, lot."WalletId" AS wallet_id, lot."Currency" AS currency,
                                           lot."Provenance" AS provenance, lot."ConfirmedAt" AS confirmed_at,
                                           ranges."StartInclusive" AS start_inclusive, ranges."EndExclusive" AS end_exclusive
                                    FROM public.economy_credit_lots lot
                                    JOIN public.economy_fragment_root_ranges ranges ON ranges."CreditLotId" = lot."Id"
                                    WHERE lot."State" = 1 AND lot."ReversalEpoch" = reversal_state."Epoch"
                                      AND ranges."RootSourceStampId" = p_root_source_stamp_id
                                      AND ranges."ReversalEpoch" = reversal_state."Epoch" AND lot."Currency" IN (1, 2)
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
                            LOOP
                                PERFORM 1 FROM public.economy_credit_lots lot WHERE lot."Id" = candidate.lot_id FOR UPDATE;
                                selected_trace := candidate.trace_units;
                                IF selected_trace <= 0 THEN CONTINUE; END IF;
                                selected_units := CASE WHEN candidate.currency = 1 THEN selected_trace / 1000 ELSE selected_trace END;
                                debit_line_id := gen_random_uuid();
                                credit_line_id := gen_random_uuid();
                                allocation_id := gen_random_uuid();
                                IF candidate.currency = 1 THEN
                                    SELECT "Id" INTO account_liability FROM public.economy_accounts
                                    WHERE "WalletId" = candidate.wallet_id AND "Code" = 2 AND "Currency" = 1 AND "Provenance" = 1;
                                    SELECT "Id" INTO account_clearing FROM public.economy_accounts
                                    WHERE "WalletId" IS NULL AND "Code" = 1 AND "Currency" = 1 AND "Provenance" IS NULL;
                                    IF account_liability IS NULL OR account_clearing IS NULL THEN
                                        RAISE EXCEPTION 'provider reversal hard account partitions are absent' USING ERRCODE = '23503';
                                    END IF;
                                    all_lines := all_lines || jsonb_build_array(
                                        jsonb_build_object('id', debit_line_id, 'account_id', account_liability, 'wallet_id', candidate.wallet_id, 'credit_lot_id', NULL, 'side', 1, 'currency', 1, 'amount_units', selected_units, 'provenance', 1),
                                        jsonb_build_object('id', credit_line_id, 'account_id', account_clearing, 'wallet_id', NULL, 'credit_lot_id', NULL, 'side', 2, 'currency', 1, 'amount_units', selected_units, 'provenance', NULL));
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
                                    all_lines := all_lines || jsonb_build_array(
                                        jsonb_build_object('id', debit_line_id, 'account_id', account_liability, 'wallet_id', candidate.wallet_id, 'credit_lot_id', NULL, 'side', 1, 'currency', 2, 'amount_units', selected_units, 'provenance', 3),
                                        jsonb_build_object('id', credit_line_id, 'account_id', account_soft_reserve, 'wallet_id', NULL, 'credit_lot_id', NULL, 'side', 2, 'currency', 2, 'amount_units', selected_units, 'provenance', NULL),
                                        jsonb_build_object('id', reserve_debit_line_id, 'account_id', account_hard_reserve, 'wallet_id', NULL, 'credit_lot_id', NULL, 'side', 1, 'currency', 1, 'amount_units', selected_units / 1000, 'provenance', NULL),
                                        jsonb_build_object('id', reserve_credit_line_id, 'account_id', account_clearing, 'wallet_id', NULL, 'credit_lot_id', NULL, 'side', 2, 'currency', 1, 'amount_units', selected_units / 1000, 'provenance', NULL));
                                    recovered_soft := recovered_soft + selected_units;
                                END IF;
                                all_allocations := all_allocations || jsonb_build_array(jsonb_build_object(
                                    'id', allocation_id, 'journal_line_id', debit_line_id, 'parent_lot_id', candidate.lot_id, 'amount_units', selected_units));
                                all_ranges := all_ranges || jsonb_build_array(jsonb_build_object(
                                    'id', gen_random_uuid(), 'root_source_stamp_id', p_root_source_stamp_id,
                                    'entry_allocation_id', allocation_id, 'start_inclusive', candidate.start_inclusive,
                                    'end_exclusive', candidate.end_exclusive, 'reversal_epoch', reversal_state."Epoch"));
                                targeted_ranges := targeted_ranges || jsonb_build_array(jsonb_build_object(
                                    'start', candidate.start_inclusive, 'endExclusive', candidate.end_exclusive, 'currency', candidate.currency));
                                all_fragments := all_fragments || jsonb_build_array(jsonb_build_object(
                                    'id', gen_random_uuid(), 'parent_lot_id', candidate.lot_id, 'wallet_id', candidate.wallet_id,
                                    'currency', candidate.currency, 'amount_units', selected_units,
                                    'start_inclusive', candidate.start_inclusive, 'end_exclusive', candidate.end_exclusive));
                                recovered_trace_units := recovered_trace_units + selected_trace;
                            END LOOP;
                            IF recovered_trace_units > expected_trace_units OR (expected_trace_units - recovered_trace_units) % 1000 <> 0 THEN
                                RAISE EXCEPTION 'provider reversal would violate lineage conservation or fixed parity' USING ERRCODE = '23514';
                            END IF;
                            IF expected_trace_units > recovered_trace_units THEN
                                IF p_irrecoverable_disposition = 1 THEN debt_hard := (expected_trace_units - recovered_trace_units) / 1000;
                                ELSE loss_hard := (expected_trace_units - recovered_trace_units) / 1000;
                                END IF;
                                debit_line_id := gen_random_uuid();
                                credit_line_id := gen_random_uuid();
                                SELECT "Id" INTO account_gap FROM public.economy_accounts
                                WHERE "WalletId" IS NULL AND "Code" = CASE WHEN debt_hard > 0 THEN 13 ELSE 15 END AND "Currency" = 1 AND "Provenance" IS NULL;
                                SELECT "Id" INTO account_clearing FROM public.economy_accounts
                                WHERE "WalletId" IS NULL AND "Code" = 1 AND "Currency" = 1 AND "Provenance" IS NULL;
                                IF account_gap IS NULL OR account_clearing IS NULL THEN
                                    RAISE EXCEPTION 'provider reversal gap account partitions are absent' USING ERRCODE = '23503';
                                END IF;
                                all_lines := all_lines || jsonb_build_array(
                                    jsonb_build_object('id', debit_line_id, 'account_id', account_gap, 'wallet_id', NULL, 'credit_lot_id', NULL, 'side', 1, 'currency', 1, 'amount_units', COALESCE(NULLIF(debt_hard, 0), loss_hard), 'provenance', NULL),
                                    jsonb_build_object('id', credit_line_id, 'account_id', account_clearing, 'wallet_id', NULL, 'credit_lot_id', NULL, 'side', 2, 'currency', 1, 'amount_units', COALESCE(NULLIF(debt_hard, 0), loss_hard), 'provenance', NULL));
                                IF debt_hard > 0 THEN
                                    INSERT INTO public.economy_wallet_debts (
                                        "WalletId", "OutstandingHardUnits", "UpdatedAt", "Version")
                                    VALUES (claim."WalletId", debt_hard, p_occurred_at, 1)
                                    ON CONFLICT ("WalletId") DO UPDATE
                                    SET "OutstandingHardUnits" = public.economy_wallet_debts."OutstandingHardUnits" + EXCLUDED."OutstandingHardUnits",
                                        "UpdatedAt" = EXCLUDED."UpdatedAt",
                                        "Version" = public.economy_wallet_debts."Version" + 1
                                    RETURNING "OutstandingHardUnits" INTO debt_outstanding_hard_units;

                                    SELECT COALESCE(max("Sequence"), 0) + 1 INTO debt_event_sequence
                                    FROM public.economy_wallet_debt_events
                                    WHERE "WalletId" = claim."WalletId";
                                    INSERT INTO public.economy_wallet_debt_events (
                                        "Id", "WalletId", "SourceStampId", "Sequence", "DeltaHardUnits", "OutstandingHardUnits", "OccurredAt")
                                    VALUES (
                                        gen_random_uuid(), claim."WalletId", p_root_source_stamp_id, debt_event_sequence,
                                        debt_hard, debt_outstanding_hard_units, p_occurred_at);
                                END IF;
                            END IF;
                            IF jsonb_array_length(all_lines) = 0 OR EXISTS (
                                SELECT 1 FROM jsonb_array_elements(all_lines) line
                                GROUP BY (line->>'currency')::integer
                                HAVING sum(CASE WHEN (line->>'side')::integer = 1 THEN (line->>'amount_units')::bigint ELSE 0 END) <>
                                       sum(CASE WHEN (line->>'side')::integer = 2 THEN (line->>'amount_units')::bigint ELSE 0 END)
                            ) THEN
                                RAISE EXCEPTION 'provider reversal journal is not currency balanced' USING ERRCODE = '23514';
                            END IF;
                            INSERT INTO public.economy_chain_head ("Id", "Sequence", "Hash", "UpdatedAt")
                            VALUES (1, 0, repeat('0', 64), p_occurred_at) ON CONFLICT ("Id") DO NOTHING;
                            SELECT * INTO chain_head FROM public.economy_chain_head WHERE "Id" = 1 FOR UPDATE;
                            journal_hash := encode(public.digest(convert_to(concat_ws('|', chain_head."Hash", p_operation_id::text,
                                (chain_head."Sequence" + 1)::text, request_hash), 'UTF8'), 'sha256'), 'hex');
                            INSERT INTO public.economy_posting_groups (
                                "Id", "IdempotencyKey", "TemplateKind", "TemplateVersion", "Authority", "Status", "CapabilityId", "ActorId", "TenantId",
                                "RiskDecisionId", "PolicyVersion", "ReserveVersion", "ReserveAuthorizationEpoch", "SourceStampId", "RecordedAt")
                            VALUES (p_operation_id, btrim(p_idempotency_key), template_kind, 1, 1, 1, p_capability_id, p_actor_id, p_tenant_id,
                                p_risk_decision_id, p_policy_version, p_reserve_version, risk_record."ReserveAuthorizationEpoch", p_root_source_stamp_id, p_occurred_at);
                            INSERT INTO public.economy_journal_entries ("Id", "PostingGroupId", "Sequence", "PreviousHash", "Hash", "RecordedAt")
                            VALUES (entry_id, p_operation_id, chain_head."Sequence" + 1, chain_head."Hash", journal_hash, p_occurred_at);
                            INSERT INTO public.economy_journal_lines (
                                "Id", "JournalEntryId", "AccountId", "WalletId", "CreditLotId", "Sequence", "Side", "Currency", "AmountUnits", "Provenance")
                            SELECT (line->>'id')::uuid, entry_id, (line->>'account_id')::uuid, NULLIF(line->>'wallet_id', '')::uuid,
                                   NULLIF(line->>'credit_lot_id', '')::uuid, ordinal::integer, (line->>'side')::integer,
                                   (line->>'currency')::integer, (line->>'amount_units')::bigint, NULLIF(line->>'provenance', '')::integer
                            FROM jsonb_array_elements(all_lines) WITH ORDINALITY AS item(line, ordinal);
                            INSERT INTO public.economy_entry_allocations ("Id", "JournalLineId", "ParentLotId", "AmountUnits")
                            SELECT (allocation->>'id')::uuid, (allocation->>'journal_line_id')::uuid,
                                   (allocation->>'parent_lot_id')::uuid, (allocation->>'amount_units')::bigint
                            FROM jsonb_array_elements(all_allocations) allocation;
                            INSERT INTO public.economy_fragment_root_ranges (
                                "Id", "RootSourceStampId", "CreditLotId", "EntryAllocationId", "StartInclusive", "EndExclusive", "ReversalEpoch")
                            SELECT (range_value->>'id')::uuid, (range_value->>'root_source_stamp_id')::uuid, NULL,
                                   (range_value->>'entry_allocation_id')::uuid, (range_value->>'start_inclusive')::bigint,
                                   (range_value->>'end_exclusive')::bigint, (range_value->>'reversal_epoch')::bigint
                            FROM jsonb_array_elements(all_ranges) range_value;
                            INSERT INTO public.economy_risk_decision_consumptions ("Id", "RiskDecisionId", "PostingGroupId", "OperationFingerprint", "ConsumedAt")
                            VALUES (gen_random_uuid(), p_risk_decision_id, p_operation_id, btrim(p_risk_operation_fingerprint), p_occurred_at);
                            INSERT INTO public.economy_idempotency_records ("Id", "Key", "RequestHash", "PostingGroupId", "CreatedAt")
                            VALUES (gen_random_uuid(), btrim(p_idempotency_key), request_hash, p_operation_id, p_occurred_at);
                            UPDATE public.economy_chain_head SET "Sequence" = chain_head."Sequence" + 1, "Hash" = journal_hash, "UpdatedAt" = p_occurred_at WHERE "Id" = 1;
                            outbox_payload := json_build_object('PostingId', p_operation_id, 'Hash', journal_hash, 'RecordedAt', p_occurred_at)::text;
                            INSERT INTO public.economy_outbox_messages ("Id", "PostingGroupId", "Type", "Payload", "PayloadHash", "OccurredAt")
                            VALUES (gen_random_uuid(), p_operation_id, 'economy.provider-reversal.accepted.v2', outbox_payload,
                                encode(public.digest(convert_to(outbox_payload, 'UTF8'), 'sha256'), 'hex'), p_occurred_at);
                            INSERT INTO public.economy_provider_reversal_operations (
                                "Id", "IdempotencyKey", "RequestHash", "RootSourceStampId", "CumulativeHardUnits", "ReversalEpoch", "IrrecoverableDisposition",
                                "RecoveredHardUnits", "RecoveredConvertedSoftUnits", "ResponsibleDebtHardUnits", "PlatformLossHardUnits", "OccurredAt")
                            VALUES (p_operation_id, btrim(p_idempotency_key), request_hash, p_root_source_stamp_id, p_cumulative_hard_units,
                                reversal_state."Epoch", p_irrecoverable_disposition, recovered_hard, recovered_soft, debt_hard, loss_hard, p_occurred_at);
                            INSERT INTO public.economy_provider_reversal_fragments (
                                "Id", "OperationId", "PostingGroupId", "ParentLotId", "WalletId", "Currency", "AmountUnits", "StartInclusive", "EndExclusive")
                            SELECT (fragment->>'id')::uuid, p_operation_id, p_operation_id, (fragment->>'parent_lot_id')::uuid,
                                   (fragment->>'wallet_id')::uuid, (fragment->>'currency')::integer, (fragment->>'amount_units')::bigint,
                                   (fragment->>'start_inclusive')::bigint, (fragment->>'end_exclusive')::bigint
                            FROM jsonb_array_elements(all_fragments) fragment;
                            UPDATE public.economy_root_reversal_states
                            SET "CumulativeProviderUnits" = p_cumulative_hard_units, "ReversedUnits" = p_cumulative_hard_units,
                                "State" = CASE WHEN is_full THEN 'reversed' ELSE 'active' END,
                                "TargetedRanges" = "TargetedRanges" || targeted_ranges, "UpdatedAt" = p_occurred_at
                            WHERE "RootSourceStampId" = p_root_source_stamp_id;
                            UPDATE public.economy_funding_claims
                            SET "State" = CASE WHEN is_full THEN 6 ELSE 5 END, "CumulativeProviderReversalUnits" = p_cumulative_hard_units,
                                "StateChangedAt" = p_occurred_at, "Version" = "Version" + 1
                            WHERE "SourceStampId" = p_root_source_stamp_id;
                            SELECT COALESCE(max("Sequence"), 0) + 1 INTO source_event_sequence
                            FROM public.economy_source_stamp_events WHERE "SourceStampId" = p_root_source_stamp_id;
                            INSERT INTO public.economy_source_stamp_events ("Id", "SourceStampId", "Sequence", "State", "EvidenceHash", "OccurredAt")
                            VALUES (gen_random_uuid(), p_root_source_stamp_id, source_event_sequence, CASE WHEN is_full THEN 6 ELSE 5 END,
                                btrim(p_evidence_hash), p_occurred_at);
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

            ALTER FUNCTION economy_private.post_provider_reversal_v2(uuid,uuid,uuid,uuid,text,uuid,bigint,integer,text,bigint,bigint,uuid,text,bigint,timestamptz,text)
                OWNER TO gameguild_economy_procedure_owner;
            REVOKE ALL ON FUNCTION economy_private.post_provider_reversal_v2(uuid,uuid,uuid,uuid,text,uuid,bigint,integer,text,bigint,bigint,uuid,text,bigint,timestamptz,text) FROM PUBLIC;
            GRANT EXECUTE ON FUNCTION economy_private.post_provider_reversal_v2(uuid,uuid,uuid,uuid,text,uuid,bigint,integer,text,bigint,bigint,uuid,text,bigint,timestamptz,text)
                TO gameguild_economy_writer;
            """);
    }
}
