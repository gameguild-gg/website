using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GameGuild.API.Database;

/// <summary>
///     Factory for creating ApplicationDbContext at design time for EF Core migrations.
///     Force-loads all referenced GameGuild assemblies so that IModelConfiguration
///     implementations are discovered correctly by ApplicationDbContext.OnModelCreating.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // Force-load all GameGuild assemblies so OnModelCreating can discover
        // every IModelConfiguration via AppDomain.CurrentDomain.GetAssemblies().
        // At design time, referenced assemblies aren't loaded until first use.
        ForceLoadGameGuildAssemblies();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = ResolveConnectionString(configuration);

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.ConfigureWarnings(w =>
            w.Ignore(RelationalEventId.PendingModelChangesWarning));

        return new ApplicationDbContext(optionsBuilder.Options);
    }

    /// <summary>Resolves the DDL-capable connection used by EF tooling.</summary>
    public static string ResolveConnectionString(IConfiguration configuration) =>
        DatabaseStartupConfiguration.ResolveMigrationConnectionString(configuration)
        ?? PostgresConnectionString.Resolve(configuration)
        // Justified constant (SonarCSharp S2068 / hard-coded-password): this
        // fallback exists only for `dotnet ef` design-time tooling when no
        // configuration source is available. It matches the local docker-compose
        // development database (postgres/postgres), is never used at runtime —
        // DatabaseStartupInitializer resolves the real connection from
        // POSTGRES_* / connection-string configuration before this factory runs.
        ?? "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres";

    /// <summary>
    ///     Scans the output directory for all GameGuild.*.dll files and loads them
    ///     into the current AppDomain so that assembly scanning finds all modules.
    /// </summary>
    private static void ForceLoadGameGuildAssemblies()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var dlls = Directory.GetFiles(baseDir, "GameGuild.*.dll", SearchOption.TopDirectoryOnly);

        foreach (var dll in dlls)
        {
            try
            {
                var name = AssemblyName.GetAssemblyName(dll);
                if (AppDomain.CurrentDomain.GetAssemblies()
                    .All(a => a.FullName != name.FullName))
                {
                    Assembly.LoadFrom(dll);
                }
            }
            catch
            {
                // Ignore assemblies that fail to load (e.g. test assemblies)
            }
        }
    }
}
