# API-key authentication through the actual host

## Confirmed regression

Issue #214 was reopened after source `c7c5d1ecf4054c4e0aa0c53d53605dbadcb696e1`
registered the real ApiKey handler and selected it in the actual default policy.
The Bearer authenticate default remained unchanged. A valid persisted key incremented
its usage counter to1 but GET `/v1/auth/api-keys` returned400 with `Auth.Required`.
The host had captured an anonymous actor before authorization authenticated the key.

Five unavailable-credential/scope controls passed. Actual tested checkout:
`ecb678b0905f346f611a5c51d2afdb3af82082b8`; tree:
`a627f90910bb7d20f426807e76ccec7e6faea57d`. Artifact `12260953820`, SHA-256
`22fbf47c0f2bcce53f9ebd8ab06887e8df4a34b775de3633f41797bc44b59429`.
An independent audit-export timeout also occurred, so this run establishes a
regression and does not constitute approved CI.

## Repair

The host selects explicit endpoint and static default/named/fallback authentication
schemes before token revocation, tenant membership and immutable actor capture.
Selection follows ASP.NET Core policy combination. It does not globally route
requests with a key header to ApiKey or change the configured Bearer default.

Tenant-dependent policy lookup remains after tenant establishment. A later policy
that authenticates a different principal is denied before authorization handlers
run. A database-backed policy selecting another scheme must also declare that
scheme in endpoint metadata or static configuration. Multiple successful
credentials cannot combine owners or scopes.

## Qualification

- Eleven real-host PostgreSQL HTTP cases: the original six identity/credential/scope
  cases plus tenant resolution from the key, foreign-tenant and inactive-membership
  rejection, complete create/rotate/revoke/audit flow and foreign-owner isolation.
- Twenty composition cases with explicitly synthetic credentials: default, named,
  explicit, embedded and fallback schemes; cookies/Bearer; unavailable credentials;
  multiple identities; deferred policies; disabled schemes; optional public identity.
  These component cases do not replace actual credential/database acceptance.
- Five actual registered repository cases for #266: local-user credentials, external
  login links/consent, WebAuthn credential state, refresh persistence and API keys.
  These establish persistence contracts, not external OAuth or WebAuthn cryptography.

The tests use early host settings because these authentication switches are consumed
while Program registers services. Earlier diagnostic sources corrected unique tenant
names and configuration timing; their401 responses were not production-defect evidence.
The actual handler/policy/Bearer-default preconditions remain in every HTTP case.

Current hosted build, tests, security checks and delivered tree must be verified
before acceptance. No local SDK, product, database, container or browser execution
was used. The original opt-in, user-owned credential scope remains unchanged.
