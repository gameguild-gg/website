using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class RefreshTokenLineageFailurePostgreSqlHttpTests(ApiPostgreSqlFixture fixture)
{
    [Theory]
    [InlineData("binding")]
    [InlineData("rotation")]
    public async Task ActualCommandFailureAfterLineageWriteRollsBackTokensSessionAndClaim(string failureStage)
    {
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)) + "Aa1!";
        var marker = Guid.NewGuid().ToString("N");
        var tenantId = Guid.NewGuid();
        User user;
        using (var seed = fixture.Factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            user = User.CreateWithPassword($"lineage-fault-{marker}@example.test", "Synthetic lineage account",
                seed.ServiceProvider.GetRequiredService<IPasswordHasher>().HashPassword(password), $"lineage-fault-{marker}");
            db.Set<User>().Add(user);
            db.Set<Tenant>().Add(new Tenant { Id = tenantId, Name = $"Lineage fault {marker}", Slug = $"lineage-fault-{marker}",
                AdminEmail = $"admin-{marker}@example.test", IsActive = true });
            db.Set<TenantMember>().Add(new TenantMember { Id = Guid.NewGuid(), UserId = user.Id,
                TenantId = tenantId, IsActive = true, Role = "Member" });
            await db.SaveChangesAsync();
        }
        string? raw = null;
        Guid? sessionId = null;
        string? storedHash = null;
        if (failureStage == "rotation")
        {
            using var initialClient = fixture.Factory.CreateClient();
            using var login = await initialClient.PostAsJsonAsync("/v1/auth/polymorphic", new { credential = user.Email, password, tenantId });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            using var payload = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
            raw = payload.RootElement.GetProperty("refreshToken").GetString()!;
            sessionId = payload.RootElement.GetProperty("sessionId").GetGuid();
            using var scope = fixture.Factory.Services.CreateScope();
            storedHash = scope.ServiceProvider.GetRequiredService<IRefreshTokenHasher>().HashToken(raw);
        }
        var evidence = new PersistedWriteEvidence();
        using var faulty = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRefreshTokenLineageRepository>();
            services.AddScoped<IRefreshTokenLineageRepository>(provider => new FailureAfterPersistedLineage(
                (IRefreshTokenLineageRepository)provider.GetRequiredService<IRefreshTokenRepository>(), failureStage, evidence));
        }));
        using var client = faulty.CreateClient();
        using var response = failureStage == "binding"
            ? await client.PostAsJsonAsync("/v1/auth/polymorphic", new { credential = user.Email, password, tenantId })
            : await client.PostAsJsonAsync("/v1/auth/tokens:refresh", new { refreshToken = raw, tenantId });
        Assert.True(evidence.WriteCompleted, "The fault must occur after the real lineage write; an earlier credential denial cannot satisfy this test.");
        Assert.Equal(failureStage == "binding" ? HttpStatusCode.Unauthorized : HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(password, body, StringComparison.Ordinal);
        if (raw is not null) { Assert.DoesNotContain(raw, body, StringComparison.Ordinal); }
        using var verification = fixture.Factory.Services.CreateScope();
        var context = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tokens = await context.Set<RefreshToken>().AsNoTracking().Where(value => value.UserId == user.Id).ToListAsync();
        var sessions = await context.Set<UserSession>().AsNoTracking().Where(value => value.UserId == user.Id).ToListAsync();
        if (failureStage == "binding")
        {
            Assert.Empty(tokens);
            Assert.Empty(sessions);
        }
        else
        {
            var token = Assert.Single(tokens);
            Assert.False(token.IsRevoked);
            Assert.Null(token.ReplacedByToken);
            Assert.Null(token.ParentTokenId);
            Assert.Equal(sessionId, token.SessionId);
            Assert.Equal(storedHash, token.Token);
            var session = Assert.Single(sessions);
            Assert.True(session.IsActive);
            Assert.Equal(sessionId, session.Id);
            Assert.Equal(storedHash, session.RefreshToken);
        }
        Assert.Equal(user.TokenVersion, (await context.Set<User>().AsNoTracking().SingleAsync(value => value.Id == user.Id)).TokenVersion);
    }

    private sealed class PersistedWriteEvidence
    {
        public bool WriteCompleted { get; set; }
    }

    private sealed class FailureAfterPersistedLineage(IRefreshTokenLineageRepository inner, string failureStage,
        PersistedWriteEvidence evidence) : IRefreshTokenLineageRepository
    {
        public Task<Guid?> RevokeFamilyAsync(Guid userId, Guid tokenId, string? revokedByIp, CancellationToken cancellationToken) =>
            inner.RevokeFamilyAsync(userId, tokenId, revokedByIp, cancellationToken);

        public async Task<bool> BindSessionAsync(Guid userId, string tokenHash, Guid sessionId, CancellationToken cancellationToken)
        {
            var result = await inner.BindSessionAsync(userId, tokenHash, sessionId, cancellationToken);
            if (failureStage == "binding")
            {
                Assert.True(result);
                evidence.WriteCompleted = true;
                throw new InvalidOperationException("Synthetic failure after persisted binding");
            }
            return result;
        }

        public async Task RecordRotationAsync(Guid userId, Guid parentTokenId, string replacementTokenHash, Guid sessionId, CancellationToken cancellationToken)
        {
            await inner.RecordRotationAsync(userId, parentTokenId, replacementTokenHash, sessionId, cancellationToken);
            evidence.WriteCompleted = true;
            throw new InvalidOperationException("Synthetic failure after persisted rotation metadata");
        }
    }
}
