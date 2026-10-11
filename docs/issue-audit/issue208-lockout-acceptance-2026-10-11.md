# Account lockout acceptance — issue208

## Current source and requirement

The active AuthenticationLockoutActionFilter is registered in MVC and gates local and polymorphic email sign-in before password verification. It counts rolling-hour failures per normalized email and across accounts per normalized source IP, normalizes IPv4-mapped IPv6, and serializes account/IP admissions with namespaced PostgreSQL advisory locks. AuthenticationSecurityOptions configures thresholds and account lockout duration.

## Added acceptance

Six supplemental full-application HTTP cases cover local/polymorphic email routes with plain and mapped IPv4 across unrelated accounts (four cases), plus lockout expiry while failures remain in the one-hour history (two cases). They assert protected generic denials, no-store responses, no risk analysis or issued session on rejection, and successful token/session issuance after expiry. Existing12HTTP lockout cases and two separate-context PostgreSQL contention cases also executed successfully in the accepted hosted run. Full-host test-only changes now select API, Authentication, Authorization and SharedKernel unit regressions in addition to the real HTTP/PostgreSQL project. Existing non-host module selection is preserved.

## Acceptance status

[PR #807](https://github.com/gameguild-gg/gameguild/pull/807) was accepted and merged into develop as be3cfa4c5da5c28121511d1fc43e10ff7718c0d0. Hosted [run38122801522](https://github.com/gameguild-gg/gameguild/actions/runs/38122801522) at signed source a28ccb64b19afb5786fbc49dfadf71d7fb504e9a passed8,885distinct native cases with0failed/skipped, including29lockout cases. All required/security/quality/CLA checks passed. The15OpenAPI rerun cases are a verified subset, so they are not added to the distinct count. Issue208 was officially closed with [the current evidence](https://github.com/gameguild-gg/gameguild/issues/208#issuecomment-6107286779). No per-account recovery flow is added to the historical account-lockout request without a specified requirement.

No local SDK, database, Docker, application or browser runner is launched. No new branch, worktree or stash is created.
