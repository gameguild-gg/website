using FluentAssertions;
using GameGuild.CQRS;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using GameGuild.Identity.Tenants;
using Moq;
using Xunit;

namespace GameGuild.Identity.Users.UnitTests.Repositories;

public class UserRepositoryTests
{
    [Fact]
    public async Task GeneratedUsername_UsesReadableBaseWhenAvailable()
    {
        await using var context = CreateContext();
        var repository = new UserRepository(context);
        var user = User.CreateOAuthUser("new@example.test", "MátHeus Martíns");
        await repository.AddAsync(user);
        await repository.SaveChangesAsync();
        (await repository.GetByIdAsync(user.Id))!.Username.Should().Be("matheus-martins");
        context.Model.FindEntityType(typeof(User))!.FindProperty("HasGeneratedUsername").Should().BeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GeneratedUsername_ReservesExistingCaseVariantsAndDeletedUsers(bool deleted)
    {
        await using var context = CreateContext();
        await SeedUsersAsync(context, CreateUser("legacy@example.test", "Legacy", "Alpha-User", deleted: deleted));
        var user = User.Create("new@example.test", "Alpha User");
        var repository = new UserRepository(context);
        await repository.AddAsync(user);
        await repository.SaveChangesAsync();
        user.Username.Should().Be($"alpha-user-{user.Id:N}");
        context.Users.Single(existing => existing.Email == "legacy@example.test").Username.Should().Be("Alpha-User");
    }

    [Fact]
    public async Task AddRange_ReservesUnsavedBatchHandles()
    {
        await using var context = CreateContext();
        var users = Enumerable.Range(0, 3).Select(index => User.Create($"batch{index}@example.test", "Same Name")).ToArray();
        var repository = new UserRepository(context);
        await repository.AddRangeAsync(users);
        await repository.SaveChangesAsync();
        users[0].Username.Should().Be("same-name");
        users[1].Username.Should().Be($"same-name-{users[1].Id:N}");
        users[2].Username.Should().Be($"same-name-{users[2].Id:N}");
        users.Select(user => user.Username).Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitUsernameCollision_ReturnsValidationWithoutRenaming(bool deleted)
    {
        await using var context = CreateContext();
        await SeedUsersAsync(context, CreateUser("legacy@example.test", "Legacy", "Alpha-User", deleted: deleted));
        var user = User.CreateWithPassword("new@example.test", "Display Name", "hash", "ALPHA USER");
        var repository = new UserRepository(context);
        var exception = await Assert.ThrowsAsync<RequestValidationException>(() => repository.AddAsync(user));
        exception.Errors.Should().ContainSingle().Which.PropertyName.Should().Be(nameof(User.Username));
        user.Username.Should().Be("alpha-user");
        context.Entry(user).State.Should().Be(EntityState.Detached);
    }

    [Fact]
    public async Task ObjectInitializedUserWithoutHandle_GetsGeneratedHandle()
    {
        await using var context = CreateContext();
        var user = new User { Email = "new@example.test", Name = "New User" };
        await new UserRepository(context).AddAsync(user);
        user.Username.Should().Be("new-user");
    }

    [Fact]
    public async Task UpdateExistingDisplayName_PreservesLegacyHandleAndNull()
    {
        await using var context = CreateContext();
        var legacy = CreateUser("legacy@example.test", "Legacy", "Legacy.Mixed_Handle");
        var withoutHandle = CreateUser("without@example.test", "Without");
        await SeedUsersAsync(context, legacy, withoutHandle);
        var repository = new UserRepository(context);
        legacy.UpdateName("Updated Legacy");
        withoutHandle.UpdateName("Updated Without");
        await repository.UpdateRangeAsync([legacy, withoutHandle]);
        await repository.SaveChangesAsync();
        legacy.Username.Should().Be("Legacy.Mixed_Handle");
        withoutHandle.Username.Should().BeNull();
    }

    [Fact]
    public async Task GeneratedCollision_BoundsStoredHandleLength()
    {
        await using var context = CreateContext();
        var user = User.Create("new@example.test", new string('a', 300));
        await SeedUsersAsync(context, CreateUser("legacy@example.test", "Legacy", new string('a', 256)));
        await new UserRepository(context).AddAsync(user);
        user.Username.Should().HaveLength(256).And.EndWith($"-{user.Id:N}");
    }

    [Fact]
    public async Task GeneratedCollision_WhenAllBoundedCandidatesAreReserved_Rejects()
    {
        await using var context = CreateContext();
        var user = User.Create("new@example.test", "Same Name");
        var reserved = new List<User> { CreateUser("base@example.test", "Legacy", "same-name") };
        reserved.AddRange(Enumerable.Range(1, 8).Select(attempt => CreateUser($"suffix{attempt}@example.test", "Legacy",
            $"same-name-{user.Id:N}" + (attempt == 1 ? string.Empty : $"-{attempt}"))));
        await SeedUsersAsync(context, reserved.ToArray());
        await Assert.ThrowsAsync<RequestValidationException>(() => new UserRepository(context).AddAsync(user));
        context.Entry(user).State.Should().Be(EntityState.Detached);
    }

    [Fact]
    public async Task AddCanceledRequest_DoesNotTrackUser()
    {
        await using var context = CreateContext();
        var user = User.Create("new@example.test", "New User");
        await Assert.ThrowsAsync<OperationCanceledException>(() => new UserRepository(context).AddAsync(user, new CancellationToken(true)));
        context.Entry(user).State.Should().Be(EntityState.Detached);
    }

    [Fact]
    public async Task GetByIdEmailAllAndExists_ShouldExcludeDeletedUsers()
    {
        await using var context = CreateContext();
        var repository = new UserRepository(context);
        var activeUser = CreateUser("active@example.com", "Active User", username: "active.user", isActive: true);
        var deletedUser = CreateUser("deleted@example.com", "Deleted User", username: "deleted.user", deleted: true);

        await SeedUsersAsync(context, activeUser, deletedUser);

        (await repository.GetByIdAsync(activeUser.Id))!.Email.Should().Be("active@example.com");
        (await repository.GetByIdAsync(deletedUser.Id)).Should().BeNull();
        (await repository.GetByEmailAsync("active@example.com"))!.Name.Should().Be("Active User");
        (await repository.GetByEmailAsync("deleted@example.com")).Should().BeNull();
        (await repository.GetAllAsync()).Should().ContainSingle().Which.Id.Should().Be(activeUser.Id);
        (await repository.ExistsAsync(activeUser.Id)).Should().BeTrue();
        (await repository.ExistsAsync(deletedUser.Id)).Should().BeFalse();
        (await repository.ExistsByEmailAsync("active@example.com")).Should().BeTrue();
        (await repository.ExistsByEmailAsync("deleted@example.com")).Should().BeFalse();
    }

    [Fact]
    public async Task SearchAndGetUsersPaged_ShouldFilterPaginateAndExcludeDeletedUsers()
    {
        await using var context = CreateContext();
        var repository = new UserRepository(context);
        var alphaOne = CreateUser("alpha.one@example.com", "Alpha One", isActive: true);
        var alphaTwo = CreateUser("alpha.two@example.com", "Alpha Two", isActive: true);
        var inactive = CreateUser("inactive@example.com", "Inactive User", isActive: false);
        var deleted = CreateUser("alpha.deleted@example.com", "Alpha Deleted", deleted: true);

        await SeedUsersAsync(context, alphaOne, alphaTwo, inactive, deleted);

        var (searchResults, searchTotalCount) = await repository.SearchAsync("Alpha", 1, 10);
        var (pagedActiveUsers, activeTotalCount) = await repository.GetUsersPagedAsync(true, 1, 1);
        var (pagedInactiveUsers, inactiveTotalCount) = await repository.GetUsersPagedAsync(false, 1, 10);

        searchTotalCount.Should().Be(2);
        searchResults.Should().HaveCount(2);
        activeTotalCount.Should().Be(2);
        pagedActiveUsers.Should().ContainSingle();
        inactiveTotalCount.Should().Be(1);
        pagedInactiveUsers.Should().ContainSingle().Which.Id.Should().Be(inactive.Id);
    }

    [Fact]
    public async Task AddUpdateDeleteAndQueryable_ShouldPersistSoftDeleteFlow()
    {
        await using var context = CreateContext();
        var repository = new UserRepository(context);
        var user = User.Create("new@example.com", "New User", "+15550000");

        await repository.AddAsync(user);
        await repository.SaveChangesAsync();

        var fetched = await repository.GetByEmailAsync("new@example.com");
        fetched.Should().NotBeNull();

        user.UpdateInfo("Updated User", "+15550123");
        await repository.UpdateAsync(user);
        await repository.SaveChangesAsync();

        repository.GetQueryable().Should().Contain(singleUser => singleUser.Id == user.Id && singleUser.Name == "Updated User");

        user.Version = 1;
        await repository.DeleteAsync(user);
        await repository.SaveChangesAsync();

        (await repository.GetByIdAsync(user.Id)).Should().BeNull();
    }

    [Fact]
    public async Task BulkOperations_ShouldHandleIdsEmailsExistenceAndDeleteRange()
    {
        await using var context = CreateContext();
        var repository = new UserRepository(context);
        var first = CreateUser("first@example.com", "First User", isActive: true);
        var second = CreateUser("second@example.com", "Second User", isActive: false);

        await repository.AddRangeAsync(new[] { first, second });
        await repository.SaveChangesAsync();

        var byIds = (await repository.GetByIdsAsync(new[] { first.Id, second.Id, Guid.NewGuid() })).ToList();
        var byEmails = (await repository.GetByEmailsAsync(new[] { "first@example.com", "second@example.com", "missing@example.com" })).ToList();
        var activeUsers = (await repository.GetActiveUsersAsync()).ToList();
        var inactiveUsers = (await repository.GetInactiveUsersAsync()).ToList();
        var existence = await repository.CheckEmailsExistAsync(new[] { "first@example.com", "missing@example.com" });

        byIds.Should().HaveCount(2);
        byEmails.Should().HaveCount(2);
        activeUsers.Should().ContainSingle().Which.Id.Should().Be(first.Id);
        inactiveUsers.Should().ContainSingle().Which.Id.Should().Be(second.Id);
        existence["first@example.com"].Should().BeTrue();
        existence["missing@example.com"].Should().BeFalse();

        first.UpdateName("First Updated");
        second.UpdateName("Second Updated");
        await repository.UpdateRangeAsync(new[] { first, second });
        await repository.SaveChangesAsync();

        (await repository.GetByIdAsync(first.Id))!.Name.Should().Be("First Updated");

        first.Version = 1;
        second.Version = 1;
        await repository.DeleteRangeAsync(new[] { first, second });
        await repository.SaveChangesAsync();

        (await repository.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task AuthMethods_ShouldHandleUsernameLookupPasswordUpdatesLoginAndTokenVersion()
    {
        await using var context = CreateContext();
        var repository = new UserRepository(context);
        var user = CreateUser("auth@example.com", "Auth User", username: "Auth.User", isActive: true);
        var originalTokenVersion = user.TokenVersion;

        await SeedUsersAsync(context, user);

        (await repository.GetByUsernameAsync("auth.user"))!.Id.Should().Be(user.Id);
        (await repository.GetByUsernameAsync(" ")).Should().BeNull();
        (await repository.ExistsByUsernameAsync("AUTH.USER")).Should().BeTrue();
        (await repository.ExistsByUsernameAsync(" ")).Should().BeFalse();

        (await repository.UpdatePasswordHashAsync(user.Id, "hashed-password", user.PasswordHash)).Should().BeTrue();
        await repository.RecordLoginAsync(user.Id);

        var updatedUser = await repository.GetByIdAsync(user.Id);
        updatedUser.Should().NotBeNull();
        updatedUser!.PasswordHash.Should().Be("hashed-password");
        updatedUser.TokenVersion.Should().Be(originalTokenVersion + 1);
        updatedUser.LastLoginAt.Should().NotBeNull();
        updatedUser.LastSeenAt.Should().NotBeNull();

        (await repository.GetTokenVersionAsync(user.Id)).Should().Be(originalTokenVersion + 1);
    }

    [Fact]
    public async Task UpdatePasswordHash_WhenAnotherChangeWins_ReturnsFalseAndPreservesWinningPassword()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using var staleContext = CreateContext(databaseName);
        var user = CreateUser("concurrent-password@example.com", "Concurrent Password User");
        user.PasswordHash = "original-hash";
        await SeedUsersAsync(staleContext, user);

        var staleRepository = new UserRepository(staleContext);
        (await staleRepository.GetByIdAsync(user.Id))!.PasswordHash.Should().Be("original-hash");

        await using var winningContext = CreateContext(databaseName);
        var winningRepository = new UserRepository(winningContext);
        (await winningRepository.UpdatePasswordHashAsync(user.Id, "winning-hash", "original-hash")).Should().BeTrue();

        (await staleRepository.UpdatePasswordHashAsync(user.Id, "stale-hash", "original-hash")).Should().BeFalse();

        await using var verificationContext = CreateContext(databaseName);
        (await new UserRepository(verificationContext).GetByIdAsync(user.Id))!.PasswordHash.Should().Be("winning-hash");
    }

    [Fact]
    public async Task PurgeOperations_ShouldRemoveUsersFromStore()
    {
        await using var context = CreateContext();
        var repository = new UserRepository(context);
        var first = CreateUser("purge.one@example.com", "Purge One");
        var second = CreateUser("purge.two@example.com", "Purge Two");

        await SeedUsersAsync(context, first, second);

        await repository.PurgeAsync(first);
        await repository.PurgeRangeAsync(new[] { second });
        await repository.SaveChangesAsync();

        repository.GetQueryable().Should().BeEmpty();
    }

    private static UsersRepositoryDbContext CreateContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<UsersRepositoryDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new UsersRepositoryDbContext(options);
    }

    private static async Task SeedUsersAsync(UsersRepositoryDbContext context, params User[] users)
    {
        await context.Users.AddRangeAsync(users);
        await context.SaveChangesAsync();
    }

    private static User CreateUser(string email, string name, string? username = null, bool isActive = true, bool deleted = false)
    {
        var user = User.Create(email, name);
        user.Username = username;
        user.IsActive = isActive;
        user.Version = 1;
        user.CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        user.UpdatedAt = new DateTime(2024, 1, 1, 1, 0, 0, DateTimeKind.Utc);

        if (deleted)
        {
            user.DeletedAt = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc);
            user.IsActive = false;
        }

        return user;
    }

    private sealed class UsersRepositoryDbContext(DbContextOptions<UsersRepositoryDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
        public DbSet<UserMetadata> UserMetadataSet => Set<UserMetadata>();
        public DbSet<UserPreferences> UserPreferencesSet => Set<UserPreferences>();
        public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
        public DbSet<TenantMember> TenantMembers => Set<TenantMember>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            new UsersModelConfiguration().Configure(modelBuilder);
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Mock.Of<IDbContextTransaction>());
        }
    }
}
