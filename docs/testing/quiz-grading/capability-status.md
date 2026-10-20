# Quiz grading capability status

Reviewed baseline: 2026-10-17

Code baseline: `3cace6a45656` plus the validation-program worktree

Catalog size: 44 cases (9 review, 18 question type, 10 lifecycle/access, 7
failure/recovery)

This is a human-reviewed snapshot, not live test output. `partial` on a question
type means its package and backend adapter contracts are covered but the new
browser case has not yet been promoted by a successful recorded execution.

## Evidence index

- **C1:** `@game-guild/grading-adapter-quiz` fixture/adapter tests.
- **C2:** backend `QuizGradingAdapterContractTests` over the same embedded
  fixture.
- **C3:** existing review-matrix Playwright flow and scenario API verifier.
- **C4:** `AssessmentGradingRuntimeTests` and
  `AssessmentGradingPostgreSqlHttpTests`.
- **C5:** authoring/publication, gradebook, queue, peer privacy, and access unit
  or integration tests.
- **C6:** Scenario Runner support tests for catalog, selection, ownership,
  locks, sanitization, and reports.
- **B1:** new per-type Playwright drivers/specs; candidate until executed
  against the local full stack.

## Review workflows

| Case                                 | Status             | Layers/evidence          | Owner                | Severity / next action                                              |
| ------------------------------------ | ------------------ | ------------------------ | -------------------- | ------------------------------------------------------------------- |
| `QG-REVIEW-AUTOMATED-001`            | `pass`             | browser + API, C3/C4     | learning-assessments | None                                                                |
| `QG-REVIEW-INSTRUCTOR-001`           | `pass`             | browser + API, C3/C4     | learning-assessments | None                                                                |
| `QG-REVIEW-AUTOMATED-INSTRUCTOR-001` | `pass`             | browser + API, C3/C4     | learning-assessments | None                                                                |
| `QG-REVIEW-SELF-001`                 | `pass`             | browser + API, C3/C4     | learning-assessments | None                                                                |
| `QG-REVIEW-SELF-INSTRUCTOR-001`      | `pass`             | browser + API, C3/C4     | learning-assessments | None                                                                |
| `QG-REVIEW-PEER-001`                 | `pass`             | browser + API, C3/C5     | learning-assessments | None                                                                |
| `QG-REVIEW-PEER-INSTRUCTOR-001`      | `pass`             | browser + API, C3/C5     | learning-assessments | None                                                                |
| `QG-REVIEW-COLLECTIVE-001`           | `pass`             | API/runtime, C4/C5       | learning-assessments | Browser is not required for the shared-result contract              |
| `QG-REVIEW-AI-001`                   | `external-blocker` | fail-closed contract, C4 | learning-assessments | Optional: approve and register a provider before an executable case |

## Question types

| Case                           | Status             | Layers/evidence                                    | Owner | Severity / next action                                              |
| ------------------------------ | ------------------ | -------------------------------------------------- | ----- | ------------------------------------------------------------------- |
| `QG-ITEM-SINGLE-CHOICE-001`    | `partial`          | package + backend, C1/C2; B1 pending               | quiz  | Medium: execute/promote browser case                                |
| `QG-ITEM-MULTIPLE-CHOICE-001`  | `partial`          | package + backend, C1/C2; B1 pending               | quiz  | Medium: exact-set browser check                                     |
| `QG-ITEM-TRUE-FALSE-001`       | `partial`          | package + backend, C1/C2; B1 pending               | quiz  | Medium: execute/promote browser case                                |
| `QG-ITEM-FILL-TEXT-001`        | `partial`          | package + backend, C1/C2; B1 pending               | quiz  | Medium: text blank browser check                                    |
| `QG-ITEM-FILL-NUMBER-001`      | `partial`          | package + backend, C1/C2; B1 pending               | quiz  | Medium: numeric blank and tolerance UI                              |
| `QG-ITEM-FILL-DROPDOWN-001`    | `partial`          | package + backend, C1/C2; B1 pending               | quiz  | Medium: dropdown browser check                                      |
| `QG-ITEM-FILL-WORD-BANK-001`   | `partial`          | package + backend, C1/C2; B1 pending               | quiz  | Medium: word-bank browser check                                     |
| `QG-ITEM-SHORT-ANSWER-001`     | `partial`          | package + backend, C1/C2; B1 pending               | quiz  | Medium: case-sensitivity browser check                              |
| `QG-ITEM-ESSAY-001`            | `partial`          | pending-state contract, C1/C2; B1 pending          | quiz  | High: validate instructor handoff in browser                        |
| `QG-ITEM-MATCHING-PARTIAL-001` | `partial`          | total/partial package + backend, C1/C2; B1 pending | quiz  | High: validate drag/select and partial score                        |
| `QG-ITEM-ORDERING-PARTIAL-001` | `partial`          | total/partial package + backend, C1/C2; B1 pending | quiz  | High: validate reorder and partial score                            |
| `QG-ITEM-CATEGORIZATION-001`   | `partial`          | package + backend, C1/C2; B1 pending               | quiz  | Medium: validate categorization UI                                  |
| `QG-ITEM-RATING-KEYED-001`     | `partial`          | package + backend, C1/C2; B1 pending               | quiz  | Medium: execute keyed browser case                                  |
| `QG-ITEM-RATING-UNKEYED-001`   | `decision-needed`  | unsupported contract, C1/C2                        | quiz  | Low: define whether unkeyed rating is reviewable or survey-only     |
| `QG-ITEM-NUMERIC-001`          | `partial`          | unsupported official adapter, C1/C2                | quiz  | Medium: decide/implement official grading capability                |
| `QG-ITEM-FORMULA-001`          | `partial`          | unsupported official adapter, C1/C2                | quiz  | Medium: decide/implement official grading capability                |
| `QG-ITEM-HOTSPOT-001`          | `external-blocker` | adapter contract, C1/C2                            | quiz  | Low: execute only after an official resolvable asset fixture exists |
| `QG-ITEM-HIGHLIGHT-001`        | `partial`          | package + backend, C1/C2; B1 pending               | quiz  | Medium: execute/promote browser case                                |

## Lifecycle and access

| Case                              | Status    | Layers/evidence                     | Owner                | Severity / next action                       |
| --------------------------------- | --------- | ----------------------------------- | -------------------- | -------------------------------------------- |
| `QG-LIFECYCLE-REVISION-001`       | `pass`    | API/unit, C4/C5                     | learning-assessments | None                                         |
| `QG-LIFECYCLE-TEST-RUN-001`       | `pass`    | browser + runtime, C3/C4            | learning-assessments | None                                         |
| `QG-LIFECYCLE-ATTEMPT-RESUME-001` | `missing` | UI inspection/catalog               | learning-assessments | High: decide durable draft/autosave contract |
| `QG-LIFECYCLE-MANUAL-RELEASE-001` | `pass`    | browser + runtime, C3/C4            | learning-assessments | None                                         |
| `QG-LIFECYCLE-REGRADE-001`        | `pass`    | runtime, C4                         | learning-assessments | None                                         |
| `QG-LIFECYCLE-WEIGHT-ZERO-001`    | `pass`    | gradebook, C5                       | learning-assessments | None                                         |
| `QG-LIFECYCLE-WEIGHTED-001`       | `pass`    | gradebook, C5                       | learning-assessments | None                                         |
| `QG-ACCESS-LEARNER-001`           | `pass`    | browser + access integration, C3/C5 | learning-assessments | None                                         |
| `QG-ACCESS-OUTSIDER-001`          | `pass`    | browser + access integration, C3/C5 | learning-assessments | None                                         |
| `QG-ACCESS-COLLECTIVE-001`        | `pass`    | API/runtime, C4                     | learning-assessments | None                                         |

## Failure and recovery

| Case                                | Status    | Layers/evidence                   | Owner                | Severity / next action                                                     |
| ----------------------------------- | --------- | --------------------------------- | -------------------- | -------------------------------------------------------------------------- |
| `QG-RECOVERY-DOUBLE-SUBMIT-001`     | `partial` | replay covered, C4                | learning-assessments | High: exercise simultaneous HTTP submits against PostgreSQL                |
| `QG-RECOVERY-IDEMPOTENT-REPLAY-001` | `pass`    | HTTP/runtime, C4                  | learning-assessments | None                                                                       |
| `QG-RECOVERY-DIVERGENT-REPLAY-001`  | `pass`    | runtime fail-closed tests, C4     | learning-assessments | None                                                                       |
| `QG-RECOVERY-STALE-RELEASE-001`     | `partial` | round/regrade runtime, C4         | learning-assessments | High: attempt release explicitly against the superseded round              |
| `QG-RECOVERY-REVIEW-CONTENTION-001` | `partial` | versioned review writes, C4       | learning-assessments | High: submit two concurrent instructor mutations against PostgreSQL        |
| `QG-RECOVERY-MANIFEST-RESUME-001`   | `pass`    | runner contract, C6               | learning-assessments | None                                                                       |
| `QG-RECOVERY-SESSION-REVOKED-001`   | `partial` | contextual access integration, C5 | learning-assessments | High: prove stale-session denial on review and release after grant removal |

## Current decisions and blockers

1. `QG-ATTEMPT-DRAFT-PERSISTENCE`: the learner attempt UI currently has no
   declared durable draft-answer contract before final submission.
2. `QG-AI-PROVIDER`: the core port fails closed and no provider is approved.
3. `RATING` without a key needs a product decision before grading semantics are
   added.
4. `NUMERIC` and `FORMULA` are valid quiz types but are not accepted by the
   official grading adapter today.
5. `HOTSPOT` browser validation remains outside this program until quiz/assets
   provide an official resolvable fixture.
