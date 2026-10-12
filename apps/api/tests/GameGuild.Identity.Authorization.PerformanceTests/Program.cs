using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromTypes(
    [
      typeof(GameGuild.Identity.Authorization.PerformanceTests.PermissionCacheLookupBenchmarks),
      typeof(GameGuild.Identity.Authorization.PerformanceTests.PermissionCacheDatabaseLookupBenchmarks),
      typeof(GameGuild.Identity.Authorization.PerformanceTests.PermissionCacheEfDatabaseLookupBenchmarks),
      typeof(GameGuild.Identity.Authorization.PerformanceTests.PermissionBulkCheckBenchmarks)
    ])
    .Run(args);
