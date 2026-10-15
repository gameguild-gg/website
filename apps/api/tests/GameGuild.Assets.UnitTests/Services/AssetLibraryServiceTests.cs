using GameGuild.Assets.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Assets.UnitTests.Services;

public sealed class AssetLibraryServiceTests : IDisposable
{
    private readonly TestContext _context;
    private readonly Mock<IAssetParentAuthorizationResolver> _parentResolver = new();
    private readonly Mock<IAssetAccessService> _accessService = new();
    private readonly Mock<IAssetFolderAuthorizationService> _folderAuthorizationService = new();
    private readonly AssetLibraryService _service;

    public AssetLibraryServiceTests()
    {
        var options = new DbContextOptionsBuilder<TestContext>()
            .UseInMemoryDatabase($"asset-library-{Guid.NewGuid():N}")
            .Options;
        _context = new TestContext(options);
        _parentResolver.Setup(resolver => resolver.Supports("Project")).Returns(true);
        _parentResolver
            .Setup(resolver => resolver.CanManageAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _service = new AssetLibraryService(
            _context,
            [_parentResolver.Object],
            _accessService.Object,
            _folderAuthorizationService.Object,
            Mock.Of<IAssetContentRepository>());
    }

    [Fact]
    public async Task CopyAsync_WhenFolderRestrictionDeniesSource_ReturnsNotFoundAndDoesNotCopy()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var source = CreateReference(actorId, tenantId);
        _context.Set<AssetReference>().Add(source);
        await _context.SaveChangesAsync();
        _accessService
            .Setup(service => service.ValidateAccessAsync(
                source.Id,
                actorId,
                tenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssetAccessValidation(false, AssetAccessDeniedReason.OwnershipRequired));

        var result = await _service.CopyAsync(source.Id, actorId, tenantId, null, null);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("NotFound");
        (await _context.Set<AssetReference>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task RestoreRevisionAsync_WhenFolderRestrictionDeniesReference_ReturnsNotFoundAndDoesNotRestore()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var reference = CreateReference(actorId, tenantId);
        var revision = reference.CreateInitialRevision(actorId);
        _context.Set<AssetReference>().Add(reference);
        await _context.SaveChangesAsync();
        _accessService
            .Setup(service => service.ValidateAccessAsync(
                reference.Id,
                actorId,
                tenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssetAccessValidation(false, AssetAccessDeniedReason.OwnershipRequired));

        var result = await _service.RestoreRevisionAsync(reference.Id, revision.Id, actorId, tenantId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("NotFound");
        reference.CurrentRevisionNumber.Should().Be(1);
    }

    [Fact]
    public async Task RestoreRevisionAsync_WhenAuthorized_AddsANewRevision()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var reference = CreateReference(actorId, tenantId);
        var revision = reference.CreateInitialRevision(actorId);
        _context.Set<AssetReference>().Add(reference);
        await _context.SaveChangesAsync();
        _accessService
            .Setup(service => service.ValidateAccessAsync(
                reference.Id,
                actorId,
                tenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssetAccessValidation(true, null));

        var result = await _service.RestoreRevisionAsync(reference.Id, revision.Id, actorId, tenantId);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RevisionNumber.Should().Be(2);
        (await _context.Set<AssetReferenceRevision>().CountAsync()).Should().Be(2);
    }

    private static AssetReference CreateReference(Guid actorId, Guid tenantId) => new(
        Guid.NewGuid(),
        actorId,
        "restricted-build.zip",
        AssetAccessPolicy.Inherited,
        "Project",
        Guid.NewGuid())
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId
    };

    public void Dispose() => _context.Dispose();

    private sealed class TestContext(DbContextOptions<TestContext> options) : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new AssetFolderConfiguration());
            modelBuilder.ApplyConfiguration(new AssetReferenceConfiguration());
            modelBuilder.ApplyConfiguration(new AssetReferenceRevisionConfiguration());
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
