using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations;

public partial class AddEconomyPayoutOperationPersistence : Migration
{
    private static void InstallPayoutOperationSecurity(MigrationBuilder migrationBuilder)
    {
        CreatePayoutOperationFunctions(migrationBuilder);
        CreatePayoutProviderEventFunction(migrationBuilder);
        CreatePayoutReaderFunctions(migrationBuilder);
        RestrictPayoutOperationAccess(migrationBuilder);
    }

    private static void CreatePayoutOperationFunctions(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION economy_private.create_payout_operation_v1(
                p_id uuid, p_idempotency_key text, p_request_hash text, p_actor_id uuid, p_payee_id uuid,
                p_wallet_id uuid, p_amount_units bigint, p_provider_account_id text, p_destination_hash text,
                p_provider_binding_hash text, p_eligibility_hash text, p_dispatch_snapshot_hash text,
                p_provider_payout_id text, p_state integer, p_version bigint, p_fencing_token bigint,
                p_kill_switch_epoch bigint, p_reserve_version bigint, p_reserve_authorization_epoch bigint,
                p_policy_version bigint, p_risk_decision_id uuid, p_created_at timestamptz, p_updated_at timestamptz)
            RETURNS void
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
            BEGIN
                IF p_id IS NULL OR p_actor_id IS NULL OR p_payee_id IS NULL OR p_wallet_id IS NULL
                   OR p_risk_decision_id IS NULL OR p_amount_units <= 0 OR p_state <> 1 OR p_version <> 1
                   OR p_fencing_token <= 0 OR p_kill_switch_epoch <= 0 OR p_reserve_version <= 0
                   OR p_reserve_authorization_epoch <= 0 OR p_policy_version <= 0
                   OR p_created_at IS NULL OR p_updated_at IS NULL OR p_updated_at < p_created_at
                   OR pg_catalog.length(pg_catalog.btrim(p_idempotency_key)) = 0
                   OR pg_catalog.length(pg_catalog.btrim(p_request_hash)) = 0
                   OR pg_catalog.length(pg_catalog.btrim(p_provider_account_id)) = 0
                   OR pg_catalog.length(pg_catalog.btrim(p_destination_hash)) = 0
                   OR pg_catalog.length(pg_catalog.btrim(p_provider_binding_hash)) = 0
                   OR pg_catalog.length(pg_catalog.btrim(p_eligibility_hash)) = 0
                   OR p_dispatch_snapshot_hash IS NOT NULL OR p_provider_payout_id IS NOT NULL THEN
                    RAISE EXCEPTION 'payout operation is invalid' USING ERRCODE = '23514';
                END IF;

                INSERT INTO public.economy_payout_operations (
                    "Id", "IdempotencyKey", "RequestHash", "ActorId", "PayeeId", "WalletId", "AmountUnits",
                    "ProviderAccountId", "DestinationHash", "ProviderBindingHash", "EligibilityHash",
                    "DispatchSnapshotHash", "ProviderPayoutId", "State", "Version", "FencingToken",
                    "KillSwitchEpoch", "ReserveVersion", "ReserveAuthorizationEpoch", "PolicyVersion",
                    "RiskDecisionId", "CreatedAt", "UpdatedAt")
                VALUES (
                    p_id, pg_catalog.btrim(p_idempotency_key), pg_catalog.btrim(p_request_hash), p_actor_id,
                    p_payee_id, p_wallet_id, p_amount_units, pg_catalog.btrim(p_provider_account_id),
                    pg_catalog.btrim(p_destination_hash), pg_catalog.btrim(p_provider_binding_hash),
                    pg_catalog.btrim(p_eligibility_hash), NULL, NULL, p_state, p_version, p_fencing_token,
                    p_kill_switch_epoch, p_reserve_version, p_reserve_authorization_epoch, p_policy_version,
                    p_risk_decision_id, p_created_at, p_updated_at);
            EXCEPTION WHEN unique_violation THEN
                RAISE EXCEPTION 'payout idempotency key was already used' USING ERRCODE = '23505';
            END
            $function$;

            CREATE OR REPLACE FUNCTION economy_private.transition_payout_operation_v1(
                p_id uuid, p_expected_version bigint, p_state integer, p_dispatch_snapshot_hash text,
                p_provider_payout_id text, p_updated_at timestamptz)
            RETURNS void
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
            DECLARE
                current public.economy_payout_operations%ROWTYPE;
                next_dispatch_snapshot_hash text;
                next_provider_payout_id text;
            BEGIN
                SELECT * INTO current FROM public.economy_payout_operations WHERE "Id" = p_id FOR UPDATE;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'payout operation was not found' USING ERRCODE = 'P0002';
                END IF;
                IF current."Version" <> p_expected_version OR p_updated_at < current."UpdatedAt" THEN
                    RAISE EXCEPTION 'payout operation version is stale' USING ERRCODE = '40001';
                END IF;
                IF NOT (
                    (current."State" = 1 AND p_state IN (2, 6)) OR
                    (current."State" = 2 AND p_state IN (3, 4, 5)) OR
                    (current."State" = 3 AND p_state IN (4, 5))) THEN
                    RAISE EXCEPTION 'payout operation transition is invalid' USING ERRCODE = '23514';
                END IF;

                next_dispatch_snapshot_hash := COALESCE(p_dispatch_snapshot_hash, current."DispatchSnapshotHash");
                next_provider_payout_id := COALESCE(p_provider_payout_id, current."ProviderPayoutId");
                IF p_state BETWEEN 2 AND 6 AND pg_catalog.length(pg_catalog.btrim(COALESCE(next_dispatch_snapshot_hash, ''))) = 0 THEN
                    RAISE EXCEPTION 'payout dispatch snapshot is required' USING ERRCODE = '23514';
                END IF;
                IF current."DispatchSnapshotHash" IS NOT NULL
                   AND current."DispatchSnapshotHash" IS DISTINCT FROM next_dispatch_snapshot_hash THEN
                    RAISE EXCEPTION 'payout dispatch snapshot is immutable' USING ERRCODE = '23514';
                END IF;
                IF current."ProviderPayoutId" IS NOT NULL
                   AND current."ProviderPayoutId" IS DISTINCT FROM next_provider_payout_id THEN
                    RAISE EXCEPTION 'payout provider binding is immutable' USING ERRCODE = '23514';
                END IF;

                UPDATE public.economy_payout_operations
                SET "State" = p_state,
                    "DispatchSnapshotHash" = pg_catalog.btrim(next_dispatch_snapshot_hash),
                    "ProviderPayoutId" = NULLIF(pg_catalog.btrim(COALESCE(next_provider_payout_id, '')), ''),
                    "Version" = current."Version" + 1,
                    "UpdatedAt" = p_updated_at
                WHERE "Id" = p_id;
            END
            $function$;
            """);
    }

    private static void CreatePayoutProviderEventFunction(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION economy_private.complete_payout_provider_event_v1(
                p_event_id text, p_event_hash text, p_operation_id uuid, p_expected_version bigint,
                p_state integer, p_provider_payout_id text, p_recorded_at timestamptz)
            RETURNS void
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
            DECLARE
                current public.economy_payout_operations%ROWTYPE;
            BEGIN
                IF pg_catalog.length(pg_catalog.btrim(p_event_id)) = 0
                   OR pg_catalog.length(pg_catalog.btrim(p_event_hash)) = 0
                   OR pg_catalog.length(pg_catalog.btrim(COALESCE(p_provider_payout_id, ''))) = 0
                   OR p_state NOT IN (4, 5) THEN
                    RAISE EXCEPTION 'payout provider event is invalid' USING ERRCODE = '23514';
                END IF;

                SELECT * INTO current FROM public.economy_payout_operations
                WHERE "Id" = p_operation_id FOR UPDATE;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'payout operation was not found' USING ERRCODE = 'P0002';
                END IF;
                IF current."Version" <> p_expected_version OR p_recorded_at < current."UpdatedAt"
                   OR current."State" NOT IN (2, 3) THEN
                    RAISE EXCEPTION 'payout provider event is stale' USING ERRCODE = '40001';
                END IF;
                IF current."ProviderPayoutId" IS NOT NULL
                   AND current."ProviderPayoutId" IS DISTINCT FROM p_provider_payout_id THEN
                    RAISE EXCEPTION 'payout provider binding is immutable' USING ERRCODE = '23514';
                END IF;

                INSERT INTO public.economy_payout_provider_events (
                    "EventId", "EventHash", "OperationId", "ResultingState", "RecordedAt")
                VALUES (
                    pg_catalog.btrim(p_event_id), pg_catalog.btrim(p_event_hash), p_operation_id, p_state,
                    p_recorded_at);

                UPDATE public.economy_payout_operations
                SET "State" = p_state,
                    "ProviderPayoutId" = pg_catalog.btrim(p_provider_payout_id),
                    "Version" = current."Version" + 1,
                    "UpdatedAt" = p_recorded_at
                WHERE "Id" = p_operation_id;
            EXCEPTION WHEN unique_violation THEN
                RAISE EXCEPTION 'payout provider event was already recorded' USING ERRCODE = '23505';
            END
            $function$;
            """);
    }

    private static void CreatePayoutReaderFunctions(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION economy_private.read_payout_operation_by_id_v1(p_id uuid)
            RETURNS SETOF public.economy_payout_operations
            LANGUAGE sql STABLE SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
                SELECT * FROM public.economy_payout_operations WHERE "Id" = p_id
            $function$;

            CREATE OR REPLACE FUNCTION economy_private.read_payout_operation_by_idempotency_v1(p_idempotency_key text)
            RETURNS SETOF public.economy_payout_operations
            LANGUAGE sql STABLE SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
                SELECT * FROM public.economy_payout_operations
                WHERE "IdempotencyKey" = pg_catalog.btrim(p_idempotency_key)
            $function$;

            CREATE OR REPLACE FUNCTION economy_private.read_payout_provider_event_v1(p_event_id text)
            RETURNS SETOF public.economy_payout_provider_events
            LANGUAGE sql STABLE SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
                SELECT * FROM public.economy_payout_provider_events
                WHERE "EventId" = pg_catalog.btrim(p_event_id)
            $function$;
            """);
    }

    private static void RestrictPayoutOperationAccess(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER FUNCTION economy_private.create_payout_operation_v1(
                uuid,text,text,uuid,uuid,uuid,bigint,text,text,text,text,text,text,integer,bigint,bigint,bigint,bigint,bigint,bigint,uuid,timestamptz,timestamptz)
                OWNER TO gameguild_economy_procedure_owner;
            ALTER FUNCTION economy_private.transition_payout_operation_v1(
                uuid,bigint,integer,text,text,timestamptz)
                OWNER TO gameguild_economy_procedure_owner;
            ALTER FUNCTION economy_private.complete_payout_provider_event_v1(
                text,text,uuid,bigint,integer,text,timestamptz)
                OWNER TO gameguild_economy_procedure_owner;
            ALTER FUNCTION economy_private.read_payout_operation_by_id_v1(uuid)
                OWNER TO gameguild_economy_procedure_owner;
            ALTER FUNCTION economy_private.read_payout_operation_by_idempotency_v1(text)
                OWNER TO gameguild_economy_procedure_owner;
            ALTER FUNCTION economy_private.read_payout_provider_event_v1(text)
                OWNER TO gameguild_economy_procedure_owner;

            REVOKE ALL ON TABLE public.economy_payout_operations,
                public.economy_payout_provider_events FROM PUBLIC;
            REVOKE ALL ON TABLE public.economy_payout_operations,
                public.economy_payout_provider_events FROM gameguild_economy_writer;
            REVOKE ALL ON TABLE public.economy_payout_operations,
                public.economy_payout_provider_events FROM gameguild_economy_runtime;

            GRANT SELECT ON TABLE public.economy_payout_operations,
                public.economy_payout_provider_events TO gameguild_economy_runtime;
            GRANT SELECT, INSERT, UPDATE ON TABLE public.economy_payout_operations,
                public.economy_payout_provider_events TO gameguild_economy_procedure_owner;
            GRANT ALL ON TABLE public.economy_payout_operations,
                public.economy_payout_provider_events TO gameguild_economy_migration;

            REVOKE ALL ON FUNCTION economy_private.create_payout_operation_v1(
                uuid,text,text,uuid,uuid,uuid,bigint,text,text,text,text,text,text,integer,bigint,bigint,bigint,bigint,bigint,bigint,uuid,timestamptz,timestamptz) FROM PUBLIC;
            REVOKE ALL ON FUNCTION economy_private.transition_payout_operation_v1(
                uuid,bigint,integer,text,text,timestamptz) FROM PUBLIC;
            REVOKE ALL ON FUNCTION economy_private.complete_payout_provider_event_v1(
                text,text,uuid,bigint,integer,text,timestamptz) FROM PUBLIC;
            REVOKE ALL ON FUNCTION economy_private.read_payout_operation_by_id_v1(uuid) FROM PUBLIC;
            REVOKE ALL ON FUNCTION economy_private.read_payout_operation_by_idempotency_v1(text) FROM PUBLIC;
            REVOKE ALL ON FUNCTION economy_private.read_payout_provider_event_v1(text) FROM PUBLIC;

            GRANT EXECUTE ON FUNCTION economy_private.create_payout_operation_v1(
                uuid,text,text,uuid,uuid,uuid,bigint,text,text,text,text,text,text,integer,bigint,bigint,bigint,bigint,bigint,bigint,uuid,timestamptz,timestamptz),
                economy_private.transition_payout_operation_v1(uuid,bigint,integer,text,text,timestamptz),
                economy_private.complete_payout_provider_event_v1(text,text,uuid,bigint,integer,text,timestamptz),
                economy_private.read_payout_operation_by_id_v1(uuid),
                economy_private.read_payout_operation_by_idempotency_v1(text),
                economy_private.read_payout_provider_event_v1(text)
                TO gameguild_economy_writer;

            CREATE TRIGGER deny_payout_provider_event_mutation
                BEFORE UPDATE OR DELETE ON public.economy_payout_provider_events
                FOR EACH ROW EXECUTE FUNCTION economy_private.deny_immutable_mutation_v1();
            """);
    }

    private static void RemovePayoutOperationSecurity(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS deny_payout_provider_event_mutation
                ON public.economy_payout_provider_events;
            DROP FUNCTION IF EXISTS economy_private.create_payout_operation_v1(
                uuid,text,text,uuid,uuid,uuid,bigint,text,text,text,text,text,text,integer,bigint,bigint,bigint,bigint,bigint,bigint,uuid,timestamptz,timestamptz);
            DROP FUNCTION IF EXISTS economy_private.transition_payout_operation_v1(
                uuid,bigint,integer,text,text,timestamptz);
            DROP FUNCTION IF EXISTS economy_private.complete_payout_provider_event_v1(
                text,text,uuid,bigint,integer,text,timestamptz);
            DROP FUNCTION IF EXISTS economy_private.read_payout_operation_by_id_v1(uuid);
            DROP FUNCTION IF EXISTS economy_private.read_payout_operation_by_idempotency_v1(text);
            DROP FUNCTION IF EXISTS economy_private.read_payout_provider_event_v1(text);
            """);
    }
}
