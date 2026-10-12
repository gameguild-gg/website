# Active bearer revocation reconciliation — #262 / #263

## Merged partial acceptance — PR #691

PR #691 merged into develop at `7b736e4f765fda0fa981e554f4b63866fd122e11`
for accepted head `c4ffdc4797c058b0544ad071e7b8764d896f7e86`. All applicable gates,
Codacy and four CodeQL analyses passed. Local 6,493 distinct .NET cases include
full integration 249 and API unit 1,049; focused 11/API selection 115 are subsets.
Actual CI passes 4,826 main cases plus 14 repeated OpenAPI, with clean builds.
Full OpenAPI remains identical (1,297 paths / 1,656 schemas), no model changes.
[Individual evidence](https://github.com/gameguild-gg/gameguild/issues/262#issuecomment-5991037620).

#262 remains OPEN after independent execution found unavailable-account bearer
and refresh acceptance. That correction and concurrency acceptance are recorded
in [lifecycle reconciliation](refresh-token-lifecycle-reconciliation.md). #263
remains OPEN for its remaining criteria. Publication-stage pending statements
below are retained as history and superseded only for this merged increment.

## Preserved acceptance and historical closeout

#262's empty original body and clarified acceptance in the fixed 328-issue matrix
remain unchanged: refresh replay invalidates the family, terminates active sessions,
advances user token version and rejects prior access tokens; one concurrent
rotation may claim a token. The earlier merged #586 and its positive repository
tests remain historical evidence. They did not prove the whole endpoint transaction
or active host's later signed-bearer consumption.

#262 was [officially reopened](https://github.com/gameguild-gg/gameguild/issues/262#issuecomment-5990010929)
on 2026-10-05 after real HTTP/PostgreSQL execution exposed rollback of containment
and source review found the revocation middleware absent from the pipeline. The
original issue body/title and prior comments were preserved. PR #690 corrects
containment persistence and mapped queries; that narrower acceptance is recorded
in [refresh-token reconciliation](refresh-token-rotation-reconciliation.md).

## Executed signed-JWT baseline

Production source `379a7b28b853f79d3a008475d5b9d8bcff08356f` was exercised through
the active host and its registered production bearer handler/events, against actual
migrated PostgreSQL. **Five failures / three passing controls / zero skips** were
recorded across eight cases. Revoked JTI, global user revocation, outdated stored
token version and the prior signed JWT after revoked/replaced refresh replay each
still reached the protected sessions endpoint with HTTP 200 instead of 401.
Replay cases verify committed containment before checking the old JWT.

Fresh current-version tokens, isolation from another user's version change and
anonymous protected-endpoint denial pass as independent controls. The fixture only
selects the production JWT handler as the default; it does not substitute claims,
signature validation, commands, tenant membership or storage. Synthetic accounts,
keys configured by the existing test host and random tokens avoid provider access.
This verifies bearer consumption, not external login/provider delivery.

[Public baseline evidence](https://github.com/gameguild-gg/gameguild/issues/262#issuecomment-5990177858).
Original definitions, build/failure logs, TRX and hashes are retained under
`artifacts/test-results/issue-262-bearer-20261005/`.

## Implementation and bounds

The host now calls the existing `UseTokenRevocation` immediately after
`UseAuthentication`, before tenant membership, actor construction and authorization.
The existing configured JTI/user revocation store and persisted token-version
comparison therefore participate in protected requests. A revoked identity is
cleared. Protected endpoints get generic 401 Problem Details and a Bearer challenge;
the configured Problem Details writer is preferred, with a JSON fallback.

An explicitly `AllowAnonymous` endpoint continues after identity removal. A stale
optional bearer cannot block public sign-in/recovery/health or contribute an
authenticated tenant/actor. Normal authenticated tokens retain the previous flow.
All eleven real PostgreSQL/HTTP scenarios and six new middleware boundary cases
pass. Fresh Authentication 2,151, SharedKernel 1,377, Authorization 1,667 and API
architecture/security/eventing 115 pass with zero failures/skips: **5,321
focused/core cases**, including 17 new definitions. Whole API integration/unit
are in progress at publication; focused subsets will be excluded from full totals.
Full Release solution has zero warnings/errors. Entire exported OpenAPI remains
identical (1,297 paths / 1,656 schemas), and EF reports no pending model changes.
Matching-head gates and merge remain required.

This activates existing checks without changing signing keys, cryptographic
policy, token claims, TTLs, schema or public DTOs. Legacy tokens without a version
claim retain existing compatibility; this acceptance specifically exercises
current production-generated versioned tokens. JTI/user timestamp revocation uses
the configured cache implementation; cross-instance cache-provider acceptance is
not inferred from the test host's local store. Replay's persisted user-version
change is read from PostgreSQL on subsequent requests.

#263 remains OPEN for explicit lineage and selective family isolation, whole-service
issuance atomicity/cancellation/races, actor-guarded revocation, session-specific
bearer enforcement, lifecycle events/alerts, retention/cleanup/metrics and complete
documentation. All 19 original criteria remain intact. #262 stays OPEN until its
bounded acceptance, matching-head gates and merge are verified.
