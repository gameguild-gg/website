using System.Security.Claims;
using System.Text.Encodings.Web;
using GameGuild.API.Database;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Resources.IntegrationTests.Infrastructure;

/// <summary>
/// Custom WebApplicationFactory that uses a real PostgreSQL database via Testcontainers.
/// This provides realistic integration testing against the actual database provider.
/// </summary>
public class PostgreSqlWebApplicationFactory : WebApplicationFactory<GameGuild.API.Program>
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UserA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly string _connectionString;

    public PostgreSqlWebApplicationFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:MigrationConnectionString", _connectionString);
        builder.UseSetting("Database:FailStartupOnMigrationFailure", "true");
        builder.ConfigureTestServices(services =>
        {
            // Remove all existing DbContext registrations
            var descriptorsToRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                            d.ServiceType == typeof(ApplicationDbContext) ||
                            (d.ServiceType.FullName?.Contains("EntityFramework") == true &&
                             d.ServiceType.FullName?.Contains("InMemory") != true) ||
                            d.ImplementationType?.FullName?.Contains("Npgsql") == true)
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            // Add PostgreSQL database using Testcontainers connection string
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseNpgsql(_connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(3);
                });
            });

            // Add test authentication scheme
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "TestScheme";
                options.DefaultChallengeScheme = "TestScheme";
            })
            .AddScheme<AuthenticationSchemeOptions, PostgreSqlTestAuthHandler>("TestScheme", _ => { });

            // Register IActorContextAccessor for AuthorizationBehavior (must be singleton)
            services.AddSingleton<IActorContextAccessor, ActorContextAccessor>();

            services.AddHttpLogging(_ => { });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Database.Migrate();
        SeedAuthorizationFixtures(context);
        return host;
    }

    private static void SeedAuthorizationFixtures(ApplicationDbContext context)
    {
        if (!context.Set<Tenant>().Any(tenant => tenant.Id == TenantA))
        {
            context.Set<Tenant>().Add(new Tenant
            {
                Id = TenantA,
                Name = "Resources Integration Tenant A",
                Slug = "resources-integration-a",
                AdminEmail = "resources-integration-a@example.test",
                IsActive = true
            });
        }

        if (!context.Set<Tenant>().Any(tenant => tenant.Id == TenantB))
        {
            context.Set<Tenant>().Add(new Tenant
            {
                Id = TenantB,
                Name = "Resources Integration Tenant B",
                Slug = "resources-integration-b",
                AdminEmail = "resources-integration-b@example.test",
                IsActive = true
            });
        }

        if (!context.Set<User>().Any(user => user.Id == UserA))
        {
            context.Set<User>().Add(new User
            {
                Id = UserA,
                Email = "resources-integration-user-a@example.test",
                Username = "resources-integration-user-a",
                Name = "Resources Integration User A",
                IsActive = true
            });
        }

        if (!context.Set<TenantMember>().Any(member => member.UserId == UserA && member.TenantId == TenantA))
        {
            context.Set<TenantMember>().Add(new TenantMember
            {
                UserId = UserA,
                TenantId = TenantA,
                Role = "Member",
                IsActive = true
            });
        }

        context.SaveChanges();
    }
}

/// <summary>
/// Test authentication handler for PostgreSQL integration tests.
/// Format: "TestScheme userId|tenantId|isSystemAdmin"
/// </summary>
public class PostgreSqlTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public PostgreSqlTestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var headerValue = authHeader.ToString();
        if (!headerValue.StartsWith("TestScheme "))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        try
        {
            var parts = headerValue["TestScheme ".Length..].Split('|');
            if (parts.Length != 3)
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid test auth format"));
            }

            var userId = parts[0];
            var tenantId = parts[1];
            var isSystemAdmin = bool.Parse(parts[2]);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId),
                new("sub", userId),
                new("tenant_id", tenantId),
                new("tid", tenantId)
            };

            if (isSystemAdmin)
            {
                claims.Add(new Claim(ClaimTypes.Role, "SystemAdmin"));
                claims.Add(new Claim("role", "SystemAdmin"));
            }

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AuthenticateResult.Fail(ex));
        }
    }
}
