using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations;

public partial class AllowFixedHardToSoftLineage : Migration
{
    private static void InstallFixedHardToSoftLineageValidation(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION economy_private.validate_lineage_edge_v1()
            RETURNS trigger
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
            DECLARE
                parent_units bigint;
                child_units bigint;
                parent_currency integer;
                child_currency integer;
                allocated_parent_units bigint;
                consumed_parent_units bigint;
            BEGIN
                PERFORM pg_advisory_xact_lock(lock_id)
                FROM (
                    SELECT hashtextextended(NEW."ParentLotId"::text, 0) AS lock_id
                    UNION
                    SELECT hashtextextended(NEW."ChildLotId"::text, 0) AS lock_id
                    ORDER BY lock_id) locks;

                SELECT "AmountUnits", "Currency" INTO parent_units, parent_currency
                FROM public.economy_credit_lots WHERE "Id" = NEW."ParentLotId" FOR SHARE;
                SELECT "AmountUnits", "Currency" INTO child_units, child_currency
                FROM public.economy_credit_lots WHERE "Id" = NEW."ChildLotId" FOR SHARE;

                IF parent_units IS NULL OR child_units IS NULL OR NEW."Currency" <> child_currency THEN
                    RAISE EXCEPTION 'lineage references absent or currency-mismatched lots' USING ERRCODE = '23514';
                END IF;

                IF parent_currency = child_currency THEN
                    consumed_parent_units := NEW."AmountUnits";
                ELSIF parent_currency = 1 AND child_currency = 2
                      AND NEW."Currency" = 2 AND NEW."AmountUnits" % 1000 = 0 THEN
                    consumed_parent_units := NEW."AmountUnits" / 1000;
                ELSE
                    RAISE EXCEPTION 'lineage currency transition is not supported' USING ERRCODE = '23514';
                END IF;

                SELECT COALESCE(sum(
                    CASE
                        WHEN parent_currency = child_currency AND edge."Currency" = parent_currency
                            THEN edge."AmountUnits"
                        WHEN parent_currency = 1 AND edge."Currency" = 2
                             AND edge."AmountUnits" % 1000 = 0
                            THEN edge."AmountUnits" / 1000
                        ELSE parent_units + 1
                    END), 0)
                INTO allocated_parent_units
                FROM public.economy_lot_lineage_edges edge
                WHERE edge."ParentLotId" = NEW."ParentLotId";

                IF allocated_parent_units > parent_units - consumed_parent_units
                   OR NEW."AmountUnits" > child_units THEN
                    RAISE EXCEPTION 'lineage exceeds parent or child amount' USING ERRCODE = '23514';
                END IF;

                RETURN NEW;
            END
            $function$;

            ALTER FUNCTION economy_private.validate_lineage_edge_v1()
                OWNER TO gameguild_economy_procedure_owner;
            REVOKE ALL ON FUNCTION economy_private.validate_lineage_edge_v1() FROM PUBLIC;
            """);
    }
}