# Code assessment prerequisite for PR #699

The owner explicitly requires correction of the Code assessment flow before merging
PR #699. The earlier merge exception for PR #697 does not apply. PR #699 remains
unmerged until this complete flow is accepted.

## Acceptance sequence

1. Register a versioned Code adapter through the existing assessment execution
   contracts. Validate authoring and response files, materialize an immutable private
   projection, and expose only public material in the learner delivery.
2. Publish Code content through revision preparation and official capability
   validation. Attempts bind to the published revision; later authoring changes must
   not alter an existing attempt.
3. Execute the frozen public and private test plan in a trusted server worker using
   the existing Emception WebAssembly toolchain. Bound input, output, runtime and
   concurrent execution. Infrastructure errors do not award a grade. Learner supplied
   scores or test reports are rejected.
4. Move the learner and instructor consumers to the existing official submission,
   review and release endpoints. Preserve submission restoration and instructor
   feedback. Public test execution in the learner IDE remains a preview.
5. Verify focused contracts, PostgreSQL HTTP ownership/revision/privacy/finalization
   cases and the actual instructor/learner browser cycle. Keep all failed runs and
   their source identities. Merge only after the coherent revision passes required
   checks and primary work preservation is verified.

The generic graded-submission guard, review methods and existing browser assertions
are acceptance requirements and remain enforced.

## Runtime configuration

Code uses the existing official grading transaction and requires a trusted worker.
The API does not accept browser test reports or fall back to client scores when
the worker is unavailable. Prepare/publish can validate the executable contract;
operational acceptance additionally requires a configured worker and the real cycle.

Build its browser runtime from the same checkout with
`node grading/build.mjs` in `tools/emception`. Retain that checkout's grading
scripts and dependencies alongside its generated `artifacts/grading/runtime`.
Install the matching Playwright Chromium and use an unprivileged account capable
of running its sandbox. Supply these API configuration values as absolute paths:

| Configuration | Required value |
| --- | --- |
| `CodeGradingWorker:NodeExecutable` | Installed Node executable |
| `CodeGradingWorker:ScriptPath` | `tools/emception/grading/worker.mjs` |
| `CodeGradingWorker:RuntimeDirectory` | Built `tools/emception/artifacts/grading/runtime` |
| `CodeGradingWorker:CdnDirectory` | Verified Emception release CDN containing `manifest.json` |
| `CodeGradingWorker:BrowserDirectory` | Optional Playwright browser installation directory |
| `CodeGradingWorker:DeadlineSeconds` | 1–300; default 300 |

Version 1 freezes artifact version `4.4.0`, ABI `emception-browser-v1` and
canonical lock hash
`bb4e8ca4a8cc4640ec8f7f1d2f7dc14829992e44ff0309527a44b8a83d0b14ed`.
The worker requires a schema-v2 manifest with matching identity and valid release
receipt/fingerprint metadata. Retain these artifacts for existing attempts. A
compiler update requires another adapter version. Legacy local manifests cannot
be relabeled to satisfy this requirement.

Each API worker admits one active execution, with a five-second slot wait. It
limits request bytes, file/test counts, pipe output and browser console output;
the owned process is terminated on cancellation or deadline. Node and Chromium
JavaScript heaps are bounded, but this is not a guarantee of a 512 MB total
process or WebAssembly memory ceiling. The enclosing deployment must provide its
normal OS/container resource limits. Compilation and student execution stay in
the browser virtual filesystem; the process receives a credential-free environment,
and its browser can reach only the local static runtime/CDN server.

The Code submit transport waits up to 330 seconds for the bounded server result.
Failed execution rolls back the academic response, grade and command receipt.
The native browser driver builds/configures this same worker automatically. This
does not configure an existing production API deployment.

The browser grade check still requires exactly 100 points. It uses the existing
`scoreValueFromPoints` helper to compare the API's hundredths (10,000 units), as
the rubric and the official review endpoints do. Treating 100 wire units as 100
points would accept only one point and contradict the academic value contract.
No acceptance assertion is removed or treated as an expected failure.

## Evidence checkpoints

- Published `11186bd6d3aaf241dbf38276661a11bf49adc1ab` passed PR Verify
  run `37534149942` with 5,959 main API cases and 15 separate OpenAPI cases.
  These are prior-source evidence, not acceptance of this Code increment.
- Native Emception run `37534149877` finished on 2026-10-06: toolchain build,
  receipt checks, packaging and cache save passed; the published Code consumer
  had eight failing assertions in its 24-assertion cycle. This failed run is retained.
- The initial repaired consumer suite passed 47 cases in four files; four
  additional action tests verified the enrollment/idempotency route and Code
  transport deadline. The complete initial assessment suite passed 424 cases.
- Six PostgreSQL HTTP cases passed with private-data redaction, frozen content
  and rubric, authorized/idempotent review, criterion-score restoration, release,
  malicious response rejection and unavailable-worker rollback.
- A local legacy-WASM diagnostic returned the expected `[true,true,false]` for
  two public cases and a deliberately failing private case. It predates the
  verified manifest requirement and is not acceptance of the pinned release.
  Contract/worker failures preceding that diagnostic are retained as well.

Fresh coherent-source tests and the real pinned-release browser cycle remain
required before merge. All local receipts, raw logs, TRX files and native
diagnostics are retained under `artifacts/test-results/issue-263-refresh-lifecycle-20261005`
(the downloaded native report also exists under `artifacts/code11186native`).

The coherent local assessment run passed 426 cases, focused PostgreSQL HTTP 21,
Authentication 2,316 and Authorization 1,667; full Web and generated-client type
checks, generated-client build and all 51 focused consumer/action tests passed.
The full solution built with warnings treated as errors (zero warnings/errors).

The local complete API-unit repetition timed out before its cleanup worker
created the initial-delay timer. Its failed testhost was explicitly identified
and interrupted after 1,006 received results (1,005 passed/one failed), and the
complete run remains unaccepted. All 20 cleanup worker cases passed separately
on the same compiled source. This points to competing test initialization;
the exact cause is not certified by the isolated pass. The class now uses the
existing nonparallel lifecycle infrastructure collection, keeping every deadline
and shutdown assertion unchanged. A fresh complete API-unit run and native CI
are required for acceptance; the failed/partial run is retained.

The corrected source rebuilt with zero warnings/errors. Its 20 cleanup worker
cases passed again. A fresh complete API-unit repetition uses an explicitly owned
loopback PostgreSQL instance and remains pending at publication preparation.
Source receipts confirm that the collection annotation is the only API file
change since the 426/21/2,316/1,667 passing runs; production and those suites'
sources are unchanged. Those results do not replace the complete API-unit run.

The native driver syntax and actual shared score helper were also checked with
Node 22.23.3, matching the CI's Node 22 major. The helper returned 10,000 units
for 100 points. The Emception compiler cache inputs remain unchanged. Publication
preservation verified all 55 primary dirty files, without byte/status changes or
overlap, with four local branches, three worktrees and no stashes. Publication
receipts and the source bridge are dated 2026-10-07 UTC under the same artifact
directory. These preparation checks do not accept the pinned browser cycle.

## Published Code increment and subsequent correction (2026-10-07 UTC)

Commit `29b916006735c541f70eb404d7c1a238e069d21f` passed the native API job
`112567889143` in PR Verify run `37551488704`: 393 integration, 1,079 API unit,
2,316 Authentication, 426 Assessments, 384 Notifications and 1,397 SharedKernel
cases (5,995 main cases), plus 15 separately recorded OpenAPI HTTP cases, with
zero failures or skips. Generated-client consistency and CodeQL also passed.
The complete PR Verify run failed on Web lint and is not an accepted merge gate.

The corresponding local full API-unit repetition also finished with all 1,079
cases passing. Its owned PostgreSQL cleanup request exceeded its initial timeout;
a subsequent exact-container check confirmed absence and a healthy Docker daemon.
Both the timeout and cleanup confirmation are retained. The earlier partial API
run remains failed evidence and is not replaced or erased.

Native Emception run `37551488773` restored and verified the concrete compiler
cache, then failed its 24-entry browser cycle. The learner edited code and passed
both public tests; official submission returned a trusted-worker failure and
rolled back, leaving the attempt in progress. Instructor/final-grade assertions
subsequently failed. Its report, screenshots, API/Web logs and source identity
are retained in `artifacts/code29bnative`. This is not acceptance of Code execution.

The subsequent correction removes synchronous effect resets by remounting the
learner/instructor session when its enrollment/submission changes, restores
runtime state in asynchronous callbacks, and keeps JSX outside data-decoding
exception handlers. Two regression cases cover switching attempts. The initial
corrected consumer suite passed 53 cases; full Web lint and types passed without
new lint suppressions. Two resolved baseline suppressions were removed.

The full local Web repetition recorded 2,991 passes and six failures: four page
tests still mocked the legacy submission route, and two unrelated chart-rendering
tests timed out. All original seven Code-page checks are retained and use the
revision-bound runtime contract; an eighth verifies that current authoring cannot
replace the frozen delivery. Those eight page cases passed separately. The full
suite and timeout repetitions still require current-source acceptance.

The worker launcher now requires existing, fully qualified deployment paths and
the installed `node`/`node.exe` and `worker.mjs` filenames. Installation directories
are passed as literal arguments with shell execution disabled. Learner files stay
in bounded stdin JSON. Path validation, deadline and cancellation cases are added
to the assessment suite. Infrastructure logging accepts only a fixed phase/kind
contract, discarding raw stderr, private test names, source paths and error text.
The Node 22 worker build passed type checking and all 18 binding/diagnostic cases,
including the actual worker's fail-closed missing-installation path. These checks
do not identify or repair the native execution failure by themselves.

An intermediate local rebuild rejected two new test fixtures for missing required
definition members (eight compiler errors, zero warnings); those fixtures were
corrected. This failed build is retained. Fresh coherent compilation, security
and HTTP tests, native checks and the pinned browser cycle remain required before
merge. No browser sandbox, acceptance assertion or review policy is weakened.

The corrected source then rebuilt with zero warnings/errors and passed all 451
Assessments, 2,316 Authentication and 1,667 Authorization cases with unchanged
source receipts. All eight chart-rendering cases passed in the isolated repetition.
Full Web lint and types passed after the Code-page mock correction. The concurrent
focused HTTP repetition reported a 409 on a Quiz grade-release case with the
database provider's likely-transient-failure message; that run remains unaccepted
while it finishes and is investigated. A fresh full Web repetition is also still
running at this subsequent publication checkpoint. Those pending checks and the
real native worker execution are explicitly required before merge.

The complete current-source Web repetition subsequently passed all 2,998 cases
in 422 files. The native Web job `112584553349` on `5bfce848` also passed lint,
types, all 2,998 tests and the production build. The focused HTTP repetition
passed all 21 test assertions but exited with a PostgreSQL test-collection cleanup
failure in Docker.DotNet's container removal. Its exit code remains unsuccessful;
the passing assertions do not turn that run into an accepted integration gate.

### Reviewed launcher finding

Codacy's remaining command-injection finding is the syntactic GitLab rule
[`csharp_injection_rule-CommandInjection`](https://gitlab.com/gitlab-org/security-products/sast-rules/-/blob/main/csharp/injection/rule-CommandInjection.yml).
It flags any nonliteral `ProcessStartInfo.FileName` initializer, without tracing
input provenance or the separate validation. `ApiProductComposition` binds these
options from server deployment configuration; no learner endpoint binds or writes
them. `CreateStartInfo` requires an existing absolute `node`/`node.exe` file, an
existing absolute `worker.mjs`, and existing absolute asset directories. Shell
execution is disabled, arguments use `ArgumentList`, and learner files are bounded
JSON on stdin. The configuration tests cover a path containing spaces and a shell
metacharacter, relative/missing/unexpected files, deadline bounds and cancellation.

The exact finding is therefore recorded as a reviewed false positive with a
single-line annotation naming that rule. Other rules and occurrences remain
enabled. This annotation neither accepts a native execution failure nor removes
the requirement to verify the sandboxed worker and complete instructor/learner
cycle. The test fixture also rejects path fragments before creating its local
files, and the finite diagnostic allowlist retains its same behavior.

### Native sandbox startup correction (2026-10-07 UTC)

The published `5bfce848` revision passed PR Verify run `37556681806` in full:
6,020 main API cases, 15 separately recorded OpenAPI HTTP cases, all 2,998 Web
cases, lint, types, production Web build and generated-client consistency. The
API build had zero warnings/errors, and CodeQL and GitGuardian also passed.
Its Emception run `37556681734` nevertheless failed the Code cycle with 15 green,
eight red and one observed entry among the original 24. The strict infrastructure
diagnostic identifies the actual failure: exit 1 during `browser startup`, with
`browser-sandbox-unavailable`. Official submission returned 409 in 1,205.7 ms
and remained uncommitted. The complete report and runtime logs are retained in
`artifacts/code5bfnative`; this is not accepted Code execution.

The correction adds the deployment-only option `CodeGradingWorker:BrowserChannel`.
It allows `chrome` or an unset value, rejects other channels/flags, and passes
the configured value through the worker's curated environment. Sandbox policy
remains fixed at `chromiumSandbox: true`, with the same fresh context, blocked
external requests/WebSockets/downloads and execution budgets. Learner requests
cannot select a browser, executable, command argument or sandbox policy.

The Ubuntu GitHub Code-cycle driver selects the runner's installed Chrome. As
documented by [Chromium](https://chromium.googlesource.com/chromium/src/+/main/docs/security/apparmor-userns-restrictions.md),
Ubuntu provides an AppArmor user-namespace profile for the installed stable binary
at `/opt/google/chrome/chrome`. The installed Playwright 1.63.0 registry resolves
the `chrome` channel to that exact Linux path; its argument builder retains the
sandbox when `chromiumSandbox` is true. This uses the existing installed browser
and profile; no host AppArmor/sysctl setting or browser isolation rule changes.
The native cycle must verify compatibility and grading before acceptance. Local
Linux runs can select the same distribution with
`CODING_CYCLE_GRADING_BROWSER_CHANNEL=chrome`; ordinary API deployment uses the
typed option above. An absent or incompatible installation remains a failed run.

The intervening local scanner-correction build hit its process-local 4 GiB heap
limit in the Roslyn analyzer, recording an `OutOfMemoryException`, zero warnings
and 39 error lines. Its failed receipt is retained and no tests from that build
are accepted. A new coherent build uses an 8 GiB limit scoped to that compiler
process, with warnings still treated as errors. Fresh configuration, worker and
HTTP regressions plus matching-head native gates remain required.

The sandbox-channel correction subsequently rebuilt the complete API solution
with zero warnings/errors. Unchanged compiled-source repetitions passed all 458
Assessments, 2,316 Authentication and 1,667 Authorization cases. The 21 Code/Quiz
and canonical-enrollment HTTP cases passed with an explicitly owned, loopback-only
PostgreSQL instance; cleanup succeeded and the exact container was confirmed absent.
The Node 22 worker build passed TypeScript, Vite and all 28 binding, diagnostic and
browser-configuration cases. Both supported browser configurations require the
sandbox; unsupported channels, objects and injected command flags are rejected.
These results are local contract/HTTP evidence, with the grading worker simulated
in the HTTP fixtures. Real native browser grading remains an acceptance requirement.

The complete 1,079-case API unit repetition also passed on those exact compiled
source bytes, with zero failed/skipped tests. Its separately owned PostgreSQL
instance was removed successfully and confirmed absent. The local source receipts
are retained before publication; matching-head native acceptance remains pending.

### Native instructor feedback failure (2026-10-07 UTC)

Published head `89e3d340` passed PR Verify `37561902348`: 6,027 main API tests,
15 separate OpenAPI HTTP tests, 2,998 Web tests, lint, types, production builds,
generated-client consistency and the required aggregate gate. CodeQL, GitGuardian
and the subsequently completed Codacy GitHub check passed on that same head.

Its Emception run `37561902334` failed the original Code cycle with 35 green,
one red and two observed entries among 38. The sandboxed worker accepted the
official submission. The submitted source, all three instructor tests including
the private test, the frozen rubric, grade 100 and persisted rubric scores passed.
The only failed assertion was `submission feedback non-null`, with `feedback=null`.
Later browser smoke and consumer builds were skipped by that failure. This run
does not accept the complete Code flow; its report and runtime logs are retained.

Instructor resolution currently replaces the safe automated feedback with an
optional empty instructor value. The preceding persisted Code item still contains
the worker's redacted feedback. Focused regressions cover retaining that trusted
item feedback when instructor input is null, empty or whitespace, explicit
instructor feedback taking precedence, idempotent replay, original input retained
in instructor evidence, publication to the learner and exclusion of private tests.
Manual-only Code and Quiz reviews keep their existing optional-feedback behavior.
The native assertion, grading policy, rubric and sandbox requirements are retained.

The focused regression baseline compiled cleanly and executed eight cases before
the production correction: three passed and five failed specifically on erased,
empty or whitespace feedback. Its failed receipt is retained. The correction
uses only a preceding automated Code item with the frozen handler key/version;
it does not manufacture feedback for manual-only Code or inherit Quiz feedback.
Instructor evidence still stores the original submitted comments and previous
result separately. The unit harness simulates only the compiler boundary. The
PostgreSQL HTTP cases exercise manual review, privacy, persistence and release;
the unconfigured-worker case verifies refusal to commit. Fresh native compilation
and browser grading remain required for the actual Code execution claim.

The corrected source rebuilt the complete API solution with zero warnings/errors
and passed all 466 Assessments, 2,316 Authentication and 1,667 Authorization tests.
All 23 focused PostgreSQL HTTP cases passed with zero failures/skips, successful
cleanup and verified absence of the owned loopback-only container. Raw source
fingerprints remained identical to the warning-clean build throughout these runs.
The complete API unit repetition is running against those same compiled files;
its result and a fresh matching-head native Code cycle remain pending at publication.

The complete local API unit repetition then passed all 1,079 cases with zero
failures/skips, unchanged compiled-source fingerprints, successful owned-database
cleanup and verified container absence. On published `c04f92abf`, native Web passed
all 2,998 tests, lint, types and the production build; all four CodeQL analyses
passed. Native API and the real Code cycle remain running at this checkpoint.

Codacy reported two unused parameters in the simulated unit executor. The follow-up
uses those inputs to assert the frozen toolchain, retained private definition and
submitted student source; it returns one simulated pass per frozen test. This
changes only the test fixture, retaining production behavior and native assertions.
Its warning-clean rebuild, affected-suite repetition and fresh scanner result are
required before acceptance. No analysis rule or protection is disabled.

### Scanner fixture validation and native checkpoint (2026-10-07 UTC)

The first scanner-fixture rebuild failed because the abstract test base has no
`Stdout` member. That failed run is retained. The fixture now asserts the concrete
`StandardTest` type before checking its expected output. The complete solution
rebuilt with zero warnings/errors and all 466 Assessments cases passed again.
The raw-source bridge verifies that only this unit fixture changed among 6,486
API source/test files. Authentication (2,316), Authorization (1,667), API unit
(1,079) and focused HTTP (23) are retained earlier executions against byte-identical
production and their own test source; they are not described as freshly rerun.

Published production head `c04f92abf` passed the entire PR Verify run
`37566083147`: 6,037 main API tests, 15 separate OpenAPI HTTP tests, 2,998 Web
tests, clean builds, lint, types and client consistency. Its actual instructor
and learner Code step passed in Emception run `37566083150`; subsequent C++ smoke
and consumer/package steps remain pending at this checkpoint. The fixture follow-up
requires fresh matching-head scanner/native gates before merge. Neither the
successful Code step alone nor historical suite results certify the final head.

### Accepted Code step and library startup correction (2026-10-07 UTC)

Published revision `f330d458239041624a0eed617a4c68fde883d295` passed the complete
PR Verify run `37569861109`: 6,037 main API cases, 2,998 Web cases, clean builds,
lint, types, generated-client consistency and the required aggregate gate. The
15-case OpenAPI gate repeats existing integration cases; it is recorded separately
and is not added to the main total. Codacy, GitGuardian and all four CodeQL analyses
also passed. Fresh local Authentication and Authorization repetitions passed 2,316
and 1,667 cases against the same warning-clean compiled source.

Emception run `37568712584`, job `112622655276`, passed the genuine Code step:
36 green, zero red/known-red and two console observations among 38. Official trusted
submission, private-test separation, instructor execution, the frozen two-criterion
rubric, grade 100 and persisted non-null feedback/rubric scores all passed. The two
observations report the learner's Monaco worker loading warning and no instructor
console errors; they are retained rather than reported as assertions.

That workflow subsequently failed before C++ browser assertions, at the unchanged
300,000 ms web-server startup deadline. `ensure-emception-libs.mjs` attempted to
create `.cache/emception-libs.lock` without first creating `.cache`; its blanket
catch converted `ENOENT` into a silent lock collision. The original helper also
left its acquired lock after a failed build and could build/remove a foreign lock
after its own wait deadline. Five focused real-CLI regressions exercised those
three failures and two passing controls before the correction.

The correction creates the cache parent, treats only `EEXIST` as a collision,
logs a wait, fails closed on the original wait deadline, and releases only its
acquired directory through `finally`. Build failure propagates a nonzero exit
without bypassing cleanup. All five corrected cases passed on Node 24.19.0 and
Node 22.23.3; all 74 existing/new script cases passed on both versions. The real workspace
root library build also succeeded and released its lock. The foreign-lock fixture
shortens only its copied helper's wait to exercise ownership; production and
Playwright deadlines are unchanged. Logs and source hashes are retained under
`artifacts/test-results/issue-263-refresh-lifecycle-20261005/`.

Consumer builds, the C++ smoke and deployable outputs were skipped/failed in the
published run and remain required on the corrected head. No failed native check,
partial workflow, simulated compiler result or predecessor run accepts this
follow-up for merge. Issue #263 remains open until matching-head acceptance and
the authorized merge are recorded.

The local C++ browser repetition reached the corrected predev helper immediately,
then failed because the existing local release CDN has a legacy manifest instead
of schema version 2. No browser assertions ran. That failure is retained without
altering generated release evidence or weakening the manifest check; the native
workflow generates and verifies the current release before running these tests.

### Current Code success and demo manifest correction (2026-10-07 UTC)

Revision `c35197a9ca680ff987b5abf19635f5703cbdda72` passed the complete PR Verify
run `37573171640`: 6,037 main API cases, 15 repeated OpenAPI gate cases, 2,998 Web
cases, the selected builds, lint, types and client checks. Codacy, GitGuardian and
all four CodeQL analyses passed on that revision. Emception run `37573171722`, job
`112636196263`, also completed the genuine instructor/learner Code step with 36
green assertions, zero known-red and two console observations among 38. Private
tests, official submission, frozen rubric, grade 100 and persisted non-null
feedback/rubric scores passed. The retained observations concern the learner's
Monaco worker load and an instructor React update-before-mount warning; neither
is counted as a passing assertion.

The following C++ suite successfully started its development server, then repeatedly
failed to reach toolchain readiness. Its logs show requests to the published
jsDelivr Toolchain 4.4.0 endpoint, including HTTP 404 for the Brotli module. The
Next.js demo's development/build hooks stage the verified local CDN, but the demo
omitted `manifestUrl` when creating the IDE. It therefore used the public package
default instead of that staged artifact. The demo now explicitly selects
`/cdn/manifest.json`, matching the existing self-hosted demo convention. The
public package default, schema/provenance validation and browser assertions remain
unchanged.

A focused staging/consumer regression failed before the correction and passed
after it. All 75 script cases passed on Node 24.19.0 and Node 22.23.3; the Next.js
demo's type check and affected-page lint also passed. The original baseline result
and raw log are retained even though its initial wrapper could not print a Unicode
test marker. The earlier preservation helper also stopped on its blanket script
directory guard because the authorized new regression lives below that directory;
the publication review verifies the original 55-file guard and each actual change
explicitly instead. No primary source was modified.

The C++ execution was deliberately cancelled after its repeated boot failures to
replace it with the corrected revision. Its completed Code report and full raw
job log are retained as partial evidence, not whole-workflow acceptance. The
cancelled C++ suite and skipped consumer, Pages, deployable-output and upload
steps remain unaccepted. Matching-head native and security gates are still required
before merge; issue #263 remains open.

### Native canvas status and public subprocess API correction (2026-10-07 UTC)

Revision `8622414e2272e14952b330cf876fa88af9b585a8` completed the actual Code step
in [Emception run 37580056804](https://github.com/gameguild-gg/gameguild/actions/runs/37580056804):
**36 green assertions, zero red and two console observations among 38**. The
official trusted submission, private-test visibility, frozen rubric, score 100,
persisted feedback and rubric payload all passed. The observations concern the
learner editor worker and an instructor React state update before mounting; they
are retained. This is acceptance of that step, not the complete workflow.

The subsequent C++ runner completed 26 cases: **12 passed, 9 failed and 5 skipped**,
with no flaky or interrupted cases. All retries and failures are retained. Five
failures occurred before actual canvas-build completion because the pending status
was `Compiling SDL3/raylib/allegro...` while the existing assertions require the
same `Compiling...` state as terminal builds. In the recompile case, the differing
status also made the completion wait return immediately. The IDE now keeps the
common pending state; the terminal retains the toolchain name and the final status
retains the canvas label.

Four strict Ninja/CMake cases used the removed private
`window.__emception_client__` hook. They now call the already exposed public
`BrowserEmceptionAPI` through `window.__emception_api__`, including
`api.workspace.writeFile` for the project fixture. They still execute the actual
tools and assert their structured exit codes, output, missing-build-file error
and successful configuration. No private client hook is reintroduced and no
browser assertion, timeout, sandbox or release requirement is relaxed.

Two focused source-contract regressions failed before these corrections and
passed after them on Node 22 and Node 24. The complete local package suite passed
**364 cases, zero failures and one conditional real-worker smoke skip** out of
365; the 25 IDE cases and focused repeats are included subsets, not additional
distinct totals. All six package type checks and the updated strict E2E file's
type check passed. The first isolated E2E type-check invocation stopped at
TypeScript 6's explicit-configuration requirement before checking source; its
diagnostic remains retained, followed by the successful `--ignoreConfig` command.
These local checks do not establish the new canvas or subprocess runtime result.

The failed native log SHA-256 is
`dcd890718aac845b0626c9b7d3a47e28d404c5aa91f7323965fd763feb37bd2c`.
Browser diagnostics artifact `11466658407` was downloaded and retained with
archive SHA-256 `8978612ffdce0b29bac54ffcfa7614471763ec3a050a05e029451d82a2c63afd`.
The initial collector's invalid encoding alias failed before extraction/download;
that preparation failure is recorded separately. Raw native evidence was preserved.
Receipts, individual failure logs and error contexts live under
`artifacts/test-results/issue-263-refresh-lifecycle-20261005/`.

Consumer, Pages, deployable-output and release-upload steps were skipped after
the C++ failure. The complete new-head native and security gates, including the
actual Code and C++ repetitions, are required before merge and closure of #263.

### Native GL import and main-loop startup correction (2026-10-07 UTC)

Revision `c753cb742a062341ae3cea231fe2bee964b84902` passed the complete
[PR Verify run 37589327594](https://github.com/gameguild-gg/gameguild/actions/runs/37589327594):
6,037 main API cases, 15 separate OpenAPI cases and 2,998 Web cases. Its security
checks passed. The actual Code step in
[Emception run 37589327579](https://github.com/gameguild-gg/gameguild/actions/runs/37589327579)
again passed 36 green assertions, zero red and two retained console observations.
The whole Emception workflow failed: C++ finished with 17 passed, four failed,
five skipped and no flaky/interrupted cases. All four strict Ninja/CMake cases
and SDL recompilation passed. Consumers and release checks after C++ were skipped.

Native traces identify three remaining startup defects. Allegro's application
imports `env.glBindBuffer`, while its generated factory supplies
`env.emscripten_glBindBuffer`. SDL's generic missing-import fallback also replaces
these real GL bindings with zero-returning functions, including shader operations.
Raylib starts its browser loop and throws Emscripten's exact `unwind` sentinel;
calling its raw `_main` incorrectly propagates that normal control transfer as an
IDE execution failure. Neither runtime exports `callMain`. The SDL cases also
require the existing runtime-detection message, which the new canvas path omitted.

The canvas adapter now forwards missing C GL names to existing callable generated
bindings for all three profiles, preserving an existing direct binding. Unsupported
imports fail actual WebAssembly instantiation instead of silently returning zero;
asynchronous instantiation errors reject startup and dispose the module URL.
SDL keeps its memory-backed WASI boundary. Only the exact main-loop `unwind`
sentinel is accepted; a real WASM trap still rejects startup. The IDE reports
runtime detection only after the public startup returns a successful session.
All native browser assertions, timeouts, retries and skips remain unchanged.

Six failures were reproduced among nine focused unit/source-contract cases before
the correction, with three passing controls. All nine passed after correction on
Node 22 and 24. The WASM import regressions instantiate and execute a real small
module; its generated JS factory boundary is controlled, so they do not establish
actual graphics acceptance. The complete package repetition passed 371 cases,
zero failures and one existing conditional worker smoke skip out of 372. All 75
script cases passed on Node 22 and 24. An earlier parallel Node 22 script execution
had one process-start out-of-memory failure; that failed receipt remains retained
alongside the successful separate repetition.

The failed native raw log SHA-256 is
`6aed4bfacf8b497abc53844967ace8bd3e330ef49436a987d989e0ab4b1eff2f`.
Diagnostics artifact `11469364484` was downloaded with matching archive SHA-256
`b4708d787b63b7f29f1820f4a7e2a06cc215ff95b930c7c8ed82212cd6f40845`.
The first buffered download timed out; the subsequent streamed transfer completed
without replacing that failed record. All three exact generated runtime pairs
were recovered from the native trace's Brotli bundle and checked against its
schema-v2 manifest file sizes/hashes. These are diagnostic inputs, not a relabeled
local CDN or release waiver. Matching-head complete native and security gates
remain required before merge. Issue #263 remains open.

### Application-addressed runtime glue correction (2026-10-07 UTC)

Revision `7c57f5d008cdfb50b1cf0d80bd6ebc41806b17e6` passed the complete
[PR Verify run 37597311857](https://github.com/gameguild-gg/gameguild/actions/runs/37597311857):
6,037 main API cases, 15 separate OpenAPI cases and 2,998 Web cases. Codacy,
GitGuardian and the four CodeQL language checks passed. The actual Code step in
[Emception run 37597312017](https://github.com/gameguild-gg/gameguild/actions/runs/37597312017)
passed all 22 functional conditions, with 36 green assertions, zero red and two
retained console observations. The whole workflow failed: C++ recorded 16 passed,
five failed and five skipped, with no flaky or interrupted cases. Raylib passed.
Allegro and four SDL cases failed; consumers and release steps after C++ skipped.

The new native errors are concrete: Allegro's `runMainThreadEmAsm` looks up an
application address in the generated stub's `ASM_CONSTS` table and throws a
TypeError. SDL's separately linked applications import `env.exit`, which the
generated factory does not expose. The release glue patch now supplies the
existing Emscripten `_exit` implementation and resolves EM_ASM from the actual
application's memory for all three profiles, including main-thread calls in SDL
and Allegro. An address collision with the stub's table cannot reuse the wrong
function. The previous SDL patch upgrades idempotently; unsupported generated
shapes still fail the release. No generic missing-import no-op is restored.

Patch set `emception-glue-v4` changes the mandatory release glue transformation.
Compiler recipes, lock identity, native cache-key inputs and browser assertions,
timeouts, retries and skips remain unchanged. The normal release command forces
fresh staging, glue, manifest and bundles, retaining their normal hash and receipt
checks. Native release validation must demonstrate that the published patch is
actually consumed; local patched factories are diagnostic evidence only.

The focused baseline retained nine failures and 12 passing controls among 21
cases. All 22 focused cases, including the additional legacy-patch upgrade case,
passed after correction on Node 22 and 24. The complete local package suite passed
383 cases, zero failures and one existing conditional worker smoke skip among
384; all 87 script cases passed on Node 22 and all six package type checks passed.
Each of the three exact native generated factories was checked against its
schema-v2 manifest, transformed by the current patch and verified for syntax and
idempotence. These checks do not certify browser rendering or the complete CI.

The failed native raw log SHA-256 is
`5e1d052b23f53656b898e1b4743fc42c9631a042c1c4746e355569e1f7670108`.
Diagnostics artifact `11472412442` was downloaded with matching archive SHA-256
`69078d9abe38fe90b26ff1ad761acbdb66aff2225c7f34a3d662c4fd0fbbdc24`.
All logs, failure contexts, original runtime pairs and local checks are retained
under the same artifact directory. Current-head complete native and security
gates remain required before merge; #699 and #263 remain open.

Codacy then reported four dynamic-execution findings in the newly added regression
harness on `764a65f71`. The tests now execute their fixed generated fixtures in
bounded Node VM contexts instead of using the `Function` constructor. The same
application-address, collision, exit and legacy-upgrade assertions are preserved;
the production glue transformation is unchanged. Against the exact `7c57f5d00`
production patch, these 12 focused regressions retained ten failures and two
passing controls. All 22 current cases pass on Node 22 and 24. The scanner finding
and both test executions are retained; a fresh successful Codacy result is required.

### SDL3 C abort import correction (2026-10-07 UTC)

Revision `b7326a5317368018879f39c636b61743c10b5f2d` passed the complete
[PR Verify run 37603887674](https://github.com/gameguild-gg/gameguild/actions/runs/37603887674):
6,037 main API cases, 15 repeated OpenAPI HTTP cases and 2,998 Web cases. Codacy,
GitGuardian and all four CodeQL analyses passed. Its actual Code step in
[Emception run 37603887575](https://github.com/gameguild-gg/gameguild/actions/runs/37603887575)
passed all 22 functional conditions: 36 green assertions, zero red and two console
observations reporting no errors. The whole workflow still failed. C++ recorded
18 passed, three failed and five skipped, with zero flaky/interrupted cases.
Allegro and raylib passed; the three remaining failures are SDL3 applications
whose WASM imports `env._abort_js`. Consumers and subsequent release steps skipped.

The generated SDL factory has the genuine Emscripten `abort` implementation but
does not include this C import in its stub-derived map. The mandatory release
patch `emception-glue-v5` supplies `_abort_js: () => abort("")` for SDL, matching
the pinned [Emscripten 5.0.7 libcore implementation](https://github.com/emscripten-core/emscripten/blob/5.0.7/src/lib/libcore.js#L390).
Calling C abort still notifies `onAbort`, reports the error, sets `ABORT` and
throws the fatal WASM runtime error. It is not replaced with a successful return.
The existing C exit binding remains idempotent when the new import precedes it.
No browser assertion, timeout, retry, skip, sandbox or release/hash gate changed.

One focused regression failed on the preceding patch with the actual WASM
linker's missing `_abort_js` error. The corrected probe instantiates and invokes
a real small WASM module. All 23 focused cases pass on Node 22 and Node 24;
the package repetition passes 384 cases, zero failures and one existing
conditional worker smoke skip among 385. All 88 script tests pass on Node 22 and
all six package type checks pass. The exact three native factories were
verified against their schema-v2 manifest, patched, syntax-checked and checked
for idempotence. Only SDL changed; Allegro and raylib remain byte-identical.
The native SDL factory's actual abort function was also executed through the
small WASM probe and preserved its fatal side effects. These controlled probes
are not browser-rendering or complete native acceptance.

The preceding application-addressed checkpoint incorrectly stated that native
cache-key inputs were unchanged. GitHub's implicit-descendant glob behavior
includes edits under `scripts/lib` and `scripts/tests`; those edits change this
workflow's cache identity. Compiler recipes and the lock remain unchanged. The
strict cache-miss forced rebuild and normal staging/receipt/hash/release checks
remain enforced. This correction was also recorded in the PR body before merge.

The failed native raw log SHA-256 is
`975a5c91ba11c70f73c0c98adaab36813d6842b2d18b9c4cc9de5fea22ef99de`.
Artifact `11478680325` was downloaded and verified with archive SHA-256
`519590a5d5a6b387fec02676a6100525cd640c4be8b28ef6a7c6dd23f7d06bf7`.
Archive hashing initially used an unavailable Python API; the completed download
was then verified using streaming SHA-256 before any extraction. That collection
repair does not alter the native test outcome. All failed logs and contexts are
retained. API production/test sources are unchanged. #699 and #263 remain OPEN
until the new matching-head native Code, C++, consumer/release and security gates
pass without a failing-check exception.
