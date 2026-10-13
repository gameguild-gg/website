using Microsoft.EntityFrameworkCore.Migrations;

namespace GameGuild.API.Database.Migrations;

public partial class AddEconomyLegacyShadowMigration : Migration
{
    private static void InstallLegacyShadowSecurity(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            INSERT INTO public.economy_registered_capabilities
                ("Id", "Name", "AllowedTemplateKinds", "IsEnabled", "CreatedAt", "RevokedAt")
            VALUES
                ('32f578aa-a580-4d65-a978-53c0c59e50cc', 'legacy-balance-backfill', '[1]'::jsonb,
                 true, now(), NULL)
            ON CONFLICT ("Name") DO UPDATE
            SET "AllowedTemplateKinds" = EXCLUDED."AllowedTemplateKinds",
                "IsEnabled" = true,
                "RevokedAt" = NULL;

            CREATE OR REPLACE FUNCTION economy_private.provision_economy_wallet_v1(
                p_tenant_id uuid,
                p_owner_id uuid,
                p_created_at timestamptz)
            RETURNS TABLE(wallet_id uuid, created boolean)
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
            DECLARE
                existing_wallet record;
                new_wallet_id uuid;
            BEGIN
                IF p_tenant_id IS NULL OR p_owner_id IS NULL OR p_created_at IS NULL THEN
                    RAISE EXCEPTION 'Economy wallet provisioning arguments are required'
                        USING ERRCODE = '22023';
                END IF;

                PERFORM pg_advisory_xact_lock(hashtextextended(
                    concat_ws(':', p_tenant_id::text, p_owner_id::text), 0));
                SELECT wallet.* INTO existing_wallet
                FROM public.economy_wallets wallet
                WHERE wallet."TenantId" = p_tenant_id AND wallet."OwnerId" = p_owner_id
                FOR UPDATE;
                IF FOUND AND existing_wallet."State" <> 1 THEN
                    RAISE EXCEPTION 'existing Economy wallet is not active' USING ERRCODE = '55000';
                END IF;

                created := NOT FOUND;
                new_wallet_id := CASE WHEN created THEN gen_random_uuid() ELSE existing_wallet."Id" END;
                IF created THEN
                    INSERT INTO public.economy_wallets ("Id", "OwnerId", "TenantId", "State", "CreatedAt")
                    VALUES (new_wallet_id, p_owner_id, p_tenant_id, 1, p_created_at);
                END IF;

                -- A NULL wallet key is not protected by the ordinary unique index, so serialize
                -- platform-account provisioning explicitly and check existence under the lock.
                PERFORM pg_advisory_xact_lock(hashtextextended('economy-platform-accounts', 0));
                INSERT INTO public.economy_accounts
                    ("Id", "WalletId", "Code", "Currency", "Provenance", "CreatedAt")
                SELECT gen_random_uuid(), NULL, 1, 1, NULL, p_created_at
                WHERE NOT EXISTS (
                    SELECT 1 FROM public.economy_accounts account
                    WHERE account."WalletId" IS NULL AND account."Code" = 1
                      AND account."Currency" = 1 AND account."Provenance" IS NULL);

                INSERT INTO public.economy_accounts
                    ("Id", "WalletId", "Code", "Currency", "Provenance", "CreatedAt")
                SELECT gen_random_uuid(), new_wallet_id, values."Code", values."Currency",
                       values."Provenance", p_created_at
                FROM (VALUES
                    (2, 1, 1),
                    (2, 1, 6),
                    (2, 1, 7),
                    (3, 1, 2),
                    (3, 1, 6),
                    (3, 1, 7),
                    (4, 2, 3),
                    (4, 2, 4),
                    (4, 2, 5),
                    (4, 2, 6),
                    (4, 2, 7),
                    (4, 2, 8)
                ) AS values("Code", "Currency", "Provenance")
                ON CONFLICT ("WalletId", "Code", "Currency", "Provenance") DO NOTHING;

                PERFORM economy_private.rebuild_wallet_projection_v1(new_wallet_id, p_created_at);
                wallet_id := new_wallet_id;
                RETURN NEXT;
            END
            $function$;

            CREATE OR REPLACE FUNCTION economy_private.post_legacy_balance_backfill_v1(
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
                p_legacy_wallet_id uuid,
                p_wallet_id uuid,
                p_source_stamp_id uuid,
                p_credit_lot_id uuid,
                p_hard_units bigint,
                p_snapshot_hash text,
                p_capability_receipt_id uuid,
                p_capability_receipt_hash text,
                p_kill_switch_epoch bigint,
                p_jurisdiction_code text,
                p_provider_hash text,
                p_destination_hash text,
                p_source_root_hash text,
                p_posted_at timestamptz)
            RETURNS TABLE(posting_id uuid, journal_sequence bigint, journal_hash text, duplicate boolean)
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
            DECLARE
                shadow_item record;
                clearing_account_id uuid;
                liability_account_id uuid;
                provider_reference text;
                source_leg_id text;
                confirmation_hash text;
                lines jsonb;
                receipt record;
            BEGIN
                IF p_capability_id IS NULL OR p_actor_id IS NULL OR p_tenant_id IS NULL
                   OR p_posting_id IS NULL OR p_risk_decision_id IS NULL
                   OR p_legacy_wallet_id IS NULL OR p_wallet_id IS NULL
                   OR p_source_stamp_id IS NULL OR p_credit_lot_id IS NULL
                   OR p_capability_receipt_id IS NULL OR p_hard_units <= 0
                   OR p_policy_version <= 0 OR p_reserve_version <= 0
                   OR p_expected_counter_version <= 0 OR p_kill_switch_epoch < 0
                   OR p_posted_at IS NULL
                   OR length(btrim(COALESCE(p_idempotency_key, ''))) = 0
                   OR length(btrim(COALESCE(p_risk_operation_fingerprint, ''))) = 0
                   OR length(btrim(COALESCE(p_snapshot_hash, ''))) = 0
                   OR length(btrim(COALESCE(p_capability_receipt_hash, ''))) = 0
                   OR length(btrim(COALESCE(p_jurisdiction_code, ''))) = 0
                   OR length(btrim(COALESCE(p_provider_hash, ''))) = 0
                   OR length(btrim(COALESCE(p_destination_hash, ''))) = 0
                   OR length(btrim(COALESCE(p_source_root_hash, ''))) = 0 THEN
                    RAISE EXCEPTION 'legacy balance backfill arguments are invalid'
                        USING ERRCODE = '22023';
                END IF;

                SELECT item.* INTO shadow_item
                FROM public.economy_legacy_shadow_wallets item
                WHERE item."TenantId" = p_tenant_id
                  AND item."LegacyWalletId" = p_legacy_wallet_id
                  AND item."EconomyWalletId" = p_wallet_id
                  AND item."SourceStampId" = p_source_stamp_id
                  AND item."PostingId" = p_posting_id
                  AND item."CreditLotId" = p_credit_lot_id
                FOR UPDATE;
                IF NOT FOUND
                   OR shadow_item."LegacyBalanceMinorUnits" <> p_hard_units
                   OR shadow_item."SnapshotHash" <> btrim(p_snapshot_hash)
                   OR shadow_item."State" NOT IN (1, 3) THEN
                    RAISE EXCEPTION 'legacy shadow item is absent, blocked, or mismatched'
                        USING ERRCODE = '23514';
                END IF;

                IF shadow_item."State" = 3 THEN
                    SELECT posting."Id", journal."Sequence", journal."Hash", true
                    INTO posting_id, journal_sequence, journal_hash, duplicate
                    FROM public.economy_posting_groups posting
                    JOIN public.economy_journal_entries journal
                      ON journal."PostingGroupId" = posting."Id"
                    WHERE posting."Id" = p_posting_id
                      AND posting."TenantId" = p_tenant_id
                      AND posting."SourceStampId" = p_source_stamp_id
                      AND posting."TemplateKind" = 1;
                    IF NOT FOUND OR shadow_item."JournalSequence" <> journal_sequence
                       OR shadow_item."JournalHash" <> journal_hash THEN
                        RAISE EXCEPTION 'legacy shadow replay has no matching journal receipt'
                            USING ERRCODE = '23514';
                    END IF;
                    RETURN NEXT;
                    RETURN;
                END IF;

                PERFORM 1
                FROM public.economy_capability_receipts capability_receipt
                JOIN public.economy_capability_receipt_consumptions consumption
                  ON consumption."ReceiptId" = capability_receipt."Id"
                WHERE capability_receipt."Id" = p_capability_receipt_id
                  AND capability_receipt."ReceiptHash" = btrim(p_capability_receipt_hash)
                  AND capability_receipt."TenantId" = p_tenant_id
                  AND capability_receipt."ActorId" = p_actor_id
                  AND capability_receipt."Capability" = 13
                  AND capability_receipt."PolicyVersion" = p_policy_version
                  AND capability_receipt."ReserveVersion" = p_reserve_version
                  AND capability_receipt."RiskDecisionId" = p_risk_decision_id
                  AND capability_receipt."KillSwitchEpoch" = p_kill_switch_epoch
                  AND capability_receipt."JurisdictionCode" = upper(btrim(p_jurisdiction_code))
                  AND capability_receipt."OperationFingerprint" = btrim(p_risk_operation_fingerprint)
                  AND capability_receipt."ProviderHash" = btrim(p_provider_hash)
                  AND capability_receipt."DestinationHash" = btrim(p_destination_hash)
                  AND capability_receipt."SourceRootHashes" = jsonb_build_array(btrim(p_source_root_hash))
                  AND capability_receipt."IssuedAt" <= p_posted_at
                  AND capability_receipt."ExpiresAt" > p_posted_at
                  AND consumption."TenantId" = p_tenant_id
                  AND consumption."ActorId" = p_actor_id
                  AND consumption."OperationFingerprint" = btrim(p_risk_operation_fingerprint)
                  AND consumption."KillSwitchEpoch" = p_kill_switch_epoch
                  AND NOT EXISTS (
                      SELECT 1 FROM public.economy_kill_switches kill_switch
                      WHERE kill_switch."IsActive"
                        AND (kill_switch."TenantId" IS NULL OR kill_switch."TenantId" = p_tenant_id)
                        AND (kill_switch."Capability" IS NULL OR kill_switch."Capability" = 13));
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'legacy backfill capability receipt is absent, stale, or mismatched'
                        USING ERRCODE = '42501';
                END IF;

                PERFORM 1 FROM public.economy_wallets wallet
                WHERE wallet."Id" = p_wallet_id AND wallet."TenantId" = p_tenant_id
                  AND wallet."OwnerId" = shadow_item."OwnerId" AND wallet."State" = 1
                FOR SHARE;
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'legacy destination wallet is absent, cross-tenant, or inactive'
                        USING ERRCODE = '23503';
                END IF;

                SELECT account."Id" INTO clearing_account_id
                FROM public.economy_accounts account
                WHERE account."WalletId" IS NULL AND account."Code" = 1
                  AND account."Currency" = 1 AND account."Provenance" IS NULL
                ORDER BY account."CreatedAt", account."Id" LIMIT 1 FOR SHARE;
                SELECT account."Id" INTO liability_account_id
                FROM public.economy_accounts account
                WHERE account."WalletId" = p_wallet_id AND account."Code" = 2
                  AND account."Currency" = 1 AND account."Provenance" = 1
                FOR SHARE;
                IF clearing_account_id IS NULL OR liability_account_id IS NULL THEN
                    RAISE EXCEPTION 'legacy backfill ledger accounts are not provisioned'
                        USING ERRCODE = '23503';
                END IF;

                provider_reference := concat_ws(chr(31), 'legacy-shadow-v1', 'migration',
                    p_tenant_id::text, p_legacy_wallet_id::text, 'balance');
                source_leg_id := encode(public.digest(convert_to(provider_reference, 'UTF8'), 'sha256'), 'hex');
                confirmation_hash := encode(public.digest(convert_to(
                    concat_ws('|', btrim(p_snapshot_hash), 'confirmed'), 'UTF8'), 'sha256'), 'hex');
                lines := jsonb_build_array(
                    jsonb_build_object(
                        'id', p_source_stamp_id, 'account_id', clearing_account_id,
                        'account_code', 1, 'wallet_id', NULL, 'credit_lot_id', NULL,
                        'side', 1, 'currency', 1, 'amount_units', p_hard_units, 'provenance', NULL),
                    jsonb_build_object(
                        'id', p_credit_lot_id, 'account_id', liability_account_id,
                        'account_code', 2, 'wallet_id', p_wallet_id, 'credit_lot_id', p_credit_lot_id,
                        'side', 2, 'currency', 1, 'amount_units', p_hard_units, 'provenance', 1));

                INSERT INTO public.economy_source_stamps (
                    "Id", "SourceKind", "InternalSourceId", "SourceLegId", "Provider", "ProviderReference",
                    "EvidenceHash", "Provenance", "State", "ActorId", "TenantId", "PostingReferenceId",
                    "PolicyVersion", "AuthoritativeUnits", "ObservedAt", "ConfirmedAt")
                VALUES (p_source_stamp_id, 'legacy-wallet', p_legacy_wallet_id::text, source_leg_id,
                    'legacy-shadow-v1', provider_reference, btrim(p_snapshot_hash), 1, 1,
                    p_actor_id, p_tenant_id, NULL, p_policy_version, p_hard_units, p_posted_at, NULL);
                INSERT INTO public.economy_source_stamp_events (
                    "Id", "SourceStampId", "Sequence", "State", "EvidenceHash", "OccurredAt")
                VALUES (gen_random_uuid(), p_source_stamp_id, 1, 1, btrim(p_snapshot_hash), p_posted_at);
                INSERT INTO public.economy_funding_claims (
                    "SourceStampId", "WalletId", "Provider", "Environment", "ConnectedAccount",
                    "ProviderObject", "ProviderMonetaryLeg", "AuthoritativeUsdMinorUnits", "State",
                    "ObservedAt", "ConfirmedAt", "StateChangedAt", "PostingGroupId", "RootCreditLotId",
                    "CumulativeProviderReversalUnits", "Version")
                VALUES (p_source_stamp_id, p_wallet_id, 'legacy-shadow-v1', 'migration',
                    p_tenant_id::text, p_legacy_wallet_id::text, 'balance', p_hard_units, 1,
                    p_posted_at, NULL, p_posted_at, NULL, NULL, 0, 1);

                SELECT * INTO receipt
                FROM economy_private.confirm_observed_hard_coin_top_up_v1(
                    p_capability_id, p_actor_id, p_tenant_id, p_posting_id, p_idempotency_key,
                    1, 1, 1, p_policy_version, p_reserve_version, p_risk_decision_id,
                    p_risk_operation_fingerprint, p_expected_counter_version, p_source_stamp_id,
                    p_snapshot_hash, p_posted_at, lines, 1, p_credit_lot_id,
                    confirmation_hash, p_capability_receipt_hash);
                IF receipt.duplicate THEN
                    RAISE EXCEPTION 'unexpected duplicate during initial legacy backfill'
                        USING ERRCODE = '40001';
                END IF;

                UPDATE public.economy_legacy_shadow_wallets
                SET "State" = 3,
                    "JournalSequence" = receipt.journal_sequence,
                    "JournalHash" = receipt.journal_hash,
                    "ObservedAt" = p_posted_at,
                    "PostedAt" = p_posted_at,
                    "Version" = "Version" + 1
                WHERE "Id" = shadow_item."Id" AND "State" = 1
                  AND "Version" = shadow_item."Version";
                IF NOT FOUND THEN
                    RAISE EXCEPTION 'legacy shadow item became stale before posting was bound'
                        USING ERRCODE = '40001';
                END IF;

                posting_id := receipt.posting_id;
                journal_sequence := receipt.journal_sequence;
                journal_hash := receipt.journal_hash;
                duplicate := false;
                RETURN NEXT;
            END
            $function$;

            CREATE OR REPLACE FUNCTION economy_private.guard_legacy_financial_mutation_v1()
            RETURNS trigger
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
            DECLARE
                payload jsonb;
                tenant_id uuid;
                legacy_wallet_id uuid;
            BEGIN
                payload := CASE WHEN TG_OP = 'DELETE' THEN to_jsonb(OLD) ELSE to_jsonb(NEW) END;
                tenant_id := NULLIF(payload->>'TenantId', '')::uuid;
                IF TG_TABLE_NAME = 'wallet_transactions' AND tenant_id IS NULL THEN
                    legacy_wallet_id := NULLIF(payload->>'WalletId', '')::uuid;
                    SELECT wallet."TenantId" INTO tenant_id
                    FROM public.user_wallets wallet WHERE wallet."Id" = legacy_wallet_id;
                END IF;
                IF tenant_id IS NOT NULL AND EXISTS (
                    SELECT 1 FROM public.economy_legacy_cutovers cutover
                    WHERE cutover."TenantId" = tenant_id AND cutover."State" = 3) THEN
                    RAISE EXCEPTION 'legacy financial writes are disabled after Economy cutover'
                        USING ERRCODE = '55000';
                END IF;
                IF TG_OP = 'DELETE' THEN
                    RETURN OLD;
                END IF;
                RETURN NEW;
            END
            $function$;

            DROP TRIGGER IF EXISTS guard_economy_cutover_user_wallets ON public.user_wallets;
            CREATE TRIGGER guard_economy_cutover_user_wallets
                BEFORE INSERT OR UPDATE OR DELETE ON public.user_wallets
                FOR EACH ROW EXECUTE FUNCTION economy_private.guard_legacy_financial_mutation_v1();
            DROP TRIGGER IF EXISTS guard_economy_cutover_wallet_transactions ON public.wallet_transactions;
            CREATE TRIGGER guard_economy_cutover_wallet_transactions
                BEFORE INSERT OR UPDATE OR DELETE ON public.wallet_transactions
                FOR EACH ROW EXECUTE FUNCTION economy_private.guard_legacy_financial_mutation_v1();

            DO $financial_ledger_guard$
            BEGIN
                IF to_regclass('public.financial_ledger_entries') IS NOT NULL
                   AND EXISTS (
                       SELECT 1 FROM information_schema.columns
                       WHERE table_schema = 'public' AND table_name = 'financial_ledger_entries'
                         AND column_name = 'TenantId') THEN
                    EXECUTE 'DROP TRIGGER IF EXISTS guard_economy_cutover_financial_ledger ON public.financial_ledger_entries';
                    EXECUTE 'CREATE TRIGGER guard_economy_cutover_financial_ledger '
                         || 'BEFORE INSERT OR UPDATE OR DELETE ON public.financial_ledger_entries '
                         || 'FOR EACH ROW EXECUTE FUNCTION economy_private.guard_legacy_financial_mutation_v1()';
                END IF;
            END
            $financial_ledger_guard$;

            CREATE TRIGGER deny_economy_legacy_cutover_audit_mutation
                BEFORE UPDATE OR DELETE ON public.economy_legacy_cutover_audit
                FOR EACH ROW EXECUTE FUNCTION economy_private.deny_immutable_mutation_v1();

            ALTER FUNCTION economy_private.provision_economy_wallet_v1(uuid,uuid,timestamptz)
                OWNER TO gameguild_economy_procedure_owner;
            ALTER FUNCTION economy_private.post_legacy_balance_backfill_v1(
                uuid,uuid,uuid,uuid,text,bigint,bigint,uuid,text,bigint,uuid,uuid,uuid,uuid,
                bigint,text,uuid,text,bigint,text,text,text,text,timestamptz)
                OWNER TO gameguild_economy_procedure_owner;
            ALTER FUNCTION economy_private.guard_legacy_financial_mutation_v1()
                OWNER TO gameguild_economy_procedure_owner;
            REVOKE ALL ON FUNCTION economy_private.provision_economy_wallet_v1(uuid,uuid,timestamptz)
                FROM PUBLIC;
            REVOKE ALL ON FUNCTION economy_private.post_legacy_balance_backfill_v1(
                uuid,uuid,uuid,uuid,text,bigint,bigint,uuid,text,bigint,uuid,uuid,uuid,uuid,
                bigint,text,uuid,text,bigint,text,text,text,text,timestamptz) FROM PUBLIC;
            REVOKE ALL ON FUNCTION economy_private.guard_legacy_financial_mutation_v1() FROM PUBLIC;
            GRANT EXECUTE ON FUNCTION economy_private.provision_economy_wallet_v1(uuid,uuid,timestamptz),
                economy_private.post_legacy_balance_backfill_v1(
                    uuid,uuid,uuid,uuid,text,bigint,bigint,uuid,text,bigint,uuid,uuid,uuid,uuid,
                    bigint,text,uuid,text,bigint,text,text,text,text,timestamptz)
                TO gameguild_economy_writer;
            GRANT SELECT ON TABLE
                public.economy_capability_receipts,
                public.economy_capability_receipt_consumptions,
                public.economy_kill_switches
                TO gameguild_economy_procedure_owner;

            DO $legacy_grants$
            DECLARE relation_name text;
            BEGIN
                FOREACH relation_name IN ARRAY ARRAY[
                    'economy_legacy_shadow_batches', 'economy_legacy_shadow_wallets',
                    'economy_legacy_cutovers', 'economy_legacy_cutover_audit']
                LOOP
                    EXECUTE format('REVOKE ALL ON TABLE public.%I FROM PUBLIC', relation_name);
                    EXECUTE format('REVOKE ALL ON TABLE public.%I FROM gameguild_economy_writer', relation_name);
                    EXECUTE format('GRANT SELECT ON TABLE public.%I TO gameguild_economy_runtime', relation_name);
                    EXECUTE format('GRANT SELECT, INSERT, UPDATE ON TABLE public.%I TO gameguild_economy_procedure_owner', relation_name);
                    EXECUTE format('GRANT ALL ON TABLE public.%I TO gameguild_economy_migration', relation_name);
                END LOOP;
            END
            $legacy_grants$;
            """);
    }

    private static void RemoveLegacyShadowSecurity(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS guard_economy_cutover_user_wallets ON public.user_wallets;
            DROP TRIGGER IF EXISTS guard_economy_cutover_wallet_transactions ON public.wallet_transactions;
            DO $financial_ledger_guard$
            BEGIN
                IF to_regclass('public.financial_ledger_entries') IS NOT NULL THEN
                    EXECUTE 'DROP TRIGGER IF EXISTS guard_economy_cutover_financial_ledger ON public.financial_ledger_entries';
                END IF;
            END
            $financial_ledger_guard$;
            DROP TRIGGER IF EXISTS deny_economy_legacy_cutover_audit_mutation
                ON public.economy_legacy_cutover_audit;
            REVOKE SELECT ON TABLE
                public.economy_capability_receipts,
                public.economy_capability_receipt_consumptions,
                public.economy_kill_switches
                FROM gameguild_economy_procedure_owner;
            DROP FUNCTION IF EXISTS economy_private.guard_legacy_financial_mutation_v1();
            DROP FUNCTION IF EXISTS economy_private.post_legacy_balance_backfill_v1(
                uuid,uuid,uuid,uuid,text,bigint,bigint,uuid,text,bigint,uuid,uuid,uuid,uuid,
                bigint,text,uuid,text,bigint,text,text,text,text,timestamptz);
            DROP FUNCTION IF EXISTS economy_private.provision_economy_wallet_v1(uuid,uuid,timestamptz);
            DELETE FROM public.economy_registered_capabilities
            WHERE "Name" = 'legacy-balance-backfill';
            """);
    }
}
