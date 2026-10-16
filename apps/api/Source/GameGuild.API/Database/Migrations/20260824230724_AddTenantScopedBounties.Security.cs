using Microsoft.EntityFrameworkCore.Migrations;

namespace GameGuild.API.Database.Migrations;

public partial class AddTenantScopedBounties : Migration
{
    private static void InstallTenantScopedBountySecurity(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE public.economy_bounties
                ALTER COLUMN "TenantId" SET DEFAULT COALESCE(
                    NULLIF(current_setting('gameguild.economy_tenant_id', true), '')::uuid,
                    '00000000-0000-0000-0000-000000000000'::uuid);
            ALTER TABLE public.economy_bounty_terminal_events
                ALTER COLUMN "TenantId" SET DEFAULT COALESCE(
                    NULLIF(current_setting('gameguild.economy_tenant_id', true), '')::uuid,
                    '00000000-0000-0000-0000-000000000000'::uuid);

            CREATE OR REPLACE FUNCTION economy_private.create_bounty_escrow_v4(
                p_tenant_id uuid,
                p_bounty_id uuid,
                p_poster_id uuid,
                p_poster_wallet_id uuid,
                p_escrow_wallet_id uuid,
                p_currency integer,
                p_amount_units bigint,
                p_reclaim_fee_ppm integer,
                p_requires_prerequisite boolean,
                p_minimum_reputation integer,
                p_requires_instructor_verification boolean,
                p_idempotency_key text,
                p_request_hash text,
                p_posted_at timestamptz,
                p_expires_at timestamptz,
                p_fragments jsonb,
                p_posting_id uuid)
            RETURNS void
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
            DECLARE
                existing public.economy_bounties%ROWTYPE;
            BEGIN
                IF p_tenant_id IS NULL OR p_tenant_id = '00000000-0000-0000-0000-000000000000'::uuid THEN
                    RAISE EXCEPTION 'bounty tenant is required' USING ERRCODE = '22023';
                END IF;

                PERFORM pg_advisory_xact_lock(hashtextextended('bounty-post|' || btrim(p_idempotency_key), 0));
                SELECT * INTO existing
                FROM public.economy_bounties
                WHERE "IdempotencyKey" = btrim(p_idempotency_key)
                FOR UPDATE;
                IF FOUND THEN
                    IF existing."TenantId" = p_tenant_id
                       AND existing."Id" = p_bounty_id
                       AND existing."RequestHash" = btrim(p_request_hash) THEN
                        RETURN;
                    END IF;
                    RAISE EXCEPTION 'bounty idempotency key is bound to another tenant or request'
                        USING ERRCODE = '23505';
                END IF;
                IF EXISTS (SELECT 1 FROM public.economy_bounties WHERE "Id" = p_bounty_id) THEN
                    RAISE EXCEPTION 'bounty id is bound to another tenant or request' USING ERRCODE = '23505';
                END IF;
                IF NOT EXISTS (
                    SELECT 1 FROM public.economy_posting_groups posting
                    WHERE posting."Id" = p_posting_id
                      AND posting."TenantId" = p_tenant_id) THEN
                    RAISE EXCEPTION 'bounty posting is absent or cross-tenant' USING ERRCODE = '42501';
                END IF;

                PERFORM set_config('gameguild.economy_tenant_id', p_tenant_id::text, true);
                PERFORM economy_private.create_bounty_escrow_v3(
                    p_bounty_id, p_poster_id, p_poster_wallet_id, p_escrow_wallet_id, p_currency,
                    p_amount_units, p_reclaim_fee_ppm, p_requires_prerequisite, p_minimum_reputation,
                    p_requires_instructor_verification, p_idempotency_key, p_request_hash, p_posted_at,
                    p_expires_at, p_fragments, p_posting_id);

                IF NOT EXISTS (
                    SELECT 1 FROM public.economy_bounties bounty
                    WHERE bounty."Id" = p_bounty_id AND bounty."TenantId" = p_tenant_id) THEN
                    RAISE EXCEPTION 'bounty tenant binding was not persisted' USING ERRCODE = '23514';
                END IF;
            END
            $function$;

            CREATE OR REPLACE FUNCTION economy_private.read_bounty_escrow_by_id_v2(
                p_tenant_id uuid,
                p_bounty_id uuid)
            RETURNS TABLE(
                "Id" uuid, "TenantId" uuid, "PosterId" uuid, "PosterWalletId" uuid,
                "EscrowWalletId" uuid, "Currency" integer, "AmountUnits" bigint,
                "ReclaimFeePpm" integer, "RequiresPrerequisite" boolean,
                "MinimumReputation" integer, "RequiresInstructorVerification" boolean,
                "Status" integer, "IdempotencyKey" text, "RequestHash" text,
                "PostedAt" timestamptz, "ExpiresAt" timestamptz, "Version" bigint)
            LANGUAGE sql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
                SELECT bounty."Id", bounty."TenantId", bounty."PosterId", bounty."PosterWalletId",
                       bounty."EscrowWalletId", bounty."Currency", bounty."AmountUnits",
                       bounty."ReclaimFeePpm", bounty."RequiresPrerequisite", bounty."MinimumReputation",
                       bounty."RequiresInstructorVerification", bounty."Status", bounty."IdempotencyKey",
                       bounty."RequestHash", bounty."PostedAt", bounty."ExpiresAt", bounty."Version"
                FROM public.economy_bounties bounty
                WHERE bounty."TenantId" = p_tenant_id AND bounty."Id" = p_bounty_id
            $function$;

            CREATE OR REPLACE FUNCTION economy_private.read_bounty_escrow_by_idempotency_v2(
                p_tenant_id uuid,
                p_idempotency_key text)
            RETURNS TABLE(
                "Id" uuid, "TenantId" uuid, "PosterId" uuid, "PosterWalletId" uuid,
                "EscrowWalletId" uuid, "Currency" integer, "AmountUnits" bigint,
                "ReclaimFeePpm" integer, "RequiresPrerequisite" boolean,
                "MinimumReputation" integer, "RequiresInstructorVerification" boolean,
                "Status" integer, "IdempotencyKey" text, "RequestHash" text,
                "PostedAt" timestamptz, "ExpiresAt" timestamptz, "Version" bigint)
            LANGUAGE sql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
                SELECT bounty."Id", bounty."TenantId", bounty."PosterId", bounty."PosterWalletId",
                       bounty."EscrowWalletId", bounty."Currency", bounty."AmountUnits",
                       bounty."ReclaimFeePpm", bounty."RequiresPrerequisite", bounty."MinimumReputation",
                       bounty."RequiresInstructorVerification", bounty."Status", bounty."IdempotencyKey",
                       bounty."RequestHash", bounty."PostedAt", bounty."ExpiresAt", bounty."Version"
                FROM public.economy_bounties bounty
                WHERE bounty."TenantId" = p_tenant_id
                  AND bounty."IdempotencyKey" = btrim(p_idempotency_key)
            $function$;

            CREATE OR REPLACE FUNCTION economy_private.read_bounty_escrow_fragments_v4(
                p_tenant_id uuid,
                p_bounty_id uuid)
            RETURNS TABLE(
                "ParentLotId" uuid, "EscrowLotId" uuid, "Currency" integer,
                "Provenance" integer, "AmountUnits" bigint, "TraceUnitsPerCoinUnit" bigint,
                "SelectedRootRanges" jsonb)
            LANGUAGE sql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
                SELECT fragment."ParentLotId", fragment."EscrowLotId", fragment."Currency",
                       fragment."Provenance", fragment."AmountUnits", fragment."TraceUnitsPerCoinUnit",
                       fragment."SelectedRootRanges"
                FROM public.economy_bounty_escrow_fragments fragment
                JOIN public.economy_bounties bounty ON bounty."Id" = fragment."BountyId"
                WHERE bounty."TenantId" = p_tenant_id AND bounty."Id" = p_bounty_id
                ORDER BY fragment."ParentLotId"
            $function$;

            CREATE OR REPLACE FUNCTION economy_private.complete_bounty_claim_v2(
                p_tenant_id uuid, p_bounty_id uuid, p_claimant_id uuid, p_claimant_wallet_id uuid,
                p_idempotency_key text, p_posting_id uuid, p_risk_decision_id uuid,
                p_evidence_hash text, p_claimed_at timestamptz)
            RETURNS void
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
            DECLARE
                existing public.economy_bounty_terminal_events%ROWTYPE;
            BEGIN
                IF p_tenant_id IS NULL OR p_tenant_id = '00000000-0000-0000-0000-000000000000'::uuid THEN
                    RAISE EXCEPTION 'bounty claim tenant is required' USING ERRCODE = '22023';
                END IF;
                PERFORM pg_advisory_xact_lock(hashtextextended('bounty-terminal|' || btrim(p_idempotency_key), 0));
                IF NOT EXISTS (
                    SELECT 1 FROM public.economy_bounties bounty
                    WHERE bounty."Id" = p_bounty_id AND bounty."TenantId" = p_tenant_id) OR NOT EXISTS (
                    SELECT 1 FROM public.economy_posting_groups posting
                    WHERE posting."Id" = p_posting_id AND posting."TenantId" = p_tenant_id) THEN
                    RAISE EXCEPTION 'bounty claim is absent or cross-tenant' USING ERRCODE = '42501';
                END IF;
                SELECT * INTO existing FROM public.economy_bounty_terminal_events
                WHERE "BountyId" = p_bounty_id OR "IdempotencyKey" = btrim(p_idempotency_key)
                FOR UPDATE;
                IF FOUND AND existing."TenantId" <> p_tenant_id THEN
                    RAISE EXCEPTION 'bounty terminal event belongs to another tenant' USING ERRCODE = '42501';
                END IF;
                PERFORM set_config('gameguild.economy_tenant_id', p_tenant_id::text, true);
                PERFORM economy_private.complete_bounty_claim_v1(
                    p_bounty_id, p_claimant_id, p_claimant_wallet_id, p_idempotency_key,
                    p_posting_id, p_risk_decision_id, p_evidence_hash, p_claimed_at);
            END
            $function$;

            CREATE OR REPLACE FUNCTION economy_private.complete_bounty_reclaim_v2(
                p_tenant_id uuid, p_bounty_id uuid, p_poster_id uuid, p_poster_wallet_id uuid,
                p_idempotency_key text, p_posting_id uuid, p_risk_decision_id uuid,
                p_reclaimed_at timestamptz)
            RETURNS void
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
            DECLARE
                existing public.economy_bounty_terminal_events%ROWTYPE;
            BEGIN
                IF p_tenant_id IS NULL OR p_tenant_id = '00000000-0000-0000-0000-000000000000'::uuid THEN
                    RAISE EXCEPTION 'bounty reclaim tenant is required' USING ERRCODE = '22023';
                END IF;
                PERFORM pg_advisory_xact_lock(hashtextextended('bounty-terminal|' || btrim(p_idempotency_key), 0));
                IF NOT EXISTS (
                    SELECT 1 FROM public.economy_bounties bounty
                    WHERE bounty."Id" = p_bounty_id AND bounty."TenantId" = p_tenant_id) OR NOT EXISTS (
                    SELECT 1 FROM public.economy_posting_groups posting
                    WHERE posting."Id" = p_posting_id AND posting."TenantId" = p_tenant_id) THEN
                    RAISE EXCEPTION 'bounty reclaim is absent or cross-tenant' USING ERRCODE = '42501';
                END IF;
                SELECT * INTO existing FROM public.economy_bounty_terminal_events
                WHERE "BountyId" = p_bounty_id OR "IdempotencyKey" = btrim(p_idempotency_key)
                FOR UPDATE;
                IF FOUND AND existing."TenantId" <> p_tenant_id THEN
                    RAISE EXCEPTION 'bounty terminal event belongs to another tenant' USING ERRCODE = '42501';
                END IF;
                PERFORM set_config('gameguild.economy_tenant_id', p_tenant_id::text, true);
                PERFORM economy_private.complete_bounty_reclaim_v1(
                    p_bounty_id, p_poster_id, p_poster_wallet_id, p_idempotency_key,
                    p_posting_id, p_risk_decision_id, p_reclaimed_at);
            END
            $function$;

            CREATE OR REPLACE FUNCTION economy_private.read_bounty_terminal_by_bounty_v2(
                p_tenant_id uuid, p_bounty_id uuid)
            RETURNS TABLE(
                "Id" uuid, "TenantId" uuid, "BountyId" uuid, "Status" integer,
                "ActorId" uuid, "DestinationWalletId" uuid, "IdempotencyKey" character varying,
                "RiskDecisionId" uuid, "ProceedsSourceStampId" uuid, "ProceedsLotId" uuid,
                "ReturnedUnits" bigint, "FeeUnits" bigint, "FirstJournalSequence" bigint,
                "OutputLots" jsonb, "OccurredAt" timestamptz)
            LANGUAGE sql SECURITY DEFINER SET search_path = pg_catalog, economy_private
            AS $function$
                SELECT terminal."Id", terminal."TenantId", terminal."BountyId", terminal."Status",
                       terminal."ActorId", terminal."DestinationWalletId", terminal."IdempotencyKey",
                       terminal."RiskDecisionId", terminal."ProceedsSourceStampId", terminal."ProceedsLotId",
                       terminal."ReturnedUnits", terminal."FeeUnits", terminal."FirstJournalSequence",
                       terminal."OutputLots", terminal."OccurredAt"
                FROM public.economy_bounty_terminal_events terminal
                WHERE terminal."TenantId" = p_tenant_id AND terminal."BountyId" = p_bounty_id
            $function$;

            CREATE OR REPLACE FUNCTION economy_private.read_bounty_terminal_by_idempotency_v2(
                p_tenant_id uuid, p_idempotency_key text)
            RETURNS TABLE(
                "Id" uuid, "TenantId" uuid, "BountyId" uuid, "Status" integer,
                "ActorId" uuid, "DestinationWalletId" uuid, "IdempotencyKey" character varying,
                "RiskDecisionId" uuid, "ProceedsSourceStampId" uuid, "ProceedsLotId" uuid,
                "ReturnedUnits" bigint, "FeeUnits" bigint, "FirstJournalSequence" bigint,
                "OutputLots" jsonb, "OccurredAt" timestamptz)
            LANGUAGE sql SECURITY DEFINER SET search_path = pg_catalog, economy_private
            AS $function$
                SELECT terminal."Id", terminal."TenantId", terminal."BountyId", terminal."Status",
                       terminal."ActorId", terminal."DestinationWalletId", terminal."IdempotencyKey",
                       terminal."RiskDecisionId", terminal."ProceedsSourceStampId", terminal."ProceedsLotId",
                       terminal."ReturnedUnits", terminal."FeeUnits", terminal."FirstJournalSequence",
                       terminal."OutputLots", terminal."OccurredAt"
                FROM public.economy_bounty_terminal_events terminal
                WHERE terminal."TenantId" = p_tenant_id
                  AND terminal."IdempotencyKey" = btrim(p_idempotency_key)
            $function$;

            ALTER FUNCTION economy_private.create_bounty_escrow_v4(uuid,uuid,uuid,uuid,uuid,integer,bigint,integer,boolean,integer,boolean,text,text,timestamptz,timestamptz,jsonb,uuid)
                OWNER TO gameguild_economy_procedure_owner;
            ALTER FUNCTION economy_private.read_bounty_escrow_by_id_v2(uuid,uuid) OWNER TO gameguild_economy_procedure_owner;
            ALTER FUNCTION economy_private.read_bounty_escrow_by_idempotency_v2(uuid,text) OWNER TO gameguild_economy_procedure_owner;
            ALTER FUNCTION economy_private.read_bounty_escrow_fragments_v4(uuid,uuid) OWNER TO gameguild_economy_procedure_owner;
            ALTER FUNCTION economy_private.complete_bounty_claim_v2(uuid,uuid,uuid,uuid,text,uuid,uuid,text,timestamptz) OWNER TO gameguild_economy_procedure_owner;
            ALTER FUNCTION economy_private.complete_bounty_reclaim_v2(uuid,uuid,uuid,uuid,text,uuid,uuid,timestamptz) OWNER TO gameguild_economy_procedure_owner;
            ALTER FUNCTION economy_private.read_bounty_terminal_by_bounty_v2(uuid,uuid) OWNER TO gameguild_economy_procedure_owner;
            ALTER FUNCTION economy_private.read_bounty_terminal_by_idempotency_v2(uuid,text) OWNER TO gameguild_economy_procedure_owner;

            REVOKE EXECUTE ON FUNCTION economy_private.create_bounty_escrow_v1(uuid,uuid,uuid,uuid,integer,bigint,integer,boolean,integer,boolean,text,text,timestamptz,timestamptz,jsonb) FROM gameguild_economy_writer;
            REVOKE EXECUTE ON FUNCTION economy_private.create_bounty_escrow_v2(uuid,uuid,uuid,uuid,integer,bigint,integer,boolean,integer,boolean,text,text,timestamptz,timestamptz,jsonb) FROM gameguild_economy_writer;
            REVOKE EXECUTE ON FUNCTION economy_private.create_bounty_escrow_v3(uuid,uuid,uuid,uuid,integer,bigint,integer,boolean,integer,boolean,text,text,timestamptz,timestamptz,jsonb,uuid) FROM gameguild_economy_writer;
            REVOKE EXECUTE ON FUNCTION economy_private.complete_bounty_terminal_v1(uuid,uuid,integer,uuid,uuid,text,uuid,uuid,uuid,bigint,bigint,bigint,jsonb,timestamptz) FROM gameguild_economy_writer;
            REVOKE EXECUTE ON FUNCTION economy_private.complete_bounty_claim_v1(uuid,uuid,uuid,text,uuid,uuid,text,timestamptz) FROM gameguild_economy_writer;
            REVOKE EXECUTE ON FUNCTION economy_private.complete_bounty_reclaim_v1(uuid,uuid,uuid,text,uuid,uuid,timestamptz) FROM gameguild_economy_writer;
            REVOKE EXECUTE ON FUNCTION economy_private.read_bounty_escrow_by_id_v1(uuid),
                economy_private.read_bounty_escrow_by_idempotency_v1(text),
                economy_private.read_bounty_escrow_fragments_v3(uuid),
                economy_private.read_bounty_terminal_by_bounty_v1(uuid),
                economy_private.read_bounty_terminal_by_idempotency_v1(text)
                FROM gameguild_economy_writer;

            REVOKE ALL ON FUNCTION economy_private.create_bounty_escrow_v4(uuid,uuid,uuid,uuid,uuid,integer,bigint,integer,boolean,integer,boolean,text,text,timestamptz,timestamptz,jsonb,uuid),
                economy_private.read_bounty_escrow_by_id_v2(uuid,uuid),
                economy_private.read_bounty_escrow_by_idempotency_v2(uuid,text),
                economy_private.read_bounty_escrow_fragments_v4(uuid,uuid),
                economy_private.complete_bounty_claim_v2(uuid,uuid,uuid,uuid,text,uuid,uuid,text,timestamptz),
                economy_private.complete_bounty_reclaim_v2(uuid,uuid,uuid,uuid,text,uuid,uuid,timestamptz),
                economy_private.read_bounty_terminal_by_bounty_v2(uuid,uuid),
                economy_private.read_bounty_terminal_by_idempotency_v2(uuid,text)
                FROM PUBLIC;
            GRANT EXECUTE ON FUNCTION economy_private.create_bounty_escrow_v4(uuid,uuid,uuid,uuid,uuid,integer,bigint,integer,boolean,integer,boolean,text,text,timestamptz,timestamptz,jsonb,uuid),
                economy_private.read_bounty_escrow_by_id_v2(uuid,uuid),
                economy_private.read_bounty_escrow_by_idempotency_v2(uuid,text),
                economy_private.read_bounty_escrow_fragments_v4(uuid,uuid),
                economy_private.complete_bounty_claim_v2(uuid,uuid,uuid,uuid,text,uuid,uuid,text,timestamptz),
                economy_private.complete_bounty_reclaim_v2(uuid,uuid,uuid,uuid,text,uuid,uuid,timestamptz),
                economy_private.read_bounty_terminal_by_bounty_v2(uuid,uuid),
                economy_private.read_bounty_terminal_by_idempotency_v2(uuid,text)
                TO gameguild_economy_writer;
            """);
    }

    private static void RemoveTenantScopedBountySecurity(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP FUNCTION IF EXISTS economy_private.read_bounty_terminal_by_idempotency_v2(uuid,text);
            DROP FUNCTION IF EXISTS economy_private.read_bounty_terminal_by_bounty_v2(uuid,uuid);
            DROP FUNCTION IF EXISTS economy_private.complete_bounty_reclaim_v2(uuid,uuid,uuid,uuid,text,uuid,uuid,timestamptz);
            DROP FUNCTION IF EXISTS economy_private.complete_bounty_claim_v2(uuid,uuid,uuid,uuid,text,uuid,uuid,text,timestamptz);
            DROP FUNCTION IF EXISTS economy_private.read_bounty_escrow_fragments_v4(uuid,uuid);
            DROP FUNCTION IF EXISTS economy_private.read_bounty_escrow_by_idempotency_v2(uuid,text);
            DROP FUNCTION IF EXISTS economy_private.read_bounty_escrow_by_id_v2(uuid,uuid);
            DROP FUNCTION IF EXISTS economy_private.create_bounty_escrow_v4(uuid,uuid,uuid,uuid,uuid,integer,bigint,integer,boolean,integer,boolean,text,text,timestamptz,timestamptz,jsonb,uuid);

            ALTER TABLE public.economy_bounties ALTER COLUMN "TenantId"
                SET DEFAULT '00000000-0000-0000-0000-000000000000'::uuid;
            ALTER TABLE public.economy_bounty_terminal_events ALTER COLUMN "TenantId"
                SET DEFAULT '00000000-0000-0000-0000-000000000000'::uuid;

            GRANT EXECUTE ON FUNCTION economy_private.create_bounty_escrow_v3(uuid,uuid,uuid,uuid,integer,bigint,integer,boolean,integer,boolean,text,text,timestamptz,timestamptz,jsonb,uuid),
                economy_private.complete_bounty_claim_v1(uuid,uuid,uuid,text,uuid,uuid,text,timestamptz),
                economy_private.complete_bounty_reclaim_v1(uuid,uuid,uuid,text,uuid,uuid,timestamptz),
                economy_private.read_bounty_escrow_by_id_v1(uuid),
                economy_private.read_bounty_escrow_by_idempotency_v1(text),
                economy_private.read_bounty_escrow_fragments_v3(uuid),
                economy_private.read_bounty_terminal_by_bounty_v1(uuid),
                economy_private.read_bounty_terminal_by_idempotency_v1(text)
                TO gameguild_economy_writer;
            """);
    }
}
