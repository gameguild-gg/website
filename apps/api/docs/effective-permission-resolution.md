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
