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

## Native candidate

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

## Acceptance status

This candidate changes tests and documentation only. No production defect has
been confirmed, and the historical issue state is retained. The new cases need
matching-source hosted CI evidence before credit. No local SDK, build, tests,
database, container, browser or server execution was performed.

Full recertification additionally requires the Web proxy/API integration evidence.
Neither these ten definitions nor the previous mocked Web cases alone complete
that review. No new issue closure or complete recertification is claimed here.
