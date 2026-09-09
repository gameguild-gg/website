using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;

namespace GameGuild.API.Database.Migrations;

/// <inheritdoc />
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260824180312_AddEconomyProductionPlatform")]
public class AddEconomyProductionPlatform : Migration
{
	private static void InstallAdRewardIssuanceWriter(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("CREATE OR REPLACE FUNCTION economy_private.post_ad_reward_issuance_v1(\n    p_capability_id uuid,\n    p_actor_id uuid,\n    p_tenant_id uuid,\n    p_posting_id uuid,\n    p_idempotency_key text,\n    p_policy_version bigint,\n    p_reserve_version bigint,\n    p_risk_decision_id uuid,\n    p_risk_operation_fingerprint text,\n    p_expected_counter_version bigint,\n    p_source_stamp_id uuid,\n    p_output_lot_id uuid,\n    p_wallet_id uuid,\n    p_soft_units bigint,\n    p_network text,\n    p_provider_event_reference text,\n    p_evidence_hash text,\n    p_issued_at timestamptz,\n    p_capability_receipt_hash text)\nRETURNS TABLE(posting_id uuid, journal_sequence bigint, journal_hash text, duplicate boolean)\nLANGUAGE plpgsql\nSECURITY DEFINER\nSET search_path = pg_catalog, economy_private\nAS $function$\nDECLARE\n    reserve_account_id uuid;\n    liability_account_id uuid;\n    -- Stable line identities are part of the registered writer request\n    -- hash, so an idempotent replay must reconstruct the same payload.\n    reserve_line_id uuid := p_source_stamp_id;\n    liability_line_id uuid := p_output_lot_id;\n    next_sequence bigint;\n    lines jsonb;\n    receipt record;\n    existing_request_hash text;\n    request_hash text;\n    outbox_payload text;\nBEGIN\n    IF p_capability_id IS NULL OR p_actor_id IS NULL OR p_tenant_id IS NULL\n       OR p_posting_id IS NULL OR p_source_stamp_id IS NULL OR p_output_lot_id IS NULL\n       OR p_wallet_id IS NULL OR p_risk_decision_id IS NULL OR p_soft_units <= 0\n       OR p_policy_version <= 0 OR p_reserve_version <= 0 OR p_expected_counter_version <= 0\n       OR p_issued_at IS NULL OR length(btrim(COALESCE(p_idempotency_key, ''))) = 0\n       OR length(btrim(COALESCE(p_network, ''))) = 0\n       OR length(btrim(COALESCE(p_provider_event_reference, ''))) = 0\n       OR length(btrim(COALESCE(p_evidence_hash, ''))) = 0\n       OR length(btrim(COALESCE(p_capability_receipt_hash, ''))) = 0\n       OR length(p_capability_receipt_hash) > 128 THEN\n        RAISE EXCEPTION 'ad reward issuance arguments are invalid' USING ERRCODE = '22023';\n    END IF;\n\n    PERFORM 1 FROM public.economy_wallets wallet\n    WHERE wallet.\"Id\" = p_wallet_id AND wallet.\"TenantId\" = p_tenant_id AND wallet.\"State\" = 1\n    FOR SHARE;\n    IF NOT FOUND THEN\n        RAISE EXCEPTION 'ad reward wallet is absent, cross-tenant, or inactive' USING ERRCODE = '23503';\n    END IF;\n\n    PERFORM 1\n    FROM public.economy_capability_receipts capability_receipt\n    JOIN public.economy_capability_receipt_consumptions consumption\n      ON consumption.\"ReceiptId\" = capability_receipt.\"Id\"\n    WHERE capability_receipt.\"ReceiptHash\" = btrim(p_capability_receipt_hash)\n      AND capability_receipt.\"TenantId\" = p_tenant_id\n      AND capability_receipt.\"ActorId\" = p_actor_id\n      AND capability_receipt.\"Capability\" = 5\n      AND capability_receipt.\"PolicyVersion\" = p_policy_version\n      AND capability_receipt.\"ReserveVersion\" = p_reserve_version\n      AND capability_receipt.\"RiskDecisionId\" = p_risk_decision_id\n      AND capability_receipt.\"OperationFingerprint\" = btrim(p_risk_operation_fingerprint)\n      AND capability_receipt.\"IssuedAt\" <= p_issued_at\n      AND capability_receipt.\"ExpiresAt\" > p_issued_at\n      AND consumption.\"TenantId\" = p_tenant_id\n      AND consumption.\"ActorId\" = p_actor_id\n      AND consumption.\"OperationFingerprint\" = btrim(p_risk_operation_fingerprint)\n      AND consumption.\"KillSwitchEpoch\" = capability_receipt.\"KillSwitchEpoch\"\n      AND NOT EXISTS (\n          SELECT 1 FROM public.economy_kill_switches kill_switch\n          WHERE kill_switch.\"IsActive\"\n            AND (kill_switch.\"TenantId\" IS NULL OR kill_switch.\"TenantId\" = p_tenant_id)\n            AND (kill_switch.\"Capability\" IS NULL OR kill_switch.\"Capability\" = 5))\n    FOR SHARE OF capability_receipt, consumption;\n    IF NOT FOUND THEN\n        RAISE EXCEPTION 'ad reward capability receipt is absent, stale, or mismatched'\n            USING ERRCODE = '42501';\n    END IF;\n\n    SELECT account.\"Id\" INTO reserve_account_id\n    FROM public.economy_accounts account\n    WHERE account.\"WalletId\" IS NULL AND account.\"Code\" = 6 AND account.\"Currency\" = 2\n    FOR SHARE;\n    SELECT account.\"Id\" INTO liability_account_id\n    FROM public.economy_accounts account\n    WHERE account.\"WalletId\" = p_wallet_id AND account.\"Code\" = 4 AND account.\"Currency\" = 2\n      AND account.\"Provenance\" = 4\n    FOR SHARE;\n    IF reserve_account_id IS NULL OR liability_account_id IS NULL THEN\n        RAISE EXCEPTION 'ad reward ledger accounts are not provisioned' USING ERRCODE = '23503';\n    END IF;\n\n    lines := jsonb_build_array(\n        jsonb_build_object(\n            'id', reserve_line_id, 'account_id', reserve_account_id,\n            'account_code', 6, 'wallet_id', '', 'credit_lot_id', '',\n            'side', 1, 'currency', 2, 'amount_units', p_soft_units, 'provenance', ''),\n        jsonb_build_object(\n            'id', liability_line_id, 'account_id', liability_account_id,\n            'account_code', 4, 'wallet_id', p_wallet_id, 'credit_lot_id', p_output_lot_id,\n            'side', 2, 'currency', 2, 'amount_units', p_soft_units, 'provenance', 4));\n\n    request_hash := encode(public.digest(convert_to(jsonb_build_object(\n        'capabilityId', p_capability_id,\n        'actorId', p_actor_id,\n        'tenantId', p_tenant_id,\n        'postingId', p_posting_id,\n        'idempotencyKey', p_idempotency_key,\n        'templateKind', 21,\n        'templateVersion', 1,\n        'authority', 3,\n        'policyVersion', p_policy_version,\n        'reserveVersion', p_reserve_version,\n        'riskDecisionId', p_risk_decision_id,\n        'riskOperationFingerprint', p_risk_operation_fingerprint,\n        'counterVersion', p_expected_counter_version,\n        'sourceStampId', p_source_stamp_id,\n        'sourceEvidenceHash', p_evidence_hash,\n        'requestedAt', p_issued_at,\n        'lines', lines,\n        'allocations', '[]'::jsonb,\n        'rootRanges', '[]'::jsonb,\n        'reversalEpochs', '[]'::jsonb,\n        'dispatchSnapshotHash', p_capability_receipt_hash)::text, 'UTF8'), 'sha256'), 'hex');\n\n    SELECT posting.\"Id\", entry.\"Sequence\", entry.\"Hash\", idempotency.\"RequestHash\"\n    INTO posting_id, journal_sequence, journal_hash, existing_request_hash\n    FROM public.economy_posting_groups posting\n    JOIN public.economy_journal_entries entry ON entry.\"PostingGroupId\" = posting.\"Id\"\n    JOIN public.economy_idempotency_records idempotency ON idempotency.\"PostingGroupId\" = posting.\"Id\"\n    WHERE posting.\"IdempotencyKey\" = p_idempotency_key;\n    IF FOUND THEN\n        IF posting_id <> p_posting_id OR existing_request_hash <> request_hash\n           OR NOT EXISTS (\n               SELECT 1 FROM public.economy_source_stamps source\n               WHERE source.\"Id\" = p_source_stamp_id\n                 AND source.\"PostingReferenceId\" = p_posting_id\n                 AND source.\"TenantId\" = p_tenant_id\n                 AND source.\"ActorId\" = p_actor_id\n                 AND source.\"EvidenceHash\" = btrim(p_evidence_hash)\n                 AND source.\"AuthoritativeUnits\" = p_soft_units)\n           OR NOT EXISTS (\n               SELECT 1 FROM public.economy_credit_lots lot\n               WHERE lot.\"Id\" = p_output_lot_id AND lot.\"WalletId\" = p_wallet_id\n                 AND lot.\"RootSourceStampId\" = p_source_stamp_id\n                 AND lot.\"Currency\" = 2 AND lot.\"AmountUnits\" = p_soft_units\n                 AND lot.\"Provenance\" = 4) THEN\n            RAISE EXCEPTION 'ad reward idempotency key is bound to another issuance' USING ERRCODE = '23505';\n        END IF;\n        duplicate := true;\n        RETURN NEXT;\n        RETURN;\n    END IF;\n\n    INSERT INTO public.economy_chain_head (\"Id\", \"Sequence\", \"Hash\", \"UpdatedAt\")\n    VALUES (1, 0, repeat('0', 64), p_issued_at)\n    ON CONFLICT (\"Id\") DO NOTHING;\n    SELECT \"Sequence\" + 1 INTO next_sequence\n    FROM public.economy_chain_head WHERE \"Id\" = 1 FOR UPDATE;\n\n    INSERT INTO public.economy_source_stamps (\n        \"Id\", \"SourceKind\", \"InternalSourceId\", \"SourceLegId\", \"Provider\", \"ProviderReference\",\n        \"EvidenceHash\", \"Provenance\", \"State\", \"ActorId\", \"TenantId\", \"PostingReferenceId\",\n        \"PolicyVersion\", \"AuthoritativeUnits\", \"ObservedAt\", \"ConfirmedAt\")\n    VALUES (\n        p_source_stamp_id, 'ad-reward', btrim(p_provider_event_reference),\n        encode(public.digest(convert_to(concat_ws('|', p_network, p_provider_event_reference), 'UTF8'), 'sha256'), 'hex'),\n        btrim(p_network), btrim(p_provider_event_reference), btrim(p_evidence_hash), 4, 2,\n        p_actor_id, p_tenant_id, p_posting_id, p_policy_version, p_soft_units, p_issued_at, p_issued_at);\n\n    INSERT INTO public.economy_source_stamp_events (\n        \"Id\", \"SourceStampId\", \"Sequence\", \"State\", \"EvidenceHash\", \"OccurredAt\")\n    VALUES (gen_random_uuid(), p_source_stamp_id, 1, 2, btrim(p_evidence_hash), p_issued_at);\n\n    INSERT INTO public.economy_credit_lots (\n        \"Id\", \"WalletId\", \"RootSourceStampId\", \"Currency\", \"AmountUnits\", \"Provenance\",\n        \"CreditedAt\", \"ConfirmedAt\", \"OriginalMaturesAt\", \"CashOutEligible\", \"JournalSequence\", \"State\", \"ReversalEpoch\")\n    VALUES (p_output_lot_id, p_wallet_id, p_source_stamp_id, 2, p_soft_units, 4,\n        p_issued_at, p_issued_at, p_issued_at, false, next_sequence, 1, 0);\n\n    INSERT INTO public.economy_root_reversal_states (\n        \"RootSourceStampId\", \"Epoch\", \"CumulativeProviderUnits\", \"ReversedUnits\", \"State\", \"TargetedRanges\", \"UpdatedAt\")\n    VALUES (p_source_stamp_id, 0, 0, 0, 'active', '[]'::jsonb, p_issued_at);\n\n    INSERT INTO public.economy_fragment_root_ranges (\n        \"Id\", \"RootSourceStampId\", \"CreditLotId\", \"EntryAllocationId\", \"StartInclusive\", \"EndExclusive\", \"ReversalEpoch\")\n    VALUES (gen_random_uuid(), p_source_stamp_id, p_output_lot_id, NULL, 0, p_soft_units, 0);\n\n    SELECT * INTO receipt\n    FROM economy_private.post_registered_posting_v1(\n        p_capability_id, p_actor_id, p_tenant_id, p_posting_id, p_idempotency_key,\n        21, 1, 3, p_policy_version, p_reserve_version, p_risk_decision_id,\n        p_risk_operation_fingerprint, p_expected_counter_version, p_source_stamp_id,\n        p_evidence_hash, p_issued_at, lines, '[]'::jsonb, '[]'::jsonb, '[]'::jsonb,\n        p_capability_receipt_hash);\n    IF receipt.duplicate THEN\n        RAISE EXCEPTION 'unexpected duplicate during ad reward issuance' USING ERRCODE = '40001';\n    END IF;\n\n    PERFORM economy_private.rebuild_wallet_projection_v1(p_wallet_id, p_issued_at);\n\n    outbox_payload := json_build_object(\n        'PostingId', p_posting_id, 'Hash', receipt.journal_hash,\n        'RecordedAt', p_issued_at, 'JournalLineIds', json_build_array(reserve_line_id, liability_line_id))::text;\n    INSERT INTO public.economy_outbox_messages (\n        \"Id\", \"PostingGroupId\", \"Type\", \"Payload\", \"PayloadHash\", \"OccurredAt\")\n    VALUES (gen_random_uuid(), p_posting_id, 'economy.posting.accepted.v1', outbox_payload,\n        encode(public.digest(convert_to(outbox_payload, 'UTF8'), 'sha256'), 'hex'), p_issued_at);\n\n    posting_id := receipt.posting_id;\n    journal_sequence := receipt.journal_sequence;\n    journal_hash := receipt.journal_hash;\n    duplicate := false;\n    RETURN NEXT;\nEND\n$function$;\n\nALTER FUNCTION economy_private.post_ad_reward_issuance_v1(\n    uuid,uuid,uuid,uuid,text,bigint,bigint,uuid,text,bigint,uuid,uuid,uuid,bigint,text,text,text,timestamptz,text)\n    OWNER TO gameguild_economy_procedure_owner;\nREVOKE ALL ON FUNCTION economy_private.post_ad_reward_issuance_v1(\n    uuid,uuid,uuid,uuid,text,bigint,bigint,uuid,text,bigint,uuid,uuid,uuid,bigint,text,text,text,timestamptz,text)\n    FROM PUBLIC;\nGRANT EXECUTE ON FUNCTION economy_private.post_ad_reward_issuance_v1(\n    uuid,uuid,uuid,uuid,text,bigint,bigint,uuid,text,bigint,uuid,uuid,uuid,bigint,text,text,text,timestamptz,text)\n    TO gameguild_economy_writer;");
	}

	private static void RemoveAdRewardIssuanceWriter(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("DROP FUNCTION IF EXISTS economy_private.post_ad_reward_issuance_v1(\n    uuid,uuid,uuid,uuid,text,bigint,bigint,uuid,text,bigint,uuid,uuid,uuid,bigint,text,text,text,timestamptz,text);");
	}

	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropIndex("ux_economy_risk_review_cases_decision", "economy_risk_review_cases");
		migrationBuilder.DropIndex("ux_economy_risk_counters_scope_window", "economy_risk_counters");
		migrationBuilder.DropIndex("ux_economy_protected_change_cooldowns_subject_kind", "economy_protected_change_cooldowns");
		migrationBuilder.DropIndex("IX_economy_marketplace_settlements_BuyerId_SettledAt", "economy_marketplace_settlements");
		migrationBuilder.DropIndex("IX_economy_marketplace_settlements_IdempotencyKey", "economy_marketplace_settlements");
		migrationBuilder.DropIndex("IX_economy_marketplace_settlements_OrderId", "economy_marketplace_settlements");
		migrationBuilder.DropIndex("IX_economy_marketplace_settlements_SellerId_SettledAt", "economy_marketplace_settlements");
		migrationBuilder.DropIndex("IX_economy_marketplace_refunds_IdempotencyKey", "economy_marketplace_refunds");
		migrationBuilder.DropIndex("IX_economy_marketplace_refunds_SettlementId_RefundedAt", "economy_marketplace_refunds");
		migrationBuilder.DropIndex("IX_economy_marketplace_refund_legs_SettlementId_Currency", "economy_marketplace_refund_legs");
		migrationBuilder.DropPrimaryKey("PK_economy_marketplace_currency_policy_versions", "economy_marketplace_currency_policy_versions");
		migrationBuilder.DropIndex("IX_economy_marketplace_currency_policy_versions_ProductId_Effe~", "economy_marketplace_currency_policy_versions");
		migrationBuilder.DropIndex("IX_economy_ad_reward_reconciliations_Network_ReportId_Version", "economy_ad_reward_reconciliations");
		migrationBuilder.DropPrimaryKey("PK_economy_ad_reward_accumulators", "economy_ad_reward_accumulators");
		migrationBuilder.DropIndex("IX_economy_ad_provider_reports_Network_BatchId_Version", "economy_ad_provider_reports");
		migrationBuilder.DropIndex("IX_economy_ad_provider_reports_Network_ReportId_Version", "economy_ad_provider_reports");
		migrationBuilder.DropPrimaryKey("PK_economy_ad_network_policy_versions", "economy_ad_network_policy_versions");
		migrationBuilder.DropIndex("IX_economy_ad_network_policy_versions_Network_EffectiveAt_Expi~", "economy_ad_network_policy_versions");
		migrationBuilder.DropCheckConstraint("ck_economy_ad_network_policy_versions_values", "economy_ad_network_policy_versions");
		object defaultValue = new Guid("00000000-0000-0000-0000-000000000000");
		migrationBuilder.AddColumn<Guid>("TenantId", "economy_risk_review_cases", "uuid", null, null, rowVersion: false, null, nullable: false, defaultValue);
		defaultValue = new Guid("00000000-0000-0000-0000-000000000000");
		migrationBuilder.AddColumn<Guid>("TenantId", "economy_risk_counters", "uuid", null, null, rowVersion: false, null, nullable: false, defaultValue);
		migrationBuilder.AddColumn<DateTimeOffset>("ConsumedAt", "economy_risk_counter_reservations", "timestamp with time zone", null, null, rowVersion: false, null, nullable: true);
		defaultValue = new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0));
		migrationBuilder.AddColumn<DateTimeOffset>("ExpiresAt", "economy_risk_counter_reservations", "timestamp with time zone", null, null, rowVersion: false, null, nullable: false, defaultValue);
		int? maxLength = 128;
		migrationBuilder.AddColumn<string>("InputFingerprint", "economy_risk_counter_reservations", "character varying(128)", null, maxLength, rowVersion: false, null, nullable: false, "");
		migrationBuilder.AddColumn<DateTimeOffset>("ReleasedAt", "economy_risk_counter_reservations", "timestamp with time zone", null, null, rowVersion: false, null, nullable: true);
		defaultValue = new Guid("00000000-0000-0000-0000-000000000000");
		migrationBuilder.AddColumn<Guid>("ReservationGroupId", "economy_risk_counter_reservations", "uuid", null, null, rowVersion: false, null, nullable: false, defaultValue);
		migrationBuilder.AddColumn<int>("Status", "economy_risk_counter_reservations", "integer", null, null, rowVersion: false, null, nullable: false, 0);
		defaultValue = new Guid("00000000-0000-0000-0000-000000000000");
		migrationBuilder.AddColumn<Guid>("TenantId", "economy_protected_change_cooldowns", "uuid", null, null, rowVersion: false, null, nullable: false, defaultValue);
		maxLength = 128;
		migrationBuilder.AddColumn<string>("CapabilityReceiptHash", "economy_marketplace_settlements", "character varying(128)", null, maxLength, rowVersion: false, null, nullable: false, "");
		defaultValue = new Guid("00000000-0000-0000-0000-000000000000");
		migrationBuilder.AddColumn<Guid>("CapabilityReceiptId", "economy_marketplace_settlements", "uuid", null, null, rowVersion: false, null, nullable: false, defaultValue);
		migrationBuilder.AddColumn<int>("EntitlementStatus", "economy_marketplace_settlements", "integer", null, null, rowVersion: false, null, nullable: false, 0);
		migrationBuilder.AddColumn<string>("EvidenceHashes", "economy_marketplace_settlements", "jsonb", null, null, rowVersion: false, null, nullable: false, "");
		maxLength = 3;
		migrationBuilder.AddColumn<string>("FiatCurrencySnapshot", "economy_marketplace_settlements", "character varying(3)", null, maxLength, rowVersion: false, null, nullable: false, "");
		maxLength = 128;
		migrationBuilder.AddColumn<string>("JournalHash", "economy_marketplace_settlements", "character varying(128)", null, maxLength, rowVersion: false, null, nullable: false, "");
		migrationBuilder.AddColumn<long>("JournalSequence", "economy_marketplace_settlements", "bigint", null, null, rowVersion: false, null, nullable: false, 0L);
		maxLength = 16;
		migrationBuilder.AddColumn<string>("JurisdictionCode", "economy_marketplace_settlements", "character varying(16)", null, maxLength, rowVersion: false, null, nullable: false, "");
		migrationBuilder.AddColumn<long>("KillSwitchEpoch", "economy_marketplace_settlements", "bigint", null, null, rowVersion: false, null, nullable: false, 0L);
		defaultValue = new Guid("00000000-0000-0000-0000-000000000000");
		migrationBuilder.AddColumn<Guid>("OrderLineItemId", "economy_marketplace_settlements", "uuid", null, null, rowVersion: false, null, nullable: false, defaultValue);
		maxLength = 128;
		migrationBuilder.AddColumn<string>("OrderSnapshotHash", "economy_marketplace_settlements", "character varying(128)", null, maxLength, rowVersion: false, null, nullable: false, "");
		defaultValue = new Guid("00000000-0000-0000-0000-000000000000");
		migrationBuilder.AddColumn<Guid>("PostingId", "economy_marketplace_settlements", "uuid", null, null, rowVersion: false, null, nullable: false, defaultValue);
		migrationBuilder.AddColumn<int>("PriceVersionSnapshot", "economy_marketplace_settlements", "integer", null, null, rowVersion: false, null, nullable: false, 0);
		defaultValue = new Guid("00000000-0000-0000-0000-000000000000");
		migrationBuilder.AddColumn<Guid>("ProductPricingVersionId", "economy_marketplace_settlements", "uuid", null, null, rowVersion: false, null, nullable: false, defaultValue);
		migrationBuilder.AddColumn<int>("Quantity", "economy_marketplace_settlements", "integer", null, null, rowVersion: false, null, nullable: false, 0);
		migrationBuilder.AddColumn<int>("RefundedQuantity", "economy_marketplace_settlements", "integer", null, null, rowVersion: false, null, nullable: false, 0);
		migrationBuilder.AddColumn<long>("ReserveVersion", "economy_marketplace_settlements", "bigint", null, null, rowVersion: false, null, nullable: false, 0L);
		defaultValue = new Guid("00000000-0000-0000-0000-000000000000");
		migrationBuilder.AddColumn<Guid>("RiskDecisionId", "economy_marketplace_settlements", "uuid", null, null, rowVersion: false, null, nullable: false, defaultValue);
		defaultValue = new Guid("00000000-0000-0000-0000-000000000000");
		migrationBuilder.AddColumn<Guid>("TenantId", "economy_marketplace_settlements", "uuid", null, null, rowVersion: false, null, nullable: false, defaultValue);
		migrationBuilder.AddColumn<decimal>("UnitPriceSnapshot", "economy_marketplace_settlements", "numeric", null, null, rowVersion: false, null, nullable: false, 0m);
		migrationBuilder.AddColumn<long>("RemainingUnits", "economy_marketplace_settlement_credits", "bigint", null, null, rowVersion: false, null, nullable: false, 0L);
		maxLength = 128;
		migrationBuilder.AddColumn<string>("CapabilityReceiptHash", "economy_marketplace_refunds", "character varying(128)", null, maxLength, rowVersion: false, null, nullable: false, "");
		defaultValue = new Guid("00000000-0000-0000-0000-000000000000");
		migrationBuilder.AddColumn<Guid>("CapabilityReceiptId", "economy_marketplace_refunds", "uuid", null, null, rowVersion: false, null, nullable: false, defaultValue);
		migrationBuilder.AddColumn<string>("EvidenceHashes", "economy_marketplace_refunds", "jsonb", null, null, rowVersion: false, null, nullable: false, "");
		maxLength = 128;
		migrationBuilder.AddColumn<string>("JournalHash", "economy_marketplace_refunds", "character varying(128)", null, maxLength, rowVersion: false, null, nullable: false, "");
		maxLength = 16;
		migrationBuilder.AddColumn<string>("JurisdictionCode", "economy_marketplace_refunds", "character varying(16)", null, maxLength, rowVersion: false, null, nullable: false, "");
		migrationBuilder.AddColumn<long>("KillSwitchEpoch", "economy_marketplace_refunds", "bigint", null, null, rowVersion: false, null, nullable: false, 0L);
		migrationBuilder.AddColumn<long>("MarketplacePolicyVersion", "economy_marketplace_refunds", "bigint", null, null, rowVersion: false, null, nullable: false, 0L);
		migrationBuilder.AddColumn<long>("PolicyVersion", "economy_marketplace_refunds", "bigint", null, null, rowVersion: false, null, nullable: false, 0L);
		defaultValue = new Guid("00000000-0000-0000-0000-000000000000");
		migrationBuilder.AddColumn<Guid>("PostingId", "economy_marketplace_refunds", "uuid", null, null, rowVersion: false, null, nullable: false, defaultValue);
		migrationBuilder.AddColumn<int>("Quantity", "economy_marketplace_refunds", "integer", null, null, rowVersion: false, null, nullable: false, 0);
		maxLength = 100;
		migrationBuilder.AddColumn<string>("ReasonCode", "economy_marketplace_refunds", "character varying(100)", null, maxLength, rowVersion: false, null, nullable: false, "");
		maxLength = 128;
		migrationBuilder.AddColumn<string>("ReasonHash", "economy_marketplace_refunds", "character varying(128)", null, maxLength, rowVersion: false, null, nullable: false, "");
		migrationBuilder.AddColumn<int>("RefundedQuantity", "economy_marketplace_refunds", "integer", null, null, rowVersion: false, null, nullable: false, 0);
		migrationBuilder.AddColumn<long>("ReserveVersion", "economy_marketplace_refunds", "bigint", null, null, rowVersion: false, null, nullable: false, 0L);
		defaultValue = new Guid("00000000-0000-0000-0000-000000000000");
		migrationBuilder.AddColumn<Guid>("RiskDecisionId", "economy_marketplace_refunds", "uuid", null, null, rowVersion: false, null, nullable: false, defaultValue);
		defaultValue = new Guid("00000000-0000-0000-0000-000000000000");
		migrationBuilder.AddColumn<Guid>("TenantId", "economy_marketplace_refunds", "uuid", null, null, rowVersion: false, null, nullable: false, defaultValue);
		defaultValue = new Guid("00000000-0000-0000-0000-000000000000");
		migrationBuilder.AddColumn<Guid>("ReservationId", "economy_marketplace_funding_fragments", "uuid", null, null, rowVersion: false, null, nullable: false, defaultValue);
		defaultValue = new Guid("00000000-0000-0000-0000-000000000000");
		migrationBuilder.AddColumn<Guid>("TenantId", "economy_marketplace_currency_policy_versions", "uuid", null, null, rowVersion: false, null, nullable: false, defaultValue);
		defaultValue = new Guid("00000000-0000-0000-0000-000000000000");
		migrationBuilder.AddColumn<Guid>("ApprovedBy", "economy_marketplace_currency_policy_versions", "uuid", null, null, rowVersion: false, null, nullable: false, defaultValue);
		migrationBuilder.AddColumn<string>("CanonicalPayload", "economy_marketplace_currency_policy_versions", "text", null, null, rowVersion: false, null, nullable: false, "");
		defaultValue = new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0));
		migrationBuilder.AddColumn<DateTimeOffset>("ExpiresAt", "economy_marketplace_currency_policy_versions", "timestamp with time zone", null, null, rowVersion: false, null, nullable: false, defaultValue);
		maxLength = 256;
		migrationBuilder.AddColumn<string>("KeyId", "economy_marketplace_currency_policy_versions", "character varying(256)", null, maxLength, rowVersion: false, null, nullable: false, "");
		maxLength = 128;
		migrationBuilder.AddColumn<string>("PayloadHash", "economy_marketplace_currency_policy_versions", "character varying(128)", null, maxLength, rowVersion: false, null, nullable: false, "");
		defaultValue = new Guid("00000000-0000-0000-0000-000000000000");
		migrationBuilder.AddColumn<Guid>("PlatformFeeWalletId", "economy_marketplace_currency_policy_versions", "uuid", null, null, rowVersion: false, null, nullable: false, defaultValue);
		defaultValue = new Guid("00000000-0000-0000-0000-000000000000");
		migrationBuilder.AddColumn<Guid>("ProposedBy", "economy_marketplace_currency_policy_versions", "uuid", null, null, rowVersion: false, null, nullable: false, defaultValue);
		defaultValue = new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0));
		migrationBuilder.AddColumn<DateTimeOffset>("PublishedAt", "economy_marketplace_currency_policy_versions", "timestamp with time zone", null, null, rowVersion: false, null, nullable: false, defaultValue);
		migrationBuilder.AddColumn<long>("RefundHoldTicks", "economy_marketplace_currency_policy_versions", "bigint", null, null, rowVersion: false, null, nullable: false, 0L);
		migrationBuilder.AddColumn<string>("Signature", "economy_marketplace_currency_policy_versions", "text", null, null, rowVersion: false, null, nullable: false, "");
		maxLength = 128;
		migrationBuilder.AddColumn<string>("CanonicalPayloadHash", "economy_journal_entries", "character varying(128)", null, maxLength, rowVersion: false, null, nullable: true);
		migrationBuilder.AddColumn<int>("HashAlgorithmVersion", "economy_journal_entries", "integer", null, null, rowVersion: false, null, nullable: false, 0);
		defaultValue = new Guid("00000000-0000-0000-0000-000000000000");
		migrationBuilder.AddColumn<Guid>("TenantId", "economy_ad_reward_reconciliations", "uuid", null, null, rowVersion: false, null, nullable: false, defaultValue);
		maxLength = 128;
		migrationBuilder.AddColumn<string>("CapabilityReceiptHash", "economy_ad_reward_completions", "character varying(128)", null, maxLength, rowVersion: false, null, nullable: true);
		migrationBuilder.AddColumn<Guid>("CapabilityReceiptId", "economy_ad_reward_completions", "uuid", null, null, rowVersion: false, null, nullable: true);
		maxLength = 128;
		migrationBuilder.AddColumn<string>("DestinationHash", "economy_ad_reward_completions", "character varying(128)", null, maxLength, rowVersion: false, null, nullable: true);
		migrationBuilder.AddColumn<string>("EvidenceHashes", "economy_ad_reward_completions", "jsonb", null, null, rowVersion: false, null, nullable: false, "");
		maxLength = 16;
		migrationBuilder.AddColumn<string>("JurisdictionCode", "economy_ad_reward_completions", "character varying(16)", null, maxLength, rowVersion: false, null, nullable: true);
		migrationBuilder.AddColumn<long>("KillSwitchEpoch", "economy_ad_reward_completions", "bigint", null, null, rowVersion: false, null, nullable: true);
		maxLength = 128;
		migrationBuilder.AddColumn<string>("ProviderHash", "economy_ad_reward_completions", "character varying(128)", null, maxLength, rowVersion: false, null, nullable: true);
		migrationBuilder.AddColumn<long>("ReserveVersion", "economy_ad_reward_completions", "bigint", null, null, rowVersion: false, null, nullable: true);
		migrationBuilder.AddColumn<Guid>("RiskDecisionId", "economy_ad_reward_completions", "uuid", null, null, rowVersion: false, null, nullable: true);
		defaultValue = new Guid("00000000-0000-0000-0000-000000000000");
		migrationBuilder.AddColumn<Guid>("TenantId", "economy_ad_reward_completions", "uuid", null, null, rowVersion: false, null, nullable: false, defaultValue);
		migrationBuilder.AddColumn<long>("Version", "economy_ad_reward_completions", "bigint", null, null, rowVersion: false, null, nullable: false, 0L);
		defaultValue = new Guid("00000000-0000-0000-0000-000000000000");
		migrationBuilder.AddColumn<Guid>("TenantId", "economy_ad_reward_budget_consumptions", "uuid", null, null, rowVersion: false, null, nullable: false, defaultValue);
		defaultValue = new Guid("00000000-0000-0000-0000-000000000000");
		migrationBuilder.AddColumn<Guid>("TenantId", "economy_ad_reward_attributions", "uuid", null, null, rowVersion: false, null, nullable: false, defaultValue);
		defaultValue = new Guid("00000000-0000-0000-0000-000000000000");
		migrationBuilder.AddColumn<Guid>("TenantId", "economy_ad_reward_accumulators", "uuid", null, null, rowVersion: false, null, nullable: false, defaultValue);
		maxLength = 128;
		migrationBuilder.AddColumn<string>("PayloadHash", "economy_ad_provider_reports", "character varying(128)", null, maxLength, rowVersion: false, null, nullable: false, "");
		migrationBuilder.AddColumn<DateTimeOffset>("ProcessedAt", "economy_ad_provider_reports", "timestamp with time zone", null, null, rowVersion: false, null, nullable: true);
		maxLength = 256;
		migrationBuilder.AddColumn<string>("ProcessingError", "economy_ad_provider_reports", "character varying(256)", null, maxLength, rowVersion: false, null, nullable: true);
		defaultValue = new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0));
		migrationBuilder.AddColumn<DateTimeOffset>("ReceivedAt", "economy_ad_provider_reports", "timestamp with time zone", null, null, rowVersion: false, null, nullable: false, defaultValue);
		migrationBuilder.AddColumn<bool>("SignatureVerified", "economy_ad_provider_reports", "boolean", null, null, rowVersion: false, null, nullable: false, false);
		defaultValue = new Guid("00000000-0000-0000-0000-000000000000");
		migrationBuilder.AddColumn<Guid>("TenantId", "economy_ad_provider_reports", "uuid", null, null, rowVersion: false, null, nullable: false, defaultValue);
		defaultValue = new Guid("00000000-0000-0000-0000-000000000000");
		migrationBuilder.AddColumn<Guid>("TenantId", "economy_ad_network_policy_versions", "uuid", null, null, rowVersion: false, null, nullable: false, defaultValue);
		defaultValue = new Guid("00000000-0000-0000-0000-000000000000");
		migrationBuilder.AddColumn<Guid>("ApprovedBy", "economy_ad_network_policy_versions", "uuid", null, null, rowVersion: false, null, nullable: false, defaultValue);
		migrationBuilder.AddColumn<long>("BudgetWindowTicks", "economy_ad_network_policy_versions", "bigint", null, null, rowVersion: false, null, nullable: false, 0L);
		migrationBuilder.AddColumn<string>("CanonicalPayload", "economy_ad_network_policy_versions", "text", null, null, rowVersion: false, null, nullable: false, "");
		migrationBuilder.AddColumn<long>("FundedLossBudgetUsdNanos", "economy_ad_network_policy_versions", "bigint", null, null, rowVersion: false, null, nullable: false, 0L);
		maxLength = 256;
		migrationBuilder.AddColumn<string>("KeyId", "economy_ad_network_policy_versions", "character varying(256)", null, maxLength, rowVersion: false, null, nullable: false, "");
		migrationBuilder.AddColumn<long>("MaximumAsnSoftUnits", "economy_ad_network_policy_versions", "bigint", null, null, rowVersion: false, null, nullable: false, 0L);
		migrationBuilder.AddColumn<long>("MaximumDeviceSoftUnits", "economy_ad_network_policy_versions", "bigint", null, null, rowVersion: false, null, nullable: false, 0L);
		migrationBuilder.AddColumn<long>("MaximumGlobalSoftUnits", "economy_ad_network_policy_versions", "bigint", null, null, rowVersion: false, null, nullable: false, 0L);
		migrationBuilder.AddColumn<long>("MaximumIpSoftUnits", "economy_ad_network_policy_versions", "bigint", null, null, rowVersion: false, null, nullable: false, 0L);
		migrationBuilder.AddColumn<long>("MaximumNetworkSoftUnits", "economy_ad_network_policy_versions", "bigint", null, null, rowVersion: false, null, nullable: false, 0L);
		migrationBuilder.AddColumn<long>("MaximumUserSoftUnits", "economy_ad_network_policy_versions", "bigint", null, null, rowVersion: false, null, nullable: false, 0L);
		maxLength = 128;
		migrationBuilder.AddColumn<string>("PayloadHash", "economy_ad_network_policy_versions", "character varying(128)", null, maxLength, rowVersion: false, null, nullable: false, "");
		defaultValue = new Guid("00000000-0000-0000-0000-000000000000");
		migrationBuilder.AddColumn<Guid>("ProposedBy", "economy_ad_network_policy_versions", "uuid", null, null, rowVersion: false, null, nullable: false, defaultValue);
		migrationBuilder.AddColumn<bool>("ProviderCertified", "economy_ad_network_policy_versions", "boolean", null, null, rowVersion: false, null, nullable: false, false);
		maxLength = 128;
		migrationBuilder.AddColumn<string>("ProviderHash", "economy_ad_network_policy_versions", "character varying(128)", null, maxLength, rowVersion: false, null, nullable: false, "");
		defaultValue = new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0));
		migrationBuilder.AddColumn<DateTimeOffset>("PublishedAt", "economy_ad_network_policy_versions", "timestamp with time zone", null, null, rowVersion: false, null, nullable: false, defaultValue);
		migrationBuilder.AddColumn<string>("Signature", "economy_ad_network_policy_versions", "text", null, null, rowVersion: false, null, nullable: false, "");
		migrationBuilder.AddPrimaryKey("PK_economy_marketplace_currency_policy_versions", "economy_marketplace_currency_policy_versions", new string[3] { "TenantId", "ProductId", "Version" });
		migrationBuilder.AddPrimaryKey("PK_economy_ad_reward_accumulators", "economy_ad_reward_accumulators", new string[3] { "TenantId", "WalletId", "Network" });
		migrationBuilder.AddPrimaryKey("PK_economy_ad_network_policy_versions", "economy_ad_network_policy_versions", new string[3] { "TenantId", "Network", "Version" });
		migrationBuilder.CreateTable("compliance_financial_crime_cases", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> tenantId = table.Column<Guid>("uuid");
			int? maxLength2 = 128;
			OperationBuilder<AddColumnOperation> subjectHash = table.Column<string>("character varying(128)", null, maxLength2);
			OperationBuilder<AddColumnOperation> state = table.Column<int>("integer");
			OperationBuilder<AddColumnOperation> assignedTo = table.Column<Guid>("uuid", null, null, rowVersion: false, null, nullable: true);
			OperationBuilder<AddColumnOperation> holdId = table.Column<Guid>("uuid");
			maxLength2 = 100;
			return new
			{
				Id = id,
				TenantId = tenantId,
				SubjectHash = subjectHash,
				State = state,
				AssignedTo = assignedTo,
				HoldId = holdId,
				ReasonCode = table.Column<string>("character varying(100)", null, maxLength2),
				Version = table.Column<long>("bigint"),
				OpenedAt = table.Column<DateTimeOffset>("timestamp with time zone"),
				UpdatedAt = table.Column<DateTimeOffset>("timestamp with time zone"),
				ClosedAt = table.Column<DateTimeOffset>("timestamp with time zone", null, null, rowVersion: false, null, nullable: true)
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_compliance_financial_crime_cases", x => x.Id);
			table.CheckConstraint("ck_financial_crime_cases_closed", "(\"State\" = 4 AND \"ClosedAt\" IS NOT NULL) OR (\"State\" <> 4 AND \"ClosedAt\" IS NULL)");
			table.CheckConstraint("ck_financial_crime_cases_version", "\"Version\" > 0");
		});
		migrationBuilder.CreateTable("compliance_sumsub_applicant_bindings", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> tenantId = table.Column<Guid>("uuid");
			int? maxLength2 = 128;
			OperationBuilder<AddColumnOperation> subjectHash = table.Column<string>("character varying(128)", null, maxLength2);
			maxLength2 = 256;
			OperationBuilder<AddColumnOperation> applicantId = table.Column<string>("character varying(256)", null, maxLength2);
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> externalUserIdHash = table.Column<string>("character varying(128)", null, maxLength2);
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> idempotencyKeyHash = table.Column<string>("character varying(128)", null, maxLength2);
			OperationBuilder<AddColumnOperation> state = table.Column<int>("integer");
			OperationBuilder<AddColumnOperation> evidenceVersion = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> lastProviderIssuedAt = table.Column<DateTimeOffset>("timestamp with time zone", null, null, rowVersion: false, null, nullable: true);
			maxLength2 = 256;
			return new
			{
				Id = id,
				TenantId = tenantId,
				SubjectHash = subjectHash,
				ApplicantId = applicantId,
				ExternalUserIdHash = externalUserIdHash,
				IdempotencyKeyHash = idempotencyKeyHash,
				State = state,
				EvidenceVersion = evidenceVersion,
				LastProviderIssuedAt = lastProviderIssuedAt,
				LastProviderEventId = table.Column<string>("character varying(256)", null, maxLength2, rowVersion: false, null, nullable: true),
				CreatedAt = table.Column<DateTimeOffset>("timestamp with time zone"),
				UpdatedAt = table.Column<DateTimeOffset>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_compliance_sumsub_applicant_bindings", x => x.Id);
			table.CheckConstraint("ck_compliance_sumsub_applicant_bindings_state", "\"State\" BETWEEN 1 AND 7");
			table.CheckConstraint("ck_compliance_sumsub_applicant_bindings_version", "\"EvidenceVersion\" >= 0");
		});
		migrationBuilder.CreateTable("compliance_sumsub_webhook_inbox", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			int? maxLength2 = 256;
			OperationBuilder<AddColumnOperation> providerEventId = table.Column<string>("character varying(256)", null, maxLength2);
			maxLength2 = 256;
			OperationBuilder<AddColumnOperation> applicantId = table.Column<string>("character varying(256)", null, maxLength2);
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> payloadHash = table.Column<string>("character varying(128)", null, maxLength2);
			maxLength2 = 2048;
			OperationBuilder<AddColumnOperation> rawObjectReference = table.Column<string>("character varying(2048)", null, maxLength2);
			OperationBuilder<AddColumnOperation> signatureVerified = table.Column<bool>("boolean");
			OperationBuilder<AddColumnOperation> issuedAt = table.Column<DateTimeOffset>("timestamp with time zone");
			OperationBuilder<AddColumnOperation> receivedAt = table.Column<DateTimeOffset>("timestamp with time zone");
			OperationBuilder<AddColumnOperation> processedAt = table.Column<DateTimeOffset>("timestamp with time zone", null, null, rowVersion: false, null, nullable: true);
			maxLength2 = 256;
			return new
			{
				Id = id,
				ProviderEventId = providerEventId,
				ApplicantId = applicantId,
				PayloadHash = payloadHash,
				RawObjectReference = rawObjectReference,
				SignatureVerified = signatureVerified,
				IssuedAt = issuedAt,
				ReceivedAt = receivedAt,
				ProcessedAt = processedAt,
				ProcessingError = table.Column<string>("character varying(256)", null, maxLength2, rowVersion: false, null, nullable: true)
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_compliance_sumsub_webhook_inbox", x => x.Id);
			table.CheckConstraint("ck_compliance_sumsub_webhook_inbox_time", "\"ReceivedAt\" >= \"IssuedAt\"");
		});
		migrationBuilder.CreateTable("economy_ad_reward_sessions", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> tenantId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> userId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> walletId = table.Column<Guid>("uuid");
			int? maxLength2 = 100;
			OperationBuilder<AddColumnOperation> network = table.Column<string>("character varying(100)", null, maxLength2);
			OperationBuilder<AddColumnOperation> policyVersion = table.Column<long>("bigint");
			maxLength2 = 256;
			OperationBuilder<AddColumnOperation> creativeId = table.Column<string>("character varying(256)", null, maxLength2);
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> deviceRiskHash = table.Column<string>("character varying(128)", null, maxLength2);
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> ipRiskHash = table.Column<string>("character varying(128)", null, maxLength2);
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> asnRiskHash = table.Column<string>("character varying(128)", null, maxLength2);
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> nonceHash = table.Column<string>("character varying(128)", null, maxLength2);
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> tokenHash = table.Column<string>("character varying(128)", null, maxLength2);
			maxLength2 = 256;
			OperationBuilder<AddColumnOperation> tokenKeyId = table.Column<string>("character varying(256)", null, maxLength2);
			OperationBuilder<AddColumnOperation> requiredDurationTicks = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> state = table.Column<int>("integer");
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> startIdempotencyKeyHash = table.Column<string>("character varying(128)", null, maxLength2);
			maxLength2 = 128;
			return new
			{
				Id = id,
				TenantId = tenantId,
				UserId = userId,
				WalletId = walletId,
				Network = network,
				PolicyVersion = policyVersion,
				CreativeId = creativeId,
				DeviceRiskHash = deviceRiskHash,
				IpRiskHash = ipRiskHash,
				AsnRiskHash = asnRiskHash,
				NonceHash = nonceHash,
				TokenHash = tokenHash,
				TokenKeyId = tokenKeyId,
				RequiredDurationTicks = requiredDurationTicks,
				State = state,
				StartIdempotencyKeyHash = startIdempotencyKeyHash,
				StartRequestHash = table.Column<string>("character varying(128)", null, maxLength2),
				IssuedAt = table.Column<DateTimeOffset>("timestamp with time zone"),
				ExpiresAt = table.Column<DateTimeOffset>("timestamp with time zone"),
				UpdatedAt = table.Column<DateTimeOffset>("timestamp with time zone"),
				Version = table.Column<long>("bigint")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_ad_reward_sessions", x => x.Id);
			table.CheckConstraint("ck_economy_ad_reward_sessions_state", "\"State\" BETWEEN 1 AND 7");
			table.CheckConstraint("ck_economy_ad_reward_sessions_values", "\"PolicyVersion\" > 0 AND \"RequiredDurationTicks\" > 0 AND \"Version\" > 0");
			table.CheckConstraint("ck_economy_ad_reward_sessions_window", "\"ExpiresAt\" > \"IssuedAt\" AND \"UpdatedAt\" >= \"IssuedAt\"");
		});
		migrationBuilder.CreateTable("economy_admin_withdrawal_dispatch_outbox", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> runId = table.Column<Guid>("uuid");
			int? maxLength2 = 256;
			OperationBuilder<AddColumnOperation> idempotencyKey = table.Column<string>("character varying(256)", null, maxLength2);
			OperationBuilder<AddColumnOperation> payload = table.Column<string>("jsonb");
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> payloadHash = table.Column<string>("character varying(128)", null, maxLength2);
			OperationBuilder<AddColumnOperation> createdAt = table.Column<DateTimeOffset>("timestamp with time zone");
			OperationBuilder<AddColumnOperation> availableAt = table.Column<DateTimeOffset>("timestamp with time zone");
			OperationBuilder<AddColumnOperation> leaseExpiresAt = table.Column<DateTimeOffset>("timestamp with time zone", null, null, rowVersion: false, null, nullable: true);
			maxLength2 = 200;
			OperationBuilder<AddColumnOperation> leaseOwner = table.Column<string>("character varying(200)", null, maxLength2, rowVersion: false, null, nullable: true);
			OperationBuilder<AddColumnOperation> attemptCount = table.Column<int>("integer");
			OperationBuilder<AddColumnOperation> completedAt = table.Column<DateTimeOffset>("timestamp with time zone", null, null, rowVersion: false, null, nullable: true);
			maxLength2 = 100;
			return new
			{
				Id = id,
				RunId = runId,
				IdempotencyKey = idempotencyKey,
				Payload = payload,
				PayloadHash = payloadHash,
				CreatedAt = createdAt,
				AvailableAt = availableAt,
				LeaseExpiresAt = leaseExpiresAt,
				LeaseOwner = leaseOwner,
				AttemptCount = attemptCount,
				CompletedAt = completedAt,
				LastErrorCode = table.Column<string>("character varying(100)", null, maxLength2, rowVersion: false, null, nullable: true)
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_admin_withdrawal_dispatch_outbox", x => x.Id);
			table.CheckConstraint("ck_economy_admin_withdrawal_dispatch_outbox_attempts", "\"AttemptCount\" >= 0");
			table.ForeignKey("FK_economy_admin_withdrawal_dispatch_outbox_economy_admin_with~", x => x.RunId, "economy_admin_withdrawal_runs", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("economy_anchor_verifications", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> externalAnchorId = table.Column<Guid>("uuid");
			int? maxLength2 = 256;
			OperationBuilder<AddColumnOperation> keyId = table.Column<string>("character varying(256)", null, maxLength2);
			maxLength2 = 256;
			OperationBuilder<AddColumnOperation> objectVersion = table.Column<string>("character varying(256)", null, maxLength2);
			maxLength2 = 256;
			OperationBuilder<AddColumnOperation> eTag = table.Column<string>("character varying(256)", null, maxLength2);
			OperationBuilder<AddColumnOperation> retainUntil = table.Column<DateTimeOffset>("timestamp with time zone");
			maxLength2 = 128;
			return new
			{
				Id = id,
				ExternalAnchorId = externalAnchorId,
				KeyId = keyId,
				ObjectVersion = objectVersion,
				ETag = eTag,
				RetainUntil = retainUntil,
				ObjectHash = table.Column<string>("character varying(128)", null, maxLength2),
				SignatureValid = table.Column<bool>("boolean"),
				ObjectMatches = table.Column<bool>("boolean"),
				VerifiedAt = table.Column<DateTimeOffset>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_anchor_verifications", x => x.Id);
			table.ForeignKey("FK_economy_anchor_verifications_economy_external_anchors_Exter~", x => x.ExternalAnchorId, "economy_external_anchors", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("economy_bounty_expiration_events", (ColumnsBuilder table) => new
		{
			Id = table.Column<Guid>("uuid"),
			BountyId = table.Column<Guid>("uuid"),
			ExpiresAt = table.Column<DateTimeOffset>("timestamp with time zone"),
			RecordedAt = table.Column<DateTimeOffset>("timestamp with time zone"),
			BountyVersion = table.Column<long>("bigint")
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_bounty_expiration_events", x => x.Id);
			table.CheckConstraint("ck_economy_bounty_expiration_events_time", "\"RecordedAt\" >= \"ExpiresAt\"");
			table.CheckConstraint("ck_economy_bounty_expiration_events_version", "\"BountyVersion\" > 1");
			table.ForeignKey("FK_economy_bounty_expiration_events_economy_bounties_BountyId", x => x.BountyId, "economy_bounties", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("economy_capability_policies", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			int? maxLength2 = 256;
			OperationBuilder<AddColumnOperation> scopeKey = table.Column<string>("character varying(256)", null, maxLength2);
			OperationBuilder<AddColumnOperation> tenantId = table.Column<Guid>("uuid", null, null, rowVersion: false, null, nullable: true);
			OperationBuilder<AddColumnOperation> capability = table.Column<int>("integer");
			maxLength2 = 16;
			OperationBuilder<AddColumnOperation> jurisdictionCode = table.Column<string>("character varying(16)", null, maxLength2);
			OperationBuilder<AddColumnOperation> version = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> canonicalPayload = table.Column<string>("jsonb");
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> payloadHash = table.Column<string>("character varying(128)", null, maxLength2);
			maxLength2 = 256;
			OperationBuilder<AddColumnOperation> keyId = table.Column<string>("character varying(256)", null, maxLength2);
			maxLength2 = 2048;
			OperationBuilder<AddColumnOperation> signature = table.Column<string>("character varying(2048)", null, maxLength2);
			maxLength2 = 128;
			return new
			{
				Id = id,
				ScopeKey = scopeKey,
				TenantId = tenantId,
				Capability = capability,
				JurisdictionCode = jurisdictionCode,
				Version = version,
				CanonicalPayload = canonicalPayload,
				PayloadHash = payloadHash,
				KeyId = keyId,
				Signature = signature,
				RequestHash = table.Column<string>("character varying(128)", null, maxLength2),
				ProposedBy = table.Column<Guid>("uuid"),
				ApprovedBy = table.Column<Guid>("uuid", null, null, rowVersion: false, null, nullable: true),
				ProposedAt = table.Column<DateTimeOffset>("timestamp with time zone"),
				ApprovedAt = table.Column<DateTimeOffset>("timestamp with time zone", null, null, rowVersion: false, null, nullable: true),
				EffectiveAt = table.Column<DateTimeOffset>("timestamp with time zone"),
				ExpiresAt = table.Column<DateTimeOffset>("timestamp with time zone"),
				ProviderReady = table.Column<bool>("boolean"),
				IsActive = table.Column<bool>("boolean")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_capability_policies", x => x.Id);
			table.CheckConstraint("ck_economy_capability_policies_dual_control", "(\"ApprovedBy\" IS NULL AND \"ApprovedAt\" IS NULL AND NOT \"IsActive\") OR (\"ApprovedBy\" IS NOT NULL AND \"ApprovedBy\" <> \"ProposedBy\" AND \"ApprovedAt\" >= \"ProposedAt\")");
			table.CheckConstraint("ck_economy_capability_policies_version", "\"Version\" > 0");
			table.CheckConstraint("ck_economy_capability_policies_window", "\"ExpiresAt\" > \"EffectiveAt\" AND (\"ApprovedAt\" IS NULL OR \"EffectiveAt\" >= \"ApprovedAt\")");
		});
		migrationBuilder.CreateTable("economy_capability_receipts", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> tenantId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> actorId = table.Column<Guid>("uuid");
			int? maxLength2 = 256;
			OperationBuilder<AddColumnOperation> subjectReference = table.Column<string>("character varying(256)", null, maxLength2);
			maxLength2 = 16;
			OperationBuilder<AddColumnOperation> jurisdictionCode = table.Column<string>("character varying(16)", null, maxLength2);
			OperationBuilder<AddColumnOperation> capability = table.Column<int>("integer");
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> operationFingerprint = table.Column<string>("character varying(128)", null, maxLength2);
			OperationBuilder<AddColumnOperation> policyVersion = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> reserveVersion = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> riskDecisionId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> killSwitchEpoch = table.Column<long>("bigint");
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> providerHash = table.Column<string>("character varying(128)", null, maxLength2);
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> destinationHash = table.Column<string>("character varying(128)", null, maxLength2);
			OperationBuilder<AddColumnOperation> sourceRootHashes = table.Column<string>("jsonb");
			OperationBuilder<AddColumnOperation> evidenceHashes = table.Column<string>("jsonb");
			OperationBuilder<AddColumnOperation> issuedAt = table.Column<DateTimeOffset>("timestamp with time zone");
			OperationBuilder<AddColumnOperation> expiresAt = table.Column<DateTimeOffset>("timestamp with time zone");
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> receiptHash = table.Column<string>("character varying(128)", null, maxLength2);
			maxLength2 = 256;
			OperationBuilder<AddColumnOperation> keyId = table.Column<string>("character varying(256)", null, maxLength2);
			maxLength2 = 2048;
			return new
			{
				Id = id,
				TenantId = tenantId,
				ActorId = actorId,
				SubjectReference = subjectReference,
				JurisdictionCode = jurisdictionCode,
				Capability = capability,
				OperationFingerprint = operationFingerprint,
				PolicyVersion = policyVersion,
				ReserveVersion = reserveVersion,
				RiskDecisionId = riskDecisionId,
				KillSwitchEpoch = killSwitchEpoch,
				ProviderHash = providerHash,
				DestinationHash = destinationHash,
				SourceRootHashes = sourceRootHashes,
				EvidenceHashes = evidenceHashes,
				IssuedAt = issuedAt,
				ExpiresAt = expiresAt,
				ReceiptHash = receiptHash,
				KeyId = keyId,
				Signature = table.Column<string>("character varying(2048)", null, maxLength2)
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_capability_receipts", x => x.Id);
			table.CheckConstraint("ck_economy_capability_receipts_lifetime", "\"ExpiresAt\" > \"IssuedAt\"");
			table.CheckConstraint("ck_economy_capability_receipts_versions", "\"PolicyVersion\" > 0 AND \"ReserveVersion\" > 0 AND \"KillSwitchEpoch\" >= 0");
			table.ForeignKey("FK_economy_capability_receipts_economy_risk_decisions_RiskDeci~", x => x.RiskDecisionId, "economy_risk_decisions", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("economy_compliance_evidence", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			int? maxLength2 = 100;
			OperationBuilder<AddColumnOperation> provider = table.Column<string>("character varying(100)", null, maxLength2);
			maxLength2 = 50;
			OperationBuilder<AddColumnOperation> environment = table.Column<string>("character varying(50)", null, maxLength2);
			maxLength2 = 256;
			OperationBuilder<AddColumnOperation> providerEventId = table.Column<string>("character varying(256)", null, maxLength2);
			OperationBuilder<AddColumnOperation> tenantId = table.Column<Guid>("uuid");
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> subjectHash = table.Column<string>("character varying(128)", null, maxLength2);
			maxLength2 = 100;
			OperationBuilder<AddColumnOperation> evidenceKind = table.Column<string>("character varying(100)", null, maxLength2);
			OperationBuilder<AddColumnOperation> version = table.Column<long>("bigint");
			maxLength2 = 100;
			OperationBuilder<AddColumnOperation> result = table.Column<string>("character varying(100)", null, maxLength2);
			OperationBuilder<AddColumnOperation> policyVersion = table.Column<long>("bigint");
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> payloadHash = table.Column<string>("character varying(128)", null, maxLength2);
			OperationBuilder<AddColumnOperation> signatureVerified = table.Column<bool>("boolean");
			maxLength2 = 1000;
			OperationBuilder<AddColumnOperation> rawObjectReference = table.Column<string>("character varying(1000)", null, maxLength2);
			maxLength2 = 128;
			return new
			{
				Id = id,
				Provider = provider,
				Environment = environment,
				ProviderEventId = providerEventId,
				TenantId = tenantId,
				SubjectHash = subjectHash,
				EvidenceKind = evidenceKind,
				Version = version,
				Result = result,
				PolicyVersion = policyVersion,
				PayloadHash = payloadHash,
				SignatureVerified = signatureVerified,
				RawObjectReference = rawObjectReference,
				EvidenceHash = table.Column<string>("character varying(128)", null, maxLength2),
				IssuedAt = table.Column<DateTimeOffset>("timestamp with time zone"),
				ExpiresAt = table.Column<DateTimeOffset>("timestamp with time zone"),
				ReceivedAt = table.Column<DateTimeOffset>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_compliance_evidence", x => x.Id);
			table.CheckConstraint("ck_economy_compliance_evidence_lifetime", "\"ExpiresAt\" > \"IssuedAt\" AND \"ReceivedAt\" >= \"IssuedAt\"");
			table.CheckConstraint("ck_economy_compliance_evidence_versions", "\"Version\" > 0 AND \"PolicyVersion\" > 0 AND length(btrim(\"EvidenceKind\")) > 0");
		});
		migrationBuilder.CreateTable("economy_compliance_holds", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			int? maxLength2 = 512;
			OperationBuilder<AddColumnOperation> scopeKey = table.Column<string>("character varying(512)", null, maxLength2);
			OperationBuilder<AddColumnOperation> tenantId = table.Column<Guid>("uuid");
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> subjectHash = table.Column<string>("character varying(128)", null, maxLength2);
			OperationBuilder<AddColumnOperation> capability = table.Column<int>("integer", null, null, rowVersion: false, null, nullable: true);
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> caseReferenceHash = table.Column<string>("character varying(128)", null, maxLength2);
			maxLength2 = 100;
			OperationBuilder<AddColumnOperation> reasonCode = table.Column<string>("character varying(100)", null, maxLength2);
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> evidenceHash = table.Column<string>("character varying(128)", null, maxLength2);
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> idempotencyKeyHash = table.Column<string>("character varying(128)", null, maxLength2);
			maxLength2 = 128;
			return new
			{
				Id = id,
				ScopeKey = scopeKey,
				TenantId = tenantId,
				SubjectHash = subjectHash,
				Capability = capability,
				CaseReferenceHash = caseReferenceHash,
				ReasonCode = reasonCode,
				EvidenceHash = evidenceHash,
				IdempotencyKeyHash = idempotencyKeyHash,
				RequestHash = table.Column<string>("character varying(128)", null, maxLength2),
				ActivatedBy = table.Column<Guid>("uuid"),
				ActivatedAt = table.Column<DateTimeOffset>("timestamp with time zone"),
				ExpiresAt = table.Column<DateTimeOffset>("timestamp with time zone"),
				ReleasedBy = table.Column<Guid>("uuid", null, null, rowVersion: false, null, nullable: true),
				ReleasedAt = table.Column<DateTimeOffset>("timestamp with time zone", null, null, rowVersion: false, null, nullable: true)
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_compliance_holds", x => x.Id);
			table.CheckConstraint("ck_economy_compliance_holds_lifetime", "\"ExpiresAt\" > \"ActivatedAt\"");
			table.CheckConstraint("ck_economy_compliance_holds_release", "(\"ReleasedAt\" IS NULL AND \"ReleasedBy\" IS NULL) OR (\"ReleasedAt\" >= \"ActivatedAt\" AND \"ReleasedBy\" IS NOT NULL)");
		});
		migrationBuilder.CreateTable("economy_compliance_inbox", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			int? maxLength2 = 100;
			OperationBuilder<AddColumnOperation> provider = table.Column<string>("character varying(100)", null, maxLength2);
			maxLength2 = 50;
			OperationBuilder<AddColumnOperation> environment = table.Column<string>("character varying(50)", null, maxLength2);
			maxLength2 = 256;
			OperationBuilder<AddColumnOperation> providerEventId = table.Column<string>("character varying(256)", null, maxLength2);
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> payloadHash = table.Column<string>("character varying(128)", null, maxLength2);
			maxLength2 = 1000;
			OperationBuilder<AddColumnOperation> rawObjectReference = table.Column<string>("character varying(1000)", null, maxLength2);
			OperationBuilder<AddColumnOperation> receivedAt = table.Column<DateTimeOffset>("timestamp with time zone");
			OperationBuilder<AddColumnOperation> processedAt = table.Column<DateTimeOffset>("timestamp with time zone", null, null, rowVersion: false, null, nullable: true);
			maxLength2 = 2000;
			return new
			{
				Id = id,
				Provider = provider,
				Environment = environment,
				ProviderEventId = providerEventId,
				PayloadHash = payloadHash,
				RawObjectReference = rawObjectReference,
				ReceivedAt = receivedAt,
				ProcessedAt = processedAt,
				ProcessingError = table.Column<string>("character varying(2000)", null, maxLength2, rowVersion: false, null, nullable: true)
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_compliance_inbox", x => x.Id);
		});
		migrationBuilder.CreateTable("economy_custody_observations", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			int? maxLength2 = 100;
			OperationBuilder<AddColumnOperation> provider = table.Column<string>("character varying(100)", null, maxLength2);
			maxLength2 = 256;
			OperationBuilder<AddColumnOperation> assetKey = table.Column<string>("character varying(256)", null, maxLength2);
			OperationBuilder<AddColumnOperation> purpose = table.Column<int>("integer");
			OperationBuilder<AddColumnOperation> version = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> eligibleUsdNanos = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> observedAt = table.Column<DateTimeOffset>("timestamp with time zone");
			OperationBuilder<AddColumnOperation> expiresAt = table.Column<DateTimeOffset>("timestamp with time zone");
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> payloadHash = table.Column<string>("character varying(128)", null, maxLength2);
			maxLength2 = 256;
			OperationBuilder<AddColumnOperation> keyId = table.Column<string>("character varying(256)", null, maxLength2);
			maxLength2 = 2048;
			return new
			{
				Id = id,
				Provider = provider,
				AssetKey = assetKey,
				Purpose = purpose,
				Version = version,
				EligibleUsdNanos = eligibleUsdNanos,
				ObservedAt = observedAt,
				ExpiresAt = expiresAt,
				PayloadHash = payloadHash,
				KeyId = keyId,
				Signature = table.Column<string>("character varying(2048)", null, maxLength2)
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_custody_observations", x => x.Id);
			table.CheckConstraint("ck_economy_custody_observations_lifetime", "\"ExpiresAt\" > \"ObservedAt\"");
			table.CheckConstraint("ck_economy_custody_observations_values", "\"Version\" > 0 AND \"EligibleUsdNanos\" >= 0");
		});
		migrationBuilder.CreateTable("economy_custody_reconciliations", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> reserveVersion = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> observationIds = table.Column<string>("jsonb");
			OperationBuilder<AddColumnOperation> liabilityUsdNanos = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> eligibleAssetUsdNanos = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> varianceUsdNanos = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> isReconciled = table.Column<bool>("boolean");
			int? maxLength2 = 128;
			return new
			{
				Id = id,
				ReserveVersion = reserveVersion,
				ObservationIds = observationIds,
				LiabilityUsdNanos = liabilityUsdNanos,
				EligibleAssetUsdNanos = eligibleAssetUsdNanos,
				VarianceUsdNanos = varianceUsdNanos,
				IsReconciled = isReconciled,
				EvidenceHash = table.Column<string>("character varying(128)", null, maxLength2),
				ReconciledBy = table.Column<Guid>("uuid"),
				ReconciledAt = table.Column<DateTimeOffset>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_custody_reconciliations", x => x.Id);
			table.CheckConstraint("ck_economy_custody_reconciliations_values", "\"ReserveVersion\" > 0 AND \"LiabilityUsdNanos\" >= 0 AND \"EligibleAssetUsdNanos\" >= 0");
		});
		migrationBuilder.CreateTable("economy_entity_graph_nodes", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> tenantId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> type = table.Column<int>("integer");
			int? maxLength2 = 128;
			OperationBuilder<AddColumnOperation> identityHash = table.Column<string>("character varying(128)", null, maxLength2);
			OperationBuilder<AddColumnOperation> version = table.Column<long>("bigint");
			maxLength2 = 128;
			return new
			{
				Id = id,
				TenantId = tenantId,
				Type = type,
				IdentityHash = identityHash,
				Version = version,
				EvidenceHash = table.Column<string>("character varying(128)", null, maxLength2),
				RecordedAt = table.Column<DateTimeOffset>("timestamp with time zone"),
				SupersededAt = table.Column<DateTimeOffset>("timestamp with time zone", null, null, rowVersion: false, null, nullable: true)
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_entity_graph_nodes", x => x.Id);
			table.CheckConstraint("ck_economy_entity_graph_nodes_version", "\"Version\" > 0");
		});
		migrationBuilder.CreateTable("economy_journal_verification_checkpoints", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> fromSequence = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> toSequence = table.Column<long>("bigint");
			int? maxLength2 = 128;
			OperationBuilder<AddColumnOperation> previousHash = table.Column<string>("character varying(128)", null, maxLength2);
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> currentHash = table.Column<string>("character varying(128)", null, maxLength2);
			OperationBuilder<AddColumnOperation> isValid = table.Column<bool>("boolean");
			maxLength2 = 100;
			return new
			{
				Id = id,
				FromSequence = fromSequence,
				ToSequence = toSequence,
				PreviousHash = previousHash,
				CurrentHash = currentHash,
				IsValid = isValid,
				FailureCode = table.Column<string>("character varying(100)", null, maxLength2, rowVersion: false, null, nullable: true),
				FencingToken = table.Column<long>("bigint"),
				StartedAt = table.Column<DateTimeOffset>("timestamp with time zone"),
				CompletedAt = table.Column<DateTimeOffset>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_journal_verification_checkpoints", x => x.Id);
			table.CheckConstraint("ck_economy_journal_verification_checkpoints_range", "\"FromSequence\" >= 0 AND \"ToSequence\" >= \"FromSequence\"");
			table.CheckConstraint("ck_economy_journal_verification_checkpoints_time", "\"CompletedAt\" >= \"StartedAt\"");
		});
		migrationBuilder.CreateTable("economy_kill_switches", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			int? maxLength2 = 256;
			OperationBuilder<AddColumnOperation> scopeKey = table.Column<string>("character varying(256)", null, maxLength2);
			OperationBuilder<AddColumnOperation> tenantId = table.Column<Guid>("uuid", null, null, rowVersion: false, null, nullable: true);
			OperationBuilder<AddColumnOperation> capability = table.Column<int>("integer", null, null, rowVersion: false, null, nullable: true);
			OperationBuilder<AddColumnOperation> epoch = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> isActive = table.Column<bool>("boolean");
			maxLength2 = 1000;
			OperationBuilder<AddColumnOperation> reason = table.Column<string>("character varying(1000)", null, maxLength2);
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> requestHash = table.Column<string>("character varying(128)", null, maxLength2);
			OperationBuilder<AddColumnOperation> activatedBy = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> activatedAt = table.Column<DateTimeOffset>("timestamp with time zone");
			OperationBuilder<AddColumnOperation> releaseProposedBy = table.Column<Guid>("uuid", null, null, rowVersion: false, null, nullable: true);
			maxLength2 = 128;
			return new
			{
				Id = id,
				ScopeKey = scopeKey,
				TenantId = tenantId,
				Capability = capability,
				Epoch = epoch,
				IsActive = isActive,
				Reason = reason,
				RequestHash = requestHash,
				ActivatedBy = activatedBy,
				ActivatedAt = activatedAt,
				ReleaseProposedBy = releaseProposedBy,
				ReleaseProposalReauthenticationHash = table.Column<string>("character varying(128)", null, maxLength2, rowVersion: false, null, nullable: true),
				ReleaseProposedAt = table.Column<DateTimeOffset>("timestamp with time zone", null, null, rowVersion: false, null, nullable: true),
				ReleasedAt = table.Column<DateTimeOffset>("timestamp with time zone", null, null, rowVersion: false, null, nullable: true)
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_kill_switches", x => x.Id);
			table.CheckConstraint("ck_economy_kill_switches_epoch", "\"Epoch\" > 0");
			table.CheckConstraint("ck_economy_kill_switches_release_proposal", "(\"ReleaseProposedBy\" IS NULL AND \"ReleaseProposedAt\" IS NULL AND \"ReleaseProposalReauthenticationHash\" IS NULL) OR (\"ReleaseProposedBy\" IS NOT NULL AND \"ReleaseProposedAt\" >= \"ActivatedAt\" AND length(btrim(\"ReleaseProposalReauthenticationHash\")) > 0)");
			table.CheckConstraint("ck_economy_kill_switches_state", "(\"IsActive\" AND \"ReleasedAt\" IS NULL) OR (NOT \"IsActive\" AND \"ReleasedAt\" >= \"ActivatedAt\")");
		});
		migrationBuilder.CreateTable("economy_marketplace_events", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> tenantId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> settlementId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> sequence = table.Column<long>("bigint");
			int? maxLength2 = 100;
			OperationBuilder<AddColumnOperation> eventKind = table.Column<string>("character varying(100)", null, maxLength2);
			maxLength2 = 128;
			return new
			{
				Id = id,
				TenantId = tenantId,
				SettlementId = settlementId,
				Sequence = sequence,
				EventKind = eventKind,
				EvidenceHash = table.Column<string>("character varying(128)", null, maxLength2),
				OccurredAt = table.Column<DateTimeOffset>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_marketplace_events", x => x.Id);
			table.CheckConstraint("ck_economy_marketplace_events_sequence", "\"Sequence\" > 0");
			table.ForeignKey("FK_economy_marketplace_events_economy_marketplace_settlements_~", x => x.SettlementId, "economy_marketplace_settlements", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("economy_marketplace_outbox", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> tenantId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> settlementId = table.Column<Guid>("uuid");
			int? maxLength2 = 150;
			OperationBuilder<AddColumnOperation> messageType = table.Column<string>("character varying(150)", null, maxLength2);
			OperationBuilder<AddColumnOperation> payload = table.Column<string>("jsonb");
			maxLength2 = 128;
			return new
			{
				Id = id,
				TenantId = tenantId,
				SettlementId = settlementId,
				MessageType = messageType,
				Payload = payload,
				PayloadHash = table.Column<string>("character varying(128)", null, maxLength2),
				OccurredAt = table.Column<DateTimeOffset>("timestamp with time zone"),
				PublishedAt = table.Column<DateTimeOffset>("timestamp with time zone", null, null, rowVersion: false, null, nullable: true),
				AttemptCount = table.Column<int>("integer")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_marketplace_outbox", x => x.Id);
			table.CheckConstraint("ck_economy_marketplace_outbox_attempts", "\"AttemptCount\" >= 0");
			table.ForeignKey("FK_economy_marketplace_outbox_economy_marketplace_settlements_~", x => x.SettlementId, "economy_marketplace_settlements", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("economy_marketplace_refund_debts", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> tenantId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> refundId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> settlementId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> responsibleWalletId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> currency = table.Column<int>("integer");
			OperationBuilder<AddColumnOperation> amountUnits = table.Column<long>("bigint");
			int? maxLength2 = 128;
			return new
			{
				Id = id,
				TenantId = tenantId,
				RefundId = refundId,
				SettlementId = settlementId,
				ResponsibleWalletId = responsibleWalletId,
				Currency = currency,
				AmountUnits = amountUnits,
				EvidenceHash = table.Column<string>("character varying(128)", null, maxLength2),
				RecordedAt = table.Column<DateTimeOffset>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_marketplace_refund_debts", x => x.Id);
			table.CheckConstraint("ck_economy_marketplace_refund_debts_amount", "\"AmountUnits\" > 0");
			table.ForeignKey("FK_economy_marketplace_refund_debts_economy_marketplace_refund~", x => x.RefundId, "economy_marketplace_refunds", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
			table.ForeignKey("FK_economy_marketplace_refund_debts_economy_marketplace_settle~", x => x.SettlementId, "economy_marketplace_settlements", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("economy_payout_connect_accounts", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> payeeId = table.Column<Guid>("uuid");
			int? maxLength2 = 50;
			OperationBuilder<AddColumnOperation> provider = table.Column<string>("character varying(50)", null, maxLength2);
			maxLength2 = 50;
			OperationBuilder<AddColumnOperation> environment = table.Column<string>("character varying(50)", null, maxLength2);
			maxLength2 = 256;
			OperationBuilder<AddColumnOperation> providerAccountId = table.Column<string>("character varying(256)", null, maxLength2);
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> destinationHash = table.Column<string>("character varying(128)", null, maxLength2);
			OperationBuilder<AddColumnOperation> state = table.Column<int>("integer");
			OperationBuilder<AddColumnOperation> chargesEnabled = table.Column<bool>("boolean");
			OperationBuilder<AddColumnOperation> payoutsEnabled = table.Column<bool>("boolean");
			OperationBuilder<AddColumnOperation> version = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> observedAt = table.Column<DateTimeOffset>("timestamp with time zone");
			OperationBuilder<AddColumnOperation> expiresAt = table.Column<DateTimeOffset>("timestamp with time zone");
			maxLength2 = 128;
			return new
			{
				PayeeId = payeeId,
				Provider = provider,
				Environment = environment,
				ProviderAccountId = providerAccountId,
				DestinationHash = destinationHash,
				State = state,
				ChargesEnabled = chargesEnabled,
				PayoutsEnabled = payoutsEnabled,
				Version = version,
				ObservedAt = observedAt,
				ExpiresAt = expiresAt,
				EvidenceHash = table.Column<string>("character varying(128)", null, maxLength2)
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_payout_connect_accounts", x => x.PayeeId);
			table.CheckConstraint("ck_economy_payout_connect_accounts_state", "\"State\" BETWEEN 1 AND 4");
			table.CheckConstraint("ck_economy_payout_connect_accounts_version", "\"Version\" > 0");
			table.CheckConstraint("ck_economy_payout_connect_accounts_window", "\"ExpiresAt\" > \"ObservedAt\"");
		});
		migrationBuilder.CreateTable("economy_payout_dispatch_outbox", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> operationId = table.Column<Guid>("uuid");
			int? maxLength2 = 256;
			OperationBuilder<AddColumnOperation> idempotencyKey = table.Column<string>("character varying(256)", null, maxLength2);
			OperationBuilder<AddColumnOperation> payload = table.Column<string>("jsonb");
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> payloadHash = table.Column<string>("character varying(128)", null, maxLength2);
			OperationBuilder<AddColumnOperation> createdAt = table.Column<DateTimeOffset>("timestamp with time zone");
			OperationBuilder<AddColumnOperation> availableAt = table.Column<DateTimeOffset>("timestamp with time zone");
			OperationBuilder<AddColumnOperation> leaseExpiresAt = table.Column<DateTimeOffset>("timestamp with time zone", null, null, rowVersion: false, null, nullable: true);
			maxLength2 = 200;
			OperationBuilder<AddColumnOperation> leaseOwner = table.Column<string>("character varying(200)", null, maxLength2, rowVersion: false, null, nullable: true);
			OperationBuilder<AddColumnOperation> attemptCount = table.Column<int>("integer");
			OperationBuilder<AddColumnOperation> completedAt = table.Column<DateTimeOffset>("timestamp with time zone", null, null, rowVersion: false, null, nullable: true);
			maxLength2 = 100;
			return new
			{
				Id = id,
				OperationId = operationId,
				IdempotencyKey = idempotencyKey,
				Payload = payload,
				PayloadHash = payloadHash,
				CreatedAt = createdAt,
				AvailableAt = availableAt,
				LeaseExpiresAt = leaseExpiresAt,
				LeaseOwner = leaseOwner,
				AttemptCount = attemptCount,
				CompletedAt = completedAt,
				LastErrorCode = table.Column<string>("character varying(100)", null, maxLength2, rowVersion: false, null, nullable: true)
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_payout_dispatch_outbox", x => x.Id);
			table.CheckConstraint("ck_economy_payout_dispatch_outbox_attempts", "\"AttemptCount\" >= 0");
			table.ForeignKey("FK_economy_payout_dispatch_outbox_economy_payout_operations_Op~", x => x.OperationId, "economy_payout_operations", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("economy_projection_generations", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> generation = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> fromSequence = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> toSequence = table.Column<long>("bigint");
			int? maxLength2 = 128;
			OperationBuilder<AddColumnOperation> projectionHash = table.Column<string>("character varying(128)", null, maxLength2);
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> journalHash = table.Column<string>("character varying(128)", null, maxLength2);
			OperationBuilder<AddColumnOperation> mismatchCount = table.Column<int>("integer");
			maxLength2 = 50;
			return new
			{
				Id = id,
				Generation = generation,
				FromSequence = fromSequence,
				ToSequence = toSequence,
				ProjectionHash = projectionHash,
				JournalHash = journalHash,
				MismatchCount = mismatchCount,
				State = table.Column<string>("character varying(50)", null, maxLength2),
				IsActive = table.Column<bool>("boolean"),
				ProposedBy = table.Column<Guid>("uuid"),
				ApprovedBy = table.Column<Guid>("uuid", null, null, rowVersion: false, null, nullable: true),
				SecondApprovedBy = table.Column<Guid>("uuid", null, null, rowVersion: false, null, nullable: true),
				StartedAt = table.Column<DateTimeOffset>("timestamp with time zone"),
				CompletedAt = table.Column<DateTimeOffset>("timestamp with time zone", null, null, rowVersion: false, null, nullable: true),
				ActivatedAt = table.Column<DateTimeOffset>("timestamp with time zone", null, null, rowVersion: false, null, nullable: true)
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_projection_generations", x => x.Id);
			table.UniqueConstraint("AK_economy_projection_generations_Generation", x => x.Generation);
			table.CheckConstraint("ck_economy_projection_generations_dual_control", "(\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"ProposedBy\") AND (\"SecondApprovedBy\" IS NULL OR (\"SecondApprovedBy\" <> \"ProposedBy\" AND \"SecondApprovedBy\" <> \"ApprovedBy\"))");
			table.CheckConstraint("ck_economy_projection_generations_range", "\"Generation\" > 0 AND \"FromSequence\" >= 0 AND \"ToSequence\" >= \"FromSequence\"");
		});
		migrationBuilder.CreateTable("economy_reserve_proposals", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> version = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> policyVersion = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> expectedActiveVersion = table.Column<long>("bigint", null, null, rowVersion: false, null, nullable: true);
			OperationBuilder<AddColumnOperation> authorizationEpoch = table.Column<long>("bigint");
			int? maxLength2 = 128;
			OperationBuilder<AddColumnOperation> snapshotHash = table.Column<string>("character varying(128)", null, maxLength2);
			OperationBuilder<AddColumnOperation> liabilityUsdNanos = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> eligibleAssetUsdNanos = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> hardFaceValueUsdMinor = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> requiredHardReserveUsdMinor = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> softFaceValueUsdNanos = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> stressedExpectedRedemptionCostUsdNanos = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> requiredSoftReserveUsdNanos = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> hardBackingUsdNanos = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> softBackingUsdNanos = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> coverage = table.Column<int>("integer");
			OperationBuilder<AddColumnOperation> observationIds = table.Column<string>("jsonb");
			OperationBuilder<AddColumnOperation> assetAllocations = table.Column<string>("jsonb");
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> evidenceHash = table.Column<string>("character varying(128)", null, maxLength2);
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> requestHash = table.Column<string>("character varying(128)", null, maxLength2);
			OperationBuilder<AddColumnOperation> proposedBy = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> approvedBy = table.Column<Guid>("uuid", null, null, rowVersion: false, null, nullable: true);
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> approvalReauthenticationHash = table.Column<string>("character varying(128)", null, maxLength2, rowVersion: false, null, nullable: true);
			OperationBuilder<AddColumnOperation> proposedAt = table.Column<DateTimeOffset>("timestamp with time zone");
			OperationBuilder<AddColumnOperation> approvedAt = table.Column<DateTimeOffset>("timestamp with time zone", null, null, rowVersion: false, null, nullable: true);
			OperationBuilder<AddColumnOperation> observedAt = table.Column<DateTimeOffset>("timestamp with time zone");
			OperationBuilder<AddColumnOperation> expiresAt = table.Column<DateTimeOffset>("timestamp with time zone");
			maxLength2 = 50;
			return new
			{
				Id = id,
				Version = version,
				PolicyVersion = policyVersion,
				ExpectedActiveVersion = expectedActiveVersion,
				AuthorizationEpoch = authorizationEpoch,
				SnapshotHash = snapshotHash,
				LiabilityUsdNanos = liabilityUsdNanos,
				EligibleAssetUsdNanos = eligibleAssetUsdNanos,
				HardFaceValueUsdMinor = hardFaceValueUsdMinor,
				RequiredHardReserveUsdMinor = requiredHardReserveUsdMinor,
				SoftFaceValueUsdNanos = softFaceValueUsdNanos,
				StressedExpectedRedemptionCostUsdNanos = stressedExpectedRedemptionCostUsdNanos,
				RequiredSoftReserveUsdNanos = requiredSoftReserveUsdNanos,
				HardBackingUsdNanos = hardBackingUsdNanos,
				SoftBackingUsdNanos = softBackingUsdNanos,
				Coverage = coverage,
				ObservationIds = observationIds,
				AssetAllocations = assetAllocations,
				EvidenceHash = evidenceHash,
				RequestHash = requestHash,
				ProposedBy = proposedBy,
				ApprovedBy = approvedBy,
				ApprovalReauthenticationHash = approvalReauthenticationHash,
				ProposedAt = proposedAt,
				ApprovedAt = approvedAt,
				ObservedAt = observedAt,
				ExpiresAt = expiresAt,
				Status = table.Column<string>("character varying(50)", null, maxLength2)
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_reserve_proposals", x => x.Id);
			table.CheckConstraint("ck_economy_reserve_proposals_dual_control", "\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"ProposedBy\"");
			table.CheckConstraint("ck_economy_reserve_proposals_values", "\"Version\" > 0 AND \"PolicyVersion\" > 0 AND \"AuthorizationEpoch\" > 0 AND \"LiabilityUsdNanos\" >= 0 AND \"EligibleAssetUsdNanos\" >= 0 AND \"HardFaceValueUsdMinor\" >= 0 AND \"RequiredHardReserveUsdMinor\" >= 0 AND \"SoftFaceValueUsdNanos\" >= 0 AND \"RequiredSoftReserveUsdNanos\" >= 0 AND \"HardBackingUsdNanos\" >= 0 AND \"SoftBackingUsdNanos\" >= 0");
			table.CheckConstraint("ck_economy_reserve_proposals_window", "\"ExpiresAt\" > \"ObservedAt\" AND \"ProposedAt\" >= \"ObservedAt\"");
		});
		migrationBuilder.CreateTable("economy_worker_leases", delegate(ColumnsBuilder table)
		{
			int? maxLength2 = 100;
			OperationBuilder<AddColumnOperation> name = table.Column<string>("character varying(100)", null, maxLength2);
			maxLength2 = 256;
			return new
			{
				Name = name,
				Owner = table.Column<string>("character varying(256)", null, maxLength2),
				FencingToken = table.Column<long>("bigint"),
				AcquiredAt = table.Column<DateTimeOffset>("timestamp with time zone"),
				ExpiresAt = table.Column<DateTimeOffset>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_worker_leases", x => x.Name);
			table.CheckConstraint("ck_economy_worker_leases_fencing", "\"FencingToken\" > 0");
			table.CheckConstraint("ck_economy_worker_leases_lifetime", "\"ExpiresAt\" > \"AcquiredAt\"");
		});
		migrationBuilder.CreateTable("trust_safety_appeals", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> tenantId = table.Column<Guid>("uuid");
			int? maxLength2 = 128;
			OperationBuilder<AddColumnOperation> subjectHash = table.Column<string>("character varying(128)", null, maxLength2);
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> restrictionReferenceHash = table.Column<string>("character varying(128)", null, maxLength2);
			OperationBuilder<AddColumnOperation> state = table.Column<int>("integer");
			OperationBuilder<AddColumnOperation> submittedBy = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> assignedTo = table.Column<Guid>("uuid", null, null, rowVersion: false, null, nullable: true);
			OperationBuilder<AddColumnOperation> decidedBy = table.Column<Guid>("uuid", null, null, rowVersion: false, null, nullable: true);
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> submissionEvidenceHash = table.Column<string>("character varying(128)", null, maxLength2);
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> decisionEvidenceHash = table.Column<string>("character varying(128)", null, maxLength2, rowVersion: false, null, nullable: true);
			maxLength2 = 100;
			return new
			{
				Id = id,
				TenantId = tenantId,
				SubjectHash = subjectHash,
				RestrictionReferenceHash = restrictionReferenceHash,
				State = state,
				SubmittedBy = submittedBy,
				AssignedTo = assignedTo,
				DecidedBy = decidedBy,
				SubmissionEvidenceHash = submissionEvidenceHash,
				DecisionEvidenceHash = decisionEvidenceHash,
				ReasonCode = table.Column<string>("character varying(100)", null, maxLength2, rowVersion: false, null, nullable: true),
				SubmittedAt = table.Column<DateTimeOffset>("timestamp with time zone"),
				DecidedAt = table.Column<DateTimeOffset>("timestamp with time zone", null, null, rowVersion: false, null, nullable: true),
				Version = table.Column<long>("bigint")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_trust_safety_appeals", x => x.Id);
			table.CheckConstraint("ck_trust_safety_appeals_decision", "(\"State\" IN (1, 2) AND \"DecidedAt\" IS NULL AND \"DecidedBy\" IS NULL) OR (\"State\" IN (3, 4) AND \"DecidedAt\" IS NOT NULL AND \"DecidedBy\" IS NOT NULL)");
			table.CheckConstraint("ck_trust_safety_appeals_version", "\"Version\" > 0");
		});
		migrationBuilder.CreateTable("trust_safety_event_inbox", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			int? maxLength2 = 256;
			OperationBuilder<AddColumnOperation> eventId = table.Column<string>("character varying(256)", null, maxLength2);
			OperationBuilder<AddColumnOperation> tenantId = table.Column<Guid>("uuid");
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> subjectHash = table.Column<string>("character varying(128)", null, maxLength2);
			OperationBuilder<AddColumnOperation> kind = table.Column<int>("integer");
			OperationBuilder<AddColumnOperation> version = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> outcome = table.Column<int>("integer");
			OperationBuilder<AddColumnOperation> policyVersion = table.Column<long>("bigint");
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> payloadHash = table.Column<string>("character varying(128)", null, maxLength2);
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> evidenceHash = table.Column<string>("character varying(128)", null, maxLength2);
			maxLength2 = 2048;
			OperationBuilder<AddColumnOperation> rawObjectReference = table.Column<string>("character varying(2048)", null, maxLength2);
			maxLength2 = 256;
			OperationBuilder<AddColumnOperation> keyId = table.Column<string>("character varying(256)", null, maxLength2);
			maxLength2 = 2048;
			OperationBuilder<AddColumnOperation> signature = table.Column<string>("character varying(2048)", null, maxLength2);
			OperationBuilder<AddColumnOperation> signatureVerified = table.Column<bool>("boolean");
			OperationBuilder<AddColumnOperation> issuedAt = table.Column<DateTimeOffset>("timestamp with time zone");
			OperationBuilder<AddColumnOperation> expiresAt = table.Column<DateTimeOffset>("timestamp with time zone");
			OperationBuilder<AddColumnOperation> receivedAt = table.Column<DateTimeOffset>("timestamp with time zone");
			OperationBuilder<AddColumnOperation> processedAt = table.Column<DateTimeOffset>("timestamp with time zone", null, null, rowVersion: false, null, nullable: true);
			maxLength2 = 100;
			return new
			{
				Id = id,
				EventId = eventId,
				TenantId = tenantId,
				SubjectHash = subjectHash,
				Kind = kind,
				Version = version,
				Outcome = outcome,
				PolicyVersion = policyVersion,
				PayloadHash = payloadHash,
				EvidenceHash = evidenceHash,
				RawObjectReference = rawObjectReference,
				KeyId = keyId,
				Signature = signature,
				SignatureVerified = signatureVerified,
				IssuedAt = issuedAt,
				ExpiresAt = expiresAt,
				ReceivedAt = receivedAt,
				ProcessedAt = processedAt,
				ProcessingError = table.Column<string>("character varying(100)", null, maxLength2, rowVersion: false, null, nullable: true)
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_trust_safety_event_inbox", x => x.Id);
			table.CheckConstraint("ck_trust_safety_event_inbox_lifetime", "\"ExpiresAt\" > \"IssuedAt\" AND \"ReceivedAt\" >= \"IssuedAt\"");
			table.CheckConstraint("ck_trust_safety_event_inbox_version", "\"Version\" > 0 AND \"PolicyVersion\" > 0");
		});
		migrationBuilder.CreateTable("trust_safety_subject_states", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> tenantId = table.Column<Guid>("uuid");
			int? maxLength2 = 128;
			OperationBuilder<AddColumnOperation> subjectHash = table.Column<string>("character varying(128)", null, maxLength2);
			OperationBuilder<AddColumnOperation> version = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> outcome = table.Column<int>("integer");
			maxLength2 = 256;
			OperationBuilder<AddColumnOperation> lastEventId = table.Column<string>("character varying(256)", null, maxLength2);
			maxLength2 = 128;
			return new
			{
				TenantId = tenantId,
				SubjectHash = subjectHash,
				Version = version,
				Outcome = outcome,
				LastEventId = lastEventId,
				EvidenceHash = table.Column<string>("character varying(128)", null, maxLength2),
				HoldId = table.Column<Guid>("uuid", null, null, rowVersion: false, null, nullable: true),
				IssuedAt = table.Column<DateTimeOffset>("timestamp with time zone"),
				ExpiresAt = table.Column<DateTimeOffset>("timestamp with time zone"),
				UpdatedAt = table.Column<DateTimeOffset>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_trust_safety_subject_states", x => new { x.TenantId, x.SubjectHash });
			table.CheckConstraint("ck_trust_safety_subject_states_lifetime", "\"ExpiresAt\" > \"IssuedAt\"");
			table.CheckConstraint("ck_trust_safety_subject_states_version", "\"Version\" > 0");
		});
		migrationBuilder.CreateTable("compliance_financial_crime_case_events", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> caseId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> sequence = table.Column<int>("integer");
			int? maxLength2 = 50;
			OperationBuilder<AddColumnOperation> kind = table.Column<string>("character varying(50)", null, maxLength2);
			OperationBuilder<AddColumnOperation> actorId = table.Column<Guid>("uuid", null, null, rowVersion: false, null, nullable: true);
			maxLength2 = 100;
			OperationBuilder<AddColumnOperation> reasonCode = table.Column<string>("character varying(100)", null, maxLength2);
			maxLength2 = 128;
			return new
			{
				Id = id,
				CaseId = caseId,
				Sequence = sequence,
				Kind = kind,
				ActorId = actorId,
				ReasonCode = reasonCode,
				EvidenceHash = table.Column<string>("character varying(128)", null, maxLength2),
				OccurredAt = table.Column<DateTimeOffset>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_compliance_financial_crime_case_events", x => x.Id);
			table.CheckConstraint("ck_financial_crime_case_events_sequence", "\"Sequence\" > 0");
			table.ForeignKey("FK_compliance_financial_crime_case_events_compliance_financial~", x => x.CaseId, "compliance_financial_crime_cases", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("compliance_financial_crime_decisions", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> caseId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> tenantId = table.Column<Guid>("uuid");
			int? maxLength2 = 128;
			OperationBuilder<AddColumnOperation> subjectHash = table.Column<string>("character varying(128)", null, maxLength2);
			OperationBuilder<AddColumnOperation> version = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> outcome = table.Column<int>("integer");
			OperationBuilder<AddColumnOperation> policyVersion = table.Column<long>("bigint");
			maxLength2 = 100;
			OperationBuilder<AddColumnOperation> reasonCode = table.Column<string>("character varying(100)", null, maxLength2);
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> evidenceHash = table.Column<string>("character varying(128)", null, maxLength2);
			maxLength2 = 2048;
			return new
			{
				Id = id,
				CaseId = caseId,
				TenantId = tenantId,
				SubjectHash = subjectHash,
				Version = version,
				Outcome = outcome,
				PolicyVersion = policyVersion,
				ReasonCode = reasonCode,
				EvidenceHash = evidenceHash,
				RawObjectReference = table.Column<string>("character varying(2048)", null, maxLength2),
				DecidedBy = table.Column<Guid>("uuid"),
				IssuedAt = table.Column<DateTimeOffset>("timestamp with time zone"),
				ExpiresAt = table.Column<DateTimeOffset>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_compliance_financial_crime_decisions", x => x.Id);
			table.CheckConstraint("ck_financial_crime_decisions_lifetime", "\"ExpiresAt\" > \"IssuedAt\"");
			table.CheckConstraint("ck_financial_crime_decisions_version", "\"Version\" > 0 AND \"PolicyVersion\" > 0");
			table.ForeignKey("FK_compliance_financial_crime_decisions_compliance_financial_c~", x => x.CaseId, "compliance_financial_crime_cases", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("compliance_financial_crime_regulatory_references", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> caseId = table.Column<Guid>("uuid");
			int? maxLength2 = 20;
			OperationBuilder<AddColumnOperation> kind = table.Column<string>("character varying(20)", null, maxLength2);
			maxLength2 = 16;
			OperationBuilder<AddColumnOperation> jurisdictionCode = table.Column<string>("character varying(16)", null, maxLength2);
			maxLength2 = 128;
			return new
			{
				Id = id,
				CaseId = caseId,
				Kind = kind,
				JurisdictionCode = jurisdictionCode,
				ReferenceHash = table.Column<string>("character varying(128)", null, maxLength2),
				RecordedBy = table.Column<Guid>("uuid"),
				RecordedAt = table.Column<DateTimeOffset>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_compliance_financial_crime_regulatory_references", x => x.Id);
			table.ForeignKey("FK_compliance_financial_crime_regulatory_references_compliance~", x => x.CaseId, "compliance_financial_crime_cases", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("compliance_financial_crime_screenings", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			int? maxLength2 = 100;
			OperationBuilder<AddColumnOperation> provider = table.Column<string>("character varying(100)", null, maxLength2);
			maxLength2 = 50;
			OperationBuilder<AddColumnOperation> environment = table.Column<string>("character varying(50)", null, maxLength2);
			maxLength2 = 256;
			OperationBuilder<AddColumnOperation> providerEventId = table.Column<string>("character varying(256)", null, maxLength2);
			OperationBuilder<AddColumnOperation> tenantId = table.Column<Guid>("uuid");
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> subjectHash = table.Column<string>("character varying(128)", null, maxLength2);
			OperationBuilder<AddColumnOperation> caseId = table.Column<Guid>("uuid", null, null, rowVersion: false, null, nullable: true);
			OperationBuilder<AddColumnOperation> version = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> outcome = table.Column<int>("integer");
			OperationBuilder<AddColumnOperation> sanctionsMatch = table.Column<bool>("boolean");
			OperationBuilder<AddColumnOperation> pepMatch = table.Column<bool>("boolean");
			OperationBuilder<AddColumnOperation> adverseMediaMatch = table.Column<bool>("boolean");
			OperationBuilder<AddColumnOperation> policyVersion = table.Column<long>("bigint");
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> payloadHash = table.Column<string>("character varying(128)", null, maxLength2);
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> evidenceHash = table.Column<string>("character varying(128)", null, maxLength2);
			maxLength2 = 2048;
			return new
			{
				Id = id,
				Provider = provider,
				Environment = environment,
				ProviderEventId = providerEventId,
				TenantId = tenantId,
				SubjectHash = subjectHash,
				CaseId = caseId,
				Version = version,
				Outcome = outcome,
				SanctionsMatch = sanctionsMatch,
				PepMatch = pepMatch,
				AdverseMediaMatch = adverseMediaMatch,
				PolicyVersion = policyVersion,
				PayloadHash = payloadHash,
				EvidenceHash = evidenceHash,
				RawObjectReference = table.Column<string>("character varying(2048)", null, maxLength2),
				SignatureVerified = table.Column<bool>("boolean"),
				IssuedAt = table.Column<DateTimeOffset>("timestamp with time zone"),
				ExpiresAt = table.Column<DateTimeOffset>("timestamp with time zone"),
				NextScreenAt = table.Column<DateTimeOffset>("timestamp with time zone"),
				ReceivedAt = table.Column<DateTimeOffset>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_compliance_financial_crime_screenings", x => x.Id);
			table.CheckConstraint("ck_financial_crime_screenings_lifetime", "\"ExpiresAt\" > \"IssuedAt\" AND \"NextScreenAt\" > \"IssuedAt\"");
			table.CheckConstraint("ck_financial_crime_screenings_version", "\"Version\" > 0 AND \"PolicyVersion\" > 0");
			table.ForeignKey("FK_compliance_financial_crime_screenings_compliance_financial_~", x => x.CaseId, "compliance_financial_crime_cases", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("compliance_financial_crime_transaction_signals", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> tenantId = table.Column<Guid>("uuid");
			int? maxLength2 = 128;
			OperationBuilder<AddColumnOperation> subjectHash = table.Column<string>("character varying(128)", null, maxLength2);
			OperationBuilder<AddColumnOperation> caseId = table.Column<Guid>("uuid");
			maxLength2 = 256;
			OperationBuilder<AddColumnOperation> operationFingerprint = table.Column<string>("character varying(256)", null, maxLength2);
			maxLength2 = 100;
			OperationBuilder<AddColumnOperation> signalType = table.Column<string>("character varying(100)", null, maxLength2);
			OperationBuilder<AddColumnOperation> score = table.Column<int>("integer");
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> evidenceHash = table.Column<string>("character varying(128)", null, maxLength2);
			maxLength2 = 128;
			return new
			{
				Id = id,
				TenantId = tenantId,
				SubjectHash = subjectHash,
				CaseId = caseId,
				OperationFingerprint = operationFingerprint,
				SignalType = signalType,
				Score = score,
				EvidenceHash = evidenceHash,
				RequestHash = table.Column<string>("character varying(128)", null, maxLength2),
				ObservedAt = table.Column<DateTimeOffset>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_compliance_financial_crime_transaction_signals", x => x.Id);
			table.CheckConstraint("ck_financial_crime_transaction_signals_score", "\"Score\" BETWEEN 0 AND 1000000");
			table.ForeignKey("FK_compliance_financial_crime_transaction_signals_compliance_f~", x => x.CaseId, "compliance_financial_crime_cases", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("economy_ad_reward_cap_consumptions", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> tenantId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> sessionId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> scope = table.Column<int>("integer");
			int? maxLength2 = 128;
			return new
			{
				Id = id,
				TenantId = tenantId,
				SessionId = sessionId,
				Scope = scope,
				SubjectHash = table.Column<string>("character varying(128)", null, maxLength2),
				WindowStartedAt = table.Column<DateTimeOffset>("timestamp with time zone"),
				WindowEndsAt = table.Column<DateTimeOffset>("timestamp with time zone"),
				SoftUnits = table.Column<long>("bigint"),
				LossBudgetUsdNanos = table.Column<long>("bigint"),
				ConsumedAt = table.Column<DateTimeOffset>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_ad_reward_cap_consumptions", x => x.Id);
			table.CheckConstraint("ck_economy_ad_reward_cap_consumptions_positive", "\"SoftUnits\" > 0 AND \"LossBudgetUsdNanos\" >= 0");
			table.CheckConstraint("ck_economy_ad_reward_cap_consumptions_scope", "\"Scope\" BETWEEN 1 AND 6");
			table.CheckConstraint("ck_economy_ad_reward_cap_consumptions_window", "\"WindowEndsAt\" > \"WindowStartedAt\" AND \"ConsumedAt\" >= \"WindowStartedAt\" AND \"ConsumedAt\" < \"WindowEndsAt\"");
			table.ForeignKey("FK_economy_ad_reward_cap_consumptions_economy_ad_reward_sessio~", x => x.SessionId, "economy_ad_reward_sessions", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("economy_ad_reward_pending_claims", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> sessionId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> tenantId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> sourceStampId = table.Column<Guid>("uuid");
			int? maxLength2 = 128;
			OperationBuilder<AddColumnOperation> completionIdempotencyKeyHash = table.Column<string>("character varying(128)", null, maxLength2);
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> completionRequestHash = table.Column<string>("character varying(128)", null, maxLength2);
			OperationBuilder<AddColumnOperation> deferredAt = table.Column<DateTimeOffset>("timestamp with time zone");
			OperationBuilder<AddColumnOperation> providerReportId = table.Column<Guid>("uuid", null, null, rowVersion: false, null, nullable: true);
			OperationBuilder<AddColumnOperation> confirmedAt = table.Column<DateTimeOffset>("timestamp with time zone", null, null, rowVersion: false, null, nullable: true);
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> confirmationIdempotencyKeyHash = table.Column<string>("character varying(128)", null, maxLength2, rowVersion: false, null, nullable: true);
			maxLength2 = 128;
			return new
			{
				SessionId = sessionId,
				TenantId = tenantId,
				SourceStampId = sourceStampId,
				CompletionIdempotencyKeyHash = completionIdempotencyKeyHash,
				CompletionRequestHash = completionRequestHash,
				DeferredAt = deferredAt,
				ProviderReportId = providerReportId,
				ConfirmedAt = confirmedAt,
				ConfirmationIdempotencyKeyHash = confirmationIdempotencyKeyHash,
				ConfirmationRequestHash = table.Column<string>("character varying(128)", null, maxLength2, rowVersion: false, null, nullable: true)
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_ad_reward_pending_claims", x => x.SessionId);
			table.ForeignKey("FK_economy_ad_reward_pending_claims_economy_ad_provider_report~", x => x.ProviderReportId, "economy_ad_provider_reports", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
			table.ForeignKey("FK_economy_ad_reward_pending_claims_economy_ad_reward_sessions~", x => x.SessionId, "economy_ad_reward_sessions", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("economy_ad_reward_playback_milestones", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> sessionId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> sequence = table.Column<int>("integer");
			OperationBuilder<AddColumnOperation> percentage = table.Column<int>("integer");
			OperationBuilder<AddColumnOperation> observedAt = table.Column<DateTimeOffset>("timestamp with time zone");
			int? maxLength2 = 128;
			return new
			{
				Id = id,
				SessionId = sessionId,
				Sequence = sequence,
				Percentage = percentage,
				ObservedAt = observedAt,
				EvidenceHash = table.Column<string>("character varying(128)", null, maxLength2)
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_ad_reward_playback_milestones", x => x.Id);
			table.CheckConstraint("ck_economy_ad_reward_playback_milestones_percentage", "\"Percentage\" BETWEEN 0 AND 100 AND \"Sequence\" > 0");
			table.ForeignKey("FK_economy_ad_reward_playback_milestones_economy_ad_reward_ses~", x => x.SessionId, "economy_ad_reward_sessions", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("economy_ad_reward_provider_batch_claims", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> tenantId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> providerReportId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> sessionId = table.Column<Guid>("uuid");
			int? maxLength2 = 256;
			return new
			{
				Id = id,
				TenantId = tenantId,
				ProviderReportId = providerReportId,
				SessionId = sessionId,
				BatchId = table.Column<string>("character varying(256)", null, maxLength2),
				ClaimedAt = table.Column<DateTimeOffset>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_ad_reward_provider_batch_claims", x => x.Id);
			table.ForeignKey("FK_economy_ad_reward_provider_batch_claims_economy_ad_provider~", x => x.ProviderReportId, "economy_ad_provider_reports", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
			table.ForeignKey("FK_economy_ad_reward_provider_batch_claims_economy_ad_reward_s~", x => x.SessionId, "economy_ad_reward_sessions", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("economy_ad_reward_provider_proof_inbox", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> tenantId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> sessionId = table.Column<Guid>("uuid");
			int? maxLength2 = 100;
			OperationBuilder<AddColumnOperation> network = table.Column<string>("character varying(100)", null, maxLength2);
			maxLength2 = 256;
			OperationBuilder<AddColumnOperation> providerEventId = table.Column<string>("character varying(256)", null, maxLength2);
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> payloadHash = table.Column<string>("character varying(128)", null, maxLength2);
			maxLength2 = 128;
			OperationBuilder<AddColumnOperation> evidenceHash = table.Column<string>("character varying(128)", null, maxLength2);
			OperationBuilder<AddColumnOperation> signatureVerified = table.Column<bool>("boolean");
			OperationBuilder<AddColumnOperation> receivedAt = table.Column<DateTimeOffset>("timestamp with time zone");
			OperationBuilder<AddColumnOperation> processedAt = table.Column<DateTimeOffset>("timestamp with time zone", null, null, rowVersion: false, null, nullable: true);
			maxLength2 = 256;
			return new
			{
				Id = id,
				TenantId = tenantId,
				SessionId = sessionId,
				Network = network,
				ProviderEventId = providerEventId,
				PayloadHash = payloadHash,
				EvidenceHash = evidenceHash,
				SignatureVerified = signatureVerified,
				ReceivedAt = receivedAt,
				ProcessedAt = processedAt,
				ProcessingError = table.Column<string>("character varying(256)", null, maxLength2, rowVersion: false, null, nullable: true)
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_ad_reward_provider_proof_inbox", x => x.Id);
			table.ForeignKey("FK_economy_ad_reward_provider_proof_inbox_economy_ad_reward_se~", x => x.SessionId, "economy_ad_reward_sessions", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("economy_ad_reward_session_events", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> sessionId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> sequence = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> state = table.Column<int>("integer");
			int? maxLength2 = 128;
			return new
			{
				Id = id,
				SessionId = sessionId,
				Sequence = sequence,
				State = state,
				EvidenceHash = table.Column<string>("character varying(128)", null, maxLength2),
				OccurredAt = table.Column<DateTimeOffset>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_ad_reward_session_events", x => x.Id);
			table.CheckConstraint("ck_economy_ad_reward_session_events_sequence", "\"Sequence\" > 0");
			table.CheckConstraint("ck_economy_ad_reward_session_events_state", "\"State\" BETWEEN 1 AND 7");
			table.ForeignKey("FK_economy_ad_reward_session_events_economy_ad_reward_sessions~", x => x.SessionId, "economy_ad_reward_sessions", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("economy_capability_policy_approvals", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> policyId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> actorId = table.Column<Guid>("uuid");
			int? maxLength2 = 128;
			return new
			{
				Id = id,
				PolicyId = policyId,
				ActorId = actorId,
				ReauthenticationHash = table.Column<string>("character varying(128)", null, maxLength2),
				ApprovedAt = table.Column<DateTimeOffset>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_capability_policy_approvals", x => x.Id);
			table.ForeignKey("FK_economy_capability_policy_approvals_economy_capability_poli~", x => x.PolicyId, "economy_capability_policies", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("economy_capability_receipt_consumptions", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> receiptId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> tenantId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> actorId = table.Column<Guid>("uuid");
			int? maxLength2 = 128;
			return new
			{
				Id = id,
				ReceiptId = receiptId,
				TenantId = tenantId,
				ActorId = actorId,
				OperationFingerprint = table.Column<string>("character varying(128)", null, maxLength2),
				KillSwitchEpoch = table.Column<long>("bigint"),
				ConsumedAt = table.Column<DateTimeOffset>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_capability_receipt_consumptions", x => x.Id);
			table.ForeignKey("FK_economy_capability_receipt_consumptions_economy_capability_~", x => x.ReceiptId, "economy_capability_receipts", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("economy_compliance_outbox", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> evidenceId = table.Column<Guid>("uuid");
			int? maxLength2 = 100;
			OperationBuilder<AddColumnOperation> type = table.Column<string>("character varying(100)", null, maxLength2);
			OperationBuilder<AddColumnOperation> payload = table.Column<string>("jsonb");
			maxLength2 = 128;
			return new
			{
				Id = id,
				EvidenceId = evidenceId,
				Type = type,
				Payload = payload,
				PayloadHash = table.Column<string>("character varying(128)", null, maxLength2),
				OccurredAt = table.Column<DateTimeOffset>("timestamp with time zone"),
				DispatchedAt = table.Column<DateTimeOffset>("timestamp with time zone", null, null, rowVersion: false, null, nullable: true)
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_compliance_outbox", x => x.Id);
			table.ForeignKey("FK_economy_compliance_outbox_economy_compliance_evidence_Evide~", x => x.EvidenceId, "economy_compliance_evidence", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("economy_compliance_hold_events", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> holdId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> sequence = table.Column<int>("integer");
			int? maxLength2 = 50;
			OperationBuilder<AddColumnOperation> kind = table.Column<string>("character varying(50)", null, maxLength2);
			OperationBuilder<AddColumnOperation> actorId = table.Column<Guid>("uuid");
			maxLength2 = 128;
			return new
			{
				Id = id,
				HoldId = holdId,
				Sequence = sequence,
				Kind = kind,
				ActorId = actorId,
				EvidenceHash = table.Column<string>("character varying(128)", null, maxLength2),
				OccurredAt = table.Column<DateTimeOffset>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_compliance_hold_events", x => x.Id);
			table.CheckConstraint("ck_economy_compliance_hold_events_sequence", "\"Sequence\" > 0");
			table.ForeignKey("FK_economy_compliance_hold_events_economy_compliance_holds_Hol~", x => x.HoldId, "economy_compliance_holds", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("economy_entity_graph_edges", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> tenantId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> leftNodeId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> rightNodeId = table.Column<Guid>("uuid");
			int? maxLength2 = 100;
			OperationBuilder<AddColumnOperation> relationship = table.Column<string>("character varying(100)", null, maxLength2);
			OperationBuilder<AddColumnOperation> version = table.Column<long>("bigint");
			maxLength2 = 128;
			return new
			{
				Id = id,
				TenantId = tenantId,
				LeftNodeId = leftNodeId,
				RightNodeId = rightNodeId,
				Relationship = relationship,
				Version = version,
				EvidenceHash = table.Column<string>("character varying(128)", null, maxLength2),
				RecordedAt = table.Column<DateTimeOffset>("timestamp with time zone"),
				SupersededAt = table.Column<DateTimeOffset>("timestamp with time zone", null, null, rowVersion: false, null, nullable: true)
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_entity_graph_edges", x => x.Id);
			table.CheckConstraint("ck_economy_entity_graph_edges_distinct_nodes", "\"LeftNodeId\" <> \"RightNodeId\"");
			table.CheckConstraint("ck_economy_entity_graph_edges_version", "\"Version\" > 0");
			table.ForeignKey("FK_economy_entity_graph_edges_economy_entity_graph_nodes_LeftN~", x => x.LeftNodeId, "economy_entity_graph_nodes", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
			table.ForeignKey("FK_economy_entity_graph_edges_economy_entity_graph_nodes_Right~", x => x.RightNodeId, "economy_entity_graph_nodes", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("economy_kill_switch_release_approvals", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> killSwitchId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> actorId = table.Column<Guid>("uuid");
			int? maxLength2 = 128;
			return new
			{
				Id = id,
				KillSwitchId = killSwitchId,
				ActorId = actorId,
				ReauthenticationHash = table.Column<string>("character varying(128)", null, maxLength2),
				ApprovedAt = table.Column<DateTimeOffset>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_kill_switch_release_approvals", x => x.Id);
			table.ForeignKey("FK_economy_kill_switch_release_approvals_economy_kill_switches~", x => x.KillSwitchId, "economy_kill_switches", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("economy_projection_generation_approvals", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> generation = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> actorId = table.Column<Guid>("uuid");
			int? maxLength2 = 128;
			return new
			{
				Id = id,
				Generation = generation,
				ActorId = actorId,
				ReauthenticationHash = table.Column<string>("character varying(128)", null, maxLength2),
				ApprovedAt = table.Column<DateTimeOffset>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_projection_generation_approvals", x => x.Id);
			table.ForeignKey("FK_economy_projection_generation_approvals_economy_projection_~", x => x.Generation, "economy_projection_generations", "Generation", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("economy_wallet_projection_generations", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> generation = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> walletId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> pendingHard = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> pendingSoft = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> purchasedHard = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> earnedHard = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> restrictedHard = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> soft = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> immatureEarnedHard = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> heldHard = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> heldSoft = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> availableHardToSpend = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> availableSoftToSpend = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> withdrawableHard = table.Column<long>("bigint");
			OperationBuilder<AddColumnOperation> sourceJournalSequence = table.Column<long>("bigint");
			int? maxLength2 = 128;
			return new
			{
				Generation = generation,
				WalletId = walletId,
				PendingHard = pendingHard,
				PendingSoft = pendingSoft,
				PurchasedHard = purchasedHard,
				EarnedHard = earnedHard,
				RestrictedHard = restrictedHard,
				Soft = soft,
				ImmatureEarnedHard = immatureEarnedHard,
				HeldHard = heldHard,
				HeldSoft = heldSoft,
				AvailableHardToSpend = availableHardToSpend,
				AvailableSoftToSpend = availableSoftToSpend,
				WithdrawableHard = withdrawableHard,
				SourceJournalSequence = sourceJournalSequence,
				ProjectionHash = table.Column<string>("character varying(128)", null, maxLength2),
				MatchesLive = table.Column<bool>("boolean"),
				RebuiltAt = table.Column<DateTimeOffset>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_economy_wallet_projection_generations", x => new { x.Generation, x.WalletId });
			table.CheckConstraint("ck_economy_wallet_projection_generations_amounts", "\"Generation\" > 0 AND \"PendingHard\" >= 0 AND \"PendingSoft\" >= 0 AND \"PurchasedHard\" >= 0 AND \"EarnedHard\" >= 0 AND \"RestrictedHard\" >= 0 AND \"Soft\" >= 0 AND \"ImmatureEarnedHard\" >= 0 AND \"HeldHard\" >= 0 AND \"HeldSoft\" >= 0 AND \"AvailableHardToSpend\" >= 0 AND \"AvailableSoftToSpend\" >= 0 AND \"WithdrawableHard\" >= 0");
			table.ForeignKey("FK_economy_wallet_projection_generations_economy_projection_ge~", x => x.Generation, "economy_projection_generations", "Generation", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
			table.ForeignKey("FK_economy_wallet_projection_generations_economy_wallets_Walle~", x => x.WalletId, "economy_wallets", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		migrationBuilder.CreateTable("compliance_financial_crime_decision_consumptions", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> decisionId = table.Column<Guid>("uuid");
			OperationBuilder<AddColumnOperation> tenantId = table.Column<Guid>("uuid");
			int? maxLength2 = 256;
			return new
			{
				Id = id,
				DecisionId = decisionId,
				TenantId = tenantId,
				OperationFingerprint = table.Column<string>("character varying(256)", null, maxLength2),
				ConsumedAt = table.Column<DateTimeOffset>("timestamp with time zone")
			};
		}, null, table =>
		{
			table.PrimaryKey("PK_compliance_financial_crime_decision_consumptions", x => x.Id);
			table.ForeignKey("FK_compliance_financial_crime_decision_consumptions_compliance~", x => x.DecisionId, "compliance_financial_crime_decisions", "Id", null, ReferentialAction.NoAction, ReferentialAction.Restrict);
		});
		InstallCanonicalHashUpgradeGuard(migrationBuilder);
		BackfillEconomyProductionColumns(migrationBuilder);
		migrationBuilder.CreateIndex("IX_economy_risk_review_cases_RiskDecisionId", "economy_risk_review_cases", "RiskDecisionId");
		migrationBuilder.CreateIndex("ux_economy_risk_review_cases_tenant_decision", "economy_risk_review_cases", new string[2] { "TenantId", "RiskDecisionId" }, null, unique: true);
		migrationBuilder.CreateIndex("ux_economy_risk_counters_scope_window", "economy_risk_counters", new string[6] { "TenantId", "Dimension", "SubjectHash", "Operation", "Currency", "WindowStartedAt" }, null, unique: true);
		migrationBuilder.CreateIndex("ux_economy_risk_counter_reservations_group_counter", "economy_risk_counter_reservations", new string[2] { "ReservationGroupId", "RiskCounterId" }, null, unique: true);
		migrationBuilder.AddCheckConstraint("ck_economy_risk_counter_reservations_lifetime", "economy_risk_counter_reservations", "\"ExpiresAt\" > \"ReservedAt\"");
		migrationBuilder.AddCheckConstraint("ck_economy_risk_counter_reservations_state", "economy_risk_counter_reservations", "(\"Status\" = 1 AND \"ConsumedAt\" IS NULL AND \"ReleasedAt\" IS NULL) OR (\"Status\" = 2 AND \"ConsumedAt\" >= \"ReservedAt\" AND \"ReleasedAt\" IS NULL) OR (\"Status\" = 3 AND \"ReleasedAt\" >= \"ReservedAt\" AND \"ConsumedAt\" IS NULL) OR (\"Status\" = 4 AND \"ReleasedAt\" >= \"ExpiresAt\" AND \"ConsumedAt\" IS NULL)");
		migrationBuilder.CreateIndex("ux_economy_protected_change_cooldowns_subject_kind_version", "economy_protected_change_cooldowns", new string[4] { "TenantId", "SubjectId", "Kind", "Version" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_marketplace_settlements_TenantId_BuyerId_SettledAt", "economy_marketplace_settlements", new string[3] { "TenantId", "BuyerId", "SettledAt" });
		migrationBuilder.CreateIndex("IX_economy_marketplace_settlements_TenantId_IdempotencyKey", "economy_marketplace_settlements", new string[2] { "TenantId", "IdempotencyKey" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_marketplace_settlements_TenantId_OrderId", "economy_marketplace_settlements", new string[2] { "TenantId", "OrderId" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_marketplace_settlements_TenantId_SellerId_SettledAt", "economy_marketplace_settlements", new string[3] { "TenantId", "SellerId", "SettledAt" });
		migrationBuilder.AddCheckConstraint("ck_economy_marketplace_settlements_order_snapshot", "economy_marketplace_settlements", "\"Quantity\" > 0 AND \"RefundedQuantity\" BETWEEN 0 AND \"Quantity\" AND \"UnitPriceSnapshot\" >= 0 AND \"PriceVersionSnapshot\" > 0");
		migrationBuilder.AddCheckConstraint("ck_economy_marketplace_settlements_receipt", "economy_marketplace_settlements", "\"ReserveVersion\" > 0 AND \"JournalSequence\" > 0");
		migrationBuilder.CreateIndex("IX_economy_marketplace_refunds_SettlementId", "economy_marketplace_refunds", "SettlementId");
		migrationBuilder.CreateIndex("IX_economy_marketplace_refunds_TenantId_IdempotencyKey", "economy_marketplace_refunds", new string[2] { "TenantId", "IdempotencyKey" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_marketplace_refunds_TenantId_SettlementId_RefundedAt", "economy_marketplace_refunds", new string[3] { "TenantId", "SettlementId", "RefundedAt" });
		migrationBuilder.AddCheckConstraint("ck_economy_marketplace_refunds_quantity", "economy_marketplace_refunds", "\"Quantity\" > 0 AND \"RefundedQuantity\" >= \"Quantity\"");
		migrationBuilder.AddCheckConstraint("ck_economy_marketplace_refunds_versions", "economy_marketplace_refunds", "\"MarketplacePolicyVersion\" > 0 AND \"PolicyVersion\" > 0 AND \"ReserveVersion\" > 0");
		migrationBuilder.CreateIndex("IX_economy_marketplace_refund_legs_SettlementId_Currency", "economy_marketplace_refund_legs", new string[2] { "SettlementId", "Currency" });
		migrationBuilder.CreateIndex("IX_economy_marketplace_currency_policy_versions_TenantId_Produ~", "economy_marketplace_currency_policy_versions", new string[4] { "TenantId", "ProductId", "EffectiveAt", "ExpiresAt" });
		migrationBuilder.AddCheckConstraint("ck_economy_marketplace_currency_policy_versions_dual_control", "economy_marketplace_currency_policy_versions", "\"ProposedBy\" <> \"ApprovedBy\"");
		migrationBuilder.AddCheckConstraint("ck_economy_marketplace_currency_policy_versions_window", "economy_marketplace_currency_policy_versions", "\"ExpiresAt\" > \"EffectiveAt\" AND \"RefundHoldTicks\" > 0");
		migrationBuilder.AddCheckConstraint("ck_economy_journal_entries_hash_algorithm", "economy_journal_entries", "(\"HashAlgorithmVersion\" = 0 AND \"CanonicalPayloadHash\" IS NULL) OR (\"HashAlgorithmVersion\" IN (1, 2) AND length(btrim(\"CanonicalPayloadHash\")) > 0)");
		migrationBuilder.CreateIndex("IX_economy_ad_reward_reconciliations_TenantId_Network_ReportId~", "economy_ad_reward_reconciliations", new string[4] { "TenantId", "Network", "ReportId", "Version" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_ad_provider_reports_TenantId_Network_BatchId_Version", "economy_ad_provider_reports", new string[4] { "TenantId", "Network", "BatchId", "Version" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_ad_provider_reports_TenantId_Network_ReportId_Versi~", "economy_ad_provider_reports", new string[4] { "TenantId", "Network", "ReportId", "Version" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_ad_network_policy_versions_TenantId_Network_Effecti~", "economy_ad_network_policy_versions", new string[4] { "TenantId", "Network", "EffectiveAt", "ExpiresAt" });
		migrationBuilder.AddCheckConstraint("ck_economy_ad_network_policy_versions_caps", "economy_ad_network_policy_versions", "\"MaximumUserSoftUnits\" > 0 AND \"MaximumDeviceSoftUnits\" > 0 AND \"MaximumIpSoftUnits\" > 0 AND \"MaximumAsnSoftUnits\" > 0 AND \"MaximumNetworkSoftUnits\" > 0 AND \"MaximumGlobalSoftUnits\" > 0 AND \"FundedLossBudgetUsdNanos\" > 0");
		migrationBuilder.AddCheckConstraint("ck_economy_ad_network_policy_versions_dual_control", "economy_ad_network_policy_versions", "\"ProposedBy\" <> \"ApprovedBy\"");
		migrationBuilder.AddCheckConstraint("ck_economy_ad_network_policy_versions_values", "economy_ad_network_policy_versions", "\"Version\" > 0 AND \"EstimatedNetEcpmUsdNanos\" > 0 AND \"MaximumRewardSoftUnits\" > 0 AND \"MaximumFocusLossTicks\" >= 0 AND \"ReportStaleAfterTicks\" > 0 AND \"Ranking\" >= 0 AND \"BudgetWindowTicks\" > 0");
		migrationBuilder.CreateIndex("IX_compliance_financial_crime_case_events_CaseId_Sequence", "compliance_financial_crime_case_events", new string[2] { "CaseId", "Sequence" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_compliance_financial_crime_cases_TenantId_SubjectHash_State", "compliance_financial_crime_cases", new string[3] { "TenantId", "SubjectHash", "State" });
		migrationBuilder.CreateIndex("IX_compliance_financial_crime_decision_consumptions_DecisionId", "compliance_financial_crime_decision_consumptions", "DecisionId", null, unique: true);
		migrationBuilder.CreateIndex("IX_compliance_financial_crime_decisions_CaseId_Version", "compliance_financial_crime_decisions", new string[2] { "CaseId", "Version" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_compliance_financial_crime_regulatory_references_CaseId_Kin~", "compliance_financial_crime_regulatory_references", new string[3] { "CaseId", "Kind", "ReferenceHash" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_compliance_financial_crime_screenings_CaseId", "compliance_financial_crime_screenings", "CaseId");
		migrationBuilder.CreateIndex("IX_compliance_financial_crime_screenings_NextScreenAt", "compliance_financial_crime_screenings", "NextScreenAt");
		migrationBuilder.CreateIndex("ux_financial_crime_screenings_provider_event", "compliance_financial_crime_screenings", new string[3] { "Provider", "Environment", "ProviderEventId" }, null, unique: true);
		migrationBuilder.CreateIndex("ux_financial_crime_screenings_subject_version", "compliance_financial_crime_screenings", new string[3] { "TenantId", "SubjectHash", "Version" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_compliance_financial_crime_transaction_signals_CaseId", "compliance_financial_crime_transaction_signals", "CaseId");
		migrationBuilder.CreateIndex("IX_compliance_financial_crime_transaction_signals_RequestHash", "compliance_financial_crime_transaction_signals", "RequestHash", null, unique: true);
		migrationBuilder.CreateIndex("IX_compliance_financial_crime_transaction_signals_TenantId_Sub~", "compliance_financial_crime_transaction_signals", new string[3] { "TenantId", "SubjectHash", "ObservedAt" });
		migrationBuilder.CreateIndex("IX_compliance_sumsub_applicant_bindings_ApplicantId", "compliance_sumsub_applicant_bindings", "ApplicantId", null, unique: true);
		migrationBuilder.CreateIndex("IX_compliance_sumsub_applicant_bindings_IdempotencyKeyHash", "compliance_sumsub_applicant_bindings", "IdempotencyKeyHash", null, unique: true);
		migrationBuilder.CreateIndex("IX_compliance_sumsub_applicant_bindings_TenantId_SubjectHash", "compliance_sumsub_applicant_bindings", new string[2] { "TenantId", "SubjectHash" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_compliance_sumsub_webhook_inbox_ApplicantId_IssuedAt", "compliance_sumsub_webhook_inbox", new string[2] { "ApplicantId", "IssuedAt" });
		migrationBuilder.CreateIndex("IX_compliance_sumsub_webhook_inbox_ProviderEventId", "compliance_sumsub_webhook_inbox", "ProviderEventId", null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_ad_reward_cap_consumptions_SessionId_Scope", "economy_ad_reward_cap_consumptions", new string[2] { "SessionId", "Scope" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_ad_reward_cap_consumptions_TenantId_Scope_SubjectHa~", "economy_ad_reward_cap_consumptions", new string[4] { "TenantId", "Scope", "SubjectHash", "ConsumedAt" });
		migrationBuilder.CreateIndex("IX_economy_ad_reward_pending_claims_ProviderReportId", "economy_ad_reward_pending_claims", "ProviderReportId");
		migrationBuilder.CreateIndex("IX_economy_ad_reward_pending_claims_TenantId_CompletionIdempot~", "economy_ad_reward_pending_claims", new string[2] { "TenantId", "CompletionIdempotencyKeyHash" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_ad_reward_pending_claims_TenantId_ConfirmationIdemp~", "economy_ad_reward_pending_claims", new string[2] { "TenantId", "ConfirmationIdempotencyKeyHash" }, null, unique: true, "\"ConfirmationIdempotencyKeyHash\" IS NOT NULL");
		migrationBuilder.CreateIndex("IX_economy_ad_reward_playback_milestones_SessionId_Sequence", "economy_ad_reward_playback_milestones", new string[2] { "SessionId", "Sequence" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_ad_reward_provider_batch_claims_ProviderReportId_Se~", "economy_ad_reward_provider_batch_claims", new string[2] { "ProviderReportId", "SessionId" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_ad_reward_provider_batch_claims_SessionId", "economy_ad_reward_provider_batch_claims", "SessionId");
		migrationBuilder.CreateIndex("IX_economy_ad_reward_provider_proof_inbox_SessionId", "economy_ad_reward_provider_proof_inbox", "SessionId");
		migrationBuilder.CreateIndex("IX_economy_ad_reward_provider_proof_inbox_TenantId_Network_Pro~", "economy_ad_reward_provider_proof_inbox", new string[3] { "TenantId", "Network", "ProviderEventId" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_ad_reward_session_events_SessionId_Sequence", "economy_ad_reward_session_events", new string[2] { "SessionId", "Sequence" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_ad_reward_sessions_TenantId_NonceHash", "economy_ad_reward_sessions", new string[2] { "TenantId", "NonceHash" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_ad_reward_sessions_TenantId_StartIdempotencyKeyHash", "economy_ad_reward_sessions", new string[2] { "TenantId", "StartIdempotencyKeyHash" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_ad_reward_sessions_TenantId_UserId_IssuedAt", "economy_ad_reward_sessions", new string[3] { "TenantId", "UserId", "IssuedAt" });
		migrationBuilder.CreateIndex("IX_economy_admin_withdrawal_dispatch_outbox_CompletedAt_Availa~", "economy_admin_withdrawal_dispatch_outbox", new string[3] { "CompletedAt", "AvailableAt", "LeaseExpiresAt" });
		migrationBuilder.CreateIndex("IX_economy_admin_withdrawal_dispatch_outbox_RunId", "economy_admin_withdrawal_dispatch_outbox", "RunId", null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_anchor_verifications_ExternalAnchorId_VerifiedAt", "economy_anchor_verifications", new string[2] { "ExternalAnchorId", "VerifiedAt" });
		migrationBuilder.CreateIndex("IX_economy_bounty_expiration_events_BountyId", "economy_bounty_expiration_events", "BountyId", null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_bounty_expiration_events_RecordedAt_Id", "economy_bounty_expiration_events", new string[2] { "RecordedAt", "Id" });
		migrationBuilder.CreateIndex("ux_economy_capability_policies_active_scope", "economy_capability_policies", "ScopeKey", null, unique: true, "\"IsActive\"");
		migrationBuilder.CreateIndex("ux_economy_capability_policies_request_hash", "economy_capability_policies", "RequestHash", null, unique: true);
		migrationBuilder.CreateIndex("ux_economy_capability_policies_scope_version", "economy_capability_policies", new string[2] { "ScopeKey", "Version" }, null, unique: true);
		migrationBuilder.CreateIndex("ux_economy_capability_policy_approvals_policy_actor", "economy_capability_policy_approvals", new string[2] { "PolicyId", "ActorId" }, null, unique: true);
		migrationBuilder.CreateIndex("ux_economy_capability_receipt_consumptions_receipt", "economy_capability_receipt_consumptions", "ReceiptId", null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_capability_receipts_RiskDecisionId", "economy_capability_receipts", "RiskDecisionId");
		migrationBuilder.CreateIndex("ux_economy_capability_receipts_hash", "economy_capability_receipts", "ReceiptHash", null, unique: true);
		migrationBuilder.CreateIndex("ux_economy_capability_receipts_tenant_operation", "economy_capability_receipts", new string[2] { "TenantId", "OperationFingerprint" }, null, unique: true);
		migrationBuilder.CreateIndex("ux_economy_compliance_evidence_provider_event", "economy_compliance_evidence", new string[3] { "Provider", "Environment", "ProviderEventId" }, null, unique: true);
		migrationBuilder.CreateIndex("ux_economy_compliance_evidence_subject_version", "economy_compliance_evidence", new string[4] { "TenantId", "SubjectHash", "EvidenceKind", "Version" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_compliance_hold_events_HoldId_Sequence", "economy_compliance_hold_events", new string[2] { "HoldId", "Sequence" }, null, unique: true);
		migrationBuilder.CreateIndex("ix_economy_compliance_holds_active_scope", "economy_compliance_holds", new string[3] { "ScopeKey", "ReleasedAt", "ExpiresAt" });
		migrationBuilder.CreateIndex("IX_economy_compliance_holds_TenantId_SubjectHash_ExpiresAt", "economy_compliance_holds", new string[3] { "TenantId", "SubjectHash", "ExpiresAt" });
		migrationBuilder.CreateIndex("ux_economy_compliance_holds_idempotency", "economy_compliance_holds", "IdempotencyKeyHash", null, unique: true);
		migrationBuilder.CreateIndex("ux_economy_compliance_inbox_provider_event", "economy_compliance_inbox", new string[3] { "Provider", "Environment", "ProviderEventId" }, null, unique: true);
		migrationBuilder.CreateIndex("ux_economy_compliance_outbox_evidence", "economy_compliance_outbox", "EvidenceId", null, unique: true);
		migrationBuilder.CreateIndex("ux_economy_custody_observations_provider_asset_version", "economy_custody_observations", new string[3] { "Provider", "AssetKey", "Version" }, null, unique: true);
		migrationBuilder.CreateIndex("ux_economy_custody_reconciliations_reserve", "economy_custody_reconciliations", "ReserveVersion", null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_entity_graph_edges_LeftNodeId", "economy_entity_graph_edges", "LeftNodeId");
		migrationBuilder.CreateIndex("IX_economy_entity_graph_edges_RightNodeId", "economy_entity_graph_edges", "RightNodeId");
		migrationBuilder.CreateIndex("ux_economy_entity_graph_edges_pair_version", "economy_entity_graph_edges", new string[4] { "TenantId", "LeftNodeId", "RightNodeId", "Version" }, null, unique: true);
		migrationBuilder.CreateIndex("ux_economy_entity_graph_nodes_identity_version", "economy_entity_graph_nodes", new string[4] { "TenantId", "Type", "IdentityHash", "Version" }, null, unique: true);
		migrationBuilder.CreateIndex("ix_economy_journal_verification_checkpoints_sequence", "economy_journal_verification_checkpoints", new string[2] { "ToSequence", "CompletedAt" });
		migrationBuilder.CreateIndex("ux_economy_kill_switch_release_approvals_switch_actor", "economy_kill_switch_release_approvals", new string[2] { "KillSwitchId", "ActorId" }, null, unique: true);
		migrationBuilder.CreateIndex("ux_economy_kill_switches_active_scope", "economy_kill_switches", "ScopeKey", null, unique: true, "\"IsActive\"");
		migrationBuilder.CreateIndex("ux_economy_kill_switches_request_hash", "economy_kill_switches", "RequestHash", null, unique: true);
		migrationBuilder.CreateIndex("ux_economy_kill_switches_scope_epoch", "economy_kill_switches", new string[2] { "ScopeKey", "Epoch" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_marketplace_events_SettlementId_Sequence", "economy_marketplace_events", new string[2] { "SettlementId", "Sequence" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_marketplace_outbox_PublishedAt_OccurredAt", "economy_marketplace_outbox", new string[2] { "PublishedAt", "OccurredAt" });
		migrationBuilder.CreateIndex("IX_economy_marketplace_outbox_SettlementId", "economy_marketplace_outbox", "SettlementId");
		migrationBuilder.CreateIndex("IX_economy_marketplace_refund_debts_RefundId", "economy_marketplace_refund_debts", "RefundId");
		migrationBuilder.CreateIndex("IX_economy_marketplace_refund_debts_SettlementId", "economy_marketplace_refund_debts", "SettlementId");
		migrationBuilder.CreateIndex("IX_economy_marketplace_refund_debts_TenantId_ResponsibleWallet~", "economy_marketplace_refund_debts", new string[3] { "TenantId", "ResponsibleWalletId", "RecordedAt" });
		migrationBuilder.CreateIndex("IX_economy_payout_connect_accounts_Provider_Environment_Provid~", "economy_payout_connect_accounts", new string[3] { "Provider", "Environment", "ProviderAccountId" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_payout_connect_accounts_State_ExpiresAt", "economy_payout_connect_accounts", new string[2] { "State", "ExpiresAt" });
		migrationBuilder.CreateIndex("IX_economy_payout_dispatch_outbox_CompletedAt_AvailableAt_Leas~", "economy_payout_dispatch_outbox", new string[3] { "CompletedAt", "AvailableAt", "LeaseExpiresAt" });
		migrationBuilder.CreateIndex("IX_economy_payout_dispatch_outbox_OperationId", "economy_payout_dispatch_outbox", "OperationId", null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_projection_generation_approvals_Generation_ActorId", "economy_projection_generation_approvals", new string[2] { "Generation", "ActorId" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_projection_generations_Generation", "economy_projection_generations", "Generation", null, unique: true);
		migrationBuilder.CreateIndex("ux_economy_projection_generations_active", "economy_projection_generations", "IsActive", null, unique: true, "\"IsActive\"");
		migrationBuilder.CreateIndex("ux_economy_reserve_proposals_version", "economy_reserve_proposals", "Version", null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_wallet_projection_generations_Generation_MatchesLive", "economy_wallet_projection_generations", new string[2] { "Generation", "MatchesLive" });
		migrationBuilder.CreateIndex("IX_economy_wallet_projection_generations_WalletId", "economy_wallet_projection_generations", "WalletId");
		migrationBuilder.CreateIndex("ux_economy_worker_leases_name", "economy_worker_leases", "Name", null, unique: true);
		migrationBuilder.CreateIndex("IX_trust_safety_appeals_TenantId_SubjectHash_State", "trust_safety_appeals", new string[3] { "TenantId", "SubjectHash", "State" });
		migrationBuilder.CreateIndex("IX_trust_safety_event_inbox_EventId", "trust_safety_event_inbox", "EventId", null, unique: true);
		migrationBuilder.CreateIndex("IX_trust_safety_event_inbox_TenantId_SubjectHash_Version", "trust_safety_event_inbox", new string[3] { "TenantId", "SubjectHash", "Version" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_trust_safety_subject_states_ExpiresAt", "trust_safety_subject_states", "ExpiresAt");
		InstallEconomyProductionSecurity(migrationBuilder);
		HardenPayoutFifoEligibility.InstallHardenedPayoutFifoEligibility(migrationBuilder);
	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		RemoveEconomyProductionSecurity(migrationBuilder);
		RestoreStrictImmutableGuard(migrationBuilder);
		migrationBuilder.DropTable("compliance_financial_crime_case_events");
		migrationBuilder.DropTable("compliance_financial_crime_decision_consumptions");
		migrationBuilder.DropTable("compliance_financial_crime_regulatory_references");
		migrationBuilder.DropTable("compliance_financial_crime_screenings");
		migrationBuilder.DropTable("compliance_financial_crime_transaction_signals");
		migrationBuilder.DropTable("compliance_sumsub_applicant_bindings");
		migrationBuilder.DropTable("compliance_sumsub_webhook_inbox");
		migrationBuilder.DropTable("economy_ad_reward_cap_consumptions");
		migrationBuilder.DropTable("economy_ad_reward_pending_claims");
		migrationBuilder.DropTable("economy_ad_reward_playback_milestones");
		migrationBuilder.DropTable("economy_ad_reward_provider_batch_claims");
		migrationBuilder.DropTable("economy_ad_reward_provider_proof_inbox");
		migrationBuilder.DropTable("economy_ad_reward_session_events");
		migrationBuilder.DropTable("economy_admin_withdrawal_dispatch_outbox");
		migrationBuilder.DropTable("economy_anchor_verifications");
		migrationBuilder.DropTable("economy_bounty_expiration_events");
		migrationBuilder.DropTable("economy_capability_policy_approvals");
		migrationBuilder.DropTable("economy_capability_receipt_consumptions");
		migrationBuilder.DropTable("economy_compliance_hold_events");
		migrationBuilder.DropTable("economy_compliance_inbox");
		migrationBuilder.DropTable("economy_compliance_outbox");
		migrationBuilder.DropTable("economy_custody_observations");
		migrationBuilder.DropTable("economy_custody_reconciliations");
		migrationBuilder.DropTable("economy_entity_graph_edges");
		migrationBuilder.DropTable("economy_journal_verification_checkpoints");
		migrationBuilder.DropTable("economy_kill_switch_release_approvals");
		migrationBuilder.DropTable("economy_marketplace_events");
		migrationBuilder.DropTable("economy_marketplace_outbox");
		migrationBuilder.DropTable("economy_marketplace_refund_debts");
		migrationBuilder.DropTable("economy_payout_connect_accounts");
		migrationBuilder.DropTable("economy_payout_dispatch_outbox");
		migrationBuilder.DropTable("economy_projection_generation_approvals");
		migrationBuilder.DropTable("economy_reserve_proposals");
		migrationBuilder.DropTable("economy_wallet_projection_generations");
		migrationBuilder.DropTable("economy_worker_leases");
		migrationBuilder.DropTable("trust_safety_appeals");
		migrationBuilder.DropTable("trust_safety_event_inbox");
		migrationBuilder.DropTable("trust_safety_subject_states");
		migrationBuilder.DropTable("compliance_financial_crime_decisions");
		migrationBuilder.DropTable("economy_ad_reward_sessions");
		migrationBuilder.DropTable("economy_capability_policies");
		migrationBuilder.DropTable("economy_capability_receipts");
		migrationBuilder.DropTable("economy_compliance_holds");
		migrationBuilder.DropTable("economy_compliance_evidence");
		migrationBuilder.DropTable("economy_entity_graph_nodes");
		migrationBuilder.DropTable("economy_kill_switches");
		migrationBuilder.DropTable("economy_projection_generations");
		migrationBuilder.DropTable("compliance_financial_crime_cases");
		migrationBuilder.DropIndex("IX_economy_risk_review_cases_RiskDecisionId", "economy_risk_review_cases");
		migrationBuilder.DropIndex("ux_economy_risk_review_cases_tenant_decision", "economy_risk_review_cases");
		migrationBuilder.DropIndex("ux_economy_risk_counters_scope_window", "economy_risk_counters");
		migrationBuilder.DropIndex("ux_economy_risk_counter_reservations_group_counter", "economy_risk_counter_reservations");
		migrationBuilder.DropCheckConstraint("ck_economy_risk_counter_reservations_lifetime", "economy_risk_counter_reservations");
		migrationBuilder.DropCheckConstraint("ck_economy_risk_counter_reservations_state", "economy_risk_counter_reservations");
		migrationBuilder.DropIndex("ux_economy_protected_change_cooldowns_subject_kind_version", "economy_protected_change_cooldowns");
		migrationBuilder.DropIndex("IX_economy_marketplace_settlements_TenantId_BuyerId_SettledAt", "economy_marketplace_settlements");
		migrationBuilder.DropIndex("IX_economy_marketplace_settlements_TenantId_IdempotencyKey", "economy_marketplace_settlements");
		migrationBuilder.DropIndex("IX_economy_marketplace_settlements_TenantId_OrderId", "economy_marketplace_settlements");
		migrationBuilder.DropIndex("IX_economy_marketplace_settlements_TenantId_SellerId_SettledAt", "economy_marketplace_settlements");
		migrationBuilder.DropCheckConstraint("ck_economy_marketplace_settlements_order_snapshot", "economy_marketplace_settlements");
		migrationBuilder.DropCheckConstraint("ck_economy_marketplace_settlements_receipt", "economy_marketplace_settlements");
		migrationBuilder.DropIndex("IX_economy_marketplace_refunds_SettlementId", "economy_marketplace_refunds");
		migrationBuilder.DropIndex("IX_economy_marketplace_refunds_TenantId_IdempotencyKey", "economy_marketplace_refunds");
		migrationBuilder.DropIndex("IX_economy_marketplace_refunds_TenantId_SettlementId_RefundedAt", "economy_marketplace_refunds");
		migrationBuilder.DropCheckConstraint("ck_economy_marketplace_refunds_quantity", "economy_marketplace_refunds");
		migrationBuilder.DropCheckConstraint("ck_economy_marketplace_refunds_versions", "economy_marketplace_refunds");
		migrationBuilder.DropIndex("IX_economy_marketplace_refund_legs_SettlementId_Currency", "economy_marketplace_refund_legs");
		migrationBuilder.DropPrimaryKey("PK_economy_marketplace_currency_policy_versions", "economy_marketplace_currency_policy_versions");
		migrationBuilder.DropIndex("IX_economy_marketplace_currency_policy_versions_TenantId_Produ~", "economy_marketplace_currency_policy_versions");
		migrationBuilder.DropCheckConstraint("ck_economy_marketplace_currency_policy_versions_dual_control", "economy_marketplace_currency_policy_versions");
		migrationBuilder.DropCheckConstraint("ck_economy_marketplace_currency_policy_versions_window", "economy_marketplace_currency_policy_versions");
		migrationBuilder.DropCheckConstraint("ck_economy_journal_entries_hash_algorithm", "economy_journal_entries");
		migrationBuilder.DropIndex("IX_economy_ad_reward_reconciliations_TenantId_Network_ReportId~", "economy_ad_reward_reconciliations");
		migrationBuilder.DropPrimaryKey("PK_economy_ad_reward_accumulators", "economy_ad_reward_accumulators");
		migrationBuilder.DropIndex("IX_economy_ad_provider_reports_TenantId_Network_BatchId_Version", "economy_ad_provider_reports");
		migrationBuilder.DropIndex("IX_economy_ad_provider_reports_TenantId_Network_ReportId_Versi~", "economy_ad_provider_reports");
		migrationBuilder.DropPrimaryKey("PK_economy_ad_network_policy_versions", "economy_ad_network_policy_versions");
		migrationBuilder.DropIndex("IX_economy_ad_network_policy_versions_TenantId_Network_Effecti~", "economy_ad_network_policy_versions");
		migrationBuilder.DropCheckConstraint("ck_economy_ad_network_policy_versions_caps", "economy_ad_network_policy_versions");
		migrationBuilder.DropCheckConstraint("ck_economy_ad_network_policy_versions_dual_control", "economy_ad_network_policy_versions");
		migrationBuilder.DropCheckConstraint("ck_economy_ad_network_policy_versions_values", "economy_ad_network_policy_versions");
		migrationBuilder.DropColumn("TenantId", "economy_risk_review_cases");
		migrationBuilder.DropColumn("TenantId", "economy_risk_counters");
		migrationBuilder.DropColumn("ConsumedAt", "economy_risk_counter_reservations");
		migrationBuilder.DropColumn("ExpiresAt", "economy_risk_counter_reservations");
		migrationBuilder.DropColumn("InputFingerprint", "economy_risk_counter_reservations");
		migrationBuilder.DropColumn("ReleasedAt", "economy_risk_counter_reservations");
		migrationBuilder.DropColumn("ReservationGroupId", "economy_risk_counter_reservations");
		migrationBuilder.DropColumn("Status", "economy_risk_counter_reservations");
		migrationBuilder.DropColumn("TenantId", "economy_protected_change_cooldowns");
		migrationBuilder.DropColumn("CapabilityReceiptHash", "economy_marketplace_settlements");
		migrationBuilder.DropColumn("CapabilityReceiptId", "economy_marketplace_settlements");
		migrationBuilder.DropColumn("EntitlementStatus", "economy_marketplace_settlements");
		migrationBuilder.DropColumn("EvidenceHashes", "economy_marketplace_settlements");
		migrationBuilder.DropColumn("FiatCurrencySnapshot", "economy_marketplace_settlements");
		migrationBuilder.DropColumn("JournalHash", "economy_marketplace_settlements");
		migrationBuilder.DropColumn("JournalSequence", "economy_marketplace_settlements");
		migrationBuilder.DropColumn("JurisdictionCode", "economy_marketplace_settlements");
		migrationBuilder.DropColumn("KillSwitchEpoch", "economy_marketplace_settlements");
		migrationBuilder.DropColumn("OrderLineItemId", "economy_marketplace_settlements");
		migrationBuilder.DropColumn("OrderSnapshotHash", "economy_marketplace_settlements");
		migrationBuilder.DropColumn("PostingId", "economy_marketplace_settlements");
		migrationBuilder.DropColumn("PriceVersionSnapshot", "economy_marketplace_settlements");
		migrationBuilder.DropColumn("ProductPricingVersionId", "economy_marketplace_settlements");
		migrationBuilder.DropColumn("Quantity", "economy_marketplace_settlements");
		migrationBuilder.DropColumn("RefundedQuantity", "economy_marketplace_settlements");
		migrationBuilder.DropColumn("ReserveVersion", "economy_marketplace_settlements");
		migrationBuilder.DropColumn("RiskDecisionId", "economy_marketplace_settlements");
		migrationBuilder.DropColumn("TenantId", "economy_marketplace_settlements");
		migrationBuilder.DropColumn("UnitPriceSnapshot", "economy_marketplace_settlements");
		migrationBuilder.DropColumn("RemainingUnits", "economy_marketplace_settlement_credits");
		migrationBuilder.DropColumn("CapabilityReceiptHash", "economy_marketplace_refunds");
		migrationBuilder.DropColumn("CapabilityReceiptId", "economy_marketplace_refunds");
		migrationBuilder.DropColumn("EvidenceHashes", "economy_marketplace_refunds");
		migrationBuilder.DropColumn("JournalHash", "economy_marketplace_refunds");
		migrationBuilder.DropColumn("JurisdictionCode", "economy_marketplace_refunds");
		migrationBuilder.DropColumn("KillSwitchEpoch", "economy_marketplace_refunds");
		migrationBuilder.DropColumn("MarketplacePolicyVersion", "economy_marketplace_refunds");
		migrationBuilder.DropColumn("PolicyVersion", "economy_marketplace_refunds");
		migrationBuilder.DropColumn("PostingId", "economy_marketplace_refunds");
		migrationBuilder.DropColumn("Quantity", "economy_marketplace_refunds");
		migrationBuilder.DropColumn("ReasonCode", "economy_marketplace_refunds");
		migrationBuilder.DropColumn("ReasonHash", "economy_marketplace_refunds");
		migrationBuilder.DropColumn("RefundedQuantity", "economy_marketplace_refunds");
		migrationBuilder.DropColumn("ReserveVersion", "economy_marketplace_refunds");
		migrationBuilder.DropColumn("RiskDecisionId", "economy_marketplace_refunds");
		migrationBuilder.DropColumn("TenantId", "economy_marketplace_refunds");
		migrationBuilder.DropColumn("ReservationId", "economy_marketplace_funding_fragments");
		migrationBuilder.DropColumn("TenantId", "economy_marketplace_currency_policy_versions");
		migrationBuilder.DropColumn("ApprovedBy", "economy_marketplace_currency_policy_versions");
		migrationBuilder.DropColumn("CanonicalPayload", "economy_marketplace_currency_policy_versions");
		migrationBuilder.DropColumn("ExpiresAt", "economy_marketplace_currency_policy_versions");
		migrationBuilder.DropColumn("KeyId", "economy_marketplace_currency_policy_versions");
		migrationBuilder.DropColumn("PayloadHash", "economy_marketplace_currency_policy_versions");
		migrationBuilder.DropColumn("PlatformFeeWalletId", "economy_marketplace_currency_policy_versions");
		migrationBuilder.DropColumn("ProposedBy", "economy_marketplace_currency_policy_versions");
		migrationBuilder.DropColumn("PublishedAt", "economy_marketplace_currency_policy_versions");
		migrationBuilder.DropColumn("RefundHoldTicks", "economy_marketplace_currency_policy_versions");
		migrationBuilder.DropColumn("Signature", "economy_marketplace_currency_policy_versions");
		migrationBuilder.DropColumn("CanonicalPayloadHash", "economy_journal_entries");
		migrationBuilder.DropColumn("HashAlgorithmVersion", "economy_journal_entries");
		migrationBuilder.DropColumn("TenantId", "economy_ad_reward_reconciliations");
		migrationBuilder.DropColumn("CapabilityReceiptHash", "economy_ad_reward_completions");
		migrationBuilder.DropColumn("CapabilityReceiptId", "economy_ad_reward_completions");
		migrationBuilder.DropColumn("DestinationHash", "economy_ad_reward_completions");
		migrationBuilder.DropColumn("EvidenceHashes", "economy_ad_reward_completions");
		migrationBuilder.DropColumn("JurisdictionCode", "economy_ad_reward_completions");
		migrationBuilder.DropColumn("KillSwitchEpoch", "economy_ad_reward_completions");
		migrationBuilder.DropColumn("ProviderHash", "economy_ad_reward_completions");
		migrationBuilder.DropColumn("ReserveVersion", "economy_ad_reward_completions");
		migrationBuilder.DropColumn("RiskDecisionId", "economy_ad_reward_completions");
		migrationBuilder.DropColumn("TenantId", "economy_ad_reward_completions");
		migrationBuilder.DropColumn("Version", "economy_ad_reward_completions");
		migrationBuilder.DropColumn("TenantId", "economy_ad_reward_budget_consumptions");
		migrationBuilder.DropColumn("TenantId", "economy_ad_reward_attributions");
		migrationBuilder.DropColumn("TenantId", "economy_ad_reward_accumulators");
		migrationBuilder.DropColumn("PayloadHash", "economy_ad_provider_reports");
		migrationBuilder.DropColumn("ProcessedAt", "economy_ad_provider_reports");
		migrationBuilder.DropColumn("ProcessingError", "economy_ad_provider_reports");
		migrationBuilder.DropColumn("ReceivedAt", "economy_ad_provider_reports");
		migrationBuilder.DropColumn("SignatureVerified", "economy_ad_provider_reports");
		migrationBuilder.DropColumn("TenantId", "economy_ad_provider_reports");
		migrationBuilder.DropColumn("TenantId", "economy_ad_network_policy_versions");
		migrationBuilder.DropColumn("ApprovedBy", "economy_ad_network_policy_versions");
		migrationBuilder.DropColumn("BudgetWindowTicks", "economy_ad_network_policy_versions");
		migrationBuilder.DropColumn("CanonicalPayload", "economy_ad_network_policy_versions");
		migrationBuilder.DropColumn("FundedLossBudgetUsdNanos", "economy_ad_network_policy_versions");
		migrationBuilder.DropColumn("KeyId", "economy_ad_network_policy_versions");
		migrationBuilder.DropColumn("MaximumAsnSoftUnits", "economy_ad_network_policy_versions");
		migrationBuilder.DropColumn("MaximumDeviceSoftUnits", "economy_ad_network_policy_versions");
		migrationBuilder.DropColumn("MaximumGlobalSoftUnits", "economy_ad_network_policy_versions");
		migrationBuilder.DropColumn("MaximumIpSoftUnits", "economy_ad_network_policy_versions");
		migrationBuilder.DropColumn("MaximumNetworkSoftUnits", "economy_ad_network_policy_versions");
		migrationBuilder.DropColumn("MaximumUserSoftUnits", "economy_ad_network_policy_versions");
		migrationBuilder.DropColumn("PayloadHash", "economy_ad_network_policy_versions");
		migrationBuilder.DropColumn("ProposedBy", "economy_ad_network_policy_versions");
		migrationBuilder.DropColumn("ProviderCertified", "economy_ad_network_policy_versions");
		migrationBuilder.DropColumn("ProviderHash", "economy_ad_network_policy_versions");
		migrationBuilder.DropColumn("PublishedAt", "economy_ad_network_policy_versions");
		migrationBuilder.DropColumn("Signature", "economy_ad_network_policy_versions");
		migrationBuilder.AddPrimaryKey("PK_economy_marketplace_currency_policy_versions", "economy_marketplace_currency_policy_versions", new string[2] { "ProductId", "Version" });
		migrationBuilder.AddPrimaryKey("PK_economy_ad_reward_accumulators", "economy_ad_reward_accumulators", new string[2] { "WalletId", "Network" });
		migrationBuilder.AddPrimaryKey("PK_economy_ad_network_policy_versions", "economy_ad_network_policy_versions", new string[2] { "Network", "Version" });
		migrationBuilder.CreateIndex("ux_economy_risk_review_cases_decision", "economy_risk_review_cases", "RiskDecisionId", null, unique: true);
		migrationBuilder.CreateIndex("ux_economy_risk_counters_scope_window", "economy_risk_counters", new string[5] { "Dimension", "SubjectHash", "Operation", "Currency", "WindowStartedAt" }, null, unique: true);
		migrationBuilder.CreateIndex("ux_economy_protected_change_cooldowns_subject_kind", "economy_protected_change_cooldowns", new string[2] { "SubjectId", "Kind" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_marketplace_settlements_BuyerId_SettledAt", "economy_marketplace_settlements", new string[2] { "BuyerId", "SettledAt" });
		migrationBuilder.CreateIndex("IX_economy_marketplace_settlements_IdempotencyKey", "economy_marketplace_settlements", "IdempotencyKey", null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_marketplace_settlements_OrderId", "economy_marketplace_settlements", "OrderId", null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_marketplace_settlements_SellerId_SettledAt", "economy_marketplace_settlements", new string[2] { "SellerId", "SettledAt" });
		migrationBuilder.CreateIndex("IX_economy_marketplace_refunds_IdempotencyKey", "economy_marketplace_refunds", "IdempotencyKey", null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_marketplace_refunds_SettlementId_RefundedAt", "economy_marketplace_refunds", new string[2] { "SettlementId", "RefundedAt" });
		migrationBuilder.CreateIndex("IX_economy_marketplace_refund_legs_SettlementId_Currency", "economy_marketplace_refund_legs", new string[2] { "SettlementId", "Currency" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_marketplace_currency_policy_versions_ProductId_Effe~", "economy_marketplace_currency_policy_versions", new string[2] { "ProductId", "EffectiveAt" });
		migrationBuilder.CreateIndex("IX_economy_ad_reward_reconciliations_Network_ReportId_Version", "economy_ad_reward_reconciliations", new string[3] { "Network", "ReportId", "Version" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_ad_provider_reports_Network_BatchId_Version", "economy_ad_provider_reports", new string[3] { "Network", "BatchId", "Version" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_ad_provider_reports_Network_ReportId_Version", "economy_ad_provider_reports", new string[3] { "Network", "ReportId", "Version" }, null, unique: true);
		migrationBuilder.CreateIndex("IX_economy_ad_network_policy_versions_Network_EffectiveAt_Expi~", "economy_ad_network_policy_versions", new string[3] { "Network", "EffectiveAt", "ExpiresAt" });
		migrationBuilder.AddCheckConstraint("ck_economy_ad_network_policy_versions_values", "economy_ad_network_policy_versions", "\"Version\" > 0 AND \"EstimatedNetEcpmUsdNanos\" > 0 AND \"MaximumRewardSoftUnits\" > 0 AND \"MaximumFocusLossTicks\" >= 0 AND \"ReportStaleAfterTicks\" > 0 AND \"Ranking\" >= 0");
	}

	/// <inheritdoc />
	protected override void BuildTargetModel(ModelBuilder modelBuilder)
	{
		modelBuilder.HasAnnotation("ProductVersion", "10.0.9").HasAnnotation("Relational:MaxIdentifierLength", 63);
		modelBuilder.UseIdentityByDefaultColumns();
		modelBuilder.Entity("GameGuild.AI.AiConversationLog", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("FinishReason").HasMaxLength(64).HasColumnType("character varying(64)");
			b.Property<int?>("InputTokens").HasColumnType("integer");
			b.Property<string>("Model").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTime>("OccurredAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Outcome").IsRequired().HasMaxLength(32)
				.HasColumnType("character varying(32)");
			b.Property<string>("OutcomeCode").HasMaxLength(64).HasColumnType("character varying(64)");
			b.Property<string>("OutcomeReason").HasMaxLength(512).HasColumnType("character varying(512)");
			b.Property<int?>("OutputTokens").HasColumnType("integer");
			b.Property<string>("Provider").IsRequired().HasMaxLength(32)
				.HasColumnType("character varying(32)");
			b.Property<string>("RequestKind").IsRequired().HasMaxLength(16)
				.HasColumnType("character varying(16)");
			b.Property<string>("RequestText").IsRequired().HasColumnType("text");
			b.Property<string>("ResponseText").HasColumnType("text");
			b.Property<string>("SystemPrompt").HasColumnType("text");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<int?>("TotalTokens").HasColumnType("integer");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("Outcome");
			b.HasIndex("Provider");
			b.HasIndex("UserId");
			b.HasIndex("TenantId", "OccurredAt");
			b.ToTable("ai_conversation_logs", (string?)null);
		});
		modelBuilder.Entity("GameGuild.AI.AiPromptTemplate", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").HasColumnType("uuid");
			b.Property<string>("Category").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("CreatedByUserId").HasColumnType("uuid");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(1024).HasColumnType("character varying(1024)");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<bool>("IsSystemTemplate").HasColumnType("boolean");
			b.Property<string>("Key").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("Name").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<string>("Prompt").IsRequired().HasColumnType("text");
			b.Property<string>("SystemPrompt").HasColumnType("text");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("UpdatedByUserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("Category");
			b.HasIndex("IsActive");
			b.HasIndex("TenantId", "Key");
			b.ToTable("ai_prompt_templates", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Analytics.AnalyticsEvent", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Environment").HasMaxLength(50).HasColumnType("character varying(50)");
			b.Property<string>("EventName").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("IpAddress").HasMaxLength(45).HasColumnType("character varying(45)");
			b.Property<string>("PageUrl").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<string>("Properties").HasColumnType("jsonb");
			b.Property<string>("Referrer").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<string>("SessionId").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("Timestamp").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("UserAgent").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<Guid?>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("EventName");
			b.HasIndex("TenantId");
			b.HasIndex("Timestamp");
			b.HasIndex("UserId");
			b.HasIndex("EventName", "Timestamp");
			b.HasIndex("UserId", "Timestamp");
			b.ToTable("analytics_events");
		});
		modelBuilder.Entity("GameGuild.Analytics.Dashboard", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<bool>("IsDefault").HasColumnType("boolean");
			b.Property<string>("Slug").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Title").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("Slug").IsUnique();
			b.ToTable("analytics_dashboards");
		});
		modelBuilder.Entity("GameGuild.Analytics.DashboardWidget", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("Configuration").HasColumnType("jsonb");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("DashboardId").HasColumnType("uuid");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("SortOrder").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Title").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("Type").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("DashboardId");
			b.ToTable("analytics_dashboard_widgets");
		});
		modelBuilder.Entity("GameGuild.Analytics.KpiDefinition", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("AggregateField").HasMaxLength(200).HasColumnType("character varying(200)");
			b.Property<string>("AggregationFunction").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<string>("EventName").HasMaxLength(200).HasColumnType("character varying(200)");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<string>("Name").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("Name").IsUnique();
			b.ToTable("analytics_kpi_definitions");
		});
		modelBuilder.Entity("GameGuild.Assets.AssetContent", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("BucketName").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<string>("ContentHash").IsRequired().HasMaxLength(64)
				.HasColumnType("character varying(64)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<double?>("DurationSeconds").HasColumnType("double precision");
			b.Property<int?>("Height").HasColumnType("integer");
			b.Property<bool>("IsDeletable").ValueGeneratedOnAdd().HasColumnType("boolean")
				.HasDefaultValue(true);
			b.Property<int>("Kind").HasColumnType("integer");
			b.Property<DateTime?>("MarkedForDeletionAt").HasColumnType("timestamp with time zone");
			b.Property<string>("MimeType").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<DateTime?>("ModerationCompletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("ModerationLabels").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<string>("ModerationReviewNotes").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<DateTime?>("ModerationReviewedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("ModerationReviewedBy").HasColumnType("uuid");
			b.Property<string>("ModerationStatus").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<string>("ObjectKey").IsRequired().HasMaxLength(500)
				.HasColumnType("character varying(500)");
			b.Property<string>("PerceptualHash").HasMaxLength(64).HasColumnType("character varying(64)");
			b.Property<int>("ReferenceCount").ValueGeneratedOnAdd().HasColumnType("integer")
				.HasDefaultValue(1);
			b.Property<byte[]>("RowVersion").IsConcurrencyToken().ValueGeneratedOnAddOrUpdate()
				.HasColumnType("bytea");
			b.Property<long>("SizeBytes").HasColumnType("bigint");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<DateTime?>("VirusScanCompletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("VirusScanStatus").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<int?>("Width").HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ContentHash").IsUnique().HasDatabaseName("IX_AssetContents_ContentHash");
			b.HasIndex("ModerationStatus").HasDatabaseName("IX_AssetContents_ModerationStatus");
			b.HasIndex("VirusScanStatus").HasDatabaseName("IX_AssetContents_VirusScanStatus");
			b.HasIndex("ReferenceCount", "MarkedForDeletionAt").HasDatabaseName("IX_AssetContents_GC");
			b.ToTable("asset_contents", "assets");
		});
		modelBuilder.Entity("GameGuild.Assets.AssetFolder", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("AllowedAuthoritiesJson").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<string>("AllowedTeamIdsJson").HasMaxLength(4000).HasColumnType("character varying(4000)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Name").IsRequired().HasMaxLength(255)
				.HasColumnType("character varying(255)");
			b.Property<Guid?>("ParentFolderId").HasColumnType("uuid");
			b.Property<Guid>("ParentResourceId").HasColumnType("uuid");
			b.Property<string>("ParentResourceType").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<string>("RestrictionMode").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ParentResourceType", "ParentResourceId", "ParentFolderId", "Name").IsUnique().HasFilter("\"DeletedAt\" IS NULL");
			b.ToTable("asset_folders", "assets");
		});
		modelBuilder.Entity("GameGuild.Assets.AssetReference", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<long>("AccessCount").ValueGeneratedOnAdd().HasColumnType("bigint")
				.HasDefaultValue(0L);
			b.Property<string>("AccessPolicy").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<string>("AltText").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<Guid>("AssetContentId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("CreatedByUserId").HasColumnType("uuid");
			b.Property<int>("CurrentRevisionNumber").ValueGeneratedOnAdd().HasColumnType("integer")
				.HasDefaultValue(0);
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<string>("DisplayName").IsRequired().HasMaxLength(255)
				.HasColumnType("character varying(255)");
			b.Property<DateTime?>("DownloadWindowExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("FolderId").HasColumnType("uuid");
			b.Property<Guid?>("GrantedByOrderId").HasColumnType("uuid");
			b.Property<DateTime?>("LastAccessedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("OriginalFilename").HasMaxLength(255).HasColumnType("character varying(255)");
			b.Property<Guid?>("ParentResourceId").HasColumnType("uuid");
			b.Property<string>("ParentResourceType").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<string>("Tags").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("AccessPolicy").HasDatabaseName("IX_AssetReferences_AccessPolicy");
			b.HasIndex("AssetContentId").HasDatabaseName("IX_AssetReferences_ContentId");
			b.HasIndex("CreatedByUserId").HasDatabaseName("IX_AssetReferences_UserId");
			b.HasIndex("FolderId");
			b.HasIndex("ParentResourceType", "ParentResourceId").HasDatabaseName("IX_AssetReferences_Parent");
			b.ToTable("asset_references", "assets");
		});
		modelBuilder.Entity("GameGuild.Assets.AssetReferenceRevision", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("AssetContentId").HasColumnType("uuid");
			b.Property<Guid>("AssetReferenceId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("CreatedByUserId").HasColumnType("uuid");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Note").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<int>("RevisionNumber").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("AssetContentId");
			b.HasIndex("AssetReferenceId", "RevisionNumber").IsUnique();
			b.ToTable("asset_reference_revisions", "assets");
		});
		modelBuilder.Entity("GameGuild.Assets.AssetReport", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("AssetReferenceId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Decision").HasMaxLength(50).HasColumnType("character varying(50)");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Details").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<string>("Reason").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<Guid>("ReportedByUserId").HasColumnType("uuid");
			b.Property<string>("ReviewNotes").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<DateTime?>("ReviewedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("ReviewedByUserId").HasColumnType("uuid");
			b.Property<string>("Status").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("AssetReferenceId").HasDatabaseName("IX_AssetReports_ReferenceId");
			b.HasIndex("ReportedByUserId").HasDatabaseName("IX_AssetReports_ReporterId");
			b.HasIndex("Status").HasDatabaseName("IX_AssetReports_Status");
			b.HasIndex("AssetReferenceId", "ReportedByUserId").IsUnique().HasDatabaseName("IX_AssetReports_Unique_UserReport");
			b.ToTable("asset_reports", "assets");
		});
		modelBuilder.Entity("GameGuild.Assets.AssetScopedAccessGrant", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("AssetReferenceId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("GrantedByUserId").HasColumnType("uuid");
			b.Property<DateTime?>("RevokedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("ScopeId").HasColumnType("uuid");
			b.Property<string>("ScopeType").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ExpiresAt");
			b.HasIndex("AssetReferenceId", "UserId", "ScopeType", "ScopeId");
			b.ToTable("asset_scoped_access_grants", "assets");
		});
		modelBuilder.Entity("GameGuild.Assets.TransformedAsset", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("BucketName").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Height").HasColumnType("integer");
			b.Property<DateTime>("LastAccessedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("MimeType").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<string>("ObjectKey").IsRequired().HasMaxLength(500)
				.HasColumnType("character varying(500)");
			b.Property<long>("SizeBytes").HasColumnType("bigint");
			b.Property<Guid>("SourceContentId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("TransformationSpec").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<int>("Width").HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("LastAccessedAt").HasDatabaseName("IX_TransformedAssets_LastAccessed");
			b.HasIndex("SourceContentId");
			b.HasIndex("SourceContentId", "TransformationSpec").IsUnique().HasDatabaseName("IX_TransformedAssets_Source_Transform");
			b.ToTable("transformed_assets", "assets");
		});
		modelBuilder.Entity("GameGuild.Commerce.Billing.BillingWebhookEvent", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("ErrorMessage").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<string>("EventSchemaVersion").HasMaxLength(50).HasColumnType("character varying(50)");
			b.Property<string>("EventType").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<string>("ExternalEventId").IsRequired().HasMaxLength(255)
				.HasColumnType("character varying(255)");
			b.Property<string>("Headers").HasColumnType("text");
			b.Property<bool>("IsFailed").HasColumnType("boolean");
			b.Property<bool?>("IsLiveMode").HasColumnType("boolean");
			b.Property<bool>("IsProcessed").HasColumnType("boolean");
			b.Property<string>("Payload").IsRequired().HasColumnType("text");
			b.Property<DateTime?>("ProcessedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("ProcessingAttempts").IsConcurrencyToken().HasColumnType("integer");
			b.Property<string>("Provider").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<string>("ProviderAccountId").HasMaxLength(255).HasColumnType("character varying(255)");
			b.Property<string>("ProviderEnvironment").HasMaxLength(32).HasColumnType("character varying(32)");
			b.Property<string>("ProviderMonetaryLeg").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<string>("ProviderObjectId").HasMaxLength(255).HasColumnType("character varying(255)");
			b.Property<string>("ProviderObjectType").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<Guid?>("SubscriptionId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<string>("WebhookEndpointId").HasMaxLength(255).HasColumnType("character varying(255)");
			b.HasKey("Id");
			b.HasIndex("CreatedAt").HasDatabaseName("ix_billing_webhook_events_created_at");
			b.HasIndex("EventType").HasDatabaseName("ix_billing_webhook_events_event_type");
			b.HasIndex("IsFailed").HasDatabaseName("ix_billing_webhook_events_is_failed");
			b.HasIndex("IsProcessed").HasDatabaseName("ix_billing_webhook_events_is_processed");
			b.HasIndex("SubscriptionId").HasDatabaseName("ix_billing_webhook_events_subscription_id");
			b.HasIndex("TenantId").HasDatabaseName("ix_billing_webhook_events_tenant_id");
			b.HasIndex("ExternalEventId", "Provider").IsUnique().HasDatabaseName("ix_billing_webhook_events_external_id_provider")
				.HasFilter("(\"ProviderEnvironment\" IS NULL AND \"ProviderAccountId\" IS NULL AND \"WebhookEndpointId\" IS NULL)");
			b.HasIndex("Provider", "ProviderEnvironment", "ProviderAccountId", "ProviderObjectId", "ProviderMonetaryLeg").HasDatabaseName("ix_billing_webhook_events_provider_object_leg").HasFilter("\"ProviderEnvironment\" IS NOT NULL AND \"ProviderAccountId\" IS NOT NULL AND \"ProviderObjectId\" IS NOT NULL AND \"ProviderMonetaryLeg\" IS NOT NULL")
				.HasAnnotation("Npgsql:CreatedConcurrently", true);
			b.HasIndex("Provider", "ProviderEnvironment", "ProviderAccountId", "WebhookEndpointId", "ExternalEventId").IsUnique().HasDatabaseName("ix_billing_webhook_events_provider_scope_event")
				.HasFilter("\"ProviderEnvironment\" IS NOT NULL AND \"ProviderAccountId\" IS NOT NULL AND \"WebhookEndpointId\" IS NOT NULL")
				.HasAnnotation("Npgsql:CreatedConcurrently", true);
			b.ToTable("billing_webhook_events", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_billing_webhook_events_provider_environment", "(\"ProviderEnvironment\" IS NULL OR (\"ProviderEnvironment\" = 'live' AND \"IsLiveMode\" = true) OR (\"ProviderEnvironment\" = 'test' AND \"IsLiveMode\" = false))");
				t.HasCheckConstraint("ck_billing_webhook_events_provider_object_complete", "((\"ProviderObjectId\" IS NULL AND \"ProviderObjectType\" IS NULL AND \"ProviderMonetaryLeg\" IS NULL) OR (\"ProviderObjectId\" IS NOT NULL AND \"ProviderObjectType\" IS NOT NULL AND \"ProviderMonetaryLeg\" IS NOT NULL AND \"ProviderEnvironment\" IS NOT NULL AND \"ProviderAccountId\" IS NOT NULL))");
				t.HasCheckConstraint("ck_billing_webhook_events_provider_scope_complete", "((\"ProviderEnvironment\" IS NULL AND \"ProviderAccountId\" IS NULL AND \"WebhookEndpointId\" IS NULL AND \"IsLiveMode\" IS NULL) OR (\"ProviderEnvironment\" IS NOT NULL AND \"ProviderAccountId\" IS NOT NULL AND \"WebhookEndpointId\" IS NOT NULL AND \"IsLiveMode\" IS NOT NULL))");
			});
		});
		modelBuilder.Entity("GameGuild.Commerce.Billing.Invoice", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<decimal>("AmountPaid").HasColumnType("decimal(18,2)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Currency").IsRequired().HasMaxLength(3)
				.HasColumnType("character varying(3)");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<decimal>("DiscountAmount").HasColumnType("decimal(18,2)");
			b.Property<DateTime?>("DueDate").HasColumnType("timestamp with time zone");
			b.Property<string>("ExternalId").HasMaxLength(255).HasColumnType("character varying(255)");
			b.Property<string>("InvoiceNumber").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<DateTime?>("IssuedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Metadata").HasMaxLength(4000).HasColumnType("character varying(4000)");
			b.Property<DateTime?>("PaidAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("PaymentId").HasColumnType("uuid");
			b.Property<DateTime?>("PeriodEnd").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("PeriodStart").HasColumnType("timestamp with time zone");
			b.Property<int>("Status").HasColumnType("integer");
			b.Property<Guid>("SubscriptionId").HasColumnType("uuid");
			b.Property<decimal>("Subtotal").HasColumnType("decimal(18,2)");
			b.Property<decimal>("TaxAmount").HasColumnType("decimal(18,2)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<decimal>("Total").HasColumnType("decimal(18,2)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<string>("VoidReason").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<DateTime?>("VoidedAt").HasColumnType("timestamp with time zone");
			b.HasKey("Id");
			b.HasIndex("DueDate");
			b.HasIndex("ExternalId").IsUnique();
			b.HasIndex("InvoiceNumber").IsUnique();
			b.HasIndex("IssuedAt");
			b.HasIndex("SubscriptionId");
			b.HasIndex("TenantId", "Status");
			b.HasIndex(new string[1] { "PaymentId" }, "IX_invoices_PaymentId_Unique").IsUnique();
			b.ToTable("invoices");
		});
		modelBuilder.Entity("GameGuild.Commerce.Orders.Order", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Currency").IsRequired().ValueGeneratedOnAdd()
				.HasMaxLength(3)
				.HasColumnType("character varying(3)")
				.HasDefaultValue("USD");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<decimal>("DiscountTotal").HasPrecision(10, 2).HasColumnType("decimal(10,2)");
			b.Property<string>("ExternalPaymentId").HasMaxLength(200).HasColumnType("character varying(200)");
			b.Property<DateTime?>("FulfilledAt").HasColumnType("timestamp with time zone");
			b.Property<string>("IdempotencyKey").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<string>("IpAddress").HasMaxLength(45).HasColumnType("character varying(45)");
			b.Property<string>("Metadata").HasColumnType("jsonb");
			b.Property<int>("OrderType").HasColumnType("integer");
			b.Property<DateTime?>("PaidAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("PaymentId").HasColumnType("uuid");
			b.Property<string>("PaymentMethod").HasMaxLength(50).HasColumnType("character varying(50)");
			b.Property<string>("PaymentProviderReference").HasMaxLength(200).HasColumnType("character varying(200)");
			b.Property<decimal?>("RefundAmount").HasPrecision(10, 2).HasColumnType("decimal(10,2)");
			b.Property<string>("RefundReason").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<DateTime?>("RefundedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Status").HasColumnType("integer");
			b.Property<decimal>("Subtotal").HasPrecision(10, 2).HasColumnType("decimal(10,2)");
			b.Property<Guid?>("TargetSubscriptionId").HasColumnType("uuid");
			b.Property<decimal>("TaxAmount").HasPrecision(10, 2).HasColumnType("decimal(10,2)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<decimal>("Total").HasPrecision(10, 2).HasColumnType("decimal(10,2)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("UserAgent").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("CreatedAt");
			b.HasIndex("IdempotencyKey").IsUnique();
			b.HasIndex("Status");
			b.HasIndex("TenantId");
			b.HasIndex("UserId");
			b.ToTable("orders");
		});
		modelBuilder.Entity("GameGuild.Commerce.Orders.OrderAuditLog", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("AdditionalContext").HasColumnType("jsonb");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("ExternalPaymentId").HasMaxLength(200).HasColumnType("character varying(200)");
			b.Property<string>("InitiatedBy").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<string>("IpAddress").HasMaxLength(45).HasColumnType("character varying(45)");
			b.Property<int>("NewStatus").HasColumnType("integer");
			b.Property<DateTime>("OccurredAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("OrderId").HasColumnType("uuid");
			b.Property<int>("PreviousStatus").HasColumnType("integer");
			b.Property<string>("Reason").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("NewStatus");
			b.HasIndex("OccurredAt");
			b.HasIndex("OrderId");
			b.HasIndex("TenantId");
			b.ToTable("order_audit_logs");
		});
		modelBuilder.Entity("GameGuild.Commerce.Orders.OrderLineItem", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<decimal>("BasePriceSnapshot").HasPrecision(10, 2).HasColumnType("decimal(10,2)");
			b.Property<string>("BillingIntervalSnapshot").HasMaxLength(20).HasColumnType("character varying(20)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("CurrencySnapshot").IsRequired().HasMaxLength(3)
				.HasColumnType("character varying(3)");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<decimal>("DiscountAmount").HasPrecision(10, 2).HasColumnType("decimal(10,2)");
			b.Property<bool>("IsSubscription").HasColumnType("boolean");
			b.Property<decimal>("LineTotal").HasPrecision(10, 2).HasColumnType("decimal(10,2)");
			b.Property<Guid>("OrderId").HasColumnType("uuid");
			b.Property<int>("PriceVersionSnapshot").HasColumnType("integer");
			b.Property<string>("PricingTierNameSnapshot").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<Guid>("ProductId").HasColumnType("uuid");
			b.Property<string>("ProductNameSnapshot").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<Guid>("ProductPricingId").HasColumnType("uuid");
			b.Property<Guid>("ProductPricingVersionId").HasColumnType("uuid");
			b.Property<string>("PromoCodesApplied").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<int>("Quantity").HasColumnType("integer");
			b.Property<decimal?>("SalePriceSnapshot").HasPrecision(10, 2).HasColumnType("decimal(10,2)");
			b.Property<Guid?>("SubscriptionPlanId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<decimal>("UnitPriceSnapshot").HasPrecision(10, 2).HasColumnType("decimal(10,2)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("UserProductId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("OrderId");
			b.HasIndex("ProductId");
			b.HasIndex("UserProductId");
			b.ToTable("order_line_items");
		});
		modelBuilder.Entity("GameGuild.Commerce.Payments.Payment", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<decimal>("Amount").HasColumnType("decimal(18,2)");
			b.Property<string>("CancellationReason").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<DateTime?>("CancelledAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("CancelledByUserId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Currency").IsRequired().HasMaxLength(3)
				.HasColumnType("character varying(3)");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<string>("ErrorCode").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<string>("ExternalCustomerId").HasMaxLength(255).HasColumnType("character varying(255)");
			b.Property<string>("ExternalPaymentId").HasMaxLength(255).HasColumnType("character varying(255)");
			b.Property<string>("ExternalTransactionId").HasMaxLength(255).HasColumnType("character varying(255)");
			b.Property<string>("FailureReason").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<string>("IdempotencyKey").IsRequired().HasMaxLength(255)
				.HasColumnType("character varying(255)");
			b.Property<Guid?>("InvoiceId").HasColumnType("uuid");
			b.Property<int>("MaxRetries").HasColumnType("integer");
			b.Property<string>("Metadata").HasColumnType("jsonb");
			b.Property<DateTime?>("NextRetryAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("OrderId").HasColumnType("uuid");
			b.Property<string>("PaymentMethodId").HasMaxLength(255).HasColumnType("character varying(255)");
			b.Property<DateTime?>("ProcessedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Provider").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<string>("ProviderAccountId").HasMaxLength(255).HasColumnType("character varying(255)");
			b.Property<string>("ProviderEnvironment").HasMaxLength(32).HasColumnType("character varying(32)");
			b.Property<string>("ProviderMonetaryLeg").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<string>("ProviderObjectId").HasMaxLength(255).HasColumnType("character varying(255)");
			b.Property<string>("ProviderObjectType").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<string>("RefundId").HasMaxLength(255).HasColumnType("character varying(255)");
			b.Property<string>("RefundReason").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<decimal>("RefundedAmount").HasColumnType("decimal(18,2)");
			b.Property<DateTime?>("RefundedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("RetryCount").HasColumnType("integer");
			b.Property<int>("Status").HasColumnType("integer");
			b.Property<Guid?>("SubscriptionId").HasColumnType("uuid");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ExternalPaymentId").IsUnique();
			b.HasIndex("IdempotencyKey").IsUnique();
			b.HasIndex("SubscriptionId");
			b.HasIndex("TenantId", "Status");
			b.HasIndex("Provider", "ProviderEnvironment", "ProviderAccountId", "ProviderObjectId", "ProviderMonetaryLeg").IsUnique().HasDatabaseName("ix_payments_provider_object_leg")
				.HasFilter("\"ProviderEnvironment\" IS NOT NULL AND \"ProviderAccountId\" IS NOT NULL AND \"ProviderObjectId\" IS NOT NULL AND \"ProviderMonetaryLeg\" IS NOT NULL")
				.HasAnnotation("Npgsql:CreatedConcurrently", true);
			b.ToTable("payments", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_payments_provider_environment", "\"ProviderEnvironment\" IS NULL OR \"ProviderEnvironment\" IN ('test', 'live')");
				t.HasCheckConstraint("ck_payments_provider_mapping_complete", "((\"ProviderEnvironment\" IS NULL AND \"ProviderAccountId\" IS NULL AND \"ProviderObjectId\" IS NULL AND \"ProviderObjectType\" IS NULL AND \"ProviderMonetaryLeg\" IS NULL) OR (\"ProviderEnvironment\" IS NOT NULL AND \"ProviderAccountId\" IS NOT NULL AND \"ProviderObjectId\" IS NOT NULL AND \"ProviderObjectType\" IS NOT NULL AND \"ProviderMonetaryLeg\" IS NOT NULL))");
				t.HasCheckConstraint("ck_payments_stripe_value_mapping_required", "(lower(\"Provider\") <> 'stripe' OR \"Status\" NOT IN (1, 2, 5, 6, 7) OR (\"ProviderEnvironment\" IS NOT NULL AND \"ProviderAccountId\" IS NOT NULL AND \"ProviderObjectId\" IS NOT NULL AND \"ProviderObjectType\" IS NOT NULL AND \"ProviderMonetaryLeg\" IS NOT NULL))");
			});
		});
		modelBuilder.Entity("GameGuild.Commerce.Payments.TaxJurisdiction", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("Code").IsRequired().HasMaxLength(20)
				.HasColumnType("character varying(20)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<bool>("IsReverseChargeApplicable").HasColumnType("boolean");
			b.Property<string>("Name").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<Guid?>("ParentJurisdictionId").HasColumnType("uuid");
			b.Property<string>("TaxRegistrationNumber").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<int>("Type").HasColumnType("integer");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("Code").IsUnique().HasDatabaseName("ix_tax_jurisdictions_code");
			b.HasIndex("IsActive").HasDatabaseName("ix_tax_jurisdictions_is_active");
			b.HasIndex("ParentJurisdictionId").HasDatabaseName("ix_tax_jurisdictions_parent_id");
			b.HasIndex("Type").HasDatabaseName("ix_tax_jurisdictions_type");
			b.ToTable("tax_jurisdictions", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Commerce.Payments.TaxRate", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<DateTime>("EffectiveFrom").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("EffectiveTo").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<decimal?>("MaximumTaxableAmount").HasColumnType("decimal(18,2)");
			b.Property<decimal?>("MinimumTaxableAmount").HasColumnType("decimal(18,2)");
			b.Property<string>("ProductCategory").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<decimal>("Rate").HasColumnType("decimal(5,4)");
			b.Property<Guid>("TaxJurisdictionId").HasColumnType("uuid");
			b.Property<int>("TaxType").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("EffectiveFrom").HasDatabaseName("ix_tax_rates_effective_from");
			b.HasIndex("EffectiveTo").HasDatabaseName("ix_tax_rates_effective_to");
			b.HasIndex("IsActive").HasDatabaseName("ix_tax_rates_is_active");
			b.HasIndex("TaxJurisdictionId").HasDatabaseName("ix_tax_rates_jurisdiction_id");
			b.HasIndex("TaxType").HasDatabaseName("ix_tax_rates_tax_type");
			b.ToTable("tax_rates", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("CK_TaxRate_Rate_Valid", "\"Rate\" >= 0 AND \"Rate\" <= 1");
			});
		});
		modelBuilder.Entity("GameGuild.Commerce.Payments.TaxRule", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<int?>("CustomerTypeFilter").HasColumnType("integer");
			b.Property<Guid?>("DefaultTaxRateId").HasColumnType("uuid");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<DateTime?>("EffectiveFrom").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("EffectiveTo").HasColumnType("timestamp with time zone");
			b.Property<string>("ExemptionConditions").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<bool>("IsReverseCharge").HasColumnType("boolean");
			b.Property<bool>("IsTaxInclusive").HasColumnType("boolean");
			b.Property<decimal?>("MaximumAmount").HasColumnType("decimal(18,2)");
			b.Property<decimal?>("MinimumAmount").HasColumnType("decimal(18,2)");
			b.Property<string>("Name").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<int>("Priority").HasColumnType("integer");
			b.Property<string>("ProductCategories").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<int>("RuleType").HasColumnType("integer");
			b.Property<Guid>("TaxJurisdictionId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("DefaultTaxRateId");
			b.HasIndex("EffectiveFrom").HasDatabaseName("ix_tax_rules_effective_from");
			b.HasIndex("EffectiveTo").HasDatabaseName("ix_tax_rules_effective_to");
			b.HasIndex("IsActive").HasDatabaseName("ix_tax_rules_is_active");
			b.HasIndex("Priority").HasDatabaseName("ix_tax_rules_priority");
			b.HasIndex("RuleType").HasDatabaseName("ix_tax_rules_rule_type");
			b.HasIndex("TaxJurisdictionId").HasDatabaseName("ix_tax_rules_jurisdiction_id");
			b.ToTable("tax_rules", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Commerce.Payments.UserWallet", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<decimal>("Balance").HasColumnType("decimal(18,2)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Currency").IsRequired().HasMaxLength(3)
				.HasColumnType("character varying(3)");
			b.Property<decimal?>("DailyLimit").HasColumnType("decimal(18,2)");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<bool>("IsLocked").HasColumnType("boolean");
			b.Property<DateTime?>("LastTransactionAt").HasColumnType("timestamp with time zone");
			b.Property<string>("LockReason").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<decimal?>("MonthlyLimit").HasColumnType("decimal(18,2)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("Currency");
			b.HasIndex("IsActive");
			b.HasIndex("UserId").IsUnique();
			b.ToTable("user_wallets", delegate(TableBuilder t)
			{
				t.HasCheckConstraint("CK_UserWallet_Balance_NonNegative", "\"Balance\" >= 0");
				t.HasCheckConstraint("CK_UserWallet_UserId_NotEmpty", "\"UserId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
			});
		});
		modelBuilder.Entity("GameGuild.Commerce.Payments.WalletTransaction", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<decimal>("Amount").HasColumnType("decimal(18,2)");
			b.Property<decimal>("BalanceAfter").HasColumnType("decimal(18,2)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").IsRequired().HasMaxLength(500)
				.HasColumnType("character varying(500)");
			b.Property<string>("Metadata").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<string>("Notes").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<DateTime?>("ProcessedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("ReferenceId").HasMaxLength(200).HasColumnType("character varying(200)");
			b.Property<int>("Status").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<int>("Type").HasColumnType("integer");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<Guid>("WalletId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("CreatedAt");
			b.HasIndex("ReferenceId");
			b.HasIndex("Status");
			b.HasIndex("Type");
			b.HasIndex("WalletId");
			b.ToTable("wallet_transactions");
		});
		modelBuilder.Entity("GameGuild.Commerce.PricingRule", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<int?>("BuyQuantity").HasColumnType("integer");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("CustomerSegment").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<string>("DaysOfWeek").HasMaxLength(20).HasColumnType("character varying(20)");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<decimal?>("DiscountAmount").HasColumnType("decimal(18,2)");
			b.Property<decimal?>("DiscountPercentage").HasColumnType("decimal(5,2)");
			b.Property<DateTime?>("EndDate").HasColumnType("timestamp with time zone");
			b.Property<decimal?>("FixedPrice").HasColumnType("decimal(18,2)");
			b.Property<int?>("GetQuantity").HasColumnType("integer");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<int?>("MaxQuantity").HasColumnType("integer");
			b.Property<int?>("MinQuantity").HasColumnType("integer");
			b.Property<string>("Name").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<int>("Priority").HasColumnType("integer");
			b.Property<Guid?>("ProductId").HasColumnType("uuid");
			b.Property<string>("Region").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<int>("RuleType").HasColumnType("integer");
			b.Property<DateTime?>("StartDate").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("TimeEnd").HasMaxLength(5).HasColumnType("character varying(5)");
			b.Property<string>("TimeStart").HasMaxLength(5).HasColumnType("character varying(5)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("EndDate");
			b.HasIndex("IsActive");
			b.HasIndex("Priority");
			b.HasIndex("ProductId");
			b.HasIndex("RuleType");
			b.HasIndex("StartDate");
			b.ToTable("pricing_rules");
		});
		modelBuilder.Entity("GameGuild.Commerce.PricingRuleTier", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<decimal?>("DiscountPercentage").HasColumnType("decimal(5,2)");
			b.Property<int?>("MaxQuantity").HasColumnType("integer");
			b.Property<int?>("MinQuantity").HasColumnType("integer");
			b.Property<decimal?>("Price").HasColumnType("decimal(18,2)");
			b.Property<Guid>("PricingRuleId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("MaxQuantity");
			b.HasIndex("MinQuantity");
			b.HasIndex("PricingRuleId");
			b.ToTable("pricing_rule_tiers");
		});
		modelBuilder.Entity("GameGuild.Commerce.Products.PricingTier", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Currency").IsRequired().ValueGeneratedOnAdd()
				.HasMaxLength(3)
				.HasColumnType("character varying(3)")
				.HasDefaultValue("USD");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<int>("DisplayOrder").HasColumnType("integer");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<int?>("MaxQuantity").HasColumnType("integer");
			b.Property<int>("MinQuantity").HasColumnType("integer");
			b.Property<string>("Name").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<Guid>("ProductId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<decimal>("UnitPrice").HasPrecision(10, 2).HasColumnType("decimal(10,2)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("IsActive");
			b.HasIndex("MinQuantity");
			b.HasIndex("ProductId");
			b.ToTable("pricing_tiers");
		});
		modelBuilder.Entity("GameGuild.Commerce.Products.Product", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("CreatorId").HasColumnType("uuid");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(4000).HasColumnType("character varying(4000)");
			b.Property<string>("ImageUrl").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<bool>("IsBundle").HasColumnType("boolean");
			b.Property<bool>("IsPublished").HasColumnType("boolean");
			b.Property<string>("Name").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("ShortDescription").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<int>("Type").HasColumnType("integer");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("CreatorId");
			b.HasIndex("IsPublished");
			b.HasIndex("Name");
			b.HasIndex("Type");
			b.ToTable("Products");
		});
		modelBuilder.Entity("GameGuild.Commerce.Products.ProductBundleItem", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<decimal?>("BundleDiscountPercentage").HasColumnType("decimal(5,2)");
			b.Property<Guid>("BundleProductId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("DisplayOrder").HasColumnType("integer");
			b.Property<Guid>("IncludedProductId").HasColumnType("uuid");
			b.Property<bool>("IsRequired").HasColumnType("boolean");
			b.Property<int>("Quantity").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("BundleProductId");
			b.HasIndex("IncludedProductId");
			b.HasIndex("BundleProductId", "IncludedProductId").IsUnique();
			b.ToTable("product_bundle_items");
		});
		modelBuilder.Entity("GameGuild.Commerce.Products.ProductCommissionConfig", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<decimal>("AffiliateCommissionPercentage").HasColumnType("decimal(5,2)");
			b.Property<bool>("CommissionOnRecurring").HasColumnType("boolean");
			b.Property<int>("CookieDurationDays").HasColumnType("integer");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<decimal>("MaxAffiliateDiscount").HasColumnType("decimal(5,2)");
			b.Property<int?>("MaxRecurringPayments").HasColumnType("integer");
			b.Property<decimal>("MinimumOrderValue").HasColumnType("decimal(10,2)");
			b.Property<Guid>("ProductId").HasColumnType("uuid");
			b.Property<decimal>("ReferralCommissionPercentage").HasColumnType("decimal(5,2)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("IsActive");
			b.HasIndex("ProductId").IsUnique();
			b.ToTable("product_commission_configs");
		});
		modelBuilder.Entity("GameGuild.Commerce.Products.ProductPricing", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<decimal>("BasePrice").HasPrecision(10, 2).HasColumnType("decimal(10,2)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Currency").IsRequired().ValueGeneratedOnAdd()
				.HasMaxLength(3)
				.HasColumnType("character varying(3)")
				.HasDefaultValue("USD");
			b.Property<int>("CurrentVersion").HasColumnType("integer");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsDefault").HasColumnType("boolean");
			b.Property<string>("Name").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<Guid>("ProductId").HasColumnType("uuid");
			b.Property<DateTime?>("SaleEndDate").HasColumnType("timestamp with time zone");
			b.Property<decimal?>("SalePrice").HasPrecision(10, 2).HasColumnType("decimal(10,2)");
			b.Property<DateTime?>("SaleStartDate").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("Currency");
			b.HasIndex("IsDefault");
			b.HasIndex("ProductId");
			b.HasIndex("SaleEndDate");
			b.HasIndex("SaleStartDate");
			b.ToTable("product_pricing");
		});
		modelBuilder.Entity("GameGuild.Commerce.Products.ProductPricingVersion", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<decimal>("BasePrice").HasColumnType("decimal(10,2)");
			b.Property<string>("ChangeReason").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("CreatedByUserId").HasColumnType("uuid");
			b.Property<string>("Currency").IsRequired().HasMaxLength(3)
				.HasColumnType("character varying(3)");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("EffectiveFrom").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("EffectiveTo").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<int>("PriceVersion").HasColumnType("integer").HasColumnName("price_version");
			b.Property<Guid>("ProductPricingId").HasColumnType("uuid");
			b.Property<decimal?>("SalePrice").HasColumnType("decimal(10,2)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("IsActive");
			b.HasIndex("ProductPricingId", "EffectiveFrom");
			b.HasIndex("ProductPricingId", "PriceVersion").IsUnique();
			b.ToTable("product_pricing_versions");
		});
		modelBuilder.Entity("GameGuild.Commerce.Products.ProductSubscriptionPlan", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<int>("BillingInterval").HasColumnType("integer");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Currency").IsRequired().ValueGeneratedOnAdd()
				.HasMaxLength(3)
				.HasColumnType("character varying(3)")
				.HasDefaultValue("USD");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<int>("IntervalCount").HasColumnType("integer");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<bool>("IsDefault").HasColumnType("boolean");
			b.Property<string>("Name").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<decimal>("Price").HasPrecision(10, 2).HasColumnType("decimal(10,2)");
			b.Property<Guid>("ProductId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<int?>("TrialPeriodDays").HasColumnType("integer");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("BillingInterval");
			b.HasIndex("IsActive");
			b.HasIndex("IsDefault");
			b.HasIndex("Name");
			b.HasIndex("Price");
			b.HasIndex("ProductId");
			b.ToTable("product_subscription_plans");
		});
		modelBuilder.Entity("GameGuild.Commerce.Products.PromoCode", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("Code").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("CreatedBy").HasColumnType("uuid");
			b.Property<string>("Currency").IsRequired().ValueGeneratedOnAdd()
				.HasMaxLength(3)
				.HasColumnType("character varying(3)")
				.HasDefaultValue("USD");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<decimal?>("DiscountAmount").HasPrecision(10, 2).HasColumnType("decimal(10,2)");
			b.Property<decimal?>("DiscountPercentage").HasPrecision(5, 2).HasColumnType("decimal(5,2)");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<bool>("IsExclusive").HasColumnType("boolean");
			b.Property<int?>("MaxUses").HasColumnType("integer");
			b.Property<int?>("MaxUsesPerUser").HasColumnType("integer");
			b.Property<decimal?>("MinimumOrderAmount").HasColumnType("decimal(10,2)");
			b.Property<string>("Name").IsRequired().HasMaxLength(255)
				.HasColumnType("character varying(255)");
			b.Property<Guid?>("ProductId").HasColumnType("uuid");
			b.Property<int>("StackingPriority").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<int>("Type").HasColumnType("integer");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("ValidFrom").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("ValidUntil").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("Code").IsUnique();
			b.HasIndex("CreatedBy");
			b.HasIndex("IsActive");
			b.HasIndex("ProductId");
			b.HasIndex("Type");
			b.HasIndex("ValidFrom");
			b.HasIndex("ValidUntil");
			b.ToTable("promo_codes");
		});
		modelBuilder.Entity("GameGuild.Commerce.Products.PromoCodeUse", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<decimal>("DiscountApplied").HasPrecision(10, 2).HasColumnType("decimal(10,2)");
			b.Property<Guid>("PromoCodeId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("PromoCodeId");
			b.HasIndex("UserId");
			b.ToTable("promo_code_uses");
		});
		modelBuilder.Entity("GameGuild.Commerce.Products.PromoStackingRule", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<bool>("AllowExclusiveStacking").HasColumnType("boolean");
			b.Property<bool>("AllowSameTypeStacking").HasColumnType("boolean");
			b.Property<string>("AllowedTypesCombinations").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<int>("ConflictStrategy").HasColumnType("integer");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<int>("MaxStackableCount").HasColumnType("integer");
			b.Property<decimal?>("MaxTotalDiscountAmount").HasPrecision(10, 2).HasColumnType("decimal(10,2)");
			b.Property<decimal?>("MaxTotalDiscountPercentage").HasPrecision(5, 2).HasColumnType("decimal(5,2)");
			b.Property<decimal?>("MinOrderAmountForStacking").HasColumnType("decimal(10,2)");
			b.Property<string>("Name").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<int>("Priority").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("IsActive");
			b.HasIndex("Name").IsUnique();
			b.HasIndex("Priority");
			b.ToTable("promo_stacking_rules");
		});
		modelBuilder.Entity("GameGuild.Commerce.Products.SupportTicket", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("AssignedToName").HasMaxLength(150).HasColumnType("character varying(150)");
			b.Property<Guid?>("AssignedToUserId").HasColumnType("uuid");
			b.Property<string>("Category").HasMaxLength(80).HasColumnType("character varying(80)");
			b.Property<DateTime?>("ClosedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("CustomerId").HasColumnType("uuid");
			b.Property<string>("CustomerName").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("FirstResponseAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("LastMessageAt").HasColumnType("timestamp with time zone");
			b.Property<string>("LastMessagePreview").HasMaxLength(240).HasColumnType("character varying(240)");
			b.Property<DateTime>("OpenedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Priority").HasColumnType("integer");
			b.Property<string>("ReporterEmail").HasMaxLength(320).HasColumnType("character varying(320)");
			b.Property<string>("ReporterName").IsRequired().HasMaxLength(150)
				.HasColumnType("character varying(150)");
			b.Property<Guid>("ReporterUserId").HasColumnType("uuid");
			b.Property<string>("ResolutionSummary").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<DateTime?>("ResolvedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("ResponseDueBy").HasColumnType("timestamp with time zone");
			b.Property<int>("Status").HasColumnType("integer");
			b.Property<string>("Subject").IsRequired().HasMaxLength(180)
				.HasColumnType("character varying(180)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("TenantId", "CustomerId");
			b.HasIndex("TenantId", "Priority");
			b.HasIndex("TenantId", "Status");
			b.ToTable("SupportTickets");
		});
		modelBuilder.Entity("GameGuild.Commerce.Products.SupportTicketMessage", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("AuthorEmail").HasMaxLength(320).HasColumnType("character varying(320)");
			b.Property<string>("AuthorName").IsRequired().HasMaxLength(150)
				.HasColumnType("character varying(150)");
			b.Property<int>("AuthorType").HasColumnType("integer");
			b.Property<Guid>("AuthorUserId").HasColumnType("uuid");
			b.Property<string>("Body").IsRequired().HasMaxLength(4000)
				.HasColumnType("character varying(4000)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsInternal").HasColumnType("boolean");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<Guid>("TicketId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("TicketId");
			b.HasIndex("TenantId", "TicketId");
			b.ToTable("SupportTicketMessages");
		});
		modelBuilder.Entity("GameGuild.Commerce.Products.UserProduct", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime?>("AccessEndDate").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("AccessStartDate").HasColumnType("timestamp with time zone");
			b.Property<int>("AccessStatus").HasColumnType("integer");
			b.Property<int>("AcquisitionType").HasColumnType("integer");
			b.Property<bool>("CancelAtPeriodEnd").HasColumnType("boolean");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Currency").IsRequired().ValueGeneratedOnAdd()
				.HasMaxLength(3)
				.HasColumnType("character varying(3)")
				.HasDefaultValue("USD");
			b.Property<DateTime?>("CurrentPeriodEnd").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("CurrentPeriodStart").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("GiftedByUserId").HasColumnType("uuid");
			b.Property<Guid?>("OrderId").HasColumnType("uuid");
			b.Property<decimal>("PricePaid").HasPrecision(10, 2).HasColumnType("decimal(10,2)");
			b.Property<Guid>("ProductId").HasColumnType("uuid");
			b.Property<string>("RevocationReason").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<Guid?>("SubscriptionId").HasColumnType("uuid");
			b.Property<string>("SubscriptionProviderReference").HasMaxLength(200).HasColumnType("character varying(200)");
			b.Property<int?>("SubscriptionStatus").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("AccessEndDate");
			b.HasIndex("AccessStatus");
			b.HasIndex("AcquisitionType");
			b.HasIndex("GiftedByUserId");
			b.HasIndex("ProductId");
			b.HasIndex("SubscriptionId");
			b.HasIndex("UserId");
			b.HasIndex("UserId", "ProductId").IsUnique();
			b.ToTable("user_products");
		});
		modelBuilder.Entity("GameGuild.Commerce.Subscriptions.Subscription", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<bool>("AutoRenew").HasColumnType("boolean");
			b.Property<string>("BillingCycle").IsRequired().HasMaxLength(20)
				.HasColumnType("character varying(20)");
			b.Property<int>("BillingCycleCount").HasColumnType("integer");
			b.Property<string>("CancellationNote").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<string>("CancellationReason").HasColumnType("text");
			b.Property<DateTime?>("CancelledAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("CreatedByUserId").HasColumnType("uuid");
			b.Property<DateTime>("CurrentPeriodEnd").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("CurrentPeriodStart").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("EndDate").HasColumnType("timestamp with time zone");
			b.Property<string>("ExternalCustomerId").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<string>("ExternalId").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<Guid?>("FulfilledOrderId").HasColumnType("uuid");
			b.Property<Guid?>("LastModifyingOrderId").HasColumnType("uuid");
			b.Property<DateTime?>("LastPaymentAt").HasColumnType("timestamp with time zone");
			b.Property<string>("LastPaymentIdempotencyKey").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<int>("LastProcessedBillingCycle").HasColumnType("integer");
			b.Property<string>("LastRenewalIdempotencyKey").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<Guid?>("LockedPriceVersionId").HasColumnType("uuid");
			b.Property<string>("Metadata").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<DateTime>("NextBillingDate").HasColumnType("timestamp with time zone");
			b.Property<Guid>("PlanId").HasColumnType("uuid");
			b.Property<byte[]>("RowVersion").IsConcurrencyToken().ValueGeneratedOnAddOrUpdate()
				.HasColumnType("bytea");
			b.Property<DateTime>("StartDate").HasColumnType("timestamp with time zone");
			b.Property<string>("Status").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<DateTime?>("TrialEndDate").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("CancelledAt");
			b.HasIndex("ExternalCustomerId");
			b.HasIndex("ExternalId").IsUnique();
			b.HasIndex("LastPaymentAt");
			b.HasIndex("NextBillingDate");
			b.HasIndex("PlanId");
			b.HasIndex("Status");
			b.HasIndex("TenantId");
			b.HasIndex("TrialEndDate");
			b.HasIndex("TenantId", "Status");
			b.ToTable("Subscriptions");
		});
		modelBuilder.Entity("GameGuild.Commerce.Subscriptions.SubscriptionInvoiceReadModel", delegate(EntityTypeBuilder b)
		{
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Currency").IsRequired().HasMaxLength(3)
				.HasColumnType("character varying(3)");
			b.Property<DateTime?>("DueDate").HasColumnType("timestamp with time zone");
			b.Property<string>("ExternalId").HasMaxLength(255).HasColumnType("character varying(255)");
			b.Property<Guid>("Id").HasColumnType("uuid");
			b.Property<string>("InvoiceNumber").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<DateTime?>("IssuedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("PaidAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("PaymentId").HasColumnType("uuid");
			b.Property<int>("Status").HasColumnType("integer");
			b.Property<Guid>("SubscriptionId").HasColumnType("uuid");
			b.Property<decimal>("Total").HasColumnType("decimal(18,2)");
			b.ToTable((string?)null);
			b.ToSqlQuery(" SELECT \\\"Id\\\",\r\n     \\\"SubscriptionId\\\",\r\n     \\\"InvoiceNumber\\\",\r\n     \\\"Total\\\",\r\n     \\\"Currency\\\",\r\n     \\\"CreatedAt\\\",\r\n     \\\"IssuedAt\\\",\r\n     \\\"DueDate\\\",\r\n     \\\"PaidAt\\\",\r\n     \\\"Status\\\",\r\n     \\\"PaymentId\\\",\r\n     \\\"ExternalId\\\"\r\nFROM invoices");
		});
		modelBuilder.Entity("GameGuild.Commerce.Subscriptions.SubscriptionPlan", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<long?>("AnnualPriceInCents").HasColumnType("bigint");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Currency").IsRequired().HasMaxLength(3)
				.HasColumnType("character varying(3)");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<string>("ExternalId").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<string>("Features").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<bool>("HasAdvancedAnalytics").HasColumnType("boolean");
			b.Property<bool>("HasCustomBranding").HasColumnType("boolean");
			b.Property<bool>("HasPrioritySupport").HasColumnType("boolean");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<bool>("IsFeatured").HasColumnType("boolean");
			b.Property<long?>("MaxApiCallsPerMonth").HasColumnType("bigint");
			b.Property<long?>("MaxStorageMb").HasColumnType("bigint");
			b.Property<int?>("MaxUsers").HasColumnType("integer");
			b.Property<string>("Metadata").HasMaxLength(4000).HasColumnType("character varying(4000)");
			b.Property<long>("MonthlyPriceInCents").HasColumnType("bigint");
			b.Property<string>("Name").IsRequired().HasMaxLength(255)
				.HasColumnType("character varying(255)");
			b.Property<string>("Slug").IsRequired().HasMaxLength(255)
				.HasColumnType("character varying(255)");
			b.Property<int>("SortOrder").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<int>("TrialPeriodDays").HasColumnType("integer");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ExternalId").IsUnique().HasDatabaseName("ix_subscription_plans_external_id");
			b.HasIndex("IsActive").HasDatabaseName("ix_subscription_plans_is_active");
			b.HasIndex("IsFeatured").HasDatabaseName("ix_subscription_plans_is_featured");
			b.HasIndex("Name").IsUnique().HasDatabaseName("ix_subscription_plans_name");
			b.HasIndex("Slug").IsUnique().HasDatabaseName("ix_subscription_plans_slug");
			b.HasIndex("SortOrder").HasDatabaseName("ix_subscription_plans_sort_order");
			b.ToTable("SubscriptionPlans", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Compliance.Audit.AuditLog", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("ActionType").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<int>("Category").HasColumnType("integer");
			b.Property<string>("CorrelationId").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<string>("ErrorMessage").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<string>("IpAddress").HasMaxLength(45).HasColumnType("character varying(45)");
			b.Property<string>("Metadata").HasColumnType("text");
			b.Property<string>("ResourceId").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<string>("ResourceType").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<int>("RiskLevel").HasColumnType("integer");
			b.Property<Guid?>("SessionId").HasColumnType("uuid");
			b.Property<bool>("Success").HasColumnType("boolean");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("UserAgent").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<Guid?>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ActionType");
			b.HasIndex("CreatedAt");
			b.HasIndex("ResourceId");
			b.HasIndex("ResourceType");
			b.HasIndex("TenantId");
			b.HasIndex("UserId");
			b.HasIndex("TenantId", "CreatedAt");
			b.ToTable("AuditLogs", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Compliance.Consent.ConsentPolicy", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<bool>("IsMandatory").HasColumnType("boolean");
			b.Property<string>("Name").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("PolicyType").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("IsActive");
			b.HasIndex("PolicyType");
			b.ToTable("consent_policies");
		});
		modelBuilder.Entity("GameGuild.Compliance.Consent.DataSubjectRequest", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("Deadline").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<DateTime?>("ProcessedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("ProcessedByUserId").HasColumnType("uuid");
			b.Property<string>("ProcessingNotes").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<string>("RequestType").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<string>("Status").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("RequestType");
			b.HasIndex("Status");
			b.HasIndex("UserId");
			b.ToTable("data_subject_requests");
		});
		modelBuilder.Entity("GameGuild.Compliance.Consent.PolicyVersion", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("ConsentPolicyId").HasColumnType("uuid");
			b.Property<string>("Content").IsRequired().HasColumnType("text");
			b.Property<string>("ContentType").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("EffectiveFrom").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("EffectiveUntil").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsCurrent").HasColumnType("boolean");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<string>("VersionNumber").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.HasKey("Id");
			b.HasIndex("ConsentPolicyId");
			b.HasIndex("EffectiveFrom");
			b.HasIndex("VersionNumber");
			b.ToTable("consent_policy_versions");
		});
		modelBuilder.Entity("GameGuild.Compliance.Consent.UserConsent", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("ConsentGivenAt").HasColumnType("timestamp with time zone");
			b.Property<string>("ConsentMethod").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<DateTime?>("ConsentRevokedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("IpAddress").HasMaxLength(45).HasColumnType("character varying(45)");
			b.Property<bool>("IsGranted").HasColumnType("boolean");
			b.Property<Guid>("PolicyVersionId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("UserAgent").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ConsentGivenAt");
			b.HasIndex("PolicyVersionId");
			b.HasIndex("UserId");
			b.HasIndex("UserId", "PolicyVersionId").IsUnique();
			b.ToTable("user_consents");
		});
		modelBuilder.Entity("GameGuild.Compliance.FERPA.FerpaDirectoryInformationPolicy", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("AllowedFieldsJson").IsRequired().HasColumnType("jsonb");
			b.Property<DateTime?>("AnnualNoticeSentAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("NoticeUrl").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<bool>("OptOutEnabled").HasColumnType("boolean");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("TenantId").IsUnique();
			b.ToTable("ferpa_directory_information_policies");
		});
		modelBuilder.Entity("GameGuild.Compliance.FERPA.FerpaDisclosureConsent", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("EffectiveFrom").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("GuardianUserId").HasColumnType("uuid");
			b.Property<string>("Purpose").IsRequired().HasMaxLength(500)
				.HasColumnType("character varying(500)");
			b.Property<string>("Recipient").IsRequired().HasMaxLength(250)
				.HasColumnType("character varying(250)");
			b.Property<DateTime?>("RevokedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Scope").IsRequired().HasMaxLength(500)
				.HasColumnType("character varying(500)");
			b.Property<Guid>("StudentUserId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("StudentUserId");
			b.HasIndex("StudentUserId", "Recipient", "Scope");
			b.ToTable("ferpa_disclosure_consents");
		});
		modelBuilder.Entity("GameGuild.Compliance.FERPA.FerpaDisclosureLog", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("Basis").IsRequired().HasMaxLength(80)
				.HasColumnType("character varying(80)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("DisclosedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("DisclosedByUserId").HasColumnType("uuid");
			b.Property<string>("Purpose").IsRequired().HasMaxLength(500)
				.HasColumnType("character varying(500)");
			b.Property<string>("Recipient").IsRequired().HasMaxLength(250)
				.HasColumnType("character varying(250)");
			b.Property<string>("RecordIdsJson").IsRequired().HasColumnType("jsonb");
			b.Property<Guid>("StudentUserId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("DisclosedAt");
			b.HasIndex("StudentUserId");
			b.ToTable("ferpa_disclosure_logs");
		});
		modelBuilder.Entity("GameGuild.Compliance.FERPA.FerpaEducationRecord", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("ExternalRecordId").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<bool>("IsDirectoryInformation").HasColumnType("boolean");
			b.Property<string>("MetadataJson").IsRequired().HasColumnType("jsonb");
			b.Property<string>("ProtectionLevel").IsRequired().HasMaxLength(80)
				.HasColumnType("character varying(80)");
			b.Property<string>("RecordKind").IsRequired().HasMaxLength(80)
				.HasColumnType("character varying(80)");
			b.Property<DateTime?>("RetentionUntil").HasColumnType("timestamp with time zone");
			b.Property<Guid>("StudentUserId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Title").IsRequired().HasMaxLength(300)
				.HasColumnType("character varying(300)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ExternalRecordId");
			b.HasIndex("RecordKind");
			b.HasIndex("StudentUserId");
			b.HasIndex("TenantId");
			b.ToTable("ferpa_education_records");
		});
		modelBuilder.Entity("GameGuild.Compliance.FERPA.FerpaInspectionRequest", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("Deadline").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<DateTime?>("ProcessedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("ProcessedByUserId").HasColumnType("uuid");
			b.Property<string>("ProcessingNotes").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<Guid>("RequestedByUserId").HasColumnType("uuid");
			b.Property<string>("Status").IsRequired().HasMaxLength(80)
				.HasColumnType("character varying(80)");
			b.Property<Guid>("StudentUserId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("Deadline");
			b.HasIndex("Status");
			b.HasIndex("StudentUserId");
			b.ToTable("ferpa_inspection_requests");
		});
		modelBuilder.Entity("GameGuild.Compliance.FinancialCrime.FinancialCrimeCaseEventRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid?>("ActorId").HasColumnType("uuid");
			b.Property<Guid>("CaseId").HasColumnType("uuid");
			b.Property<string>("EvidenceHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("Kind").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<DateTimeOffset>("OccurredAt").HasColumnType("timestamp with time zone");
			b.Property<string>("ReasonCode").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<int>("Sequence").HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("CaseId", "Sequence").IsUnique();
			b.ToTable("compliance_financial_crime_case_events", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_financial_crime_case_events_sequence", "\"Sequence\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Compliance.FinancialCrime.FinancialCrimeCaseRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid?>("AssignedTo").HasColumnType("uuid");
			b.Property<DateTimeOffset?>("ClosedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("HoldId").HasColumnType("uuid");
			b.Property<DateTimeOffset>("OpenedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("ReasonCode").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<int>("State").HasColumnType("integer");
			b.Property<string>("SubjectHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<long>("Version").HasColumnType("bigint");
			b.HasKey("Id");
			b.HasIndex("TenantId", "SubjectHash", "State");
			b.ToTable("compliance_financial_crime_cases", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_financial_crime_cases_closed", "(\"State\" = 4 AND \"ClosedAt\" IS NOT NULL) OR (\"State\" <> 4 AND \"ClosedAt\" IS NULL)");
				t.HasCheckConstraint("ck_financial_crime_cases_version", "\"Version\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Compliance.FinancialCrime.FinancialCrimeDecisionConsumptionRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTimeOffset>("ConsumedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("DecisionId").HasColumnType("uuid");
			b.Property<string>("OperationFingerprint").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("DecisionId").IsUnique();
			b.ToTable("compliance_financial_crime_decision_consumptions", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Compliance.FinancialCrime.FinancialCrimeDecisionRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("CaseId").HasColumnType("uuid");
			b.Property<Guid>("DecidedBy").HasColumnType("uuid");
			b.Property<string>("EvidenceHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTimeOffset>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<DateTimeOffset>("IssuedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Outcome").HasColumnType("integer");
			b.Property<long>("PolicyVersion").HasColumnType("bigint");
			b.Property<string>("RawObjectReference").IsRequired().HasMaxLength(2048)
				.HasColumnType("character varying(2048)");
			b.Property<string>("ReasonCode").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<string>("SubjectHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<long>("Version").HasColumnType("bigint");
			b.HasKey("Id");
			b.HasIndex("CaseId", "Version").IsUnique();
			b.ToTable("compliance_financial_crime_decisions", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_financial_crime_decisions_lifetime", "\"ExpiresAt\" > \"IssuedAt\"");
				t.HasCheckConstraint("ck_financial_crime_decisions_version", "\"Version\" > 0 AND \"PolicyVersion\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Compliance.FinancialCrime.FinancialCrimeRegulatoryReferenceRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("CaseId").HasColumnType("uuid");
			b.Property<string>("JurisdictionCode").IsRequired().HasMaxLength(16)
				.HasColumnType("character varying(16)");
			b.Property<string>("Kind").IsRequired().HasMaxLength(20)
				.HasColumnType("character varying(20)");
			b.Property<DateTimeOffset>("RecordedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("RecordedBy").HasColumnType("uuid");
			b.Property<string>("ReferenceHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.HasKey("Id");
			b.HasIndex("CaseId", "Kind", "ReferenceHash").IsUnique();
			b.ToTable("compliance_financial_crime_regulatory_references", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Compliance.FinancialCrime.FinancialCrimeScreeningRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<bool>("AdverseMediaMatch").HasColumnType("boolean");
			b.Property<Guid?>("CaseId").HasColumnType("uuid");
			b.Property<string>("Environment").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<string>("EvidenceHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTimeOffset>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<DateTimeOffset>("IssuedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTimeOffset>("NextScreenAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Outcome").HasColumnType("integer");
			b.Property<string>("PayloadHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<bool>("PepMatch").HasColumnType("boolean");
			b.Property<long>("PolicyVersion").HasColumnType("bigint");
			b.Property<string>("Provider").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<string>("ProviderEventId").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<string>("RawObjectReference").IsRequired().HasMaxLength(2048)
				.HasColumnType("character varying(2048)");
			b.Property<DateTimeOffset>("ReceivedAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("SanctionsMatch").HasColumnType("boolean");
			b.Property<bool>("SignatureVerified").HasColumnType("boolean");
			b.Property<string>("SubjectHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<long>("Version").HasColumnType("bigint");
			b.HasKey("Id");
			b.HasIndex("CaseId");
			b.HasIndex("NextScreenAt");
			b.HasIndex("Provider", "Environment", "ProviderEventId").IsUnique().HasDatabaseName("ux_financial_crime_screenings_provider_event");
			b.HasIndex("TenantId", "SubjectHash", "Version").IsUnique().HasDatabaseName("ux_financial_crime_screenings_subject_version");
			b.ToTable("compliance_financial_crime_screenings", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_financial_crime_screenings_lifetime", "\"ExpiresAt\" > \"IssuedAt\" AND \"NextScreenAt\" > \"IssuedAt\"");
				t.HasCheckConstraint("ck_financial_crime_screenings_version", "\"Version\" > 0 AND \"PolicyVersion\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Compliance.FinancialCrime.FinancialCrimeTransactionSignalRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("CaseId").HasColumnType("uuid");
			b.Property<string>("EvidenceHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTimeOffset>("ObservedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("OperationFingerprint").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<string>("RequestHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<int>("Score").HasColumnType("integer");
			b.Property<string>("SignalType").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<string>("SubjectHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("CaseId");
			b.HasIndex("RequestHash").IsUnique();
			b.HasIndex("TenantId", "SubjectHash", "ObservedAt");
			b.ToTable("compliance_financial_crime_transaction_signals", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_financial_crime_transaction_signals_score", "\"Score\" BETWEEN 0 AND 1000000");
			});
		});
		modelBuilder.Entity("GameGuild.Compliance.KYC.SumSubApplicantBindingRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").HasColumnType("uuid");
			b.Property<string>("ApplicantId").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<long>("EvidenceVersion").HasColumnType("bigint");
			b.Property<string>("ExternalUserIdHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("IdempotencyKeyHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("LastProviderEventId").HasMaxLength(256).HasColumnType("character varying(256)");
			b.Property<DateTimeOffset?>("LastProviderIssuedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("State").HasColumnType("integer");
			b.Property<string>("SubjectHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.HasKey("Id");
			b.HasIndex("ApplicantId").IsUnique();
			b.HasIndex("IdempotencyKeyHash").IsUnique();
			b.HasIndex("TenantId", "SubjectHash").IsUnique();
			b.ToTable("compliance_sumsub_applicant_bindings", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_compliance_sumsub_applicant_bindings_state", "\"State\" BETWEEN 1 AND 7");
				t.HasCheckConstraint("ck_compliance_sumsub_applicant_bindings_version", "\"EvidenceVersion\" >= 0");
			});
		});
		modelBuilder.Entity("GameGuild.Compliance.KYC.SumSubWebhookInboxRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").HasColumnType("uuid");
			b.Property<string>("ApplicantId").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<DateTimeOffset>("IssuedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("PayloadHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTimeOffset?>("ProcessedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("ProcessingError").HasMaxLength(256).HasColumnType("character varying(256)");
			b.Property<string>("ProviderEventId").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<string>("RawObjectReference").IsRequired().HasMaxLength(2048)
				.HasColumnType("character varying(2048)");
			b.Property<DateTimeOffset>("ReceivedAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("SignatureVerified").HasColumnType("boolean");
			b.HasKey("Id");
			b.HasIndex("ProviderEventId").IsUnique();
			b.HasIndex("ApplicantId", "IssuedAt");
			b.ToTable("compliance_sumsub_webhook_inbox", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_compliance_sumsub_webhook_inbox_time", "\"ReceivedAt\" >= \"IssuedAt\"");
			});
		});
		modelBuilder.Entity("GameGuild.Content.Pages.ContentResource", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid?>("AuthorId").HasColumnType("uuid");
			b.Property<string>("AuthorName").HasMaxLength(200).HasColumnType("character varying(200)");
			b.Property<string>("Body").HasColumnType("text");
			b.Property<string>("CategorySlug").HasMaxLength(200).HasColumnType("character varying(200)");
			b.Property<string>("CoverImageUrl").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("CustomData").HasColumnType("jsonb");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("DownloadUrl").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<string>("ExternalUrl").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<bool>("IsFeatured").HasColumnType("boolean");
			b.Property<Guid?>("LinkedEntityId").HasColumnType("uuid");
			b.Property<string>("LinkedEntityType").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<string>("Locale").HasMaxLength(10).HasColumnType("character varying(10)");
			b.Property<string>("MetaDescription").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<string>("MetaTitle").HasMaxLength(300).HasColumnType("character varying(300)");
			b.Property<string>("OgImageUrl").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<DateTime?>("PublishedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("PublishedBy").HasColumnType("uuid");
			b.Property<int?>("ReadingTimeMinutes").HasColumnType("integer");
			b.Property<string>("ResourceType").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<DateTime?>("ScheduledPublishAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Slug").IsRequired().HasMaxLength(500)
				.HasColumnType("character varying(500)");
			b.Property<int>("SortOrder").HasColumnType("integer");
			b.Property<string>("Status").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<string>("StructuredData").HasColumnType("jsonb");
			b.Property<string>("Summary").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<string>("Tags").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Title").IsRequired().HasMaxLength(300)
				.HasColumnType("character varying(300)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<string>("VideoUrl").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<long>("ViewCount").HasColumnType("bigint");
			b.HasKey("Id");
			b.HasIndex("AuthorId");
			b.HasIndex("CategorySlug");
			b.HasIndex("IsFeatured");
			b.HasIndex("Locale");
			b.HasIndex("PublishedAt");
			b.HasIndex("ResourceType");
			b.HasIndex("Slug").IsUnique();
			b.HasIndex("Status");
			b.ToTable("content_resources");
		});
		modelBuilder.Entity("GameGuild.Content.Pages.MarketingLead", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("Company").HasMaxLength(200).HasColumnType("character varying(200)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Email").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("Locale").HasMaxLength(10).HasColumnType("character varying(10)");
			b.Property<string>("Message").HasMaxLength(4000).HasColumnType("character varying(4000)");
			b.Property<string>("Name").HasMaxLength(120).HasColumnType("character varying(120)");
			b.Property<string>("PagePath").HasMaxLength(300).HasColumnType("character varying(300)");
			b.Property<string>("Plan").HasMaxLength(60).HasColumnType("character varying(60)");
			b.Property<string>("Referrer").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<string>("Source").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<string>("Status").IsRequired().ValueGeneratedOnAdd()
				.HasMaxLength(40)
				.HasColumnType("character varying(40)")
				.HasDefaultValue("new");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Topic").HasMaxLength(40).HasColumnType("character varying(40)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("UserAgent").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("Email");
			b.HasIndex("Source", "CreatedAt");
			b.HasIndex("Status", "CreatedAt");
			b.ToTable("marketing_leads");
		});
		modelBuilder.Entity("GameGuild.Content.Pages.Page", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("Body").HasColumnType("text");
			b.Property<string>("CanonicalUrl").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("CustomData").HasColumnType("jsonb");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<string>("Locale").HasMaxLength(10).HasColumnType("character varying(10)");
			b.Property<string>("MetaDescription").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<string>("MetaKeywords").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<string>("MetaTitle").HasMaxLength(300).HasColumnType("character varying(300)");
			b.Property<string>("OgDescription").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<string>("OgImageUrl").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<string>("OgTitle").HasMaxLength(300).HasColumnType("character varying(300)");
			b.Property<string>("OgType").HasMaxLength(50).HasColumnType("character varying(50)");
			b.Property<string>("PageType").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<Guid?>("ParentPageId").HasColumnType("uuid");
			b.Property<DateTime?>("PublishedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("PublishedBy").HasColumnType("uuid");
			b.Property<string>("RobotsDirective").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<DateTime?>("ScheduledPublishAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Slug").IsRequired().HasMaxLength(500)
				.HasColumnType("character varying(500)");
			b.Property<int>("SortOrder").HasColumnType("integer");
			b.Property<string>("Status").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<string>("StructuredData").HasColumnType("jsonb");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Title").IsRequired().HasMaxLength(300)
				.HasColumnType("character varying(300)");
			b.Property<string>("TwitterCard").HasMaxLength(50).HasColumnType("character varying(50)");
			b.Property<string>("TwitterSite").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("Locale");
			b.HasIndex("PageType");
			b.HasIndex("ParentPageId");
			b.HasIndex("Slug").IsUnique();
			b.HasIndex("Status");
			b.ToTable("pages");
		});
		modelBuilder.Entity("GameGuild.Content.Pages.PageSection", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("CssClasses").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<string>("Data").HasColumnType("jsonb");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Heading").HasMaxLength(300).HasColumnType("character varying(300)");
			b.Property<bool>("IsVisible").HasColumnType("boolean");
			b.Property<Guid>("PageId").HasColumnType("uuid");
			b.Property<string>("SectionType").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<int>("SortOrder").HasColumnType("integer");
			b.Property<string>("Subheading").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("PageId");
			b.HasIndex("SectionType");
			b.HasIndex("PageId", "SortOrder");
			b.ToTable("page_sections");
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.AdRewards.Persistence.AdNetworkPolicyVersionRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<string>("Network").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<long>("Version").HasColumnType("bigint");
			b.Property<Guid>("ApprovedBy").HasColumnType("uuid");
			b.Property<long>("BudgetWindowTicks").HasColumnType("bigint");
			b.Property<string>("CanonicalPayload").IsRequired().HasColumnType("text");
			b.Property<int>("ContractedRevenueSharePpm").HasColumnType("integer");
			b.Property<DateTimeOffset>("EffectiveAt").HasColumnType("timestamp with time zone");
			b.Property<long>("EstimatedNetEcpmUsdNanos").HasColumnType("bigint");
			b.Property<DateTimeOffset>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<long>("FundedLossBudgetUsdNanos").HasColumnType("bigint");
			b.Property<int>("IssuanceMode").HasColumnType("integer");
			b.Property<string>("KeyId").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<long>("MaximumAsnSoftUnits").HasColumnType("bigint");
			b.Property<long>("MaximumDeviceSoftUnits").HasColumnType("bigint");
			b.Property<long>("MaximumFocusLossTicks").HasColumnType("bigint");
			b.Property<long>("MaximumGlobalSoftUnits").HasColumnType("bigint");
			b.Property<long>("MaximumIpSoftUnits").HasColumnType("bigint");
			b.Property<long>("MaximumNetworkSoftUnits").HasColumnType("bigint");
			b.Property<long>("MaximumRewardSoftUnits").HasColumnType("bigint");
			b.Property<long>("MaximumUserSoftUnits").HasColumnType("bigint");
			b.Property<int>("MinimumVisiblePpm").HasColumnType("integer");
			b.Property<string>("PayloadHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<Guid>("ProposedBy").HasColumnType("uuid");
			b.Property<bool>("ProviderCertified").HasColumnType("boolean");
			b.Property<string>("ProviderHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTimeOffset>("PublishedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Ranking").HasColumnType("integer");
			b.Property<long>("ReportStaleAfterTicks").HasColumnType("bigint");
			b.Property<DateTimeOffset>("ReportsCurrentThrough").HasColumnType("timestamp with time zone");
			b.Property<int>("SafetyBufferPpm").HasColumnType("integer");
			b.Property<string>("Signature").IsRequired().HasColumnType("text");
			b.Property<int>("YieldState").HasColumnType("integer");
			b.HasKey("TenantId", "Network", "Version");
			b.HasIndex("TenantId", "Network", "EffectiveAt", "ExpiresAt");
			b.ToTable("economy_ad_network_policy_versions", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_ad_network_policy_versions_caps", "\"MaximumUserSoftUnits\" > 0 AND \"MaximumDeviceSoftUnits\" > 0 AND \"MaximumIpSoftUnits\" > 0 AND \"MaximumAsnSoftUnits\" > 0 AND \"MaximumNetworkSoftUnits\" > 0 AND \"MaximumGlobalSoftUnits\" > 0 AND \"FundedLossBudgetUsdNanos\" > 0");
				t.HasCheckConstraint("ck_economy_ad_network_policy_versions_dual_control", "\"ProposedBy\" <> \"ApprovedBy\"");
				t.HasCheckConstraint("ck_economy_ad_network_policy_versions_ppm", "\"ContractedRevenueSharePpm\" BETWEEN 0 AND 1000000 AND \"SafetyBufferPpm\" BETWEEN 0 AND 999999 AND \"MinimumVisiblePpm\" BETWEEN 0 AND 1000000");
				t.HasCheckConstraint("ck_economy_ad_network_policy_versions_values", "\"Version\" > 0 AND \"EstimatedNetEcpmUsdNanos\" > 0 AND \"MaximumRewardSoftUnits\" > 0 AND \"MaximumFocusLossTicks\" >= 0 AND \"ReportStaleAfterTicks\" > 0 AND \"Ranking\" >= 0 AND \"BudgetWindowTicks\" > 0");
				t.HasCheckConstraint("ck_economy_ad_network_policy_versions_window", "\"ExpiresAt\" > \"EffectiveAt\"");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.AdRewards.Persistence.AdProviderReportRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").HasColumnType("uuid");
			b.Property<long>("ActualRevenueUsdNanos").HasColumnType("bigint");
			b.Property<string>("BatchId").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<string>("EvidenceHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTimeOffset>("ImportedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Network").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<string>("PayloadHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTimeOffset>("PeriodEnd").HasColumnType("timestamp with time zone");
			b.Property<DateTimeOffset>("PeriodStart").HasColumnType("timestamp with time zone");
			b.Property<DateTimeOffset?>("ProcessedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("ProcessingError").HasMaxLength(256).HasColumnType("character varying(256)");
			b.Property<DateTimeOffset>("ReceivedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("ReportId").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<string>("Signature").IsRequired().HasColumnType("text");
			b.Property<bool>("SignatureVerified").HasColumnType("boolean");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<string>("VerifiedSessionIds").IsRequired().HasColumnType("jsonb");
			b.Property<int>("Version").HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("TenantId", "Network", "BatchId", "Version").IsUnique();
			b.HasIndex("TenantId", "Network", "ReportId", "Version").IsUnique();
			b.ToTable("economy_ad_provider_reports", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_ad_provider_reports_revenue", "\"ActualRevenueUsdNanos\" >= 0");
				t.HasCheckConstraint("ck_economy_ad_provider_reports_version", "\"Version\" > 0");
				t.HasCheckConstraint("ck_economy_ad_provider_reports_window", "\"PeriodEnd\" > \"PeriodStart\" AND \"ImportedAt\" >= \"PeriodEnd\"");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.AdRewards.Persistence.AdRewardAccumulatorRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<Guid>("WalletId").HasColumnType("uuid");
			b.Property<string>("Network").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<string>("CanonicalDenominator").IsRequired().HasColumnType("text");
			b.Property<long>("PolicyVersion").HasColumnType("bigint");
			b.Property<string>("RemainderNumerator").IsRequired().HasColumnType("text");
			b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<long>("Version").HasColumnType("bigint");
			b.HasKey("TenantId", "WalletId", "Network");
			b.ToTable("economy_ad_reward_accumulators", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_ad_reward_accumulators_numbers", "\"RemainderNumerator\" ~ '^[0-9]+$' AND \"CanonicalDenominator\" ~ '^[1-9][0-9]*$'");
				t.HasCheckConstraint("ck_economy_ad_reward_accumulators_version", "\"PolicyVersion\" > 0 AND \"Version\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.AdRewards.Persistence.AdRewardAttributionRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("SessionId").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTimeOffset>("CompletedAt").HasColumnType("timestamp with time zone");
			b.Property<long>("EstimatedRevenueUsdNanos").HasColumnType("bigint");
			b.Property<string>("Network").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<long>("PolicyVersion").HasColumnType("bigint");
			b.Property<string>("ProviderBatchId").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<long>("RewardSoftUnits").HasColumnType("bigint");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.HasKey("SessionId");
			b.HasIndex("Network", "ProviderBatchId", "CompletedAt");
			b.ToTable("economy_ad_reward_attributions", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_ad_reward_attributions_nonnegative", "\"EstimatedRevenueUsdNanos\" >= 0 AND \"RewardSoftUnits\" >= 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.AdRewards.Persistence.AdRewardBudgetConsumptionRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("SessionId").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTimeOffset>("ConsumedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("DeviceRiskHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<long>("LossBudgetUsdNanos").HasColumnType("bigint");
			b.Property<string>("Network").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<long>("SoftUnits").HasColumnType("bigint");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.HasKey("SessionId");
			b.HasIndex("DeviceRiskHash", "ConsumedAt");
			b.HasIndex("Network", "ConsumedAt");
			b.HasIndex("UserId", "ConsumedAt");
			b.ToTable("economy_ad_reward_budget_consumptions", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_ad_reward_budget_consumptions_positive", "\"SoftUnits\" > 0 AND \"LossBudgetUsdNanos\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.AdRewards.Persistence.AdRewardCapConsumptionRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTimeOffset>("ConsumedAt").HasColumnType("timestamp with time zone");
			b.Property<long>("LossBudgetUsdNanos").HasColumnType("bigint");
			b.Property<int>("Scope").HasColumnType("integer");
			b.Property<Guid>("SessionId").HasColumnType("uuid");
			b.Property<long>("SoftUnits").HasColumnType("bigint");
			b.Property<string>("SubjectHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<DateTimeOffset>("WindowEndsAt").HasColumnType("timestamp with time zone");
			b.Property<DateTimeOffset>("WindowStartedAt").HasColumnType("timestamp with time zone");
			b.HasKey("Id");
			b.HasIndex("SessionId", "Scope").IsUnique();
			b.HasIndex("TenantId", "Scope", "SubjectHash", "ConsumedAt");
			b.ToTable("economy_ad_reward_cap_consumptions", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_ad_reward_cap_consumptions_positive", "\"SoftUnits\" > 0 AND \"LossBudgetUsdNanos\" >= 0");
				t.HasCheckConstraint("ck_economy_ad_reward_cap_consumptions_scope", "\"Scope\" BETWEEN 1 AND 6");
				t.HasCheckConstraint("ck_economy_ad_reward_cap_consumptions_window", "\"WindowEndsAt\" > \"WindowStartedAt\" AND \"ConsumedAt\" >= \"WindowStartedAt\" AND \"ConsumedAt\" < \"WindowEndsAt\"");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.AdRewards.Persistence.AdRewardCompletionRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("SessionId").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("CapabilityReceiptHash").HasMaxLength(128).HasColumnType("character varying(128)");
			b.Property<Guid?>("CapabilityReceiptId").HasColumnType("uuid");
			b.Property<DateTimeOffset>("CompletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("DestinationHash").HasMaxLength(128).HasColumnType("character varying(128)");
			b.Property<string>("EvidenceHashes").IsRequired().HasColumnType("jsonb");
			b.Property<string>("IdempotencyKey").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<string>("JurisdictionCode").HasMaxLength(16).HasColumnType("character varying(16)");
			b.Property<long?>("KillSwitchEpoch").HasColumnType("bigint");
			b.Property<string>("Network").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<Guid?>("OutputLotId").HasColumnType("uuid");
			b.Property<long>("PolicyVersion").HasColumnType("bigint");
			b.Property<Guid?>("PostingId").HasColumnType("uuid");
			b.Property<string>("ProviderEventId").HasMaxLength(256).HasColumnType("character varying(256)");
			b.Property<string>("ProviderHash").HasMaxLength(128).HasColumnType("character varying(128)");
			b.Property<long?>("ReserveVersion").HasColumnType("bigint");
			b.Property<long>("RewardSoftUnits").HasColumnType("bigint");
			b.Property<Guid?>("RiskDecisionId").HasColumnType("uuid");
			b.Property<Guid?>("SourceStampId").HasColumnType("uuid");
			b.Property<int>("State").HasColumnType("integer");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<long>("Version").IsConcurrencyToken().HasColumnType("bigint");
			b.Property<Guid>("WalletId").HasColumnType("uuid");
			b.HasKey("SessionId");
			b.HasIndex("IdempotencyKey").IsUnique();
			b.HasIndex("ProviderEventId").IsUnique().HasFilter("\"ProviderEventId\" IS NOT NULL");
			b.HasIndex("Network", "PolicyVersion");
			b.HasIndex("UserId", "CompletedAt");
			b.ToTable("economy_ad_reward_completions", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_ad_reward_completions_issued_binding", "\"State\" <> 1 OR (\"RewardSoftUnits\" > 0 AND \"SourceStampId\" IS NOT NULL AND \"PostingId\" IS NOT NULL AND \"OutputLotId\" IS NOT NULL)");
				t.HasCheckConstraint("ck_economy_ad_reward_completions_reward_nonnegative", "\"RewardSoftUnits\" >= 0");
				t.HasCheckConstraint("ck_economy_ad_reward_completions_state", "\"State\" BETWEEN 1 AND 3");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.AdRewards.Persistence.AdRewardPendingClaimRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("SessionId").HasColumnType("uuid");
			b.Property<string>("CompletionIdempotencyKeyHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("CompletionRequestHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("ConfirmationIdempotencyKeyHash").HasMaxLength(128).HasColumnType("character varying(128)");
			b.Property<string>("ConfirmationRequestHash").HasMaxLength(128).HasColumnType("character varying(128)");
			b.Property<DateTimeOffset?>("ConfirmedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTimeOffset>("DeferredAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("ProviderReportId").HasColumnType("uuid");
			b.Property<Guid>("SourceStampId").HasColumnType("uuid");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.HasKey("SessionId");
			b.HasIndex("ProviderReportId");
			b.HasIndex("TenantId", "CompletionIdempotencyKeyHash").IsUnique();
			b.HasIndex("TenantId", "ConfirmationIdempotencyKeyHash").IsUnique().HasFilter("\"ConfirmationIdempotencyKeyHash\" IS NOT NULL");
			b.ToTable("economy_ad_reward_pending_claims", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.AdRewards.Persistence.AdRewardPlaybackMilestoneRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("EvidenceHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTimeOffset>("ObservedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Percentage").HasColumnType("integer");
			b.Property<int>("Sequence").HasColumnType("integer");
			b.Property<Guid>("SessionId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("SessionId", "Sequence").IsUnique();
			b.ToTable("economy_ad_reward_playback_milestones", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_ad_reward_playback_milestones_percentage", "\"Percentage\" BETWEEN 0 AND 100 AND \"Sequence\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.AdRewards.Persistence.AdRewardProviderBatchClaimRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("BatchId").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<DateTimeOffset>("ClaimedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("ProviderReportId").HasColumnType("uuid");
			b.Property<Guid>("SessionId").HasColumnType("uuid");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("SessionId");
			b.HasIndex("ProviderReportId", "SessionId").IsUnique();
			b.ToTable("economy_ad_reward_provider_batch_claims", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.AdRewards.Persistence.AdRewardProviderProofInboxRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("EvidenceHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("Network").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<string>("PayloadHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTimeOffset?>("ProcessedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("ProcessingError").HasMaxLength(256).HasColumnType("character varying(256)");
			b.Property<string>("ProviderEventId").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<DateTimeOffset>("ReceivedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("SessionId").HasColumnType("uuid");
			b.Property<bool>("SignatureVerified").HasColumnType("boolean");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("SessionId");
			b.HasIndex("TenantId", "Network", "ProviderEventId").IsUnique();
			b.ToTable("economy_ad_reward_provider_proof_inbox", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.AdRewards.Persistence.AdRewardReconciliationRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").HasColumnType("uuid");
			b.Property<long>("ActualDeltaUsdNanos").HasColumnType("bigint");
			b.Property<long>("ActualRevenueUsdNanos").HasColumnType("bigint");
			b.Property<string>("BatchId").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<long>("EstimatedRevenueUsdNanos").HasColumnType("bigint");
			b.Property<long>("HistoricalRewardSoftUnits").HasColumnType("bigint");
			b.Property<string>("Network").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<long>("PreviousActualRevenueUsdNanos").HasColumnType("bigint");
			b.Property<Guid>("ProviderReportId").HasColumnType("uuid");
			b.Property<DateTimeOffset>("ReconciledAt").HasColumnType("timestamp with time zone");
			b.Property<string>("ReportId").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<long>("VarianceUsdNanos").HasColumnType("bigint");
			b.Property<int>("Version").HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ProviderReportId").IsUnique();
			b.HasIndex("TenantId", "Network", "ReportId", "Version").IsUnique();
			b.ToTable("economy_ad_reward_reconciliations", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_ad_reward_reconciliations_conservation", "\"ActualDeltaUsdNanos\" = \"ActualRevenueUsdNanos\" - \"PreviousActualRevenueUsdNanos\" AND \"VarianceUsdNanos\" = \"ActualRevenueUsdNanos\" - \"EstimatedRevenueUsdNanos\"");
				t.HasCheckConstraint("ck_economy_ad_reward_reconciliations_nonnegative", "\"EstimatedRevenueUsdNanos\" >= 0 AND \"PreviousActualRevenueUsdNanos\" >= 0 AND \"ActualRevenueUsdNanos\" >= 0 AND \"HistoricalRewardSoftUnits\" >= 0");
				t.HasCheckConstraint("ck_economy_ad_reward_reconciliations_version", "\"Version\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.AdRewards.Persistence.AdRewardSessionEventRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("EvidenceHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTimeOffset>("OccurredAt").HasColumnType("timestamp with time zone");
			b.Property<long>("Sequence").HasColumnType("bigint");
			b.Property<Guid>("SessionId").HasColumnType("uuid");
			b.Property<int>("State").HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("SessionId", "Sequence").IsUnique();
			b.ToTable("economy_ad_reward_session_events", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_ad_reward_session_events_sequence", "\"Sequence\" > 0");
				t.HasCheckConstraint("ck_economy_ad_reward_session_events_state", "\"State\" BETWEEN 1 AND 7");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.AdRewards.Persistence.AdRewardSessionRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("AsnRiskHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("CreativeId").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<string>("DeviceRiskHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTimeOffset>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<string>("IpRiskHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTimeOffset>("IssuedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Network").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<string>("NonceHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<long>("PolicyVersion").HasColumnType("bigint");
			b.Property<long>("RequiredDurationTicks").HasColumnType("bigint");
			b.Property<string>("StartIdempotencyKeyHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("StartRequestHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<int>("State").HasColumnType("integer");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<string>("TokenHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("TokenKeyId").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<long>("Version").IsConcurrencyToken().HasColumnType("bigint");
			b.Property<Guid>("WalletId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("TenantId", "NonceHash").IsUnique();
			b.HasIndex("TenantId", "StartIdempotencyKeyHash").IsUnique();
			b.HasIndex("TenantId", "UserId", "IssuedAt");
			b.ToTable("economy_ad_reward_sessions", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_ad_reward_sessions_state", "\"State\" BETWEEN 1 AND 7");
				t.HasCheckConstraint("ck_economy_ad_reward_sessions_values", "\"PolicyVersion\" > 0 AND \"RequiredDurationTicks\" > 0 AND \"Version\" > 0");
				t.HasCheckConstraint("ck_economy_ad_reward_sessions_window", "\"ExpiresAt\" > \"IssuedAt\" AND \"UpdatedAt\" >= \"IssuedAt\"");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Bounties.Persistence.BountyEscrowFragmentRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").HasColumnType("uuid");
			b.Property<long>("AmountUnits").HasColumnType("bigint");
			b.Property<Guid>("BountyId").HasColumnType("uuid");
			b.Property<int>("Currency").HasColumnType("integer");
			b.Property<Guid?>("EscrowLotId").HasColumnType("uuid");
			b.Property<Guid>("ParentLotId").HasColumnType("uuid");
			b.Property<int>("Provenance").HasColumnType("integer");
			b.Property<string>("SelectedRootRanges").IsRequired().HasColumnType("jsonb");
			b.Property<long>("TraceUnitsPerCoinUnit").HasColumnType("bigint");
			b.HasKey("Id");
			b.HasIndex("BountyId", "EscrowLotId").IsUnique().HasDatabaseName("ux_economy_bounty_escrow_fragments_bounty_escrow_lot")
				.HasFilter("\"EscrowLotId\" IS NOT NULL");
			b.HasIndex("BountyId", "ParentLotId").IsUnique();
			b.ToTable("economy_bounty_escrow_fragments", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_bounty_escrow_fragments_amount_positive", "\"AmountUnits\" > 0");
				t.HasCheckConstraint("ck_economy_bounty_escrow_fragments_provenance", "\"Provenance\" BETWEEN 1 AND 7");
				t.HasCheckConstraint("ck_economy_bounty_escrow_fragments_scale_positive", "\"TraceUnitsPerCoinUnit\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Bounties.Persistence.BountyExpirationEventRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").HasColumnType("uuid");
			b.Property<Guid>("BountyId").HasColumnType("uuid");
			b.Property<long>("BountyVersion").HasColumnType("bigint");
			b.Property<DateTimeOffset>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<DateTimeOffset>("RecordedAt").HasColumnType("timestamp with time zone");
			b.HasKey("Id");
			b.HasIndex("BountyId").IsUnique();
			b.HasIndex("RecordedAt", "Id");
			b.ToTable("economy_bounty_expiration_events", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_bounty_expiration_events_time", "\"RecordedAt\" >= \"ExpiresAt\"");
				t.HasCheckConstraint("ck_economy_bounty_expiration_events_version", "\"BountyVersion\" > 1");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Bounties.Persistence.BountyRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").HasColumnType("uuid");
			b.Property<long>("AmountUnits").HasColumnType("bigint");
			b.Property<int>("Currency").HasColumnType("integer");
			b.Property<Guid>("EscrowWalletId").HasColumnType("uuid");
			b.Property<DateTimeOffset>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<string>("IdempotencyKey").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<int>("MinimumReputation").HasColumnType("integer");
			b.Property<DateTimeOffset>("PostedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("PosterId").HasColumnType("uuid");
			b.Property<Guid>("PosterWalletId").HasColumnType("uuid");
			b.Property<int>("ReclaimFeePpm").HasColumnType("integer");
			b.Property<string>("RequestHash").HasMaxLength(128).HasColumnType("character varying(128)");
			b.Property<bool>("RequiresInstructorVerification").HasColumnType("boolean");
			b.Property<bool>("RequiresPrerequisite").HasColumnType("boolean");
			b.Property<int>("Status").HasColumnType("integer");
			b.Property<long>("Version").HasColumnType("bigint");
			b.HasKey("Id");
			b.HasIndex("IdempotencyKey").IsUnique();
			b.HasIndex("Status", "ExpiresAt");
			b.HasIndex("PosterId", "Status", "ExpiresAt");
			b.ToTable("economy_bounties", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_bounties_amount_positive", "\"AmountUnits\" > 0");
				t.HasCheckConstraint("ck_economy_bounties_fee", "\"ReclaimFeePpm\" BETWEEN 0 AND 999999");
				t.HasCheckConstraint("ck_economy_bounties_reputation", "\"MinimumReputation\" >= 0");
				t.HasCheckConstraint("ck_economy_bounties_state", "\"Status\" BETWEEN 1 AND 4");
				t.HasCheckConstraint("ck_economy_bounties_version", "\"Version\" > 0");
				t.HasCheckConstraint("ck_economy_bounties_window", "\"ExpiresAt\" > \"PostedAt\"");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Bounties.Persistence.BountyTerminalEventRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").HasColumnType("uuid");
			b.Property<Guid>("ActorId").HasColumnType("uuid");
			b.Property<Guid>("BountyId").HasColumnType("uuid");
			b.Property<Guid>("DestinationWalletId").HasColumnType("uuid");
			b.Property<long>("FeeUnits").HasColumnType("bigint");
			b.Property<long>("FirstJournalSequence").HasColumnType("bigint");
			b.Property<string>("IdempotencyKey").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<DateTimeOffset>("OccurredAt").HasColumnType("timestamp with time zone");
			b.Property<string>("OutputLots").IsRequired().HasColumnType("jsonb");
			b.Property<Guid?>("ProceedsLotId").HasColumnType("uuid");
			b.Property<Guid?>("ProceedsSourceStampId").HasColumnType("uuid");
			b.Property<long>("ReturnedUnits").HasColumnType("bigint");
			b.Property<Guid?>("RiskDecisionId").HasColumnType("uuid");
			b.Property<int>("Status").HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("BountyId").IsUnique();
			b.HasIndex("IdempotencyKey").IsUnique();
			b.ToTable("economy_bounty_terminal_events", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_bounty_terminal_events_claim_binding", "\"Status\" <> 3 OR (\"RiskDecisionId\" IS NOT NULL AND \"ProceedsSourceStampId\" IS NOT NULL AND \"ProceedsLotId\" IS NOT NULL)");
				t.HasCheckConstraint("ck_economy_bounty_terminal_events_state", "\"Status\" IN (3, 4)");
				t.HasCheckConstraint("ck_economy_bounty_terminal_events_units", "\"ReturnedUnits\" >= 0 AND \"FeeUnits\" >= 0 AND \"FirstJournalSequence\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Integrations.AI.AiProviderCostFactEntity", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").HasColumnType("uuid");
			b.Property<Guid>("ActorId").HasColumnType("uuid");
			b.Property<Guid>("AuthorizationId").HasColumnType("uuid");
			b.Property<long>("ChargedSoftUnits").HasColumnType("bigint");
			b.Property<DateTimeOffset>("CompletedAt").HasColumnType("timestamp with time zone");
			b.Property<long>("ExactProviderCostUsdNanos").HasColumnType("bigint");
			b.Property<long>("InputCostUsdNanos").HasColumnType("bigint");
			b.Property<int>("InputTokens").HasColumnType("integer");
			b.Property<string>("Model").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<long>("OutputCostUsdNanos").HasColumnType("bigint");
			b.Property<int>("OutputTokens").HasColumnType("integer");
			b.Property<int>("Provider").HasColumnType("integer");
			b.Property<string>("ProviderUsageId").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<string>("RateCardVersion").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<Guid>("RequestId").HasColumnType("uuid");
			b.Property<string>("ServiceCode").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<int>("TotalTokens").HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("AuthorizationId").IsUnique();
			b.HasIndex("Provider", "ProviderUsageId").IsUnique();
			b.HasIndex("ServiceCode", "CompletedAt");
			b.HasIndex("TenantId", "CompletedAt");
			b.ToTable("ai_provider_cost_facts", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_ai_provider_cost_facts_charge_positive", "\"ChargedSoftUnits\" > 0");
				t.HasCheckConstraint("ck_ai_provider_cost_facts_cost_conservation", "\"InputCostUsdNanos\" >= 0 AND \"OutputCostUsdNanos\" >= 0 AND \"ExactProviderCostUsdNanos\" = \"InputCostUsdNanos\" + \"OutputCostUsdNanos\"");
				t.HasCheckConstraint("ck_ai_provider_cost_facts_token_conservation", "\"InputTokens\" >= 0 AND \"OutputTokens\" >= 0 AND \"TotalTokens\" = \"InputTokens\" + \"OutputTokens\"");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Marketplace.Persistence.MarketplaceCurrencyPolicyVersionRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<Guid>("ProductId").HasColumnType("uuid");
			b.Property<long>("Version").HasColumnType("bigint");
			b.Property<Guid>("ApprovedBy").HasColumnType("uuid");
			b.Property<string>("CanonicalPayload").IsRequired().HasColumnType("text");
			b.Property<DateTimeOffset>("EffectiveAt").HasColumnType("timestamp with time zone");
			b.Property<DateTimeOffset>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<long>("HardPriceUnits").HasColumnType("bigint");
			b.Property<string>("KeyId").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<int>("Mode").HasColumnType("integer");
			b.Property<string>("PayloadHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<int>("PlatformFeePpm").HasColumnType("integer");
			b.Property<Guid>("PlatformFeeWalletId").HasColumnType("uuid");
			b.Property<Guid>("ProposedBy").HasColumnType("uuid");
			b.Property<DateTimeOffset>("PublishedAt").HasColumnType("timestamp with time zone");
			b.Property<long>("RefundHoldTicks").HasColumnType("bigint");
			b.Property<Guid>("SellerId").HasColumnType("uuid");
			b.Property<string>("Signature").IsRequired().HasColumnType("text");
			b.Property<long>("SoftPriceUnits").HasColumnType("bigint");
			b.HasKey("TenantId", "ProductId", "Version");
			b.HasIndex("TenantId", "ProductId", "EffectiveAt", "ExpiresAt");
			b.ToTable("economy_marketplace_currency_policy_versions", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_marketplace_currency_policy_versions_dual_control", "\"ProposedBy\" <> \"ApprovedBy\"");
				t.HasCheckConstraint("ck_economy_marketplace_currency_policy_versions_fee", "\"PlatformFeePpm\" BETWEEN 0 AND 999999");
				t.HasCheckConstraint("ck_economy_marketplace_currency_policy_versions_prices", "(\"Mode\" = 1 AND \"HardPriceUnits\" > 0 AND \"SoftPriceUnits\" = 0) OR (\"Mode\" = 2 AND \"HardPriceUnits\" = 0 AND \"SoftPriceUnits\" > 0) OR (\"Mode\" IN (3, 4) AND \"HardPriceUnits\" > 0 AND \"SoftPriceUnits\" > 0)");
				t.HasCheckConstraint("ck_economy_marketplace_currency_policy_versions_version", "\"Version\" > 0");
				t.HasCheckConstraint("ck_economy_marketplace_currency_policy_versions_window", "\"ExpiresAt\" > \"EffectiveAt\" AND \"RefundHoldTicks\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Marketplace.Persistence.MarketplaceEventRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("EventKind").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<string>("EvidenceHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTimeOffset>("OccurredAt").HasColumnType("timestamp with time zone");
			b.Property<long>("Sequence").HasColumnType("bigint");
			b.Property<Guid>("SettlementId").HasColumnType("uuid");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("SettlementId", "Sequence").IsUnique();
			b.ToTable("economy_marketplace_events", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_marketplace_events_sequence", "\"Sequence\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Marketplace.Persistence.MarketplaceFundingFragmentRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").HasColumnType("uuid");
			b.Property<long>("AmountUnits").HasColumnType("bigint");
			b.Property<int>("Currency").HasColumnType("integer");
			b.Property<Guid>("ParentLotId").HasColumnType("uuid");
			b.Property<Guid>("ReservationId").HasColumnType("uuid");
			b.Property<string>("SelectedRootRanges").IsRequired().HasColumnType("jsonb");
			b.Property<Guid>("SettlementId").HasColumnType("uuid");
			b.Property<long>("TraceUnitsPerCoinUnit").HasColumnType("bigint");
			b.HasKey("Id");
			b.HasIndex("ReservationId").IsUnique();
			b.HasIndex("SettlementId", "ParentLotId", "Currency");
			b.ToTable("economy_marketplace_funding_fragments", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_marketplace_funding_fragments_amount", "\"AmountUnits\" > 0");
				t.HasCheckConstraint("ck_economy_marketplace_funding_fragments_scale", "\"TraceUnitsPerCoinUnit\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Marketplace.Persistence.MarketplaceOutboxRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<int>("AttemptCount").HasColumnType("integer");
			b.Property<string>("MessageType").IsRequired().HasMaxLength(150)
				.HasColumnType("character varying(150)");
			b.Property<DateTimeOffset>("OccurredAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Payload").IsRequired().HasColumnType("jsonb");
			b.Property<string>("PayloadHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTimeOffset?>("PublishedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("SettlementId").HasColumnType("uuid");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("SettlementId");
			b.HasIndex("PublishedAt", "OccurredAt");
			b.ToTable("economy_marketplace_outbox", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_marketplace_outbox_attempts", "\"AttemptCount\" >= 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Marketplace.Persistence.MarketplaceRefundDebtRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<long>("AmountUnits").HasColumnType("bigint");
			b.Property<int>("Currency").HasColumnType("integer");
			b.Property<string>("EvidenceHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTimeOffset>("RecordedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("RefundId").HasColumnType("uuid");
			b.Property<Guid>("ResponsibleWalletId").HasColumnType("uuid");
			b.Property<Guid>("SettlementId").HasColumnType("uuid");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("RefundId");
			b.HasIndex("SettlementId");
			b.HasIndex("TenantId", "ResponsibleWalletId", "RecordedAt");
			b.ToTable("economy_marketplace_refund_debts", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_marketplace_refund_debts_amount", "\"AmountUnits\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Marketplace.Persistence.MarketplaceRefundLegRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("RefundId").HasColumnType("uuid");
			b.Property<int>("Currency").HasColumnType("integer");
			b.Property<Guid>("SettlementId").HasColumnType("uuid");
			b.Property<long>("Units").HasColumnType("bigint");
			b.HasKey("RefundId", "Currency");
			b.HasIndex("SettlementId", "Currency");
			b.ToTable("economy_marketplace_refund_legs", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_marketplace_refund_legs_amount", "\"Units\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Marketplace.Persistence.MarketplaceRefundRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").HasColumnType("uuid");
			b.Property<Guid>("BuyerId").HasColumnType("uuid");
			b.Property<string>("CapabilityReceiptHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<Guid>("CapabilityReceiptId").HasColumnType("uuid");
			b.Property<bool>("EntitlementRevoked").HasColumnType("boolean");
			b.Property<string>("EvidenceHashes").IsRequired().HasColumnType("jsonb");
			b.Property<long>("FirstJournalSequence").HasColumnType("bigint");
			b.Property<string>("IdempotencyKey").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<bool>("IsFullRefund").HasColumnType("boolean");
			b.Property<string>("JournalHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("JurisdictionCode").IsRequired().HasMaxLength(16)
				.HasColumnType("character varying(16)");
			b.Property<long>("KillSwitchEpoch").HasColumnType("bigint");
			b.Property<long>("MarketplacePolicyVersion").HasColumnType("bigint");
			b.Property<long>("PolicyVersion").HasColumnType("bigint");
			b.Property<Guid>("PostingId").HasColumnType("uuid");
			b.Property<int>("Quantity").HasColumnType("integer");
			b.Property<string>("ReasonCode").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<string>("ReasonHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTimeOffset>("RefundedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("RefundedQuantity").HasColumnType("integer");
			b.Property<long>("ReserveVersion").HasColumnType("bigint");
			b.Property<Guid>("RiskDecisionId").HasColumnType("uuid");
			b.Property<Guid>("SettlementId").HasColumnType("uuid");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("SettlementId");
			b.HasIndex("TenantId", "IdempotencyKey").IsUnique();
			b.HasIndex("TenantId", "SettlementId", "RefundedAt");
			b.ToTable("economy_marketplace_refunds", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_marketplace_refunds_quantity", "\"Quantity\" > 0 AND \"RefundedQuantity\" >= \"Quantity\"");
				t.HasCheckConstraint("ck_economy_marketplace_refunds_sequence", "\"FirstJournalSequence\" > 0");
				t.HasCheckConstraint("ck_economy_marketplace_refunds_versions", "\"MarketplacePolicyVersion\" > 0 AND \"PolicyVersion\" > 0 AND \"ReserveVersion\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Marketplace.Persistence.MarketplaceSettlementCreditRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").HasColumnType("uuid");
			b.Property<long>("AmountUnits").HasColumnType("bigint");
			b.Property<Guid>("CreditLotId").HasColumnType("uuid");
			b.Property<int>("Currency").HasColumnType("integer");
			b.Property<string>("ParentLineage").IsRequired().HasColumnType("jsonb");
			b.Property<int>("Purpose").HasColumnType("integer");
			b.Property<Guid>("RefundHoldId").HasColumnType("uuid");
			b.Property<DateTimeOffset>("RefundHoldUntil").HasColumnType("timestamp with time zone");
			b.Property<long>("RemainingUnits").HasColumnType("bigint");
			b.Property<Guid>("SettlementId").HasColumnType("uuid");
			b.Property<Guid?>("SourceStampId").HasColumnType("uuid");
			b.Property<Guid>("WalletId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("CreditLotId").IsUnique();
			b.HasIndex("SettlementId");
			b.HasIndex("SourceStampId").IsUnique().HasFilter("\"SourceStampId\" IS NOT NULL");
			b.ToTable("economy_marketplace_settlement_credits", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_marketplace_settlement_credits_amount", "\"AmountUnits\" > 0");
				t.HasCheckConstraint("ck_economy_marketplace_settlement_credits_purpose", "\"Purpose\" BETWEEN 1 AND 2");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Marketplace.Persistence.MarketplaceSettlementLegRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("SettlementId").HasColumnType("uuid");
			b.Property<int>("Currency").HasColumnType("integer");
			b.Property<long>("PlatformFeeUnits").HasColumnType("bigint");
			b.Property<long>("RefundedUnits").HasColumnType("bigint");
			b.Property<long>("SellerUnits").HasColumnType("bigint");
			b.Property<long>("Units").HasColumnType("bigint");
			b.HasKey("SettlementId", "Currency");
			b.ToTable("economy_marketplace_settlement_legs", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_marketplace_settlement_legs_conservation", "\"Units\" > 0 AND \"SellerUnits\" >= 0 AND \"PlatformFeeUnits\" >= 0 AND \"SellerUnits\" + \"PlatformFeeUnits\" = \"Units\"");
				t.HasCheckConstraint("ck_economy_marketplace_settlement_legs_refund", "\"RefundedUnits\" BETWEEN 0 AND \"Units\"");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Marketplace.Persistence.MarketplaceSettlementRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").HasColumnType("uuid");
			b.Property<Guid>("BuyerId").HasColumnType("uuid");
			b.Property<Guid>("BuyerWalletId").HasColumnType("uuid");
			b.Property<string>("CapabilityReceiptHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<Guid>("CapabilityReceiptId").HasColumnType("uuid");
			b.Property<int>("CurrencyMode").HasColumnType("integer");
			b.Property<Guid>("EntitlementId").HasColumnType("uuid");
			b.Property<int>("EntitlementStatus").HasColumnType("integer");
			b.Property<string>("EvidenceHashes").IsRequired().HasColumnType("jsonb");
			b.Property<string>("FiatCurrencySnapshot").IsRequired().HasMaxLength(3)
				.HasColumnType("character varying(3)");
			b.Property<string>("IdempotencyKey").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<string>("JournalHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<long>("JournalSequence").HasColumnType("bigint");
			b.Property<string>("JurisdictionCode").IsRequired().HasMaxLength(16)
				.HasColumnType("character varying(16)");
			b.Property<long>("KillSwitchEpoch").HasColumnType("bigint");
			b.Property<Guid>("OrderId").HasColumnType("uuid");
			b.Property<Guid>("OrderLineItemId").HasColumnType("uuid");
			b.Property<string>("OrderSnapshotHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<Guid>("PlatformFeeWalletId").HasColumnType("uuid");
			b.Property<long>("PolicyVersion").HasColumnType("bigint");
			b.Property<Guid>("PostingId").HasColumnType("uuid");
			b.Property<int>("PriceVersionSnapshot").HasColumnType("integer");
			b.Property<Guid>("ProductId").HasColumnType("uuid");
			b.Property<Guid>("ProductPricingVersionId").HasColumnType("uuid");
			b.Property<int>("Quantity").HasColumnType("integer");
			b.Property<DateTimeOffset>("RefundHoldUntil").HasColumnType("timestamp with time zone");
			b.Property<int>("RefundedQuantity").HasColumnType("integer");
			b.Property<long>("ReserveVersion").HasColumnType("bigint");
			b.Property<Guid>("RiskDecisionId").HasColumnType("uuid");
			b.Property<Guid>("SellerId").HasColumnType("uuid");
			b.Property<Guid>("SellerWalletId").HasColumnType("uuid");
			b.Property<DateTimeOffset>("SettledAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Status").HasColumnType("integer");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<decimal>("UnitPriceSnapshot").HasColumnType("numeric");
			b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<long>("Version").HasColumnType("bigint");
			b.HasKey("Id");
			b.HasIndex("TenantId", "IdempotencyKey").IsUnique();
			b.HasIndex("TenantId", "OrderId").IsUnique();
			b.HasIndex("TenantId", "BuyerId", "SettledAt");
			b.HasIndex("TenantId", "SellerId", "SettledAt");
			b.ToTable("economy_marketplace_settlements", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_marketplace_settlements_hold", "\"RefundHoldUntil\" > \"SettledAt\"");
				t.HasCheckConstraint("ck_economy_marketplace_settlements_order_snapshot", "\"Quantity\" > 0 AND \"RefundedQuantity\" BETWEEN 0 AND \"Quantity\" AND \"UnitPriceSnapshot\" >= 0 AND \"PriceVersionSnapshot\" > 0");
				t.HasCheckConstraint("ck_economy_marketplace_settlements_receipt", "\"ReserveVersion\" > 0 AND \"JournalSequence\" > 0");
				t.HasCheckConstraint("ck_economy_marketplace_settlements_state", "\"Status\" BETWEEN 1 AND 3");
				t.HasCheckConstraint("ck_economy_marketplace_settlements_version", "\"PolicyVersion\" > 0 AND \"Version\" > 0");
				t.HasCheckConstraint("ck_economy_marketplace_settlements_wallets", "\"BuyerWalletId\" <> \"SellerWalletId\" AND \"BuyerWalletId\" <> \"PlatformFeeWalletId\" AND \"SellerWalletId\" <> \"PlatformFeeWalletId\"");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Payouts.PayoutConnectAccountRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("PayeeId").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<bool>("ChargesEnabled").HasColumnType("boolean");
			b.Property<string>("DestinationHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("Environment").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<string>("EvidenceHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTimeOffset>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<DateTimeOffset>("ObservedAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("PayoutsEnabled").HasColumnType("boolean");
			b.Property<string>("Provider").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<string>("ProviderAccountId").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<int>("State").HasColumnType("integer");
			b.Property<long>("Version").HasColumnType("bigint");
			b.HasKey("PayeeId");
			b.HasIndex("State", "ExpiresAt");
			b.HasIndex("Provider", "Environment", "ProviderAccountId").IsUnique();
			b.ToTable("economy_payout_connect_accounts", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_payout_connect_accounts_state", "\"State\" BETWEEN 1 AND 4");
				t.HasCheckConstraint("ck_economy_payout_connect_accounts_version", "\"Version\" > 0");
				t.HasCheckConstraint("ck_economy_payout_connect_accounts_window", "\"ExpiresAt\" > \"ObservedAt\"");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Payouts.PayoutDispatchOutboxRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").HasColumnType("uuid");
			b.Property<int>("AttemptCount").HasColumnType("integer");
			b.Property<DateTimeOffset>("AvailableAt").HasColumnType("timestamp with time zone");
			b.Property<DateTimeOffset?>("CompletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("IdempotencyKey").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<string>("LastErrorCode").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<DateTimeOffset?>("LeaseExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<string>("LeaseOwner").HasMaxLength(200).HasColumnType("character varying(200)");
			b.Property<Guid>("OperationId").HasColumnType("uuid");
			b.Property<string>("Payload").IsRequired().HasColumnType("jsonb");
			b.Property<string>("PayloadHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.HasKey("Id");
			b.HasIndex("OperationId").IsUnique();
			b.HasIndex("CompletedAt", "AvailableAt", "LeaseExpiresAt");
			b.ToTable("economy_payout_dispatch_outbox", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_payout_dispatch_outbox_attempts", "\"AttemptCount\" >= 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Payouts.PayoutOperationRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").HasColumnType("uuid");
			b.Property<Guid>("ActorId").HasColumnType("uuid");
			b.Property<long>("AmountUnits").HasColumnType("bigint");
			b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("DestinationHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("DispatchSnapshotHash").HasMaxLength(128).HasColumnType("character varying(128)");
			b.Property<string>("EligibilityHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<long>("FencingToken").HasColumnType("bigint");
			b.Property<string>("IdempotencyKey").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<long>("KillSwitchEpoch").HasColumnType("bigint");
			b.Property<Guid>("PayeeId").HasColumnType("uuid");
			b.Property<long>("PolicyVersion").HasColumnType("bigint");
			b.Property<string>("ProviderAccountId").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<string>("ProviderBindingHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("ProviderPayoutId").HasMaxLength(256).HasColumnType("character varying(256)");
			b.Property<string>("RequestHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<long>("ReserveAuthorizationEpoch").HasColumnType("bigint");
			b.Property<long>("ReserveVersion").HasColumnType("bigint");
			b.Property<Guid>("RiskDecisionId").HasColumnType("uuid");
			b.Property<int>("State").HasColumnType("integer");
			b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<long>("Version").IsConcurrencyToken().HasColumnType("bigint");
			b.Property<Guid>("WalletId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("IdempotencyKey").IsUnique().HasDatabaseName("ux_economy_payout_operations_idempotency");
			b.HasIndex("State", "UpdatedAt").HasDatabaseName("ix_economy_payout_operations_state_updated");
			b.ToTable("economy_payout_operations", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_payout_operations_dispatch", "(\"State\" = 1 AND \"DispatchSnapshotHash\" IS NULL) OR (\"State\" BETWEEN 2 AND 6 AND \"DispatchSnapshotHash\" IS NOT NULL)");
				t.HasCheckConstraint("ck_economy_payout_operations_positive_values", "\"AmountUnits\" > 0 AND \"Version\" > 0 AND \"FencingToken\" > 0 AND \"KillSwitchEpoch\" > 0 AND \"ReserveVersion\" > 0 AND \"ReserveAuthorizationEpoch\" > 0 AND \"PolicyVersion\" > 0");
				t.HasCheckConstraint("ck_economy_payout_operations_state", "\"State\" BETWEEN 1 AND 6");
				t.HasCheckConstraint("ck_economy_payout_operations_timestamps", "\"UpdatedAt\" >= \"CreatedAt\"");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Payouts.PayoutProviderEventRow", delegate(EntityTypeBuilder b)
		{
			b.Property<string>("EventId").HasMaxLength(256).HasColumnType("character varying(256)");
			b.Property<string>("EventHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<Guid>("OperationId").HasColumnType("uuid");
			b.Property<DateTimeOffset>("RecordedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("ResultingState").HasColumnType("integer");
			b.HasKey("EventId");
			b.HasIndex("OperationId", "RecordedAt").HasDatabaseName("ix_economy_payout_provider_events_operation_recorded");
			b.ToTable("economy_payout_provider_events", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyAccountRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<int>("Code").HasColumnType("integer");
			b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Currency").HasColumnType("integer");
			b.Property<int?>("Provenance").HasColumnType("integer");
			b.Property<Guid?>("WalletId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("WalletId", "Code", "Currency", "Provenance").IsUnique();
			b.ToTable("economy_accounts", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_accounts_wallet_partition", "(\"WalletId\" IS NULL AND \"Code\" NOT IN (2, 3, 4)) OR (\"WalletId\" IS NOT NULL AND \"Code\" IN (2, 3, 4))");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyAnchorVerificationRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("ETag").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<Guid>("ExternalAnchorId").HasColumnType("uuid");
			b.Property<string>("KeyId").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<string>("ObjectHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<bool>("ObjectMatches").HasColumnType("boolean");
			b.Property<string>("ObjectVersion").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<DateTimeOffset>("RetainUntil").HasColumnType("timestamp with time zone");
			b.Property<bool>("SignatureValid").HasColumnType("boolean");
			b.Property<DateTimeOffset>("VerifiedAt").HasColumnType("timestamp with time zone");
			b.HasKey("Id");
			b.HasIndex("ExternalAnchorId", "VerifiedAt");
			b.ToTable("economy_anchor_verifications", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyCapabilityPolicyApprovalRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("ActorId").HasColumnType("uuid");
			b.Property<DateTimeOffset>("ApprovedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("PolicyId").HasColumnType("uuid");
			b.Property<string>("ReauthenticationHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.HasKey("Id");
			b.HasIndex("PolicyId", "ActorId").IsUnique().HasDatabaseName("ux_economy_capability_policy_approvals_policy_actor");
			b.ToTable("economy_capability_policy_approvals", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyCapabilityPolicyRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTimeOffset?>("ApprovedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("ApprovedBy").HasColumnType("uuid");
			b.Property<string>("CanonicalPayload").IsRequired().HasColumnType("jsonb");
			b.Property<int>("Capability").HasColumnType("integer");
			b.Property<DateTimeOffset>("EffectiveAt").HasColumnType("timestamp with time zone");
			b.Property<DateTimeOffset>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<string>("JurisdictionCode").IsRequired().HasMaxLength(16)
				.HasColumnType("character varying(16)");
			b.Property<string>("KeyId").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<string>("PayloadHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTimeOffset>("ProposedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("ProposedBy").HasColumnType("uuid");
			b.Property<bool>("ProviderReady").HasColumnType("boolean");
			b.Property<string>("RequestHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("ScopeKey").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<string>("Signature").IsRequired().HasMaxLength(2048)
				.HasColumnType("character varying(2048)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<long>("Version").HasColumnType("bigint");
			b.HasKey("Id");
			b.HasIndex("RequestHash").IsUnique().HasDatabaseName("ux_economy_capability_policies_request_hash");
			b.HasIndex("ScopeKey").IsUnique().HasDatabaseName("ux_economy_capability_policies_active_scope")
				.HasFilter("\"IsActive\"");
			b.HasIndex("ScopeKey", "Version").IsUnique().HasDatabaseName("ux_economy_capability_policies_scope_version");
			b.ToTable("economy_capability_policies", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_capability_policies_dual_control", "(\"ApprovedBy\" IS NULL AND \"ApprovedAt\" IS NULL AND NOT \"IsActive\") OR (\"ApprovedBy\" IS NOT NULL AND \"ApprovedBy\" <> \"ProposedBy\" AND \"ApprovedAt\" >= \"ProposedAt\")");
				t.HasCheckConstraint("ck_economy_capability_policies_version", "\"Version\" > 0");
				t.HasCheckConstraint("ck_economy_capability_policies_window", "\"ExpiresAt\" > \"EffectiveAt\" AND (\"ApprovedAt\" IS NULL OR \"EffectiveAt\" >= \"ApprovedAt\")");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyCapabilityReceiptConsumptionRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("ActorId").HasColumnType("uuid");
			b.Property<DateTimeOffset>("ConsumedAt").HasColumnType("timestamp with time zone");
			b.Property<long>("KillSwitchEpoch").HasColumnType("bigint");
			b.Property<string>("OperationFingerprint").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<Guid>("ReceiptId").HasColumnType("uuid");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("ReceiptId").IsUnique().HasDatabaseName("ux_economy_capability_receipt_consumptions_receipt");
			b.ToTable("economy_capability_receipt_consumptions", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyCapabilityReceiptRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("ActorId").HasColumnType("uuid");
			b.Property<int>("Capability").HasColumnType("integer");
			b.Property<string>("DestinationHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("EvidenceHashes").IsRequired().HasColumnType("jsonb");
			b.Property<DateTimeOffset>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<DateTimeOffset>("IssuedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("JurisdictionCode").IsRequired().HasMaxLength(16)
				.HasColumnType("character varying(16)");
			b.Property<string>("KeyId").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<long>("KillSwitchEpoch").HasColumnType("bigint");
			b.Property<string>("OperationFingerprint").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<long>("PolicyVersion").HasColumnType("bigint");
			b.Property<string>("ProviderHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("ReceiptHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<long>("ReserveVersion").HasColumnType("bigint");
			b.Property<Guid>("RiskDecisionId").HasColumnType("uuid");
			b.Property<string>("Signature").IsRequired().HasMaxLength(2048)
				.HasColumnType("character varying(2048)");
			b.Property<string>("SourceRootHashes").IsRequired().HasColumnType("jsonb");
			b.Property<string>("SubjectReference").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("ReceiptHash").IsUnique().HasDatabaseName("ux_economy_capability_receipts_hash");
			b.HasIndex("RiskDecisionId");
			b.HasIndex("TenantId", "OperationFingerprint").IsUnique().HasDatabaseName("ux_economy_capability_receipts_tenant_operation");
			b.ToTable("economy_capability_receipts", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_capability_receipts_lifetime", "\"ExpiresAt\" > \"IssuedAt\"");
				t.HasCheckConstraint("ck_economy_capability_receipts_versions", "\"PolicyVersion\" > 0 AND \"ReserveVersion\" > 0 AND \"KillSwitchEpoch\" >= 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyChainHeadRow", delegate(EntityTypeBuilder b)
		{
			b.Property<short>("Id").ValueGeneratedOnAdd().HasColumnType("smallint");
			b.Property<short>("Id").UseIdentityByDefaultColumn();
			b.Property<string>("Hash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<long>("Sequence").HasColumnType("bigint");
			b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.HasKey("Id");
			b.ToTable("economy_chain_head", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_chain_head_singleton", "\"Id\" = 1");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyComplianceEvidenceRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("Environment").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<string>("EvidenceHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("EvidenceKind").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<DateTimeOffset>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<DateTimeOffset>("IssuedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("PayloadHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<long>("PolicyVersion").HasColumnType("bigint");
			b.Property<string>("Provider").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<string>("ProviderEventId").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<string>("RawObjectReference").IsRequired().HasMaxLength(1000)
				.HasColumnType("character varying(1000)");
			b.Property<DateTimeOffset>("ReceivedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Result").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<bool>("SignatureVerified").HasColumnType("boolean");
			b.Property<string>("SubjectHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<long>("Version").HasColumnType("bigint");
			b.HasKey("Id");
			b.HasIndex("Provider", "Environment", "ProviderEventId").IsUnique().HasDatabaseName("ux_economy_compliance_evidence_provider_event");
			b.HasIndex("TenantId", "SubjectHash", "EvidenceKind", "Version").IsUnique().HasDatabaseName("ux_economy_compliance_evidence_subject_version");
			b.ToTable("economy_compliance_evidence", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_compliance_evidence_lifetime", "\"ExpiresAt\" > \"IssuedAt\" AND \"ReceivedAt\" >= \"IssuedAt\"");
				t.HasCheckConstraint("ck_economy_compliance_evidence_versions", "\"Version\" > 0 AND \"PolicyVersion\" > 0 AND length(btrim(\"EvidenceKind\")) > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyComplianceHoldEventRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("ActorId").HasColumnType("uuid");
			b.Property<string>("EvidenceHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<Guid>("HoldId").HasColumnType("uuid");
			b.Property<string>("Kind").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<DateTimeOffset>("OccurredAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Sequence").HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("HoldId", "Sequence").IsUnique();
			b.ToTable("economy_compliance_hold_events", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_compliance_hold_events_sequence", "\"Sequence\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyComplianceHoldRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTimeOffset>("ActivatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("ActivatedBy").HasColumnType("uuid");
			b.Property<int?>("Capability").HasColumnType("integer");
			b.Property<string>("CaseReferenceHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("EvidenceHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTimeOffset>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<string>("IdempotencyKeyHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("ReasonCode").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<DateTimeOffset?>("ReleasedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("ReleasedBy").HasColumnType("uuid");
			b.Property<string>("RequestHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("ScopeKey").IsRequired().HasMaxLength(512)
				.HasColumnType("character varying(512)");
			b.Property<string>("SubjectHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("IdempotencyKeyHash").IsUnique().HasDatabaseName("ux_economy_compliance_holds_idempotency");
			b.HasIndex("ScopeKey", "ReleasedAt", "ExpiresAt").HasDatabaseName("ix_economy_compliance_holds_active_scope");
			b.HasIndex("TenantId", "SubjectHash", "ExpiresAt");
			b.ToTable("economy_compliance_holds", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_compliance_holds_lifetime", "\"ExpiresAt\" > \"ActivatedAt\"");
				t.HasCheckConstraint("ck_economy_compliance_holds_release", "(\"ReleasedAt\" IS NULL AND \"ReleasedBy\" IS NULL) OR (\"ReleasedAt\" >= \"ActivatedAt\" AND \"ReleasedBy\" IS NOT NULL)");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyComplianceInboxRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("Environment").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<string>("PayloadHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTimeOffset?>("ProcessedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("ProcessingError").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<string>("Provider").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<string>("ProviderEventId").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<string>("RawObjectReference").IsRequired().HasMaxLength(1000)
				.HasColumnType("character varying(1000)");
			b.Property<DateTimeOffset>("ReceivedAt").HasColumnType("timestamp with time zone");
			b.HasKey("Id");
			b.HasIndex("Provider", "Environment", "ProviderEventId").IsUnique().HasDatabaseName("ux_economy_compliance_inbox_provider_event");
			b.ToTable("economy_compliance_inbox", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyComplianceOutboxRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTimeOffset?>("DispatchedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("EvidenceId").HasColumnType("uuid");
			b.Property<DateTimeOffset>("OccurredAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Payload").IsRequired().HasColumnType("jsonb");
			b.Property<string>("PayloadHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("Type").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.HasKey("Id");
			b.HasIndex("EvidenceId").IsUnique().HasDatabaseName("ux_economy_compliance_outbox_evidence");
			b.ToTable("economy_compliance_outbox", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyCreditLotRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<long>("AmountUnits").HasColumnType("bigint");
			b.Property<bool>("CashOutEligible").HasColumnType("boolean");
			b.Property<DateTimeOffset>("ConfirmedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTimeOffset>("CreditedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Currency").HasColumnType("integer");
			b.Property<long>("JournalSequence").HasColumnType("bigint");
			b.Property<DateTimeOffset>("OriginalMaturesAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Provenance").HasColumnType("integer");
			b.Property<long>("ReversalEpoch").HasColumnType("bigint");
			b.Property<Guid>("RootSourceStampId").HasColumnType("uuid");
			b.Property<int>("State").HasColumnType("integer");
			b.Property<Guid>("WalletId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("RootSourceStampId").HasDatabaseName("ix_economy_credit_lots_root_source");
			b.HasIndex("WalletId");
			b.ToTable("economy_credit_lots", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_credit_lots_amount_positive", "\"AmountUnits\" > 0");
				t.HasCheckConstraint("ck_economy_credit_lots_maturity_order", "\"OriginalMaturesAt\" >= \"ConfirmedAt\"");
				t.HasCheckConstraint("ck_economy_credit_lots_maturity_policy", "(\"Provenance\" = 2 AND \"Currency\" = 1 AND \"CashOutEligible\" AND \"OriginalMaturesAt\" = \"ConfirmedAt\" + INTERVAL '120 days') OR (\"Provenance\" <> 2 AND NOT \"CashOutEligible\")");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyCustodyObservationRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("AssetKey").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<long>("EligibleUsdNanos").HasColumnType("bigint");
			b.Property<DateTimeOffset>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<string>("KeyId").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<DateTimeOffset>("ObservedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("PayloadHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("Provider").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<int>("Purpose").HasColumnType("integer");
			b.Property<string>("Signature").IsRequired().HasMaxLength(2048)
				.HasColumnType("character varying(2048)");
			b.Property<long>("Version").HasColumnType("bigint");
			b.HasKey("Id");
			b.HasIndex("Provider", "AssetKey", "Version").IsUnique().HasDatabaseName("ux_economy_custody_observations_provider_asset_version");
			b.ToTable("economy_custody_observations", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_custody_observations_lifetime", "\"ExpiresAt\" > \"ObservedAt\"");
				t.HasCheckConstraint("ck_economy_custody_observations_values", "\"Version\" > 0 AND \"EligibleUsdNanos\" >= 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyCustodyReconciliationRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<long>("EligibleAssetUsdNanos").HasColumnType("bigint");
			b.Property<string>("EvidenceHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<bool>("IsReconciled").HasColumnType("boolean");
			b.Property<long>("LiabilityUsdNanos").HasColumnType("bigint");
			b.Property<string>("ObservationIds").IsRequired().HasColumnType("jsonb");
			b.Property<DateTimeOffset>("ReconciledAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("ReconciledBy").HasColumnType("uuid");
			b.Property<long>("ReserveVersion").HasColumnType("bigint");
			b.Property<long>("VarianceUsdNanos").HasColumnType("bigint");
			b.HasKey("Id");
			b.HasIndex("ReserveVersion").IsUnique().HasDatabaseName("ux_economy_custody_reconciliations_reserve");
			b.ToTable("economy_custody_reconciliations", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_custody_reconciliations_values", "\"ReserveVersion\" > 0 AND \"LiabilityUsdNanos\" >= 0 AND \"EligibleAssetUsdNanos\" >= 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyDispatchSnapshotRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<long>("AmountUnits").HasColumnType("bigint");
			b.Property<string>("ChainHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<long>("ChainSequence").HasColumnType("bigint");
			b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Currency").HasColumnType("integer");
			b.Property<string>("Destination").IsRequired().HasMaxLength(512)
				.HasColumnType("character varying(512)");
			b.Property<string>("EligibilityPayload").IsRequired().HasColumnType("jsonb");
			b.Property<long>("FencingToken").HasColumnType("bigint");
			b.Property<long>("KillSwitchEpoch").HasColumnType("bigint");
			b.Property<Guid>("PostingGroupId").HasColumnType("uuid");
			b.Property<long>("ReserveAuthorizationEpoch").HasColumnType("bigint");
			b.Property<long>("ReserveVersion").HasColumnType("bigint");
			b.Property<string>("SnapshotHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.HasKey("Id");
			b.HasIndex("PostingGroupId");
			b.HasIndex("SnapshotHash").IsUnique().HasDatabaseName("ux_economy_dispatch_snapshots_hash");
			b.ToTable("economy_dispatch_snapshots", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_dispatch_snapshots_amount_positive", "\"AmountUnits\" > 0");
				t.HasCheckConstraint("ck_economy_dispatch_snapshots_reserve_authorization", "\"ReserveVersion\" > 0 AND \"ReserveAuthorizationEpoch\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyDisputeFragmentFreezeRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<long>("AmountUnits").HasColumnType("bigint");
			b.Property<Guid>("CreditLotId").HasColumnType("uuid");
			b.Property<int>("Currency").HasColumnType("integer");
			b.Property<DateTimeOffset>("PlacedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("ProviderDisputeReference").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<Guid>("RootSourceStampId").HasColumnType("uuid");
			b.Property<int>("Status").HasColumnType("integer");
			b.Property<DateTimeOffset?>("TerminalAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("WalletId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("CreditLotId");
			b.HasIndex("ProviderDisputeReference");
			b.HasIndex("WalletId");
			b.HasIndex("RootSourceStampId", "Status").HasDatabaseName("ix_economy_dispute_fragment_freezes_root_status");
			b.ToTable("economy_dispute_fragment_freezes", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_dispute_fragment_freezes_amount_positive", "\"AmountUnits\" > 0");
				t.HasCheckConstraint("ck_economy_dispute_fragment_freezes_state_timestamp", "(\"Status\" = 1 AND \"TerminalAt\" IS NULL) OR (\"Status\" IN (2, 3) AND \"TerminalAt\" >= \"PlacedAt\")");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyDisputeFragmentRangeRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("DisputeFragmentFreezeId").HasColumnType("uuid");
			b.Property<long>("EndExclusive").HasColumnType("bigint");
			b.Property<long>("ReversalEpoch").HasColumnType("bigint");
			b.Property<long>("StartInclusive").HasColumnType("bigint");
			b.HasKey("Id");
			b.HasIndex("DisputeFragmentFreezeId", "StartInclusive", "EndExclusive").IsUnique().HasDatabaseName("ux_economy_dispute_fragment_ranges_freeze_interval");
			b.ToTable("economy_dispute_fragment_ranges", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_dispute_fragment_ranges_half_open", "\"StartInclusive\" >= 0 AND \"EndExclusive\" > \"StartInclusive\" AND \"ReversalEpoch\" >= 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyEntityGraphEdgeRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("EvidenceHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<Guid>("LeftNodeId").HasColumnType("uuid");
			b.Property<DateTimeOffset>("RecordedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Relationship").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<Guid>("RightNodeId").HasColumnType("uuid");
			b.Property<DateTimeOffset?>("SupersededAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<long>("Version").HasColumnType("bigint");
			b.HasKey("Id");
			b.HasIndex("LeftNodeId");
			b.HasIndex("RightNodeId");
			b.HasIndex("TenantId", "LeftNodeId", "RightNodeId", "Version").IsUnique().HasDatabaseName("ux_economy_entity_graph_edges_pair_version");
			b.ToTable("economy_entity_graph_edges", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_entity_graph_edges_distinct_nodes", "\"LeftNodeId\" <> \"RightNodeId\"");
				t.HasCheckConstraint("ck_economy_entity_graph_edges_version", "\"Version\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyEntityGraphNodeRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("EvidenceHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("IdentityHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTimeOffset>("RecordedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTimeOffset?>("SupersededAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<int>("Type").HasColumnType("integer");
			b.Property<long>("Version").HasColumnType("bigint");
			b.HasKey("Id");
			b.HasIndex("TenantId", "Type", "IdentityHash", "Version").IsUnique().HasDatabaseName("ux_economy_entity_graph_nodes_identity_version");
			b.ToTable("economy_entity_graph_nodes", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_entity_graph_nodes_version", "\"Version\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyEntryAllocationRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<long>("AmountUnits").HasColumnType("bigint");
			b.Property<Guid>("JournalLineId").HasColumnType("uuid");
			b.Property<Guid>("ParentLotId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("ParentLotId").HasDatabaseName("ix_economy_entry_allocations_parent_lot");
			b.HasIndex("JournalLineId", "ParentLotId").IsUnique().HasDatabaseName("ux_economy_entry_allocations_line_parent");
			b.ToTable("economy_entry_allocations", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_entry_allocations_amount_positive", "\"AmountUnits\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyExternalAnchorRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTimeOffset>("AnchoredAt").HasColumnType("timestamp with time zone");
			b.Property<string>("DispatchSnapshotHash").HasMaxLength(128).HasColumnType("character varying(128)");
			b.Property<string>("JournalHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<long>("JournalSequence").HasColumnType("bigint");
			b.Property<string>("Provider").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<string>("ProviderReference").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<string>("Signature").IsRequired().HasMaxLength(1024)
				.HasColumnType("character varying(1024)");
			b.Property<string>("WormReference").IsRequired().HasMaxLength(1024)
				.HasColumnType("character varying(1024)");
			b.HasKey("Id");
			b.HasIndex("JournalSequence").HasDatabaseName("ix_economy_external_anchors_chain_sequence");
			b.ToTable("economy_external_anchors", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyFragmentRootRangeRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid?>("CreditLotId").HasColumnType("uuid");
			b.Property<long>("EndExclusive").HasColumnType("bigint");
			b.Property<Guid?>("EntryAllocationId").HasColumnType("uuid");
			b.Property<long>("ReversalEpoch").HasColumnType("bigint");
			b.Property<Guid>("RootSourceStampId").HasColumnType("uuid");
			b.Property<long>("StartInclusive").HasColumnType("bigint");
			b.HasKey("Id");
			b.HasIndex("CreditLotId");
			b.HasIndex("EntryAllocationId");
			b.HasIndex("RootSourceStampId", "ReversalEpoch").HasDatabaseName("ix_economy_fragment_root_ranges_root_epoch");
			b.ToTable("economy_fragment_root_ranges", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_fragment_root_ranges_half_open", "\"StartInclusive\" >= 0 AND \"EndExclusive\" > \"StartInclusive\"");
				t.HasCheckConstraint("ck_economy_fragment_root_ranges_single_owner", "(\"CreditLotId\" IS NULL) <> (\"EntryAllocationId\" IS NULL)");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyFundingClaimRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("SourceStampId").HasColumnType("uuid");
			b.Property<long>("AuthoritativeUsdMinorUnits").HasColumnType("bigint");
			b.Property<DateTimeOffset?>("ConfirmedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("ConnectedAccount").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<long>("CumulativeProviderReversalUnits").HasColumnType("bigint");
			b.Property<string>("Environment").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<DateTimeOffset>("ObservedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("PostingGroupId").HasColumnType("uuid");
			b.Property<string>("Provider").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<string>("ProviderMonetaryLeg").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<string>("ProviderObject").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<Guid?>("RootCreditLotId").HasColumnType("uuid");
			b.Property<int>("State").HasColumnType("integer");
			b.Property<DateTimeOffset>("StateChangedAt").HasColumnType("timestamp with time zone");
			b.Property<long>("Version").IsConcurrencyToken().HasColumnType("bigint");
			b.Property<Guid>("WalletId").HasColumnType("uuid");
			b.HasKey("SourceStampId");
			b.HasIndex("PostingGroupId").IsUnique().HasDatabaseName("ux_economy_funding_claims_posting_group")
				.HasFilter("\"PostingGroupId\" IS NOT NULL");
			b.HasIndex("RootCreditLotId").IsUnique().HasDatabaseName("ux_economy_funding_claims_root_lot")
				.HasFilter("\"RootCreditLotId\" IS NOT NULL");
			b.HasIndex("WalletId");
			b.HasIndex("Provider", "Environment", "ConnectedAccount", "ProviderObject", "ProviderMonetaryLeg").IsUnique().HasDatabaseName("ux_economy_funding_claims_provider_leg");
			b.ToTable("economy_funding_claims", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_funding_claims_amount_positive", "\"AuthoritativeUsdMinorUnits\" > 0");
				t.HasCheckConstraint("ck_economy_funding_claims_lifecycle", "(\"State\" = 1 AND \"ConfirmedAt\" IS NULL AND \"StateChangedAt\" = \"ObservedAt\" AND \"PostingGroupId\" IS NULL AND \"RootCreditLotId\" IS NULL AND \"CumulativeProviderReversalUnits\" = 0) OR (\"State\" = 2 AND \"ConfirmedAt\" >= \"ObservedAt\" AND \"StateChangedAt\" >= \"ConfirmedAt\" AND \"PostingGroupId\" IS NOT NULL AND \"RootCreditLotId\" IS NOT NULL) OR (\"State\" IN (3, 4) AND \"ConfirmedAt\" IS NULL AND \"StateChangedAt\" >= \"ObservedAt\" AND \"PostingGroupId\" IS NULL AND \"RootCreditLotId\" IS NULL AND \"CumulativeProviderReversalUnits\" = 0) OR (\"State\" IN (5, 6) AND \"ConfirmedAt\" >= \"ObservedAt\" AND \"StateChangedAt\" >= \"ConfirmedAt\" AND \"PostingGroupId\" IS NOT NULL AND \"RootCreditLotId\" IS NOT NULL AND \"CumulativeProviderReversalUnits\" > 0)");
				t.HasCheckConstraint("ck_economy_funding_claims_provider_reversal_bounds", "\"CumulativeProviderReversalUnits\" >= 0 AND \"CumulativeProviderReversalUnits\" <= \"AuthoritativeUsdMinorUnits\"");
				t.HasCheckConstraint("ck_economy_funding_claims_version_positive", "\"Version\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyHoldEventRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("ActorId").HasColumnType("uuid");
			b.Property<string>("EvidenceHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<Guid>("HoldId").HasColumnType("uuid");
			b.Property<int>("Kind").HasColumnType("integer");
			b.Property<DateTimeOffset>("OccurredAt").HasColumnType("timestamp with time zone");
			b.Property<long>("Sequence").HasColumnType("bigint");
			b.HasKey("Id");
			b.HasIndex("HoldId", "Sequence").IsUnique().HasDatabaseName("ux_economy_hold_events_hold_sequence");
			b.ToTable("economy_hold_events", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_hold_events_sequence_positive", "\"Sequence\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyHoldRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<long>("AmountUnits").HasColumnType("bigint");
			b.Property<int>("Currency").HasColumnType("integer");
			b.Property<DateTimeOffset>("EffectiveAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Reason").HasColumnType("integer");
			b.Property<DateTimeOffset?>("ReleasedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Status").HasColumnType("integer");
			b.Property<Guid>("WalletId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("WalletId", "Status").HasDatabaseName("ix_economy_holds_wallet_status");
			b.ToTable("economy_holds", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_holds_amount_positive", "\"AmountUnits\" > 0");
				t.HasCheckConstraint("ck_economy_holds_state_timestamp", "(\"Status\" = 1 AND \"ReleasedAt\" IS NULL) OR (\"Status\" <> 1 AND \"ReleasedAt\" >= \"EffectiveAt\")");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyIdempotencyRecordRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Key").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<Guid>("PostingGroupId").HasColumnType("uuid");
			b.Property<string>("RequestHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.HasKey("Id");
			b.HasIndex("Key").IsUnique().HasDatabaseName("ux_economy_idempotency_records_key");
			b.HasIndex("PostingGroupId");
			b.ToTable("economy_idempotency_records", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyJournalEntryRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("CanonicalPayloadHash").HasMaxLength(128).HasColumnType("character varying(128)");
			b.Property<string>("Hash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<int>("HashAlgorithmVersion").HasColumnType("integer");
			b.Property<Guid>("PostingGroupId").HasColumnType("uuid");
			b.Property<string>("PreviousHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTimeOffset>("RecordedAt").HasColumnType("timestamp with time zone");
			b.Property<long>("Sequence").HasColumnType("bigint");
			b.HasKey("Id");
			b.HasIndex("PostingGroupId").IsUnique().HasDatabaseName("ux_economy_journal_entries_posting_group_id");
			b.HasIndex("Sequence").IsUnique().HasDatabaseName("ux_economy_journal_entries_sequence");
			b.ToTable("economy_journal_entries", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_journal_entries_hash_algorithm", "(\"HashAlgorithmVersion\" = 0 AND \"CanonicalPayloadHash\" IS NULL) OR (\"HashAlgorithmVersion\" IN (1, 2) AND length(btrim(\"CanonicalPayloadHash\")) > 0)");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyJournalLineRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("AccountId").HasColumnType("uuid");
			b.Property<long>("AmountUnits").HasColumnType("bigint");
			b.Property<Guid?>("CreditLotId").HasColumnType("uuid");
			b.Property<int>("Currency").HasColumnType("integer");
			b.Property<Guid>("JournalEntryId").HasColumnType("uuid");
			b.Property<int?>("Provenance").HasColumnType("integer");
			b.Property<int>("Sequence").HasColumnType("integer");
			b.Property<int>("Side").HasColumnType("integer");
			b.Property<Guid?>("WalletId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("AccountId");
			b.HasIndex("CreditLotId");
			b.HasIndex("WalletId");
			b.HasIndex("JournalEntryId", "Sequence").IsUnique();
			b.ToTable("economy_journal_lines", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_journal_lines_amount_positive", "\"AmountUnits\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyJournalVerificationCheckpointRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTimeOffset>("CompletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("CurrentHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("FailureCode").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<long>("FencingToken").HasColumnType("bigint");
			b.Property<long>("FromSequence").HasColumnType("bigint");
			b.Property<bool>("IsValid").HasColumnType("boolean");
			b.Property<string>("PreviousHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTimeOffset>("StartedAt").HasColumnType("timestamp with time zone");
			b.Property<long>("ToSequence").HasColumnType("bigint");
			b.HasKey("Id");
			b.HasIndex("ToSequence", "CompletedAt").HasDatabaseName("ix_economy_journal_verification_checkpoints_sequence");
			b.ToTable("economy_journal_verification_checkpoints", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_journal_verification_checkpoints_range", "\"FromSequence\" >= 0 AND \"ToSequence\" >= \"FromSequence\"");
				t.HasCheckConstraint("ck_economy_journal_verification_checkpoints_time", "\"CompletedAt\" >= \"StartedAt\"");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyKillSwitchReleaseApprovalRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("ActorId").HasColumnType("uuid");
			b.Property<DateTimeOffset>("ApprovedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("KillSwitchId").HasColumnType("uuid");
			b.Property<string>("ReauthenticationHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.HasKey("Id");
			b.HasIndex("KillSwitchId", "ActorId").IsUnique().HasDatabaseName("ux_economy_kill_switch_release_approvals_switch_actor");
			b.ToTable("economy_kill_switch_release_approvals", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyKillSwitchRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTimeOffset>("ActivatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("ActivatedBy").HasColumnType("uuid");
			b.Property<int?>("Capability").HasColumnType("integer");
			b.Property<long>("Epoch").HasColumnType("bigint");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<string>("Reason").IsRequired().HasMaxLength(1000)
				.HasColumnType("character varying(1000)");
			b.Property<string>("ReleaseProposalReauthenticationHash").HasMaxLength(128).HasColumnType("character varying(128)");
			b.Property<DateTimeOffset?>("ReleaseProposedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("ReleaseProposedBy").HasColumnType("uuid");
			b.Property<DateTimeOffset?>("ReleasedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("RequestHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("ScopeKey").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("RequestHash").IsUnique().HasDatabaseName("ux_economy_kill_switches_request_hash");
			b.HasIndex("ScopeKey").IsUnique().HasDatabaseName("ux_economy_kill_switches_active_scope")
				.HasFilter("\"IsActive\"");
			b.HasIndex("ScopeKey", "Epoch").IsUnique().HasDatabaseName("ux_economy_kill_switches_scope_epoch");
			b.ToTable("economy_kill_switches", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_kill_switches_epoch", "\"Epoch\" > 0");
				t.HasCheckConstraint("ck_economy_kill_switches_release_proposal", "(\"ReleaseProposedBy\" IS NULL AND \"ReleaseProposedAt\" IS NULL AND \"ReleaseProposalReauthenticationHash\" IS NULL) OR (\"ReleaseProposedBy\" IS NOT NULL AND \"ReleaseProposedAt\" >= \"ActivatedAt\" AND length(btrim(\"ReleaseProposalReauthenticationHash\")) > 0)");
				t.HasCheckConstraint("ck_economy_kill_switches_state", "(\"IsActive\" AND \"ReleasedAt\" IS NULL) OR (NOT \"IsActive\" AND \"ReleasedAt\" >= \"ActivatedAt\")");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyLotLineageEdgeRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<long>("AmountUnits").HasColumnType("bigint");
			b.Property<Guid>("ChildLotId").HasColumnType("uuid");
			b.Property<int>("Currency").HasColumnType("integer");
			b.Property<Guid>("ParentLotId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("ChildLotId");
			b.HasIndex("ParentLotId").HasDatabaseName("ix_economy_lot_lineage_edges_parent_lot");
			b.HasIndex("ParentLotId", "ChildLotId").IsUnique().HasDatabaseName("ux_economy_lot_lineage_edges_parent_child");
			b.ToTable("economy_lot_lineage_edges", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_lot_lineage_edges_amount_positive", "\"AmountUnits\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyOutboxMessageRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTimeOffset>("OccurredAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Payload").IsRequired().HasColumnType("text");
			b.Property<string>("PayloadHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<Guid>("PostingGroupId").HasColumnType("uuid");
			b.Property<string>("Type").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.HasKey("Id");
			b.HasIndex("PayloadHash").IsUnique().HasDatabaseName("ux_economy_outbox_messages_payload_hash");
			b.HasIndex("PostingGroupId");
			b.ToTable("economy_outbox_messages", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyPostingGroupRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("ActorId").HasColumnType("uuid");
			b.Property<int>("Authority").HasColumnType("integer");
			b.Property<Guid>("CapabilityId").HasColumnType("uuid");
			b.Property<string>("IdempotencyKey").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<long>("PolicyVersion").HasColumnType("bigint");
			b.Property<DateTimeOffset>("RecordedAt").HasColumnType("timestamp with time zone");
			b.Property<long>("ReserveAuthorizationEpoch").HasColumnType("bigint");
			b.Property<long>("ReserveVersion").HasColumnType("bigint");
			b.Property<Guid?>("RiskDecisionId").HasColumnType("uuid");
			b.Property<Guid?>("SourceStampId").HasColumnType("uuid");
			b.Property<int>("Status").HasColumnType("integer");
			b.Property<int>("TemplateKind").HasColumnType("integer");
			b.Property<int>("TemplateVersion").HasColumnType("integer");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("IdempotencyKey").IsUnique().HasDatabaseName("ux_economy_posting_groups_idempotency_key");
			b.HasIndex("SourceStampId").IsUnique().HasDatabaseName("ux_economy_posting_groups_source_stamp")
				.HasFilter("\"SourceStampId\" IS NOT NULL AND \"TemplateKind\" = 1");
			b.ToTable("economy_posting_groups", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_posting_groups_authority_template", "(\"TemplateKind\" IN (1, 2, 3, 18, 19, 20) AND \"Authority\" = 1) OR (\"TemplateKind\" IN (4, 5, 7, 8, 17, 22) AND \"Authority\" = 2) OR (\"TemplateKind\" IN (6, 21) AND \"Authority\" = 3) OR (\"TemplateKind\" IN (9, 10, 23, 24) AND \"Authority\" = 4) OR (\"TemplateKind\" IN (11, 12, 13) AND \"Authority\" = 5) OR (\"TemplateKind\" IN (14, 15, 16) AND \"Authority\" = 6) OR (\"TemplateKind\" IN (25, 26) AND \"Authority\" = 7)");
				t.HasCheckConstraint("ck_economy_posting_groups_reserve_authorization", "\"ReserveVersion\" > 0 AND \"ReserveAuthorizationEpoch\" > 0 AND \"RiskDecisionId\" IS NOT NULL");
				t.HasCheckConstraint("ck_economy_posting_groups_source_requirement", "\"TemplateKind\" NOT IN (1, 2, 3, 18, 19, 20) OR \"SourceStampId\" IS NOT NULL");
				t.HasCheckConstraint("ck_economy_posting_groups_template_state", "\"TemplateKind\" BETWEEN 1 AND 26 AND \"TemplateVersion\" = 1 AND \"Status\" = 1");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyProjectionGenerationApprovalRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("ActorId").HasColumnType("uuid");
			b.Property<DateTimeOffset>("ApprovedAt").HasColumnType("timestamp with time zone");
			b.Property<long>("Generation").HasColumnType("bigint");
			b.Property<string>("ReauthenticationHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.HasKey("Id");
			b.HasIndex("Generation", "ActorId").IsUnique();
			b.ToTable("economy_projection_generation_approvals", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyProjectionGenerationRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTimeOffset?>("ActivatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("ApprovedBy").HasColumnType("uuid");
			b.Property<DateTimeOffset?>("CompletedAt").HasColumnType("timestamp with time zone");
			b.Property<long>("FromSequence").HasColumnType("bigint");
			b.Property<long>("Generation").HasColumnType("bigint");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<string>("JournalHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<int>("MismatchCount").HasColumnType("integer");
			b.Property<string>("ProjectionHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<Guid>("ProposedBy").HasColumnType("uuid");
			b.Property<Guid?>("SecondApprovedBy").HasColumnType("uuid");
			b.Property<DateTimeOffset>("StartedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("State").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<long>("ToSequence").HasColumnType("bigint");
			b.HasKey("Id");
			b.HasIndex("Generation").IsUnique();
			b.HasIndex("IsActive").IsUnique().HasDatabaseName("ux_economy_projection_generations_active")
				.HasFilter("\"IsActive\"");
			b.ToTable("economy_projection_generations", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_projection_generations_dual_control", "(\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"ProposedBy\") AND (\"SecondApprovedBy\" IS NULL OR (\"SecondApprovedBy\" <> \"ProposedBy\" AND \"SecondApprovedBy\" <> \"ApprovedBy\"))");
				t.HasCheckConstraint("ck_economy_projection_generations_range", "\"Generation\" > 0 AND \"FromSequence\" >= 0 AND \"ToSequence\" >= \"FromSequence\"");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyProjectionReconciliationEventRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTimeOffset>("DetectedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("PreviousHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("RebuiltHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<long>("SourceJournalSequence").HasColumnType("bigint");
			b.Property<Guid>("WalletId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("WalletId", "DetectedAt").HasDatabaseName("ix_economy_projection_reconciliation_events_wallet_detected");
			b.ToTable("economy_projection_reconciliation_events", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_projection_events_sequence_nonnegative", "\"SourceJournalSequence\" >= 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyProtectedChangeCooldownRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTimeOffset>("AvailableAt").HasColumnType("timestamp with time zone");
			b.Property<DateTimeOffset>("ChangedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Kind").HasColumnType("integer");
			b.Property<Guid>("SubjectId").HasColumnType("uuid");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<string>("ValueHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<long>("Version").HasColumnType("bigint");
			b.HasKey("Id");
			b.HasIndex("TenantId", "SubjectId", "Kind", "Version").IsUnique().HasDatabaseName("ux_economy_protected_change_cooldowns_subject_kind_version");
			b.ToTable("economy_protected_change_cooldowns", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_protected_change_cooldowns_version", "\"Version\" > 0");
				t.HasCheckConstraint("ck_economy_protected_change_cooldowns_window", "\"AvailableAt\" > \"ChangedAt\"");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyProviderDisputeEventRow", delegate(EntityTypeBuilder b)
		{
			b.Property<string>("ProviderEventId").HasMaxLength(256).HasColumnType("character varying(256)");
			b.Property<long>("CumulativeDisputedHardUnits").HasColumnType("bigint");
			b.Property<DateTimeOffset>("OccurredAt").HasColumnType("timestamp with time zone");
			b.Property<string>("ProviderDisputeReference").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<long>("ProviderSequence").HasColumnType("bigint");
			b.Property<string>("RequestHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<Guid>("SourceStampId").HasColumnType("uuid");
			b.Property<int>("Status").HasColumnType("integer");
			b.HasKey("ProviderEventId");
			b.HasIndex("SourceStampId");
			b.HasIndex("ProviderDisputeReference", "ProviderSequence").IsUnique().HasDatabaseName("ux_economy_provider_dispute_events_dispute_sequence");
			b.ToTable("economy_provider_dispute_events", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_provider_dispute_events_amount_positive", "\"CumulativeDisputedHardUnits\" > 0");
				t.HasCheckConstraint("ck_economy_provider_dispute_events_sequence_positive", "\"ProviderSequence\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyProviderDisputeRow", delegate(EntityTypeBuilder b)
		{
			b.Property<string>("ProviderDisputeReference").HasMaxLength(256).HasColumnType("character varying(256)");
			b.Property<long>("BaselineReversedHardUnits").HasColumnType("bigint");
			b.Property<long>("CumulativeDisputedHardUnits").HasColumnType("bigint");
			b.Property<long>("FrozenHardEquivalentUnits").HasColumnType("bigint");
			b.Property<long>("LatestProviderSequence").HasColumnType("bigint");
			b.Property<Guid>("ResponsibleWalletId").HasColumnType("uuid");
			b.Property<string>("ReversalIdempotencyKey").HasMaxLength(128).HasColumnType("character varying(128)");
			b.Property<Guid>("SourceStampId").HasColumnType("uuid");
			b.Property<int>("Status").HasColumnType("integer");
			b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<long>("Version").IsConcurrencyToken().HasColumnType("bigint");
			b.HasKey("ProviderDisputeReference");
			b.HasIndex("ResponsibleWalletId");
			b.HasIndex("SourceStampId").IsUnique().HasDatabaseName("ux_economy_provider_disputes_active_source")
				.HasFilter("\"Status\" = 1");
			b.ToTable("economy_provider_disputes", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_provider_disputes_amount_partition", "\"CumulativeDisputedHardUnits\" > 0 AND \"BaselineReversedHardUnits\" >= 0 AND \"BaselineReversedHardUnits\" <= \"CumulativeDisputedHardUnits\" AND \"FrozenHardEquivalentUnits\" >= 0 AND \"FrozenHardEquivalentUnits\" <= (\"CumulativeDisputedHardUnits\" - \"BaselineReversedHardUnits\")");
				t.HasCheckConstraint("ck_economy_provider_disputes_lifecycle", "(\"Status\" = 1 AND \"ReversalIdempotencyKey\" IS NULL) OR (\"Status\" = 2 AND \"FrozenHardEquivalentUnits\" = 0 AND \"ReversalIdempotencyKey\" IS NULL) OR (\"Status\" = 3 AND \"FrozenHardEquivalentUnits\" = 0 AND \"ReversalIdempotencyKey\" IS NOT NULL)");
				t.HasCheckConstraint("ck_economy_provider_disputes_sequence_positive", "\"LatestProviderSequence\" > 0");
				t.HasCheckConstraint("ck_economy_provider_disputes_version_positive", "\"Version\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyProviderFactAllocationRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<long>("AllocatedUnits").HasColumnType("bigint");
			b.Property<long>("AuthoritativeUnits").HasColumnType("bigint");
			b.Property<string>("ConnectedAccount").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<long>("CumulativeCreditedUnits").HasColumnType("bigint");
			b.Property<int>("Currency").HasColumnType("integer");
			b.Property<string>("Environment").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<Guid>("JournalLineId").HasColumnType("uuid");
			b.Property<string>("Provider").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<string>("ProviderMonetaryLeg").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<string>("ProviderObject").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<Guid>("SourceStampId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("JournalLineId");
			b.HasIndex("SourceStampId");
			b.HasIndex("Provider", "Environment", "ConnectedAccount", "ProviderObject", "ProviderMonetaryLeg").IsUnique().HasDatabaseName("ux_economy_provider_fact_allocations_provider_leg");
			b.ToTable("economy_provider_fact_allocations", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_provider_fact_allocations_cumulative_bounds", "\"AllocatedUnits\" > 0 AND \"CumulativeCreditedUnits\" >= \"AllocatedUnits\" AND \"CumulativeCreditedUnits\" <= \"AuthoritativeUnits\"");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyRegisteredCapabilityRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("AllowedTemplateKinds").IsRequired().HasColumnType("jsonb");
			b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsEnabled").HasColumnType("boolean");
			b.Property<string>("Name").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<DateTimeOffset?>("RevokedAt").HasColumnType("timestamp with time zone");
			b.HasKey("Id");
			b.HasIndex("Name").IsUnique().HasDatabaseName("ux_economy_registered_capabilities_name");
			b.ToTable("economy_registered_capabilities", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_registered_capabilities_state", "(\"IsEnabled\" AND \"RevokedAt\" IS NULL) OR (NOT \"IsEnabled\" AND \"RevokedAt\" IS NOT NULL)");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyReserveAssetAllocationRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("AssetKey").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<long>("EligibleUsdNanos").HasColumnType("bigint");
			b.Property<int>("Purpose").HasColumnType("integer");
			b.Property<long>("ReserveVersion").HasColumnType("bigint");
			b.HasKey("Id");
			b.HasIndex("ReserveVersion", "AssetKey").IsUnique().HasDatabaseName("ux_economy_reserve_asset_allocations_version_asset");
			b.ToTable("economy_reserve_asset_allocations", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_reserve_asset_allocations_value_positive", "\"EligibleUsdNanos\" > 0");
				t.HasCheckConstraint("ck_economy_reserve_asset_allocations_values_valid", "\"Purpose\" IN (1, 2) AND length(btrim(\"AssetKey\")) > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyReserveHeadRow", delegate(EntityTypeBuilder b)
		{
			b.Property<long>("Version").HasColumnType("bigint");
			b.Property<DateTimeOffset>("ActivatedAt").HasColumnType("timestamp with time zone");
			b.Property<long>("AuthorizationEpoch").HasColumnType("bigint");
			b.Property<int>("Coverage").HasColumnType("integer");
			b.Property<string>("EvidenceHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTimeOffset>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<long>("HardBackingUsdNanos").HasColumnType("bigint");
			b.Property<long>("HardFaceValueUsdMinor").HasColumnType("bigint");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<DateTimeOffset>("ObservedAt").HasColumnType("timestamp with time zone");
			b.Property<long>("PolicyVersion").HasColumnType("bigint");
			b.Property<long>("RequiredHardReserveUsdMinor").HasColumnType("bigint");
			b.Property<long>("RequiredSoftReserveUsdNanos").HasColumnType("bigint");
			b.Property<long>("SoftBackingUsdNanos").HasColumnType("bigint");
			b.Property<long>("SoftFaceValueUsdNanos").HasColumnType("bigint");
			b.Property<long>("StressedExpectedRedemptionCostUsdNanos").HasColumnType("bigint");
			b.HasKey("Version");
			b.HasIndex("AuthorizationEpoch").IsUnique().HasDatabaseName("ux_economy_reserve_heads_authorization_epoch");
			b.HasIndex("IsActive").IsUnique().HasDatabaseName("ux_economy_reserve_heads_active")
				.HasFilter("\"IsActive\" = TRUE");
			b.ToTable("economy_reserve_heads", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_reserve_heads_amounts_nonnegative", "\"HardFaceValueUsdMinor\" >= 0 AND \"RequiredHardReserveUsdMinor\" >= 0 AND \"SoftFaceValueUsdNanos\" >= 0 AND \"StressedExpectedRedemptionCostUsdNanos\" >= 0 AND \"RequiredSoftReserveUsdNanos\" >= 0 AND \"HardBackingUsdNanos\" >= 0 AND \"SoftBackingUsdNanos\" >= 0");
				t.HasCheckConstraint("ck_economy_reserve_heads_values_valid", "\"Coverage\" IN (1, 2) AND length(btrim(\"EvidenceHash\")) > 0");
				t.HasCheckConstraint("ck_economy_reserve_heads_versions_positive", "\"Version\" > 0 AND \"PolicyVersion\" > 0 AND \"AuthorizationEpoch\" > 0");
				t.HasCheckConstraint("ck_economy_reserve_heads_window", "\"ExpiresAt\" > \"ObservedAt\" AND \"ActivatedAt\" >= \"ObservedAt\"");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyReserveProposalRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("ApprovalReauthenticationHash").HasMaxLength(128).HasColumnType("character varying(128)");
			b.Property<DateTimeOffset?>("ApprovedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("ApprovedBy").HasColumnType("uuid");
			b.Property<string>("AssetAllocations").IsRequired().HasColumnType("jsonb");
			b.Property<long>("AuthorizationEpoch").HasColumnType("bigint");
			b.Property<int>("Coverage").HasColumnType("integer");
			b.Property<long>("EligibleAssetUsdNanos").HasColumnType("bigint");
			b.Property<string>("EvidenceHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<long?>("ExpectedActiveVersion").HasColumnType("bigint");
			b.Property<DateTimeOffset>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<long>("HardBackingUsdNanos").HasColumnType("bigint");
			b.Property<long>("HardFaceValueUsdMinor").HasColumnType("bigint");
			b.Property<long>("LiabilityUsdNanos").HasColumnType("bigint");
			b.Property<string>("ObservationIds").IsRequired().HasColumnType("jsonb");
			b.Property<DateTimeOffset>("ObservedAt").HasColumnType("timestamp with time zone");
			b.Property<long>("PolicyVersion").HasColumnType("bigint");
			b.Property<DateTimeOffset>("ProposedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("ProposedBy").HasColumnType("uuid");
			b.Property<string>("RequestHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<long>("RequiredHardReserveUsdMinor").HasColumnType("bigint");
			b.Property<long>("RequiredSoftReserveUsdNanos").HasColumnType("bigint");
			b.Property<string>("SnapshotHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<long>("SoftBackingUsdNanos").HasColumnType("bigint");
			b.Property<long>("SoftFaceValueUsdNanos").HasColumnType("bigint");
			b.Property<string>("Status").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<long>("StressedExpectedRedemptionCostUsdNanos").HasColumnType("bigint");
			b.Property<long>("Version").HasColumnType("bigint");
			b.HasKey("Id");
			b.HasIndex("Version").IsUnique().HasDatabaseName("ux_economy_reserve_proposals_version");
			b.ToTable("economy_reserve_proposals", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_reserve_proposals_dual_control", "\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"ProposedBy\"");
				t.HasCheckConstraint("ck_economy_reserve_proposals_values", "\"Version\" > 0 AND \"PolicyVersion\" > 0 AND \"AuthorizationEpoch\" > 0 AND \"LiabilityUsdNanos\" >= 0 AND \"EligibleAssetUsdNanos\" >= 0 AND \"HardFaceValueUsdMinor\" >= 0 AND \"RequiredHardReserveUsdMinor\" >= 0 AND \"SoftFaceValueUsdNanos\" >= 0 AND \"RequiredSoftReserveUsdNanos\" >= 0 AND \"HardBackingUsdNanos\" >= 0 AND \"SoftBackingUsdNanos\" >= 0");
				t.HasCheckConstraint("ck_economy_reserve_proposals_window", "\"ExpiresAt\" > \"ObservedAt\" AND \"ProposedAt\" >= \"ObservedAt\"");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyRiskAuditEvidenceRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("EventKind").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<string>("EvidenceHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("OperationFingerprint").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("Payload").IsRequired().HasColumnType("jsonb");
			b.Property<DateTimeOffset>("RecordedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("RiskDecisionId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("RiskDecisionId", "EvidenceHash").IsUnique().HasDatabaseName("ux_economy_risk_audit_evidence_decision_hash");
			b.ToTable("economy_risk_audit_evidence", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyRiskCounterReservationRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<long>("AmountUnits").HasColumnType("bigint");
			b.Property<DateTimeOffset?>("ConsumedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTimeOffset>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<string>("InputFingerprint").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTimeOffset?>("ReleasedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("ReservationGroupId").HasColumnType("uuid");
			b.Property<DateTimeOffset>("ReservedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("RiskCounterId").HasColumnType("uuid");
			b.Property<Guid>("RiskDecisionId").HasColumnType("uuid");
			b.Property<int>("Status").HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("RiskCounterId");
			b.HasIndex("ReservationGroupId", "RiskCounterId").IsUnique().HasDatabaseName("ux_economy_risk_counter_reservations_group_counter");
			b.HasIndex("RiskDecisionId", "RiskCounterId").IsUnique().HasDatabaseName("ux_economy_risk_counter_reservations_decision_counter");
			b.ToTable("economy_risk_counter_reservations", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_risk_counter_reservations_amount_positive", "\"AmountUnits\" > 0");
				t.HasCheckConstraint("ck_economy_risk_counter_reservations_lifetime", "\"ExpiresAt\" > \"ReservedAt\"");
				t.HasCheckConstraint("ck_economy_risk_counter_reservations_state", "(\"Status\" = 1 AND \"ConsumedAt\" IS NULL AND \"ReleasedAt\" IS NULL) OR (\"Status\" = 2 AND \"ConsumedAt\" >= \"ReservedAt\" AND \"ReleasedAt\" IS NULL) OR (\"Status\" = 3 AND \"ReleasedAt\" >= \"ReservedAt\" AND \"ConsumedAt\" IS NULL) OR (\"Status\" = 4 AND \"ReleasedAt\" >= \"ExpiresAt\" AND \"ConsumedAt\" IS NULL)");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyRiskCounterRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<long>("CounterVersion").HasColumnType("bigint");
			b.Property<int>("Currency").HasColumnType("integer");
			b.Property<int>("Dimension").HasColumnType("integer");
			b.Property<long>("MaxUnits").HasColumnType("bigint");
			b.Property<int>("Operation").HasColumnType("integer");
			b.Property<string>("SubjectHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<long>("UsedUnits").HasColumnType("bigint");
			b.Property<DateTimeOffset>("WindowEndsAt").HasColumnType("timestamp with time zone");
			b.Property<DateTimeOffset>("WindowStartedAt").HasColumnType("timestamp with time zone");
			b.HasKey("Id");
			b.HasIndex("TenantId", "Dimension", "SubjectHash", "Operation", "Currency", "WindowStartedAt").IsUnique().HasDatabaseName("ux_economy_risk_counters_scope_window");
			b.ToTable("economy_risk_counters", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_risk_counters_bounds", "\"CounterVersion\" > 0 AND \"MaxUnits\" > 0 AND \"UsedUnits\" >= 0 AND \"UsedUnits\" <= \"MaxUnits\"");
				t.HasCheckConstraint("ck_economy_risk_counters_window", "\"WindowEndsAt\" > \"WindowStartedAt\"");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyRiskDecisionConsumptionRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTimeOffset>("ConsumedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("OperationFingerprint").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<Guid>("PostingGroupId").HasColumnType("uuid");
			b.Property<Guid>("RiskDecisionId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("PostingGroupId").IsUnique().HasDatabaseName("ux_economy_risk_decision_consumptions_posting");
			b.HasIndex("RiskDecisionId").IsUnique().HasDatabaseName("ux_economy_risk_decision_consumptions_decision");
			b.ToTable("economy_risk_decision_consumptions", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyRiskDecisionRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("ActorHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<long>("AmountUnits").HasColumnType("bigint");
			b.Property<long>("CounterVersion").HasColumnType("bigint");
			b.Property<int>("Currency").HasColumnType("integer");
			b.Property<string>("CurrencyLegs").IsRequired().HasColumnType("jsonb");
			b.Property<Guid>("DestinationWalletId").HasColumnType("uuid");
			b.Property<string>("EntityGraphEvidenceHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<long>("EntityGraphVersion").HasColumnType("bigint");
			b.Property<DateTimeOffset>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<long>("FeatureVersion").HasColumnType("bigint");
			b.Property<string>("IdempotencyKey").HasMaxLength(128).HasColumnType("character varying(128)");
			b.Property<DateTimeOffset>("IssuedAt").HasColumnType("timestamp with time zone");
			b.Property<long>("KillSwitchEpoch").HasColumnType("bigint");
			b.Property<string>("OperationFingerprint").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<int>("Outcome").HasColumnType("integer");
			b.Property<long>("PolicyVersion").HasColumnType("bigint");
			b.Property<string>("ProviderReferenceHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("ReasonCodes").IsRequired().HasColumnType("jsonb");
			b.Property<long>("ReserveAuthorizationEpoch").HasColumnType("bigint");
			b.Property<long>("ReserveVersion").HasColumnType("bigint");
			b.Property<string>("SourceRoots").IsRequired().HasColumnType("jsonb");
			b.Property<Guid>("SourceWalletId").HasColumnType("uuid");
			b.Property<int>("TemplateKind").HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("DestinationWalletId");
			b.HasIndex("OperationFingerprint").IsUnique().HasDatabaseName("ix_economy_risk_decisions_operation_fingerprint");
			b.HasIndex("SourceWalletId");
			b.ToTable("economy_risk_decisions", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_risk_decisions_amount_positive", "\"AmountUnits\" > 0");
				t.HasCheckConstraint("ck_economy_risk_decisions_lifetime", "\"ExpiresAt\" > \"IssuedAt\"");
				t.HasCheckConstraint("ck_economy_risk_decisions_versions_positive", "\"PolicyVersion\" > 0 AND \"ReserveVersion\" > 0 AND \"ReserveAuthorizationEpoch\" > 0 AND \"FeatureVersion\" > 0 AND \"CounterVersion\" > 0 AND \"EntityGraphVersion\" >= 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyRiskReviewCaseRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid?>("AppealOf").HasColumnType("uuid");
			b.Property<int>("RequiredApprovals").HasColumnType("integer");
			b.Property<string>("Resolution").HasColumnType("text");
			b.Property<DateTimeOffset?>("ResolvedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("ResolvedBy").HasColumnType("uuid");
			b.Property<Guid>("RiskDecisionId").HasColumnType("uuid");
			b.Property<int>("Status").HasColumnType("integer");
			b.Property<DateTimeOffset>("SubmittedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("SubmittedBy").HasColumnType("uuid");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("AppealOf");
			b.HasIndex("RiskDecisionId");
			b.HasIndex("TenantId", "RiskDecisionId").IsUnique().HasDatabaseName("ux_economy_risk_review_cases_tenant_decision");
			b.ToTable("economy_risk_review_cases", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_risk_review_cases_approvals", "\"RequiredApprovals\" BETWEEN 1 AND 2");
				t.HasCheckConstraint("ck_economy_risk_review_cases_state", "(\"Status\" = 1 AND \"ResolvedAt\" IS NULL AND \"ResolvedBy\" IS NULL AND \"Resolution\" IS NULL) OR (\"Status\" IN (2, 3) AND \"ResolvedAt\" >= \"SubmittedAt\" AND \"ResolvedBy\" IS NOT NULL AND length(btrim(\"Resolution\")) > 0)");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyRiskReviewEventRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("ActorId").HasColumnType("uuid");
			b.Property<int?>("DecisionCode").HasColumnType("integer");
			b.Property<string>("EvidenceHashes").IsRequired().HasColumnType("jsonb");
			b.Property<int>("Kind").HasColumnType("integer");
			b.Property<DateTimeOffset>("OccurredAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Resolution").HasColumnType("text");
			b.Property<Guid>("RiskReviewCaseId").HasColumnType("uuid");
			b.Property<long>("Sequence").HasColumnType("bigint");
			b.HasKey("Id");
			b.HasIndex("RiskReviewCaseId", "Sequence").IsUnique().HasDatabaseName("ux_economy_risk_review_events_case_sequence");
			b.ToTable("economy_risk_review_events", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_risk_review_events_sequence_positive", "\"Sequence\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyRootReversalStateRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("RootSourceStampId").HasColumnType("uuid");
			b.Property<long>("CumulativeProviderUnits").HasColumnType("bigint");
			b.Property<long>("Epoch").HasColumnType("bigint");
			b.Property<long>("ReversedUnits").HasColumnType("bigint");
			b.Property<string>("State").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<string>("TargetedRanges").IsRequired().HasColumnType("jsonb");
			b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.HasKey("RootSourceStampId");
			b.HasIndex("RootSourceStampId", "Epoch").IsUnique().HasDatabaseName("ux_economy_root_reversal_states_root_epoch");
			b.ToTable("economy_root_reversal_states", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_root_reversal_states_cumulative_bounds", "\"CumulativeProviderUnits\" >= 0 AND \"ReversedUnits\" >= 0 AND \"ReversedUnits\" <= \"CumulativeProviderUnits\"");
				t.HasCheckConstraint("ck_economy_root_reversal_states_epoch_nonnegative", "\"Epoch\" >= 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomySourceStampEventRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("EvidenceHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTimeOffset>("OccurredAt").HasColumnType("timestamp with time zone");
			b.Property<long>("Sequence").HasColumnType("bigint");
			b.Property<Guid>("SourceStampId").HasColumnType("uuid");
			b.Property<int>("State").HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("SourceStampId", "Sequence").IsUnique().HasDatabaseName("ux_economy_source_stamp_events_source_sequence");
			b.ToTable("economy_source_stamp_events", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_source_stamp_events_sequence_positive", "\"Sequence\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomySourceStampRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("ActorId").HasColumnType("uuid");
			b.Property<long>("AuthoritativeUnits").HasColumnType("bigint");
			b.Property<DateTimeOffset?>("ConfirmedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("EvidenceHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("InternalSourceId").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<DateTimeOffset>("ObservedAt").HasColumnType("timestamp with time zone");
			b.Property<long>("PolicyVersion").HasColumnType("bigint");
			b.Property<Guid?>("PostingReferenceId").HasColumnType("uuid");
			b.Property<int>("Provenance").HasColumnType("integer");
			b.Property<string>("Provider").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<string>("ProviderReference").HasMaxLength(256).HasColumnType("character varying(256)");
			b.Property<string>("SourceKind").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<string>("SourceLegId").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<int>("State").HasColumnType("integer");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("Provider", "ProviderReference").IsUnique().HasDatabaseName("ux_economy_source_stamps_provider_reference")
				.HasFilter("\"Provider\" IS NOT NULL AND \"ProviderReference\" IS NOT NULL");
			b.HasIndex("SourceKind", "InternalSourceId", "SourceLegId").IsUnique().HasDatabaseName("ux_economy_source_stamps_internal_leg");
			b.ToTable("economy_source_stamps", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_source_stamps_confirmation", "(\"State\" IN (2, 5, 6) AND \"ConfirmedAt\" IS NOT NULL AND \"ConfirmedAt\" >= \"ObservedAt\") OR (\"State\" IN (1, 3, 4) AND \"ConfirmedAt\" IS NULL)");
				t.HasCheckConstraint("ck_economy_source_stamps_units_nonnegative", "\"AuthoritativeUnits\" >= 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyWalletBalanceProjectionRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("WalletId").HasColumnType("uuid");
			b.Property<long>("AvailableHardToSpend").HasColumnType("bigint");
			b.Property<long>("AvailableSoftToSpend").HasColumnType("bigint");
			b.Property<long>("EarnedHard").HasColumnType("bigint");
			b.Property<long>("HeldHard").HasColumnType("bigint");
			b.Property<long>("HeldSoft").HasColumnType("bigint");
			b.Property<long>("ImmatureEarnedHard").HasColumnType("bigint");
			b.Property<long>("PendingHard").HasColumnType("bigint");
			b.Property<long>("PendingSoft").HasColumnType("bigint");
			b.Property<string>("ProjectionHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<long>("PurchasedHard").HasColumnType("bigint");
			b.Property<DateTimeOffset>("RebuiltAt").HasColumnType("timestamp with time zone");
			b.Property<long>("RestrictedHard").HasColumnType("bigint");
			b.Property<int>("ReviewState").HasColumnType("integer");
			b.Property<long>("Soft").HasColumnType("bigint");
			b.Property<long>("SourceJournalSequence").HasColumnType("bigint");
			b.Property<long>("WithdrawableHard").HasColumnType("bigint");
			b.HasKey("WalletId");
			b.HasIndex("ReviewState").HasDatabaseName("ix_economy_wallet_balance_projections_review_state");
			b.ToTable("economy_wallet_balance_projections", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_wallet_balance_projections_amounts_nonnegative", "\"PendingHard\" >= 0 AND \"PendingSoft\" >= 0 AND \"PurchasedHard\" >= 0 AND \"EarnedHard\" >= 0 AND \"RestrictedHard\" >= 0 AND \"Soft\" >= 0 AND \"ImmatureEarnedHard\" >= 0 AND \"HeldHard\" >= 0 AND \"HeldSoft\" >= 0 AND \"AvailableHardToSpend\" >= 0 AND \"AvailableSoftToSpend\" >= 0 AND \"WithdrawableHard\" >= 0");
				t.HasCheckConstraint("ck_economy_wallet_balance_projections_sequence_nonnegative", "\"SourceJournalSequence\" >= 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyWalletDebtEventRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<long>("DeltaHardUnits").HasColumnType("bigint");
			b.Property<DateTimeOffset>("OccurredAt").HasColumnType("timestamp with time zone");
			b.Property<long>("OutstandingHardUnits").HasColumnType("bigint");
			b.Property<long>("Sequence").HasColumnType("bigint");
			b.Property<Guid>("SourceStampId").HasColumnType("uuid");
			b.Property<Guid>("WalletId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("SourceStampId");
			b.HasIndex("WalletId", "Sequence").IsUnique().HasDatabaseName("ux_economy_wallet_debt_events_wallet_sequence");
			b.ToTable("economy_wallet_debt_events", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_wallet_debt_events_delta_nonzero", "\"DeltaHardUnits\" <> 0 AND \"OutstandingHardUnits\" >= 0");
				t.HasCheckConstraint("ck_economy_wallet_debt_events_sequence_positive", "\"Sequence\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyWalletDebtRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("WalletId").HasColumnType("uuid");
			b.Property<long>("OutstandingHardUnits").HasColumnType("bigint");
			b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<long>("Version").IsConcurrencyToken().HasColumnType("bigint");
			b.HasKey("WalletId");
			b.ToTable("economy_wallet_debts", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_wallet_debts_nonnegative", "\"OutstandingHardUnits\" >= 0");
				t.HasCheckConstraint("ck_economy_wallet_debts_version_positive", "\"Version\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyWalletProjectionGenerationRow", delegate(EntityTypeBuilder b)
		{
			b.Property<long>("Generation").HasColumnType("bigint");
			b.Property<Guid>("WalletId").HasColumnType("uuid");
			b.Property<long>("AvailableHardToSpend").HasColumnType("bigint");
			b.Property<long>("AvailableSoftToSpend").HasColumnType("bigint");
			b.Property<long>("EarnedHard").HasColumnType("bigint");
			b.Property<long>("HeldHard").HasColumnType("bigint");
			b.Property<long>("HeldSoft").HasColumnType("bigint");
			b.Property<long>("ImmatureEarnedHard").HasColumnType("bigint");
			b.Property<bool>("MatchesLive").HasColumnType("boolean");
			b.Property<long>("PendingHard").HasColumnType("bigint");
			b.Property<long>("PendingSoft").HasColumnType("bigint");
			b.Property<string>("ProjectionHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<long>("PurchasedHard").HasColumnType("bigint");
			b.Property<DateTimeOffset>("RebuiltAt").HasColumnType("timestamp with time zone");
			b.Property<long>("RestrictedHard").HasColumnType("bigint");
			b.Property<long>("Soft").HasColumnType("bigint");
			b.Property<long>("SourceJournalSequence").HasColumnType("bigint");
			b.Property<long>("WithdrawableHard").HasColumnType("bigint");
			b.HasKey("Generation", "WalletId");
			b.HasIndex("WalletId");
			b.HasIndex("Generation", "MatchesLive");
			b.ToTable("economy_wallet_projection_generations", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_wallet_projection_generations_amounts", "\"Generation\" > 0 AND \"PendingHard\" >= 0 AND \"PendingSoft\" >= 0 AND \"PurchasedHard\" >= 0 AND \"EarnedHard\" >= 0 AND \"RestrictedHard\" >= 0 AND \"Soft\" >= 0 AND \"ImmatureEarnedHard\" >= 0 AND \"HeldHard\" >= 0 AND \"HeldSoft\" >= 0 AND \"AvailableHardToSpend\" >= 0 AND \"AvailableSoftToSpend\" >= 0 AND \"WithdrawableHard\" >= 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyWalletRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("OwnerId").HasColumnType("uuid");
			b.Property<int>("State").HasColumnType("integer");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("TenantId", "OwnerId").IsUnique();
			b.ToTable("economy_wallets", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyWorkerLeaseRow", delegate(EntityTypeBuilder b)
		{
			b.Property<string>("Name").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<DateTimeOffset>("AcquiredAt").HasColumnType("timestamp with time zone");
			b.Property<DateTimeOffset>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<long>("FencingToken").HasColumnType("bigint");
			b.Property<string>("Owner").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.HasKey("Name");
			b.HasIndex("Name").IsUnique().HasDatabaseName("ux_economy_worker_leases_name");
			b.ToTable("economy_worker_leases", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_worker_leases_fencing", "\"FencingToken\" > 0");
				t.HasCheckConstraint("ck_economy_worker_leases_lifetime", "\"ExpiresAt\" > \"AcquiredAt\"");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.FifoFragmentReservationReceiptRow", delegate(EntityTypeBuilder b)
		{
			b.Property<long>("AmountUnits").HasColumnType("bigint").HasColumnName("amount_units");
			b.Property<long>("EndExclusive").HasColumnType("bigint").HasColumnName("end_exclusive");
			b.Property<Guid>("ParentLotId").HasColumnType("uuid").HasColumnName("parent_lot_id");
			b.Property<Guid>("ReservationId").HasColumnType("uuid").HasColumnName("reservation_id");
			b.Property<long>("ReversalEpoch").HasColumnType("bigint").HasColumnName("reversal_epoch");
			b.Property<Guid>("RootSourceStampId").HasColumnType("uuid").HasColumnName("root_source_stamp_id");
			b.Property<long>("StartInclusive").HasColumnType("bigint").HasColumnName("start_inclusive");
			b.ToTable((string?)null);
			b.ToView((string?)null, (string?)null);
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.HardToSoftConversionRiskDecisionReceiptRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("RiskDecisionId").HasColumnType("uuid").HasColumnName("risk_decision_id");
			b.Property<string>("SourceRoots").IsRequired().HasColumnType("text")
				.HasColumnName("source_roots");
			b.ToTable((string?)null);
			b.ToView((string?)null, (string?)null);
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.MarketplaceFifoReservationReceiptRow", delegate(EntityTypeBuilder b)
		{
			b.Property<long>("AmountUnits").HasColumnType("bigint").HasColumnName("amount_units");
			b.Property<int>("Currency").HasColumnType("integer").HasColumnName("currency");
			b.Property<long>("EndExclusive").HasColumnType("bigint").HasColumnName("end_exclusive");
			b.Property<Guid>("ParentLotId").HasColumnType("uuid").HasColumnName("parent_lot_id");
			b.Property<Guid>("ReservationId").HasColumnType("uuid").HasColumnName("reservation_id");
			b.Property<long>("ReversalEpoch").HasColumnType("bigint").HasColumnName("reversal_epoch");
			b.Property<Guid>("RootSourceStampId").HasColumnType("uuid").HasColumnName("root_source_stamp_id");
			b.Property<long>("StartInclusive").HasColumnType("bigint").HasColumnName("start_inclusive");
			b.ToTable((string?)null);
			b.ToView((string?)null, (string?)null);
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.ProviderReversalReceiptRow", delegate(EntityTypeBuilder b)
		{
			b.Property<bool>("Duplicate").HasColumnType("boolean").HasColumnName("duplicate");
			b.Property<Guid>("OperationId").HasColumnType("uuid").HasColumnName("operation_id");
			b.Property<long>("PlatformLossHardUnits").HasColumnType("bigint").HasColumnName("platform_loss_hard_units");
			b.Property<long>("RecoveredConvertedSoftUnits").HasColumnType("bigint").HasColumnName("recovered_converted_soft_units");
			b.Property<long>("RecoveredHardUnits").HasColumnType("bigint").HasColumnName("recovered_hard_units");
			b.Property<long>("ResponsibleDebtHardUnits").HasColumnType("bigint").HasColumnName("responsible_debt_hard_units");
			b.ToTable((string?)null);
			b.ToView((string?)null, (string?)null);
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.RegisteredPostingReceiptRow", delegate(EntityTypeBuilder b)
		{
			b.Property<bool>("Duplicate").HasColumnType("boolean").HasColumnName("duplicate");
			b.Property<string>("JournalHash").IsRequired().HasColumnType("text")
				.HasColumnName("journal_hash");
			b.Property<long>("JournalSequence").HasColumnType("bigint").HasColumnName("journal_sequence");
			b.Property<Guid>("PostingId").HasColumnType("uuid").HasColumnName("posting_id");
			b.ToTable((string?)null);
			b.ToView((string?)null, (string?)null);
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Treasury.AdminWithdrawalAuditEventRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("RunId").HasColumnType("uuid");
			b.Property<long>("Sequence").HasColumnType("bigint");
			b.Property<Guid?>("ActorId").HasColumnType("uuid");
			b.Property<string>("Evidence").IsRequired().HasColumnType("text");
			b.Property<string>("Hash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("Kind").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<DateTimeOffset>("OccurredAt").HasColumnType("timestamp with time zone");
			b.Property<string>("PreviousHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.HasKey("RunId", "Sequence");
			b.HasIndex("Hash").IsUnique().HasDatabaseName("ux_economy_admin_withdrawal_audit_events_hash");
			b.ToTable("economy_admin_withdrawal_audit_events", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_admin_withdrawal_audit_events_sequence", "\"Sequence\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Treasury.AdminWithdrawalDispatchOutboxRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").HasColumnType("uuid");
			b.Property<int>("AttemptCount").HasColumnType("integer");
			b.Property<DateTimeOffset>("AvailableAt").HasColumnType("timestamp with time zone");
			b.Property<DateTimeOffset?>("CompletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("IdempotencyKey").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<string>("LastErrorCode").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<DateTimeOffset?>("LeaseExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<string>("LeaseOwner").HasMaxLength(200).HasColumnType("character varying(200)");
			b.Property<string>("Payload").IsRequired().HasColumnType("jsonb");
			b.Property<string>("PayloadHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<Guid>("RunId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("RunId").IsUnique();
			b.HasIndex("CompletedAt", "AvailableAt", "LeaseExpiresAt");
			b.ToTable("economy_admin_withdrawal_dispatch_outbox", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_admin_withdrawal_dispatch_outbox_attempts", "\"AttemptCount\" >= 0");
			});
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Treasury.AdminWithdrawalProviderEventRow", delegate(EntityTypeBuilder b)
		{
			b.Property<string>("EventId").HasMaxLength(256).HasColumnType("character varying(256)");
			b.Property<string>("EventHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTimeOffset>("RecordedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("RunId").HasColumnType("uuid");
			b.HasKey("EventId");
			b.HasIndex("RunId", "RecordedAt").HasDatabaseName("ix_economy_admin_withdrawal_provider_events_run_recorded");
			b.ToTable("economy_admin_withdrawal_provider_events", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Treasury.AdminWithdrawalRunRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").HasColumnType("uuid");
			b.Property<long>("AmountUnits").HasColumnType("bigint");
			b.Property<Guid?>("ApprovedBy").HasColumnType("uuid");
			b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("DestinationHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("DispatchSnapshotHash").HasMaxLength(128).HasColumnType("character varying(128)");
			b.Property<long>("ExecutionEpoch").HasColumnType("bigint");
			b.Property<long>("FencingToken").HasColumnType("bigint");
			b.Property<string>("IdempotencyKey").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<DateOnly>("PeriodStart").HasColumnType("date");
			b.Property<Guid>("PlatformFeeWalletId").HasColumnType("uuid");
			b.Property<long>("PolicyVersion").HasColumnType("bigint");
			b.Property<string>("ProviderTransferId").HasMaxLength(256).HasColumnType("character varying(256)");
			b.Property<string>("RequestHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<Guid>("RequestedBy").HasColumnType("uuid");
			b.Property<long>("ReserveAuthorizationEpoch").HasColumnType("bigint");
			b.Property<long>("ReserveVersion").HasColumnType("bigint");
			b.Property<string>("SourceAssetKey").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<int>("State").HasColumnType("integer");
			b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<long>("Version").IsConcurrencyToken().HasColumnType("bigint");
			b.HasKey("Id");
			b.HasIndex("IdempotencyKey").IsUnique().HasDatabaseName("ux_economy_admin_withdrawal_runs_idempotency");
			b.HasIndex("PeriodStart").IsUnique().HasDatabaseName("ux_economy_admin_withdrawal_runs_active_period")
				.HasFilter("\"State\" NOT IN (6, 7)");
			b.HasIndex("State", "UpdatedAt").HasDatabaseName("ix_economy_admin_withdrawal_runs_state_updated");
			b.ToTable("economy_admin_withdrawal_runs", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_economy_admin_withdrawal_runs_amount_positive", "\"AmountUnits\" > 0");
				t.HasCheckConstraint("ck_economy_admin_withdrawal_runs_approval", "(\"State\" = 1 AND \"ApprovedBy\" IS NULL) OR (\"State\" BETWEEN 2 AND 7 AND \"ApprovedBy\" IS NOT NULL)");
				t.HasCheckConstraint("ck_economy_admin_withdrawal_runs_dispatch_snapshot", "(\"State\" IN (1, 2) AND \"DispatchSnapshotHash\" IS NULL) OR (\"State\" BETWEEN 3 AND 7 AND \"DispatchSnapshotHash\" IS NOT NULL)");
				t.HasCheckConstraint("ck_economy_admin_withdrawal_runs_positive_versions", "\"Version\" > 0 AND \"FencingToken\" > 0 AND \"ExecutionEpoch\" > 0 AND \"ReserveVersion\" > 0 AND \"ReserveAuthorizationEpoch\" > 0 AND \"PolicyVersion\" > 0");
				t.HasCheckConstraint("ck_economy_admin_withdrawal_runs_state", "\"State\" BETWEEN 1 AND 7");
				t.HasCheckConstraint("ck_economy_admin_withdrawal_runs_timestamps", "\"UpdatedAt\" >= \"CreatedAt\"");
			});
		});
		modelBuilder.Entity("GameGuild.Features.CapabilityAuditLog", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("CapabilityKey").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<string>("ChangeReason").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<int>("ChangeType").HasColumnType("integer");
			b.Property<DateTimeOffset>("ChangedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("ChangedByUserId").HasColumnType("uuid");
			b.Property<string>("CorrelationId").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("IpAddress").HasMaxLength(45).HasColumnType("character varying(45)");
			b.Property<string>("NewSource").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<bool>("NewValue").HasColumnType("boolean");
			b.Property<string>("OldSource").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<bool?>("OldValue").HasColumnType("boolean");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("UserAgent").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("CapabilityKey").HasDatabaseName("ix_capability_audit_logs_capability");
			b.HasIndex("ChangedByUserId").HasDatabaseName("ix_capability_audit_logs_user").HasFilter("\"ChangedByUserId\" IS NOT NULL");
			b.HasIndex("TenantId", "ChangedAt").HasDatabaseName("ix_capability_audit_logs_tenant_changed");
			b.ToTable("capability_audit_logs", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Features.FeatureFlag", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("DefaultValue").HasMaxLength(1000).HasColumnType("character varying(1000)")
				.HasColumnName("default_value");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").IsRequired().HasMaxLength(500)
				.HasColumnType("character varying(500)")
				.HasColumnName("description");
			b.Property<string>("EnabledValue").HasMaxLength(1000).HasColumnType("character varying(1000)")
				.HasColumnName("enabled_value");
			b.Property<string>("Environment").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)")
				.HasColumnName("environment");
			b.Property<string>("EscalationContact").HasMaxLength(500).HasColumnType("character varying(500)")
				.HasColumnName("escalation_contact");
			b.Property<DateTimeOffset?>("ExpiresAt").HasColumnType("timestamp with time zone").HasColumnName("expires_at");
			b.Property<string>("GovernanceNotes").HasMaxLength(2000).HasColumnType("character varying(2000)")
				.HasColumnName("governance_notes");
			b.Property<bool>("IsEnabled").HasColumnType("boolean").HasColumnName("is_enabled");
			b.Property<bool>("IsGlobal").HasColumnType("boolean").HasColumnName("is_global");
			b.Property<bool>("IsKillSwitch").HasColumnType("boolean").HasColumnName("is_kill_switch");
			b.Property<string>("Key").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)")
				.HasColumnName("key");
			b.Property<string>("Name").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)")
				.HasColumnName("name");
			b.Property<string>("Owner").HasMaxLength(200).HasColumnType("character varying(200)")
				.HasColumnName("owner");
			b.Property<bool>("RequiresEncryption").HasColumnType("boolean").HasColumnName("requires_encryption");
			b.Property<DateTimeOffset?>("ReviewDate").HasColumnType("timestamp with time zone").HasColumnName("review_date");
			b.Property<int>("RolloutPercentage").HasColumnType("integer").HasColumnName("rollout_percentage");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Type").IsRequired().HasColumnType("text")
				.HasColumnName("type");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("Environment").HasDatabaseName("idx_feature_flags_environment");
			b.HasIndex("ExpiresAt").HasDatabaseName("idx_feature_flags_expires_at");
			b.HasIndex("IsEnabled").HasDatabaseName("idx_feature_flags_is_enabled");
			b.HasIndex("Key").IsUnique().HasDatabaseName("idx_feature_flags_key");
			b.HasIndex("ReviewDate").HasDatabaseName("idx_feature_flags_review_date");
			b.HasIndex("TenantId").HasDatabaseName("idx_feature_flags_tenant_id");
			b.HasIndex("Type").HasDatabaseName("idx_feature_flags_type");
			b.HasIndex("Key", "Environment").HasDatabaseName("idx_feature_flags_key_environment");
			b.ToTable("feature_flags", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Features.FeatureFlagDependencyLink", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("DependencyType").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)")
				.HasColumnName("dependency_type");
			b.Property<Guid>("DependsOnFeatureFlagId").HasColumnType("uuid").HasColumnName("depends_on_feature_flag_id");
			b.Property<Guid>("FeatureFlagId").HasColumnType("uuid").HasColumnName("feature_flag_id");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("DependsOnFeatureFlagId").HasDatabaseName("idx_feature_flag_dependencies_depends_on_feature_flag_id");
			b.HasIndex("FeatureFlagId").HasDatabaseName("idx_feature_flag_dependencies_feature_flag_id");
			b.HasIndex("FeatureFlagId", "DependsOnFeatureFlagId", "DependencyType").IsUnique().HasDatabaseName("idx_feature_flag_dependencies_unique_edge");
			b.ToTable("feature_flag_dependencies", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Features.FeatureFlagTarget", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("CustomValue").HasMaxLength(1000).HasColumnType("character varying(1000)")
				.HasColumnName("custom_value");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("DependsOn").HasMaxLength(255).HasColumnType("character varying(255)")
				.HasColumnName("depends_on");
			b.Property<Guid>("FeatureFlagId").HasColumnType("uuid").HasColumnName("feature_flag_id");
			b.Property<bool>("IsEnabled").HasColumnType("boolean").HasColumnName("is_enabled");
			b.Property<string>("Metadata").HasMaxLength(2000).HasColumnType("character varying(2000)")
				.HasColumnName("metadata");
			b.Property<int>("Priority").HasColumnType("integer").HasColumnName("priority");
			b.Property<int>("RolloutPercentage").HasColumnType("integer").HasColumnName("rollout_percentage");
			b.Property<string>("TargetIdentifier").IsRequired().HasMaxLength(255)
				.HasColumnType("character varying(255)")
				.HasColumnName("target_identifier");
			b.Property<string>("TargetType").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)")
				.HasColumnName("target_type");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("FeatureFlagId").HasDatabaseName("idx_feature_flag_targets_feature_flag_id");
			b.HasIndex("Priority").HasDatabaseName("idx_feature_flag_targets_priority");
			b.HasIndex("TargetIdentifier").HasDatabaseName("idx_feature_flag_targets_target_identifier");
			b.HasIndex("TargetType").HasDatabaseName("idx_feature_flag_targets_target_type");
			b.HasIndex("TenantId").HasDatabaseName("idx_feature_flag_targets_tenant_id");
			b.HasIndex("FeatureFlagId", "TargetType", "TargetIdentifier").IsUnique().HasDatabaseName("idx_feature_flag_targets_unique");
			b.ToTable("feature_flag_targets", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Features.FeatureFlagUsage", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<long>("AccessCount").HasColumnType("bigint").HasColumnName("access_count");
			b.Property<string>("ContextData").HasMaxLength(2000).HasColumnType("character varying(2000)")
				.HasColumnName("context_data");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Environment").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)")
				.HasColumnName("environment");
			b.Property<Guid>("FeatureFlagId").HasColumnType("uuid").HasColumnName("feature_flag_id");
			b.Property<DateTime>("FirstAccessAt").HasColumnType("timestamp with time zone").HasColumnName("first_access_at");
			b.Property<DateTime>("LastAccessAt").HasColumnType("timestamp with time zone").HasColumnName("last_access_at");
			b.Property<string>("ReturnedValue").HasMaxLength(1000).HasColumnType("character varying(1000)")
				.HasColumnName("returned_value");
			b.Property<Guid?>("TenantId").HasColumnType("uuid").HasColumnName("tenant_id");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("UserId").HasColumnType("uuid").HasColumnName("user_id");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<bool>("WasEnabled").HasColumnType("boolean").HasColumnName("was_enabled");
			b.HasKey("Id");
			b.HasIndex("CreatedAt").HasDatabaseName("idx_feature_flag_usage_created_at");
			b.HasIndex("Environment").HasDatabaseName("idx_feature_flag_usage_environment");
			b.HasIndex("FeatureFlagId").HasDatabaseName("idx_feature_flag_usage_feature_flag_id");
			b.HasIndex("LastAccessAt").HasDatabaseName("idx_feature_flag_usage_last_access_at");
			b.HasIndex("TenantId").HasDatabaseName("idx_feature_flag_usage_tenant_id");
			b.HasIndex("UserId").HasDatabaseName("idx_feature_flag_usage_user_id");
			b.HasIndex("FeatureFlagId", "TenantId", "Environment").HasDatabaseName("idx_feature_flag_usage_composite");
			b.ToTable("feature_flag_usage", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Features.TenantCapability", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("CapabilityKey").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTimeOffset?>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsEnabled").ValueGeneratedOnAdd().HasColumnType("boolean")
				.HasDefaultValue(false);
			b.Property<string>("Metadata").HasMaxLength(4000).HasColumnType("character varying(4000)");
			b.Property<string>("ModificationReason").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<Guid?>("ModifiedByUserId").HasColumnType("uuid");
			b.Property<int>("Priority").ValueGeneratedOnAdd().HasColumnType("integer")
				.HasDefaultValue(0);
			b.Property<string>("Source").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ExpiresAt").HasDatabaseName("ix_tenant_capabilities_expires_at").HasFilter("\"ExpiresAt\" IS NOT NULL");
			b.HasIndex("TenantId").HasDatabaseName("ix_tenant_capabilities_tenant_id");
			b.HasIndex("TenantId", "CapabilityKey").IsUnique().HasDatabaseName("ix_tenant_capabilities_tenant_capability");
			b.ToTable("tenant_capabilities", (string?)null);
		});
		modelBuilder.Entity("GameGuild.GameJams.Jam", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("CreatedBy").HasColumnType("uuid");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasColumnType("text");
			b.Property<DateTime>("EndDate").HasColumnType("timestamp with time zone");
			b.Property<int?>("MaxParticipants").HasColumnType("integer");
			b.Property<string>("Name").IsRequired().HasMaxLength(255)
				.HasColumnType("character varying(255)");
			b.Property<int>("ParticipantCount").HasColumnType("integer");
			b.Property<string>("Rules").HasColumnType("text");
			b.Property<string>("Slug").IsRequired().HasMaxLength(255)
				.HasColumnType("character varying(255)");
			b.Property<DateTime>("StartDate").HasColumnType("timestamp with time zone");
			b.Property<string>("Status").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<string>("SubmissionCriteria").HasColumnType("text");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Theme").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<DateTime?>("VotingEndDate").HasColumnType("timestamp with time zone");
			b.HasKey("Id");
			b.HasIndex("Slug").IsUnique();
			b.HasIndex("Status");
			b.ToTable("game_jams", (string?)null);
		});
		modelBuilder.Entity("GameGuild.GameJams.JamJudgingCriteria", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasColumnType("text");
			b.Property<Guid>("JamId").HasColumnType("uuid");
			b.Property<int>("MaxScore").HasColumnType("integer");
			b.Property<string>("Name").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<decimal>("Weight").HasPrecision(8, 2).HasColumnType("numeric(8,2)");
			b.HasKey("Id");
			b.HasIndex("JamId");
			b.ToTable("game_jam_judging_criteria", (string?)null);
		});
		modelBuilder.Entity("GameGuild.GameJams.JamScore", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("CriteriaId").HasColumnType("uuid");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Feedback").HasColumnType("text");
			b.Property<Guid>("JudgeUserId").HasColumnType("uuid");
			b.Property<Guid?>("ProjectJamSubmissionId").HasColumnType("uuid");
			b.Property<int>("Score").HasColumnType("integer");
			b.Property<Guid>("SubmissionId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ProjectJamSubmissionId");
			b.HasIndex("SubmissionId", "CriteriaId", "JudgeUserId").IsUnique();
			b.ToTable("game_jam_scores", (string?)null);
		});
		modelBuilder.Entity("GameGuild.GameJams.JamSubmission", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("JamId").HasColumnType("uuid");
			b.Property<Guid>("ProjectVersionId").HasColumnType("uuid");
			b.Property<string>("SubmissionNotes").HasColumnType("text");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ProjectVersionId");
			b.HasIndex("JamId", "UserId").IsUnique();
			b.ToTable("game_jam_submissions", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Identity.Authentication.ApiKey", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid")
				.HasColumnName("id");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone").HasColumnName("created_at");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone").HasColumnName("deleted_at");
			b.Property<DateTime?>("ExpiresAt").HasColumnType("timestamp with time zone").HasColumnName("expires_at");
			b.Property<string>("IpWhitelist").HasMaxLength(100).HasColumnType("character varying(100)")
				.HasColumnName("ip_whitelist");
			b.Property<bool>("IsActive").ValueGeneratedOnAdd().HasColumnType("boolean")
				.HasDefaultValue(true)
				.HasColumnName("is_active");
			b.Property<string>("KeyHash").IsRequired().HasMaxLength(64)
				.HasColumnType("character varying(64)")
				.HasColumnName("key_hash");
			b.Property<string>("KeyPrefix").IsRequired().HasMaxLength(20)
				.HasColumnType("character varying(20)")
				.HasColumnName("key_prefix");
			b.Property<DateTime?>("LastUsedAt").HasColumnType("timestamp with time zone").HasColumnName("last_used_at");
			b.Property<string>("Name").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)")
				.HasColumnName("name");
			b.Property<string>("RevocationReason").HasMaxLength(200).HasColumnType("character varying(200)")
				.HasColumnName("revocation_reason");
			b.Property<DateTime?>("RevokedAt").HasColumnType("timestamp with time zone").HasColumnName("revoked_at");
			b.Property<string>("Scopes").IsRequired().HasMaxLength(1000)
				.HasColumnType("character varying(1000)")
				.HasColumnName("scopes");
			b.Property<Guid?>("TenantId").HasColumnType("uuid").HasColumnName("tenant_id");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone").HasColumnName("updated_at");
			b.Property<long>("UsageCount").ValueGeneratedOnAdd().HasColumnType("bigint")
				.HasDefaultValue(0L)
				.HasColumnName("usage_count");
			b.Property<Guid>("UserId").HasColumnType("uuid").HasColumnName("user_id");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer")
				.HasColumnName("version");
			b.HasKey("Id");
			b.HasIndex("ExpiresAt").HasDatabaseName("ix_api_keys_expires_at");
			b.HasIndex("IsActive").HasDatabaseName("ix_api_keys_is_active");
			b.HasIndex("KeyHash").IsUnique().HasDatabaseName("ix_api_keys_key_hash");
			b.HasIndex("TenantId").HasDatabaseName("ix_api_keys_tenant_id");
			b.HasIndex("UserId").HasDatabaseName("ix_api_keys_user_id");
			b.ToTable("api_keys", "gameguild.authentication");
		});
		modelBuilder.Entity("GameGuild.Identity.Authentication.AuthenticationAttempt", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid")
				.HasColumnName("id");
			b.Property<DateTime>("AttemptedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("CorrelationId").HasMaxLength(64).HasColumnType("character varying(64)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("DeviceFingerprint").HasMaxLength(64).HasColumnType("character varying(64)");
			b.Property<string>("Email").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<string>("FailureReason").HasMaxLength(50).HasColumnType("character varying(50)");
			b.Property<string>("IpAddress").IsRequired().HasMaxLength(45)
				.HasColumnType("character varying(45)");
			b.Property<bool>("IsSuccessful").HasColumnType("boolean");
			b.Property<bool>("IsSuspicious").HasColumnType("boolean");
			b.Property<string>("Location").HasMaxLength(200).HasColumnType("character varying(200)");
			b.Property<string>("Metadata").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<TimeSpan>("ProcessingTime").HasColumnType("interval");
			b.Property<int>("RiskScore").HasColumnType("integer");
			b.Property<Guid?>("SessionId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("UserAgent").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<Guid?>("UserId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("AttemptedAt").HasDatabaseName("ix_authenticationattempt_attempted_at");
			b.HasIndex("Email").HasDatabaseName("ix_authenticationattempt_email");
			b.HasIndex("IpAddress").HasDatabaseName("ix_authenticationattempt_ip_address");
			b.HasIndex("TenantId").HasDatabaseName("ix_authenticationattempt_tenant_id");
			b.HasIndex("UserId").HasDatabaseName("ix_authenticationattempt_user_id");
			b.ToTable("authenticationattempt", "gameguild.authentication");
		});
		modelBuilder.Entity("GameGuild.Identity.Authentication.BlockchainCertificateAnchor", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid")
				.HasColumnName("id");
			b.Property<DateTime>("AnchoredAt").HasColumnType("timestamp with time zone");
			b.Property<long?>("BlockNumber").HasColumnType("bigint");
			b.Property<string>("BlockchainNetwork").IsRequired().HasMaxLength(64)
				.HasColumnType("character varying(64)");
			b.Property<string>("CertificateData").IsRequired().HasMaxLength(4000)
				.HasColumnType("character varying(4000)");
			b.Property<string>("CertificateHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("CertificateType").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTime?>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsRevoked").HasColumnType("boolean");
			b.Property<string>("Metadata").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<string>("RevocationReason").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<string>("RevocationTransactionHash").HasMaxLength(128).HasColumnType("character varying(128)");
			b.Property<DateTime?>("RevokedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("TransactionHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("CertificateHash").IsUnique().HasDatabaseName("ix_blockchaincertificateanchor_certificate_hash");
			b.HasIndex("TransactionHash").HasDatabaseName("ix_blockchaincertificateanchor_transaction_hash");
			b.HasIndex("UserId").HasDatabaseName("ix_blockchaincertificateanchor_user_id");
			b.ToTable("blockchaincertificateanchor", "gameguild.authentication");
		});
		modelBuilder.Entity("GameGuild.Identity.Authentication.ContentTypePermission", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid")
				.HasColumnName("id");
			b.Property<string>("ContentTypeName").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<DateTime?>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("GrantedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("GrantedBy").HasColumnType("uuid");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<string>("Notes").HasColumnType("text");
			b.Property<string>("Permissions").IsRequired().HasMaxLength(500)
				.HasColumnType("character varying(500)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("TenantId").HasDatabaseName("ix_contenttypepermission_tenant_id");
			b.HasIndex("UserId").HasDatabaseName("ix_contenttypepermission_user_id");
			b.HasIndex("TenantId", "ContentTypeName").HasDatabaseName("ix_contenttypepermission_tenant_contenttype");
			b.ToTable("contenttypepermission", "gameguild.authentication");
		});
		modelBuilder.Entity("GameGuild.Identity.Authentication.ExternalLogin", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid")
				.HasColumnName("id");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Provider").IsRequired().HasMaxLength(64)
				.HasColumnType("character varying(64)");
			b.Property<string>("ProviderKey").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("UserId").HasDatabaseName("ix_externallogin_user_id");
			b.HasIndex("Provider", "ProviderKey").IsUnique().HasDatabaseName("ix_externallogin_provider_provider_key");
			b.ToTable("externallogin", "gameguild.authentication");
		});
		modelBuilder.Entity("GameGuild.Identity.Authentication.IdentityVerification", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid")
				.HasColumnName("id");
			b.Property<DateTime?>("CompletedAt").HasColumnType("timestamp with time zone");
			b.Property<double?>("ConfidenceScore").HasColumnType("double precision");
			b.Property<string>("DocumentIds").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<DateTime?>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<string>("ExternalVerificationId").HasMaxLength(256).HasColumnType("character varying(256)");
			b.Property<DateTime>("InitiatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Metadata").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<string>("Notes").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<DateTime?>("ReviewedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("ReviewedBy").HasColumnType("uuid");
			b.Property<string>("Status").IsRequired().HasMaxLength(64)
				.HasColumnType("character varying(64)");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<string>("VerificationProvider").HasMaxLength(256).HasColumnType("character varying(256)");
			b.Property<string>("VerificationType").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("VerifiedValue").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.HasKey("Id");
			b.HasIndex("Status").HasDatabaseName("ix_identityverification_status");
			b.HasIndex("UserId").HasDatabaseName("ix_identityverification_user_id");
			b.HasIndex("UserId", "VerificationType").HasDatabaseName("ix_identityverification_user_type");
			b.ToTable("identityverification", "gameguild.authentication");
		});
		modelBuilder.Entity("GameGuild.Identity.Authentication.MfaAttempt", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid")
				.HasColumnName("id");
			b.Property<DateTime>("AttemptedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("DeviceFingerprint").HasMaxLength(256).HasColumnType("character varying(256)");
			b.Property<string>("FailureReason").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<string>("IpAddress").IsRequired().HasMaxLength(45)
				.HasColumnType("character varying(45)");
			b.Property<bool>("IsSuccessful").HasColumnType("boolean");
			b.Property<string>("Metadata").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<string>("Method").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<int>("ProcessingTimeMs").HasColumnType("integer");
			b.Property<Guid?>("SessionId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("UserAgent").IsRequired().HasMaxLength(500)
				.HasColumnType("character varying(500)");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("AttemptedAt").HasDatabaseName("ix_mfaattempt_attempted_at");
			b.HasIndex("TenantId").HasDatabaseName("ix_mfaattempt_tenant_id");
			b.HasIndex("UserId").HasDatabaseName("ix_mfaattempt_user_id");
			b.ToTable("mfaattempt", "gameguild.authentication");
		});
		modelBuilder.Entity("GameGuild.Identity.Authentication.RefreshToken", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid")
				.HasColumnName("id");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("CreatedByIp").IsRequired().HasMaxLength(45)
				.HasColumnType("character varying(45)");
			b.Property<DateTime>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsRevoked").HasColumnType("boolean");
			b.Property<string>("ReplacedByToken").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<DateTime?>("RevokedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("RevokedByIp").HasMaxLength(45).HasColumnType("character varying(45)");
			b.Property<string>("Token").IsRequired().HasMaxLength(500)
				.HasColumnType("character varying(500)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("ExpiresAt").HasDatabaseName("ix_refreshtoken_expires_at");
			b.HasIndex("Token").IsUnique().HasDatabaseName("ix_refreshtoken_token");
			b.HasIndex("UserId").HasDatabaseName("ix_refreshtoken_user_id");
			b.ToTable("refreshtoken", "gameguild.authentication");
		});
		modelBuilder.Entity("GameGuild.Identity.Authentication.Role", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid")
				.HasColumnName("id");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone").HasColumnName("created_at");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").IsRequired().HasMaxLength(500)
				.HasColumnType("character varying(500)")
				.HasColumnName("description");
			b.Property<bool>("IsActive").ValueGeneratedOnAdd().HasColumnType("boolean")
				.HasDefaultValue(true)
				.HasColumnName("is_active");
			b.Property<string>("Name").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)")
				.HasColumnName("name");
			b.Property<string>("Permissions").IsRequired().HasMaxLength(4000)
				.HasColumnType("jsonb")
				.HasColumnName("permissions");
			b.Property<Guid?>("TenantId").HasColumnType("uuid").HasColumnName("tenant_id");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone").HasColumnName("updated_at");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("IsActive").HasDatabaseName("idx_role_is_active");
			b.HasIndex("Name").HasDatabaseName("idx_role_name");
			b.HasIndex("TenantId").HasDatabaseName("idx_role_tenant_id");
			b.HasIndex("Name", "TenantId").IsUnique().HasDatabaseName("idx_role_name_tenant_id");
			b.ToTable("role", "gameguild.authentication");
		});
		modelBuilder.Entity("GameGuild.Identity.Authentication.ServiceAccount", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid")
				.HasColumnName("id");
			b.Property<string>("AllowedIpAddresses").HasMaxLength(2000).HasColumnType("character varying(2000)")
				.HasColumnName("allowed_ip_addresses");
			b.Property<long>("AuthenticationCount").ValueGeneratedOnAdd().HasColumnType("bigint")
				.HasDefaultValue(0L)
				.HasColumnName("authentication_count");
			b.Property<string>("ClientId").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)")
				.HasColumnName("client_id");
			b.Property<string>("ClientSecretHash").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)")
				.HasColumnName("client_secret_hash");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone").HasColumnName("created_at");
			b.Property<string>("CreatedBy").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)")
				.HasColumnName("created_by");
			b.Property<string>("Description").HasMaxLength(1000).HasColumnType("character varying(1000)")
				.HasColumnName("description");
			b.Property<DateTime?>("ExpiresAt").HasColumnType("timestamp with time zone").HasColumnName("expires_at");
			b.Property<int>("FailedAuthenticationAttempts").ValueGeneratedOnAdd().HasColumnType("integer")
				.HasDefaultValue(0)
				.HasColumnName("failed_authentication_attempts");
			b.Property<bool>("IsActive").ValueGeneratedOnAdd().HasColumnType("boolean")
				.HasDefaultValue(true)
				.HasColumnName("is_active");
			b.Property<bool>("IsLocked").ValueGeneratedOnAdd().HasColumnType("boolean")
				.HasDefaultValue(false)
				.HasColumnName("is_locked");
			b.Property<DateTime?>("LastAuthenticatedAt").HasColumnType("timestamp with time zone").HasColumnName("last_authenticated_at");
			b.Property<string>("LastAuthenticatedFromIp").HasMaxLength(45).HasColumnType("character varying(45)")
				.HasColumnName("last_authenticated_from_ip");
			b.Property<DateTime?>("LockedAt").HasColumnType("timestamp with time zone").HasColumnName("locked_at");
			b.Property<string>("Name").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)")
				.HasColumnName("name");
			b.Property<string>("Scopes").IsRequired().HasMaxLength(2000)
				.HasColumnType("character varying(2000)")
				.HasColumnName("scopes");
			b.Property<DateTime?>("SecretRotatedAt").HasColumnType("timestamp with time zone").HasColumnName("secret_rotated_at");
			b.Property<int>("SecretRotationCount").ValueGeneratedOnAdd().HasColumnType("integer")
				.HasDefaultValue(0)
				.HasColumnName("secret_rotation_count");
			b.Property<Guid?>("TenantId").HasColumnType("uuid").HasColumnName("tenant_id");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone").HasColumnName("updated_at");
			b.HasKey("Id");
			b.HasIndex("ClientId").IsUnique().HasDatabaseName("idx_service_accounts_client_id");
			b.HasIndex("TenantId").HasDatabaseName("idx_service_accounts_tenant_id");
			b.HasIndex("TenantId", "IsActive").HasDatabaseName("idx_service_accounts_tenant_active");
			b.ToTable("service_accounts", "gameguild.authentication");
		});
		modelBuilder.Entity("GameGuild.Identity.Authentication.TrustedDevice", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid")
				.HasColumnName("id");
			b.Property<string>("AssociatedIpAddresses").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("DeviceFingerprint").IsRequired().HasMaxLength(64)
				.HasColumnType("character varying(64)");
			b.Property<string>("DeviceInfo").IsRequired().HasMaxLength(2000)
				.HasColumnType("character varying(2000)");
			b.Property<string>("DeviceName").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<DateTime?>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<DateTime>("LastUsedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("TrustedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("UserId").HasDatabaseName("ix_trusteddevice_user_id");
			b.HasIndex("UserId", "DeviceFingerprint").IsUnique().HasDatabaseName("ix_trusteddevice_user_fingerprint");
			b.ToTable("trusteddevice", "gameguild.authentication");
		});
		modelBuilder.Entity("GameGuild.Identity.Authentication.UserMfaConfiguration", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid")
				.HasColumnName("id");
			b.Property<string>("BackupCodes").HasMaxLength(2000).HasColumnType("character varying(2000)")
				.HasColumnName("backup_codes");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone").HasColumnName("created_at");
			b.Property<DateTime?>("EnabledAt").HasColumnType("timestamp with time zone").HasColumnName("enabled_at");
			b.Property<int>("FailedAttempts").HasColumnType("integer").HasColumnName("failed_attempts");
			b.Property<bool>("IsEnabled").HasColumnType("boolean").HasColumnName("is_enabled");
			b.Property<bool>("IsSetupComplete").HasColumnType("boolean").HasColumnName("is_setup_complete");
			b.Property<DateTime?>("LastUsedAt").HasColumnType("timestamp with time zone").HasColumnName("last_used_at");
			b.Property<DateTime?>("LockedOutUntil").HasColumnType("timestamp with time zone").HasColumnName("locked_out_until");
			b.Property<string>("PreferredMethod").IsRequired().HasColumnType("text")
				.HasColumnName("preferred_method");
			b.Property<string>("QrCodeSetupData").HasMaxLength(1000).HasColumnType("character varying(1000)")
				.HasColumnName("qr_code_setup_data");
			b.Property<string>("TotpSecretKey").HasMaxLength(500).HasColumnType("character varying(500)")
				.HasColumnName("totp_secret_key");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone").HasColumnName("updated_at");
			b.Property<Guid>("UserId").HasColumnType("uuid").HasColumnName("user_id");
			b.HasKey("Id");
			b.HasIndex("UserId").HasDatabaseName("ix_user_mfa_configuration_user_id");
			b.ToTable("user_mfa_configuration", "gameguild.authentication");
		});
		modelBuilder.Entity("GameGuild.Identity.Authentication.UserRole", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid")
				.HasColumnName("id");
			b.Property<DateTime>("AssignedAt").HasColumnType("timestamp with time zone").HasColumnName("assigned_at");
			b.Property<Guid?>("AssignedBy").HasColumnType("uuid").HasColumnName("assigned_by");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone").HasColumnName("created_at");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("ExpiresAt").HasColumnType("timestamp with time zone").HasColumnName("expires_at");
			b.Property<Guid>("RoleId").HasColumnType("uuid").HasColumnName("role_id");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone").HasColumnName("updated_at");
			b.Property<Guid>("UserId").HasColumnType("uuid").HasColumnName("user_id");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("AssignedBy").HasDatabaseName("idx_user_role_assigned_by");
			b.HasIndex("ExpiresAt").HasDatabaseName("idx_user_role_expires_at");
			b.HasIndex("RoleId").HasDatabaseName("idx_user_role_role_id");
			b.HasIndex("UserId").HasDatabaseName("idx_user_role_user_id");
			b.HasIndex("UserId", "RoleId").IsUnique().HasDatabaseName("idx_user_role_user_id_role_id");
			b.ToTable("user_role", "gameguild.authentication");
		});
		modelBuilder.Entity("GameGuild.Identity.Authentication.UserSession", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid")
				.HasColumnName("id");
			b.Property<string>("AccessTokenHash").HasMaxLength(128).HasColumnType("character varying(128)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("DeviceFingerprint").HasMaxLength(64).HasColumnType("character varying(64)");
			b.Property<string>("DeviceInfo").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<DateTime>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<string>("IpAddress").IsRequired().HasMaxLength(45)
				.HasColumnType("character varying(45)");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<bool>("IsTrustedDevice").HasColumnType("boolean");
			b.Property<DateTime>("LastUsedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Location").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<string>("RefreshToken").IsRequired().HasMaxLength(512)
				.HasColumnType("character varying(512)");
			b.Property<DateTime?>("TerminatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("TerminationReason").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<DateTime?>("TrustedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("UserAgent").IsRequired().HasMaxLength(1000)
				.HasColumnType("character varying(1000)");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.HasKey("Id");
			b.HasIndex("ExpiresAt").HasDatabaseName("ix_usersession_expires_at");
			b.HasIndex("RefreshToken").IsUnique().HasDatabaseName("ix_usersession_refresh_token");
			b.HasIndex("UserId").HasDatabaseName("ix_usersession_user_id");
			b.ToTable("usersession", "gameguild.authentication");
		});
		modelBuilder.Entity("GameGuild.Identity.Authentication.UserWebAuthnCredential", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("AaGuid").HasMaxLength(36).HasColumnType("character varying(36)");
			b.Property<int>("AuthenticatorType").HasColumnType("integer");
			b.Property<bool>("BackedUp").HasColumnType("boolean");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("CredentialId").IsRequired().HasMaxLength(1024)
				.HasColumnType("character varying(1024)");
			b.Property<string>("CredentialType").IsRequired().ValueGeneratedOnAdd()
				.HasMaxLength(50)
				.HasColumnType("character varying(50)")
				.HasDefaultValue("public-key");
			b.Property<string>("FriendlyName").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<bool>("IsDefault").HasColumnType("boolean");
			b.Property<bool>("IsPasswordless").HasColumnType("boolean");
			b.Property<DateTime?>("LastUsedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("PublicKey").IsRequired().HasColumnType("text");
			b.Property<string>("RegisteredFromIp").HasMaxLength(45).HasColumnType("character varying(45)");
			b.Property<string>("RegisteredUserAgent").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<DateTime?>("RevokedAt").HasColumnType("timestamp with time zone");
			b.Property<long>("SignatureCounter").HasColumnType("bigint");
			b.Property<string>("Transports").HasMaxLength(200).HasColumnType("character varying(200)");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<bool>("UserVerified").HasColumnType("boolean");
			b.HasKey("Id");
			b.HasIndex("CredentialId").IsUnique().HasDatabaseName("IX_UserWebAuthnCredentials_CredentialId");
			b.HasIndex("UserId").HasDatabaseName("IX_UserWebAuthnCredentials_UserId");
			b.HasIndex("UserId", "IsActive").HasDatabaseName("IX_UserWebAuthnCredentials_UserId_IsActive");
			b.ToTable("UserWebAuthnCredentials", "auth");
		});
		modelBuilder.Entity("GameGuild.Identity.Authorization.AbacPolicy", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("ActionConditions").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<string>("AttributeExpression").HasMaxLength(4000).HasColumnType("character varying(4000)");
			b.Property<string>("ConditionExpression").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("CreatedBy").HasColumnType("uuid");
			b.Property<string>("Description").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<int>("Effect").HasColumnType("integer");
			b.Property<DateTime?>("EffectiveFrom").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("EffectiveUntil").HasColumnType("timestamp with time zone");
			b.Property<string>("EnvironmentConditions").HasMaxLength(4000).HasColumnType("character varying(4000)");
			b.Property<bool>("IsEnabled").HasColumnType("boolean");
			b.Property<string>("LocationConditions").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<string>("Name").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<string>("Obligations").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<int>("Priority").HasColumnType("integer");
			b.Property<string>("ResourceConditions").HasMaxLength(4000).HasColumnType("character varying(4000)");
			b.Property<string>("ResourceType").HasMaxLength(256).HasColumnType("character varying(256)");
			b.Property<string>("SubjectConditions").HasMaxLength(4000).HasColumnType("character varying(4000)");
			b.Property<string>("Tags").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<string>("TargetActions").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<string>("TargetResources").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("TimeConditions").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<DateTime?>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("UpdatedBy").HasColumnType("uuid");
			b.Property<int>("Version").HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("IsEnabled");
			b.HasIndex("Priority");
			b.HasIndex("TenantId");
			b.ToTable("AbacPolicy");
		});
		modelBuilder.Entity("GameGuild.Identity.Authorization.AccessControlListEntry", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<int>("AccessLevel").HasColumnType("integer");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("GrantedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("GrantedBy").HasColumnType("uuid");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<bool>("IsDenied").HasColumnType("boolean");
			b.Property<string>("Notes").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<Guid?>("PrincipalId").HasColumnType("uuid");
			b.Property<int>("PrincipalType").HasColumnType("integer");
			b.Property<string>("ResourceId").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<string>("ResourceType").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ResourceType", "ResourceId");
			b.HasIndex("TenantId", "PrincipalType", "PrincipalId");
			b.HasIndex("TenantId", "ResourceType", "ResourceId");
			b.HasIndex("TenantId", "ResourceType", "ResourceId", "IsDenied");
			b.HasIndex("TenantId", "PrincipalType", "PrincipalId", "ResourceType", "ResourceId");
			b.HasIndex("TenantId", "ResourceType", "ResourceId", "PrincipalType", "PrincipalId").IsUnique();
			b.ToTable("AccessControlListEntries");
		});
		modelBuilder.Entity("GameGuild.Identity.Authorization.AccessReviewCampaign", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<int>("ApprovedItems").HasColumnType("integer");
			b.Property<bool>("AutoRevokeOnNoResponse").HasColumnType("boolean");
			b.Property<DateTime?>("CompletedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("CompletedBy").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("CreatedBy").HasColumnType("uuid");
			b.Property<string>("Description").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<DateTime>("EndDate").HasColumnType("timestamp with time zone");
			b.Property<string>("Name").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<string>("NotificationTemplate").HasMaxLength(4000).HasColumnType("character varying(4000)");
			b.Property<int>("ReminderFrequencyDays").HasColumnType("integer");
			b.Property<int>("ReviewType").HasColumnType("integer");
			b.Property<int>("ReviewedItems").HasColumnType("integer");
			b.Property<int>("RevokedItems").HasColumnType("integer");
			b.Property<int>("Scope").HasColumnType("integer");
			b.Property<string>("ScopeFilter").HasMaxLength(4000).HasColumnType("character varying(4000)");
			b.Property<DateTime>("StartDate").HasColumnType("timestamp with time zone");
			b.Property<int>("Status").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<int>("TotalItems").HasColumnType("integer");
			b.Property<DateTime?>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.HasKey("Id");
			b.HasIndex("Status");
			b.HasIndex("TenantId");
			b.HasIndex("StartDate", "EndDate");
			b.ToTable("AccessReviewCampaign");
		});
		modelBuilder.Entity("GameGuild.Identity.Authorization.AccessReviewItem", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("CampaignId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<int?>("Decision").HasColumnType("integer");
			b.Property<string>("DecisionReason").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<DateTime?>("LastReminderSent").HasColumnType("timestamp with time zone");
			b.Property<string>("PermissionDetails").IsRequired().HasColumnType("text");
			b.Property<int>("ReminderCount").HasColumnType("integer");
			b.Property<Guid?>("ResourceId").HasColumnType("uuid");
			b.Property<string>("ResourceType").HasMaxLength(256).HasColumnType("character varying(256)");
			b.Property<DateTime?>("ReviewedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("ReviewerId").HasColumnType("uuid");
			b.Property<string>("ReviewerNotes").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<int>("Status").HasColumnType("integer");
			b.Property<Guid>("SubjectUserId").HasColumnType("uuid");
			b.Property<DateTime?>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.HasKey("Id");
			b.HasIndex("CampaignId");
			b.HasIndex("Decision");
			b.HasIndex("ReviewerId");
			b.HasIndex("SubjectUserId");
			b.ToTable("AccessReviewItem");
		});
		modelBuilder.Entity("GameGuild.Identity.Authorization.ConditionalPolicy", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<int>("Action").HasColumnType("integer");
			b.Property<int>("ConditionType").HasColumnType("integer");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("CreatedBy").HasColumnType("uuid");
			b.Property<string>("CustomConditions").HasColumnType("text");
			b.Property<string>("Description").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<string>("DeviceConditions").HasColumnType("text");
			b.Property<string>("EnvironmentConditions").HasColumnType("text");
			b.Property<bool>("IsEnabled").HasColumnType("boolean");
			b.Property<string>("LocationConditions").HasColumnType("text");
			b.Property<string>("Name").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<string>("PermissionType").HasColumnType("text");
			b.Property<int>("Priority").HasColumnType("integer");
			b.Property<string>("ResourceType").HasColumnType("text");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("TimeConditions").HasColumnType("text");
			b.Property<DateTime?>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.HasKey("Id");
			b.HasIndex("IsEnabled");
			b.HasIndex("Priority");
			b.HasIndex("TenantId");
			b.ToTable("ConditionalPolicy");
		});
		modelBuilder.Entity("GameGuild.Identity.Authorization.DataMaskingRule", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("CreatedBy").HasColumnType("uuid");
			b.Property<string>("Description").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<string>("ExemptRoles").HasColumnType("text");
			b.Property<string>("ExemptUsers").HasColumnType("text");
			b.Property<string>("FieldName").IsRequired().HasMaxLength(500)
				.HasColumnType("character varying(500)");
			b.Property<bool>("IsEnabled").HasColumnType("boolean");
			b.Property<char>("MaskCharacter").HasColumnType("character(1)");
			b.Property<string>("MaskingPattern").HasColumnType("text");
			b.Property<int>("MaskingType").HasColumnType("integer");
			b.Property<string>("Name").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<int>("Priority").HasColumnType("integer");
			b.Property<string>("RequiredPermissions").HasColumnType("text");
			b.Property<string>("ResourceType").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<int?>("ShowFirst").HasColumnType("integer");
			b.Property<int?>("ShowLast").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime?>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.HasKey("Id");
			b.HasIndex("IsEnabled");
			b.HasIndex("ResourceType");
			b.HasIndex("TenantId");
			b.ToTable("DataMaskingRule");
		});
		modelBuilder.Entity("GameGuild.Identity.Authorization.DelegatedAdminScope", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("AdminUserId").HasColumnType("uuid");
			b.Property<string>("AllowedDepartments").HasColumnType("text");
			b.Property<string>("AllowedResourceIds").HasColumnType("text");
			b.Property<string>("AllowedResourceTypes").HasColumnType("text");
			b.Property<string>("AllowedRoles").HasColumnType("text");
			b.Property<string>("AllowedTeams").HasColumnType("text");
			b.Property<string>("AllowedUserIds").HasColumnType("text");
			b.Property<bool>("CanManagePermissions").HasColumnType("boolean");
			b.Property<bool>("CanManageResources").HasColumnType("boolean");
			b.Property<bool>("CanManageUsers").HasColumnType("boolean");
			b.Property<bool>("CanViewAuditLogs").HasColumnType("boolean");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("CreatedBy").HasColumnType("uuid");
			b.Property<string>("DeniedPermissions").HasColumnType("text");
			b.Property<string>("Description").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<DateTime?>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<string>("GrantablePermissions").HasColumnType("text");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<string>("Name").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<int>("ScopeType").HasColumnType("integer");
			b.Property<DateTime>("StartsAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime?>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.HasKey("Id");
			b.HasIndex("AdminUserId");
			b.HasIndex("IsActive");
			b.HasIndex("TenantId");
			b.HasIndex("StartsAt", "ExpiresAt");
			b.ToTable("DelegatedAdminScope");
		});
		modelBuilder.Entity("GameGuild.Identity.Authorization.DynamicRole", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.PrimitiveCollection<string[]>("DenyPermissions").IsRequired().HasColumnType("text[]");
			b.Property<string>("Description").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<string>("DisplayName").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<bool>("IsSystem").HasColumnType("boolean");
			b.Property<int>("MaxAssignments").HasColumnType("integer");
			b.Property<string>("Metadata").HasColumnType("jsonb");
			b.PrimitiveCollection<Guid[]>("MutuallyExclusiveRoleIds").IsRequired().HasColumnType("uuid[]");
			b.Property<string>("Name").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<Guid?>("ParentRoleId").HasColumnType("uuid");
			b.PrimitiveCollection<string[]>("Permissions").IsRequired().HasColumnType("text[]");
			b.PrimitiveCollection<Guid[]>("PrerequisiteRoleIds").IsRequired().HasColumnType("uuid[]");
			b.Property<int>("Priority").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("IsActive");
			b.HasIndex("Name");
			b.HasIndex("ParentRoleId");
			b.HasIndex("TenantId");
			b.HasIndex("TenantId", "Name").IsUnique();
			b.ToTable("DynamicRole");
		});
		modelBuilder.Entity("GameGuild.Identity.Authorization.DynamicRoleAssignment", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("GrantedBy").HasColumnType("uuid");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<string>("Reason").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<Guid>("RoleId").HasColumnType("uuid");
			b.Property<DateTime?>("StartsAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("IsActive");
			b.HasIndex("RoleId");
			b.HasIndex("TenantId");
			b.HasIndex("UserId");
			b.HasIndex("StartsAt", "ExpiresAt");
			b.HasIndex("UserId", "RoleId", "TenantId").IsUnique();
			b.ToTable("DynamicRoleAssignment");
		});
		modelBuilder.Entity("GameGuild.Identity.Authorization.JitElevationRequest", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime?>("ActivatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("DurationMinutes").HasColumnType("integer");
			b.Property<DateTime>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Justification").IsRequired().HasMaxLength(2000)
				.HasColumnType("character varying(2000)");
			b.Property<string>("Permission").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<Guid>("RequesterId").HasColumnType("uuid");
			b.Property<Guid?>("ResourceId").HasColumnType("uuid");
			b.Property<string>("ResourceType").HasMaxLength(256).HasColumnType("character varying(256)");
			b.Property<DateTime?>("ReviewedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("ReviewerComments").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<Guid?>("ReviewerId").HasColumnType("uuid");
			b.Property<string>("RevocationReason").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<DateTime?>("RevokedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("RevokedBy").HasColumnType("uuid");
			b.Property<DateTime?>("StartsAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Status").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime?>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.HasKey("Id");
			b.HasIndex("ExpiresAt");
			b.HasIndex("RequesterId");
			b.HasIndex("Status");
			b.HasIndex("TenantId");
			b.ToTable("JitElevationRequest");
		});
		modelBuilder.Entity("GameGuild.Identity.Authorization.PermissionDelegation", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<bool>("CanSubDelegate").HasColumnType("boolean");
			b.Property<string>("Conditions").HasColumnType("text");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("DelegateUserId").HasColumnType("uuid");
			b.PrimitiveCollection<string[]>("DelegatedPermissions").IsRequired().HasColumnType("text[]");
			b.Property<Guid>("DelegatorUserId").HasColumnType("uuid");
			b.Property<DateTime?>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<string>("Reason").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<Guid?>("ResourceId").HasColumnType("uuid");
			b.Property<DateTime>("StartsAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime?>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("UsageCount").HasColumnType("integer");
			b.Property<int?>("UsageLimit").HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("DelegateUserId");
			b.HasIndex("DelegatorUserId");
			b.HasIndex("ExpiresAt");
			b.HasIndex("IsActive");
			b.HasIndex("TenantId");
			b.ToTable("PermissionDelegation");
		});
		modelBuilder.Entity("GameGuild.Identity.Authorization.PermissionTemplate", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("Category").HasMaxLength(50).HasColumnType("character varying(50)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").IsRequired().HasMaxLength(500)
				.HasColumnType("character varying(500)");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<bool>("IsSystemTemplate").HasColumnType("boolean");
			b.Property<string>("Metadata").HasColumnType("jsonb");
			b.Property<string>("MinimumTier").HasMaxLength(50).HasColumnType("character varying(50)");
			b.Property<string>("Name").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.PrimitiveCollection<string[]>("Permissions").IsRequired().HasColumnType("text[]");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex(new string[1] { "IsSystemTemplate" }, "IX_PermissionTemplates_IsSystemTemplate");
			b.HasIndex(new string[1] { "Name" }, "IX_PermissionTemplates_Name").IsUnique();
			b.ToTable("PermissionTemplates");
		});
		modelBuilder.Entity("GameGuild.Identity.Authorization.PolicyDefinitionEntity", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("AuthenticationSchemesJson").IsRequired().HasMaxLength(1000)
				.HasColumnType("character varying(1000)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<bool>("IsTenantScoped").HasColumnType("boolean");
			b.Property<string>("MinimumAccessLevel").HasMaxLength(50).HasColumnType("character varying(50)");
			b.Property<string>("PolicyName").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<long>("PolicyVersion").HasColumnType("bigint");
			b.Property<bool>("RequireAccessControlListAccess").HasColumnType("boolean");
			b.Property<bool>("RequireAuthentication").HasColumnType("boolean");
			b.Property<string>("RequiredPermissionsJson").IsRequired().HasMaxLength(2000)
				.HasColumnType("character varying(2000)");
			b.Property<string>("RequiredRolesJson").IsRequired().HasMaxLength(1000)
				.HasColumnType("character varying(1000)");
			b.Property<string>("ResourceType").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<string>("RulesJson").HasMaxLength(8000).HasColumnType("character varying(8000)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("UseRuleBasedEvaluation").HasColumnType("boolean");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("PolicyName", "TenantId").IsUnique();
			b.ToTable("PolicyDefinitions");
		});
		modelBuilder.Entity("GameGuild.Identity.Authorization.ResourceInvitation", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime?>("AcceptedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("AcceptedByUserId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("DeclineReason").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<DateTime?>("DeclinedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Email").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<DateTime?>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("InvitedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("InvitedByUserId").HasColumnType("uuid");
			b.Property<string>("InvitedByUserName").HasMaxLength(256).HasColumnType("character varying(256)");
			b.Property<string>("Message").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.PrimitiveCollection<string[]>("Permissions").IsRequired().HasMaxLength(4000)
				.HasColumnType("text[]");
			b.Property<string>("ResourceId").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<string>("ResourceType").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<DateTime?>("RevokedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("RevokedByUserId").HasColumnType("uuid");
			b.Property<int>("Status").HasColumnType("integer");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ExpiresAt");
			b.HasIndex("Status");
			b.HasIndex("TenantId", "Email");
			b.HasIndex("TenantId", "ResourceType", "ResourceId");
			b.ToTable("ResourceInvitation");
		});
		modelBuilder.Entity("GameGuild.Identity.Authorization.ResourceUserPermission", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("GrantedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("GrantedByUserId").HasColumnType("uuid");
			b.Property<string>("GrantedByUserName").HasMaxLength(256).HasColumnType("character varying(256)");
			b.Property<bool>("IsOwner").HasColumnType("boolean");
			b.Property<DateTime?>("LastAccessedAt").HasColumnType("timestamp with time zone");
			b.PrimitiveCollection<string[]>("Permissions").IsRequired().HasMaxLength(4000)
				.HasColumnType("text[]");
			b.Property<string>("ResourceId").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<string>("ResourceType").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<string>("RevocationReason").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<DateTime?>("RevokedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("RevokedByUserId").HasColumnType("uuid");
			b.Property<string>("RevokedByUserName").HasMaxLength(256).HasColumnType("character varying(256)");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ExpiresAt");
			b.HasIndex("TenantId", "UserId");
			b.HasIndex("TenantId", "ResourceType", "ResourceId");
			b.HasIndex("TenantId", "UserId", "ResourceType", "ResourceId");
			b.ToTable("ResourceUserPermission");
		});
		modelBuilder.Entity("GameGuild.Identity.Authorization.SoDRule", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("AllowedExceptions").HasColumnType("text");
			b.Property<string>("ApproverRoles").HasColumnType("text");
			b.Property<string>("ConflictingPermissions").IsRequired().HasColumnType("text");
			b.Property<string>("ConflictingResources").HasColumnType("text");
			b.Property<string>("ConflictingRoles").HasColumnType("text");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("CreatedBy").HasColumnType("uuid");
			b.Property<string>("Description").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<bool>("IsEnabled").HasColumnType("boolean");
			b.Property<DateTime?>("LastViolationDetected").HasColumnType("timestamp with time zone");
			b.Property<string>("MitigationStrategy").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<string>("Name").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<bool>("RequireApproval").HasColumnType("boolean");
			b.Property<int>("RuleType").HasColumnType("integer");
			b.Property<int>("Severity").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime?>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("ViolationCount").HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("IsEnabled");
			b.HasIndex("TenantId");
			b.ToTable("SoDRule");
		});
		modelBuilder.Entity("GameGuild.Identity.Authorization.SoDViolation", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime?>("ApprovedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("ApprovedBy").HasColumnType("uuid");
			b.Property<string>("ConflictingItems").IsRequired().HasColumnType("text");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("DetectedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("DetectedBy").HasColumnType("uuid");
			b.Property<string>("ExceptionJustification").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<bool>("IsException").HasColumnType("boolean");
			b.Property<int?>("ResolutionAction").HasColumnType("integer");
			b.Property<string>("ResolutionNotes").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<DateTime?>("ResolvedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("ResolvedBy").HasColumnType("uuid");
			b.Property<Guid>("RuleId").HasColumnType("uuid");
			b.Property<int>("Status").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime?>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<string>("ViolationDetails").IsRequired().HasColumnType("text");
			b.HasKey("Id");
			b.HasIndex("RuleId");
			b.HasIndex("Status");
			b.HasIndex("TenantId");
			b.HasIndex("UserId");
			b.ToTable("SoDViolation");
		});
		modelBuilder.Entity("GameGuild.Identity.Authorization.TenantPermission", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.PrimitiveCollection<string[]>("DenyPermissions").IsRequired().HasColumnType("text[]");
			b.Property<DateTime?>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("GrantedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("GrantedBy").HasColumnType("uuid");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<string>("Metadata").HasColumnType("jsonb");
			b.PrimitiveCollection<string[]>("Permissions").IsRequired().HasColumnType("text[]");
			b.Property<string>("Reason").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ExpiresAt");
			b.HasIndex("TenantId");
			b.HasIndex("UserId");
			b.HasIndex("TenantId", "UserId").IsUnique();
			b.HasIndex(new string[1] { "ExpiresAt" }, "IX_TenantPermissions_ExpiresAt");
			b.HasIndex(new string[1] { "TenantId" }, "IX_TenantPermissions_TenantId");
			b.HasIndex(new string[1] { "UserId" }, "IX_TenantPermissions_UserId");
			b.HasIndex(new string[2] { "UserId", "TenantId" }, "IX_TenantPermissions_User_Tenant").IsUnique();
			b.ToTable("TenantPermissions");
		});
		modelBuilder.Entity("GameGuild.Identity.Authorization.TenantSecurityVersion", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("LastChangeReason").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<DateTime>("LastUpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<long>("SecurityVersion").HasColumnType("bigint");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("TenantId").IsUnique();
			b.ToTable("TenantSecurityVersions");
		});
		modelBuilder.Entity("GameGuild.Identity.Tenants.Tenant", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("AdminEmail").IsRequired().HasMaxLength(255)
				.HasColumnType("character varying(255)");
			b.Property<DateTime?>("ArchivedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<bool>("IsArchived").HasColumnType("boolean");
			b.Property<bool>("IsDefault").HasColumnType("boolean");
			b.Property<string>("Name").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<string>("Slug").IsRequired().HasMaxLength(255)
				.HasColumnType("character varying(255)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("AdminEmail");
			b.HasIndex("IsActive");
			b.HasIndex("Name").IsUnique();
			b.HasIndex("Slug").IsUnique();
			b.ToTable("Tenants");
		});
		modelBuilder.Entity("GameGuild.Identity.Tenants.TenantAuditLog", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("Action").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<string>("ActorEmail").HasMaxLength(256).HasColumnType("character varying(256)");
			b.Property<Guid?>("ActorId").HasColumnType("uuid");
			b.Property<string>("ActorName").HasMaxLength(256).HasColumnType("character varying(256)");
			b.Property<string>("AfterValues").HasColumnType("jsonb");
			b.Property<string>("BeforeValues").HasColumnType("jsonb");
			b.Property<string>("CorrelationId").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("IpAddress").HasMaxLength(45).HasColumnType("character varying(45)");
			b.Property<string>("Metadata").HasColumnType("jsonb");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("Timestamp").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("UserAgent").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("Action");
			b.HasIndex("ActorId");
			b.HasIndex("TenantId");
			b.HasIndex("Timestamp");
			b.HasIndex("TenantId", "Timestamp");
			b.ToTable("TenantAuditLogs", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Identity.Tenants.TenantDomain", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsMainDomain").HasColumnType("boolean");
			b.Property<bool>("IsSecondaryDomain").HasColumnType("boolean");
			b.Property<string>("Subdomain").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<string>("TopLevelDomain").IsRequired().HasMaxLength(255)
				.HasColumnType("character varying(255)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("UserGroupId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("TenantId", "IsMainDomain");
			b.HasIndex("TopLevelDomain", "Subdomain").IsUnique();
			b.ToTable("TenantDomains");
		});
		modelBuilder.Entity("GameGuild.Identity.Tenants.TenantMember", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<DateTime>("JoinedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("LeaveReason").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<DateTime?>("LeftAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Metadata").HasMaxLength(4000).HasColumnType("character varying(4000)");
			b.Property<Guid?>("ParentMemberId").HasColumnType("uuid");
			b.Property<string>("Role").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("JoinedAt");
			b.HasIndex("ParentMemberId");
			b.HasIndex("TenantId", "IsActive");
			b.HasIndex("UserId", "TenantId").IsUnique();
			b.ToTable("TenantMembers");
		});
		modelBuilder.Entity("GameGuild.Identity.Tenants.TenantMetadata", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("BusinessInfo").IsRequired().ValueGeneratedOnAdd()
				.HasMaxLength(8000)
				.HasColumnType("jsonb")
				.HasDefaultValue("{}");
			b.Property<string>("ContactInfo").IsRequired().ValueGeneratedOnAdd()
				.HasMaxLength(8000)
				.HasColumnType("jsonb")
				.HasDefaultValue("{}");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("CustomFields").IsRequired().ValueGeneratedOnAdd()
				.HasMaxLength(10000)
				.HasColumnType("jsonb")
				.HasDefaultValue("{}");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("ExternalReferences").IsRequired().ValueGeneratedOnAdd()
				.HasMaxLength(8000)
				.HasColumnType("jsonb")
				.HasDefaultValue("{}");
			b.Property<string>("Industry").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<string>("Notes").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<int?>("Size").HasColumnType("integer");
			b.Property<string>("Tags").IsRequired().ValueGeneratedOnAdd()
				.HasMaxLength(5000)
				.HasColumnType("jsonb")
				.HasDefaultValue("[]");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<string>("Type").HasMaxLength(50).HasColumnType("character varying(50)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("Industry");
			b.HasIndex("Size");
			b.HasIndex("TenantId").IsUnique();
			b.HasIndex("Type");
			b.ToTable("TenantMetadata");
		});
		modelBuilder.Entity("GameGuild.Identity.Tenants.TenantSettings", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<bool>("AllowUserRegistration").HasColumnType("boolean");
			b.Property<string>("BrandingSettings").HasMaxLength(5000).HasColumnType("character varying(5000)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("DefaultCurrency").IsRequired().HasMaxLength(3)
				.HasColumnType("character varying(3)");
			b.Property<string>("DefaultLanguage").IsRequired().HasMaxLength(10)
				.HasColumnType("character varying(10)");
			b.Property<string>("DefaultTimezone").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("EnableApiAccess").HasColumnType("boolean");
			b.Property<bool>("EnableAuditLogging").HasColumnType("boolean");
			b.Property<string>("IntegrationSettingsJson").HasColumnType("text");
			b.Property<int?>("MaxUsers").HasColumnType("integer");
			b.Property<string>("NotificationSettings").HasMaxLength(5000).HasColumnType("character varying(5000)");
			b.Property<bool>("RequireRegistrationApproval").HasColumnType("boolean");
			b.Property<bool>("RequireTwoFactorAuth").HasColumnType("boolean");
			b.Property<string>("SecuritySettings").HasMaxLength(5000).HasColumnType("character varying(5000)");
			b.Property<long?>("StorageQuota").HasColumnType("bigint");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("TenantId").IsUnique();
			b.ToTable("TenantSettings");
		});
		modelBuilder.Entity("GameGuild.Identity.Tenants.TenantStatistics", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<int>("ActiveMembers").HasColumnType("integer");
			b.Property<int>("ApiCalls").HasColumnType("integer");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("CustomMetrics").HasMaxLength(10000).HasColumnType("character varying(10000)");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("InactiveMembers").HasColumnType("integer");
			b.Property<int>("MembersLeft").HasColumnType("integer");
			b.Property<int>("NewMembers").HasColumnType("integer");
			b.Property<DateTime>("StatisticDate").HasColumnType("timestamp with time zone");
			b.Property<long>("StorageUsed").HasColumnType("bigint");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<int>("TotalMembers").HasColumnType("integer");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("StatisticDate");
			b.HasIndex("TenantId").IsUnique();
			b.ToTable("TenantStatistics");
		});
		modelBuilder.Entity("GameGuild.Identity.Tenants.UsageTracking", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<decimal>("Cost").HasColumnType("decimal(18,2)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("Date").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Metadata").HasMaxLength(4000).HasColumnType("character varying(4000)");
			b.Property<string>("ResourceType").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<string>("Unit").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<long>("UsageAmount").HasColumnType("bigint");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ResourceType");
			b.HasIndex("TenantId", "Date");
			b.ToTable("UsageTracking");
		});
		modelBuilder.Entity("GameGuild.Identity.Users.User", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Email").IsRequired().HasMaxLength(255)
				.HasColumnType("character varying(255)");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<bool>("IsEmailVerified").HasColumnType("boolean");
			b.Property<bool>("IsSuspended").HasColumnType("boolean");
			b.Property<DateTime?>("LastLoginAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("LastSeenAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Name").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<string>("PasswordHash").HasMaxLength(512).HasColumnType("character varying(512)");
			b.Property<string>("PhoneNumber").HasMaxLength(20).HasColumnType("character varying(20)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<int>("TokenVersion").HasColumnType("integer");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Username").HasMaxLength(256).HasColumnType("character varying(256)");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("Email").IsUnique();
			b.HasIndex("Username").IsUnique();
			b.ToTable("Users");
		});
		modelBuilder.Entity("GameGuild.Identity.Users.UserMetadata", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("CustomFields").IsRequired().HasMaxLength(50000)
				.HasColumnType("jsonb");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("ExternalReferences").IsRequired().HasMaxLength(25000)
				.HasColumnType("jsonb");
			b.Property<string>("Notes").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<string>("Tags").IsRequired().HasMaxLength(10000)
				.HasColumnType("jsonb");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("UserId").IsUnique();
			b.ToTable("UserMetadata");
		});
		modelBuilder.Entity("GameGuild.Identity.Users.UserNotification", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("ActionUrl").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<DateTime?>("ArchivedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Content").IsRequired().HasMaxLength(2000)
				.HasColumnType("character varying(2000)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsArchived").HasColumnType("boolean");
			b.Property<bool>("IsRead").HasColumnType("boolean");
			b.Property<string>("Metadata").IsRequired().HasMaxLength(10000)
				.HasColumnType("jsonb");
			b.Property<int>("Priority").HasColumnType("integer");
			b.Property<DateTime?>("ReadAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("RelatedEntityId").HasColumnType("uuid");
			b.Property<string>("RelatedEntityType").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<Guid?>("SenderId").HasColumnType("uuid");
			b.Property<string>("Source").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Title").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("Type").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("CreatedAt");
			b.HasIndex("IsRead");
			b.HasIndex("Priority");
			b.HasIndex("Type");
			b.HasIndex("UserId");
			b.HasIndex("UserId", "IsArchived");
			b.HasIndex("UserId", "IsRead");
			b.HasIndex("UserId", "Type", "IsRead");
			b.ToTable("UserNotifications");
		});
		modelBuilder.Entity("GameGuild.Identity.Users.UserPreferences", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("AccessibilityPreferences").IsRequired().HasMaxLength(10000)
				.HasColumnType("jsonb");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("GeneralPreferences").IsRequired().HasMaxLength(10000)
				.HasColumnType("jsonb");
			b.Property<string>("LocalizationPreferences").IsRequired().HasMaxLength(10000)
				.HasColumnType("jsonb");
			b.Property<string>("NotificationPreferences").IsRequired().HasMaxLength(10000)
				.HasColumnType("jsonb");
			b.Property<string>("PrivacyPreferences").IsRequired().HasMaxLength(10000)
				.HasColumnType("jsonb");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("UserId").IsUnique();
			b.ToTable("UserPreferences");
		});
		modelBuilder.Entity("GameGuild.Identity.Users.UserProfile", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("AvatarUrl").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<string>("BannerUrl").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<string>("Bio").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<string>("Company").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateOnly?>("DateOfBirth").HasColumnType("date");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("DisplayName").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<string>("Gender").HasMaxLength(20).HasColumnType("character varying(20)");
			b.Property<bool>("IsVerified").HasColumnType("boolean");
			b.Property<string>("JobTitle").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<string>("Location").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<int>("Visibility").HasColumnType("integer");
			b.Property<string>("Website").HasMaxLength(255).HasColumnType("character varying(255)");
			b.HasKey("Id");
			b.HasIndex("UserId").IsUnique();
			b.ToTable("UserProfiles");
		});
		modelBuilder.Entity("GameGuild.LaunchPad.LaunchChecklistItem", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("Category").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<DateTime?>("CompletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsComplete").HasColumnType("boolean");
			b.Property<bool>("IsRequired").HasColumnType("boolean");
			b.Property<Guid>("LaunchPlanId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Title").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("LaunchPlanId");
			b.ToTable("launch_checklist_items", (string?)null);
		});
		modelBuilder.Entity("GameGuild.LaunchPad.LaunchPadApplication", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("LaunchPadEventId").HasColumnType("uuid");
			b.Property<string>("Pitch").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<Guid>("ProjectId").HasColumnType("uuid");
			b.Property<Guid>("ProjectVersionId").HasColumnType("uuid");
			b.Property<DateTime?>("ReviewedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("ReviewedByUserId").HasColumnType("uuid");
			b.Property<int>("Status").HasColumnType("integer");
			b.Property<string>("SubmittedAssetReferenceIdsJson").HasMaxLength(10000).HasColumnType("character varying(10000)");
			b.Property<DateTime>("SubmittedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("SubmittedByUserId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ProjectId");
			b.HasIndex("ProjectVersionId");
			b.HasIndex("SubmittedByUserId");
			b.HasIndex("LaunchPadEventId", "ProjectId").IsUnique();
			b.ToTable("launch_pad_applications", (string?)null);
		});
		modelBuilder.Entity("GameGuild.LaunchPad.LaunchPadEvent", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime?>("ApplicationsCloseAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("ApplicationsOpenAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<DateTime>("EndsAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Name").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<DateTime>("StartsAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Status").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("TenantId", "Status", "StartsAt");
			b.ToTable("launch_pad_events", (string?)null);
		});
		modelBuilder.Entity("GameGuild.LaunchPad.LaunchPadParticipantRegistration", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime?>("CheckedInAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("CompletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("LaunchPadParticipantSlotId").HasColumnType("uuid");
			b.Property<DateTime>("RegisteredAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Status").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("UserId");
			b.HasIndex("LaunchPadParticipantSlotId", "UserId").IsUnique();
			b.ToTable("launch_pad_participant_registrations", (string?)null);
		});
		modelBuilder.Entity("GameGuild.LaunchPad.LaunchPadParticipantSlot", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<int>("Capacity").HasColumnType("integer");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("EndsAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("LaunchPadEventId").HasColumnType("uuid");
			b.Property<string>("Name").IsRequired().HasMaxLength(120)
				.HasColumnType("character varying(120)");
			b.Property<int>("ReservedCount").HasColumnType("integer");
			b.Property<int>("Role").HasColumnType("integer");
			b.Property<DateTime>("StartsAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("LaunchPadEventId", "Role");
			b.ToTable("launch_pad_participant_slots", (string?)null);
		});
		modelBuilder.Entity("GameGuild.LaunchPad.LaunchPlan", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.PrimitiveCollection<string[]>("Channels").IsRequired().HasColumnType("text[]");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("LaunchPadApplicationId").HasColumnType("uuid");
			b.Property<Guid?>("LaunchPadEventId").HasColumnType("uuid");
			b.Property<DateTime?>("LaunchedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Name").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("Positioning").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<Guid>("ProjectId").HasColumnType("uuid");
			b.Property<Guid?>("ProjectVersionId").HasColumnType("uuid");
			b.Property<int>("Status").HasColumnType("integer");
			b.Property<DateTime?>("TargetLaunchAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("LaunchPadApplicationId").IsUnique().HasFilter("\"LaunchPadApplicationId\" IS NOT NULL AND \"DeletedAt\" IS NULL");
			b.HasIndex("LaunchPadEventId");
			b.HasIndex("ProjectId").IsUnique().HasDatabaseName("IX_launch_plans_ProjectId")
				.HasFilter("\"DeletedAt\" IS NULL AND \"LaunchPadEventId\" IS NULL");
			b.HasIndex("ProjectVersionId");
			b.HasIndex(new string[2] { "Status", "TargetLaunchAt" }, "IX_launch_plans_Status_TargetLaunchAt");
			b.ToTable("launch_plans", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Assessments.Assessment", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<bool>("AllowLateSubmissions").HasColumnType("boolean");
			b.Property<Guid?>("AssessmentGroupId").HasColumnType("uuid");
			b.Property<DateTime?>("AvailableFrom").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("AvailableUntil").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("ContentId").HasColumnType("uuid");
			b.Property<Guid>("CourseId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("DefinitionPayload").HasColumnType("jsonb");
			b.Property<int>("DefinitionSchemaVersion").ValueGeneratedOnAdd().HasColumnType("integer")
				.HasDefaultValue(1);
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<DateTime?>("DueAt").HasColumnType("timestamp with time zone");
			b.Property<int>("GradingMethods").HasColumnType("integer");
			b.Property<Guid?>("GroupSetId").HasColumnType("uuid");
			b.Property<bool>("IsRequired").HasColumnType("boolean");
			b.Property<DateTime?>("LateSubmissionDeadline").HasColumnType("timestamp with time zone");
			b.Property<int?>("MaxAttempts").HasColumnType("integer");
			b.Property<int>("MaxScore").HasColumnType("integer");
			b.Property<int>("Order").HasColumnType("integer");
			b.Property<int>("PassingScore").HasColumnType("integer");
			b.Property<int>("PeerReviewsRequiredCount").HasColumnType("integer");
			b.Property<int>("PresentationMode").HasColumnType("integer");
			b.Property<Guid?>("RubricId").HasColumnType("uuid");
			b.Property<int>("SubmissionModalities").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<int?>("TimeLimitMinutes").HasColumnType("integer");
			b.Property<string>("Title").IsRequired().HasMaxLength(500)
				.HasColumnType("character varying(500)");
			b.Property<int>("Type").HasColumnType("integer");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("AssessmentGroupId");
			b.HasIndex("CourseId");
			b.ToTable("Assessments", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("CK_Assessments_DeliverySchedule", "(\"AvailableFrom\" IS NULL OR \"AvailableUntil\" IS NULL OR \"AvailableFrom\" <= \"AvailableUntil\") AND (\"DueAt\" IS NULL OR \"AvailableFrom\" IS NULL OR \"DueAt\" >= \"AvailableFrom\") AND (\"DueAt\" IS NULL OR \"AvailableUntil\" IS NULL OR \"DueAt\" <= \"AvailableUntil\") AND (NOT \"AllowLateSubmissions\" OR (\"DueAt\" IS NOT NULL AND \"LateSubmissionDeadline\" IS NOT NULL AND \"LateSubmissionDeadline\" > \"DueAt\" AND (\"AvailableUntil\" IS NULL OR \"LateSubmissionDeadline\" <= \"AvailableUntil\"))) AND (\"AllowLateSubmissions\" OR \"LateSubmissionDeadline\" IS NULL)");
				t.HasCheckConstraint("CK_Assessments_GradingMethods", "\"GradingMethods\" >= 0 AND (\"GradingMethods\" & ~15) = 0");
				t.HasCheckConstraint("CK_Assessments_PresentationMode", "\"PresentationMode\" IN (0, 1)");
				t.HasCheckConstraint("CK_Assessments_ScoreRange", "\"MaxScore\" > 0 AND \"PassingScore\" >= 0 AND \"PassingScore\" <= \"MaxScore\"");
				t.HasCheckConstraint("CK_Assessments_SubmissionModalities", "\"SubmissionModalities\" > 0 AND (\"SubmissionModalities\" & ~127) = 0");
			});
		});
		modelBuilder.Entity("GameGuild.Learning.Assessments.AssessmentGroup", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("CourseId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<string>("Name").IsRequired().HasMaxLength(160)
				.HasColumnType("character varying(160)");
			b.Property<int>("Order").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<decimal>("WeightPercent").HasPrecision(5, 2).HasColumnType("numeric(5,2)");
			b.HasKey("Id");
			b.HasIndex("CourseId");
			b.HasIndex("CourseId", "Order");
			b.ToTable("AssessmentGroups", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Assessments.AssessmentPeerReview", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("AssessmentId").HasColumnType("uuid");
			b.Property<DateTime>("AssignedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Feedback").HasColumnType("text");
			b.Property<Guid>("ReviewerUserId").HasColumnType("uuid");
			b.Property<string>("RubricScoresPayload").HasColumnType("jsonb");
			b.Property<int?>("Score").HasColumnType("integer");
			b.Property<int>("Status").HasColumnType("integer");
			b.Property<Guid>("SubmissionId").HasColumnType("uuid");
			b.Property<DateTime?>("SubmittedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("AssessmentId");
			b.HasIndex("ReviewerUserId");
			b.HasIndex("SubmissionId");
			b.HasIndex("ReviewerUserId", "SubmissionId").IsUnique();
			b.ToTable("AssessmentPeerReviews", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Assessments.AssessmentRubric", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Title").IsRequired().HasMaxLength(160)
				.HasColumnType("character varying(160)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.ToTable("AssessmentRubrics", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Assessments.AssessmentSubmission", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("AssessmentId").HasColumnType("uuid");
			b.Property<int>("AttemptNumber").HasColumnType("integer");
			b.Property<string>("CodePayload").HasColumnType("text");
			b.Property<Guid?>("CourseGroupId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("EnrollmentId").HasColumnType("uuid");
			b.Property<string>("Feedback").HasColumnType("text");
			b.Property<string>("FilePayload").HasMaxLength(2048).HasColumnType("character varying(2048)");
			b.Property<DateTime?>("GradedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("GradedBy").HasColumnType("uuid");
			b.Property<bool>("IsLate").HasColumnType("boolean");
			b.Property<string>("MediaPayload").HasMaxLength(2048).HasColumnType("character varying(2048)");
			b.Property<bool?>("Passed").HasColumnType("boolean");
			b.Property<string>("ProjectPayload").HasMaxLength(2048).HasColumnType("character varying(2048)");
			b.Property<string>("RubricScoresPayload").HasColumnType("jsonb");
			b.Property<int?>("Score").HasColumnType("integer");
			b.Property<DateTime>("StartedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Status").HasColumnType("integer");
			b.Property<string>("StructuredAnswerPayload").HasColumnType("jsonb");
			b.Property<DateTime?>("SubmittedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("SubmittedModalities").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("TextPayload").HasColumnType("text");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("UrlPayload").HasMaxLength(2048).HasColumnType("character varying(2048)");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("AssessmentId");
			b.HasIndex("EnrollmentId");
			b.HasIndex("UserId");
			b.HasIndex("AssessmentId", "EnrollmentId", "AttemptNumber").IsUnique().HasDatabaseName("UX_AssessmentSubmissions_Assessment_Enrollment_Attempt");
			b.ToTable("AssessmentSubmissions", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("CK_AssessmentSubmissions_AttemptNumberPositive", "\"AttemptNumber\" > 0");
				t.HasCheckConstraint("CK_AssessmentSubmissions_PayloadConsistency", "((\"SubmittedModalities\" & 1) = 0 OR \"TextPayload\" IS NOT NULL) AND ((\"SubmittedModalities\" & 2) = 0 OR \"FilePayload\" IS NOT NULL) AND ((\"SubmittedModalities\" & 4) = 0 OR \"UrlPayload\" IS NOT NULL) AND ((\"SubmittedModalities\" & 8) = 0 OR \"CodePayload\" IS NOT NULL) AND ((\"SubmittedModalities\" & 16) = 0 OR \"MediaPayload\" IS NOT NULL) AND ((\"SubmittedModalities\" & 32) = 0 OR \"ProjectPayload\" IS NOT NULL) AND ((\"SubmittedModalities\" & 64) = 0 OR \"StructuredAnswerPayload\" IS NOT NULL) AND (\"TextPayload\" IS NULL OR (\"SubmittedModalities\" & 1) <> 0) AND (\"FilePayload\" IS NULL OR (\"SubmittedModalities\" & 2) <> 0) AND (\"UrlPayload\" IS NULL OR (\"SubmittedModalities\" & 4) <> 0) AND (\"CodePayload\" IS NULL OR (\"SubmittedModalities\" & 8) <> 0) AND (\"MediaPayload\" IS NULL OR (\"SubmittedModalities\" & 16) <> 0) AND (\"ProjectPayload\" IS NULL OR (\"SubmittedModalities\" & 32) <> 0) AND (\"StructuredAnswerPayload\" IS NULL OR (\"SubmittedModalities\" & 64) <> 0)");
				t.HasCheckConstraint("CK_AssessmentSubmissions_ScoreNonNegative", "\"Score\" IS NULL OR \"Score\" >= 0");
				t.HasCheckConstraint("CK_AssessmentSubmissions_SubmittedModalities", "\"SubmittedModalities\" >= 0 AND (\"SubmittedModalities\" & ~127) = 0");
			});
		});
		modelBuilder.Entity("GameGuild.Learning.Assessments.CourseGroup", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<int>("Capacity").HasColumnType("integer");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("GroupSetId").HasColumnType("uuid");
			b.Property<string>("Name").IsRequired().HasMaxLength(160)
				.HasColumnType("character varying(160)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("GroupSetId");
			b.ToTable("CourseGroups", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Assessments.CourseGroupMember", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("GroupId").HasColumnType("uuid");
			b.Property<DateTime>("JoinedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("UserId");
			b.HasIndex("GroupId", "UserId").IsUnique();
			b.ToTable("CourseGroupMembers", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Assessments.CourseGroupSet", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("CourseId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Name").IsRequired().HasMaxLength(160)
				.HasColumnType("character varying(160)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("CourseId", "Name").IsUnique();
			b.ToTable("CourseGroupSets", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Assessments.InteractiveVideoAssessmentCue", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("AssessmentId").HasColumnType("uuid");
			b.Property<Guid>("ContentId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("CueId").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<decimal?>("CuePositionSeconds").HasPrecision(12, 3).HasColumnType("numeric(12,3)");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ContentId");
			b.HasIndex("AssessmentId", "ContentId", "CueId").IsUnique();
			b.ToTable("InteractiveVideoAssessmentCues", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Assessments.RubricCriterion", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").IsRequired().HasColumnType("text");
			b.Property<int>("Order").HasColumnType("integer");
			b.Property<int>("Points").HasColumnType("integer");
			b.Property<Guid>("RubricId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("RubricId");
			b.ToTable("RubricCriteria", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Certificates.Certificate", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("CertificateNumber").IsRequired().HasMaxLength(80)
				.HasColumnType("character varying(80)");
			b.Property<Guid>("CourseId").HasColumnType("uuid");
			b.Property<string>("CourseName").IsRequired().HasMaxLength(500)
				.HasColumnType("character varying(500)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("DigitalSignature").HasColumnType("text");
			b.Property<Guid>("EnrollmentId").HasColumnType("uuid");
			b.Property<DateTime?>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("IssuedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("RecipientName").IsRequired().HasMaxLength(250)
				.HasColumnType("character varying(250)");
			b.Property<string>("RevocationReason").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<DateTime?>("RevokedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Status").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<Guid>("TemplateId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<string>("VerificationUrl").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("CertificateNumber").IsUnique();
			b.HasIndex("CourseId");
			b.HasIndex("EnrollmentId");
			b.HasIndex("Status");
			b.HasIndex("TemplateId");
			b.HasIndex("UserId");
			b.ToTable("learning_certificates", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Certificates.CertificateTag", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("CertificateId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTimeOffset>("LinkedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Source").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<Guid>("TagProficiencyId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("CertificateId");
			b.HasIndex("TagProficiencyId");
			b.HasIndex("CertificateId", "TagProficiencyId").IsUnique();
			b.ToTable("certificate_tags");
		});
		modelBuilder.Entity("GameGuild.Learning.Certificates.CertificateTemplate", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("CourseId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<bool>("IsDefault").HasColumnType("boolean");
			b.Property<string>("Name").IsRequired().HasMaxLength(250)
				.HasColumnType("character varying(250)");
			b.Property<string>("TemplateHtml").IsRequired().HasColumnType("text");
			b.Property<string>("TemplateStyles").HasColumnType("text");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("CourseId");
			b.HasIndex("CourseId", "IsDefault");
			b.HasIndex("TenantId", "IsActive");
			b.ToTable("learning_certificate_templates", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Cohorts.Cohort", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("CourseId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("CurrentEnrollmentCount").HasColumnType("integer");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<DateTime>("EndDate").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("InstructorId").HasColumnType("uuid");
			b.Property<bool>("IsOpen").HasColumnType("boolean");
			b.Property<int>("MaxCapacity").HasColumnType("integer");
			b.Property<string>("MeetingSchedule").HasMaxLength(4000).HasColumnType("character varying(4000)");
			b.Property<string>("Name").IsRequired().HasMaxLength(250)
				.HasColumnType("character varying(250)");
			b.Property<DateTime>("StartDate").HasColumnType("timestamp with time zone");
			b.Property<string>("Status").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("CourseId");
			b.HasIndex("InstructorId");
			b.HasIndex("TenantId");
			b.HasIndex("CourseId", "Status", "IsOpen");
			b.ToTable("learning_cohorts", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Cohorts.CohortSchedule", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("CohortId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("MeetingDays").IsRequired().HasColumnType("text");
			b.Property<int>("MeetingDurationMinutes").HasColumnType("integer");
			b.Property<TimeOnly>("MeetingStartTime").HasColumnType("time without time zone");
			b.Property<int>("PacingMode").HasColumnType("integer");
			b.Property<int>("ReleasePolicy").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("TimezoneId").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<int>("UnitsPerPeriod").HasColumnType("integer");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("CohortId").IsUnique();
			b.HasIndex("TenantId");
			b.ToTable("learning_cohort_schedules", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Cohorts.CohortScheduleItem", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid?>("AssessmentId").HasColumnType("uuid");
			b.Property<DateTime?>("AvailableFrom").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("AvailableUntil").HasColumnType("timestamp with time zone");
			b.Property<Guid>("CohortId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DueAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("EndsAt").HasColumnType("timestamp with time zone");
			b.Property<int>("InstructionalWeek").HasColumnType("integer");
			b.Property<string>("Location").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<string>("MeetingUrl").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<Guid?>("ProgramContentId").HasColumnType("uuid");
			b.Property<int>("SortOrder").HasColumnType("integer");
			b.Property<DateTime?>("StartsAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Status").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Title").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<int>("Type").HasColumnType("integer");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<int>("VisibilityOverride").HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("AssessmentId");
			b.HasIndex("ProgramContentId");
			b.HasIndex("TenantId");
			b.HasIndex("CohortId", "InstructionalWeek", "SortOrder");
			b.ToTable("learning_cohort_schedule_items", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Courses.ActivityGrade", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<int>("AttemptNumber").HasColumnType("integer");
			b.Property<Guid>("ContentInteractionId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Feedback").HasColumnType("text");
			b.Property<string>("GradeLetter").HasMaxLength(10).HasColumnType("character varying(10)");
			b.Property<int>("GradeType").HasColumnType("integer");
			b.Property<DateTime>("GradedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("GraderId").HasColumnType("uuid");
			b.Property<Guid?>("GraderProgramUserId").HasColumnType("uuid");
			b.Property<string>("GradingDetails").HasColumnType("text");
			b.Property<int?>("GradingTimeMinutes").HasColumnType("integer");
			b.Property<bool>("IsFinalized").HasColumnType("boolean");
			b.Property<decimal?>("MaxPoints").HasColumnType("decimal(5,2)");
			b.Property<decimal?>("Points").HasColumnType("decimal(5,2)");
			b.Property<Guid>("ProgramUserId").HasColumnType("uuid");
			b.Property<string>("RubricData").HasColumnType("text");
			b.Property<Guid>("StudentId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ContentInteractionId");
			b.HasIndex("GradedAt");
			b.HasIndex("GraderId");
			b.HasIndex("GraderProgramUserId");
			b.HasIndex("Points");
			b.HasIndex("ProgramUserId");
			b.HasIndex("StudentId");
			b.HasIndex("TenantId");
			b.HasIndex("StudentId", "ContentInteractionId").IsUnique();
			b.ToTable("activity_grades");
		});
		modelBuilder.Entity("GameGuild.Learning.Courses.ContentInteraction", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<int>("AttemptCount").HasColumnType("integer");
			b.Property<decimal?>("BestScore").HasColumnType("decimal(5,2)");
			b.Property<string>("BookmarkPosition").HasColumnType("text");
			b.Property<DateTime?>("CompletedAt").HasColumnType("timestamp with time zone");
			b.Property<decimal>("CompletionPercentage").HasColumnType("numeric");
			b.Property<Guid>("ContentId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("FirstAccessedAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsCompleted").HasColumnType("boolean");
			b.Property<DateTime?>("LastAccessedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Notes").HasColumnType("text");
			b.Property<Guid?>("ProgramContentId").HasColumnType("uuid");
			b.Property<Guid>("ProgramUserId").HasColumnType("uuid");
			b.Property<decimal?>("ProgressPercentage").HasColumnType("decimal(5,2)");
			b.Property<DateTime?>("StartedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Status").HasColumnType("integer");
			b.Property<string>("SubmissionData").HasColumnType("text");
			b.Property<DateTime?>("SubmittedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<int?>("TimeSpentMinutes").HasColumnType("integer");
			b.Property<int>("TimeSpentSeconds").HasColumnType("integer");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("CompletedAt");
			b.HasIndex("ContentId");
			b.HasIndex("IsCompleted");
			b.HasIndex("ProgramContentId");
			b.HasIndex("ProgramUserId");
			b.HasIndex("StartedAt");
			b.HasIndex("TenantId");
			b.HasIndex("UserId");
			b.HasIndex("UserId", "ContentId").IsUnique().HasDatabaseName("IX_content_interactions_UserId_ContentId")
				.HasFilter("\"SubmittedAt\" IS NULL AND \"DeletedAt\" IS NULL");
			b.ToTable("content_interactions", delegate(TableBuilder t)
			{
				t.HasCheckConstraint("CK_content_interactions_TimeSpentSeconds_NonNegative", "\"TimeSpentSeconds\" >= 0");
			});
		});
		modelBuilder.Entity("GameGuild.Learning.Courses.ContentInteractionEvent", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<int?>("DurationSeconds").HasColumnType("integer");
			b.Property<string>("IdempotencyKey").HasMaxLength(128).HasColumnType("character varying(128)");
			b.Property<Guid>("InteractionId").HasColumnType("uuid");
			b.Property<DateTime>("OccurredAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Payload").HasColumnType("text");
			b.Property<decimal?>("PositionSeconds").HasColumnType("decimal(12,3)");
			b.Property<decimal?>("ProgressPercentage").HasColumnType("decimal(5,2)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<int>("Type").HasColumnType("integer");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("InteractionId", "IdempotencyKey").IsUnique();
			b.HasIndex("InteractionId", "OccurredAt");
			b.ToTable("content_interaction_events", delegate(TableBuilder t)
			{
				t.HasCheckConstraint("CK_content_interaction_events_DurationSeconds_Positive", "\"DurationSeconds\" IS NULL OR \"DurationSeconds\" > 0");
				t.HasCheckConstraint("CK_content_interaction_events_PositionSeconds_NonNegative", "\"PositionSeconds\" IS NULL OR \"PositionSeconds\" >= 0");
				t.HasCheckConstraint("CK_content_interaction_events_ProgressPercentage_Range", "\"ProgressPercentage\" IS NULL OR (\"ProgressPercentage\" >= 0 AND \"ProgressPercentage\" <= 100)");
				t.HasCheckConstraint("CK_content_interaction_events_Type_Valid", "\"Type\" BETWEEN 0 AND 8");
			});
		});
		modelBuilder.Entity("GameGuild.Learning.Courses.ContentProgress", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<int>("Attempts").HasColumnType("integer");
			b.Property<DateTime?>("CompletedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("CompletionStatus").HasColumnType("integer");
			b.Property<Guid>("ContentId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("FirstAccessedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("LastAccessedAt").HasColumnType("timestamp with time zone");
			b.Property<decimal?>("MaxScore").HasPrecision(5, 2).HasColumnType("numeric(5,2)");
			b.Property<Guid>("ProgramEnrollmentId").HasColumnType("uuid");
			b.Property<string>("ProgressData").HasColumnType("jsonb");
			b.Property<decimal>("ProgressPercentage").HasPrecision(5, 2).HasColumnType("numeric(5,2)");
			b.Property<decimal?>("Score").HasPrecision(5, 2).HasColumnType("numeric(5,2)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<int>("TimeSpentSeconds").HasColumnType("integer");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("CompletedAt");
			b.HasIndex("CompletionStatus");
			b.HasIndex("ContentId");
			b.HasIndex("ProgramEnrollmentId");
			b.HasIndex("UserId");
			b.HasIndex("UserId", "ContentId").IsUnique();
			b.ToTable("content_progress", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Courses.CoursePrerequisite", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("CourseId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<int>("DisplayOrder").ValueGeneratedOnAdd().HasColumnType("integer")
				.HasDefaultValue(0);
			b.Property<int?>("MinimumGrade").HasColumnType("integer");
			b.Property<Guid>("PrerequisiteCourseId").HasColumnType("uuid");
			b.Property<string>("PrerequisiteGroup").HasMaxLength(50).HasColumnType("character varying(50)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Type").IsRequired().HasMaxLength(20)
				.HasColumnType("character varying(20)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("CourseId");
			b.HasIndex("PrerequisiteCourseId");
			b.HasIndex("TenantId");
			b.HasIndex("CourseId", "PrerequisiteCourseId").IsUnique();
			b.ToTable("course_prerequisites", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Courses.ProductProgram", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsOptional").HasColumnType("boolean");
			b.Property<Guid>("ProductId").HasColumnType("uuid");
			b.Property<Guid>("ProgramId").HasColumnType("uuid");
			b.Property<int>("SortOrder").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ProductId");
			b.HasIndex("ProgramId");
			b.HasIndex("ProductId", "ProgramId").IsUnique();
			b.ToTable("product_programs");
		});
		modelBuilder.Entity("GameGuild.Learning.Courses.Program", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<int>("Category").HasColumnType("integer");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("CreatorId").HasColumnType("uuid");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<int>("Difficulty").HasColumnType("integer");
			b.Property<DateTime?>("EnrollmentDeadline").HasColumnType("timestamp with time zone");
			b.Property<int>("EnrollmentStatus").HasColumnType("integer");
			b.Property<int?>("EstimatedHours").HasColumnType("integer");
			b.Property<int?>("MaxEnrollments").HasColumnType("integer");
			b.Property<string>("Metadata").HasMaxLength(4000).HasColumnType("character varying(4000)");
			b.Property<decimal>("PassingScore").ValueGeneratedOnAdd().HasPrecision(5, 2)
				.HasColumnType("numeric(5,2)")
				.HasDefaultValue(60m);
			b.Property<string>("Slug").HasMaxLength(200).HasColumnType("character varying(200)");
			b.Property<int>("Status").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Thumbnail").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<string>("Title").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<string>("VideoShowcaseUrl").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<int>("Visibility").HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("Category");
			b.HasIndex("CreatedAt");
			b.HasIndex("CreatorId");
			b.HasIndex("Difficulty");
			b.HasIndex("EnrollmentStatus");
			b.HasIndex("Slug").IsUnique();
			b.HasIndex("Status");
			b.HasIndex("TenantId");
			b.ToTable("programs");
		});
		modelBuilder.Entity("GameGuild.Learning.Courses.ProgramContent", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("ActivitySettingsData").HasColumnType("jsonb");
			b.Property<string>("Body").HasColumnType("text");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<int?>("EstimatedMinutes").HasColumnType("integer");
			b.Property<bool>("IsRequired").HasColumnType("boolean");
			b.Property<string>("JsonBody").HasColumnType("jsonb");
			b.Property<int?>("LessonFormat").HasColumnType("integer");
			b.Property<Guid?>("ParentId").HasColumnType("uuid");
			b.Property<Guid>("ProgramId").HasColumnType("uuid");
			b.Property<int>("SortOrder").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Title").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<int>("Type").HasColumnType("integer");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<int>("Visibility").HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("IsRequired");
			b.HasIndex("ParentId");
			b.HasIndex("ProgramId");
			b.HasIndex("SortOrder");
			b.HasIndex("TenantId");
			b.HasIndex("Type");
			b.ToTable("program_contents", delegate(TableBuilder t)
			{
				t.HasCheckConstraint("CK_program_contents_LessonFormat", "((\"Type\" IN (0, 1)) AND \"LessonFormat\" IN (0, 1, 2, 3, 4, 5)) OR ((\"Type\" NOT IN (0, 1)) AND \"LessonFormat\" IS NULL)");
			});
		});
		modelBuilder.Entity("GameGuild.Learning.Courses.ProgramEnrollment", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<bool>("CertificateIssued").HasColumnType("boolean");
			b.Property<DateTime?>("CertificateIssuedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("CompletedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("CompletionStatus").HasColumnType("integer");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("EnrolledAt").HasColumnType("timestamp with time zone");
			b.Property<int>("EnrollmentSource").HasColumnType("integer");
			b.Property<int>("EnrollmentStatus").HasColumnType("integer");
			b.Property<decimal?>("FinalGrade").HasPrecision(5, 2).HasColumnType("decimal(5,2)");
			b.Property<Guid>("ProgramId").HasColumnType("uuid");
			b.Property<decimal>("ProgressPercentage").HasPrecision(5, 2).HasColumnType("decimal(5,2)");
			b.Property<DateTime?>("StartDate").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("CompletedAt");
			b.HasIndex("EnrolledAt");
			b.HasIndex("EnrollmentStatus");
			b.HasIndex("ProgramId");
			b.HasIndex("TenantId");
			b.HasIndex("UserId");
			b.HasIndex("UserId", "ProgramId").IsUnique();
			b.ToTable("program_enrollments", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Courses.ProgramRating", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("HelpfulVotes").ValueGeneratedOnAdd().HasColumnType("integer")
				.HasDefaultValue(0);
			b.Property<bool>("IsFeatured").ValueGeneratedOnAdd().HasColumnType("boolean")
				.HasDefaultValue(false);
			b.Property<bool>("IsVerified").ValueGeneratedOnAdd().HasColumnType("boolean")
				.HasDefaultValue(false);
			b.Property<Guid>("ProgramId").HasColumnType("uuid");
			b.Property<Guid?>("ProgramUserId").HasColumnType("uuid");
			b.Property<decimal>("Rating").HasPrecision(3, 2).HasColumnType("numeric(3,2)");
			b.Property<string>("Review").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<int>("UnhelpfulVotes").ValueGeneratedOnAdd().HasColumnType("integer")
				.HasDefaultValue(0);
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("UserId").IsRequired().HasMaxLength(450)
				.HasColumnType("character varying(450)");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("CreatedAt").HasDatabaseName("IX_ProgramRatings_CreatedAt");
			b.HasIndex("IsFeatured").HasDatabaseName("IX_ProgramRatings_IsFeatured");
			b.HasIndex("IsVerified").HasDatabaseName("IX_ProgramRatings_IsVerified");
			b.HasIndex("ProgramId");
			b.HasIndex("ProgramUserId").HasDatabaseName("IX_ProgramRatings_ProgramUserId");
			b.HasIndex("Rating").HasDatabaseName("IX_ProgramRatings_Rating");
			b.HasIndex("UserId");
			b.HasIndex("ProgramId", "UserId").IsUnique().HasDatabaseName("IX_ProgramRatings_ProgramId_UserId_Unique");
			b.ToTable("program_ratings", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Courses.ProgramUser", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime?>("CompletedAt").HasColumnType("timestamp with time zone");
			b.Property<decimal>("CompletionPercentage").HasColumnType("decimal(5,2)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<decimal?>("FinalGrade").HasColumnType("decimal(5,2)");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<DateTime>("JoinedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("LastAccessedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("ProgramId").HasColumnType("uuid");
			b.Property<DateTime?>("StartedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("CompletedAt");
			b.HasIndex("IsActive");
			b.HasIndex("JoinedAt");
			b.HasIndex("ProgramId");
			b.HasIndex("TenantId");
			b.HasIndex("UserId");
			b.HasIndex("UserId", "ProgramId").IsUnique();
			b.ToTable("program_users");
		});
		modelBuilder.Entity("GameGuild.Learning.Courses.ProgramWishlist", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("AddedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("InterestedTags").HasMaxLength(200).HasColumnType("character varying(200)");
			b.Property<DateTime?>("LastNotificationSentAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Notes").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<bool>("NotificationSent").HasColumnType("boolean");
			b.Property<bool>("NotifyWhenAvailable").HasColumnType("boolean");
			b.Property<int>("Priority").HasColumnType("integer");
			b.Property<Guid>("ProgramId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("AddedAt");
			b.HasIndex("NotifyWhenAvailable");
			b.HasIndex("Priority");
			b.HasIndex("ProgramId");
			b.HasIndex("TenantId");
			b.HasIndex("UserId");
			b.HasIndex("UserId", "ProgramId").IsUnique();
			b.ToTable("program_wishlists");
		});
		modelBuilder.Entity("GameGuild.Learning.Enrollments.Enrollment", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid?>("CohortId").HasColumnType("uuid");
			b.Property<DateTime?>("CompletedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("CourseId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DroppedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("EnrolledAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("LastActivityAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Progress").HasColumnType("integer");
			b.Property<string>("Status").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("CourseId");
			b.HasIndex("Status");
			b.HasIndex("UserId");
			b.HasIndex("CourseId", "UserId");
			b.ToTable("learning_enrollments", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Experience.Discovery.CourseCollection", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<int>("CourseCount").HasColumnType("integer");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("CuratorId").HasColumnType("uuid");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<string>("ImageUrl").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<bool>("IsFeatured").HasColumnType("boolean");
			b.Property<bool>("IsPublished").HasColumnType("boolean");
			b.Property<string>("Slug").IsRequired().HasMaxLength(220)
				.HasColumnType("character varying(220)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Title").IsRequired().HasMaxLength(300)
				.HasColumnType("character varying(300)");
			b.Property<string>("Type").IsRequired().HasMaxLength(60)
				.HasColumnType("character varying(60)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("CuratorId");
			b.HasIndex("TenantId", "Slug").IsUnique();
			b.HasIndex("TenantId", "IsPublished", "IsFeatured");
			b.ToTable("learning_course_collections", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Experience.Discovery.FeaturedContent", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid?>("CourseId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("DisplayOrder").HasColumnType("integer");
			b.Property<DateTime?>("EndsAt").HasColumnType("timestamp with time zone");
			b.Property<string>("ImageUrl").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<Guid?>("LearningPathId").HasColumnType("uuid");
			b.Property<string>("LinkUrl").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<DateTime?>("StartsAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Subtitle").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<string>("TargetAudience").HasMaxLength(4000).HasColumnType("character varying(4000)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Title").IsRequired().HasMaxLength(300)
				.HasColumnType("character varying(300)");
			b.Property<string>("Type").IsRequired().HasMaxLength(60)
				.HasColumnType("character varying(60)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("CourseId");
			b.HasIndex("LearningPathId");
			b.HasIndex("Type");
			b.HasIndex("TenantId", "IsActive", "DisplayOrder");
			b.ToTable("learning_featured_content", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Experience.Discovery.SearchHistory", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid?>("ClickedCourseId").HasColumnType("uuid");
			b.Property<int?>("ClickedPosition").HasColumnType("integer");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Filters").HasMaxLength(4000).HasColumnType("character varying(4000)");
			b.Property<string>("Query").IsRequired().HasMaxLength(500)
				.HasColumnType("character varying(500)");
			b.Property<int>("ResultCount").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ClickedCourseId");
			b.HasIndex("Query");
			b.HasIndex("TenantId");
			b.HasIndex("UserId");
			b.ToTable("learning_search_history", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Experience.LearningPaths.LearningPath", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<int>("CompletionCount").HasColumnType("integer");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("CreatorId").HasColumnType("uuid");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(4000).HasColumnType("character varying(4000)");
			b.Property<string>("Difficulty").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<int>("EnrollmentCount").HasColumnType("integer");
			b.Property<int>("EstimatedHours").HasColumnType("integer");
			b.Property<string>("ImageUrl").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<bool>("IsFeatured").HasColumnType("boolean");
			b.Property<bool>("IsPublished").HasColumnType("boolean");
			b.Property<string>("Slug").IsRequired().HasMaxLength(220)
				.HasColumnType("character varying(220)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Title").IsRequired().HasMaxLength(300)
				.HasColumnType("character varying(300)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("CreatorId");
			b.HasIndex("TenantId", "Slug").IsUnique();
			b.HasIndex("TenantId", "IsPublished", "IsFeatured");
			b.ToTable("learning_paths", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Experience.LearningPaths.LearningPathCourse", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("LearningPathId").HasColumnType("uuid");
			b.Property<Guid>("CourseId").HasColumnType("uuid");
			b.Property<bool>("IsRequired").HasColumnType("boolean");
			b.Property<int>("Order").HasColumnType("integer").HasColumnName("SortOrder");
			b.HasKey("LearningPathId", "CourseId");
			b.HasIndex("CourseId");
			b.HasIndex("LearningPathId", "Order").IsUnique();
			b.ToTable("learning_path_courses", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Experience.LearningPaths.LearningPathEnrollment", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime?>("CompletedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("CoursesCompleted").HasColumnType("integer");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("EnrolledAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("LearningPathId").HasColumnType("uuid");
			b.Property<int>("Progress").HasColumnType("integer");
			b.Property<string>("Status").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<int>("TotalCourses").HasColumnType("integer");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("Status");
			b.HasIndex("UserId");
			b.HasIndex("LearningPathId", "UserId").IsUnique();
			b.ToTable("learning_path_enrollments", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Experience.Recommendations.CourseRecommendation", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("CourseId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsDismissed").HasColumnType("boolean");
			b.Property<bool>("IsViewed").HasColumnType("boolean");
			b.Property<string>("Reason").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<double>("Score").HasColumnType("double precision");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Type").IsRequired().HasMaxLength(60)
				.HasColumnType("character varying(60)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("CourseId");
			b.HasIndex("Type");
			b.HasIndex("UserId", "CourseId");
			b.HasIndex("UserId", "IsDismissed", "ExpiresAt");
			b.ToTable("learning_course_recommendations", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Experience.Recommendations.UserLearningProfile", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("LastActivityAt").HasColumnType("timestamp with time zone");
			b.Property<string>("LearningGoals").HasMaxLength(4000).HasColumnType("character varying(4000)");
			b.Property<string>("PreferredCategories").HasMaxLength(4000).HasColumnType("character varying(4000)");
			b.Property<string>("PreferredDifficulty").HasMaxLength(80).HasColumnType("character varying(80)");
			b.Property<string>("PreferredDuration").HasMaxLength(80).HasColumnType("character varying(80)");
			b.Property<string>("Skills").HasMaxLength(4000).HasColumnType("character varying(4000)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<int>("TotalCoursesCompleted").HasColumnType("integer");
			b.Property<int>("TotalHoursLearned").HasColumnType("integer");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("LastActivityAt");
			b.HasIndex("UserId").IsUnique();
			b.ToTable("learning_user_profiles", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Experience.Social.CourseDiscussion", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("AuthorId").HasColumnType("uuid");
			b.Property<string>("Content").IsRequired().HasMaxLength(10000)
				.HasColumnType("character varying(10000)");
			b.Property<Guid?>("ContentId").HasColumnType("uuid");
			b.Property<Guid>("CourseId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsPinned").HasColumnType("boolean");
			b.Property<bool>("IsResolved").HasColumnType("boolean");
			b.Property<DateTime?>("LastActivityAt").HasColumnType("timestamp with time zone");
			b.Property<int>("ReplyCount").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Title").IsRequired().HasMaxLength(300)
				.HasColumnType("character varying(300)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<int>("ViewCount").HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("AuthorId").HasDatabaseName("IX_CourseDiscussions_AuthorId");
			b.HasIndex("CourseId").HasDatabaseName("IX_CourseDiscussions_CourseId");
			b.HasIndex("CourseId", "ContentId").HasDatabaseName("IX_CourseDiscussions_CourseId_ContentId");
			b.HasIndex("CourseId", "IsPinned", "LastActivityAt").HasDatabaseName("IX_CourseDiscussions_CourseId_IsPinned_LastActivityAt");
			b.ToTable("course_discussions", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Experience.Social.CourseLike", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("CourseId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("TenantId").HasColumnType("character varying(36)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("CourseId").HasDatabaseName("IX_CourseLikes_CourseId");
			b.HasIndex("UserId").HasDatabaseName("IX_CourseLikes_UserId");
			b.HasIndex("CourseId", "UserId").IsUnique().HasDatabaseName("IX_CourseLikes_CourseId_UserId");
			b.ToTable("course_likes", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Experience.Social.CourseReview", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("Content").HasMaxLength(5000).HasColumnType("character varying(5000)");
			b.Property<Guid>("CourseId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("EnrollmentId").HasColumnType("uuid");
			b.Property<int>("HelpfulCount").HasColumnType("integer");
			b.Property<bool>("IsApproved").HasColumnType("boolean");
			b.Property<bool>("IsFeatured").HasColumnType("boolean");
			b.Property<bool>("IsVerifiedPurchase").HasColumnType("boolean");
			b.Property<int>("Rating").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Title").HasMaxLength(200).HasColumnType("character varying(200)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("CourseId").HasDatabaseName("IX_CourseReviews_CourseId");
			b.HasIndex("UserId").HasDatabaseName("IX_CourseReviews_UserId");
			b.HasIndex("CourseId", "UserId").IsUnique().HasDatabaseName("IX_CourseReviews_CourseId_UserId");
			b.HasIndex("CourseId", "IsApproved", "IsFeatured").HasDatabaseName("IX_CourseReviews_CourseId_IsApproved_IsFeatured");
			b.ToTable("course_reviews", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Experience.Social.CourseWishlist", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("CourseId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("NotifyOnSale").HasColumnType("boolean");
			b.Property<bool>("NotifyOnUpdate").HasColumnType("boolean");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("UserId").HasDatabaseName("IX_CourseWishlists_UserId");
			b.HasIndex("CourseId", "UserId").IsUnique().HasDatabaseName("IX_CourseWishlists_CourseId_UserId");
			b.ToTable("course_wishlists", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Experience.Social.DiscussionReply", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("AuthorId").HasColumnType("uuid");
			b.Property<string>("Content").IsRequired().HasMaxLength(10000)
				.HasColumnType("character varying(10000)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("DiscussionId").HasColumnType("uuid");
			b.Property<bool>("IsAcceptedAnswer").HasColumnType("boolean");
			b.Property<Guid?>("ParentReplyId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("UpvoteCount").HasColumnType("integer");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("AuthorId").HasDatabaseName("IX_DiscussionReplies_AuthorId");
			b.HasIndex("DiscussionId").HasDatabaseName("IX_DiscussionReplies_DiscussionId");
			b.HasIndex("ParentReplyId").HasDatabaseName("IX_DiscussionReplies_ParentReplyId");
			b.HasIndex("DiscussionId", "IsAcceptedAnswer").HasDatabaseName("IX_DiscussionReplies_DiscussionId_IsAcceptedAnswer");
			b.ToTable("discussion_replies", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Experience.Social.PersonalizedFeedItem", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid?>("CourseId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("DiscussionId").HasColumnType("uuid");
			b.Property<DateTime>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsDismissed").HasColumnType("boolean");
			b.Property<bool>("IsViewed").HasColumnType("boolean");
			b.Property<string>("ItemType").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<Guid?>("LearningPathId").HasColumnType("uuid");
			b.Property<string>("Reason").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<double>("RelevanceScore").HasColumnType("double precision");
			b.Property<Guid?>("ReviewId").HasColumnType("uuid");
			b.Property<string>("TenantId").HasColumnType("character varying(36)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ExpiresAt").HasDatabaseName("IX_PersonalizedFeedItems_ExpiresAt");
			b.HasIndex("UserId").HasDatabaseName("IX_PersonalizedFeedItems_UserId");
			b.HasIndex("UserId", "ItemType").HasDatabaseName("IX_PersonalizedFeedItems_UserId_ItemType");
			b.HasIndex("UserId", "IsDismissed", "ExpiresAt").HasDatabaseName("IX_PersonalizedFeedItems_UserId_IsDismissed_ExpiresAt");
			b.ToTable("personalized_feed_items", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.TestingLab.TestingLabLearningEvidenceReceipt", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid?>("CohortId").HasColumnType("uuid");
			b.Property<DateTime>("ConsumedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("CourseId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("EvidenceCompletedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("EvidenceId").HasColumnType("uuid");
			b.Property<Guid>("LearningActivityId").HasColumnType("uuid");
			b.Property<Guid>("RegistrationId").HasColumnType("uuid");
			b.Property<string>("Requirement").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<Guid>("SlotId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<Guid>("TestingEventId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("EvidenceId").IsUnique();
			b.HasIndex("RegistrationId").IsUnique();
			b.HasIndex("TenantId");
			b.HasIndex("UserId", "CourseId", "LearningActivityId");
			b.ToTable("testing_lab_learning_evidence_receipts", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Localization.Language", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("Code").IsRequired().HasMaxLength(64)
				.HasColumnType("character varying(64)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<bool>("IsDefault").HasColumnType("boolean");
			b.Property<string>("Name").IsRequired().HasMaxLength(64)
				.HasColumnType("character varying(64)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("Code").IsUnique();
			b.HasIndex("Name");
			b.ToTable("languages");
		});
		modelBuilder.Entity("GameGuild.Localization.ResourceLocalization", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid?>("AssetReferenceId").HasColumnType("uuid");
			b.Property<string>("Content").IsRequired().HasColumnType("text");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("FieldName").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<Guid>("LanguageId").HasColumnType("uuid");
			b.Property<Guid>("ResourceId").HasColumnType("uuid");
			b.Property<string>("ResourceType").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<int>("Status").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("AssetReferenceId");
			b.HasIndex("FieldName");
			b.HasIndex("LanguageId");
			b.HasIndex("ResourceId");
			b.HasIndex("ResourceId", "FieldName", "LanguageId").IsUnique();
			b.ToTable("resource_localizations");
		});
		modelBuilder.Entity("GameGuild.Learning.Lti.LtiDeployment", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<bool>("Active").HasColumnType("boolean");
			b.Property<string>("AuthTokenUrl").IsRequired().HasMaxLength(1024)
				.HasColumnType("character varying(1024)");
			b.Property<string>("AuthorizationUrl").IsRequired().HasMaxLength(1024)
				.HasColumnType("character varying(1024)");
			b.Property<string>("ClientId").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("DeploymentId").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<string>("Issuer").IsRequired().HasMaxLength(512)
				.HasColumnType("character varying(512)");
			b.Property<string>("KeyId").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("PlatformJwksUrl").IsRequired().HasMaxLength(1024)
				.HasColumnType("character varying(1024)");
			b.Property<string>("PrivateKeyPem").IsRequired().HasColumnType("text");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("Issuer", "ClientId", "DeploymentId").IsUnique().HasDatabaseName("UX_LtiDeployments_Issuer_Client_Deployment");
			b.ToTable("LtiDeployments", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Lti.LtiLineItemMapping", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("AssessmentId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("DeploymentId").HasColumnType("uuid");
			b.Property<string>("LineItemId").IsRequired().HasMaxLength(512)
				.HasColumnType("character varying(512)");
			b.Property<string>("LineItemUrl").IsRequired().HasMaxLength(1024)
				.HasColumnType("character varying(1024)");
			b.Property<int>("MaxScore").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("AssessmentId").IsUnique();
			b.HasIndex("DeploymentId");
			b.ToTable("LtiLineItemMappings", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Learning.Lti.LtiUserMapping", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("DeploymentId").HasColumnType("uuid");
			b.Property<string>("Sub").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("UserId");
			b.HasIndex("DeploymentId", "Sub").IsUnique().HasDatabaseName("UX_LtiUserMappings_Deployment_Sub");
			b.ToTable("LtiUserMappings", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Monitoring.SLA.ServiceLevelIndicator", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Endpoint").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<string>("ErrorMessage").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<bool>("IsSuccessful").HasColumnType("boolean");
			b.Property<string>("Metadata").HasMaxLength(4000).HasColumnType("character varying(4000)");
			b.Property<long?>("ResponseTimeMs").HasColumnType("bigint");
			b.Property<Guid>("ServiceLevelObjectiveId").HasColumnType("uuid");
			b.Property<int?>("StatusCode").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTimeOffset>("Timestamp").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<double>("Value").HasColumnType("double precision");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ServiceLevelObjectiveId").HasDatabaseName("ix_sli_slo_id");
			b.HasIndex("Timestamp").HasDatabaseName("ix_sli_timestamp");
			b.HasIndex("ServiceLevelObjectiveId", "Timestamp").HasDatabaseName("ix_sli_slo_timestamp");
			b.ToTable("service_level_indicators", "gameguild.sla");
		});
		modelBuilder.Entity("GameGuild.Monitoring.SLA.ServiceLevelObjective", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<double>("AlertThresholdPercentage").ValueGeneratedOnAdd().HasColumnType("double precision")
				.HasDefaultValue(50.0);
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<double?>("CurrentActualPercentage").HasColumnType("double precision");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<double>("ErrorBudgetPercentage").HasColumnType("double precision");
			b.Property<bool>("IsEnabled").ValueGeneratedOnAdd().HasColumnType("boolean")
				.HasDefaultValue(true);
			b.Property<DateTimeOffset?>("LastEvaluatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Name").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<double?>("RemainingErrorBudget").HasColumnType("double precision");
			b.Property<string>("ServiceName").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("Status").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<double>("TargetPercentage").HasColumnType("double precision");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<int>("TimeWindowDays").ValueGeneratedOnAdd().HasColumnType("integer")
				.HasDefaultValue(30);
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("IsEnabled").HasDatabaseName("ix_slo_is_enabled");
			b.HasIndex("ServiceName").HasDatabaseName("ix_slo_service_name");
			b.HasIndex("Status").HasDatabaseName("ix_slo_status");
			b.HasIndex("TenantId").HasDatabaseName("ix_slo_tenant_id");
			b.ToTable("service_level_objectives", "gameguild.sla");
		});
		modelBuilder.Entity("GameGuild.Monitoring.SLA.SloViolation", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTimeOffset?>("AcknowledgedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("AcknowledgedByUserId").HasColumnType("uuid");
			b.Property<double>("ActualValue").HasColumnType("double precision");
			b.Property<DateTimeOffset?>("AlertSentAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("AlertTriggered").HasColumnType("boolean");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<DateTimeOffset?>("EndedAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsAcknowledged").HasColumnType("boolean");
			b.Property<string>("Notes").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<Guid>("ServiceLevelObjectiveId").HasColumnType("uuid");
			b.Property<string>("Severity").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<DateTimeOffset>("StartedAt").HasColumnType("timestamp with time zone");
			b.Property<double>("TargetValue").HasColumnType("double precision");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ServiceLevelObjectiveId").HasDatabaseName("ix_sloviolation_slo_id");
			b.HasIndex("Severity").HasDatabaseName("ix_sloviolation_severity");
			b.HasIndex("StartedAt").HasDatabaseName("ix_sloviolation_started_at");
			b.HasIndex("TenantId").HasDatabaseName("ix_sloviolation_tenant_id");
			b.ToTable("slo_violations", "gameguild.sla");
		});
		modelBuilder.Entity("GameGuild.Notifications.EmailDeliveryEvent", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("BounceType").HasMaxLength(30).HasColumnType("character varying(30)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("DiagnosticCode").HasMaxLength(200).HasColumnType("character varying(200)");
			b.Property<string>("EventType").IsRequired().HasMaxLength(20)
				.HasColumnType("character varying(20)");
			b.Property<DateTime>("OccurredAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Payload").HasColumnType("jsonb");
			b.Property<string>("ProviderMessageId").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<string>("RecipientEmail").IsRequired().HasMaxLength(320)
				.HasColumnType("character varying(320)");
			b.Property<string>("SnsMessageId").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ProviderMessageId");
			b.HasIndex("SnsMessageId").IsUnique();
			b.ToTable("EmailDeliveryEvents");
		});
		modelBuilder.Entity("GameGuild.Notifications.EmailSuppression", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("BounceType").HasMaxLength(30).HasColumnType("character varying(30)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("EmailAddress").IsRequired().HasMaxLength(320)
				.HasColumnType("character varying(320)");
			b.Property<string>("Reason").IsRequired().HasMaxLength(20)
				.HasColumnType("character varying(20)");
			b.Property<DateTime?>("ReleasedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("SourceEventId").HasColumnType("uuid");
			b.Property<DateTime>("SuppressedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("EmailAddress").IsUnique();
			b.ToTable("EmailSuppressions");
		});
		modelBuilder.Entity("GameGuild.Notifications.Notification", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("ActionUrl").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<int>("AttemptCount").HasColumnType("integer");
			b.Property<string>("Channel").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("DeliveryStatus").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<string>("IconUrl").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<bool>("IsRead").HasColumnType("boolean");
			b.Property<bool>("IsSent").HasColumnType("boolean");
			b.Property<string>("LastError").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<string>("Message").IsRequired().HasMaxLength(2000)
				.HasColumnType("character varying(2000)");
			b.Property<string>("Metadata").HasMaxLength(4000).HasColumnType("character varying(4000)");
			b.Property<DateTime?>("NextAttemptAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Priority").IsRequired().HasMaxLength(20)
				.HasColumnType("character varying(20)");
			b.Property<string>("ProviderMessageId").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<DateTime?>("ReadAt").HasColumnType("timestamp with time zone");
			b.Property<string>("RecipientEmail").HasMaxLength(320).HasColumnType("character varying(320)");
			b.Property<Guid?>("RecipientId").HasColumnType("uuid");
			b.Property<Guid?>("ReferenceEntityId").HasColumnType("uuid");
			b.Property<string>("ReferenceEntityType").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<int>("RequeueCount").HasColumnType("integer");
			b.Property<DateTime?>("ScheduledAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("SentAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("TemplateId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Title").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("Type").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("Channel");
			b.HasIndex("ProviderMessageId");
			b.HasIndex("ScheduledAt");
			b.HasIndex("TemplateId");
			b.HasIndex("Type");
			b.HasIndex("RecipientId", "CreatedAt");
			b.HasIndex("RecipientId", "IsRead");
			b.HasIndex("Channel", "DeliveryStatus", "NextAttemptAt");
			b.ToTable("Notifications");
		});
		modelBuilder.Entity("GameGuild.Notifications.NotificationPreference", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<bool>("AchievementsEnabled").HasColumnType("boolean");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("EmailDigestFrequency").HasMaxLength(20).HasColumnType("character varying(20)");
			b.Property<bool>("EmailEnabled").HasColumnType("boolean");
			b.Property<bool>("InAppEnabled").HasColumnType("boolean");
			b.Property<bool>("LearningEnabled").HasColumnType("boolean");
			b.Property<bool>("MarketingEnabled").HasColumnType("boolean");
			b.Property<string>("MutedTypes").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<bool>("PushEnabled").HasColumnType("boolean");
			b.Property<string>("QuietHoursBypassPriority").IsRequired().HasMaxLength(20)
				.HasColumnType("character varying(20)");
			b.Property<TimeOnly?>("QuietHoursEnd").HasColumnType("time without time zone");
			b.Property<TimeOnly?>("QuietHoursStart").HasColumnType("time without time zone");
			b.Property<bool>("SmsEnabled").HasColumnType("boolean");
			b.Property<bool>("SocialEnabled").HasColumnType("boolean");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Timezone").HasMaxLength(50).HasColumnType("character varying(50)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("UserId").IsUnique();
			b.ToTable("NotificationPreferences");
		});
		modelBuilder.Entity("GameGuild.Notifications.NotificationTemplate", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("ActionUrlTemplate").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<string>("Category").HasMaxLength(50).HasColumnType("character varying(50)");
			b.Property<string>("Channel").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<string>("Code").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("DefaultIconUrl").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<string>("DefaultPriority").IsRequired().HasMaxLength(20)
				.HasColumnType("character varying(20)");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<string>("MessageTemplate").IsRequired().HasMaxLength(4000)
				.HasColumnType("character varying(4000)");
			b.Property<string>("Name").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("SupportedPlaceholders").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("TitleTemplate").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("Type").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("Channel");
			b.HasIndex("Code").IsUnique();
			b.HasIndex("IsActive");
			b.HasIndex("Type");
			b.ToTable("NotificationTemplates");
		});
		modelBuilder.Entity("GameGuild.ProjectWork.ProjectBoard", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Name").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<Guid>("ProjectId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ProjectId").IsUnique();
			b.ToTable("project_work_boards", (string?)null);
		});
		modelBuilder.Entity("GameGuild.ProjectWork.ProjectMilestone", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime?>("CompletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<DateTime?>("DueAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Name").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<Guid>("ProjectId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ProjectId");
			b.ToTable("project_milestones", (string?)null);
		});
		modelBuilder.Entity("GameGuild.ProjectWork.ProjectTaskChecklistItem", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsCompleted").HasColumnType("boolean");
			b.Property<int>("Position").HasColumnType("integer");
			b.Property<Guid>("TaskId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Text").IsRequired().HasMaxLength(500)
				.HasColumnType("character varying(500)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("TaskId");
			b.ToTable("project_task_checklist_items", (string?)null);
		});
		modelBuilder.Entity("GameGuild.ProjectWork.ProjectTaskComment", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("AuthorUserId").HasColumnType("uuid");
			b.Property<string>("Body").IsRequired().HasMaxLength(10000)
				.HasColumnType("character varying(10000)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("EditedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("TaskId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("TaskId");
			b.ToTable("project_task_comments", (string?)null);
		});
		modelBuilder.Entity("GameGuild.ProjectWork.ProjectTaskDependency", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("DependsOnTaskId").HasColumnType("uuid");
			b.Property<Guid>("TaskId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("DependsOnTaskId");
			b.HasIndex("TaskId", "DependsOnTaskId").IsUnique();
			b.ToTable("project_task_dependencies", (string?)null);
		});
		modelBuilder.Entity("GameGuild.ProjectWork.ProjectTaskLabel", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("Color").IsRequired().HasMaxLength(20)
				.HasColumnType("character varying(20)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Name").IsRequired().HasMaxLength(80)
				.HasColumnType("character varying(80)");
			b.Property<Guid>("ProjectId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ProjectId", "Name").IsUnique();
			b.ToTable("project_task_labels", (string?)null);
		});
		modelBuilder.Entity("GameGuild.ProjectWork.ProjectTaskLabelAssignment", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("LabelId").HasColumnType("uuid");
			b.Property<Guid>("TaskId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("LabelId");
			b.HasIndex("TaskId", "LabelId").IsUnique();
			b.ToTable("project_task_label_assignments", (string?)null);
		});
		modelBuilder.Entity("GameGuild.ProjectWork.ProjectWorkColumn", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("BoardId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Kind").IsRequired().HasMaxLength(30)
				.HasColumnType("character varying(30)");
			b.Property<string>("Name").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<int>("Position").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<int?>("WorkInProgressLimit").HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("BoardId", "Position").IsUnique();
			b.ToTable("project_work_columns", (string?)null);
		});
		modelBuilder.Entity("GameGuild.ProjectWork.ProjectWorkHistory", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("Action").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<Guid>("ActorUserId").HasColumnType("uuid");
			b.Property<string>("ChangesJson").HasMaxLength(10000).HasColumnType("character varying(10000)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("ProjectId").HasColumnType("uuid");
			b.Property<Guid?>("TaskId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ProjectId", "CreatedAt");
			b.ToTable("project_work_history", (string?)null);
		});
		modelBuilder.Entity("GameGuild.ProjectWork.ProjectWorkTask", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid?>("AssigneeUserId").HasColumnType("uuid");
			b.Property<Guid>("BoardId").HasColumnType("uuid");
			b.Property<Guid>("ColumnId").HasColumnType("uuid");
			b.Property<DateTime?>("CompletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("CreatedByUserId").HasColumnType("uuid");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(10000).HasColumnType("character varying(10000)");
			b.Property<DateTime?>("DueAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("MilestoneId").HasColumnType("uuid");
			b.Property<int>("Position").HasColumnType("integer");
			b.Property<string>("Priority").IsRequired().HasMaxLength(30)
				.HasColumnType("character varying(30)");
			b.Property<Guid>("ProjectId").HasColumnType("uuid");
			b.Property<string>("Status").IsRequired().HasMaxLength(30)
				.HasColumnType("character varying(30)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Title").IsRequired().HasMaxLength(300)
				.HasColumnType("character varying(300)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("AssigneeUserId");
			b.HasIndex("ColumnId");
			b.HasIndex("MilestoneId");
			b.HasIndex("ProjectId", "ColumnId", "Position");
			b.ToTable("project_work_tasks", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Projects.Project", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid?>("CategoryId").HasColumnType("uuid");
			b.Property<string>("Copyright").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("CreatedById").HasColumnType("uuid");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(2000).HasColumnType("text");
			b.Property<int>("DevelopmentStatus").HasColumnType("integer");
			b.Property<string>("DownloadUrl").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<string>("FeaturedImageUrl").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<string>("ImageUrl").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<string>("License").HasMaxLength(200).HasColumnType("character varying(200)");
			b.Property<Guid?>("ProjectCategoryId").HasColumnType("uuid");
			b.Property<DateTime?>("PublishedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("RepositoryUrl").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<string>("ShortDescription").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<string>("Slug").IsRequired().HasMaxLength(500)
				.HasColumnType("character varying(500)");
			b.Property<string>("SocialLinks").HasColumnType("text");
			b.Property<int>("Status").HasColumnType("integer");
			b.Property<string>("Tags").HasColumnType("text");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Title").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<int>("Type").HasColumnType("integer");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<int>("Visibility").HasColumnType("integer");
			b.Property<string>("WebsiteUrl").HasMaxLength(500).HasColumnType("character varying(500)");
			b.HasKey("Id");
			b.HasIndex("CategoryId");
			b.HasIndex("CreatedAt");
			b.HasIndex("CreatedById");
			b.HasIndex("ProjectCategoryId");
			b.HasIndex("Status");
			b.HasIndex("TenantId");
			b.HasIndex("Title");
			b.HasIndex("UpdatedAt");
			b.HasIndex("Visibility");
			b.HasIndex("CategoryId", "Status");
			b.HasIndex("Status", "Visibility");
			b.HasIndex("TenantId", "Status");
			b.ToTable("projects", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Projects.ProjectCategory", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Name").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("Name").IsUnique();
			b.ToTable("project_categories", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Projects.ProjectCollaborator", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<DateTime>("JoinedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("LeftAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Permissions").IsRequired().HasMaxLength(500)
				.HasColumnType("character varying(500)");
			b.Property<Guid>("ProjectId").HasColumnType("uuid");
			b.Property<string>("Role").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("UserId").HasDatabaseName("IX_ProjectCollaborators_User");
			b.HasIndex("ProjectId", "UserId").IsUnique().HasDatabaseName("IX_ProjectCollaborators_Project_User");
			b.HasIndex(new string[2] { "ProjectId", "UserId" }, "IX_ProjectCollaborators_Project_User").IsUnique();
			b.HasIndex(new string[1] { "UserId" }, "IX_ProjectCollaborators_User");
			b.ToTable("ProjectCollaborators");
		});
		modelBuilder.Entity("GameGuild.Projects.ProjectFeedback", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("Categories").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<string>("Content").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("HelpfulVotes").HasColumnType("integer");
			b.Property<bool>("IsFeatured").HasColumnType("boolean");
			b.Property<bool>("IsVerified").HasColumnType("boolean");
			b.Property<string>("Platform").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<Guid>("ProjectId").HasColumnType("uuid");
			b.Property<string>("ProjectVersion").HasMaxLength(50).HasColumnType("character varying(50)");
			b.Property<int>("Rating").HasColumnType("integer");
			b.Property<int>("Status").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Title").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<int>("TotalVotes").HasColumnType("integer");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("CreatedAt").HasDatabaseName("IX_ProjectFeedbacks_Date");
			b.HasIndex("UserId").HasDatabaseName("IX_ProjectFeedbacks_User");
			b.HasIndex("ProjectId", "Rating").HasDatabaseName("IX_ProjectFeedbacks_Project_Rating");
			b.HasIndex("ProjectId", "UserId").IsUnique().HasDatabaseName("IX_ProjectFeedbacks_Project_User");
			b.HasIndex(new string[1] { "CreatedAt" }, "IX_ProjectFeedbacks_Date");
			b.HasIndex(new string[2] { "ProjectId", "Rating" }, "IX_ProjectFeedbacks_Project_Rating");
			b.HasIndex(new string[2] { "ProjectId", "UserId" }, "IX_ProjectFeedbacks_Project_User").IsUnique();
			b.HasIndex(new string[1] { "UserId" }, "IX_ProjectFeedbacks_User");
			b.ToTable("ProjectFeedbacks");
		});
		modelBuilder.Entity("GameGuild.Projects.ProjectFollower", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("EmailNotifications").HasColumnType("boolean");
			b.Property<DateTime>("FollowedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("NotificationSettings").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<Guid>("ProjectId").HasColumnType("uuid");
			b.Property<bool>("PushNotifications").HasColumnType("boolean");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("FollowedAt").HasDatabaseName("IX_ProjectFollowers_Date");
			b.HasIndex("UserId").HasDatabaseName("IX_ProjectFollowers_User");
			b.HasIndex("ProjectId", "UserId").IsUnique().HasDatabaseName("IX_ProjectFollowers_Project_User");
			b.HasIndex(new string[1] { "FollowedAt" }, "IX_ProjectFollowers_Date");
			b.HasIndex(new string[2] { "ProjectId", "UserId" }, "IX_ProjectFollowers_Project_User").IsUnique();
			b.HasIndex(new string[1] { "UserId" }, "IX_ProjectFollowers_User");
			b.ToTable("ProjectFollowers");
		});
		modelBuilder.Entity("GameGuild.Projects.ProjectInvitation", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("InvitedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("InvitedByUserId").HasColumnType("uuid");
			b.Property<string>("InvitedEmail").HasMaxLength(255).HasColumnType("character varying(255)");
			b.Property<Guid?>("InvitedUserId").HasColumnType("uuid");
			b.Property<string>("Permissions").IsRequired().HasMaxLength(500)
				.HasColumnType("character varying(500)");
			b.Property<Guid>("ProjectId").HasColumnType("uuid");
			b.Property<DateTime?>("RespondedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Role").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<int>("Status").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Token").IsRequired().HasMaxLength(64)
				.HasColumnType("character varying(64)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("InvitedByUserId");
			b.HasIndex(new string[2] { "ProjectId", "Status" }, "IX_ProjectInvitations_Project_Status");
			b.HasIndex(new string[1] { "Token" }, "IX_ProjectInvitations_Token").IsUnique();
			b.HasIndex(new string[2] { "InvitedUserId", "Status" }, "IX_ProjectInvitations_User_Status");
			b.ToTable("project_invitations", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Projects.ProjectJamSubmission", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("AwardDetails").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<decimal?>("FinalScore").HasColumnType("numeric");
			b.Property<bool>("HasAward").HasColumnType("boolean");
			b.Property<bool>("IsEligible").ValueGeneratedOnAdd().HasColumnType("boolean")
				.HasDefaultValue(true);
			b.Property<Guid?>("JamId").HasColumnType("uuid");
			b.Property<string>("Metadata").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<Guid>("ProjectId").HasColumnType("uuid");
			b.Property<int?>("Ranking").HasColumnType("integer");
			b.Property<string>("SubmissionNotes").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<DateTime>("SubmittedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("FinalScore").HasDatabaseName("IX_ProjectJamSubmissions_Score");
			b.HasIndex("JamId").HasDatabaseName("IX_ProjectJamSubmissions_Jam");
			b.HasIndex("SubmittedAt").HasDatabaseName("IX_ProjectJamSubmissions_Date");
			b.HasIndex("ProjectId", "JamId").IsUnique().HasDatabaseName("IX_ProjectJamSubmissions_Project_Jam");
			b.HasIndex(new string[1] { "SubmittedAt" }, "IX_ProjectJamSubmissions_Date");
			b.HasIndex(new string[1] { "JamId" }, "IX_ProjectJamSubmissions_Jam");
			b.HasIndex(new string[2] { "ProjectId", "JamId" }, "IX_ProjectJamSubmissions_Project_Jam").IsUnique();
			b.HasIndex(new string[1] { "FinalScore" }, "IX_ProjectJamSubmissions_Score");
			b.ToTable("ProjectJamSubmissions");
		});
		modelBuilder.Entity("GameGuild.Projects.ProjectMemberAllocation", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<decimal>("CapacityPercentage").HasColumnType("numeric");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("EndsAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Function").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<Guid>("ProjectId").HasColumnType("uuid");
			b.Property<Guid>("ProjectTeamId").HasColumnType("uuid");
			b.Property<DateTime>("StartsAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ProjectTeamId");
			b.HasIndex("UserId");
			b.HasIndex("ProjectId", "UserId", "ProjectTeamId").IsUnique();
			b.ToTable("project_member_allocations", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Projects.ProjectMetadata", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("DownloadCount").HasColumnType("integer");
			b.Property<int>("FollowerCount").HasColumnType("integer");
			b.Property<Guid>("ProjectId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<int>("ViewCount").HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ProjectId").IsUnique();
			b.ToTable("project_metadata", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Projects.ProjectRelease", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("BuildNumber").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<string>("Checksum").HasMaxLength(128).HasColumnType("character varying(128)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasColumnType("text");
			b.Property<int>("DownloadCount").HasColumnType("integer");
			b.Property<string>("DownloadUrl").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<long?>("FileSize").HasColumnType("bigint");
			b.Property<bool>("IsLatest").HasColumnType("boolean");
			b.Property<bool>("IsPrerelease").HasColumnType("boolean");
			b.Property<Guid>("ProjectId").HasColumnType("uuid");
			b.Property<string>("ReleaseMetadata").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<string>("ReleaseNotes").HasColumnType("text");
			b.Property<string>("ReleaseType").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<string>("ReleaseVersion").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<DateTime>("ReleasedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Status").HasColumnType("integer");
			b.Property<string>("SupportedPlatforms").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<string>("SystemRequirements").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Title").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ProjectId", "ReleaseVersion").IsUnique();
			b.HasIndex(new string[1] { "IsLatest" }, "IX_ProjectReleases_Latest");
			b.HasIndex(new string[2] { "ProjectId", "ReleasedAt" }, "IX_ProjectReleases_Project_Date");
			b.HasIndex(new string[2] { "ProjectId", "ReleaseVersion" }, "IX_ProjectReleases_Project_Version").IsUnique();
			b.ToTable("project_releases", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Projects.ProjectStoreProduct", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("ProductId").HasColumnType("uuid");
			b.Property<Guid>("ProjectId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ProductId");
			b.HasIndex("TenantId");
			b.HasIndex("ProjectId", "ProductId").IsUnique().HasDatabaseName("IX_project_store_products_active_pair")
				.HasFilter("\"DeletedAt\" IS NULL");
			b.ToTable("project_store_products", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Projects.ProjectTeam", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("AssignedAt").HasColumnType("timestamp with time zone");
			b.Property<decimal>("ContributionPercentage").HasColumnType("numeric");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("EndedAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<string>("Notes").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<string>("ParticipationMode").IsRequired().HasMaxLength(30)
				.HasColumnType("character varying(30)");
			b.Property<string>("Permissions").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<Guid>("ProjectId").HasColumnType("uuid");
			b.Property<string>("Role").IsRequired().HasMaxLength(30)
				.HasColumnType("character varying(30)");
			b.Property<Guid>("TeamId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("AssignedAt");
			b.HasIndex("TeamId");
			b.HasIndex("ProjectId", "TeamId").IsUnique();
			b.HasIndex(new string[1] { "AssignedAt" }, "IX_ProjectTeams_Date");
			b.HasIndex(new string[2] { "ProjectId", "TeamId" }, "IX_ProjectTeams_Project_Team").IsUnique();
			b.HasIndex(new string[1] { "TeamId" }, "IX_ProjectTeams_Team");
			b.ToTable("project_teams", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Projects.ProjectTeamAgreement", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime?>("AcceptedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("AcceptedByUserId").HasColumnType("uuid");
			b.Property<DateTime?>("CancelledAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("CompletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Deliverables").IsRequired().HasMaxLength(2000)
				.HasColumnType("character varying(2000)");
			b.Property<DateTime>("EndsAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("ProjectId").HasColumnType("uuid");
			b.Property<Guid>("ProposedByUserId").HasColumnType("uuid");
			b.Property<Guid>("ProposingTeamId").HasColumnType("uuid");
			b.Property<Guid>("ReceivingTeamId").HasColumnType("uuid");
			b.Property<int>("Revision").HasColumnType("integer");
			b.Property<string>("Scope").IsRequired().HasMaxLength(1000)
				.HasColumnType("character varying(1000)");
			b.Property<DateTime>("StartsAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Status").IsRequired().HasMaxLength(30)
				.HasColumnType("character varying(30)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ProjectId");
			b.HasIndex("ProposingTeamId");
			b.HasIndex("ReceivingTeamId");
			b.ToTable("project_team_agreements", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Projects.ProjectVersion", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("CreatedById").HasColumnType("uuid");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("DownloadCount").HasColumnType("integer");
			b.Property<Guid>("ProjectId").HasColumnType("uuid");
			b.Property<string>("ReleaseNotes").HasColumnType("text");
			b.Property<string>("Status").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<string>("VersionNumber").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.HasKey("Id");
			b.HasIndex("CreatedById");
			b.HasIndex("ProjectId");
			b.HasIndex("VersionNumber");
			b.ToTable("project_versions", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Resources.Contents.ContentVersion", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("Body").HasColumnType("text");
			b.Property<string>("ChangeNotes").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("CreatedBy").HasColumnType("uuid");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("EntityId").HasColumnType("uuid");
			b.Property<string>("EntityType").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<bool>("IsCurrentVersion").HasColumnType("boolean");
			b.Property<string>("Metadata").HasColumnType("jsonb");
			b.Property<DateTime?>("PublishedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("PublishedBy").HasColumnType("uuid");
			b.Property<string>("ReviewNotes").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<DateTime?>("ReviewedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("ReviewedBy").HasColumnType("uuid");
			b.Property<DateTime?>("ScheduledPublishAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Status").IsRequired().HasMaxLength(20)
				.HasColumnType("character varying(20)");
			b.Property<Guid?>("SubmittedBy").HasColumnType("uuid");
			b.Property<DateTime?>("SubmittedForReviewAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Summary").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Title").IsRequired().HasMaxLength(500)
				.HasColumnType("character varying(500)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<int>("VersionNumber").HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("CreatedAt");
			b.HasIndex("ScheduledPublishAt");
			b.HasIndex("Status");
			b.HasIndex("EntityId", "EntityType");
			b.HasIndex("EntityId", "EntityType", "VersionNumber");
			b.ToTable("content_versions", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Resources.Contents.ContentVersionReview", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("ContentVersionId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Decision").IsRequired().HasMaxLength(20)
				.HasColumnType("character varying(20)");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Feedback").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<Guid>("ReviewerId").HasColumnType("uuid");
			b.Property<string>("Suggestions").HasColumnType("jsonb");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ContentVersionId");
			b.HasIndex("ReviewerId");
			b.ToTable("content_version_reviews", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Resources.Contents.DocumentTemplate", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("Category").HasMaxLength(120).HasColumnType("character varying(120)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<bool>("IsSystemTemplate").HasColumnType("boolean");
			b.Property<string>("Name").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("PlaceholderSchema").HasColumnType("jsonb");
			b.Property<string>("SupportedEntityType").HasMaxLength(120).HasColumnType("character varying(120)");
			b.Property<string>("TemplateKey").IsRequired().HasMaxLength(160)
				.HasColumnType("character varying(160)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("Category");
			b.HasIndex("SupportedEntityType");
			b.HasIndex("TemplateKey").IsUnique();
			b.ToTable("document_templates", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Resources.CostAllocationReport", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid")
				.HasColumnName("id");
			b.Property<string>("AllocationTags").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<string>("CostCenter").HasMaxLength(200).HasColumnType("character varying(200)");
			b.Property<decimal>("CostPerUnit").HasColumnType("decimal(18,4)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("ExportedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("InvoiceReference").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<bool>("IsExported").HasColumnType("boolean");
			b.Property<string>("Metadata").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<string>("Owner").HasMaxLength(200).HasColumnType("character varying(200)");
			b.Property<DateTime>("PeriodEnd").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("PeriodStart").HasColumnType("timestamp with time zone");
			b.Property<string>("Project").HasMaxLength(200).HasColumnType("character varying(200)");
			b.Property<string>("ResourceUsageType").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<decimal>("TotalCost").HasColumnType("decimal(18,2)");
			b.Property<long>("TotalUsage").HasColumnType("bigint");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ResourceUsageType").HasDatabaseName("ix_costallocationreport_type");
			b.HasIndex("TenantId").HasDatabaseName("ix_costallocationreport_tenant_id");
			b.HasIndex("PeriodStart", "PeriodEnd").HasDatabaseName("ix_costallocationreport_period");
			b.ToTable("cost_allocation_reports", "gameguild.resources");
		});
		modelBuilder.Entity("GameGuild.Resources.ResourceMetadata", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("Category").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("DataType").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<int>("DisplayOrder").HasColumnType("integer");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<bool>("IsSystemManaged").HasColumnType("boolean");
			b.Property<string>("Key").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<Guid?>("ResourceId").HasColumnType("uuid");
			b.Property<byte[]>("RowVersion").IsConcurrencyToken().ValueGeneratedOnAddOrUpdate()
				.HasColumnType("bytea");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("UserId").HasColumnType("uuid");
			b.Property<string>("Value").HasMaxLength(4000).HasColumnType("character varying(4000)");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("TenantId", "Category");
			b.HasIndex("TenantId", "Key").IsUnique();
			b.HasIndex("UserId", "Key");
			b.ToTable("resource_metadata", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Resources.ResourceQuota", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid")
				.HasComment("Unique identifier for the resource quota");
			b.Property<DateTime>("CreatedAt").ValueGeneratedOnAdd().HasColumnType("timestamp with time zone")
				.HasDefaultValueSql("CURRENT_TIMESTAMP")
				.HasComment("When the quota was created");
			b.Property<long>("CurrentUsage").ValueGeneratedOnAdd().HasColumnType("bigint")
				.HasDefaultValue(0L)
				.HasComment("Current usage amount");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<long?>("HardLimit").HasColumnType("bigint").HasComment("Hard limit (enforcement threshold)");
			b.Property<bool>("IsActive").ValueGeneratedOnAdd().HasColumnType("boolean")
				.HasDefaultValue(true)
				.HasComment("Whether this quota is actively enforced");
			b.Property<DateTime?>("LastReset").HasColumnType("timestamp with time zone");
			b.Property<string>("Metadata").HasMaxLength(2000).HasColumnType("varchar(2000)")
				.HasComment("Additional metadata stored as JSON");
			b.Property<string>("NotificationThresholds").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<bool>("NotificationsEnabled").HasColumnType("boolean");
			b.Property<int>("Period").HasColumnType("integer").HasComment("Period type for quota reset");
			b.Property<int?>("ResetDayOfMonth").HasColumnType("integer");
			b.Property<int?>("ResetDayOfWeek").HasColumnType("integer");
			b.Property<TimeSpan?>("ResetTime").HasColumnType("interval");
			b.Property<byte[]>("RowVersion").IsConcurrencyToken().ValueGeneratedOnAddOrUpdate()
				.HasColumnType("bytea")
				.HasComment("Optimistic concurrency token for quota updates");
			b.Property<long?>("SoftLimit").HasColumnType("bigint").HasComment("Soft limit (warning threshold)");
			b.Property<Guid>("TenantId").HasColumnType("uuid").HasComment("Tenant that owns this quota");
			b.Property<int>("Type").HasColumnType("integer").HasComment("Type of resource being limited");
			b.Property<DateTime>("UpdatedAt").ValueGeneratedOnAdd().HasColumnType("timestamp with time zone")
				.HasDefaultValueSql("CURRENT_TIMESTAMP")
				.HasComment("When the quota was last updated");
			b.Property<Guid?>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("Type").HasDatabaseName("IX_ResourceQuotas_ResourceType");
			b.HasIndex("TenantId", "Type").IsUnique().HasDatabaseName("IX_ResourceQuotas_TenantId_ResourceType");
			b.ToTable("resource_quotas", "resources", delegate(TableBuilder t)
			{
				t.HasCheckConstraint("CK_ResourceQuota_CurrentUsage_LessEqual_MaxUsage", "\"HardLimit\" IS NULL OR \"CurrentUsage\" <= \"HardLimit\"");
				t.HasCheckConstraint("CK_ResourceQuota_CurrentUsage_NonNegative", "\"CurrentUsage\" >= 0");
				t.HasCheckConstraint("CK_ResourceQuota_MaxUsage_NonNegative", "\"HardLimit\" IS NULL OR \"HardLimit\" >= 0");
			});
		});
		modelBuilder.Entity("GameGuild.Resources.ResourceSettings", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<bool>("AllowUserOverride").HasColumnType("boolean");
			b.Property<string>("Category").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("DataType").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<string>("DefaultValue").HasMaxLength(4000).HasColumnType("character varying(4000)");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<int>("DisplayOrder").HasColumnType("integer");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<bool>("IsSystemManaged").HasColumnType("boolean");
			b.Property<string>("Key").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<byte[]>("RowVersion").IsConcurrencyToken().ValueGeneratedOnAddOrUpdate()
				.HasColumnType("bytea");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("UserId").HasColumnType("uuid");
			b.Property<string>("ValidationRules").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<string>("Value").HasMaxLength(4000).HasColumnType("character varying(4000)");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("TenantId", "Category");
			b.HasIndex("TenantId", "Key").IsUnique();
			b.HasIndex("UserId", "Key");
			b.ToTable("resource_settings", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Resources.ResourceThrottlingPolicy", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid")
				.HasColumnName("id");
			b.Property<string>("Configuration").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<decimal>("DegradationFactor").ValueGeneratedOnAdd().HasColumnType("decimal(5,2)")
				.HasDefaultValue(0.5m);
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsActive").ValueGeneratedOnAdd().HasColumnType("boolean")
				.HasDefaultValue(true);
			b.Property<int?>("MaxRequestsPerWindow").HasColumnType("integer");
			b.Property<int?>("PriorityThreshold").HasColumnType("integer");
			b.Property<string>("ResourceType").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<string>("Strategy").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<int>("ThrottlingThresholdPercent").ValueGeneratedOnAdd().HasColumnType("integer")
				.HasDefaultValue(80);
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<int?>("WindowDurationSeconds").HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("TenantId").HasDatabaseName("ix_resourcethrottlingpolicy_tenant_id");
			b.HasIndex("TenantId", "ResourceType").HasDatabaseName("ix_resourcethrottlingpolicy_tenant_type");
			b.ToTable("resource_throttling_policies", "gameguild.resources");
		});
		modelBuilder.Entity("GameGuild.Resources.ResourceUsageTrend", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid")
				.HasColumnName("id");
			b.Property<int>("AnomalyCount").HasColumnType("integer");
			b.Property<double>("AverageUsage").HasColumnType("double precision");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<double>("GrowthRate").HasColumnType("double precision");
			b.Property<long>("MaxUsage").HasColumnType("bigint");
			b.Property<string>("Metadata").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<long>("MinUsage").HasColumnType("bigint");
			b.Property<string>("Pattern").IsRequired().ValueGeneratedOnAdd()
				.HasMaxLength(50)
				.HasColumnType("character varying(50)")
				.HasDefaultValue("Steady");
			b.Property<double>("PatternConfidence").ValueGeneratedOnAdd().HasColumnType("double precision")
				.HasDefaultValue(1.0);
			b.Property<DateTime?>("PeakUsageTime").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("PeriodEnd").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("PeriodStart").HasColumnType("timestamp with time zone");
			b.Property<string>("ResourceType").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<double>("StandardDeviation").HasColumnType("double precision");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("TenantId").HasDatabaseName("ix_resourceusagetrend_tenant_id");
			b.HasIndex("PeriodStart", "PeriodEnd").HasDatabaseName("ix_resourceusagetrend_period");
			b.HasIndex("TenantId", "ResourceType").HasDatabaseName("ix_resourceusagetrend_tenant_type");
			b.ToTable("resource_usage_trends", "gameguild.resources");
		});
		modelBuilder.Entity("GameGuild.Resources.SlaImpactAnalysis", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid")
				.HasColumnName("id");
			b.Property<long>("ActualValue").HasColumnType("bigint");
			b.Property<string>("BusinessImpact").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<decimal>("DeviationPercentage").HasColumnType("decimal(18,2)");
			b.Property<int>("DurationSeconds").HasColumnType("integer");
			b.Property<long>("ExpectedValue").HasColumnType("bigint");
			b.Property<bool>("IncidentCreated").HasColumnType("boolean");
			b.Property<string>("IncidentTicketId").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<bool>("IsResolved").HasColumnType("boolean");
			b.Property<string>("Metadata").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<string>("MitigationActions").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<bool>("RequiresEscalation").HasColumnType("boolean");
			b.Property<DateTime?>("ResolvedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("ResolvedByUserId").HasColumnType("uuid");
			b.Property<Guid>("ResourceQuotaId").HasColumnType("uuid");
			b.Property<string>("RootCause").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<string>("Severity").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<DateTime?>("ViolationEndTime").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("ViolationStartTime").HasColumnType("timestamp with time zone");
			b.Property<string>("ViolationType").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.HasKey("Id");
			b.HasIndex("IsResolved").HasDatabaseName("ix_slaimpactanalysis_is_resolved");
			b.HasIndex("ResourceQuotaId").HasDatabaseName("ix_slaimpactanalysis_quota_id");
			b.HasIndex("Severity").HasDatabaseName("ix_slaimpactanalysis_severity");
			b.HasIndex("TenantId").HasDatabaseName("ix_slaimpactanalysis_tenant_id");
			b.HasIndex("ViolationStartTime").HasDatabaseName("ix_slaimpactanalysis_start_time");
			b.ToTable("sla_impact_analyses", "gameguild.resources");
		});
		modelBuilder.Entity("GameGuild.Resources.UsageRecord", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid")
				.HasComment("Unique identifier for the usage record");
			b.Property<double?>("AveragePerDay").HasColumnType("double precision").HasComment("Average usage per day");
			b.Property<long>("Count").HasColumnType("bigint").HasComment("Amount of resource consumed");
			b.Property<DateTime>("CreatedAt").ValueGeneratedOnAdd().HasColumnType("timestamp with time zone")
				.HasDefaultValueSql("CURRENT_TIMESTAMP");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Metadata").HasMaxLength(1000).HasColumnType("jsonb")
				.HasComment("Additional metadata in JSON format");
			b.Property<long?>("PeakUsage").HasColumnType("bigint").HasComment("Peak usage during period");
			b.Property<DateTime?>("PeakUsageDate").HasColumnType("timestamp with time zone").HasComment("When peak usage occurred");
			b.Property<DateTime>("PeriodEnd").HasColumnType("timestamp with time zone").HasComment("When the usage period ended");
			b.Property<DateTime>("PeriodStart").HasColumnType("timestamp with time zone").HasComment("When the usage period started");
			b.Property<Guid?>("ResourceId").HasColumnType("uuid");
			b.Property<Guid>("ResourceQuotaId").HasColumnType("uuid").HasComment("Associated resource quota");
			b.Property<string>("Source").HasMaxLength(50).HasColumnType("character varying(50)");
			b.Property<Guid>("TenantId").HasColumnType("uuid").HasComment("Tenant that used the resource");
			b.Property<int>("Type").HasColumnType("integer").HasComment("Type of resource used");
			b.Property<DateTime>("UpdatedAt").ValueGeneratedOnAdd().HasColumnType("timestamp with time zone")
				.HasDefaultValueSql("CURRENT_TIMESTAMP");
			b.Property<long>("UsageAmount").HasColumnType("bigint");
			b.Property<Guid?>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("PeriodStart").HasDatabaseName("IX_UsageRecords_PeriodStart");
			b.HasIndex("PeriodStart", "PeriodEnd").HasDatabaseName("IX_UsageRecords_UsagePeriod");
			b.HasIndex("TenantId", "Type", "PeriodStart").HasDatabaseName("IX_UsageRecords_Tenant_Resource_Time");
			b.ToTable("usage_records", "resources", delegate(TableBuilder t)
			{
				t.HasCheckConstraint("CK_UsageRecord_Count_NonNegative", "\"Count\" >= 0");
				t.HasCheckConstraint("CK_UsageRecord_PeakUsage_NonNegative", "\"PeakUsage\" IS NULL OR \"PeakUsage\" >= 0");
				t.HasCheckConstraint("CK_UsageRecord_PeriodOrder", "\"PeriodEnd\" >= \"PeriodStart\"");
			});
		});
		modelBuilder.Entity("GameGuild.Resources.UsageRetentionPolicy", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid")
				.HasColumnName("id");
			b.Property<int>("ArchiveAfterDays").ValueGeneratedOnAdd().HasColumnType("integer")
				.HasDefaultValue(30);
			b.Property<int>("CompactionIntervalDays").ValueGeneratedOnAdd().HasColumnType("integer")
				.HasDefaultValue(7);
			b.Property<string>("Configuration").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<string>("DownSamplingStrategy").IsRequired().ValueGeneratedOnAdd()
				.HasMaxLength(50)
				.HasColumnType("character varying(50)")
				.HasDefaultValue("daily");
			b.Property<bool>("EnableCompaction").ValueGeneratedOnAdd().HasColumnType("boolean")
				.HasDefaultValue(true);
			b.Property<bool>("IsActive").ValueGeneratedOnAdd().HasColumnType("boolean")
				.HasDefaultValue(true);
			b.Property<DateTime?>("LastExecutedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Name").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<DateTime?>("NextExecutionAt").HasColumnType("timestamp with time zone");
			b.Property<string>("ResourceType").HasMaxLength(50).HasColumnType("character varying(50)");
			b.Property<int>("RetentionDays").ValueGeneratedOnAdd().HasColumnType("integer")
				.HasDefaultValue(90);
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("IsActive").HasDatabaseName("ix_usageretentionpolicy_is_active");
			b.HasIndex("NextExecutionAt").HasDatabaseName("ix_usageretentionpolicy_next_execution");
			b.HasIndex("TenantId").HasDatabaseName("ix_usageretentionpolicy_tenant_id");
			b.ToTable("usage_retention_policies", "gameguild.resources");
		});
		modelBuilder.Entity("GameGuild.Social.Blog.BlogPost", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<bool>("AllowComments").HasColumnType("boolean");
			b.Property<Guid>("AuthorId").HasColumnType("uuid");
			b.Property<int>("CommentsCount").HasColumnType("integer");
			b.Property<string>("Content").IsRequired().HasColumnType("text");
			b.Property<string>("CoverImageUrl").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Excerpt").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<bool>("IsFeatured").HasColumnType("boolean");
			b.Property<int>("LikesCount").HasColumnType("integer");
			b.Property<DateTime?>("PublishedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("ReadTimeMinutes").HasColumnType("integer");
			b.Property<string>("Slug").IsRequired().HasMaxLength(220)
				.HasColumnType("character varying(220)");
			b.Property<string>("Status").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Title").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<int>("ViewsCount").HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("AuthorId");
			b.HasIndex("IsFeatured");
			b.HasIndex("Slug").IsUnique();
			b.HasIndex("Status");
			b.ToTable("social_blog_posts", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Social.Feed.FeedItem", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("AuthorId").HasColumnType("uuid");
			b.Property<DateTime>("ContentCreatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("ContentId").HasColumnType("uuid");
			b.Property<string>("ContentType").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsHidden").HasColumnType("boolean");
			b.Property<bool>("IsRead").HasColumnType("boolean");
			b.Property<string>("Reason").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<double>("RelevanceScore").HasColumnType("double precision");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ContentCreatedAt");
			b.HasIndex("UserId", "IsHidden", "IsRead");
			b.ToTable("social_feed_items", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Social.Follows.Block", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("BlockedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("BlockedId").HasColumnType("uuid");
			b.Property<Guid>("BlockerId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Reason").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("BlockedId");
			b.HasIndex("BlockerId", "BlockedId").IsUnique();
			b.ToTable("blocks", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Social.Follows.Follow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("FollowedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("FollowedEntityId").HasColumnType("uuid");
			b.Property<string>("FollowedEntityType").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<Guid>("FollowerId").HasColumnType("uuid");
			b.Property<bool>("NotificationsEnabled").ValueGeneratedOnAdd().HasColumnType("boolean")
				.HasDefaultValue(true);
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("FollowerId");
			b.HasIndex("FollowedEntityId", "FollowedEntityType");
			b.HasIndex("FollowerId", "FollowedEntityId", "FollowedEntityType").IsUnique();
			b.ToTable("follows", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Social.Follows.FollowPrivacySettings", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<bool>("AllowFollowers").ValueGeneratedOnAdd().HasColumnType("boolean")
				.HasDefaultValue(true);
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsFollowerListPublic").ValueGeneratedOnAdd().HasColumnType("boolean")
				.HasDefaultValue(true);
			b.Property<bool>("IsFollowingListPublic").ValueGeneratedOnAdd().HasColumnType("boolean")
				.HasDefaultValue(true);
			b.Property<bool>("NotifyOnNewFollower").ValueGeneratedOnAdd().HasColumnType("boolean")
				.HasDefaultValue(true);
			b.Property<bool>("ShowFollowerCount").ValueGeneratedOnAdd().HasColumnType("boolean")
				.HasDefaultValue(true);
			b.Property<bool>("ShowFollowingCount").ValueGeneratedOnAdd().HasColumnType("boolean")
				.HasDefaultValue(true);
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("UserId").IsUnique();
			b.ToTable("follow_privacy_settings", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Social.Follows.Mute", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("MutedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("MutedId").HasColumnType("uuid");
			b.Property<Guid>("MuterId").HasColumnType("uuid");
			b.Property<string>("Reason").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ExpiresAt").HasFilter("\"ExpiresAt\" IS NOT NULL");
			b.HasIndex("MutedId");
			b.HasIndex("MuterId", "MutedId").IsUnique();
			b.ToTable("mutes", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Social.Groups.SocialGroup", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<int>("MemberCount").HasColumnType("integer");
			b.Property<string>("Name").IsRequired().HasMaxLength(120)
				.HasColumnType("character varying(120)");
			b.Property<Guid>("OwnerId").HasColumnType("uuid");
			b.Property<int>("PendingMemberCount").HasColumnType("integer");
			b.Property<string>("Slug").IsRequired().HasMaxLength(160)
				.HasColumnType("character varying(160)");
			b.Property<string>("Status").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Type").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<string>("Visibility").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.HasKey("Id");
			b.HasIndex("OwnerId");
			b.HasIndex("Slug").IsUnique();
			b.HasIndex("TenantId");
			b.HasIndex("Status", "Visibility", "Type");
			b.ToTable("social_groups", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Social.Groups.SocialGroupMember", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid?>("ApprovedByUserId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("GroupId").HasColumnType("uuid");
			b.Property<DateTime?>("JoinedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("RemovedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("RequestedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Role").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<string>("Status").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("GroupId", "Status");
			b.HasIndex("GroupId", "UserId").IsUnique();
			b.ToTable("social_group_members", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Social.Posts.Post", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("AuthorId").HasColumnType("uuid");
			b.Property<int>("CommentsCount").HasColumnType("integer");
			b.Property<string>("Content").IsRequired().HasMaxLength(10000)
				.HasColumnType("character varying(10000)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("EditedAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsEdited").HasColumnType("boolean");
			b.Property<bool>("IsPinned").HasColumnType("boolean");
			b.Property<int>("LikesCount").HasColumnType("integer");
			b.Property<int?>("MediaType").HasColumnType("integer");
			b.Property<string>("MediaUrl").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<Guid?>("ReplyToPostId").HasColumnType("uuid");
			b.Property<Guid?>("RepostOfPostId").HasColumnType("uuid");
			b.Property<int>("SharesCount").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<int>("ViewsCount").HasColumnType("integer");
			b.Property<int>("Visibility").HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("AuthorId");
			b.HasIndex("CreatedAt");
			b.HasIndex("IsPinned");
			b.HasIndex("TenantId");
			b.HasIndex("Visibility");
			b.ToTable("posts", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Social.Posts.PostComment", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("AuthorId").HasColumnType("uuid");
			b.Property<string>("Content").IsRequired().HasMaxLength(2000)
				.HasColumnType("character varying(2000)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("EditedAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsEdited").HasColumnType("boolean");
			b.Property<int>("LikesCount").HasColumnType("integer");
			b.Property<Guid?>("ParentCommentId").HasColumnType("uuid");
			b.Property<Guid>("PostId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("AuthorId");
			b.HasIndex("ParentCommentId");
			b.HasIndex("PostId");
			b.ToTable("post_comments", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Social.Posts.PostContentReference", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("Context").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Order").HasColumnType("integer");
			b.Property<Guid>("PostId").HasColumnType("uuid");
			b.Property<string>("ReferenceType").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<Guid>("ReferencedResourceId").HasColumnType("uuid");
			b.Property<string>("ResourceType").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("PostId");
			b.HasIndex("ReferenceType");
			b.HasIndex("ReferencedResourceId");
			b.ToTable("post_content_references", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Social.Posts.PostFollower", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("NotifyOnComments").HasColumnType("boolean");
			b.Property<bool>("NotifyOnLikes").HasColumnType("boolean");
			b.Property<bool>("NotifyOnShares").HasColumnType("boolean");
			b.Property<bool>("NotifyOnUpdates").HasColumnType("boolean");
			b.Property<Guid>("PostId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("PostId");
			b.HasIndex("UserId");
			b.HasIndex("PostId", "UserId").IsUnique();
			b.ToTable("post_followers", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Social.Posts.PostLike", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("PostId").HasColumnType("uuid");
			b.Property<string>("ReactionType").IsRequired().HasMaxLength(20)
				.HasColumnType("character varying(20)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("PostId");
			b.HasIndex("UserId");
			b.HasIndex("PostId", "UserId").IsUnique();
			b.ToTable("post_likes", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Social.Posts.PostStatistics", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<double>("AverageEngagementTime").HasColumnType("double precision");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<double>("EngagementScore").HasColumnType("double precision");
			b.Property<int>("ExternalSharesCount").HasColumnType("integer");
			b.Property<DateTime>("LastCalculatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("PostId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<double>("TrendingScore").HasColumnType("double precision");
			b.Property<int>("UniqueViewersCount").HasColumnType("integer");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<int>("ViewsCount").HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("EngagementScore");
			b.HasIndex("PostId").IsUnique();
			b.HasIndex("TrendingScore");
			b.ToTable("post_statistics", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Social.Posts.PostTag", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("Category").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<string>("Color").HasMaxLength(7).HasColumnType("character varying(7)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<string>("DisplayName").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<bool>("IsFeatured").HasColumnType("boolean");
			b.Property<string>("Name").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("UsageCount").HasColumnType("integer");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("Category");
			b.HasIndex("Name").IsUnique();
			b.HasIndex("UsageCount");
			b.ToTable("post_tags", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Social.Posts.PostTagAssignment", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Order").HasColumnType("integer");
			b.Property<Guid>("PostId").HasColumnType("uuid");
			b.Property<Guid>("TagId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("PostId");
			b.HasIndex("TagId");
			b.HasIndex("PostId", "TagId").IsUnique();
			b.ToTable("post_tag_assignments", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Social.Posts.PostView", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("DurationSeconds").HasColumnType("integer");
			b.Property<string>("IpAddress").HasMaxLength(45).HasColumnType("character varying(45)");
			b.Property<bool>("IsEngaged").HasColumnType("boolean");
			b.Property<Guid>("PostId").HasColumnType("uuid");
			b.Property<string>("Referrer").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("UserAgent").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<Guid?>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<DateTime>("ViewedAt").HasColumnType("timestamp with time zone");
			b.HasKey("Id");
			b.HasIndex("IpAddress");
			b.HasIndex("PostId");
			b.HasIndex("UserId");
			b.HasIndex("ViewedAt");
			b.ToTable("post_views", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Social.Profiles.ProfilePortfolioItem", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<int>("DisplayOrder").HasColumnType("integer");
			b.Property<string>("ImageUrl").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<bool>("IsPinned").HasColumnType("boolean");
			b.Property<Guid>("ProfileId").HasColumnType("uuid");
			b.Property<Guid?>("ProjectId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Title").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Url").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ProfileId");
			b.HasIndex("ProjectId");
			b.ToTable("social_profile_portfolio_items");
		});
		modelBuilder.Entity("GameGuild.Social.Profiles.ProfileSkill", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("DisplayOrder").HasColumnType("integer");
			b.Property<string>("Name").IsRequired().HasMaxLength(120)
				.HasColumnType("character varying(120)");
			b.Property<string>("Proficiency").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<Guid>("ProfileId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ProfileId");
			b.HasIndex("ProfileId", "Name").IsUnique();
			b.ToTable("social_profile_skills");
		});
		modelBuilder.Entity("GameGuild.Social.Profiles.SocialProfile", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("AvailabilityStatus").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<string>("AvatarUrl").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<string>("BannerUrl").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<string>("Bio").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<int>("CompletenessScore").HasColumnType("integer");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("DisplayName").IsRequired().HasMaxLength(180)
				.HasColumnType("character varying(180)");
			b.Property<int>("FollowerCount").HasColumnType("integer");
			b.Property<int>("FollowingCount").HasColumnType("integer");
			b.Property<string>("Handle").IsRequired().HasMaxLength(80)
				.HasColumnType("character varying(80)");
			b.Property<string>("Headline").HasMaxLength(120).HasColumnType("character varying(120)");
			b.Property<string>("Location").HasMaxLength(120).HasColumnType("character varying(120)");
			b.Property<int>("PostCount").HasColumnType("integer");
			b.Property<int>("ProjectCount").HasColumnType("integer");
			b.Property<bool>("ShowActivity").HasColumnType("boolean");
			b.Property<bool>("ShowPortfolio").HasColumnType("boolean");
			b.Property<bool>("ShowSkills").HasColumnType("boolean");
			b.Property<string>("SocialLinksJson").IsRequired().HasColumnType("jsonb");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("TimeZone").HasMaxLength(80).HasColumnType("character varying(80)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<DateTime?>("VerifiedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<string>("Visibility").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<string>("WebsiteUrl").HasMaxLength(500).HasColumnType("character varying(500)");
			b.HasKey("Id");
			b.HasIndex("Handle").IsUnique();
			b.HasIndex("UserId").IsUnique();
			b.HasIndex("Visibility");
			b.ToTable("social_profiles");
		});
		modelBuilder.Entity("GameGuild.Social.Reactions.Reaction", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("TargetId").HasColumnType("uuid");
			b.Property<string>("TargetType").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Type").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("TargetId", "TargetType");
			b.HasIndex("UserId", "TargetId", "TargetType").IsUnique();
			b.ToTable("social_reactions", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Tags.Tag", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("Color").HasMaxLength(7).HasColumnType("character varying(7)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<string>("Icon").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<string>("Name").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<int>("Type").HasColumnType("integer");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("IsActive");
			b.HasIndex("Name");
			b.HasIndex("TenantId");
			b.HasIndex("Type");
			b.HasIndex("Name", "TenantId").IsUnique();
			b.ToTable("tags");
		});
		modelBuilder.Entity("GameGuild.Tags.TagProficiency", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("Color").HasMaxLength(7).HasColumnType("character varying(7)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<string>("Icon").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<string>("Name").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<int>("ProficiencyLevel").HasColumnType("integer");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<int>("Type").HasColumnType("integer");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("IsActive");
			b.HasIndex("Name");
			b.HasIndex("ProficiencyLevel");
			b.HasIndex("Type");
			b.ToTable("tag_proficiencies");
		});
		modelBuilder.Entity("GameGuild.Tags.TagRelationship", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Metadata").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<Guid>("SourceId").HasColumnType("uuid");
			b.Property<Guid>("TargetId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<int>("Type").HasColumnType("integer");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<decimal?>("Weight").HasColumnType("decimal(3,2)");
			b.HasKey("Id");
			b.HasIndex("SourceId");
			b.HasIndex("TargetId");
			b.HasIndex("Type");
			b.HasIndex("SourceId", "TargetId").IsUnique();
			b.ToTable("tag_relationships", delegate(TableBuilder t)
			{
				t.HasCheckConstraint("CK_TagRelationships_NoSelfReference", "\"SourceId\" != \"TargetId\"");
			});
		});
		modelBuilder.Entity("GameGuild.Teams.Team", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<bool>("IsPersonal").HasColumnType("boolean");
			b.Property<string>("Name").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("Slug").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("Status").IsRequired().HasMaxLength(30)
				.HasColumnType("character varying(30)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<string>("Visibility").IsRequired().HasMaxLength(30)
				.HasColumnType("character varying(30)");
			b.HasKey("Id");
			b.HasIndex("TenantId", "Slug").IsUnique();
			b.ToTable("project_collaboration_teams", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Teams.TeamInvitation", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid?>("AcceptedByUserId").HasColumnType("uuid");
			b.Property<string>("Authority").IsRequired().HasMaxLength(30)
				.HasColumnType("character varying(30)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("InvitedByUserId").HasColumnType("uuid");
			b.Property<string>("InvitedEmail").HasMaxLength(255).HasColumnType("character varying(255)");
			b.Property<Guid?>("InvitedUserId").HasColumnType("uuid");
			b.Property<DateTime?>("RevokedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("TeamId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("TokenHash").IsRequired().HasMaxLength(64)
				.HasColumnType("character varying(64)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("UsedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("TokenHash").IsUnique();
			b.HasIndex("TeamId", "InvitedEmail");
			b.ToTable("team_invitations", (string?)null);
		});
		modelBuilder.Entity("GameGuild.Teams.TeamMember", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("Authority").IsRequired().HasMaxLength(30)
				.HasColumnType("character varying(30)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<DateTime>("JoinedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("LeftAt").HasColumnType("timestamp with time zone");
			b.Property<string>("ProfessionalTitle").HasMaxLength(150).HasColumnType("character varying(150)");
			b.Property<Guid>("TeamId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("UserId");
			b.HasIndex("TeamId", "UserId").IsUnique();
			b.ToTable("project_collaboration_team_members", (string?)null);
		});
		modelBuilder.Entity("GameGuild.TestingLab.FeedbackQualityRating", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("FeedbackId").HasColumnType("uuid");
			b.Property<int>("QualityRating").HasColumnType("integer");
			b.Property<Guid>("RatedByUserId").HasColumnType("uuid");
			b.Property<string>("Reason").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("FeedbackId");
			b.HasIndex("QualityRating");
			b.HasIndex("RatedByUserId");
			b.HasIndex("TenantId");
			b.ToTable("feedback_quality_ratings", (string?)null);
		});
		modelBuilder.Entity("GameGuild.TestingLab.SessionProject", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<string>("Notes").HasColumnType("text");
			b.Property<Guid>("ProjectId").HasColumnType("uuid");
			b.Property<Guid?>("ProjectVersionId").HasColumnType("uuid");
			b.Property<DateTime>("RegisteredAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("RegisteredById").HasColumnType("uuid");
			b.Property<Guid>("SessionId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ProjectId");
			b.HasIndex("ProjectVersionId");
			b.HasIndex("RegisteredById");
			b.HasIndex("TenantId");
			b.HasIndex("SessionId", "ProjectId").IsUnique().HasDatabaseName("IX_session_projects_active_pair")
				.HasFilter("\"DeletedAt\" IS NULL AND \"IsActive\" = TRUE");
			b.ToTable("session_projects", (string?)null);
		});
		modelBuilder.Entity("GameGuild.TestingLab.SessionRegistration", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("AttendanceStatus").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<DateTime?>("CheckedInAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("CheckedOutAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("ConfirmedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Notes").HasColumnType("text");
			b.Property<DateTime>("RegisteredAt").HasColumnType("timestamp with time zone");
			b.Property<string>("RegistrationType").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<Guid>("SessionId").HasColumnType("uuid");
			b.Property<string>("Status").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("RegisteredAt");
			b.HasIndex("RegistrationType");
			b.HasIndex("SessionId");
			b.HasIndex("Status");
			b.HasIndex("TenantId");
			b.HasIndex("UserId");
			b.ToTable("session_registrations", (string?)null);
		});
		modelBuilder.Entity("GameGuild.TestingLab.SessionWaitlist", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Position").HasColumnType("integer");
			b.Property<string>("RegistrationNotes").HasColumnType("text");
			b.Property<string>("RegistrationType").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<Guid>("SessionId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("SessionId");
			b.HasIndex("UserId");
			b.ToTable("session_waitlist", (string?)null);
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingApplicationVote", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("ApplicationId").HasColumnType("uuid");
			b.Property<string>("Comments").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Decision").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("ReviewerId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ReviewerId");
			b.HasIndex("TenantId");
			b.HasIndex("ApplicationId", "ReviewerId").IsUnique().HasDatabaseName("IX_testing_application_votes_active_application_reviewer")
				.HasFilter("\"DeletedAt\" IS NULL");
			b.ToTable("testing_application_votes", (string?)null);
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingCommitteeMember", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("EventId").HasColumnType("uuid");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<bool>("IsChair").HasColumnType("boolean");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("TenantId");
			b.HasIndex("UserId");
			b.HasIndex("EventId", "UserId").IsUnique().HasDatabaseName("IX_testing_committee_members_active_event_user")
				.HasFilter("\"DeletedAt\" IS NULL AND \"IsActive\" = TRUE");
			b.ToTable("testing_committee_members", (string?)null);
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingEvent", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("ApplicationsCloseAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("ApplicationsOpenAt").HasColumnType("timestamp with time zone");
			b.Property<string>("ApprovalMode").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<string>("CancellationReason").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<DateTime?>("CancelledAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("CohortId").HasColumnType("uuid");
			b.Property<Guid?>("CourseId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<DateTime>("EndsAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("LearningActivityId").HasColumnType("uuid");
			b.Property<string>("LearningCompletionRequirement").IsRequired().HasMaxLength(100)
				.HasColumnType("character varying(100)");
			b.Property<Guid>("ManagerUserId").HasColumnType("uuid");
			b.Property<string>("Mode").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<string>("Name").IsRequired().HasMaxLength(255)
				.HasColumnType("character varying(255)");
			b.Property<string>("RecurrenceDaysOfWeek").HasMaxLength(64).HasColumnType("character varying(64)");
			b.Property<DateTime?>("RecurrenceEndsAt").HasColumnType("timestamp with time zone");
			b.Property<string>("RecurrenceFrequency").HasMaxLength(20).HasColumnType("character varying(20)");
			b.Property<int?>("RecurrenceInterval").HasColumnType("integer");
			b.Property<int?>("RecurrenceOccurrence").HasColumnType("integer");
			b.Property<int?>("RecurrenceOccurrenceCount").HasColumnType("integer");
			b.Property<Guid?>("RecurrenceSeriesId").HasColumnType("uuid");
			b.Property<string>("ReminderDaysBeforeOverride").HasMaxLength(64).HasColumnType("character varying(64)");
			b.Property<bool>("RequiresFeedback").HasColumnType("boolean");
			b.Property<string>("SentReminderDays").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTime>("StartsAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Status").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ManagerUserId");
			b.HasIndex("TenantId");
			b.HasIndex("RecurrenceSeriesId", "RecurrenceOccurrence").HasDatabaseName("IX_testing_events_recurrence_series_occurrence");
			b.HasIndex("TenantId", "Status", "StartsAt");
			b.ToTable("testing_events", (string?)null);
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingEventSlot", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("CampusName").HasMaxLength(200).HasColumnType("character varying(200)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("EndsAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("EventId").HasColumnType("uuid");
			b.Property<Guid?>("LocationId").HasColumnType("uuid");
			b.Property<int?>("MaxProjects").HasColumnType("integer");
			b.Property<int?>("MaxTesters").HasColumnType("integer");
			b.Property<string>("MeetingUrl").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<string>("Mode").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<string>("RoomName").HasMaxLength(200).HasColumnType("character varying(200)");
			b.Property<DateTime>("StartsAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("LocationId");
			b.HasIndex("TenantId");
			b.HasIndex("EventId", "StartsAt");
			b.ToTable("testing_event_slots", (string?)null);
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingFeedback", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("AdditionalNotes").HasColumnType("text");
			b.Property<Guid?>("ApplicationId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("EventId").HasColumnType("uuid");
			b.Property<string>("FeedbackData").IsRequired().HasColumnType("text");
			b.Property<Guid?>("FeedbackFormId").HasColumnType("uuid");
			b.Property<bool>("IsReported").HasColumnType("boolean");
			b.Property<int?>("OverallRating").HasColumnType("integer");
			b.Property<string>("QualityRating").HasMaxLength(40).HasColumnType("character varying(40)");
			b.Property<string>("ReportReason").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<DateTime?>("ReportedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("ReportedById").HasColumnType("uuid");
			b.Property<Guid?>("SessionId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("TestingContext").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<Guid?>("TestingParticipantId").HasColumnType("uuid");
			b.Property<Guid?>("TestingRequestId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<bool?>("WouldRecommend").HasColumnType("boolean");
			b.HasKey("Id");
			b.HasIndex("ApplicationId");
			b.HasIndex("FeedbackFormId");
			b.HasIndex("IsReported");
			b.HasIndex("OverallRating");
			b.HasIndex("QualityRating");
			b.HasIndex("ReportedById");
			b.HasIndex("SessionId");
			b.HasIndex("TenantId");
			b.HasIndex("TestingContext");
			b.HasIndex("TestingParticipantId");
			b.HasIndex("TestingRequestId");
			b.HasIndex("UserId");
			b.HasIndex("EventId", "ApplicationId", "UserId");
			b.ToTable("testing_feedback", (string?)null);
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingFeedbackForm", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasColumnType("text");
			b.Property<string>("FormData").IsRequired().HasColumnType("text");
			b.Property<string>("FormType").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<int>("FormVersion").HasColumnType("integer");
			b.Property<bool>("IsActive").HasColumnType("boolean");
			b.Property<bool>("IsForOnline").HasColumnType("boolean");
			b.Property<bool>("IsForSessions").HasColumnType("boolean");
			b.Property<string>("Name").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("Tags").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<Guid?>("TestingRequestId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("FormType");
			b.HasIndex("IsActive");
			b.HasIndex("Name");
			b.HasIndex("TenantId");
			b.HasIndex("TestingRequestId");
			b.ToTable("testing_feedback_forms", (string?)null);
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingFeedbackObligation", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid>("ApplicationId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("EventId").HasColumnType("uuid");
			b.Property<Guid?>("FeedbackId").HasColumnType("uuid");
			b.Property<DateTime?>("FulfilledAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("SlotId").HasColumnType("uuid");
			b.Property<string>("Status").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<Guid>("TesterUserId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("ApplicationId");
			b.HasIndex("EventId");
			b.HasIndex("FeedbackId");
			b.HasIndex("Status");
			b.HasIndex("TenantId");
			b.HasIndex("TesterUserId");
			b.HasIndex("SlotId", "ApplicationId", "TesterUserId").IsUnique().HasDatabaseName("IX_testing_feedback_obligations_active_assignment")
				.HasFilter("\"DeletedAt\" IS NULL");
			b.ToTable("testing_feedback_obligations", (string?)null);
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingLabSettings", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<bool>("AllowPublicSignups").HasColumnType("boolean");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("DefaultSessionDuration").HasColumnType("integer");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<bool>("EnableNotifications").HasColumnType("boolean");
			b.Property<string>("LabName").IsRequired().HasMaxLength(255)
				.HasColumnType("character varying(255)");
			b.Property<int>("MaxSimultaneousSessions").HasColumnType("integer");
			b.Property<string>("ReminderDaysBefore").IsRequired().HasMaxLength(64)
				.HasColumnType("character varying(64)");
			b.Property<bool>("RequireApproval").HasColumnType("boolean");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Timezone").IsRequired().HasMaxLength(50)
				.HasColumnType("character varying(50)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("TenantId");
			b.ToTable("testing_lab_settings", (string?)null);
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingLocation", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("Address").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<int?>("Capacity").HasColumnType("integer");
			b.Property<string>("City").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<string>("ContactEmail").HasMaxLength(255).HasColumnType("character varying(255)");
			b.Property<string>("ContactPhone").HasMaxLength(50).HasColumnType("character varying(50)");
			b.Property<string>("Country").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasColumnType("text");
			b.Property<string>("Equipment").HasColumnType("text");
			b.Property<bool>("IsVirtual").HasColumnType("boolean");
			b.Property<int>("MaxProjectsCapacity").HasColumnType("integer");
			b.Property<string>("Name").IsRequired().HasMaxLength(200)
				.HasColumnType("character varying(200)");
			b.Property<string>("PostalCode").HasMaxLength(20).HasColumnType("character varying(20)");
			b.Property<string>("State").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<string>("Status").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<string>("VirtualUrl").HasMaxLength(500).HasColumnType("character varying(500)");
			b.HasKey("Id");
			b.HasIndex("Capacity");
			b.HasIndex("City");
			b.HasIndex("IsVirtual");
			b.HasIndex("Name");
			b.HasIndex("Status");
			b.HasIndex("TenantId");
			b.ToTable("testing_locations", (string?)null);
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingParticipant", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime?>("CompletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("FeedbackCount").HasColumnType("integer");
			b.Property<bool>("InstructionsAcknowledged").HasColumnType("boolean");
			b.Property<DateTime?>("InstructionsAcknowledgedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Notes").HasColumnType("text");
			b.Property<DateTime>("StartedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Status").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<Guid>("TestingRequestId").HasColumnType("uuid");
			b.Property<int?>("TimeSpentMinutes").HasColumnType("integer");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("CompletedAt");
			b.HasIndex("InstructionsAcknowledged");
			b.HasIndex("StartedAt");
			b.HasIndex("TenantId");
			b.HasIndex("TestingRequestId");
			b.HasIndex("UserId");
			b.ToTable("testing_participants", (string?)null);
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingProjectApplication", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid?>("AssignedSlotId").HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DecidedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("DecidedByUserId").HasColumnType("uuid");
			b.Property<string>("DecisionRationale").HasMaxLength(2000).HasColumnType("character varying(2000)");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("EventId").HasColumnType("uuid");
			b.Property<string>("PreferredAvailability").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<Guid>("ProjectId").HasColumnType("uuid");
			b.Property<Guid?>("ProjectVersionId").HasColumnType("uuid");
			b.Property<string>("Status").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<string>("SubmittedAssetReferenceIdsJson").HasMaxLength(10000).HasColumnType("character varying(10000)");
			b.Property<Guid>("SubmittedByUserId").HasColumnType("uuid");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("AssignedSlotId");
			b.HasIndex("DecidedByUserId");
			b.HasIndex("ProjectId");
			b.HasIndex("ProjectVersionId");
			b.HasIndex("SubmittedByUserId");
			b.HasIndex("TenantId");
			b.HasIndex("EventId", "ProjectId").IsUnique().HasDatabaseName("IX_testing_project_applications_active_event_project")
				.HasFilter("\"DeletedAt\" IS NULL AND \"Status\" NOT IN ('Rejected', 'Withdrawn')");
			b.HasIndex("EventId", "Status");
			b.ToTable("testing_project_applications", (string?)null);
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingRequest", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("CreatedById").HasColumnType("uuid");
			b.Property<int>("CurrentTesterCount").HasColumnType("integer");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Description").HasColumnType("text");
			b.Property<string>("DownloadUrl").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<DateTime>("EndDate").HasColumnType("timestamp with time zone");
			b.Property<int?>("EstimatedDurationHours").HasColumnType("integer");
			b.Property<string>("FeedbackFormContent").HasColumnType("text");
			b.Property<string>("InstructionsContent").HasColumnType("text");
			b.Property<Guid?>("InstructionsFileId").HasColumnType("uuid");
			b.Property<string>("InstructionsType").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<string>("InstructionsUrl").HasMaxLength(500).HasColumnType("character varying(500)");
			b.Property<int?>("MaxTesters").HasColumnType("integer");
			b.Property<string>("Mode").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<string>("Priority").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<Guid?>("ProjectVersionId").HasColumnType("uuid");
			b.Property<DateTime>("StartDate").HasColumnType("timestamp with time zone");
			b.Property<string>("Status").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<string>("Title").IsRequired().HasMaxLength(255)
				.HasColumnType("character varying(255)");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("CreatedById");
			b.HasIndex("EndDate");
			b.HasIndex("InstructionsType");
			b.HasIndex("ProjectVersionId");
			b.HasIndex("StartDate");
			b.HasIndex("Status");
			b.HasIndex("TenantId");
			b.ToTable("testing_requests", (string?)null);
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingSession", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("CreatedById").HasColumnType("uuid");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("EndTime").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("EventSlotId").HasColumnType("uuid");
			b.Property<Guid>("LocationId").HasColumnType("uuid");
			b.Property<Guid>("ManagerId").HasColumnType("uuid");
			b.Property<Guid>("ManagerUserId").HasColumnType("uuid");
			b.Property<int>("MaxProjects").HasColumnType("integer");
			b.Property<int>("MaxTesters").HasColumnType("integer");
			b.Property<int>("RegisteredProjectCount").HasColumnType("integer");
			b.Property<int>("RegisteredProjectMemberCount").HasColumnType("integer");
			b.Property<int>("RegisteredTesterCount").HasColumnType("integer");
			b.Property<DateTime>("SessionDate").HasColumnType("timestamp with time zone");
			b.Property<string>("SessionName").IsRequired().HasMaxLength(255)
				.HasColumnType("character varying(255)");
			b.Property<DateTime>("StartTime").HasColumnType("timestamp with time zone");
			b.Property<string>("Status").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<Guid>("TestingRequestId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("CreatedById");
			b.HasIndex("EventSlotId");
			b.HasIndex("LocationId");
			b.HasIndex("ManagerId");
			b.HasIndex("SessionDate");
			b.HasIndex("Status");
			b.HasIndex("TenantId");
			b.HasIndex("TestingRequestId");
			b.ToTable("testing_sessions", (string?)null);
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingSlotRegistration", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<DateTime?>("CheckedInAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("CheckedOutAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("CompletedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime?>("DeletedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("EventId").HasColumnType("uuid");
			b.Property<string>("Notes").HasMaxLength(1000).HasColumnType("character varying(1000)");
			b.Property<DateTime?>("PromotedAt").HasColumnType("timestamp with time zone");
			b.Property<DateTime>("RegisteredAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("SlotId").HasColumnType("uuid");
			b.Property<string>("Status").IsRequired().HasMaxLength(40)
				.HasColumnType("character varying(40)");
			b.Property<Guid?>("TenantId").HasColumnType("uuid");
			b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("UserId").HasColumnType("uuid");
			b.Property<int>("Version").IsConcurrencyToken().HasColumnType("integer");
			b.Property<int?>("WaitlistPosition").HasColumnType("integer");
			b.HasKey("Id");
			b.HasIndex("EventId");
			b.HasIndex("TenantId");
			b.HasIndex("UserId");
			b.HasIndex("SlotId", "Status");
			b.HasIndex("SlotId", "UserId").IsUnique().HasDatabaseName("IX_testing_slot_registrations_active_slot_user")
				.HasFilter("\"DeletedAt\" IS NULL AND \"Status\" <> 'Cancelled'");
			b.HasIndex("SlotId", "WaitlistPosition").IsUnique().HasDatabaseName("IX_testing_slot_registrations_waitlist_position")
				.HasFilter("\"DeletedAt\" IS NULL AND \"Status\" = 'Waitlisted'");
			b.ToTable("testing_slot_registrations", (string?)null);
		});
		modelBuilder.Entity("GameGuild.TrustSafety.TrustSafetyAppealRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<Guid?>("AssignedTo").HasColumnType("uuid");
			b.Property<DateTimeOffset?>("DecidedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("DecidedBy").HasColumnType("uuid");
			b.Property<string>("DecisionEvidenceHash").HasMaxLength(128).HasColumnType("character varying(128)");
			b.Property<string>("ReasonCode").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<string>("RestrictionReferenceHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<int>("State").HasColumnType("integer");
			b.Property<string>("SubjectHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<string>("SubmissionEvidenceHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTimeOffset>("SubmittedAt").HasColumnType("timestamp with time zone");
			b.Property<Guid>("SubmittedBy").HasColumnType("uuid");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<long>("Version").IsConcurrencyToken().HasColumnType("bigint");
			b.HasKey("Id");
			b.HasIndex("TenantId", "SubjectHash", "State");
			b.ToTable("trust_safety_appeals", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_trust_safety_appeals_decision", "(\"State\" IN (1, 2) AND \"DecidedAt\" IS NULL AND \"DecidedBy\" IS NULL) OR (\"State\" IN (3, 4) AND \"DecidedAt\" IS NOT NULL AND \"DecidedBy\" IS NOT NULL)");
				t.HasCheckConstraint("ck_trust_safety_appeals_version", "\"Version\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.TrustSafety.TrustSafetyEventInboxRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
			b.Property<string>("EventId").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<string>("EvidenceHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTimeOffset>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<DateTimeOffset>("IssuedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("KeyId").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<int>("Kind").HasColumnType("integer");
			b.Property<int>("Outcome").HasColumnType("integer");
			b.Property<string>("PayloadHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<long>("PolicyVersion").HasColumnType("bigint");
			b.Property<DateTimeOffset?>("ProcessedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("ProcessingError").HasMaxLength(100).HasColumnType("character varying(100)");
			b.Property<string>("RawObjectReference").IsRequired().HasMaxLength(2048)
				.HasColumnType("character varying(2048)");
			b.Property<DateTimeOffset>("ReceivedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("Signature").IsRequired().HasMaxLength(2048)
				.HasColumnType("character varying(2048)");
			b.Property<bool>("SignatureVerified").HasColumnType("boolean");
			b.Property<string>("SubjectHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<long>("Version").HasColumnType("bigint");
			b.HasKey("Id");
			b.HasIndex("EventId").IsUnique();
			b.HasIndex("TenantId", "SubjectHash", "Version").IsUnique();
			b.ToTable("trust_safety_event_inbox", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_trust_safety_event_inbox_lifetime", "\"ExpiresAt\" > \"IssuedAt\" AND \"ReceivedAt\" >= \"IssuedAt\"");
				t.HasCheckConstraint("ck_trust_safety_event_inbox_version", "\"Version\" > 0 AND \"PolicyVersion\" > 0");
			});
		});
		modelBuilder.Entity("GameGuild.TrustSafety.TrustSafetySubjectStateRow", delegate(EntityTypeBuilder b)
		{
			b.Property<Guid>("TenantId").HasColumnType("uuid");
			b.Property<string>("SubjectHash").HasMaxLength(128).HasColumnType("character varying(128)");
			b.Property<string>("EvidenceHash").IsRequired().HasMaxLength(128)
				.HasColumnType("character varying(128)");
			b.Property<DateTimeOffset>("ExpiresAt").HasColumnType("timestamp with time zone");
			b.Property<Guid?>("HoldId").HasColumnType("uuid");
			b.Property<DateTimeOffset>("IssuedAt").HasColumnType("timestamp with time zone");
			b.Property<string>("LastEventId").IsRequired().HasMaxLength(256)
				.HasColumnType("character varying(256)");
			b.Property<int>("Outcome").HasColumnType("integer");
			b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
			b.Property<long>("Version").IsConcurrencyToken().HasColumnType("bigint");
			b.HasKey("TenantId", "SubjectHash");
			b.HasIndex("ExpiresAt");
			b.ToTable("trust_safety_subject_states", null, delegate(TableBuilder t)
			{
				t.HasCheckConstraint("ck_trust_safety_subject_states_lifetime", "\"ExpiresAt\" > \"IssuedAt\"");
				t.HasCheckConstraint("ck_trust_safety_subject_states_version", "\"Version\" > 0");
			});
		});
		modelBuilder.Entity("Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.DataProtectionKey", delegate(EntityTypeBuilder b)
		{
			b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("integer");
			b.Property<int>("Id").UseIdentityByDefaultColumn();
			b.Property<string>("FriendlyName").HasColumnType("text");
			b.Property<string>("Xml").HasColumnType("text");
			b.HasKey("Id");
			b.ToTable("DataProtectionKeys");
		});
		modelBuilder.Entity("GameGuild.Analytics.DashboardWidget", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Analytics.Dashboard", "Dashboard").WithMany("Widgets").HasForeignKey("DashboardId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Dashboard");
		});
		modelBuilder.Entity("GameGuild.Assets.AssetReference", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Assets.AssetContent", "Content").WithMany("References").HasForeignKey("AssetContentId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.Assets.AssetFolder", null).WithMany().HasForeignKey("FolderId")
				.OnDelete(DeleteBehavior.SetNull);
			b.Navigation("Content");
		});
		modelBuilder.Entity("GameGuild.Assets.AssetReferenceRevision", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Assets.AssetContent", "Content").WithMany().HasForeignKey("AssetContentId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.Assets.AssetReference", "Reference").WithMany("Revisions").HasForeignKey("AssetReferenceId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Content");
			b.Navigation("Reference");
		});
		modelBuilder.Entity("GameGuild.Assets.AssetReport", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Assets.AssetReference", "Reference").WithMany("Reports").HasForeignKey("AssetReferenceId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Reference");
		});
		modelBuilder.Entity("GameGuild.Assets.AssetScopedAccessGrant", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Assets.AssetReference", null).WithMany().HasForeignKey("AssetReferenceId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Assets.TransformedAsset", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Assets.AssetContent", "SourceContent").WithMany("TransformedVersions").HasForeignKey("SourceContentId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("SourceContent");
		});
		modelBuilder.Entity("GameGuild.Commerce.Orders.Order", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Identity.Users.User", "User").WithMany().HasForeignKey("UserId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("User");
		});
		modelBuilder.Entity("GameGuild.Commerce.Orders.OrderAuditLog", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Commerce.Orders.Order", "Order").WithMany().HasForeignKey("OrderId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Order");
		});
		modelBuilder.Entity("GameGuild.Commerce.Orders.OrderLineItem", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Commerce.Orders.Order", "Order").WithMany("LineItems").HasForeignKey("OrderId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Commerce.Products.Product", "Product").WithMany().HasForeignKey("ProductId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.Commerce.Products.UserProduct", "UserProduct").WithMany().HasForeignKey("UserProductId")
				.OnDelete(DeleteBehavior.SetNull);
			b.Navigation("Order");
			b.Navigation("Product");
			b.Navigation("UserProduct");
		});
		modelBuilder.Entity("GameGuild.Commerce.Payments.TaxJurisdiction", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Commerce.Payments.TaxJurisdiction", "ParentJurisdiction").WithMany("ChildJurisdictions").HasForeignKey("ParentJurisdictionId")
				.OnDelete(DeleteBehavior.Restrict);
			b.Navigation("ParentJurisdiction");
		});
		modelBuilder.Entity("GameGuild.Commerce.Payments.TaxRate", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Commerce.Payments.TaxJurisdiction", "TaxJurisdiction").WithMany().HasForeignKey("TaxJurisdictionId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("TaxJurisdiction");
		});
		modelBuilder.Entity("GameGuild.Commerce.Payments.TaxRule", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Commerce.Payments.TaxRate", "DefaultTaxRate").WithMany().HasForeignKey("DefaultTaxRateId");
			b.HasOne("GameGuild.Commerce.Payments.TaxJurisdiction", "TaxJurisdiction").WithMany("TaxRules").HasForeignKey("TaxJurisdictionId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("DefaultTaxRate");
			b.Navigation("TaxJurisdiction");
		});
		modelBuilder.Entity("GameGuild.Commerce.Payments.WalletTransaction", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Commerce.Payments.UserWallet", "Wallet").WithMany("Transactions").HasForeignKey("WalletId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Wallet");
		});
		modelBuilder.Entity("GameGuild.Commerce.PricingRuleTier", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Commerce.PricingRule", "PricingRule").WithMany("PricingTiers").HasForeignKey("PricingRuleId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("PricingRule");
		});
		modelBuilder.Entity("GameGuild.Commerce.Products.PricingTier", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Commerce.Products.Product", "Product").WithMany().HasForeignKey("ProductId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Product");
		});
		modelBuilder.Entity("GameGuild.Commerce.Products.Product", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Identity.Users.User", "Creator").WithMany().HasForeignKey("CreatorId");
			b.Navigation("Creator");
		});
		modelBuilder.Entity("GameGuild.Commerce.Products.ProductBundleItem", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Commerce.Products.Product", "BundleProduct").WithMany("BundleItems").HasForeignKey("BundleProductId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Commerce.Products.Product", "IncludedProduct").WithMany("IncludedInBundles").HasForeignKey("IncludedProductId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.Navigation("BundleProduct");
			b.Navigation("IncludedProduct");
		});
		modelBuilder.Entity("GameGuild.Commerce.Products.ProductCommissionConfig", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Commerce.Products.Product", "Product").WithOne("CommissionConfig").HasForeignKey("GameGuild.Commerce.Products.ProductCommissionConfig", "ProductId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Product");
		});
		modelBuilder.Entity("GameGuild.Commerce.Products.ProductPricing", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Commerce.Products.Product", "Product").WithMany("Pricing").HasForeignKey("ProductId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Product");
		});
		modelBuilder.Entity("GameGuild.Commerce.Products.ProductPricingVersion", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Commerce.Products.ProductPricing", "ProductPricing").WithMany("Versions").HasForeignKey("ProductPricingId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("ProductPricing");
		});
		modelBuilder.Entity("GameGuild.Commerce.Products.ProductSubscriptionPlan", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Commerce.Products.Product", "Product").WithMany("SubscriptionPlans").HasForeignKey("ProductId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Product");
		});
		modelBuilder.Entity("GameGuild.Commerce.Products.PromoCode", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Identity.Users.User", "CreatedByUser").WithMany().HasForeignKey("CreatedBy")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Commerce.Products.Product", "Product").WithMany("PromoCodes").HasForeignKey("ProductId");
			b.Navigation("CreatedByUser");
			b.Navigation("Product");
		});
		modelBuilder.Entity("GameGuild.Commerce.Products.PromoCodeUse", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Commerce.Products.PromoCode", "PromoCode").WithMany("PromoCodeUses").HasForeignKey("PromoCodeId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Identity.Users.User", "User").WithMany().HasForeignKey("UserId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("PromoCode");
			b.Navigation("User");
		});
		modelBuilder.Entity("GameGuild.Commerce.Products.SupportTicketMessage", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Commerce.Products.SupportTicket", "Ticket").WithMany("Messages").HasForeignKey("TicketId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Ticket");
		});
		modelBuilder.Entity("GameGuild.Commerce.Products.UserProduct", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Identity.Users.User", "GiftedByUser").WithMany().HasForeignKey("GiftedByUserId");
			b.HasOne("GameGuild.Commerce.Products.Product", "Product").WithMany("UserProducts").HasForeignKey("ProductId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Identity.Users.User", "User").WithMany().HasForeignKey("UserId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("GiftedByUser");
			b.Navigation("Product");
			b.Navigation("User");
		});
		modelBuilder.Entity("GameGuild.Commerce.Subscriptions.Subscription", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Commerce.Subscriptions.SubscriptionPlan", "Plan").WithMany("Subscriptions").HasForeignKey("PlanId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.OwnsOne("GameGuild.Money", "Amount", delegate(OwnedNavigationBuilder ownedNavigationBuilder)
			{
				ownedNavigationBuilder.Property<Guid>("SubscriptionId").HasColumnType("uuid");
				ownedNavigationBuilder.Property<decimal>("Amount").HasColumnType("numeric").HasColumnName("Amount");
				ownedNavigationBuilder.Property<string>("Currency").IsRequired().HasMaxLength(3)
					.HasColumnType("character varying(3)")
					.HasColumnName("Currency");
				ownedNavigationBuilder.HasKey("SubscriptionId");
				ownedNavigationBuilder.ToTable("Subscriptions");
				ownedNavigationBuilder.WithOwner().HasForeignKey("SubscriptionId");
			});
			b.Navigation("Amount").IsRequired();
			b.Navigation("Plan");
		});
		modelBuilder.Entity("GameGuild.Compliance.Consent.PolicyVersion", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Compliance.Consent.ConsentPolicy", "ConsentPolicy").WithMany("Versions").HasForeignKey("ConsentPolicyId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("ConsentPolicy");
		});
		modelBuilder.Entity("GameGuild.Compliance.Consent.UserConsent", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Compliance.Consent.PolicyVersion", "PolicyVersion").WithMany().HasForeignKey("PolicyVersionId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.Navigation("PolicyVersion");
		});
		modelBuilder.Entity("GameGuild.Compliance.FinancialCrime.FinancialCrimeCaseEventRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Compliance.FinancialCrime.FinancialCrimeCaseRow", null).WithMany().HasForeignKey("CaseId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Compliance.FinancialCrime.FinancialCrimeDecisionConsumptionRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Compliance.FinancialCrime.FinancialCrimeDecisionRow", null).WithMany().HasForeignKey("DecisionId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Compliance.FinancialCrime.FinancialCrimeDecisionRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Compliance.FinancialCrime.FinancialCrimeCaseRow", null).WithMany().HasForeignKey("CaseId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Compliance.FinancialCrime.FinancialCrimeRegulatoryReferenceRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Compliance.FinancialCrime.FinancialCrimeCaseRow", null).WithMany().HasForeignKey("CaseId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Compliance.FinancialCrime.FinancialCrimeScreeningRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Compliance.FinancialCrime.FinancialCrimeCaseRow", null).WithMany().HasForeignKey("CaseId")
				.OnDelete(DeleteBehavior.Restrict);
		});
		modelBuilder.Entity("GameGuild.Compliance.FinancialCrime.FinancialCrimeTransactionSignalRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Compliance.FinancialCrime.FinancialCrimeCaseRow", null).WithMany().HasForeignKey("CaseId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Content.Pages.Page", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Content.Pages.Page", "ParentPage").WithMany("ChildPages").HasForeignKey("ParentPageId")
				.OnDelete(DeleteBehavior.Restrict);
			b.Navigation("ParentPage");
		});
		modelBuilder.Entity("GameGuild.Content.Pages.PageSection", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Content.Pages.Page", "Page").WithMany("Sections").HasForeignKey("PageId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Page");
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.AdRewards.Persistence.AdRewardCapConsumptionRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.AdRewards.Persistence.AdRewardSessionRow", null).WithMany().HasForeignKey("SessionId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.AdRewards.Persistence.AdRewardPendingClaimRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.AdRewards.Persistence.AdProviderReportRow", null).WithMany().HasForeignKey("ProviderReportId")
				.OnDelete(DeleteBehavior.Restrict);
			b.HasOne("GameGuild.Finance.Economy.AdRewards.Persistence.AdRewardSessionRow", null).WithOne().HasForeignKey("GameGuild.Finance.Economy.AdRewards.Persistence.AdRewardPendingClaimRow", "SessionId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.AdRewards.Persistence.AdRewardPlaybackMilestoneRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.AdRewards.Persistence.AdRewardSessionRow", null).WithMany().HasForeignKey("SessionId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.AdRewards.Persistence.AdRewardProviderBatchClaimRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.AdRewards.Persistence.AdProviderReportRow", null).WithMany().HasForeignKey("ProviderReportId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.Finance.Economy.AdRewards.Persistence.AdRewardSessionRow", null).WithMany().HasForeignKey("SessionId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.AdRewards.Persistence.AdRewardProviderProofInboxRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.AdRewards.Persistence.AdRewardSessionRow", null).WithMany().HasForeignKey("SessionId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.AdRewards.Persistence.AdRewardReconciliationRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.AdRewards.Persistence.AdProviderReportRow", null).WithOne().HasForeignKey("GameGuild.Finance.Economy.AdRewards.Persistence.AdRewardReconciliationRow", "ProviderReportId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.AdRewards.Persistence.AdRewardSessionEventRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.AdRewards.Persistence.AdRewardSessionRow", null).WithMany().HasForeignKey("SessionId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Bounties.Persistence.BountyEscrowFragmentRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Bounties.Persistence.BountyRow", null).WithMany().HasForeignKey("BountyId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Bounties.Persistence.BountyExpirationEventRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Bounties.Persistence.BountyRow", null).WithOne().HasForeignKey("GameGuild.Finance.Economy.Bounties.Persistence.BountyExpirationEventRow", "BountyId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Bounties.Persistence.BountyTerminalEventRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Bounties.Persistence.BountyRow", null).WithOne().HasForeignKey("GameGuild.Finance.Economy.Bounties.Persistence.BountyTerminalEventRow", "BountyId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Marketplace.Persistence.MarketplaceEventRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Marketplace.Persistence.MarketplaceSettlementRow", null).WithMany().HasForeignKey("SettlementId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Marketplace.Persistence.MarketplaceFundingFragmentRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Marketplace.Persistence.MarketplaceSettlementRow", null).WithMany().HasForeignKey("SettlementId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Marketplace.Persistence.MarketplaceOutboxRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Marketplace.Persistence.MarketplaceSettlementRow", null).WithMany().HasForeignKey("SettlementId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Marketplace.Persistence.MarketplaceRefundDebtRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Marketplace.Persistence.MarketplaceRefundRow", null).WithMany().HasForeignKey("RefundId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.Finance.Economy.Marketplace.Persistence.MarketplaceSettlementRow", null).WithMany().HasForeignKey("SettlementId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Marketplace.Persistence.MarketplaceRefundLegRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Marketplace.Persistence.MarketplaceRefundRow", null).WithMany().HasForeignKey("RefundId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.Finance.Economy.Marketplace.Persistence.MarketplaceSettlementRow", null).WithMany().HasForeignKey("SettlementId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Marketplace.Persistence.MarketplaceRefundRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Marketplace.Persistence.MarketplaceSettlementRow", null).WithMany().HasForeignKey("SettlementId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Marketplace.Persistence.MarketplaceSettlementCreditRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Marketplace.Persistence.MarketplaceSettlementRow", null).WithMany().HasForeignKey("SettlementId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Marketplace.Persistence.MarketplaceSettlementLegRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Marketplace.Persistence.MarketplaceSettlementRow", null).WithMany().HasForeignKey("SettlementId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Payouts.PayoutDispatchOutboxRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Payouts.PayoutOperationRow", null).WithMany().HasForeignKey("OperationId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Payouts.PayoutProviderEventRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Payouts.PayoutOperationRow", null).WithMany().HasForeignKey("OperationId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyAccountRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyWalletRow", null).WithMany().HasForeignKey("WalletId")
				.OnDelete(DeleteBehavior.Restrict);
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyAnchorVerificationRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyExternalAnchorRow", null).WithMany().HasForeignKey("ExternalAnchorId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyCapabilityPolicyApprovalRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyCapabilityPolicyRow", null).WithMany().HasForeignKey("PolicyId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyCapabilityReceiptConsumptionRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyCapabilityReceiptRow", null).WithMany().HasForeignKey("ReceiptId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyCapabilityReceiptRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyRiskDecisionRow", null).WithMany().HasForeignKey("RiskDecisionId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyComplianceHoldEventRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyComplianceHoldRow", null).WithMany().HasForeignKey("HoldId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyComplianceOutboxRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyComplianceEvidenceRow", null).WithMany().HasForeignKey("EvidenceId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyCreditLotRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomySourceStampRow", null).WithMany().HasForeignKey("RootSourceStampId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyWalletRow", null).WithMany().HasForeignKey("WalletId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyDispatchSnapshotRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyPostingGroupRow", null).WithMany().HasForeignKey("PostingGroupId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyDisputeFragmentFreezeRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyCreditLotRow", null).WithMany().HasForeignKey("CreditLotId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyProviderDisputeRow", null).WithMany().HasForeignKey("ProviderDisputeReference")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomySourceStampRow", null).WithMany().HasForeignKey("RootSourceStampId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyWalletRow", null).WithMany().HasForeignKey("WalletId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyDisputeFragmentRangeRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyDisputeFragmentFreezeRow", null).WithMany().HasForeignKey("DisputeFragmentFreezeId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyEntityGraphEdgeRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyEntityGraphNodeRow", null).WithMany().HasForeignKey("LeftNodeId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyEntityGraphNodeRow", null).WithMany().HasForeignKey("RightNodeId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyEntryAllocationRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyJournalLineRow", null).WithMany().HasForeignKey("JournalLineId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyCreditLotRow", null).WithMany().HasForeignKey("ParentLotId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyFragmentRootRangeRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyCreditLotRow", null).WithMany().HasForeignKey("CreditLotId")
				.OnDelete(DeleteBehavior.Restrict);
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyEntryAllocationRow", null).WithMany().HasForeignKey("EntryAllocationId")
				.OnDelete(DeleteBehavior.Restrict);
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomySourceStampRow", null).WithMany().HasForeignKey("RootSourceStampId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyFundingClaimRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyPostingGroupRow", null).WithOne().HasForeignKey("GameGuild.Finance.Economy.Persistence.EconomyFundingClaimRow", "PostingGroupId")
				.OnDelete(DeleteBehavior.Restrict);
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyCreditLotRow", null).WithOne().HasForeignKey("GameGuild.Finance.Economy.Persistence.EconomyFundingClaimRow", "RootCreditLotId")
				.OnDelete(DeleteBehavior.Restrict);
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomySourceStampRow", null).WithOne().HasForeignKey("GameGuild.Finance.Economy.Persistence.EconomyFundingClaimRow", "SourceStampId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyWalletRow", null).WithMany().HasForeignKey("WalletId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyHoldEventRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyHoldRow", null).WithMany().HasForeignKey("HoldId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyHoldRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyWalletRow", null).WithMany().HasForeignKey("WalletId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyIdempotencyRecordRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyPostingGroupRow", null).WithMany().HasForeignKey("PostingGroupId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyJournalEntryRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyPostingGroupRow", null).WithMany().HasForeignKey("PostingGroupId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyJournalLineRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyAccountRow", null).WithMany().HasForeignKey("AccountId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyCreditLotRow", null).WithMany().HasForeignKey("CreditLotId")
				.OnDelete(DeleteBehavior.Restrict);
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyJournalEntryRow", null).WithMany().HasForeignKey("JournalEntryId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyWalletRow", null).WithMany().HasForeignKey("WalletId")
				.OnDelete(DeleteBehavior.Restrict);
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyKillSwitchReleaseApprovalRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyKillSwitchRow", null).WithMany().HasForeignKey("KillSwitchId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyLotLineageEdgeRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyCreditLotRow", null).WithMany().HasForeignKey("ChildLotId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyCreditLotRow", null).WithMany().HasForeignKey("ParentLotId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyOutboxMessageRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyPostingGroupRow", null).WithMany().HasForeignKey("PostingGroupId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyPostingGroupRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomySourceStampRow", null).WithMany().HasForeignKey("SourceStampId")
				.OnDelete(DeleteBehavior.Restrict);
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyProjectionGenerationApprovalRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyProjectionGenerationRow", null).WithMany().HasForeignKey("Generation")
				.HasPrincipalKey("Generation")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyProjectionReconciliationEventRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyWalletRow", null).WithMany().HasForeignKey("WalletId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyProviderDisputeEventRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyProviderDisputeRow", null).WithMany().HasForeignKey("ProviderDisputeReference")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomySourceStampRow", null).WithMany().HasForeignKey("SourceStampId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyProviderDisputeRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyWalletRow", null).WithMany().HasForeignKey("ResponsibleWalletId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomySourceStampRow", null).WithMany().HasForeignKey("SourceStampId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyProviderFactAllocationRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyJournalLineRow", null).WithMany().HasForeignKey("JournalLineId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomySourceStampRow", null).WithMany().HasForeignKey("SourceStampId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyReserveAssetAllocationRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyReserveHeadRow", null).WithMany().HasForeignKey("ReserveVersion")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyRiskAuditEvidenceRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyRiskDecisionRow", null).WithMany().HasForeignKey("RiskDecisionId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyRiskCounterReservationRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyRiskCounterRow", null).WithMany().HasForeignKey("RiskCounterId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyRiskDecisionRow", null).WithMany().HasForeignKey("RiskDecisionId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyRiskDecisionConsumptionRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyPostingGroupRow", null).WithMany().HasForeignKey("PostingGroupId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyRiskDecisionRow", null).WithMany().HasForeignKey("RiskDecisionId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyRiskDecisionRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyWalletRow", null).WithMany().HasForeignKey("DestinationWalletId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyWalletRow", null).WithMany().HasForeignKey("SourceWalletId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyRiskReviewCaseRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyRiskReviewCaseRow", null).WithMany().HasForeignKey("AppealOf")
				.OnDelete(DeleteBehavior.Restrict);
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyRiskDecisionRow", null).WithMany().HasForeignKey("RiskDecisionId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyRiskReviewEventRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyRiskReviewCaseRow", null).WithMany().HasForeignKey("RiskReviewCaseId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyRootReversalStateRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomySourceStampRow", null).WithMany().HasForeignKey("RootSourceStampId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomySourceStampEventRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomySourceStampRow", null).WithMany().HasForeignKey("SourceStampId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyWalletBalanceProjectionRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyWalletRow", null).WithOne().HasForeignKey("GameGuild.Finance.Economy.Persistence.EconomyWalletBalanceProjectionRow", "WalletId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyWalletDebtEventRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomySourceStampRow", null).WithMany().HasForeignKey("SourceStampId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyWalletDebtRow", null).WithMany().HasForeignKey("WalletId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyWalletDebtRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyWalletRow", null).WithOne().HasForeignKey("GameGuild.Finance.Economy.Persistence.EconomyWalletDebtRow", "WalletId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Persistence.EconomyWalletProjectionGenerationRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyProjectionGenerationRow", null).WithMany().HasForeignKey("Generation")
				.HasPrincipalKey("Generation")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.Finance.Economy.Persistence.EconomyWalletRow", null).WithMany().HasForeignKey("WalletId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Treasury.AdminWithdrawalAuditEventRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Treasury.AdminWithdrawalRunRow", null).WithMany().HasForeignKey("RunId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Treasury.AdminWithdrawalDispatchOutboxRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Treasury.AdminWithdrawalRunRow", null).WithMany().HasForeignKey("RunId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Finance.Economy.Treasury.AdminWithdrawalProviderEventRow", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Finance.Economy.Treasury.AdminWithdrawalRunRow", null).WithMany().HasForeignKey("RunId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Features.FeatureFlagDependencyLink", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Features.FeatureFlag", "DependsOnFeatureFlag").WithMany().HasForeignKey("DependsOnFeatureFlagId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.Features.FeatureFlag", "FeatureFlag").WithMany().HasForeignKey("FeatureFlagId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("DependsOnFeatureFlag");
			b.Navigation("FeatureFlag");
		});
		modelBuilder.Entity("GameGuild.Features.FeatureFlagTarget", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Features.FeatureFlag", "FeatureFlag").WithMany("Targets").HasForeignKey("FeatureFlagId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("FeatureFlag");
		});
		modelBuilder.Entity("GameGuild.Features.FeatureFlagUsage", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Features.FeatureFlag", "FeatureFlag").WithMany("UsageAnalytics").HasForeignKey("FeatureFlagId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("FeatureFlag");
		});
		modelBuilder.Entity("GameGuild.GameJams.JamScore", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Projects.ProjectJamSubmission", null).WithMany("Scores").HasForeignKey("ProjectJamSubmissionId");
		});
		modelBuilder.Entity("GameGuild.Identity.Authentication.UserRole", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Identity.Authentication.Role", "Role").WithMany().HasForeignKey("RoleId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Role");
		});
		modelBuilder.Entity("GameGuild.Identity.Authorization.AccessReviewItem", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Identity.Authorization.AccessReviewCampaign", "Campaign").WithMany("Items").HasForeignKey("CampaignId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Campaign");
		});
		modelBuilder.Entity("GameGuild.Identity.Authorization.DynamicRole", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Identity.Authorization.DynamicRole", "ParentRole").WithMany("ChildRoles").HasForeignKey("ParentRoleId")
				.OnDelete(DeleteBehavior.Restrict);
			b.Navigation("ParentRole");
		});
		modelBuilder.Entity("GameGuild.Identity.Authorization.DynamicRoleAssignment", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Identity.Authorization.DynamicRole", "Role").WithMany().HasForeignKey("RoleId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Role");
		});
		modelBuilder.Entity("GameGuild.Identity.Authorization.SoDViolation", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Identity.Authorization.SoDRule", "Rule").WithMany("Violations").HasForeignKey("RuleId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Rule");
		});
		modelBuilder.Entity("GameGuild.Identity.Tenants.TenantAuditLog", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Identity.Tenants.Tenant", "Tenant").WithMany().HasForeignKey("TenantId")
				.OnDelete(DeleteBehavior.SetNull);
			b.Navigation("Tenant");
		});
		modelBuilder.Entity("GameGuild.Identity.Tenants.TenantDomain", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Identity.Tenants.Tenant", "Tenant").WithMany("TenantDomains").HasForeignKey("TenantId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Tenant");
		});
		modelBuilder.Entity("GameGuild.Identity.Tenants.TenantMember", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Identity.Tenants.TenantMember", "ParentMember").WithMany("ChildMembers").HasForeignKey("ParentMemberId")
				.OnDelete(DeleteBehavior.Restrict);
			b.HasOne("GameGuild.Identity.Tenants.Tenant", "Tenant").WithMany("TenantMembers").HasForeignKey("TenantId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Identity.Users.User", null).WithMany("TenantMemberships").HasForeignKey("UserId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("ParentMember");
			b.Navigation("Tenant");
		});
		modelBuilder.Entity("GameGuild.Identity.Tenants.TenantMetadata", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Identity.Tenants.Tenant", "Tenant").WithOne().HasForeignKey("GameGuild.Identity.Tenants.TenantMetadata", "TenantId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Tenant");
		});
		modelBuilder.Entity("GameGuild.Identity.Tenants.TenantSettings", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Identity.Tenants.Tenant", "Tenant").WithOne("TenantSettings").HasForeignKey("GameGuild.Identity.Tenants.TenantSettings", "TenantId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Tenant");
		});
		modelBuilder.Entity("GameGuild.Identity.Tenants.TenantStatistics", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Identity.Tenants.Tenant", "Tenant").WithOne("TenantStatistics").HasForeignKey("GameGuild.Identity.Tenants.TenantStatistics", "TenantId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Tenant");
		});
		modelBuilder.Entity("GameGuild.Identity.Tenants.UsageTracking", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Identity.Tenants.Tenant", "Tenant").WithMany("UsageTrackingRecords").HasForeignKey("TenantId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Tenant");
		});
		modelBuilder.Entity("GameGuild.Identity.Users.UserMetadata", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Identity.Users.User", "User").WithOne("Metadata").HasForeignKey("GameGuild.Identity.Users.UserMetadata", "UserId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("User");
		});
		modelBuilder.Entity("GameGuild.Identity.Users.UserNotification", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Identity.Users.User", "User").WithMany("Notifications").HasForeignKey("UserId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("User");
		});
		modelBuilder.Entity("GameGuild.Identity.Users.UserPreferences", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Identity.Users.User", "User").WithOne("Preferences").HasForeignKey("GameGuild.Identity.Users.UserPreferences", "UserId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("User");
		});
		modelBuilder.Entity("GameGuild.Identity.Users.UserProfile", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Identity.Users.User", "User").WithOne("Profile").HasForeignKey("GameGuild.Identity.Users.UserProfile", "UserId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("User");
		});
		modelBuilder.Entity("GameGuild.LaunchPad.LaunchChecklistItem", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.LaunchPad.LaunchPlan", "LaunchPlan").WithMany("ChecklistItems").HasForeignKey("LaunchPlanId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("LaunchPlan");
		});
		modelBuilder.Entity("GameGuild.LaunchPad.LaunchPadApplication", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.LaunchPad.LaunchPadEvent", "LaunchPadEvent").WithMany("Applications").HasForeignKey("LaunchPadEventId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Projects.Project", "Project").WithMany().HasForeignKey("ProjectId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.Projects.ProjectVersion", "ProjectVersion").WithMany().HasForeignKey("ProjectVersionId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.Identity.Users.User", "SubmittedByUser").WithMany().HasForeignKey("SubmittedByUserId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.Navigation("LaunchPadEvent");
			b.Navigation("Project");
			b.Navigation("ProjectVersion");
			b.Navigation("SubmittedByUser");
		});
		modelBuilder.Entity("GameGuild.LaunchPad.LaunchPadParticipantRegistration", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.LaunchPad.LaunchPadParticipantSlot", "LaunchPadParticipantSlot").WithMany("Registrations").HasForeignKey("LaunchPadParticipantSlotId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Identity.Users.User", "User").WithMany().HasForeignKey("UserId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.Navigation("LaunchPadParticipantSlot");
			b.Navigation("User");
		});
		modelBuilder.Entity("GameGuild.LaunchPad.LaunchPadParticipantSlot", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.LaunchPad.LaunchPadEvent", "LaunchPadEvent").WithMany("Slots").HasForeignKey("LaunchPadEventId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("LaunchPadEvent");
		});
		modelBuilder.Entity("GameGuild.LaunchPad.LaunchPlan", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.LaunchPad.LaunchPadApplication", "LaunchPadApplication").WithOne().HasForeignKey("GameGuild.LaunchPad.LaunchPlan", "LaunchPadApplicationId")
				.OnDelete(DeleteBehavior.Restrict);
			b.HasOne("GameGuild.LaunchPad.LaunchPadEvent", "LaunchPadEvent").WithMany().HasForeignKey("LaunchPadEventId")
				.OnDelete(DeleteBehavior.Restrict);
			b.HasOne("GameGuild.Projects.Project", "Project").WithMany().HasForeignKey("ProjectId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Projects.ProjectVersion", "ProjectVersion").WithMany().HasForeignKey("ProjectVersionId")
				.OnDelete(DeleteBehavior.Restrict);
			b.Navigation("LaunchPadApplication");
			b.Navigation("LaunchPadEvent");
			b.Navigation("Project");
			b.Navigation("ProjectVersion");
		});
		modelBuilder.Entity("GameGuild.Learning.Assessments.Assessment", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Learning.Assessments.AssessmentGroup", "AssessmentGroup").WithMany().HasForeignKey("AssessmentGroupId")
				.OnDelete(DeleteBehavior.SetNull);
			b.Navigation("AssessmentGroup");
		});
		modelBuilder.Entity("GameGuild.Learning.Assessments.InteractiveVideoAssessmentCue", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Learning.Assessments.Assessment", "Assessment").WithMany("InteractiveVideoCues").HasForeignKey("AssessmentId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Assessment");
		});
		modelBuilder.Entity("GameGuild.Learning.Certificates.CertificateTag", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Tags.TagProficiency", "TagProficiency").WithMany().HasForeignKey("TagProficiencyId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("TagProficiency");
		});
		modelBuilder.Entity("GameGuild.Learning.Cohorts.CohortSchedule", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Learning.Cohorts.Cohort", null).WithOne().HasForeignKey("GameGuild.Learning.Cohorts.CohortSchedule", "CohortId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Learning.Cohorts.CohortScheduleItem", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Learning.Cohorts.CohortSchedule", null).WithMany().HasForeignKey("CohortId")
				.HasPrincipalKey("CohortId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Learning.Courses.ProgramContent", null).WithMany().HasForeignKey("ProgramContentId")
				.OnDelete(DeleteBehavior.Restrict);
		});
		modelBuilder.Entity("GameGuild.Learning.Courses.ActivityGrade", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Learning.Courses.ContentInteraction", "ContentInteraction").WithMany("ActivityGrades").HasForeignKey("ContentInteractionId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Identity.Users.User", "Grader").WithMany().HasForeignKey("GraderId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Learning.Courses.ProgramUser", "GraderProgramUser").WithMany("GivenGrades").HasForeignKey("GraderProgramUserId")
				.OnDelete(DeleteBehavior.Restrict);
			b.HasOne("GameGuild.Learning.Courses.ProgramUser", "ProgramUser").WithMany("ReceivedGrades").HasForeignKey("ProgramUserId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Identity.Users.User", "Student").WithMany().HasForeignKey("StudentId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("ContentInteraction");
			b.Navigation("Grader");
			b.Navigation("GraderProgramUser");
			b.Navigation("ProgramUser");
			b.Navigation("Student");
		});
		modelBuilder.Entity("GameGuild.Learning.Courses.ContentInteraction", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Learning.Courses.ProgramContent", "Content").WithMany().HasForeignKey("ContentId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Learning.Courses.ProgramContent", null).WithMany("ContentInteractions").HasForeignKey("ProgramContentId");
			b.HasOne("GameGuild.Learning.Courses.ProgramUser", "ProgramUser").WithMany("ContentInteractions").HasForeignKey("ProgramUserId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Identity.Users.User", "User").WithMany().HasForeignKey("UserId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Content");
			b.Navigation("ProgramUser");
			b.Navigation("User");
		});
		modelBuilder.Entity("GameGuild.Learning.Courses.ContentInteractionEvent", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Learning.Courses.ContentInteraction", "Interaction").WithMany("Events").HasForeignKey("InteractionId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Interaction");
		});
		modelBuilder.Entity("GameGuild.Learning.Courses.ContentProgress", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Learning.Courses.ProgramContent", "Content").WithMany().HasForeignKey("ContentId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Learning.Courses.ProgramEnrollment", "ProgramEnrollment").WithMany().HasForeignKey("ProgramEnrollmentId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Identity.Users.User", "User").WithMany().HasForeignKey("UserId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Content");
			b.Navigation("ProgramEnrollment");
			b.Navigation("User");
		});
		modelBuilder.Entity("GameGuild.Learning.Courses.CoursePrerequisite", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Learning.Courses.Program", "Course").WithMany().HasForeignKey("CourseId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Learning.Courses.Program", "PrerequisiteCourse").WithMany().HasForeignKey("PrerequisiteCourseId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.Navigation("Course");
			b.Navigation("PrerequisiteCourse");
		});
		modelBuilder.Entity("GameGuild.Learning.Courses.ProductProgram", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Commerce.Products.Product", null).WithMany().HasForeignKey("ProductId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Learning.Courses.Program", "Program").WithMany().HasForeignKey("ProgramId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Program");
		});
		modelBuilder.Entity("GameGuild.Learning.Courses.ProgramContent", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Learning.Courses.ProgramContent", "Parent").WithMany("Children").HasForeignKey("ParentId")
				.OnDelete(DeleteBehavior.Restrict);
			b.HasOne("GameGuild.Learning.Courses.Program", "Program").WithMany("ProgramContents").HasForeignKey("ProgramId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Parent");
			b.Navigation("Program");
		});
		modelBuilder.Entity("GameGuild.Learning.Courses.ProgramEnrollment", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Learning.Courses.Program", "Program").WithMany().HasForeignKey("ProgramId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Identity.Users.User", "User").WithMany().HasForeignKey("UserId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Program");
			b.Navigation("User");
		});
		modelBuilder.Entity("GameGuild.Learning.Courses.ProgramRating", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Learning.Courses.Program", "Program").WithMany("ProgramRatings").HasForeignKey("ProgramId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Learning.Courses.ProgramUser", "ProgramUser").WithMany("ProgramRatings").HasForeignKey("ProgramUserId")
				.OnDelete(DeleteBehavior.SetNull);
			b.Navigation("Program");
			b.Navigation("ProgramUser");
		});
		modelBuilder.Entity("GameGuild.Learning.Courses.ProgramUser", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Learning.Courses.Program", "Program").WithMany("ProgramUsers").HasForeignKey("ProgramId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Identity.Users.User", "User").WithMany().HasForeignKey("UserId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Program");
			b.Navigation("User");
		});
		modelBuilder.Entity("GameGuild.Learning.Courses.ProgramWishlist", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Learning.Courses.Program", "Program").WithMany("ProgramWishlists").HasForeignKey("ProgramId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Identity.Users.User", "User").WithMany().HasForeignKey("UserId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Program");
			b.Navigation("User");
		});
		modelBuilder.Entity("GameGuild.Learning.Experience.LearningPaths.LearningPathCourse", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Learning.Experience.LearningPaths.LearningPath", null).WithMany("Courses").HasForeignKey("LearningPathId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Localization.ResourceLocalization", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Assets.AssetReference", null).WithMany("Localizations").HasForeignKey("AssetReferenceId");
			b.HasOne("GameGuild.Localization.Language", "Language").WithMany("ResourceLocalizations").HasForeignKey("LanguageId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Language");
		});
		modelBuilder.Entity("GameGuild.Monitoring.SLA.ServiceLevelIndicator", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Monitoring.SLA.ServiceLevelObjective", "ServiceLevelObjective").WithMany("Indicators").HasForeignKey("ServiceLevelObjectiveId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("ServiceLevelObjective");
		});
		modelBuilder.Entity("GameGuild.Monitoring.SLA.SloViolation", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Monitoring.SLA.ServiceLevelObjective", "ServiceLevelObjective").WithMany("Violations").HasForeignKey("ServiceLevelObjectiveId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("ServiceLevelObjective");
		});
		modelBuilder.Entity("GameGuild.Notifications.Notification", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Notifications.NotificationTemplate", "Template").WithMany().HasForeignKey("TemplateId")
				.OnDelete(DeleteBehavior.SetNull);
			b.Navigation("Template");
		});
		modelBuilder.Entity("GameGuild.ProjectWork.ProjectBoard", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Projects.Project", null).WithMany().HasForeignKey("ProjectId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.ProjectWork.ProjectMilestone", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Projects.Project", null).WithMany().HasForeignKey("ProjectId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.ProjectWork.ProjectTaskChecklistItem", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.ProjectWork.ProjectWorkTask", "Task").WithMany("Checklist").HasForeignKey("TaskId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Task");
		});
		modelBuilder.Entity("GameGuild.ProjectWork.ProjectTaskComment", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.ProjectWork.ProjectWorkTask", "Task").WithMany("Comments").HasForeignKey("TaskId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Task");
		});
		modelBuilder.Entity("GameGuild.ProjectWork.ProjectTaskDependency", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.ProjectWork.ProjectWorkTask", null).WithMany().HasForeignKey("DependsOnTaskId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.ProjectWork.ProjectWorkTask", null).WithMany().HasForeignKey("TaskId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.ProjectWork.ProjectTaskLabel", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Projects.Project", null).WithMany().HasForeignKey("ProjectId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.ProjectWork.ProjectTaskLabelAssignment", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.ProjectWork.ProjectTaskLabel", null).WithMany().HasForeignKey("LabelId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.ProjectWork.ProjectWorkTask", null).WithMany().HasForeignKey("TaskId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.ProjectWork.ProjectWorkColumn", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.ProjectWork.ProjectBoard", "Board").WithMany("Columns").HasForeignKey("BoardId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Board");
		});
		modelBuilder.Entity("GameGuild.ProjectWork.ProjectWorkHistory", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Projects.Project", null).WithMany().HasForeignKey("ProjectId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.ProjectWork.ProjectWorkTask", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Identity.Users.User", "AssigneeUser").WithMany().HasForeignKey("AssigneeUserId")
				.OnDelete(DeleteBehavior.SetNull);
			b.HasOne("GameGuild.ProjectWork.ProjectWorkColumn", "Column").WithMany("Tasks").HasForeignKey("ColumnId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.ProjectWork.ProjectMilestone", "Milestone").WithMany("Tasks").HasForeignKey("MilestoneId")
				.OnDelete(DeleteBehavior.SetNull);
			b.HasOne("GameGuild.Projects.Project", null).WithMany().HasForeignKey("ProjectId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("AssigneeUser");
			b.Navigation("Column");
			b.Navigation("Milestone");
		});
		modelBuilder.Entity("GameGuild.Projects.Project", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Projects.ProjectCategory", "Category").WithMany().HasForeignKey("CategoryId")
				.OnDelete(DeleteBehavior.SetNull);
			b.HasOne("GameGuild.Identity.Users.User", "CreatedBy").WithMany().HasForeignKey("CreatedById")
				.OnDelete(DeleteBehavior.SetNull);
			b.HasOne("GameGuild.Projects.ProjectCategory", null).WithMany("Projects").HasForeignKey("ProjectCategoryId");
			b.Navigation("Category");
			b.Navigation("CreatedBy");
		});
		modelBuilder.Entity("GameGuild.Projects.ProjectCollaborator", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Projects.Project", "Project").WithMany("Collaborators").HasForeignKey("ProjectId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Identity.Users.User", "User").WithMany().HasForeignKey("UserId")
				.OnDelete(DeleteBehavior.SetNull);
			b.Navigation("Project");
			b.Navigation("User");
		});
		modelBuilder.Entity("GameGuild.Projects.ProjectFeedback", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Projects.Project", "Project").WithMany("Feedbacks").HasForeignKey("ProjectId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Identity.Users.User", "User").WithMany().HasForeignKey("UserId")
				.OnDelete(DeleteBehavior.SetNull);
			b.Navigation("Project");
			b.Navigation("User");
		});
		modelBuilder.Entity("GameGuild.Projects.ProjectFollower", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Projects.Project", "Project").WithMany("Followers").HasForeignKey("ProjectId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Identity.Users.User", "User").WithMany().HasForeignKey("UserId")
				.OnDelete(DeleteBehavior.SetNull);
			b.Navigation("Project");
			b.Navigation("User");
		});
		modelBuilder.Entity("GameGuild.Projects.ProjectInvitation", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Identity.Users.User", "InvitedByUser").WithMany().HasForeignKey("InvitedByUserId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.Identity.Users.User", "InvitedUser").WithMany().HasForeignKey("InvitedUserId")
				.OnDelete(DeleteBehavior.SetNull);
			b.HasOne("GameGuild.Projects.Project", "Project").WithMany().HasForeignKey("ProjectId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("InvitedByUser");
			b.Navigation("InvitedUser");
			b.Navigation("Project");
		});
		modelBuilder.Entity("GameGuild.Projects.ProjectJamSubmission", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.GameJams.Jam", "Jam").WithMany().HasForeignKey("JamId")
				.OnDelete(DeleteBehavior.SetNull);
			b.HasOne("GameGuild.Projects.Project", "Project").WithMany("JamSubmissions").HasForeignKey("ProjectId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Jam");
			b.Navigation("Project");
		});
		modelBuilder.Entity("GameGuild.Projects.ProjectMemberAllocation", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Projects.Project", "Project").WithMany("Allocations").HasForeignKey("ProjectId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Projects.ProjectTeam", "ProjectTeam").WithMany("Allocations").HasForeignKey("ProjectTeamId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Identity.Users.User", "User").WithMany().HasForeignKey("UserId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.Navigation("Project");
			b.Navigation("ProjectTeam");
			b.Navigation("User");
		});
		modelBuilder.Entity("GameGuild.Projects.ProjectMetadata", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Projects.Project", "Project").WithOne("ProjectMetadata").HasForeignKey("GameGuild.Projects.ProjectMetadata", "ProjectId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Project");
		});
		modelBuilder.Entity("GameGuild.Projects.ProjectRelease", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Projects.Project", "Project").WithMany("Releases").HasForeignKey("ProjectId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Project");
		});
		modelBuilder.Entity("GameGuild.Projects.ProjectStoreProduct", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Commerce.Products.Product", "Product").WithMany().HasForeignKey("ProductId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Projects.Project", "Project").WithMany().HasForeignKey("ProjectId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.Navigation("Product");
			b.Navigation("Project");
		});
		modelBuilder.Entity("GameGuild.Projects.ProjectTeam", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Projects.Project", "Project").WithMany("Teams").HasForeignKey("ProjectId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Teams.Team", "Team").WithMany().HasForeignKey("TeamId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.Navigation("Project");
			b.Navigation("Team");
		});
		modelBuilder.Entity("GameGuild.Projects.ProjectTeamAgreement", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Projects.Project", null).WithMany("TeamAgreements").HasForeignKey("ProjectId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Teams.Team", null).WithMany().HasForeignKey("ProposingTeamId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.Teams.Team", null).WithMany().HasForeignKey("ReceivingTeamId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Projects.ProjectVersion", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Identity.Users.User", "CreatedBy").WithMany().HasForeignKey("CreatedById")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Projects.Project", "Project").WithMany("Versions").HasForeignKey("ProjectId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("CreatedBy");
			b.Navigation("Project");
		});
		modelBuilder.Entity("GameGuild.Resources.SlaImpactAnalysis", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Resources.ResourceQuota", "ResourceQuota").WithMany().HasForeignKey("ResourceQuotaId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.Navigation("ResourceQuota");
		});
		modelBuilder.Entity("GameGuild.Social.Groups.SocialGroupMember", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Social.Groups.SocialGroup", null).WithMany().HasForeignKey("GroupId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.Social.Profiles.ProfilePortfolioItem", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Social.Profiles.SocialProfile", "Profile").WithMany("PortfolioItems").HasForeignKey("ProfileId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Profile");
		});
		modelBuilder.Entity("GameGuild.Social.Profiles.ProfileSkill", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Social.Profiles.SocialProfile", "Profile").WithMany("Skills").HasForeignKey("ProfileId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Profile");
		});
		modelBuilder.Entity("GameGuild.Tags.TagRelationship", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Tags.Tag", "Source").WithMany("SourceRelationships").HasForeignKey("SourceId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Tags.Tag", "Target").WithMany("TargetRelationships").HasForeignKey("TargetId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Source");
			b.Navigation("Target");
		});
		modelBuilder.Entity("GameGuild.Teams.TeamInvitation", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Teams.Team", "Team").WithMany("Invitations").HasForeignKey("TeamId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Team");
		});
		modelBuilder.Entity("GameGuild.Teams.TeamMember", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Teams.Team", "Team").WithMany("Members").HasForeignKey("TeamId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Identity.Users.User", "User").WithMany().HasForeignKey("UserId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.Navigation("Team");
			b.Navigation("User");
		});
		modelBuilder.Entity("GameGuild.TestingLab.FeedbackQualityRating", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.TestingLab.TestingFeedback", "Feedback").WithMany("QualityRatings").HasForeignKey("FeedbackId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Identity.Users.User", "RatedBy").WithMany().HasForeignKey("RatedByUserId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.Navigation("Feedback");
			b.Navigation("RatedBy");
		});
		modelBuilder.Entity("GameGuild.TestingLab.SessionProject", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Projects.Project", "Project").WithMany().HasForeignKey("ProjectId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.Projects.ProjectVersion", "ProjectVersion").WithMany().HasForeignKey("ProjectVersionId")
				.OnDelete(DeleteBehavior.SetNull);
			b.HasOne("GameGuild.Identity.Users.User", "RegisteredBy").WithMany().HasForeignKey("RegisteredById")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.TestingLab.TestingSession", "Session").WithMany().HasForeignKey("SessionId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("Project");
			b.Navigation("ProjectVersion");
			b.Navigation("RegisteredBy");
			b.Navigation("Session");
		});
		modelBuilder.Entity("GameGuild.TestingLab.SessionRegistration", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.TestingLab.TestingSession", "Session").WithMany("Registrations").HasForeignKey("SessionId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Identity.Users.User", "User").WithMany().HasForeignKey("UserId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.Navigation("Session");
			b.Navigation("User");
		});
		modelBuilder.Entity("GameGuild.TestingLab.SessionWaitlist", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.TestingLab.TestingSession", "Session").WithMany().HasForeignKey("SessionId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Identity.Users.User", "User").WithMany().HasForeignKey("UserId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.Navigation("Session");
			b.Navigation("User");
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingApplicationVote", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.TestingLab.TestingProjectApplication", "Application").WithMany("Votes").HasForeignKey("ApplicationId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Identity.Users.User", "Reviewer").WithMany().HasForeignKey("ReviewerId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.Navigation("Application");
			b.Navigation("Reviewer");
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingCommitteeMember", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.TestingLab.TestingEvent", "Event").WithMany("CommitteeMembers").HasForeignKey("EventId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Identity.Users.User", "User").WithMany().HasForeignKey("UserId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.Navigation("Event");
			b.Navigation("User");
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingEvent", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Identity.Users.User", "Manager").WithMany().HasForeignKey("ManagerUserId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.Navigation("Manager");
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingEventSlot", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.TestingLab.TestingEvent", "Event").WithMany("Slots").HasForeignKey("EventId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.TestingLab.TestingLocation", "Location").WithMany().HasForeignKey("LocationId")
				.OnDelete(DeleteBehavior.Restrict);
			b.Navigation("Event");
			b.Navigation("Location");
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingFeedback", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.TestingLab.TestingProjectApplication", "Application").WithMany().HasForeignKey("ApplicationId")
				.OnDelete(DeleteBehavior.Restrict);
			b.HasOne("GameGuild.TestingLab.TestingEvent", "Event").WithMany().HasForeignKey("EventId")
				.OnDelete(DeleteBehavior.Restrict);
			b.HasOne("GameGuild.TestingLab.TestingFeedbackForm", "FeedbackForm").WithMany("Feedback").HasForeignKey("FeedbackFormId")
				.OnDelete(DeleteBehavior.Restrict);
			b.HasOne("GameGuild.Identity.Users.User", "ReportedBy").WithMany().HasForeignKey("ReportedById")
				.OnDelete(DeleteBehavior.SetNull);
			b.HasOne("GameGuild.TestingLab.TestingSession", "Session").WithMany("Feedback").HasForeignKey("SessionId")
				.OnDelete(DeleteBehavior.SetNull);
			b.HasOne("GameGuild.TestingLab.TestingParticipant", null).WithMany("Feedback").HasForeignKey("TestingParticipantId");
			b.HasOne("GameGuild.TestingLab.TestingRequest", "TestingRequest").WithMany("Feedback").HasForeignKey("TestingRequestId")
				.OnDelete(DeleteBehavior.Cascade);
			b.HasOne("GameGuild.Identity.Users.User", "User").WithMany().HasForeignKey("UserId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.Navigation("Application");
			b.Navigation("Event");
			b.Navigation("FeedbackForm");
			b.Navigation("ReportedBy");
			b.Navigation("Session");
			b.Navigation("TestingRequest");
			b.Navigation("User");
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingFeedbackForm", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.TestingLab.TestingRequest", null).WithMany("FeedbackForms").HasForeignKey("TestingRequestId")
				.OnDelete(DeleteBehavior.SetNull);
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingFeedbackObligation", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.TestingLab.TestingProjectApplication", null).WithMany().HasForeignKey("ApplicationId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.TestingLab.TestingEvent", null).WithMany().HasForeignKey("EventId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.TestingLab.TestingFeedback", null).WithMany().HasForeignKey("FeedbackId")
				.OnDelete(DeleteBehavior.SetNull);
			b.HasOne("GameGuild.TestingLab.TestingEventSlot", null).WithMany().HasForeignKey("SlotId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Identity.Users.User", null).WithMany().HasForeignKey("TesterUserId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingLabSettings", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Identity.Tenants.Tenant", "Tenant").WithMany().HasForeignKey("TenantId")
				.OnDelete(DeleteBehavior.Cascade);
			b.Navigation("Tenant");
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingParticipant", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.TestingLab.TestingRequest", "TestingRequest").WithMany("Participants").HasForeignKey("TestingRequestId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Identity.Users.User", "User").WithMany().HasForeignKey("UserId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.Navigation("TestingRequest");
			b.Navigation("User");
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingProjectApplication", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.TestingLab.TestingEventSlot", "AssignedSlot").WithMany().HasForeignKey("AssignedSlotId")
				.OnDelete(DeleteBehavior.Restrict);
			b.HasOne("GameGuild.Identity.Users.User", "DecidedBy").WithMany().HasForeignKey("DecidedByUserId")
				.OnDelete(DeleteBehavior.Restrict);
			b.HasOne("GameGuild.TestingLab.TestingEvent", "Event").WithMany("Applications").HasForeignKey("EventId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Projects.Project", "Project").WithMany().HasForeignKey("ProjectId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.Projects.ProjectVersion", "ProjectVersion").WithMany().HasForeignKey("ProjectVersionId")
				.OnDelete(DeleteBehavior.SetNull);
			b.HasOne("GameGuild.Identity.Users.User", "SubmittedBy").WithMany().HasForeignKey("SubmittedByUserId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.Navigation("AssignedSlot");
			b.Navigation("DecidedBy");
			b.Navigation("Event");
			b.Navigation("Project");
			b.Navigation("ProjectVersion");
			b.Navigation("SubmittedBy");
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingRequest", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Identity.Users.User", "CreatedBy").WithMany().HasForeignKey("CreatedById")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.Projects.ProjectVersion", "ProjectVersion").WithMany().HasForeignKey("ProjectVersionId")
				.OnDelete(DeleteBehavior.SetNull);
			b.Navigation("CreatedBy");
			b.Navigation("ProjectVersion");
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingSession", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.Identity.Users.User", "CreatedBy").WithMany().HasForeignKey("CreatedById")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.TestingLab.TestingEventSlot", "EventSlot").WithMany().HasForeignKey("EventSlotId")
				.OnDelete(DeleteBehavior.SetNull);
			b.HasOne("GameGuild.TestingLab.TestingLocation", "Location").WithMany("Sessions").HasForeignKey("LocationId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.Identity.Users.User", "Manager").WithMany().HasForeignKey("ManagerId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.HasOne("GameGuild.TestingLab.TestingRequest", "TestingRequest").WithMany("Sessions").HasForeignKey("TestingRequestId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.Navigation("CreatedBy");
			b.Navigation("EventSlot");
			b.Navigation("Location");
			b.Navigation("Manager");
			b.Navigation("TestingRequest");
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingSlotRegistration", delegate(EntityTypeBuilder b)
		{
			b.HasOne("GameGuild.TestingLab.TestingEvent", "Event").WithMany().HasForeignKey("EventId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.TestingLab.TestingEventSlot", "Slot").WithMany().HasForeignKey("SlotId")
				.OnDelete(DeleteBehavior.Cascade)
				.IsRequired();
			b.HasOne("GameGuild.Identity.Users.User", "User").WithMany().HasForeignKey("UserId")
				.OnDelete(DeleteBehavior.Restrict)
				.IsRequired();
			b.Navigation("Event");
			b.Navigation("Slot");
			b.Navigation("User");
		});
		modelBuilder.Entity("GameGuild.Analytics.Dashboard", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Widgets");
		});
		modelBuilder.Entity("GameGuild.Assets.AssetContent", delegate(EntityTypeBuilder b)
		{
			b.Navigation("References");
			b.Navigation("TransformedVersions");
		});
		modelBuilder.Entity("GameGuild.Assets.AssetReference", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Localizations");
			b.Navigation("Reports");
			b.Navigation("Revisions");
		});
		modelBuilder.Entity("GameGuild.Commerce.Orders.Order", delegate(EntityTypeBuilder b)
		{
			b.Navigation("LineItems");
		});
		modelBuilder.Entity("GameGuild.Commerce.Payments.TaxJurisdiction", delegate(EntityTypeBuilder b)
		{
			b.Navigation("ChildJurisdictions");
			b.Navigation("TaxRules");
		});
		modelBuilder.Entity("GameGuild.Commerce.Payments.UserWallet", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Transactions");
		});
		modelBuilder.Entity("GameGuild.Commerce.PricingRule", delegate(EntityTypeBuilder b)
		{
			b.Navigation("PricingTiers");
		});
		modelBuilder.Entity("GameGuild.Commerce.Products.Product", delegate(EntityTypeBuilder b)
		{
			b.Navigation("BundleItems");
			b.Navigation("CommissionConfig");
			b.Navigation("IncludedInBundles");
			b.Navigation("Pricing");
			b.Navigation("PromoCodes");
			b.Navigation("SubscriptionPlans");
			b.Navigation("UserProducts");
		});
		modelBuilder.Entity("GameGuild.Commerce.Products.ProductPricing", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Versions");
		});
		modelBuilder.Entity("GameGuild.Commerce.Products.PromoCode", delegate(EntityTypeBuilder b)
		{
			b.Navigation("PromoCodeUses");
		});
		modelBuilder.Entity("GameGuild.Commerce.Products.SupportTicket", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Messages");
		});
		modelBuilder.Entity("GameGuild.Commerce.Subscriptions.SubscriptionPlan", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Subscriptions");
		});
		modelBuilder.Entity("GameGuild.Compliance.Consent.ConsentPolicy", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Versions");
		});
		modelBuilder.Entity("GameGuild.Content.Pages.Page", delegate(EntityTypeBuilder b)
		{
			b.Navigation("ChildPages");
			b.Navigation("Sections");
		});
		modelBuilder.Entity("GameGuild.Features.FeatureFlag", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Targets");
			b.Navigation("UsageAnalytics");
		});
		modelBuilder.Entity("GameGuild.Identity.Authorization.AccessReviewCampaign", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Items");
		});
		modelBuilder.Entity("GameGuild.Identity.Authorization.DynamicRole", delegate(EntityTypeBuilder b)
		{
			b.Navigation("ChildRoles");
		});
		modelBuilder.Entity("GameGuild.Identity.Authorization.SoDRule", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Violations");
		});
		modelBuilder.Entity("GameGuild.Identity.Tenants.Tenant", delegate(EntityTypeBuilder b)
		{
			b.Navigation("TenantDomains");
			b.Navigation("TenantMembers");
			b.Navigation("TenantSettings");
			b.Navigation("TenantStatistics");
			b.Navigation("UsageTrackingRecords");
		});
		modelBuilder.Entity("GameGuild.Identity.Tenants.TenantMember", delegate(EntityTypeBuilder b)
		{
			b.Navigation("ChildMembers");
		});
		modelBuilder.Entity("GameGuild.Identity.Users.User", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Metadata");
			b.Navigation("Notifications");
			b.Navigation("Preferences");
			b.Navigation("Profile");
			b.Navigation("TenantMemberships");
		});
		modelBuilder.Entity("GameGuild.LaunchPad.LaunchPadEvent", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Applications");
			b.Navigation("Slots");
		});
		modelBuilder.Entity("GameGuild.LaunchPad.LaunchPadParticipantSlot", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Registrations");
		});
		modelBuilder.Entity("GameGuild.LaunchPad.LaunchPlan", delegate(EntityTypeBuilder b)
		{
			b.Navigation("ChecklistItems");
		});
		modelBuilder.Entity("GameGuild.Learning.Assessments.Assessment", delegate(EntityTypeBuilder b)
		{
			b.Navigation("InteractiveVideoCues");
		});
		modelBuilder.Entity("GameGuild.Learning.Courses.ContentInteraction", delegate(EntityTypeBuilder b)
		{
			b.Navigation("ActivityGrades");
			b.Navigation("Events");
		});
		modelBuilder.Entity("GameGuild.Learning.Courses.Program", delegate(EntityTypeBuilder b)
		{
			b.Navigation("ProgramContents");
			b.Navigation("ProgramRatings");
			b.Navigation("ProgramUsers");
			b.Navigation("ProgramWishlists");
		});
		modelBuilder.Entity("GameGuild.Learning.Courses.ProgramContent", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Children");
			b.Navigation("ContentInteractions");
		});
		modelBuilder.Entity("GameGuild.Learning.Courses.ProgramUser", delegate(EntityTypeBuilder b)
		{
			b.Navigation("ContentInteractions");
			b.Navigation("GivenGrades");
			b.Navigation("ProgramRatings");
			b.Navigation("ReceivedGrades");
		});
		modelBuilder.Entity("GameGuild.Learning.Experience.LearningPaths.LearningPath", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Courses");
		});
		modelBuilder.Entity("GameGuild.Localization.Language", delegate(EntityTypeBuilder b)
		{
			b.Navigation("ResourceLocalizations");
		});
		modelBuilder.Entity("GameGuild.Monitoring.SLA.ServiceLevelObjective", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Indicators");
			b.Navigation("Violations");
		});
		modelBuilder.Entity("GameGuild.ProjectWork.ProjectBoard", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Columns");
		});
		modelBuilder.Entity("GameGuild.ProjectWork.ProjectMilestone", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Tasks");
		});
		modelBuilder.Entity("GameGuild.ProjectWork.ProjectWorkColumn", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Tasks");
		});
		modelBuilder.Entity("GameGuild.ProjectWork.ProjectWorkTask", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Checklist");
			b.Navigation("Comments");
		});
		modelBuilder.Entity("GameGuild.Projects.Project", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Allocations");
			b.Navigation("Collaborators");
			b.Navigation("Feedbacks");
			b.Navigation("Followers");
			b.Navigation("JamSubmissions");
			b.Navigation("ProjectMetadata");
			b.Navigation("Releases");
			b.Navigation("TeamAgreements");
			b.Navigation("Teams");
			b.Navigation("Versions");
		});
		modelBuilder.Entity("GameGuild.Projects.ProjectCategory", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Projects");
		});
		modelBuilder.Entity("GameGuild.Projects.ProjectJamSubmission", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Scores");
		});
		modelBuilder.Entity("GameGuild.Projects.ProjectTeam", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Allocations");
		});
		modelBuilder.Entity("GameGuild.Social.Profiles.SocialProfile", delegate(EntityTypeBuilder b)
		{
			b.Navigation("PortfolioItems");
			b.Navigation("Skills");
		});
		modelBuilder.Entity("GameGuild.Tags.Tag", delegate(EntityTypeBuilder b)
		{
			b.Navigation("SourceRelationships");
			b.Navigation("TargetRelationships");
		});
		modelBuilder.Entity("GameGuild.Teams.Team", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Invitations");
			b.Navigation("Members");
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingEvent", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Applications");
			b.Navigation("CommitteeMembers");
			b.Navigation("Slots");
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingFeedback", delegate(EntityTypeBuilder b)
		{
			b.Navigation("QualityRatings");
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingFeedbackForm", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Feedback");
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingLocation", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Sessions");
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingParticipant", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Feedback");
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingProjectApplication", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Votes");
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingRequest", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Feedback");
			b.Navigation("FeedbackForms");
			b.Navigation("Participants");
			b.Navigation("Sessions");
		});
		modelBuilder.Entity("GameGuild.TestingLab.TestingSession", delegate(EntityTypeBuilder b)
		{
			b.Navigation("Feedback");
			b.Navigation("Registrations");
		});
	}

	private static void InstallMarketplaceWriters(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("CREATE OR REPLACE FUNCTION economy_private.derive_economy_uuid_v1(\n    p_namespace uuid,\n    p_label text)\nRETURNS uuid\nLANGUAGE sql\nIMMUTABLE\nSTRICT\nSECURITY DEFINER\nSET search_path = pg_catalog, economy_private\nAS $function$\n    SELECT (\n        substr(value, 1, 8) || '-' || substr(value, 9, 4) || '-' ||\n        substr(value, 13, 4) || '-' || substr(value, 17, 4) || '-' ||\n        substr(value, 21, 12))::uuid\n    FROM (SELECT encode(public.digest(\n        convert_to(p_namespace::text || '|' || p_label, 'UTF8'), 'sha256'), 'hex') AS value) hashed\n$function$;\n\nCREATE OR REPLACE FUNCTION economy_private.reserve_marketplace_fifo_fragments_v1(\n    p_operation_id uuid,\n    p_wallet_id uuid,\n    p_legs jsonb,\n    p_purpose integer,\n    p_reserved_at timestamptz)\nRETURNS TABLE(\n    reservation_id uuid,\n    parent_lot_id uuid,\n    root_source_stamp_id uuid,\n    reversal_epoch bigint,\n    start_inclusive bigint,\n    end_exclusive bigint,\n    currency integer,\n    amount_units bigint)\nLANGUAGE plpgsql\nSECURITY DEFINER\nSET search_path = pg_catalog, economy_private\nAS $function$\nDECLARE\n    requested record;\n    candidate record;\n    source_range record;\n    free_range record;\n    trace_scale bigint;\n    remaining_trace bigint;\n    selected_trace bigint;\nBEGIN\n    IF p_operation_id IS NULL OR p_wallet_id IS NULL OR p_reserved_at IS NULL\n       OR p_purpose <> 7 OR jsonb_typeof(p_legs) <> 'array'\n       OR jsonb_array_length(p_legs) = 0\n       OR EXISTS (\n           SELECT 1\n           FROM jsonb_to_recordset(p_legs) AS leg(currency integer, units bigint)\n           WHERE leg.currency NOT IN (1, 2) OR leg.units <= 0)\n       OR EXISTS (\n           SELECT 1\n           FROM jsonb_to_recordset(p_legs) AS leg(currency integer, units bigint)\n           GROUP BY leg.currency HAVING count(*) <> 1) THEN\n        RAISE EXCEPTION 'Marketplace FIFO reservation arguments are invalid' USING ERRCODE = '22023';\n    END IF;\n\n    PERFORM 1 FROM public.economy_wallets wallet\n    WHERE wallet.\"Id\" = p_wallet_id AND wallet.\"State\" = 1\n    FOR SHARE;\n    IF NOT FOUND THEN\n        RAISE EXCEPTION 'Marketplace FIFO wallet is absent or inactive' USING ERRCODE = '23503';\n    END IF;\n\n    IF EXISTS (\n        SELECT 1 FROM public.economy_fragment_reservations reservation\n        WHERE reservation.\"OperationId\" = p_operation_id) THEN\n        IF EXISTS (\n            SELECT 1 FROM public.economy_fragment_reservations reservation\n            WHERE reservation.\"OperationId\" = p_operation_id\n              AND (reservation.\"WalletId\" <> p_wallet_id\n                   OR reservation.\"Purpose\" <> p_purpose\n                   OR reservation.\"Status\" <> 1))\n           OR EXISTS (\n               SELECT 1\n               FROM (\n                   SELECT leg.currency, leg.units\n                   FROM jsonb_to_recordset(p_legs) AS leg(currency integer, units bigint)) expected\n               FULL JOIN (\n                   SELECT reservation.\"Currency\" AS currency,\n                          sum((reservation.\"EndExclusive\" - reservation.\"StartInclusive\") /\n                              CASE WHEN reservation.\"Currency\" = 1 THEN 1000 ELSE 1 END) AS units\n                   FROM public.economy_fragment_reservations reservation\n                   WHERE reservation.\"OperationId\" = p_operation_id\n                   GROUP BY reservation.\"Currency\") actual USING (currency)\n               WHERE expected.currency IS NULL OR actual.currency IS NULL OR expected.units <> actual.units) THEN\n            RAISE EXCEPTION 'Marketplace FIFO operation is bound to another request' USING ERRCODE = '23505';\n        END IF;\n\n        RETURN QUERY\n        SELECT reservation.\"Id\", reservation.\"ParentLotId\", reservation.\"RootSourceStampId\",\n               reservation.\"ReversalEpoch\", reservation.\"StartInclusive\", reservation.\"EndExclusive\",\n               reservation.\"Currency\",\n               (reservation.\"EndExclusive\" - reservation.\"StartInclusive\") /\n                   CASE WHEN reservation.\"Currency\" = 1 THEN 1000 ELSE 1 END\n        FROM public.economy_fragment_reservations reservation\n        JOIN public.economy_credit_lots lot ON lot.\"Id\" = reservation.\"ParentLotId\"\n        WHERE reservation.\"OperationId\" = p_operation_id\n        ORDER BY reservation.\"Currency\", lot.\"ConfirmedAt\", lot.\"JournalSequence\", lot.\"Id\",\n                 reservation.\"StartInclusive\";\n        RETURN;\n    END IF;\n\n    FOR requested IN\n        SELECT leg.currency, leg.units\n        FROM jsonb_to_recordset(p_legs) AS leg(currency integer, units bigint)\n        ORDER BY leg.currency\n    LOOP\n        trace_scale := CASE WHEN requested.currency = 1 THEN 1000 ELSE 1 END;\n        remaining_trace := requested.units * trace_scale;\n\n        FOR candidate IN\n            SELECT lot.\"Id\", lot.\"Provenance\", lot.\"RootSourceStampId\",\n                   lot.\"ConfirmedAt\", lot.\"JournalSequence\"\n            FROM public.economy_credit_lots lot\n            WHERE lot.\"WalletId\" = p_wallet_id\n              AND lot.\"Currency\" = requested.currency\n              AND lot.\"State\" = 1\n              AND lot.\"ConfirmedAt\" <= p_reserved_at\n              AND ((requested.currency = 1 AND lot.\"Provenance\" IN (1, 2))\n                   OR (requested.currency = 2 AND lot.\"Provenance\" IN (3, 4, 5, 6, 7, 8)))\n            ORDER BY lot.\"ConfirmedAt\", lot.\"JournalSequence\", lot.\"Id\"\n            FOR UPDATE\n        LOOP\n            EXIT WHEN remaining_trace = 0;\n\n            FOR source_range IN\n                SELECT range_row.\"RootSourceStampId\", range_row.\"ReversalEpoch\",\n                       range_row.\"StartInclusive\", range_row.\"EndExclusive\"\n                FROM public.economy_fragment_root_ranges range_row\n                JOIN public.economy_root_reversal_states reversal\n                  ON reversal.\"RootSourceStampId\" = range_row.\"RootSourceStampId\"\n                 AND reversal.\"Epoch\" = range_row.\"ReversalEpoch\"\n                 AND reversal.\"State\" = 'active'\n                WHERE range_row.\"CreditLotId\" = candidate.\"Id\"\n                ORDER BY range_row.\"RootSourceStampId\", range_row.\"StartInclusive\", range_row.\"EndExclusive\"\n            LOOP\n                EXIT WHEN remaining_trace = 0;\n\n                FOR free_range IN\n                    WITH blocked AS (\n                        SELECT int8range(range_row.\"StartInclusive\", range_row.\"EndExclusive\", '[)') AS fragment\n                        FROM public.economy_entry_allocations allocation\n                        JOIN public.economy_fragment_root_ranges range_row\n                          ON range_row.\"EntryAllocationId\" = allocation.\"Id\"\n                        WHERE allocation.\"ParentLotId\" = candidate.\"Id\"\n                          AND range_row.\"RootSourceStampId\" = source_range.\"RootSourceStampId\"\n                          AND range_row.\"ReversalEpoch\" = source_range.\"ReversalEpoch\"\n                        UNION ALL\n                        SELECT int8range(reservation.\"StartInclusive\", reservation.\"EndExclusive\", '[)')\n                        FROM public.economy_fragment_reservations reservation\n                        WHERE reservation.\"ParentLotId\" = candidate.\"Id\"\n                          AND reservation.\"RootSourceStampId\" = source_range.\"RootSourceStampId\"\n                          AND reservation.\"ReversalEpoch\" = source_range.\"ReversalEpoch\"\n                          AND reservation.\"Status\" IN (1, 4)\n                    )\n                    SELECT lower(fragment)::bigint AS start_inclusive,\n                           upper(fragment)::bigint AS end_exclusive\n                    FROM unnest(\n                        int8multirange(int8range(source_range.\"StartInclusive\", source_range.\"EndExclusive\", '[)')) -\n                        COALESCE((SELECT range_agg(fragment) FROM blocked), '{}'::int8multirange)\n                    ) AS fragment\n                    ORDER BY lower(fragment), upper(fragment)\n                LOOP\n                    EXIT WHEN remaining_trace = 0;\n                    selected_trace := LEAST(\n                        remaining_trace, free_range.end_exclusive - free_range.start_inclusive);\n                    IF mod(selected_trace, trace_scale) <> 0 THEN\n                        selected_trace := selected_trace - mod(selected_trace, trace_scale);\n                    END IF;\n                    IF selected_trace <= 0 THEN\n                        CONTINUE;\n                    END IF;\n\n                    INSERT INTO public.economy_fragment_reservations (\n                        \"Id\", \"OperationId\", \"ParentLotId\", \"WalletId\", \"Currency\", \"Purpose\", \"Status\",\n                        \"RootSourceStampId\", \"ReversalEpoch\", \"StartInclusive\", \"EndExclusive\",\n                        \"ReservedAt\", \"TerminalAt\")\n                    VALUES (\n                        gen_random_uuid(), p_operation_id, candidate.\"Id\", p_wallet_id, requested.currency,\n                        p_purpose, 1, source_range.\"RootSourceStampId\", source_range.\"ReversalEpoch\",\n                        free_range.start_inclusive, free_range.start_inclusive + selected_trace,\n                        p_reserved_at, NULL);\n                    remaining_trace := remaining_trace - selected_trace;\n                END LOOP;\n            END LOOP;\n        END LOOP;\n\n        IF remaining_trace <> 0 THEN\n            RAISE EXCEPTION 'Marketplace FIFO reservation has insufficient confirmed fragments'\n                USING ERRCODE = 'P0001';\n        END IF;\n    END LOOP;\n\n    RETURN QUERY\n    SELECT reservation.\"Id\", reservation.\"ParentLotId\", reservation.\"RootSourceStampId\",\n           reservation.\"ReversalEpoch\", reservation.\"StartInclusive\", reservation.\"EndExclusive\",\n           reservation.\"Currency\",\n           (reservation.\"EndExclusive\" - reservation.\"StartInclusive\") /\n               CASE WHEN reservation.\"Currency\" = 1 THEN 1000 ELSE 1 END\n    FROM public.economy_fragment_reservations reservation\n    JOIN public.economy_credit_lots lot ON lot.\"Id\" = reservation.\"ParentLotId\"\n    WHERE reservation.\"OperationId\" = p_operation_id\n    ORDER BY reservation.\"Currency\", lot.\"ConfirmedAt\", lot.\"JournalSequence\", lot.\"Id\",\n             reservation.\"StartInclusive\";\nEND\n$function$;\n\n-- The settlement/refund writers are installed with their exact protected\n-- signatures here; their bodies are replaced below in this migration by\n-- the provenance-preserving implementation.\nCREATE OR REPLACE FUNCTION economy_private.post_marketplace_settlement_v1(\n    p_capability_id uuid, p_actor_id uuid, p_tenant_id uuid, p_settlement_id uuid,\n    p_posting_id uuid, p_idempotency_key text, p_policy_version bigint,\n    p_reserve_version bigint, p_risk_decision_id uuid, p_risk_operation_fingerprint text,\n    p_expected_counter_version bigint, p_buyer_id uuid, p_buyer_wallet_id uuid,\n    p_seller_id uuid, p_seller_wallet_id uuid, p_platform_fee_wallet_id uuid,\n    p_marketplace_policy_version bigint, p_currency_mode integer, p_order jsonb,\n    p_legs jsonb, p_reservation_ids jsonb, p_entitlement_id uuid,\n    p_refund_hold_until timestamptz, p_settled_at timestamptz,\n    p_capability_receipt_id uuid, p_capability_receipt_hash text,\n    p_kill_switch_epoch bigint, p_jurisdiction_code text, p_evidence_hashes jsonb)\nRETURNS TABLE(posting_id uuid, journal_sequence bigint, journal_hash text, duplicate boolean)\nLANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, economy_private\nAS $function$\nBEGIN\n    RAISE EXCEPTION 'Marketplace settlement writer installation is incomplete' USING ERRCODE = '55000';\nEND\n$function$;\n\nCREATE OR REPLACE FUNCTION economy_private.post_marketplace_refund_v1(\n    p_capability_id uuid, p_actor_id uuid, p_tenant_id uuid, p_refund_id uuid,\n    p_settlement_id uuid, p_posting_id uuid, p_idempotency_key text,\n    p_policy_version bigint, p_reserve_version bigint, p_risk_decision_id uuid,\n    p_risk_operation_fingerprint text, p_expected_counter_version bigint, p_buyer_id uuid,\n    p_marketplace_policy_version bigint, p_quantity integer, p_cumulative_refunded_quantity integer,\n    p_legs jsonb, p_reason_code text, p_reason_hash text, p_refunded_at timestamptz,\n    p_capability_receipt_id uuid, p_capability_receipt_hash text,\n    p_kill_switch_epoch bigint, p_jurisdiction_code text, p_evidence_hashes jsonb)\nRETURNS TABLE(posting_id uuid, journal_sequence bigint, journal_hash text, duplicate boolean)\nLANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, economy_private\nAS $function$\nBEGIN\n    RAISE EXCEPTION 'Marketplace refund writer installation is incomplete' USING ERRCODE = '55000';\nEND\n$function$;\n\nALTER FUNCTION economy_private.reserve_marketplace_fifo_fragments_v1(uuid,uuid,jsonb,integer,timestamptz)\n    OWNER TO gameguild_economy_procedure_owner;\nALTER FUNCTION economy_private.derive_economy_uuid_v1(uuid,text)\n    OWNER TO gameguild_economy_procedure_owner;\nREVOKE ALL ON FUNCTION economy_private.derive_economy_uuid_v1(uuid,text) FROM PUBLIC;\nALTER FUNCTION economy_private.post_marketplace_settlement_v1(\n    uuid,uuid,uuid,uuid,uuid,text,bigint,bigint,uuid,text,bigint,uuid,uuid,uuid,uuid,uuid,\n    bigint,integer,jsonb,jsonb,jsonb,uuid,timestamptz,timestamptz,uuid,text,bigint,text,jsonb)\n    OWNER TO gameguild_economy_procedure_owner;\nALTER FUNCTION economy_private.post_marketplace_refund_v1(\n    uuid,uuid,uuid,uuid,uuid,uuid,text,bigint,bigint,uuid,text,bigint,uuid,bigint,integer,integer,\n    jsonb,text,text,timestamptz,uuid,text,bigint,text,jsonb)\n    OWNER TO gameguild_economy_procedure_owner;\nREVOKE ALL ON FUNCTION economy_private.reserve_marketplace_fifo_fragments_v1(uuid,uuid,jsonb,integer,timestamptz) FROM PUBLIC;\nREVOKE ALL ON FUNCTION economy_private.post_marketplace_settlement_v1(\n    uuid,uuid,uuid,uuid,uuid,text,bigint,bigint,uuid,text,bigint,uuid,uuid,uuid,uuid,uuid,\n    bigint,integer,jsonb,jsonb,jsonb,uuid,timestamptz,timestamptz,uuid,text,bigint,text,jsonb) FROM PUBLIC;\nREVOKE ALL ON FUNCTION economy_private.post_marketplace_refund_v1(\n    uuid,uuid,uuid,uuid,uuid,uuid,text,bigint,bigint,uuid,text,bigint,uuid,bigint,integer,integer,\n    jsonb,text,text,timestamptz,uuid,text,bigint,text,jsonb) FROM PUBLIC;\nGRANT EXECUTE ON FUNCTION economy_private.reserve_marketplace_fifo_fragments_v1(uuid,uuid,jsonb,integer,timestamptz),\n    economy_private.post_marketplace_settlement_v1(\n        uuid,uuid,uuid,uuid,uuid,text,bigint,bigint,uuid,text,bigint,uuid,uuid,uuid,uuid,uuid,\n        bigint,integer,jsonb,jsonb,jsonb,uuid,timestamptz,timestamptz,uuid,text,bigint,text,jsonb),\n    economy_private.post_marketplace_refund_v1(\n        uuid,uuid,uuid,uuid,uuid,uuid,text,bigint,bigint,uuid,text,bigint,uuid,bigint,integer,integer,\n        jsonb,text,text,timestamptz,uuid,text,bigint,text,jsonb)\n    TO gameguild_economy_writer;");
		InstallMarketplaceSettlementWriter(migrationBuilder);
		InstallMarketplaceRefundWriter(migrationBuilder);
	}

	private static void RemoveMarketplaceWriters(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("DROP FUNCTION IF EXISTS economy_private.post_marketplace_refund_v1(\n    uuid,uuid,uuid,uuid,uuid,uuid,text,bigint,bigint,uuid,text,bigint,uuid,bigint,integer,integer,\n    jsonb,text,text,timestamptz,uuid,text,bigint,text,jsonb);\nDROP FUNCTION IF EXISTS economy_private.post_marketplace_settlement_v1(\n    uuid,uuid,uuid,uuid,uuid,text,bigint,bigint,uuid,text,bigint,uuid,uuid,uuid,uuid,uuid,\n    bigint,integer,jsonb,jsonb,jsonb,uuid,timestamptz,timestamptz,uuid,text,bigint,text,jsonb);\nDROP FUNCTION IF EXISTS economy_private.reserve_marketplace_fifo_fragments_v1(uuid,uuid,jsonb,integer,timestamptz);\nDROP FUNCTION IF EXISTS economy_private.derive_economy_uuid_v1(uuid,text);");
	}

	private static void InstallMarketplaceRefundWriter(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("CREATE OR REPLACE FUNCTION economy_private.post_marketplace_refund_v1(\n    p_capability_id uuid, p_actor_id uuid, p_tenant_id uuid, p_refund_id uuid,\n    p_settlement_id uuid, p_posting_id uuid, p_idempotency_key text,\n    p_policy_version bigint, p_reserve_version bigint, p_risk_decision_id uuid,\n    p_risk_operation_fingerprint text, p_expected_counter_version bigint, p_buyer_id uuid,\n    p_marketplace_policy_version bigint, p_quantity integer, p_cumulative_refunded_quantity integer,\n    p_legs jsonb, p_reason_code text, p_reason_hash text, p_refunded_at timestamptz,\n    p_capability_receipt_id uuid, p_capability_receipt_hash text,\n    p_kill_switch_epoch bigint, p_jurisdiction_code text, p_evidence_hashes jsonb)\nRETURNS TABLE(posting_id uuid, journal_sequence bigint, journal_hash text, duplicate boolean)\nLANGUAGE plpgsql\nSECURITY DEFINER\nSET search_path = pg_catalog, economy_private\nAS $function$\nDECLARE\n    settlement record;\n    refund_leg record;\n    settlement_credit record;\n    restoration record;\n    source_range record;\n    free_range record;\n    funding_fragment record;\n    original_range record;\n    parent_lot record;\n    receipt record;\n    account_id uuid;\n    line_id uuid;\n    allocation_id uuid;\n    restored_lot_id uuid;\n    trace_scale bigint;\n    logical_take bigint;\n    available_units bigint;\n    selected_trace bigint;\n    debt_units bigint;\n    remaining_units bigint;\n    skip_trace bigint;\n    remaining_trace bigint;\n    range_trace bigint;\n    restored_start bigint;\n    restored_end bigint;\n    output_account integer;\n    next_event_sequence bigint;\n    next_journal_sequence bigint;\n    lines jsonb := '[]'::jsonb;\n    allocations jsonb := '[]'::jsonb;\n    root_ranges jsonb := '[]'::jsonb;\n    expected_epochs jsonb := '[]'::jsonb;\n    debt_rows jsonb := '[]'::jsonb;\n    restore_rows jsonb := '[]'::jsonb;\n    revoke_payload jsonb;\nBEGIN\n    IF p_capability_id IS NULL OR p_actor_id IS NULL OR p_tenant_id IS NULL\n       OR p_refund_id IS NULL OR p_settlement_id IS NULL OR p_posting_id IS NULL\n       OR p_risk_decision_id IS NULL OR p_buyer_id IS NULL\n       OR p_policy_version <= 0 OR p_reserve_version <= 0\n       OR p_marketplace_policy_version <= 0 OR p_expected_counter_version <= 0\n       OR p_quantity <= 0 OR p_cumulative_refunded_quantity < p_quantity\n       OR p_refunded_at IS NULL OR p_capability_receipt_id IS NULL\n       OR p_kill_switch_epoch < 0\n       OR length(btrim(COALESCE(p_idempotency_key, ''))) = 0\n       OR length(btrim(COALESCE(p_risk_operation_fingerprint, ''))) = 0\n       OR length(btrim(COALESCE(p_reason_code, ''))) = 0\n       OR length(btrim(COALESCE(p_reason_hash, ''))) = 0\n       OR length(btrim(COALESCE(p_capability_receipt_hash, ''))) = 0\n       OR length(btrim(COALESCE(p_jurisdiction_code, ''))) = 0\n       OR jsonb_typeof(p_legs) <> 'array' OR jsonb_array_length(p_legs) = 0\n       OR jsonb_typeof(p_evidence_hashes) <> 'array' THEN\n        RAISE EXCEPTION 'Marketplace refund arguments are invalid' USING ERRCODE = '22023';\n    END IF;\n\n    SELECT refund.\"PostingId\", refund.\"FirstJournalSequence\", refund.\"JournalHash\", true\n    INTO posting_id, journal_sequence, journal_hash, duplicate\n    FROM public.economy_marketplace_refunds refund\n    WHERE refund.\"TenantId\" = p_tenant_id\n      AND refund.\"IdempotencyKey\" = p_idempotency_key;\n    IF FOUND THEN\n        IF NOT EXISTS (\n            SELECT 1 FROM public.economy_marketplace_refunds refund\n            WHERE refund.\"Id\" = p_refund_id\n              AND refund.\"SettlementId\" = p_settlement_id\n              AND refund.\"PostingId\" = p_posting_id\n              AND refund.\"Quantity\" = p_quantity\n              AND refund.\"RefundedQuantity\" = p_cumulative_refunded_quantity\n              AND refund.\"ReasonHash\" = btrim(p_reason_hash)\n              AND refund.\"CapabilityReceiptId\" = p_capability_receipt_id\n              AND refund.\"CapabilityReceiptHash\" = btrim(p_capability_receipt_hash)) THEN\n            RAISE EXCEPTION 'Marketplace idempotency key is bound to another refund'\n                USING ERRCODE = '23505';\n        END IF;\n        RETURN NEXT;\n        RETURN;\n    END IF;\n\n    SELECT * INTO settlement\n    FROM public.economy_marketplace_settlements row\n    WHERE row.\"Id\" = p_settlement_id AND row.\"TenantId\" = p_tenant_id\n    FOR UPDATE;\n    IF NOT FOUND OR settlement.\"BuyerId\" <> p_buyer_id\n       OR settlement.\"PolicyVersion\" <> p_marketplace_policy_version\n       OR settlement.\"Status\" = 3\n       OR p_cumulative_refunded_quantity <> settlement.\"RefundedQuantity\" + p_quantity\n       OR p_cumulative_refunded_quantity > settlement.\"Quantity\" THEN\n        RAISE EXCEPTION 'Marketplace settlement is absent, stale, or not refundable'\n            USING ERRCODE = '40001';\n    END IF;\n\n    PERFORM 1\n    FROM public.economy_capability_receipts capability_receipt\n    JOIN public.economy_capability_receipt_consumptions consumption\n      ON consumption.\"ReceiptId\" = capability_receipt.\"Id\"\n    WHERE capability_receipt.\"Id\" = p_capability_receipt_id\n      AND capability_receipt.\"TenantId\" = p_tenant_id\n      AND capability_receipt.\"ActorId\" = p_actor_id\n      AND capability_receipt.\"Capability\" = 11\n      AND capability_receipt.\"PolicyVersion\" = p_policy_version\n      AND capability_receipt.\"ReserveVersion\" = p_reserve_version\n      AND capability_receipt.\"RiskDecisionId\" = p_risk_decision_id\n      AND capability_receipt.\"KillSwitchEpoch\" = p_kill_switch_epoch\n      AND capability_receipt.\"JurisdictionCode\" = upper(btrim(p_jurisdiction_code))\n      AND capability_receipt.\"OperationFingerprint\" = btrim(p_risk_operation_fingerprint)\n      AND capability_receipt.\"ReceiptHash\" = btrim(p_capability_receipt_hash)\n      AND capability_receipt.\"EvidenceHashes\" = p_evidence_hashes\n      AND capability_receipt.\"IssuedAt\" <= p_refunded_at\n      AND capability_receipt.\"ExpiresAt\" > p_refunded_at\n      AND consumption.\"TenantId\" = p_tenant_id\n      AND consumption.\"ActorId\" = p_actor_id\n      AND consumption.\"OperationFingerprint\" = btrim(p_risk_operation_fingerprint)\n      AND consumption.\"KillSwitchEpoch\" = p_kill_switch_epoch\n    FOR SHARE OF capability_receipt, consumption;\n    IF NOT FOUND THEN\n        RAISE EXCEPTION 'Marketplace refund capability receipt is absent or mismatched'\n            USING ERRCODE = '42501';\n    END IF;\n\n    PERFORM 1 FROM public.economy_marketplace_currency_policy_versions policy\n    WHERE policy.\"TenantId\" = p_tenant_id\n      AND policy.\"ProductId\" = settlement.\"ProductId\"\n      AND policy.\"Version\" = p_marketplace_policy_version\n      AND policy.\"SellerId\" = settlement.\"SellerId\"\n      AND length(btrim(policy.\"Signature\")) > 0\n      AND policy.\"KeyId\" <> 'legacy-untrusted'\n    FOR SHARE;\n    IF NOT FOUND THEN\n        RAISE EXCEPTION 'historical signed Marketplace policy is absent or mismatched'\n            USING ERRCODE = '42501';\n    END IF;\n\n    IF jsonb_array_length(p_legs) <>\n           (SELECT count(DISTINCT (leg->>'currency')::integer)\n            FROM jsonb_array_elements(p_legs) leg)\n       OR EXISTS (\n           SELECT 1\n           FROM jsonb_to_recordset(p_legs) AS requested(currency integer, units bigint)\n           LEFT JOIN public.economy_marketplace_settlement_legs persisted\n             ON persisted.\"SettlementId\" = p_settlement_id\n            AND persisted.\"Currency\" = requested.currency\n           WHERE requested.currency NOT IN (1, 2) OR requested.units <= 0\n              OR persisted.\"SettlementId\" IS NULL\n              OR requested.units <> (\n                  (persisted.\"Units\" * p_cumulative_refunded_quantity / settlement.\"Quantity\")\n                  - persisted.\"RefundedUnits\"))\n       OR EXISTS (\n           SELECT 1 FROM public.economy_marketplace_settlement_legs persisted\n           WHERE persisted.\"SettlementId\" = p_settlement_id\n             AND (persisted.\"Units\" * p_cumulative_refunded_quantity / settlement.\"Quantity\")\n                 > persisted.\"RefundedUnits\"\n             AND NOT EXISTS (\n                 SELECT 1 FROM jsonb_to_recordset(p_legs)\n                     AS requested(currency integer, units bigint)\n                 WHERE requested.currency = persisted.\"Currency\")) THEN\n        RAISE EXCEPTION 'Marketplace refund legs do not match the cumulative order quantity'\n            USING ERRCODE = '23514';\n    END IF;\n\n    -- Debit the exact unspent proceeds first. If a recipient has already\n    -- consumed its original fragments, recognize a durable receivable;\n    -- the buyer restoration never depends on rewriting that history.\n    FOR refund_leg IN\n        SELECT * FROM jsonb_to_recordset(p_legs) AS leg(currency integer, units bigint)\n        ORDER BY currency\n    LOOP\n        remaining_units := refund_leg.units;\n        trace_scale := CASE WHEN refund_leg.currency = 1 THEN 1000 ELSE 1 END;\n        FOR settlement_credit IN\n            SELECT credit.*\n            FROM public.economy_marketplace_settlement_credits credit\n            WHERE credit.\"SettlementId\" = p_settlement_id\n              AND credit.\"Currency\" = refund_leg.currency\n              AND credit.\"RemainingUnits\" > 0\n            ORDER BY credit.\"Purpose\", credit.\"Id\"\n            FOR UPDATE\n        LOOP\n            EXIT WHEN remaining_units = 0;\n            logical_take := LEAST(remaining_units, settlement_credit.\"RemainingUnits\");\n            available_units := 0;\n\n            FOR source_range IN\n                SELECT ranges.*\n                FROM public.economy_fragment_root_ranges ranges\n                JOIN public.economy_root_reversal_states reversal\n                  ON reversal.\"RootSourceStampId\" = ranges.\"RootSourceStampId\"\n                 AND reversal.\"Epoch\" = ranges.\"ReversalEpoch\"\n                 AND reversal.\"State\" = 'active'\n                WHERE ranges.\"CreditLotId\" = settlement_credit.\"CreditLotId\"\n                ORDER BY ranges.\"RootSourceStampId\", ranges.\"StartInclusive\"\n            LOOP\n                EXIT WHEN available_units = logical_take;\n                FOR free_range IN\n                    WITH blocked AS (\n                        SELECT int8range(ranges.\"StartInclusive\", ranges.\"EndExclusive\", '[)') fragment\n                        FROM public.economy_entry_allocations allocation\n                        JOIN public.economy_fragment_root_ranges ranges\n                          ON ranges.\"EntryAllocationId\" = allocation.\"Id\"\n                        WHERE allocation.\"ParentLotId\" = settlement_credit.\"CreditLotId\"\n                          AND ranges.\"RootSourceStampId\" = source_range.\"RootSourceStampId\"\n                          AND ranges.\"ReversalEpoch\" = source_range.\"ReversalEpoch\"\n                        UNION ALL\n                        SELECT int8range(reservation.\"StartInclusive\", reservation.\"EndExclusive\", '[)')\n                        FROM public.economy_fragment_reservations reservation\n                        WHERE reservation.\"ParentLotId\" = settlement_credit.\"CreditLotId\"\n                          AND reservation.\"RootSourceStampId\" = source_range.\"RootSourceStampId\"\n                          AND reservation.\"ReversalEpoch\" = source_range.\"ReversalEpoch\"\n                          AND reservation.\"Status\" IN (1, 4)\n                    )\n                    SELECT lower(fragment)::bigint start_inclusive,\n                           upper(fragment)::bigint end_exclusive\n                    FROM unnest(\n                        int8multirange(int8range(source_range.\"StartInclusive\",\n                            source_range.\"EndExclusive\", '[)')) -\n                        COALESCE((SELECT range_agg(fragment) FROM blocked),\n                            '{}'::int8multirange)) fragment\n                    ORDER BY lower(fragment), upper(fragment)\n                LOOP\n                    EXIT WHEN available_units = logical_take;\n                    selected_trace := LEAST(\n                        (logical_take - available_units) * trace_scale,\n                        free_range.end_exclusive - free_range.start_inclusive);\n                    selected_trace := selected_trace - mod(selected_trace, trace_scale);\n                    IF selected_trace <= 0 THEN CONTINUE; END IF;\n\n                    SELECT lot.* INTO parent_lot FROM public.economy_credit_lots lot\n                    WHERE lot.\"Id\" = settlement_credit.\"CreditLotId\" FOR SHARE;\n                    output_account := CASE WHEN refund_leg.currency = 1\n                        THEN CASE WHEN parent_lot.\"Provenance\" = 2 THEN 3 ELSE 2 END\n                        ELSE 4 END;\n                    SELECT account.\"Id\" INTO account_id\n                    FROM public.economy_accounts account\n                    WHERE account.\"WalletId\" = settlement_credit.\"WalletId\"\n                      AND account.\"Code\" = output_account\n                      AND account.\"Currency\" = refund_leg.currency\n                      AND account.\"Provenance\" = parent_lot.\"Provenance\";\n                    IF account_id IS NULL THEN\n                        RAISE EXCEPTION 'Marketplace refund proceeds account is not provisioned'\n                            USING ERRCODE = '23503';\n                    END IF;\n\n                    line_id := economy_private.derive_economy_uuid_v1(\n                        p_refund_id, 'proceeds-debit:' || settlement_credit.\"Id\"::text ||\n                        ':' || free_range.start_inclusive::text);\n                    allocation_id := economy_private.derive_economy_uuid_v1(\n                        line_id, 'allocation');\n                    lines := lines || jsonb_build_array(jsonb_build_object(\n                        'id', line_id, 'account_id', account_id,\n                        'account_code', output_account,\n                        'wallet_id', settlement_credit.\"WalletId\",\n                        'credit_lot_id', '', 'side', 1,\n                        'currency', refund_leg.currency,\n                        'amount_units', selected_trace / trace_scale,\n                        'provenance', parent_lot.\"Provenance\"));\n                    allocations := allocations || jsonb_build_array(jsonb_build_object(\n                        'id', allocation_id, 'journal_line_id', line_id,\n                        'parent_lot_id', settlement_credit.\"CreditLotId\",\n                        'amount_units', selected_trace / trace_scale));\n                    root_ranges := root_ranges || jsonb_build_array(jsonb_build_object(\n                        'id', economy_private.derive_economy_uuid_v1(allocation_id, 'range'),\n                        'root_source_stamp_id', source_range.\"RootSourceStampId\",\n                        'credit_lot_id', '', 'entry_allocation_id', allocation_id,\n                        'start_inclusive', free_range.start_inclusive,\n                        'end_exclusive', free_range.start_inclusive + selected_trace,\n                        'reversal_epoch', source_range.\"ReversalEpoch\"));\n                    IF NOT EXISTS (\n                        SELECT 1 FROM jsonb_array_elements(expected_epochs) epoch\n                        WHERE (epoch->>'root_source_stamp_id')::uuid =\n                              source_range.\"RootSourceStampId\") THEN\n                        expected_epochs := expected_epochs || jsonb_build_array(jsonb_build_object(\n                            'root_source_stamp_id', source_range.\"RootSourceStampId\",\n                            'expected_epoch', source_range.\"ReversalEpoch\"));\n                    END IF;\n                    available_units := available_units + selected_trace / trace_scale;\n                END LOOP;\n            END LOOP;\n\n            debt_units := logical_take - available_units;\n            IF debt_units > 0 THEN\n                output_account := CASE WHEN refund_leg.currency = 1 THEN 13 ELSE 16 END;\n                SELECT account.\"Id\" INTO account_id\n                FROM public.economy_accounts account\n                WHERE account.\"WalletId\" IS NULL\n                  AND account.\"Code\" = output_account\n                  AND account.\"Currency\" = refund_leg.currency\n                  AND account.\"Provenance\" IS NULL;\n                IF account_id IS NULL THEN\n                    RAISE EXCEPTION 'Marketplace refund receivable account is not provisioned'\n                        USING ERRCODE = '23503';\n                END IF;\n                line_id := economy_private.derive_economy_uuid_v1(\n                    p_refund_id, 'receivable:' || settlement_credit.\"Id\"::text);\n                lines := lines || jsonb_build_array(jsonb_build_object(\n                    'id', line_id, 'account_id', account_id,\n                    'account_code', output_account, 'wallet_id', '',\n                    'credit_lot_id', '', 'side', 1,\n                    'currency', refund_leg.currency, 'amount_units', debt_units,\n                    'provenance', ''));\n                debt_rows := debt_rows || jsonb_build_array(jsonb_build_object(\n                    'id', economy_private.derive_economy_uuid_v1(\n                        p_refund_id, 'debt:' || settlement_credit.\"Id\"::text),\n                    'wallet_id', settlement_credit.\"WalletId\",\n                    'currency', refund_leg.currency, 'units', debt_units));\n            END IF;\n\n            restore_rows := restore_rows || jsonb_build_array(jsonb_build_object(\n                'credit_lot_id', settlement_credit.\"CreditLotId\",\n                'currency', refund_leg.currency,\n                'units', logical_take,\n                'skip_units', settlement_credit.\"AmountUnits\" -\n                    settlement_credit.\"RemainingUnits\"));\n\n            UPDATE public.economy_marketplace_settlement_credits\n            SET \"RemainingUnits\" = \"RemainingUnits\" - logical_take\n            WHERE \"Id\" = settlement_credit.\"Id\";\n            IF settlement_credit.\"RemainingUnits\" = logical_take THEN\n                UPDATE public.economy_holds SET \"Status\" = 3, \"ReleasedAt\" = p_refunded_at\n                WHERE \"Id\" = settlement_credit.\"RefundHoldId\" AND \"Status\" = 1;\n                IF FOUND THEN\n                    INSERT INTO public.economy_hold_events (\n                        \"Id\", \"HoldId\", \"Sequence\", \"Kind\", \"ActorId\", \"EvidenceHash\", \"OccurredAt\")\n                    VALUES (economy_private.derive_economy_uuid_v1(\n                            settlement_credit.\"RefundHoldId\", 'consumed'),\n                        settlement_credit.\"RefundHoldId\", 2, 3, p_actor_id,\n                        p_reason_hash, p_refunded_at);\n                END IF;\n            END IF;\n            remaining_units := remaining_units - logical_take;\n        END LOOP;\n        IF remaining_units <> 0 THEN\n            RAISE EXCEPTION 'Marketplace settlement credits do not conserve the refund leg'\n                USING ERRCODE = '23514';\n        END IF;\n    END LOOP;\n\n    -- Re-create buyer lots from the exact original source intervals. The\n    -- output retains the original provenance, maturity, cash-out flag and\n    -- reversal epoch, so partial refunds remain replayable.\n    INSERT INTO public.economy_chain_head (\"Id\", \"Sequence\", \"Hash\", \"UpdatedAt\")\n    VALUES (1, 0, repeat('0', 64), p_refunded_at)\n    ON CONFLICT (\"Id\") DO NOTHING;\n    SELECT \"Sequence\" + 1 INTO next_journal_sequence\n    FROM public.economy_chain_head WHERE \"Id\" = 1 FOR UPDATE;\n\n    FOR restoration IN\n        SELECT * FROM jsonb_to_recordset(restore_rows) AS row(\n            credit_lot_id uuid, currency integer, units bigint, skip_units bigint)\n        ORDER BY currency, credit_lot_id\n    LOOP\n        trace_scale := CASE WHEN restoration.currency = 1 THEN 1000 ELSE 1 END;\n        skip_trace := restoration.skip_units * trace_scale;\n        remaining_trace := restoration.units * trace_scale;\n        FOR source_range IN\n            SELECT ranges.*\n            FROM public.economy_fragment_root_ranges ranges\n            WHERE ranges.\"CreditLotId\" = restoration.credit_lot_id\n            ORDER BY ranges.\"RootSourceStampId\", ranges.\"StartInclusive\"\n        LOOP\n            range_trace := source_range.\"EndExclusive\" - source_range.\"StartInclusive\";\n            IF skip_trace >= range_trace THEN\n                skip_trace := skip_trace - range_trace;\n                CONTINUE;\n            END IF;\n            EXIT WHEN remaining_trace = 0;\n            restored_start := source_range.\"StartInclusive\" + skip_trace;\n            selected_trace := LEAST(\n                remaining_trace, source_range.\"EndExclusive\" - restored_start);\n            selected_trace := selected_trace - mod(selected_trace, trace_scale);\n            skip_trace := 0;\n            IF selected_trace <= 0 THEN CONTINUE; END IF;\n            restored_end := restored_start + selected_trace;\n\n            -- The reversal edge starts at the seller/platform proceeds\n            -- lot, while the restored lot's attributes are recovered from\n            -- the original buyer funding range covering this interval.\n            SELECT original_lot.* INTO parent_lot\n            FROM public.economy_marketplace_funding_fragments funding\n            JOIN public.economy_credit_lots original_lot\n              ON original_lot.\"Id\" = funding.\"ParentLotId\"\n            JOIN LATERAL jsonb_to_recordset(funding.\"SelectedRootRanges\") AS original(\n                \"RootSourceStampId\" uuid,\n                \"StartInclusive\" bigint,\n                \"EndExclusive\" bigint,\n                \"ReversalEpoch\" bigint) ON true\n            WHERE funding.\"SettlementId\" = p_settlement_id\n              AND funding.\"Currency\" = restoration.currency\n              AND original.\"RootSourceStampId\" = source_range.\"RootSourceStampId\"\n              AND original.\"ReversalEpoch\" = source_range.\"ReversalEpoch\"\n              AND original.\"StartInclusive\" <= restored_start\n              AND original.\"EndExclusive\" >= restored_end\n            ORDER BY funding.\"Id\"\n            LIMIT 1\n            FOR SHARE OF original_lot;\n            IF NOT FOUND THEN\n                RAISE EXCEPTION 'Marketplace refund original funding provenance is absent'\n                    USING ERRCODE = '23514';\n            END IF;\n\n            output_account := CASE WHEN restoration.currency = 1\n                THEN CASE WHEN parent_lot.\"Provenance\" = 2 THEN 3 ELSE 2 END\n                ELSE 4 END;\n            SELECT account.\"Id\" INTO account_id\n            FROM public.economy_accounts account\n            WHERE account.\"WalletId\" = settlement.\"BuyerWalletId\"\n              AND account.\"Code\" = output_account\n              AND account.\"Currency\" = restoration.currency\n              AND account.\"Provenance\" = parent_lot.\"Provenance\";\n            IF account_id IS NULL THEN\n                RAISE EXCEPTION 'Marketplace buyer restoration account is not provisioned'\n                    USING ERRCODE = '23503';\n            END IF;\n\n            restored_lot_id := economy_private.derive_economy_uuid_v1(\n                p_refund_id, 'restored-lot:' || restoration.credit_lot_id::text || ':' ||\n                source_range.\"RootSourceStampId\"::text || ':' ||\n                restored_start::text || ':' || restored_end::text);\n            INSERT INTO public.economy_credit_lots (\n                \"Id\", \"WalletId\", \"RootSourceStampId\", \"Currency\", \"AmountUnits\",\n                \"Provenance\", \"CreditedAt\", \"ConfirmedAt\", \"OriginalMaturesAt\",\n                \"CashOutEligible\", \"JournalSequence\", \"State\", \"ReversalEpoch\")\n            VALUES (restored_lot_id, settlement.\"BuyerWalletId\",\n                source_range.\"RootSourceStampId\", restoration.currency,\n                selected_trace / trace_scale, parent_lot.\"Provenance\",\n                parent_lot.\"CreditedAt\", parent_lot.\"ConfirmedAt\",\n                parent_lot.\"OriginalMaturesAt\", parent_lot.\"CashOutEligible\",\n                next_journal_sequence, 1, source_range.\"ReversalEpoch\");\n            INSERT INTO public.economy_lot_lineage_edges (\n                \"Id\", \"ParentLotId\", \"ChildLotId\", \"Currency\", \"AmountUnits\")\n            VALUES (economy_private.derive_economy_uuid_v1(restored_lot_id, 'lineage'),\n                restoration.credit_lot_id, restored_lot_id,\n                restoration.currency, selected_trace / trace_scale);\n            INSERT INTO public.economy_fragment_root_ranges (\n                \"Id\", \"RootSourceStampId\", \"CreditLotId\", \"EntryAllocationId\",\n                \"StartInclusive\", \"EndExclusive\", \"ReversalEpoch\")\n            VALUES (economy_private.derive_economy_uuid_v1(restored_lot_id, 'range'),\n                source_range.\"RootSourceStampId\", restored_lot_id, NULL,\n                restored_start, restored_end, source_range.\"ReversalEpoch\");\n            line_id := economy_private.derive_economy_uuid_v1(restored_lot_id, 'line');\n            lines := lines || jsonb_build_array(jsonb_build_object(\n                'id', line_id, 'account_id', account_id,\n                'account_code', output_account,\n                'wallet_id', settlement.\"BuyerWalletId\",\n                'credit_lot_id', restored_lot_id, 'side', 2,\n                'currency', restoration.currency,\n                'amount_units', selected_trace / trace_scale,\n                'provenance', parent_lot.\"Provenance\"));\n            IF NOT EXISTS (\n                SELECT 1 FROM jsonb_array_elements(expected_epochs) epoch\n                WHERE (epoch->>'root_source_stamp_id')::uuid =\n                      source_range.\"RootSourceStampId\") THEN\n                expected_epochs := expected_epochs || jsonb_build_array(jsonb_build_object(\n                    'root_source_stamp_id', source_range.\"RootSourceStampId\",\n                    'expected_epoch', source_range.\"ReversalEpoch\"));\n            END IF;\n            remaining_trace := remaining_trace - selected_trace;\n            EXIT WHEN remaining_trace = 0;\n        END LOOP;\n        IF skip_trace <> 0 OR remaining_trace <> 0 THEN\n            RAISE EXCEPTION 'Marketplace refund cannot restore the original provenance ranges'\n                USING ERRCODE = '23514';\n        END IF;\n    END LOOP;\n\n    SELECT * INTO receipt FROM economy_private.post_registered_posting_v1(\n        p_capability_id, p_actor_id, p_tenant_id, p_posting_id, p_idempotency_key,\n        26, 1, 7, p_policy_version, p_reserve_version, p_risk_decision_id,\n        p_risk_operation_fingerprint, p_expected_counter_version,\n        NULL, NULL, p_refunded_at, lines, allocations, root_ranges, expected_epochs,\n        p_capability_receipt_hash);\n    IF receipt.duplicate THEN\n        RAISE EXCEPTION 'unexpected duplicate during Marketplace refund'\n            USING ERRCODE = '40001';\n    END IF;\n\n    INSERT INTO public.economy_marketplace_refunds (\n        \"Id\", \"TenantId\", \"SettlementId\", \"BuyerId\", \"IdempotencyKey\",\n        \"IsFullRefund\", \"EntitlementRevoked\", \"FirstJournalSequence\", \"PostingId\",\n        \"JournalHash\", \"ReasonCode\", \"ReasonHash\", \"Quantity\", \"RefundedQuantity\",\n        \"MarketplacePolicyVersion\", \"PolicyVersion\", \"CapabilityReceiptId\",\n        \"CapabilityReceiptHash\", \"ReserveVersion\", \"RiskDecisionId\", \"KillSwitchEpoch\",\n        \"JurisdictionCode\", \"EvidenceHashes\", \"RefundedAt\")\n    VALUES (p_refund_id, p_tenant_id, p_settlement_id, p_buyer_id, p_idempotency_key,\n        p_cumulative_refunded_quantity = settlement.\"Quantity\",\n        p_cumulative_refunded_quantity = settlement.\"Quantity\",\n        receipt.journal_sequence, p_posting_id, receipt.journal_hash,\n        upper(btrim(p_reason_code)), btrim(p_reason_hash), p_quantity,\n        p_cumulative_refunded_quantity, p_marketplace_policy_version,\n        p_policy_version, p_capability_receipt_id, btrim(p_capability_receipt_hash),\n        p_reserve_version, p_risk_decision_id, p_kill_switch_epoch,\n        upper(btrim(p_jurisdiction_code)), p_evidence_hashes, p_refunded_at);\n\n    INSERT INTO public.economy_marketplace_refund_legs (\n        \"RefundId\", \"SettlementId\", \"Currency\", \"Units\")\n    SELECT p_refund_id, p_settlement_id, leg.currency, leg.units\n    FROM jsonb_to_recordset(p_legs) AS leg(currency integer, units bigint);\n    INSERT INTO public.economy_marketplace_refund_debts (\n        \"Id\", \"TenantId\", \"RefundId\", \"SettlementId\", \"ResponsibleWalletId\",\n        \"Currency\", \"AmountUnits\", \"EvidenceHash\", \"RecordedAt\")\n    SELECT (row->>'id')::uuid, p_tenant_id, p_refund_id, p_settlement_id,\n        (row->>'wallet_id')::uuid, (row->>'currency')::integer,\n        (row->>'units')::bigint, btrim(p_reason_hash), p_refunded_at\n    FROM jsonb_array_elements(debt_rows) row;\n\n    UPDATE public.economy_marketplace_settlement_legs leg\n    SET \"RefundedUnits\" = leg.\"RefundedUnits\" + requested.units\n    FROM jsonb_to_recordset(p_legs) AS requested(currency integer, units bigint)\n    WHERE leg.\"SettlementId\" = p_settlement_id\n      AND leg.\"Currency\" = requested.currency;\n    UPDATE public.economy_marketplace_settlements\n    SET \"RefundedQuantity\" = p_cumulative_refunded_quantity,\n        \"Status\" = CASE WHEN p_cumulative_refunded_quantity = \"Quantity\" THEN 3 ELSE 2 END,\n        \"EntitlementStatus\" = CASE\n            WHEN p_cumulative_refunded_quantity = \"Quantity\" THEN 3\n            ELSE \"EntitlementStatus\" END,\n        \"UpdatedAt\" = p_refunded_at,\n        \"Version\" = \"Version\" + 1\n    WHERE \"Id\" = p_settlement_id;\n\n    SELECT COALESCE(max(event.\"Sequence\"), 0) + 1 INTO next_event_sequence\n    FROM public.economy_marketplace_events event\n    WHERE event.\"SettlementId\" = p_settlement_id;\n    INSERT INTO public.economy_marketplace_events (\n        \"Id\", \"TenantId\", \"SettlementId\", \"Sequence\", \"EventKind\", \"EvidenceHash\", \"OccurredAt\")\n    VALUES (economy_private.derive_economy_uuid_v1(p_refund_id, 'event'),\n        p_tenant_id, p_settlement_id, next_event_sequence,\n        CASE WHEN p_cumulative_refunded_quantity = settlement.\"Quantity\"\n             THEN 'Refunded' ELSE 'PartiallyRefunded' END,\n        btrim(p_reason_hash), p_refunded_at);\n\n    IF p_cumulative_refunded_quantity = settlement.\"Quantity\" THEN\n        revoke_payload := jsonb_build_object(\n            'settlementId', p_settlement_id,\n            'entitlementId', settlement.\"EntitlementId\",\n            'refundId', p_refund_id, 'occurredAt', p_refunded_at);\n        INSERT INTO public.economy_marketplace_outbox (\n            \"Id\", \"TenantId\", \"SettlementId\", \"MessageType\", \"Payload\", \"PayloadHash\",\n            \"OccurredAt\", \"PublishedAt\", \"AttemptCount\")\n        VALUES (economy_private.derive_economy_uuid_v1(\n                p_refund_id, 'entitlement-revoke-outbox'),\n            p_tenant_id, p_settlement_id,\n            'marketplace.entitlement.revoke.v1', revoke_payload,\n            encode(public.digest(convert_to(revoke_payload::text, 'UTF8'), 'sha256'), 'hex'),\n            p_refunded_at, NULL, 0);\n    END IF;\n\n    PERFORM economy_private.rebuild_wallet_projection_v1(\n        settlement.\"BuyerWalletId\", p_refunded_at);\n    PERFORM economy_private.rebuild_wallet_projection_v1(\n        settlement.\"SellerWalletId\", p_refunded_at);\n    PERFORM economy_private.rebuild_wallet_projection_v1(\n        settlement.\"PlatformFeeWalletId\", p_refunded_at);\n\n    posting_id := receipt.posting_id;\n    journal_sequence := receipt.journal_sequence;\n    journal_hash := receipt.journal_hash;\n    duplicate := false;\n    RETURN NEXT;\nEND\n$function$;\n\nALTER FUNCTION economy_private.post_marketplace_refund_v1(\n    uuid,uuid,uuid,uuid,uuid,uuid,text,bigint,bigint,uuid,text,bigint,uuid,bigint,integer,integer,\n    jsonb,text,text,timestamptz,uuid,text,bigint,text,jsonb)\n    OWNER TO gameguild_economy_procedure_owner;\nREVOKE ALL ON FUNCTION economy_private.post_marketplace_refund_v1(\n    uuid,uuid,uuid,uuid,uuid,uuid,text,bigint,bigint,uuid,text,bigint,uuid,bigint,integer,integer,\n    jsonb,text,text,timestamptz,uuid,text,bigint,text,jsonb) FROM PUBLIC;\nGRANT EXECUTE ON FUNCTION economy_private.post_marketplace_refund_v1(\n    uuid,uuid,uuid,uuid,uuid,uuid,text,bigint,bigint,uuid,text,bigint,uuid,bigint,integer,integer,\n    jsonb,text,text,timestamptz,uuid,text,bigint,text,jsonb)\n    TO gameguild_economy_writer;");
	}

	private static void InstallMarketplaceSettlementWriter(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("CREATE OR REPLACE FUNCTION economy_private.post_marketplace_settlement_v1(\n    p_capability_id uuid, p_actor_id uuid, p_tenant_id uuid, p_settlement_id uuid,\n    p_posting_id uuid, p_idempotency_key text, p_policy_version bigint,\n    p_reserve_version bigint, p_risk_decision_id uuid, p_risk_operation_fingerprint text,\n    p_expected_counter_version bigint, p_buyer_id uuid, p_buyer_wallet_id uuid,\n    p_seller_id uuid, p_seller_wallet_id uuid, p_platform_fee_wallet_id uuid,\n    p_marketplace_policy_version bigint, p_currency_mode integer, p_order jsonb,\n    p_legs jsonb, p_reservation_ids jsonb, p_entitlement_id uuid,\n    p_refund_hold_until timestamptz, p_settled_at timestamptz,\n    p_capability_receipt_id uuid, p_capability_receipt_hash text,\n    p_kill_switch_epoch bigint, p_jurisdiction_code text, p_evidence_hashes jsonb)\nRETURNS TABLE(posting_id uuid, journal_sequence bigint, journal_hash text, duplicate boolean)\nLANGUAGE plpgsql\nSECURITY DEFINER\nSET search_path = pg_catalog, economy_private\nAS $function$\nDECLARE\n    reservation_row record;\n    parent_lot record;\n    price_leg record;\n    credit_part record;\n    account_id uuid;\n    line_id uuid;\n    allocation_id uuid;\n    range_id uuid;\n    credit_id uuid;\n    credit_lot_id uuid;\n    hold_id uuid;\n    trace_scale bigint;\n    fragment_units bigint;\n    seller_remaining bigint;\n    fee_remaining bigint;\n    seller_take bigint;\n    fee_take bigint;\n    part_start bigint;\n    part_end bigint;\n    output_provenance integer;\n    output_account integer;\n    output_matures_at timestamptz;\n    next_sequence bigint;\n    lines jsonb := '[]'::jsonb;\n    allocations jsonb := '[]'::jsonb;\n    root_ranges jsonb := '[]'::jsonb;\n    expected_epochs jsonb := '[]'::jsonb;\n    funding_rows jsonb := '[]'::jsonb;\n    credit_rows jsonb := '[]'::jsonb;\n    receipt record;\n    outbox_payload jsonb;\nBEGIN\n    IF p_capability_id IS NULL OR p_actor_id IS NULL OR p_tenant_id IS NULL\n       OR p_settlement_id IS NULL OR p_posting_id IS NULL OR p_risk_decision_id IS NULL\n       OR p_buyer_id IS NULL OR p_buyer_wallet_id IS NULL OR p_seller_id IS NULL\n       OR p_seller_wallet_id IS NULL OR p_platform_fee_wallet_id IS NULL\n       OR p_entitlement_id IS NULL OR p_capability_receipt_id IS NULL\n       OR p_buyer_id = p_seller_id\n       OR p_buyer_wallet_id IN (p_seller_wallet_id, p_platform_fee_wallet_id)\n       OR p_seller_wallet_id = p_platform_fee_wallet_id\n       OR p_policy_version <= 0 OR p_reserve_version <= 0\n       OR p_marketplace_policy_version <= 0 OR p_expected_counter_version <= 0\n       OR p_kill_switch_epoch < 0 OR p_settled_at IS NULL\n       OR p_refund_hold_until <= p_settled_at\n       OR length(btrim(COALESCE(p_idempotency_key, ''))) = 0\n       OR length(btrim(COALESCE(p_capability_receipt_hash, ''))) = 0\n       OR length(btrim(COALESCE(p_jurisdiction_code, ''))) = 0\n       OR jsonb_typeof(p_order) <> 'object' OR jsonb_typeof(p_legs) <> 'array'\n       OR jsonb_typeof(p_reservation_ids) <> 'array'\n       OR jsonb_typeof(p_evidence_hashes) <> 'array'\n       OR jsonb_array_length(p_legs) = 0 OR jsonb_array_length(p_reservation_ids) = 0\n       OR (p_order->>'order_id')::uuid IS NULL\n       OR (p_order->>'line_item_id')::uuid IS NULL\n       OR (p_order->>'product_id')::uuid IS NULL\n       OR (p_order->>'pricing_version_id')::uuid IS NULL\n       OR (p_order->>'price_version')::integer <= 0\n       OR (p_order->>'quantity')::integer <= 0\n       OR (p_order->>'unit_price')::numeric < 0\n       OR length(btrim(COALESCE(p_order->>'fiat_currency', ''))) <> 3\n       OR length(btrim(COALESCE(p_order->>'snapshot_hash', ''))) = 0 THEN\n        RAISE EXCEPTION 'Marketplace settlement arguments are invalid' USING ERRCODE = '22023';\n    END IF;\n\n    SELECT settlement.\"PostingId\", settlement.\"JournalSequence\", settlement.\"JournalHash\", true\n    INTO posting_id, journal_sequence, journal_hash, duplicate\n    FROM public.economy_marketplace_settlements settlement\n    WHERE settlement.\"TenantId\" = p_tenant_id\n      AND settlement.\"IdempotencyKey\" = p_idempotency_key;\n    IF FOUND THEN\n        IF NOT EXISTS (\n            SELECT 1 FROM public.economy_marketplace_settlements settlement\n            WHERE settlement.\"Id\" = p_settlement_id\n              AND settlement.\"PostingId\" = p_posting_id\n              AND settlement.\"OrderId\" = (p_order->>'order_id')::uuid\n              AND settlement.\"OrderSnapshotHash\" = btrim(p_order->>'snapshot_hash')\n              AND settlement.\"CapabilityReceiptId\" = p_capability_receipt_id\n              AND settlement.\"CapabilityReceiptHash\" = btrim(p_capability_receipt_hash)) THEN\n            RAISE EXCEPTION 'Marketplace idempotency key is bound to another settlement'\n                USING ERRCODE = '23505';\n        END IF;\n        RETURN NEXT;\n        RETURN;\n    END IF;\n\n    IF EXISTS (\n        SELECT 1\n        FROM jsonb_to_recordset(p_legs) AS leg(\n            currency integer, units bigint, seller_units bigint, platform_fee_units bigint)\n        WHERE leg.currency NOT IN (1, 2) OR leg.units <= 0\n           OR leg.seller_units < 0 OR leg.platform_fee_units < 0\n           OR leg.seller_units + leg.platform_fee_units <> leg.units)\n       OR EXISTS (\n           SELECT 1 FROM jsonb_to_recordset(p_legs) AS leg(\n               currency integer, units bigint, seller_units bigint, platform_fee_units bigint)\n           GROUP BY leg.currency HAVING count(*) <> 1) THEN\n        RAISE EXCEPTION 'Marketplace settlement legs are invalid' USING ERRCODE = '22023';\n    END IF;\n\n    PERFORM 1\n    FROM public.economy_capability_receipts capability_receipt\n    JOIN public.economy_capability_receipt_consumptions consumption\n      ON consumption.\"ReceiptId\" = capability_receipt.\"Id\"\n    WHERE capability_receipt.\"Id\" = p_capability_receipt_id\n      AND capability_receipt.\"TenantId\" = p_tenant_id\n      AND capability_receipt.\"ActorId\" = p_actor_id\n      AND capability_receipt.\"Capability\" = 8\n      AND capability_receipt.\"PolicyVersion\" = p_policy_version\n      AND capability_receipt.\"ReserveVersion\" = p_reserve_version\n      AND capability_receipt.\"RiskDecisionId\" = p_risk_decision_id\n      AND capability_receipt.\"KillSwitchEpoch\" = p_kill_switch_epoch\n      AND capability_receipt.\"JurisdictionCode\" = upper(btrim(p_jurisdiction_code))\n      AND capability_receipt.\"OperationFingerprint\" = btrim(p_risk_operation_fingerprint)\n      AND capability_receipt.\"ReceiptHash\" = btrim(p_capability_receipt_hash)\n      AND capability_receipt.\"EvidenceHashes\" = p_evidence_hashes\n      AND capability_receipt.\"IssuedAt\" <= p_settled_at\n      AND capability_receipt.\"ExpiresAt\" > p_settled_at\n      AND consumption.\"TenantId\" = p_tenant_id\n      AND consumption.\"ActorId\" = p_actor_id\n      AND consumption.\"OperationFingerprint\" = btrim(p_risk_operation_fingerprint)\n      AND consumption.\"KillSwitchEpoch\" = p_kill_switch_epoch\n    FOR SHARE OF capability_receipt, consumption;\n    IF NOT FOUND THEN\n        RAISE EXCEPTION 'Marketplace settlement capability receipt is absent or mismatched'\n            USING ERRCODE = '42501';\n    END IF;\n\n    PERFORM 1 FROM public.economy_marketplace_currency_policy_versions policy\n    WHERE policy.\"TenantId\" = p_tenant_id\n      AND policy.\"ProductId\" = (p_order->>'product_id')::uuid\n      AND policy.\"Version\" = p_marketplace_policy_version\n      AND policy.\"SellerId\" = p_seller_id\n      AND policy.\"Mode\" = p_currency_mode\n      AND policy.\"EffectiveAt\" <= p_settled_at\n      AND policy.\"ExpiresAt\" > p_settled_at\n      AND length(btrim(policy.\"Signature\")) > 0\n      AND policy.\"KeyId\" <> 'legacy-untrusted'\n    FOR SHARE;\n    IF NOT FOUND THEN\n        RAISE EXCEPTION 'signed Marketplace policy is absent, stale, or mismatched' USING ERRCODE = '42501';\n    END IF;\n\n    PERFORM 1 FROM public.economy_wallets wallet\n    WHERE wallet.\"Id\" = p_buyer_wallet_id AND wallet.\"OwnerId\" = p_buyer_id\n      AND wallet.\"TenantId\" = p_tenant_id AND wallet.\"State\" = 1 FOR SHARE;\n    IF NOT FOUND THEN\n        RAISE EXCEPTION 'Marketplace buyer wallet is absent, cross-tenant, or inactive' USING ERRCODE = '23503';\n    END IF;\n    PERFORM 1 FROM public.economy_wallets wallet\n    WHERE wallet.\"Id\" = p_seller_wallet_id AND wallet.\"OwnerId\" = p_seller_id\n      AND wallet.\"TenantId\" = p_tenant_id AND wallet.\"State\" = 1 FOR SHARE;\n    IF NOT FOUND THEN\n        RAISE EXCEPTION 'Marketplace seller wallet is absent, cross-tenant, or inactive' USING ERRCODE = '23503';\n    END IF;\n    PERFORM 1 FROM public.economy_wallets wallet\n    WHERE wallet.\"Id\" = p_platform_fee_wallet_id\n      AND wallet.\"TenantId\" = p_tenant_id AND wallet.\"State\" = 1 FOR SHARE;\n    IF NOT FOUND THEN\n        RAISE EXCEPTION 'Marketplace fee wallet is absent, cross-tenant, or inactive' USING ERRCODE = '23503';\n    END IF;\n\n    IF jsonb_array_length(p_reservation_ids) <>\n           (SELECT count(DISTINCT value) FROM jsonb_array_elements_text(p_reservation_ids))\n       OR EXISTS (\n           SELECT 1 FROM jsonb_array_elements_text(p_reservation_ids) requested\n           LEFT JOIN public.economy_fragment_reservations reserved\n             ON reserved.\"Id\" = requested.value::uuid\n           WHERE reserved.\"Id\" IS NULL OR reserved.\"OperationId\" <> p_settlement_id\n              OR reserved.\"WalletId\" <> p_buyer_wallet_id\n              OR reserved.\"Purpose\" <> 7 OR reserved.\"Status\" <> 1)\n       OR EXISTS (\n           SELECT 1\n           FROM (\n               SELECT leg.currency, leg.units\n               FROM jsonb_to_recordset(p_legs) AS leg(\n                   currency integer, units bigint, seller_units bigint, platform_fee_units bigint)) expected\n           FULL JOIN (\n               SELECT reserved.\"Currency\" AS currency,\n                      sum((reserved.\"EndExclusive\" - reserved.\"StartInclusive\") /\n                          CASE WHEN reserved.\"Currency\" = 1 THEN 1000 ELSE 1 END) AS units\n               FROM public.economy_fragment_reservations reserved\n               JOIN jsonb_array_elements_text(p_reservation_ids) requested\n                 ON requested.value::uuid = reserved.\"Id\"\n               GROUP BY reserved.\"Currency\") actual USING (currency)\n           WHERE expected.currency IS NULL OR actual.currency IS NULL OR expected.units <> actual.units) THEN\n        RAISE EXCEPTION 'Marketplace reservation set is incomplete, stale, or mismatched'\n            USING ERRCODE = '23514';\n    END IF;\n\n    INSERT INTO public.economy_chain_head (\"Id\", \"Sequence\", \"Hash\", \"UpdatedAt\")\n    VALUES (1, 0, repeat('0', 64), p_settled_at)\n    ON CONFLICT (\"Id\") DO NOTHING;\n    SELECT \"Sequence\" + 1 INTO next_sequence\n    FROM public.economy_chain_head WHERE \"Id\" = 1 FOR UPDATE;\n\n    -- First materialize the buyer debits and their exact FIFO ranges.\n    FOR reservation_row IN\n        SELECT reserved.*\n        FROM public.economy_fragment_reservations reserved\n        JOIN jsonb_array_elements_text(p_reservation_ids) requested\n          ON requested.value::uuid = reserved.\"Id\"\n        ORDER BY reserved.\"Currency\", reserved.\"ReservedAt\", reserved.\"ParentLotId\",\n                 reserved.\"StartInclusive\"\n        FOR UPDATE OF reserved\n    LOOP\n        SELECT * INTO parent_lot FROM public.economy_credit_lots lot\n        WHERE lot.\"Id\" = reservation_row.\"ParentLotId\" FOR SHARE;\n        trace_scale := CASE WHEN reservation_row.\"Currency\" = 1 THEN 1000 ELSE 1 END;\n        fragment_units := (reservation_row.\"EndExclusive\" - reservation_row.\"StartInclusive\") / trace_scale;\n        output_account := CASE WHEN reservation_row.\"Currency\" = 1\n            THEN CASE WHEN parent_lot.\"Provenance\" = 2 THEN 3 ELSE 2 END ELSE 4 END;\n        SELECT account.\"Id\" INTO account_id FROM public.economy_accounts account\n        WHERE account.\"WalletId\" = p_buyer_wallet_id\n          AND account.\"Code\" = output_account\n          AND account.\"Currency\" = reservation_row.\"Currency\"\n          AND account.\"Provenance\" = parent_lot.\"Provenance\" FOR SHARE;\n        IF account_id IS NULL THEN\n            RAISE EXCEPTION 'Marketplace buyer liability account is not provisioned' USING ERRCODE = '23503';\n        END IF;\n\n        line_id := reservation_row.\"Id\";\n        allocation_id := economy_private.derive_economy_uuid_v1(reservation_row.\"Id\", 'marketplace-debit-allocation');\n        range_id := economy_private.derive_economy_uuid_v1(reservation_row.\"Id\", 'marketplace-debit-range');\n        lines := lines || jsonb_build_array(jsonb_build_object(\n            'id', line_id, 'account_id', account_id, 'account_code', output_account,\n            'wallet_id', p_buyer_wallet_id, 'credit_lot_id', '', 'side', 1,\n            'currency', reservation_row.\"Currency\", 'amount_units', fragment_units,\n            'provenance', parent_lot.\"Provenance\"));\n        allocations := allocations || jsonb_build_array(jsonb_build_object(\n            'id', allocation_id, 'journal_line_id', line_id,\n            'parent_lot_id', reservation_row.\"ParentLotId\", 'amount_units', fragment_units));\n        root_ranges := root_ranges || jsonb_build_array(jsonb_build_object(\n            'id', range_id, 'root_source_stamp_id', reservation_row.\"RootSourceStampId\",\n            'credit_lot_id', '', 'entry_allocation_id', allocation_id,\n            'start_inclusive', reservation_row.\"StartInclusive\",\n            'end_exclusive', reservation_row.\"EndExclusive\",\n            'reversal_epoch', reservation_row.\"ReversalEpoch\"));\n        IF NOT EXISTS (\n            SELECT 1 FROM jsonb_array_elements(expected_epochs) epoch\n            WHERE (epoch->>'root_source_stamp_id')::uuid = reservation_row.\"RootSourceStampId\") THEN\n            expected_epochs := expected_epochs || jsonb_build_array(jsonb_build_object(\n                'root_source_stamp_id', reservation_row.\"RootSourceStampId\",\n                'expected_epoch', reservation_row.\"ReversalEpoch\"));\n        END IF;\n        funding_rows := funding_rows || jsonb_build_array(jsonb_build_object(\n            'id', economy_private.derive_economy_uuid_v1(reservation_row.\"Id\", 'marketplace-funding-fragment'),\n            'parent_lot_id', reservation_row.\"ParentLotId\", 'currency', reservation_row.\"Currency\",\n            'amount_units', fragment_units, 'reservation_id', reservation_row.\"Id\",\n            'trace_scale', trace_scale,\n            'ranges', jsonb_build_array(jsonb_build_object(\n                'RootSourceStampId', reservation_row.\"RootSourceStampId\",\n                'StartInclusive', reservation_row.\"StartInclusive\",\n                'EndExclusive', reservation_row.\"EndExclusive\",\n                'ReversalEpoch', reservation_row.\"ReversalEpoch\"))));\n    END LOOP;\n\n    -- Split each reserved range deterministically between seller proceeds\n    -- and platform fee. Every output lot retains a parent edge and the\n    -- exact root interval that funded it.\n    FOR price_leg IN\n        SELECT * FROM jsonb_to_recordset(p_legs) AS leg(\n            currency integer, units bigint, seller_units bigint, platform_fee_units bigint)\n        ORDER BY currency\n    LOOP\n        seller_remaining := price_leg.seller_units;\n        fee_remaining := price_leg.platform_fee_units;\n        FOR reservation_row IN\n            SELECT reserved.*\n            FROM public.economy_fragment_reservations reserved\n            JOIN jsonb_array_elements_text(p_reservation_ids) requested\n              ON requested.value::uuid = reserved.\"Id\"\n            WHERE reserved.\"Currency\" = price_leg.currency\n            ORDER BY reserved.\"ReservedAt\", reserved.\"ParentLotId\", reserved.\"StartInclusive\"\n        LOOP\n            SELECT * INTO parent_lot FROM public.economy_credit_lots lot\n            WHERE lot.\"Id\" = reservation_row.\"ParentLotId\" FOR SHARE;\n            trace_scale := CASE WHEN reservation_row.\"Currency\" = 1 THEN 1000 ELSE 1 END;\n            fragment_units := (reservation_row.\"EndExclusive\" - reservation_row.\"StartInclusive\") / trace_scale;\n            seller_take := LEAST(fragment_units, seller_remaining);\n            fee_take := fragment_units - seller_take;\n            IF fee_take > fee_remaining THEN\n                RAISE EXCEPTION 'Marketplace fee split exceeds the signed quote' USING ERRCODE = '23514';\n            END IF;\n            part_start := reservation_row.\"StartInclusive\";\n\n            FOR credit_part IN\n                SELECT * FROM (VALUES\n                    (1, p_seller_wallet_id, seller_take),\n                    (2, p_platform_fee_wallet_id, fee_take)\n                ) AS part(purpose, wallet_id, units)\n            LOOP\n                IF credit_part.units = 0 THEN\n                    CONTINUE;\n                END IF;\n                part_end := part_start + credit_part.units * trace_scale;\n                output_provenance := CASE WHEN reservation_row.\"Currency\" = 1 THEN 2 ELSE 8 END;\n                output_account := CASE WHEN reservation_row.\"Currency\" = 1 THEN 3 ELSE 4 END;\n                output_matures_at := CASE WHEN reservation_row.\"Currency\" = 1\n                    THEN p_settled_at + interval '120 days' ELSE p_settled_at END;\n                SELECT account.\"Id\" INTO account_id FROM public.economy_accounts account\n                WHERE account.\"WalletId\" = credit_part.wallet_id\n                  AND account.\"Code\" = output_account\n                  AND account.\"Currency\" = reservation_row.\"Currency\"\n                  AND account.\"Provenance\" = output_provenance FOR SHARE;\n                IF account_id IS NULL THEN\n                    RAISE EXCEPTION 'Marketplace recipient liability account is not provisioned'\n                        USING ERRCODE = '23503';\n                END IF;\n\n                credit_id := economy_private.derive_economy_uuid_v1(\n                    reservation_row.\"Id\", 'marketplace-credit:' || credit_part.purpose::text);\n                credit_lot_id := economy_private.derive_economy_uuid_v1(\n                    reservation_row.\"Id\", 'marketplace-credit-lot:' || credit_part.purpose::text);\n                line_id := economy_private.derive_economy_uuid_v1(\n                    reservation_row.\"Id\", 'marketplace-credit-line:' || credit_part.purpose::text);\n                hold_id := economy_private.derive_economy_uuid_v1(\n                    reservation_row.\"Id\", 'marketplace-refund-hold:' || credit_part.purpose::text);\n\n                INSERT INTO public.economy_credit_lots (\n                    \"Id\", \"WalletId\", \"RootSourceStampId\", \"Currency\", \"AmountUnits\", \"Provenance\",\n                    \"CreditedAt\", \"ConfirmedAt\", \"OriginalMaturesAt\", \"CashOutEligible\",\n                    \"JournalSequence\", \"State\", \"ReversalEpoch\")\n                VALUES (credit_lot_id, credit_part.wallet_id, reservation_row.\"RootSourceStampId\",\n                    reservation_row.\"Currency\", credit_part.units, output_provenance,\n                    p_settled_at, p_settled_at, output_matures_at,\n                    reservation_row.\"Currency\" = 1, next_sequence, 1, reservation_row.\"ReversalEpoch\");\n                INSERT INTO public.economy_lot_lineage_edges (\n                    \"Id\", \"ParentLotId\", \"ChildLotId\", \"Currency\", \"AmountUnits\")\n                VALUES (economy_private.derive_economy_uuid_v1(\n                        credit_lot_id, 'marketplace-lineage'), reservation_row.\"ParentLotId\",\n                    credit_lot_id, reservation_row.\"Currency\", credit_part.units);\n                INSERT INTO public.economy_fragment_root_ranges (\n                    \"Id\", \"RootSourceStampId\", \"CreditLotId\", \"EntryAllocationId\",\n                    \"StartInclusive\", \"EndExclusive\", \"ReversalEpoch\")\n                VALUES (economy_private.derive_economy_uuid_v1(\n                        credit_lot_id, 'marketplace-root-range'), reservation_row.\"RootSourceStampId\",\n                    credit_lot_id, NULL, part_start, part_end, reservation_row.\"ReversalEpoch\");\n                INSERT INTO public.economy_holds (\n                    \"Id\", \"WalletId\", \"Currency\", \"AmountUnits\", \"Reason\", \"Status\",\n                    \"EffectiveAt\", \"ReleasedAt\")\n                VALUES (hold_id, credit_part.wallet_id, reservation_row.\"Currency\", credit_part.units,\n                    3, 1, p_settled_at, NULL);\n                INSERT INTO public.economy_hold_events (\n                    \"Id\", \"HoldId\", \"Sequence\", \"Kind\", \"ActorId\", \"EvidenceHash\", \"OccurredAt\")\n                VALUES (economy_private.derive_economy_uuid_v1(hold_id, 'placed'), hold_id, 1, 1,\n                    p_actor_id, p_capability_receipt_hash, p_settled_at);\n\n                lines := lines || jsonb_build_array(jsonb_build_object(\n                    'id', line_id, 'account_id', account_id, 'account_code', output_account,\n                    'wallet_id', credit_part.wallet_id, 'credit_lot_id', credit_lot_id,\n                    'side', 2, 'currency', reservation_row.\"Currency\",\n                    'amount_units', credit_part.units, 'provenance', output_provenance));\n                credit_rows := credit_rows || jsonb_build_array(jsonb_build_object(\n                    'id', credit_id, 'purpose', credit_part.purpose,\n                    'wallet_id', credit_part.wallet_id, 'credit_lot_id', credit_lot_id,\n                    'currency', reservation_row.\"Currency\", 'amount_units', credit_part.units,\n                    'hold_id', hold_id,\n                    'lineage', jsonb_build_array(jsonb_build_object(\n                        'ParentLotId', reservation_row.\"ParentLotId\",\n                        'AmountUnits', credit_part.units,\n                        'RootSourceStampId', reservation_row.\"RootSourceStampId\",\n                        'StartInclusive', part_start, 'EndExclusive', part_end,\n                        'ReversalEpoch', reservation_row.\"ReversalEpoch\"))));\n                part_start := part_end;\n            END LOOP;\n            seller_remaining := seller_remaining - seller_take;\n            fee_remaining := fee_remaining - fee_take;\n        END LOOP;\n        IF seller_remaining <> 0 OR fee_remaining <> 0 THEN\n            RAISE EXCEPTION 'Marketplace credit split does not conserve the signed quote'\n                USING ERRCODE = '23514';\n        END IF;\n    END LOOP;\n\n    SELECT * INTO receipt FROM economy_private.post_registered_posting_v1(\n        p_capability_id, p_actor_id, p_tenant_id, p_posting_id, p_idempotency_key,\n        25, 1, 7, p_policy_version, p_reserve_version, p_risk_decision_id,\n        p_risk_operation_fingerprint, p_expected_counter_version,\n        NULL, NULL, p_settled_at, lines, allocations, root_ranges, expected_epochs,\n        p_capability_receipt_hash);\n    IF receipt.duplicate THEN\n        RAISE EXCEPTION 'unexpected duplicate during Marketplace settlement' USING ERRCODE = '40001';\n    END IF;\n\n    INSERT INTO public.economy_marketplace_settlements (\n        \"Id\", \"TenantId\", \"OrderId\", \"OrderLineItemId\", \"ProductId\", \"ProductPricingVersionId\",\n        \"PriceVersionSnapshot\", \"Quantity\", \"RefundedQuantity\", \"UnitPriceSnapshot\",\n        \"FiatCurrencySnapshot\", \"OrderSnapshotHash\", \"BuyerId\", \"BuyerWalletId\", \"SellerId\",\n        \"SellerWalletId\", \"PlatformFeeWalletId\", \"PolicyVersion\", \"CurrencyMode\", \"Status\",\n        \"IdempotencyKey\", \"EntitlementId\", \"EntitlementStatus\", \"PostingId\", \"JournalSequence\",\n        \"JournalHash\", \"CapabilityReceiptId\", \"CapabilityReceiptHash\", \"ReserveVersion\",\n        \"RiskDecisionId\", \"KillSwitchEpoch\", \"JurisdictionCode\", \"EvidenceHashes\",\n        \"RefundHoldUntil\", \"SettledAt\", \"UpdatedAt\", \"Version\")\n    VALUES (p_settlement_id, p_tenant_id, (p_order->>'order_id')::uuid,\n        (p_order->>'line_item_id')::uuid, (p_order->>'product_id')::uuid,\n        (p_order->>'pricing_version_id')::uuid, (p_order->>'price_version')::integer,\n        (p_order->>'quantity')::integer, 0, (p_order->>'unit_price')::numeric,\n        upper(btrim(p_order->>'fiat_currency')), btrim(p_order->>'snapshot_hash'),\n        p_buyer_id, p_buyer_wallet_id, p_seller_id, p_seller_wallet_id, p_platform_fee_wallet_id,\n        p_marketplace_policy_version, p_currency_mode, 1, p_idempotency_key,\n        p_entitlement_id, 1, p_posting_id, receipt.journal_sequence, receipt.journal_hash,\n        p_capability_receipt_id, btrim(p_capability_receipt_hash), p_reserve_version,\n        p_risk_decision_id, p_kill_switch_epoch, upper(btrim(p_jurisdiction_code)),\n        p_evidence_hashes, p_refund_hold_until, p_settled_at, p_settled_at, 1);\n\n    INSERT INTO public.economy_marketplace_settlement_legs (\n        \"SettlementId\", \"Currency\", \"Units\", \"SellerUnits\", \"PlatformFeeUnits\", \"RefundedUnits\")\n    SELECT p_settlement_id, leg.currency, leg.units, leg.seller_units, leg.platform_fee_units, 0\n    FROM jsonb_to_recordset(p_legs) AS leg(\n        currency integer, units bigint, seller_units bigint, platform_fee_units bigint);\n\n    INSERT INTO public.economy_marketplace_funding_fragments (\n        \"Id\", \"SettlementId\", \"ParentLotId\", \"Currency\", \"AmountUnits\", \"ReservationId\",\n        \"TraceUnitsPerCoinUnit\", \"SelectedRootRanges\")\n    SELECT (row->>'id')::uuid, p_settlement_id, (row->>'parent_lot_id')::uuid,\n        (row->>'currency')::integer, (row->>'amount_units')::bigint,\n        (row->>'reservation_id')::uuid, (row->>'trace_scale')::bigint, row->'ranges'\n    FROM jsonb_array_elements(funding_rows) row;\n\n    INSERT INTO public.economy_marketplace_settlement_credits (\n        \"Id\", \"SettlementId\", \"Purpose\", \"WalletId\", \"CreditLotId\", \"SourceStampId\",\n        \"Currency\", \"AmountUnits\", \"RemainingUnits\", \"RefundHoldId\", \"RefundHoldUntil\", \"ParentLineage\")\n    SELECT (row->>'id')::uuid, p_settlement_id, (row->>'purpose')::integer,\n        (row->>'wallet_id')::uuid, (row->>'credit_lot_id')::uuid, NULL,\n        (row->>'currency')::integer, (row->>'amount_units')::bigint,\n        (row->>'amount_units')::bigint, (row->>'hold_id')::uuid,\n        p_refund_hold_until, row->'lineage'\n    FROM jsonb_array_elements(credit_rows) row;\n\n    INSERT INTO public.economy_marketplace_events (\n        \"Id\", \"TenantId\", \"SettlementId\", \"Sequence\", \"EventKind\", \"EvidenceHash\", \"OccurredAt\")\n    VALUES (economy_private.derive_economy_uuid_v1(p_settlement_id, 'settled-event'),\n        p_tenant_id, p_settlement_id, 1, 'Settled', p_capability_receipt_hash, p_settled_at);\n    outbox_payload := jsonb_build_object(\n        'settlementId', p_settlement_id, 'entitlementId', p_entitlement_id,\n        'orderId', (p_order->>'order_id')::uuid, 'productId', (p_order->>'product_id')::uuid,\n        'buyerId', p_buyer_id, 'occurredAt', p_settled_at);\n    INSERT INTO public.economy_marketplace_outbox (\n        \"Id\", \"TenantId\", \"SettlementId\", \"MessageType\", \"Payload\", \"PayloadHash\",\n        \"OccurredAt\", \"PublishedAt\", \"AttemptCount\")\n    VALUES (economy_private.derive_economy_uuid_v1(p_settlement_id, 'entitlement-grant-outbox'),\n        p_tenant_id, p_settlement_id, 'marketplace.entitlement.grant.v1', outbox_payload,\n        encode(public.digest(convert_to(outbox_payload::text, 'UTF8'), 'sha256'), 'hex'),\n        p_settled_at, NULL, 0);\n\n    PERFORM economy_private.transition_fifo_fragment_reservations_v1(\n        p_settlement_id, 1, 3, p_settled_at);\n    PERFORM economy_private.rebuild_wallet_projection_v1(p_buyer_wallet_id, p_settled_at);\n    PERFORM economy_private.rebuild_wallet_projection_v1(p_seller_wallet_id, p_settled_at);\n    PERFORM economy_private.rebuild_wallet_projection_v1(p_platform_fee_wallet_id, p_settled_at);\n\n    posting_id := receipt.posting_id;\n    journal_sequence := receipt.journal_sequence;\n    journal_hash := receipt.journal_hash;\n    duplicate := false;\n    RETURN NEXT;\nEND\n$function$;\n\nALTER FUNCTION economy_private.post_marketplace_settlement_v1(\n    uuid,uuid,uuid,uuid,uuid,text,bigint,bigint,uuid,text,bigint,uuid,uuid,uuid,uuid,uuid,\n    bigint,integer,jsonb,jsonb,jsonb,uuid,timestamptz,timestamptz,uuid,text,bigint,text,jsonb)\n    OWNER TO gameguild_economy_procedure_owner;\nREVOKE ALL ON FUNCTION economy_private.post_marketplace_settlement_v1(\n    uuid,uuid,uuid,uuid,uuid,text,bigint,bigint,uuid,text,bigint,uuid,uuid,uuid,uuid,uuid,\n    bigint,integer,jsonb,jsonb,jsonb,uuid,timestamptz,timestamptz,uuid,text,bigint,text,jsonb) FROM PUBLIC;\nGRANT EXECUTE ON FUNCTION economy_private.post_marketplace_settlement_v1(\n    uuid,uuid,uuid,uuid,uuid,text,bigint,bigint,uuid,text,bigint,uuid,uuid,uuid,uuid,uuid,\n    bigint,integer,jsonb,jsonb,jsonb,uuid,timestamptz,timestamptz,uuid,text,bigint,text,jsonb)\n    TO gameguild_economy_writer;");
	}

	private static void InstallEconomyProductionPostingValidator(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("ALTER FUNCTION economy_private.validate_posting_lines_v1(integer,jsonb)\n    RENAME TO validate_posting_lines_before_economy_production_v1;\n\nCREATE FUNCTION economy_private.validate_posting_lines_v1(\n    p_template_kind integer,\n    p_lines jsonb)\nRETURNS boolean\nLANGUAGE plpgsql\nSECURITY DEFINER\nSET search_path = pg_catalog, economy_private\nAS $function$\nDECLARE\n    line_count integer;\nBEGIN\n    IF jsonb_typeof(p_lines) <> 'array' THEN\n        RETURN false;\n    END IF;\n    line_count := jsonb_array_length(p_lines);\n\n    IF p_template_kind = 21 THEN\n        RETURN line_count = 2\n           AND (p_lines->0->>'side')::integer = 1\n           AND (p_lines->0->>'account_code')::integer = 6\n           AND (p_lines->0->>'currency')::integer = 2\n           AND NULLIF(p_lines->0->>'wallet_id', '') IS NULL\n           AND NULLIF(p_lines->0->>'provenance', '') IS NULL\n           AND (p_lines->1->>'side')::integer = 2\n           AND (p_lines->1->>'account_code')::integer = 4\n           AND (p_lines->1->>'currency')::integer = 2\n           AND NULLIF(p_lines->1->>'wallet_id', '') IS NOT NULL\n           AND (p_lines->1->>'provenance')::integer = 4\n           AND (p_lines->0->>'amount_units')::bigint > 0\n           AND (p_lines->0->>'amount_units')::bigint =\n               (p_lines->1->>'amount_units')::bigint;\n    END IF;\n\n    IF p_template_kind IN (25, 26) THEN\n        IF line_count < (CASE WHEN p_template_kind = 25 THEN 3 ELSE 2 END)\n           OR NOT EXISTS (\n               SELECT 1 FROM jsonb_array_elements(p_lines) line\n               WHERE (line->>'side')::integer = 1)\n           OR NOT EXISTS (\n               SELECT 1 FROM jsonb_array_elements(p_lines) line\n               WHERE (line->>'side')::integer = 2)\n           OR EXISTS (\n               SELECT 1 FROM jsonb_array_elements(p_lines) line\n               WHERE (line->>'amount_units')::bigint <= 0\n                  OR (line->>'side')::integer NOT IN (1, 2)\n                  OR (line->>'currency')::integer NOT IN (1, 2))\n           OR EXISTS (\n               SELECT 1\n               FROM jsonb_array_elements(p_lines) line\n               GROUP BY (line->>'currency')::integer\n               HAVING sum(CASE WHEN (line->>'side')::integer = 1\n                               THEN (line->>'amount_units')::bigint\n                               ELSE -(line->>'amount_units')::bigint END) <> 0) THEN\n            RETURN false;\n        END IF;\n\n        IF p_template_kind = 25 AND EXISTS (\n            SELECT 1 FROM jsonb_array_elements(p_lines) line\n            WHERE NULLIF(line->>'wallet_id', '') IS NULL\n               OR ((line->>'currency')::integer = 1 AND (\n                   (line->>'account_code')::integer NOT IN (2, 3)\n                   OR ((line->>'account_code')::integer = 3) <>\n                      ((line->>'provenance')::integer = 2)))\n               OR ((line->>'currency')::integer = 2 AND (\n                   (line->>'account_code')::integer <> 4\n                   OR (line->>'provenance')::integer NOT BETWEEN 3 AND 8))) THEN\n            RETURN false;\n        END IF;\n\n        IF p_template_kind = 26 AND EXISTS (\n            SELECT 1 FROM jsonb_array_elements(p_lines) line\n            WHERE NOT (\n                ((line->>'side')::integer = 1\n                 AND (line->>'account_code')::integer IN (13, 16)\n                 AND NULLIF(line->>'wallet_id', '') IS NULL\n                 AND NULLIF(line->>'provenance', '') IS NULL\n                 AND (((line->>'account_code')::integer = 13\n                       AND (line->>'currency')::integer = 1)\n                      OR ((line->>'account_code')::integer = 16\n                          AND (line->>'currency')::integer = 2)))\n                OR (NULLIF(line->>'wallet_id', '') IS NOT NULL\n                    AND (line->>'account_code')::integer IN (2, 3, 4)\n                    AND (((line->>'currency')::integer = 1\n                          AND (line->>'account_code')::integer IN (2, 3)\n                          AND ((line->>'account_code')::integer = 3) =\n                              ((line->>'provenance')::integer = 2))\n                         OR ((line->>'currency')::integer = 2\n                             AND (line->>'account_code')::integer = 4\n                             AND (line->>'provenance')::integer BETWEEN 3 AND 8))))) THEN\n            RETURN false;\n        END IF;\n\n        IF p_template_kind = 25 AND EXISTS (\n            SELECT 1 FROM jsonb_array_elements(p_lines) line\n            WHERE (line->>'side')::integer = 2\n              AND (((line->>'currency')::integer = 1\n                    AND ((line->>'account_code')::integer <> 3\n                         OR (line->>'provenance')::integer <> 2))\n                   OR ((line->>'currency')::integer = 2\n                       AND ((line->>'account_code')::integer <> 4\n                            OR (line->>'provenance')::integer <> 8)))) THEN\n            RETURN false;\n        END IF;\n        RETURN true;\n    END IF;\n\n    RETURN economy_private.validate_posting_lines_before_economy_production_v1(\n        p_template_kind, p_lines);\nEXCEPTION\n    WHEN invalid_text_representation OR numeric_value_out_of_range THEN\n        RETURN false;\nEND\n$function$;\n\nALTER FUNCTION economy_private.validate_posting_lines_v1(integer,jsonb)\n    OWNER TO gameguild_economy_procedure_owner;\nALTER FUNCTION economy_private.validate_posting_lines_before_economy_production_v1(integer,jsonb)\n    OWNER TO gameguild_economy_procedure_owner;\nREVOKE ALL ON FUNCTION economy_private.validate_posting_lines_v1(integer,jsonb) FROM PUBLIC;\nREVOKE ALL ON FUNCTION economy_private.validate_posting_lines_before_economy_production_v1(integer,jsonb) FROM PUBLIC;\nGRANT EXECUTE ON FUNCTION economy_private.validate_posting_lines_v1(integer,jsonb),\n    economy_private.validate_posting_lines_before_economy_production_v1(integer,jsonb)\n    TO gameguild_economy_writer;");
	}

	private static void RemoveEconomyProductionPostingValidator(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("DROP FUNCTION IF EXISTS economy_private.validate_posting_lines_v1(integer,jsonb);\nALTER FUNCTION economy_private.validate_posting_lines_before_economy_production_v1(integer,jsonb)\n    RENAME TO validate_posting_lines_v1;\nALTER FUNCTION economy_private.validate_posting_lines_v1(integer,jsonb)\n    OWNER TO gameguild_economy_procedure_owner;\nREVOKE ALL ON FUNCTION economy_private.validate_posting_lines_v1(integer,jsonb) FROM PUBLIC;\nGRANT EXECUTE ON FUNCTION economy_private.validate_posting_lines_v1(integer,jsonb)\n    TO gameguild_economy_writer;");
	}

	private static void InstallRegisteredPostingRiskAggregation(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("ALTER FUNCTION economy_private.post_registered_posting_v1(\n    uuid,uuid,uuid,uuid,text,integer,integer,integer,bigint,bigint,uuid,text,bigint,\n    uuid,text,timestamptz,jsonb,jsonb,jsonb,jsonb,text)\n    RENAME TO post_registered_posting_before_risk_aggregation_v1;\n\nCREATE OR REPLACE FUNCTION economy_private.post_registered_posting_v1(\n    p_capability_id uuid,\n    p_actor_id uuid,\n    p_tenant_id uuid,\n    p_posting_id uuid,\n    p_idempotency_key text,\n    p_template_kind integer,\n    p_template_version integer,\n    p_authority integer,\n    p_policy_version bigint,\n    p_reserve_version bigint,\n    p_risk_decision_id uuid,\n    p_risk_operation_fingerprint text,\n    p_expected_counter_version bigint,\n    p_source_stamp_id uuid,\n    p_source_evidence_hash text,\n    p_requested_at timestamptz,\n    p_lines jsonb,\n    p_allocations jsonb,\n    p_root_ranges jsonb,\n    p_expected_reversal_epochs jsonb,\n    p_dispatch_snapshot_hash text)\nRETURNS TABLE(posting_id uuid, journal_sequence bigint, journal_hash text, duplicate boolean)\nLANGUAGE plpgsql\nSECURITY DEFINER\nSET search_path = pg_catalog, economy_private\nAS $function$\nDECLARE\n    risk_record record;\n    chain_record record;\n    canonical text;\n    request_hash text;\n    existing_request_hash text;\n    outbox_payload text;\n    protected_amount bigint;\n    risk_found boolean;\nBEGIN\n    IF p_posting_id IS NULL OR p_actor_id IS NULL OR p_tenant_id IS NULL\n       OR p_idempotency_key IS NULL OR length(btrim(p_idempotency_key)) = 0\n       OR p_template_version <> 1 OR p_policy_version <= 0 OR p_reserve_version <= 0\n       OR p_expected_counter_version <= 0\n       OR jsonb_typeof(p_lines) <> 'array'\n       OR jsonb_typeof(p_allocations) <> 'array'\n       OR jsonb_typeof(p_root_ranges) <> 'array'\n       OR jsonb_typeof(p_expected_reversal_epochs) <> 'array' THEN\n        RAISE EXCEPTION 'invalid registered posting arguments' USING ERRCODE = '22023';\n    END IF;\n\n    request_hash := encode(public.digest(convert_to(jsonb_build_object(\n        'capabilityId', p_capability_id,\n        'actorId', p_actor_id,\n        'tenantId', p_tenant_id,\n        'postingId', p_posting_id,\n        'idempotencyKey', p_idempotency_key,\n        'templateKind', p_template_kind,\n        'templateVersion', p_template_version,\n        'authority', p_authority,\n        'policyVersion', p_policy_version,\n        'reserveVersion', p_reserve_version,\n        'riskDecisionId', p_risk_decision_id,\n        'riskOperationFingerprint', p_risk_operation_fingerprint,\n        'counterVersion', p_expected_counter_version,\n        'sourceStampId', p_source_stamp_id,\n        'sourceEvidenceHash', p_source_evidence_hash,\n        'requestedAt', p_requested_at,\n        'lines', p_lines,\n        'allocations', p_allocations,\n        'rootRanges', p_root_ranges,\n        'reversalEpochs', p_expected_reversal_epochs,\n        'dispatchSnapshotHash', p_dispatch_snapshot_hash)::text, 'UTF8'), 'sha256'), 'hex');\n\n    SELECT pg.\"Id\", je.\"Sequence\", je.\"Hash\", idempotency.\"RequestHash\"\n    INTO posting_id, journal_sequence, journal_hash, existing_request_hash\n    FROM public.economy_posting_groups pg\n    JOIN public.economy_journal_entries je ON je.\"PostingGroupId\" = pg.\"Id\"\n    JOIN public.economy_idempotency_records idempotency ON idempotency.\"PostingGroupId\" = pg.\"Id\"\n    WHERE pg.\"IdempotencyKey\" = p_idempotency_key;\n    IF FOUND THEN\n        IF posting_id <> p_posting_id OR existing_request_hash <> request_hash THEN\n            RAISE EXCEPTION 'idempotency key is bound to another request' USING ERRCODE = '23505';\n        END IF;\n        duplicate := true;\n        RETURN NEXT;\n        RETURN;\n    END IF;\n\n    PERFORM 1\n    FROM public.economy_registered_capabilities capability\n    WHERE capability.\"Id\" = p_capability_id\n      AND capability.\"IsEnabled\"\n      AND capability.\"RevokedAt\" IS NULL\n      AND capability.\"AllowedTemplateKinds\" @> jsonb_build_array(p_template_kind)\n    FOR SHARE;\n    IF NOT FOUND THEN\n        RAISE EXCEPTION 'caller capability is absent, disabled, or unauthorized' USING ERRCODE = '42501';\n    END IF;\n\n    IF NOT economy_private.validate_posting_lines_v1(p_template_kind, p_lines) THEN\n        RAISE EXCEPTION 'posting lines do not match the registered template' USING ERRCODE = '23514';\n    END IF;\n\n    SELECT * INTO risk_record\n    FROM public.economy_risk_decisions decision\n    WHERE decision.\"Id\" = p_risk_decision_id\n    FOR UPDATE;\n    risk_found := FOUND;\n    IF risk_found THEN\n        SELECT COALESCE(sum((line->>'amount_units')::bigint), 0)\n        INTO protected_amount\n        FROM jsonb_array_elements(p_lines) line\n        WHERE (line->>'side')::integer = 1\n          AND (line->>'currency')::integer = risk_record.\"Currency\";\n    END IF;\n    IF NOT risk_found\n       OR risk_record.\"Outcome\" <> 1\n       OR risk_record.\"OperationFingerprint\" <> p_risk_operation_fingerprint\n       OR risk_record.\"TemplateKind\" <> p_template_kind\n       OR risk_record.\"PolicyVersion\" <> p_policy_version\n       OR risk_record.\"ReserveVersion\" <> p_reserve_version\n       OR risk_record.\"CounterVersion\" <> p_expected_counter_version\n       OR risk_record.\"AmountUnits\" <> protected_amount\n       OR risk_record.\"IssuedAt\" > p_requested_at\n       OR risk_record.\"ExpiresAt\" <= p_requested_at THEN\n        RAISE EXCEPTION 'risk decision is missing, stale, denied, or operation-mismatched' USING ERRCODE = '42501';\n    END IF;\n\n    IF EXISTS (\n        SELECT 1 FROM public.economy_risk_decision_consumptions\n        WHERE \"RiskDecisionId\" = p_risk_decision_id\n    ) THEN\n        RAISE EXCEPTION 'risk decision has already been consumed' USING ERRCODE = '23505';\n    END IF;\n    IF NOT EXISTS (\n        SELECT 1\n        FROM public.economy_risk_counter_reservations reservation\n        JOIN public.economy_risk_counters counter ON counter.\"Id\" = reservation.\"RiskCounterId\"\n        WHERE reservation.\"RiskDecisionId\" = p_risk_decision_id\n          AND reservation.\"AmountUnits\" = risk_record.\"AmountUnits\"\n          AND counter.\"CounterVersion\" = p_expected_counter_version\n    ) THEN\n        RAISE EXCEPTION 'risk decision has no persisted aggregate-counter reservation' USING ERRCODE = '42501';\n    END IF;\n\n    IF EXISTS (\n        SELECT 1\n        FROM jsonb_array_elements(p_lines) line\n        LEFT JOIN public.economy_accounts account ON account.\"Id\" = (line->>'account_id')::uuid\n        WHERE account.\"Id\" IS NULL\n           OR account.\"Code\" <> (line->>'account_code')::integer\n           OR account.\"Currency\" <> (line->>'currency')::integer\n           OR account.\"WalletId\" IS DISTINCT FROM NULLIF(line->>'wallet_id', '')::uuid\n           OR account.\"Provenance\" IS DISTINCT FROM NULLIF(line->>'provenance', '')::integer\n    ) THEN\n        RAISE EXCEPTION 'posting line does not match its registered account partition' USING ERRCODE = '23514';\n    END IF;\n\n    IF EXISTS (\n        SELECT 1\n        FROM jsonb_array_elements(p_expected_reversal_epochs) expected\n        LEFT JOIN public.economy_root_reversal_states reversal\n          ON reversal.\"RootSourceStampId\" = (expected->>'root_source_stamp_id')::uuid\n        WHERE COALESCE(reversal.\"Epoch\", 0) <> (expected->>'expected_epoch')::bigint\n    ) OR EXISTS (\n        SELECT 1\n        FROM jsonb_array_elements(p_root_ranges) root_range\n        WHERE NOT EXISTS (\n            SELECT 1 FROM jsonb_array_elements(p_expected_reversal_epochs) expected\n            WHERE expected->>'root_source_stamp_id' = root_range->>'root_source_stamp_id'\n              AND (expected->>'expected_epoch')::bigint =\n                  (root_range->>'reversal_epoch')::bigint)\n    ) THEN\n        RAISE EXCEPTION 'root range uses a stale or absent reversal epoch fence' USING ERRCODE = '23514';\n    END IF;\n\n    IF p_template_kind IN (1, 2, 3) AND p_source_stamp_id IS NULL THEN\n        RAISE EXCEPTION 'registered template requires source evidence' USING ERRCODE = '23514';\n    END IF;\n    IF p_source_stamp_id IS NOT NULL THEN\n        PERFORM 1\n        FROM public.economy_source_stamps source\n        WHERE source.\"Id\" = p_source_stamp_id\n          AND source.\"EvidenceHash\" = p_source_evidence_hash\n          AND source.\"PolicyVersion\" = p_policy_version\n          AND (p_template_kind <> 1 OR EXISTS (\n              SELECT 1\n              FROM public.economy_funding_claims funding\n              WHERE funding.\"SourceStampId\" = source.\"Id\"\n                AND funding.\"State\" = 2\n                AND funding.\"ConfirmedAt\" IS NOT NULL\n                AND funding.\"ConfirmedAt\" <= p_requested_at\n                AND funding.\"PostingGroupId\" = p_posting_id\n                AND funding.\"AuthoritativeUsdMinorUnits\" >=\n                    (p_lines->0->>'amount_units')::bigint))\n        FOR SHARE;\n        IF NOT FOUND THEN\n            RAISE EXCEPTION 'source evidence is absent or mismatched' USING ERRCODE = '23514';\n        END IF;\n    END IF;\n\n    INSERT INTO public.economy_chain_head (\"Id\", \"Sequence\", \"Hash\", \"UpdatedAt\")\n    VALUES (1, 0, repeat('0', 64), p_requested_at)\n    ON CONFLICT (\"Id\") DO NOTHING;\n    SELECT \"Sequence\", \"Hash\" INTO chain_record\n    FROM public.economy_chain_head WHERE \"Id\" = 1 FOR UPDATE;\n\n    journal_sequence := chain_record.\"Sequence\" + 1;\n    canonical := concat_ws('|', chain_record.\"Hash\", p_posting_id::text,\n        journal_sequence::text, request_hash);\n    journal_hash := encode(public.digest(convert_to(canonical, 'UTF8'), 'sha256'), 'hex');\n\n    INSERT INTO public.economy_posting_groups (\n        \"Id\", \"IdempotencyKey\", \"TemplateKind\", \"TemplateVersion\", \"Authority\", \"Status\",\n        \"CapabilityId\", \"ActorId\", \"TenantId\", \"RiskDecisionId\", \"PolicyVersion\", \"ReserveVersion\",\n        \"SourceStampId\", \"RecordedAt\")\n    VALUES (p_posting_id, p_idempotency_key, p_template_kind, p_template_version,\n        p_authority, 1, p_capability_id, p_actor_id, p_tenant_id,\n        p_risk_decision_id, p_policy_version, p_reserve_version,\n        p_source_stamp_id, p_requested_at);\n\n    INSERT INTO public.economy_journal_entries (\n        \"Id\", \"PostingGroupId\", \"Sequence\", \"PreviousHash\", \"Hash\", \"RecordedAt\",\n        \"CanonicalPayloadHash\", \"HashAlgorithmVersion\")\n    VALUES (gen_random_uuid(), p_posting_id, journal_sequence,\n        chain_record.\"Hash\", journal_hash, p_requested_at, request_hash, 2);\n\n    WITH entry AS (\n        SELECT \"Id\" FROM public.economy_journal_entries\n        WHERE \"PostingGroupId\" = p_posting_id\n    )\n    INSERT INTO public.economy_journal_lines (\n        \"Id\", \"JournalEntryId\", \"AccountId\", \"WalletId\", \"CreditLotId\", \"Sequence\",\n        \"Side\", \"Currency\", \"AmountUnits\", \"Provenance\")\n    SELECT (line->>'id')::uuid, entry.\"Id\", (line->>'account_id')::uuid,\n        NULLIF(line->>'wallet_id', '')::uuid,\n        NULLIF(line->>'credit_lot_id', '')::uuid, ordinal::integer,\n        (line->>'side')::integer, (line->>'currency')::integer,\n        (line->>'amount_units')::bigint,\n        NULLIF(line->>'provenance', '')::integer\n    FROM jsonb_array_elements(p_lines) WITH ORDINALITY AS item(line, ordinal)\n    CROSS JOIN entry;\n\n    INSERT INTO public.economy_entry_allocations (\n        \"Id\", \"JournalLineId\", \"ParentLotId\", \"AmountUnits\")\n    SELECT (allocation->>'id')::uuid,\n        (allocation->>'journal_line_id')::uuid,\n        (allocation->>'parent_lot_id')::uuid,\n        (allocation->>'amount_units')::bigint\n    FROM jsonb_array_elements(p_allocations) allocation;\n\n    INSERT INTO public.economy_fragment_root_ranges (\n        \"Id\", \"RootSourceStampId\", \"CreditLotId\", \"EntryAllocationId\",\n        \"StartInclusive\", \"EndExclusive\", \"ReversalEpoch\")\n    SELECT (root_range->>'id')::uuid,\n        (root_range->>'root_source_stamp_id')::uuid,\n        NULLIF(root_range->>'credit_lot_id', '')::uuid,\n        NULLIF(root_range->>'entry_allocation_id', '')::uuid,\n        (root_range->>'start_inclusive')::bigint,\n        (root_range->>'end_exclusive')::bigint,\n        (root_range->>'reversal_epoch')::bigint\n    FROM jsonb_array_elements(p_root_ranges) root_range;\n\n    INSERT INTO public.economy_risk_decision_consumptions (\n        \"Id\", \"RiskDecisionId\", \"PostingGroupId\", \"OperationFingerprint\", \"ConsumedAt\")\n    VALUES (gen_random_uuid(), p_risk_decision_id, p_posting_id,\n        p_risk_operation_fingerprint, p_requested_at);\n    INSERT INTO public.economy_idempotency_records (\n        \"Id\", \"Key\", \"RequestHash\", \"PostingGroupId\", \"CreatedAt\")\n    VALUES (gen_random_uuid(), p_idempotency_key, request_hash,\n        p_posting_id, p_requested_at);\n    UPDATE public.economy_chain_head\n    SET \"Sequence\" = journal_sequence, \"Hash\" = journal_hash, \"UpdatedAt\" = p_requested_at\n    WHERE \"Id\" = 1;\n\n    INSERT INTO public.economy_risk_audit_evidence (\n        \"Id\", \"RiskDecisionId\", \"EventKind\", \"OperationFingerprint\", \"EvidenceHash\", \"Payload\", \"RecordedAt\")\n    VALUES (gen_random_uuid(), p_risk_decision_id, 'posting-authorized',\n        p_risk_operation_fingerprint, journal_hash,\n        jsonb_build_object('postingId', p_posting_id, 'sequence', journal_sequence),\n        p_requested_at);\n\n    outbox_payload := json_build_object(\n        'PostingId', p_posting_id,\n        'Hash', journal_hash,\n        'RecordedAt', p_requested_at,\n        'JournalLineIds', (\n            SELECT json_agg(line.\"Id\" ORDER BY line.\"Sequence\")\n            FROM public.economy_journal_entries entry\n            JOIN public.economy_journal_lines line ON line.\"JournalEntryId\" = entry.\"Id\"\n            WHERE entry.\"PostingGroupId\" = p_posting_id))::text;\n    INSERT INTO public.economy_outbox_messages (\n        \"Id\", \"PostingGroupId\", \"Type\", \"Payload\", \"PayloadHash\", \"OccurredAt\")\n    VALUES (gen_random_uuid(), p_posting_id, 'economy.posting.accepted.v1',\n        outbox_payload,\n        encode(public.digest(convert_to(outbox_payload, 'UTF8'), 'sha256'), 'hex'),\n        p_requested_at);\n    posting_id := p_posting_id;\n    duplicate := false;\n    RETURN NEXT;\nEND\n$function$;\n\nALTER FUNCTION economy_private.post_registered_posting_v1(\n    uuid,uuid,uuid,uuid,text,integer,integer,integer,bigint,bigint,uuid,text,bigint,\n    uuid,text,timestamptz,jsonb,jsonb,jsonb,jsonb,text)\n    OWNER TO gameguild_economy_procedure_owner;\nREVOKE ALL ON FUNCTION economy_private.post_registered_posting_v1(\n    uuid,uuid,uuid,uuid,text,integer,integer,integer,bigint,bigint,uuid,text,bigint,\n    uuid,text,timestamptz,jsonb,jsonb,jsonb,jsonb,text) FROM PUBLIC;\nGRANT EXECUTE ON FUNCTION economy_private.post_registered_posting_v1(\n    uuid,uuid,uuid,uuid,text,integer,integer,integer,bigint,bigint,uuid,text,bigint,\n    uuid,text,timestamptz,jsonb,jsonb,jsonb,jsonb,text) TO gameguild_economy_writer;\nREVOKE ALL ON FUNCTION economy_private.post_registered_posting_before_risk_aggregation_v1(\n    uuid,uuid,uuid,uuid,text,integer,integer,integer,bigint,bigint,uuid,text,bigint,\n    uuid,text,timestamptz,jsonb,jsonb,jsonb,jsonb,text) FROM PUBLIC;");
	}

	private static void RemoveRegisteredPostingRiskAggregation(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("DROP FUNCTION economy_private.post_registered_posting_v1(\n    uuid,uuid,uuid,uuid,text,integer,integer,integer,bigint,bigint,uuid,text,bigint,\n    uuid,text,timestamptz,jsonb,jsonb,jsonb,jsonb,text);\nALTER FUNCTION economy_private.post_registered_posting_before_risk_aggregation_v1(\n    uuid,uuid,uuid,uuid,text,integer,integer,integer,bigint,bigint,uuid,text,bigint,\n    uuid,text,timestamptz,jsonb,jsonb,jsonb,jsonb,text)\n    RENAME TO post_registered_posting_v1;\nGRANT EXECUTE ON FUNCTION economy_private.post_registered_posting_v1(\n    uuid,uuid,uuid,uuid,text,integer,integer,integer,bigint,bigint,uuid,text,bigint,\n    uuid,text,timestamptz,jsonb,jsonb,jsonb,jsonb,text) TO gameguild_economy_writer;");
	}

	private static void InstallCanonicalHashUpgradeGuard(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("CREATE OR REPLACE FUNCTION economy_private.deny_immutable_mutation_v1()\nRETURNS trigger\nLANGUAGE plpgsql\nSECURITY DEFINER\nSET search_path = pg_catalog, economy_private\nAS $function$\nBEGIN\n    -- Canonical hash metadata was introduced after the journal became\n    -- immutable. Permit exactly one monotonic stamp, in the same\n    -- transaction as its matching durable idempotency record. No other\n    -- journal field and no other immutable relation can be changed.\n    IF TG_TABLE_NAME = 'economy_journal_entries'\n       AND TG_OP = 'UPDATE'\n       AND OLD.\"HashAlgorithmVersion\" = 0\n       AND OLD.\"CanonicalPayloadHash\" IS NULL\n       AND NEW.\"HashAlgorithmVersion\" = 2\n       AND length(btrim(COALESCE(NEW.\"CanonicalPayloadHash\", ''))) > 0\n       AND (to_jsonb(NEW) - 'HashAlgorithmVersion' - 'CanonicalPayloadHash') =\n           (to_jsonb(OLD) - 'HashAlgorithmVersion' - 'CanonicalPayloadHash')\n       AND EXISTS (\n           SELECT 1\n           FROM public.economy_idempotency_records idempotency\n           WHERE idempotency.\"PostingGroupId\" = OLD.\"PostingGroupId\"\n             AND idempotency.\"RequestHash\" = NEW.\"CanonicalPayloadHash\") THEN\n        RETURN NEW;\n    END IF;\n\n    RAISE EXCEPTION 'immutable economy relation % rejects %', TG_TABLE_NAME, TG_OP\n        USING ERRCODE = '42501';\nEND\n$function$;\n\nALTER FUNCTION economy_private.deny_immutable_mutation_v1()\n    OWNER TO gameguild_economy_procedure_owner;\nREVOKE ALL ON FUNCTION economy_private.deny_immutable_mutation_v1() FROM PUBLIC;");
	}

	private static void RestoreStrictImmutableGuard(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("CREATE OR REPLACE FUNCTION economy_private.deny_immutable_mutation_v1()\nRETURNS trigger\nLANGUAGE plpgsql\nSECURITY DEFINER\nSET search_path = pg_catalog, economy_private\nAS $function$\nBEGIN\n    RAISE EXCEPTION 'immutable economy relation % rejects %', TG_TABLE_NAME, TG_OP\n        USING ERRCODE = '42501';\nEND\n$function$;\n\nALTER FUNCTION economy_private.deny_immutable_mutation_v1()\n    OWNER TO gameguild_economy_procedure_owner;\nREVOKE ALL ON FUNCTION economy_private.deny_immutable_mutation_v1() FROM PUBLIC;");
	}

	private static void BackfillEconomyProductionColumns(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("-- Rows written by the old counter implementation did not have a durable\n-- reservation lifecycle. Quarantine them as expired rather than making\n-- them look consumable after the upgrade.\nUPDATE public.economy_risk_counter_reservations\nSET \"ReservationGroupId\" = \"Id\",\n    \"InputFingerprint\" = 'legacy:' || \"Id\"::text,\n    \"ExpiresAt\" = \"ReservedAt\" + interval '5 minutes',\n    \"Status\" = 4,\n    \"ReleasedAt\" = \"ReservedAt\" + interval '5 minutes',\n    \"ConsumedAt\" = NULL;\n\n-- SQL posting writers have always chained the idempotency request hash.\n-- Persist that canonical hash explicitly so the incremental verifier can\n-- validate historical entries without changing their immutable Hash.\nUPDATE public.economy_journal_entries entry\nSET \"CanonicalPayloadHash\" = idempotency.\"RequestHash\",\n    \"HashAlgorithmVersion\" = 2\nFROM public.economy_idempotency_records idempotency\nWHERE idempotency.\"PostingGroupId\" = entry.\"PostingGroupId\"\n  AND length(btrim(idempotency.\"RequestHash\")) > 0;\n\nUPDATE public.economy_marketplace_settlement_credits\nSET \"RemainingUnits\" = \"AmountUnits\";\n\nUPDATE public.economy_marketplace_funding_fragments\nSET \"ReservationId\" = \"Id\";\n\n-- Existing Marketplace rows predate tenant/capability receipts. Preserve\n-- them in the all-zero quarantine tenant and make their provenance gap\n-- explicit. They cannot authorize value because their policy signatures\n-- and capability receipts are deliberately untrusted.\nUPDATE public.economy_marketplace_currency_policy_versions\nSET \"TenantId\" = '00000000-0000-0000-0000-000000000000'::uuid,\n    \"PlatformFeeWalletId\" = '00000000-0000-0000-0000-000000000000'::uuid,\n    \"RefundHoldTicks\" = 1,\n    \"ExpiresAt\" = \"EffectiveAt\" + interval '1 microsecond',\n    \"CanonicalPayload\" = jsonb_build_object(\n        'legacyUntrusted', true, 'productId', \"ProductId\", 'version', \"Version\")::text,\n    \"PayloadHash\" = encode(public.digest(convert_to(\n        concat_ws('|', 'legacy-marketplace-policy', \"ProductId\"::text, \"Version\"::text), 'UTF8'),\n        'sha256'), 'hex'),\n    \"KeyId\" = 'legacy-untrusted',\n    \"Signature\" = 'legacy-untrusted',\n    \"ProposedBy\" = '11111111-1111-1111-1111-111111111111'::uuid,\n    \"ApprovedBy\" = '22222222-2222-2222-2222-222222222222'::uuid,\n    \"PublishedAt\" = \"EffectiveAt\";\n\nUPDATE public.economy_marketplace_settlements settlement\nSET \"TenantId\" = '00000000-0000-0000-0000-000000000000'::uuid,\n    \"OrderLineItemId\" = settlement.\"Id\",\n    \"ProductPricingVersionId\" = settlement.\"ProductId\",\n    \"PriceVersionSnapshot\" = 1,\n    \"Quantity\" = 1,\n    \"RefundedQuantity\" = CASE WHEN EXISTS (\n        SELECT 1 FROM public.economy_marketplace_refunds refund\n        WHERE refund.\"SettlementId\" = settlement.\"Id\") THEN 1 ELSE 0 END,\n    \"UnitPriceSnapshot\" = 0,\n    \"FiatCurrencySnapshot\" = 'ZZZ',\n    \"OrderSnapshotHash\" = encode(public.digest(convert_to(\n        concat_ws('|', 'legacy-marketplace-settlement', settlement.\"Id\"::text), 'UTF8'),\n        'sha256'), 'hex'),\n    \"EntitlementStatus\" = CASE WHEN settlement.\"Status\" = 3 THEN 2 ELSE 1 END,\n    \"PostingId\" = settlement.\"Id\",\n    \"JournalSequence\" = COALESCE((\n        SELECT min(lot.\"JournalSequence\")\n        FROM public.economy_marketplace_settlement_credits credit\n        JOIN public.economy_credit_lots lot ON lot.\"Id\" = credit.\"CreditLotId\"\n        WHERE credit.\"SettlementId\" = settlement.\"Id\"), 1),\n    \"JournalHash\" = 'legacy-unreconciled',\n    \"CapabilityReceiptId\" = settlement.\"Id\",\n    \"CapabilityReceiptHash\" = 'legacy-untrusted',\n    \"ReserveVersion\" = 1,\n    \"RiskDecisionId\" = settlement.\"Id\",\n    \"KillSwitchEpoch\" = 0,\n    \"JurisdictionCode\" = 'ZZ',\n    \"EvidenceHashes\" = '[\"legacy-unreconciled\"]'::jsonb;\n\nUPDATE public.economy_marketplace_refunds refund\nSET \"TenantId\" = '00000000-0000-0000-0000-000000000000'::uuid,\n    \"PostingId\" = refund.\"Id\",\n    \"JournalHash\" = 'legacy-unreconciled',\n    \"ReasonCode\" = 'legacy-unreconciled',\n    \"ReasonHash\" = encode(public.digest(convert_to(\n        concat_ws('|', 'legacy-marketplace-refund', refund.\"Id\"::text), 'UTF8'),\n        'sha256'), 'hex'),\n    \"Quantity\" = 1,\n    \"RefundedQuantity\" = 1,\n    \"MarketplacePolicyVersion\" = settlement.\"PolicyVersion\",\n    \"PolicyVersion\" = settlement.\"PolicyVersion\",\n    \"CapabilityReceiptId\" = refund.\"Id\",\n    \"CapabilityReceiptHash\" = 'legacy-untrusted',\n    \"ReserveVersion\" = 1,\n    \"RiskDecisionId\" = refund.\"Id\",\n    \"KillSwitchEpoch\" = 0,\n    \"JurisdictionCode\" = 'ZZ',\n    \"EvidenceHashes\" = '[\"legacy-unreconciled\"]'::jsonb\nFROM public.economy_marketplace_settlements settlement\nWHERE settlement.\"Id\" = refund.\"SettlementId\";\n\n-- Old ad policies/reports are also retained as evidence only. A missing\n-- production signature and ProviderCertified=false keep issuance closed.\nUPDATE public.economy_ad_network_policy_versions\nSET \"TenantId\" = '00000000-0000-0000-0000-000000000000'::uuid,\n    \"ProviderCertified\" = false,\n    \"ProviderHash\" = 'legacy-untrusted',\n    \"BudgetWindowTicks\" = 1,\n    \"MaximumUserSoftUnits\" = GREATEST(\"MaximumRewardSoftUnits\", 1),\n    \"MaximumDeviceSoftUnits\" = GREATEST(\"MaximumRewardSoftUnits\", 1),\n    \"MaximumIpSoftUnits\" = GREATEST(\"MaximumRewardSoftUnits\", 1),\n    \"MaximumAsnSoftUnits\" = GREATEST(\"MaximumRewardSoftUnits\", 1),\n    \"MaximumNetworkSoftUnits\" = GREATEST(\"MaximumRewardSoftUnits\", 1),\n    \"MaximumGlobalSoftUnits\" = GREATEST(\"MaximumRewardSoftUnits\", 1),\n    \"FundedLossBudgetUsdNanos\" = 1,\n    \"CanonicalPayload\" = jsonb_build_object(\n        'legacyUntrusted', true, 'network', \"Network\", 'version', \"Version\")::text,\n    \"PayloadHash\" = encode(public.digest(convert_to(\n        concat_ws('|', 'legacy-ad-policy', \"Network\", \"Version\"::text), 'UTF8'),\n        'sha256'), 'hex'),\n    \"KeyId\" = 'legacy-untrusted',\n    \"Signature\" = 'legacy-untrusted',\n    \"ProposedBy\" = '11111111-1111-1111-1111-111111111111'::uuid,\n    \"ApprovedBy\" = '22222222-2222-2222-2222-222222222222'::uuid,\n    \"PublishedAt\" = \"EffectiveAt\";\n\nUPDATE public.economy_ad_provider_reports\nSET \"TenantId\" = '00000000-0000-0000-0000-000000000000'::uuid,\n    \"PayloadHash\" = COALESCE(NULLIF(\"EvidenceHash\", ''), 'legacy-untrusted'),\n    \"SignatureVerified\" = false,\n    \"ReceivedAt\" = \"ImportedAt\";\n\nUPDATE public.economy_ad_reward_completions\nSET \"TenantId\" = '00000000-0000-0000-0000-000000000000'::uuid,\n    \"EvidenceHashes\" = '[\"legacy-unreconciled\"]'::jsonb,\n    \"Version\" = 1;\n\nUPDATE public.economy_ad_reward_accumulators\nSET \"TenantId\" = '00000000-0000-0000-0000-000000000000'::uuid;\n\nUPDATE public.economy_ad_reward_attributions\nSET \"TenantId\" = '00000000-0000-0000-0000-000000000000'::uuid;\n\nUPDATE public.economy_ad_reward_budget_consumptions\nSET \"TenantId\" = '00000000-0000-0000-0000-000000000000'::uuid;\n\nUPDATE public.economy_ad_reward_reconciliations\nSET \"TenantId\" = '00000000-0000-0000-0000-000000000000'::uuid;");
	}

	private static void InstallEconomyProductionSecurity(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("-- Refresh the least-privilege grants for every Economy relation created\n-- since the foundation migration. SECURITY DEFINER writers execute as\n-- the procedure owner, while the application writer role receives only\n-- EXECUTE and the runtime role remains read-only.\nDO $production_grants$\nDECLARE\n    relation_name text;\nBEGIN\n    FOR relation_name IN\n        SELECT tablename FROM pg_tables\n        WHERE schemaname = 'public' AND tablename LIKE 'economy_%'\n    LOOP\n        EXECUTE format('REVOKE ALL ON TABLE public.%I FROM PUBLIC', relation_name);\n        EXECUTE format('REVOKE ALL ON TABLE public.%I FROM gameguild_economy_writer', relation_name);\n        EXECUTE format('GRANT SELECT ON TABLE public.%I TO gameguild_economy_runtime', relation_name);\n        EXECUTE format(\n            'GRANT SELECT, INSERT, UPDATE ON TABLE public.%I TO gameguild_economy_procedure_owner',\n            relation_name);\n        EXECUTE format('GRANT ALL ON TABLE public.%I TO gameguild_economy_migration', relation_name);\n    END LOOP;\nEND\n$production_grants$;\n\nALTER TABLE public.economy_posting_groups\n    DROP CONSTRAINT IF EXISTS ck_economy_posting_groups_template_state;\nALTER TABLE public.economy_posting_groups\n    ADD CONSTRAINT ck_economy_posting_groups_template_state\n    CHECK (\"TemplateKind\" BETWEEN 1 AND 26 AND \"TemplateVersion\" = 1 AND \"Status\" = 1);\nALTER TABLE public.economy_posting_groups\n    DROP CONSTRAINT IF EXISTS ck_economy_posting_groups_authority_template;\nALTER TABLE public.economy_posting_groups\n    ADD CONSTRAINT ck_economy_posting_groups_authority_template CHECK (\n        (\"TemplateKind\" IN (1, 2, 3, 18, 19, 20) AND \"Authority\" = 1) OR\n        (\"TemplateKind\" IN (4, 5, 7, 8, 17, 22) AND \"Authority\" = 2) OR\n        (\"TemplateKind\" IN (6, 21) AND \"Authority\" = 3) OR\n        (\"TemplateKind\" IN (9, 10, 23, 24) AND \"Authority\" = 4) OR\n        (\"TemplateKind\" IN (11, 12, 13) AND \"Authority\" = 5) OR\n        (\"TemplateKind\" IN (14, 15, 16) AND \"Authority\" = 6) OR\n        (\"TemplateKind\" IN (25, 26) AND \"Authority\" = 7));\n\nDROP INDEX IF EXISTS public.\"IX_economy_marketplace_funding_fragments_SettlementId_ParentLo~\";\nCREATE UNIQUE INDEX IF NOT EXISTS ux_economy_marketplace_funding_fragments_reservation\n    ON public.economy_marketplace_funding_fragments (\"ReservationId\");\nCREATE INDEX IF NOT EXISTS ix_economy_marketplace_funding_fragments_settlement_parent_currency\n    ON public.economy_marketplace_funding_fragments (\"SettlementId\", \"ParentLotId\", \"Currency\");\n\nALTER TABLE public.economy_fragment_reservations\n    DROP CONSTRAINT IF EXISTS ck_economy_fragment_reservations_values;\nALTER TABLE public.economy_fragment_reservations\n    DROP CONSTRAINT IF EXISTS ck_economy_fragment_reservations_state;\nALTER TABLE public.economy_fragment_reservations\n    DROP CONSTRAINT IF EXISTS ck_economy_fragment_reservations_lifecycle;\nALTER TABLE public.economy_fragment_reservations\n    DROP CONSTRAINT IF EXISTS ex_economy_fragment_reservations_active_no_overlap;\nALTER TABLE public.economy_fragment_reservations\n    ADD CONSTRAINT ck_economy_fragment_reservations_state\n    CHECK (\"Currency\" IN (1, 2) AND \"Purpose\" BETWEEN 1 AND 7 AND \"Status\" BETWEEN 1 AND 4);\nALTER TABLE public.economy_fragment_reservations\n    ADD CONSTRAINT ck_economy_fragment_reservations_lifecycle\n    CHECK ((\"Status\" IN (1, 4) AND \"TerminalAt\" IS NULL)\n           OR (\"Status\" IN (2, 3) AND \"TerminalAt\" IS NOT NULL));\nALTER TABLE public.economy_fragment_reservations\n    ADD CONSTRAINT ex_economy_fragment_reservations_active_no_overlap\n    EXCLUDE USING gist (\n        \"ParentLotId\" WITH =,\n        int8range(\"StartInclusive\", \"EndExclusive\", '[)') WITH &&\n    ) WHERE (\"Status\" IN (1, 4));\n\nCREATE OR REPLACE FUNCTION economy_private.transition_fifo_fragment_reservations_v1(\n    p_operation_id uuid,\n    p_expected_status integer,\n    p_next_status integer,\n    p_terminal_at timestamptz)\nRETURNS bigint\nLANGUAGE plpgsql\nSECURITY DEFINER\nSET search_path = pg_catalog, economy_private\nAS $function$\nDECLARE\n    changed_count bigint;\nBEGIN\n    IF p_operation_id IS NULL OR p_terminal_at IS NULL\n       OR NOT ((p_expected_status = 1 AND p_next_status IN (2, 3, 4))\n               OR (p_expected_status = 4 AND p_next_status IN (2, 3))) THEN\n        RAISE EXCEPTION 'FIFO reservation transition is invalid' USING ERRCODE = '22023';\n    END IF;\n\n    UPDATE public.economy_fragment_reservations\n    SET \"Status\" = p_next_status,\n        \"TerminalAt\" = CASE WHEN p_next_status IN (2, 3) THEN p_terminal_at ELSE NULL END\n    WHERE \"OperationId\" = p_operation_id AND \"Status\" = p_expected_status;\n    GET DIAGNOSTICS changed_count = ROW_COUNT;\n    IF changed_count = 0 THEN\n        RAISE EXCEPTION 'FIFO reservation is stale or absent' USING ERRCODE = '40001';\n    END IF;\n    RETURN changed_count;\nEND\n$function$;\n\nALTER FUNCTION economy_private.transition_fifo_fragment_reservations_v1(uuid,integer,integer,timestamptz)\n    OWNER TO gameguild_economy_procedure_owner;\nREVOKE ALL ON FUNCTION economy_private.transition_fifo_fragment_reservations_v1(uuid,integer,integer,timestamptz) FROM PUBLIC;\nGRANT EXECUTE ON FUNCTION economy_private.transition_fifo_fragment_reservations_v1(uuid,integer,integer,timestamptz)\n    TO gameguild_economy_writer;\n\nCREATE OR REPLACE FUNCTION economy_private.economy_stamp_journal_hash_on_insert_v1()\nRETURNS trigger\nLANGUAGE plpgsql\nSECURITY DEFINER\nSET search_path = pg_catalog, economy_private\nAS $function$\nBEGIN\n    IF NEW.\"HashAlgorithmVersion\" NOT IN (1, 2) OR NEW.\"CanonicalPayloadHash\" IS NULL\n       OR length(btrim(NEW.\"CanonicalPayloadHash\")) = 0 THEN\n        NEW.\"HashAlgorithmVersion\" := 0;\n        NEW.\"CanonicalPayloadHash\" := NULL;\n    END IF;\n    RETURN NEW;\nEND\n$function$;\n\nCREATE OR REPLACE FUNCTION economy_private.economy_stamp_journal_hash_from_idempotency_v1()\nRETURNS trigger\nLANGUAGE plpgsql\nSECURITY DEFINER\nSET search_path = pg_catalog, economy_private\nAS $function$\nBEGIN\n    UPDATE public.economy_journal_entries\n    SET \"CanonicalPayloadHash\" = NEW.\"RequestHash\",\n        \"HashAlgorithmVersion\" = 2\n    WHERE \"PostingGroupId\" = NEW.\"PostingGroupId\"\n      AND (\"CanonicalPayloadHash\" IS NULL OR \"HashAlgorithmVersion\" = 0);\n    RETURN NEW;\nEND\n$function$;\n\nDROP TRIGGER IF EXISTS stamp_economy_journal_hash_on_insert ON public.economy_journal_entries;\nCREATE TRIGGER stamp_economy_journal_hash_on_insert\n    BEFORE INSERT ON public.economy_journal_entries\n    FOR EACH ROW EXECUTE FUNCTION economy_private.economy_stamp_journal_hash_on_insert_v1();\n\nDROP TRIGGER IF EXISTS stamp_economy_journal_hash_from_idempotency ON public.economy_idempotency_records;\nCREATE TRIGGER stamp_economy_journal_hash_from_idempotency\n    AFTER INSERT ON public.economy_idempotency_records\n    FOR EACH ROW EXECUTE FUNCTION economy_private.economy_stamp_journal_hash_from_idempotency_v1();\n\nALTER FUNCTION economy_private.economy_stamp_journal_hash_on_insert_v1()\n    OWNER TO gameguild_economy_procedure_owner;\nALTER FUNCTION economy_private.economy_stamp_journal_hash_from_idempotency_v1()\n    OWNER TO gameguild_economy_procedure_owner;\nREVOKE ALL ON FUNCTION economy_private.economy_stamp_journal_hash_on_insert_v1() FROM PUBLIC;\nREVOKE ALL ON FUNCTION economy_private.economy_stamp_journal_hash_from_idempotency_v1() FROM PUBLIC;");
		InstallEconomyProductionPostingValidator(migrationBuilder);
		InstallRegisteredPostingRiskAggregation(migrationBuilder);
		InstallAdRewardIssuanceWriter(migrationBuilder);
		InstallMarketplaceWriters(migrationBuilder);
	}

	private static void RemoveEconomyProductionSecurity(MigrationBuilder migrationBuilder)
	{
		RemoveMarketplaceWriters(migrationBuilder);
		RemoveAdRewardIssuanceWriter(migrationBuilder);
		RemoveRegisteredPostingRiskAggregation(migrationBuilder);
		RemoveEconomyProductionPostingValidator(migrationBuilder);
		migrationBuilder.Sql("DROP INDEX IF EXISTS public.ux_economy_marketplace_funding_fragments_reservation;\nDROP INDEX IF EXISTS public.ix_economy_marketplace_funding_fragments_settlement_parent_currency;\nDO $marketplace_down$\nBEGIN\n    IF EXISTS (\n        SELECT 1 FROM public.economy_marketplace_funding_fragments\n        GROUP BY \"SettlementId\", \"ParentLotId\", \"Currency\" HAVING count(*) > 1) THEN\n        RAISE EXCEPTION 'cannot downgrade Marketplace fragment indexing while one parent has multiple ranges';\n    END IF;\nEND\n$marketplace_down$;\nCREATE UNIQUE INDEX \"IX_economy_marketplace_funding_fragments_SettlementId_ParentLo~\"\n    ON public.economy_marketplace_funding_fragments (\"SettlementId\", \"ParentLotId\", \"Currency\");\n\nDROP TRIGGER IF EXISTS stamp_economy_journal_hash_from_idempotency ON public.economy_idempotency_records;\nDROP TRIGGER IF EXISTS stamp_economy_journal_hash_on_insert ON public.economy_journal_entries;\nDROP FUNCTION IF EXISTS economy_private.economy_stamp_journal_hash_from_idempotency_v1();\nDROP FUNCTION IF EXISTS economy_private.economy_stamp_journal_hash_on_insert_v1();\n\nALTER TABLE public.economy_posting_groups\n    DROP CONSTRAINT IF EXISTS ck_economy_posting_groups_authority_template;\nALTER TABLE public.economy_posting_groups\n    ADD CONSTRAINT ck_economy_posting_groups_authority_template CHECK (\n        (\"TemplateKind\" IN (1, 2, 3, 18, 19, 20) AND \"Authority\" = 1) OR\n        (\"TemplateKind\" IN (4, 5, 7, 8, 17) AND \"Authority\" = 2) OR\n        (\"TemplateKind\" IN (6, 21) AND \"Authority\" = 3) OR\n        (\"TemplateKind\" IN (9, 10) AND \"Authority\" = 4) OR\n        (\"TemplateKind\" IN (11, 12, 13) AND \"Authority\" = 5) OR\n        (\"TemplateKind\" IN (14, 15, 16) AND \"Authority\" = 6));\nALTER TABLE public.economy_posting_groups\n    DROP CONSTRAINT IF EXISTS ck_economy_posting_groups_template_state;\nALTER TABLE public.economy_posting_groups\n    ADD CONSTRAINT ck_economy_posting_groups_template_state\n    CHECK (\"TemplateKind\" BETWEEN 1 AND 21 AND \"TemplateVersion\" = 1 AND \"Status\" = 1);\n\nDO $down$\nBEGIN\n    IF EXISTS (SELECT 1 FROM public.economy_fragment_reservations WHERE \"Status\" = 4) THEN\n        RAISE EXCEPTION 'cannot downgrade while FIFO reservations are dispatching';\n    END IF;\nEND\n$down$;\n\nALTER TABLE public.economy_fragment_reservations\n    DROP CONSTRAINT IF EXISTS ck_economy_fragment_reservations_values;\nALTER TABLE public.economy_fragment_reservations\n    DROP CONSTRAINT IF EXISTS ck_economy_fragment_reservations_state;\nALTER TABLE public.economy_fragment_reservations\n    DROP CONSTRAINT IF EXISTS ck_economy_fragment_reservations_lifecycle;\nALTER TABLE public.economy_fragment_reservations\n    DROP CONSTRAINT IF EXISTS ex_economy_fragment_reservations_active_no_overlap;\nALTER TABLE public.economy_fragment_reservations\n    ADD CONSTRAINT ck_economy_fragment_reservations_state\n    CHECK (\"Currency\" IN (1, 2) AND \"Purpose\" BETWEEN 1 AND 5 AND \"Status\" BETWEEN 1 AND 3);\nALTER TABLE public.economy_fragment_reservations\n    ADD CONSTRAINT ck_economy_fragment_reservations_lifecycle\n    CHECK ((\"Status\" = 1 AND \"TerminalAt\" IS NULL)\n           OR (\"Status\" IN (2, 3) AND \"TerminalAt\" IS NOT NULL));\nALTER TABLE public.economy_fragment_reservations\n    ADD CONSTRAINT ex_economy_fragment_reservations_active_no_overlap\n    EXCLUDE USING gist (\n        \"ParentLotId\" WITH =,\n        int8range(\"StartInclusive\", \"EndExclusive\", '[)') WITH &&\n    ) WHERE (\"Status\" = 1);\n\nCREATE OR REPLACE FUNCTION economy_private.transition_fifo_fragment_reservations_v1(\n    p_operation_id uuid,\n    p_expected_status integer,\n    p_next_status integer,\n    p_terminal_at timestamptz)\nRETURNS bigint\nLANGUAGE plpgsql\nSECURITY DEFINER\nSET search_path = pg_catalog, economy_private\nAS $function$\nDECLARE\n    changed_count bigint;\nBEGIN\n    IF p_operation_id IS NULL OR p_terminal_at IS NULL\n       OR NOT (p_expected_status = 1 AND p_next_status IN (2, 3)) THEN\n        RAISE EXCEPTION 'FIFO reservation transition is invalid' USING ERRCODE = '22023';\n    END IF;\n    UPDATE public.economy_fragment_reservations\n    SET \"Status\" = p_next_status, \"TerminalAt\" = p_terminal_at\n    WHERE \"OperationId\" = p_operation_id AND \"Status\" = p_expected_status;\n    GET DIAGNOSTICS changed_count = ROW_COUNT;\n    IF changed_count = 0 THEN\n        RAISE EXCEPTION 'FIFO reservation is stale or absent' USING ERRCODE = '40001';\n    END IF;\n    RETURN changed_count;\nEND\n$function$;\nALTER FUNCTION economy_private.transition_fifo_fragment_reservations_v1(uuid,integer,integer,timestamptz)\n    OWNER TO gameguild_economy_procedure_owner;\nREVOKE ALL ON FUNCTION economy_private.transition_fifo_fragment_reservations_v1(uuid,integer,integer,timestamptz) FROM PUBLIC;\nGRANT EXECUTE ON FUNCTION economy_private.transition_fifo_fragment_reservations_v1(uuid,integer,integer,timestamptz)\n    TO gameguild_economy_writer;");
	}
}
