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

Use this order and stop once the failing boundary is established:

```bash
pnpm grading:validation:catalog
pnpm grading:validation:contracts
pnpm grading:scenario test --scenario <scenario> --case <case-id> --fresh
```

For browser execution, first keep `pnpm dev:fast` healthy in another terminal.
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

Scenario commands write under:

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
