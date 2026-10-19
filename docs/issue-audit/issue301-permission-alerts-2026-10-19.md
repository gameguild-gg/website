# Issue #301: permission mutation pattern alerts

## Requirement and scope

This patch addresses the remaining unchecked functional criterion in #301: alerting for suspicious permission patterns and bulk operations. It extends the existing durable security pipeline and tenant-scoped administrator alert queue. The other 29 functional criteria retain their independent acceptance requirements; checkbox state and this patch do not certify the whole issue.

## Production path

1. The host ApplicationDbContext retains its existing committed permission-change grouping by action, actor, tenant, correlation and command. Its before/after snapshots now include an explicit ChangeCount.
2. AuditService routes canonical PermissionGranted, PermissionRevoked and PermissionChanged requests with ResourceType Permission and mutation evidence through ISecurityEventLogger. Ordinary successful access decisions with the same action name remain ordinary audit events and do not count as state changes. Optional logger resolution occurs in the existing fresh scope to avoid a constructor cycle with the logger's fallback audit service.
3. SecurityAlertRuleEvaluator recognizes a positive top-level ChangeCount or a nonempty legacy Changes snapshot array, including camel-case spool payloads. It counts successful mutation events for the same actor and tenant within the configured capture-time window. Unknown actors can use their IP as the subject; known and unknown actors never share a burst window. Failed changes, access decisions, other actors, other tenants and expired/future persisted rows are excluded.
4. Bulk changes count records in one grouped snapshot; burst detection counts committed mutation events. Candidates are streamed until the burst threshold is established, without imposing a pre-evidence row limit. The description explicitly reports an at-least count. Alerts use the existing open-alert deduplication and acknowledgement/resolution lifecycle.
5. On PostgreSQL, permission evaluation takes a transaction-scoped advisory lock derived from tenant and actor (or unknown actor/IP). Standalone logger capture uses its private context, an explicit READ COMMITTED transaction and the provider execution strategy, keeping the lock through save and commit. Stable event IDs make lost-commit-acknowledgement retries idempotent. Required command capture keeps the caller's transaction; rollback removes both the event and its alerts.
6. Failed standalone persistence retains the existing sanitized durable spool and replay behavior. A committed mutation's audit delivery is not cancelled when the originating HTTP request aborts. Alert evaluation preserves the existing policy that alert failures cannot prevent durable security-event capture; persistence/spool failures remain observable through its capture outcome and diagnostics.

## Configuration

All values live under Audit:SecurityEventPipeline. Binding and production DI registration both apply these bounds:

| Option | Default | Bounds |
|---|---:|---:|
| PermissionChangeAlertThreshold | 10 events | 2–1000 |
| PermissionChangeWindowMinutes | 5 minutes | 1–1440 |
| BulkPermissionChangeAlertThreshold | 25 records | 2–100000 |

No endpoint, DTO, database schema or generated client changes are required. The existing GET /api/audit/security-events/alerts endpoint exposes both new rule IDs: security.permission-change-burst and security.bulk-permission-change.

## Test definitions and acceptance gate

- SecurityEventPipelineTests: exact threshold, repeated-hit deduplication, actor and tenant isolation, failed changes and successful read decisions, bulk counts, Pascal/camel-case metadata, legacy snapshots, capture-time window, durable spool replay exactly once, unknown-actor IP isolation, bounded option binding and actual AddAuditServices registration.
- GovernanceAuditHookCaptureTests: real host permission snapshots through the real AuditService into the security logger, grouped counts, tenant separation and durable/failed capture outcomes.
- PermissionPatternAlertsPostgreSqlTests: concurrent native PostgreSQL writes at the burst threshold, actual production-context bulk mutations and tenant-isolated administrator HTTP reads, and required-capture transactional rollback.

Source and diff review have been performed. No local SDK build, test suite, database, application server or browser was run because of the existing local execution hold. These definitions require matching-head hosted CI, including native PostgreSQL integration and architecture checks, before implementation acceptance. Issue #301 remains open until its complete retained scope is verified.
