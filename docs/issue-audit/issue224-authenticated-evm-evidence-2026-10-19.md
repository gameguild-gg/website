# #224: authenticated asynchronous certificate evidence

Refs #224. Full issue acceptance remains pending.

The host adds authenticated `GET /api/certificates/{id}/evm` and
`GET /api/certificates/{id}/evm/proof` routes. Before any journal/RPC lookup, the controller loads the
persisted document with an exact authenticated owner/tenant predicate. An explicit trusted
SystemAdmin may cross that boundary; soft-deleted rows remain hidden. The controller uses the trusted
tenant context. An unjoined tenant selected through a query is rejected by tenant middleware before
the evidence lookup; owner and transaction query fields cannot replace the stored document identity.
Responses declare no-store caching.

Local certificate status, durable publication state and fresh chain proof are separate fields.
Cached confirmations never prove current validity. A proof request reads the original recorded
digest/transaction/network, checks the configured profile, and independently calls the proof
verifier. Returned evidence must match the original identity. Revocation, expiry, reordered or
ambiguous identities, unavailable profiles, missing transactions and provider failure cannot become
a valid result. A verified external original proof can still be read when there is no local signing
identity; no signer or nonce is invented. Reading does not mutate the journal or submit a transaction.

The response projects public network/operation/evidence fields only. It excludes internal owner and
tenant IDs, signer references, spending budgets, raw transactions, credentials and lease tokens.
Provider exception details are discarded. Disabled hosts retain a no-op reader and explicitly report
disabled configuration without registering publication persistence, RPC, keys or workers.

## Authored validation and outstanding acceptance

There are 59 authored cases: 30 reader-policy cases, 12 controller-guard cases and 17 HTTP cases on
the real API host with actual PostgreSQL application/journal models and real signed bearer/session
validation. Reader-policy cases control the lookup/proof boundaries; controller cases use InMemory.
HTTP cases simulate magic-link verification and chain-proof responses. Their stored transaction is
prepared through the real journal and EIP-1559 signer using an ephemeral fixture key. This covers
HTTP scope/privacy/binding; it does not establish actual RPC, chain finality or public-testnet acceptance.

These are definitions pending matching-source hosted execution. No local SDK/build/restore/test,
database/container/server/EVM execution occurred. Generated-client reconciliation from the new
captured OpenAPI contract and hosted API/HTTP/security gates are required before this increment is
accepted. Public Sepolia deployment/funding/anchor/revoke/finality and operational recovery remain
pending for the full issue. The issue remains open.

The existing OpenAPI CI verifier now archives the generated client source beside its captured
contract before checking drift. The drift gate still fails until matching client output is reviewed
and committed. This provides a source-bound reconciliation path without local API/SDK execution.
The release classifier recognizes controller/contract filename suffixes outside conventional
directories and changes to the OpenAPI verification scripts. Four additional authored classifier
regressions cover those paths and preserve test-only/CI-only deployment behavior. Existing full
Economy qualification for changes to the classifier remains in force.

The client endpoint descriptors, certificate module, exports and public response schemas were
reconciled with the hosted OpenAPI generation from source
`a36e7f01423b3257278c4102ff8881dd69a666f8` (artifact `12233194987`, SHA-256
`3649790abe9cfba29f0fa1f482dd2b98eafe2d9c57416d05a278601d13fddd59`). Only those four
generated TypeScript files changed; existing hidden generator metadata was preserved because the
artifact upload excludes hidden files. This source reconciliation requires a passing client drift
gate and the other selected checks on the updated PR source before acceptance.

Hosted regression on source `6591a26b504df002b23972cd754bc1d1b01ac74a` exposed an incorrect HTTP
test expectation: combining a transaction query override with an unjoined tenant expected 200,
while tenant middleware correctly returned 403. The original-transaction test now isolates its
transaction query. Two additional HTTP cases explicitly require 403 and zero evidence/proof calls
for an unjoined tenant query on each route. Tenant authorization behavior is preserved. Matching
updated-source CI remains required; the failed run is not acceptance evidence.
