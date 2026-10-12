using BenchmarkDotNet.Attributes;
using GameGuild.Configuration.PresentationLayer.Authorization;
using GameGuild.Identity.Authorization.Caching;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace GameGuild.Identity.Authorization.PerformanceTests;

/// <summary>
/// Compares the current EF Core ACL repository with a warm per-process permission cache.
/// PostgreSQL contains 5,000 unrelated indexed ACL rows. Each concurrent operation owns its
/// own DbContext, matching the request-scoped context lifetime used by the API.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class PermissionCacheEfDatabaseLookupBenchmarks
{
    private static readonly Guid TenantId = Guid.Parse("23ca1a7a-a58b-4fc8-b1da-c2a94706a46e");
    private static readonly Guid UserId = Guid.Parse("3c2e06b1-83e3-4d4b-82cf-e77ddeee19ea");
    private const string ResourceType = "Document";
    private const string ResourceId = "permission-cache-ef-benchmark";

    private EconomyPostgreSqlTestDatabase _database = null!;
    private BenchmarkDbContextFactory _contextFactory = null!;
    private MemoryCache _memoryCache = null!;
    private IAccessControlListService _databaseService = null!;
    private CachedAccessControlListService _cachedService = null!;
    private AclSubject _subject = null!;

    [Params(1, 8, 32)]
    public int ConcurrentLookupCount { get; set; }

    [GlobalSetup]
    public async Task SetupAsync()
    {
        _database = await EconomyPostgreSqlTestDatabase.CreateAsync("authorization_ef_benchmarks").ConfigureAwait(false);

        // The shared Testcontainers helper disables pooling to isolate ordinary tests. A
        // performance fixture must model the API's pooled Npgsql connections; otherwise
        // repeated short-lived DbContexts churn TCP sockets and can exhaust local ports.
        var benchmarkConnectionString = new NpgsqlConnectionStringBuilder(_database.ConnectionString)
        {
            Pooling = true,
            MaxPoolSize = 128
        }.ConnectionString;
        var dbOptions = new DbContextOptionsBuilder<PermissionCacheBenchmarkDbContext>()
            .UseNpgsql(benchmarkConnectionString)
            .Options;
        _contextFactory = new BenchmarkDbContextFactory(dbOptions);
        await SeedDatabaseAsync().ConfigureAwait(false);

        var aclRepository = new EfAccessControlListEntryRepository(_contextFactory);
        var versionRepository = new EfTenantSecurityVersionRepository(_contextFactory);
        _databaseService = new DatabaseAccessControlListService(aclRepository, versionRepository);
        _subject = AclSubject.ForUser(UserId);

        _memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 5_000 });
        var cacheOptions = Options.Create(new AuthorizationCacheOptions
        {
            UseDistributedCache = false,
            PermissionTtlSeconds = 300,
            MaxL1CacheSize = 5_000
        });
        var metrics = new CacheMetricsService();
        var hybridCache = new HybridPermissionCache(
            _memoryCache,
            cacheOptions,
            metrics,
            NullLogger<HybridPermissionCache>.Instance);
        var versionStore = new DatabaseTenantSecurityVersionStore(versionRepository);
        _cachedService = new CachedAccessControlListService(
            _databaseService,
            _memoryCache,
            versionStore,
            new FixedUserSecurityVersionStore(),
            cacheOptions,
            hybridCache);

        var databaseResult = await _databaseService
            .EvaluateAccessAsync(_subject, TenantId, ResourceType, ResourceId)
            .ConfigureAwait(false);
        var cachedResult = await _cachedService
            .EvaluateAccessAsync(_subject, TenantId, ResourceType, ResourceId)
            .ConfigureAwait(false);
        if (databaseResult != AccessLevel.Write || cachedResult != databaseResult)
        {
            throw new InvalidOperationException(
                $"Database and cached EF ACL setup returned different results: {databaseResult} vs {cachedResult}.");
        }
    }

    [Benchmark(Baseline = true, Description = "PostgreSQL + EF ACL repository batch")]
    public Task<AccessLevel[]> EfDatabaseAclLookupAsync() => Task.WhenAll(
        Enumerable.Range(0, ConcurrentLookupCount)
            .Select(_ => _databaseService.EvaluateAccessAsync(_subject, TenantId, ResourceType, ResourceId)));

    [Benchmark(Description = "Warm L1 cache batch with EF security-version lookup")]
    public Task<AccessLevel[]> CachedEfAclLookupAsync() => Task.WhenAll(
        Enumerable.Range(0, ConcurrentLookupCount)
            .Select(_ => _cachedService.EvaluateAccessAsync(_subject, TenantId, ResourceType, ResourceId)));

    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        _memoryCache?.Dispose();
        if (_database is not null)
        {
            await _database.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task SeedDatabaseAsync()
    {
        await using var context = _contextFactory.CreateDbContext();
        await context.Database.EnsureCreatedAsync().ConfigureAwait(false);

        context.Set<TenantSecurityVersion>().AddRange(
            new TenantSecurityVersion { TenantId = TenantId, SecurityVersion = 1 },
            new TenantSecurityVersion { TenantId = Guid.Empty, SecurityVersion = 1 });

        var unrelatedEntries = Enumerable.Range(1, 5_000)
            .Select(index => AccessControlListEntry.ForUser(
                TenantId,
                Guid.NewGuid(),
                ResourceType,
                $"noise-{index}",
                AccessLevel.Read,
                Guid.Empty));
        context.Set<AccessControlListEntry>().AddRange(unrelatedEntries);
        context.Set<AccessControlListEntry>().Add(AccessControlListEntry.ForUser(
            TenantId,
            UserId,
            ResourceType,
            ResourceId,
            AccessLevel.Write,
            Guid.Empty));
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    private sealed class PermissionCacheBenchmarkDbContext(
        DbContextOptions<PermissionCacheBenchmarkDbContext> options) : DbContext(options)
    {
        public DbSet<AccessControlListEntry> AccessControlListEntries => Set<AccessControlListEntry>();

        public DbSet<TenantSecurityVersion> TenantSecurityVersions => Set<TenantSecurityVersion>();
    }

    private sealed class BenchmarkDbContextFactory(
        DbContextOptions<PermissionCacheBenchmarkDbContext> options) : IDbContextFactory<PermissionCacheBenchmarkDbContext>
    {
        public PermissionCacheBenchmarkDbContext CreateDbContext() => new(options);

        public Task<PermissionCacheBenchmarkDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }

    private sealed class ApplicationDbContextAdapter(PermissionCacheBenchmarkDbContext context) : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => context.Set<T>();

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            context.SaveChangesAsync(cancellationToken);

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            context.Database.BeginTransactionAsync(cancellationToken);
    }

    private sealed class EfAccessControlListEntryRepository(
        IDbContextFactory<PermissionCacheBenchmarkDbContext> contextFactory) : IAccessControlListEntryRepository
    {
        public Task<IReadOnlyList<AccessControlListEntry>> GetByResourceAndPrincipalsAsync(
            Guid tenantId,
            string resourceType,
            string resourceId,
            IEnumerable<(AclPrincipalType Type, Guid? Id)> principals,
            CancellationToken cancellationToken = default) =>
            WithRepositoryAsync(repository => repository.GetByResourceAndPrincipalsAsync(
                tenantId, resourceType, resourceId, principals, cancellationToken));

        public Task<AccessControlListEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            WithRepositoryAsync(repository => repository.GetByIdAsync(id, cancellationToken));

        public Task<AccessControlListEntry?> GetByPrincipalAndResourceAsync(
            Guid tenantId, AclPrincipalType principalType, Guid? principalId, string resourceType, string resourceId,
            CancellationToken cancellationToken = default) =>
            WithRepositoryAsync(repository => repository.GetByPrincipalAndResourceAsync(
                tenantId, principalType, principalId, resourceType, resourceId, cancellationToken));

        public Task<AccessControlListEntry?> GetByUserAndResourceAsync(
            Guid tenantId, Guid userId, string resourceType, string resourceId, CancellationToken cancellationToken = default) =>
            WithRepositoryAsync(repository => repository.GetByUserAndResourceAsync(
                tenantId, userId, resourceType, resourceId, cancellationToken));

        public Task<IReadOnlyList<AccessControlListEntry>> GetByUserAsync(
            Guid tenantId, Guid userId, CancellationToken cancellationToken = default) =>
            WithRepositoryAsync(repository => repository.GetByUserAsync(tenantId, userId, cancellationToken));

        public Task<IReadOnlyList<AccessControlListEntry>> GetByResourceAsync(
            Guid tenantId, string resourceType, string resourceId, CancellationToken cancellationToken = default) =>
            WithRepositoryAsync(repository => repository.GetByResourceAsync(
                tenantId, resourceType, resourceId, cancellationToken));

        public Task<IReadOnlyList<AccessControlListEntry>> GetByTenantAsync(
            Guid tenantId, CancellationToken cancellationToken = default) =>
            WithRepositoryAsync(repository => repository.GetByTenantAsync(tenantId, cancellationToken));

        public Task<IReadOnlyList<AccessControlListEntry>> GetExpiredEntriesAsync(
            DateTime cutoffDate, CancellationToken cancellationToken = default) =>
            WithRepositoryAsync(repository => repository.GetExpiredEntriesAsync(cutoffDate, cancellationToken));

        public Task AddAsync(AccessControlListEntry entry, CancellationToken cancellationToken = default) =>
            WithRepositoryAsync(repository => repository.AddAsync(entry, cancellationToken));

        public Task UpdateAsync(AccessControlListEntry entry, CancellationToken cancellationToken = default) =>
            WithRepositoryAsync(repository => repository.UpdateAsync(entry, cancellationToken));

        public Task DeleteAsync(AccessControlListEntry entry, CancellationToken cancellationToken = default) =>
            WithRepositoryAsync(repository => repository.DeleteAsync(entry, cancellationToken));

        public Task DeleteByResourceAsync(
            Guid tenantId, string resourceType, string resourceId, CancellationToken cancellationToken = default) =>
            WithRepositoryAsync(repository => repository.DeleteByResourceAsync(
                tenantId, resourceType, resourceId, cancellationToken));

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            WithRepositoryAsync(repository => repository.SaveChangesAsync(cancellationToken));

        private async Task<TResult> WithRepositoryAsync<TResult>(
            Func<IAccessControlListEntryRepository, Task<TResult>> action)
        {
            await using var context = contextFactory.CreateDbContext();
            var repository = new AccessControlListEntryRepository(new ApplicationDbContextAdapter(context));
            return await action(repository).ConfigureAwait(false);
        }

        private async Task WithRepositoryAsync(Func<IAccessControlListEntryRepository, Task> action)
        {
            await using var context = contextFactory.CreateDbContext();
            var repository = new AccessControlListEntryRepository(new ApplicationDbContextAdapter(context));
            await action(repository).ConfigureAwait(false);
        }
    }

    private sealed class EfTenantSecurityVersionRepository(
        IDbContextFactory<PermissionCacheBenchmarkDbContext> contextFactory) : ITenantSecurityVersionRepository
    {
        public Task<IReadOnlyDictionary<Guid, long>> GetVersionsAsync(IReadOnlyCollection<Guid> tenantIds) =>
            GetVersionsAsync(tenantIds, CancellationToken.None);

        public Task<IReadOnlyDictionary<Guid, long>> GetVersionsAsync(
            IReadOnlyCollection<Guid> tenantIds,
            CancellationToken cancellationToken) =>
            WithRepositoryAsync(repository => repository.GetVersionsAsync(tenantIds, cancellationToken));

        public Task<TenantSecurityVersion?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            WithRepositoryAsync(repository => repository.GetByTenantIdAsync(tenantId, cancellationToken));

        public Task<TenantSecurityVersion> GetOrCreateAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            WithRepositoryAsync(repository => repository.GetOrCreateAsync(tenantId, cancellationToken));

        public Task<long> IncrementVersionAsync(Guid tenantId, string? reason = null, CancellationToken cancellationToken = default) =>
            WithRepositoryAsync(repository => repository.IncrementVersionAsync(tenantId, reason, cancellationToken));

        public Task AddAsync(TenantSecurityVersion version, CancellationToken cancellationToken = default) =>
            WithRepositoryAsync(repository => repository.AddAsync(version, cancellationToken));

        public Task UpdateAsync(TenantSecurityVersion version, CancellationToken cancellationToken = default) =>
            WithRepositoryAsync(repository => repository.UpdateAsync(version, cancellationToken));

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            WithRepositoryAsync(repository => repository.SaveChangesAsync(cancellationToken));

        private async Task<TResult> WithRepositoryAsync<TResult>(
            Func<ITenantSecurityVersionRepository, Task<TResult>> action)
        {
            await using var context = contextFactory.CreateDbContext();
            var repository = new TenantSecurityVersionRepository(new ApplicationDbContextAdapter(context));
            return await action(repository).ConfigureAwait(false);
        }

        private async Task WithRepositoryAsync(Func<ITenantSecurityVersionRepository, Task> action)
        {
            await using var context = contextFactory.CreateDbContext();
            var repository = new TenantSecurityVersionRepository(new ApplicationDbContextAdapter(context));
            await action(repository).ConfigureAwait(false);
        }
    }

    private sealed class FixedUserSecurityVersionStore : IUserSecurityVersionStore
    {
        public Task<long> GetVersionAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            _ = userId;
            _ = cancellationToken;
            return Task.FromResult(1L);
        }

        public Task<long> IncrementVersionAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            _ = userId;
            _ = cancellationToken;
            return Task.FromResult(2L);
        }

        public Task IncrementVersionsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
        {
            _ = userIds;
            _ = cancellationToken;
            return Task.CompletedTask;
        }
    }
}
