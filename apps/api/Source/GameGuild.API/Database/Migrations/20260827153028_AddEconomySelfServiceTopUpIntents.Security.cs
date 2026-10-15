using Microsoft.EntityFrameworkCore.Migrations;

namespace GameGuild.API.Database.Migrations;

public partial class AddEconomySelfServiceTopUpIntents : Migration
{
    private static void InstallEconomyTopUpSecurity(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddForeignKey(
            name: "FK_economy_top_up_intents_payments_PaymentId",
            table: "economy_top_up_intents",
            column: "PaymentId",
            principalTable: "payments",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION economy_private.guard_economy_top_up_intent_mutation_v1()
            RETURNS trigger
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
            BEGIN
                IF TG_OP = 'DELETE' THEN
                    RAISE EXCEPTION 'Economy top-up intents are append-preserving'
                        USING ERRCODE = '42501';
                END IF;
                IF NEW."Id" <> OLD."Id" OR NEW."PaymentId" <> OLD."PaymentId"
                   OR NEW."TenantId" <> OLD."TenantId" OR NEW."ActorId" <> OLD."ActorId"
                   OR NEW."WalletId" <> OLD."WalletId" OR NEW."HardCoinUnits" <> OLD."HardCoinUnits"
                   OR NEW."UsdMinorUnits" <> OLD."UsdMinorUnits"
                   OR NEW."JurisdictionCode" <> OLD."JurisdictionCode"
                   OR NEW."PolicyVersion" <> OLD."PolicyVersion" OR NEW."PolicyHash" <> OLD."PolicyHash"
                   OR NEW."Provider" <> OLD."Provider" OR NEW."IdempotencyKey" <> OLD."IdempotencyKey"
                   OR NEW."RequestHash" <> OLD."RequestHash" OR NEW."RequestedAt" <> OLD."RequestedAt" THEN
                    RAISE EXCEPTION 'Economy top-up authority is immutable'
                        USING ERRCODE = '42501';
                END IF;
                IF OLD."ProviderObjectId" IS NOT NULL AND
                   (NEW."ProviderEnvironment" IS DISTINCT FROM OLD."ProviderEnvironment"
                    OR NEW."ProviderAccountId" IS DISTINCT FROM OLD."ProviderAccountId"
                    OR NEW."ProviderObjectId" IS DISTINCT FROM OLD."ProviderObjectId"
                    OR NEW."ProviderObjectType" IS DISTINCT FROM OLD."ProviderObjectType"
                    OR NEW."ProviderMonetaryLeg" IS DISTINCT FROM OLD."ProviderMonetaryLeg"
                    OR NEW."ProviderBoundAt" IS DISTINCT FROM OLD."ProviderBoundAt") THEN
                    RAISE EXCEPTION 'Economy top-up provider binding is immutable'
                        USING ERRCODE = '42501';
                END IF;
                IF NOT (
                    (OLD."Status" = 1 AND NEW."Status" IN (2, 3)) OR
                    (OLD."Status" IN (2, 3) AND NEW."Status" IN (2, 3, 4, 6, 7, 8, 9)) OR
                    (OLD."Status" = 4 AND NEW."Status" IN (4, 5, 6, 8, 9)) OR
                    (OLD."Status" = 5 AND NEW."Status" IN (5, 10)) OR
                    (OLD."Status" IN (6, 7, 8, 9, 10) AND NEW."Status" = OLD."Status")) THEN
                    RAISE EXCEPTION 'Economy top-up state transition is invalid'
                        USING ERRCODE = '22023';
                END IF;
                IF NEW."Version" <> OLD."Version" + 1 THEN
                    RAISE EXCEPTION 'Economy top-up version must advance exactly once'
                        USING ERRCODE = '40001';
                END IF;
                RETURN NEW;
            END
            $function$;

            DROP TRIGGER IF EXISTS guard_economy_top_up_intent_mutation
                ON public.economy_top_up_intents;
            CREATE TRIGGER guard_economy_top_up_intent_mutation
                BEFORE UPDATE OR DELETE ON public.economy_top_up_intents
                FOR EACH ROW EXECUTE FUNCTION economy_private.guard_economy_top_up_intent_mutation_v1();

            CREATE OR REPLACE FUNCTION economy_private.prepare_economy_top_up_intent_v1(
                p_id uuid,
                p_payment_id uuid,
                p_tenant_id uuid,
                p_actor_id uuid,
                p_wallet_id uuid,
                p_hard_coin_units bigint,
                p_usd_minor_units bigint,
                p_jurisdiction_code text,
                p_policy_version bigint,
                p_policy_hash text,
                p_provider text,
                p_idempotency_key text,
                p_request_hash text,
                p_requested_at timestamptz)
            RETURNS SETOF public.economy_top_up_intents
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
            DECLARE
                normalized_key text := btrim(COALESCE(p_idempotency_key, ''));
                normalized_jurisdiction text := upper(btrim(COALESCE(p_jurisdiction_code, '')));
                expected_hash text;
                stored public.economy_top_up_intents%ROWTYPE;
            BEGIN
                IF p_id IS NULL OR p_payment_id IS NULL OR p_tenant_id IS NULL OR p_actor_id IS NULL
                   OR p_wallet_id IS NULL OR p_hard_coin_units <= 0
                   OR p_usd_minor_units <> p_hard_coin_units OR p_policy_version <= 0
                   OR normalized_jurisdiction !~ '^[A-Z]{3}$'
                   OR p_provider <> 'stripe' OR length(normalized_key) = 0 OR length(normalized_key) > 128
                   OR length(btrim(COALESCE(p_policy_hash, ''))) = 0 OR p_requested_at IS NULL THEN
                    RAISE EXCEPTION 'Economy top-up intent arguments are invalid'
                        USING ERRCODE = '22023';
                END IF;
                expected_hash := encode(public.digest(convert_to(concat_ws('|',
                    'economy-hard-coin-top-up-v1', replace(p_tenant_id::text, '-', ''),
                    replace(p_actor_id::text, '-', ''), replace(p_wallet_id::text, '-', ''),
                    p_hard_coin_units::text, p_usd_minor_units::text, normalized_jurisdiction,
                    p_policy_version::text, p_policy_hash, p_provider, normalized_key), 'UTF8'), 'sha256'), 'hex');
                IF p_request_hash <> expected_hash THEN
                    RAISE EXCEPTION 'Economy top-up request hash is invalid'
                        USING ERRCODE = '42501';
                END IF;

                PERFORM pg_advisory_xact_lock(hashtextextended(concat_ws(':',
                    'economy-top-up', p_tenant_id::text, p_actor_id::text, normalized_key), 0));

                SELECT intent.* INTO stored
                FROM public.economy_top_up_intents intent
                WHERE intent."TenantId" = p_tenant_id AND intent."ActorId" = p_actor_id
                  AND intent."IdempotencyKey" = normalized_key;
                IF FOUND THEN
                    IF stored."RequestHash" <> expected_hash THEN
                        RAISE EXCEPTION 'Economy top-up idempotency key is bound to another request'
                            USING ERRCODE = '23505';
                    END IF;
                    RETURN NEXT stored;
                    RETURN;
                END IF;

                INSERT INTO public.payments (
                    "Id", "TenantId", "Amount", "Currency", "Status", "Provider", "IdempotencyKey",
                    "Description", "RetryCount", "MaxRetries", "RefundedAmount", "Version",
                    "CreatedAt", "UpdatedAt")
                VALUES (
                    p_payment_id, p_tenant_id, p_usd_minor_units / 100.0, 'USD', 0, p_provider,
                    concat('economy-top-up:', replace(p_tenant_id::text, '-', ''), ':',
                        replace(p_actor_id::text, '-', ''), ':', normalized_key),
                    'Economy HardCoin top-up', 0, 3, 0, 0, p_requested_at, p_requested_at);

                INSERT INTO public.economy_top_up_intents (
                    "Id", "PaymentId", "TenantId", "ActorId", "WalletId", "HardCoinUnits",
                    "UsdMinorUnits", "JurisdictionCode", "PolicyVersion", "PolicyHash", "Provider",
                    "IdempotencyKey", "RequestHash", "Status", "RequestedAt", "Version")
                VALUES (
                    p_id, p_payment_id, p_tenant_id, p_actor_id, p_wallet_id, p_hard_coin_units,
                    p_usd_minor_units, normalized_jurisdiction, p_policy_version, p_policy_hash, p_provider,
                    normalized_key, expected_hash, 1, p_requested_at, 1)
                RETURNING * INTO stored;
                RETURN NEXT stored;
            END
            $function$;

            CREATE OR REPLACE FUNCTION economy_private.bind_economy_top_up_provider_v1(
                p_top_up_id uuid,
                p_provider text,
                p_environment text,
                p_account_id text,
                p_object_id text,
                p_object_type text,
                p_monetary_leg text,
                p_status integer,
                p_bound_at timestamptz)
            RETURNS SETOF public.economy_top_up_intents
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
            DECLARE
                stored public.economy_top_up_intents%ROWTYPE;
            BEGIN
                IF p_top_up_id IS NULL OR p_provider <> 'stripe'
                   OR length(btrim(COALESCE(p_environment, ''))) = 0
                   OR length(btrim(COALESCE(p_account_id, ''))) = 0
                   OR length(btrim(COALESCE(p_object_id, ''))) = 0
                   OR p_object_type <> 'payment_intent' OR p_monetary_leg <> 'capture'
                   OR p_status NOT IN (2, 3) OR p_bound_at IS NULL THEN
                    RAISE EXCEPTION 'Economy top-up provider binding is invalid'
                        USING ERRCODE = '22023';
                END IF;
                SELECT intent.* INTO stored FROM public.economy_top_up_intents intent
                WHERE intent."Id" = p_top_up_id FOR UPDATE;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'Economy top-up intent was not found' USING ERRCODE = 'P0002';
                END IF;
                IF stored."ProviderObjectId" IS NOT NULL THEN
                    IF stored."Provider" <> p_provider OR stored."ProviderEnvironment" <> p_environment
                       OR stored."ProviderAccountId" <> p_account_id OR stored."ProviderObjectId" <> p_object_id
                       OR stored."ProviderObjectType" <> p_object_type
                       OR stored."ProviderMonetaryLeg" <> p_monetary_leg OR stored."Status" <> p_status THEN
                        RAISE EXCEPTION 'Economy top-up provider object cannot be rebound'
                            USING ERRCODE = '23505';
                    END IF;
                    RETURN NEXT stored;
                    RETURN;
                END IF;
                IF stored."Provider" <> p_provider THEN
                    RAISE EXCEPTION 'Economy top-up provider does not match policy'
                        USING ERRCODE = '42501';
                END IF;
                UPDATE public.economy_top_up_intents
                SET "ProviderEnvironment" = p_environment, "ProviderAccountId" = p_account_id,
                    "ProviderObjectId" = p_object_id, "ProviderObjectType" = p_object_type,
                    "ProviderMonetaryLeg" = p_monetary_leg, "Status" = p_status,
                    "ProviderBoundAt" = p_bound_at, "Version" = "Version" + 1
                WHERE "Id" = p_top_up_id RETURNING * INTO stored;

                UPDATE public.payments
                SET "ProviderEnvironment" = p_environment, "ProviderAccountId" = p_account_id,
                    "ProviderObjectId" = p_object_id, "ProviderObjectType" = p_object_type,
                    "ProviderMonetaryLeg" = p_monetary_leg, "ExternalTransactionId" = p_object_id,
                    "Status" = CASE WHEN p_status = 2 THEN 5 ELSE 1 END,
                    "UpdatedAt" = p_bound_at, "Version" = "Version" + 1
                WHERE "Id" = stored."PaymentId"
                  AND "Provider" = p_provider
                  AND "ProviderObjectId" IS NULL;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'Economy top-up payment provider binding is invalid'
                        USING ERRCODE = '42501';
                END IF;
                RETURN NEXT stored;
            END
            $function$;

            ALTER FUNCTION economy_private.guard_economy_top_up_intent_mutation_v1()
                OWNER TO gameguild_economy_procedure_owner;
            ALTER FUNCTION economy_private.prepare_economy_top_up_intent_v1(
                uuid,uuid,uuid,uuid,uuid,bigint,bigint,text,bigint,text,text,text,text,timestamptz)
                OWNER TO gameguild_economy_procedure_owner;
            ALTER FUNCTION economy_private.bind_economy_top_up_provider_v1(
                uuid,text,text,text,text,text,text,integer,timestamptz)
                OWNER TO gameguild_economy_procedure_owner;
            REVOKE ALL ON TABLE public.economy_top_up_intents FROM PUBLIC;
            REVOKE ALL ON TABLE public.economy_top_up_intents FROM gameguild_economy_writer;
            REVOKE ALL ON TABLE public.economy_top_up_intents FROM gameguild_economy_runtime;
            GRANT SELECT ON TABLE public.economy_top_up_intents TO gameguild_economy_runtime;
            GRANT SELECT, INSERT, UPDATE ON TABLE public.economy_top_up_intents
                TO gameguild_economy_procedure_owner;
            GRANT SELECT, INSERT, UPDATE ON TABLE public.payments
                TO gameguild_economy_procedure_owner;
            GRANT ALL ON TABLE public.economy_top_up_intents TO gameguild_economy_migration;
            REVOKE ALL ON FUNCTION economy_private.guard_economy_top_up_intent_mutation_v1() FROM PUBLIC;
            REVOKE ALL ON FUNCTION economy_private.prepare_economy_top_up_intent_v1(
                uuid,uuid,uuid,uuid,uuid,bigint,bigint,text,bigint,text,text,text,text,timestamptz) FROM PUBLIC;
            REVOKE ALL ON FUNCTION economy_private.bind_economy_top_up_provider_v1(
                uuid,text,text,text,text,text,text,integer,timestamptz) FROM PUBLIC;
            GRANT EXECUTE ON FUNCTION economy_private.prepare_economy_top_up_intent_v1(
                uuid,uuid,uuid,uuid,uuid,bigint,bigint,text,bigint,text,text,text,text,timestamptz)
                TO gameguild_economy_runtime;
            GRANT EXECUTE ON FUNCTION economy_private.bind_economy_top_up_provider_v1(
                uuid,text,text,text,text,text,text,integer,timestamptz)
                TO gameguild_economy_runtime;
            """);
    }

    private static void RemoveEconomyTopUpSecurity(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP FUNCTION IF EXISTS economy_private.bind_economy_top_up_provider_v1(
                uuid,text,text,text,text,text,text,integer,timestamptz);
            DROP FUNCTION IF EXISTS economy_private.prepare_economy_top_up_intent_v1(
                uuid,uuid,uuid,uuid,uuid,bigint,bigint,text,bigint,text,text,text,text,timestamptz);
            DROP TRIGGER IF EXISTS guard_economy_top_up_intent_mutation
                ON public.economy_top_up_intents;
            DROP FUNCTION IF EXISTS economy_private.guard_economy_top_up_intent_mutation_v1();
            """);
        migrationBuilder.DropForeignKey(
            name: "FK_economy_top_up_intents_payments_PaymentId",
            table: "economy_top_up_intents");
    }
}
