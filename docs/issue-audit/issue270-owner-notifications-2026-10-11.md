# Security owner notifications — issue #270

Current production wiring persists suspicious-login signals independently of failed sign-in transactions, dispatches them through durable inbox/outbox, and queues urgent redacted InApp notifications for the known account owner. Email is preference-gated and queued for the configured dispatcher; these tests make no claim about actual external email-provider delivery.

Eight supplemental PostgreSQL/HTTP definitions exercise the delivered central notification list, individual lookup and unread count for platform/tenant events, anonymous denial, and cross-account read/change denial in the same and a different tenant. The fixture supplies synthetic authenticated actors for these access tests; existing suspicious-login tests use real anonymous sign-in and the actual durable consumers. Notifications are owner-scoped: tenant context alone never grants another account access.

Status: applied in this PR; hosted compilation and execution are pending. Require matching-head hosted execution of all eight access cases, existing six security-notification pipeline cases, current handler/publisher unit tests, API/Auth/Authz/SharedKernel regressions and all required checks before merge or closure.
