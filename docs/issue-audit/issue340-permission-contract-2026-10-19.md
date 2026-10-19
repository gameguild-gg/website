# Issue #340: complete permission service contracts

## Retained scope

The original request covers five core operations (create, retrieve, update,
revoke and filtered/paged listing), specialized checking/effective resolution,
bulk grant/revoke and history, for tenant, content-type and resource permissions.
All 15 original functional criteria remain required. The separately labelled
fluent API is optional; it does not replace any mandatory operation.

The two existing `IPermissionService` namespaces are not interchangeable. The
Authentication contract is the existing three-layer compatibility facade.
Authorization's deprecated facade and focused services remain separate existing
consumers. Changing their method signatures or old return types would break
providers and callers, so the new read capability uses an additive V2 contract.

## Current read increment

- `IPermissionServiceV2` inherits the existing three-layer contract plus
  `IPermissionReadService` and `IPermissionUpdateService`; the concrete service
  implements each. Production DI aliases the old contract, V2, read/update capabilities and concrete type to one instance
  per scope. No existing interface member or constructor argument is changed.
- Direct tenant/content-type/typed-resource records and stored permission history
  return the canonical `Result<PagedResult<T>>`. This adds no alternative
  authorization engine or inferred history projection.
- The actor supplies the default subject/tenant. Cross-tenant and explicit global
  reads require SystemAdmin. Record listing for other subjects requires tenant
  administration or `permissions:manage`; other-subject/default/all-subject audit
  history requires SystemAdmin. Missing context fails closed before storage.
- Pagination is one-based, 1-200 rows per page, with an overflow-checked offset.
  Subject, tenant, exact type/resource, active/expiry and inclusive UTC bounds are
  applied in the database before count/skip/take. Soft-deleted grants are excluded.
  Ties use ascending identifiers after descending grant/event timestamps.
- Stored audit snapshots remain exact: a bulk event is not fabricated into a
  single enum permission. Reads are untracked. Cancellation and storage failures
  propagate; invalid/access-denied requests return typed errors without data.

This is a service-contract increment, not completion of issue #340. No public
HTTP route, payload, database schema or generated client is changed.

## Guarded existing-grant update increment

`PermissionGrantUpdate<T>` replaces an existing grant's allow payload, active
state, UTC expiration and bounded administration note. Tenant keys retain their
native string representation (including custom keys); typed grants retain their
existing enum encoding. Empty allows clear that payload; tenant denies, subject,
grantor, granted-at, tenant and content/resource bindings stay unchanged.

The actor must administer the selected tenant. Cross-tenant selection requires
SystemAdmin; explicit global selection requires SystemAdmin or
`system:manage-global-defaults`. Anonymous, invalid scope/payload/version and
unauthorized requests fail before storage. The positive expected entity version
is mandatory; stale or concurrent updates return Conflict. Soft-deleted records
cannot be reactivated. Missing version/audit collaborators fail closed.

Successful writes advance the entity version and the selected tenant/global
security version and persist a required `PermissionAuditLog` in one transaction.
Updates record `Update` / `GrantState` with the bounded family label and exact
existing V1 before/after snapshots; resource/type bindings remain in the snapshot.
A batch of allows is not mislabelled as one enum permission. Administrative notes
are not copied into access-state snapshots.

Standalone updates own the transaction. An existing transaction must support a
savepoint; its owner controls final commit/retry/evidence delivery. Unsaved caller
mutations or queued domain/integration events are rejected to prevent a permission
write from unexpectedly persisting unrelated work. Rollback restores tracked
state, pending permission evidence and the outer operation's event-capture state.
Explicit change detection catches pending caller edits even when automatic
detection was disabled. The transaction temporarily enables automatic detection
for grant, version-repository and audit writes, then restores the caller's setting.
Only the authenticated actor and explicit authorized target scope feed the host
audit buffer; a null global target does not inherit the caller's tenant.

No automatic replay is introduced: these mutations have no idempotency key, and
an ambiguous commit error propagates for reconciliation. The execution wrapper
uses EF's execution-strategy scope with zero retries to avoid an inner provider
replaying individual writes; an already active outer strategy retains ownership.
See [EF connection resiliency and commit ambiguity](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency).

`AddPermissionServiceContracts` is called by the actual host infrastructure setup
as well as the optional presentation registration. The host previously called
Authentication application/data registration without the presentation entry point.

## Verification definitions

`PermissionReadContractTests` defines validation, overflow, cancellation,
authentication, tenant/subject/default/global isolation, stricter history access,
all three grant families, stable page boundaries, inactive metadata opt-in,
single-sided history bounds, exact snapshots, storage failure and production DI
aliasing cases. Native `PermissionReadContractPostgreSqlTests` exercises real
PostgreSQL for all three tables and history, including SQL LIMIT/OFFSET and
denial before another command. The actor boundary is controlled; data/storage
operations are real.

`PermissionUpdateContractTests` defines actor/scope and payload rejection before
storage, authorized scope handling, required collaborators, cancellation and null
requests. `UseCaseOperationCheckpointTests` defines nested rollback preservation
of the earlier operation event and restoration of uncaptured state.

`PermissionUpdateContractPostgreSqlTests` defines actual-host DI resolution and
all three persisted updates, native payload/deny/identity preservation, exact
scope/actor evidence, cache versions, stale/cross-tenant/deleted rejection,
post-persistence audit failure rollback, caller savepoint/event preservation,
pending-work rejection, concurrent expected-version updates, caller-disabled change
detection and cancellation after a real security-version write.
Its injected failure wraps the real audit repository after a successful write;
it does not replace grant/version/audit persistence with an in-memory fake.

No local SDK, test, database, application or browser execution was performed under
the current execution hold. These definitions have not yet been accepted by
matching-source hosted CI. Source/diff review is not a passing runtime receipt.

## Work required before final acceptance

| Original area | Existing evidence / remaining action |
|---|---|
| Comprehensive signatures / all three types | Preserve all existing create/check/generic/bulk methods; qualify the added typed reads. |
| Async/error contracts | New reads carry cancellation and typed validation/access failures; qualify provider failures and native consumer compatibility. |
| Generic resource operations | Existing generics retained; new resource-page method retains the mapped permission/resource constraints. |
| Bulk operations | Existing tenant bulk grant and bulk/stream check seams retained. Reconcile the explicitly requested bulk revoke surface and its outcomes. |
| Evaluation/checking | Preserve the existing effective-permission and deny-first engines. New records must never become an authorization shortcut. |
| Audit/history | New exact stored-history query implemented; native actor/tenant/privacy acceptance remains pending. |
| Filtering/pagination | New database-side read paths implemented; native SQL execution and boundaries remain pending. |
| Dependency injection | Production aliases authored; qualify actual registration and consumers. |
| Tests/mocks/XML/validation | New definitions and XML contracts authored; compile and inspect their executed results before acceptance. |
| Result pattern | New read/update paths expose the canonical Result type. Finish the other mutation/result contracts without breaking legacy returns. |
| Interface versioning | Additive V2 preserves current consumers; document its final mutation surface before declaring the version complete. |

The concrete compatibility service already has revoke methods that are absent
from its old interface, plus default setters and mutation audit/version helpers.
Before exposing a complete mutation contract, inspect every original CRUD and
bulk-revoke operation, declare/adapt the correct existing implementation, and
qualify the new update metadata behavior. Enforce actor/tenant/global-default
authorization **before** a mutation, preserve version/audit and transactional
failure semantics, and exercise native rejection/rollback cases. Optional
collaborators on older construction sites are not proof of production mutation
authorization. No mutation acceptance is inferred from this read increment.

The old create/default/revoke helpers are not certified by the new guarded update
path. Their remaining guards, typed mutation results, exact retrieval and bulk
revoke outcomes must still be reconciled before claiming complete CRUD coverage.

Do not close the issue or mark all its checkboxes complete from this increment.
