# GameGuild issue closeout inventory

## 2026-10-06 published scanner corrections and LLVM verification

[PR #699](https://github.com/gameguild-gg/gameguild/pull/699) remains draft.
Published revision `3991ff703bef0fc85f71b701a99ce962654df0e3` passed Codacy with
zero annotations. Its API source matches the clean build and passed all 5,764
module cases plus 73 focused host cases. Complete final-source host suites are
running. The dependency-updated SDK passed all 1,135 cases, typecheck and build;
the site passed all 2,982 cases and typecheck.

The exact further LLVM archive rejected by CI has now been verified: its 175,014
files reconstruct the pinned upstream Git tree, with only the known Git describe
abbreviation differing. The 191,207 archive headers also match, except that
file's size. The exact checksum is explicitly reviewed with regression coverage;
unknown checksums still fail before extraction. Matching-head CI remains required.
#263 remains open with every original acceptance criterion retained. Earlier
dated checkpoints below preserve the failed runs and their subsequent corrections.

## 2026-10-06 refresh lifecycle draft — #263

[PR #699](https://github.com/gameguild-gg/gameguild/pull/699) is a draft against
develop. Both owner-requested replay policies are implemented: family containment
by default or account containment, with account fallback for unprovable legacy
families. Lifecycle audit, durable alerts, bounded cleanup, metrics, capped
provider deadlines and client cookie recovery are included.

Four complete module suites passed 5,764 cases with zero failures/skips;
50 focused HTTP/PostgreSQL cases and all 1,135 SDK cases passed. Full host/site
regression and matching-revision CI remain required. The first policy gate found
three newly published dependency advisories; the corrected lock audit excludes
all three, while frozen installation and policy acceptance remain pending.
Codacy annotations are retained for remediation. The #697 exception applies only
to that accepted merge. #263 remains open with all 19 original criteria.

The annotations have now been corrected locally. A fresh warning/error-clean
Release build, all four complete module suites and 73 focused API cases passed.
The previous full API unit run passed all 1,079 cases before those corrections;
complete final-source host regression remains pending. The full site repetition
passed all 2,982 cases, and CI verified them again after installing the corrected
dependencies. Emception CI found a further LLVM archive digest that requires
upstream tree verification before acceptance. No scanner/checksum rule is disabled.

The fixed scope remains 328 IDs. The snapshot verified at
`2026-10-06T18:02:19.756077+00:00` retained 72 closed and 256 open, including
historical/administrative closures.
All 55 unrelated primary file bytes/statuses are preserved. The same worktree
is reused, with three worktrees, four local branches and zero stashes.

## 2026-10-06 accepted Web3 backend merge and closure

[PR #697](https://github.com/gameguild-gg/gameguild/pull/697) is merged into
develop at `295ee212828ab1cc233ce7ed61f2a2b81eb39ed4` from tested revision
`af8595b63e5cbd4229de622909036c4b2df02349`.
[#292](https://github.com/gameguild-gg/gameguild/issues/292#issuecomment-6016888510)
is officially closed as completed: its five backend criteria have matching-head
CI and 20 actual Web3 HTTP/PostgreSQL cases within 327 full API integration cases,
all passed with zero skips. #291 and wallet/provider/UI children retain their work.

The owner explicitly accepted the [documented preexisting native CI failure](https://github.com/gameguild-gg/gameguild/pull/697#issuecomment-6016849454)
for this merge. Its enrollment rejection was reproduced with freshly compiled
develop production source. Native packaging/deployment and later browser steps
remain unverified; no CI assertion or rule was disabled.

The fixed original scope retains **328 IDs: 72 closed /256 open**, verified at
`2026-10-06T13:05:59.977381+00:00`. This includes historical/duplicate/administrative
closures and does not mean 72 new implementations. Empty #271 was explicitly
cancelled by its owner and closed as **not planned**, with its original body retained.
The matrix preserves every scoped ID and original acceptance field.

Both the primary develop and reused implementation worktree were fast-forwarded
to the merged revision. All 55 unrelated primary file bytes/statuses and the
unfinished #263 draft were preserved. The merged remote branch was removed.
Three worktrees remain (primary, this reused checkout and another code-quality
chat's checkout), four local branches and zero stashes; no worktree or stash was
created for this delivery.

#263 remains open. The current local candidate has passing expiration and client
denial regressions, full authentication/client tests and typecheck; complete host
regression and publication/merge acceptance are separate. Its retained 19 criteria
and security boundaries are mapped in
[refresh-token reconciliation](../architecture/refresh-token-rotation-reconciliation.md).
Earlier dated sections below retain their historical status/counts.

## 2026-10-05 Web3 backend local acceptance — #292

The native five-criterion child #292 now has actual bounded-host-cache HTTP,
real EOA signatures and migrated PostgreSQL coverage:20 focused cases within
the entire327-case integration suite. Final source passes7,796 distinct .NET/SDK
cases/35 new; Authentication2256/Authorization1667/SharedKernel1377/APIunit1050/
Integration327/SDK1119. Full solution is warning/error-clean; full spec unchanged
1297paths/1656schemas, no pending model changes, SDK types/client consistency pass.
[Implementation and bounded evidence](../architecture/web3-backend-authentication-reconciliation.md).

**#292 remains OPEN pending matching-head CI and accepted merge.** #291 retains
provider/UI/linking/multiple wallets/disconnection/per-address limits/metrics/full
journey requirements; #263 retains remaining all-provider lifecycle/audit/retention
criteria. No live provider or distributed nonce-store acceptance is inferred.
The fixed328-ID scope is **70closed/258open** at `2026-10-05T17:04:34.129922+00:00`; every original
acceptance field is preserved. All55 unrelated primary files remain preserved.
The same isolated checkout is reused; two worktrees/zero stashes, no agents.
Earlier dated sections remain historical.

## 2026-10-05 accepted persisted refresh-token lineage — PR #695

[PR #695](https://github.com/gameguild-gg/gameguild/pull/695) merged into develop
at `7952eaf967003e536b8ff736509400660d6dd2da`, after every applicable gate passed for
`e49579db56abf04ffef5a0fb54e42c93fd573a60`. Codacy's migration-test SQL annotation was corrected
with constant commands and explicit parameters; no scanner rule was suppressed.
Actual current-head CI passes **4,975 distinct main cases** plus15 repeated
OpenAPI cases: integration307/APIunit1050/Authentication2241/SharedKernel1377.
Together with verified unchanged Authorization1667/SDK1119 receipts, coverage is
**7,761 distinct .NET/SDK cases**, including37 new cases. The reviewed source
also has a warning/error-clean full solution build and fresh PostgreSQL73/OpenAPI15.
The full spec remains equal to accepted #694,1297paths/1656schemas; model and
client consistency are verified. [Individual partial acceptance](https://github.com/gameguild-gg/gameguild/issues/263#issuecomment-5998430026).

**#263 remains OPEN**, with all19 original criteria. Known predecessor/session
GUIDs are stored only from observed owned-session issuance/rotation, with fresh
state checks, restrictive foreign keys, failure rollback and retained ancestry.
All-provider behavior, complete token audit/reuse alerts, scheduled cleanup,
retention and metrics still require acceptance. Web3 #291/#292 cache/account/
session/plaintext defects remain separate; their prepared tests are not passing
acceptance evidence. The fixed snapshot remains **70 closed/258 open**
across328 original IDs; original acceptance text is preserved in every row.
All55 unrelated primary files are preserved. Two worktrees/zero stashes remain;
the completed lineage branch is removed and the isolated checkout is reused.
Earlier dated sections retain historical statuses and counts.

## 2026-10-05 all-user revocation accepted and merged

Live snapshot **2026-10-05T14:10:57.070481+00:00** retains **328 IDs/all original criteria**:
**70 closed /258 open**. No issue was additionally closed in this increment.
[#694](https://github.com/gameguild-gg/gameguild/pull/694) merged into develop at
`fddbc684c55e5ea0607ef4a5097601f82f041152`, accepted head
`cd6c3e9b68fc4f660d4591e7730458f936141b4b`. Existing self all-session logout now
revokes owned refresh tokens/sessions, advances persisted version once and records
a version-bound cutoff; actual credential re-login at the logout second succeeds
while prior bearers are denied. Ordinary/legacy cutoffs remain compatible.
**55 new tests /7,724 distinct current-source .NET/SDK cases** pass: CI276integration,
1,050APIunit/2,235Authentication plus fresh local1,667Authorization/1,377SharedKernel/
1,119SDK. Repeated OpenAPI15/local subsets and failed/historical receipts excluded.
All applicable gates, Codacy and four CodeQL languages pass. Entire OpenAPI unchanged
(1,297paths/1,656schemas); warning/error-clean builds and no pending model changes.
[Public bounded acceptance](https://github.com/gameguild-gg/gameguild/issues/263#issuecomment-5996108933).

#263 remains OPEN with all19 criteria. [Actual login/two-rotation baseline](https://github.com/gameguild-gg/gameguild/issues/263#issuecomment-5995693416)
confirms forward links/stable session but missing ParentTokenId/SessionId metadata.
[Separate Web3 diagnostics](https://github.com/gameguild-gg/gameguild/issues/291#issuecomment-5996026613)
confirm bounded-host cache Size failure. An explicitly unbounded downstream fixture
retains real signature/facade/JWT/PostgreSQL and confirms plaintext duplicate plus
absent account/session; this is not HTTP/command-transaction/production-cache or
provider/UI acceptance. #291 and its approved5-criterion native child#292 stay OPEN.
Original #292 inventory field is retained alongside evidence of its current scope.

Both checkouts synchronized;55 unrelated primary files preserved. Completed branch
removed locally/remotely; two worktrees/zero stashes. Same isolated checkout reused
for next lineage work; no agents used. Earlier dated sections retain history.

## 2026-10-05 ownership and stored-session revocation merged

Live snapshot **2026-10-05T11:54:21.248742+00:00** retains all **328 IDs and original criteria**:
**70 closed / 258 open**. No additional issue was closed by this partial increment.
[#693](https://github.com/gameguild-gg/gameguild/pull/693) merged into develop at
`280ea75a68c739b807c08a6ad724d6cf29f059bf`, reviewed head
`c8d5bd95c064f85de5846bfe4892e446fe2cf916`. Trusted owner-only token revocation
and stored-session bearer validity have actual PostgreSQL/production-JWT/HTTP
acceptance, with 43 new definitions. Matching-head CI passes 266 integration and
2,191 authentication cases, plus a repeated 15-case OpenAPI subset. Fresh local
authorization/SharedKernel/API architecture-security/SDK receipts establish
**6,735 distinct combined cases**; CI also passes **2,959 Web cases in 420 files**.
Historical and incomplete local receipts and repeated subsets are excluded.
All applicable gates, Codacy and four CodeQL languages pass. The sole specification
addition is non-owner403 on the existing revoke endpoint; client consistency and
warning/error-clean builds pass, with no pending model changes.

[#263 remains OPEN with all 19 criteria](https://github.com/gameguild-gg/gameguild/issues/263#issuecomment-5993781040).
[The next actual baseline](https://github.com/gameguild-gg/gameguild/issues/263#issuecomment-5993552561)
returns200 and terminates two sessions but leaves both refresh tokens active and
the user version unchanged. Work continues on the existing all-session endpoint.
Primary is synchronized; all 55 unrelated local files are preserved. Completed
#693 branch removed locally/remotely; two worktrees and zero stashes remain.
Earlier dated sections retain historical stages and pending statements.

## 2026-10-05 reviewed refresh-revoke403 contract

PR693 review adds the documented non-owner403 and an actual Swagger HTTP regression
test. This is the sole OpenAPI difference; other content remains deep-equal. The
client was regenerated and its1,119 tests/typecheck pass; only generation metadata
changed. Fresh reviewed source passes6,479 distinct focused/core/client cases,
43 new definitions, with clean builds and no pending EF model changes. Full
current-head CI/merge are pending; a local whole OpenAPI attempt without terminal
TRX is preserved and excluded. The first commit's6,549 complete local .NET cases
and successful CI remain historical receipts, not current-head totals.

The fixed328-ID scope stays70closed/258open at the last live verification, with
original acceptance fields retained. #263 remains OPEN with all19 criteria.
[PR693](https://github.com/gameguild-gg/gameguild/pull/693),
[bounded progress](https://github.com/gameguild-gg/gameguild/issues/263#issuecomment-5992816972).

## 2026-10-05 explicit refresh-revoke ownership and linked session continuation

The live fixed inventory at **2026-10-05T10:35:31.493481+00:00** retains **328 IDs**,
**70 closed /258 open**, with original acceptance fields unchanged. #262 is
officially completed; #263 remains OPEN with all19 original criteria.

The production ownership baseline found two non-owner204 failures and two passing
controls. The guarded intermediate exposed own-session bearer200 after stored
logout; that failed assertion is retained. Trusted actor ownership and stored
session-bound bearer validation now pass **5,359 focused/core cases**, including
**42 new definitions**: Auth2,191/Authz1,667/SharedKernel1,377/actual PostgreSQL9/
API architecture, security and eventing115. Full API suites and matching-head
gates/merge are pending and excluded from complete totals. Builds have zero
warnings/errors, full OpenAPI remains deep-equal and EF has no pending changes.
[Baseline and retained scope](https://github.com/gameguild-gg/gameguild/issues/263#issuecomment-5992442921),
[current lifecycle reconciliation](../architecture/refresh-token-lifecycle-reconciliation.md).

## 2026-10-05 global refresh-replay official closeout and lifecycle continuation

Live snapshot **2026-10-05T10:06:29.522540+00:00** retains all **328** IDs and original criteria:
**70 closed / 258 open**. [#262 is CLOSED/COMPLETED](https://github.com/gameguild-gg/gameguild/issues/262#issuecomment-5992219521)
after five owner criteria were reconciled to the merged #690/#691/#692 code,
actual PostgreSQL/JWT/HTTP/transaction and forced concurrent endpoint evidence.
[PR #692](https://github.com/gameguild-gg/gameguild/pull/692) merged into develop at
`f3346a891e4887bcf362d0544d23386aeda30ed1` for head `065c6cf39298fd6f66f678d8735bc4bd058f873b`.

Actual current-head CI passes 2,414 main cases plus 14 repeated OpenAPI; all
applicable gates/Codacy/four CodeQL and clean builds pass. Current local core,
CI integration and full APIunit from byte-equivalent scope establish **6,507
distinct combined cases**, with 14 new cases; repeated subsets are excluded.
Final local whole-API aborts and Docker-unavailable fixture failures remain
preserved and are not complete acceptance. Docker has been restored. Full
OpenAPI stays identical (1,297 paths / 1,656 schemas), no model changes pending.

**#263 remains OPEN** with all 19 criteria, including original ParentTokenId and
SessionId metadata requirements, owner revocation, lifecycle/family/audit/alerts,
scheduled cleanup and metrics. [Individual partial acceptance](https://github.com/gameguild-gg/gameguild/issues/263#issuecomment-5992285551).
The owner-guard baseline is being executed next in the same isolated checkout.
Primary is synced, all 55 unrelated files preserved, completed #692 branch
removed locally/remotely; two worktrees and zero stashes remain. Earlier dated
sections preserve their historical stages and totals.

## 2026-10-05 active bearer merge and unavailable-account reconciliation

Live snapshot **2026-10-05T08:48:13.356757+00:00** retains all **328** IDs and original acceptance fields:
**69 closed / 259 open**. [PR #691](https://github.com/gameguild-gg/gameguild/pull/691)
is merged into develop at `7b736e4f765fda0fa981e554f4b63866fd122e11` after all
applicable checks: 6,493 distinct local .NET, CI 4,826 main cases plus 14 repeated
OpenAPI, clean builds, unchanged contracts/model, Codacy/four CodeQL pass.
[Individual partial acceptance](https://github.com/gameguild-gg/gameguild/issues/262#issuecomment-5991037620).

**#262 and #263 remain OPEN.** Independent lifecycle execution found two
deleted-account token failures and four passing controls. The correction denies
unavailable-account refresh before issuance/mutation and rejects versioned bearer;
whole-endpoint concurrency and full acceptance are being executed.
See [lifecycle reconciliation](../architecture/refresh-token-lifecycle-reconciliation.md).
Primary is synced with 55 unrelated files preserved; the completed #262 branch
was removed locally/remotely. Two worktrees/zero stashes remain. Earlier dated
sections preserve historical counts and publication stages.

## 2026-10-05 replay containment merge and bearer requirement reopening

Live snapshot **2026-10-05T07:55:47.216031+00:00** retains all **328** IDs and original acceptance fields:
**69 closed / 259 open**. #262 was reopened after signed-bearer execution confirmed
five revoked-access failures and three passing controls; previous #586 evidence is
retained. [Reopening](https://github.com/gameguild-gg/gameguild/issues/262#issuecomment-5990010929)
and [executed baseline](https://github.com/gameguild-gg/gameguild/issues/262#issuecomment-5990177858).

[PR #690](https://github.com/gameguild-gg/gameguild/pull/690) is merged into develop
at cd152b6587f7b27ac8850b4121652d3db7b6d94d. Mapped token queries and committed
replay containment pass 6,476 distinct local .NET cases, with 22 new definitions.
Actual matching-head CI passes 4,809 main cases plus 14 repeated OpenAPI; all
applicable gates, Codacy/four CodeQL and warning/error-clean builds pass.
Public contracts and EF model are unchanged. [Individual partial acceptance](https://github.com/gameguild-gg/gameguild/issues/263#issuecomment-5990377649).
**#263 remains OPEN** with all 19 criteria. Active signed-bearer enforcement is
under implementation for reopened #262; its integrated acceptance is pending.
Primary is synced, all 55 unrelated files preserved, completed #263 branch removed
locally/remotely, two worktrees/zero stashes remain. Earlier dated sections below
retain their historical states and counts.

## 2026-10-05 polymorphic password official closeout

Live snapshot **2026-10-05T07:03:07.338343+00:00** preserves all **328** original IDs and acceptance fields:
**70 closed / 258 open**, including historical and duplicate
closures. #258 is officially CLOSED/COMPLETED after [PR #689](https://github.com/gameguild-gg/gameguild/pull/689)
merged into `develop` at `47131474c221bff080ec1efd17b53a17aa62b4b2`.
[Individual acceptance](https://github.com/gameguild-gg/gameguild/issues/258#issuecomment-5989540682)
maps all five approved criteria to implemented code and 77 new unit/actual
PostgreSQL/SDK transport definitions. The local functional set passes 8,287
distinct cases, with final-head core/43 PostgreSQL/100 API architecture-security
reruns retained separately. Actual matching-head CI passes **5,501 main API
cases**, 14 repeated OpenAPI and **2,959 web cases**; API/client/web builds,
lint/typecheck, OpenAPI consistency, Codacy and all four CodeQL analyses pass.
Primary is synced with all 55 unrelated files preserved. The completed #258 branch
was removed locally/remotely. Two checkouts and zero stashes remain, with the
same isolated checkout reused for #263.

#263 remains OPEN: two relational predicate failures were reproduced and corrected;
new endpoint execution also exposed rollback of replay containment. That transaction
defect and the remaining original lifecycle criteria require integrated acceptance.
Earlier snapshot sections below retain their original publication states and totals.

## 2026-10-05 polymorphic password implementation acceptance

#258 now has local implementation/acceptance for email, username and international
phone password sign-in. Thirty new unit contracts, 43 actual PostgreSQL/API cases
and four SDK transport cases pass. Current solution/core/integration/client builds
and runs pass (7,238 local cases); complete API unit repeat and matching-head remote
checks/merge are pending. Original five criteria remain intact and **#258 is OPEN**.
[Implementation and explicit boundaries](../architecture/polymorphic-signin-reconciliation.md).
Overall fixed scope remains **328 IDs: 69 closed / 259 open**, as verified in the
official #251 closeout below. The failed earlier fixture startup and compiler
attempts are retained and excluded; previous/repeated cases are not added to totals.

## 2026-10-05 password history official closeout

Live snapshot **2026-10-05T05:37:51.169284+00:00** preserves all **328** original IDs and acceptance fields:
**69 closed / 259 open**, including historical and duplicate
closures. #251 is officially CLOSED/COMPLETED after [PR #688](https://github.com/gameguild-gg/gameguild/pull/688)
merged into `develop` at `8dfd092ff578c27e224d6c9f3154697b3d222fd2`.
[Individual acceptance](https://github.com/gameguild-gg/gameguild/issues/251#issuecomment-5988663714)
maps all retained requirements to 16 new migrated-storage/HTTP/real-handler race cases.
Actual full integration passes 180 cases; local current core/integration 5,332,
with 7,496 local evidence footprint including explicitly retained API unit/client
receipts. CI independently passes 2,294 main cases plus 14 repeated OpenAPI executions.
All applicable gates, Codacy and four CodeQL analyses pass; actual local/CI builds
are warning/error clean. Entire OpenAPI is unchanged and no EF model changes are pending.
Primary is synced with all 55 unrelated files preserved; the completed branch is
removed locally/remotely. Two checkouts and zero stashes are retained.

#258 remains OPEN. Its four failing source-baseline findings are being corrected
without changing the five approved criteria. #223/#243/#285 still require their
separate email/authenticator acceptance; no external provider acceptance is inferred.

## 2026-10-05 password hashing/policy official closeout and next acceptance

Live snapshot **2026-10-05T04:55:32.160488+00:00** preserves all **328** original IDs and acceptance fields:
**68 closed / 260 open**. These are overall states including historical
and duplicate closures, not newly implemented feature counts.

**#254/#255 are officially CLOSED/COMPLETED**, with separate
[hashing acceptance](https://github.com/gameguild-gg/gameguild/issues/254#issuecomment-5988318761)
and [policy acceptance](https://github.com/gameguild-gg/gameguild/issues/255#issuecomment-5988319209),
after [PR #687](https://github.com/gameguild-gg/gameguild/pull/687) merged into
`develop` at `abd7ca4f4adafdb76315ce839c1862ee2ee142ae`.
All applicable accepted-head gates, Codacy and four CodeQL analyses passed.
**7,480 distinct local cases** include 44 new password cases; full API suites replace
their subsets. CI passed **3,649 principal cases** plus 14 repeated OpenAPI executions.
Full API unit is local evidence, not claimed in that CI selection. Builds are
warning/error clean; EF has no pending model changes, and complete OpenAPI/client
remains unchanged. [Compatibility and implementation map](../architecture/password-hashing-reconciliation.md).

The primary checkout is synced with all 55 unrelated local files preserved.
The merged local/remote branch is removed; two checkouts are reused with zero stashes.

**#251 remains OPEN:** 16 new temporary migrated PostgreSQL/HTTP/history/race cases
pass alongside the three fresh old controls. Their temporary build retains five
XML-package audit warnings. Integration in the actual API test project is now
verified: its Release build has zero warnings/errors and all 180 integration
cases, including the 16 new cases, pass without skips. Full-solution/PR acceptance
is in progress; temporary and repeated results are not added to current totals.
**#258 remains OPEN:** a real-source six-case baseline confirms four failures and
two controls, with no DB/provider/production acceptance inferred. Its original
five approved criteria remain intact. #223/#243/#285 retain their email/authenticator
functional acceptance gaps; no new external acceptance is claimed.

## 2026-10-05 MFA recovery merge and password reconciliation

The live fixed scope still contains all **328** original IDs: **66 closed / 262 open**
(03:41:58 UTC). State counts include historical and duplicate closures; they are
not a count of newly implemented features. All original acceptance fields remain preserved.

[PR #686](https://github.com/gameguild-gg/gameguild/pull/686) merged into `develop`
at `8e1266698a0998364ab39a34078e0fd16eaa17c8`. All matching-head applicable gates,
Codacy and four CodeQL analyses passed. CI principal suites passed **4,654** cases,
plus 14 repeated OpenAPI executions; local evidence passed **5,239 distinct cases**
including 35 new cases. The prior API timeout and Debug crash are retained/excluded.
The API allowance increased to 30 minutes without removing a test or gate.
[Full boundaries and evidence](../architecture/mfa-recovery-reconciliation.md).

**#223/#243/#285 remain OPEN:** configured recovery-email delivery and real
authenticator QR acceptance remain unverified. No sign-in challenge/session or
production-delivery acceptance is inferred. The primary checkout preserves all
55 unrelated files, the integrated branch is removed and two checkouts are reused
with no stashes.

**#254/#255 remain OPEN** after an eight-failure hashing baseline and implemented
cost/format/cancellation/full-length corrections. After incorporating #686,
**6,409 distinct local cases** (5,294 .NET and 1,115 client; 44 new password cases
included) passed, including actual migrated PostgreSQL
signup/login/change/reset, all three weak-password writer rejections, configured
policy and independent score controls. The full solution build is warning/error
clean, EF has no pending model changes, and the entire OpenAPI/client contract
matches merged #686. Legacy BCrypt suffixes
cannot be recovered; long legacy login requires reset and history conservatively
rejects matching prefixes. Verification supports BCrypt costs 04–16; higher
imported costs require recovery or a designed migration. Matching-head PR
gates/merge and official acceptance are next in [PR #687](https://github.com/gameguild-gg/gameguild/pull/687).
Its initial 22 Codacy items were addressed in code and all 5,294 .NET cases
passed again; no scanner rule or gate was suppressed. [Requirement and compatibility map](../architecture/password-hashing-reconciliation.md).

## 2026-10-04 authentication DTO official closeout

Snapshot **2026-10-05T01:00:14.386097+00:00** preserves all **328** original IDs/criteria: **66 closed / 262 open**. Counts include historical/duplicate closures and are not newly implemented feature counts.

**#218 is officially CLOSED/COMPLETED** after [PR #685](https://github.com/gameguild-gg/gameguild/pull/685) merged into `develop` at `21fa6f31e3d943d98190e6f7178204485086f775`. [Full acceptance](https://github.com/gameguild-gg/gameguild/issues/218#issuecomment-5986287807) maps response field/callsite/identity/challenge/expiry/mutation behavior to **42 new cases**, **3,864 distinct local cases** and **2,202 distinct exact-head CI cases** (plus 14 repeated OpenAPI executions). Final configured solution/Release builds are warning/error clean; all applicable gates, OpenAPI/client, Codacy and four CodeQL passed. Web/migration/other nonapplicable checks were skipped. Complete v1/client remains unchanged. Failed fixture/setup/interrupted attempts are retained and excluded; optional signup-profile inputs and provider/delivery/production acceptance are not inferred.

**#223 remains OPEN** with [a newly executed backup-status gap](https://github.com/gameguild-gg/gameguild/issues/223#issuecomment-5986323447): 12 remaining codes produce total 10 and used -2 in the actual controller. The configuration DTO lacks original issued count; generation uses configurable count. Synthetic actor/config-only probe, zero database/provider calls or token-authentication acceptance. Further recovery/backup requirements remain to be reconciled before implementation/closure.

All 55 primary local files are preserved. The merged branch is removed, two checkouts are reused and no stashes remain. This continuation officially completed #222/#218; the overall program remains active.

## 2026-10-04 authentication DTO implementation verification (history)

**#218 remains OPEN** with explicit [engineering criteria](https://github.com/gameguild-gg/gameguild/issues/218#issuecomment-5985972085) and [field/callsite/security map](../architecture/authentication-dto-reconciliation.md). Complete names, completed-auth phone and server-owned expiry/duration/profile/token/session/tenant/challenge/risk projection are implemented. **3,864 distinct local cases**, including **42 new cases**, pass; configured solution and targeted Release builds are warning/error clean. Complete OpenAPI/client contract is unchanged; force regeneration, semantic diff and typecheck pass. Actual anonymous signup/login/refresh, high-risk challenge, wrong password and unjoined-tenant rejection are verified with PostgreSQL. Initial missing-membership fixture failure and metadata-tool setup failure remain recorded and excluded. Exact-head CI, merge and official acceptance are pending. The 328-ID snapshot stays **65 closed / 263 open**, and all original criteria remain preserved.

## 2026-10-04 automatic username slugification closeout

Snapshot **2026-10-05T00:03:36.356908+00:00** retains all **328** original IDs: **65 closed / 263 open**. These counts include implementations, duplicate consolidation and historical closures; they are not newly implemented feature counts.

**#222 is officially CLOSED/COMPLETED** after [PR #684](https://github.com/gameguild-gg/gameguild/pull/684) merged into `develop` at `8e946f72143e7f2e1c69219df24542fb817786f2`. [Complete acceptance](https://github.com/gameguild-gg/gameguild/issues/222#issuecomment-5985839842) and [requirement/compatibility map](../architecture/username-slugification-reconciliation.md) record new-user normalization/assignment, generated/chosen collision handling, actual PostgreSQL race/savepoint/version/outbox proofs, real signup HTTP and unchanged persistence/JSON wire structure. **66 new cases**, **6,986 distinct local cases across implementation/follow-up revisions**, and **3,948 distinct exact-head CI cases** passed. Release builds, all latest applicable gates, Web/OpenAPI/client, Codacy and four CodeQL passed; superseded runs remain history. The sole v1/client documentation change is the username description.

**#218 remains OPEN**, with [an executable DTO-conversion reproduction](https://github.com/gameguild-gg/gameguild/issues/218#issuecomment-5985551148). Main-mapper surname/phone loss and the public legacy refresh overload's expiry/ExpiresIn/user loss are recorded separately; the current refresh endpoint uses the main SignInResponse mapper. No database/provider/endpoint proof or implementation is inferred from that probe. Seven live handlers were mapped for the next work item.

All 328 criteria fields and 55 primary local files remain preserved; the merged branch is removed, two checkouts are reused and no stashes remain. The full program remains active.

## 2026-10-04 automatic username slugification — implementation verification (history)

**#222 remains OPEN**, with [explicitly inferred title-level criteria](https://github.com/gameguild-gg/gameguild/issues/222#issuecomment-5985363788) and a [complete implementation/verification map](../architecture/username-slugification-reconciliation.md). New users receive canonical handles across password/OAuth/administrative/bulk factories and actual signup; generated collisions are disambiguated, explicitly chosen collisions fail validation, and existing handles/display names are preserved. The PostgreSQL retry is limited to the username constraint and preserves entity versions and durable-event capture.

The baseline failed 19/20 factory cases. Complete local API unit/integration suites now pass 1,048/1,048 and 137/137 on the implementation revision, alongside Users 714, Authentication 2,023, Authorization 1,667, SharedKernel 1,371 and Users integration 25. After scan corrections, Users 714, Authentication 2,023, the eight new PostgreSQL cases and signup/OpenAPI HTTP 21 pass again; the additional actual-model/JSON case passes 1/1. **6,986 distinct local cases across these revisions** and **66 new cases** are recorded, without adding repeated focused executions. Complete solution and fresh follow-up Release builds have zero warnings/errors. Matching-head CI/merge remains pending. Complete v1 retains 1,296 paths and 1,654 schemas with only the reviewed username-description update; regenerated-client diff and typecheck pass. The last fixed-scope snapshot below remains **64 closed / 264 open**, with all 328 IDs retained.

## 2026-10-04 JWT generation/validation closeout

Snapshot **2026-10-04T22:19:32.145079+00:00** retains all **328** original IDs: **64 closed / 264 open**. These totals include implementations, duplicate consolidation and historical closures; they are not newly implemented feature counts.

**#237 is officially CLOSED/COMPLETED** after [PR #683](https://github.com/gameguild-gg/gameguild/pull/683) merged into `develop` at `a3952444077f4f1482897d2570bd3b445786faf8`. [Complete acceptance](https://github.com/gameguild-gg/gameguild/issues/237#issuecomment-5985063129) and [requirement/compatibility mapping](../architecture/jwt-generation-validation-reconciliation.md) record consistent HS256 enforcement, corrected legitimate additional claims and rejection of protected identity/tenant/session/permission/MFA/actor claims. Seventy-eight new cases were added. **6,104 distinct local cases** and **4,437 exact-head CI cases** passed; Release builds were warning-clean and all applicable checks, Codacy, four CodeQL and OpenAPI/client consistency passed. Complete v1 and generated contracts are unchanged.

#284/#41 retain refresh, session, account-state and revocation requirements. #238 remains a native duplicate. All 328 criteria fields and the 55 primary local files remain preserved; the merged branch is removed, two checkouts are reused and no stashes remain. The full program remains active.

**Next #222 remains OPEN:** [the executed signup gap](https://github.com/gameguild-gg/gameguild/issues/222#issuecomment-5985095513) captures `User.Username = null` at the repository boundary, while the supplied username becomes `Name`. The real signup service/password hasher ran with synthetic inputs; persistence and external providers were intentionally not called. This is a pre-persistence defect reproduction, not signup/HTTP/OAuth acceptance. The matrix retains the original empty-description/title provenance and further verification requirements.

## 2026-10-04 authentication facade closeout

Snapshot **2026-10-04T21:01:12.847150+00:00** retains all **328** original IDs: **63 closed / 265 open**. Counts include implementation acceptance, consolidation and historical closures; they are not a count of newly implemented features.

**#219 is officially CLOSED/COMPLETED** after [PR #682](https://github.com/gameguild-gg/gameguild/pull/682) merged to `develop` at `e5352249efcc076eb0fd825b400c679f94f3f541`. [Complete acceptance](https://github.com/gameguild-gg/gameguild/issues/219#issuecomment-5984333216) and [all five requirements/consumers](../architecture/authentication-facade-reconciliation.md) record the existing facade's 18 operations, strict delegation and argument/task/result/error/cancellation preservation, plus actual API registration of five real scoped services and eight handlers. One hundred new cases were added. Local Authentication/Authorization/API registration passed **3,619 cases** without duplicate counting; exact-head CI passed **2,988** API/Authentication/full-application HTTP cases with warning-clean Release builds and all applicable required/security checks. No remote-provider or persistence acceptance for other issues is inferred.

**#237 remains OPEN** under native parent #284. An isolated executable probe reproduced inconsistent HS256 algorithm enforcement and dropped additional claims; [the gap record](https://github.com/gameguild-gg/gameguild/issues/237#issuecomment-5984239259) records precise limits and the next correction. All 328 requirement fields are retained. The merged feature branch is removed, two checkouts are reused, no stashes remain and all 55 primary local files were preserved.

## 2026-10-04 API version/OpenAPI reconciliation

Snapshot **2026-10-04T20:23:07.298955+00:00** retains all **328** original scope IDs: **62 closed / 266 open**. Earlier counts below are dated snapshots. State counts do not establish feature completion.

Canonical **#144 and #147 are officially CLOSED/COMPLETED**, after reproducing and correcting full-version document selection, native semantic format tokens and inherited span/interpolation gaps. The [#144 acceptance comment](https://github.com/gameguild-gg/gameguild/issues/144#issuecomment-5984012777) and [#147 acceptance comment](https://github.com/gameguild-gg/gameguild/issues/147#issuecomment-5984014026) record all original functional criteria, technical requirements, Definition of Done and limitations. #272 and #275 are native DUPLICATE links to them; those closures consolidate tracking and are not additional implemented features. Original descriptions, source reviews and all 328 matrix requirement fields are preserved.

PRs [#679](https://github.com/gameguild-gg/gameguild/pull/679), [#680](https://github.com/gameguild-gg/gameguild/pull/680) and [#681](https://github.com/gameguild-gg/gameguild/pull/681) merged into `develop`; final merge `92842cdd731bd77ee329073eed33c70287f87508` at `2026-10-04T20:19:47Z`. [Full requirement mapping](../architecture/versioned-openapi-reconciliation.md) includes configuration/migration and reproducible performance fixtures. Exact final head `09c838b2a636aa75321145671c8a72d3f689e497` passed **2,407 CI cases**: 1,022 API, 1,371 SharedKernel and 14 complete-application HTTP, warning-clean fresh Release builds and all required/security checks. Final local SharedKernel 1,371/1,371 and selected API cases 42/42 passed. Interrupted/failed attempts are excluded.

The complete `v1` document and generated client remain unchanged: 1,296 paths / 1,654 schemas, canonical SHA-256 `45b42923086a1f05d6a0288c161039965f8b06d92c2fc661232c78be9972d454`. All 55 primary local files retained their bytes and statuses. The merged feature branches were removed; the isolated checkout is reused. This closeout does not establish completion of the remaining 266 open issues or of the full program.

## 2026-10-03 22:16 UTC reconciliation and retention closeout

The authored-or-assigned union remains **328 unique issues** (316 authored, 321 assigned). The GitHub snapshot at **22:16:28 UTC** contains **304 open and 24 closed**. The separate closed-issue review has reopened additional historical closures, including #208; the matrix preserves their previous implementation/closure evidence as historical and marks the current requirement review as pending. Earlier counts below are dated snapshots, not the current state.

Issue **#194 is implemented and officially closed** after [PR #672](https://github.com/gameguild-gg/gameguild/pull/672) merged into `develop` at `3886e260bb05234666f75c365fe1993294c14d58`. Its [resolution comment](https://github.com/gameguild-gg/gameguild/issues/194#issuecomment-5973995369) and [criterion mapping](../architecture/audit-retention-simulation.md) record the complete simulation feature, measured storage/access evidence, configuration and report persistence, safeguards and model limits. Local verification passed 3,841 tests across Audit, Authentication, Authorization, API architecture/security and PostgreSQL/HTTP, plus a warning-clean solution build and client generation/type checks. Current-head API/Web/OpenAPI/migration/policy/artifact/PR Required Gate and all four CodeQL analyses passed. The nine earlier Codacy style findings were corrected; its new-head suite remained queued at merge, and a rerequest returned HTTP 404. This pending scanner result is disclosed rather than counted as a pass.

Issue **#178 remains open and unimplemented as an executable packaging feature**. Full-body/source review found a manually populated entity and interface declarations, but no packaging service, controller, EF package mapping, automatic collectors, framework templates, quality/gap/timeline validators or verifiable artifact generation. Its row records these gaps and the required functional/security tests; this source review does not claim functional completion.

The primary `develop` checkout was fast-forwarded to the #672 merge with all **55 unrelated modified/untracked files** verified byte-for-byte against their preservation manifest. The merged feature branch was removed locally and remotely. Two worktrees remain and there are no stashes.

Snapshot date: 2026-09-28 (GitHub API).

This initial source inventory contains **328 unique issues** authored by or assigned to `mathrmartins` in `gameguild-gg/gameguild`: **188 open** and **140 closed**. Pull requests are excluded. The scope is the union of GitHub's `creator=mathrmartins` and `assignee=mathrmartins` issue filters, deduplicated by issue number.

A fresh GitHub query on 2026-09-29 confirmed 316 authored and 321 assigned issues, deduplicating to 328 unique records. Current state after PRs #596, #597, #599, and #582: 182 open and 146 closed.

A fresh GitHub API query on 2026-09-30 returned 316 issues created by and 321 assigned to `mathrmartins`; their union remains 328 unique issues. The PR #611 snapshot had 179 open and 149 closed. A subsequent live refresh on 2026-09-30 found 178 open and 150 closed. `gameguild-issues-2026-09-30.csv` refreshes state, `updated_at`, and `closed_at` for all 328 rows and adds verified closeout evidence for #148, #308, and #324. `gameguild-issues-2026-09-28.csv` and `gameguild-issues-source-2026-09-28.jsonl` preserve the original source snapshot. Live counts alone do not prove issue completion.

`gameguild-issues-2026-09-28.csv` preserves issue metadata and per-issue audit fields. `gameguild-issues-source-2026-09-28.jsonl` preserves each original issue body and source metadata as one JSON-escaped record per issue. Most issues still need an evidence review. A mapped PR is an evidence pointer, not proof that the acceptance criteria are complete; update issue dispositions only with current source, tests, dependencies, related issues, and the corresponding PR or rationale.


## Initial issue-level pass

Forty-nine issues have a completed issue-level evidence pass: #8, #9, #10, #12, #13, #16–#28, #30–#35, #37–#43, #144–#149, #260, #261, #262, #271, #308, #309, #324, #335, #353, #354, #384, and #390. #8/#9/#12 are supported by current repository structure and their closure comments; #10 is obsolete under the current .NET architecture; #13 is consolidated with #72 and both current application Dockerfiles built successfully. #16 and #17 are duplicates of #291: the owner closed both with explicit rationale, #291 covers EIP-1193 providers and names Trust Wallet, and its implementation remains open. #18, #38, and #39 are also covered subsets of #291 (WalletConnect, frontend wallet context, and MetaMask); the owner recorded each consolidation. These closures consolidate tracking; they do not prove wallet authentication is implemented. Issue #31 is also verified complete at title level: the maintainer said the basic components were done, the owner added a 2026-09-26 closeout note, and `packages/ui` exports reusable components/styles consumed by `apps/web` through the `@game-guild/ui` workspace package. #32 is complete at title level: packages/tooling contains reusable TypeScript, Prettier, ESLint, and Jest config packages; workspace references and current manifests confirm the requested packages, though the web runner has since moved to Vitest. #33, #35, and #37 are verified against the current manifest, robots, and async sitemap routes. Their bodies are empty; owner closeout comments record focused tests that passed historically, while a matching persistent route-test file was not found in the current web tree. Issue #37 was originally closed for inactivity, so the current source and owner evidence—not that closure—support keeping it closed. #34 and #40 have current localized sign-in/sign-up routes and corresponding route tests; the owner recorded focused test success, and #40 was closed under the decision to combine the entry experience. #41 was once closed with a never-expiring access-token workaround, but current JWT access/refresh expirations and refresh rotation replace it; the current unit suite covers expired and rotated tokens, and the owner recorded 76/76 refresh-related tests passing. #42 was explicitly replaced by #43 in its closure discussion; current LocalAuthService still implements email/password, while #43 remains open for the separately tracked magic-link flow. #19 was closed by the maintainer as completed/published; the current web application imports shared UI tokens and components. #20 was explicitly closed as not planned due to specialist effort and delivery priorities; the current shared UI package provides reusable primitives but is not treated as proof of a complete design system. #21 has current editor and public profile routes; the four current SocialProfileView tests pass, though no profile-settings page test was found. #22 and #24 were closed in favor of #52; the public course page and its four focused tests confirm the UI subset, not the full CMS scope of #52. #23's shared auth frame and sign-in/sign-up routes are present, with four focused entry-page tests passing. #25 remains non-actionable because no redesign brief or verifiable target exists. #26 and #27 are represented by the current localized sign-up/sign-in flows; their focused tests pass 2/2 each. #271 was already closed with an owner-authored explanation that the title-only request has no problem statement, user impact, or verifiable acceptance; keep it closed unless those details are added. #30 has a clarified SIWE acceptance and is implemented on PR #597, merged to develop after Authentication tests passed 1,753/1,753, focused SIWE tests passed 12/12, and the required API, Web, OpenAPI, and PR gates passed. Durable wallet/account linking remains tracked under #291; earlier PR #587 is closed as superseded. #144 is implemented on PR #599, merged to develop after API versioning/OpenAPI tests passed 19/19, SharedKernel option tests passed 21/21, and semantic routing telemetry passed 1/1. API Verify, OpenAPI client consistency, Codacy, CodeQL, repository policy, and PR Required Gate passed. The four-case BenchmarkDotNet ShortRun is diagnostic, not a production performance guarantee; earlier PR #591 is closed as superseded. #149 was implemented by PR #582 and closed at 2026-09-29T20:25:49Z. The acceptance criteria and Definition of Done require in-memory and Redis storage; the database backend appears only in the Proposed Solution. Fixed-window, sliding-window, and token-bucket algorithms, Redis distributed enforcement, failure modes, response headers, metrics/alerts, access lists, and concurrent multi-host integration coverage were verified. Representative production profiling remains an operational follow-up, not an open acceptance item. #260 and #261 were independently reviewed against their title-only requests: API-key IDs have buckets isolated from user IDs without trusting raw credentials; anonymous authentication requests partition by client IP and authenticated requests by user ID. Both issue bodies are empty, so these criteria are explicitly inferred from their titles. #262 had an empty body; the owner clarified refresh-token replay acceptance, and PR #586 merged the atomic rotation claim plus family-wide session invalidation. Authentication tests passed 32/32 and PostgreSQL rotation tests passed 2/2; #262 closed with that evidence. #308's issue body is empty, but the owner clarified acceptance in a GitHub comment. PR #610 merged and closed #308 on 2026-09-30; the handler, controller, repository, and tests implement the 1–500-user SystemAdmin flow, prevalidate non-deleted targets, deduplicate IDs, preserve or reactivate assignments, return per-user outcomes, and persist changes once. Authentication tests passed 1,768/1,768; API Release publish, PostgreSQL 17 OpenAPI capture, generated-client diff/build, and applicable GitHub gates passed. PR #576 is superseded. #309 had 4/4 focused PostgreSQL integration tests, but PR #613 explicitly leaves safe cross-call caching/version invalidation and repeated representative PostgreSQL benchmarks incomplete; its benchmark harness uses EF Core InMemory. After the issue was closed by the owner at 09:23 UTC, the evidence audit reopened it at 09:26 UTC with those remaining acceptance items.

Issue #28 is implemented on PR #596, merged to develop after the clean-base port of #592. It provides a responsive branded 404 with accessible heading/navigation and locale-aware home/course links; focused tests passed 2/2, and Web Verify plus PR Required Gate passed. The earlier PR #592 is closed as superseded; issue #28 closed at 2026-09-29T16:55:25Z.

Issues #353 and #354 were independently compared with their original acceptance criteria and source. The implementation includes L1/L2 caching and fallback, tenant/global versioned ACL keys, dynamic-role and tenant-role invalidation, administrator statistics, explicit and popularity-driven scheduled warm-up, cache metrics/alert definitions, per-type TTLs, and Social Groups membership/token refresh. New coverage also verifies stale-allow and stale-deny database reads that overlap a version change. On earlier PR #581 implementation head `f427dec`, Authorization UnitTests pass 1,628/1,628 and the Docker-backed Redis integration selection passes 7/7, including cross-instance invalidation, dependency eviction, subscriber recovery, and both concurrency cases. A local PostgreSQL 16 benchmark includes the database-backed tenant/global version lookup and compares 1/8/32-query batches: database/cache means were 763.6/673.6 µs, 2.312/3.218 ms, and 9.068/7.031 ms. The 99.9% confidence intervals overlap widely, so a repeatable performance improvement is not established; the fixture also excludes EF materialization and production deployment. On that head, API Verify, Web Verify, OpenAPI consistency, Repository policy, and PR Required Gate passed; Codacy identified three unused parameters on the benchmark fake. Commit `1f1a5b783` explicitly discards those required interface inputs, and the affected performance-test project builds with zero warnings and errors. On current head `2f20d3542`, the administrator statistics endpoint returns a concurrent, lock-free aggregate of average lookup duration and sample count by cache type, including direct L1 ACL and policy lookups; it stores no raw samples. Authorization UnitTests pass 1,631/1,631 and Authentication UnitTests pass 1,755/1,755. API Verify, Web Verify, OpenAPI consistency, Codacy, repository policy, tracked-artifact validation, release classification, and PR Required Gate pass; migration compatibility and Economy Release Gate were skipped by workflow conditions. This remains partial implementation, not issue completion. Production-like performance evidence and validation in the configured metrics collector remain incomplete or unverified. Keep both issues open.


Issue #145 was reviewed against its acceptance criteria and current authentication setup. JWT bearer is the default scheme; provider-specific OAuth login flows exist, security headers and some password/lockout configuration exist in separate code, but the API-key handler had not been registered by the main setup and its source was fixed to one header. PR #594 adds typed opt-in API-key registration alongside JWT, configurable header/query sources plus a custom resolver, and HTTPS enforcement for query credentials. It now makes validated `AuthenticationOptions` drive JWT bearer validation and `JwtOptions` used for signing and token lifetime; local, OAuth, Web3, magic-link, and WebAuthn responses read the same access/refresh lifetimes. `SecurityServiceCollectionExtensionsTests` pass 29/29, Authentication UnitTests pass 1,741/1,741, and SharedKernel UnitTests pass 1,267/1,267 on code head `4b8fff4`. The regression test verifies that differing configuration and `AuthenticationOptions` values resolve consistently for bearer validation and token issuance. PR #594 was squash-merged into `develop` at `dd00a51165e825c6f4b3eaff8f3cbdb6c4dded92` on 2026-09-30; issue #145 remains open. API Verify, OpenAPI consistency, Actions/C#/JavaScript/Python analysis, CodeQL, Codacy, PR Required Gate, repository policy, artifact validation, release classification, Emception detection, and NuGet submission passed on the merged head. Web, Migration, Economy, Testing Lab, and Emception build/deploy jobs were skipped by workflow conditions. This remains partial: Basic/cookie schemes, broader provider configuration, full Identity integration, MFA/policy configuration, and remaining functional/documentation criteria are unresolved. Keep #145 open.

Forty-nine issues have now received an issue-level evidence review; 279 records remain without an issue-level audit.

Eleven issues have now been closed by implementation PRs in this program: #28/#596, #30/#597, #144/#599, #148/#608, #149/#260/#261/#582, #262/#586, #308/#610, #324/#609, and #384/#584.


## Linked implementation pull requests

The matrix contains evidence pointers for 21 issue records. Implementation merges have closed #28/#30/#144/#148/#149/#260/#261/#262/#308/#324/#384 through #596/#597/#599/#608/#582/#586/#610/#609/#584. PR #583 and #585 merged partial work: #390 remains open pending confirmed acceptance and runtime/database evidence, and #335 remains open for schema filtering, masking, audit, and broader integration coverage. PR #613 merged partial bulk-check work; #309 was reopened on 2026-09-30 because caching with safe version invalidation and representative repeated PostgreSQL benchmarks remain incomplete. PR #594 is partial, so #145 remains open. The active implementation PRs are #616 for #147 and drafts #579 for #251 and #581 for #353/#354. PR #589 has merged to develop, but #579/#581 still target its old feature branch and need base reconciliation before merge. PR #584 completed #384; #387 remains open for broader tenant-permission criteria. Issue #43 remains open for sandbox delivery verification after PR #606 merged into develop. #587/#591/#592 are closed as superseded; #272 remains the historical duplicate reference for #144. PR URLs, head commits, and last captured checks are in the matrix; refresh checks before treating them as current. These links are evidence pointers, not closure evidence.


## 2026-09-29 implementation follow-up

Issue #43 has a blank body; its owner comment clarifies a localized magic-link request and consume flow, with a sandbox email-delivery round trip still required. PR #606 was squash-merged into develop at `94e944d45a182f056e052d061116fba6dae617c8` on 2026-09-30. API tests passed 1,755/1,755, web tests passed 39/39, and API Verify, Web Verify, OpenAPI consistency, Codacy, CodeQL, and PR Required Gate passed. Notification-queue failures preserve the generic response and are logged. Keep #43 open until sandbox email delivery is verified.

The closeout inventory retains 20 issue records. Nine implementation issues are closed by #596, #597, #599, #608, #582, #610, and #609: #28/#30/#144/#148/#149/#260/#261/#308/#324. Ten other mapped issues remain open across eight implementation PRs; #43 remains open for sandbox delivery verification after #606 merged. Those PRs include partial changes, related issue references, and code awaiting review; none counts as a completed issue until its acceptance criteria and resolution evidence are satisfied.

PR #609 superseded the draft #580 and merged the title-level field-masking implementation, closing #324 on 2026-09-30. It applies tenant-aware masking to explicitly marked successful MVC object/JSON responses and fails closed if rules cannot be evaluated. Six API field-masking tests, seven authorization-service tests, and one PostgreSQL tenant-isolation integration test passed; API Verify, OpenAPI consistency, PR Required Gate, Codacy, CodeQL, and repository-policy gates passed.

PR #581 is at head `2f20d3542` and remains a draft. It adds bounded bulk cache reads and writes (up to 500 submitted entries, concurrency capped at 16), batched permission warm-up, L1 fallback and metric fixes, ACL cache-size metadata for `MemoryCache.SizeLimit`, retry protection when an ACL database read crosses a permission-version change, and a concurrent, lock-free process-local average latency/sample aggregate in the cache statistics endpoint. GameGuild Authorization UnitTests pass 1,631/1,631; Authentication UnitTests pass 1,755/1,755; Docker-backed Redis integration tests pass 7/7 on the earlier implementation head `f427dec`. The local database/cache comparison is recorded above and does not establish consistent speed improvement. API Verify, Web Verify, OpenAPI consistency, Codacy, repository policy, tracked-artifact validation, release classification, and PR Required Gate pass on current head `2f20d3542`; migration compatibility and Economy Release Gate were skipped by workflow conditions. PR #589 has since merged to develop, but #581 still targets its old feature base and is dirty against develop; rebase it before considering merge. Issues #353 and #354 remain open for production-like performance evidence, configured collector/alert validation, review/merge, and their other acceptance criteria.


PR #594 delivered a partial implementation for #145 and was squash-merged into develop at `dd00a51165e825c6f4b3eaff8f3cbdb6c4dded92` on 2026-09-30; keep #145 open. The existing no-argument API-key extension remains source- and binary-compatible; API-key authentication is opt-in alongside JWT, keeps the handler's existing default header and query names unless overridden, supports a custom resolver, rejects ambiguous sources, and requires HTTPS for query credentials. `AuthenticationOptions` now drives JWT signing/validation credentials and access/refresh lifetimes; local, OAuth, Web3, magic-link, and WebAuthn responses use those same lifetimes. API security configuration tests pass 29/29, Authentication UnitTests 1,741/1,741, and SharedKernel UnitTests 1,267/1,267. API Verify, OpenAPI consistency, Actions/C#/JavaScript/Python analysis, CodeQL, Codacy, PR Required Gate, repository policy, artifact validation, release classification, Emception detection, and NuGet submission passed on the merged head. Web, Migration, Economy, Testing Lab, and Emception build/deploy jobs were skipped by workflow conditions. Keep #145 open for its remaining Basic/cookie, provider configuration, Identity, MFA/password-policy, documentation, and integration criteria.


## 2026-10-01 account lockout concurrency follow-up: #145

PR #644 merged into `develop` at `28f05b99894de8cba67be06f7c9ed09dd167999b`; PR #645 merged at `d50cdf81e9c069f7208a0b35474a4cb15eee09c3` (head `259f711e869cf9483e36d19534a1197ebb244f62`). The follow-up uses a PostgreSQL session advisory lock keyed by normalized email to serialize the lockout count through the local sign-in action across API instances. A concurrent request receives the generic unauthorized response and can retry. The regression test uses separate database contexts to prove the second request cannot pass while the first sign-in is in flight.

The PostgreSQL concurrency regression passed 1/1; focused API security/filter tests passed 44/44; Authentication UnitTests passed 1,826/1,826; Authorization UnitTests passed 1,662/1,662; and the full API solution build completed with 0 warnings and 0 errors. API Verify, CodeQL C#, Codacy, repository policy, artifact validation, release classification, and PR Required Gate passed on PR #645. Issue #145 remains open for its other authentication configuration and integration requirements.

## 2026-09-30 implementation closeout: #148

Issue #148 is implemented and closed by merged PR #608 (merge `608487f9b85428b301860598863f2705d2c77cc0`, head `abeeb07a8a9412e578dc921743beb39862612607`). The typed options, exception mapper/middleware, MVC result filter, and status-code pipeline cover common exception mapping, safe environment-gated details, RFC 7807 envelopes for MVC validation/results and empty status-only failures, preservation of legacy bodies and all validation field errors, localization, trace/correlation IDs, custom extensions, configuration validation, docs, and migration guidance. API UnitTests passed 822 before the final legacy-body adjustment and 20 focused ProblemDetails tests after it; SharedKernel tests passed 39; API Release publish with warnings-as-errors passed. The CI API/Web/OpenAPI, Codacy, CodeQL, repository policy, artifact, and required-gate checks passed. Its ShortRun benchmark compares formatting/serialization only and is not a production HTTP latency guarantee.

## 2026-09-30 permission-history closeout: #384

Issue #384 was reopened for the verified tenant-permission history gap and closed after PR #584 merged to develop at `4a5b3b01ae0c9519147e754676af502808b91574`. Revocations preserve rows with `DeletedAt`, ordinary reads filter deleted rows, new grants preserve prior history, and the by-ID revoke path now applies tenant authorization, actor context, audit, and version invalidation. The solution build passed with zero warnings/errors; Authorization UnitTests passed 1,591/1,591, Authentication UnitTests 1,742/1,742, and API architecture/security tests 48/48. Migration compatibility, API verify, OpenAPI consistency, repository policy, and PR Required Gate passed. The broader tenant-permission issue #387 remains open.

## 2026-09-30 option-configuration review: #146 and #147

Issue #146 is only partially represented by the current options contract. AuthorizationOptions contains a default policy name, an authenticated-user flag, and the system-account ID. SetupAuthorization validates the supplied object but registers the separately configuration-bound options and three hard-coded role/tenant policies. Identity.Authorization has database-backed policy definitions, role/claim rules, caching, and audit services, but they are not composed through this options object. Role hierarchies, resource/claim transformations, external authorization configuration, time/location policies, and their option-driven tests remain unverified or absent. Keep #146 open.

Issue #147 has typed options for basic document metadata, but the service setup hard-codes the Bearer security definition and requirement, and the UI route/endpoints remain in PipelineExtensions. The configured terms and license fields and OpenApiOptions.EnableOpenApi have no consumers; the active registration gate is PresentationLayerOptions.EnableOpenApi. OpenApiInfo.Version uses the assembly release version, while OpenApiOptions.Version selects only the fallback document key. Typed server/security/UI/extension settings, localization, schema examples, and their tests remain open. Keep #147 open.

## 2026-09-30 live issue-state refresh

A live query over the current authored-or-assigned issue set returned 328 unique issues: 177 open and 151 closed. The refreshed CSV updates each row's current title, state, assignees, labels, milestone, and timestamps while preserving the issue-level audit fields. PR #614 was squash-merged to develop at 4af6096dabb65247c03ad94106bbe577485a95e2; it advances #353/#354 but leaves both open because representative performance/load measurements and configured collector/alert validation remain outstanding. PR #584 also merged to develop and closed #384. PR #581 is still open and draft; its overlapping cache work still needs reconciliation.

## 2026-10-03 live issue-state refresh

A fresh GitHub query reconciled all **328** authored-or-assigned issues: **163 open and 165 closed**. [gameguild-issues-2026-10-03.csv](gameguild-issues-2026-10-03.csv) refreshes titles, state, assignees, labels, milestones, and timestamps for every row while preserving the prior audit fields and recording the state-verification timestamp. The 2026-09-30 snapshot remains unchanged as history. In the current snapshot, **110 closed issues are still marked Not reviewed**; their closed status is not treated as proof of implementation or valid disposition.

Issues #353 and #354 were reopened on 2026-10-03 with evidence comments because the recorded cached-versus-uncached benchmark had overlapping confidence intervals and excluded production topology, and target metrics/alert delivery remained unverified. PR #662 adds configurable cache-health thresholds and structured warning logs, but its CI status is mixed: API verify and OpenAPI consistency passed; Codacy, Repository Policy, and PR Required Gate failed; CodeQL was skipped. Keep #353/#354 open until representative performance and target telemetry evidence are available.

Owner crosswalk comments support the following duplicate-only closures: #349 → #306, #295 → #294, #315 → #314, #326 → #327, #332 → #333, #345 → #346, #347 → #330, #398 → #413, #399 → #414, #402 → #418, and #407 → #408. Their canonical issues remain open; these closures do not establish implementation.

This snapshot is a state refresh plus the specific closeout evidence listed above. It does not complete the remaining issue-by-issue audit.

## 2026-10-03 post-merge reconciliation

A live query at 2026-10-03 15:45 UTC confirms 328 authored-or-assigned issues: **156 open and 172 closed**. #147 was completed after PR #651 merged and the corrected schema-description/example tests passed. #154 and #156 were closed as duplicates after their unique criteria were transferred to canonical issues #150 and #165. #170/#172 were already duplicate-closed under #171/#173; the canonical CSV/JSON export issues were completed by merged PRs #653 and #656, with their export tests and required CI gates passing. #150 and #165 remain open. The matrix records #164 and #166 as partial implementations, not completed work. #158 and #159 remain open and partial in PR #669 (head 6bb889ddd087091e47740bfe5b911003d108fc3d); focused authorization-audit tests pass 11/11. At 15:45 UTC, repository policy failed on the transitive braces@3.0.3 CVE-2026-93687 advisory, while API/Web and C# CodeQL checks were pending; OpenAPI consistency and Codacy passed.

## 2026-10-03 closed-audit issue review

A live review of the remaining closed issues in #151–#197 reconciled eleven rows that had been incorrectly left as `Not reviewed` in the matrix. Ten remain closed with recorded reasons or implementation evidence: #151/#152 are underspecified console requests; #153 is a duplicate chain to #150; #155 is covered across PRs #653/#656/#658; #161 is implemented by #660; #162 by #659; and #163/#167/#168/#197 are title-level capabilities verified against current services and tests.

Issue #157 was reopened after reviewing merged PR #661. Its normal path captures permission changes and before/after state, and its focused API test suite passed eight tests. The failure path is incomplete: `ApplicationDbContext` clears the in-memory pending changes before delivery, then logs and drops them if the central audit service is unavailable or throws. There is no durable retry/recovery. The issue comment records the evidence: https://github.com/gameguild-gg/gameguild/issues/157#issuecomment-5970894047.

After reopening #157, a live query at 2026-10-03 16:06 UTC reports 328 authored-or-assigned issues: 157 open and 171 closed. The matrix now records the ten reviewed closed issues and #157's reopened state; audit-gate failures for merged PRs #658/#661 are retained in their rows rather than presented as green checks.


## 2026-10-03 historical closeout review: #8–#143

Reconciled all twelve closed/unreviewed rows in #8–#143. #44/#45 remain closed as unscoped analytics follow-ups; #54 records a product decision against WhatsApp notifications; #55 and #81 are non-actionable historical ideas/campaigns; #46/#106 are superseded by the .NET API architecture; #56 is implemented at title level by the shared localized auth layout; #99 is a duplicate of still-open #92; #115 is obsolete because Wasmer was removed; and #143 is covered by current version-reader/routing tests, with a fresh focused run passing 6/6 tests.

#109 was reopened because its concrete brand-guide/logo alternatives/typography/color/spacing/usage acceptance remains unmet. The repository has a few legacy PNGs and a generic UI palette, but no coherent brand guide or usage examples. Reopen evidence: https://github.com/gameguild-gg/gameguild/issues/109#issuecomment-5970990942.

After reopening #109, the live issue set is 328 total: 158 open and 170 closed. This review does not count a stale/inactivity closure as implementation evidence.
## 2026-10-03 GitHub closure-event reconciliation

The live set is 328 issues: 158 open and 170 closed. In the GitHub timeline, 90 of the currently closed issue IDs have at least one close event by the authenticated account `mathrmartins` since 2026-09-25; these IDs account for 100 close transitions because some were reopened and closed again. This is an account-action count, not a count of implemented features: it includes duplicate and obsolete dispositions. The matrix has review notes for 89 of the 170 currently closed issues and 13 of the 158 open issues; 81 closed and 145 open issues still have no completed review entry (226 total).

## 2026-10-03 live reconciliation after reopening #208

A fresh authored-or-assigned query returns 328 unique issues: 159 open and 169 closed. The issue matrix was corrected for #208, which is OPEN and has no close timestamp. The latest matrix review notes cover 89 of the 169 currently closed issues and 14 of the 159 open issues; 80 closed and 145 open issues remain without a completed review entry.

The prior GitHub closure-event reconciliation recorded 90 distinct issue IDs closed by the GitHub account “mathrmartins” since 2026-09-25, across 100 close transitions. #208 is now reopened, so this is an account-action history count, not the count of issues currently closed or implemented.

For #208, the production MVC sign-in filter lacked the cross-account IP threshold even though its earlier closeout claimed it was active. The issue was reopened with source evidence. The local fix now enforces per-email and cross-account per-IP rolling-hour thresholds under PostgreSQL advisory locks. Focused API unit tests passed 6/6, and the PostgreSQL concurrency integration passed 1/1. The fix was committed and pushed to PR #669 as 4775a56. At 2026-10-03 17:54 UTC, Repository Policy had failed while API, Web, OpenAPI consistency, C# analysis, and Codacy were pending; CodeQL was skipped. Keep #208 open until required checks pass and the PR merges.

## 2026-10-03 open-issue requirements reconciliation

A fresh GitHub query returned **159 open issues**, all belonging to the existing 328-issue scope. The matrix now records the original acceptance checklist, original prose requirements, or explicit title-only scope for every open issue. **147 rows** previously marked `Pending issue review` received their live requirements. This captures requirements; it does not establish code coverage or completion. The open-issue code review counts are **15 with review notes and 144 without a completed implementation review**.

Issue #194 was reviewed against its ten acceptance criteria and current production wiring. `RetentionPolicySimulation` has a standalone linear growth formula, one storage price, and a fixed 90-day recommendation heuristic. The advanced service delegates through an interface with no production repository, registration, persistence mapping, or retention-simulation endpoint found. Historical measurements, tiered multi-year scenarios, configurable compliance obligations, access-based optimization, budget comparisons, and risk assessment remain to be implemented and validated. The issue remains open.

PR #669 now includes a pinned runtime patch for the unreleased braces nesting-depth fix, mandatory checks of installed consumer resolutions, and a strict audit validator preserving the original registry report. The installed security regressions pass **9/9** and validator tests pass **6/6**; local repository policy passes with **63 shell checks** and **14 deployment checks** (three Linux/jq checks are skipped locally). On head `41ee275a1de6b899b22cab78e5d71b766c1a97ae`, GitHub Repository Policy and OpenAPI consistency pass. Codacy still flags the published dependency version despite the verified patch; the merge remains pending while that finding and the other required checks are reconciled. No issue is declared completed from this pending PR.

## 2026-10-03 authentication audit merge and #208 closeout

PR #669 merged to `develop` at **2026-10-03 19:10:51 UTC**, merge `3eb1747a034f0153a3529475229ce50aaf246a0e`, verified head `41ee275a1de6b899b22cab78e5d71b766c1a97ae`. API verify, Web verify, OpenAPI consistency, Repository policy, PR Required Gate, all CodeQL language analyses and the aggregate, and tracked-artifact validation passed. Codacy remains `ACTION_REQUIRED` for the published braces version despite the pinned and regression-tested runtime patch; authenticated finding triage is pending. No scanner pattern or security gate was disabled. The current `develop` has no branch rule requiring that Codacy check.

Issue #208 closed automatically after the merge at **2026-10-03 19:10:53 UTC**. The additional [closeout evidence](https://github.com/gameguild-gg/gameguild/issues/208#issuecomment-5972589587) identifies the active production filter, normalized email/IP thresholds, cross-instance advisory locks, **6/6** focused API tests, and **1/1** PostgreSQL concurrency test. #158 and #159 remain open with their remaining provider/decision-path, correlation, and incident-timeline requirements recorded separately.

The matrix now has **158 open and 170 closed** issues. This is a state count, not a count of implemented features. The independent user-created chat reviews the original 153 closed-issue sample after the 17 explicitly excluded confirmations; new implementation closeouts do not silently change its snapshot scope. PR #670 was updated with the merged `develop` and is awaiting its current-head checks.

The integrated `feature/issue-158-auth-audit-20261003` branch was removed after merge. The older `game-guild-issue-161-action-search` worktree and integrated `feature/issue-audit-refresh-20261003` branch were also removed after verifying that all 171 reported modified tracked files had no semantic difference from either their HEAD or current `origin/develop`. Its raw files and 373 local validation artifacts were preserved as 544 SHA-256-verified entries in `E:\repositories\game-guild\worktree-recovery-20261003\issue-161-recovery.zip` (38,398,388 bytes). Two worktrees remain: the original checkout and the reused implementation checkout. No Git stashes remain.

## 2026-10-03 visual-identity merge and #109 closeout

PR #670 merged to `develop` at **2026-10-03 19:30:14 UTC**, merge `6d8f0f92f9429eafdea8a5595b951160e00132e0`, verified head `a46a44623f4291c366ae7ad3578990180b7e382c`. Web verify, Repository policy, PR Required Gate, tracked-artifact validation, Codacy, and all CodeQL analyses/aggregate passed. API/OpenAPI/migration jobs were classified as not applicable. The [#109 closeout](https://github.com/gameguild-gg/gameguild/issues/109#issuecomment-5972747723) maps the editable logos/alternatives, colors, branding, typography, spacing/grid and usage examples to the repository, and records the logo **1/1** test, lint and SVG XML checks. The guide retains the current name and explicitly avoids claiming replacement naming/domain or marketing approval.

#109 closed at **2026-10-03 19:30:15 UTC**; that snapshot recorded **157 open and 171 closed** issues. Its merged local and remote feature branches were removed. At that point #194 remained an implementation gap, and #158/#159 remained partial despite their merged audit improvements.

## 2026-10-03 retention simulation implementation: #194

The new tenant-admin API, guarded CQRS commands, revisioned configuration, immutable saved runs and PostgreSQL migration implement all ten #194 criteria. Historical counts and logical row sizes come from both primary and signed audit records; actual queried/exported row ages inform access and latency assessments. The deterministic daily-cohort engine supports three growth models, four storage tiers, monthly/yearly costs, retrieval, configured obligations and holds, comparisons, budget variance, constrained candidate optimization and explicit policy-change risks. The generated client is refreshed from the actual API OpenAPI document. [The feature guide](../architecture/audit-retention-simulation.md) maps each criterion, includes API examples and states measurement/model limits. A simulation changes no enforced retention or stored log data. Configured obligations are assessed without assuming a legal minimum or claiming regulatory certification.

The issue remains open until the feature PR's current-head verification and merge. Engine/service and PostgreSQL/HTTP tests are the evidence path; the older manually populated calculator is retained for compatibility. The refreshed 328-row matrix also records the live states observed during the separate closed-issue review. That review reopened 47 issues from the prior 171-closed snapshot; **204 open and 124 closed** describes this subsequent snapshot, not a loss of merged code or a claim that the remaining closed issues have all been verified. Prior evidence is preserved as historical for reopened rows.

## 2026-10-04 CSV/JSON original-route reconciliation: #170–173

New real PostgreSQL HTTP tests reproduced **404** at both original requested routes,
/api/audit/export/csv and /api/audit/export/json. Canonical #171 and #173 were
reopened with that evidence. The prior #653/#656 implementations provide the
existing export and scheduling services, but their closure records did not establish
these original routes.

The correction adds aliases to the same guarded actions, CSV spreadsheet literal
escaping, format negotiation and explicit CSV download metadata. Real HTTP cases
exercise filters, pagination, gzip, actor/tenant isolation, progress ownership,
structured safe errors and a shared user concurrency limit across aliases.
The inherited JSON default also caused the generated CSV methods to send the wrong
Accept header; a failing generated-client HTTP test reproduced that defect.
Explicit status-specific OpenAPI media declarations now take precedence, and the
client is regenerated from the actual API document.

The [export contract guide](../architecture/audit-export-contracts.md) maps the
original CSV/JSON criteria to code and tests, including streaming, disconnects,
failures after the response starts and the limitations of spreadsheet text guards.
The final local C# selections passed **4,136 cases**: audit 437, authentication
1,852, authorization 1,667, API architecture/security/OpenAPI 147 and export
PostgreSQL HTTP 33. The complete solution build passed with zero warnings/errors.
The regenerated client suite passed another **1,112 cases**, including the download
contract that previously failed.
Failed memory-constrained and unconfigured-database attempts remain in the local
artifacts and are excluded from that passing count. All 328 original acceptance
fields and the other 324 matrix rows remain unchanged.

All four issues remain open until the feature PR is verified and merged. #170 and
#172 are title-only duplicates of #171 and #173 respectively, and their closeout
will reference those canonical implementations. The 2026-10-04 live snapshot after
reopening the canonicals is **268 open and 60 closed**. Primary-checkout snapshots
confirm all 55 local files were preserved.

## 2026-10-04 CSV/JSON merge and final closeout: #170-173

[PR #678](https://github.com/gameguild-gg/gameguild/pull/678) merged to develop at **2026-10-04T15:16:33Z**, verified head `0197b882ddd49774dbaa075692b7f685f4b0ed8f`, merge `c9c6e0cb535144408862f544022bc4babc2929f6`. Final-head API/Web/OpenAPI, repository policy, PR Required Gate, tracked artifacts, Codacy (zero findings) and CodeQL checks were accepted. The nine initial Codacy findings in test doubles were corrected; no scanner rule or gate was disabled.

Canonical #171 and #173 closed **COMPLETED** after their original-route gaps were reproduced, fixed and covered by executed acceptance evidence. #170 and #172 closed as native GitHub **DUPLICATE** links to #171 and #173. Each issue retains its original description, source review and comments, with a final public closeout. Local evidence totals **5,248 successful cases** (4,136 C# and 1,112 client), a warning-clean full solution build, client TypeScript and generated-output consistency. The full local solution build belongs to the unchanged production/client implementation commit 22732e81de72fa02deaee1ab061e85fef3d85fc1. Final-head edited test projects built locally without warnings/errors; the final-head Release API build and 2,946 API-related cases passed on GitHub. Subsequent capped local full-build attempts hit compiler memory limits; failed/aborted resource or fixture attempts are excluded from successful evidence.

A fresh GitHub query verifies all **328** original scope IDs: **64 closed / 264 open**. The entire matrix now reflects those live states and timestamps. Other rows only received state metadata and explicit historical reconciliation notes; all 328 acceptance fields and their existing implementation evidence are preserved. A GitHub closed-state count alone does not establish feature implementation. Primary-checkout snapshots verify preservation of all 55 local files.
# 2026-10-06 canonical assessment membership candidate

PR #699's published `d9309fcf621d3dbef6941b232e1c3dad2b362a20` passes native API,
web, OpenAPI/client, policy and scanner checks; native Emception remains in
progress. Native API acceptance includes full integration376/APIunit1079 and
15 repeated OpenAPI HTTP cases. This is not complete PR or issue acceptance.

The actual current SDK/API coding flow exposed a canonical course enrollment
mapping gap. The local correction passes11 actual HTTP/PostgreSQL cases,
all1254 Learning module cases and a zero-warning/error Release solution build.
Complete current-source API regression is still in progress. The SDK now reaches
a separate preexisting Code grading boundary; the native browser cycle remains
unaccepted. [Evidence and required integration](../architecture/coding-assessment-runtime-reconciliation.md).

No additional issue was closed by these checks. The fixed328-ID snapshot remains
72closed/256open, verified `2026-10-06T19:36:07.854893Z`; #263 retains all19
original criteria and is OPEN. Historical sections below keep their dated
results and limits.
