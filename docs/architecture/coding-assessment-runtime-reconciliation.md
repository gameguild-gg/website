# Coding assessment runtime reconciliation

## Publication checkpoint — 2026-10-07

PR #699 publishes revision `f330d458239041624a0eed617a4c68fde883d295`.
Canonical enrollment resolution and the official Code runtime corrections are
committed. The complete local solution rebuilt with zero warnings/errors and
the final source passed 466 Assessments, 2,316 Authentication and 1,667
Authorization cases. These are suite results, not additional cases to add to
the later complete native regression totals.

PR Verify run `37569861109` passed on that published revision: 6,037 main API
cases, 2,998 Web cases and the required aggregate gate. Its additional 15-case
OpenAPI gate repeats integration cases and is recorded separately, without
adding them to the main total. Security checks passed on the same revision.

Emception run `37568712584` passed the actual instructor/learner Code step with
36 green, zero red and two console observations among 38. The subsequent C++
smoke failed before reaching browser assertions: the demo predev helper silently
waited for a builder lock whose parent directory did not exist. Consumer and
deployable-output steps were consequently skipped. This partial workflow does
not certify complete package/consumer acceptance or complete issue #263.

The helper correction creates the cache parent, distinguishes a lock collision
from other filesystem errors, releases its acquired lock even when a build fails,
and refuses to build or remove another process's lock after a wait timeout.
Its regression baseline was three failures and two passing controls; the corrected
five cases passed on both Node 24 and Node 22. All 74 script cases also passed on
both versions, and the real workspace library build completed with the lock released.
The local C++ repetition then stopped at the existing legacy CDN's schema check;
no browser assertions executed and that local run is not accepted.
The original browser startup deadline and native acceptance assertions remain
unchanged. Fresh matching-head native gates are required for this follow-up.

The superseded predecessor `c04f92abf` also passed the Code step, but its Emception
run was cancelled during C++ smoke. That cancellation remains historical evidence.
See [the retained merge evidence](coding-assessment-merge-acceptance-20261006.md)
for the published fixes, preserved failed attempts and required final gates.

The following sections retain the initial diagnostic evidence; their historical
revision, uncommitted state and pending results describe that earlier checkpoint.

## Initial diagnostic checkpoint — 2026-10-06

At the initial diagnostic checkpoint the published refresh lifecycle candidate
was PR #699, revision `d9309fcf621d3dbef6941b232e1c3dad2b362a20`. The Learning
correction was then uncommitted. That initial record did not certify the
instructor/learner browser cycle or complete issue #263.

The actual SDK/API probe, using the migrated application and disposable
PostgreSQL, first failed because course workspaces return `ProgramEnrollment.Id`,
while assessment membership recognized only `Enrollment.Id` and `ProgramUser.Id`.

The correction resolves canonical enrollment through the application service and
a shared query. It requires an active, nondeleted enrollment in the actual,
nondeleted course. An explicit enrollment tenant must match the course tenant;
historical null enrollment tenant metadata is accepted only through that course
join. Existing actor ownership and course authorization remain enforced.
Individual and collective grading membership use the same predicate. Existing
collective attempts remain group-owned, with each member persisted as a separate
participant; no primary user/enrollment is invented for collective submissions.

| Current candidate acceptance | Result |
| --- | --- |
| Actual HTTP/PostgreSQL canonical membership tests | 11 passed, no failures/skips |
| Complete Assessments unit suite | 396 passed |
| Complete Courses unit suite | 837 passed |
| Complete Workspaces unit suite | 21 passed |
| Complete Release solution | 0 warnings / 0 errors |
| Complete API unit/integration regression | In progress; not accepted here |
| Native coding browser cycle | Not accepted |

The focused HTTP cases reject another owner, another course, inconsistent tenant
metadata, cancelled/expired/paused status and deleted enrollment without storing
an attempt. The complete module total is 1,254 distinct cases. The 11 focused
HTTP cases will be a subset of the complete integration suite, not additional
cases to count twice.

Evidence, failed fixture attempts, corrected expectations, raw logs, actual TRX
records, source hashes and owned-container cleanup are retained under
`artifacts/test-results/issue-263-refresh-lifecycle-20261005/`.
`course-membership-and-code-runtime-reviewed-20261006.json` reconciles the
accepted local membership/build receipts and the still-failing SDK probe.

## Separate Code grading incompatibility

After canonical membership was corrected, the real SDK probe reached a second
HTTP 400: `Content-backed graded assessments cannot start through the generic
submission endpoint.` No attempt was accepted and no browser acceptance is
inferred from this diagnostic.

The rejected generic path is intentional. `AssessmentService` requires content
backed assessments with review methods to use the official grading runtime.
`AssessmentGradingSync` creates Code assessments with automated and instructor
review, but the application currently registers only the Quiz type adapter.
The native consumer still calls the generic start/submit endpoints.

The guard, Code synchronization and Quiz registration are source-identical to
develop `295ee212828ab1cc233ce7ed61f2a2b81eb39ed4`. This is source evidence that
the grading boundary predates the refresh lifecycle change. The original
develop end-to-end flow stops at membership before reaching this second guard;
it is not presented as an independently passing baseline browser run.

## Required integration work

Follow the accepted
[assessment revision ADR](ADR-20260903-assessment-revisions-and-executable-snapshots.md).
The implementation order is:

1. Add and register a versioned Code assessment adapter that projects the
   existing `CodingAssignmentContent` authoring document. Define immutable
   executable items, allowed submission files, delivery projection and response
   decoding. Keep private test material outside learner projections.
2. Integrate Code publication with assessment revision preparation, exact
   capability/version validation and publication. Published attempts must bind
   the immutable revision, execution manifest and snapshot. Author test runs
   retain their distinct execution context.
3. Implement trusted automated Code review with explicit execution limits,
   cancellation and failure semantics. Client supplied test results or scores
   must not become official grades. Keep instructor review, override provenance
   and grade release within the existing grading orchestration.
4. Update learner, instructor and SDK consumers to use official runtime
   start/submit/review/result contracts. Preserve persisted source payload,
   grading queue and the existing coding cycle assertions. Regenerate API
   clients if public contracts change.
5. Verify ownership and tenant isolation, inactive membership, collective
   participants, revision changes after start, private test visibility,
   malformed/oversized responses, execution timeout/cancellation, instructor
   review and grading finalization. Run the actual instructor/learner browser
   cycle and existing native packaging/compiler checks.

The membership correction introduces no public DTO or database schema change.
The generic grading denial, review flags, original CI assertions and accepted
revision requirements are unchanged. PR #697's explicitly authorized CI
exception applies only to that merged PR; it does not accept PR #699.

## Owner decision and Code increment (2026-10-06)

The owner requires correction of the complete Code flow before merging #699.
The implementation and operational acceptance sequence is recorded in
[Code merge acceptance](coding-assessment-merge-acceptance-20261006.md).
Its versioned Code adapter, trusted WASM worker, frozen rubric and official
learner/instructor consumers are under validation. This increment adds optional
staff-only frozen-content/rubric/criterion-score fields and optional instructor
rubric resolution scores; its client must be regenerated from the full OpenAPI
contract. The earlier no-DTO-change statement applies to the membership fix.

The prior published-source native run `37534149877` has now finished. Toolchain
compilation and verified cache save passed, while its old Code cycle failed eight
of 24 assertions. It is not an accepted cycle and does not cover the local Code
increment. The existing assertions remain required on the repaired coherent
revision. No merge exception or issue closure is inferred from focused tests.
