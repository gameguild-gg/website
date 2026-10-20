# Issue 221: authentication state acceptance

## Existing scope

The issue has an empty body. PR 842 delivered its self-service contract:
`GET /v1/auth/session-state`, the generated client, a same-origin Web proxy,
`useAuthState`, and an account-settings card. The query reports the authenticated
caller's session and MFA state; it accepts no client-selected account identity.

Current source review at develop `c24c68abdca2377ec9c3d2eae6e3e50d5357cefb`
preserves that scope. Nine handler cases passed in the accepted PR 913 API
artifact. Eight Web cases passed in PR 842 run 38503283985, Web job 115759235294;
their six source/test files remain unchanged. These cases mock the respective
service or transport boundaries, so they do not establish native HTTP acceptance.

## Native acceptance delivered

`AuthSessionStatePostgreSqlHttpTests` adds ten cases against the existing migrated
PostgreSQL fixture and real JWT authentication:

| Requirement | Cases |
| --- | --- |
| Authenticated owner, persisted timestamps and active-session count | MFA disabled/enabled |
| Enrollment does not imply verification of the current token | Both projection cases |
| Read-only session projection | Fresh-scope database assertions |
| Account/session query parameters cannot change the caller | Foreign identity parameters |
| Valid bearer without a session binding | Null session metadata, retained owner count |
| Authentication required | Anonymous request denied |
| Invalid bindings cannot disclose session metadata | Expired, terminated, foreign-owner, missing |
| Current token version required | Version advanced after issuance |

The fixture does not replace the bearer handler, state query, session repository
or MFA repository. JWTs are signed by the registered service; rejection is checked
through the actual middleware and route.

Invalid or revoked session-bound credentials are rejected by middleware before
the query. The handler's unit-level invalid-session projections are therefore not
advertised as HTTP responses for those credentials.

PR 915 delivered the ten cases in commit
`66773ef1eeea6ecf692ab053a559e8985c07f2d2`, after all matching-source CI passed.
The actual tested checkout was `86aa74ec9122902f7c6e6effd5c3cac42e88b2da`;
its tree and the delivered tree are
`4e23e600e192f367eb92e32c1ce20e56202b0ded`.
PR Verify run 39427264763, API job 118643096622, contains 10,303 passed API
cases, including all ten above and nine handler cases, plus 16 OpenAPI cases.

## Composed Web acceptance candidate

The dedicated `Session state functional acceptance` workflow starts the actual
API on Kestrel with a migrated disposable PostgreSQL database, then runs the
actual Next.js development application and Chromium. A separate console project
reuses the existing API fixture; ordinary API test jobs do not gain a Node or
browser dependency.

The synthetic account has two active sessions and a persisted tenant membership.
Another account owns a third session. The registered JWT service signs the
access token, and the built client library encrypts the browser session cookie.
Only this bootstrap is synthetic: the Next request cookie reader, authentication
code, state route, hook, UI, API middleware, query and repositories are real.
Credentials travel through a child-process input pipe, never command arguments
or evidence files.

| Hosted scenario | Required observable result |
| --- | --- |
| Anonymous browser | Same-origin route returns 401 |
| Tampered encrypted cookie | Same-origin route returns 401 |
| Actual account page | Persisted owner/session/timestamps/count/MFA state matches the response and card |
| Refresh button | Successful current-state response with `cache-control: no-store` |
| Foreign identity parameters | Projection remains bound to the authenticated owner |
| Actual API session termination | Authenticated DELETE succeeds |
| Refresh after termination | BFF forwards 401; card removes stale fields and renders an error |
| Independent fresh database scope | Current session terminated; two unrelated sessions unchanged |

The workflow retains exact checkout/tree/parents, seven browser scenario
outcomes, a separate persistence verdict, screenshots and redacted Next logs.
Missing or failed evidence fails the job. No authentication or transport mocks,
manual CI reruns, local product execution, or production-build qualification
are implied.

## Acceptance status

The native API increment is delivered. Composed Web acceptance is a candidate
until its matching-source hosted results and all CI have been reviewed. The
historical issue state is retained; this candidate does not count as a new
implementation, official closure or complete recertification.
