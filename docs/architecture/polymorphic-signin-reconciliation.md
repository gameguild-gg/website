# Polymorphic password sign-in reconciliation — #258

## Official acceptance after merge

**#258 is CLOSED/COMPLETED** after [PR #689](https://github.com/gameguild-gg/gameguild/pull/689)
merged into `develop` at `47131474c221bff080ec1efd17b53a17aa62b4b2`.
[Individual evidence](https://github.com/gameguild-gg/gameguild/issues/258#issuecomment-5989540682)
accepts all five approved criteria and the consolidated #289 scope. Accepted head
`8b4cc4eac530d3476b103a8e7f28616144d3984b` passes all applicable gates, Codacy and
four CodeQL analyses. CI passes 5,501 main API cases, 14 repeated OpenAPI HTTP
executions and 2,959 web cases, with client/web/API builds and lint/typecheck.
The complete local functional set is 8,287 distinct cases (77 new definitions):
full API unit 1,049 now passes after the nontracking query followup. Final-head
solution/core/43 PostgreSQL/100 API architecture-security repeats also pass and
are excluded from that total. Internal flags remain absent from OpenAPI and no
model changes are pending. Boundaries on phone possession, providers and full MFA
completion remain as stated below.

The sections below retain the baseline and publication-stage history; their
pending statements describe those earlier stages, superseded by this acceptance.

## Retained requirement and baseline

The five user-approved criteria under [#258](https://github.com/gameguild-gg/gameguild/issues/258)
include email, username and international phone plus password; unique account
resolution and generic failures; reuse of local risk/tenant/session controls;
handler and consuming endpoint verification; and a reviewable integrated change.
#289 remains a duplicate consolidation, with its username scope retained here.
Original acceptance fields and checklist status are preserved.

The executed six-case source baseline at `afb2090700a020ed7d276e8322ef39ab8d747131`
had four failures and two positive controls: username/phone were forwarded as
email, device fingerprint was dropped and the HTTP entry point was missing.
Those mock/metadata observations did not establish a successful real login.

## Implementation and interfaces

- Adds `POST /v1/auth/polymorphic`, explicitly anonymous, reviewed in the host
  allowlist and subject to the controller's existing authentication rate limit.
  Its command has the durable use-case event contract and both supported DI
  registration paths. Event payloads do not contain the credentials or issued tokens.
- `PolymorphicSignInRequest` accepts `credential`, optional `credentialType`,
  `password`, optional `tenantId` and `deviceFingerprint`. The response remains
  the existing `SignInResponse`, including incomplete high-risk challenge results.
  Semantic invalidity, missing/ambiguous accounts and incorrect passwords return
  the local flow's generic 401 Problem Details. Malformed JSON/type representation
  can still fail model binding with 400, as for the other API entry points.
- Trims the identifier. Explicit type is honored. Automatic detection uses `@`
  for email, leading `+` for phone and otherwise username, including numeric handles.
  Email is bounded to the existing 255-character field and validated by the
  framework. Username accepts the existing ASCII handle alphabet up to 256
  characters, with case-insensitive matching and no accent/alias conversion.
- Phone syntax is a leading `+`, nonzero first ASCII digit and 2–15 digits.
  The [ITU E.164 numbering plan](https://www.itu.int/ITU-T/recommendations/rec.aspx?lang=en&rec=16273)
  provides the 15-digit bound. Syntax and exact stored matching do not validate
  national numbering plans, number allocation or possession. Legacy formatted
  phone values are not rewritten or guessed; email remains available.
- The Users module exposes a bounded two-candidate repository query and a
  domain-free identifier enum. Email/username comparison ignores case; phone
  comparison is exact. Deleted rows are excluded. Zero or multiple matches
  are denied, without selecting a candidate by requested tenant or choosing
  the first account. The existing case-sensitive indexes remain unchanged.
- The handler passes the canonical account email and server-resolved ID into
  the local flow. Internal resolution fields cannot bind from JSON or appear
  in OpenAPI. Failed resolution still follows existing denial, timing, attempt
  recording and risk analysis; it cannot fall back to another email account.
  The selected account is reloaded before its current password is verified.
- Existing tenant membership checks remain authoritative. IP/user agent use
  the existing HTTP context. Device fingerprint reaches risk and persisted
  session data; a nonempty existing header takes precedence over the body hint.
  A fingerprint does not independently establish device trust. The existing
  email endpoint also now forwards its declared body fingerprint.
- Password format/history, JWT/session contracts, tenant model and database
  schema are reused. This increment does not certify complete MFA challenge
  consumption, phone ownership, provider delivery or production readiness.

## Acceptance definitions and current execution

| Retained criterion | New cases |
|---|---|
| All three credential kinds and common response | Six real HTTP explicit/automatic and case-variant successes; three real handler successes; three generated SDK anonymous transport cases. |
| Unique accounts and generic invalid/missing/ambiguous failures | Nine real HTTP wrong-password/missing/deleted cases; ten semantic-invalid cases; three legacy ambiguity cases which become unique after deletion; server-ID JSON spoof rejection. |
| Reuse local risk, tenant and session flow | Actual JWT subject/tenant and hashed refresh session storage; body fingerprint/user agent/risk context; three high-risk challenge cases with no session; three unjoined-tenant denials. |
| Handler plus endpoint verification | Thirty focused mock/metadata contract cases and 42 actual migrated PostgreSQL/API cases, including three real handler incorrect-password denials, numeric username and all listed HTTP combinations. |
| Reviewable implementation/evidence | This map, retained baseline/failures and exact current receipt set; publication and matching-head merge remain required. |

The actual full Release solution builds with **zero warnings/errors**.
Authentication **2,144**, Authorization **1,667**, SharedKernel **1,371** and
Users **714** pass freshly. Client **1,119** cases pass, including four new
transport cases; its type check passes. SDK transport uses synthetic local
responses and is not independent real authentication proof.

All 42 new migrated PostgreSQL/API cases pass in the first complete integration
execution. That suite has **221 PASS / 1 FAIL / 222 total / zero skips**: an
unchanged durable-transport fixture timed out while starting its PostgreSQL
container. That failed suite is retained and is not complete acceptance.
An earlier compile failed the missing use-case event contract; the contract
was added and the current actual solution build is clean. No analyzer/gate or
test was removed. Full API unit and a complete integration rerun remain pending.

Offline OpenAPI has **1,297 paths / 1,656 schemas**; generated types, endpoint
descriptor and Auth module include the new flow. Existing paths/schemas must
remain unchanged and internal resolution fields absent. EF reports no pending
model changes. Final complete receipts, applicable PR checks, merge and official
acceptance are pending. **#258 remains OPEN**. Logs and TRX receipts are retained
under `artifacts/test-results/issue-258-polymorphic-20261005/`.


## Latest execution after the nontracking followup

Candidate lookup now explicitly uses `AsNoTracking`, so the local flow's ID lookup
loads the current password instead of returning an earlier tracked candidate.
One additional real PostgreSQL interleaving case changes the password after
resolution: the old input is denied, the new input succeeds and only one session
is issued. Only the interleaving is controlled; persistence and authentication are real.

The latest full Release solution has zero warnings/errors. Fresh Authentication
2,144, Authorization 1,667, SharedKernel 1,371, Users 714 and **entire API integration
223/223** pass with zero skips, including **all 43 new PostgreSQL cases**. Client
1,119 cases/build/type check pass, including four new transport cases. This is
**7,238 current local cases**, including 77 new definitions across the three suites.
The earlier full API unit 1,049 passes are retained but not added to this current
total; that whole suite is being repeated after the final query change. Earlier
core runs and the failed 222-case integration execution are also excluded.

All preexisting OpenAPI paths/schemas remain byte-equivalent as parsed JSON.
Exactly one path and two schemas are added; internal resolution fields are absent.
No model changes are pending. Matching-head checks, merge and official acceptance
remain required; **#258 is OPEN**. `local-proof-publication.json` records the current
receipt hashes and exact test-key counts (definition ID plus displayed case name).
