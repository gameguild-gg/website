using System.Security.Cryptography;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Identity.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class RefreshTokenLineagePostgreSqlTests(ApiPostgreSqlFixture fixture)
{
    [Fact]
    public void RequiredLineageCapabilityResolvesTheSameConfiguredScopedTokenStore()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        Assert.Same(scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>(),
            scope.ServiceProvider.GetRequiredService<IRefreshTokenLineageRepository>());
        Assert.Same(scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>(),
            scope.ServiceProvider.GetRequiredService<IRefreshTokenCleanupRepository>());
        Assert.Same(scope.ServiceProvider.GetRequiredService<IUserSessionRepository>(),
            scope.ServiceProvider.GetRequiredService<IUserSessionCleanupRepository>());
    }

    [Theory]
    [InlineData("other-token-owner")]
    [InlineData("other-session-owner")]
    [InlineData("missing-session")]
    [InlineData("session-hash-mismatch")]
    [InlineData("inactive-session")]
    [InlineData("expired-session")]
    [InlineData("revoked-token")]
    [InlineData("expired-token")]
    [InlineData("different-existing-session")]
    public async Task SessionBindingRejectsInvalidOwnershipStateOrReplacement(string scenario)
    {
        var userId = Guid.NewGuid();
        var token = Token(userId);
        var session = Session(userId, token.Token);
        var existingSession = Session(userId, Hash());
        if (scenario == "other-token-owner") { token.UserId = Guid.NewGuid(); }
        if (scenario == "other-session-owner") { session.UserId = Guid.NewGuid(); }
        if (scenario == "session-hash-mismatch") { session.RefreshToken = Hash(); }
        if (scenario == "inactive-session") { session.IsActive = false; }
        if (scenario == "expired-session") { session.ExpiresAt = DateTime.UtcNow.AddMinutes(-1); }
        if (scenario == "revoked-token") { token.IsRevoked = true; }
        if (scenario == "expired-token") { token.ExpiresAt = DateTime.UtcNow.AddMinutes(-1); }
        if (scenario == "different-existing-session") { token.SessionId = existingSession.Id; }
        await SeedAsync([token], [session, existingSession]);
        using var scope = fixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRefreshTokenLineageRepository>();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repository.BindSessionAsync(
            userId, token.Token, scenario == "missing-session" ? Guid.NewGuid() : session.Id, CancellationToken.None));

        using var verification = fixture.Factory.Services.CreateScope();
        var stored = await verification.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Set<RefreshToken>().AsNoTracking().SingleAsync(value => value.Id == token.Id);
        Assert.Equal(token.SessionId, stored.SessionId);
        Assert.Null(stored.ParentTokenId);
    }

    [Fact]
    public async Task MissingTokenReportsNoBindingWithoutInventingMetadata()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRefreshTokenLineageRepository>();
        Assert.False(await repository.BindSessionAsync(Guid.NewGuid(), Hash(), Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task LegacyRootBindingIsIdempotentAndOwnedByThePersistedSession()
    {
        var userId = Guid.NewGuid();
        var token = Token(userId);
        var session = Session(userId, token.Token);
        await SeedAsync([token], [session]);
        using var scope = fixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRefreshTokenLineageRepository>();
        Assert.True(await repository.BindSessionAsync(userId, token.Token, session.Id, CancellationToken.None));
        Assert.True(await repository.BindSessionAsync(userId, token.Token, session.Id, CancellationToken.None));
        using var verification = fixture.Factory.Services.CreateScope();
        var stored = await verification.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Set<RefreshToken>().AsNoTracking().SingleAsync(value => value.Id == token.Id);
        Assert.Equal(session.Id, stored.SessionId);
        Assert.Null(stored.ParentTokenId);
    }

    [Theory]
    [InlineData("unclaimed")]
    [InlineData("wrong-replacement")]
    [InlineData("other-parent-owner")]
    [InlineData("other-child-owner")]
    [InlineData("other-session-owner")]
    [InlineData("revoked-child")]
    [InlineData("expired-child")]
    [InlineData("inactive-session")]
    [InlineData("session-hash-mismatch")]
    public async Task RotationMetadataRequiresTheWinningClaimAndOwnedActiveSuccessor(string scenario)
    {
        var userId = Guid.NewGuid();
        var parent = Token(userId);
        var child = Token(userId);
        var session = Session(userId, child.Token);
        parent.IsRevoked = scenario != "unclaimed";
        parent.ReplacedByToken = scenario == "wrong-replacement" ? Hash() : child.Token;
        if (scenario == "other-parent-owner") { parent.UserId = Guid.NewGuid(); }
        if (scenario == "other-child-owner") { child.UserId = Guid.NewGuid(); }
        if (scenario == "other-session-owner") { session.UserId = Guid.NewGuid(); }
        if (scenario == "revoked-child") { child.IsRevoked = true; }
        if (scenario == "expired-child") { child.ExpiresAt = DateTime.UtcNow.AddMinutes(-1); }
        if (scenario == "inactive-session") { session.IsActive = false; }
        if (scenario == "session-hash-mismatch") { session.RefreshToken = Hash(); }
        await SeedAsync([parent, child], [session]);
        using var scope = fixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRefreshTokenLineageRepository>();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repository.RecordRotationAsync(
            userId, parent.Id, child.Token, session.Id, CancellationToken.None));

        using var verification = fixture.Factory.Services.CreateScope();
        var db = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await db.Set<RefreshToken>().AsNoTracking().Where(value => value.Id == parent.Id || value.Id == child.Id).ToListAsync();
        Assert.All(stored, value => { Assert.Null(value.SessionId); Assert.Null(value.ParentTokenId); });
    }

    [Fact]
    public async Task AtomicClaimUsesFreshParentStateAndOnlyAddsMetadataToItsStaleTrackedCopy()
    {
        var userId = Guid.NewGuid();
        var parent = Token(userId);
        var child = Token(userId);
        var session = Session(userId, child.Token);
        await SeedAsync([parent, child], [session]);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repository = new RefreshTokenRepository(db);
        var trackedParent = await repository.GetByIdAsync(parent.Id);
        Assert.True(await repository.TryRevokeForRotationAsync(parent.Id, parent.Token, child.Token,
            DateTime.UtcNow, "192.0.2.1", CancellationToken.None));
        Assert.False(trackedParent!.IsRevoked);
        await repository.RecordRotationAsync(userId, parent.Id, child.Token, session.Id, CancellationToken.None);
        using var verification = fixture.Factory.Services.CreateScope();
        var context = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var storedParent = await context.Set<RefreshToken>().AsNoTracking().SingleAsync(value => value.Id == parent.Id);
        var storedChild = await context.Set<RefreshToken>().AsNoTracking().SingleAsync(value => value.Id == child.Id);
        Assert.True(storedParent.IsRevoked);
        Assert.Equal(child.Token, storedParent.ReplacedByToken);
        Assert.Equal("192.0.2.1", storedParent.RevokedByIp);
        Assert.Equal(session.Id, storedParent.SessionId);
        Assert.Equal(session.Id, storedChild.SessionId);
        Assert.Equal(parent.Id, storedChild.ParentTokenId);
        Assert.Null(storedParent.ParentTokenId);
    }

    [Fact]
    public async Task RollbackRestoresClaimAndBothLineageLinks()
    {
        var userId = Guid.NewGuid();
        var parent = Token(userId);
        var child = Token(userId);
        var session = Session(userId, child.Token);
        await SeedAsync([parent, child], [session]);
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var repository = new RefreshTokenRepository(db);
            Assert.True(await repository.TryRevokeForRotationAsync(parent.Id, parent.Token, child.Token,
                DateTime.UtcNow, "192.0.2.1", CancellationToken.None));
            await repository.RecordRotationAsync(userId, parent.Id, child.Token, session.Id, CancellationToken.None);
            await transaction.RollbackAsync();
        }
        using var verification = fixture.Factory.Services.CreateScope();
        var context = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var storedParent = await context.Set<RefreshToken>().AsNoTracking().SingleAsync(value => value.Id == parent.Id);
        var storedChild = await context.Set<RefreshToken>().AsNoTracking().SingleAsync(value => value.Id == child.Id);
        Assert.False(storedParent.IsRevoked);
        Assert.Null(storedParent.ReplacedByToken);
        Assert.Null(storedParent.SessionId);
        Assert.Null(storedChild.SessionId);
        Assert.Null(storedChild.ParentTokenId);
    }

    [Theory]
    [InlineData("child")]
    [InlineData("session")]
    public async Task RotationValidationUsesFreshStoredStateWhenTheTrackerRetainsActiveRows(string scenario)
    {
        var userId = Guid.NewGuid();
        var parent = Token(userId);
        var child = Token(userId);
        var session = Session(userId, child.Token);
        parent.IsRevoked = true;
        parent.ReplacedByToken = child.Token;
        await SeedAsync([parent, child], [session]);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var trackedChild = await db.Set<RefreshToken>().SingleAsync(value => value.Id == child.Id);
        var trackedSession = await db.Set<UserSession>().SingleAsync(value => value.Id == session.Id);
        if (scenario == "child")
        {
            await db.Set<RefreshToken>().Where(value => value.Id == child.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.IsRevoked, true));
        }
        else
        {
            await db.Set<UserSession>().Where(value => value.Id == session.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.IsActive, false));
        }
        Assert.False(trackedChild.IsRevoked);
        Assert.True(trackedSession.IsActive);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new RefreshTokenRepository(db)
            .RecordRotationAsync(userId, parent.Id, child.Token, session.Id, CancellationToken.None));

        using var verification = fixture.Factory.Services.CreateScope();
        var context = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var storedChild = await context.Set<RefreshToken>().AsNoTracking().SingleAsync(value => value.Id == child.Id);
        Assert.Null(storedChild.ParentTokenId);
        Assert.Null(storedChild.SessionId);
    }

    [Fact]
    public async Task RetentionKeepsTheCompleteActiveChainAndItsSessionThenDeletesLeavesBeforeParents()
    {
        var userId = Guid.NewGuid();
        var root = Token(userId);
        var child = Token(userId);
        var leaf = Token(userId);
        var session = Session(userId, leaf.Token);
        session.ExpiresAt = DateTime.UtcNow.AddDays(-40);
        child.ParentTokenId = root.Id;
        leaf.ParentTokenId = child.Id;
        foreach (var token in new[] { root, child, leaf }) { token.SessionId = session.Id; }
        root.IsRevoked = child.IsRevoked = true;
        root.RevokedAt = child.RevokedAt = DateTime.UtcNow.AddDays(-40);
        root.ExpiresAt = child.ExpiresAt = DateTime.UtcNow.AddDays(-40);
        await SeedAsync([root, child, leaf], [session]);
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await new RefreshTokenRepository(db).DeleteExpiredAndRevokedAsync(DateTime.UtcNow.AddDays(-30));
            await new UserSessionRepository(db).DeleteExpiredAsync(DateTime.UtcNow);
            Assert.Equal(3, await db.Set<RefreshToken>().CountAsync(value => value.UserId == userId));
            Assert.True(await db.Set<UserSession>().AnyAsync(value => value.Id == session.Id));
            var trackedLeaf = await db.Set<RefreshToken>().SingleAsync(value => value.Id == leaf.Id);
            trackedLeaf.ExpiresAt = DateTime.UtcNow.AddDays(-40);
            await db.SaveChangesAsync();
            await new RefreshTokenRepository(db).DeleteExpiredAndRevokedAsync(DateTime.UtcNow.AddDays(-30));
            await new UserSessionRepository(db).DeleteExpiredAsync(DateTime.UtcNow);
        }
        using var verification = fixture.Factory.Services.CreateScope();
        var context = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await context.Set<RefreshToken>().AnyAsync(value => value.UserId == userId));
        Assert.False(await context.Set<UserSession>().AnyAsync(value => value.Id == session.Id));
    }

    [Theory]
    [InlineData(7, -40, true)]
    [InlineData(-1, -40, true)]
    [InlineData(-40, -40, false)]
    [InlineData(-30, -40, true)]
    [InlineData(-40, -1, true)]
    [InlineData(-40, -30, true)]
    public async Task CleanupRetainsRevokedReplayMarkersUntilOriginalExpiryAndRetentionWindowEnd(
        int expiryDaysFromNow, int revocationDaysFromNow, bool retain)
    {
        // PostgreSQL stores microsecond precision; use the same precision for exact cutoff cases.
        var now = new DateTime(DateTime.UtcNow.Ticks / 10 * 10, DateTimeKind.Utc);
        var userId = Guid.NewGuid();
        var token = Token(userId);
        token.CreatedAt = now.AddDays(-60);
        token.IsRevoked = true;
        token.RevokedAt = now.AddDays(revocationDaysFromNow);
        token.ExpiresAt = now.AddDays(expiryDaysFromNow);
        var session = Session(userId, token.Token);
        session.IsActive = false;
        session.ExpiresAt = now.AddDays(-40);
        token.SessionId = session.Id;
        await SeedAsync([token], [session]);

        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await new RefreshTokenRepository(db).DeleteExpiredAndRevokedAsync(now.AddDays(-30));
            await new UserSessionRepository(db).DeleteExpiredAsync(now);
        }

        using var verification = fixture.Factory.Services.CreateScope();
        var context = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(retain, await context.Set<RefreshToken>().AsNoTracking().AnyAsync(value => value.Id == token.Id));
        Assert.Equal(retain, await context.Set<UserSession>().AsNoTracking().AnyAsync(value => value.Id == session.Id));
    }

    [Fact]
    public async Task DatabaseRejectsDanglingParentAndSessionLinks()
    {
        foreach (var missingParent in new[] { true, false })
        {
            using var scope = fixture.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var token = Token(Guid.NewGuid());
            if (missingParent) { token.ParentTokenId = Guid.NewGuid(); }
            else { token.SessionId = Guid.NewGuid(); }
            db.Set<RefreshToken>().Add(token);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }

    private async Task SeedAsync(RefreshToken[] tokens, UserSession[] sessions)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Set<UserSession>().AddRange(sessions);
        db.Set<RefreshToken>().AddRange(tokens);
        await db.SaveChangesAsync();
    }

    private static RefreshToken Token(Guid userId) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, Token = Hash(), CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(7), CreatedByIp = "192.0.2.1"
    };

    private static UserSession Session(Guid userId, string tokenHash) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, RefreshToken = tokenHash, CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow, LastUsedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddHours(1),
        IsActive = true, IpAddress = "192.0.2.1"
    };

    private static string Hash() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}
