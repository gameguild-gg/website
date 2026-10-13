# Local password sign-in policy — issue #240

The current local service already reloads and validates the account before issuing a session or MFA challenge. This increment connects typed server configuration for email verification after active tenant resolution and before compliance, MFA, JWT, refresh-token or session issuance. Both local email and polymorphic email routes use that service. Generic credential denials preserve the current response contract.

## Configuration

- Authentication:LocalSignIn:RequireEmailVerification requires verified email for every tenant. Default false preserves existing host behavior.
- Authentication:LocalSignIn:RequireEmailVerificationForTenants is a list of additional non-empty tenant GUIDs. A tenant list cannot weaken the global requirement.
- Invalid tenant lists fail startup validation and evaluation. No client request can supply or override policy; evaluation receives the server-resolved active tenant.

This policy governs password sign-in. Existing independent OAuth, magic-link, WebAuthn and Web3 rules are unchanged. No API schema, migration or client regeneration is required.

## Pending acceptance

Written: 10 option cases plus 18 full-host PostgreSQL/HTTP cases, including inactive/suspended accounts, global and tenant enforcement, verified success, compatible disabled policy, unrelated tenant policy, and unverified high-risk attempts producing no MFA challenge. These cases are included in this PR; hosted compilation and execution are pending. Hosted current-head acceptance, affected API/Auth/Authz/SharedKernel regressions, OpenAPI and security/quality gates are required before merge or closing #240.

No new branch, worktree or stash; no local SDK, database, Docker or application runner.
