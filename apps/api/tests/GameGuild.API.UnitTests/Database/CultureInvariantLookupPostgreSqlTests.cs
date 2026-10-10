using System.Globalization;
using GameGuild.API.Database;
using GameGuild.Assets;
using GameGuild.Identity.Users;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GameGuild.API.UnitTests.Database;

public sealed class CultureInvariantLookupPostgreSqlTests(CultureInvariantLookupPostgreSqlFixture fixture)
    : IClassFixture<CultureInvariantLookupPostgreSqlFixture>
{
    [Theory]
    [InlineData("en-US", false, false)]
    [InlineData("en-US", false, true)]
    [InlineData("en-US", true, false)]
    [InlineData("en-US", true, true)]
    [InlineData("tr-TR", false, false)]
    [InlineData("tr-TR", false, true)]
    [InlineData("tr-TR", true, false)]
    [InlineData("tr-TR", true, true)]
    public async Task UsernameLookup_PreservesCaseInsensitiveMatchingAndDeletedFilter(
        string cultureName, bool legacyUppercase, bool existenceLookup)
    {
        await using var context = fixture.CreateContext();
        var marker = Guid.NewGuid().ToString("N");
        var handle = $"identity-{marker}";
        var user = User.Create($"active-{marker}@example.test", "Identity User");
        user.Username = legacyUppercase ? handle.ToUpperInvariant() : handle;
        var deleted = User.Create($"deleted-{marker}@example.test", "Deleted User");
        deleted.Username = $"inactive-{marker}";
        deleted.DeletedAt = DateTime.UtcNow;
        var withoutHandle = User.Create($"null-{marker}@example.test", "Without Handle");
        withoutHandle.Username = null;
        context.AddRange(user, deleted, withoutHandle);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        using var culture = new CultureScope(cultureName);
        var repository = new UserRepository(context);
        var input = handle.ToUpperInvariant();
        if (existenceLookup)
        {
            Assert.True(await repository.ExistsByUsernameAsync(input));
            Assert.False(await repository.ExistsByUsernameAsync(deleted.Username.ToUpperInvariant()));
            Assert.False(await repository.ExistsByUsernameAsync($"missing-{marker}"));
            Assert.False(await repository.ExistsByUsernameAsync(" "));
        }
        else
        {
            Assert.Equal(user.Id, (await repository.GetByUsernameAsync(input))?.Id);
            Assert.Null(await repository.GetByUsernameAsync(deleted.Username.ToUpperInvariant()));
            Assert.Null(await repository.GetByUsernameAsync($"missing-{marker}"));
            Assert.Null(await repository.GetByUsernameAsync(" "));
        }
        Assert.Equal(legacyUppercase ? handle.ToUpperInvariant() : handle,
            (await context.Set<User>().SingleAsync(value => value.Id == user.Id)).Username);
    }

    [Theory]
    [InlineData("en-US", false, false)]
    [InlineData("en-US", false, true)]
    [InlineData("en-US", true, false)]
    [InlineData("en-US", true, true)]
    [InlineData("tr-TR", false, false)]
    [InlineData("tr-TR", false, true)]
    [InlineData("tr-TR", true, false)]
    [InlineData("tr-TR", true, true)]
    public async Task AssetLookup_PreservesMatchingOrderingAndPerItemAuthorization(
        string cultureName, bool legacyUppercase, bool inspectFolders)
    {
        await using var context = fixture.CreateContext();
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var storedType = legacyUppercase ? "IDENTITY" : "identity";
        var first = AssetFolder.Create(tenantId, storedType, resourceId, null, "A");
        var last = AssetFolder.Create(tenantId, storedType, resourceId, null, "Z");
        var hidden = AssetFolder.Create(tenantId, storedType, resourceId, null, "Hidden");
        var unrelated = AssetFolder.Create(tenantId, storedType, Guid.NewGuid(), null, "Unrelated");
        var otherType = AssetFolder.Create(tenantId, "other", resourceId, null, "Other");
        var content = CreateContent();
        var firstAsset = CreateReference(content.Id, actorId, tenantId, storedType, resourceId, "A");
        var lastAsset = CreateReference(content.Id, actorId, tenantId, storedType, resourceId, "Z");
        var hiddenAsset = CreateReference(content.Id, actorId, tenantId, storedType, resourceId, "Hidden");
        var unrelatedAsset = CreateReference(content.Id, actorId, tenantId, storedType, Guid.NewGuid(), "Unrelated");
        var otherTypeAsset = CreateReference(content.Id, actorId, tenantId, "other", resourceId, "Other");
        var nullTypeAsset = CreateReference(content.Id, actorId, tenantId, null, resourceId, "Null type");
        context.AddRange(first, last, hidden, unrelated, otherType, content,
            firstAsset, lastAsset, hiddenAsset, unrelatedAsset, otherTypeAsset, nullTypeAsset);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        using var request = new CancellationTokenSource();
        var parent = CreateParentResolver(resourceId, actorId, tenantId, request.Token);
        var access = new Mock<IAssetAccessService>(MockBehavior.Strict);
        foreach (var reference in new[] { firstAsset, lastAsset, hiddenAsset })
        {
            access.Setup(service => service.ValidateAccessAsync(reference.Id, actorId, tenantId, request.Token))
                .ReturnsAsync(new AssetAccessValidation(reference.Id != hiddenAsset.Id,
                    reference.Id == hiddenAsset.Id ? AssetAccessDeniedReason.OwnershipRequired : null));
        }
        var folders = new Mock<IAssetFolderAuthorizationService>(MockBehavior.Strict);
        folders.Setup(service => service.CanReadFolderAsync(It.IsAny<AssetFolder>(), actorId, tenantId, request.Token))
            .ReturnsAsync((AssetFolder folder, Guid _, Guid? _, CancellationToken _) => folder.Id != hidden.Id);
        var service = new AssetLibraryService(context, [parent.Object], access.Object, folders.Object);

        using var culture = new CultureScope(cultureName);
        var result = await service.GetAsync("IDENTITY", resourceId, actorId, tenantId, request.Token);

        Assert.True(result.IsSuccess);
        if (inspectFolders)
        {
            Assert.Equal(new[] { first.Id, last.Id }, result.Value!.Folders.Select(folder => folder.Id));
        }
        else
        {
            Assert.Equal(new[] { firstAsset.Id, lastAsset.Id }, result.Value!.Assets.Select(reference => reference.Id));
        }
        parent.Verify(resolver => resolver.Supports("IDENTITY"), Times.Once);
        parent.Verify(resolver => resolver.CanReadAsync(resourceId, actorId, tenantId, request.Token), Times.Once);
        parent.VerifyNoOtherCalls();
        access.VerifyAll();
        foreach (var folder in new[] { first, last, hidden })
        {
            folders.Verify(service => service.CanReadFolderAsync(
                It.Is<AssetFolder>(value => value.Id == folder.Id), actorId, tenantId, request.Token), Times.Once);
        }
        folders.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("en-US", false)]
    [InlineData("en-US", true)]
    [InlineData("tr-TR", false)]
    [InlineData("tr-TR", true)]
    public async Task CreateFolder_PreservesCaseInsensitiveParentAndOriginalResourceType(string cultureName, bool legacyUppercase)
    {
        await using var context = fixture.CreateContext();
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var parentFolder = AssetFolder.Create(tenantId, legacyUppercase ? "IDENTITY" : "identity", resourceId, null, "Parent");
        context.Add(parentFolder);
        await context.SaveChangesAsync();
        using var request = new CancellationTokenSource();
        var parent = CreateParentResolver(resourceId, actorId, tenantId, request.Token);
        var service = CreateService(context, parent);

        using var culture = new CultureScope(cultureName);
        var result = await service.CreateFolderAsync("IDENTITY", resourceId, actorId, tenantId, "Child", parentFolder.Id, request.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(parentFolder.Id, result.Value!.ParentFolderId);
        Assert.Equal(tenantId, result.Value.TenantId);
        Assert.Equal(resourceId, result.Value.ParentResourceId);
        Assert.Equal("IDENTITY", result.Value.ParentResourceType);
        parent.Verify(resolver => resolver.Supports("IDENTITY"), Times.Once);
        parent.Verify(resolver => resolver.CanManageAsync(resourceId, actorId, tenantId, request.Token), Times.Once);
        parent.VerifyNoOtherCalls();
        context.ChangeTracker.Clear();
        Assert.Equal(parentFolder.Id, (await context.Set<AssetFolder>().SingleAsync(folder => folder.Id == result.Value.Id)).ParentFolderId);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("resource")]
    [InlineData("type")]
    public async Task CreateFolder_RejectsParentOutsideAuthorizedScope(string mismatch)
    {
        await using var context = fixture.CreateContext();
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var parentFolder = AssetFolder.Create(mismatch == "tenant" ? Guid.NewGuid() : tenantId,
            mismatch == "type" ? "other" : "identity", mismatch == "resource" ? Guid.NewGuid() : resourceId, null, "Parent");
        context.Add(parentFolder);
        await context.SaveChangesAsync();
        var parent = CreateParentResolver(resourceId, actorId, tenantId, CancellationToken.None);
        using var culture = new CultureScope("tr-TR");

        var result = await CreateService(context, parent).CreateFolderAsync("IDENTITY", resourceId, actorId, tenantId, "Child", parentFolder.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("InvalidParentFolder", result.Error);
        Assert.False(await context.Set<AssetFolder>().AnyAsync(folder => folder.ParentFolderId == parentFolder.Id));
    }

    [Fact]
    public async Task AssetLookup_AndCreateFolder_DenyUnknownParentWithoutQuerying()
    {
        await using var context = fixture.CreateContext();
        var parent = new Mock<IAssetParentAuthorizationResolver>(MockBehavior.Strict);
        parent.Setup(resolver => resolver.Supports("IDENTITY")).Returns(false);
        var service = CreateService(context, parent);
        using var culture = new CultureScope("tr-TR");
        var resourceId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        await context.Database.CloseConnectionAsync();

        Assert.Equal("NotFound", (await service.GetAsync("IDENTITY", resourceId, actorId, tenantId)).Error);
        Assert.Equal("Forbidden", (await service.CreateFolderAsync("IDENTITY", resourceId, actorId, tenantId, "Child", Guid.NewGuid())).Error);
        Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);
        parent.Verify(resolver => resolver.Supports("IDENTITY"), Times.Exactly(2));
        parent.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Lookups_PreserveCancellation()
    {
        await using var context = fixture.CreateContext();
        using var request = new CancellationTokenSource();
        await request.CancelAsync();
        var repository = new UserRepository(context);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.GetByUsernameAsync("IDENTITY", request.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.ExistsByUsernameAsync("IDENTITY", request.Token));
        var resourceId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var parent = CreateParentResolver(resourceId, actorId, tenantId, request.Token);
        var service = CreateService(context, parent);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetAsync("IDENTITY", resourceId, actorId, tenantId, request.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CreateFolderAsync("IDENTITY", resourceId, actorId, tenantId, "Child", Guid.NewGuid(), request.Token));
    }

    private static Mock<IAssetParentAuthorizationResolver> CreateParentResolver(Guid resourceId, Guid actorId, Guid tenantId, CancellationToken ct)
    {
        var parent = new Mock<IAssetParentAuthorizationResolver>(MockBehavior.Strict);
        parent.Setup(resolver => resolver.Supports("IDENTITY")).Returns(true);
        parent.Setup(resolver => resolver.CanReadAsync(resourceId, actorId, tenantId, ct)).ReturnsAsync(true);
        parent.Setup(resolver => resolver.CanManageAsync(resourceId, actorId, tenantId, ct)).ReturnsAsync(true);
        return parent;
    }

    private static AssetLibraryService CreateService(ApplicationDbContext context, Mock<IAssetParentAuthorizationResolver> parent) =>
        new(context, [parent.Object], Mock.Of<IAssetAccessService>(MockBehavior.Strict), Mock.Of<IAssetFolderAuthorizationService>(MockBehavior.Strict));

    private static AssetContent CreateContent() => new("lookup-tests", Guid.NewGuid().ToString("N"),
        Guid.NewGuid().ToString("N"), "application/octet-stream", 1, null, null) { Id = Guid.NewGuid() };

    private static AssetReference CreateReference(Guid contentId, Guid actorId, Guid tenantId, string? type, Guid resourceId, string name) =>
        new(contentId, actorId, name, AssetAccessPolicy.Inherited, type, resourceId) { Id = Guid.NewGuid(), TenantId = tenantId };

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentCulture;

        public CultureScope(string cultureName) => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);

        public void Dispose() => CultureInfo.CurrentCulture = _previous;
    }
}

public sealed class CultureInvariantLookupPostgreSqlFixture : IAsyncLifetime
{
    private EconomyPostgreSqlTestDatabase? _database;

    public async Task InitializeAsync()
    {
        _database = await EconomyPostgreSqlTestDatabase.CreateAsync("culture_invariant_lookup");
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
    }

    public ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseNpgsql(_database!.ConnectionString).Options);

    public async Task DisposeAsync()
    {
        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }
}
