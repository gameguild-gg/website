# Account lockout acceptance — issue208

## Current source and requirement

The active AuthenticationLockoutActionFilter is registered in MVC and gates local and polymorphic email sign-in before password verification. It counts rolling-hour failures per normalized email and across accounts per normalized source IP, normalizes IPv4-mapped IPv6, and serializes account/IP admissions with namespaced PostgreSQL advisory locks. AuthenticationSecurityOptions configures thresholds and account lockout duration.

## Prepared acceptance

Six supplemental full-application HTTP cases cover local/polymorphic email routes with plain and mapped IPv4 across unrelated accounts (four cases), plus lockout expiry while failures remain in the one-hour history (two cases). They assert protected generic denials, no-store responses, no risk analysis or issued session on rejection, and successful token/session issuance after expiry. Existing12HTTP lockout cases and two separate-context PostgreSQL contention cases must also execute. Full-host test-only changes now select API, Authentication, Authorization and SharedKernel unit regressions in addition to the real HTTP/PostgreSQL project. Existing non-host module selection is preserved.

## Acceptance status

This packet is prepared outside the repository. The six native cases have not compiled or executed. Hosted current-head runs, complete TRX evidence and all required security/quality gates are required before merge or issue closure. Historical passes and written definitions are not credited as current acceptance. No per-account recovery flow is added to the historical account-lockout request without a specified requirement.

No local SDK, database, Docker, application or browser runner is launched. No new branch, worktree or stash is created.
