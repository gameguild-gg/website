# Issue #210: active session listing and bulk revocation acceptance

The preserved title-level scope is authenticated active-session listing and
revocation of other sessions or all sessions. The historical source review found
expiry filtering and active bearer enforcement incomplete. The accepted source
now uses the expiry-aware owning-user repository query and the production token
revocation middleware. The all-session endpoint dispatches the guarded
`RevokeAllUserTokensCommand`; termination of other sessions preserves the current
session and user token version.

The accepted #897 artifact already contains nine native PostgreSQL/HTTP results
for all-session/other-session revocation, owner isolation, failure rollback and
the refresh race. Those existing results are reused evidence, not new tests.

The listing unit test mocks the expiry-aware repository. A successful authenticated
GET alone does not establish that the real SQL omits an expired row whose stored
`IsActive` flag is still true. This increment adds two native cases to
`RevokeAllUserTokensPostgreSqlHttpTests`, with another account in the same tenant
or a different tenant. They use migrated PostgreSQL, real JWT issuance and bearer
validation, actual tenant membership, the active HTTP route and the production
session service and repository.

The cases require:

- Only the authenticated owner's unexpired, active session is returned; another
  user ID supplied in the query cannot select that user's sessions.
- An expired row with `IsActive=true` and a future-dated inactive row are omitted.
  Their flags remain unchanged after the read, so a cleanup job is not the proof.
- The actual current session is identified correctly for each caller, and the
  other account retains its own two sessions.
- Anonymous listing is rejected.

No production behavior, authentication scheme, permission guard, API contract or
database migration changes in this increment. No local application, SDK, build,
test or database execution was performed. Matching-source hosted CI and inspection
of its native results remain required before current-source recertification.
The existing issue closure is not counted again as a new implementation closure.
