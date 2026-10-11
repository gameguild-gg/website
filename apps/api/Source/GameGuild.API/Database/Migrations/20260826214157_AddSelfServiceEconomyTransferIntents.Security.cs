using Microsoft.EntityFrameworkCore.Migrations;

namespace GameGuild.API.Database.Migrations;

public partial class AddSelfServiceEconomyTransferIntents : Migration
{
    private static void InstallSelfServiceTransferSecurity(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            INSERT INTO public.economy_registered_capabilities
                ("Id", "Name", "AllowedTemplateKinds", "IsEnabled", "CreatedAt", "RevokedAt")
            VALUES
                ('e1000000-0000-0000-0000-000000000011', 'fifo-transfer', '[4]'::jsonb,
                 true, TIMESTAMPTZ '2026-08-26T00:00:00Z', NULL)
            ON CONFLICT ("Name") DO UPDATE
            SET "AllowedTemplateKinds" = EXCLUDED."AllowedTemplateKinds",
                "IsEnabled" = true,
                "RevokedAt" = NULL;

            CREATE OR REPLACE FUNCTION economy_private.prepare_self_service_transfer_intent_v1(
                p_id uuid,
                p_tenant_id uuid,
                p_actor_id uuid,
                p_recipient_user_id uuid,
                p_transfer_type integer,
                p_currency integer,
                p_provenance integer,
                p_amount_units bigint,
                p_idempotency_key text,
                p_request_hash text,
                p_provider_reference_hash text,
                p_destination_hash text,
                p_requested_at timestamptz)
            RETURNS TABLE(
                "Id" uuid,
                "TenantId" uuid,
                "ActorId" uuid,
                "RecipientUserId" uuid,
                "TransferType" integer,
                "Currency" integer,
                "Provenance" integer,
                "AmountUnits" bigint,
                "IdempotencyKey" text,
                "RequestHash" text,
                "ProviderReferenceHash" text,
                "DestinationHash" text,
                "RequestedAt" timestamptz)
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
            #variable_conflict use_column
            DECLARE
                normalized_key text := btrim(COALESCE(p_idempotency_key, ''));
                expected_request_hash text;
                expected_provider_hash text;
                expected_destination_hash text;
                stored public.economy_self_service_transfer_intents%ROWTYPE;
            BEGIN
                IF p_id IS NULL OR p_tenant_id IS NULL OR p_actor_id IS NULL
                   OR p_recipient_user_id IS NULL OR p_actor_id = p_recipient_user_id
                   OR p_transfer_type NOT IN (1, 2, 3) OR p_currency NOT IN (1, 2)
                   OR p_amount_units <= 0 OR p_requested_at IS NULL
                   OR length(normalized_key) = 0 OR length(normalized_key) > 128
                   OR (p_currency = 1 AND p_provenance <> 1)
                   OR (p_currency = 2 AND p_provenance <> 3) THEN
                    RAISE EXCEPTION 'self-service transfer intent arguments are invalid'
                        USING ERRCODE = '22023';
                END IF;

                expected_request_hash := encode(public.digest(convert_to(concat_ws('|',
                    'economy-self-service-transfer-v1',
                    replace(p_tenant_id::text, '-', ''),
                    replace(p_actor_id::text, '-', ''),
                    replace(p_recipient_user_id::text, '-', ''),
                    p_transfer_type::text,
                    p_currency::text,
                    p_provenance::text,
                    p_amount_units::text,
                    normalized_key), 'UTF8'), 'sha256'), 'hex');
                expected_provider_hash := encode(public.digest(convert_to(
                    'internal-economy-transfer-v1', 'UTF8'), 'sha256'), 'hex');
                expected_destination_hash := encode(public.digest(convert_to(concat_ws('|',
                    'economy-transfer-destination-v1',
                    replace(p_tenant_id::text, '-', ''),
                    replace(p_recipient_user_id::text, '-', ''),
                    p_transfer_type::text), 'UTF8'), 'sha256'), 'hex');
                IF p_request_hash <> expected_request_hash
                   OR p_provider_reference_hash <> expected_provider_hash
                   OR p_destination_hash <> expected_destination_hash THEN
                    RAISE EXCEPTION 'self-service transfer intent hashes are invalid'
                        USING ERRCODE = '42501';
                END IF;

                PERFORM pg_advisory_xact_lock(hashtextextended(concat_ws(':',
                    'economy-self-service-transfer', p_tenant_id::text,
                    p_actor_id::text, normalized_key), 0));
                INSERT INTO public.economy_self_service_transfer_intents (
                    "Id", "TenantId", "ActorId", "RecipientUserId", "TransferType", "Currency", "Provenance",
                    "AmountUnits", "IdempotencyKey", "RequestHash", "ProviderReferenceHash", "DestinationHash", "RequestedAt")
                VALUES (
                    p_id, p_tenant_id, p_actor_id, p_recipient_user_id, p_transfer_type, p_currency, p_provenance,
                    p_amount_units, normalized_key, expected_request_hash, expected_provider_hash,
                    expected_destination_hash, p_requested_at)
                ON CONFLICT ("TenantId", "ActorId", "IdempotencyKey") DO NOTHING;

                SELECT intent.* INTO stored
                FROM public.economy_self_service_transfer_intents intent
                WHERE intent."TenantId" = p_tenant_id
                  AND intent."ActorId" = p_actor_id
                  AND intent."IdempotencyKey" = normalized_key;
                IF NOT FOUND OR stored."Id" <> p_id
                   OR stored."RequestHash" <> expected_request_hash THEN
                    RAISE EXCEPTION 'self-service transfer idempotency key is bound to another request'
                        USING ERRCODE = '23505';
                END IF;

                RETURN QUERY SELECT
                    stored."Id", stored."TenantId", stored."ActorId", stored."RecipientUserId",
                    stored."TransferType", stored."Currency", stored."Provenance", stored."AmountUnits",
                    stored."IdempotencyKey"::text, stored."RequestHash"::text,
                    stored."ProviderReferenceHash"::text, stored."DestinationHash"::text,
                    stored."RequestedAt";
            END
            $function$;

            ALTER FUNCTION economy_private.prepare_self_service_transfer_intent_v1(
                uuid,uuid,uuid,uuid,integer,integer,integer,bigint,text,text,text,text,timestamptz)
                OWNER TO gameguild_economy_procedure_owner;
            REVOKE ALL ON FUNCTION economy_private.prepare_self_service_transfer_intent_v1(
                uuid,uuid,uuid,uuid,integer,integer,integer,bigint,text,text,text,text,timestamptz)
                FROM PUBLIC;
            GRANT EXECUTE ON FUNCTION economy_private.prepare_self_service_transfer_intent_v1(
                uuid,uuid,uuid,uuid,integer,integer,integer,bigint,text,text,text,text,timestamptz)
                TO gameguild_economy_runtime;

            CREATE OR REPLACE FUNCTION economy_private.reserve_self_service_transfer_roots_v1(
                p_intent_id uuid,
                p_tenant_id uuid,
                p_actor_id uuid,
                p_source_wallet_id uuid,
                p_destination_wallet_id uuid)
            RETURNS TABLE(source_root_id uuid)
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
            DECLARE
                stored public.economy_self_service_transfer_intents%ROWTYPE;
                reserved_units bigint;
            BEGIN
                IF p_intent_id IS NULL OR p_tenant_id IS NULL OR p_actor_id IS NULL
                   OR p_source_wallet_id IS NULL OR p_destination_wallet_id IS NULL
                   OR p_source_wallet_id = p_destination_wallet_id THEN
                    RAISE EXCEPTION 'self-service transfer root reservation arguments are invalid'
                        USING ERRCODE = '22023';
                END IF;

                SELECT intent.* INTO stored
                FROM public.economy_self_service_transfer_intents intent
                WHERE intent."Id" = p_intent_id
                  AND intent."TenantId" = p_tenant_id
                  AND intent."ActorId" = p_actor_id;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'self-service transfer intent is absent or outside actor authority'
                        USING ERRCODE = '42501';
                END IF;

                PERFORM 1
                FROM public.economy_wallets source_wallet
                JOIN public.economy_wallets destination_wallet
                  ON destination_wallet."Id" = p_destination_wallet_id
                 AND destination_wallet."OwnerId" = stored."RecipientUserId"
                 AND destination_wallet."TenantId" = stored."TenantId"
                 AND destination_wallet."State" = 1
                WHERE source_wallet."Id" = p_source_wallet_id
                  AND source_wallet."OwnerId" = stored."ActorId"
                  AND source_wallet."TenantId" = stored."TenantId"
                  AND source_wallet."State" = 1
                FOR SHARE OF source_wallet, destination_wallet;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'self-service transfer wallets are absent, inactive, or outside actor authority'
                        USING ERRCODE = '42501';
                END IF;

                SELECT COALESCE(sum(reservation.amount_units), 0)
                INTO reserved_units
                FROM economy_private.reserve_fifo_fragments_v1(
                    stored."Id", p_source_wallet_id, stored."Currency", stored."Provenance",
                    stored."AmountUnits", 4, stored."RequestedAt") reservation;
                IF reserved_units <> stored."AmountUnits" THEN
                    RAISE EXCEPTION 'self-service transfer FIFO reservation is incomplete'
                        USING ERRCODE = '42501';
                END IF;

                RETURN QUERY
                SELECT DISTINCT reservation.root_source_stamp_id
                FROM economy_private.reserve_fifo_fragments_v1(
                    stored."Id", p_source_wallet_id, stored."Currency", stored."Provenance",
                    stored."AmountUnits", 4, stored."RequestedAt") reservation
                ORDER BY reservation.root_source_stamp_id;
            END
            $function$;

            ALTER FUNCTION economy_private.reserve_self_service_transfer_roots_v1(uuid,uuid,uuid,uuid,uuid)
                OWNER TO gameguild_economy_procedure_owner;
            REVOKE ALL ON FUNCTION economy_private.reserve_self_service_transfer_roots_v1(uuid,uuid,uuid,uuid,uuid)
                FROM PUBLIC;
            GRANT EXECUTE ON FUNCTION economy_private.reserve_self_service_transfer_roots_v1(uuid,uuid,uuid,uuid,uuid)
                TO gameguild_economy_runtime;

            CREATE OR REPLACE FUNCTION economy_private.validate_self_service_transfer_root_binding_v1()
            RETURNS trigger
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
            DECLARE
                expected_roots jsonb;
            BEGIN
                IF NOT EXISTS (
                    SELECT 1
                    FROM public.economy_self_service_transfer_intents intent
                    WHERE intent."Id" = NEW."Id") THEN
                    RETURN NEW;
                END IF;

                SELECT decision."SourceRoots"
                INTO expected_roots
                FROM public.economy_posting_groups posting
                JOIN public.economy_risk_decisions decision
                  ON decision."Id" = posting."RiskDecisionId"
                JOIN public.economy_self_service_transfer_intents intent
                  ON intent."Id" = NEW."Id"
                JOIN public.economy_wallets source_wallet
                  ON source_wallet."Id" = NEW."SourceWalletId"
                 AND source_wallet."OwnerId" = intent."ActorId"
                 AND source_wallet."TenantId" = intent."TenantId"
                 AND source_wallet."State" = 1
                JOIN public.economy_wallets destination_wallet
                  ON destination_wallet."Id" = NEW."DestinationWalletId"
                 AND destination_wallet."OwnerId" = intent."RecipientUserId"
                 AND destination_wallet."TenantId" = intent."TenantId"
                 AND destination_wallet."State" = 1
                WHERE posting."Id" = NEW."Id"
                  AND decision."SourceWalletId" = NEW."SourceWalletId"
                  AND decision."DestinationWalletId" = NEW."DestinationWalletId"
                  AND decision."Currency" = NEW."Currency"
                  AND decision."AmountUnits" = NEW."AmountUnits"
                FOR SHARE OF posting, decision, source_wallet, destination_wallet;
                IF NOT FOUND OR jsonb_typeof(expected_roots) IS DISTINCT FROM 'array'
                   OR jsonb_array_length(expected_roots) = 0 THEN
                    RAISE EXCEPTION 'self-service transfer risk decision has no valid source-root authority'
                        USING ERRCODE = '42501';
                END IF;

                IF EXISTS (
                    SELECT DISTINCT reservation."RootSourceStampId"
                    FROM public.economy_fragment_reservations reservation
                    WHERE reservation."OperationId" = NEW."Id"
                      AND reservation."Status" = 3
                    EXCEPT
                    SELECT DISTINCT (item #>> '{}')::uuid
                    FROM jsonb_array_elements(expected_roots) item)
                   OR EXISTS (
                    SELECT DISTINCT (item #>> '{}')::uuid
                    FROM jsonb_array_elements(expected_roots) item
                    EXCEPT
                    SELECT DISTINCT reservation."RootSourceStampId"
                    FROM public.economy_fragment_reservations reservation
                    WHERE reservation."OperationId" = NEW."Id"
                      AND reservation."Status" = 3) THEN
                    RAISE EXCEPTION 'self-service transfer source roots do not match the risk decision'
                        USING ERRCODE = '42501';
                END IF;
                RETURN NEW;
            END
            $function$;

            ALTER FUNCTION economy_private.validate_self_service_transfer_root_binding_v1()
                OWNER TO gameguild_economy_procedure_owner;
            REVOKE ALL ON FUNCTION economy_private.validate_self_service_transfer_root_binding_v1() FROM PUBLIC;
            DROP TRIGGER IF EXISTS tr_economy_self_service_transfer_root_binding
                ON public.economy_fifo_transfer_operations;
            CREATE TRIGGER tr_economy_self_service_transfer_root_binding
                BEFORE INSERT ON public.economy_fifo_transfer_operations
                FOR EACH ROW EXECUTE FUNCTION economy_private.validate_self_service_transfer_root_binding_v1();

            GRANT USAGE ON SCHEMA economy_private TO gameguild_economy_runtime;

            REVOKE ALL ON TABLE public.economy_self_service_transfer_intents FROM PUBLIC;
            GRANT SELECT, INSERT ON TABLE public.economy_self_service_transfer_intents
                TO gameguild_economy_procedure_owner;
            GRANT SELECT ON TABLE public.economy_wallets, public.economy_posting_groups,
                public.economy_risk_decisions, public.economy_fragment_reservations
                TO gameguild_economy_procedure_owner;
            DROP TRIGGER IF EXISTS deny_immutable_mutation
                ON public.economy_self_service_transfer_intents;
            CREATE TRIGGER deny_immutable_mutation
                BEFORE UPDATE OR DELETE ON public.economy_self_service_transfer_intents
                FOR EACH ROW EXECUTE FUNCTION economy_private.deny_immutable_mutation_v1();
            """);
    }

    private static void RemoveSelfServiceTransferSecurity(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS tr_economy_self_service_transfer_root_binding
                ON public.economy_fifo_transfer_operations;
            DROP FUNCTION IF EXISTS economy_private.validate_self_service_transfer_root_binding_v1();
            DROP FUNCTION IF EXISTS economy_private.reserve_self_service_transfer_roots_v1(uuid,uuid,uuid,uuid,uuid);
            DROP FUNCTION IF EXISTS economy_private.prepare_self_service_transfer_intent_v1(
                uuid,uuid,uuid,uuid,integer,integer,integer,bigint,text,text,text,text,timestamptz);
            DELETE FROM public.economy_registered_capabilities
            WHERE "Name" = 'fifo-transfer';
            """);
    }
}
