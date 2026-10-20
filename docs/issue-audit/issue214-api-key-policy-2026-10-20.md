# #214: configured API-key policy and trusted actor

Regression-only candidate. Refs #214 and the lifecycle evidence review in #215.
No defect, implementation acceptance, recertification or issue closure is credited until hosted evidence is inspected.

The host supports opt-in ApiKey registration and named/default authorization policies with explicit schemes.
Its authentication middleware runs before tenant membership validation and immutable actor capture;
authorization runs afterward. These tests qualify the actual supported configuration, without substituting
a synthetic principal or inferring a routing defect merely because Bearer remains the default.

One actual-host PostgreSQL HTTP case requests the real API-key management route with a valid owner/tenant
credential under a configured ApiKey policy. It requires the authenticated owner to reach the actual query
handler and verifies persisted usage without exposing plaintext in the list response. Five controls cover
missing, unknown, expired, revoked and unrelated-scope credentials.

The common fixture's synthetic default is replaced with the production Bearer default. The policy must
select the real ApiKey handler. Actual migrations, entities, repository, middleware, actor and endpoint
remain in use. No signing, database, SDK/build/restore/test/container/server/browser process runs locally.
Matching-source hosted compilation, response reports and controls determine whether a real integration
defect exists. Broader API-key rotation/audit/tenant scenarios and full #266 repository scope remain pending.
