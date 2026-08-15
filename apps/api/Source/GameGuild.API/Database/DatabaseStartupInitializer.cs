using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GameGuild.API.Database;

internal readonly record struct MigrationStartupOptions(
    int MaxAttempts,
    int RetryDelaySeconds,
    int MaxRetryDelaySeconds);

internal static class DatabaseStartupInitializer
{
    public static async Task<bool> InitializeAsync(
        WebApplication app,
        Func<IServiceProvider, CancellationToken, Task> seedAsync)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(seedAsync);

        DatabaseStartupConfiguration.ThrowIfInvalid(app.Configuration, app.Environment.EnvironmentName);
        if (!DatabaseStartupConfiguration.ShouldRunStartupInitialization(
                app.Configuration,
                app.Environment.EnvironmentName))
            return true;

        var databaseConnectivityProbe = app.Services.GetRequiredService<DatabaseConnectivityProbe>();
        if (!await databaseConnectivityProbe.IsReachableAsync().ConfigureAwait(false))
        {
            app.Logger.LogWarning(
                "Initial database probe failed. Continuing into the migration retry loop so transient database startup delays do not leave the schema unapplied.");
        }

        if (!await ApplyMigrationsAsync(app).ConfigureAwait(false))
            return false;

        try
        {
            using var scope = app.Services.CreateScope();
            await seedAsync(scope.ServiceProvider, app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var failStartupOnSeedFailure = app.Configuration.GetValue<bool?>("Database:FailStartupOnSeedFailure")
                ?? !DatabaseStartupConfiguration.AllowsRuntimeFallback(app.Environment.EnvironmentName);
            if (failStartupOnSeedFailure)
            {
                app.Logger.LogCritical(ex, "Database seeding failed. Refusing to open the HTTP listener.");
                throw;
            }

            app.Logger.LogWarning(ex, "Database seeding failed - required startup data may not exist.");
        }

        return true;
    }

    internal static MigrationStartupOptions ResolveMigrationOptions(IConfiguration configuration) =>
        new(
            Math.Clamp(configuration.GetValue<int?>("Database:MigrationMaxAttempts") ?? 5, 1, 20),
            Math.Clamp(configuration.GetValue<int?>("Database:MigrationRetryDelaySeconds") ?? 2, 1, 60),
            Math.Clamp(configuration.GetValue<int?>("Database:MigrationMaxRetryDelaySeconds") ?? 30, 1, 120));

    internal static TimeSpan CalculateRetryDelay(int attempt, int retryDelaySeconds, int maxRetryDelaySeconds) =>
        TimeSpan.FromSeconds(Math.Min(maxRetryDelaySeconds, Math.Max(1, attempt) * retryDelaySeconds));

    internal static string QuotePostgresIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static Task<bool> ApplyMigrationsAsync(WebApplication app) =>
        ApplyMigrationsAsync(app, CreateMigrationDbContext);

    internal static async Task<bool> ApplyMigrationsAsync(
        WebApplication app,
        Func<string, ApplicationDbContext> migrationContextFactory)
    {
        ArgumentNullException.ThrowIfNull(migrationContextFactory);
        var options = ResolveMigrationOptions(app.Configuration);
        var failStartupOnMigrationFailure = app.Configuration.GetValue<bool?>("Database:FailStartupOnMigrationFailure")
            ?? !DatabaseStartupConfiguration.AllowsRuntimeFallback(app.Environment.EnvironmentName);
        var attempt = 1;

        while (true)
        {
            try
            {
                using var scope = app.Services.CreateScope();
                var migrationConnectionString = DatabaseStartupConfiguration.ResolveMigrationConnectionString(app.Configuration);
                var ownsMigrationContext = !string.IsNullOrWhiteSpace(migrationConnectionString);
                await using var db = ownsMigrationContext
                    ? migrationContextFactory(migrationConnectionString!)
                    : scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                if (!db.Database.IsRelational())
                    return true;

                var pendingMigrations = (await db.Database
                        .GetPendingMigrationsAsync(app.Lifetime.ApplicationStopping)
                        .ConfigureAwait(false))
                    .ToArray();

                if (pendingMigrations.Length != 0)
                {
                    app.Logger.LogInformation(
                        "Applying {PendingMigrationCount} pending database migrations. Attempt {Attempt}/{MaxAttempts}.",
                        pendingMigrations.Length,
                        attempt,
                        options.MaxAttempts);

                    await db.Database
                        .MigrateAsync(app.Lifetime.ApplicationStopping)
                        .ConfigureAwait(false);

                    app.Logger.LogInformation("Database migrations applied successfully.");
                }
                else
                {
                    app.Logger.LogInformation("Database schema is current.");
                }

                if (ownsMigrationContext)
                    await GrantRuntimeRolePrivilegesAsync(app, db).ConfigureAwait(false);

                return true;
            }
            catch (Exception ex) when (attempt < options.MaxAttempts)
            {
                var retryDelay = CalculateRetryDelay(
                    attempt,
                    options.RetryDelaySeconds,
                    options.MaxRetryDelaySeconds);
                app.Logger.LogWarning(
                    ex,
                    "Database migration attempt {Attempt}/{MaxAttempts} failed. Retrying in {RetryDelaySeconds}s.",
                    attempt,
                    options.MaxAttempts,
                    retryDelay.TotalSeconds);

                await Task.Delay(retryDelay, app.Lifetime.ApplicationStopping).ConfigureAwait(false);
                attempt++;
            }
            catch (Exception ex)
            {
                if (failStartupOnMigrationFailure)
                {
                    app.Logger.LogCritical(
                        ex,
                        "Database migrations failed after {MaxAttempts} attempts. Refusing to start because this environment requires a ready schema.",
                        options.MaxAttempts);
                    throw;
                }

                app.Logger.LogWarning(
                    ex,
                    "Database migrations failed after {MaxAttempts} attempts. API will start without database-backed readiness.",
                    options.MaxAttempts);
                return false;
            }
        }
    }

    private static ApplicationDbContext CreateMigrationDbContext(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(PostgresConnectionString.Normalize(connectionString), npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
            npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null);
            npgsqlOptions.CommandTimeout(120);
        });
        optionsBuilder.ConfigureWarnings(w =>
            w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));

        return new ApplicationDbContext(optionsBuilder.Options);
    }

    private static async Task GrantRuntimeRolePrivilegesAsync(WebApplication app, DbContext migrationDb)
    {
        if (!(app.Configuration.GetValue<bool?>("Database:GrantRuntimeRoleAfterMigrations") ?? true))
            return;

        var runtimeConnectionString = PostgresConnectionString.Resolve(app.Configuration);
        var migrationConnectionString = DatabaseStartupConfiguration.ResolveMigrationConnectionString(app.Configuration);
        if (string.IsNullOrWhiteSpace(runtimeConnectionString) || string.IsNullOrWhiteSpace(migrationConnectionString))
            return;

        var runtimeUser = new NpgsqlConnectionStringBuilder(runtimeConnectionString).Username;
        var migrationUser = new NpgsqlConnectionStringBuilder(migrationConnectionString).Username;
        if (string.IsNullOrWhiteSpace(runtimeUser) ||
            string.Equals(runtimeUser, migrationUser, StringComparison.Ordinal))
        {
            return;
        }

        var quotedRuntimeUser = QuotePostgresIdentifier(runtimeUser);
        var schemas = migrationDb.Model.GetEntityTypes()
            .Select(entityType => entityType.GetSchema() ?? "public")
            .Append("public")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(schema => schema, StringComparer.Ordinal)
            .ToArray();

        try
        {
            foreach (var schema in schemas)
            {
                var quotedSchema = QuotePostgresIdentifier(schema);
                var sql = $"""
                    GRANT USAGE ON SCHEMA {quotedSchema} TO {quotedRuntimeUser};
                    GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA {quotedSchema} TO {quotedRuntimeUser};
                    GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA {quotedSchema} TO {quotedRuntimeUser};
                    ALTER DEFAULT PRIVILEGES IN SCHEMA {quotedSchema} GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {quotedRuntimeUser};
                    ALTER DEFAULT PRIVILEGES IN SCHEMA {quotedSchema} GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO {quotedRuntimeUser};
                    """;

                await migrationDb.Database
                    .ExecuteSqlRawAsync(sql, app.Lifetime.ApplicationStopping)
                    .ConfigureAwait(false);
            }

            app.Logger.LogInformation("Granted runtime database privileges after migrations.");
        }
        catch (Exception ex)
        {
            var failStartupOnGrantFailure = app.Configuration.GetValue<bool?>("Database:FailStartupOnGrantFailure")
                ?? !DatabaseStartupConfiguration.AllowsRuntimeFallback(app.Environment.EnvironmentName);
            if (failStartupOnGrantFailure)
            {
                app.Logger.LogCritical(ex, "Failed to grant runtime database privileges after migrations.");
                throw;
            }

            app.Logger.LogWarning(ex, "Failed to grant runtime database privileges after migrations.");
        }
    }
}
