# Refresh-token lifecycle and unavailable accounts — #262 / #263

## Final acceptance after merged PR #699 — 2026-10-07 UTC

[PR #699](https://github.com/gameguild-gg/gameguild/pull/699) merged into `develop`
at `d5f417328e9ff9d3fa0517796a623e310aeb1ef0`; accepted implementation head `bf2775e5dcfeab72e78afa7be69ec16e36034276`.
[Issue #263 acceptance](https://github.com/gameguild-gg/gameguild/issues/263#issuecomment-6038587329) retains all 19 original criteria;
the issue is now **CLOSED/COMPLETED**. No failing-check exception was used.

The already-closed duplicate #264 now has a
[published and verified canonical acceptance follow-up](https://github.com/gameguild-gg/gameguild/issues/264#issuecomment-6046080570).
Its duplicate state and original criteria are preserved. This reconciliation
adds no issue closure to the count.

Matching-head native validation passed 6,037 main API cases,
15 repeated OpenAPI cases and 2,998 Web cases. The actual Code
cycle passed 36 green assertions, zero red and
2 console observations: learner submission, trusted private
execution, frozen instructor rubric, score 100 and persisted feedback. C++ passed
21 cases with 5 skipped and zero
failed/flaky/interrupted; skipped cases are not executed passes. Complete Emception
release, consumers, Pages and deployable validation plus all selected security
checks passed. The original 55 dirty primary files remain unchanged.

Earlier partial, pending, failed and superseded checkpoints below remain historical.
The fixed 328-ID inventory currently has 73 closed and
255 open; closure counts include administrative dispositions.
#287/#288 remain independently open with executed failing baselines. Production
email/provider receipt and production deployment are not claimed by this acceptance.

## All-user revocation accepted — #694

[#694](https://github.com/gameguild-gg/gameguild/pull/694) merged into develop at
`fddbc684c55e5ea0607ef4a5097601f82f041152`, accepted head
`cd6c3e9b68fc4f660d4591e7730458f936141b4b`, at2026-10-05T14:02:38Z.
All applicable checks, Codacy and four CodeQL languages pass. Current-source
acceptance totals **7,724 distinct .NET/SDK cases,55 new definitions**: CI276full
integration/1,050full APIunit/2,235Authentication plus fresh local1,667Authorization/
1,377SharedKernel/1,119SDK. Repeated OpenAPI15, focused PostgreSQL10, local API129
and repeated suites are subsets excluded; historical/failed receipts are retained
separately. Entire OpenAPI remains equal (1,297paths/1,656schemas), builds have zero
warnings/errors and no model changes are pending. This accepted stage supersedes
earlier publication-stage pending statements below.
[Public bounded acceptance](https://github.com/gameguild-gg/gameguild/issues/263#issuecomment-5996108933).

Both checkouts are synchronized and all55 unrelated primary files remain preserved.
Completed feature branch removed locally/remotely; same isolated checkout reused
for further work, two worktrees/zero stashes. No additional issue was closed:
the refreshed328-ID snapshot remains70closed/258open with original fields retained.

The [next actual login/two-rotation baseline](https://github.com/gameguild-gg/gameguild/issues/263#issuecomment-5995693416)
passes rotation/revocation, forward hash links and stable-session controls but fails
required ParentTokenId/SessionId entity/storage metadata. Its1failed diagnostic is
excluded from #694 acceptance. All19 original #263 criteria remain authoritative.

[Separate Web3 diagnostics](https://github.com/gameguild-gg/gameguild/issues/291#issuecomment-5996026613)
confirm missing entry Size with the bounded host cache: HTTP challenge500 and actual
service exception. A downstream diagnostic explicitly removes that cache limit;
real facade/SIWE/EIP191/JWT/PostgreSQL then rejects wrong-key/replay and accepts the
valid signature, but persists a plaintext duplicate and no account/session. That
stage is not HTTP/command-transaction/production-cache/provider/UI acceptance.
All three failed stages are preserved/excluded. #291 and its5-criterion approved
native child#292 remain OPEN. Global metadata/hashing acceptance cannot be inferred
from LocalAuth receipts alone; complete audit/alerts/retention/cleanup/metrics and
all issuance-path acceptance remain outstanding.

## Ownership/session acceptance merged and all-session revocation continuation

The #694 review reproduced a further boundary defect at head3ddfa552e:
logout200, actual credential re-login200 and stored/signed version2 still
yield401 for a signed iat at the exact stored logout second. Only iat is
re-signed to that exact second to make the boundary deterministic; identity,
credential flow, stored version/session and JWT verification remain real.
The precise source/observations/TRX are retained, excluding route/configuration
preparation failures. [Public counterexample](https://github.com/gameguild-gg/gameguild/issues/263#issuecomment-5994788645).

The correction records the operation's advanced minimum version with its
cutoff, and passes the signed version to the configured revocation store.
Current-version tokens avoid the fractional-second mismatch. Earlier versions
still fail the persisted version check; legacy tokens and ordinary time-only
cutoffs retain timestamp rejection. Existing cache payloads without the new
optional field remain compatible. Required typed capability is registered as
an alias of the same configured store; custom providers must implement it.
Fresh reviewed-source acceptance passes **6,552 distinct .NET/SDK cases**, with
**55 new definitions**: Authentication2,235, Authorization1,667, SharedKernel1,377,
actual PostgreSQL10, API architecture/security/registration129, full-host OpenAPI15
and SDK1,119. The actual credential re-login's bearer and the bearer pinned to the
exact logout second receive200; the previous bearer receives401. Both configured
store implementations cover version-bound and ordinary cutoffs, legacy payloads,
isolation, invalid minimum versions and cancelled writes. The HTTP cache fixture
uses the actual distributed service with distributed-memory storage; this does
not establish Redis or cross-node acceptance. Full solution builds have zero
warnings/errors; entire OpenAPI remains equal (1,297paths/1,656schemas), SDK
typecheck/consistency pass and EF reports no pending model changes. Matching-head
CI is still required before merge.

The first reviewed authentication run retained one failure out of2,235: an old
cleanup test cast a private cache dictionary to its previous Guid/DateTime shape.
That source/TRX is preserved and excluded. The test now uses a controlled clock
and public revoke/check/cleanup operations, keeping and strengthening expiry
assertions. Production was unchanged by this test repair; the fresh run passes.

Further Codacy review required explicit arguments on the new typed service
signatures and a generated synthetic password in the credential regression.
Those findings are addressed without ignores; original time-only overloads and
all revocation/credential assertions remain. Earlier source receipts are retained
separately; publication requires fresh receipts for this final source.

[#693](https://github.com/gameguild-gg/gameguild/pull/693) merged at
`280ea75a68c739b807c08a6ad724d6cf29f059bf`, accepted head
`c8d5bd95c064f85de5846bfe4892e446fe2cf916`. Actual current-head CI supplies
266 full integration and 2,191 authentication cases, plus a repeated 15-case
OpenAPI subset. Fresh local authorization, SharedKernel, API architecture/security
and SDK receipts establish 6,735 distinct combined cases; CI also passes 2,959
Web cases. All applicable gates/Codacy/four CodeQL languages pass. The sole
specification addition is403 on the existing revoke endpoint. This supersedes
the publication-stage pending statements below for this bounded increment.
[Public acceptance and remaining scope](https://github.com/gameguild-gg/gameguild/issues/263#issuecomment-5993781040).

The next actual baseline exercised `POST /v1/auth/sessions:terminate-all`
against migrated PostgreSQL and production JWT/HTTP: it returned200 and
terminated both owned sessions, but both refresh tokens remained active and the
stored token version stayed1. Another user's token/session remained unchanged.
The failing source, observations and TRX are retained, excluded from #693 counts.
[Public baseline](https://github.com/gameguild-gg/gameguild/issues/263#issuecomment-5993552561).

The existing route now binds a dedicated `RevokeAllUserTokensCommand` with no
user selector. Its handler requires the authenticated User actor, revokes that
user's active refresh tokens, terminates their sessions, advances the persisted
token version once and writes the existing distributed user revocation cutoff.
This includes legacy access tokens without version/session claims. The host
supplies the IP; the existing response and the separate terminate-others path
are preserved. The database writes use the existing command transaction and
optimistic concurrency; failures and cancellation propagate. The cache is not
in that database transaction: a successfully written cutoff is conservative
denial if a later commit fails, and successful logout must never be reported in
that case. The initial publication stage passed **6,514 distinct .NET/SDK cases**, with
**29 new definitions**: Authentication2,210, Authorization1,667, SharedKernel1,377,
actual PostgreSQL9 (eight HTTP cases plus real command-transaction cancellation),
API architecture/security/registration129, CommonOpenAPI HTTP3 and SDK1,119.
Complete captured OpenAPI remains identical (1,297 paths / 1,656 schemas), SDK
typecheck/consistency and warning/error-clean full solution builds pass, with no
pending model changes. Those initial-head receipts are historical; the reviewed
acceptance above supersedes them for this increment.

Initial test-only corrections are preserved and excluded from accepted totals:
the generic operation event uses the first mutated aggregate, its jsonb payload
requires typed deserialization after actor/event selection, and the new guarded
coordinator is tested separately from existing facade consumers. Their original
IAuthService requirement remains unchanged. These test repairs did not modify
the production implementation. The original real failing logout baseline remains.

**#263 remains OPEN with all 19 original criteria.** Parent/session lineage,
complete token-operation audit/alerts, scheduled retention/cleanup and metrics
are outstanding. Session lifecycle and the generic durable command event do
not establish the complete token-operation audit requirement.

## Reviewed403 contract and client regeneration

The review correctly identified that the new non-owner403 was absent from the
declared revocation contract. A new actual full-application Swagger HTTP test
failed for that omission and now passes after declaring403 on the existing
action. Captured OpenAPI differs solely by that response; all other specification
content is deep-equal (1,297paths/1,656schemas), with no pending EF model changes.
The client was regenerated; only generation metadata changes because its existing
generic error types already support403. Client1,119 tests and typecheck pass.

Fresh reviewed source passes **5,360 focused/core .NET plus1,119 client cases**:
**6,479 distinct local cases**,43 new definitions. Full Authentication2,191,
Authorization1,667 and SharedKernel1,377, actual PostgreSQL9, API architecture/
security/eventing115 and the new HTTP contract fact1 pass. A local complete
OpenAPI attempt ended without terminal TRX and is preserved/excluded; matching-head
CI must supply full integration266 and complete OpenAPI15 before merge.

The initial commit26c1f86bc passed6,549 whole local .NET cases and all applicable
CI checks (2,456 main plus14 repeated OpenAPI). Those earlier receipts remain
historical evidence and are excluded from reviewed-head totals. Pending merge and
the19 original #263 criteria are not completed by this bounded increment.

## Explicit revocation ownership continuation — #263

Production `f3346a891e4887bcf362d0544d23386aeda30ed1` was exercised against actual
migrated PostgreSQL and production signed JWT/HTTP revocation. **Four cases yielded
two failures and two passing controls:** different users in the same or another
tenant received204 instead of403 when submitting the owner's refresh token.
Those two baseline storage assertions follow HTTP status and were not reached.
Own-token204 revocation terminates its linked session; anonymous401 leaves it
unchanged. The initial provisional owner-session-active assertion was corrected
to the existing `LocalAuthService` logout contract; initial source/TRX/logs remain.
[Public baseline](https://github.com/gameguild-gg/gameguild/issues/263#issuecomment-5992442921).

The command now obtains identity from the trusted actor context, requires an
authenticated user subject and compares stored token ownership before delegation.
Command/body userId and administrator roles cannot authorize another user's token.
The host supplies audit IP, and successful own-token logout behavior is preserved.
After that guard, three of the four HTTP cases passed; the owner case exposed a
second gap: token revocation and inactive linked session were persisted, but the
same session's bearer still received200. Its exact source/log/TRX and intermediate
production hashes are retained separately. The assertion remains in the suite.
Session-bound bearer middleware now requires one valid nonempty session claim,
an existing active/unexpired/nonterminated session and matching subject ownership.
Denial clears identity; explicit public endpoints continue anonymously. Legacy
and service tokens without a session claim keep their existing compatibility.
Unit tests cover invalid actor kinds/subjects, spoofed userId, admin roles, missing
tokens, cancellation, stored session failures, malformed/duplicate session claims
and public boundaries. Actual PostgreSQL HTTP tests retain both original denials
and controls plus invalid signed-session cases. **5,359 distinct focused/core
cases pass**, zero failures/skips: Authentication2,191, Authorization1,667,
SharedKernel1,377, PostgreSQL HTTP9 and API architecture/security/eventing115.
Forty-two definitions are new; earlier failed attempts remain evidence. Full API
suites and matching-head gates/merge are pending. Full OpenAPI is deep-equal
(1,297paths/1,656schemas); EF reports no pending model changes. #263 remains OPEN
with all19 criteria unchanged.

## Official bounded #262 acceptance after merge

PR #692 merged into develop at `f3346a891e4887bcf362d0544d23386aeda30ed1` after all
applicable checks for `065c6cf39298fd6f66f678d8735bc4bd058f873b` passed.
[#262 officially CLOSED/COMPLETED](https://github.com/gameguild-gg/gameguild/issues/262#issuecomment-5992219521)
after each preserved owner criterion was mapped to the actual replay/transaction,
signed bearer, stored user-version and forced concurrent endpoint acceptance.
#263 remains OPEN with all 19 original criteria and its remaining bounds below.

| Project | Distinct cases | Receipt provenance |
| --- | ---: | --- |
| Full API integration | 256 | CI on final head, actual PostgreSQL |
| Authentication | 2,158 | CI on final head and local current core; counted once |
| Authorization | 1,667 | Fresh local current core |
| SharedKernel | 1,377 | Fresh local current core |
| Full API unit | 1,049 | Initial head 8c199709d; production C#, APIunit project and compilation inputs byte-identical to final head |

**6,507 distinct combined cases**, including 14 new cases. Actual CI selected
2,414 main cases plus 14 repeated OpenAPI; it did not select full APIunit or
SharedKernel for this changed module surface. Local focused seven, repeated core
and fresh API architecture/security/eventing 115 are subsets excluded from totals.
The final local whole-API repetitions crashed; the sequential integration retry
failed fixture initialization while Docker was unavailable. These receipts are
retained, not counted as complete. Docker local was restored; CI supplied final
whole integration. Current-head required gates, OpenAPI/client consistency,
Codacy/four CodeQL and warning/error-clean builds passed. Full contract remains
identical (1,297 paths / 1,656 schemas), no model changes.

Publication-stage pending statements below remain history and are superseded
only for this bounded #262 acceptance. No complete #263 or unexercised provider/
distributed-cache acceptance is inferred.

## Preserved scope

#262 retains its original empty body and owner clarification: replay invalidates
the family and global sessions, advances user version, rejects prior access tokens,
and only one concurrent rotation may claim a token. #263 retains all 19 criteria;
duplicate #264 closure does not prove implementation. Merged #690 and #691 are
bounded increments, recorded in the existing refresh and bearer reconciliation
documents. #262 remains open until integrated acceptance and merge are verified.

## Executed baseline

Production `c4ffdc4797c058b0544ad071e7b8764d896f7e86` was exercised with actual
migrated PostgreSQL, the production signing/validation/pipeline and HTTP endpoints.
Six cases produced **two failures / four passes / zero skips**: a soft-deleted
account's previously issued bearer and refresh each returned 200 instead of 401.
The failure assertions stopped at HTTP status; later unchanged-storage assertions
were not executed and are not claimed as baseline evidence.

Passing controls perform two successive refresh rotations for TTLs 1, 7 and 30
days, verifying original auth_time, session reuse, random token bytes, hash-only
storage, predecessor invalidation/replacement, configured expiry, later bearer
consumption and replay containment. A tenant without membership returns 403 and
leaves token/session unchanged. An initial expectation of 401 for that tenant was
corrected to the existing 403 contract; all state/denial assertions and original
attempt receipts were retained.

[Public baseline](https://github.com/gameguild-gg/gameguild/issues/263#issuecomment-5990979868).
Sources, initial/corrected TRX, logs and hashes are retained under
`artifacts/test-results/issue-263-lifecycle-20261005/`.

## Correction and acceptance boundaries

An otherwise active/unexpired refresh now denies if the existing live-user lookup returns null, before tenant
provisioning, issuance or session mutation. Fallback version/email identity is
removed. Versioned user bearer validation also denies null live-user versions.
Rejected protected requests receive generic 401 and no identity; explicit public
endpoints continue anonymously. Unit tests cover both boundaries, no issuance or
mutations, and retained versionless legacy/service compatibility. Revoked/replaced
replay still requires committed containment before denial, even if the profile is
missing/deleted: its extant token/session rows are revoked/terminated, without
issuance or an invented version/profile update. Two explicit service cases retain
this distinction; moving the availability guard ahead of replay would bypass the
owner's global-containment requirement. The unchanged-state guarantee applies to
otherwise active tokens denied due to unavailable account, not replay containment.

The original six relational scenarios pass after correction. A further endpoint
test synchronizes two actual SELECT results so both requests observe the same
active token before rotation; the interceptor only delays reads, without replacing
the database, repository, command, JWT or authentication. Acceptance requires one
successful claim/response, one denial, committed global containment, increased
version and rejection of the winner's now-compromised bearer, with an unrelated
user unaffected. All seven scenarios pass against actual migrated PostgreSQL.
The first synchronization attempt also counted session SELECTs and failed its
arrival-count assertion; the interceptor was narrowed to refresh-token SELECTs,
without changing any response/containment assertion, and the attempt was retained.

At first publication, full Authentication 2,156, Authorization 1,667 and SharedKernel 1,377 pass with
zero failures/skips: 5,200 core cases plus seven focused relational cases. The
55 service/boundary cases are a subset of Authentication and are not added again.
Twelve definitions are new (seven relational, five unit). Full API suites are in
progress at publication and will supersede, not add to, focused subset counts.
Full Release solution and final integration-only build are warning/error clean.
Entire exported OpenAPI equals accepted #691 (1,297 paths / 1,656 schemas); EF
reports no pending model changes. Matching-head CI and merge remain required.

## Review correction and policy clarification

Codacy reported one unused required override parameter in the read synchronizer.
The seam now uses `eventData.CommandSource` to restrict synchronization to LINQ
SELECTs, retaining table/hash filters and all functional assertions; no rule is
suppressed. Seven fresh relational scenarios pass after this correction.
Two additional unit cases explicitly verify committed replay containment for
revoked/replaced tokens without a live profile, while forbidding issuance or a
synthetic profile/version update. The availability check deliberately remains
after replay detection to preserve the owner's global-containment acceptance.

Fresh Authentication 2,158, Authorization 1,667, SharedKernel 1,377 and relational
seven pass: **5,209 focused/core cases**, with **14 new definitions** across this
increment. Full suites and the corrected commit's CI/merge are still required.
First-head complete execution (6,505 distinct cases), quality finding and
intermediate failures remain separate history; focused subsets are never added
again to whole-suite totals.

This does not introduce persisted parent/family IDs, selective-family semantics,
session-specific bearer enforcement, cancellation/issuance-failure acceptance,
lifecycle audit/alerts, scheduled cleanup, metrics or optional absolute/sliding
policy. Those remaining #263 criteria stay open. Cross-instance external-cache
provider acceptance is separate from the PostgreSQL token-version boundary.
Whole suites, contracts and matching-head CI/merge are required before closing #262.
