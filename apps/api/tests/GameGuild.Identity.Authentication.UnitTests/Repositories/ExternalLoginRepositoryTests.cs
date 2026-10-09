using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Repositories;

public class ExternalLoginRepositoryTests
{
    [Fact]
    public async Task GetByProviderKeyAsync_ReturnsInsertedRow_AndNullOnMiss()
    {
        await using var context = CreateContext();
        var repository = new ExternalLoginRepository(context);
        var userId = Guid.NewGuid();

        var inserted = await repository.UpsertAsync(
            CreateExternalLogin(userId: userId, provider: "google", providerKey: "sub-123"));

        var hit = await repository.GetByProviderKeyAsync("google", "sub-123");
        var miss = await repository.GetByProviderKeyAsync("google", "missing");

        hit.Should().NotBeNull();
        hit!.Id.Should().Be(inserted.Id);
        hit.UserId.Should().Be(userId);
        hit.Provider.Should().Be("google");
        hit.ProviderKey.Should().Be("sub-123");
        miss.Should().BeNull();
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsAllRowsForUser_AcrossProviders()
    {
        await using var context = CreateContext();
        var repository = new ExternalLoginRepository(context);
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        await repository.UpsertAsync(CreateExternalLogin(userId, "google", "sub-1"));
        await repository.UpsertAsync(CreateExternalLogin(userId, "github", "gh-9"));
        await repository.UpsertAsync(CreateExternalLogin(otherUserId, "google", "sub-2"));

        var rows = await repository.GetByUserIdAsync(userId);

        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(r => r.UserId == userId);
    }

    [Fact]
    public async Task UpsertAsync_InsertsNew_ThenUpdatesSameProviderKeyWithoutDuplicating()
    {
        await using var context = CreateContext();
        var repository = new ExternalLoginRepository(context);
        var originalUserId = Guid.NewGuid();

        var inserted = await repository.UpsertAsync(
            CreateExternalLogin(userId: originalUserId, provider: "google", providerKey: "sub-1"));
        inserted.Id.Should().NotBe(Guid.Empty);
        inserted.CreatedAt.Should().BeOnOrAfter(DateTime.UtcNow.AddSeconds(-5));
        inserted.UpdatedAt.Should().BeOnOrAfter(DateTime.UtcNow.AddSeconds(-5));

        // Caller now passes a fresh DTO (untracked) with the SAME (Provider,ProviderKey)
        // but a different UserId — Upsert must update the existing row, not insert a duplicate.
        var reLinkedUserId = Guid.NewGuid();
        var updateDto = CreateExternalLogin(userId: reLinkedUserId, provider: "google", providerKey: "sub-1");
        var updated = await repository.UpsertAsync(updateDto);

        updated.Id.Should().Be(inserted.Id);
        updated.UserId.Should().Be(reLinkedUserId);

        var allForOriginal = await repository.GetByUserIdAsync(originalUserId);
        allForOriginal.Should().BeEmpty();

        var hit = await repository.GetByProviderKeyAsync("google", "sub-1");
        hit.Should().NotBeNull();
        hit!.Id.Should().Be(inserted.Id);
        hit.UserId.Should().Be(reLinkedUserId);
        hit.UpdatedAt.Should().BeOnOrAfter(inserted.CreatedAt);
    }

    /// <summary>
    ///     In-memory provider builds the EF model but does NOT enforce unique constraints at runtime
    ///     (sibling RefreshToken has a unique index on Token and likewise has no runtime duplicate-throw
    ///     test). Asserting the index is configured at the model level is the proper TDD guard for the
    ///     schema; runtime enforcement is the relational DB's job (covered by the migration).
    /// </summary>
    [Fact]
    public void Configuration_HasUniqueIndexOnProviderAndProviderKey_AndNonUniqueIndexOnUserId()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(ExternalLogin));
        entityType.Should().NotBeNull();

        var indexes = entityType!.GetIndexes().ToList();

        var providerKeyIndex = indexes.SingleOrDefault(i =>
            i.Properties.Select(p => p.Name)
                .SequenceEqual(new[] { nameof(ExternalLogin.Provider), nameof(ExternalLogin.ProviderKey) }));
        providerKeyIndex.Should().NotBeNull("expected unique index on (Provider, ProviderKey)");
        providerKeyIndex!.IsUnique.Should().BeTrue();

        var userIdIndex = indexes.SingleOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(ExternalLogin.UserId) }));
        userIdIndex.Should().NotBeNull("expected index on UserId");
        userIdIndex!.IsUnique.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_RemovesOnlyTheTargetRow_AndReturnsTrue()
    {
        await using var context = CreateContext();
        var repository = new ExternalLoginRepository(context);
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        await repository.UpsertAsync(CreateExternalLogin(userId, "google", "sub-1"));
        await repository.UpsertAsync(CreateExternalLogin(userId, "discord", "snow-1"));
        await repository.UpsertAsync(CreateExternalLogin(otherUserId, "google", "sub-2"));

        var removed = await repository.DeleteAsync("google", userId);

        removed.Should().BeTrue();
        (await repository.GetByProviderKeyAsync("google", "sub-1")).Should().BeNull();
        (await repository.GetByProviderKeyAsync("google", "sub-2")).Should().NotBeNull("row of another user must be untouched");
        var remaining = await repository.GetByUserIdAsync(userId);
        remaining.Should().ContainSingle().Which.Provider.Should().Be("discord");
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenNoRowForProviderAndUser()
    {
        await using var context = CreateContext();
        var repository = new ExternalLoginRepository(context);
        var userId = Guid.NewGuid();

        await repository.UpsertAsync(CreateExternalLogin(userId, "google", "sub-1"));

        var wrongProvider = await repository.DeleteAsync("discord", userId);
        var wrongUser = await repository.DeleteAsync("google", Guid.NewGuid());

        wrongProvider.Should().BeFalse();
        wrongUser.Should().BeFalse();
        (await repository.GetByProviderKeyAsync("google", "sub-1")).Should().NotBeNull("no row may be removed on a miss");
    }

    [Fact]
    public async Task AddAsync_InsertsRowWithTimestamps_WithoutTouchingExistingRows()
    {
        await using var context = CreateContext();
        var repository = new ExternalLoginRepository(context);
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        await repository.UpsertAsync(CreateExternalLogin(otherUserId, "google", "sub-other"));

        var added = await repository.AddAsync(new ExternalLogin { UserId = userId, Provider = "google", ProviderKey = "sub-1" });

        added.Id.Should().NotBe(Guid.Empty);
        added.CreatedAt.Should().BeOnOrAfter(DateTime.UtcNow.AddSeconds(-5));
        added.UpdatedAt.Should().BeOnOrAfter(DateTime.UtcNow.AddSeconds(-5));

        var hit = await repository.GetByProviderKeyAsync("google", "sub-1");
        hit.Should().NotBeNull();
        hit!.UserId.Should().Be(userId);

        // Insert-only contract: rows that DO exist for the provider are never read or updated.
        (await repository.GetByProviderKeyAsync("google", "sub-other")).Should().NotBeNull();
        (await repository.GetByUserIdAsync(otherUserId)).Should().HaveCount(1);
    }

    /// <summary>
    ///     AddAsync relies on the relational unique index on (Provider, ProviderKey) to reject a
    ///     duplicate insert with DbUpdateException — the in-memory provider does not enforce it
    ///     at runtime (same limitation documented on the index-configuration test above). The
    ///     conflict behavior is covered at the handler level with a mocked DbUpdateException.
    /// </summary>
    [Fact]
    public async Task AddAsync_DuplicateInsertIsRejectedByTheDatabase_NotByTheRepository()
    {
        await using var context = CreateContext();
        var repository = new ExternalLoginRepository(context);
        var userId = Guid.NewGuid();

        var first = await repository.AddAsync(new ExternalLogin { UserId = userId, Provider = "google", ProviderKey = "sub-1" });
        var second = await repository.AddAsync(new ExternalLogin { UserId = Guid.NewGuid(), Provider = "google", ProviderKey = "sub-1" });

        second.Id.Should().NotBe(first.Id, "InMemory does not enforce the unique index — duplicate rejection is the relational DB's job");
        (await repository.GetByProviderKeyAsync("google", "sub-1")).Should().NotBeNull();
    }

    // ── Scope-consent persistence (issue #250) ──────────────────────────

    [Fact]
    public async Task AddAsync_GrantedScopesRoundTrip_ThroughTheJsonColumn()
    {
        await using var context = CreateContext();
        var repository = new ExternalLoginRepository(context);
        var userId = Guid.NewGuid();
        var consentedAt = DateTime.UtcNow;

        var added = await repository.AddAsync(new ExternalLogin
        {
            UserId = userId,
            Provider = "discord",
            ProviderKey = "snow-1",
            GrantedScopes = ExternalLoginGrants.Serialize(new[] { "identify", "email" }),
            ConsentedAt = consentedAt,
            ConsentVersion = OAuthConsentVersions.Current
        });

        var hit = await repository.GetByProviderKeyAsync("discord", "snow-1");
        hit.Should().NotBeNull();
        ExternalLoginGrants.Deserialize(hit!.GrantedScopes).Should().Equal("identify", "email", "JSON list must round-trip in order");
        hit.ConsentedAt.Should().Be(consentedAt);
        hit.ConsentVersion.Should().Be(OAuthConsentVersions.Current);
        added.GrantedScopes.Should().Be(hit.GrantedScopes);
    }

    [Fact]
    public async Task UpdateGrantedScopesAsync_ReplacesList_WithoutTouchingConsentStamp()
    {
        await using var context = CreateContext();
        var repository = new ExternalLoginRepository(context);
        var userId = Guid.NewGuid();
        var consentedAt = DateTime.UtcNow.AddDays(-9);
        var inserted = await repository.AddAsync(new ExternalLogin
        {
            UserId = userId,
            Provider = "google",
            ProviderKey = "sub-1",
            GrantedScopes = ExternalLoginGrants.Serialize(new[] { "openid", "email", "profile" }),
            ConsentedAt = consentedAt,
            ConsentVersion = OAuthConsentVersions.Current
        });

        var updated = await repository.UpdateGrantedScopesAsync("google", userId, ["openid", "profile"]);

        updated.Should().NotBeNull();
        ExternalLoginGrants.Deserialize(updated!.GrantedScopes).Should().Equal("openid", "profile");
        updated.ConsentedAt.Should().Be(consentedAt, "revocation must keep the first-consent stamp");
        updated.ConsentVersion.Should().Be(OAuthConsentVersions.Current);
        updated.UpdatedAt.Should().BeOnOrAfter(inserted.CreatedAt);
        (await repository.GetByProviderKeyAsync("google", "sub-1"))!.GrantedScopes.Should().Be(updated.GrantedScopes);
    }

    [Fact]
    public async Task UpdateGrantedScopesAsync_NoRowForProviderAndUser_ReturnsNull()
    {
        await using var context = CreateContext();
        var repository = new ExternalLoginRepository(context);
        var userId = Guid.NewGuid();
        await repository.AddAsync(new ExternalLogin { UserId = userId, Provider = "google", ProviderKey = "sub-1" });

        var missedProvider = await repository.UpdateGrantedScopesAsync("discord", userId, ["identify"]);
        var missedUser = await repository.UpdateGrantedScopesAsync("google", Guid.NewGuid(), ["email"]);

        missedProvider.Should().BeNull();
        missedUser.Should().BeNull();
    }

    [Fact]
    public async Task RecordConsentAsync_StampsConsentAtAndCurrentVersion()
    {
        await using var context = CreateContext();
        var repository = new ExternalLoginRepository(context);
        var userId = Guid.NewGuid();
        await repository.AddAsync(new ExternalLogin { UserId = userId, Provider = "discord", ProviderKey = "snow-1" });

        var updated = await repository.RecordConsentAsync("discord", userId, ["identify", "email"]);

        updated.Should().NotBeNull();
        ExternalLoginGrants.Deserialize(updated!.GrantedScopes).Should().Equal("identify", "email");
        updated.ConsentedAt.Should().BeOnOrAfter(DateTime.UtcNow.AddSeconds(-5));
        updated.ConsentVersion.Should().Be(OAuthConsentVersions.Current);
    }

    [Fact]
    public void Configuration_MapsGrantColumns()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(ExternalLogin));

        entityType.Should().NotBeNull();

        var grantedScopes = entityType!.FindProperty(nameof(ExternalLogin.GrantedScopes));
        grantedScopes.Should().NotBeNull();
        grantedScopes!.GetMaxLength().Should().Be(1024);

        var consentedAt = entityType.FindProperty(nameof(ExternalLogin.ConsentedAt));
        consentedAt.Should().NotBeNull();
        consentedAt!.IsNullable.Should().BeTrue();

        var consentVersion = entityType.FindProperty(nameof(ExternalLogin.ConsentVersion));
        consentVersion.Should().NotBeNull();
        consentVersion!.IsNullable.Should().BeFalse();
    }

    private static TestExternalLoginDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestExternalLoginDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new TestExternalLoginDbContext(options);
    }

    private static ExternalLogin CreateExternalLogin(Guid userId, string provider, string providerKey) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Provider = provider,
        ProviderKey = providerKey,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private sealed class TestExternalLoginDbContext(DbContextOptions<TestExternalLoginDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new ExternalLoginConfiguration());
            base.OnModelCreating(modelBuilder);
        }
    }
}
