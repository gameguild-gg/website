using GameGuild.API.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using GameGuild.TestSupport.Finance.Economy;

namespace GameGuild.API.IntegrationTests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ApiPostgreSqlCollection : ICollectionFixture<ApiPostgreSqlFixture>
{
    public const string Name = "API PostgreSQL";
}

public sealed class ApiPostgreSqlFixture : IAsyncLifetime
{
    private EconomyPostgreSqlTestDatabase _container = null!;

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _container = await EconomyPostgreSqlTestDatabase.CreateAsync("api_integration");
        await ApplyMigrationsAsync(_container.ConnectionString);
        Factory = new ApiPostgreSqlWebApplicationFactory(_container.ConnectionString);
    }

    public async Task DisposeAsync()
    {
        Factory?.Dispose();
        await _container.DisposeAsync();
    }

    private static async Task ApplyMigrationsAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
            .ConfigureWarnings(warnings =>
                warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        await using var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.MigrateAsync();
    }

    private sealed class ApiPostgreSqlWebApplicationFactory(string connectionString)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var connection = new NpgsqlConnectionStringBuilder(connectionString);

            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connectionString,
                    ["ConnectionStrings:MigrationConnection"] = connectionString,
                    ["Database:FailStartupOnMigrationFailure"] = "true",
                    ["Database:GrantRuntimeRoleAfterMigrations"] = "false",
                    ["Database:RunStartupInitialization"] = "false",
                    ["POSTGRES_HOST"] = connection.Host,
                    ["POSTGRES_PORT"] = connection.Port.ToString(),
                    ["POSTGRES_DB"] = connection.Database,
                    ["POSTGRES_USER"] = connection.Username,
                    ["POSTGRES_PASSWORD"] = connection.Password,
                    ["POSTGRES_MIN_POOL_SIZE"] = "0"
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ApplicationDbContext>();
                services.RemoveAll<DbContext>();
                services.RemoveAll<DbContextOptions>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();

                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseNpgsql(connectionString, npgsql =>
                        npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));
                });
                services.AddScoped<DbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
                services.AddHttpLogging(_ => { });
            });
        }
    }
}
