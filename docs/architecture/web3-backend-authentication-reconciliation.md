## Emception source integrity and release peer repair - 2026-10-06 UTC

The genuine Emception [CI run 37406138023](https://github.com/gameguild-gg/gameguild/actions/runs/37406138023)
for `cdc65d14be3550dab75e9b42421aebfb16b745b4` failed before extraction because
LLVM's downloaded archive SHA-256 differed from the lock. The failed job log is
retained. Two independently downloaded archives reproduce the old lock checksum
(`0791c693...`) and the CI checksum (`0d7fb3e4...`). Comparing all **175,014 files**
found only a Git-generated describe abbreviation difference in
`clang/bindings/python/.git_archival.txt`; all other files are identical.

The original upstream blobs independently verify the declared export substitution
and six CRLF test fixture transformations. Reconstructing the complete Git tree
then matches `34cd378b6fba47d5bd42003cdaf9505179879004` for the unchanged LLVM commit
`7b58716d96c3ae4c0c4e6f72e29b16137bb6224b`. The reviewed checksum is pinned to the
actual CI archive. LLVM remains `23.0.0git` and owned by the same EMSDK release.
Runtime checksum enforcement, extraction safeguards and the CI release gates are
unchanged. The initial direct Git-tree comparison failure is retained separately
from the successful comparison that accounts for upstream export transformations.

Full local script testing also exposed three stale internal Xterm peer ranges:
React, Webcomponent and IDE were version 4.4.0 but still advertised `^4.3.0`.
Their minimum internal peer is now `^4.4.0`. Version preparation synchronizes
internal peers after Changesets and before lock generation; external peers and
optional peer metadata are preserved. The pnpm lock changes only those three
peer specifiers. The initial **56/57** script result is retained; the final
**59/59** script cases pass with no skips, including four new integrity and
version synchronization regressions. Full local package testing passes **346 of
347 cases**, including those 59 script cases, with one existing opt-in real-worker
smoke test skipped. All six package type checks and the Core public type tests
pass. The installed dependency patches and strict audit validator pass a fresh
**26/26** cases. The complete Linux Emception release
execution remains required before merge; these local results do not close an issue.

## Dependency security and public navigation verification - 2026-10-06 UTC

CI run [37402770647](https://github.com/gameguild-gg/gameguild/actions/runs/37402770647)
for `2a91869eb9f0cea14bbdd6d1723db383b358f4b7` passed API, Web and OpenAPI.
Repository Policy failed on dependency advisories; the full Economy profile passed
its .NET, SDK, Web, coverage, generation and build stages but failed the public
browser smoke test. Both failures and the uploaded artifacts are retained.

The dependency repair resolves actual installed consumers to the following versions:

| Dependency | Resolved version | Reviewed advisory |
| --- | --- | --- |
| KaTeX | 0.18.7 | [GHSA-238p-pmpm-9mq7](https://github.com/advisories/GHSA-238p-pmpm-9mq7) |
| source-map-js | 1.2.2 | [GHSA-68fv-2mgg-jv7q](https://github.com/advisories/GHSA-68fv-2mgg-jv7q) |
| proxy-addr | 2.0.8 | [GHSA-jqcg-44mw-7w3h](https://github.com/advisories/GHSA-jqcg-44mw-7w3h) |
| postcss-selector-parser | 7.1.6 | [GHSA-rj75-hqrm-r3gf](https://github.com/advisories/GHSA-rj75-hqrm-r3gf) |
| snowflake-sdk | 3.3.0 | [GHSA-qqj6-54q6-cxv6](https://github.com/advisories/GHSA-qqj6-54q6-cxv6) |
| sprintf-js | 1.1.3 with repository patch | [GHSA-hp3w-g68c-fv3c](https://github.com/advisories/GHSA-hp3w-g68c-fv3c) |

The sprintf advisory has no upstream fixed release. Its source and browser
distribution now bound numeric precision to the ECMAScript ranges; the distribution
and source map are regenerated with the existing Terser dependency and preserve
the license. All actual consumers use the patched version, including legacy
argparse. The baseline produces uncaught RangeError failures in real asynchronous
child processes (9 failures in 15 cases). The final source, distribution, compatibility
and audit contract checks pass all **26 cases** without skips.

Fresh audit still reports exactly two vendor advisories, for braces and sprintf;
both installed patches are verified by executable security regressions before the
validator accepts their exact package, advisory, CVE and version. Unknown advisories,
incorrect versions, malformed reports and inconsistent exit codes remain failures.
This is verified mitigation, not a claim that the vendor audit contains zero alerts.
The full local Repository Policy passes; three existing Windows-only deployment
contract skips are retained and require applicable Linux CI checks.

With the new dependency graph, fresh full SDK **1121/1121** and Web **2959/2959**
executions pass with no failures or skips. The separately recorded upstream smoke
checks exercise inherited KaTeX trust rejection, a 400 KB flat selector, valid and
oversized indexed source-map offsets, and IPv4 proxy spoofing denial. Snowflake
module initialization and DBML CLI initialization pass; no external Snowflake
connection or external-provider acceptance is claimed. Failed source-map fixture
attempts remain retained: the corrected test expects the upstream offset rejection
and supplies the source content required for an indexed source map.

The public smoke test previously searched for a direct desktop Courses link.
The current UI places Courses inside the Learn dropdown. The test now waits for
hydration, opens Learn and clicks the Courses menu item, retaining URL, heading,
mobile-navigation, route, request and console assertions. The product UI is unchanged.
Its syntax check passes; complete production browser acceptance remains pending
for the next matching-head CI execution.

These receipts retain their actual `2a91869` execution origin and the uncommitted
dependency changes. They do not relabel earlier .NET execution commits. All five
native #292 criteria remain intact. #292 stays OPEN until complete matching-head
CI and accepted develop merge; #291 and #263 retain their remaining original scope.
All 55 unrelated primary files are preserved. No new worktree, branch or stash is
created for this repair.

The preceding verification history follows.

# Web3 backend challenge and authentication — #292

## TLS and timing verification — 2026-10-06 UTC

The complete API integration suite was freshly executed at
`0bad48b86d4c89cba44a7705f717e0f47f0da7d7`: **327 passed, zero failed or skipped**,
in 14 minutes 55 seconds, using the full migrated PostgreSQL template. Resources
also passed all 63 cases at that publication. The template sentinel and 146
migrations remained intact and disposable container cleanup succeeded. Receipts
retain their actual execution commits, timestamps and source hashes.

CI run [37397324819](https://github.com/gameguild-gg/gameguild/actions/runs/37397324819)
exposed a certificate fixture race in API verification and Economy: Resources
passed 62 of 63 cases because the Kestrel/Redis leaf certificate ended one second
after the issuer's encoded expiration. Separate wall-clock reads and certificate
timestamp precision made their validity intervals inconsistent. This failed CI
is retained. Its skipped TestingLab journey does not establish acceptance.

The leaf now uses its issuer's actual UTC validity bounds. A deterministic
five-minute authority reproduces the old failure and verifies the repaired leaf's
validity, private key and trusted server-authentication chain. All **17 Redis/TLS
cases pass**, including real Kestrel containers and TLS handshakes.

The first complete 64-case local run passed 63 and failed the tenant timing
comparison by 135.4 ms against its existing 100 ms bound. That failed receipt is
preserved. A separate diagnostic whole run passed all 64 cases, recording HTTP403
for all 32 requests. Grouped means differed by 20.4 ms and balanced means by
1.10078 ms. These later samples do not uniquely establish the earlier failure's
cause, because the original receipt lacked individual timings and status codes.

The timing test now measures ten adjacent pairs with each group first in five
pairs and second in five. It disposes every response, records individual timings,
and requires HTTP403 for warmups and every measured sample. The **100 ms bound
is unchanged**. Its complete final Resources execution passes **64 of 64 cases,
zero failed or skipped**, with means 152.215/149.165 ms and difference
3.050 ms. The new certificate regression is counted once.
The final Resources build has zero warnings and errors. Failed and diagnostic
receipts remain separate from this final source-bound execution.

Only two Resources test files and this document change from the successful local
API publication. Authentication production, API integration, fixture support,
SDK and Web sources remain identical. Their earlier execution origin stays
explicit. All applicable matching-head CI checks and accepted develop merge
remain required before closing #292. #291 and #263 keep their remaining scope.

## Release fixture verification — 2026-10-05

The complete CI run for `b441ee88facf901f3892fa3c2c2a148ac5434a26`
failed the Resources integration project: 45 cases failed during gate schema reset
with PostgreSQL SQLSTATE 53200, and one distributed rate-limit case could not find
its Release probe host. CI built that project in Debug because it was absent from
the solution configuration graph. Those failed receipts remain retained.

Gate reset now drops and recreates only the fixture's generated database, retaining
the gate template and its administrative connection. The reset refuses the
administrative database or a name outside the generated fixture prefix. The real
Resources test host also restores the scoped abstract `DbContext` registration
removed by its database replacement; authorization repositories resolve the same
`ApplicationDbContext`. The existing rate-limit probe project is registered in the
solution with all twelve Debug/Release platform mappings.

The complete Resources suite passes **63 cases**, with no failed or skipped cases.
Two added regressions exercise reset with 5,000 independently created tables and
actual host resolution of the scoped context and masking repository. The disposable
PostgreSQL environment uses the full 146-migration template, 2,177 relations and
default lock capacity. Its sentinel and migration history remain intact; the
owned container is removed after execution. Support, probe, Resources and auxiliary
bootstrap builds have zero warnings and errors. The first local reset repair passed
54 of 62 cases and exposed seven HTTP 500 failures from the removed context alias
and one timing failure on those erroneous responses; that receipt is retained too.

Current publication acceptance still requires all applicable checks for its exact
commit, including the complete Economy profile and TestingLab browser journey,
followed by the accepted develop merge. #292 remains OPEN; #291 and #263 retain
their complete remaining scope. These test infrastructure repairs do not change
production authentication, authorization policy, PostgreSQL lock limits or API
contracts.

## Retained scope

#292 is the native child of #291 for backend challenge generation, nonce handling,
signature verification and token issuance. Its five approved criteria remain
authoritative. This implementation also repairs the inherited account/session and
plaintext-token defects that prevented a verified signature from establishing a
usable authenticated session.

| Approved criterion | Implementation and focused evidence |
| --- | --- |
| Generate and store a nonce bound to the wallet and its authentication challenge. | `Web3Service` stores random nonces with canonical wallet, configured origin, message and chain bindings in the actual size-bounded host cache. It rejects generation when either binding was not retained. Bounded-cache storage and capacity tests; actual HTTP challenge requests. |
| Validate the wallet address, signature, chain ID and challenge format. | Shape and mixed-case EIP-55 checksum validation; configured chain allowlist; canonical SIWE message/field checks; actual EIP-191 recovered signer. Unit vectors and actual HTTP invalid input, wrong-key, signature/message/nonce/address/network cases. |
| Expire the challenge after five minutes and reject replay. | Trusted `TimeProvider` supplies generation and verification time. Exact five-minute expiry is rejected. Atomic nonce consumption precedes account or credential writes. Actual HTTP expiry, concurrent verification and subsequent replay tests. |
| Issue the authentication token only after successful verification. | The verified provider key resolves a stored account. Status and tenant membership precede the existing hashed-token generator, actual session binding and versioned JWT. Actual bearer/session access, refresh and second-sign-in continuity tests. |
| Cover invalid signatures, expired/reused nonces, and address/network mismatches. | Actual HTTP/PostgreSQL vectors, real cryptographic signatures and unit cases. Denials cannot create wallet identity, session or token rows. |

Execution receipts and publication acceptance are recorded below when verified.
Test definitions alone do not complete a criterion.

## Concurrent develop integration — 2026-10-05

The reviewed Web3 branch incorporates develop's security revision
`45941e7da3f0995b1011ea92127ced0de895b527`. Its log-redaction implementation and
frontend changes are preserved. Six manually edited generated client modules
revealed drift from the unchanged generator. The module generator now emits the
same shorthand syntax for simple array response assertions consistently; public
method types, HTTP contracts and runtime behavior remain unchanged. A generator
regression test and regenerated modules make that formatting reproducible.
Matching-head integration checks and accepted merge are required before closure.

The same develop revision introduced invalid accesses on `unknown`, an invalid
rest parameter and an unparenthesized mix of `??` and `||` in Web dependencies.
The integration repair uses existing Monaco, Lexical and Vega types, a typed
JavaScript compiler export boundary and valid operator grouping. Existing editor
controls and rendering behavior are retained. Web runtime dependencies and the
complete Web typecheck pass locally. Obsolete ESLint suppression counts are only
removed as the corresponding violations disappear; no new suppression or disabled
rule is introduced. The complete integrated .NET/SDK receipts pass 7,810 distinct
cases: Authentication2256/Authorization1667/SharedKernel1389/APIunit1050/
Integration327/SDK1121, including35 new Web3 cases and2 generator regression cases.

The full Economy profile exposed an existing five-minute outer deadline for the
entire API integration suite. Its retained timing receipt records termination at
300seconds with status124, while the same327-case suite passes its independent
API check in8min24s. Both API test projects now use the existing bounded12-minute
API suite deadline; the five-minute individual-test hang guard and all assertions
remain active. A shell regression exercises integration, unit and ordinary project
deadline selection. No test project, coverage requirement or release stage is skipped.

The Testing Lab browser fixture also generated a 51-character reviewer username,
which the existing 50-character API handle limit correctly rejected. Its shorter
fixture prefix preserves the complete unique tag and role, with a guard before
sign-up. Production validation is unchanged. All 64 Economy shell regressions and
23 Testing Lab runner/quality cases pass locally, including five new username cases.

With the deadline fixed, Economy passes the complete 327-case API integration
project in 9min06s and its 1,050 API unit cases. Its next failure exposed a stale
legacy direct-service test that expected refresh replay to throw. The accepted
containment contract deliberately returns an internal denial so the command
transaction commits revocation; the HTTP boundary returns 401. The test now
requires that commit outcome, no returned credentials or identity, all two stored
tokens revoked, both stored sessions inactive and exactly one user-version advance.
It verifies containment of a second active login as well as the revoked input.
The production denial and all actual HTTP assertions are unchanged.
All 42 legacy authentication integration cases pass locally with these stronger
assertions. Combined with the independently retained suites above, this gives
7,852 distinct .NET/SDK cases. These are existing integration cases, not 42 new
Web3 tests. The original 35 new Web3 cases and two generator cases remain separate.

The Testing Lab runner now stores host logs and its exit receipt under the CI
artifact upload directory, including failures during identity bootstrap before
the first browser page. This preserves the cause of fixture sign-in failures.
Matching-head acceptance remains pending until every applicable gate passes.

An exact Node reproduction confirms that concurrent fixture sign-ins contend on
the configured PostgreSQL source-IP advisory lock and correctly receive generic
401 before password verification. Fixture identities now complete in sequence,
with regression coverage for ordering, no overlap and failure propagation. The
API lockout, IP throttling and credential checks are unchanged.
All 25 runner/quality cases pass locally. The exact Node identity bootstrap also
passes against the real API and a freshly migrated disposable PostgreSQL database:
all three fixture identities receive valid sign-in responses. This accepts fixture
preparation only; the complete browser journey still requires matching-head CI.

The next full Economy execution reached the whole-solution tests and exposed two
obsolete Learning/LTI expectations after the existing numeric value migration.
Both failures reproduce locally: completed progress is `PercentValue.Hundred`,
and LTI scores convert integer centesimal units to human point JSON numbers.
The tests now use the typed completion value and independently check whole,
fractional and one-cent point values, retaining the exact five-field AGS payload.
Learning's full 148-case suite and LTI's full 47-case suite pass, with two added
fractional-score cases and no production change. Including these distinct suites,
the combined local .NET/SDK receipts contain 8,047 passing cases. Failed baseline
and CI receipts are retained; publication acceptance still requires the complete
current-head Economy profile and browser journey.

An additional 22 unchanged unit suites pass locally (3,561 distinct cases,
without counting SharedKernel's existing receipt again). Their execution exposed
an inherited solution mapping defect: Announcements' test project mapped both
Debug and Release to the configuration `Any CPU`, producing its DLL under
`bin/Any CPU` instead of the gate's `bin/Release` path. The twelve mapping entries
now retain the requested configuration and use the existing Any CPU platform.
The actual solution target demonstrates the corrected Release output, followed
by all seven tests passing with `--no-build`, as used by the complete CI gate.
Neither production code nor the test assertions were changed for this repair.

## Account and credential boundaries

The insert-only `web3` external-login provider key is the lowercase verified
address. Subsequent sign-ins resolve the same stored user. A missing linked user,
inactive/suspended/deleted account or unavailable requested tenant receives generic
401 denial. A matching `address@web3.local` identifier never automatically links
an existing email account; wallet possession does not verify an email address.
New wallet accounts have an unverified internal identifier and no password.
Explicit linking and multiple-wallet product flows remain in #291.

The existing JWT generator persists one hashed refresh credential. Web3 no longer
writes an additional plaintext copy. Session creation uses the actual session GUID
and the same token hash; JWT claims carry that session, the stored user version and
resolved tenant/roles. Optional tenant and device-fingerprint inputs reach the
service through the existing command handler. The public request/response routes
and contracts remain compatible.

The actual host command transaction encompasses account, provider link, membership,
token and session writes. A required binding failure cannot return credentials or
record success. A test injects the fault only after a real stored token/session
binding, then verifies rollback of identity and credential rows. The consumed nonce
remains consumed after rollback; a fresh challenge is required to retry.
Cancellation propagates before persistence and through required dependencies.

Expected verification/account denials use the existing sanitized security exception
and HTTP 401 mapping. Known invalid challenge address/chain inputs use the existing
validation exception and HTTP 400 mapping. Real storage failures retain HTTP 500.

## Configuration and deployment contract

`Authentication:Web3:Siwe:Origin` is mandatory. It accepts an HTTPS origin, or an
HTTP loopback origin for local development. Configured allowed chain IDs govern
generation and verification; the existing default is Ethereum chain 1. The statement
is host configured. SIWE fields and byte-for-byte canonical messages are checked
before signer recovery and nonce consumption.

Nonce storage and atomic consumption are process local. Requests that cannot find
the wallet/nonce binding fail closed, including eviction and restart. A deployment
with multiple API processes needs routing affinity for the challenge/verification
pair, or a separately accepted shared nonce store. These tests do not establish
distributed-store, replica-routing or live wallet-provider acceptance. No private
wallet key or raw authentication token is persisted in test receipts.

The implemented signer boundary is EOA EIP-191 with SIWE formatting. Contract-wallet
verification, EIP-712, frontend/provider adapters, explicit linking, multiple wallets,
disconnection, per-address limits, metrics and complete product journeys remain
under #291 and its provider/UI children. #263 retains its remaining original
all-provider lifecycle/audit/retention criteria.

## Executed verification

The initial isolated source passed **7,796 distinct .NET/SDK cases**, including
**35 new cases**, before the subsequent develop integration and release repairs
recorded above:

| Suite | Passing cases |
| --- | ---: |
| Authentication unit | 2,256 |
| Authorization unit | 1,667 |
| SharedKernel unit | 1,377 |
| API unit, architecture and security | 1,050 |
| Entire API integration suite | 327 |
| SDK | 1,119 |

Integration includes **20 actual Web3 HTTP/PostgreSQL cases** and **15 full-application
OpenAPI HTTP cases**. Neither is counted again in the total. The full solution build
has zero warnings/errors with analyzers enabled. EF reports no pending model change.
The entire exported OpenAPI document equals accepted #695, with 1,297 paths and
1,656 schemas. Fresh SDK typecheck, generation and committed-client consistency pass.

The actual pre-fix HTTP baseline failed challenge generation with 500; unchanged
registered-service diagnostics identified missing cache sizes, plaintext duplicate
refresh storage and absent account/session persistence. Failed intermediate receipts
and the passing preliminary 14-case subset are retained and excluded from final
counts. Logs, TRX/source hashes and `local-proof.json` remain under
`artifacts/test-results/issue-292-web3-20261005/`.

Revision `af8595b63e5cbd4229de622909036c4b2df02349` passed matching-head
[PR Verify 37442150591](https://github.com/gameguild-gg/gameguild/actions/runs/37442150591),
including 327 API integration cases (20 Web3 HTTP/PostgreSQL cases included),
2,256 authentication unit cases, API/Web/OpenAPI, Economy, Testing Lab and policy
gates. [PR #697](https://github.com/gameguild-gg/gameguild/pull/697) was merged into
develop as `295ee212828ab1cc233ce7ed61f2a2b81eb39ed4` on 2026-10-06. #292's five
backend criteria were accepted and the issue closed. #291/#263 and provider/UI
children retain their remaining criteria.

The Emception native job remains failed. Its assessment enrollment rejection was
reproduced using freshly compiled develop production API source at `45941e7`;
the owner explicitly [accepted this preexisting failure for the merge](https://github.com/gameguild-gg/gameguild/pull/697#issuecomment-6016849454).
Later native browser/consumer/package/deployment steps remain unverified. No
failing assertion or CI gate was disabled, and no native success is claimed.

## Standards

The message and checksum boundaries follow [EIP-4361](https://eips.ethereum.org/EIPS/eip-4361)
and [EIP-55](https://eips.ethereum.org/EIPS/eip-55). The existing Nethereum signer and
SIWE libraries remain in use; no new signing implementation or dependency is added.
