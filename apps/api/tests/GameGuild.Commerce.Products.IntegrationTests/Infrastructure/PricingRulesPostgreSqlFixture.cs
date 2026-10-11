using GameGuild.API.Core.Security;
using GameGuild.API.Database;
using GameGuild.TestSupport.Finance.Economy;
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

namespace GameGuild.Commerce.Products.IntegrationTests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PricingRulesPostgreSqlCollection : ICollectionFixture<PricingRulesPostgreSqlFixture>
{
    public const string Name = "Pricing rules PostgreSQL";
}

/// <summary>
/// Real-PostgreSQL host fixture for the pricing-engine endpoint tests, following the
/// <c>ApiPostgreSqlFixture</c> convention (<c>EconomyPostgreSqlTestDatabase</c> uses the
/// CI disposable gate server when <c>ECONOMY_POSTGRES_CONNECTION</c> is set and
/// Testcontainers otherwise). The pricing mutation pipeline wraps commands in real
/// transactions and records compliance evidence through PostgreSQL-specific
/// <c>FromSqlRaw</c> data sources, so the full-API host must run on PostgreSQL rather
/// than the EF in-memory provider (which fails both paths with 500s).
/// </summary>
public sealed class PricingRulesPostgreSqlFixture : IAsyncLifetime
{
    private EconomyPostgreSqlTestDatabase _database = null!;

    public WebApplicationFactory<GameGuild.API.Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _database = await EconomyPostgreSqlTestDatabase.CreateAsync("pricing_rules");
        await ApplyMigrationsAsync(_database.ConnectionString);
        Factory = new PricingRulesWebApplicationFactory(_database.ConnectionString);
        PricingRulesTestTenantServices.SeedDefaultTenant(Factory.Services);
    }

    public async Task DisposeAsync()
    {
        Factory?.Dispose();
        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
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

    private sealed class PricingRulesWebApplicationFactory(string connectionString)
        : WebApplicationFactory<GameGuild.API.Program>
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
                    ["Authentication:RefreshTokenCleanup:Enabled"] = "false",
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

                services.AddDbContext<ApplicationDbContext>((provider, options) =>
                {
                    options.AddInterceptors(provider.GetRequiredService<RefreshTokenLifecycleMetricBuffer>());
                    options.UseNpgsql(connectionString, npgsql =>
                        npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));
                });
                services.AddScoped<DbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
            });
        }
    }
}
