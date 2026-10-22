# Issue 166: mapped entity properties in automatic audit capture

## Observed defect

The hosted API integration execution for PR #940, source
`1bdd550d07fb0b03c9d59dad3dc1a33c65464a89`, recorded:

- `Could not capture an entity change audit record for TenantMember`.
- `System.Reflection.AmbiguousMatchException` resolving `TenantMember.TenantId`.
- The catch in `EntityChangeAuditCapture.Capture` then omitted the entity's record.

Evidence: [API job 119481247749](https://github.com/gameguild-gg/gameguild/actions/runs/39674616446/job/119481247749),
artifact `affected-api-1bdd550d07fb0b03c9d59dad3dc1a33c65464a89`, artifact ID
`12381164399`, SHA-256
`6423bf0b4c1738533079e1868343df9ced56128c8bc27230e81c59b1870847a1`.
The actual tested merge was `1ebe37d45fc0fdedb9cd05483fd7b1a5351d337b`.
The warning appears in the server output embedded in the integration TRX.

`TenantMember` hides the nullable `EntityBase.TenantId` with a required `Guid`.
EF's mapping resolves the actual member, but the sensitivity check repeated a
CLR lookup using only its name. That lookup is ambiguous for hidden members.

## Correction

Audit capture now checks `PropertyEntry.Metadata.PropertyInfo`. The cache uses
that resolved member as its key. Shadow properties have no CLR attribute to
inspect and remain part of the snapshot. The change introduces no extra queries
and retains the existing transaction, actor, tenant, snapshot and row-count paths.

Inherited sensitivity is checked with `Attribute.IsDefined`. Microsoft's
[reflection documentation](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.memberinfo.isdefined?view=net-10.0)
explains that `MemberInfo.IsDefined` ignores its inheritance argument for
properties; `Attribute.IsDefined` supports the property inheritance chain.
An unrelated hidden base property does not mark a new mapped property sensitive.

## Validation required on this source

No local build, SDK, test, database or container execution was performed.
The following definitions require successful hosted execution on the exact
candidate and merge tree before acceptance:

| Test class | Cases | Required evidence |
| --- | ---: | --- |
| `EntityChangeAuditMappedPropertyTests` | 4 | Added/modified/deleted snapshots retain a hidden sensitive property, inherited sensitivity, a shadow property and visible values; an unrelated unmapped sensitive base property does not redact the mapped public property |
| `EntityChangeAuditMappedPropertyPostgreSqlTests` | 9 | Three real membership lifecycle operations, three real user-secret lifecycle operations, two-tenant bulk capture without recursion, transaction rollback and failed persistence |

The native cases use the migrated PostgreSQL fixture and the real
`ApplicationDbContext.SaveChangesAsync` path. They inspect persisted records from
a separate context, verify the business mutation, and require exact audit counts.
The membership snapshots must use the mapped tenant even when the hidden base
property holds another value. Sensitive values are randomly generated fixture
data and must never appear in persisted snapshots.

The existing eight `EntityChangeAuditCaptureTests` remain required regression
coverage for other entities, operation context, redaction, bulk writes, affected
row counts, audit-store exclusions and rollback. The complete affected API suite
and all current PR checks must also pass.

## Full issue 166 remains open

The issue requests ten acceptance criteria. Checked boxes in its body are not
substitutes for a complete requirement and evidence review:

| Original criterion | Evidence or remaining review |
| --- | --- |
| Central logging interface across modules | `IAuditService` and `AuditService` exist; reconcile actual module integration |
| Universal automatic entity changes | This fixes a demonstrated omission in tracked writes; inventory remaining write paths |
| Standard schema and metadata | `EntityChangeAuditRecord` and audit models exist; reconcile complete event families |
| EF change-tracking hooks | The async context hook exists; verify every supported persistence entry point |
| High-volume bulk logging | Native batch correctness is covered here; high-volume behavior remains to be qualified |
| Pluggable abstraction | Verify registration and alternate implementation contracts |
| Integrity and completeness | Transactional cases here; broader validation, retry and integrity requirements remain |
| Configurable filtering | Reconcile service configuration, intentional exclusions and sensitive-field policy |
| Concurrent operations | The cache remains concurrent; qualify service and persistence concurrency separately |
| External audit/compliance integration | Reconcile delivery, failures and evidence with related audit issues |

For example, `ApplicationDbContext` currently overrides the cancellation-token
async overload, while other persistence overloads require explicit review.
`AssetContentRepository`, `PromoCodeRepository`, `RefreshTokenRepository` and
`UserNotificationRepository` contain set-based updates outside tracked saves;
their dedicated event/audit coverage must be mapped before claiming universal
capture. This change does not alter those paths or declare them accepted.

No issue closure or recertification is justified by this increment alone.
