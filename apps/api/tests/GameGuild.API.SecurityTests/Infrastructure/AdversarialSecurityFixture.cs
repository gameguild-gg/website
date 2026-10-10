using System.Net.Http.Headers;
using System.Security.Cryptography;
using GameGuild.API.Core.Security;
using GameGuild.API.Database;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

namespace GameGuild.API.SecurityTests.Infrastructure;

/// <summary>
///     Shared host for the adversarial deny-by-default validation suite (issue #327).
/// </summary>
/// <remarks>
///     <para>
///         The fixture boots the real API host (migrations applied to a disposable
///         PostgreSQL instance) with the production JWT bearer handler as the only
///         authentication scheme — no synthetic test handler is registered, so every
///         token presented over HTTP must survive the full production validation
///         pipeline (signature, algorithm, issuer, audience, lifetime, revocation
///         middleware, tenant middleware, actor-context middleware, authorization).
///     </para>
///     <para>
///         Differences from production configuration, chosen to isolate the
///         authorization decision under test and documented in
///         <c>docs/security/deny-by-default-adversarial-validation.md</c>:
///         <list type="bullet">
///             <item>Startup database initialization/seeding is disabled; the fixture applies migrations itself and runs the production <see cref="PolicyDefinitionSeeder"/> directly.</item>
///             <item>Rate limiting is disabled so bulk route probes measure the authorization outcome, not 429s. The rate-limiting layer has its own dedicated suite (GameGuild.API.RateLimiting.PerformanceTests).</item>
///             <item>Refresh-token cleanup background service is disabled.</item>
///         </list>
///     </para>
/// </remarks>
public sealed class AdversarialSecurityFixture : IAsyncLifetime
{
    private EconomyPostgreSqlTestDatabase _container = null!;

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public string ConnectionString => _container.ConnectionString;

    public async Task InitializeAsync()
    {
        _container = await EconomyPostgreSqlTestDatabase.CreateAsync("api_securitytests");
        await ApplyMigrationsAsync(_container.ConnectionString);
        Factory = new AdversarialWebApplicationFactory(_container.ConnectionString);
        await SeedPolicyDefinitionsAsync();
    }

    public async Task DisposeAsync()
    {
        Factory?.Dispose();
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>
    ///     Runs the production policy-definition seeder against the test host, matching
    ///     what startup seeding performs in a production deployment. Without this, every
    ///     registered policy resolves to the fail-closed deny policy (missing store
    ///     definition), which would make positive controls impossible to evaluate.
    /// </summary>
    private async Task SeedPolicyDefinitionsAsync()
    {
        using var scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PolicyDefinitionSeeder>().SeedAsync();
    }

    /// <summary>Mints a production access token for the seeded account using the host's own <see cref="IJwtTokenService"/>.</summary>
    public async Task<string> MintAccessTokenAsync(AdversarialAccount account)
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessTokenAsync(
            account.User.Id,
            account.User.Email,
            [account.MembershipRole],
            account.TenantId,
            account.User.TokenVersion,
            account.Session.Id);
    }

    /// <summary>Creates an HTTP client authenticating with a freshly minted production token.</summary>
    public async Task<HttpClient> CreateBearerClientAsync(AdversarialAccount account)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            await MintAccessTokenAsync(account));
        return client;
    }

    /// <summary>
    ///     Seeds a tenant, a user with a membership role, an active session and an optional
    ///     explicit tenant-permission grant. Mirrors the seeding performed by
    ///     <c>BearerRevocationPostgreSqlHttpTests</c>.
    /// </summary>
    public async Task<AdversarialAccount> SeedAccountAsync(
        string membershipRole = "Member",
        string[]? grantedPermissions = null,
        string[]? deniedPermissions = null)
    {
        var marker = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IRefreshTokenHasher>();

        var user = User.Create($"adv-{marker}@example.test", $"Adversarial {marker}");
        user.Username = $"adv-{marker}";
        var tenantId = Guid.NewGuid();
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var refresh = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = hasher.HashToken(raw),
            CreatedAt = now,
            UpdatedAt = now,
            ExpiresAt = now.AddHours(1),
        };
        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            RefreshToken = refresh.Token,
            IpAddress = "127.0.0.1",
            CreatedAt = now,
            UpdatedAt = now,
            LastUsedAt = now,
            ExpiresAt = now.AddHours(1),
            IsActive = true,
        };
        db.Set<User>().Add(user);
        db.Set<Tenant>().Add(new Tenant
        {
            Id = tenantId,
            Name = $"Adversarial {marker}",
            Slug = $"adv-{marker}",
            AdminEmail = $"admin-{marker}@example.test",
            IsActive = true,
        });
        db.Set<TenantMember>().Add(new TenantMember
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = user.Id,
            Role = membershipRole,
            IsActive = true,
        });
        db.Set<RefreshToken>().Add(refresh);
        db.Set<UserSession>().Add(session);

        if (grantedPermissions is { Length: > 0 } || deniedPermissions is { Length: > 0 })
        {
            db.Set<TenantPermission>().Add(new TenantPermission
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TenantId = tenantId,
                Permissions = grantedPermissions ?? [],
                DenyPermissions = deniedPermissions ?? [],
                GrantedBy = user.Id,
                Reason = "Adversarial validation seed",
                IsActive = true,
            });
        }

        await db.SaveChangesAsync();
        return new AdversarialAccount(user, tenantId, membershipRole, session, refresh);
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

    private sealed class AdversarialWebApplicationFactory(string connectionString)
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
                    // The sweep probes fire hundreds of requests from one address; the
                    // authorization decision under test must not be masked by 429s.
                    ["PresentationLayer:EnableRateLimiting"] = "false",
                    ["POSTGRES_HOST"] = connection.Host,
                    ["POSTGRES_PORT"] = connection.Port.ToString(),
                    ["POSTGRES_DB"] = connection.Database,
                    ["POSTGRES_USER"] = connection.Username,
                    ["POSTGRES_PASSWORD"] = connection.Password,
                    ["POSTGRES_MIN_POOL_SIZE"] = "0",
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

                services.AddHttpLogging(_ => { });
            });
        }
    }
}

/// <summary>A seeded identity (user + tenant membership + session) used by the adversarial scenarios.</summary>
public sealed record AdversarialAccount(
    User User,
    Guid TenantId,
    string MembershipRole,
    UserSession Session,
    RefreshToken RefreshToken);
