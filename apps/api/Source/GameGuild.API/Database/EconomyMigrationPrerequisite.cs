using Microsoft.EntityFrameworkCore;

namespace GameGuild.API.Database;

internal sealed class EconomyMigrationPrerequisite : IDatabaseMigrationPrerequisite
{
    private const string BootstrapSql = """
        DO $bootstrap$
        DECLARE
            migration_role name := current_user;
        BEGIN
            PERFORM pg_advisory_xact_lock(hashtext('gameguild.economy.migration-prerequisite'));

            IF NOT EXISTS (
                SELECT 1
                FROM pg_roles
                WHERE rolname = 'gameguild_economy_procedure_owner') THEN
                BEGIN
                    CREATE ROLE gameguild_economy_procedure_owner NOLOGIN;
                EXCEPTION
                    WHEN insufficient_privilege THEN
                        RAISE EXCEPTION
                            'Migration role % must have CREATEROLE to prepare the economy procedure owner role.',
                            migration_role
                            USING ERRCODE = '42501';
                END;
            END IF;

            IF NOT pg_has_role(
                migration_role,
                'gameguild_economy_procedure_owner',
                'USAGE') THEN
                BEGIN
                    EXECUTE format(
                        'GRANT gameguild_economy_procedure_owner TO %I WITH INHERIT TRUE, SET TRUE',
                        migration_role);
                EXCEPTION
                    WHEN insufficient_privilege THEN
                        RAISE EXCEPTION
                            'Migration role % must be granted membership in gameguild_economy_procedure_owner by a role administrator.',
                            migration_role
                            USING ERRCODE = '42501';
                END;
            END IF;
        END
        $bootstrap$;
        """;

    public Task PrepareAsync(DbContext db, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        if (!db.Database.IsNpgsql())
            return Task.CompletedTask;

        return db.Database.ExecuteSqlRawAsync(BootstrapSql, cancellationToken);
    }
}
