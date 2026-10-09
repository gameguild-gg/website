using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Tenants;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GameGuild.Tests.Authentication.Integration;

/// <summary>
/// Prepends a middleware that pins <c>Connection.RemoteIpAddress</c>: TestServer requests
/// otherwise carry no remote address, and the production IP extraction deliberately reads
/// only the connection (forwarding headers can be forged), so the test assigns one here.
/// </summary>
internal sealed class RemoteIpAddressStartupFilter(IPAddress remoteIpAddress) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        => app =>
        {
            app.Use(async (context, proceed) =>
            {
                context.Connection.RemoteIpAddress = remoteIpAddress;
                await proceed();
            });
            next(app);
        };
}

/// <summary>
/// Host configuration for threat-intelligence scenarios: the real module registration and
/// options binding are exercised, pointed at a real local feed file.
/// </summary>
public sealed class ThreatIntelligenceApiFactory : WebApplicationFactory<GameGuild.API.Program>
{
    private const string ClientIpAddress = "203.0.113.77";

    private readonly string _databaseName = $"ThreatIntelIntegrationTests_{Guid.NewGuid():N}";
    private readonly string _enforcementMode;

    public ThreatIntelligenceApiFactory(string enforcementMode)
    {
        _enforcementMode = enforcementMode;
        FeedPath = Path.Combine(Path.GetTempPath(), $"gg-threat-intel-feed-{Guid.NewGuid():N}.json");
    }

    public string FeedPath { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ThreatIntelligence:Provider", "LocalFile");
        builder.UseSetting("ThreatIntelligence:EnforcementMode", _enforcementMode);
        builder.UseSetting("ThreatIntelligence:MaliciousIpRiskScore", "60");
        builder.UseSetting("ThreatIntelligence:LocalFile:FilePath", FeedPath);

        builder.ConfigureTestServices(services =>
        {
            var descriptorsToRemove = services
                .Where(descriptor =>
                    descriptor.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                    descriptor.ServiceType == typeof(ApplicationDbContext) ||
                    descriptor.ServiceType.FullName?.Contains("EntityFramework", StringComparison.Ordinal) == true ||
                    descriptor.ImplementationType?.FullName?.Contains("Npgsql", StringComparison.Ordinal) == true)
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(_databaseName));
            services.AddScoped<DbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
            services.AddMemoryCache();
            services.AddHttpLogging(_ => { });
            services.AddSingleton<IStartupFilter>(new RemoteIpAddressStartupFilter(IPAddress.Parse(ClientIpAddress)));
        });
    }
}

/// <summary>
/// Integration coverage for issue #227: a fed malicious IP raises the login risk score
/// into the step-up flow in Enforce mode, while the default Observation mode only logs.
/// </summary>
public sealed class ThreatIntelligenceFeedIntegrationTests
{
    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter<RiskLevel>() }
    };

    private static void WriteMaliciousBlocklistFeed(string feedPath) =>
        File.WriteAllText(feedPath, """
        {
          "maliciousIpCidrs": [ "203.0.113.0/24" ],
          "breachedPasswordSha256": []
        }
        """);

    private static (ThreatIntelligenceApiFactory Factory, HttpClient Client) CreateHost(string enforcementMode)
    {
        var factory = new ThreatIntelligenceApiFactory(enforcementMode);
        WriteMaliciousBlocklistFeed(factory.FeedPath);

        using (var bootstrapScope = factory.Services.CreateScope())
        {
            var dbContext = bootstrapScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Database.EnsureCreated();
            if (!dbContext.Set<Tenant>().Any(tenant => tenant.IsDefault))
            {
                dbContext.Add(new Tenant
                {
                    Name = "Public tenant",
                    Slug = "public",
                    AdminEmail = "admin@public.test",
                    IsDefault = true,
                    IsActive = true
                });
                dbContext.SaveChanges();
            }
        }

        return (factory, factory.CreateClient());
    }

    private static async Task<SignInResponse> SignUpUserAsync(ThreatIntelligenceApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        return await authService.LocalSignUpAsync(new LocalSignUpRequest
        {
            Email = $"threatintel.{Guid.NewGuid():N}@test.com",
            Username = $"threatintel{Guid.NewGuid():N}"[..30],
            Password = "ThreatIntelTest123!"
        });
    }

    [Fact]
    public async Task FedMaliciousClientIp_InEnforceMode_RaisesSignInToStepUp()
    {
        var (factory, client) = CreateHost("Enforce");
        await using (factory)
        {
            var signUp = await SignUpUserAsync(factory);

            var response = await client.PostAsJsonAsync("/v1/auth/sign-in", new LocalSignInRequest
            {
                Email = signUp.Email,
                Password = "ThreatIntelTest123!"
            });

            var body = await response.Content.ReadAsStringAsync();
            response.IsSuccessStatusCode.Should().BeTrue($"HTTP {response.StatusCode}: {body}");

            var signIn = await response.Content.ReadFromJsonAsync<SignInResponse>(ResponseJsonOptions);
            signIn.Should().NotBeNull();
            signIn!.RequiresStepUp.Should().BeTrue(
                "the client IP is fed as malicious and Enforce mode must raise the risk score into the step-up machinery");
            signIn.Success.Should().BeFalse();
            signIn.AccessToken.Should().BeNullOrEmpty("step-up must precede token issuance");
            ((int)signIn.RiskLevel!.Value).Should().BeGreaterThanOrEqualTo((int)RiskLevel.High);
            signIn.RiskFactors.Should().Contain(f => f.Contains("ThreatIntel:MaliciousIp", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task FedMaliciousClientIp_InDefaultObservationMode_DoesNotBlockOrStepUp()
    {
        var (factory, client) = CreateHost("Observation");
        await using (factory)
        {
            var signUp = await SignUpUserAsync(factory);

            var response = await client.PostAsJsonAsync("/v1/auth/sign-in", new LocalSignInRequest
            {
                Email = signUp.Email,
                Password = "ThreatIntelTest123!"
            });

            var body = await response.Content.ReadAsStringAsync();
            response.IsSuccessStatusCode.Should().BeTrue($"HTTP {response.StatusCode}: {body}");

            var signIn = await response.Content.ReadFromJsonAsync<SignInResponse>(ResponseJsonOptions);
            signIn.Should().NotBeNull();
            signIn!.Success.Should().BeTrue("observation mode must not act on the match");
            signIn.RequiresStepUp.Should().BeFalse();
            signIn.AccessToken.Should().NotBeNullOrEmpty();
        }
    }
}
