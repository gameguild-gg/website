using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using Asp.Versioning;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.CQRS;
using GameGuild.Features;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Moq;
using PresentationAuthenticationOptions = GameGuild.Configuration.PresentationLayer.Authentication.AuthenticationOptions;
using Subscription = GameGuild.Commerce.Subscriptions.Subscription;
using SubscriptionPlan = GameGuild.Commerce.Subscriptions.SubscriptionPlan;

namespace GameGuild.API.UnitTests.Security;

public sealed class CapabilityAuthorizationHttpTests
{
    private static readonly string Secret = new('c', 64);
    private static readonly Guid UserId = Guid.Parse("24604e55-82bf-4e99-8060-9ed5c58197cc");
    private static readonly Guid TenantId = Guid.Parse("286e6538-fdb7-4745-a198-e13a59c259c6");
    private static readonly Guid OtherTenantId = Guid.Parse("c7a335c0-45d4-4711-96d5-1a0c5e01c499");
    private static readonly string[] Endpoints = ["list", "check", "set", "remove", "sync", "audit"];

    public static IEnumerable<object[]> AllEndpoints() => Endpoints.Select(endpoint => new object[] { endpoint });

    public static IEnumerable<object[]> CrossTenantCases()
    {
        foreach (var endpoint in Endpoints)
        foreach (var role in new[] { "Member", "TenantAdmin" })
            yield return [endpoint, role];
    }

    public static IEnumerable<object[]> InvalidSubjects()
    {
        foreach (var endpoint in Endpoints)
        foreach (var subject in new[] { "missing", "invalid", Guid.Empty.ToString() })
            yield return [endpoint, subject];
    }

    public static IEnumerable<object[]> Administrators()
    {
        foreach (var endpoint in Endpoints)
        foreach (var role in new[] { "Owner", "TenantAdmin", "Admin", "SystemAdmin" })
            yield return [endpoint, role];
    }

    [Theory]
    [MemberData(nameof(AllEndpoints))]
    public async Task Endpoints_RequireAuthentication(string endpoint)
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();
        using var response = await SendAsync(client, endpoint, TenantId);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertSeedUnchangedAsync(app);
    }

    [Theory]
    [MemberData(nameof(CrossTenantCases))]
    public async Task Endpoints_RejectOtherTenantBeforeReadingOrWriting(string endpoint, string role)
    {
        await using var app = await CreateAppAsync();
        using var client = AuthenticatedClient(app, UserId.ToString(), TenantId, role);
        using var response = await SendAsync(client, endpoint, OtherTenantId);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertSeedUnchangedAsync(app);
    }

    [Theory]
    [MemberData(nameof(AllEndpoints))]
    public async Task Endpoints_RejectMissingCurrentTenant(string endpoint)
    {
        await using var app = await CreateAppAsync();
        using var client = AuthenticatedClient(app, UserId.ToString(), null, "TenantAdmin");
        using var response = await SendAsync(client, endpoint, TenantId);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertSeedUnchangedAsync(app);
    }

    [Theory]
    [MemberData(nameof(AllEndpoints))]
    public async Task Endpoints_RejectEmptyTargetTenant(string endpoint)
    {
        await using var app = await CreateAppAsync();
        using var client = AuthenticatedClient(app, UserId.ToString(), TenantId, "SystemAdmin");
        using var response = await SendAsync(client, endpoint, Guid.Empty);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertSeedUnchangedAsync(app);
    }

    [Theory]
    [InlineData("set")]
    [InlineData("remove")]
    [InlineData("sync")]
    [InlineData("audit")]
    public async Task Management_RequiresAdministratorEvenWithFeatureFlagPermission(string endpoint)
    {
        await using var app = await CreateAppAsync();
        using var client = AuthenticatedClient(app, UserId.ToString(), TenantId, "Member");
        using var response = await SendAsync(client, endpoint, TenantId);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertSeedUnchangedAsync(app);
    }

    [Theory]
    [MemberData(nameof(InvalidSubjects))]
    public async Task Endpoints_RejectInvalidAuthenticatedUser(string endpoint, string subject)
    {
        await using var app = await CreateAppAsync();
        using var client = AuthenticatedClient(app, subject, TenantId, "TenantAdmin");
        using var response = await SendAsync(client, endpoint, TenantId);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertSeedUnchangedAsync(app);
    }

    [Theory]
    [MemberData(nameof(AllEndpoints))]
    public async Task Endpoints_FailClosedWithoutResolvedActor(string endpoint)
    {
        await using var app = await CreateAppAsync(resolveActor: false);
        using var client = AuthenticatedClient(app, UserId.ToString(), TenantId, "TenantAdmin");
        using var response = await SendAsync(client, endpoint, TenantId);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertSeedUnchangedAsync(app);
    }

    [Theory]
    [InlineData("list")]
    [InlineData("check")]
    public async Task Reads_AllowAuthenticatedMemberOfCurrentTenant(string endpoint)
    {
        await using var app = await CreateAppAsync();
        using var client = AuthenticatedClient(app, UserId.ToString(), TenantId, "Member");
        using var response = await SendAsync(client, endpoint, TenantId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("branding.custom", await response.Content.ReadAsStringAsync());
        await AssertSeedUnchangedAsync(app);
    }

    [Theory]
    [MemberData(nameof(Administrators))]
    public async Task Endpoints_PreserveCurrentTenantAdministration(string endpoint, string role)
    {
        await using var app = await CreateAppAsync();
        using var client = AuthenticatedClient(app, UserId.ToString(), TenantId, role);
        using var response = await SendAsync(client, endpoint, TenantId);
        Assert.Equal(ExpectedSuccess(endpoint), response.StatusCode);
        await AssertAuthorizedStateAsync(app, endpoint, TenantId);
    }

    [Theory]
    [MemberData(nameof(AllEndpoints))]
    public async Task Endpoints_PreserveExplicitSystemAdministratorCrossTenantAccess(string endpoint)
    {
        await using var app = await CreateAppAsync();
        using var client = AuthenticatedClient(app, UserId.ToString(), null, "SystemAdmin");
        using var response = await SendAsync(client, endpoint, OtherTenantId);
        Assert.Equal(ExpectedSuccess(endpoint), response.StatusCode);
        await AssertAuthorizedStateAsync(app, endpoint, OtherTenantId);
    }

    [Fact]
    public async Task Member_CannotEnableCapabilityInPersistedState()
    {
        await using var app = await CreateAppAsync();
        using var client = AuthenticatedClient(app, UserId.ToString(), TenantId, "Member");
        using var response = await SendAsync(client, "set", TenantId);
        await AssertSeedUnchangedAsync(app);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static HttpStatusCode ExpectedSuccess(string endpoint) => endpoint is "list" or "check" or "audit"
        ? HttpStatusCode.OK : HttpStatusCode.NoContent;

    private static async Task AssertSeedUnchangedAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CapabilityDbContext>();
        var capabilities = await db.Set<TenantCapability>().ToListAsync();
        Assert.Equal(2, capabilities.Count);
        Assert.All(capabilities, capability => Assert.False(capability.IsEnabled));
        Assert.Equal(2, await db.Set<CapabilityAuditLog>().CountAsync());
    }

    private static async Task AssertAuthorizedStateAsync(WebApplication app, string endpoint, Guid target)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CapabilityDbContext>();
        var other = await db.Set<TenantCapability>().SingleAsync(row => row.TenantId != target);
        Assert.False(other.IsEnabled);
        var rows = await db.Set<TenantCapability>().Where(row => row.TenantId == target).ToListAsync();
        if (endpoint == "set")
        {
            Assert.True(Assert.Single(rows).IsEnabled);
            Assert.Equal(UserId, rows[0].ModifiedByUserId);
            var audit = await db.Set<CapabilityAuditLog>().SingleAsync(row => row.TenantId == target && row.ChangeReason == "native test");
            Assert.Equal(UserId, audit.ChangedByUserId);
        }
        else if (endpoint == "remove")
        {
            Assert.Empty(rows);
            var audit = await db.Set<CapabilityAuditLog>().SingleAsync(row => row.TenantId == target && row.NewSource == "removed");
            Assert.Equal(UserId, audit.ChangedByUserId);
        }
        else if (endpoint == "sync")
        {
            Assert.Contains(rows, row => row.Source == "plan:starter" && row.IsEnabled);
            Assert.False(rows.Single(row => row.CapabilityKey == "branding.custom" && row.Source == "override:test").IsEnabled);
        }
        else
        {
            await AssertSeedUnchangedAsync(app);
        }
    }

    private static async Task<WebApplication> CreateAppAsync(bool resolveActor = true)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = Secret, ["Jwt:Issuer"] = "capability-test-issuer",
            ["Jwt:Audience"] = "capability-test-audience"
        });
        builder.Services.SetupAuthentication(builder.Configuration, new PresentationAuthenticationOptions
        {
            JwtSecretKey = Secret, JwtIssuer = "capability-test-issuer", JwtAudience = "capability-test-audience"
        });
        builder.Services.AddAuthorization();
        builder.Services.AddApiVersioning(options => options.ApiVersionReader = new UrlSegmentApiVersionReader()).AddMvc();
        builder.Services.AddControllers().ConfigureApplicationPartManager(manager =>
        {
            manager.ApplicationParts.Clear();
            manager.ApplicationParts.Add(new CapabilityControllerPart());
        });
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<IActorContextAccessor, ActorContextAccessor>();
        builder.Services.AddScoped<IClaimsPrincipalAccessor, HttpContextClaimsPrincipalAccessor>();
        var resolver = new Mock<IAuthorizationTenantResolver>();
        resolver.Setup(service => service.ResolveTenantIdAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>()))
            .Returns<HttpContext, CancellationToken>((context, _) => Task.FromResult(context.User.FindFirst("tenant_id")?.Value));
        builder.Services.AddSingleton(resolver.Object);
        var permissions = new Mock<IAuthorizationPermissionService>();
        permissions.Setup(service => service.GetPermissionsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["features:manage"]);
        builder.Services.AddSingleton(permissions.Object);
        var databaseName = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<CapabilityDbContext>(options => options.UseInMemoryDatabase(databaseName));
        builder.Services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<CapabilityDbContext>());
        builder.Services.AddMemoryCache();
        builder.Services.AddScoped<ICapabilityService, CapabilityService>();
        builder.Services.AddSingleton(Mock.Of<IFeatureFlagEvaluationService>());
        builder.Services.AddScoped<FeatureOperationCommandHandler>();
        builder.Services.AddScoped<ISender>(provider =>
        {
            var handler = provider.GetRequiredService<FeatureOperationCommandHandler>();
            var sender = new Mock<ISender>();
            sender.Setup(service => service.Send(It.IsAny<SetCapabilityOverrideCommand>(), It.IsAny<CancellationToken>()))
                .Returns<SetCapabilityOverrideCommand, CancellationToken>(handler.Handle);
            sender.Setup(service => service.Send(It.IsAny<RemoveCapabilityOverrideCommand>(), It.IsAny<CancellationToken>()))
                .Returns<RemoveCapabilityOverrideCommand, CancellationToken>(handler.Handle);
            sender.Setup(service => service.Send(It.IsAny<SyncCapabilitiesFromPlanCommand>(), It.IsAny<CancellationToken>()))
                .Returns<SyncCapabilitiesFromPlanCommand, CancellationToken>(handler.Handle);
            return sender.Object;
        });
        var app = builder.Build();
        app.UseAuthentication();
        if (resolveActor) app.UseMiddleware<ActorContextMiddleware>();
        app.UseAuthorization();
        app.MapControllers();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CapabilityDbContext>();
            var plan = new SubscriptionPlan("Starter", "starter", 1000) { Id = Guid.NewGuid() };
            db.Add(plan);
            foreach (var tenant in new[] { TenantId, OtherTenantId })
            {
                var subscription = new Subscription(tenant, plan.Id, Guid.NewGuid(), BillingCycle.Monthly,
                    new Money(10m), DateTime.UtcNow.AddDays(-1)) { Id = Guid.NewGuid(), Plan = plan };
                subscription.Activate();
                db.Add(subscription);
                db.Add(new TenantCapability { TenantId = tenant, CapabilityKey = "branding.custom", IsEnabled = false, Source = "override:test", Priority = 1000 });
                db.Add(new CapabilityAuditLog { TenantId = tenant, CapabilityKey = "branding.custom", NewValue = false, ChangedAt = DateTimeOffset.UtcNow });
            }
            await db.SaveChangesAsync();
        }
        await app.StartAsync();
        return app;
    }

    private static HttpClient AuthenticatedClient(WebApplication app, string subject, Guid? tenant, string role)
    {
        var claims = new List<Claim> { new("actor_type", "user"), new("role", role), new("permission", "features:manage") };
        if (subject != "missing") claims.Add(new Claim("sub", subject));
        if (tenant.HasValue) claims.Add(new Claim("tenant_id", tenant.Value.ToString()));
        var token = new JwtSecurityToken("capability-test-issuer", "capability-test-audience", claims,
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), "HS256"));
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string endpoint, Guid tenant)
    {
        var path = $"/v1.0/tenants/{tenant}/capabilities";
        return endpoint switch
        {
            "list" => client.GetAsync(path),
            "check" => client.GetAsync(path + "/branding.custom"),
            "audit" => client.GetAsync(path + "/audit-log"),
            "set" => client.PostAsJsonAsync(path, new SetCapabilityOverrideRequest("branding.custom", true, "override:test", "native test", null)),
            "remove" => client.DeleteAsync(path + "/branding.custom"),
            "sync" => client.PostAsync(path + "/sync", null),
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint))
        };
    }

    private sealed class CapabilityControllerPart : ApplicationPart, IApplicationPartTypeProvider
    {
        public override string Name => nameof(CapabilityControllerPart);
        public IEnumerable<TypeInfo> Types => [typeof(CapabilitiesController).GetTypeInfo()];
    }

    private sealed class CapabilityDbContext(DbContextOptions<CapabilityDbContext> options) : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TenantCapability>();
            modelBuilder.Entity<CapabilityAuditLog>();
            modelBuilder.Entity<SubscriptionPlan>();
            modelBuilder.Entity<Subscription>().OwnsOne(subscription => subscription.Amount);
            modelBuilder.Entity<Subscription>().HasOne(subscription => subscription.Plan)
                .WithMany(plan => plan.Subscriptions).HasForeignKey("PlanId");
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Database.BeginTransactionAsync(cancellationToken);
    }
}
