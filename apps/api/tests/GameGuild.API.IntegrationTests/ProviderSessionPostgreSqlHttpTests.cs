using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace GameGuild.API.IntegrationTests;

[CollectionDefinition(nameof(ProviderSessionPostgreSqlCollection), DisableParallelization = true)]
public sealed class ProviderSessionPostgreSqlCollection : ICollectionFixture<ProviderSessionPostgreSqlFixture>
{
}

public sealed class ProviderSessionPostgreSqlFixture : IAsyncLifetime
{
    private readonly ApiPostgreSqlFixture inner = new();
    public WebApplicationFactory<Program> Factory => inner.Factory;
    public Guid TenantId { get; } = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await inner.InitializeAsync();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var marker = TenantId.ToString("N");
        db.Set<Tenant>().Add(new Tenant { Id = TenantId, Name = "Provider session tenant", Slug = "provider-" + marker,
            AdminEmail = "admin-" + marker + "@example.test", IsActive = true, IsDefault = true });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => inner.DisposeAsync();
}

// Provider verification is the only simulated boundary. JWT signing, bearer validation,
// tenant resolution, command transactions and the migrated PostgreSQL stores are real.
[Collection(nameof(ProviderSessionPostgreSqlCollection))]
public sealed class ProviderSessionPostgreSqlHttpTests(ProviderSessionPostgreSqlFixture fixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProviderSignInCreatesHashedRootAndUsableBearerWithContinuousRefreshSession(bool webAuthn)
    {
        var user = CreateUser();
        using var factory = CreateFactory(user);
        await SeedAsync(factory, user);
        using var client = factory.CreateClient();
        using var response = await SignInAsync(client, webAuthn, fixture.TenantId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var access = payload.RootElement.GetProperty("accessToken").GetString()!;
        var rawRefresh = payload.RootElement.GetProperty("refreshToken").GetString()!;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(access);
        var sessionId = Guid.Parse(jwt.Claims.Single(claim => claim.Type == JwtClaimTypes.SessionId).Value);
        Assert.Equal(user.Id.ToString(), jwt.Subject);
        Assert.Equal(fixture.TenantId.ToString(), jwt.Claims.Single(claim => claim.Type == JwtClaimTypes.TenantId).Value);
        if (!webAuthn)
        {
            Assert.Equal(sessionId, payload.RootElement.GetProperty("sessionId").GetGuid());
            Assert.Equal(fixture.TenantId, payload.RootElement.GetProperty("tenantId").GetGuid());
        }
        using (var verification = factory.Services.CreateScope())
        {
            var db = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stored = await db.Set<RefreshToken>().SingleAsync(token => token.UserId == user.Id);
            var session = await db.Set<UserSession>().SingleAsync(value => value.Id == sessionId);
            Assert.Equal(sessionId, stored.SessionId);
            Assert.Null(stored.ParentTokenId);
            Assert.Equal(new RefreshTokenHasher().HashToken(rawRefresh), stored.Token);
            Assert.Equal(stored.Token, session.RefreshToken);
            Assert.NotEqual(rawRefresh, stored.Token);
            Assert.True(session.IsValid);
            Assert.Equal(stored.ExpiresAt, session.ExpiresAt);
            Assert.True(Math.Abs((stored.ExpiresAt - payload.RootElement.GetProperty("refreshTokenExpiresAt").GetDateTime()).Ticks) < 10,
                "PostgreSQL stores timestamps with microsecond precision.");
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        using var sessions = await client.GetAsync("/v1/auth/sessions");
        Assert.Equal(HttpStatusCode.OK, sessions.StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        using var refreshed = await client.PostAsJsonAsync("/v1/auth/tokens:refresh", new { refreshToken = rawRefresh, tenantId = fixture.TenantId });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        using var refreshPayload = JsonDocument.Parse(await refreshed.Content.ReadAsStringAsync());
        Assert.Equal(sessionId, refreshPayload.RootElement.GetProperty("sessionId").GetGuid());
        using var scope = factory.Services.CreateScope();
        var finalDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tokens = await finalDb.Set<RefreshToken>().Where(token => token.UserId == user.Id).ToListAsync();
        var root = Assert.Single(tokens, token => token.ParentTokenId is null);
        var child = Assert.Single(tokens, token => token.ParentTokenId == root.Id);
        Assert.True(root.IsRevoked);
        Assert.Equal(sessionId, child.SessionId);
        Assert.Equal(new RefreshTokenHasher().HashToken(refreshPayload.RootElement.GetProperty("refreshToken").GetString()!), child.Token);
    }

    [Fact]
    public async Task MagicLinkCannotSelectForeignTenantOrCreateUnboundCredentials()
    {
        var user = CreateUser();
        using var factory = CreateFactory(user);
        await SeedAsync(factory, user);
        using var client = factory.CreateClient();
        using var response = await SignInAsync(client, false, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertNoCredentialsAsync(factory, user.Id);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task RevokedOrPendingMembershipCannotBeReactivatedByProviderLogin(bool webAuthn, bool pending)
    {
        var user = CreateUser();
        using var factory = CreateFactory(user);
        await SeedAsync(factory, user, activeMembership: pending, pendingInvite: pending);
        using var client = factory.CreateClient();
        using var response = await SignInAsync(client, webAuthn, fixture.TenantId);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertNoCredentialsAsync(factory, user.Id);
        using var scope = factory.Services.CreateScope();
        var member = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<TenantMember>().SingleAsync(value => value.UserId == user.Id);
        Assert.Equal(pending, member.IsActive);
        Assert.Equal(pending ? TenantMemberInviteStatuses.Pending : null, TenantMemberInviteMetadata.FromJson(member.Metadata).InviteStatus);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuspendedAccountCannotIssueProviderCredentials(bool webAuthn)
    {
        var user = CreateUser();
        user.IsSuspended = true;
        using var factory = CreateFactory(user);
        await SeedAsync(factory, user);
        using var client = factory.CreateClient();
        using var response = await SignInAsync(client, webAuthn, fixture.TenantId);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertNoCredentialsAsync(factory, user.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailureAfterActualBindingRollsBackSessionAndRootWithoutReturningCredentials(bool webAuthn)
    {
        var user = CreateUser();
        var evidence = new BindingEvidence();
        using var factory = CreateFactory(user, services =>
        {
            services.RemoveAll<IRefreshTokenLineageRepository>();
            services.AddScoped<IRefreshTokenLineageRepository>(provider => new PostBindingFailure(
                (IRefreshTokenLineageRepository)provider.GetRequiredService<IRefreshTokenRepository>(), evidence));
        });
        await SeedAsync(factory, user);
        using var client = factory.CreateClient();
        using var response = await SignInAsync(client, webAuthn, fixture.TenantId);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.True(evidence.Written, "The fault must follow an actual database binding.");
        await AssertNoCredentialsAsync(factory, user.Id);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("accessToken", body);
        Assert.DoesNotContain("refreshToken", body);
    }

    private WebApplicationFactory<Program> CreateFactory(User user, Action<IServiceCollection>? extraServices = null) =>
        fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            });
            var magicLink = new Mock<IEmailVerificationService>();
            magicLink.Setup(service => service.VerifyMagicLinkTokenAsync("synthetic-verified-magic-link"))
                .ReturnsAsync(new TokenValidationResult(true, user.Id, user.Email));
            services.RemoveAll<IEmailVerificationService>();
            services.AddSingleton(magicLink.Object);
            var webAuthn = new Mock<IWebAuthnService>();
            webAuthn.Setup(service => service.CompleteAuthenticationAsync("synthetic-verified-assertion", It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new WebAuthnAuthenticationResult { Success = true, UserId = user.Id,
                    CredentialId = Guid.NewGuid(), IsPasswordless = true });
            services.RemoveAll<IWebAuthnService>();
            services.AddSingleton(webAuthn.Object);
            extraServices?.Invoke(services);
        }));

    private async Task SeedAsync(WebApplicationFactory<Program> factory, User user, bool activeMembership = true, bool pendingInvite = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Set<User>().Add(user);
        db.Set<TenantMember>().Add(new TenantMember { Id = Guid.NewGuid(), UserId = user.Id, TenantId = fixture.TenantId,
            IsActive = activeMembership, Role = "Member", Metadata = pendingInvite
                ? TenantMemberInviteMetadata.CreatePending("admin@example.test", DateTime.UtcNow, user.Email).ToJson() : null });
        await db.SaveChangesAsync();
    }

    private static User CreateUser()
    {
        var marker = Guid.NewGuid().ToString("N");
        var user = User.Create("provider-" + marker + "@example.test", "Synthetic provider account");
        user.Username = "provider-" + marker;
        user.TokenVersion = 7;
        return user;
    }

    private static Task<HttpResponseMessage> SignInAsync(HttpClient client, bool webAuthn, Guid tenantId) => webAuthn
        ? client.PostAsJsonAsync("/v1/auth/webauthn/authentication:complete", new { assertionResponse = "synthetic-verified-assertion" })
        : client.PostAsJsonAsync("/v1/auth/magic-link:consume", new { token = "synthetic-verified-magic-link", tenantId, deviceFingerprint = "synthetic-provider-device" });

    private static async Task AssertNoCredentialsAsync(WebApplicationFactory<Program> factory, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Set<RefreshToken>().AnyAsync(token => token.UserId == userId));
        Assert.False(await db.Set<UserSession>().AnyAsync(session => session.UserId == userId));
    }

    private sealed class BindingEvidence
    {
        public bool Written { get; set; }
    }

    private sealed class PostBindingFailure(IRefreshTokenLineageRepository inner, BindingEvidence evidence) : IRefreshTokenLineageRepository
    {
        public Task<Guid?> RevokeFamilyAsync(Guid userId, Guid tokenId, string? revokedByIp, CancellationToken cancellationToken) =>
            inner.RevokeFamilyAsync(userId, tokenId, revokedByIp, cancellationToken);

        public async Task<bool> BindSessionAsync(Guid userId, string tokenHash, Guid sessionId, CancellationToken cancellationToken)
        {
            evidence.Written = await inner.BindSessionAsync(userId, tokenHash, sessionId, cancellationToken);
            throw new InvalidOperationException("Synthetic post-binding failure.");
        }

        public Task RecordRotationAsync(Guid userId, Guid parentTokenId, string replacementHash, Guid sessionId, CancellationToken cancellationToken) =>
            inner.RecordRotationAsync(userId, parentTokenId, replacementHash, sessionId, cancellationToken);
    }
}
