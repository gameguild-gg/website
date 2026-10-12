# Authentication DTO transformation reconciliation — issue #218

## Requirement provenance and scope

The original [issue #218](https://github.com/gameguild-gg/gameguild/issues/218)
contains only the title `AuthenticationConverter - DTO Transformations`; its body
is empty. The criteria below are explicit engineering interpretations of that
title and the executed defects, not a reconstructed historical checklist or
claim of prior product approval. The original title/body and all 328 scoped IDs
remain preserved in the audit matrix.

The public [reproduction](https://github.com/gameguild-gg/gameguild/issues/218#issuecomment-5985551148)
ran actual mapping methods with synthetic server data and a read-only repository
proxy. It observed truncated surnames, missing phone, and a legacy refresh
converter discarding expiry, duration and profile. It did not exercise HTTP,
persistence or an external provider.

## Criteria and implementation map

| Criterion | Implementation | Evidence |
| --- | --- | --- |
| Preserve server token, expiry, session, tenant and challenge/risk fields | Both public `AuthenticationMappings.ToDto` overloads | All-field, fixed-clock, past/future expiry and seven-handler contract tests |
| Preserve complete repository profile names | Split on whitespace; first component is `FirstName`, all remaining components form `LastName`; no mutation of `User.Name` | Eight name cases, repository projection assertions, actual signup/login/refresh HTTP |
| Return stored phone only to completed authentication | Requires success, a nonempty access token, no MFA requirement and no step-up requirement | Six phone-boundary cases, successful login/refresh and high-risk challenge HTTP |
| Never infer phone verification | Repository does not expose a verified-phone assertion; sign-in projection remains `PhoneNumberVerified=false` | Profile, challenge and HTTP assertions |
| Preserve explicit refresh expiry even if expired | Derive only a missing/default expiry, and only from a positive duration | Fixed-clock expiry/duration cases; the former contradictory test now supplies a genuinely missing expiry |
| Copy every legacy refresh profile field | Project the supplied server-owned `UserDto` to a separate object; keep duration and tokens | All-field copy and source-mutation isolation test |
| Preserve authoritative identity and failure behavior | Main mapper reads by server `UserId`; ignores embedded profile data; propagates repository errors/cancellation | Strict read-only repository expectations, unavailable-profile fallback and cancellation tests |
| Avoid mutable-container aliasing | Separate tenant collection, risk/method lists and legacy refresh profile | Mutation-isolation tests |
| Preserve API compatibility | No DTO properties, route, column, JWT or authorization policy changes; original three-argument method remains, plus a two-argument convenience overload | OpenAPI/client comparison and API architecture/security regression |

## Current call paths

The seven live handlers using the asynchronous main mapper are
`LocalSignUpHandler`, `LocalSignInHandler`, `RefreshTokenHandler`,
`PolymorphicSignInHandler`, `SocialSignInHandler`, `GoogleIdTokenSignInHandler`
and `DiscordCallbackCommandHandler`. Contract tests call each real handler,
controlling only the authentication service/validator boundary and repository.
They do not certify any OAuth provider or external token verifier.

`AuthController` signup, signin and refresh endpoints reach their handlers through
the real CQRS pipeline. In particular, **the current HTTP refresh endpoint uses
the main `SignInResponse` mapper**, not the public legacy
`RefreshTokenResponse.ToDto` overload. The latter still needs correctness because
it remains a public module API, but it is not claimed as a live HTTP path.

The main mapper retains its historical missing-repository fallback: response
user ID/email, email as username, current creation timestamp, absent names/phone
and unverified flags. It does not trust the response's embedded profile or query
another identity. This work does not certify account existence from that fallback.

The signup service/controller's separate handling of optional first/last/phone
request fields is not certified by these response-conversion tests. HTTP signup
uses the existing username/display-name behavior; stored phone is added through
the real entity/context before the login/refresh assertions.

## Validation and acceptance status

Before changing production code, **18 of the 30 new mapper contract cases failed**
against `develop`; 12 passed. The failure log and TRX retain each defect.
After the first correction, all 30 new mapper cases and four existing neighboring
model cases pass. Seven live-handler contract cases and five full-application
PostgreSQL HTTP cases are added for broader verification.

The first HTTP selection had 14 passes and one failure: its success account had
no active tenant membership because startup seeds are disabled. Production
correctly rejected login with 403 before conversion. Test arrangement now seeds a
real active tenant/member; the guard is unchanged, with an additional negative
case asserting that an unjoined requested tenant remains forbidden. That initial
arrangement failure is retained and is not included in accepted results.

The HTTP fixture uses real CQRS, validators, authentication, password hashing,
JWT issuance, sessions, migrations and PostgreSQL. Its login cases control only
risk classification to reproducibly exercise low/high-risk responses. They use
anonymous requests without test authorization headers. The fixture's alternate
authentication scheme is not evidence of normal bearer-token validation; that
remains covered by the API bearer/security regression. No external provider,
mail delivery or production environment acceptance is inferred.

Evidence is retained under
`artifacts/test-results/issue-218-authentication-dto-20261004` in the reused
isolated checkout. Full regressions, warning-clean builds, OpenAPI/client
consistency, exact-head CI, merge and public acceptance are pending. **#218
remains open until those checks and official closeout are recorded.**


## Accepted local verification

**3,864 distinct cases** passed: Authentication 2,060; Authorization 1,667;
API architecture/security/bearer 121; full-application HTTP and OpenAPI 16
(five added HTTP cases and 11 existing OpenAPI cases). **42 new cases** comprise
30 mapper contracts, seven current-handler contracts and five HTTP cases.
Focused repeats and initial failed attempts are excluded from the totals.

The configured full solution built in Release with zero warnings/errors. Fresh
targeted Release builds passed; Authentication DLL SHA-256 is identical in the
source, API, both API test assemblies, Authentication tests and metadata tool.
Complete v1 is unchanged at 1,296 paths/1,654 schemas, canonical SHA-256
`165f69d940e9a2cf9b4558e9671c9df5dc08fcaadf21de007f739ab7a41f89ee`.
Force generation, semantic generated-client diff and typecheck pass. Only
generator timestamp/author provenance refreshed; the original metadata bytes
were restored after confirming semantic equality. Exact-head CI and merge remain
pending; #218 is open.


## Exact-head CI, merge and official closeout

[PR #685](https://github.com/gameguild-gg/gameguild/pull/685) merged into `develop` at
`21fa6f31e3d943d98190e6f7178204485086f775` on `2026-10-05T00:55:41Z`. Head `32ba8340a85f7cdf79abc53b73e2dc7ccfddb17b`
passed **2,202 distinct CI cases**: API integration 142 and Authentication 2,060.
The selector chose those two affected projects; the separately executed 121-case
API architecture/security/bearer and 1,667-case Authorization local suites are
not claimed as additional CI cases. The 14-case OpenAPI HTTP gate repeats
integration cases: 2,216 executions without duplicate counting.
[Run/log](https://github.com/gameguild-gg/gameguild/actions/runs/37248150956/job/111570125387).

Fresh Release builds and all applicable gates, OpenAPI/client, Codacy and four
CodeQL analyses passed. Web/migration/other nonapplicable jobs were skipped and
are not claimed as executed. The final configured solution build also completed
with exit code zero and zero warnings/errors. Initial failed/interrupted attempts
remain recorded and excluded from acceptance; no missing native handle was
treated as successful completion. Complete OpenAPI/client structure is unchanged.

#218 is officially CLOSED/COMPLETED at `2026-10-05T00:55:48Z` with
[full acceptance](https://github.com/gameguild-gg/gameguild/issues/218#issuecomment-5986287807). Earlier pending statuses in this document are
historical. The feature branch is removed, two existing checkouts are reused,
zero stashes remain and all 55 primary local files are preserved. The fixed scope
retains all 328 criteria/IDs: **66 closed / 262 open**.
The full program remains active. Next #223 remains open with
[executed backup-status gap](https://github.com/gameguild-gg/gameguild/issues/223#issuecomment-5986323447); no completion is inferred from the probe.
