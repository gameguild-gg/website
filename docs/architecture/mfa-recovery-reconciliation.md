# MFA recovery reconciliation — issues #223 and #243

## Requirement provenance

The original issue bodies were empty. The previously recorded acceptance on
[#243](https://github.com/gameguild-gg/gameguild/issues/243#issuecomment-5845883517)
requires ten default 12-character codes, salted/versioned PBKDF2-HMAC-SHA256 at
600,000 iterations, legacy SHA-256 compatibility, concurrent single use and a
five-failure/15-minute lockout. These criteria remain binding. #243 was already
reopened by the independent review; its open child #285 retains its own functional
authenticator acceptance.

[#223](https://github.com/gameguild-gg/gameguild/issues/223#issuecomment-5845886338)
also retains password recovery and configured sandbox email delivery acceptance.
Improving backup storage does not establish that delivery or an undefined support
identity-proofing flow. Neither issue is closed by this working document.

## Implementation decisions

- Keep the existing backup-code database column and legacy comma-separated SHA-256 sets.
  New sets encode their original issued count alongside the remaining hashes.
  A deployment changing the configured generation count cannot rewrite that count.
- New hash entries carry algorithm, iteration count, random 128-bit salt and a
  256-bit derived value. Comparisons use constant-time byte comparison. The maximum
  configured 20-code set fits the existing 2,000-character column.
- Use optimistic concurrency across the MFA row's security state, including the
  backup set, failed attempts, lockout, enablement, setup completion and encrypted
  secret. A stale write must never resurrect a consumed or regenerated code.
  Reload after a conflict before a bounded retry; report success only after saving.
- Keep legacy hashes verifiable until consumed or regenerated. Their historical
  original set size is unknown. The API must represent unknown total/used counts
  explicitly rather than assume ten or today's configuration.
  The v1 numeric fields remain numeric: when unknown they carry lower bounds
  (remaining count/zero) and `areUsageCountsKnown=false`. They cannot be used as
  historical evidence. Configuration includes nullable `backupCodesIssued`.
- Backup codes cannot complete a pending TOTP enrollment. Successful TOTP
  confirmation records completion. Existing enabled installations stay compatible.
- New TOTP enrollments persist a fixed nullable deadline. Failed attempts updating
  the row cannot extend it. Legacy pending rows without a recorded deadline retain
  the previous update-time fallback; their original start time cannot be invented.
- Cancellation propagates without returning success or exposing uncommitted codes.
  A cancellation after a committed consumption cannot roll that consumption back.

## Executed local evidence

Baseline at develop `09e1714d4d0b003229f80c7ab43e5894a2c930f1` reproduced four
unit failures (length, hashing, pending enrollment and cancellation), plus a real
PostgreSQL failure accepting one code twice through independent stale contexts.

| Current verification | Passed executions | Boundary |
|---|---:|---|
| Authentication unit suite | 2,083 | 23 new crypto/recovery/TOTP cases; synthetic configured AES key, real crypto and independent OTP calculation |
| Authorization unit suite | 1,667 | Existing authorization regressions |
| SharedKernel unit suite | 1,371 | Changed default configuration and existing regressions |
| API architecture/security/JWT selection | 106 | Architecture/security namespaces, host security registration and real bearer HTTP checks |
| PostgreSQL/HTTP recovery suite | 9 | Real migrated disposable PostgreSQL, actual repository/state transitions; HTTP test principal is a fixture, not a new bearer certification |
| Client contract suite | 3 | Existing numeric v1 fields, additive unknown-history flag and issuance metadata |

The current recovery tests cover deterministic stale-context replay, simultaneous
verification, stale failure after consumption, five concurrent failures/15-minute
lockout, stale verification after regeneration, legacy status, authenticated actor
selection, generation/consumption counts, replay rejection and anonymous status/
regeneration denial. MFA verification retains its existing reviewed anonymous
user-ID endpoint and returns no access/refresh tokens; this work does not certify
the separate sign-in challenge/session completion flow.

The final configured Release solution build exited 0 with zero warnings/errors.
EF reports no pending model changes. Migration adds only nullable
`setup_expires_at`; concurrency predicates require no additional schema columns.
Source/API/integration copies of the authentication assembly have identical SHA-256
`4d683f7443088db92e73a76a7dc84410e67bfa595c826d15d4fd26f1b73d1768`.
Offline OpenAPI retains all 1,296 paths and 1,654 schemas; only the backup-status
and MFA-configuration schemas change. Client regeneration/type checking pass.

Earlier nullable-status revision, incorrect assertion expecting the null access
token field to be absent, interrupted native processes and an overlapping build
with DLL locks are retained in local artifacts and excluded from acceptance.
The broad API unit run interrupted before fresh serialized checks is not claimed
as passed. Earlier repeated/superseded focused executions are not added to totals.

Codacy follow-up makes the retry stop condition explicitly test the bound and
replaces fixed public test keys with cryptographically generated synthetic keys.
The API rebuild is warning/error clean; the full 2,083 authentication and nine
PostgreSQL/HTTP cases pass again. These repeated cases are not additional coverage.
Chrome inspection was attempted twice, but the connector timed out on
`Emulation.setFocusEmulationEnabled`; GitHub's check annotations supplied the four
findings. None was suppressed or dismissed.

Fresh Release API architecture/security/JWT selection again passed 106/106. An
earlier Debug repetition crashed after 75 cases and is excluded, not added to totals.
Exact head `11b41bf063181ab821368351be7afe3cff98c53a` passed all four main CI suites:
151 API integration, 1,049 API unit, 2,083 authentication and 1,371 SharedKernel
(4,654 executions). The job then reached its 20-minute limit during the final
OpenAPI test-project build, so its aggregate gate is not accepted as a pass.
The prior head completed in about 19 minutes. The API job allowance is extended to
30 minutes to accommodate this measured variation; all test selections, warning
requirements and final HTTP verification stay active. Matching-head gates must
run again before merge.

## Remaining acceptance

PR #686 merged to develop at `8e1266698a0998364ab39a34078e0fd16eaa17c8` on
2026-10-05 at 03:31:03 UTC. Accepted head `10e1df87c50d4f6a8280b42b6ffcdfff450b2429`
passed all applicable gates, Codacy and four CodeQL analyses. Its CI passed 4,654
principal executions (151 API integration, 1,049 API unit, 2,083 authentication and
1,371 SharedKernel), then 14 repeated OpenAPI HTTP cases. Both Release builds
were warning/error clean. The prior timeout stays excluded from aggregate acceptance.
Primary develop was synchronized with all 55 local files preserved; the integrated
branch was removed locally/remotely, two checkouts are reused and no stashes remain.

Configured provider email delivery for
#223 and a real authenticator QR round trip for #285 remain unverified. #243 keeps
its native child and its requirement provenance. No issue is closed by this code
increment or by the local test totals; closure requires its remaining acceptance.
