# Deny-by-default adversarial validation — GameGuild API

**Issue:** [#327](https://github.com/gameguild-gg/gameguild/issues/327) — acceptance criterion "Penetration testing validates security model".
**Scope:** HTTP-level adversarial validation of the deny-by-default authorization model of the GameGuild API (`apps/api`).
**Suite:** `apps/api/tests/GameGuild.API.SecurityTests` (xUnit, real host, real PostgreSQL, production JWT pipeline).
**Status:** PASS after remediation — the exercise **found two real defects** (one of them a privilege-escalation path), both fixed in the same change; see [Results](#results) and [Findings](#findings).

## What this is — and what it is not

This is a **first-party (internal) adversarial security validation pass**: a committed,
repeatable test suite that probes the running API the way an attacker (or an external
penetration tester) would — forged tokens, cross-tenant requests, self-grants, revocation
races, cache-poisoning windows, route-table sweeps — and asserts the deny-by-default
outcome of every attempt.

It is **not an external professional penetration test**. No independent third party
performed this work, and independence is part of what a pentest report certifies. An
external professional penetration test **supersedes** this document for the criterion
above; if one is later commissioned and finds issues, those findings govern. This suite
remains valuable after such an engagement as the regression harness that encodes the
attempted-attack inventory.

## Methodology

### Harness

`AdversarialSecurityFixture` boots the **real API host** via `WebApplicationFactory<Program>`:

- Migrations are applied to a disposable PostgreSQL 17 instance (Testcontainers locally;
  the CI gate database in PR verification).
- The **production JWT bearer handler is the only authentication scheme** — no synthetic
  test-auth handler is registered, so every token must survive signature, algorithm,
  issuer, audience, lifetime, revocation-middleware, tenant-middleware, actor-context and
  authorization checks exactly as in production.
- Production startup parity: the production `PolicyDefinitionSeeder` runs against the
  host at fixture initialization (CI's affected-test runner applies migrations only).
- Deliberate, documented deviations from production configuration:
  - `PresentationLayer:EnableRateLimiting=false` — the sweep fires hundreds of requests
    from one address; the property under test is the authorization decision, not 429s.
    The rate-limiting layer has its own dedicated suite
    (`GameGuild.API.RateLimiting.PerformanceTests`).
  - Startup seeding/background cleanup disabled during host boot (fixture controls
    migration and seeding order), matching the existing API integration-test fixture.
- Identities are seeded directly in the database (user + tenant + membership + session),
  and valid tokens are minted **by the host's own `IJwtTokenService`** — the attacker
  model is "authenticated low-privilege tenant member".

### Scenario inventory

| # | Family | File | Attack under test |
|---|--------|------|-------------------|
| a | Privilege escalation | `PrivilegeEscalationAdversarialTests.cs` | Zero-grant member hits permissioned surfaces (feature-flag reads behind `Features.Read`, permission-engine mutations, global-default writes, other-user inventory reads); asserts 401/403 (or fail-closed 500, see finding F-1) **and** asserts no grant row appears in the database. |
| b | Tenant crossover | `TenantCrossoverAdversarialTests.cs` | Valid tenant-A token attempts to reach tenant-B data via route parameter, `?tenantId=` query, `X-Tenant-Id` header and request body; asserts denial on every carriage, no cross-tenant grant row, and no tenant-B identifiers in the tenant-A response. |
| c | Token forgery / tampering | `TokenForgeryAdversarialTests.cs` | Wrong signing key; corrupted signature; payload swap keeping the valid signature (with forged `SystemAdmin` role and bumped `token_version`); unsigned `alg=none`; disallowed `HS512`; expired; not-yet-valid; wrong issuer; wrong audience; malformed bearer values; revoked jti; stale `token_version`. All must yield 401 problem+json with `WWW-Authenticate` and no token echo. |
| d | Unguarded-endpoint sweep | `UnguardedRouteSweepTests.cs` | Reads the **running host's route table** (`EndpointDataSource`): every controller endpoint must carry `[Authorize]` or `[AllowAnonymous]` metadata; every anonymous endpoint must be registered in `AnonymousEndpointRegistry` with a justification; every registry entry must point at a real controller action (no stale allowlist rows). Then an anonymous HTTP walk over every materializable GET route: protected routes must answer 401/403 to anonymous callers (never 2xx); registered anonymous routes must not answer 401. |
| e | TOCTOU / race | `GrantRevokeRaceAdversarialTests.cs` | 32 concurrent requests in flight while the grant is revoked through the production `IPermissionGrantService` (guarded, versioned, audited path); asserts no 5xx (fail closed, never open, under concurrency) and that after the revocation commits, a concurrent 32-request burst is denied with 403 — no stale-allow window. Control experiment: a committed grant is honored by a concurrent burst (proves denials are semantic, not a broken path). |
| f | Cache poisoning / stale-allow | `RevocationCachePoisoningTests.cs` | Warms the decision caches with allowed requests, then revokes through the production mutation path (tenant security version bump — the #746 stale guard) and immediately re-requests: 5 grant/revoke cycles, deny-rule-after-warmed-allow, and pre-expired/inactive grants never authorizing. |
| g | Elevation / impersonation seams | `ElevationSeamAdversarialTests.cs` | JIT elevation surface (`/v1/jit-elevations`): self-approval (entity guard), approval with a **spoofed reviewer id** (the reviewer arrives in the request body), elevation request forged with another user's requester id, and self-assigned delegated-admin scope. The invariant: no path leaves the attacker (or the named victim) holding an in-force elevation or the requested permission. **This family found a real escalation (F-2) — fixed in the same PR; the scenarios now pin the remediated behavior.** |

### Denial semantics used by assertions

- **401** — authentication-layer rejection (token validation, anonymous on guarded route).
- **403** — authorization-policy rejection (policy denial, tenant mismatch, membership check).
- **500 problem+json, generic body** — fail-closed rejection surfaces where a guard throws
  `UnauthorizedAccessException` and the global exception middleware maps it to the generic
  internal-error response (no detail leakage; the request is denied and no capability is
  acquired). Counted as a denial for the security property; recorded as finding F-1 for
  response hygiene.

## Results

Run: `dotnet test apps/api/tests/GameGuild.API.SecurityTests` in PR verification (CI),
triggered automatically because the project lives under `apps/api/tests/*Tests/` and the
affected-test selector picks this up for any change inside it. See the PR checks and the
`affected-api-*` artifacts for the executed TRX evidence; the summary is reproduced in the
issue close-out comment.

The first full run of the suite **failed**: it exposed two real defects (findings F-2
and F-3 below) plus three harness defects in the suite itself (HS512 forge key shorter
than 512 bits; mutation endpoints probed with GET yielding 405 before authorization was
evaluated; a delegated-admin probe body missing a validator-required field). The harness
defects were corrected, the two product defects were fixed in the same change, and the
suite now passes in full.

| Family | Result |
|--------|--------|
| (a) Privilege escalation | PASS — no 2xx on any probed surface; zero grant rows created |
| (b) Tenant crossover | PASS — every carriage denied; no cross-tenant data or grant rows |
| (c) Token forgery / tampering | PASS — all forged/tampered/revoked/stale variants rejected 401 |
| (d) Unguarded-endpoint sweep | PASS — full route table guarded or allowlisted; anonymous walk clean |
| (e) TOCTOU / race | PASS — fail-closed under race; no stale-allow window post-revocation |
| (f) Cache poisoning / stale-allow | PASS — version-bumped invalidation observed over HTTP; deny-wins; lifecycle states dead |
| (g) Elevation / impersonation seams | PASS — **after fixing the F-2 escalation in this PR**; no in-force elevation or effective permission via any seam |

**Overall: one exploitable privilege-escalation path (F-2) and one audit-integrity defect
(F-3) were found by this exercise and are remediated in the same change; after
remediation no authorization bypass remains** against every attempted attack in the
inventory above.

## Findings

### F-1 (low, response hygiene): guard rejections surface as generic 500s

Command handlers enforce authorization by throwing `UnauthorizedAccessException`
(e.g. `GrantTenantPermissionCommandHandler`, `GrantDelegatedAdminHandler`); the global
`ExceptionHandlingMiddleware` maps it to a generic 500 problem+json rather than 403,
while the OpenAPI annotations document 401/403 for those endpoints. The security
property is intact (denied, no side effects, no detail leakage), but the status code
misleads API consumers and monitoring. Suggested follow-up: map
`UnauthorizedAccessException` → 403 in the exception middleware (a one-line addition to
its catch chain). Not fixed here to keep this change test-focused.

### F-2 (critical, privilege escalation — FOUND AND FIXED): JIT elevation self-approval with a spoofed reviewer id

Before this change, `POST /v1/jit-elevations/{id}:approve` trusted the reviewer identity
from the request body, and the only guard was the entity-level check
`reviewerId == requesterId` (self-approval). Naming **any other GUID** as the reviewer
bypassed it: the elevation auto-activated, and `EffectivePermissionResolverService`
(layer 7, in-force JIT grants) then contributed the requested permission to the
attacker's allow set. Exploit chain exercised by scenario (g): authenticated member →
request elevation for `features:read` → approve it themselves with a random reviewer id
→ hold the permission for the requested duration. Discovery trail: the first CI run of
the suite could not complete scenario (g) — every JIT request 500'd from finding F-3;
tracing that failure exposed both F-3 and, behind it, the unguarded approval path
(`ApproveJitElevationHandler` at the parent commit passes the body reviewer straight
through — verifiable in the PR diff). Both were fixed before the suite was re-run.

Remediation (same PR): all four JIT elevation command handlers now take the acting
identity from `IActorContextAccessor` and reject a body-supplied identity that does not
match the authenticated actor; approval additionally requires system-admin or
same-tenant-admin authority (the elevation's tenant, from the stored request — never
from the body; tenant-less/global elevations require system admin). Deny/revoke keep
self-service semantics (privilege-reducing) but with actor-derived attribution. Pinned
by `JitElevationActorIdentityGuardTests` (unit) and the scenario-(g) HTTP tests.

### F-3 (high, audit integrity — FOUND AND FIXED): the permission audit log was never persisted

`PermissionAuditLog` had no EF mapping, no `DbSet`, and no table in any migration —
`PermissionAuditService.LogPermissionChangeAsync` (called by every guarded permission
mutation: grant, revoke, deny, JIT elevation, delegated-admin scopes) threw
`InvalidOperationException: Cannot create a DbSet for 'PermissionAuditLog' …` against a
real database. Consequence: the architecture invariant "permission mutations are
audited" was broken at runtime — the mutation itself committed (it is saved before the
audit write) and the request then failed 500 with no audit row. Not an access bypass
(deny-by-default still governed the mutation), but the audit trail required for
security monitoring of exactly the operations this issue cares about silently did not
exist. The suite's TOCTOU/cache scenarios (e), which drive the production
grant/revoke services, failed on this and made the gap impossible to miss.

Remediation (same PR): `PermissionAuditLogConfiguration` maps the entity (TenantId
value conversion, enum-as-int, query indexes) and migration `AddPermissionAuditLog`
creates the `PermissionAuditLogs` table; every permission mutation now writes its audit
row through the same code path.

## Relationship to existing security testing

This suite complements — not replaces — the existing developer security tests:
`Security/AuthorizationBehaviorFailClosedTests.cs`, `SecurityBoundaryRegressionTests.cs`,
`Security/TenantPermissionCommandSecurityTests.cs` (service-level fail-closed matrices),
`BearerRevocationPostgreSqlHttpTests.cs` (token revocation lifecycle), the #746
stale-guard tests in `PermissionCacheRedisIntegrationTests.cs` (service-level cache
staleness) and the GGARCH compile-time analyzers (endpoint guard requirements). The
contribution of this suite is the attacker's perspective at the HTTP boundary: the full
production pipeline, real tokens, real mutations, and denial asserted end-to-end.

## Re-running

```bash
dotnet test apps/api/tests/GameGuild.API.SecurityTests/GameGuild.API.SecurityTests.csproj
```

Requires Docker (Testcontainers) or `ECONOMY_POSTGRES_CONNECTION`. PR verification runs
the suite automatically for any change under the project directory.
