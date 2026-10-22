# Agent protocol for Quiz + Grading validation

Use this protocol for audits, regression checks, and diagnosis. Product changes
belong in a separate task after the finding is reviewed.

## 1. Establish scope

1. Read the repository `AGENTS.md`, the executable capability catalog, and
   [`capability-status.md`](./capability-status.md).
2. Record `git rev-parse HEAD`, working-tree status, selected case IDs, and the
   local API/web URLs.
3. Do not clean unrelated resources, reset a database, or use a remote host.

## 2. Run the cheapest reproducer

Start with contracts:

```bash
pnpm grading:validation:contracts
```

For human inspection, keep `pnpm dev:fast` healthy, sign in locally as a
SystemAdmin, and use `/workspace/learning/grading-validation`. The page is the
only supported human lifecycle surface for listing, preparing, refreshing,
verifying, and resetting scenarios.

For a focused browser case, invoke Playwright directly:

```bash
E2E_RUN=1 \
QUIZ_GRADING_SCENARIO=question-type-matrix \
QUIZ_GRADING_CASES=QG-ITEM-MATCHING-PARTIAL-001 \
pnpm exec playwright test --config=playwright.grading.config.mjs
```

The Playwright setup prepares the scenario through the protected web lab. Its
teardown verifies the recorded browser evidence and resets only resources
owned by the scenario manifest. Set `QUIZ_GRADING_KEEP_SCENARIO=1` only when a
failed execution must remain available for local diagnosis.

When API/web are not already healthy, the Playwright supervisor starts the
lean `dev:fast` stack. It must not start generated-client polling or build
watchers: browser validation is read-only with respect to source files, and
those watchers add substantial memory pressure without contributing evidence.
The E2E-owned stack also builds the API once with one MSBuild worker, runs that
build without hot reload or a second compilation, gives Turbopack an isolated
`.next-e2e-grading-turbo` cache, and caps the Next.js heap at 4096 MB by default.
The isolated cache prevents interactive, stale, or different-bundler artifacts
from being restored into the validation process. Use
`E2E_WEB_MAX_OLD_SPACE_SIZE_MB` only when a measured validation run requires a
different local limit.

Use separate browser contexts for each persona. Never replace learner,
reviewer, or instructor behavior with SystemAdmin.

## 3. Preserve evidence

For each finding record:

- stable case ID, commit, scenario, persona, and checkpoint;
- expected UI/API/grading/audit oracle from the catalog;
- observed result and sanitized API/browser evidence;
- whether the failure belongs to product, harness, environment, or an external
  dependency.

Do not store tokens, cookies, passwords, private answer keys, or unredacted
authoring payloads. Do not query the database to make a browser assertion pass.

## 4. Classify without changing behavior

Use exactly one catalog status: `pass`, `bug`, `partial`, `missing`,
`decision-needed`, `harness-gap`, `external-blocker`, or `not-applicable`.
A skip requires its case ID, reason, and tracking reference. Unsupported or
pending work is never converted to a zero score.

Use [`product-heuristics.md`](./product-heuristics.md) to assign impact. Do not
infer a requirement for AI, assets, passback, autosave, or another module.

## 5. Produce a candidate report

Validation runs write under:

```text
apps/web/test-results/quiz-grading-validation/<run-id>/
```

Review `report.json`, `report.md`, and evidence. Generated output never edits
the canonical snapshot automatically. Preserve a report in
`docs/testing/quiz-grading/reports/` only after human review.

## 6. Correct separately and regress neighbors

After a finding is accepted, implement it in a separate change. Respect module
ownership: grading stays quiz-agnostic and quiz-specific translation remains
in `grading-adapter-quiz`. Schema changes need explicit justification and a
real migration; the validation harness itself never authorizes one.

Re-run the case, its contract layer, adjacent cases of the same type/workflow,
and applicable authorization negatives. Promote the canonical status only
when its declared evidence layer passes.

## Stop conditions

Stop and report rather than improvising when:

- expected behavior is not defined (`decision-needed`);
- an approved provider/module is absent (`external-blocker`);
- the harness cannot observe the product (`harness-gap`);
- the requested action would bypass authorization, write directly to the
  database, alter schema, or remove resources not owned by the manifest.
