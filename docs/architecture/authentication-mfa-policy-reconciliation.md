# Authentication configuration and MFA reconciliation — #145

## Tenant-login contract and hosted Code evidence — 2026-10-09

At signed head `278d67e81d4d0ad2b80bf344eb392ee47a6377cc`, Economy job
`113825631339` completed all 477 API integration and 1,090 API unit cases without
failures or skips. The complete HTTP budget correction therefore ran successfully.
The subsequent Authentication integration suite passed 47 of 48 cases; its one
failure expected `AvailableTenants` from a password-only login with `Owner`
memberships. Current elevated-role policy correctly returned a limited MFA
enrollment challenge, preserving the selected tenant but withholding ordinary
credentials and the complete tenant list until the second factor succeeds.

The existing tenant-login test now covers three real service paths: `Member`
must receive successful credentials and all three memberships; `Owner` and
`Admin` must receive a bound MFA challenge, no ordinary tokens or session, no
complete tenant list, and no additional persisted session. Production policy,
credential issuance and response projection are unchanged. No MFA requirement
is disabled to satisfy an outdated password-only expectation. This source
correction requires fresh hosted execution; no local SQL or full-suite run is
claimed. The original failed log, artifact and TRX are retained under
`pr704-278-economy-failure92-20261009`.

The earlier Emception job `113782448171` for head `8fa5106a` completed its actual
learner submission and instructor grading cycle: 36 functional checks passed,
zero hard failures, zero known-red failures and two console observations.
Persisted typed code, all three grading results, final grade, feedback and SQL
rubric payload passed. Its unchanged job log is
`pr704-8fa-code-completed93-20261009.log`. This historical result does not replace
the current candidate's Code, Economy or security gates, and closes no issue.

## Migration-guide deliverable — 2026-10-09

The live #145 audit reopened the issue at develop `e6fc4a0b` for its original
Definition of Done requirement, "Migration guide for existing authentication systems."
The [new guide](../api/authentication-migration-guide.md) maps actual legacy aliases
to the host's nested typed sections, explains the separate security/MFA/session
sections, preserves credential and JWT/session/tenant compatibility, adapts pending
MFA consumers, specifies rehearsal evidence, staged cutover and rollback. It identifies
unsupported credential imports and external-provider checks instead of asserting
automatic compatibility. The options reference links to the guide.

This is a documentation delivery. No production migration or external-provider
sandbox execution is claimed. #145/#288 and the complete original criteria remain
open until applicable execution and integration evidence is accepted. Hosted Economy
and the real instructor/learner Code cycle remain required for #704.

## Complete HTTP regression budget — 2026-10-09

At signed head `d3eba856e8ddda89451650b5c5093e8211fa1c65`, hosted API Verify
job `113807112417` passed all 477 API integration tests in 13 minutes 32 seconds,
all 1,090 API unit tests, and all 15 full-application OpenAPI HTTP tests. The
Economy job `113807112317` built the complete Release solution without warnings
or errors and passed its Economy suites and the 1,090 API unit tests. Its full
API integration process was interrupted after exactly 720 seconds with status
124. The immutable Economy artifact `11615227174` contains that timing and no
completed integration TRX; this interruption is not a successful test result.

The complete HTTP suite therefore receives a separate 20-minute process budget,
with margin above the independently observed 13m32s passing run. API unit tests
retain their 12-minute budget; other projects and the per-test hang detector
retain five minutes. `ECONOMY_API_INTEGRATION_TEST_TIMEOUT` can override only the
HTTP suite's process budget. No test is filtered or skipped and no acceptance
criterion is relaxed. The mocked runner regression checks each default budget,
the dedicated override, the unchanged hang detector, and absence of a filter.
Fresh hosted Economy and instructor/learner Code acceptance remain required.
This correction runs no local database, container, or SQL and closes no issue.

## Economy analyzer gate compatibility — 2026-10-09

The Economy Release Gate for published head `8fa5106ad5026d01faa02cbd063bec96b2060471`
checked out merge ref `a8c12999ce0af5b05f9e3ba9e45992d3350aa183`, including newer
develop `e6fc4a0b5c15e922cbdb7ad05711119efcbcc003`. Its ordinary whole-solution
Release build failed with two `xUnit1030` errors in the API key rotation grace
test at lines 602 and 609. This source was introduced by the newer develop;
the original failed CI log is retained as `pr704-8fa-economy-job-download47-20261009.log`.

The same develop revision is now integrated in the existing worktree. The two
awaits retain the xUnit synchronization context by removing `ConfigureAwait(false)`.
The grace-period authentication, rejection and lazy-revocation assertions are
unchanged. No analyzer is suppressed, and the CI warnings-as-errors requirement
is unchanged. Offline qualification is recorded in
`economy-analyzer-offline-validation50-20261009`; the combined compiled OpenAPI
and generated consumer are qualified separately in `economy-fix-client52-20261009`.
Neither receipt substitutes for the fresh hosted Economy and real learner/instructor
Code cycle required before merging #704.

The four ordinary Release builds and all 4,861 selected native tests passed
(2,714 Authentication, 2,014 Authorization, 133 host architecture/security/OpenAPI),
with no test failures or skips. The original collector nevertheless exited one:
Git's default abbreviated index hashes changed from nine to ten characters during
validation. `economy-validation50-qualified-diff-reconciliation57-20261009.json`
reproduces the original frozen diff SHA256 exactly with explicit nine-character
abbreviations, checks every command exit and individual TRX case, and confirms
the original receipt and all 55 primary changes unchanged. Subsequent guards use
full Git index hashes. The original failed collector is preserved separately.
The compiled presentation export contains 1,346 paths and 1,711 schemas; it
does not replace HTTP acceptance.

The client was regenerated from that compiled contract. Ordinary build including
declarations, typecheck and all 1,155 tests passed with zero failures or pending
cases. Its source, individual assertions and generated hashes are recorded in
`economy-fix-client52-20261009/result.json`; its normalized metadata hash is
`374f585c8e87363a1450c0de57fd39910080a1900ea4e5459ad817bda3b63877`.

At the prior published head, API, Web, OpenAPI consistency, migration compatibility,
CodeQL and Codacy passed. The GitGuardian check's only two findings were verified
against their original commits: a runtime-generated synthetic fixture password
and the public RFC 6238 Appendix B key. Its supported false-positive action
recorded the specific classification; the check conclusion is `skipped`, not a
clean scan. Receipt and screenshot `pr704-8fa-gitguardian-false-positive-confirmed54-20261009`
preserve that disposition. No file/rule exclusion or history rewrite was added.

The PostgreSQL containment hold remains in force: this qualification creates no
local database or container and performs no SQL. All 55 unrelated primary-checkout
changes remain protected. This checkpoint accepts or closes no issue; #145 and
#288 retain their complete original criteria.

## Native cancellation correction and Code attempt03 — 2026-10-08

The web consumer checkpoint is published in #704 at
`2bda92a60f4a58a7473b877f02c3fd250f96e39d`. Real Code attempt03 passed its
28 runtime contracts, trusted runtime build and CDN checks. PostgreSQL became
ready within the unchanged 60-second budget. The native API and web booted;
first-factor sign-in and limited authenticator enrollment returned HTTP 200.
MFA completion then exceeded the unchanged 30-second request deadline, before
the student submission and instructor grading cycle. Its inherited report from
an earlier run is excluded from current functional acceptance.

The API recorded session creation, followed by cancellation. The operation
behavior reused the canceled request token for rollback and masked the original
exception. Two new tests reproduced this against the original source and the
qualified native22 API binary: both failed. Their assertions remain unchanged.
Rollback now uses an independent, uncancelable token; cleanup clears tracked
writes, pending permission audit snapshots and retry state in a `finally` block.
A rollback transport failure is logged by error type and the original command
exception is preserved. Authentication command data is excluded from that log.

The affected host Debug build passed with zero warnings/errors, using the
unchanged, qualified module references. All **147** host transaction,
architecture, authentication, security and migration cases passed with no skips
when provided their supported real PostgreSQL connection. The first run's
**145 passed / 2 failed** Docker named-pipe connection result is retained.
The entire authentication (**2,535**) and authorization (**1,667**) projects
also passed with no skips: **4,349** cases across the three current selections.
Sources and all 55 unrelated primary-checkout changes were preserved, and the
exact owned PostgreSQL container was removed. A new complete Code run is still
required before this checkpoint can support broader acceptance.

Receipts under the existing artifact root include
`code-mfa-cancellation-baseline-cached-20261008/result.json`,
`code-mfa-cancellation-api-build-20261008.log`,
`code-mfa-cancellation-validation-host-20261008/results.trx` and
`code-mfa-cancellation-validation-host-postgres-20261008/host/results.trx`.
`code-cycle3-postgres-final-cleanup-reconciliation-20261008.json` independently
verifies the exact IDs and names of both the attempt03 PostgreSQL container and
the tmpfs readiness diagnostic absent. Earlier timeout/removing observations
and failed receipts remain intact. No issue is accepted or closed by this step;
#145 and #288 retain their full original requirements.

## Web MFA consumer checkpoint — 2026-10-08

The existing #704 branch incorporates develop merge #705 at
`7e91dcaa0486789cff02081e56a49e752c3ab405`. The conflict retains both the
uniform email-verification message and the monotonic credential timing floor.
This checkpoint connects the browser credentials consumer to the native limited
MFA enrollment/completion endpoints; #145 and #288 remain open.

- Credentials preserve the opaque expiring first-factor bearer and available
  methods. Completion sends only bearer, method and code: it does not resend a
  password, supplied actor or supplied tenant. An incomplete HTTP success cannot
  become an authenticated provider result.
- Next enrollment and completion require the existing CSRF protection. Enrollment
  exposes only provisioning fields and never sets an ordinary session cookie.
  Error logging records the error type, without serializing the challenge bearer.
- The existing sign-in form supports TOTP, recovery codes and manual authenticator
  setup. Challenge/provisioning information stays in mounted component/hook memory.
  Recovery codes are shown once after confirmation, before client redirect, and
  are excluded from JWT/session storage. Ordinary sign-in retains its layout.
- The Code runner continues the administrator's required MFA before creating a
  course and through browser CSRF/cookie handling before entering SpeedGrader.
  Its independent RFC 6238 helper waits for a new counter rather than disabling
  native replay protection. Original Code functional checks remain verbatim.

Current combined sources passed Authentication 2,535, Authorization 1,667,
required audit 6, host architecture/security 137 and PostgreSQL HTTP/migration 34:
4,379 tests, with no failures/skips. Client build/typecheck and all 1,155 client
tests passed. All 21 form tests and six TOTP support tests passed.
Native22's cleanup collection timeout remains in its original receipt; separate
exact ID/name checks confirmed the owned container absent and all 55 primary
checkout files preserved.

The actual local Code cycle has not passed. Attempts 1 and 2 rebuilt the trusted
runtime and passed its 28 frozen contracts, but stopped before API/browser tests
because the disposable PostgreSQL did not become ready within 60 seconds.
The previous report is inherited historical evidence and must not be treated as
this checkpoint's functional result. The current Debug API build was reused with
recorded hash/source provenance; full CI validation is still required. No timeout,
MFA policy, grading assertion, browser sandbox or scanner was relaxed.

Evidence under `D:/Codex/work-artifacts/release-2026-11-25/gameguild-issues-01a0d900/`:

- `native22-post-develop-merge-cleanup-reconciliation-20261008.json`
- `issue-145-web-mfa-client-validation-20261008/result.json`
- `web-mfa-credentials-baseline-20261008.json` and `web-mfa-handlers-baseline-20261008.json`
- `web-mfa-form-baseline-20261008.json` and `web-mfa-form-final-source-20261008.json`
- `issue145-web-mfa-real-code-cycle-20261008/result.json`
- `issue145-web-mfa-real-code-cycle-attempt02-20261008/result.json`

The published native checkpoint `2868db9576563253e8405e0ec43cf5c0f1a92a16`
passed CodeQL. GitGuardian still fails and Codacy requires action. Release/API/
OpenAPI licensing, fresh combined CI/Code acceptance and the remaining original
#145 requirements remain merge/closure gates. This checkpoint closes zero issues.

## Native limited enrollment checkpoint — 2026-10-08

This follow-up to published PR #704 head `a99605af74bd2a46aa2887e21bd0e0915e6f4394`
adds a locally validated limited enrollment flow. #145 remains open: its twelve
original acceptance criteria, technical requirements and fifteen definition-of-done
items below remain authoritative. Publication/CI and the complete issue are separate gates.

`POST /v1/auth/mfa/sign-in/enrollment` selects identity, tenant, account version and
policy only through the server's expiring first-factor challenge. It returns TOTP
provisioning data with an expiry bounded by that five-minute challenge, and creates
no ordinary token, authenticated session or recovery code. The database records
configuration identity, a canonical-secret fingerprint and initialization time,
never the plaintext challenge bearer or provisioning data in the challenge.
The same live challenge resumes the exact pending configuration. Another challenge
cannot overwrite a live setup. Subject transaction locks and existing configuration
row locks remain held through proof verification and credential commit.

Enrollment completion accepts TOTP only, rechecks exact setup/account/tenant/version/
policy, consumes the challenge once, and persists the existing MFA session proof.
Recovery codes are generated and returned once, only after verified TOTP. Challenge,
configuration, replay watermark, session, refresh token, recovery set and required
MFA audit writes share the owning transaction. Required MFA audit failures now
propagate through the central audit bridge. Ordinary/signup responses omit the new
nullable `mfaEnrollmentBackupCodes`. A separate signup-success contract decision
remains unresolved in this chat; this checkpoint does not change signup behavior.

Native EF Core 10.0.9 generated `20261008125046_BindLimitedMfaEnrollment` and its
snapshot. It adds challenge binding columns/checks and a unique user MFA index.
An explicit preflight aborts on duplicate historical configurations while preserving
every factor row for reconciliation. No factor is automatically chosen or deleted.
The historical challenge-table emptiness test now projects its existing Id column
when testing the older migration, preserving both original empty-table assertions.

| Verified selection | Passed | Scope |
|---|---:|---|
| Authentication UnitTests | 2,528 | Entire native project, including enrollment proof and trusted remote peer tests. |
| Authorization UnitTests | 1,667 | Entire native project. |
| Required MFA audit | 6 | Audit sink success and required-failure propagation. |
| API architecture/security | 137 | Focused native host selection. |
| Configured OpenAPI documentation | 8 | Native documentation selection. |
| MFA PostgreSQL HTTP/migrations | 34 | Original password/completion/replay/refresh cases plus limited enrollment, exact resume, concurrent confirmation, expired/stale/revoked bindings, failed TOTP accounting, backup rejection, audit rollback and migration upgrade/down/duplicate preservation. |
| Native OpenAPI capture | 1 | Actual host export; completion/enrollment request/response schema independently checked. |
| Generated TypeScript client | 1,140 | Native-document generation, build, typecheck and complete client tests. |

The module generator now honors the same strict boolean AllowAnonymous metadata as the endpoint generator. Its original regression had one anonymous-marker failure and eight passing protected/empty-security controls; the assertions are retained. Regeneration changes only authentication decisions in affected modules, and the full client build/typecheck/test selection includes those regressions.

These bounded selections passed with zero failures/skips. Unit17's four completed
groups bind byte-identical production/unit sources; its final Integration compilation
failure remains recorded. The two subsequently repaired integration test files
passed in native20. Native20's and capture21's cleanup collection timeouts also remain in its original
receipt: separate exact-ID/name inspection confirmed absence. No failed receipt is
rewritten as a successful full run. Baseline10's original required-but-unenrolled
regression reproduced HTTP 404 and remains byte-for-byte unchanged; it now passes.
Capture19 failed before tests during Docker startup; capture21 uses the exact owned container after verified recovery. Earlier compiler/restore/precondition failures are retained separately.

Receipts under `D:/Codex/work-artifacts/release-2026-11-25/gameguild-issues-01a0d900/`:

- `native-enrollment-validation20-and-unchanged-unit17-reconciliation-20261008.json`
- `issue-145-native-repository-enrollment-openapi-capture21-execution01/result.json`
- `native-openapi-capture21-cleanup-reconciliation-20261008.json`
- `issue-145-limited-enrollment-anonymous-client-generation-20261008/result.json`
- `issue-145-limited-enrollment-anonymous-client-validation-20261008/result.json`

The 55 unrelated primary files remain preserved; owned test PostgreSQL containers
are verified absent. The Debug native host builds; Release/API/OpenAPI CI still
requires the ImageSharp license decision. GitGuardian's exact a996 occurrence is a
synthetic per-test generated password, but scanner disposition is still pending.
Chrome login reached personal workspace 934842; direct navigation to the check's
incident in workspace 934528 redirects back to the personal workspace without
integrated sources. No integration/access expansion or scanner waiver was performed.
The published Code pipeline also needs the administrative credentials consumer to
complete required MFA instead of attempting course creation with an empty token.

The live fixed-scope refresh records #287 independently closed by merged PR #705
(`ae4301fa045170e51340f93aa076a93b14963ceb`, 2026-10-08 13:22:44 UTC), for uniform
email-verification messages and delivery-failure responses. Its timing child #288
remains open. That develop update has not yet been incorporated into this locally
validated source; its reported 2,323 unit tests are separate evidence and are not
attributed to this checkpoint. Every scoped issue ID remains in the matrix.

Remaining #145 work includes complete MFA integration with the web sign-in flow,
other sign-in schemes, high-risk whole-flow acceptance, ordinary-issuance role/tenant
races, external-provider sandboxes, legacy/recovery requirements and the remaining
original criteria. This checkpoint performs no merge or issue closure.

## Scope and provenance

The original #145 request has twelve acceptance criteria, technical requirements,
and a fifteen-item definition of done. The original #273 description is retained
in canonical #145. Configuration switches and an endpoint's existence alone do
not establish enforcement or successful completion of an authentication flow.

This checkpoint uses the owned checkout based on accepted develop
`d5f417328e9ff9d3fa0517796a623e310aeb1ef0`, published PR #704 head
`b4cf8156baf4ea74a5735146cf70af328564f022`, and its separately guarded local
follow-up. #145 remains open. The separate ASP.NET Core Identity persistence
requirement is recorded as historically superseded in
`docs/api/authentication-options-configuration.md`; the current custom identity
stack does not literally implement that original framework requirement.

## Original acceptance criteria

| Original requirement | Existing integration | Remaining evidence or implementation |
|---|---|---|
| JWT Bearer token authentication with custom claims | `SecurityServiceCollectionExtensions.SetupAuthentication`, `JwtTokenService` | Run current issuance/validation and invalid issuer, audience, lifetime, signature, subject and token-version cases; reconcile custom claims. |
| OAuth2 with Google, Microsoft and GitHub | `OAuthService`, `OAuthAuthService`, typed provider options | Verify state/PKCE, callback exchange, provider identity and tenant binding; run configured provider sandboxes. |
| API keys from header, query and custom sources | `ApiKeyAuthenticationHandler`, configured resolver registration | Run real authentication boundaries for all sources, conflicting sources, HTTPS, expiry, revocation and default scheme preservation. |
| Cookies with secure defaults | Opt-in cookie scheme in `SetupAuthentication` | Verify issuance, deletion, expiration, sliding behavior, secure/HttpOnly/SameSite and CSRF boundaries. |
| Basic authentication for legacy systems | Opt-in `BasicAuthenticationHandler` | Verify HTTPS and account state; enrolled MFA is rejected, but global/subject policy also needs reconciliation. |
| Configurable token expiration and refresh policies | Typed JWT options; accepted PR #699/#263 lifecycle and containment | Preserve accepted rotation, family/account containment, durable notification and session deadlines; validate configuration precedence and public-boundary expiration. |
| MFA integration points | TOTP, backup codes, policy reader, MFA endpoints and authenticated step-up receipts | Reproduce mandatory-policy behavior before claiming a login bypass; enforce a complete first-factor/second-factor flow before ordinary token/session issuance. |
| Custom authentication scheme registration | Additional scheme callback in `SetupAuthentication` | Verify coexistence, named selection and JWT defaults at real middleware boundaries. |
| HSTS, CSP and X-Frame-Options configuration | `SecurityHeadersMiddleware`, pipeline HSTS configuration | Verify actual responses and supported environments, including authenticated and failure responses. |
| Account lockout and rate limiting policies | Shared password admission follow-up and rate-limiting middleware | Current shared-admission HTTP selection passed 78/78, including all 12 added polymorphic account/IP/held-lock cases. This is not every authentication scheme or the complete API Integration project. |
| Password complexity configuration | Typed options and `PasswordHasher` | Verify enforcement at registration, reset and change, policy precedence and long-input handling without weakening existing rules. |
| External identity provider integration | Typed provider configuration and OAuth dispatch | Verify callbacks, external identity binding and configured provider sandboxes; configuration alone is insufficient. |

Technical requirements for concurrent schemes, thread safety, performance,
security audit, authorization integration, migration guidance and comprehensive
tests remain part of the issue. This table does not reduce them to MFA alone.

## Actual-source MFA observations — 2026-10-08

Artifacts are under
`D:/Codex/work-artifacts/release-2026-11-25/gameguild-issues-01a0d900/`.
The diagnostic calls anonymous HTTP against actual API/authentication assemblies,
real password hashing, JWT/session services and migrated PostgreSQL. Only the
risk classifier is controlled to low risk; policy results are not mocked.

1. Baseline03 failed package restore before executing either case. Its original
   failed receipt is retained. Exact container removal was independently verified
   absent at 03:52:59 UTC after an earlier removal collection timeout.
2. Baseline04 executed two cases: one passed, one failed before the true-policy
   sign-in request because the real policy reader returned false despite the
   effective `IOptions<MfaOptions>` value being true. The original assertions,
   log and failed TRX are retained. Its owned container was verified absent.
3. Baseline05's Docker creation collection timed out before any tests. The exact
   owned container subsequently started; its identity, label and loopback binding
   were checked before a separate recovery execution.
4. Baseline05 recovery executed two cases: one passed, one again failed the
   policy precondition before sign-in. Source, primary files, loaded assemblies
   and harness guards stayed unchanged. Cleanup returned zero and exact-ID
   inspection confirmed absence. Log/TRX SHA256:
   `a63aaa6c8a04f3d59ca035e3c67a9f1882f35cdee63f096b0eb8ae8a75ddf2bf`
   / `3849643685673772a67cbfcacdf41bcf094aa3e1d19aea502944712a6b62ba48`.
5. Baseline06 keeps every original assertion and additionally requires actual
   `MfaService`/`MfaAttemptTrackingService` implementations and their effective
   captured options. The real policy reader is constructed with the explicit
   effective options to qualify the factory configuration seam. Results are
   pending. No runtime bypass is established by the preceding failed preconditions.

Static inspection still identifies two concrete concerns: low-risk local password
sign-in does not call the MFA policy reader, and that reader uses the current
HTTP principal's roles instead of resolving the requested subject. Anonymous
sign-in cannot obtain subject-role policy from an authenticated caller principal.
The high-risk response currently returns an unpersisted GUID as step-up evidence.
These source findings require implementation and functional acceptance.

## Required complete flow

- Resolve the authenticated first-factor subject and active tenant membership on
  the server. Evaluate enrolled MFA and required policy for that subject before
  issuing ordinary access/refresh tokens or persisting an authenticated session.
- Persist a short-lived, cryptographically random pre-login challenge. Store a
  hash of its opaque bearer value; bind subject, tenant, token version, validated
  first factor and expiry. Do not accept a client-supplied subject as authority.
- Complete verification through supported MFA providers and existing backup-code
  protections. Expired, replayed, mismatched, revoked or unavailable challenges
  must fail closed. Concurrent consumption must allow at most one issuance.
- Revalidate account, token version and tenant access at completion and preserve
  accepted session/refresh lifecycle invariants. Audit requirement, failure and
  successful completion without recording credentials, codes or bearer values.
- Required but unenrolled accounts need a limited enrollment/recovery flow; they
  must not obtain a normal access token to satisfy enrollment.
- Reconcile Basic, cookies, OAuth and other sign-in methods against the same
  requirement. High-risk login needs usable, persisted verification evidence.
- Preserve existing request/response contracts and regenerate API clients when
  new completion contracts are accepted.

Existing `StepUpReceiptService` requires an authenticated actor, tenant and
session. It cannot directly represent an anonymous pre-login challenge. Existing
`POST /v1/auth/mfa/verify` verifies a supplied user's code and returns a boolean;
it must not become an ordinary token issuer without first-factor and challenge
binding.

Acceptance must include mandatory/enrolled/admin MFA, no ordinary token before
verification, successful TOTP and backup-code completion, failed/expired/replayed
evidence, concurrent consumption, tenant/account/token-version changes, lockout,
enrollment/recovery and every supported sign-in method. Native/security checks
and external-provider sandbox evidence remain required before #145 closes.

## Qualified mandatory-policy defect

Baseline06 again failed before sign-in, this time at the effective options
precondition. That failed result and its original assertions are retained;
no login-enforcement conclusion is drawn from it.

Baseline07 uses the normal test-host `PostConfigure<MfaOptions>` seam to qualify
enforcement independently of configuration-provider ordering. The real
`MfaAttemptTrackingService` and `MfaService` remain in use; no policy result is
mocked. Effective, directly resolved and captured MFA options all match the
requested setting, and the real policy precondition passes.

The execution completed at **04:24:08 UTC**: two cases executed, one control
passed and one mandatory-policy case failed, zero skipped. For the mandatory
case, actual anonymous low-risk password HTTP returned ordinary access and
refresh tokens, with one persisted refresh token and one session, despite the
real policy requiring MFA. The original no-ordinary-token assertion then failed.
This reproduces a local sign-in enforcement defect under the qualified test-host
options. It does not certify production configuration binding, every sign-in
scheme or complete #145 acceptance.

Source, primary files, actual loaded assemblies and harness stayed byte-guarded
unchanged; exact owned-container cleanup and absence were verified. Artifacts:
`issue-145-mfa-policy-http-current-execution07/result.json` and
`issue-145-mfa-required-policy-actual-http-defect-20261008.json`.
Log/TRX SHA256:
`6dcafeeafb367bb7cd8b938963cf0070ac3b008507b8659c452efa8c01d56786`
/ `8c9d0f8238aa2f4d191cfa33ee6ed10aa2e914759f534b7803bfa434c8b40712`.
This is retained defect evidence, not a passing acceptance result. The complete
flow above still needs implementation and verification. Its completion should
reuse `IAuthenticatedSessionIssuer` and the existing MFA providers while
preserving accepted session/root-refresh binding and command transactions.

## Native password preparation and persistence checkpoint — 2026-10-08

The validated challenge, subject-policy, TOTP replay-store and internal response
mapping sources are now applied to the existing authentication worktree.
Password sign-in captures the account token version before password verification,
checks the current account against that evidence, and calls the mandatory MFA
preparation service before its existing credential and session issuance path.
Required, enrolled and high-risk cases produce a persisted limited challenge.
The earlier unpersisted high-risk GUID has been removed from this path. A pending
outcome is retained through the mapper and command transaction; it contains no
ordinary access/refresh credentials or authenticated session.

Ordinary preparation carries its verified subject and policy decision so the
coordinator does not repeat the same reads. The original ordering assertion was
retained: the first native run exposed the extra reads, and the second run passed
all 2,488 authentication unit cases after correcting the implementation. The local
handler records a successful sign-in only when the response is successful.

The native migration `20261008091741_AddSignInMfaChallengesAndTotpReplayState`
adds only `sign_in_mfa_challenges`, `totp_replay_state` and the challenge indexes.
It stores challenge hashes and per-enrollment/key TOTP time-step watermarks;
challenge bearers, TOTP secrets and supplied codes are not added to these tables.
The native migration test checks upgrade, rollback, retention of an existing MFA
enrollment, and `HasPendingModelChanges() == false` without adding a warning
suppression to its database context.

The retained external regression execution ran the original two baseline07 HTTP
cases byte-for-byte plus the native migration case: **3/3 passed**, zero skipped.
Mandatory low-risk sign-in emitted no ordinary credentials, refresh row or session;
the optional policy control retained its normal token/session result. This fixes
the previously qualified password-sign-in defect under the same options seam.

The subsequent execution used the actual repository test projects:

| Native selection | Executed / passed | Scope |
|---|---:|---|
| Authentication UnitTests | 2,488 / 2,488 | Entire authentication unit project. |
| Authorization UnitTests | 1,667 / 1,667 | Entire authorization unit project. |
| API UnitTests architecture/security | 137 / 137 | Focused host architecture, authentication, security and migration-deployment selection. |
| API IntegrationTests MFA policy/migration | 3 / 3 | Native mandatory/optional HTTP cases and migration upgrade/rollback/model consistency. |

All **4,295** selected cases passed with zero skipped. The native HTTP coverage
also requires the real local-authentication implementation and checks the pending
response, persisted bearer hash, subject version, tenant, first factor, enrollment
purpose and five-minute expiry. This selection does not represent the entire API
UnitTests or IntegrationTests projects.

Receipts under the artifact root above:

- `issue-145-native-password-preparation-unit01-execution01/result.json` retains
  the original failed ordering assertion; unit02 retains its passing correction.
- `issue-145-native-migration-scaffold01-execution01/result.json` records the
  offline EF Core 10.0.9 scaffold and its exact generated files.
- `issue-145-mfa-policy-http-native-execution08/result.json` records the unchanged
  original HTTP requirements and the additional migration case.
- `issue-145-native-repository-validation03-execution01/result.json` records the
  four native selections, commands, source binding and log/TRX hashes.

Every execution preserved all 55 unrelated primary-checkout changes. Both owned
PostgreSQL containers were removed and their exact identities verified absent.
The full Debug API build had no errors and retained the existing ImageSharp
license warning; Release/CI license acceptance is unresolved.

**This is an implementation checkpoint, not complete #145 acceptance.** The
public challenge-completion and limited enrollment/recovery flows, MFA evidence
in issued credentials and sessions, completion/risk HTTP acceptance, concurrent
whole-command failure handling, and enforcement across other sign-in schemes
still require work. Account, tenant and role changes across preparation and
ordinary credential issuance also need explicit acceptance. Signup contract
changes remain dependent on the pending #287 product decision. All original
#145 acceptance criteria, technical requirements and definition-of-done items
remain authoritative; the issue is open.

## Native public completion and session proof checkpoint — 2026-10-08

`POST /v1/auth/mfa/sign-in/complete` now completes a persisted first-factor
challenge using TOTP or a backup code. Its request contains only the opaque
challenge bearer, code, method and optional device fingerprint. Account, tenant,
token version and policy fingerprint are read from the server's challenge and
revalidated before and after provider verification. A client cannot select these
bindings. The command uses the existing transaction and durable operation-event
pipeline; its host anonymous entry is explicitly registered and rate limited.

After atomic challenge consumption, the coordinator creates immutable
`SignInMfaProof` and calls the MFA-aware authenticated session issuer. The issuer
persists `SessionMfaEvidence` before signing the access token. The new native
migration `20261008102604_AddSessionMfaEvidence` adds only
`gameguild.authentication.session_mfa_evidence`, its session foreign key,
unique challenge index and integrity constraints. This table contains proof
metadata, not challenge bearers, MFA secrets or supplied codes.

The token carries `amr=mfa`, password/OTP method entries where applicable,
`mfa_verified=true`, the original first-factor `auth_time` and original
`mfa_time`. These AMR values follow [RFC 8176](https://www.rfc-editor.org/rfc/rfc8176.html).
Ordinary issuance still carries no MFA evidence; reserved custom claims cannot
create this evidence. Refresh restores the session proof and checks the current
account, enrollment, tenant, token version and policy fingerprint. It preserves
both original times. A required/enrolled session without proof must authenticate
again; an optional, unenrolled ordinary session retains its ordinary refresh.

The original public-completion regression failed with **404** before the route
was implemented. Its test source is byte-for-byte unchanged, including
completion, replay, preserved refresh timestamps and persisted row assertions.
It now passes against the actual native API and PostgreSQL.

| Native selection | Passed | Scope |
|---|---:|---|
| Authentication UnitTests | 2,518 | Entire project, including typed proof, MFA issuer and refresh-binding cases. |
| Authorization UnitTests | 1,667 | Entire project. |
| API UnitTests architecture/security | 137 | Focused host architecture, authentication, security and migration-deployment selection. |
| API IntegrationTests MFA policy/completion/migration | 19 | TOTP and legacy/new backup codes; replay across challenges; refresh; client binding forgery; expiry; changed account/version/policy/membership; concurrent HTTP issuance; rollback after proof persistence; migration upgrade/rollback/model consistency. |
| API UnitTests configured OpenAPI documentation | 8 | Configured documents, examples and operation documentation. |
| Native OpenAPI capture | 1 | Existing export contract test produces the actual host document; the completion request schema and response contract are independently checked. |

All listed selections passed with zero failed or skipped cases. The four native
groups in validation05 executed **4,339** passing cases; validation07 retains
those production sources and extends the integration selection from 17 to 19.
These are bounded selections, not the complete API UnitTests/IntegrationTests
projects or acceptance of all #145 requirements.

Receipts under the same artifact root:

- `issue-145-native-repository-completion-baseline04-execution01/result.json`
  retains the failing public-route reproduction.
- `issue-145-public-completion-original-regression-preservation-20261008.json`
  records the unchanged original regression SHA256.
- `issue-145-native-repository-completion-validation05-execution01/result.json`
  records the full authentication/authorization and focused host/HTTP selections.
- `issue-145-native-repository-completion-totp-openapi07-execution01/result.json`
  records the 19 native HTTP/migration cases and eight documentation cases.
- `issue-145-native-repository-completion-openapi06-execution01/result.json`
  and `openapi.json` record the actual native document and public contract.
- `issue-145-public-completion-client-generation-20261008/result.json`
  binds the regenerated TypeScript client to that document.
- `issue-145-public-completion-client-validation-20261008/result.json`
  records successful build and typecheck plus **1,135/1,135** client tests,
  zero failures or skipped tests, with generated sources and primary files preserved.

All executions preserved the 55 primary-checkout changes. The exact owned
PostgreSQL containers were removed and independently verified absent.

**#145 remains open.** Limited enrollment/recovery for required but unenrolled
accounts, enforcement and complete acceptance across other sign-in schemes,
high-risk whole-flow HTTP acceptance, role/tenant changes during ordinary
issuance, external-provider sandbox evidence and the remaining original criteria
still require work. The pending #287 signup decision and #704 Release/CI license
and historical-secret scanner gates remain separate. No issue was closed by this
checkpoint.

## Current review and Code workflow checkpoint — 2026-10-08

At `687e489f582bf402d0496008c31ecf8a114d2cd3`, the Emception CI run
[37825033649](https://github.com/gameguild-gg/gameguild/actions/runs/37825033649)
passed the native Toolchain receipt and release gates, the actual instructor and
learner assessment cycle, and browser C++ compilation. This is current CI evidence
for the Code workflow; it does not override the other required PR checks.

The separate retained local attempt05 opened the learner editor, reproduced the
failing starter tests, and passed the two public tests after editing the real Monaco
model. The API rejected submission with 409: the local deployment still contained
the legacy schema-1 compiler manifest and the private grading worker correctly
rejected it as `artifact-binding-failed`. The instructor had no completed attempt
to grade. This local execution is failed evidence, not acceptance: its 24 recorded
assertions do not establish successful submission or grading.

The browser runner now verifies the deployed manifest with the unchanged
`requireFrozenCodeToolchain` validator after CDN synchronization and before starting
PostgreSQL. The adapter remains bound to artifact 4.4.0, ABI
`emception-browser-v1`, and lock hash
`bb4e8ca4a8cc4640ec8f7f1d2f7dc14829992e44ff0309527a44b8a83d0b14ed`.
Legacy artifacts and mismatched receipt/fingerprint metadata fail before fixture
creation. Nine focused tests cover this preflight; all six MFA support tests also
pass. Original workflow instructions, assertions and deadlines are retained.
Failure diagnostics capture bounded DOM text and browser errors before disposal
without exporting cookies or storage state. The public RFC 6238 test key is clearly
identified; its bytes and expected TOTP vectors are unchanged.

The MFA review changes use explicit cancellation-token overloads, retain the
private `SignInMfaPreparation` constructor and its validated internal factories,
and make test fault switches exhaustive. Seven affected native projects built
without warnings or errors. Validation26 passed all 147 selected host cases,
2,535 authentication cases and 1,739 authorization cases: **4,421 passed, zero
failed or skipped**. Its Docker cleanup command timed out; the retained receipt
records that cleanup limitation and must be reconciled independently. The original
two request-cancellation regressions remain present and passed. Source hashes and
all 55 unrelated primary-checkout changes were preserved.

Receipts under the artifact root:

- `post-codacy-native-validation26-20261008/result.json` and each native TRX.
- `issue145-web-mfa-real-code-cycle-attempt05-20261008/result.json` and its retained
  local report, SHA256
  `6b1da205f73935432c51562b0af5e03c784ef91222d98d5bc0548def6083c191`.
- `code-artifact-preflight-public-rfc-and-mfa-tests-20261008.log`.
- `coding-cycle-failure-diagnostics-tests-20261008.log` (six passing focused cases).

The PR remains open. Release compilation still requires a Six Labors license for
ImageSharp 4.1.2; the current review and secret-scanner checks require re-evaluation.
No failed-check merge waiver applies to #704, and no issue is closed by this
checkpoint. Original #145/#288 criteria and the fixed 328-issue inventory remain
authoritative.

## Develop reconciliation and transactional MFA audit — 2026-10-08

The branch incorporates develop through
`5fcc2b3a10a3834c8645f114ac02c3d47b5d6940`, including signed policies, temporary
elevation, effective permission resolution, moderation scopes, security-event
capture and Next 16.3.8. The audit bridge conflict is resolved with a dedicated
`ISecurityEventLogger.RecordInCommandAsync` path for required sign-in MFA events.
It receives the owning scoped context, requires an existing relational command
transaction, classifies the event and evaluates alerts in that context. Database
or alert failures propagate without opening an independent scope or accepting a
spool result as transactional success. Other security events retain the existing
bounded retry and spool transport.

The original four required-audit failure assertions remain enforced through the
updated logger port. Additional unit cases reject nontransactional capture
outcomes, verify the exact supplied context/token, and preserve the original
persistence exception without retries or spool fallback. The actual PostgreSQL
regression injects a failure after verified MFA audit persistence and confirms
that audit, session proof, challenge consumption, backup-code consumption, session
and refresh token roll back together. Retrying the original challenge succeeds
and persists exactly one verified audit and session proof.

Validation27 built nine affected projects without warnings or errors and passed:

| Native selection | Passed | Scope |
|---|---:|---|
| Host | 147 | Original architecture/security/cancellation selection. |
| Authentication | 2,535 | Entire authentication unit project. |
| Authorization | 1,796 | Entire current authorization unit project. |
| Security-event/MFA audit | 28 | Bridge, owning-command capture and original security-event pipeline cases. |
| MFA HTTP/migrations | 35 | Original 34 cases plus the required-audit atomic rollback regression. |

Total: **4,541 passed; zero failed or skipped**. Validation28 then passed the
existing native OpenAPI export contract case. Its document SHA256 is
`3bd150cb4383b7517fdd012fdbb1f140a440602990b7cd3066584e8f65650a2a`;
the public completion/enrollment schemas retain their reviewed limited inputs.
Both executions preserved the source hashes and primary 55 changes and verified
their exact PostgreSQL containers absent after cleanup. The earlier validation26
cleanup timeout is retained and reconciled by a separate exact-ID absence receipt.

The local compiler deployment is restored from immutable GitHub artifact
11491711234, source-bound successful CI run 37625876160. The archive SHA256 is
`1e08b01fac7a38ea15839d16bea31b5ab162e766f72b1b43782d8e5e9ad691b8`.
All 29 bundle hashes and 9,719 materialized-file hashes were verified; the released
manifest matches the bundles/release receipt outputs and the glue receipt binding.
The unchanged frozen worker validator accepts its identity. The final bundled
manifest file map cannot directly reproduce the earlier pre-bundle fingerprint:
bundling adds bundle fields and replaces duplicate entries with symlinks. All
earlier collector failures and this provenance limitation are retained. No compiler
metadata or worker validator was changed. Legacy canonical/public trees are
hash-preserved in both the owned worktree and external artifact backups.

Receipts:

- `post-security-pipeline-native-validation27-20261008/result.json`.
- `post-security-pipeline-native-openapi28-20261008/result.json`.
- `post-codacy-native-validation26-cleanup-reconciliation-20261008.json`.
- `qualified-code-toolchain-d5-provenance04-20261008/result.json`.
- `qualified-code-toolchain-d5-owned-restoration-20261008/result.json`.

The local Code cycle is being repeated with these qualified sources/artifacts.
Release licensing and current scanner checks remain unresolved. #145/#288 remain
open pending their complete original criteria and required PR acceptance.

## Code cycle and dependency build blockers — 2026-10-08

The published `182979591241a19f55fc7e937b7c0462633b76f3` source tree matches
the generated CI merge tree `3d88f2cd89c1bfd5cfb192202e7d0659aba447e0` exactly.
Emception run 37842866036, job 113536473081, passed the real learner and
instructor Code journey: starter failures, corrected public tests, persisted
submission, the instructor's hidden test, rubric grading and persisted score.
Its report contains **36 green functional checks, two observed console
diagnostics and no red checks**. The interrupted local attempt06 is retained
as an interruption, not a successful local run. Collector count corrections
and the two console observations remain available in the evidence directory.

Handlebars 4.7.10 replaces 4.7.9 in the client and transitive lock entries.
Five new compiler/property regressions retain the original seven checks; the
vulnerable baseline failed four of the new checks. The complete repository
policy command, client build, typecheck and all 1,155 client tests passed.
The deploy policy selection retains three Windows environment skips. No
advisory ignore or scanner gate was added.

The sole ImageSharp consumer, optional asset average hashing, now uses
Magick.NET-Q8-AnyCPU 14.17.2. This removes the proprietary build-license
requirement and the inherited NU1902/NU1903 suppression. The complete upstream
`Notice.txt` is copied to output and publish under `third-party`. SHA-256
deduplication and public interfaces remain unchanged. The raster reader
selects an explicit recognized format, reads the first frame, converts CMYK
to sRGB, applies Catmull-Rom resizing and retains the 8x8 Rec.709 average hash.
It bounds encoded input to 64 MiB and native memory to 256 MiB, disables native
disk fallback and caps dimensions, profile size, threads and decoding time.
An optional hash failure still returns null and restores seekable streams.

Assets Release validation06 passed **1,189 cases with no failures or skips**,
including ten raster formats, resizing, independently calculated color hashes,
CMYK, first-frame selection, non-seekable streams, invalid input and cancellation.
The added color baseline exposed two CMYK failures, fixed by explicit sRGB
conversion. The earlier actual-module Alpine x64 probe passed all ten raster
formats and checked the complete notice; the full API Release publish passed
with warnings as errors and NuGet audit enabled. Those two executions precede
the final CMYK line and must be repeated for the reconciled source. No universal
byte-for-byte equivalence with historical decoder outputs or Alpine ARM64
support is claimed; exact content hashes and existing stored data are unchanged.

Evidence under the artifact root:

- `code-ci-36-green-2-observed-and-pr-correction02-20261008/result.json`.
- `handlebars-policy-and-client-validation-20261008/result.json`.
- `magick-color-contract-baseline05-20261008` and its failing TRX.
- `magick-assets-release-validation06-20261008` and its passing TRX.
- `magick-alpine-validation04-20261008/result.json`.
- `magick-api-release-validation04-20261008/result.json`.

Develop has advanced to `2124a610b01d8e850a10c1a4da6b2dc555f5d368`. The
branch will preserve its real-authentication fixture and this PR's bounded
per-test factory while regenerating the combined API client. Fresh integrated
qualification and current PR checks are still required before merge. This
checkpoint closes no issue.


## Verified pending MFA identity projection and coordinated PG pause — 2026-10-09

The integrated `5ffbc0f82e348b6cc17dc90e94add7d668b1df78` plus develop
`2124a610b01d8e850a10c1a4da6b2dc555f5d368` qualification29 exposed one real
failure: `HighRiskLoginKeepsChallengeMetadataAndWithholdsPhone` expected the
verified user ID and received `Guid.Empty`. The private pending transaction
marker bypassed ordinary response mapping and lost the established profile
projection. Its original HTTP regression test is unchanged.

The pending response now requires the verified subject and resolved tenant,
returns a detached first-factor profile using the ordinary pure projection,
and still withholds the stored phone, access/refresh credentials and session.
Its commit-on-failure marker survives mapping so the challenge and audit persist.
Invalid bindings, denial outcomes, cancellation and credential-issuance guards
remain covered. No public constructor was added to `SignInMfaPreparation`.

Both focused Release builds passed with warnings as errors and NuGet audit.
The completed TRX files contain **131 authentication and 73 real PostgreSQL
HTTP cases passed, zero failures and zero skips**, including the unchanged
original regression, enrollment/policy/completion/security and migration cases.
This is focused qualification, not a fresh full-suite result. The original
collector was suspended and then stopped after the tests completed, to honor
the coordinated local PostgreSQL intervention and prevent automatic teardown.
The integration process exit code was not collected; the unchanged original
runner result is not represented as a terminal successful/cleanup receipt.
A separate read-only receipt verifies both complete TRX files, the frozen source
hashes, actual captured OpenAPI and all 55 preserved primary-checkout changes.

The owned `gg-145-mfa-profile-validation31-20261009` container (PG17) is preserved
for the coordinator. It has no persistent volume: data uses a 512 MiB tmpfs.
No new local PostgreSQL instance or heavy PG proof will start until the shared
configuration/window is released. No real database reset or infrastructure
restart/removal is authorized by this checkpoint.

Codacy's specific S3453 finding on `SignInMfaPreparation.cs:6` was classified as
false positive with the validated factories and the official private-constructor
exception documented. Reanalysis is requested; current scanner acceptance is
still pending. The constructor and rule remain unchanged.

The fixed scope remains 328 IDs. The live snapshot at 2026-10-09 08:40 UTC is
145 closed and 183 open; original criteria/evidence are preserved. This
checkpoint closes no issue and does not attribute external closures to itself.
Latest develop `4a917fe176be4531ed246ee207e031c08d4025f1` still needs integration,
combined-client regeneration and fresh candidate checks before PR #704 merge.
Issues #145 and #288 remain open for their complete original requirements.

Evidence under the artifact root:

- `mfa-pending-profile-validation31-20261009` (unchanged runner/logs/TRX).
- `mfa-profile-validation31-recovered-trx-20261009.json` (separate verification).
- `mfa-profile-validation31-coordinated-pause-20261009.json`.
- `mfa-profile-validation31-owned-collector-stop-20261009.json`.
- `postgres-coordination-owned-resource31-20261009.json`.
- `mfa-profile-resume-matrix-reconciliation-20261009/result.json`.
- `pr704-codacy-false-positive-reanalysis-requested-20261009.png`.


## Integrated develop offline qualification and scanner corrections - 2026-10-09

The pending identity fix is signed in `b6c74ef3a65b8dd965da59ec83c3a7370bb16f34`.
The next combined candidate incorporates develop
`4a917fe176be4531ed246ee207e031c08d4025f1`. All five ordinary Release builds
passed with warnings as errors and NuGet audit enabled. The combined-source
selection passed 2,652 authentication, 2,014 authorization, 133 host
architecture/security/OpenAPI and 1,189 Assets cases, with zero failures or
skips. These are 5,988 cases across those selections, not full API HTTP/Code
acceptance. No local PostgreSQL or container was created or started.

Sixteen Codacy style findings were corrected with braces and inferred byte-array
typing, without changing decoder limits, format ordering, expected hashes or
test assertions. The complete 1,189-case Assets suite passed again after the
changes. The existing presentation-only exporter generated the actual combined
compiled OpenAPI: 1,345 paths and 1,709 schemas, SHA256
`d1afa753a5b99887a80d8ebf6a0feff8657331ae6fa4151b4d5772dc13ea86d2`.
This offline contract capture starts no hosted service or database. Fresh hosted
API HTTP and Code checks remain required on the published candidate.

Codacy's S3453 finding on the private preparation constructor disappeared after
its specific false-positive reanalysis. S1172 finding
`46570d42460ab8ef4716b6faed39eaff` on the test `Stream.Seek` override was also
classified specifically as false positive: both parameters are required by the
base signature, and SonarSource's documented rule exceptions exclude overrides.
The UI confirms pending reanalysis. Neither rule nor file was disabled; scanner
acceptance of the new combined candidate remains pending.

The coordinator stopped the owned PG17 fixture after zero clients and no
mandatory consumer had been confirmed. Its pre-stop receipt records
`AutoRemove=true`, no persistent mounts and 512 MiB tmpfs. Stopping that fixture
therefore removes the container and its ephemeral data. Logs, complete TRX and
captured OpenAPI remain outside it. The interrupted collector's missing process
exit code is still not fabricated. Persistent databases remain untouched, and
no heavy local PostgreSQL or Code proof resumes until the coordinated window
is released.

Evidence under the artifact root:

- `final-develop-offline-validation32-20261009/result.json`.
- `codacy-assets-style-proposal33-20261009/proposal.diff`.
- `codacy-assets-style-validation34-20261009/result.json`.
- `postgres-coordination-final-owned-state31-20261009.json` (pre-stop state).
- `pr704-codacy-stream-override-pending-reanalysis-20261009.png`.

The fixed 328-ID matrix and all 55 primary-checkout changes remain preserved.
This checkpoint closes no issue; #145 and #288 retain their original criteria.


### Combined client generation and separate UTF-8 receipt

The client was regenerated from the combined compiled OpenAPI. Generation,
ordinary build including declarations, typecheck and the complete client test
command all returned exit code zero. All 1,155 assertions passed, with no failed
or pending tests. The original collector then failed while decoding the JSON
report with Windows' default code page; its failed receipt remains unchanged.
A separate UTF-8 reconciliation verifies all four command exits, every assertion,
API and non-generated client source hashes, all generated files and the 55
primary-checkout changes. No second test run or rewritten original result is
represented as acceptance.

The metadata hash was verified through the actual generator's `normalizeSpec`
and SHA256 pipeline, not by assuming the raw OpenAPI JSON byte hash matches it.
Normalized contract hash:
`c4b5c07d5e43bbc73013956f3f34b9b624fcfdff272acac9e22aa5d97dcbd81b`.
The generated metadata resolves the only remaining integration conflict.

The 15 existing MFA/Code-support tests and 12 advisory regressions also passed,
without failures or skips. These are support checks, not the real student and
instructor Code cycle. Fresh hosted API HTTP/Code and security gates remain
required before merge; #145 and #288 are not automatically closed.

Additional evidence:

- `final-develop-client-regeneration35-20261009` (unchanged original receipt/logs).
- `final-develop-client35-utf8-reconciliation-20261009.json`.
- `code-support-advisory-validation36-20261009/result.json`.
- `postgres-coordinated-stop-autoremove-reconciliation31-20261009.json`.
