# Quiz Grading Scenario Runner

This local-only runner creates reproducible quiz grading workflows through the
same authenticated HTTP contracts used by the product. It never writes to the
database directly and does not add development endpoints to the API.

## Prerequisites

Start the application and keep it running:

```bash
pnpm dev:fast
```

The defaults are `http://localhost:3000` and `http://localhost:8080`. The
runner refuses remote hosts and uses the generated client from `dist-fast`
when that development mode is active.

## Review Matrix

`review-matrix` creates one course with seven independent quizzes:

| Quiz key               | Workflow                              |
| ---------------------- | ------------------------------------- |
| `automated`            | `AutomatedReview`                     |
| `instructor`           | `InstructorReview`                    |
| `automated-instructor` | `AutomatedReview -> InstructorReview` |
| `self`                 | `SelfReview`                          |
| `self-instructor`      | `SelfReview -> InstructorReview`      |
| `peer`                 | `PeerReview`                          |
| `peer-instructor`      | `PeerReview -> InstructorReview`      |

Every quiz contains exactly two deterministic questions worth one point each:
a true/false question and a single-choice question. This keeps browser actions
and expected scores stable while isolating the behavior of each review stage.

`AIReview` is deliberately absent from the executable matrix. Its durable port
exists, but the application does not register an approved provider. Creating a
fake result in this runner would hide the production capability gate instead
of testing the real workflow.

The existing single-workflow scenarios remain available, as does
`collective-automated-instructor` for API verification of one shared group
submission.

## Capability Validation

The runner also owns three validation-oriented scenarios:

| Scenario                  | Purpose                                                                         |
| ------------------------- | ------------------------------------------------------------------------------- |
| `question-type-matrix`    | One quiz for each of the 14 question types and four fill-blank variants         |
| `lifecycle-access-matrix` | Stable deterministic quizzes for lifecycle, release, and access cases           |
| `acceptance-lab`          | Review and question-type matrices in one persistent course for human inspection |

Inspect the stable capability catalog without starting the application:

```bash
pnpm grading:scenario catalog
pnpm grading:scenario catalog --suite question-type
pnpm grading:scenario catalog --case QG-ITEM-ESSAY-001
```

Run one question case or all currently executable question types:

```bash
pnpm grading:scenario test \
  --scenario question-type-matrix \
  --case QG-ITEM-MATCHING-PARTIAL-001 \
  --fresh

pnpm grading:scenario test \
  --scenario question-type-matrix \
  --fresh
```

`--question-type` accepts a `QuizEntryType` value or a question fixture key.
Case and type filters may be repeated or comma-separated. Cases that are
unsupported, need a product decision, or have an external blocker are reported
with their stable ID and are not treated as passing browser tests.
In complete matrices, those quizzes stop at their declared maximum checkpoint
while supported quizzes continue. Currently unkeyed rating, numeric, and
formula stop at `test-run-ready`; the runner does not add `InstructorReview` as
an undeclared workaround.

The repository-level profiles are:

```bash
pnpm grading:validation:contracts # no running application required
pnpm grading:validation:smoke     # bounded real-persona browser set
pnpm grading:validation:full      # all executable browser matrices
pnpm grading:validation:lab       # persistent human exploration course
```

Every prepare, verify, or test command with catalog cases writes a sanitized
candidate report to:

```text
apps/web/test-results/quiz-grading-validation/<run-id>/
```

See `docs/testing/quiz-grading/README.md` for status semantics, evidence rules,
and the reviewed product snapshot.

## Manual Testing

Prepare the complete course for instructor-only authoring and Test Run work:

```bash
pnpm grading:scenario prepare \
  --scenario review-matrix \
  --checkpoint test-run-ready \
  --fresh
```

Prepare it for official learner attempts instead:

```bash
pnpm grading:scenario prepare \
  --scenario review-matrix \
  --checkpoint learner-ready \
  --fresh
```

The printed `urls.quizzes` object contains the content editor, assessment
editor, Test Run, learner activity, and SpeedGrader URLs for each quiz. The
course and manifest remain available after the command exits, so each workflow
can be manipulated manually.

To create only part of the matrix, repeat `--quiz` or pass comma-separated
keys:

```bash
pnpm grading:scenario prepare \
  --scenario review-matrix \
  --quiz self,peer-instructor \
  --checkpoint learner-ready \
  --fresh
```

Quiz selection is fixed when a manifest is created. Reset the scenario before
changing that prepared selection.

Reserved local personas are intentionally short:

- instructor: `instructor1@qgs.test` / `Qgs!1234` (`instructor-1`)
- learner A: `learner1@qgs.test` / `Qgs!1234` (`learner-a`)
- learner B: `learner2@qgs.test` / `Qgs!1234` (`learner-b`)
- outsider: `outsider1@qgs.test` / `Qgs!1234` (`outsider-1`)

Peer workflows enroll both learners. Other individual workflows use learner A.
Use a private window or separate browser profile when switching personas.
Learners enter through `urls.learnerCourses` or the relevant
`urls.quizzes.<key>.learnerActivity`; `/workspace` is not the learner course
dashboard.

For manual release policies, completing a review does not expose the score.
Use **Release to learner** in SpeedGrader. Before release, the learner sees a
retained result without its score.

## Automated Browser Tests

Run every review workflow as a separate serial Playwright test against the same
course:

```bash
pnpm grading:scenario test \
  --scenario review-matrix \
  --fresh
```

Run selected tests only:

```bash
pnpm grading:scenario test \
  --scenario review-matrix \
  --quiz self \
  --quiz peer-instructor \
  --fresh
```

Add `--headed` to watch the persona interactions. Each quiz test covers the
instructor Test Run, outsider denial, official learner submission, applicable
self/peer/instructor review stages, manual release, learner gradebook, and
restored result. Peer tests use isolated contexts for both learners and ensure
that each learner reviews the other's submission.

When a full matrix manifest already exists, `--quiz` selects which browser
tests run without rebuilding the course. Without an existing manifest,
`--quiz` also determines which quizzes are provisioned.

Screenshots are grouped by stable case ID and referenced by the candidate
report under:

```text
apps/web/test-results/quiz-grading-validation/<run-id>/evidence/<case-id>/screenshots/
```

Playwright traces are retained on failure. The runner preserves the scenario
unless `--cleanup` is explicitly supplied.

## Checkpoints

- `authoring-ready`: editable course, quizzes, assessments, and grading group
- `test-run-ready`: immutable candidates ready for instructor Test Run
- `learner-ready`: published course and active learner enrollments, no attempts
- `review-ready`: submitted attempts waiting for an instructor stage
- `release-ready`: finalized results retained by the manual release policy
- `released`: results visible in learner activities and gradebooks

For a mixed matrix, each quiz owns its checkpoint. The manifest-level
checkpoint is the earliest quiz checkpoint. A non-instructor workflow skips
`review-ready` and proceeds directly to `release-ready`.

## Inspect and Verify

```bash
pnpm grading:scenario list
pnpm grading:scenario status --scenario review-matrix
pnpm grading:scenario verify --scenario review-matrix
```

`status` reports divergence without repairing it. `verify` fails when the
manifest claims a state that the API cannot confirm. Interrupted preparation
can be resumed with the same command; stale process locks are recovered, while
a live concurrent runner remains protected.

## Reset

```bash
pnpm grading:scenario reset --scenario review-matrix
```

Reset validates the local host and ownership marker, then removes only the
resources recorded by that scenario through public APIs. It does not clear the
database, Docker volumes, unrelated courses, or the reserved identity accounts.
If deletion is incomplete, the manifest is retained so cleanup can be retried.

If the local database was recreated first, the recorded tenant no longer
exists. In that case reset verifies the new local tenant through the reserved
instructor identity and removes the stale manifest without trying to delete
resource IDs from the previous database. An authentication failure inside the
same tenant still fails closed and retains the manifest.
