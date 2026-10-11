# Legacy permission facade snapshots — issue #301

## Delivered scope

- Serialize safe, immutable before/after permission-state projections for tenant, content-type and generic resource mutations. Snapshot schema version 1 includes grant identity/scope, stored allow/deny values, activation, expiry, grantor and deletion state. Arbitrary metadata, notes, descriptions, titles and entity navigation properties are excluded.
- New grants have null before-state. Physical deletions have null after-state; tenant soft deletion retains its persisted after-state. Existing public signatures and audit-service contracts are preserved.
- Sharing persists its final expiry in the same operation as the grant, then invalidates and audits that final state once.
- Expiration cleanup captures each grant before mutation, persists the changes, invalidates each affected scope once (including global defaults), and records each affected grant with its subject/resource and before/after state.
- Persistence, invalidation and audit failures propagate. Audit records are emitted only after successful persistence.

## Validation

32 focused native cases cover creation/update in all three layers, partial/full/no-op revocation, default scopes, sharing creation/update/expiry clearing, per-grant cleanup, omitted private fields, trusted actor identity, failure ordering and delegated bulk/membership operations.

Source integrity, preserved public signatures, capture/persistence/audit ordering, cleanup scope handling, one-save sharing and whitespace were checked statically before integration. The native tests remain pending until executed against the published composition in hosted CI. No local .NET SDK, database, application or native browser runner was started under INC-PG.

## Remaining issue scope

Refs #301; all 30 original criteria and the #351/#359 context remain authoritative. This increment covers the legacy PermissionService facade, without certifying all other mutation handlers, audit/security functionality, universal entity snapshots (#176), or the evaluation-system hierarchy/soft-deletion requirements of #360. It does not close these issues.

Matching-head hosted build, Authentication.UnitTests, Authorization.UnitTests and API.UnitTests architecture/security acceptance remain required before merge. The snapshot increment follows the independently accepted PR #797 date-bound/session audit increment.
