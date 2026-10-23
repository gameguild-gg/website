# Issue 166: capture at every SaveChanges entry point

## Requirement and demonstrated gap

Issue #166 requires automatic capture of every entity modification. The delivered
mapped-property repair (#943) fixed lost snapshots but only the cancellation-token
overload of `ApplicationDbContext.SaveChangesAsync` invoked the application pipeline.
Calls to either synchronous overload or the asynchronous boolean overload inherited
EF's persistence directly, bypassing capture, operation outbox staging, permission
change buffering and version updates.

## Change contract

- Route all four public overrides through one application persistence pipeline.
- Preserve synchronous provider/interceptor dispatch for synchronous calls and
  asynchronous dispatch for asynchronous calls.
- Pass `acceptAllChangesOnSuccess` to EF unchanged. When false, business, audit and
  outbox entries retain their state until the caller accepts them.
- Keep entity snapshots and operation outbox rows in the same database save and
  transaction as the business changes. Return the business affected-row count.
- Preserve actor/tenant/correlation attribution, sensitive-property redaction,
  cancellation, audit-store exclusions, and one version update per save.
- Call the explicit boolean **base** overload once. Calling a convenience base
  overload would dispatch back through the overrides and recurse.

No new HTTP contract, migration, database permission, configuration or client
generation is needed. The synchronous API waits for existing asynchronous permission
delivery when applicable; database persistence and its EF interceptors stay synchronous.

## Hosted verification required

`EntityChangeAuditSaveEntrypointTests` defines eight cases:

- Six combinations of sync/async/default/explicit acceptance check virtual dispatch
  through `DbContext`, provider interceptor selection, one audit record, version
  increment, business row count and explicit acceptance without a second insert.
- Two cancellation cases check that neither business nor audit data persists.

`EntityChangeAuditSaveEntrypointPostgreSqlTests` defines eighteen cases against the
existing migrated PostgreSQL fixture:

- Six lifecycle cases each exercise Added, Modified and Deleted, verify the actual
  business row from a new context, before/after values, redaction, actor/tenant,
  exactly one audit record and operation outbox row, and a no-change second save.
- Six transaction-rollback cases verify business, audit and outbox are all absent.
- Six foreign-key-failure cases verify a pre-existing user's update, the invalid
  membership, both audit entries and the operation outbox cannot partially commit.

These are case definitions awaiting hosted CI. No local SDK, build, test, database,
container or application execution was used. The existing 21 mapped/general capture
cases must continue to pass, together with affected API, authorization, identity and
eventing regressions.

## Full issue remains open

This fixes the SaveChanges entry-point gap. Direct `ExecuteUpdate`, `ExecuteDelete`
and raw SQL still bypass ChangeTracker. The demonstrated guarded promo usage counter
mutations require transactionally accurate before/after audit evidence while preserving
concurrent limits and idempotency. The source inventory also requires reconciliation
of other set-based paths, purpose-specific SQL, dedicated audit coverage and caller
reachability. The performance/concurrency evidence must cover the real persistence
and redaction path; an in-memory benchmark is not PostgreSQL acceptance.

All ten original criteria remain in the issue. This PR does not close or recertify
#166 and does not turn textual search matches into confirmed defects.
