using GameGuild.Assets.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Assets.UnitTests.Services;

/// <summary>
/// Reference-counting regression tests for AssetLibraryService copy/restore paths (T1: RED → GREEN).
/// </summary>
public sealed class AssetLibraryReferenceCountingTests : IDisposable
{
    private readonly AssetLibraryRefCountTestContext _context;
    private readonly Mock<IAssetParentAuthorizationResolver> _parentResolver = new();
    private readonly Mock<IAssetAccessService> _accessService = new();
    private readonly AssetLibraryService _service;

    public AssetLibraryReferenceCountingTests()
    {
        var options = new DbContextOptionsBuilder<AssetLibraryRefCountTestContext>()
            .UseInMemoryDatabase($"asset-library-refcount-{Guid.NewGuid():N}")
            .Options;
        _context = new AssetLibraryRefCountTestContext(options);
        _parentResolver.Setup(resolver => resolver.Supports("Project")).Returns(true);
        _parentResolver
            .Setup(resolver => resolver.CanManageAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _accessService
            .Setup(service => service.ValidateAccessAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssetAccessValidation(true, null));
        _service = new AssetLibraryService(
            _context,
            [_parentResolver.Object],
            _accessService.Object,
            Mock.Of<IAssetFolderAuthorizationService>(),
            new AssetContentRepositoryForCounts(_context));
    }

    [Fact]
    public async Task CopyAsync_IncrementsContentReferenceCount()
    {
        var (actorId, tenantId) = (Guid.NewGuid(), Guid.NewGuid());
        var (reference, content) = CreateSeededReference(actorId, tenantId, referenceCount: 1);

        var result = await _service.CopyAsync(reference.Id, actorId, tenantId, "copy", null);

        result.IsSuccess.Should().BeTrue();
        (await _context.Set<AssetReference>().CountAsync()).Should().Be(2);
        var reloaded = await _context.Set<AssetContent>().SingleAsync(candidate => candidate.Id == content.Id);
        reloaded.ReferenceCount.Should().Be(2, "CopyTo creates a new logical reference to the same content");
    }

    [Fact]
    public async Task RestoreRevisionAsync_DecrementsOldContentAndIncrementsRestoredContent()
    {
        var (actorId, tenantId) = (Guid.NewGuid(), Guid.NewGuid());
        var (reference, oldContent) = CreateSeededReference(actorId, tenantId, referenceCount: 1);
        var newContent = new AssetContent("bucket", "keys/new", new string('b', 64), "image/png", 10, null, null)
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReferenceCount = 2
        };
        _context.Set<AssetContent>().Add(newContent);
        await _context.SaveChangesAsync();

        reference.ReplaceContent(newContent.Id, actorId, "switch to new content");
        _context.Set<AssetReferenceRevision>().Add(reference.Revisions.Last());
        await _context.SaveChangesAsync();
        var revisionToRestore = reference.Revisions.Single(revision => revision.AssetContentId == oldContent.Id);

        var result = await _service.RestoreRevisionAsync(reference.Id, revisionToRestore.Id, actorId, tenantId);

        result.IsSuccess.Should().BeTrue();
        reference.AssetContentId.Should().Be(oldContent.Id);
        var oldRow = await _context.Set<AssetContent>().SingleAsync(candidate => candidate.Id == oldContent.Id);
        var newRow = await _context.Set<AssetContent>().SingleAsync(candidate => candidate.Id == newContent.Id);
        oldRow.ReferenceCount.Should().Be(2,
            "restoring the old revision re-binds the reference to the old content");
        oldRow.MarkedForDeletionAt.Should().BeNull();
        newRow.ReferenceCount.Should().Be(1,
            "the replaced content loses the reference that moved back to the restored revision");
    }

    [Fact]
    public async Task RestoreRevisionAsync_LastReferenceSwap_MarksAbandonedContentForDeletion()
    {
        var (actorId, tenantId) = (Guid.NewGuid(), Guid.NewGuid());
        var (reference, oldContent) = CreateSeededReference(actorId, tenantId, referenceCount: 1);
        var newContent = new AssetContent("bucket", "keys/new", new string('c', 64), "image/png", 10, null, null)
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReferenceCount = 1
        };
        _context.Set<AssetContent>().Add(newContent);
        await _context.SaveChangesAsync();

        reference.ReplaceContent(newContent.Id, actorId);
        _context.Set<AssetReferenceRevision>().Add(reference.Revisions.Last());
        await _context.SaveChangesAsync();
        var revisionToRestore = reference.Revisions.Single(revision => revision.AssetContentId == oldContent.Id);

        var result = await _service.RestoreRevisionAsync(reference.Id, revisionToRestore.Id, actorId, tenantId);

        result.IsSuccess.Should().BeTrue();
        var oldRow = await _context.Set<AssetContent>().SingleAsync(candidate => candidate.Id == oldContent.Id);
        var newRow = await _context.Set<AssetContent>().SingleAsync(candidate => candidate.Id == newContent.Id);
        oldRow.ReferenceCount.Should().Be(2);
        newRow.ReferenceCount.Should().Be(0);
        newRow.MarkedForDeletionAt.Should().NotBeNull(
            "a content row that loses its last reference must become GC-eligible");
    }

    private (AssetReference Reference, AssetContent Content) CreateSeededReference(Guid actorId, Guid tenantId, int referenceCount)
    {
        var content = new AssetContent("bucket", "keys/old", new string('a', 64), "image/png", 10, null, null)
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReferenceCount = referenceCount
        };
        var reference = new AssetReference(content.Id, actorId, "reference.zip", AssetAccessPolicy.Inherited, "Project", Guid.NewGuid())
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId
        };
        reference.CreateInitialRevision(actorId);
        _context.Set<AssetContent>().Add(content);
        _context.Set<AssetReference>().Add(reference);
        _context.SaveChanges();
        return (reference, content);
    }

    public void Dispose() => _context.Dispose();

    private sealed class AssetContentRepositoryForCounts(AssetLibraryRefCountTestContext context) : IAssetContentRepository
    {
        public Task<AssetContent?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            context.Set<AssetContent>().FirstOrDefaultAsync(content => content.Id == id, ct);

        public Task<AssetContent?> GetByContentHashAsync(string contentHash, CancellationToken ct = default) =>
            context.Set<AssetContent>().FirstOrDefaultAsync(content => content.ContentHash == contentHash, ct);

        public Task<AssetContent> AddAsync(AssetContent content, CancellationToken ct = default)
        {
            context.Set<AssetContent>().Add(content);
            return Task.FromResult(content);
        }

        public Task UpdateAsync(AssetContent content, CancellationToken ct = default)
        {
            context.Set<AssetContent>().Update(content);
            return Task.CompletedTask;
        }

        public async Task IncrementReferenceCountAsync(Guid id, CancellationToken ct = default)
        {
            var content = await context.Set<AssetContent>().SingleAsync(candidate => candidate.Id == id, ct);
            content.ReferenceCount++;
            content.MarkedForDeletionAt = null;
            await context.SaveChangesAsync(ct);
        }

        public async Task DecrementReferenceCountAsync(Guid id, CancellationToken ct = default)
        {
            var content = await context.Set<AssetContent>().SingleAsync(candidate => candidate.Id == id, ct);
            content.ReferenceCount--;
            if (content.ReferenceCount <= 0 && content.MarkedForDeletionAt == null)
            {
                content.MarkedForDeletionAt = DateTime.UtcNow;
            }

            await context.SaveChangesAsync(ct);
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<int> GetCurrentReferenceCountAsync(Guid id, CancellationToken ct = default) =>
            context.Set<AssetContent>().Where(content => content.Id == id).Select(content => content.ReferenceCount).FirstOrDefaultAsync(ct);

        public Task<IReadOnlyList<AssetContent>> GetPendingVirusScanAsync(int limit = 100, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<bool> TryBeginVirusScanAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<AssetContent>> GetPendingModerationAsync(int limit = 100, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<bool> TryBeginModerationAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<AssetContent>> GetPendingModerationQueueAsync(int limit = 100, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<AssetContent>> GetGarbageCollectionCandidatesAsync(TimeSpan gracePeriod, int limit = 100, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<List<AssetContent>> GetMarkedForDeletionAsync(DateTime cutoffDate, int limit = 100, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class AssetLibraryRefCountTestContext(DbContextOptions<AssetLibraryRefCountTestContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new AssetReferenceConfiguration());
            modelBuilder.ApplyConfiguration(new AssetReferenceRevisionConfiguration());
            modelBuilder.Entity<AssetContent>();
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
