# GameGuild Grading

`@game-guild/grading` contains the framework-independent contracts and pure
rules shared by assessment authoring and grading runtimes.

It does not own React UI, HTTP routes, persistence, course navigation, or the
implementation of a specific assessment type.

## Package boundary

The dependency direction for a content-specific integration is:

```text
@game-guild/grading <- adapter -> content domain
```

Quiz integration lives in `@game-guild/grading-adapter-quiz`. Neither this
package nor `@game-guild/quiz` depends on that adapter.

## Core contracts

- `ContentGradingDefinitionV2` identifies authored items by stable ID.
- `AssessmentExecutionPolicyV1` fixes attempts, completion, release,
  presentation, and review workflow.
- `AssessmentExecutionManifestV1` fixes executable component keys and exact
  versions.
- `AssessmentExecutionSnapshotV1` binds authoring, policy, manifest, and
  immutable item projections.
- `AssessmentExecutionDeliveryV1` records the concrete learner-safe delivery.
- `AssessmentResponseEnvelopeV1` carries an opaque, content-discriminated
  response.
- `GradeResultV1`, `GradeRoundV1`, and `GradingExecutionV1` describe generic
  review output and execution state.

`ScoreValue` and `PercentValue` are integer units with a fixed scale of `100`.
JSON decimals and numeric strings are not accepted for academic values;
human decimal input is parsed only at UI boundaries.

## Validation and identity

Use the public validators before hashing or executing untrusted contract data:

- `validateContentGradingDefinition`
- `validateAssessmentExecutionPolicy`
- `validateAssessmentExecutionManifest`
- `validateAssessmentExecutionSnapshot`
- `validateAssessmentExecutionDelivery`

Snapshot validation binds the same item IDs across grading metadata, manifest,
and projections. It also binds projection type/source fields and requires the
manifest stages to match the policy review workflow in canonical order.

Canonical identity uses JSON Canonicalization Scheme bytes and SHA-256:

- `hashAssessmentAuthoringSource`
- `hashAssessmentExecutionSnapshot`
- `hashAssessmentExecutionDelivery`

Changing executable versions changes the execution snapshot hash without
changing authoring identity.

Persist canonical JSON bytes together with their hash. Server code must hash
the validated raw JSON representation, not a DTO serialized again later. A
runtime request carries the authoritative execution snapshot hash so delivery
bindings can be checked without reconstructing identity.

## Runtime safety

Authoring content may contain private answer material. Learner delivery and
response envelopes must be produced by the content-specific adapter. Trusted
scores, correctness, feedback, and pass/fail decisions are produced by the
server runtime, never accepted from the browser.

`src/content-storage.ts` only reads and writes the generic
`ContentGradingDefinitionV2` field in an authoring document. It does not execute
grading.

## Development rules

- Keep this package independent of quiz and every other content domain.
- Keep content-specific projection, redaction, decoding, and evaluation in an
  adapter package.
- Keep operational state out of authored content documents.
- Resolve one aggregate assessment-type adapter, plus generic handlers and
  policies, by the exact key, version, and execution context fixed in the
  manifest.
- Add cross-language fixtures whenever a wire contract is implemented in both
  TypeScript and C#.

## Validation

```bash
pnpm --filter @game-guild/grading test
pnpm --filter @game-guild/grading typecheck
```
