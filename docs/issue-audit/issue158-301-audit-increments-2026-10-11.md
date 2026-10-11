# Authentication and permission audit increments — issues #158 and #301

Baseline: develop e755dfab2076806512d8b6d32df1962cadde1fa8, after accepted [PR #791](https://github.com/gameguild-gg/gameguild/pull/791).

## Delivered change scope

| Issue | Confirmed gap | Increment | Focused regression coverage |
| --- | --- | --- | --- |
| [#158](https://github.com/gameguild-gg/gameguild/issues/158) | Session-limit eviction persisted termination without forwarding its authentication audit event | Forward Authentication.SessionTerminated after each successful eviction update | Extend the existing single-eviction test; add multiple oldest-first evictions and persistence-failure coverage |
| [#301](https://github.com/gameguild-gg/gameguild/issues/301) | Permission history ignored a date filter when only one bound was supplied | Apply inclusive fromDate/toDate filtering before the limit on the tenant-query path | 12 cases: inclusive bounds, limit ordering, empty matches, existing indexed range path, order, global scope, zero limit, cancellation and repository failure |

The existing event sink and permission repository contracts are preserved. These changes introduce no controller, database migration or public API shape.

The affected-test selector now includes Authentication.UnitTests, Authorization.UnitTests and API.UnitTests for changed Identity source modules, following the repository's AGENTS.md minimum security/architecture test requirement. The existing core fallback also selects SharedKernel.UnitTests. Four selector regression cases exercise Authentication, Authorization, Context and deduplication.

## Acceptance evidence

The selector's 12 Node source tests passed without starting a database, SDK, application or browser. Its two solution-configuration cases inspect solution mappings; they do not compile C#.

Native C# compilation and focused/full tests are required in hosted CI before merge. A prepared test file is not a passing test result. Preserve and publish matching-head CI evidence, including the 12 new permission-history cases and the session eviction cases.

## Remaining original issue scope

- #158 retains all 10 original criteria. The session-eviction correction advances criterion 6; universal event-path completeness still needs verification. Required SAML provider auditing remains dependent on open [#268](https://github.com/gameguild-gg/gameguild/issues/268).
- #301 retains all 30 original criteria and the organized #351/#359 context. Legacy mutation paths still need complete before/after snapshots. Administrative approval/rollback, complete monitoring, SIEM delivery, retention/export, visualization and other retained requirements require their own acceptance evidence.

Both issues remain open. This increment must not auto-close either issue.
