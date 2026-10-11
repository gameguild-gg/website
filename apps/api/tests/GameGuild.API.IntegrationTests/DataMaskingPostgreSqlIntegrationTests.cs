using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.CQRS.Models;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class DataMaskingPostgreSqlIntegrationTests(ApiPostgreSqlFixture fixture)
{
    private const string TestAuthenticationScheme = "DataMaskingTest";

    [Fact]
    public async Task GetUsersPage_AppliesPersistedTenantRuleUsingRequestActorContext()
    {
        var tenantAId = Guid.NewGuid();
        var tenantBId = Guid.NewGuid();
        var userAId = Guid.NewGuid();
        var userBId = Guid.NewGuid();

        using var factory = fixture.CreateFactory(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationScheme;
                    options.DefaultChallengeScheme = TestAuthenticationScheme;
                    options.DefaultForbidScheme = TestAuthenticationScheme;
                }).AddScheme<AuthenticationSchemeOptions, DataMaskingTestAuthenticationHandler>(
                    TestAuthenticationScheme,
                    _ => { });
                services.AddHttpContextAccessor();
                services.RemoveAll<IActorContextAccessor>();
                services.AddScoped<IActorContextAccessor, DataMaskingTestActorContextAccessor>();
            }));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await SeedAsync(dbContext, tenantAId, tenantBId, userAId, userBId);
        }

        using var tenantAClient = CreateClient(factory, tenantAId, userAId);
        using var tenantBClient = CreateClient(factory, tenantBId, userBId);

        // The collection shares a migrated database. Locate this fixture's rows regardless of earlier tests.
        var tenantAPageResponse = await tenantAClient.GetAsync($"/v1/users?limit=20&q=masking-{userAId:N}");
        var tenantBPageResponse = await tenantBClient.GetAsync($"/v1/users?limit=20&q=masking-{userBId:N}");

        Assert.True(
            tenantAPageResponse.StatusCode == HttpStatusCode.OK,
            $"Tenant A users endpoint returned {(int)tenantAPageResponse.StatusCode}: {await tenantAPageResponse.Content.ReadAsStringAsync()}");
        Assert.True(
            tenantBPageResponse.StatusCode == HttpStatusCode.OK,
            $"Tenant B users endpoint returned {(int)tenantBPageResponse.StatusCode}: {await tenantBPageResponse.Content.ReadAsStringAsync()}");

        using var tenantAPage = await System.Text.Json.JsonDocument.ParseAsync(await tenantAPageResponse.Content.ReadAsStreamAsync());
        var tenantAListedUser = tenantAPage.RootElement.GetProperty("items")
            .EnumerateArray()
            .Single();
        Assert.Equal(userAId, tenantAListedUser.GetProperty("id").GetGuid());
        Assert.Equal("[REDACTED]", tenantAListedUser.GetProperty("email").GetString());

        using var tenantBPage = await System.Text.Json.JsonDocument.ParseAsync(await tenantBPageResponse.Content.ReadAsStreamAsync());
        var tenantBListedUser = tenantBPage.RootElement.GetProperty("items")
            .EnumerateArray()
            .Single();
        Assert.Equal(userBId, tenantBListedUser.GetProperty("id").GetGuid());
        Assert.Equal($"masking-{userBId:N}@example.test", tenantBListedUser.GetProperty("email").GetString());
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory, Guid tenantId, Guid actorId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TenantResolver.TenantIdHeader, tenantId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Actor", actorId.ToString());
        return client;
    }

    private static async Task SeedAsync(
        ApplicationDbContext dbContext,
        Guid tenantAId,
        Guid tenantBId,
        Guid userAId,
        Guid userBId)
    {
        var tenantA = new Tenant
        {
            Id = tenantAId,
            Name = $"Masking Tenant {tenantAId:N}",
            Slug = $"masking-{tenantAId:N}",
            AdminEmail = $"admin-{tenantAId:N}@example.test",
            IsActive = true
        };
        var tenantB = new Tenant
        {
            Id = tenantBId,
            Name = $"Masking Tenant {tenantBId:N}",
            Slug = $"masking-{tenantBId:N}",
            AdminEmail = $"admin-{tenantBId:N}@example.test",
            IsActive = true
        };
        var userA = User.CreateOAuthUser($"masking-{userAId:N}@example.test", "Masking User A");
        userA.Id = userAId;
        var userB = User.CreateOAuthUser($"masking-{userBId:N}@example.test", "Masking User B");
        userB.Id = userBId;

        dbContext.Set<Tenant>().AddRange(tenantA, tenantB);
        dbContext.Set<User>().AddRange(userA, userB);
        dbContext.Set<TenantMember>().AddRange(
            new TenantMember { TenantId = tenantAId, UserId = userAId, Role = "Member", IsActive = true },
            new TenantMember { TenantId = tenantBId, UserId = userBId, Role = "Member", IsActive = true });
        dbContext.Set<DataMaskingRule>().Add(new DataMaskingRule
        {
            TenantId = new TenantId(tenantAId),
            Name = "Mask user email for tenant A",
            ResourceType = "User",
            FieldName = "Email",
            MaskingType = MaskingType.Redact,
            CreatedBy = userAId,
            IsEnabled = true
        });
        await dbContext.SaveChangesAsync();
    }

    public sealed class DataMaskingTestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var actorId = Request.Headers["X-Test-Actor"].ToString();
            var tenantId = Request.Headers[TenantResolver.TenantIdHeader].ToString();
            if (!Guid.TryParse(actorId, out _) || !Guid.TryParse(tenantId, out _))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
            [
                new Claim("sub", actorId),
                new Claim("tenant_id", tenantId),
                new Claim(ClaimTypes.Role, Policies.SystemAdmin),
                new Claim("role", Policies.SystemAdmin),
                new Claim("permission", "users:read:self"),
                new Claim("permission", UsersPermission.Keys.Read)
            ],
            Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }

    private sealed class DataMaskingTestActorContextAccessor(IHttpContextAccessor httpContextAccessor)
        : IActorContextAccessor
    {
        private ActorContext? _actorContext;

        public ActorContext ActorContext
        {
            get
            {
                if (_actorContext is not null)
                {
                    return _actorContext;
                }

                var request = httpContextAccessor.HttpContext?.Request;
                if (request is null ||
                    !Guid.TryParse(request.Headers["X-Test-Actor"], out var actorId) ||
                    !Guid.TryParse(request.Headers[TenantResolver.TenantIdHeader], out var tenantId))
                {
                    return ActorContext.Anonymous;
                }

                return new ActorContext
                {
                    ActorKind = ActorKind.User,
                    SubjectId = actorId.ToString(),
                    TenantId = tenantId,
                    Roles = new HashSet<string>([Policies.SystemAdmin], StringComparer.OrdinalIgnoreCase),
                    Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    IsAuthenticated = true
                };
            }
        }

        public void SetActorContext(ActorContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _actorContext = context;
        }

        public void ClearActorContext()
        {
            _actorContext = null;
        }
    }
}
