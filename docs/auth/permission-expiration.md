# Permission Expiration (Temporal Access Control)

Implementation of issue #331. Permission grants can carry an `ExpiresAt` timestamp, after
which they stop being honored automatically. This enables time-limited access (trial
periods, temporary collaborations, emergency/break-glass access) without requiring an
administrator to remember to revoke anything.

## Where expiration lives

| Surface | File | Behavior |
|---|---|---|
| `TenantPermission.ExpiresAt` | `apps/api/Source/Modules/GameGuild.Identity.Authorization/Entities/TenantPermission.cs` | Nullable timestamp; `IsExpired` computed against `SystemClock.UtcNow`; indexed via `IX_TenantPermissions_ExpiresAt` |
| `ResourceUserPermission.ExpiresAt` / `AccessControlListEntry.ExpiresAt` | `Entities/ResourcePermissionEntities.cs`, `Entities/AccessControlListEntry.cs` | Same semantics for resource-scoped grants |
| Evaluation filtering | `Services/EffectivePermissionResolverService.cs`, `PermissionQueryService` | Expired grants are excluded from the effective permission set — **fail closed, independent of cleanup** |
| Cleanup + reminders | `Services/PermissionExpirationService.cs` | Deactivates expired grants (audit + tenant security-version bump) and publishes upcoming-expiration notifications |
| Background worker | `BackgroundServices/PermissionExpirationWorker.cs` | Periodic cycles driving the service above |
| Alert delivery | `apps/api/Source/GameGuild.API/Core/Security/PermissionExpirationAlertHandler.cs` | Turns the published notification into in-app + email alerts |

Key design point: expiration is enforced **at evaluation time**. Even if the background
worker is disabled or a cycle is missed, an expired grant never grants access; the worker
only performs the durable deactivation (so revocation survives re-activation paths) plus
auditing and notifications.

## Configuration

All knobs live under `Authorization:PermissionExpiration`
(`PermissionExpirationOptions.SectionName`, bound in `AuthorizationModuleExtensions`):

```json
{
  "Authorization": {
    "PermissionExpiration": {
      "Enabled": true,
      "InitialDelay": "00:00:30",
      "ScanInterval": "00:15:00",
      "ExecutionTimeout": "00:05:00",
      "BatchSize": 100,
      "UpcomingNotificationWindow": "7.00:00:00",
      "MinimumReminderInterval": "1.00:00:00",
      "ApplyDefaultsOnGrant": true,
      "DefaultGrantExpiration": "30.00:00:00",
      "DefaultExpirationByPermission": {
        "courses:*": "14.00:00:00",
        "monetization:monetize": "7.00:00:00"
      }
    }
  }
}
```

| Setting | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | Master switch for the expiration worker **and** for expiration alert delivery. When false, no automatic cleanup, reminders, or user-facing alerts happen; administrative endpoints still work. |
| `InitialDelay` / `ScanInterval` / `ExecutionTimeout` | 30s / 15m / 5m | Worker cadence: warm-up delay, cycle spacing, and per-cycle deadline (a timed-out cycle is retried next interval). |
| `BatchSize` | `100` | Upper bound of grants handled per cycle phase, so large tenants cannot stall a cycle. |
| `UpcomingNotificationWindow` | `7` days | How far ahead the worker looks when publishing "expiring soon" notifications. |
| `MinimumReminderInterval` | `24` hours | Dedup spacing between reminders for the same grant (stamped in `Metadata["expirationReminderAt"]`). |
| `ApplyDefaultsOnGrant` | `false` | When true, grants issued without an explicit `ExpiresAt` receive the resolved default period. Opt-in so existing permanent grants keep their semantics. |
| `DefaultGrantExpiration` | `null` | Global fallback default applied when no per-permission default matches. `null` = no default. |
| `DefaultExpirationByPermission` | empty | Per-permission-type overrides. Keys are exact permission strings (e.g. `courses:create`) or wildcards (e.g. `courses:*`). |

### Default resolution and wildcard precedence

`PermissionExpirationOptions.ResolveDefaultExpiration` resolves, in order:

1. **Exact match** — any granted permission equal to a configured key wins immediately
   (first exact match among the granted permissions).
2. **Longest wildcard prefix** — among `prefix:*` keys whose prefix matches at least one
   granted permission (case-insensitive), the most specific (longest prefix) wins.
3. **Global default** — `DefaultGrantExpiration`.

Example: with `{ "courses:*": 14d, "courses:grade": 3d, "DefaultGrantExpiration": 30d }`,
a grant of `["courses:grade"]` gets 3 days, `["courses:create"]` gets 14 days, and
`["social:post"]` gets 30 days.

## Worker cadence and idempotency

`PermissionExpirationWorker` (a `BackgroundService`) runs two phases per cycle, each in a
fresh DI scope and bounded by `BatchSize`:

1. **Expired sweep** (`ProcessExpiredAsync`): loads grants with `ExpiresAt <= now`, skips
   ones already stamped `Metadata["expirationProcessedAt"]` (idempotency), calls
   `TenantPermission.Expire()` (sets `IsActive = false`), writes a `PermissionAuditLog`
   entry (`PermissionOperationType.Expire`, system actor), bumps the tenant security
   version (cache invalidation), and publishes a
   `PermissionExpirationNotification(Kind: Expired)`.
2. **Reminder sweep** (`SendUpcomingExpirationRemindersAsync`): loads grants expiring
   before `now + UpcomingNotificationWindow`, skips ones reminded within
   `MinimumReminderInterval`, stamps `expirationReminderAt`, and publishes a
   `PermissionExpirationNotification(Kind: Upcoming)`.

Per-grant failures are logged and retried on the next cycle; a cycle that exceeds
`ExecutionTimeout` is abandoned and retried. The worker exits immediately when
`Enabled = false` or the configuration is invalid.

## Notifications (upcoming and past expirations)

`PermissionExpirationService` publishes `PermissionExpirationNotification` through the
mediator. Delivery is handled by `PermissionExpirationAlertHandler` in the API
composition root (the Authorization module stays platform-pure and cannot reference the
Notifications delivery stack):

- Resolves the recipient via `IUserRepository`; deleted/missing users and grants without
  a subject are skipped silently.
- Queues an **in-app** notification, plus an **email** when the user has an address.
  Both flow through `INotificationService`, so the recipient's per-channel notification
  preferences (drop / digest / hold-until) are honored.
- Priority mapping: `Kind = Expired` → `NotificationPriority.Urgent`;
  `Kind = Upcoming` → `NotificationPriority.Normal`. Type is `NotificationType.Security`.
- Delivery is gated on `Authorization:PermissionExpiration:Enabled` — the same switch as
  the worker — so administrative triggers cannot produce alerts while the feature is off.
- Queueing failures are logged, not thrown: the publisher stamps dedup metadata before
  dispatch and has no redelivery mechanism, so rethrowing would not retry the alert.

## Administrative endpoints

All live on `TenantPermissionsController` (`api/v{version}/authorization/tenants`),
tenant-admin authorized unless noted:

| Operation | Endpoint | Notes |
|---|---|---|
| Bulk set absolute expiration | `POST permissions:set-expiration` | `expiresAt` must be in the future; `null` clears the expiration (grant becomes permanent) |
| Bulk extend by period | `POST permissions:extend-expiration` | Extension extends from the later of current `ExpiresAt` / now; must be positive |
| List expiring grants | `GET {tenantId}/permissions:expiring` | Optional `expiresBefore` cutoff (defaults to the notification window) |
| Process expired now | `POST permissions:process-expired` | System admin only; runs the expired sweep without waiting for the worker |
| Send reminders now | `POST permissions:send-expiration-reminders` | System admin only; runs the reminder sweep on demand |

Every administrative mutation is audited (`PermissionOperationType.Update`, acting user
from `IActorContextAccessor`), bumps the tenant security version, and clears the grant's
reminder/processed stamps so the new lifecycle generates fresh notifications.

## Remediation flows

- **Access lost too early:** find the grant via `permissions:expiring` (or audit log),
  then `permissions:extend-expiration` with the needed period, or `permissions:set-expiration`
  with a new absolute time. Extension re-activates an expired grant (`IsActive = true`)
  when it pushes `ExpiresAt` back into the future.
- **Make a grant permanent:** `permissions:set-expiration` with `expiresAt: null`.
- **User reports not being warned:** reminders are deduplicated by
  `MinimumReminderInterval`; trigger `permissions:send-expiration-reminders` (system
  admin) to force a sweep, and check the user's notification preferences — preference
  drops are the most common silent-suppression cause.
- **Compliance reconciliation:** `PermissionAuditLog` rows with
  `PermissionOperationType.Expire` are the durable record of every automatic revocation.

## Best practices

- Prefer short expirations and deliberate extensions over long ones — the audit trail is
  per-mutation, so extension churn is visible while overly long grants are not.
- Set `ApplyDefaultsOnGrant` only together with explicit `DefaultExpirationByPermission`
  entries or a `DefaultGrantExpiration`; enabling defaults without any period configured
  is a no-op and only invites confusion.
- Use exact keys for high-risk permissions (`monetization:monetize`) and wildcards for
  families (`courses:*`); remember exact beats wildcard beats global.
- Do not rely on the worker for revocation timing — evaluation already fails closed at
  `ExpiresAt`. The worker exists for durable deactivation, audit, and user notification.
- When adding a new permission-sensitive surface that issues grants, accept the optional
  `expiresAt` parameter end-to-end instead of forcing callers through later bulk edits.

## Tests

- `GameGuild.Identity.Authorization.UnitTests/Services/PermissionExpirationServiceTests.cs` —
  sweep/reminder logic, idempotency stamps, audit, security-version bumps.
- `GameGuild.Identity.Authorization.UnitTests/PermissionExpirationOptionsTests.cs` —
  defaults resolution, wildcard precedence, validation.
- `GameGuild.Identity.Authorization.UnitTests/Handlers/PermissionExpirationCommandHandlerTests.cs` —
  administrative command handlers.
- `GameGuild.API.UnitTests/Security/PermissionExpirationAlertHandlerTests.cs` — alert
  delivery: config gate, recipient resolution, InApp+Email channels, priority mapping,
  preference-driven suppression tolerance, failure tolerance.
