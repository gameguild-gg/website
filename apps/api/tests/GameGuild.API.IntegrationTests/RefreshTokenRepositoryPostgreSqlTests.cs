using System.Security.Cryptography;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using GameGuild.API.Database;
using GameGuild.API.Eventing;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.CQRS;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class RefreshTokenRepositoryPostgreSqlTests(ApiPostgreSqlFixture fixture)
{
    private static readonly string RefreshEndpoint = "/" + typeof(AuthController).GetMethod(nameof(AuthController.RefreshToken))!
        .GetCustomAttribute<HttpPostAttribute>()!.Template!.Replace("v{version:apiVersion}", "v1", StringComparison.Ordinal);

    [Theory]
    [InlineData("ordinary-success", true)]
    [InlineData("ordinary-denial", false)]
    [InlineData("committed-denial", true)]
    [InlineData("exception", false)]
    public async Task RealTransactionKeepsExplicitCompletedDenialButRollsBackOrdinaryFailure(string scenario, bool commit)
    {
        var now = DateTime.UtcNow;
        var token = Token(Guid.NewGuid(), now.AddHours(1), now);
        await SeedAsync(token);
        using var scope = fixture.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var accessor = scope.ServiceProvider.GetRequiredService<IUseCaseOperationContextAccessor>();
        var actor = new Mock<IActorContextAccessor>();
        actor.SetupGet(value => value.ActorContext).Returns(ActorContext.Anonymous);
        var behavior = new UseCaseOperationBehavior<TransactionProbeCommand, object>(context, actor.Object, accessor);
        var beforeOutbox = await context.Set<OutboxMessage>().CountAsync();
        async Task<object> Mutation()
        {
            var stored = await context.Set<RefreshToken>().SingleAsync(value => value.Id == token.Id);
            stored.IsRevoked = true;
            stored.RevokedAt = now;
            await context.SaveChangesAsync();
            if (scenario == "exception") { throw new InvalidOperationException("Synthetic failure after persistence"); }
            return scenario == "committed-denial" ? new CompletedDenial(false) : new OrdinaryResult(scenario == "ordinary-success");
        }
        if (scenario == "exception")
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(new TransactionProbeCommand(), Mutation, CancellationToken.None));
        }
        else
        {
            var outcome = await behavior.Handle(new TransactionProbeCommand(), Mutation, CancellationToken.None);
            Assert.Equal(scenario != "ordinary-success", CommandOutcome.IsFailure(outcome));
        }
        Assert.Null(accessor.Current);
        using var verification = fixture.Factory.Services.CreateScope();
        var db = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(commit, (await db.Set<RefreshToken>().AsNoTracking().SingleAsync(value => value.Id == token.Id)).IsRevoked);
        Assert.Equal(beforeOutbox + (commit ? 1 : 0), await db.Set<OutboxMessage>().CountAsync());
    }

    [Theory]
    [InlineData("replaced")]
    [InlineData("revoked")]
    [InlineData("expired")]
    [InlineData("unknown")]
    public async Task ActualRefreshEndpointDeniesInvalidTokensAndContainsOnlyReplay(string scenario)
    {
        var now = DateTime.UtcNow;
        var input = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var marker = Guid.NewGuid().ToString("N");
        var user = User.Create($"refresh-{marker}@example.test", "Synthetic refresh account");
        user.Username = $"refresh-{marker}";
        using var scope = fixture.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IRefreshTokenHasher>();
        var presented = Token(user.Id, scenario == "expired" ? now.AddMinutes(-1) : now.AddHours(1), now,
            revoked: scenario is "revoked" or "replaced");
        presented.Token = hasher.HashToken(input);
        if (scenario == "replaced") { presented.ReplacedByToken = Hash(); }
        var survivor = Token(user.Id, now.AddHours(1), now);
        var otherUser = Token(Guid.NewGuid(), now.AddHours(1), now);
        var session = new UserSession
        {
            Id = Guid.NewGuid(), UserId = user.Id, RefreshToken = survivor.Token, IpAddress = "127.0.0.1",
            ExpiresAt = now.AddHours(1), CreatedAt = now, UpdatedAt = now, LastUsedAt = now, IsActive = true
        };
        context.Set<User>().Add(user);
        context.Set<RefreshToken>().AddRange(survivor, otherUser);
        if (scenario != "unknown") { context.Set<RefreshToken>().Add(presented); }
        context.Set<UserSession>().Add(session);
        await context.SaveChangesAsync();
        using var client = fixture.Factory.CreateClient();
        using var response = await client.PostAsJsonAsync(RefreshEndpoint, new { refreshToken = input });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(input, body, StringComparison.Ordinal);
        Assert.DoesNotContain(presented.Token, body, StringComparison.Ordinal);
        using var verification = fixture.Factory.Services.CreateScope();
        var db = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var replay = scenario is "revoked" or "replaced";
        var storedSurvivor = await db.Set<RefreshToken>().AsNoTracking().SingleAsync(value => value.Id == survivor.Id);
        var storedSession = await db.Set<UserSession>().AsNoTracking().SingleAsync(value => value.Id == session.Id);
        var storedUser = await db.Set<User>().AsNoTracking().SingleAsync(value => value.Id == user.Id);
        Assert.True(storedSurvivor.IsRevoked == replay, $"Replay containment mismatch for {scenario}. Credential-free denial: {body}");
        Assert.Equal(!replay, storedSession.IsActive);
        Assert.Equal(user.TokenVersion + (replay ? 1 : 0), storedUser.TokenVersion);
        Assert.False((await db.Set<RefreshToken>().AsNoTracking().SingleAsync(value => value.Id == otherUser.Id)).IsRevoked);
        Assert.Equal(scenario == "unknown" ? 1 : 2, await db.Set<RefreshToken>().CountAsync(value => value.UserId == user.Id));
        if (replay)
        {
            Assert.Equal(SessionTerminationReason.SecurityViolation.ToString(), storedSession.TerminationReason);
            Assert.NotNull(storedSession.TerminatedAt);
        }
    }

    [Fact]
    public async Task ActiveUserQueryUsesMappedColumnsAndReturnsOnlyUnexpiredUnrevokedRows()
    {
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var newer = Token(userId, now.AddHours(1), now.AddMinutes(-1));
        var older = Token(userId, now.AddHours(1), now.AddMinutes(-2));
        await SeedAsync(newer, older, Token(userId, now.AddMinutes(-1), now),
            Token(userId, now.AddHours(1), now, revoked: true), Token(Guid.NewGuid(), now.AddHours(1), now));
        using var scope = fixture.Factory.Services.CreateScope();
        var repository = new RefreshTokenRepository(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        var result = await repository.GetActiveByUserIdAsync(userId);
        Assert.Equal(new[] { newer.Id, older.Id }, result.Select(value => value.Id));
        Assert.All(result, value => Assert.True(value.IsActive));
    }

    [Fact]
    public async Task UserWideRevocationPersistsMappedStateAndKeepsOtherUsersAndInactiveRows()
    {
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var active = Token(userId, now.AddHours(1), now);
        var expired = Token(userId, now.AddMinutes(-1), now);
        var alreadyRevoked = Token(userId, now.AddHours(1), now, revoked: true);
        var otherUser = Token(Guid.NewGuid(), now.AddHours(1), now);
        await SeedAsync(active, expired, alreadyRevoked, otherUser);
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var repository = new RefreshTokenRepository(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
            await repository.RevokeAllForUserAsync(userId, "127.0.0.1");
        }
        using var verification = fixture.Factory.Services.CreateScope();
        var context = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await context.Set<RefreshToken>().AsNoTracking().SingleAsync(value => value.Id == active.Id);
        Assert.True(stored.IsRevoked);
        Assert.NotNull(stored.RevokedAt);
        Assert.Equal("127.0.0.1", stored.RevokedByIp);
        Assert.Equal(stored.RevokedAt, stored.UpdatedAt);
        Assert.False((await context.Set<RefreshToken>().AsNoTracking().SingleAsync(value => value.Id == expired.Id)).IsRevoked);
        Assert.False((await context.Set<RefreshToken>().AsNoTracking().SingleAsync(value => value.Id == otherUser.Id)).IsRevoked);
        Assert.Equal(alreadyRevoked.RevokedAt, (await context.Set<RefreshToken>().AsNoTracking().SingleAsync(value => value.Id == alreadyRevoked.Id)).RevokedAt);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("revoked")]
    [InlineData("replaced")]
    [InlineData("wrong-hash")]
    public async Task AtomicRotationRejectsInactiveOrMismatchedClaims(string scenario)
    {
        var now = DateTime.UtcNow;
        var token = Token(Guid.NewGuid(), scenario == "expired" ? now.AddMinutes(-1) : now.AddHours(1), now,
            revoked: scenario == "revoked");
        if (scenario == "replaced") { token.ReplacedByToken = Hash(); }
        await SeedAsync(token);
        using var scope = fixture.Factory.Services.CreateScope();
        var repository = new RefreshTokenRepository(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        Assert.False(await repository.TryRevokeForRotationAsync(token.Id,
            scenario == "wrong-hash" ? Hash() : token.Token, Hash(), now, "127.0.0.1", CancellationToken.None));
    }

    [Fact]
    public async Task TwoIndependentContextsCanClaimExactlyOneRotation()
    {
        var now = DateTime.UtcNow;
        var token = Token(Guid.NewGuid(), now.AddHours(1), now);
        await SeedAsync(token);
        using var firstScope = fixture.Factory.Services.CreateScope();
        using var secondScope = fixture.Factory.Services.CreateScope();
        var first = new RefreshTokenRepository(firstScope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        var second = new RefreshTokenRepository(secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        var replacements = new[] { Hash(), Hash() };
        var result = await Task.WhenAll(
            first.TryRevokeForRotationAsync(token.Id, token.Token, replacements[0], now, "127.0.0.1", CancellationToken.None),
            second.TryRevokeForRotationAsync(token.Id, token.Token, replacements[1], now, "127.0.0.1", CancellationToken.None));
        Assert.Single(result, success => success);
        using var verification = fixture.Factory.Services.CreateScope();
        var stored = await verification.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<RefreshToken>()
            .AsNoTracking().SingleAsync(value => value.Id == token.Id);
        Assert.True(stored.IsRevoked);
        Assert.Equal(replacements[Array.FindIndex(result, success => success)], stored.ReplacedByToken);
    }

    private async Task SeedAsync(params RefreshToken[] tokens)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Set<RefreshToken>().AddRange(tokens);
        await context.SaveChangesAsync();
    }

    private static RefreshToken Token(Guid userId, DateTime expiresAt, DateTime createdAt, bool revoked = false) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, Token = Hash(), ExpiresAt = expiresAt,
        CreatedAt = DatabaseInstant(createdAt), UpdatedAt = DatabaseInstant(createdAt), CreatedByIp = "127.0.0.1", IsRevoked = revoked,
        RevokedAt = revoked ? DatabaseInstant(createdAt) : null, RevokedByIp = revoked ? "127.0.0.2" : null
    };

    // PostgreSQL timestamps retain microseconds; compare the exact persisted instant.
    private static DateTime DatabaseInstant(DateTime value) => new(value.Ticks - value.Ticks % 10, DateTimeKind.Utc);

    private static string Hash() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private sealed record TransactionProbeCommand : ICommand<object>;
    private sealed record OrdinaryResult(bool Success);
    private sealed record CompletedDenial(bool Success) : ICommitOnFailureOutcome;
}
