# Authorization cache configuration

Authorization caching uses an in-memory L1 cache and an optional Redis L2 cache.
Register the Redis helper before the authorization cache so the same Redis
connection and invalidation channel are available to the cache and its
subscriber:

```csharp
var redisConnectionString = configuration.GetConnectionString("AuthorizationRedis")
    ?? throw new InvalidOperationException("AuthorizationRedis connection is required.");

services.AddAuthorizationRedisCache(redisConnectionString, instanceName: "gg:auth:");
services.AddAuthorizationCaching(options =>
{
    options.PolicyTtlSeconds = 300;
    options.PermissionTtlSeconds = 300;
    options.AccessControlListTtlSeconds = 60;
    options.DistributedCacheTtlSeconds = 600;
    options.MaxL1CacheSize = 5000;
    options.UsePubSubInvalidation = true;
    options.InvalidationChannelName = "gg:auth:invalidate";
    options.L2CompressionEnabled = true;
    options.L2CompressionAlgorithm = L2CompressionAlgorithm.GZip;
    options.L2CompressionThresholdBytes = 1024;
    options.RedisServiceName = null; // Sentinel master set name, when Sentinel fronts the topology
    options.RedisConnectRetry = 3;   // failover connect attempts
});
```

Keep the Redis connection string in the deployment secret store. `AddAuthorizationRedisCache`
configures `IDistributedCache`, enables L2, registers the StackExchange.Redis
multiplexer used for Pub/Sub, and starts the per-instance invalidation subscriber.
The subscriber retries subscription failures; StackExchange.Redis restores an
active subscription after reconnecting. See the official [connection behavior
documentation](https://github.com/StackExchange/StackExchange.Redis/blob/main/docs/Configuration.md)
and [Pub/Sub reconnect fixes](https://github.com/StackExchange/StackExchange.Redis/blob/main/docs/ReleaseNotes.md).

The cache options also include `RulesetTtlSeconds`, `MaxPolicyCacheSize`,
`EnableMetrics`, and `MetricsLoggingIntervalSeconds`. The Redis channel must be
non-empty when distributed Pub/Sub invalidation is enabled. Use a separate
channel for each isolated application deployment that shares the same Redis
server.

`MaxL1CacheSize` bounds the entries registered through the authorization cache
key tracker. When the cap is exceeded, the tracker removes the
least-recently-used L1 entry and records a `capacity` eviction. This cap is
scoped to tracked authorization entries; it does not set `SizeLimit` on the
shared application `IMemoryCache`.

Permission changes advance the shared tenant security version before publishing
user, resource, or policy invalidation events. Cache keys include this version,
so entries from the old version are no longer selected if a Pub/Sub publish
fails. Publish failures are logged, and old cache entries expire through their
configured TTLs. Pub/Sub provides prompt local cleanup. A singleton per-process key index tracks L1 entries from cache services and request scopes so received events can evict the actual local entries.
Wildcard matching scans this in-memory index; the implementation does not issue Redis key scans for deletion.

ACL cache keys fingerprint their free-form segments: the `resourceType` and
`resourceId` parts of `acl:` and `acl:subj:` keys are SHA-256 hex fingerprints
(the same scheme as the bulk permission keys) instead of the raw strings, so
delimiter-bearing identifiers can never collide two distinct resources onto one
cached access decision. GUID-valued segments (tenant, user, role, group) and the
`tv`/`uv`/`gv` version suffixes remain human-readable, so key-inspection output
shows which subject and version scope an entry belongs to; the two 64-character
hex segments after the subject parts identify the resource pair.

ACL keys also include a shared global security version stored under the reserved
`Guid.Empty` version scope. Global role assignment, removal, update, and deletion
advance this version and publish a global invalidation event. This makes prior
ACL keys unreachable across tenants even when Pub/Sub is unavailable; received
events also clear tracked ACL entries from the local L1 cache.
The tenant and global versions are read together in one database query.

ACL decisions derived from time-bound grants (`ExpiresAt` on ACL entries) are
cached with the boundary attached. Time-based expiration performs no mutation and
advances no security version, so the cached copy polices itself: writes clamp the
L1 and L2 TTL to the seconds remaining before the earliest effective grant
expiration (never above the configured TTL), and every read re-checks the
boundary before serving, which also covers entries promoted from L2 to L1.
A decision whose grants already lapsed is returned once from the authoritative
evaluation but never cached.

Bulk invalidation accepts 1–500 typed targets (user, resource, policy, or role/group
dependency), advances the tenant version once, evicts matching local L1 entries,
and publishes one versioned Redis event. ACL grant and revoke operations publish
the affected resource together with its role or group dependency, so cached
subject decisions are evicted across request scopes. The subscriber validates
batch targets before applying them. This covers direct ACL dependencies; broader
hierarchical inheritance invalidation still needs explicit verification.

The hidden permission administration controller exposes
`GET /v{version}/permissions/cache/stats` to system administrators and
`POST /v{version}/permissions/cache:clear` for the authorized user or tenant
scope. The statistics response reports process-local L1 entry counts, distinct
users represented by those keys, and the recorded L1/L2 hits, misses, evictions,
and per-cache-type counts. `PerformanceMetrics` includes the average measured
cache lookup duration and sample count per cache type since process start. It
retains no raw samples and does not aggregate across instances. The OpenTelemetry
histogram remains the source for percentile analysis in the configured collector.

Route reconciliation for issue #353: the issue's requested
`/api/cache/permissions/*` surface is implemented under the platform's versioned
API convention as `/v{version}/permissions/cache/*` —
`GET /v{version}/permissions/cache/stats`,
`POST /v{version}/permissions/cache:clear`,
`POST /v{version}/permissions/cache:warm`,
`GET /v{version}/permissions/cache/keys`, and
`GET /v{version}/permissions/cache/keys:inspect?key=...`.
No unversioned `/api/cache/permissions` alias is registered because the
repository has no precedent for duplicate unversioned routes.

System administrators can list and inspect tracked L1 cache keys for debugging
with `GET /v{version}/permissions/cache/keys` (query parameters: `search`, a
case-insensitive substring of the key; `cacheType`, an exact match such as
`acl`; `page`, 1-based; `pageSize`, capped at 200 with a default of 50) and
`GET /v{version}/permissions/cache/keys:inspect?key=<cacheKey>` (404 when the
key is not tracked in this process). Both endpoints are `[Authorize]`-guarded
with the SystemAdmin policy — they are not anonymous and are not listed in the
anonymous endpoint registry. Each entry reports the key, its cache type, the
first-tracked and last-access timestamps, and whether the entry is currently
present in the process-local L1 cache. Results are process-local snapshots
ordered most-recently-used first; they reflect only the serving replica and are
read-only (no cache or permission mutation).

Set `OpenTelemetry:Enabled` to `true` to export the authorization cache meter
alongside traces. It uses the configured console exporter and OTLP endpoint and
includes hit, miss, eviction, and lookup-duration instruments. Give each API
replica a distinct `OpenTelemetry:ServiceInstanceId` (for example, its pod ID);
when omitted, the API generates a process-unique instance ID. Aggregate the
counters across replicas in the collector or backend. The Prometheus rules below
assume the default translation that appends `_total` to counters and expands the
histogram's `ms` unit to `milliseconds`. The lookup histogram measures only cache
reads and does not include the authoritative database lookup after a miss.
Prometheus alert examples for high miss ratio, p95 lookup latency, and capacity
evictions are in [authorization-cache-alerts.yml](./authorization-cache-alerts.yml).
The thresholds are starting points and should be tuned to observed workload.
Cache-operation counters include both request traffic and scheduled warmup lookups.

System administrators can prewarm up to 500 selected ACL decisions with
`POST /v{version}/permissions/cache:warm`. Each item names a tenant resource and
either a user with role/group IDs or the anonymous subject. The endpoint uses
the normal versioned ACL evaluation path and returns only requested, warmed, and
duplicate counts; it does not return access decisions. This is explicit
operator-selected prewarming.

Automatic popularity-based warming is enabled by default for subject-based ACL
evaluations. Each API process keeps a bounded, process-local frequency window
(`PopularityTrackingCapacity`, default 5,000 distinct pairs). Every
`AutomaticWarmupIntervalSeconds` (default 60), it selects up to
`AutomaticWarmupMaxEntriesPerCycle` (default 50) pairs seen at least
`AutomaticWarmupMinimumAccessCount` times in that window (default 5). Warmup
evaluates the selected pairs through the normal versioned ACL path, and those
warmup evaluations are excluded from popularity counts. If the tracking window
is full, new distinct pairs are ignored until the next cycle; existing pairs
continue accumulating. Configure `AutomaticWarmupEnabled` to `false` to disable
both observation and scheduled work. Each replica ranks its own observations;
when Redis L2 is enabled, the usual distributed cache remains shared. A failed
cycle is logged and a later cycle continues.

If an L2 read fails or contains invalid data, the hybrid cache records a warning
and returns a miss so the calling service can load the authoritative value from
its backing store. L2 write and removal failures are logged without failing the
request; the L1 operation remains in effect. Caller-requested cancellation still
propagates.

### L2 payload compression

L2 values are compressed before they are written to Redis when
`L2CompressionEnabled` is `true` (the default), `L2CompressionAlgorithm` is not
`None`, and the serialized payload is at least `L2CompressionThresholdBytes`
bytes (default 1024). `GZip` (the default) trades a smaller ratio for lower CPU
per write; `Brotli` trades higher CPU for a better ratio. Compression failures
fall back to storing the raw payload instead of failing the write.

Compressed payloads carry a versioned envelope (a `GGC1` magic marker, an
algorithm byte, then the compressed stream). Readers negotiate transparently:
enveloped bytes are decompressed, and bytes without the marker are treated as
the raw JSON written before compression was enabled (or by a replica with
compression disabled). An enveloped entry that cannot be decoded — for example
an unknown algorithm byte after a downgrade — is logged and treated as a miss,
so the authoritative value is reloaded and rewritten. This keeps mixed-version
deployments readable during a rolling upgrade.

### L1 capacity eviction is least-recently-used

When the tracked-entry cap (`MaxL1CacheSize`) is exceeded, the key tracker
removes the least-recently-used entry rather than the oldest-inserted one.
Every L1 read hit recorded through the hybrid cache refreshes the key's
last-access ordering; entries that are never read again are evicted first.
TTL behavior is unchanged: expired entries still leave the index through the
registered post-eviction callbacks, and insertion order breaks ties.

### Redis topology and failover

`RedisServiceName` passes the Sentinel master set name through to StackExchange.Redis
(`ConfigurationOptions.ServiceName`); list the Sentinel endpoints themselves in
the connection string. `RedisConnectTimeoutMilliseconds` and `RedisConnectRetry`
override the connect timeout and the number of failover connect attempts, and
`RedisProxy` selects `None`, `Twemproxy`, or `Envoyproxy`. Unset options never
override values from the connection string. Redis Cluster needs no dedicated
setting: list the cluster endpoints comma-separated and the client follows
MOVED/ASK redirects automatically. The multiplexer is always built with
`AbortOnConnectFail = false` so a failed initial connect retries in the
background. TLS client-certificate selection and per-endpoint Sentinel
credentials are deferred: configure them through the connection string.

### Redis cache backup and disaster recovery

The authorization cache is a **derived, rebuildable** store. PostgreSQL holds the
authoritative permissions and ACL entries; every cache miss falls through to the
database. Disaster recovery for the cache is therefore an availability concern,
never a data-recovery concern: the safest recovery is an empty cache.

**Persistence tradeoffs (RDB snapshots vs AOF).** The local Compose Redis already
enables AOF (`--appendonly yes` with the `redis-data` volume). AOF journals every
write and, with the default `appendfsync everysec`, loses at most about one
second of writes at the cost of larger files and a replay-based restart. RDB
snapshots (`--save <seconds> <changes>`, or `BGSAVE`) are compact point-in-time
files that restart fast but lose everything since the last snapshot. The
recommended combination for a deployment that wants warm-restart optimization is
both: AOF for the small loss window and a scheduled RDB as the cold-backup
artifact. For the authorization cache specifically, either choice is acceptable,
because the versioned-key rule below makes restored entries harmless.

**Versioned-key safety on restore.** Every ACL and permission cache key embeds
the tenant, global, and user security versions current at write time. Restoring
a Redis backup (or failing over to a replica with stale data) cannot resurrect
stale authorization decisions: lookups build keys from the *current* security
versions, so old-version entries are unreachable and simply expire through
their TTLs. This is the same mechanism that makes a lost Pub/Sub invalidation
harmless.

**Backup procedure.** To take a backup without stopping the service:

```bash
# Snapshot to RDB without blocking Redis
docker exec game-guild-redis redis-cli BGSAVE
# Copy the artifact out of the volume, then store it offsite (encrypted)
docker run --rm -v game-guild-redis-data:/data -v "$PWD:/backup" alpine \
  cp /data/dump.rdb /backup/redis-auth-cache-$(date -u +%Y%m%dT%H%M%SZ).rdb
```

Alternative for remote capture: `redis-cli --rdb /backup/dump.rdb`. Schedule
`BGSAVE` through `--save 900 1` (or the deployment's scheduler) and keep the
retention the deployment requires; the files are small because cache values are
single enum decisions.

**Restore procedure (database-authoritative).**

1. Prefer not restoring at all: start Redis empty and let the cache refill from
   misses. This is the correct choice after any permission incident.
2. If warm restart is wanted (for example restoring a whole host), restore the
   RDB into the `redis-data` volume (or AOF files) while the API is stopped, then
   start Redis, then the API. Restored entries whose embedded security versions
   still match remain usable; everything else is unreachable and expires.
3. After a restore, optionally prewarm hot decisions with
   `POST /v{version}/permissions/cache:warm` and let popularity-based warming
   refill the rest.
4. Never restore a backup into a deployment whose database was rolled back
   further than the backup: the database is authoritative, and a cache newer
   than the database only causes harmless misses, but a database rollback must
   be followed by a cache flush (below) so long-TTL entries cannot mask the
   rollback.

**Flush procedure for a suspect or corrupted cache.** Delete only this
deployment's key namespace, never `FLUSHALL`/`FLUSHDB` on a shared Redis:

```bash
# The instanceName configured in AddAuthorizationRedisCache prefixes every key
docker exec game-guild-redis sh -c \
  'redis-cli --scan --pattern "gg:auth:*" | xargs -r redis-cli UNLINK'
```

Then warm as in step 3. (Redis `SCAN` never blocks the server; `UNLINK` deletes
in the background.)

**Failover monitoring.** The Compose healthcheck (`redis-cli ping`) gates
container restarts. At the application boundary, the multiplexer reconnects in
the background (`AbortOnConnectFail = false`, `RedisConnectRetry`) and the cache
degrades to database reads while Redis is unavailable — watch the cache metrics
for sustained miss-ratio elevation (alert rules in
[authorization-cache-alerts.yml](./authorization-cache-alerts.yml)) as the
signal of a degraded or failed Redis. For the Redis server itself, export
`redis_up`, `redis_master_repl_offset` drift on replicas, `redis_aof_last_write_status`,
and `redis_rdb_last_save_timestamp_seconds` staleness into the same Prometheus
pipeline that loads the authorization-cache rules, and route alerts through the
Alertmanager receiver configured for the deployment. Sentinel and Cluster
topologies additionally alert on `redis_master_up` / managed-failover events
from the provider.

The authorization and authentication unit suites cover the L1/L2 cache behavior,
DI registration, version increments, serialized event payloads, subscriber
dispatch and retry, Redis publish failure handling, and cache availability when
L2 reads, writes, or removals fail. The Redis integration project contains
focused cases for stale-ACL protection, Pub/Sub dispatch, role/group dependency
invalidation, and subscriber recovery after a Redis restart. The four focused
Redis integration tests passed locally with Docker enabled. API Verify now
selects the integration-test project when its files change.

Full network fault injection during Redis reconnects, inherited hierarchy
invalidation through actual group-definition and membership mutation paths,
and production-representative database-backed performance/load measurements
remain open work. Cross-instance metric export and example alert thresholds
are implemented but still need CI and deployment-level validation.
