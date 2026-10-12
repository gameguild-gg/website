# Authorization cache benchmarks

This project contains three complementary benchmarks:

- `PermissionCacheLookupBenchmarks` compares a direct in-memory permission lookup with a warm L1 cache lookup.
- `PermissionCacheDatabaseLookupBenchmarks` compares database-backed and warm cached ACL batches at concurrency levels 1, 8, and 32.
- `PermissionCacheEfDatabaseLookupBenchmarks` compares the current ACL repository through EF Core/Npgsql against a warm L1 cache while retaining the database-backed tenant/global security-version lookup.
- `PermissionBulkCheckBenchmarks` compares individual permission reads with cold and warm batched decisions against PostgreSQL.

## Bulk permission checks

Use `IPermissionService.BulkCheckPermissionsAsync` for a finite collection when the caller needs all decisions returned together. The service evaluates at most 256 requests per database batch, preserves input order, and returns one result for every input request. The returned collection is still held in memory.

For a large or unbounded source, use `IPermissionService.StreamBulkCheckPermissionsAsync`. Its default batch size is 128; a caller-supplied batch size must be from 1 through 256. It yields each result in request order, accepts a cancellation token, and lets callers process results without retaining the complete request and response sets.

When authorization caching is enabled and both the hybrid cache and tenant security-version store are registered, bulk decisions are reused across calls. Cache keys include tenant and global security versions; grant changes advance the relevant version, so later calls use fresh decisions. With either cache dependency absent, the service keeps the database-backed batch path and does not cache bulk results.

The bulk benchmark seeds 5,000 unrelated permission grants and evaluates 64 or 256 requested user checks against disposable PostgreSQL 17. It compares one database-backed check per request, a cold call that populates the versioned cache, and a warm call that still validates security versions. Each cold benchmark invocation clears the L1 cache before five independent API calls so BenchmarkDotNet can measure a stable operation duration. It validates that individual and bulk decisions agree before measurement. Results are local comparative evidence only; the fixture does not model Redis, production traffic distribution, or deployment networking.

### Bulk permission local comparison (2026-10-03)

BenchmarkDotNet 0.14.0 ran 8 warmups, 10 measured iterations, and 3 launches on Windows 10, .NET 10.0.10, and Docker PostgreSQL 17. Npgsql pooling was enabled with a maximum of 128 connections. The reported error is the 99.9% confidence interval margin; allocations are managed memory per API operation.

| Requests | Individual PostgreSQL checks | Cold batched checks | Warm batched checks with version validation | Allocated: individual / cold / warm |
| ---: | ---: | ---: | ---: | ---: |
| 64 | 54.194 ± 6.979 ms | 16.970 ± 0.787 ms | 1.702 ± 0.160 ms | 963.21 / 510.17 / 107.32 KB |
| 256 | 193.734 ± 11.155 ms | 57.573 ± 2.797 ms | 1.716 ± 0.039 ms | 3,750.22 / 1,793.19 / 230.06 KB |

BenchmarkDotNet flagged the cold 64-request distribution as multimodal and removed several outliers across cases. The local results show the expected relative benefit for this fixture but are not production capacity measurements. Reproduce the run with:

```powershell
dotnet run --project apps/api/tests/GameGuild.Identity.Authorization.PerformanceTests/GameGuild.Identity.Authorization.PerformanceTests.csproj --configuration Release -- --filter "*PermissionBulkCheckBenchmarks*" --iterationCount 10 --warmupCount 8 --launchCount 3 --artifacts "$env:TEMP\GameGuildBulkPermissionBenchmarks"
```

The raw-SQL database benchmark starts a disposable PostgreSQL 17 container, creates indexed ACL and tenant-version tables, and seeds 5,000 unrelated ACL entries plus the grant under test. It exercises `DatabaseAccessControlListService` and `CachedAccessControlListService`; the ACL repository adapter runs a PostgreSQL query with the same tenant, resource, active, expiry, and principal filters as `AccessControlListEntryRepository`. The cached case includes the production `DatabaseTenantSecurityVersionStore` lookup of tenant and global versions, then measures a warm per-process L1 ACL lookup. The shared Testcontainers helper disables pooling for ordinary test isolation, so this benchmark explicitly enables Npgsql pooling with a maximum of 128 connections. This lets independent lookups execute concurrently without measuring repeated TCP connection setup. The fixture omits EF materialization, Redis, production data distribution, and networked application deployment. Treat it as a local comparative fixture, not a production capacity result.

## Raw-SQL local comparison (2026-10-03)

ShortRun used 8 warmup iterations, 10 measured iterations, and one launch on Windows 10, .NET 10.0.10, with Docker PostgreSQL 16. Values are total time for each concurrent batch, not per-request latency. Pooling is enabled explicitly for this run. These results replace the 2026-09-29 values, which inherited `Pooling=false` from the shared test database helper and therefore did not model pooled API connections.

| Concurrent lookups per batch | Database ACL batch | Warm cached ACL batch | Cache/database ratio | Managed allocation ratio |
| ---: | ---: | ---: | ---: | ---: |
| 1 | 0.767 ms ± 0.092 ms | 0.569 ms ± 0.029 ms | 0.75 | 0.53 |
| 8 | 1.916 ms ± 0.101 ms | 1.472 ms ± 0.211 ms | 0.77 | 0.51 |
| 32 | 6.027 ms ± 0.846 ms | 4.705 ms ± 0.870 ms | 0.79 | 0.51 |

The reported error intervals are 99.9% confidence intervals. They overlap at concurrency 32, and this one-launch raw-SQL comparison is diagnostic only. The EF-backed comparison below uses three launches and is the stronger local measurement; neither fixture establishes production capacity.

## EF-backed local comparison (2026-10-03)

The EF benchmark uses the current `AccessControlListEntryRepository` and `TenantSecurityVersionRepository` with a request-scoped test `DbContext` per lookup. It starts PostgreSQL 17 in Docker, seeds 5,000 unrelated ACL rows plus the grant under test, and compares database ACL reads with a warm process-local L1 ACL cache. Both paths read the tenant/global security versions from PostgreSQL. The shared Testcontainers helper disables Npgsql pooling for ordinary test isolation, so this benchmark explicitly turns pooling on with a maximum of 128 connections; this models API request handling and avoids measuring repeated TCP connection setup. The fixture does not run the full API `ApplicationDbContext`, Redis, production data distribution, or a networked application deployment.

BenchmarkDotNet 0.14.0 ran 10 warmup iterations, 20 measured iterations, and 3 launches on Windows 10 with .NET 10.0.10, Docker Desktop 29.8.0, and an Intel Xeon W-2235 3.80 GHz host. Values are total time and managed allocation for a concurrent batch. The reported error intervals are 99.9% confidence intervals. This fresh run measured 21–30% lower mean batch latency and 23–24% lower managed allocation for the cached path; the confidence intervals do not overlap at any tested concurrency.

| Concurrent lookups per batch | PostgreSQL + EF batch | Warm L1 + EF version-read batch | Cache/database latency ratio | Managed allocation ratio |
| ---: | ---: | ---: | ---: | ---: |
| 1 | 1.140 ms ± 0.088 ms | 0.895 ms ± 0.062 ms | 0.80 | 0.77 |
| 8 | 3.341 ms ± 0.246 ms | 2.355 ms ± 0.088 ms | 0.72 | 0.77 |
| 32 | 10.326 ms ± 0.365 ms | 7.758 ms ± 0.332 ms | 0.76 | 0.76 |

The cache/database latency intervals do not overlap at the three measured concurrency levels. This is evidence for the isolated EF-backed fixture only; it does not establish production capacity or behavior with Redis, representative production data, the full API context, or networked database deployment.

Run the database benchmark with Docker available:

```powershell
dotnet run --project apps/api/tests/GameGuild.Identity.Authorization.PerformanceTests/GameGuild.Identity.Authorization.PerformanceTests.csproj --configuration Release -- --filter "*PermissionCacheDatabaseLookupBenchmarks*" --iterationCount 10 --warmupCount 8 --launchCount 1 --artifacts "$env:TEMP\GameGuild.AuthorizationCacheBenchmarks"
```

Run the EF-backed benchmark with Docker available:

```powershell
dotnet run --project apps/api/tests/GameGuild.Identity.Authorization.PerformanceTests/GameGuild.Identity.Authorization.PerformanceTests.csproj --configuration Release -- --filter "*PermissionCacheEfDatabaseLookupBenchmarks*" --iterationCount 20 --warmupCount 10 --launchCount 3 --artifacts "$env:TEMP\GameGuild.AuthorizationEfCacheBenchmarks"
```

```bash
dotnet run --project apps/api/tests/GameGuild.Identity.Authorization.PerformanceTests/GameGuild.Identity.Authorization.PerformanceTests.csproj --configuration Release -- --filter '*PermissionCacheDatabaseLookupBenchmarks*' --iterationCount 10 --warmupCount 8 --launchCount 1 --artifacts "${TMPDIR:-/tmp}/gameguild-authorization-cache-benchmarks"
```

```bash
dotnet run --project apps/api/tests/GameGuild.Identity.Authorization.PerformanceTests/GameGuild.Identity.Authorization.PerformanceTests.csproj --configuration Release -- --filter '*PermissionCacheEfDatabaseLookupBenchmarks*' --iterationCount 20 --warmupCount 10 --launchCount 3 --artifacts "${TMPDIR:-/tmp}/gameguild-authorization-ef-cache-benchmarks"
```

Benchmark results describe this isolated local fixture. Use a production-like database, data distribution, deployment topology, and repeated runs before using them for capacity planning.
