# Common foundation reconciliation

Status: in progress. Do not interpret module-file parity as production/runtime parity.

## Decision

The shared foundation in ModuEstate and GameGuild must have the same implementation, tests and dependency contracts. Changes are reviewed in both directions. Repository identity (namespace/package name, truthful npm repository.directory), UTF-8 BOM and line endings are the only normalization rules. Whitespace, string values, comments, runtime paths and dependency versions remain significant.

Product domains remain separate: real estate is not copied into GameGuild, and learning/economy are not copied into ModuEstate. Product-generated SDK files are reported separately; their exclusion from byte comparison is not evidence of endpoint compatibility.

The required shared host paths are blocking too: event transport, email providers, cost instrumentation, quota reconciliation, ApplicationDbContext and the outbox/inbox model configuration. Remaining product host composition is advisory. An advisory difference is not an approval or a source-of-truth decision.

## Repeatable verification

Run from either repository root with the other checkout available:

```sh
pnpm parity:test
pnpm parity:common --peer-root E:/repositories/game-guild/game-guild --output-dir artifacts/common-module-audit/review-001
pnpm parity:tests --report artifacts/common-module-audit/review-001/report.json --output-dir artifacts/common-module-tests/review-001
```

When running from GameGuild, point --peer-root at ModuEstate instead. Choose a new output directory on every run. The audit is read-only and never fetches, merges or rewrites source. Exit codes: 0 passes; 1 confirmed parity/inventory failure; 2 invalid or incomplete audit. --report-only allows collecting evidence while differences exist, but retains result=drift in the report; it must not be used as a green CI gate.

The test runner discovers all shared .NET test project roots from the report. It runs unit suites by default; --kind integration or --kind all is explicit. It runs at most one .NET build per repository concurrently. No live AWS account is required by the unit suites. Each result retains its command project, exit code, TRX counters, skipped count and log. Empty execution or missing TRX cannot pass.

The runner verifies the source fingerprints before and after execution and records the audit SHA-256. Changes during testing invalidate the aggregate result even if individual tests passed. Passing unit tests does not override a failing source-parity gate.

## Reviewed reconciliation

The implementation was combined, not chosen globally from one repository:

- ModuEstate durable lifecycle events, operation contracts, quota/cost ledgers, secure asset handling and mediated mutations.
- GameGuild queued notifications, SES delivery receipts, step-up authentication, KYC orchestration, marketplace/commerce improvements and expanded Base UI components.
- Both test inventories and all one-sided shared components retained.
- Real command handlers exercised by controller fixtures; expected results are not fabricated by a fallback sender in production.
- Shared UI compatibility adapters preserve legacy single-value controls, labels before portal mounting, mixed checkboxes, child handlers/refs, force-mounted tabs, layer cancellation and menu selection.
- Both hosts expose SES, SendGrid and SMTP through the same configuration-driven email sender. SES headers reject control characters and provider clients are disposed.

Applied batches, reasons, source hashes and recoverable before-images are retained under artifacts/common-module-reconciliation in the ModuEstate checkout. Root dependency overrides align React/React DOM/Next, and workspace links must actually be reinstalled, not just updated in the lockfile.

## Outstanding runtime work — blocks a 100% claim

The expanded audit still detects the following shared host discrepancies; these must not be suppressed:

1. Port and wire durable event transport into GameGuild. Preserve its database-backed data-protection support when reconciling ApplicationDbContext.
2. Preserve legacy order-audit and subscription listeners during cutover. Do not clear their domain events without a replacement, serialize private free-text into public outbox payloads, or publish after commit.
3. Separate the property-specific authoritative quota count behind a product adapter; share the quota reconciliation engine.
4. Wire cost telemetry consistently into both request pipelines and database instrumentation; telemetry failures must not turn committed requests into errors.
5. Verify command-contract enforcement against each host's actual command inventory. Do not blindly copy ModuEstate product-command contracts into GameGuild.
6. Reconcile database migrations and run PostgreSQL atomicity, retry, inbox, crash/replay and quota-reservation/reconciliation tests in both hosts.
7. Compare the common portions of generated API contracts after regeneration, in addition to consumer typechecks.

The remaining host differences are deliberately visible as failures. This document records an incomplete reconciliation, not a completion certificate.

## Verification boundaries

Use fresh reports and test logs rather than historical counts. The test evidence does not establish live AWS pricing/billing integration, a deployed migration, browser visual parity or passing product-only suites. Product-only failures must be distinguished from regressions introduced by the common foundation; do not change product behavior just to make unrelated tests green.

## Trade-offs

Keeping two source copies preserves independent products but requires a two-checkout verification gate. A single shared package/repository could reduce future duplication, but that changes repository ownership/versioning and is not part of this reconciliation. Explicit per-file decisions and full test inventories are slower than a bulk overwrite, but retain improvements from both sides and make reruns auditable.
