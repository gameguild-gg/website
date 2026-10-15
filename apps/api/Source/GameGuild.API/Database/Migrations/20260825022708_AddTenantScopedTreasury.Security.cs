using Microsoft.EntityFrameworkCore.Migrations;

namespace GameGuild.API.Database.Migrations;

public partial class AddTenantScopedTreasury : Migration
{
    private static void InstallRiskCounterReservationTransitions(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS deny_immutable_mutation ON public.economy_risk_counter_reservations;

            CREATE OR REPLACE FUNCTION economy_private.guard_risk_counter_reservation_transition_v1()
            RETURNS trigger
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
            BEGIN
                IF TG_OP = 'DELETE' THEN
                    RAISE EXCEPTION 'risk counter reservations are append-only' USING ERRCODE = '42501';
                END IF;
                IF ROW(NEW."Id", NEW."ReservationGroupId", NEW."RiskDecisionId", NEW."RiskCounterId",
                       NEW."InputFingerprint", NEW."AmountUnits", NEW."ReservedAt", NEW."ExpiresAt")
                   IS DISTINCT FROM
                   ROW(OLD."Id", OLD."ReservationGroupId", OLD."RiskDecisionId", OLD."RiskCounterId",
                       OLD."InputFingerprint", OLD."AmountUnits", OLD."ReservedAt", OLD."ExpiresAt") THEN
                    RAISE EXCEPTION 'risk counter reservation immutable fields cannot change'
                        USING ERRCODE = '42501';
                END IF;
                IF OLD."Status" <> 1 OR NEW."Status" NOT IN (2, 3, 4)
                   OR (NEW."Status" = 2 AND
                       (NEW."ConsumedAt" IS NULL OR NEW."ReleasedAt" IS NOT NULL OR
                        NEW."ConsumedAt" < OLD."ReservedAt" OR NEW."ConsumedAt" >= OLD."ExpiresAt"))
                   OR (NEW."Status" = 3 AND
                       (NEW."ReleasedAt" IS NULL OR NEW."ConsumedAt" IS NOT NULL OR
                        NEW."ReleasedAt" < OLD."ReservedAt" OR NEW."ReleasedAt" >= OLD."ExpiresAt"))
                   OR (NEW."Status" = 4 AND
                       (NEW."ReleasedAt" IS NULL OR NEW."ConsumedAt" IS NOT NULL OR
                        NEW."ReleasedAt" < OLD."ExpiresAt")) THEN
                    RAISE EXCEPTION 'risk counter reservation transition is invalid'
                        USING ERRCODE = '42501';
                END IF;
                RETURN NEW;
            END
            $function$;

            ALTER FUNCTION economy_private.guard_risk_counter_reservation_transition_v1()
                OWNER TO gameguild_economy_procedure_owner;
            REVOKE ALL ON FUNCTION economy_private.guard_risk_counter_reservation_transition_v1() FROM PUBLIC;
            CREATE TRIGGER guard_risk_counter_reservation_transition
                BEFORE UPDATE OR DELETE ON public.economy_risk_counter_reservations
                FOR EACH ROW EXECUTE FUNCTION economy_private.guard_risk_counter_reservation_transition_v1();

            CREATE OR REPLACE FUNCTION economy_private.transition_risk_counter_reservation_v1(
                p_reservation_group_id uuid,
                p_consume boolean,
                p_occurred_at timestamptz)
            RETURNS integer
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
            DECLARE
                target_status integer;
                reservation_count integer;
            BEGIN
                IF p_reservation_group_id IS NULL OR p_occurred_at IS NULL THEN
                    RAISE EXCEPTION 'risk counter reservation transition inputs are required'
                        USING ERRCODE = '22023';
                END IF;
                PERFORM 1 FROM public.economy_risk_counter_reservations
                WHERE "ReservationGroupId" = p_reservation_group_id FOR UPDATE;
                GET DIAGNOSTICS reservation_count = ROW_COUNT;
                IF reservation_count = 0 THEN
                    RAISE EXCEPTION 'risk counter reservation was not found' USING ERRCODE = 'P0002';
                END IF;
                IF EXISTS (
                    SELECT 1 FROM public.economy_risk_counter_reservations
                    WHERE "ReservationGroupId" = p_reservation_group_id AND "Status" <> 1) THEN
                    RAISE EXCEPTION 'risk counter reservation is no longer pending'
                        USING ERRCODE = '40001';
                END IF;
                IF EXISTS (
                    SELECT 1 FROM public.economy_risk_counter_reservations
                    WHERE "ReservationGroupId" = p_reservation_group_id
                      AND p_occurred_at < "ReservedAt") THEN
                    RAISE EXCEPTION 'risk counter reservation transition predates reservation'
                        USING ERRCODE = '22023';
                END IF;

                IF EXISTS (
                    SELECT 1 FROM public.economy_risk_counter_reservations
                    WHERE "ReservationGroupId" = p_reservation_group_id
                      AND p_occurred_at >= "ExpiresAt") THEN
                    target_status := 4;
                ELSIF p_consume THEN
                    target_status := 2;
                ELSE
                    target_status := 3;
                END IF;

                IF target_status IN (3, 4) THEN
                    UPDATE public.economy_risk_counters AS counter
                    SET "UsedUnits" = counter."UsedUnits" - released."AmountUnits",
                        "UpdatedAt" = p_occurred_at
                    FROM (
                        SELECT "RiskCounterId", sum("AmountUnits")::bigint AS "AmountUnits"
                        FROM public.economy_risk_counter_reservations
                        WHERE "ReservationGroupId" = p_reservation_group_id
                        GROUP BY "RiskCounterId") AS released
                    WHERE counter."Id" = released."RiskCounterId"
                      AND counter."UsedUnits" >= released."AmountUnits";
                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'risk counter capacity cannot be released safely'
                            USING ERRCODE = '23514';
                    END IF;
                END IF;

                UPDATE public.economy_risk_counter_reservations
                SET "Status" = target_status,
                    "ConsumedAt" = CASE WHEN target_status = 2 THEN p_occurred_at ELSE NULL END,
                    "ReleasedAt" = CASE WHEN target_status IN (3, 4) THEN p_occurred_at ELSE NULL END
                WHERE "ReservationGroupId" = p_reservation_group_id;
                RETURN target_status;
            END
            $function$;

            ALTER FUNCTION economy_private.transition_risk_counter_reservation_v1(uuid,boolean,timestamptz)
                OWNER TO gameguild_economy_procedure_owner;
            REVOKE ALL ON FUNCTION economy_private.transition_risk_counter_reservation_v1(uuid,boolean,timestamptz)
                FROM PUBLIC;
            GRANT EXECUTE ON FUNCTION economy_private.transition_risk_counter_reservation_v1(uuid,boolean,timestamptz)
                TO gameguild_economy_writer;
            REVOKE UPDATE, DELETE ON public.economy_risk_counter_reservations
                FROM gameguild_economy_runtime, gameguild_economy_writer;
            GRANT UPDATE ON public.economy_risk_counter_reservations
                TO gameguild_economy_procedure_owner;
            """);
    }

    private static void RemoveRiskCounterReservationTransitions(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP FUNCTION IF EXISTS economy_private.transition_risk_counter_reservation_v1(uuid,boolean,timestamptz);
            DROP TRIGGER IF EXISTS guard_risk_counter_reservation_transition
                ON public.economy_risk_counter_reservations;
            DROP FUNCTION IF EXISTS economy_private.guard_risk_counter_reservation_transition_v1();
            DROP TRIGGER IF EXISTS deny_immutable_mutation ON public.economy_risk_counter_reservations;
            CREATE TRIGGER deny_immutable_mutation
                BEFORE UPDATE OR DELETE ON public.economy_risk_counter_reservations
                FOR EACH ROW EXECUTE FUNCTION economy_private.deny_immutable_mutation_v1();
            """);
    }

    private static void InstallRegisteredEconomyCapabilities(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            INSERT INTO public.economy_registered_capabilities (
                "Id", "Name", "AllowedTemplateKinds", "IsEnabled", "CreatedAt", "RevokedAt")
            VALUES
                ('e1000000-0000-0000-0000-000000000001', 'ad-reward-issuance', '[21]'::jsonb, true, TIMESTAMPTZ '2026-08-25T00:00:00Z', NULL),
                ('e1000000-0000-0000-0000-000000000002', 'bounty-escrow', '[22]'::jsonb, true, TIMESTAMPTZ '2026-08-25T00:00:00Z', NULL),
                ('e1000000-0000-0000-0000-000000000003', 'bounty-claim', '[23]'::jsonb, true, TIMESTAMPTZ '2026-08-25T00:00:00Z', NULL),
                ('e1000000-0000-0000-0000-000000000004', 'bounty-reclaim', '[24]'::jsonb, true, TIMESTAMPTZ '2026-08-25T00:00:00Z', NULL),
                ('e1000000-0000-0000-0000-000000000005', 'marketplace-settlement', '[25]'::jsonb, true, TIMESTAMPTZ '2026-08-25T00:00:00Z', NULL),
                ('e1000000-0000-0000-0000-000000000006', 'marketplace-refund', '[26]'::jsonb, true, TIMESTAMPTZ '2026-08-25T00:00:00Z', NULL),
                ('e1000000-0000-0000-0000-000000000007', 'admin-withdrawal-reservation', '[14]'::jsonb, true, TIMESTAMPTZ '2026-08-25T00:00:00Z', NULL),
                ('e1000000-0000-0000-0000-000000000008', 'admin-withdrawal-provider-terminal', '[15,16]'::jsonb, true, TIMESTAMPTZ '2026-08-25T00:00:00Z', NULL)
            ON CONFLICT ("Name") DO NOTHING;
            """);
    }

    private static void RemoveRegisteredEconomyCapabilities(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE FROM public.economy_registered_capabilities
            WHERE "Id" IN (
                'e1000000-0000-0000-0000-000000000001'::uuid,
                'e1000000-0000-0000-0000-000000000002'::uuid,
                'e1000000-0000-0000-0000-000000000003'::uuid,
                'e1000000-0000-0000-0000-000000000004'::uuid,
                'e1000000-0000-0000-0000-000000000005'::uuid,
                'e1000000-0000-0000-0000-000000000006'::uuid,
                'e1000000-0000-0000-0000-000000000007'::uuid,
                'e1000000-0000-0000-0000-000000000008'::uuid);
            """);
    }

    private static void BackfillAdminWithdrawalTenantScope(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE public.economy_admin_withdrawal_runs AS run
            SET "TenantId" = wallet."TenantId"
            FROM public.economy_wallets AS wallet
            WHERE wallet."Id" = run."PlatformFeeWalletId"
              AND run."TenantId" = '00000000-0000-0000-0000-000000000000'::uuid;

            UPDATE public.economy_admin_withdrawal_provider_events AS event
            SET "TenantId" = run."TenantId"
            FROM public.economy_admin_withdrawal_runs AS run
            WHERE run."Id" = event."RunId";

            UPDATE public.economy_admin_withdrawal_dispatch_outbox AS item
            SET "TenantId" = run."TenantId"
            FROM public.economy_admin_withdrawal_runs AS run
            WHERE run."Id" = item."RunId";

            UPDATE public.economy_admin_withdrawal_audit_events AS event
            SET "TenantId" = run."TenantId"
            FROM public.economy_admin_withdrawal_runs AS run
            WHERE run."Id" = event."RunId";

            DO $block$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM public.economy_admin_withdrawal_runs
                    WHERE "TenantId" = '00000000-0000-0000-0000-000000000000'::uuid)
                   OR EXISTS (
                    SELECT 1 FROM public.economy_admin_withdrawal_provider_events
                    WHERE "TenantId" = '00000000-0000-0000-0000-000000000000'::uuid)
                   OR EXISTS (
                    SELECT 1 FROM public.economy_admin_withdrawal_dispatch_outbox
                    WHERE "TenantId" = '00000000-0000-0000-0000-000000000000'::uuid)
                   OR EXISTS (
                    SELECT 1 FROM public.economy_admin_withdrawal_audit_events
                    WHERE "TenantId" = '00000000-0000-0000-0000-000000000000'::uuid) THEN
                    RAISE EXCEPTION 'admin withdrawal tenant backfill is incomplete'
                        USING ERRCODE = '23514';
                END IF;
            END
            $block$;

            ALTER TABLE public.economy_admin_withdrawal_runs ALTER COLUMN "TenantId" DROP DEFAULT;
            ALTER TABLE public.economy_admin_withdrawal_provider_events ALTER COLUMN "TenantId" DROP DEFAULT;
            ALTER TABLE public.economy_admin_withdrawal_dispatch_outbox ALTER COLUMN "TenantId" DROP DEFAULT;
            ALTER TABLE public.economy_admin_withdrawal_audit_events ALTER COLUMN "TenantId" DROP DEFAULT;
            """);
    }

    private static void InstallTenantScopedAdminWithdrawalSecurity(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE SEQUENCE IF NOT EXISTS economy_private.admin_withdrawal_fencing_sequence
                AS bigint START WITH 1 INCREMENT BY 1 MINVALUE 1 NO CYCLE;
            ALTER SEQUENCE economy_private.admin_withdrawal_fencing_sequence
                OWNER TO gameguild_economy_procedure_owner;
            REVOKE ALL ON SEQUENCE economy_private.admin_withdrawal_fencing_sequence FROM PUBLIC;

            CREATE OR REPLACE FUNCTION economy_private.next_admin_withdrawal_fencing_token_v1()
            RETURNS bigint
            LANGUAGE sql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
                SELECT pg_catalog.nextval('economy_private.admin_withdrawal_fencing_sequence');
            $function$;

            CREATE OR REPLACE FUNCTION economy_private.create_admin_withdrawal_run_v2(
                p_id uuid, p_tenant_id uuid, p_idempotency_key text, p_request_hash text, p_period_start date,
                p_requested_by uuid, p_platform_fee_wallet_id uuid, p_amount_units bigint,
                p_source_asset_key text, p_destination_hash text, p_state integer,
                p_version bigint, p_fencing_token bigint, p_execution_epoch bigint,
                p_reserve_version bigint, p_reserve_authorization_epoch bigint,
                p_policy_version bigint, p_dispatch_snapshot_hash text,
                p_provider_transfer_id text, p_created_at timestamptz, p_updated_at timestamptz)
            RETURNS void
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
            BEGIN
                IF p_id IS NULL OR p_tenant_id IS NULL
                   OR p_tenant_id = '00000000-0000-0000-0000-000000000000'::uuid
                   OR p_requested_by IS NULL OR p_platform_fee_wallet_id IS NULL
                   OR p_period_start IS NULL OR p_amount_units <= 0
                   OR p_version <= 0 OR p_fencing_token <= 0 OR p_execution_epoch <= 0
                   OR p_reserve_version <= 0 OR p_reserve_authorization_epoch <= 0
                   OR p_policy_version <= 0 OR p_created_at IS NULL OR p_updated_at < p_created_at
                   OR p_state <> 1 OR p_dispatch_snapshot_hash IS NOT NULL OR p_provider_transfer_id IS NOT NULL
                   OR pg_catalog.length(pg_catalog.btrim(p_idempotency_key)) = 0
                   OR pg_catalog.length(pg_catalog.btrim(p_request_hash)) = 0
                   OR pg_catalog.length(pg_catalog.btrim(p_source_asset_key)) = 0
                   OR pg_catalog.length(pg_catalog.btrim(p_destination_hash)) = 0 THEN
                    RAISE EXCEPTION 'admin withdrawal creation violates immutable request rules'
                        USING ERRCODE = '23514';
                END IF;
                IF NOT EXISTS (
                    SELECT 1 FROM public.economy_wallets
                    WHERE "Id" = p_platform_fee_wallet_id AND "TenantId" = p_tenant_id) THEN
                    RAISE EXCEPTION 'admin withdrawal wallet is outside the tenant'
                        USING ERRCODE = '23514';
                END IF;

                INSERT INTO public.economy_admin_withdrawal_runs (
                    "Id", "TenantId", "IdempotencyKey", "RequestHash", "PeriodStart", "RequestedBy", "ApprovedBy",
                    "PlatformFeeWalletId", "AmountUnits", "SourceAssetKey", "DestinationHash", "State",
                    "Version", "FencingToken", "ExecutionEpoch", "ReserveVersion",
                    "ReserveAuthorizationEpoch", "PolicyVersion", "DispatchSnapshotHash",
                    "ProviderTransferId", "CreatedAt", "UpdatedAt")
                VALUES (
                    p_id, p_tenant_id, pg_catalog.btrim(p_idempotency_key), pg_catalog.btrim(p_request_hash), p_period_start,
                    p_requested_by, NULL, p_platform_fee_wallet_id, p_amount_units,
                    pg_catalog.btrim(p_source_asset_key), pg_catalog.btrim(p_destination_hash), p_state,
                    p_version, p_fencing_token, p_execution_epoch, p_reserve_version,
                    p_reserve_authorization_epoch, p_policy_version, NULL, NULL, p_created_at, p_updated_at);
            EXCEPTION WHEN unique_violation THEN
                RAISE EXCEPTION 'admin withdrawal run overlaps an existing request or period'
                    USING ERRCODE = '23505';
            END
            $function$;

            CREATE OR REPLACE FUNCTION economy_private.transition_admin_withdrawal_run_v2(
                p_tenant_id uuid, p_id uuid, p_expected_version bigint, p_state integer, p_approved_by uuid,
                p_dispatch_snapshot_hash text, p_provider_transfer_id text, p_updated_at timestamptz)
            RETURNS void
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
            DECLARE
                current public.economy_admin_withdrawal_runs%ROWTYPE;
                transition_allowed boolean := false;
            BEGIN
                IF p_tenant_id IS NULL OR p_tenant_id = '00000000-0000-0000-0000-000000000000'::uuid THEN
                    RAISE EXCEPTION 'admin withdrawal tenant is required' USING ERRCODE = '23514';
                END IF;
                SELECT * INTO current FROM public.economy_admin_withdrawal_runs
                WHERE "TenantId" = p_tenant_id AND "Id" = p_id FOR UPDATE;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'admin withdrawal run was not found' USING ERRCODE = 'P0002';
                END IF;
                IF current."Version" <> p_expected_version OR p_updated_at < current."UpdatedAt" THEN
                    RAISE EXCEPTION 'admin withdrawal run version is stale' USING ERRCODE = '40001';
                END IF;

                transition_allowed :=
                    (current."State" = 1 AND p_state = 2)
                    OR (current."State" = 2 AND p_state = 3)
                    OR (current."State" = 3 AND p_state IN (3, 4))
                    OR (current."State" = 4 AND p_state IN (4, 5, 6, 7));
                IF NOT transition_allowed THEN
                    RAISE EXCEPTION 'admin withdrawal state transition is invalid' USING ERRCODE = '23514';
                END IF;
                IF p_state >= 2 AND p_approved_by IS NULL THEN
                    RAISE EXCEPTION 'admin withdrawal approval is required' USING ERRCODE = '23514';
                END IF;
                IF p_state >= 3
                   AND pg_catalog.length(pg_catalog.btrim(COALESCE(p_dispatch_snapshot_hash, ''))) = 0 THEN
                    RAISE EXCEPTION 'admin withdrawal dispatch evidence is required' USING ERRCODE = '23514';
                END IF;
                IF p_state = 3 AND current."DispatchSnapshotHash" IS NOT NULL
                   AND p_dispatch_snapshot_hash IS DISTINCT FROM current."DispatchSnapshotHash" THEN
                    RAISE EXCEPTION 'admin withdrawal dispatch snapshot is immutable' USING ERRCODE = '23514';
                END IF;
                IF p_provider_transfer_id IS NOT NULL AND current."ProviderTransferId" IS NOT NULL
                   AND p_provider_transfer_id IS DISTINCT FROM current."ProviderTransferId" THEN
                    RAISE EXCEPTION 'admin withdrawal provider evidence is immutable' USING ERRCODE = '23514';
                END IF;

                UPDATE public.economy_admin_withdrawal_runs
                SET "State" = p_state,
                    "ApprovedBy" = COALESCE(p_approved_by, current."ApprovedBy"),
                    "DispatchSnapshotHash" = COALESCE(p_dispatch_snapshot_hash, current."DispatchSnapshotHash"),
                    "ProviderTransferId" = COALESCE(p_provider_transfer_id, current."ProviderTransferId"),
                    "Version" = current."Version" + 1,
                    "UpdatedAt" = p_updated_at
                WHERE "TenantId" = p_tenant_id AND "Id" = p_id;
            END
            $function$;

            CREATE OR REPLACE FUNCTION economy_private.complete_admin_withdrawal_provider_event_v2(
                p_tenant_id uuid, p_event_id text, p_event_hash text, p_run_id uuid, p_expected_version bigint,
                p_state integer, p_provider_transfer_id text, p_updated_at timestamptz)
            RETURNS void
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
            DECLARE
                current public.economy_admin_withdrawal_runs%ROWTYPE;
            BEGIN
                IF p_tenant_id IS NULL OR p_tenant_id = '00000000-0000-0000-0000-000000000000'::uuid
                   OR pg_catalog.length(pg_catalog.btrim(p_event_id)) = 0
                   OR pg_catalog.length(pg_catalog.btrim(p_event_hash)) = 0
                   OR pg_catalog.length(pg_catalog.btrim(COALESCE(p_provider_transfer_id, ''))) = 0
                   OR p_state NOT IN (5, 6) THEN
                    RAISE EXCEPTION 'admin withdrawal provider evidence is invalid' USING ERRCODE = '23514';
                END IF;

                SELECT * INTO current FROM public.economy_admin_withdrawal_runs
                WHERE "TenantId" = p_tenant_id AND "Id" = p_run_id FOR UPDATE;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'admin withdrawal run was not found' USING ERRCODE = 'P0002';
                END IF;
                IF current."Version" <> p_expected_version OR p_updated_at < current."UpdatedAt"
                   OR current."State" NOT IN (3, 4) THEN
                    RAISE EXCEPTION 'admin withdrawal provider event is stale' USING ERRCODE = '40001';
                END IF;
                IF current."ProviderTransferId" IS NOT NULL
                   AND current."ProviderTransferId" IS DISTINCT FROM p_provider_transfer_id THEN
                    RAISE EXCEPTION 'admin withdrawal provider evidence is immutable' USING ERRCODE = '23514';
                END IF;

                INSERT INTO public.economy_admin_withdrawal_provider_events (
                    "TenantId", "EventId", "EventHash", "RunId", "RecordedAt")
                VALUES (
                    p_tenant_id, pg_catalog.btrim(p_event_id), pg_catalog.btrim(p_event_hash), p_run_id, p_updated_at);

                UPDATE public.economy_admin_withdrawal_runs
                SET "State" = p_state,
                    "ProviderTransferId" = p_provider_transfer_id,
                    "Version" = current."Version" + 1,
                    "UpdatedAt" = p_updated_at
                WHERE "TenantId" = p_tenant_id AND "Id" = p_run_id;
            EXCEPTION WHEN unique_violation THEN
                RAISE EXCEPTION 'admin withdrawal provider evidence was already recorded'
                    USING ERRCODE = '23505';
            END
            $function$;

            CREATE OR REPLACE FUNCTION economy_private.append_admin_withdrawal_audit_event_v2(
                p_tenant_id uuid, p_run_id uuid, p_kind text, p_actor_id uuid,
                p_evidence text, p_occurred_at timestamptz)
            RETURNS TABLE(
                "RunId" uuid, "Sequence" bigint, "Kind" text, "ActorId" uuid, "Evidence" text,
                "OccurredAt" timestamptz, "PreviousHash" text, "Hash" text)
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
            DECLARE
                previous_hash text;
                next_sequence bigint;
                next_hash text;
            BEGIN
                IF p_tenant_id IS NULL OR p_tenant_id = '00000000-0000-0000-0000-000000000000'::uuid
                   OR pg_catalog.length(pg_catalog.btrim(p_kind)) = 0
                   OR pg_catalog.length(pg_catalog.btrim(p_evidence)) = 0 THEN
                    RAISE EXCEPTION 'admin withdrawal audit evidence is required' USING ERRCODE = '23514';
                END IF;
                PERFORM 1 FROM public.economy_admin_withdrawal_runs
                WHERE "TenantId" = p_tenant_id AND "Id" = p_run_id FOR UPDATE;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'admin withdrawal run was not found' USING ERRCODE = 'P0002';
                END IF;
                SELECT COALESCE(MAX(audit."Sequence"), 0) + 1,
                       COALESCE((array_agg(audit."Hash" ORDER BY audit."Sequence" DESC))[1], repeat('0', 64))
                INTO next_sequence, previous_hash
                FROM public.economy_admin_withdrawal_audit_events AS audit
                WHERE audit."TenantId" = p_tenant_id AND audit."RunId" = p_run_id;
                next_hash := encode(public.digest(convert_to(
                    replace(p_run_id::text, '-', '') || '|' || next_sequence::text || '|' ||
                    pg_catalog.btrim(p_kind) || '|' ||
                    COALESCE(replace(p_actor_id::text, '-', ''), '') || '|' ||
                    pg_catalog.btrim(p_evidence) || '|' ||
                    to_char(p_occurred_at AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.US"Z"') || '|' ||
                    previous_hash, 'UTF8'), 'sha256'), 'hex');

                INSERT INTO public.economy_admin_withdrawal_audit_events (
                    "TenantId", "RunId", "Sequence", "Kind", "ActorId", "Evidence", "OccurredAt", "PreviousHash", "Hash")
                VALUES (
                    p_tenant_id, p_run_id, next_sequence, pg_catalog.btrim(p_kind), p_actor_id,
                    pg_catalog.btrim(p_evidence), p_occurred_at, previous_hash, next_hash);

                RETURN QUERY SELECT p_run_id, next_sequence, pg_catalog.btrim(p_kind), p_actor_id,
                    pg_catalog.btrim(p_evidence), p_occurred_at, previous_hash, next_hash;
            END
            $function$;
            """);

        migrationBuilder.Sql(
            """
            ALTER FUNCTION economy_private.create_admin_withdrawal_run_v2(
                uuid,uuid,text,text,date,uuid,uuid,bigint,text,text,integer,bigint,bigint,bigint,bigint,bigint,bigint,text,text,timestamptz,timestamptz)
                OWNER TO gameguild_economy_procedure_owner;
            ALTER FUNCTION economy_private.next_admin_withdrawal_fencing_token_v1()
                OWNER TO gameguild_economy_procedure_owner;
            ALTER FUNCTION economy_private.transition_admin_withdrawal_run_v2(
                uuid,uuid,bigint,integer,uuid,text,text,timestamptz)
                OWNER TO gameguild_economy_procedure_owner;
            ALTER FUNCTION economy_private.complete_admin_withdrawal_provider_event_v2(
                uuid,text,text,uuid,bigint,integer,text,timestamptz)
                OWNER TO gameguild_economy_procedure_owner;
            ALTER FUNCTION economy_private.append_admin_withdrawal_audit_event_v2(
                uuid,uuid,text,uuid,text,timestamptz)
                OWNER TO gameguild_economy_procedure_owner;

            REVOKE ALL ON FUNCTION economy_private.create_admin_withdrawal_run_v2(
                uuid,uuid,text,text,date,uuid,uuid,bigint,text,text,integer,bigint,bigint,bigint,bigint,bigint,bigint,text,text,timestamptz,timestamptz) FROM PUBLIC;
            REVOKE ALL ON FUNCTION economy_private.transition_admin_withdrawal_run_v2(
                uuid,uuid,bigint,integer,uuid,text,text,timestamptz) FROM PUBLIC;
            REVOKE ALL ON FUNCTION economy_private.complete_admin_withdrawal_provider_event_v2(
                uuid,text,text,uuid,bigint,integer,text,timestamptz) FROM PUBLIC;
            REVOKE ALL ON FUNCTION economy_private.append_admin_withdrawal_audit_event_v2(
                uuid,uuid,text,uuid,text,timestamptz) FROM PUBLIC;
            REVOKE ALL ON FUNCTION economy_private.next_admin_withdrawal_fencing_token_v1() FROM PUBLIC;

            REVOKE EXECUTE ON FUNCTION economy_private.create_admin_withdrawal_run_v1(
                uuid,text,text,date,uuid,uuid,bigint,text,text,integer,bigint,bigint,bigint,bigint,bigint,bigint,text,text,timestamptz,timestamptz),
                economy_private.transition_admin_withdrawal_run_v1(uuid,bigint,integer,uuid,text,text,timestamptz),
                economy_private.complete_admin_withdrawal_provider_event_v1(text,text,uuid,bigint,integer,text,timestamptz),
                economy_private.append_admin_withdrawal_audit_event_v1(uuid,text,uuid,text,timestamptz)
                FROM gameguild_economy_writer;

            GRANT EXECUTE ON FUNCTION economy_private.create_admin_withdrawal_run_v2(
                uuid,uuid,text,text,date,uuid,uuid,bigint,text,text,integer,bigint,bigint,bigint,bigint,bigint,bigint,text,text,timestamptz,timestamptz),
                economy_private.transition_admin_withdrawal_run_v2(uuid,uuid,bigint,integer,uuid,text,text,timestamptz),
                economy_private.complete_admin_withdrawal_provider_event_v2(uuid,text,text,uuid,bigint,integer,text,timestamptz),
                economy_private.append_admin_withdrawal_audit_event_v2(uuid,uuid,text,uuid,text,timestamptz)
                TO gameguild_economy_writer;
            GRANT EXECUTE ON FUNCTION economy_private.next_admin_withdrawal_fencing_token_v1()
                TO gameguild_economy_writer;
            """);
    }

    private static void RemoveTenantScopedAdminWithdrawalSecurity(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP FUNCTION IF EXISTS economy_private.create_admin_withdrawal_run_v2(
                uuid,uuid,text,text,date,uuid,uuid,bigint,text,text,integer,bigint,bigint,bigint,bigint,bigint,bigint,text,text,timestamptz,timestamptz);
            DROP FUNCTION IF EXISTS economy_private.transition_admin_withdrawal_run_v2(
                uuid,uuid,bigint,integer,uuid,text,text,timestamptz);
            DROP FUNCTION IF EXISTS economy_private.complete_admin_withdrawal_provider_event_v2(
                uuid,text,text,uuid,bigint,integer,text,timestamptz);
            DROP FUNCTION IF EXISTS economy_private.append_admin_withdrawal_audit_event_v2(
                uuid,uuid,text,uuid,text,timestamptz);
            DROP FUNCTION IF EXISTS economy_private.next_admin_withdrawal_fencing_token_v1();
            DROP SEQUENCE IF EXISTS economy_private.admin_withdrawal_fencing_sequence;

            GRANT EXECUTE ON FUNCTION economy_private.create_admin_withdrawal_run_v1(
                uuid,text,text,date,uuid,uuid,bigint,text,text,integer,bigint,bigint,bigint,bigint,bigint,bigint,text,text,timestamptz,timestamptz),
                economy_private.transition_admin_withdrawal_run_v1(uuid,bigint,integer,uuid,text,text,timestamptz),
                economy_private.complete_admin_withdrawal_provider_event_v1(text,text,uuid,bigint,integer,text,timestamptz),
                economy_private.append_admin_withdrawal_audit_event_v1(uuid,text,uuid,text,timestamptz)
                TO gameguild_economy_writer;
            """);
    }
}
