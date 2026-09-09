using GameGuild.API.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260909120000_BridgeEconomyPostingOutbox")]
public partial class BridgeEconomyPostingOutbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION "gameguild.integration".enqueue_economy_posting_accepted_v1(
                p_event_id uuid,
                p_posting_group_id uuid,
                p_occurred_at timestamptz)
            RETURNS void
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog
            AS $function$
            DECLARE
                tenant_id uuid;
                actor_id uuid;
                posting_hash text;
                posting_lines jsonb;
                event_payload jsonb;
            BEGIN
                SELECT posting."TenantId", posting."ActorId", journal."Hash",
                       COALESCE(
                           jsonb_agg(
                               jsonb_build_object(
                                   'sequence', line."Sequence",
                                   'side', CASE line."Side"
                                       WHEN 1 THEN 'Debit'
                                       WHEN 2 THEN 'Credit'
                                       ELSE line."Side"::text
                                   END,
                                   'accountCode', account."Code"::text,
                                   'currencyCode', CASE line."Currency"
                                       WHEN 1 THEN 'HardCoin'
                                       WHEN 2 THEN 'SoftCoin'
                                       ELSE line."Currency"::text
                                   END,
                                   'units', line."AmountUnits",
                                   'walletId', line."WalletId")
                               ORDER BY line."Sequence")
                           FILTER (WHERE line."Id" IS NOT NULL),
                           '[]'::jsonb)
                INTO tenant_id, actor_id, posting_hash, posting_lines
                FROM public.economy_posting_groups posting
                JOIN public.economy_journal_entries journal
                  ON journal."PostingGroupId" = posting."Id"
                LEFT JOIN public.economy_journal_lines line
                  ON line."JournalEntryId" = journal."Id"
                LEFT JOIN public.economy_accounts account
                  ON account."Id" = line."AccountId"
                WHERE posting."Id" = p_posting_group_id
                GROUP BY posting."TenantId", posting."ActorId", journal."Hash";

                IF tenant_id IS NULL OR actor_id IS NULL OR posting_hash IS NULL THEN
                    RAISE EXCEPTION 'Economy posting % cannot be bridged to the integration outbox.',
                        p_posting_group_id;
                END IF;

                event_payload := jsonb_build_object(
                    'postingId', p_posting_group_id,
                    'postingHash', posting_hash,
                    'lines', posting_lines,
                    'eventId', p_event_id,
                    'occurredAt', p_occurred_at,
                    'sourceModule', 'Finance.Economy',
                    'eventName', 'economy.posting.accepted.v1',
                    'schemaVersion', 1,
                    'tenantId', tenant_id,
                    'actorId', actor_id,
                    'aggregateType', 'EconomyPosting',
                    'aggregateId', p_posting_group_id::text,
                    'correlationId', p_posting_group_id,
                    'causationId', NULL);

                INSERT INTO "gameguild.integration"."outbox_messages" (
                    "EventId", "TenantId", "ActorId", "EventName", "EventType",
                    "SourceModule", "AggregateType", "AggregateId", "CorrelationId",
                    "CausationId", "OccurredAtUtc", "SchemaVersion", "Payload",
                    "CreatedAtUtc", "ClaimedUntilUtc", "CompletedAtUtc", "DeadLetteredAtUtc")
                VALUES (
                    p_event_id, tenant_id, actor_id, 'economy.posting.accepted.v1',
                    'GameGuild.Finance.Contracts.EconomyPostingAcceptedEventV1, GameGuild.Finance.Contracts',
                    'Finance.Economy', 'EconomyPosting', p_posting_group_id::text,
                    p_posting_group_id, NULL, p_occurred_at, 1, event_payload,
                    clock_timestamp(), NULL, NULL, NULL)
                ON CONFLICT ("EventId") DO NOTHING;
            END
            $function$;

            CREATE OR REPLACE FUNCTION public.bridge_economy_posting_outbox_v1()
            RETURNS trigger
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog
            AS $function$
            BEGIN
                PERFORM "gameguild.integration".enqueue_economy_posting_accepted_v1(
                    NEW."Id", NEW."PostingGroupId", NEW."OccurredAt");
                RETURN NEW;
            END
            $function$;

            REVOKE ALL ON FUNCTION
                "gameguild.integration".enqueue_economy_posting_accepted_v1(uuid, uuid, timestamptz)
                FROM PUBLIC;
            REVOKE ALL ON FUNCTION public.bridge_economy_posting_outbox_v1() FROM PUBLIC;

            DROP TRIGGER IF EXISTS trg_bridge_economy_posting_outbox_v1
                ON public.economy_outbox_messages;
            CREATE TRIGGER trg_bridge_economy_posting_outbox_v1
            AFTER INSERT ON public.economy_outbox_messages
            FOR EACH ROW
            WHEN (NEW."Type" = 'economy.posting.accepted.v1')
            EXECUTE FUNCTION public.bridge_economy_posting_outbox_v1();

            SELECT "gameguild.integration".enqueue_economy_posting_accepted_v1(
                source_message."Id",
                source_message."PostingGroupId",
                source_message."OccurredAt")
            FROM public.economy_outbox_messages source_message
            WHERE source_message."Type" = 'economy.posting.accepted.v1';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS trg_bridge_economy_posting_outbox_v1
                ON public.economy_outbox_messages;
            DROP FUNCTION IF EXISTS public.bridge_economy_posting_outbox_v1();
            DROP FUNCTION IF EXISTS
                "gameguild.integration".enqueue_economy_posting_accepted_v1(
                    uuid, uuid, timestamptz);
            """);
    }
}
