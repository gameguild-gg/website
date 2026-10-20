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

## First hosted diagnostic and fixture correction

The first signed source `3aecfa003d330d197c9fc22bd1f4fcb290b02048` compiled. Its digest-verified
hosted artifact `12240095754` (SHA-256
`a53423461aaf7a27691a5aec6a9b03599ecebcb10cb978e0ece0e99f680a244a`) contains all six cases:
one passed and five failed while seeding, with no unrelated failure. PostgreSQL rejected the repeated
tenant name under `IX_Tenants_Name`; those five cases never reached their intended HTTP assertions.
This is a fixture defect and does not establish a production authentication defect.

The correction gives each seeded tenant a unique name using the existing case marker. All six
assertions, production authentication code, database constraints and actual host configuration remain
unchanged. Fresh matching-source hosted evidence is required before drawing a functional conclusion.

## Configuration preconditions and independent accepted base

The second diagnostic source `cff5587d68e3e079d01c07ba8aa32b4442f8d4e5` compiled. The four unavailable
credential controls passed, while the valid owner and unrelated-scope cases received 401. Artifact
`12241071438`, SHA-256 `a2dc78ae0085965120002716c07f0d70bd2c7f188f2d707e76ba47221739bae1`, also
contains one separate assessment HTTP 500 failure. No failure is waived and no production root cause
or issue completion is inferred from those responses alone.

Current qualification now explicitly requires the real ApiKey scheme/handler registration, the ApiKey
authentication scheme in the actual default authorization policy, and the production Bearer default
before issuing each HTTP request. The valid-owner assertion also reports the persisted key usage count
on failure, so a disabled/misconfigured scheme can be distinguished from a later actor/policy failure.
Root configuration remains the host's supported `Authentication`/`Authorization` path; the test does
not replace policy or principal services to make the assertions pass.

Accepted #913/develop `915c16a4056752e900f2f03d5e4a47534f4082ef` is merged normally into this branch.
The six-case candidate still changes only test/documentation source relative to that accepted base.
