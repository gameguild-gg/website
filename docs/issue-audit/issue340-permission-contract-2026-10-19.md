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
  `IPermissionReadService`; the concrete service implements both. Production DI
  aliases the old contract, V2, read capability and concrete type to one instance
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

## Verification definitions

`PermissionReadContractTests` defines validation, overflow, cancellation,
authentication, tenant/subject/default/global isolation, stricter history access,
all three grant families, stable page boundaries, inactive metadata opt-in,
single-sided history bounds, exact snapshots, storage failure and production DI
aliasing cases. Native `PermissionReadContractPostgreSqlTests` exercises real
PostgreSQL for all three tables and history, including SQL LIMIT/OFFSET and
denial before another command. The actor boundary is controlled; data/storage
operations are real.

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
| Result pattern | New read paths expose the canonical Result type. Finish the complete mutation/result contract without breaking legacy returns. |
| Interface versioning | Additive V2 preserves current consumers; document its final mutation surface before declaring the version complete. |

The concrete compatibility service already has revoke methods that are absent
from its old interface, plus default setters and mutation audit/version helpers.
Before exposing a complete mutation contract, inspect every original CRUD and
bulk-revoke operation, declare/adapt the correct existing implementation, and
complete missing update metadata behavior. Enforce actor/tenant/global-default
authorization **before** a mutation, preserve version/audit and transactional
failure semantics, and exercise native rejection/rollback cases. Optional
collaborators on older construction sites are not proof of production mutation
authorization. No mutation acceptance is inferred from this read increment.

Do not close the issue or mark all its checkboxes complete from this increment.
