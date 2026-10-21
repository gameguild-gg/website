# Issue #354: grant expiry during asynchronous ACL evaluation

## Acceptance gap

The current cache wrapper checks the effective lifetime of L1/L2 hits and clamps
cache writes, but a database result can expire while the subsequent security
version read or distributed cache write is awaited. Both the subject and legacy
user paths still return that earlier access level. The pre-existing
`AlreadyExpiredDecision_IsReturnedButNeverCached` test explicitly accepted this
expired source answer. Avoiding a cache write does not make that answer current.

This affects the #354 criterion "Security testing to ensure no stale permission
vulnerabilities" and the shared cache correctness scope on #353. The audit found
this gap on `develop` commit `65f6676f3ea8d5b99384c51a490404ea9384bb0f`.

## Correction

Both public evaluation paths validate the source decision's effective lifetime
after the version check and after the awaited cache write. An expired decision is
discarded and re-evaluated under a fresh security-version snapshot. The existing
three-attempt budget bounds retries and fails closed when no stable decision can
be obtained. A fresh denial can replace an expired allow in L1. Existing version
isolation, cache TTL bounds, public interfaces and database schema are preserved.

## Regression evidence required

`CachedAclStalePermissionSecurityTests` now covers:

- Actual `DatabaseAccessControlListService` filtering, with a controlled clock
  advanced during the post-query version read, for both public paths.
- Actual database ACL evaluation with expiry during the distributed write, for
  both public paths, including a subsequent cached denial.
- A persistently expired source answer exhausting bounded retries without an
  L1/L2 write, for both public paths.
- The existing hit-expiry, L2-promotion, TTL-clamping and mutation-race cases.

These tests have been authored but were not executed locally under the current
local execution hold. Matching-source hosted CI and the relevant unit results
must pass before merging or claiming implementation acceptance. The previous
Redis integration receipt (PR #885, run 39194739354, remaining-1 shard: 14/14
authorization integration cases, including 7 Redis cases) concerns unchanged
distributed-cache behavior; it does not prove this new correction.

This patch establishes a validity check immediately before returning a decision.
It does not claim to make a later resource operation atomic with a future grant
expiry, or to establish production capacity or telemetry delivery.
