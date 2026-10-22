# Quiz grading validation

This directory is the reviewed reference for the Quiz + Grading validation
program. The executable source of stable case IDs and expected oracles is
[`capability-catalog.mjs`](../../../apps/web/src/lib/grading-scenarios/adapters/quiz/capability-catalog.mjs).
The Markdown files here explain how to run the program and record the last
human-reviewed product snapshot.

The program separates four dimensions so a failure can be attributed without
running a Cartesian product:

- **review:** review ordering, actors, release, and learner visibility;
- **question type:** authoring, redaction, response, score, and rendering for
  every quiz discriminant;
- **lifecycle and access:** publication, Test Run, attempts, weights, and
  contextual authorization;
- **failure and recovery:** idempotency, concurrency, stale work, and harness
  recovery.

## Sources of truth

| Concern                              | Source                                                                          |
| ------------------------------------ | ------------------------------------------------------------------------------- |
| Generic web orchestration            | `apps/web/src/lib/grading-scenarios/`                                           |
| Quiz lifecycle adapter               | `apps/web/src/lib/grading-scenarios/adapters/quiz/`                             |
| Stable case IDs and expected oracles | `capability-catalog.mjs`                                                        |
| All 14 types and four fill variants  | `packages/features/grading-adapter-quiz/fixtures/quiz-validation-cases-v1.json` |
| Browser interactions and lifecycle   | `e2e/learning/quiz-grading/support/`                                            |
| Reviewed product snapshot            | [`capability-status.md`](./capability-status.md)                                |
| Audit procedure                      | [`AGENT_PROTOCOL.md`](./AGENT_PROTOCOL.md)                                      |

The Markdown snapshot is not generated automatically. An execution creates a
candidate report; a person reviews the evidence before changing canonical
statuses.

## Commands

The contract profile does not need the application:

```bash
pnpm grading:validation:contracts
```

Browser profiles use Playwright directly. They can start the local stack, or
reuse a healthy `pnpm dev:fast` process:

```bash
pnpm grading:validation:smoke
pnpm grading:validation:full
```

When Playwright owns the stack, it uses the lean `dev:fast` bootstrap with no
SDK watchers or API hot reload. It builds the API once with one MSBuild worker,
starts that build without another compilation, uses an isolated
`.next-e2e-grading-turbo` Turbopack cache, and caps the Next.js heap at 4096 MB.
This keeps a long validation run from competing with the IDE for unbounded
memory and prevents interactive or stale bundler artifacts from entering the
automated run. Override the measured local limit only when necessary with
`E2E_WEB_MAX_OLD_SPACE_SIZE_MB`.

For persistent human exploration, sign in to the local application as a
SystemAdmin and open:

```text
http://localhost:3000/workspace/learning/grading-validation
```

Select the grading adapter, scenario, targets, and checkpoint. The page lists
the capability catalog, short local credentials, and links for every persona
and target. It is the only supported human surface for scenario lifecycle.

Run a single stable case with the standard Playwright runner:

```bash
E2E_RUN=1 \
QUIZ_GRADING_SCENARIO=question-type-matrix \
QUIZ_GRADING_CASES=QG-ITEM-MATCHING-PARTIAL-001 \
pnpm exec playwright test --config=playwright.grading.config.mjs
```

Set `QUIZ_GRADING_KEEP_SCENARIO=1` to preserve an automated scenario for local
inspection. Otherwise teardown verifies browser evidence and resets resources
owned by its manifest through the web lab.

## Reports and evidence

Candidate output is local and ignored by Git:

```text
apps/web/test-results/quiz-grading-validation/<run-id>/
  manifest.json
  report.json
  report.md
  evidence/<case-id>/screenshots/
```

Reports include the commit, scenario, environment, completed automation layers,
status, owner, tracking reference, and sanitized evidence. Tokens, cookies,
passwords, answer keys, and private authoring fields are redacted before a
report is written.

Only deliberately reviewed reports belong in [`reports/`](./reports/). Never
copy a generated report there merely because a command completed.

## Status semantics

`pass` means that the declared layer has evidence. `bug`, `partial`, `missing`,
`decision-needed`, `harness-gap`, `external-blocker`, and `not-applicable`
remain distinct. A skipped browser case must retain its stable ID, reason, and
tracking reference; unsupported grading must never be represented as score
zero.

No command or page in this program is authorized to add database seeds, bypass
permissions, write through `DbContext`, or modify schemas and migrations.
