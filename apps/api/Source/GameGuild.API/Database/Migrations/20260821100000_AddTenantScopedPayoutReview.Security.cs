using Microsoft.EntityFrameworkCore.Migrations;

namespace GameGuild.API.Database.Migrations;

public partial class AddTenantScopedPayoutReview : Migration
{
    private static void InstallPayoutRequestReviewSecurity(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION economy_private.create_payout_request_v2(
                p_id uuid, p_idempotency_key text, p_request_hash text, p_payee_id uuid,
                p_wallet_id uuid, p_amount_units bigint, p_state integer, p_version bigint,
                p_created_at timestamptz, p_updated_at timestamptz)
            RETURNS void
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
            DECLARE
                wallet_tenant_id uuid;
            BEGIN
                IF p_id IS NULL OR p_payee_id IS NULL OR p_wallet_id IS NULL
                   OR p_amount_units <= 0 OR p_state <> 1 OR p_version <> 1
                   OR p_created_at IS NULL OR p_updated_at <> p_created_at
                   OR pg_catalog.length(pg_catalog.btrim(p_idempotency_key)) = 0
                   OR pg_catalog.length(pg_catalog.btrim(p_request_hash)) <> 64 THEN
                    RAISE EXCEPTION 'payout request creation violates immutable request rules'
                        USING ERRCODE = '23514';
                END IF;

                SELECT "TenantId" INTO wallet_tenant_id
                FROM public.economy_wallets
                WHERE "Id" = p_wallet_id AND "OwnerId" = p_payee_id AND "State" = 1;
                IF NOT FOUND OR wallet_tenant_id IS NULL THEN
                    RAISE EXCEPTION 'payout request wallet does not belong to the requesting payee'
                        USING ERRCODE = '42501';
                END IF;

                INSERT INTO public.economy_payout_requests (
                    "Id", "IdempotencyKey", "RequestHash", "PayeeId", "TenantId", "WalletId", "AmountUnits",
                    "State", "Version", "CreatedAt", "UpdatedAt", "FirstApprovalActorId")
                VALUES (
                    p_id, pg_catalog.btrim(p_idempotency_key), pg_catalog.btrim(p_request_hash),
                    p_payee_id, wallet_tenant_id, p_wallet_id, p_amount_units, p_state, p_version,
                    p_created_at, p_updated_at, NULL);
            EXCEPTION WHEN unique_violation THEN
                RAISE EXCEPTION 'payout request overlaps an active wallet request or reuses an idempotency key'
                    USING ERRCODE = '23505';
            END
            $function$;

            CREATE OR REPLACE FUNCTION economy_private.read_payout_request_for_review_v2(
                p_tenant_id uuid,
                p_id uuid)
            RETURNS SETOF public.economy_payout_requests
            LANGUAGE sql STABLE SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
                SELECT * FROM public.economy_payout_requests
                WHERE "TenantId" = p_tenant_id AND "Id" = p_id
            $function$;

            CREATE OR REPLACE FUNCTION economy_private.read_payout_requests_for_review_v2(
                p_tenant_id uuid,
                p_take integer)
            RETURNS SETOF public.economy_payout_requests
            LANGUAGE sql STABLE SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
                SELECT * FROM public.economy_payout_requests
                WHERE "TenantId" = p_tenant_id
                  AND "State" IN (1, 5)
                ORDER BY "State", "CreatedAt", "Id"
                LIMIT GREATEST(1, LEAST(p_take, 100))
            $function$;

            CREATE OR REPLACE FUNCTION economy_private.read_payout_request_review_audit_v2(
                p_tenant_id uuid,
                p_request_id uuid)
            RETURNS SETOF public.economy_payout_request_review_audit_events
            LANGUAGE sql STABLE SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
                SELECT * FROM public.economy_payout_request_review_audit_events
                WHERE "TenantId" = p_tenant_id AND "RequestId" = p_request_id
                ORDER BY "OccurredAt", "Id"
            $function$;

            CREATE OR REPLACE FUNCTION economy_private.review_payout_request_v2(
                p_tenant_id uuid,
                p_id uuid,
                p_expected_version bigint,
                p_reviewer_id uuid,
                p_outcome integer,
                p_reason text,
                p_occurred_at timestamptz)
            RETURNS SETOF public.economy_payout_requests
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
            DECLARE
                current public.economy_payout_requests%ROWTYPE;
                next_state integer;
            BEGIN
                SELECT * INTO current
                FROM public.economy_payout_requests
                WHERE "TenantId" = p_tenant_id AND "Id" = p_id
                FOR UPDATE;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'payout request was not found for the reviewing tenant'
                        USING ERRCODE = 'P0002';
                END IF;
                IF p_reviewer_id IS NULL OR p_reviewer_id = current."PayeeId" THEN
                    RAISE EXCEPTION 'payout requester cannot review the same payout request'
                        USING ERRCODE = '42501';
                END IF;
                IF p_outcome NOT IN (3, 4) THEN
                    RAISE EXCEPTION 'payout request review transition is invalid'
                        USING ERRCODE = '23514';
                END IF;
                IF current."Version" <> p_expected_version OR p_occurred_at < current."UpdatedAt" THEN
                    RAISE EXCEPTION 'payout request review version is stale'
                        USING ERRCODE = '40001';
                END IF;
                IF p_occurred_at IS NULL OR pg_catalog.length(pg_catalog.btrim(p_reason)) NOT BETWEEN 3 AND 1000 THEN
                    RAISE EXCEPTION 'payout request review reason is invalid'
                        USING ERRCODE = '23514';
                END IF;

                IF current."State" = 1 THEN
                    next_state := CASE WHEN p_outcome = 3 THEN 5 ELSE 4 END;
                ELSIF current."State" = 5 THEN
                    IF current."FirstApprovalActorId" IS NULL OR current."FirstApprovalActorId" = p_reviewer_id THEN
                        RAISE EXCEPTION 'a different tenant administrator must complete the payout approval'
                            USING ERRCODE = '42501';
                    END IF;
                    next_state := p_outcome;
                ELSE
                    RAISE EXCEPTION 'payout request review transition is invalid'
                        USING ERRCODE = '23514';
                END IF;

                UPDATE public.economy_payout_requests
                SET "State" = next_state,
                    "Version" = current."Version" + 1,
                    "UpdatedAt" = p_occurred_at,
                    "FirstApprovalActorId" = CASE
                        WHEN current."State" = 1 AND p_outcome = 3 THEN p_reviewer_id
                        ELSE current."FirstApprovalActorId"
                    END
                WHERE "Id" = current."Id";

                INSERT INTO public.economy_payout_request_review_audit_events (
                    "Id", "RequestId", "TenantId", "ActorId", "Outcome", "Reason", "OccurredAt")
                VALUES (
                    gen_random_uuid(), current."Id", current."TenantId", p_reviewer_id,
                    p_outcome, pg_catalog.btrim(p_reason), p_occurred_at);

                RETURN QUERY
                SELECT * FROM public.economy_payout_requests
                WHERE "Id" = current."Id";
            END
            $function$;
            """);

        migrationBuilder.Sql(
            """
            ALTER FUNCTION economy_private.create_payout_request_v2(
                uuid,text,text,uuid,uuid,bigint,integer,bigint,timestamptz,timestamptz)
                OWNER TO gameguild_economy_procedure_owner;
            ALTER FUNCTION economy_private.read_payout_request_for_review_v2(uuid,uuid)
                OWNER TO gameguild_economy_procedure_owner;
            ALTER FUNCTION economy_private.read_payout_requests_for_review_v2(uuid,integer)
                OWNER TO gameguild_economy_procedure_owner;
            ALTER FUNCTION economy_private.read_payout_request_review_audit_v2(uuid,uuid)
                OWNER TO gameguild_economy_procedure_owner;
            ALTER FUNCTION economy_private.review_payout_request_v2(uuid,uuid,bigint,uuid,integer,text,timestamptz)
                OWNER TO gameguild_economy_procedure_owner;

            REVOKE ALL ON TABLE public.economy_payout_request_review_audit_events FROM PUBLIC;
            REVOKE ALL ON TABLE public.economy_payout_request_review_audit_events FROM gameguild_economy_writer;
            REVOKE ALL ON TABLE public.economy_payout_request_review_audit_events FROM gameguild_economy_runtime;
            GRANT SELECT, INSERT ON TABLE public.economy_payout_request_review_audit_events
                TO gameguild_economy_procedure_owner;
            GRANT ALL ON TABLE public.economy_payout_request_review_audit_events TO gameguild_economy_migration;

            REVOKE ALL ON FUNCTION economy_private.create_payout_request_v1(
                uuid,text,text,uuid,uuid,bigint,integer,bigint,timestamptz,timestamptz)
                FROM gameguild_economy_writer;
            REVOKE ALL ON FUNCTION economy_private.create_payout_request_v2(
                uuid,text,text,uuid,uuid,bigint,integer,bigint,timestamptz,timestamptz) FROM PUBLIC;
            REVOKE ALL ON FUNCTION economy_private.read_payout_request_for_review_v2(uuid,uuid) FROM PUBLIC;
            REVOKE ALL ON FUNCTION economy_private.read_payout_requests_for_review_v2(uuid,integer) FROM PUBLIC;
            REVOKE ALL ON FUNCTION economy_private.read_payout_request_review_audit_v2(uuid,uuid) FROM PUBLIC;
            REVOKE ALL ON FUNCTION economy_private.review_payout_request_v2(uuid,uuid,bigint,uuid,integer,text,timestamptz) FROM PUBLIC;

            GRANT EXECUTE ON FUNCTION economy_private.create_payout_request_v2(
                uuid,text,text,uuid,uuid,bigint,integer,bigint,timestamptz,timestamptz),
                economy_private.read_payout_request_for_review_v2(uuid,uuid),
                economy_private.read_payout_requests_for_review_v2(uuid,integer),
                economy_private.read_payout_request_review_audit_v2(uuid,uuid),
                economy_private.review_payout_request_v2(uuid,uuid,bigint,uuid,integer,text,timestamptz)
                TO gameguild_economy_writer;
            """);
    }

    private static void RemovePayoutRequestReviewSecurity(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP FUNCTION IF EXISTS economy_private.review_payout_request_v2(uuid,uuid,bigint,uuid,integer,text,timestamptz);
            DROP FUNCTION IF EXISTS economy_private.read_payout_request_review_audit_v2(uuid,uuid);
            DROP FUNCTION IF EXISTS economy_private.read_payout_requests_for_review_v2(uuid,integer);
            DROP FUNCTION IF EXISTS economy_private.read_payout_request_for_review_v2(uuid,uuid);
            DROP FUNCTION IF EXISTS economy_private.create_payout_request_v2(uuid,text,text,uuid,uuid,bigint,integer,bigint,timestamptz,timestamptz);

            GRANT EXECUTE ON FUNCTION economy_private.create_payout_request_v1(
                uuid,text,text,uuid,uuid,bigint,integer,bigint,timestamptz,timestamptz),
                economy_private.transition_payout_request_v1(uuid,uuid,bigint,integer,timestamptz),
                economy_private.read_payout_request_by_idempotency_v1(uuid,text),
                economy_private.read_payout_request_by_id_for_payee_v1(uuid,uuid),
                economy_private.read_payout_requests_by_payee_v1(uuid,integer)
                TO gameguild_economy_writer;
            """);
    }
}
