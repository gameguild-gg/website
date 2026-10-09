# Refresh-token reconciliation — #263

## Accepted partial increment after merge

PR #690 merged into develop at cd152b6587f7b27ac8850b4121652d3db7b6d94d after all
applicable gates passed for accepted head379a7b28b853f79d3a008475d5b9d8bcff08356f.
Local 6,476 distinct .NET cases include full integration238 and APIunit1049;
focused15 and architecture/security108 are subsets excluded from totals. Actual
CI passes 4,809 main cases plus14 repeated OpenAPI, with clean builds,
OpenAPI/client consistency, Codacy and four CodeQL analyses. Contracts/model
remain unchanged. [Individual acceptance](https://github.com/gameguild-gg/gameguild/issues/263#issuecomment-5990377649).
**#263 is OPEN**, with its original 19 criteria and remaining table below intact.
#262 was reopened for independently executed prior-bearer invalidation failures;
its active-host correction is being accepted separately.

Publication-stage pending statements below remain historical, superseded for
this partial increment by the acceptance above.

## Requirement provenance

[#263](https://github.com/gameguild-gg/gameguild/issues/263) retains all 19 original
criteria and the approved consolidation of duplicate #264. This record separates
the executed repository correction from the remaining integrated token lifecycle.
It does not change the issue's original checkboxes or certify its full completion.

## Executed baseline and repository correction

Production source `8b4cc4eac530d3476b103a8e7f28616144d3984b` was exercised against
actual PostgreSQL with the application's migrations applied. Seven new cases
yielded **two failures and five passing controls**, with zero skips. Both
`GetActiveByUserIdAsync` and `RevokeAllForUserAsync` referenced computed `IsActive`,
which EF explicitly ignores. Both threw SQL translation exceptions before any
listing or user-wide revocation could complete. This confirms the earlier static
review's persistence concern by execution.

Both predicates now use mapped `UserId`, `IsRevoked` and `ExpiresAt`, with a UTC
timestamp captured once per operation. Evaluation remains in PostgreSQL. Active
rows still mean unrevoked and strictly unexpired; listing remains newest first.
User-wide revocation retains its previous active-row behavior, metadata update
and single `SaveChangesAsync`. Other users, expired rows and already revoked rows
are left intact. This correction introduces no API, schema or token-format change.

The five positive controls use the existing actual conditional rotation update:
expired/revoked/already replaced/wrong-hash tokens cannot be claimed, and two
independent contexts produce exactly one committed winner with the winner's hash
stored as the replacement. This is a repository claim test; it does not establish
atomicity of the complete service's token/session issuance and rollback.

The test fixture generates synthetic hash values without credentials or providers.
Seed timestamps are aligned to PostgreSQL's microsecond precision so historical
revocation timestamps can be compared exactly after storage. No authentication,
repository or database operation in these seven cases is mocked.

Executed definitions, initial failures, logs, TRX files and hashes are retained
under `artifacts/test-results/issue-263-refresh-20261005/`.
[Public baseline evidence](https://github.com/gameguild-gg/gameguild/issues/263#issuecomment-5989397406).
The baseline and intervening failures are retained separately from the passing
implementation receipts below.

## Replay containment and command transactions

After the predicate correction, actual HTTP execution exposed a second defect:
the endpoint returned 401 for revoked/replaced tokens, but the command transaction
rolled back its earlier token revocation, session termination and token-version
updates when the service threw the denial. Two replay cases failed their persisted
state assertions; expired and unknown tokens were valid negative controls. An
earlier HTTP fixture attempt also failed three cases because it reused the empty
default username; seed users now have unique usernames. All attempts are retained.

An internal `RefreshTokenContainmentDenial` is now returned only after all required
containment writes complete. It has `Success=false`, a generic message and no
identity/tokens. Its explicit server-only `ICommitOnFailureOutcome` contract tells
the transaction owner to retain those completed mutations. The handler preserves
this internal outcome without profile mapping; the controller still emits the
existing generic 401 Problem Details after the command transaction finishes.
It is not a bindable request flag or a new public API model.

`CommandOutcome.IsFailure` remains unchanged: denial is still business failure,
including for quota accounting. `ShouldRollback` adds an explicit typed commit
decision only for trusted outcomes. Ordinary failed responses, null/false results,
exceptions and cancellation retain their rollback behavior. A similarly named
public flag or a nested marked result cannot grant its wrapper commit authority.
Nested commands still depend on their outer transaction owner; this acceptance
exercises the actual top-level refresh endpoint and its transaction.

All **15** new PostgreSQL cases now pass: the original seven repository cases,
four actual refresh HTTP scenarios and four direct actual transaction scenarios
(ordinary success, ordinary denial, committed denial and thrown failure). The
HTTP replay cases verify stored revocation, terminated sessions and increased user
token version through independent contexts, with no new replacement token or
change to another user's token. Expired/unknown requests do not contain an unrelated
session. Direct transaction cases verify both business outcome and committed
state/outbox count, preserving rollback of ordinary failures and exceptions.

Fresh Authentication **2,145**, SharedKernel **1,377**, Authorization **1,667** and
API architecture/security/eventing **108** pass with zero failures/skips. Full
Release solution is warning/error clean. This is **5,312 focused/core cases**,
including 22 new definitions; those focused cases are not added again to a full
suite receipt. Whole API integration now passes **238** cases; its 15 focused cases
are not counted again. Whole API unit execution is in progress at publication.
The complete exported OpenAPI is identical to accepted #258: **1,297 paths and
1,656 schemas**, with the internal containment result/contract absent. EF reports
no model changes since the last migration; no client regeneration is required.
Matching-head gates and merge remain required. **#263 stays OPEN**.

## Remaining acceptance, in implementation order

| Work | Required acceptance |
|---|---|
| Persisted lineage and family identity | Parent/child lineage and family membership survive restart, isolate independent sessions and support existing records through a documented migration. |
| Atomic service rotation | Exactly one complete replacement token/session commits; losing, cancelled or partially failed requests cannot leave a usable orphan replacement. Old tokens are immediately invalid and expiry/TTL remain enforced. |
| Reuse containment | Replaying any replaced token revokes its family, rejects descendants and produces the required security event. Concurrent replay/rotation has an explicit committed outcome. |
| Revocation entry points and bearer consumption | Explicit and user-wide revocation have actor checks and observable effect on real signed JWT requests through the active host pipeline, including persisted token versions and sessions. |
| Alerts and audit | Token generation, rotation, revocation and reuse write durable, credential-free audit events. Reuse alerts reach the chosen user/security surface with retained delivery evidence. |
| Cleanup and metrics | A registered scheduled caller cleans eligible expired/revoked state with retention boundaries, preserves required lineage/replay evidence and exports bounded-label lifecycle counters. |
| Documentation and whole-flow acceptance | Document hashed storage, cryptographic generation, configurable TTL, race/replay behavior, failure recovery and client refresh coordination. Exercise actual endpoints/storage plus unit contracts. Optional sliding expiration is decided and documented explicitly. |

Existing code may satisfy parts of these requirements. Each must be mapped to its
original criterion and demonstrated before acceptance; a passing repository test
does not certify the whole flow. **#263 remains OPEN.**

## Current lifecycle candidate — 2026-10-06

The working candidate extends the accepted increments on develop `295ee2128`.
It is not yet merged and #263 remains open. The preceding execution/publication
statements are historical; the boundaries below describe the current candidate.

### Latest publication checkpoint — 2026-10-06

Revision `3991ff703bef0fc85f71b701a99ce962654df0e3` is published in draft
PR #699. Codacy passed with zero annotations. The final scanner-corrected API
source passed the clean Release build, all 5,764 complete module cases and 73
focused host cases. Complete final-source host integration and unit execution
remains in progress. After the dependency updates, the complete SDK again passed
all 1,135 cases; SDK typecheck/build and site typecheck passed with unchanged
source/lock bytes. The complete site repetition passed all 2,982 cases.

The LLVM digest rejected by the preceding native CI run was independently
downloaded and checked against the immutable upstream commit and original Git
blobs. All 175,014 files reconstruct the expected Git tree; 175,013 are identical
to the primary approved archive. Only `.git_archival.txt` has a different valid
abbreviation of the same full commit. All 191,207 archive headers, including
directories, also match except that file's declared size (137 instead of 138
bytes). The exact verified digest is added to the explicit reviewed list, with
committed provenance and regression coverage. Unknown digests remain rejected
before extraction; downloads never update the lock. Matching-head native CI
still must pass before merge. #263 remains open with all 19 criteria.

### Retained requirement mapping

| Original criterion | Implementation and acceptance surface |
|---|---|
| Full refresh-token metadata | Persisted token/session/predecessor identifiers, creation/expiry, revocation reason/IP/time and hashed successor; existing null lineage is preserved rather than inferred. |
| Rotation on every refresh | Actual refresh endpoint creates a fresh random credential and records one successor after an atomic predecessor claim. |
| Immediate old-token invalidation | The old hash is revoked in the command transaction; subsequent use produces a credential-free denial and committed containment. |
| Parent-child lineage | Restrictive token/session foreign keys; binding and rotation validate owner, hash, active state and immutable lineage. |
| Reuse detection | Replaced/revoked tokens and losing rotation claims use the server-only containment outcome; ordinary rejection keeps ordinary rollback semantics. |
| Family revocation | Server-selected `Family` (default) or `Account` containment. Family mode revokes the persisted owned session family and rejects its signed bearers while preserving an independent same-account session; account mode retains all-session revocation and version invalidation. Unknown legacy families retain account containment. Both modes, concurrent rotation and stale session updates passed focused real HTTP/PostgreSQL checks; full regression and publication remain pending. |
| Explicit revocation endpoint | Authenticated owner guard, stored token/session mutation and real signed bearer rejection through the host pipeline. |
| Revoke-all endpoint | Self-only actor binding; no user selector is accepted; all owned sessions/credentials and bearer version/cutoff are invalidated. |
| Expiration enforcement | Actual expiry predicates and session idle/absolute limits; four HTTP/PostgreSQL cases cover capped stored/session/response deadlines. |
| Cryptographic generation | Existing cryptographic random generator remains authoritative; no deterministic or example token generator is used. |
| Hashed database storage | Only refresh hashes are stored in token/session rows; provider issuers create real bound roots, and raw credentials are excluded from lifecycle evidence/events. |
| Optional sliding expiration | Typed `Jwt:RefreshTokenSlidingExpiration` defaults to true; false preserves the predecessor deadline or a shorter configured TTL. Both modes retain the session's original absolute limit. |
| Configurable TTL | `Jwt:RefreshTokenExpirationDays` remains canonical, with historical `RefreshTokenExpiryInDays` fallback; reported deadlines reflect actual session caps. |
| Reuse alerts | Credential-free `RefreshTokenReplayContainedV1` is persisted in the same transaction as containment. The durable inbox handler queues urgent InApp/Email rows for the stored owner, with retry and duplicate-delivery protection. |
| Audit operations | Issuance, rotation, revocation and containment audit rows commit with their mutations; rejection evidence uses a separate scope. Transaction failure retains neither partial mutation nor accepted mutation audit. |
| Cleanup job | Registered scoped worker, validated bounded batches/retention and failure handling; leaf-first cleanup preserves retained descendants and referenced sessions. |
| Rotation metrics | Registered OpenTelemetry meter; bounded operation/outcome/reason dimensions; committed-outcome counts are buffered until transaction commit and discarded on rollback. |
| Security documentation | This record plus authentication architecture describe storage, races, expiry, delivery, retention and client recovery boundaries. |
| Unit/integration tests | Dedicated real PostgreSQL/HTTP lineage, ownership, audit, cleanup, provider, replay-alert and expiration suites plus module/host tests. Complete regression and matching-revision publication gates remain required. |

### Owner decision: support both replay-containment policies

On 2026-10-06 the owner requested both available containment scopes. The typed
`Jwt:RefreshTokenReplayContainmentScope` selects `Family` or `Account`; `Family` follows the
original automatic-replay criterion, while the explicit revoke-all operation
remains available independently. Legacy credentials without a provable persisted
family must retain account containment rather than infer ownership or lineage.
The initial five-case real HTTP/PostgreSQL run reproduced two failures: family
mode revoked the independent same-account session, and a stale metadata write
reactivated a terminated session. Three account/legacy controls passed. The
candidate now terminates the owner-checked persisted family before querying its
descendants and prevents active metadata writes from clearing termination state.
Account mode retains all-session revocation and persisted token-version change.
The corrected five cases passed in the combined 50-case focused run below;
complete regression, matching-revision CI and merge remain required.

### Current verification checkpoint — 2026-10-06

The fresh Release solution build for both policies completed with zero warnings
and errors. A combined **50-case focused HTTP/PostgreSQL run** passed with zero
failures/skips, including selected scope, independent same-account sessions,
legacy fallback, stale session updates, concurrent rotation, expiry, DTO/storage
metadata, provider bindings, lifecycle audit and durable alert retry/rollback.
Its actual TRX records and counters match; all API/test source remained identical
to the build receipt, and its dedicated database container was removed and verified
absent. These cases are subsets of the pending full integration suite.

The subsequent full Authentication suite retained 2,315 passes and one failed
mock verification. Its legacy concurrent-loss case did not recognize the new
family resolution attempt before account fallback. The test now explicitly
models the unavailable family, checks that resolution occurs in order, and still
rejects every unverified call. Production and HTTP/PostgreSQL test sources did
not change; a source bridge retains the 50-case focused evidence. The fresh full
solution build after this test-only correction again completed with zero warnings
and errors. Four complete module suites subsequently passed on unchanged source:
Authentication 2,316, Authorization 1,667, SharedKernel 1,397 and Notifications 384
(5,764 distinct cases, zero failures/skips). Complete host suites remain running.

[PR #699](https://github.com/gameguild-gg/gameguild/pull/699) publishes this candidate
as a draft against develop. Matching-revision CI and accepted merge remain
required. Its first policy gate rejected three newly published dependency
advisories in sharp, shell-quote and the transitive shadcn MCP SDK; the corrected
lock audit excludes those advisories. Frozen installation and full policy
verification remain pending. Codacy annotations are retained for remediation.
The native CI exception authorized for #697 is not applied to this PR.

The Codacy corrections were applied after the previous complete API unit suite
finished with 1,079/1,079 cases and verified disposal of its database container.
The integration stage was deliberately deferred before executing any test so it
can run against the corrected final source; the orchestration diagnostic and
explicit deferral receipt remain retained. The subsequent full Release build
again had zero warnings/errors. Fresh complete Authentication 2,316,
Authorization 1,667, SharedKernel 1,397 and Notifications 384 suites passed;
73 focused API registration, cleanup/lifecycle and email cases also passed with
matching source/build fingerprints and verified owned-database disposal. The
complete final-source host unit/integration suites and new matching-head scanner
result remain required.

The repeated full site suite passed all 2,982 cases without changing source or
timeout limits. After the corrected dependency installation, CI also passed the
complete 2,982-case site suite, lint, typecheck and build. Seven local runtime
checks verify selected patched package versions, benign native SVG rendering,
rejection of four post-comment shell line terminators and ordinary quoting.
The installed audit retains only the two existing independently verified local
patches. A UTF-8 report reader reconciles an automation decoding error after the
new 1,135-case SDK run exited successfully; the failed diagnostic remains
retained, and final client type/build checks are being completed.

Matching-head CI for the published dependency revision passes API, Web, OpenAPI,
policy and static language analysis. Emception packaging failed while verifying
the downloaded LLVM archive checksum. Its full upstream tree must be verified
before accepting a further archive variant; no checksum check is disabled and
this failure has no inherited owner exception.

The previous complete API integration run is retained with 362 passed and five
failed cases. Three TTL fixtures omitted the session ceiling and two DTO cases
required refresh expiry to exceed a 24-hour access token despite a 24-hour absolute
session limit. TTL cases now explicitly exercise their configured lifetimes below
a larger test-only session ceiling; the independent shorter-cap suite remains
unchanged. DTO checks verify live expiry, persisted token/session ownership and
deadline equality against the configured absolute ceiling. All five passed in
the focused run; production expiry limits were preserved.

The complete SDK passed all **1,135** cases with zero skips, and its typecheck and
build passed on unchanged client/site source. The first full site run retained
2,980 passes and two chart timeouts; a focused retry retained six passes and two
timeouts. A read-only source control against develop and a subsequent unchanged
candidate run each passed all eight chart cases. All nine candidate site-cookie
cases and the candidate site typecheck passed. The initial baseline preparation
rejected checkout CRLF bytes before executing tests; a content comparison corrected
only that diagnostic and retained the failed preparation. Full site repetition
is pending; neither earlier failed run is represented as a complete passing run.

### Session and provider lifetime

Binding caps a token at its actual persisted session deadline. Credential sign-in,
sign-up, OAuth/Discord and Web3 return that deadline in `ExpiresAt` and
`RefreshTokenExpiresAt`; a null, foreign, inactive, expired or wrongly bound session
cannot return successful credentials. The Magic Link/WebAuthn issuer retains its
own persisted root/token/session validation. Rotation never resets the original
session creation time to evade `SessionOptions.AbsoluteTimeoutMinutes`.

Four new real HTTP/PostgreSQL cases first failed because child token expiration
exceeded the session limit and non-sliding configuration was ignored. All four
passed after correction: sliding true/false crossed with one-day/thirty-day
absolute limits. Nine additional wallet unit cases cover rejected returned
session bindings and the shorter reported deadline. Test doubles model the
actual owner/session/hash/lifetime fields; production denial is preserved.

Session binding can shorten the same tracked refresh-token object used by the
provider issuer. A new regression reproduced rejection of a valid bound root
when the persisted deadline was shortened during binding. The issuer now captures
the requested deadline before that mutation and validates the returned session
against this immutable bound; owner, session, hash, activity and expiry checks
remain mandatory. The new baseline failed one case, and the complete issuer
suite passed all 25 after the correction. A fresh provider HTTP/PostgreSQL run
passed all 11 provider cases and all four expiration cases, with zero skips,
unchanged API/test source and verified disposal of its dedicated database
container. Complete host regression acceptance remains required for this
revision; these 15 focused cases are not added to later complete suite totals.

The first complete API unit/integration attempts were interrupted after the
Docker Linux engine became unavailable. Their failures and logs are retained;
neither attempt is accepted as a complete passing suite. The provider deadline
failure was reproduced independently and corrected separately. Docker was
restored, and replacement executions use a dedicated loopback PostgreSQL instance
one host suite at a time. This infrastructure recovery does not erase the
product regression or certify the interrupted suites.

### Alert delivery and failure recovery

The replay event carries opaque identifiers and bounded enums, never a token,
hash, email or IP. User and recipient data are resolved from storage by the host
consumer. Alert queue failure rolls back the consumer inbox and both notification
rows, permitting retry without reversing committed containment. Event production
failure rolls back containment, its audit and the outbox together; success is
not acknowledged without its required evidence. Duplicate inbox processing
cannot create a second set of owner alerts.

Security email requires a rendered message and an accepted `IConfirmedEmailSender`
receipt before its row becomes Sent. Disabled/unconfirmed senders use retries and
dead-letter handling. Rendered text is HTML-encoded and persistent metadata is
excluded. Provider acceptance is distinct from inbox placement or human reading.
The InApp row is the durable user security surface; email requires configured
delivery infrastructure, whose production inbox delivery is not claimed here.

### Client coordination and recovery

The shared session layer coalesces refreshes of the same token within a process;
the web reader shares one request-bound authentication result. Store the returned
replacement pair together, and never intentionally reuse the predecessor. A
401/403 refresh denial immediately clears the returned authenticated session,
even when the old access token is merely near expiry. The refresh manager requests
authentication once and does not retry that denied credential. A temporary 503
may preserve a still-valid access session; an expired access session is cleared.

The writable session endpoint and proxy also remove the encrypted session cookie
and its chunks when authentication returns null. A successful proxy refresh writes
the encrypted replacement pair to the response. The site's request-bound reader
attempts deletion in writable contexts and still returns anonymous state when
Server Components cannot mutate cookies. Immutable Fetch redirects are preserved
while attaching cookie changes to a copied response; actual NextResponse objects
use their public cookie setter so middleware cookie metadata is retained.

Seven real encrypted-cookie cases first reproduced six failures (denial deletion,
replacement persistence and immutable redirects) with a passing temporary-failure
control. The site reader independently reproduced two missing deletion attempts
with seven passing controls. The corrected focused Next suite passes 49 cases,
including real NextResponse replacement/deletion metadata and retained existing
action tests. Complete SDK/site regression remains required for this expanded
candidate. These focused cases are subsets of those complete suites.

Four new negative client cases reproduced preservation/retry of denied 401/403
credentials before the correction. Their passing controls distinguish temporary
failure, real expiry and concurrent request coalescing. Process-local coalescing
is not a distributed mutex: parallel workers/browser contexts must coordinate
refresh requests, and competing use can trigger the selected server containment
policy. No distributed single-flight guarantee is claimed.

Failed and passing receipts are retained under
`artifacts/test-results/issue-263-refresh-lifecycle-20261005/`, including
`expiry-before`, `expiry-after`, `client-denial-before.json`,
`client-retry-denial-before.json`, `client-denial-after.json` and the complete
regression runs. Focused cases are subsets of complete suites and are not counted
again. This mapping preserves all 19 criteria; it does not mark #263 completed
before full acceptance, publication and merge.
