using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Repositories;

/// <summary>
///     Persistence tests for the API-key credential repository (#266).
/// </summary>
public class ApiKeyRepositoryTests
{
    [Fact]
    public async Task GetByKeyHashAsync_ReturnsMatchingKey_AndNullOnMiss()
    {
        await using var context = CreateContext();
        var repository = new ApiKeyRepository(context);
        var (key, plaintext) = ApiKey.Create(Guid.NewGuid(), Guid.NewGuid(), "integration", ["reports:read"]);
        await repository.AddAsync(key);

        var hit = await repository.GetByKeyHashAsync(key.KeyHash);
        var miss = await repository.GetByKeyHashAsync(ComputeHash("gg_live_unknown"));

        hit.Should().NotBeNull();
        hit!.Id.Should().Be(key.Id);
        hit.ValidateKey(plaintext).Should().BeTrue("the row stores the hash of the issued plaintext");
        miss.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdForUserAsync_ScopesLookupToTheOwningUser()
    {
        await using var context = CreateContext();
        var repository = new ApiKeyRepository(context);
        var userId = Guid.NewGuid();
        var (key, _) = ApiKey.Create(userId, Guid.NewGuid(), "integration", ["read"]);
        await repository.AddAsync(key);

        var hit = await repository.GetByIdForUserAsync(key.Id, userId);
        var wrongUser = await repository.GetByIdForUserAsync(key.Id, Guid.NewGuid());
        var wrongId = await repository.GetByIdForUserAsync(Guid.NewGuid(), userId);

        hit.Should().NotBeNull();
        hit!.Id.Should().Be(key.Id);
        wrongUser.Should().BeNull("a key owned by another user must not be addressable");
        wrongId.Should().BeNull();
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsAllKeysNewestFirst_IncludingRevoked()
    {
        await using var context = CreateContext();
        var repository = new ApiKeyRepository(context);
        var userId = Guid.NewGuid();
        var (oldest, _) = ApiKey.Create(userId, Guid.NewGuid(), "oldest", ["read"]);
        oldest.CreatedAt = SystemClock.UtcNow.AddHours(-2);
        var (newest, _) = ApiKey.Create(userId, Guid.NewGuid(), "newest", ["read"]);
        newest.CreatedAt = SystemClock.UtcNow;
        var (revoked, _) = ApiKey.Create(userId, Guid.NewGuid(), "revoked", ["read"]);
        revoked.CreatedAt = SystemClock.UtcNow.AddHours(-1);
        revoked.Revoke("leaked");
        var (otherUserKey, _) = ApiKey.Create(Guid.NewGuid(), Guid.NewGuid(), "other", ["read"]);
        await repository.AddAsync(oldest);
        await repository.AddAsync(newest);
        await repository.AddAsync(revoked);
        await repository.AddAsync(otherUserKey);

        var keys = await repository.GetByUserIdAsync(userId);

        keys.Select(k => k.Id).Should().Equal([newest.Id, revoked.Id, oldest.Id],
            "all keys of the user are listed newest-first, revoked keys included");
    }

    [Fact]
    public async Task GetActiveByUserIdAsync_ReturnsOnlyKeysThatAreStillActive()
    {
        await using var context = CreateContext();
        var repository = new ApiKeyRepository(context);
        var userId = Guid.NewGuid();
        var (active, _) = ApiKey.Create(userId, Guid.NewGuid(), "active", ["read"]);
        var (revoked, _) = ApiKey.Create(userId, Guid.NewGuid(), "revoked", ["read"]);
        revoked.Revoke("leaked");
        var (otherUserKey, _) = ApiKey.Create(Guid.NewGuid(), Guid.NewGuid(), "other", ["read"]);
        await repository.AddAsync(active);
        await repository.AddAsync(revoked);
        await repository.AddAsync(otherUserKey);

        var keys = await repository.GetActiveByUserIdAsync(userId);

        keys.Should().ContainSingle().Which.Id.Should().Be(active.Id);
    }

    [Fact]
    public async Task AddAsync_PersistsTheKey_AndReturnsIt()
    {
        await using var context = CreateContext();
        var repository = new ApiKeyRepository(context);
        var (key, plaintext) = ApiKey.Create(Guid.NewGuid(), Guid.NewGuid(), "integration", ["read"]);

        var added = await repository.AddAsync(key);

        added.Should().BeSameAs(key);
        var reloaded = await repository.GetByKeyHashAsync(key.KeyHash);
        reloaded.Should().NotBeNull();
        reloaded!.ValidateKey(plaintext).Should().BeTrue();
    }

    [Fact]
    public async Task RevokeAsync_RevokesTheOwnedKey_AndPersistsTheTransition()
    {
        await using var context = CreateContext();
        var repository = new ApiKeyRepository(context);
        var userId = Guid.NewGuid();
        var (key, _) = ApiKey.Create(userId, Guid.NewGuid(), "integration", ["read"]);
        await repository.AddAsync(key);

        var revoked = await repository.RevokeAsync(key.Id, userId, "compromised");

        revoked.Should().NotBeNull();
        revoked!.RevokedAt.Should().NotBeNull();
        revoked.RevocationReason.Should().Be("compromised");
        revoked.IsActive.Should().BeFalse();
        (await repository.GetActiveByUserIdAsync(userId)).Should().BeEmpty("a revoked key is no longer active");
    }

    [Fact]
    public async Task RevokeAsync_ReturnsNull_WhenKeyIsMissingOrOwnedByAnotherUser()
    {
        await using var context = CreateContext();
        var repository = new ApiKeyRepository(context);
        var userId = Guid.NewGuid();
        var (key, _) = ApiKey.Create(userId, Guid.NewGuid(), "integration", ["read"]);
        await repository.AddAsync(key);

        var missing = await repository.RevokeAsync(Guid.NewGuid(), userId, "reason");
        var wrongOwner = await repository.RevokeAsync(key.Id, Guid.NewGuid(), "reason");

        missing.Should().BeNull();
        wrongOwner.Should().BeNull();
        (await repository.GetByIdForUserAsync(key.Id, userId)).Should().NotBeNull("a miss must not revoke anything");
    }

    [Fact]
    public async Task RecordUsageAsync_UpdatesLastUsedAtAndUsageCount()
    {
        await using var context = CreateContext();
        var repository = new ApiKeyRepository(context);
        var (key, _) = ApiKey.Create(Guid.NewGuid(), Guid.NewGuid(), "integration", ["read"]);
        await repository.AddAsync(key);
        var tracked = (await repository.GetByKeyHashAsync(key.KeyHash))!;
        tracked.UsageCount.Should().Be(0);
        tracked.LastUsedAt.Should().BeNull();

        await repository.RecordUsageAsync(tracked);

        var reloaded = await repository.GetByKeyHashAsync(key.KeyHash);
        reloaded!.UsageCount.Should().Be(1);
        reloaded.LastUsedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RotateAsync_WithGraceWindow_LinksKeysAndPersistsBothInOneSave()
    {
        await using var context = CreateContext();
        var repository = new ApiKeyRepository(context);
        var userId = Guid.NewGuid();
        var (oldKey, _) = ApiKey.Create(userId, Guid.NewGuid(), "integration", ["reports:read"]);
        await repository.AddAsync(oldKey);
        var trackedOld = (await repository.GetByIdForUserAsync(oldKey.Id, userId))!;
        var (newKey, newPlaintext) = ApiKey.Create(userId, trackedOld.TenantId!.Value, "integration", ["reports:read"]);
        var graceEndsAt = SystemClock.UtcNow.AddHours(1);

        await repository.RotateAsync(trackedOld, newKey, graceEndsAt);

        var reloadedOld = await repository.GetByIdForUserAsync(oldKey.Id, userId);
        reloadedOld!.ReplacesKeyId.Should().BeNull("the old key is replaced, not a replacement");
        reloadedOld.RotationGraceEndsAt.Should().Be(graceEndsAt);
        reloadedOld.IsValid().Should().BeTrue("the old key is honored within the grace window");
        reloadedOld.RevokedAt.Should().BeNull();

        var reloadedNew = await repository.GetByKeyHashAsync(ComputeHash(newPlaintext));
        reloadedNew.Should().NotBeNull("the replacement key is persisted by the rotation");
        reloadedNew!.ReplacesKeyId.Should().Be(oldKey.Id, "rotation linkage must be persisted");
        reloadedNew.IsValid().Should().BeTrue();
    }

    [Fact]
    public async Task RotateAsync_WithoutGrace_RevokesTheOldKeyImmediately()
    {
        await using var context = CreateContext();
        var repository = new ApiKeyRepository(context);
        var userId = Guid.NewGuid();
        var (oldKey, _) = ApiKey.Create(userId, Guid.NewGuid(), "integration", ["read"]);
        await repository.AddAsync(oldKey);
        var trackedOld = (await repository.GetByIdForUserAsync(oldKey.Id, userId))!;
        var (newKey, _) = ApiKey.Create(userId, Guid.NewGuid(), "integration", ["read"]);

        await repository.RotateAsync(trackedOld, newKey, graceEndsAt: null);

        var reloadedOld = await repository.GetByIdForUserAsync(oldKey.Id, userId);
        reloadedOld!.RevokedAt.Should().NotBeNull();
        reloadedOld.RevocationReason.Should().Contain("Rotated");
        reloadedOld.IsValid().Should().BeFalse();
        (await repository.GetByKeyHashAsync(newKey.KeyHash)).Should().NotBeNull();
    }

    [Fact]
    public async Task FinalizeRotationRevocationAsync_IsNoOpWhileGraceIsOpen_RecordsRevocationAfterExpiry()
    {
        await using var context = CreateContext();
        var repository = new ApiKeyRepository(context);
        var (key, _) = ApiKey.Create(Guid.NewGuid(), Guid.NewGuid(), "integration", ["read"]);
        await repository.AddAsync(key);
        var tracked = (await repository.GetByKeyHashAsync(key.KeyHash))!;

        tracked.BeginRotationGrace(SystemClock.UtcNow.AddHours(1));
        await repository.FinalizeRotationRevocationAsync(tracked);
        (await repository.GetByKeyHashAsync(key.KeyHash))!.RevokedAt.Should().BeNull("nothing to finalize while the window is open");

        tracked.BeginRotationGrace(SystemClock.UtcNow.AddMinutes(-1));
        await repository.FinalizeRotationRevocationAsync(tracked);

        var reloaded = await repository.GetByKeyHashAsync(key.KeyHash);
        reloaded!.RevokedAt.Should().NotBeNull("expired rotation must be finalized on use");
        reloaded.IsActive.Should().BeFalse();
    }

    /// <summary>
    ///     The in-memory provider builds the EF model but does not enforce unique constraints
    ///     at runtime; asserting the index configuration is the schema-level guard (the
    ///     relational database enforces uniqueness, covered by the migration).
    /// </summary>
    [Fact]
    public void Configuration_HasUniqueIndexOnKeyHash()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(ApiKey));
        entityType.Should().NotBeNull();

        var keyHashIndex = entityType!.GetIndexes().SingleOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(ApiKey.KeyHash) }));
        keyHashIndex.Should().NotBeNull("expected unique index on KeyHash");
        keyHashIndex!.IsUnique.Should().BeTrue();
    }

    private static string ComputeHash(string plaintext)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static TestApiKeyDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApiKeyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new TestApiKeyDbContext(options);
    }

    private sealed class TestApiKeyDbContext(DbContextOptions<TestApiKeyDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new ApiKeyConfiguration());
            base.OnModelCreating(modelBuilder);
        }
    }
}
