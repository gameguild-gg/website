# Effective Permission Resolution Contract (issue #330)

Status: implemented. Canonical implementation:
`apps/api/Source/Modules/GameGuild.Identity.Authorization/Services/EffectivePermissionResolverService.cs`
(`IEffectivePermissionResolver`). This document is the contract for computing a user's
**effective permissions** across all applicable authorization layers. It consolidates #330
and its native duplicate #347, supplies the effective-resolution behavior referenced by #307,
and aligns deny-by-default behavior with #327.

## Resolution context

Resolution always takes an `EffectivePermissionContext`:

| Field | Required | Meaning |
|---|---|---|
| `UserId` | yes (non-empty) | The acting user. `Guid.Empty` is invalid. |
| `TenantId` | yes (non-empty) | Tenant scope. Tenant comes from the request context, never from a route/body value. `Guid.Empty` or `null` is invalid. |
| `ResourceType` + `ResourceId` | no | The resource layer. Both must be present together or both absent. |

**Fail-closed:** a missing or invalid context (empty user or tenant, half-specified
resource) resolves to an **empty permission set** with `ContextValid = false`, and a
warning is logged. Invalid context never falls back to global defaults.

## Layers (fixed, deterministic order)

1. **Static** — the configured system account (`AuthorizationOptions.SystemAccountId`)
   receives the non-deniable wildcard `*`. There are no other hard-coded permissions:
   the global baseline is data-driven (layer 4).
2. **Dynamic RBAC roles** — `IRbacPermissionResolver` resolves assigned dynamic roles
   *with hierarchy inheritance*. Direct role contributions are attributed to
   `PermissionSource.Role`; inherited ones to `PermissionSource.RoleInheritance`.
3. **Role permission providers** — every registered `IAuthorizationRolePermissionProvider`
   (tenant membership roles, authentication roles, ...). Universal wildcards (`admin:*`)
   are **not delegable** and never contribute.
4. **Global defaults** — the data row `UserId = null, TenantId = null`.
5. **Tenant defaults** — the data row `UserId = null, TenantId = <current tenant>`.
6. **Direct grants** — the data row `UserId = <current user>, TenantId = <current tenant>`.
7. **Just-in-Time elevation grants** (issue #341) — approved `JitElevationRequest` rows for
   the current user and tenant that are inside their time window
   (`IsGrantInForce`). Only tenant-scoped (resource-unscoped) elevations contribute,
   and `admin:*` is never grantable through elevation. JIT grants remain subject to
   DENY-WINS and are attributed to `PermissionSource.TemporaryElevation`.
8. **Resource grants** — `ResourceUserPermission` rows for the current user and tenant.
   They contribute **only** when the context carries the exact matching
   (`ResourceType`, `ResourceId`) pair.

Rows in layers 4–6 contribute only while **active and unexpired**
(`IsActive == true` and `ExpiresAt` is null or in the future). Resource grants contribute
only while **unrevoked and unexpired**.

## Precedence, conflicts and inheritance

- **DENY-WINS:** `Effective = (union of all layers' allows) − (union of all layers'
  denies)`. An explicit deny at *any* layer (global, tenant, direct, dynamic role)
  removes the permission regardless of which layer granted it.
- **Absent = deny:** a permission granted by no layer is denied (deny-by-default, #327).
  No implicit access is introduced by resolution itself.
- **Non-deniable:** only the static system-account wildcard survives an explicit deny.
- **Inheritance:** role hierarchy is honored — permissions of junior roles flow into
  senior roles through layer 2, and provider-level static role permissions through
  layer 3.
- **Determinism:** layer order is fixed and providers are evaluated in registration
  order, so the same inputs always produce the same output. First layer to grant a
  permission wins the `Sources` attribution.

## Context isolation

- Only grants whose `UserId`/`TenantId` match the context contribute; grants from
  unrelated tenants or users can never affect a resolution.
- Resource grants never leak into tenant-wide results: without a resource context the
  resource layer is skipped entirely.
- A grant row scoped to a different tenant (including `TenantId = null` user-global
  rows) does not contribute to tenant-scoped resolution.

## Where the rules are used (entry points and query callers)

| Caller | Path |
|---|---|
| `[Authorize]` permission requirements | `PermissionHandler` → `IAuthorizationPermissionService` (`AuthorizationPermissionServiceAdapter`) → `IPermissionQueryService` → **`IEffectivePermissionResolver`** |
| Permission query APIs / handlers (#307) | `GetEffectivePermissionsQueryHandler`, `TenantPermissionQueries`, `SoDService`, `TasksService`, ... → `IPermissionQueryService` → **`IEffectivePermissionResolver`** |
| Tenant permission single check | `PermissionQueryService.HasTenantPermissionAsync` → **`IEffectivePermissionResolver`** (fail-closed on missing user/tenant) |
| Resource-scoped decisions | `ResourceAccessHandler` / `ResourcePermissionAuthorizationFilter` via the ACL service; resource grants inside effective resolution use layer 7 above |

`IPermissionQueryService.GetEffectivePermissionsAsync` and
`HasTenantPermissionAsync` do not implement their own combination rules — they delegate
to the resolver so that every caller uses this single contract.

## Caching

The resolver itself is **cache-free and deterministic** by design. Authorization caches
that sit in front of permission data must scope entries by authorization context and be
invalidated when permissions change:

- `CachedAccessControlListService` scopes keys by subject + tenant + resource + tenant /
  user / global security version and re-validates the version before trusting an entry.
- Permission mutations (`PermissionGrantService`, dynamic-role repositories) increment
  the tenant/global security version (`ITenantSecurityVersionStore`), invalidating scoped
  cache entries; `CacheInvalidationService` / `RedisPermissionCacheInvalidationPublisher`
  propagate invalidations across nodes.

## Verification

Focused tests: `apps/api/tests/GameGuild.Identity.Authorization.UnitTests/Services/EffectivePermissionResolverServiceTests.cs`
(absent permissions, explicit grants/denies at each layer, cross-layer conflicts,
inheritance, JIT elevation grants, tenant/resource context isolation,
inactive/expired/revoked grants, invalid/missing contexts, system-account wildcard
protection, delegable-wildcard exclusion, determinism),
`apps/api/tests/GameGuild.Identity.Authorization.UnitTests/Services/PermissionQueryServiceTests.cs`
(fail-closed delegation of the query surface), and
`apps/api/tests/GameGuild.Identity.Authorization.UnitTests/JitElevationEnforcementTests.cs`
(JIT enforcement end-to-end through the query surface and the resolver).

## Evaluation engine extensions (issue #358)

### Multi-parent role inheritance

A dynamic role's effective parent set is `{ ParentRoleId } ∪ AdditionalParentRoleIds`.
`RoleInheritanceEngine` computes the closure breadth-first with a visited set: cycle
edges are detected, cut and reported (`RoleInheritanceClosure.CyclesCut`); unknown
parent references contribute nothing (fail closed). Permission flow follows
`inheritedSet(R) = ∪ over parents P of ((directSet(P) ∪ inheritedSet(P)) − blocked(R))`
where `blocked(R)` is the role's `BlockedInheritedPermissions` — a role can opt out of
specific inherited permissions without touching its own direct grants or denies.
Ancestor `DenyPermissions` always flow down unblocked (DENY-WINS stays global).
`PermissionEngine:Inheritance` configures the traversal: `Enabled=false` disables all
inheritance flow, `MaxDepth` (default 10) caps graph depth.

### Evaluation extensions (plugin point)

`IPermissionEvaluationExtension` implementations registered in DI run after built-in
layers 1–8, ordered by `Order` (ties resolve in DI registration order). Each extension
receives a read-only snapshot of the context plus the accumulated allow/deny sets and
returns additional contributions, attributed `PermissionSource.Extension`. Contributions
are fully subject to DENY-WINS, cannot mint static grants and cannot grant the
non-delegable `admin:*` wildcard. A throwing extension is logged and skipped — a broken
plugin can neither open nor break the decision path.

### Evaluation-layer denial throttle (enumeration protection)

`IEvaluationDenialThrottleService` (in-memory sliding window, per user+tenant) is fed by
the resolver's `HasPermission*` checks whenever an evaluation denies. When the deny count
inside `PermissionEngine:EvaluationThrottle:WindowSeconds` exceeds
`MaxDeniedEvaluationsPerWindow`, the pair is throttled for `ThrottleDurationSeconds`:
resolutions short-circuit to an empty fail-closed result
(`EffectivePermissions.Throttled = true`) without consulting any permission store. This
is distinct from endpoint rate limiting — requests are never blocked; only evaluation
outcomes fail closed.

### Change notifications, synchronization, restoration and compliance

- Permission mutations fan out `PermissionChangeEvent`s to registered
  `IPermissionChangeNotifier` implementations. The default webhook notifier is
  config-gated (`PermissionEngine:Webhooks`), signs the payload with HMAC-SHA256
  (`X-GameGuild-Signature: sha256=…` over the exact body bytes) and retries with
  exponential backoff; delivery failures never affect the mutation.
- `IPermissionSyncService` exports/imports permission state as a portable JSON document
  (schema `1.0`, roles reference parents by name). Imports are admin-guarded, validated
  in full and fail closed — invalid documents apply nothing; `dryRun` reports the change
  plan without applying.
- `IPermissionRestorationService` restores soft-deleted permission rows and undoes
  Grant/Revoke/Deny audit entries inside `PermissionEngine:Restoration:RetentionDays`.
  Guards take the tenant from the restored record (never the caller); restorations bump
  the security version, audit as `PermissionOperationType.Restore` and notify webhooks.
- `GET /api/v{version}/authorization/compliance/report` summarizes allow/deny rates by
  permission, evaluation surface and operation from the durable permission evaluation
  log (`PermissionEvaluationLogs`, written through `IPermissionEvaluationLogSink`).

### Verification (issue #358)

`apps/api/tests/GameGuild.Identity.Authorization.UnitTests/PermissionEngineInheritanceTests.cs`,
`…/PermissionEngineEvaluationTests.cs` and `…/PermissionEngineOperationsTests.cs`.
