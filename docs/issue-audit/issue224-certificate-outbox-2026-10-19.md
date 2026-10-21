# #224: certificate transaction and original publication identity

Refs #224. Full issue acceptance remains pending.

## Implemented source

The certificates module now exposes a domain-local durable-evidence producer port. Its default is a no-op; the host supplies the EVM implementation explicitly. Issuance and revocation stage integration events inside the existing audited mutation transaction before its save. The existing host context captures them in its transactional outbox. There are no network, signing or journal calls on this commit path.

The host producer captures the original public network/profile, canonical issuer and digest at issuance. Its event identifiers are deterministic per certificate and operation. Recipient names, endpoints, key references/material and revocation explanations do not enter the event payload. Internal owner/tenant identifiers remain inside the application transport, never the chain. Actor identity comes from the trusted actor context, with explicit system/platform sentinels for background/global activity.

Revocation stages a reference to the original issuance event. Its consumer first uses the immutable original journal identity; when original delivery is still pending it recovers that identity from the outbox and queues the original anchor before the revocation. Delivery and replay converge through journal idempotency even if the outer consumer transaction rolls back after the separate journal commit. Unknown original identity or an unavailable original profile fails visibly rather than recomputing a digest from current settings.

The certificate factory/service now retain the supplied tenant on the issued row. Repeated revocation is a no-op success that preserves the first reason and durable event. The platform journal adds only an owner/tenant-scoped producer/document lookup; it has no domain-module reference or schema change. Neither the existing none/local selector nor legacy transaction-hash contracts change.

## Verification status

Twenty-four new cases are authored: ten producer/identity/composition cases and fourteen PostgreSQL service/outbox/journal cases. PostgreSQL cases use the production application EF model and certificate service plus the real dedicated journal migration; universal entity-change audit capture is disabled in that fixture to isolate transaction behavior. The required learning-audit port is controlled to prove rollback on its failure. Cases cover durable commit, post-commit network failure, restart/replay, owner/tenant/profile mismatch, original digest after configuration changes, reordered delivery, repeated revocation, missing original identity and ambiguous prior network bindings.

Core producer/outbox tests were authored before the production changes; additional regression coverage was added during review. No local SDK/build/restore/test/database/container/server/EVM execution occurred. Matching-source hosted CI/report inspection is required. These are not HTTP authorization, active deployed-host configuration or external-chain acceptance.

## Remaining work

Connect the actual configured host/provider, persistence/key/worker composition and authenticated asynchronous status/proof surface; test that real composition and its failure paths; handle original-profile availability, quarantined nonce gaps, operator recovery and discovery after journal loss; then deploy/fund/anchor/revoke and verify finality on the approved external testnet. An absent original publication identity is not represented as successful revocation. The full #224 stays open.
