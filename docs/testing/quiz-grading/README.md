# Quiz grading validation

This directory is the reviewed reference for the Quiz + Grading validation
program. The executable source of truth is the capability catalog in
`apps/web/scripts/quiz-grading-scenario/capability-catalog.mjs`; this directory
explains how to run it and records the last human-reviewed product snapshot.

The program separates four dimensions so a failure can be attributed without
running a Cartesian product:

- **review:** review ordering, actors, release, and learner visibility;
- **question type:** authoring, redaction, response, score, and rendering for
  every quiz discriminant;
- **lifecycle and access:** publication, Test Run, attempts, weights, and
  contextual authorization;
- **failure and recovery:** idempotency, concurrency, stale work, and runner
  recovery.

## Sources of truth

| Concern                               | Source                                           |
| ------------------------------------- | ------------------------------------------------ |
| Stable case IDs and expected oracles  | `capability-catalog.mjs`                         |
| All 14 types and four fill variants   | `quiz-validation-cases-v1.json`                  |
| Scenario definitions and checkpoints  | `definitions.mjs`                                |
| Browser interactions by question type | `question-type-drivers.mjs`                      |
| Candidate report format and redaction | `report.mjs`                                     |
| Reviewed product snapshot             | [`capability-status.md`](./capability-status.md) |
| Audit procedure                       | [`AGENT_PROTOCOL.md`](./AGENT_PROTOCOL.md)       |

The Markdown snapshot is not generated automatically. An execution creates a
candidate report; a person reviews the evidence before changing canonical
statuses.

## Commands

The contract profile does not need the application:

```bash
pnpm grading:validation:catalog
pnpm grading:validation:contracts
```

Browser profiles require a healthy local stack in another terminal:

```bash
pnpm dev:fast
```

Run the bounded browser smoke or the full executable matrix:

```bash
pnpm grading:validation:smoke
pnpm grading:validation:full
```

Create a persistent course for human exploration:

```bash
pnpm grading:validation:lab
```

The lab output contains short local credentials and URLs for each persona and
quiz. It remains available until explicitly reset:

```bash
pnpm grading:scenario reset --scenario acceptance-lab
```

The lab respects each quiz's declared capability. Supported items reach the
requested official checkpoint, while an unsupported item remains available at
its last valid checkpoint (currently `test-run-ready`) instead of being
published through a fallback review workflow.

Run a single stable case while diagnosing it:

```bash
pnpm grading:scenario test \
  --scenario question-type-matrix \
  --case QG-ITEM-MATCHING-PARTIAL-001 \
  --fresh
```

`--question-type`, `--quiz`, and repeated/comma-separated selections are also
supported. A failed browser run preserves its owned scenario and evidence.

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

No command in this program is authorized to add database seeds, bypass
permissions, write through `DbContext`, or modify schemas and migrations.
