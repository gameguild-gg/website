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

Each new public cancellable operation has explicit overloads with and without a
token. Convenience overloads forward `CancellationToken.None`; the explicit-token
signature remains available for cancellation and mocks. This replaces optional
parameters flagged by the first hosted Codacy review. Framework overrides retain
their required parameters and mark intentionally unused inputs explicitly.

## Current read increment

- `IPermissionServiceV2` inherits the existing three-layer contract plus
  `IPermissionReadService`, `IPermissionUpdateService` and
  `IPermissionLifecycleService`; the concrete service implements each. Production
  DI aliases the old contract, V2, read/update/lifecycle capabilities and concrete type to one instance
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

## Exact stored-grant retrieval

`PermissionGrantLookup` selects an existing grant ID and the same explicit
tenant/subject scope, without irrelevant list pagination. Three typed get methods
perform one untracked database lookup with identity, tenant, subject and deleted
predicates applied in SQL. Inactive/expired metadata remains visible to an
authorized reader; it does not become an allow decision. Missing, foreign-subject,
foreign-tenant and deleted records return NotFound within the selected scope.
Explicit unauthorized subject/tenant selection fails before storage. An
administrator still selects another subject, all users or default rows explicitly.
Provider errors and cancellation are not converted into NotFound.

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
audit buffer and per-entity change history; a null global target does not inherit
the caller's tenant. The original grantor remains immutable and is not attributed
as the actor who performed an update.

No automatic replay is introduced: these mutations have no idempotency key, and
an ambiguous commit error propagates for reconciliation. The execution wrapper
uses EF's execution-strategy scope with zero retries to avoid an inner provider
replaying individual writes; an already active outer strategy retains ownership.
See [EF connection resiliency and commit ambiguity](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency).

`AddPermissionServiceContracts` is called by the actual host infrastructure setup
as well as the optional presentation registration. The host previously called
Authentication application/data registration without the presentation entry point.

## Create, delete and complete batch contracts

Typed creation takes an explicit user or an explicit default-subject selection.
Tenant allows retain custom string keys; content/resource allows retain native
defined enum values and exact bindings. A non-deleted subject/binding already
present returns Conflict: changing that grant uses the version-checked update
contract rather than silently reactivating or extending it. The authenticated
actor supplies the grantor. Delete soft-deletes the complete identified grant,
including its allow/deny state, after checking its expected version. The existing
engine continues evaluating inherited/default permissions after deletion.

All three families expose bulk grant and bulk revoke. A request contains 1-256
ordered items in one authorized scope. Bounds, authority, payloads and duplicate
input bindings are checked before data access. Mutable allow collections are
cloned once and the clone is validated before any await. SQL restricts reads to
the requested subject and type/resource sets; complete binding keys are compared
using a projection of their columns.
The scope version is advanced once per accepted typed batch, and each saved grant
has its own required audit entry and exact snapshot. Reloading saved grants lets
snapshots reflect provider timestamp precision rather than unsaved input ticks.

`Result<PermissionMutationBatchResult<T>>.IsSuccess` describes the result envelope.
Callers must also inspect `Applied` and each indexed item result. A rejected batch
returns `Applied=false`, keeps every original input index and concrete known
failure, and marks otherwise eligible siblings as `BatchNotApplied`. No batch
write survives. Empty/oversized/mixed-scope requests and missing collaborators
are envelope failures; provider errors/cancellation propagate. With a caller
transaction, `Applied=true` leaves final commit to that transaction's owner.

Creation advances the scope version inside its transaction before checking
bindings, serializing competing creators. Existing version-token races and a
unique initial security-version row race return Conflict after rollback. The
latter uses SQLSTATE 23505 only when an added security-version entity participates
in the failed save. Other database exceptions propagate; no automatic replay or
commit-success assumption is introduced.

## Compatible mutation paths

The original signatures and return types remain. Every grant, revoke, default,
membership, share, bulk and cleanup entry checks authority and required version/
audit collaborators. New grants use the actor as grantor. Invalid user/resource
identifiers and undefined permission values are rejected; bulk invalid IDs are
rejected before processing instead of silently omitted. Deleted tenant rows are
excluded consistently with the other two grant families.

On the production relational host, compatible writes use the same atomic executor,
including nested savepoints for old batch/membership calls. A later failure rolls
back earlier batch items, security versions and all staged audit evidence. Existing
non-relational component providers retain their original persistence behavior;
their unit tests qualify model/side-effect behavior, not atomic storage. Real-host
PostgreSQL definitions independently cover rollback through the compatible API.

Cross-tenant expiry cleanup requires SystemAdmin and retains each entity's own
tenant in both centralized and per-entity evidence, including a null global
tenant. It never stamps every affected row with the administrator's tenant.
Required cleanup version writes use stable scope order and skip deleted grants.
The actor-only, per-entity tenant mode is confined to that maintenance operation.

## Verification definitions

`PermissionReadContractTests` defines validation, overflow, cancellation,
authentication, tenant/subject/default/global isolation, stricter history access,
all three grant families, stable page boundaries, inactive metadata opt-in,
single-sided history bounds, exact snapshots, storage failure and production DI
aliasing cases. Native `PermissionReadContractPostgreSqlTests` exercises real
PostgreSQL for all three tables and history, including SQL LIMIT/OFFSET and
denial before another command. The actor boundary is controlled; data/storage
operations are real.

The read definitions additionally cover exact retrieval for all three native
families, inactive/expired details, absent/deleted/other-subject isolation,
explicit administration and global selection, pre-storage validation, provider
failure and cancellation. Native SQL observation requires three scoped selects
for three exact gets, without a count query or tracked result entities.

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

`PermissionLifecycleContractTests` defines all-family lifecycle authority,
payload/subject/lifetime/version validation, batch bounds, indexed duplicate and
validation failures, mixed scopes, one-time payload cloning, required collaborators,
cancellation, legacy mutation guards and restricted cross-tenant cleanup.

`PermissionLifecycleContractPostgreSqlTests` defines actual-host create/delete,
persisted timestamp snapshots, each family's bulk grant/revoke, one version bump
per typed batch, binding conflicts, indexed stale/missing failures, failure after
the second real audit write through both typed and compatible batches, concurrent
creation with and without an existing version row, caller savepoints/rollback,
explicit global default creation, and cross-tenant cleanup attribution. Cleanup
owns a separate migrated test database so it cannot expire another test's rows.
Existing legacy snapshot/cache/model tests retain their positive assertions and
now explicitly configure an authorized actor and audit/version collaborators.

No local SDK, test, database, application or browser execution was performed under
the current execution hold. These definitions have not yet been accepted by
matching-source hosted CI. Source/diff review is not a passing runtime receipt.

## Work required before final acceptance

| Original area | Existing evidence / remaining action |
|---|---|
| Comprehensive signatures / all three types | Preserve all existing create/check/generic/bulk methods; qualify added typed exact/list/history reads and updates. |
| Async/error contracts | New reads carry cancellation and typed validation/access failures; qualify provider failures and native consumer compatibility. |
| Generic resource operations | Existing generics retained; new resource-page method retains the mapped permission/resource constraints. |
| Bulk operations | Typed atomic grant/revoke for all three families authored with indexed outcomes; compatible bulk and bulk/stream checks retained. Qualify native behavior and bounds. |
| Evaluation/checking | Preserve the existing effective-permission and deny-first engines. New records must never become an authorization shortcut. |
| Audit/history | New exact stored-history query implemented; native actor/tenant/privacy acceptance remains pending. |
| Filtering/pagination | New database-side read paths implemented; native SQL execution and boundaries remain pending. |
| Dependency injection | Production aliases authored; qualify actual registration and consumers. |
| Tests/mocks/XML/validation | New definitions and XML contracts authored; compile and inspect their executed results before acceptance. |
| Result pattern | All five core operations and all-family grant/revoke batches expose canonical typed Results; qualify rejection/rollback behavior. Legacy returns remain unchanged. |
| Interface versioning | Additive V2 and focused read/update/lifecycle capabilities authored; qualify current providers, mocks and actual host aliases. |

The complete original CRUD, specialized operations and all-family bulk surfaces
are now represented in source and regression definitions. This does not certify
that they compile or meet functional acceptance. Matching hosted compilation,
all affected/native results, actual consumer compatibility and the full retained
15-criterion review are still required. Optional collaborators on a read-only
construction site never establish production mutation authorization.

Do not close the issue or mark all its checkboxes complete from this increment.
