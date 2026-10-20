# #224: authenticated asynchronous certificate evidence

Refs #224. Full issue acceptance remains pending.

The host adds authenticated `GET /api/certificates/{id}/evm` and
`GET /api/certificates/{id}/evm/proof` routes. Before any journal/RPC lookup, the controller loads the
persisted document with an exact authenticated owner/tenant predicate. An explicit trusted
SystemAdmin may cross that boundary; soft-deleted rows remain hidden. Tenant and owner query
parameters do not select scope. Responses declare no-store caching.

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

There are 57 authored cases: 30 reader-policy cases, 12 controller-guard cases and 15 HTTP cases on
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
