using BenchmarkDotNet.Attributes;
using GameGuild.Configuration.PresentationLayer.Authorization;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.Caching;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using LegacyPermissionService = GameGuild.Identity.Authentication.PermissionService;

namespace GameGuild.Identity.Authorization.PerformanceTests;

#pragma warning disable CS0618 // This benchmark intentionally measures the legacy bulk API named in issue #309.
/// <summary>
/// Compares individual legacy permission reads with batched permission checks against PostgreSQL.
/// The table includes 5,000 unrelated grants so the measurements exercise indexed filtering,
/// request batching, version reads, and both cold and warm permission-cache paths.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class PermissionBulkCheckBenchmarks
{
    private const int UnrelatedGrantCount = 5_000;

    private EconomyPostgreSqlTestDatabase _database = null!;
    private DbContextOptions<PermissionBulkBenchmarkDbContext> _dbOptions = null!;
    private MemoryCache _coldMemoryCache = null!;
    private MemoryCache _warmMemoryCache = null!;
    private IHybridPermissionCache _coldHybridCache = null!;
    private IHybridPermissionCache _warmHybridCache = null!;
    private BulkPermissionCheckRequest[] _requests = [];
    private Guid _tenantId;

    [Params(64, 256)]
    public int RequestCount { get; set; }

    [GlobalSetup]
    public async Task SetupAsync()
    {
        _database = await EconomyPostgreSqlTestDatabase.CreateAsync("permission_bulk_benchmarks").ConfigureAwait(false);
        var benchmarkConnectionString = new NpgsqlConnectionStringBuilder(_database.ConnectionString)
        {
            Pooling = true,
            MaxPoolSize = 128
        }.ConnectionString;
        _dbOptions = new DbContextOptionsBuilder<PermissionBulkBenchmarkDbContext>()
            .UseNpgsql(benchmarkConnectionString)
            .Options;

        _tenantId = Guid.NewGuid();
        var seededUserIds = Enumerable.Range(0, Math.Max(RequestCount, 1))
            .Select(_ => Guid.NewGuid())
            .ToArray();
        var userIds = seededUserIds.Take(RequestCount).ToArray();
        _requests = userIds
            .Select(userId => new BulkPermissionCheckRequest(userId, _tenantId, PermissionType.Read))
            .ToArray();
        var expectedAllowedCount = (RequestCount + 1) / 2;

        await SeedDatabaseAsync(seededUserIds).ConfigureAwait(false);
        _coldMemoryCache = CreateMemoryCache();
        _warmMemoryCache = CreateMemoryCache();
        _coldHybridCache = CreateHybridCache(_coldMemoryCache);
        _warmHybridCache = CreateHybridCache(_warmMemoryCache);

        var warmResult = await RunBulkChecksAsync(_warmHybridCache).ConfigureAwait(false);
        if (warmResult != expectedAllowedCount)
        {
            throw new InvalidOperationException(
                $"The PostgreSQL benchmark fixture expected {expectedAllowedCount} allowed requests but evaluated {warmResult}.");
        }

        var individualResult = await RunIndividualChecksAsync().ConfigureAwait(false);
        if (individualResult != warmResult)
        {
            throw new InvalidOperationException(
                $"Individual and bulk permission checks disagreed during setup: {individualResult} vs {warmResult}.");
        }
    }

    [Benchmark(Baseline = true, Description = "PostgreSQL individual checks")]
    public Task<int> IndividualChecksAsync() => RunIndividualChecksAsync();

    [Benchmark(OperationsPerInvoke = 5, Description = "PostgreSQL cold bulk checks with versioned decision cache")]
    public async Task<int> ColdBulkChecksAsync()
    {
        var totalAllowed = 0;
        for (var operation = 0; operation < 5; operation++)
        {
            _coldMemoryCache.Compact(1.0);
            totalAllowed += await RunBulkChecksAsync(_coldHybridCache).ConfigureAwait(false);
        }

        return totalAllowed;
    }

    [Benchmark(Description = "PostgreSQL warm bulk checks with version validation")]
    public Task<int> WarmBulkChecksAsync() => RunBulkChecksAsync(_warmHybridCache);

    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        _coldMemoryCache?.Dispose();
        _warmMemoryCache?.Dispose();
        if (_database is not null)
        {
            await _database.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<int> RunIndividualChecksAsync()
    {
        await using var context = new PermissionBulkBenchmarkDbContext(_dbOptions);
        var permissionService = new LegacyPermissionService(context);
        var allowedCount = 0;
        foreach (var request in _requests)
        {
            if (await permissionService.HasPermissionAsync(
                    request.UserId,
                    request.TenantId,
                    request.Permission).ConfigureAwait(false))
            {
                allowedCount++;
            }
        }

        return allowedCount;
    }

    private async Task<int> RunBulkChecksAsync(IHybridPermissionCache hybridCache)
    {
        await using var context = new PermissionBulkBenchmarkDbContext(_dbOptions);
        var versionStore = new DatabaseTenantSecurityVersionStore(new TenantSecurityVersionRepository(context));
        var permissionService = new LegacyPermissionService(
            context,
            versionStore,
            hybridPermissionCache: hybridCache);
        var results = await permissionService.BulkCheckPermissionsAsync(_requests).ConfigureAwait(false);
        return results.Count(result => result.IsGranted);
    }

    private async Task SeedDatabaseAsync(IReadOnlyList<Guid> requestedUserIds)
    {
        await using var context = new PermissionBulkBenchmarkDbContext(_dbOptions);
        await context.Database.EnsureCreatedAsync().ConfigureAwait(false);

        context.Set<TenantSecurityVersion>().AddRange(
            new TenantSecurityVersion { TenantId = _tenantId, SecurityVersion = 1 },
            new TenantSecurityVersion { TenantId = Guid.Empty, SecurityVersion = 1 });
        context.Set<TenantPermission>().AddRange(requestedUserIds.Select((userId, index) => new TenantPermission
        {
            UserId = userId,
            TenantId = _tenantId,
            Permissions = index % 2 == 0 ? [nameof(PermissionType.Read)] : [],
            DenyPermissions = index % 2 == 0 ? [] : [nameof(PermissionType.Read)]
        }));
        context.Set<TenantPermission>().AddRange(Enumerable.Range(0, UnrelatedGrantCount).Select(_ => new TenantPermission
        {
            UserId = Guid.NewGuid(),
            TenantId = _tenantId,
            Permissions = [nameof(PermissionType.Create)]
        }));

        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    private static MemoryCache CreateMemoryCache() =>
        new(new MemoryCacheOptions { SizeLimit = 20_000 });

    private static IHybridPermissionCache CreateHybridCache(MemoryCache memoryCache)
    {
        var cacheOptions = Options.Create(new AuthorizationCacheOptions
        {
            UseDistributedCache = false,
            PermissionTtlSeconds = 300,
            MaxL1CacheSize = 20_000
        });
        return new HybridPermissionCache(
            memoryCache,
            cacheOptions,
            new CacheMetricsService(),
            NullLogger<HybridPermissionCache>.Instance);
    }

    private sealed class PermissionBulkBenchmarkDbContext(DbContextOptions<PermissionBulkBenchmarkDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        DbSet<T> IApplicationDbContext.Set<T>() => Set<T>();

        Task<int> IApplicationDbContext.SaveChangesAsync(CancellationToken cancellationToken) =>
            SaveChangesAsync(cancellationToken);

        Task<IDbContextTransaction> IApplicationDbContext.BeginTransactionAsync(CancellationToken cancellationToken) =>
            Database.BeginTransactionAsync(cancellationToken);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new TenantSecurityVersionConfiguration());
            modelBuilder.Entity<TenantPermission>().Ignore(permission => permission.Metadata);
            modelBuilder.Entity<ContentTypePermission>();
            modelBuilder.Entity<GenericResourcePermission>();
            modelBuilder.Entity<TenantPermission>().HasIndex(permission => new { permission.TenantId, permission.UserId });
        }
    }
}
#pragma warning restore CS0618
