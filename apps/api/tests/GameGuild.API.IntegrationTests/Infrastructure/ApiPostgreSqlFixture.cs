using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using GameGuild.API.Database;
using GameGuild.API.Core.Security;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

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

    public WebApplicationFactory<Program> RealAuthenticationFactory { get; private set; } = null!;

    public string ConnectionString => _container.ConnectionString;

    public HttpClient CreateAuthenticatedClient(Guid userId, Guid tenantId, bool isSystemAdmin = false)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            ApiPostgreSqlTestAuthHandler.SchemeName,
            "authenticated");
        client.DefaultRequestHeaders.Add(ApiPostgreSqlTestAuthHandler.UserIdHeader, userId.ToString());
        client.DefaultRequestHeaders.Add(ApiPostgreSqlTestAuthHandler.TenantIdHeader, tenantId.ToString());
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        if (isSystemAdmin)
        {
            client.DefaultRequestHeaders.Add(ApiPostgreSqlTestAuthHandler.SystemAdminHeader, "true");
        }

        return client;
    }

    public async Task InitializeAsync()
    {
        _container = await EconomyPostgreSqlTestDatabase.CreateAsync("api_integration");
        await ApplyMigrationsAsync(_container.ConnectionString);
        Factory = new ApiPostgreSqlWebApplicationFactory(
            _container.ConnectionString,
            useSyntheticAuthentication: true);
        RealAuthenticationFactory = new ApiPostgreSqlWebApplicationFactory(
            _container.ConnectionString,
            useSyntheticAuthentication: false);
    }

    public async Task DisposeAsync()
    {
        Factory?.Dispose();
        RealAuthenticationFactory?.Dispose();
        if (_container is not null)
        {
            await _container.DisposeAsync();
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

    private sealed class ApiPostgreSqlWebApplicationFactory(
        string connectionString,
        bool useSyntheticAuthentication)
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
                if (useSyntheticAuthentication)
                {
                    services.AddAuthentication(options =>
                        {
                            options.DefaultAuthenticateScheme = ApiPostgreSqlTestAuthHandler.SchemeName;
                            options.DefaultChallengeScheme = ApiPostgreSqlTestAuthHandler.SchemeName;
                        })
                        .AddScheme<AuthenticationSchemeOptions, ApiPostgreSqlTestAuthHandler>(
                            ApiPostgreSqlTestAuthHandler.SchemeName,
                            _ => { });
                }

                services.AddHttpLogging(_ => { });
            });
        }
    }
}

internal sealed class ApiPostgreSqlTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiPostgreSqlTest";
    public const string UserIdHeader = "X-Test-User-Id";
    public const string TenantIdHeader = "X-Test-Tenant-Id";
    public const string SystemAdminHeader = "X-Test-System-Admin";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!Guid.TryParse(Request.Headers[UserIdHeader].FirstOrDefault(), out var userId) ||
            !Guid.TryParse(Request.Headers[TenantIdHeader].FirstOrDefault(), out var tenantId))
        {
            return Task.FromResult(AuthenticateResult.Fail("The test actor headers are invalid."));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("sub", userId.ToString()),
            new(ClaimTypes.Name, $"Integration actor {userId:N}"),
            new("tenant_id", tenantId.ToString()),
            new("tid", tenantId.ToString()),
        };
        if (string.Equals(
                Request.Headers[SystemAdminHeader].FirstOrDefault(),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            claims.Add(new Claim(ClaimTypes.Role, "SystemAdmin"));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
