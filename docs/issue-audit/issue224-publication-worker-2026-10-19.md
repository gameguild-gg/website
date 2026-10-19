# #224: durable EVM publication worker

Refs #224. Full issue acceptance remains pending.

## Scope

Builds on the contract/read proof, PostgreSQL journal, signing and publication ports. An explicit immutable profile policy supplies signer reference, captured integer-wei gas/fee caps, initial fees, retry/check/lease/replacement delays and a maximum of ten signed attempts.

The worker acquires fenced journal leases, independently verifies every known candidate before signing again, commits each signed payload before transport, and confirms only accepted canonical proofs. An older replacement can win. A restarted worker can recover an accepted send without resolving current key material. Included transactions wait for finality without replacement. Previously confirmed operations remain scheduled for reorganization checks.

Retries retain the same nonce and signed intent. Unacknowledged sends initially reuse their exact durable payload; after the replacement delay a bounded fee increase is possible even if no acknowledgement ever arrived. Both fee bounds increase while preserving the original captured transaction budget, chain, contract, digest, operation, signer, nonce, gas and zero value. Exhausted limits, network/proof mismatches and unavailable signing material enter a durable attention state, including failures before any hash exists. Caller cancellation preserves recoverable state and stale leases cannot commit progress.

`AddEvmCertificatePublicationWorker` explicitly registers the scoped processor and background service. The host must separately supply persistence, signer and network ports; missing dependencies fail startup. The default host/provider selection is unchanged. Background error logs omit provider/database exception values, which may contain credentials. No domain module references or HTTP contract changes are introduced.

## Verification status

Tests were authored before the corresponding implementation. No local SDK, build, restore, database, container, EVM, server or test execution was performed. Source inspection is not a passing test result; matching-current-source hosted CI and report inspection are required.

- Unit policy/registration cases cover invalid bounds, integer-wei identity, explicit composition and duplicate-profile rejection.
- Actual PostgreSQL cases exercise the production worker with controlled RPC/proof boundaries: persist-before-send, competing workers, uncertain send/restart, fee replacement and caps, earlier canonical winner, pending finality, network/key failures, externally recovered proofs, cancellation and stale-lease fencing. Constructed proofs in these cases establish journal/control-flow binding only.
- Native cases invoke the actual production worker against PostgreSQL and deployed Anvil over HTTPS: anchor, revoke using the original digest, accepted-but-unacknowledged-send recovery and an explicitly started/stopped background service. Public-only transaction projections are retained; they are not external-testnet evidence.

## Remaining full requirement

Durable host issuance/revocation across the existing post-commit crash gap; retention of original profile/issuer/digest; discovery of original chain operations if journal data is missing; operator handling of quarantined nonce gaps/external wallet use; actual host/provider acceptance; and approved external testnet deployment, funding, anchor/revoke finality and owner acceptance remain required. An operation requiring attention is not represented as successful or assigned an invented hash. Anvil chain12345 remains a private CI fixture.
