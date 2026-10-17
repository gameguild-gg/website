using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations;

public partial class RequireConfirmedFragmentReservationSources : Migration
{
    private static void InstallConfirmedFragmentReservationSourceGuard(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION economy_private.enforce_confirmed_fragment_reservation_source_v1()
            RETURNS trigger
            LANGUAGE plpgsql
            SECURITY DEFINER
            SET search_path = pg_catalog, economy_private
            AS $function$
            BEGIN
                PERFORM 1
                FROM public.economy_source_stamps source_stamp
                WHERE source_stamp."Id" = NEW."RootSourceStampId"
                  AND source_stamp."State" = 2
                  AND source_stamp."ConfirmedAt" IS NOT NULL
                  AND source_stamp."ConfirmedAt" <= NEW."ReservedAt";

                IF NOT FOUND THEN
                    RAISE EXCEPTION 'fragment reservation requires a confirmed source stamp available at reservation time'
                        USING ERRCODE = '42501';
                END IF;

                RETURN NEW;
            END
            $function$;

            ALTER FUNCTION economy_private.enforce_confirmed_fragment_reservation_source_v1()
                OWNER TO gameguild_economy_procedure_owner;
            REVOKE ALL ON FUNCTION economy_private.enforce_confirmed_fragment_reservation_source_v1() FROM PUBLIC;

            DROP TRIGGER IF EXISTS tr_economy_fragment_reservations_require_confirmed_source
                ON public.economy_fragment_reservations;
            CREATE TRIGGER tr_economy_fragment_reservations_require_confirmed_source
                BEFORE INSERT OR UPDATE OF "RootSourceStampId", "ReservedAt"
                ON public.economy_fragment_reservations
                FOR EACH ROW
                EXECUTE FUNCTION economy_private.enforce_confirmed_fragment_reservation_source_v1();
            """);
    }

    private static void RemoveConfirmedFragmentReservationSourceGuard(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS tr_economy_fragment_reservations_require_confirmed_source
                ON public.economy_fragment_reservations;
            DROP FUNCTION IF EXISTS economy_private.enforce_confirmed_fragment_reservation_source_v1();
            """);
    }
}
