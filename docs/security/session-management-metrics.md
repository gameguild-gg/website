# Session analytics metrics

Session lifecycle telemetry for the identity authentication module. The meter is registered
with the OpenTelemetry runtime in `apps/api/Source/GameGuild.API/Core/Extensions/OpenTelemetryExtensions.cs`
(meter name `GameGuild.Identity.Authentication.Sessions`) and is exported through the configured
console/OTLP pipelines like every other platform meter.

## Metrics

| Metric | Kind | Unit | Description |
| --- | --- | --- | --- |
| `authentication.sessions.created` | counter | `{session}` | Sessions created and persisted. |
| `authentication.sessions.terminated` | counter | `{session}` | Sessions terminated (single or bulk), tagged with `reason`. |
| `authentication.sessions.active` | observable gauge | `{session}` | In-process balance of created minus terminated sessions. |

### Tags

- `reason` (on `authentication.sessions.terminated`): the lowercased
  `SessionTerminationReason` enum value (`userlogout`, `security`, `expired`, …).

## Semantics and caveats

- **Process counters, database evidence.** The counters track what this process did; the
  sessions tables remain the authoritative history. A process restart resets the counters —
  they measure lifecycle activity, not inventory.
- **The active gauge is per-process.** Horizontal replicas each report their own balance, so
  the fleet-wide active count is the *sum* across instances, not any single reported value.
  Pre-existing sessions (created before the process started) are not part of the balance.
- **Fail-open emission.** Every emission is wrapped so a failing in-process telemetry listener
  can never change session lifecycle behavior.

## Emitting sites

- `SessionManagementService.CreateSessionAsync` records a creation.
- `TerminateSessionAsync` / `TerminateUserSessionsAsync` / `RevokeAllSessionsForUserAsync`
  record terminations with the effective reason.
- Housekeeping paths also record: expired-session cleanup (`Expired`) and max-session-count
  eviction during creation (`MaxSessionsExceeded`, with the evicted count).

Source: `apps/api/Source/Modules/GameGuild.Identity.Authentication/Services/SessionAnalyticsMetrics.cs`.
Tests: `apps/api/tests/GameGuild.Identity.Authentication.UnitTests/Services/SessionAnalyticsMetricsTests.cs`.
