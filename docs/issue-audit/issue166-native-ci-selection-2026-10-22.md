# Issue 166: native persistence test selection

PR #948 corrected the asynchronous SaveChanges overloads and unit-test interceptor
assertions. Its API job passed, but the affected-test selector scheduled only
`GameGuild.API.UnitTests` and `GameGuild.SharedKernel.UnitTests`. The artifact contains
20 focused unit cases; the 27 mapped-entity and save-entrypoint PostgreSQL cases were
not executed. The separate full-host OpenAPI group passed 16 cases. These results do
not establish the missing native persistence coverage.

## Correction

- Changes under `apps/api/Source/GameGuild.API/Database/` select the full-host API
  integration suite plus API, SharedKernel, authentication and authorization units.
- Changes to the affected-test selector, its tests or its runner select the same
  suites and enable API verification without scheduling a runtime deployment.
- Existing Economy gate selection for classifier changes remains enabled.
- Thirteen regression cases cover individual persistence sources, CI infrastructure,
  Windows paths, deduplication, similar directory names, missing projects and the
  separation of verification from deployment.

## Acceptance

Run these cases in hosted CI. The resulting native artifact must contain all 47
focused audit cases: 8 capture units, 4 mapped-entity units, 8 save-entrypoint units,
9 mapped-entity PostgreSQL cases and 18 save-entrypoint PostgreSQL cases. Review the
artifact's tested commit, tree and parents against the current PR source.

No local SDK, build, test runner or database was used for this change. The local
path-selection preview is metadata evidence only. No API contract or workflow has
been changed and no checks have been removed.

This corrects validation coverage for the delivered increment. Issue #166 remains
open for set-based/raw SQL mutations and the remaining performance/concurrency
acceptance; this PR must not automatically close it.
