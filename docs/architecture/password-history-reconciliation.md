# Password history acceptance reconciliation — #251

## Requirement provenance

The original body was empty and the title requests prevention of password reuse.
The approved history established current-password and five replaced-hash checks,
both authenticated change and reset, bounded storage, serialization protection and
concurrent update safety. The retained 2026-10-04 source review additionally asks
for fresh migrated storage, capacity/eviction and a racing change/reset execution.
The original criteria and historical [PR #579](https://github.com/gameguild-gg/gameguild/pull/579)
remain preserved; old results alone are not treated as current acceptance.

## Existing implementation retained

- `User.SetPasswordHash` prepends the replaced current hash, retains four older
  entries, replaces the current hash and increments token version. The getter
  caps history at five; EF stores it in the same user record with max length 2,600.
- Both current and historical hashes are excluded from user JSON serialization.
- Change/reset handlers check the current hash plus retained history through the
  actual hasher and use expected-current-hash repository updates. EF treats the
  password hash as a concurrency token. Conflicting updates return a controlled
  failure; only the winning database transition changes committed history/version.
- Merged [PR #687](https://github.com/gameguild-gg/gameguild/pull/687) adds full-input
  long-password hashing while preserving conservative reuse rejection for
  ambiguous legacy BCrypt prefixes. Its [explicit compatibility boundaries](password-hashing-reconciliation.md)
  apply here. No additional password format, schema, retention policy or endpoint
  is introduced by this acceptance increment.

## New acceptance definitions

`PasswordHistoryPostgreSqlHttpTests` adds 16 cases to the existing API PostgreSQL
collection and migrates the real application schema:

| Requirement | Cases and expected observation |
|---|---|
| Current plus all five previous passwords | 12 HTTP cases: each age rejected through change/reset; stored hash/history/token/entity versions unchanged. |
| Evicted password and rotation | Two HTTP cases permit the older evicted input, write a new salted hash and retain exactly the newest five replaced hashes with one version increment. |
| Capacity, persistence and secrecy | One fresh-scope round trip checks mixed BCrypt/PBKDF2 history order, five-entry/2,600-character capacity, applied history migration and no hash serialization/response leakage. |
| Competing change/reset updates | One case schedules real scoped repositories to load the same committed snapshot before real handlers compete. Exactly one succeeds; the loser reports conflict, and final history/token/entity versions reflect only one transition. |

Only the race-read timing is controlled. Database, hasher, handlers and repository
updates remain real. HTTP change uses the existing fixture principal; this does
not establish real bearer identity proofing. Reset tokens come directly from the
real token service, without requesting or delivering email. Delivery acceptance
remains separate under #223/#253.

## Execution status

Two existing capacity/serialization unit cases and the old PostgreSQL concurrency
case passed freshly. The old case uses `EnsureCreated` and is not migration proof.
The temporary 16-case prototype passed against the migrated API fixture; its five
transitive XML audit warnings and earlier setup/compilation failures remain
recorded. Prototype results do not certify a warning-clean actual project build.

The cases are now in the actual API test project, whose Release build passed with
**zero warnings/errors**. The complete 180-case API integration suite passed with
zero failures or skips, including all 16 new cases in the actual project.
The full Release solution builds with zero warnings/errors. After that build,
Authentication (2,114), Authorization (1,667), SharedKernel (1,371) and the focused
16 history cases passed again. The focused rerun is already included in the full
180-case integration coverage and is not added again. Current core/integration
coverage is 5,332 cases; retaining the unchanged-source full API unit (1,049) and
client (1,115) receipts from #687 yields a **7,496-case local footprint**. The latter
two suites are explicitly prior same-source evidence, not new executions in this
increment. The three old controls/prototype runs remain separate.

All applicable checks, Codacy and four CodeQL analyses passed at accepted head
`8a331b98b5c784defe923aac8fcccfb96d8a66d0`. [PR #688](https://github.com/gameguild-gg/gameguild/pull/688)
merged into `develop` at `8dfd092ff578c27e224d6c9f3154697b3d222fd2`.
**#251 is officially CLOSED/COMPLETED**, with [individual acceptance](https://github.com/gameguild-gg/gameguild/issues/251#issuecomment-5988663714).
The actual CI selection passed 180 integration and 2,114 Authentication cases
(**2,294 main cases**), plus 14 repeated OpenAPI executions. Both CI builds have
zero warnings/errors; unaffected client/migration gates were skipped by classification.
Original acceptance fields remain intact. Current receipts are retained in
`artifacts/test-results/issue-251-history-20261005/actual-project-proof.json`.
