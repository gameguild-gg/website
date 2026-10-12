# JWT generation and validation acceptance: #237

## Retained requirement and provenance

[#237](https://github.com/gameguild-gg/gameguild/issues/237) originally had an
empty description and the title "JWT Generation & Validation". Its maintainer
record identifies signed generation, claims, signature, HS256 policy, issuer,
audience, lifetime and rejection of invalid tokens. The 2026-10-04 source review
reopened it because the asynchronous and active bearer validators did not enforce
the HS256 restriction present in synchronous validation. Native duplicate #238
preserves its historical discussion. #237 remains a native child of #284.

The public `GenerateAccessToken(..., IEnumerable<Claim> additionalClaims)` contract
also discarded its argument. Preserving legitimate custom claims and preventing
them from changing server-owned security assertions are additional engineering
checks grounded in that public contract and the repository's tenant/actor invariants.
They do not replace the original generation/validation requirement.

## Reproduced defects

On the unchanged `develop` base `99f24d42e6adc2110eb2efe33648847006fd12af`, a
synthetic 64-byte signing key allowed HS384 and HS512 to pass asynchronous
validation while the synchronous validator rejected both. Actual bearer requests
also returned 200 for those algorithms rather than 401. Custom claims disappeared
from tokens produced by the public additional-claims overload.

The first 55 service cases produced **44 failures / 11 passes**. The 13 HTTP
cases produced **3 failures / 10 passes**, specifically HS384, HS512 and dropped
custom claims. Those failures are retained under
`artifacts/test-results/issue-237-jwt-20261004/gap-auth` and `gap-http` in the
reused isolated checkout. They are regression evidence, not successful acceptance.

## Implementation and compatibility

`JwtTokenService.CreateValidationParameters` and the API's active
`SetupAuthentication` bearer registration now set `ValidAlgorithms` to HS256.
Issuance already signs HS256; accepted algorithms no longer depend on whether
the caller chose asynchronous, synchronous, expired-principal or HTTP validation.
This applies the application's recorded algorithm policy and the supported-set
verification described by [RFC 8725 section 3.1](https://www.rfc-editor.org/rfc/rfc8725.html#section-3.1).
It does not establish compliance with every requirement of that RFC.

The additional-claims overload forwards the supplied collection into the existing
generation path. Legitimate custom claims retain values, repeated entries and
JSON string/boolean/numeric semantics. JWT JSON does not encode a .NET integer
width: the decoder infers Integer32 for small numbers and Integer64 for larger
values. The numeric test therefore uses `2147483648` to verify the latter.

Null collections, null entries, blank claim types and reserved types throw
`ArgumentException` (or `ArgumentNullException` for the collection). Reserved
types include protocol/validity, subject/email, roles/groups/permissions,
tenant/session/token version, MFA and actor/grant/scope assertions. Existing
extractor aliases such as `TenantId`, `UserId`, `actor_type` and MFA timestamps,
plus configured authorization claim names, are reserved case-insensitively.
Use existing typed issuance arguments and dedicated issuers for security data.
No current production caller of the additional-claims overload was found in the
source pass; the ordinary overload used by `Web3AuthService` is preserved.

Public signatures, DTOs and HTTP routes are unchanged. The deliberate behavior
changes are rejection of previously accepted HS384/HS512 tokens and validation
of formerly ignored additional claims. Tokens issued by the existing HS256
generators continue to work. Deployments with independently issued non-HS256
tokens must align those issuers with this recorded contract before rollout.

`GetPrincipalFromExpiredToken` bypasses **lifetime validation**, including expiry
and not-before, while retaining algorithm, signature, issuer and audience checks.
It is a recovery helper, not an authenticated HTTP entry point.

## Requirement mapping

| Requirement / provenance | Implementation | Executed verification |
| --- | --- | --- |
| Signed JWT generation and configured claims/expiry; original title and maintainer record | Existing generator, configured signing key/issuer/audience/expiry, canonical identity/role/tenant/session/auth-time/version claims | Generated-token assertions and complete existing JWT unit suite; service-generated token through actual bearer HTTP registration |
| Signature and recorded HS256 acceptance across validators; reopened source finding | Explicit allowlist in shared service parameters and active API bearer parameters | HS256 accepted; HS384/HS512/unsigned rejected by every service path and by HTTP; wrong key and tampered payload rejected |
| Issuer, audience and lifetime checks; original maintainer record | Existing validation settings and clock skew retained | Wrong issuer/audience, expired and future tokens rejected by normal service validation and HTTP; expired-principal helper retains cryptographic checks |
| Preserve the public additional-claims argument; contract defect | Validated cloned custom claims appended to existing issuance | String/boolean/large-number/repeated claims, canonical identity and role retained; custom claim reaches HTTP principal |
| Tenant/actor and authorization assertions remain server-owned; repository invariants | Reserved standard/legacy/configured claims, case-insensitive rejection | Default and configured reserved names, tenant/actor/MFA aliases, null/blank/empty inputs |

## Verification boundaries and pending release evidence

The service fixture uses the real token service and cryptographic library, with
unused refresh persistence represented by mocks. The HTTP fixture uses TestServer,
the actual API `SetupAuthentication`, `UseAuthentication` and `UseAuthorization`,
and a protected route. It exercises real bearer validation and 401 challenges.
It does not load the entire application's tenant/account middleware or call
external providers. Complete-application regression tests are recorded separately.

The focused service suite passed **65/65** after the patch. An initial post-patch
run had one incorrect integer-width expectation for the value 42; correcting the
independent JSON oracle to a value beyond Int32 fixed it without changing production
behavior. An initial Release API-test build used a stale host reference and failed
on two existing authorization types; it is excluded from acceptance. A fresh
solution Release build and the final HTTP/full suites are required for closure.

The fresh solution build in Release configuration completed with zero warnings
and errors. The resolved-configuration HTTP test project was subsequently rebuilt
warning-clean. Complete Authentication **2,004**, Authorization **1,667** and
SharedKernel **1,371** unit cases passed. The **13** specific bearer HTTP cases
and **14** complete-application OpenAPI HTTP cases passed. These **5,069 distinct
cases** do not double-count the 65 service regressions included in Authentication.
The complete API unit suite remains running at initial PR publication.

The bearer generation test receives `IOptions<JwtOptions>` resolved by the actual
API registration, verifies HS256/issuer/audience and the typed 17-minute expiry
overriding a conflicting 999-minute configuration value, and submits the resulting
token to the protected route. The full `v1` document remains identical to the
accepted baseline: **1,296 paths / 1,654 schemas**, canonical SHA-256
`45b42923086a1f05d6a0288c161039965f8b06d92c2fc661232c78be9972d454`.
Forced client regeneration, generated diff and TypeScript checking passed. Only
the generated timestamp changed and was restored; no generated contract changes
are included.

The initial OPEN/pending status above is historical; accepted CI, merge and
public closeout are recorded below. #284/#41 retain their
refresh, session, account-state and revocation scopes. This change does not close
those requirements or certify a configured production deployment.


## Final CI, merge and official closeout

[PR #683](https://github.com/gameguild-gg/gameguild/pull/683) merged at
`a3952444077f4f1482897d2570bd3b445786faf8` on `2026-10-04T22:17:05Z`. Exact head
`a7764719e52490f2a93052f3c9c20940b068039c` passed **4,437 CI cases**: 1,048 API, 2,004 Authentication,
1,371 SharedKernel and 14 complete-application HTTP OpenAPI cases.
Fresh Release API/integration builds had zero warnings/errors. OpenAPI/client,
Codacy, four CodeQL analyses and all applicable checks passed.
[CI evidence](https://github.com/gameguild-gg/gameguild/actions/runs/37238091161/job/111541188332).

The previously running local API suite also passed **1,048/1,048**, bringing
distinct successful local cases to **6,104**. The 65 new service and 13 bearer
cases are included in the complete Authentication/API suites and are not added
twice. No failed or interrupted attempt is counted as acceptance.

#237 was officially CLOSED/COMPLETED at `2026-10-04T22:18:33Z` with
[complete requirement evidence](https://github.com/gameguild-gg/gameguild/issues/237#issuecomment-5985063129). Parent #284/#41 and duplicate
#238 retain their native relations and independent scope. All 328 requirement
fields and all 55 primary local files were preserved. The merged feature branch
is removed and the existing isolated checkout reused.
