# Persisted refresh token lineage — #263

## Accepted partial increment after merge

PR #695 merged to develop at `7952eaf967003e536b8ff736509400660d6dd2da` after all applicable checks
passed for `e49579db56abf04ffef5a0fb54e42c93fd573a60`. Current-head CI passes4975 main
cases plus15 repeated OpenAPI; combined CI/local coverage is7761 distinct
.NET/SDK cases/37 new cases. The reviewed migration test passes fresh PG73 and
OpenAPI15 after its constant-SQL/explicit-parameter repair. Unchanged production,
unit and SDK sources and receipt hashes are verified. Full solution build is
warning/error-clean; migration, OpenAPI/client, Codacy and all four CodeQL
analyses pass. Full spec remains equal1297/1656; no pending model changes.
[Official bounded acceptance](https://github.com/gameguild-gg/gameguild/issues/263#issuecomment-5998430026). #263 stays OPEN; all19 criteria
and the remaining acceptance below are retained. Publication-stage statements
below remain historical and are superseded for this increment by this evidence.

## Scope and implementation

All 19 original issue criteria remain authoritative. This increment adds persisted
`ParentTokenId` and `SessionId` to credential authentication and local refresh
rotation. It does not close #263 or infer acceptance for every issuance provider.

The nullable GUID columns reference the actual predecessor token and session with
restrictive foreign keys and indexes. Migration
`20261005143331_AddRefreshTokenLineage` leaves unknown historical bindings null.
No binding is inferred from user IDs, timestamps or historical hashes. A legacy
predecessor is bound only during an observed successful owned-session rotation.

The required `IRefreshTokenLineageRepository` resolves the same configured scoped
token repository. Session creation and refresh bind the stored credential after
persisting the session. An issued credential without a stored token fails; manual
sessions created without a refresh credential retain their existing behavior.
Rotation records the predecessor only after winning the existing atomic claim.
The repository validates stored owners, matching successor/session hashes, active
state, expiry and compatible existing links before writing metadata.

SQL atomic claims bypass EF tracking. Fresh stored parent, child and session
state is read without tracking; only metadata is changed on attached/tracked
token rows. Stale tracked flags must neither authorize a revoked successor or
inactive session nor undo an already persisted parent revocation.

Required write failures cannot return successful authentication credentials.
Credential login retains its generic401 response; refresh failure returns500.
Actual host command transactions roll back binding, replacement, session hash
and revocation mutations after the injected fault is confirmed to have occurred
after a real lineage write. Refresh cancellation propagates. Login credentials
are generated inside the normal guarded authentication flow.

Retention removes eligible leaves before predecessors. Retained descendants keep
their known ancestry, and retained tokens keep their referenced session. Once
the complete chain is eligible, leaf-first cleanup permits all token rows and
then their expired session to be removed without dangling links. Downgrade drops
the metadata and its constraints while preserving token rows and revocation
state; new lineage metadata is lost on downgrade.

## Executed local evidence

Fresh source passes **6,621 distinct .NET/SDK cases**, including **37 new cases**:

| Suite | Passing cases |
| --- | ---: |
| Authentication unit | 2,241 |
| Authorization unit | 1,667 |
| SharedKernel unit | 1,377 |
| Actual PostgreSQL/HTTP refresh, lineage and revocation | 73 |
| API architecture, security, registration and eventing | 129 |
| Full application OpenAPI HTTP | 15 |
| SDK | 1,119 |

The PostgreSQL cases include actual credential login/two rotations, legacy
rotation, forced simultaneous requests, fresh-state denial, owner isolation,
real post-write fault rollback, restrictive foreign keys, retention and a real
legacy-row migration upgrade/downgrade/reupgrade. Full solution builds pass with
zero warnings/errors; EF reports no pending model changes. The entire OpenAPI
document remains equal to accepted #694 (1,297 paths/1,656 schemas); SDK types
and generated-client consistency pass. Matching-head CI and merge are pending.

Failed intermediate receipts are preserved separately and excluded: the two-case
stale-state baseline, initial cancellation-fixture attempts, the outdated unit
expectation and the fault test's original status expectation. The corrected fault
test additionally proves the injected failure happened after persistence, so an
earlier credential denial cannot satisfy it. Incomplete builds and the explicit
4GB compiler heap failure are excluded. The successful full build keeps analyzers
active with a local16GB heap cap/four reported processors; no repository analyzer
policy is changed. Detailed logs, TRX hashes, source hashes and proof are retained
under `artifacts/test-results/issue-263-lineage-20261005/`.

## Remaining acceptance

The fixed inventory retains all328 IDs and original criteria,70closed/258open at
the latest live verification. #263 remains OPEN for all-provider issuance
metadata/hashing, complete operation audit and reuse alerts, scheduled retention
and cleanup, metrics and remaining original policy/documentation requirements.
Observed Web3 cache, plaintext duplicate and absent account/session failures in
#291/#292 are separate unresolved boundaries; local credential evidence does not
establish their acceptance. Further work reuses the isolated checkout and
preserves all55 unrelated primary files; no agents are used.
