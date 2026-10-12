using System.Data.Common;
using FluentAssertions;
using GameGuild.Configuration.PresentationLayer.Authorization;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.Caching;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;
using LegacyPermissionService = GameGuild.Identity.Authentication.PermissionService;

namespace GameGuild.Identity.Authentication.IntegrationTests;

public sealed class PermissionServiceBulkCachePostgreSqlTests
{
    private const int RequestedUserCount = 1_024;
    private const int UnrelatedGrantCount = 5_000;

    [Fact]
    public async Task BulkChecksStreamLargePostgreSqlResultSetsAndInvalidateAcrossCalls()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("bulk_permission_cache_integration");
        var connectionString = new NpgsqlConnectionStringBuilder(database.ConnectionString)
        {
            Pooling = true,
            MaxPoolSize = 64
        }.ConnectionString;
        var queryCounter = new PermissionGrantQueryCounter();
        var dbOptions = new DbContextOptionsBuilder<PermissionBulkCacheDbContext>()
            .UseNpgsql(connectionString)
            .AddInterceptors(queryCounter)
            .Options;
        var tenantId = Guid.NewGuid();
        var userIds = Enumerable.Range(0, RequestedUserCount).Select(_ => Guid.NewGuid()).ToArray();

        await using var context = new PermissionBulkCacheDbContext(dbOptions);
        await context.Database.EnsureCreatedAsync();
        context.Set<TenantSecurityVersion>().AddRange(
            new TenantSecurityVersion { TenantId = tenantId, SecurityVersion = 1 },
            new TenantSecurityVersion { TenantId = Guid.Empty, SecurityVersion = 1 });
        context.Set<TenantPermission>().AddRange(userIds.Select((userId, index) => new TenantPermission
        {
            UserId = userId,
            TenantId = tenantId,
            Permissions = index % 2 == 0
                ? [nameof(PermissionType.Read), nameof(PermissionType.Edit)]
                : [nameof(PermissionType.Edit)],
            DenyPermissions = index % 2 == 0 ? [] : [nameof(PermissionType.Read)]
        }));
        context.Set<TenantPermission>().AddRange(Enumerable.Range(0, UnrelatedGrantCount).Select(_ => new TenantPermission
        {
            UserId = Guid.NewGuid(),
            TenantId = tenantId,
            Permissions = [nameof(PermissionType.Create)]
        }));
        await context.SaveChangesAsync();
        queryCounter.Reset();

        using var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 20_000 });
        var cacheOptions = Options.Create(new AuthorizationCacheOptions
        {
            UseDistributedCache = false,
            PermissionTtlSeconds = 300,
            MaxL1CacheSize = 20_000
        });
        var hybridCache = new HybridPermissionCache(
            memoryCache,
            cacheOptions,
            new CacheMetricsService(),
            NullLogger<HybridPermissionCache>.Instance);
        var versionStore = new DatabaseTenantSecurityVersionStore(new TenantSecurityVersionRepository(context));
        var permissionService = new LegacyPermissionService(
            context,
            securityVersionStore: versionStore,
            hybridPermissionCache: hybridCache);
        var requests = userIds
            .Select(userId => new BulkPermissionCheckRequest(userId, tenantId, PermissionType.Read))
            .ToArray();

        var firstResults = await StreamAsync(permissionService, requests);
        firstResults.Should().HaveCount(RequestedUserCount);
        firstResults.Select(result => result.Request).Should().Equal(requests);
        firstResults.Count(result => result.IsGranted).Should().Be(RequestedUserCount / 2);
        queryCounter.PermissionGrantSelectCount.Should().Be(8);

        queryCounter.Reset();
        var cachedResults = await StreamAsync(permissionService, requests);
        cachedResults.Should().BeEquivalentTo(firstResults);
        queryCounter.PermissionGrantSelectCount.Should().Be(0);

        await permissionService.RevokeTenantPermissionAsync(userIds[0], tenantId, [PermissionType.Read]);

        queryCounter.Reset();
        var invalidatedResults = await StreamAsync(permissionService, requests);
        invalidatedResults.Should().HaveCount(RequestedUserCount);
        invalidatedResults.Single(result => result.Request.UserId == userIds[0]).IsGranted.Should().BeFalse();
        invalidatedResults.Count(result => result.IsGranted).Should().Be((RequestedUserCount / 2) - 1);
        queryCounter.PermissionGrantSelectCount.Should().Be(8);

        queryCounter.Reset();
        var refreshedResults = await StreamAsync(permissionService, requests);
        refreshedResults.Should().BeEquivalentTo(invalidatedResults);
        queryCounter.PermissionGrantSelectCount.Should().Be(0);
    }

    private static async Task<IReadOnlyList<BulkPermissionCheckResult>> StreamAsync(
        LegacyPermissionService permissionService,
        IEnumerable<BulkPermissionCheckRequest> requests)
    {
        var results = new List<BulkPermissionCheckResult>();
        await foreach (var result in permissionService.StreamBulkCheckPermissionsAsync(
                           ToAsyncEnumerable(requests),
                           batchSize: 128))
        {
            results.Add(result);
        }

        return results;
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> values)
    {
        foreach (var value in values)
        {
            yield return value;
            await Task.CompletedTask;
        }
    }

    private sealed class PermissionBulkCacheDbContext(DbContextOptions<PermissionBulkCacheDbContext> options)
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

    private sealed class PermissionGrantQueryCounter : DbCommandInterceptor
    {
        private int _permissionGrantSelectCount;

        public int PermissionGrantSelectCount => Volatile.Read(ref _permissionGrantSelectCount);

        public void Reset() => Interlocked.Exchange(ref _permissionGrantSelectCount, 0);

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            _ = eventData;
            CountPermissionGrantRead(command);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            _ = eventData;
            cancellationToken.ThrowIfCancellationRequested();
            CountPermissionGrantRead(command);
            return ValueTask.FromResult(result);
        }

        private void CountPermissionGrantRead(DbCommand command)
        {
            if (command.CommandText.Contains("TenantPermissions", StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref _permissionGrantSelectCount);
            }
        }
    }
}
