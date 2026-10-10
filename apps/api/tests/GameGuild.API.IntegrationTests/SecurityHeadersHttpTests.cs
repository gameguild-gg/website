using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using FluentAssertions;
using GameGuild.API.Core.Security;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace GameGuild.API.IntegrationTests;

/// <summary>
///     HTTP-level response verification of the platform security headers
///     (issue #276: the middleware configuration existed, but no executed-HTTP
///     test asserted the headers on real responses). Exercises the real request
///     pipeline — routing, authentication, ProblemDetails error paths, the
///     Swagger documentation surface and the production-only HSTS branch — and
///     asserts the shipped default header values on every response class.
/// </summary>
/// <remarks>
///     <para>
///         The shared <see cref="ApiPostgreSqlFixture" /> factory runs under the
///         <c>Testing</c> environment for the normal/error/anonymous/sensitive-path
///         cases. The documentation and HSTS cases use dedicated factories that only
///         change the environment (<c>Development</c> maps Swagger UI at
///         <c>/documentation</c>; <c>Production</c> enables <c>UseHsts</c>).
///     </para>
///     <para>
///         Production start-up validates operational configuration
///         (<c>OperationalStartupConfiguration</c>), so the production factory supplies
///         synthetic deployment values through process environment variables (read by
///         <c>Program</c> before the validation runs) and disables rate limiting so the
///         header assertions stay isolated from unrelated infrastructure.
///     </para>
/// </remarks>
[Collection(ApiPostgreSqlCollection.Name)]
public sealed class SecurityHeadersHttpTests(ApiPostgreSqlFixture fixture)
{
    private const string RestrictiveContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
    private const string DocumentationContentSecurityPolicy =
        "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: https:; font-src 'self' data:; connect-src 'self'; frame-ancestors 'none'";

    private const string ExpectedPermissionsPolicy =
        "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";

    private const string SensitiveCacheControl = "no-store, no-cache, must-revalidate";

    [Fact]
    public async Task AuthenticatedSuccessResponseCarriesDefaultRestrictiveSecurityHeaders()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedTenantMemberAsync(tenantId, userId);

        using var client = fixture.CreateAuthenticatedClient(userId, tenantId);
        using var response = await client.GetAsync("/v1/access/capabilities");

        response.StatusCode.Should().Be(HttpStatusCode.OK, "the seeded member must reach the endpoint");
        AssertDefaultSecurityHeaders(response);
    }

    [Fact]
    public async Task AnonymousUnauthorizedResponseCarriesDefaultRestrictiveSecurityHeaders()
    {
        using var client = fixture.Factory.CreateClient();
        using var response = await client.GetAsync("/v1/access/capabilities");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the request carries no credentials and must be rejected before the action runs");
        AssertDefaultSecurityHeaders(response);
    }

    [Fact]
    public async Task ProblemDetailsErrorResponseCarriesDefaultRestrictiveSecurityHeaders()
    {
        using var client = fixture.Factory.CreateClient();

        // The ProblemDetails status-code pages path: an unmatched route produces an
        // RFC 7807 body, and the pre-routing middleware must still decorate it.
        using var response = await client.GetAsync("/v1/routes-that-do-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        AssertDefaultSecurityHeaders(response);
    }

    [Fact]
    public async Task DocumentationPathCarriesTheSwaggerContentSecurityPolicy()
    {
        using var environment = TemporaryEnvironmentVariables.Apply(
            SecurityHeadersEnvironmentFactory.CreateConfiguration(fixture.ConnectionString, "Development"));
        using var factory = new SecurityHeadersEnvironmentFactory(fixture.ConnectionString, "Development");
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/documentation/index.html");

        response.StatusCode.Should().Be(HttpStatusCode.OK, "Swagger UI must be served outside production");
        SingleHeader(response, "Content-Security-Policy").Should().Be(DocumentationContentSecurityPolicy);
        SingleHeader(response, "Content-Security-Policy").Should().NotBe(RestrictiveContentSecurityPolicy);
        SingleHeader(response, "X-Frame-Options").Should().Be("DENY");
        SingleHeader(response, "X-Content-Type-Options").Should().Be("nosniff");
    }

    [Fact]
    public async Task SensitiveAuthenticationPathResponsesAreNotCacheable()
    {
        using var client = fixture.Factory.CreateClient();

        // Token refresh on a sensitive /auth path: the response is an authentication
        // failure that must never be cacheable, and no endpoint filter overrides the
        // cache directives on this action.
        using var refreshResponse = await client.PostAsJsonAsync(
            "/v1/auth/tokens:refresh",
            new { refreshToken = "not-a-real-refresh-token" });

        refreshResponse.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.BadRequest);
        // RFC 9111 §5.2: directive order within Cache-Control is not significant,
        // so compare the directive set rather than the exact emitted ordering.
        CacheControlDirectives(refreshResponse).Should().BeEquivalentTo(
            SensitiveCacheControl.Split(',').Select(directive => directive.Trim()));
        SingleHeader(refreshResponse, "Pragma").Should().Be("no-cache");
        AssertDefaultSecurityHeaders(refreshResponse);

        // Local sign-in applies a stricter endpoint-level "no-store" through the
        // lockout action filter; the pre-routing middleware must not weaken it.
        using var signInResponse = await client.PostAsJsonAsync(
            "/v1/auth/sign-in",
            new { email = $"unknown-{Guid.NewGuid():N}@security-headers.test", password = "not-a-real-password" });

        signInResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        SingleHeader(signInResponse, "Cache-Control").Should().Contain("no-store");
        SingleHeader(signInResponse, "Pragma").Should().Be("no-cache");
    }

    [Fact]
    public async Task ProductionHttpsResponseCarriesStrictTransportSecurity()
    {
        using var environment = TemporaryEnvironmentVariables.Apply(
            SecurityHeadersEnvironmentFactory.CreateConfiguration(fixture.ConnectionString, "Production"));
        using var factory = new SecurityHeadersEnvironmentFactory(fixture.ConnectionString, "Production");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            // Non-loopback host: the in-memory host accepts any authority, and a
            // public host name avoids every loopback special case in the HTTPS
            // redirection and forwarded-headers handling.
            BaseAddress = new Uri("https://hsts-check.gameguild.test")
        });
        using var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // Diagnostics are reflected from the live host so a failure names the broken
        // link (environment name, HstsOptions exclusions, forwarded-headers wiring)
        // instead of only reporting the absent header.
        var hostDiagnostics = DescribeHostSecurityConfiguration(factory.Services);
        HasHeader(response, "Strict-Transport-Security").Should().BeTrue(
            $"HSTS is production-only and must be present on HTTPS responses. Host: {hostDiagnostics}");
        var strictTransportSecurity = SingleHeader(response, "Strict-Transport-Security");
        strictTransportSecurity.Should().StartWith("max-age=",
            "HSTS is production-only and must be present on HTTPS responses");
        AssertDefaultSecurityHeaders(response);
    }

    [Fact]
    public async Task NonProductionHttpsResponseOmitsStrictTransportSecurity()
    {
        using var client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        using var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        HasHeader(response, "Strict-Transport-Security").Should().BeFalse(
            "the shared fixture runs under the Testing environment, where ConfigurePipeline skips UseHsts");
        AssertDefaultSecurityHeaders(response);
    }

    private static void AssertDefaultSecurityHeaders(HttpResponseMessage response)
    {
        SingleHeader(response, "X-Content-Type-Options").Should().Be("nosniff");
        SingleHeader(response, "X-Frame-Options").Should().Be("DENY");
        SingleHeader(response, "X-XSS-Protection").Should().Be("0");
        SingleHeader(response, "Referrer-Policy").Should().Be("strict-origin-when-cross-origin");
        SingleHeader(response, "Permissions-Policy").Should().Be(ExpectedPermissionsPolicy);
        SingleHeader(response, "Content-Security-Policy").Should().Be(RestrictiveContentSecurityPolicy);
    }

    private static bool HasHeader(HttpResponseMessage response, string name) =>
        response.Headers.Contains(name) || response.Content.Headers.Contains(name);

    /// <summary>
    ///     Reflects the security-relevant host configuration (environment name,
    ///     HSTS exclusions, forwarded-headers proxy wiring) for assertion
    ///     diagnostics. Reflection avoids coupling this test project to framework
    ///     option types that resolve inconsistently at compile time here.
    /// </summary>
    private static string DescribeHostSecurityConfiguration(IServiceProvider services)
    {
        var builder = new StringBuilder();
        var environment = services.GetService(typeof(Microsoft.AspNetCore.Hosting.IWebHostEnvironment));
        builder.Append("environment=")
            .Append(environment?.GetType().GetProperty("EnvironmentName")?.GetValue(environment) ?? "<unresolved>");
        AppendOptionsDiagnostics(builder, services, "Microsoft.AspNetCore.HttpsPolicy.HstsOptions, Microsoft.AspNetCore.HttpsPolicy", "hsts");
        AppendOptionsDiagnostics(builder, services, "Microsoft.AspNetCore.HttpOverrides.ForwardedHeadersOptions, Microsoft.AspNetCore.HttpOverrides", "forwarded");
        return builder.ToString();
    }

    private static void AppendOptionsDiagnostics(StringBuilder builder, IServiceProvider services, string typeName, string label)
    {
        var type = Type.GetType(typeName);
        if (type is null)
        {
            builder.Append($"; {label}=type-unresolved");
            return;
        }

        var optionsInterface = typeof(IOptions<>).MakeGenericType(type);
        var options = services.GetService(optionsInterface);
        if (options is null)
        {
            builder.Append($"; {label}=no-ioptions");
            return;
        }

        var value = optionsInterface.GetProperty("Value")!.GetValue(options);
        builder.Append($"; {label}=");
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.Name is "ForwardedHeaders" or "ExcludedHosts" or "KnownProxies"
                or "KnownNetworks" or "KnownIPNetworks" or "ForwardLimit" or "MaxAge" or "IncludeSubDomains")
            {
                builder.Append(property.Name).Append(':').Append(property.GetValue(value)).Append(' ');
            }
        }
    }

    private static IEnumerable<string> CacheControlDirectives(HttpResponseMessage response) =>
        SingleHeader(response, "Cache-Control")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string SingleHeader(HttpResponseMessage response, string name)
    {
        if (response.Headers.TryGetValues(name, out var values))
        {
            return values.Single();
        }

        if (response.Content.Headers.TryGetValues(name, out var contentValues))
        {
            return contentValues.Single();
        }

        throw new InvalidOperationException($"The expected response header '{name}' is missing.");
    }

    private async Task SeedTenantMemberAsync(Guid tenantId, Guid userId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var marker = tenantId.ToString("N");
        context.Set<Tenant>().Add(new Tenant
        {
            Id = tenantId,
            Name = $"Security headers tenant {marker}",
            Slug = $"security-headers-{marker}",
            AdminEmail = $"admin-{marker}@security-headers.test",
            IsActive = true,
        });
        var user = User.CreateOAuthUser(
            $"security-headers-{userId:N}@security-headers.test",
            $"Security headers user {userId:N}");
        user.Id = userId;
        context.Set<User>().Add(user);
        context.Set<TenantMember>().Add(new TenantMember
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            Role = "Member",
            IsActive = true,
        });
        await context.SaveChangesAsync();
    }

    /// <summary>
    ///     Boots the real API host against the fixture database under a chosen
    ///     environment name, mirroring the shared fixture's database and synthetic
    ///     authentication setup.
    /// </summary>
    private sealed class SecurityHeadersEnvironmentFactory(string connectionString, string environmentName)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environmentName);
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
                services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = ApiPostgreSqlTestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = ApiPostgreSqlTestAuthHandler.SchemeName;
                    })
                    .AddScheme<AuthenticationSchemeOptions, ApiPostgreSqlTestAuthHandler>(
                        ApiPostgreSqlTestAuthHandler.SchemeName,
                        _ => { });
                services.AddHttpLogging(_ => { });
            });
        }

        public static IReadOnlyDictionary<string, string?> CreateConfiguration(string cs, string environmentName)
        {
            var connection = new NpgsqlConnectionStringBuilder(cs);
            var configuration = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = cs,
                ["ConnectionStrings:MigrationConnection"] = cs,
                ["ConnectionStrings:AuthenticationDb"] = cs,
                ["Database:FailStartupOnMigrationFailure"] = "true",
                ["Database:GrantRuntimeRoleAfterMigrations"] = "false",
                ["Database:RunStartupInitialization"] = "false",
                ["Authentication:RefreshTokenCleanup:Enabled"] = "false",
                ["PresentationLayer:EnableRateLimiting"] = "false",
                ["POSTGRES_HOST"] = connection.Host,
                ["POSTGRES_PORT"] = connection.Port.ToString(),
                ["POSTGRES_DB"] = connection.Database,
                ["POSTGRES_USER"] = connection.Username,
                ["POSTGRES_PASSWORD"] = connection.Password,
                ["POSTGRES_MIN_POOL_SIZE"] = "0"
            };

            if (string.Equals(environmentName, "Production", StringComparison.Ordinal))
            {
                // OperationalStartupConfiguration requires deployment-grade values for
                // Production even though nothing under test connects to them.
                configuration["Jwt:SecretKey"] = "security-headers-http-tests-jwt-secret-with-at-least-32-characters";
                configuration["Encryption:EncryptionKey"] = Convert.ToBase64String(new byte[32]);
                configuration["Redis:Enabled"] = "true";
                configuration["Redis:ConnectionString"] = "localhost:6379,abortConnect=false";
                // /health aggregates registered health checks; nothing under test
                // connects to Redis, so its readiness check must not be registered.
                configuration["Redis:EnableHealthChecks"] = "false";
                configuration["EmailDelivery:Enabled"] = "true";
                configuration["EmailDelivery:FromEmail"] = "security-headers@gameguild.test";
                configuration["EmailDelivery:Provider"] = "Smtp";
                configuration["EmailDelivery:SmtpHost"] = "localhost";
                configuration["EmailDelivery:SmtpPort"] = "2525";
                configuration["Assets:Storage:ServiceUrl"] = "http://localhost:9000";
                configuration["Assets:Storage:AccessKey"] = "security-headers-access-key";
                configuration["Assets:Storage:SecretKey"] = "security-headers-secret-key";
                configuration["Assets:Storage:BucketName"] = "security-headers-bucket";
                configuration["Assets:Token:SecretKey"] = Convert.ToBase64String(new byte[32]);
            }

            return configuration;
        }
    }

    /// <summary>
    ///     Publishes configuration through process environment variables and restores
    ///     the previous values on disposal. Environment variables are used instead of
    ///     in-memory overrides because <c>Program</c> reads configuration before the
    ///     host is built (operational validation) and env-var providers are ordered
    ///     after the JSON files by the application itself.
    /// </summary>
    private sealed class TemporaryEnvironmentVariables : IDisposable
    {
        private readonly Dictionary<string, string?> _savedValues;

        private TemporaryEnvironmentVariables(Dictionary<string, string?> savedValues)
        {
            _savedValues = savedValues;
        }

        public static TemporaryEnvironmentVariables Apply(IReadOnlyDictionary<string, string?> configuration)
        {
            var saved = new Dictionary<string, string?>();
            foreach (var (key, value) in configuration)
            {
                // .NET configuration maps hierarchical separators to "__"; flat keys
                // (POSTGRES_*) pass through unchanged.
                var environmentKey = key.Contains(':') || key.Contains('.')
                    ? string.Join("__", key.Split([':', '.']))
                    : key;
                saved[environmentKey] = Environment.GetEnvironmentVariable(environmentKey);
                Environment.SetEnvironmentVariable(environmentKey, value);
            }

            return new TemporaryEnvironmentVariables(saved);
        }

        public void Dispose()
        {
            foreach (var (key, original) in _savedValues)
            {
                Environment.SetEnvironmentVariable(key, original);
            }
        }
    }
}
