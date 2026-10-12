using BenchmarkDotNet.Attributes;
using GameGuild.Configuration.PresentationLayer.Authorization;
using GameGuild.Identity.Authorization.Caching;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using Moq;

namespace GameGuild.Identity.Authorization.PerformanceTests;

/// <summary>
/// Compares concurrent database-backed ACL evaluation batches with warm cached ACL batches.
/// PostgreSQL uses the shared disposable database lifecycle and contains an indexed ACL table
/// with 5,000 unrelated entries plus the benchmark grant. The repository adapter uses the
/// production repository's ACL filters while keeping this benchmark independent of migrations.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class PermissionCacheDatabaseLookupBenchmarks
{
    private static readonly Guid TenantId = Guid.Parse("23ca1a7a-a58b-4fc8-b1da-c2a94706a46e");
    private static readonly Guid UserId = Guid.Parse("3c2e06b1-83e3-4d4b-82cf-e77ddeee19ea");
    private const string ResourceType = "Document";
    private const string ResourceId = "permission-cache-benchmark";

    private EconomyPostgreSqlTestDatabase _database = null!;
    private NpgsqlDataSource _dataSource = null!;
    private MemoryCache _memoryCache = null!;
    private DatabaseAccessControlListService _databaseService = null!;
    private CachedAccessControlListService _cachedService = null!;
    private AclSubject _subject = null!;

    [Params(1, 8, 32)]
    public int ConcurrentLookupCount { get; set; }

    [GlobalSetup]
    public async Task SetupAsync()
    {
        _database = await EconomyPostgreSqlTestDatabase.CreateAsync("authorization_benchmarks").ConfigureAwait(false);

        // The shared test database helper disables pooling for test isolation. Keep the
        // benchmark representative of request traffic and avoid TCP socket churn.
        var benchmarkConnectionString = new NpgsqlConnectionStringBuilder(_database.ConnectionString)
        {
            Pooling = true,
            MaxPoolSize = 128
        }.ConnectionString;
        _dataSource = NpgsqlDataSource.Create(benchmarkConnectionString);
        await SeedDatabaseAsync().ConfigureAwait(false);

        var aclRepository = new Mock<IAccessControlListEntryRepository>();
        aclRepository
            .Setup(repo => repo.GetByResourceAndPrincipalsAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<(AclPrincipalType Type, Guid? Id)>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Guid tenantId,
                string resourceType,
                string resourceId,
                IEnumerable<(AclPrincipalType Type, Guid? Id)> principals,
                CancellationToken cancellationToken) =>
                LoadMatchingEntriesAsync(tenantId, resourceType, resourceId, principals, cancellationToken));

        var versionRepository = new Mock<ITenantSecurityVersionRepository>();
        versionRepository
            .Setup(repo => repo.GetVersionsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .Returns((IReadOnlyCollection<Guid> tenantIds, CancellationToken cancellationToken) =>
                LoadTenantVersionsAsync(tenantIds, cancellationToken));

        _databaseService = new DatabaseAccessControlListService(aclRepository.Object, null!);
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
        _cachedService = new CachedAccessControlListService(
            _databaseService,
            _memoryCache,
            new DatabaseTenantSecurityVersionStore(versionRepository.Object),
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
                $"Database and cached ACL setup returned different results: {databaseResult} vs {cachedResult}.");
        }
    }

    [Benchmark(Baseline = true, Description = "PostgreSQL database-backed ACL batch")]
    public Task<AccessLevel[]> DatabaseAclLookupAsync() => Task.WhenAll(
        Enumerable.Range(0, ConcurrentLookupCount)
            .Select(_ => _databaseService.EvaluateAccessAsync(_subject, TenantId, ResourceType, ResourceId)));

    [Benchmark(Description = "Warm cached ACL batch")]
    public Task<AccessLevel[]> CachedAclLookupAsync() => Task.WhenAll(
        Enumerable.Range(0, ConcurrentLookupCount)
            .Select(_ => _cachedService.EvaluateAccessAsync(_subject, TenantId, ResourceType, ResourceId)));

    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        _memoryCache?.Dispose();
        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync().ConfigureAwait(false);
        }

        if (_database is not null)
        {
            await _database.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task SeedDatabaseAsync()
    {
        await using var connection = await _dataSource.OpenConnectionAsync().ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE acl_entries (
                tenant_id uuid NOT NULL,
                principal_type integer NOT NULL,
                principal_id uuid NULL,
                resource_type text NOT NULL,
                resource_id text NOT NULL,
                access_level integer NOT NULL,
                is_denied boolean NOT NULL,
                is_active boolean NOT NULL,
                deleted_at timestamptz NULL,
                expires_at timestamptz NULL
            );
            CREATE INDEX ix_acl_entries_benchmark_lookup
                ON acl_entries (tenant_id, resource_type, resource_id, principal_type, principal_id)
                WHERE deleted_at IS NULL AND is_active = TRUE;
            CREATE TABLE tenant_security_versions (
                tenant_id uuid PRIMARY KEY,
                security_version bigint NOT NULL,
                deleted_at timestamptz NULL
            );
            INSERT INTO tenant_security_versions (tenant_id, security_version)
            VALUES (@tenant_id, 1), (@global_tenant_id, 1);
            INSERT INTO acl_entries
                (tenant_id, principal_type, principal_id, resource_type, resource_id,
                 access_level, is_denied, is_active, deleted_at, expires_at)
            SELECT @tenant_id, @principal_type, md5(noise::text)::uuid, 'Document', 'noise-' || noise::text,
                   1, FALSE, TRUE, NULL, NULL
            FROM generate_series(1, 5000) AS noise;
            INSERT INTO acl_entries
                (tenant_id, principal_type, principal_id, resource_type, resource_id,
                 access_level, is_denied, is_active, deleted_at, expires_at)
            VALUES (@tenant_id, @principal_type, @user_id, @resource_type, @resource_id,
                    @access_level, FALSE, TRUE, NULL, NULL);
            """;
        command.Parameters.AddWithValue("tenant_id", TenantId);
        command.Parameters.AddWithValue("global_tenant_id", Guid.Empty);
        command.Parameters.AddWithValue("principal_type", (int)AclPrincipalType.User);
        command.Parameters.AddWithValue("user_id", UserId);
        command.Parameters.AddWithValue("resource_type", ResourceType);
        command.Parameters.AddWithValue("resource_id", ResourceId);
        command.Parameters.AddWithValue("access_level", (int)AccessLevel.Write);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private async Task<IReadOnlyDictionary<Guid, long>> LoadTenantVersionsAsync(
        IReadOnlyCollection<Guid> tenantIds,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT tenant_id, security_version
            FROM tenant_security_versions
            WHERE tenant_id = ANY(@tenant_ids) AND deleted_at IS NULL;
            """;
        command.Parameters.Add("tenant_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = tenantIds.ToArray();

        var versions = new Dictionary<Guid, long>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            versions.Add(reader.GetGuid(0), reader.GetInt64(1));
        }

        return versions;
    }

    private async Task<IReadOnlyList<AccessControlListEntry>> LoadMatchingEntriesAsync(
        Guid tenantId,
        string resourceType,
        string resourceId,
        IEnumerable<(AclPrincipalType Type, Guid? Id)> principals,
        CancellationToken cancellationToken)
    {
        var principalList = principals.ToArray();
        var namedTypes = principalList
            .Where(principal => principal.Id.HasValue)
            .Select(principal => (int)principal.Type)
            .Distinct()
            .ToArray();
        var namedIds = principalList
            .Where(principal => principal.Id.HasValue)
            .Select(principal => principal.Id!.Value)
            .Distinct()
            .ToArray();
        var anonymousTypes = principalList
            .Where(principal => !principal.Id.HasValue)
            .Select(principal => (int)principal.Type)
            .Distinct()
            .ToArray();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT principal_type, principal_id, access_level, is_denied, is_active, expires_at
            FROM acl_entries
            WHERE tenant_id = @tenant_id
              AND resource_type = @resource_type
              AND resource_id = @resource_id
              AND deleted_at IS NULL
              AND is_active = TRUE
              AND (expires_at IS NULL OR expires_at > NOW())
              AND (
                  (principal_type = ANY(@named_types) AND principal_id IS NOT NULL AND principal_id = ANY(@named_ids))
                  OR (principal_id IS NULL AND principal_type = ANY(@anonymous_types))
              );
            """;
        command.Parameters.Add("tenant_id", NpgsqlDbType.Uuid).Value = tenantId;
        command.Parameters.Add("resource_type", NpgsqlDbType.Text).Value = resourceType;
        command.Parameters.Add("resource_id", NpgsqlDbType.Text).Value = resourceId;
        command.Parameters.Add("named_types", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = namedTypes;
        command.Parameters.Add("named_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = namedIds;
        command.Parameters.Add("anonymous_types", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = anonymousTypes;

        var entries = new List<AccessControlListEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            entries.Add(new AccessControlListEntry
            {
                TenantId = tenantId,
                ResourceType = resourceType,
                ResourceId = resourceId,
                PrincipalType = (AclPrincipalType)reader.GetInt32(0),
                PrincipalId = reader.IsDBNull(1) ? null : reader.GetGuid(1),
                AccessLevel = (AccessLevel)reader.GetInt32(2),
                IsDenied = reader.GetBoolean(3),
                IsActive = reader.GetBoolean(4),
                ExpiresAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5)
            });
        }

        return entries;
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
