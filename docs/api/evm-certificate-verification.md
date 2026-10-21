# EVM certificate verification — issue #224

This is a staged backend component for the real-chain provider. It does not
activate `Provider=evm`, create durable publication operations, sign or send
transactions. Existing `none`/`local` registration and canonical payloads are
unchanged. The complete issue remains open until the durable publisher, host
integration and actual testnet acceptance are delivered.

## Explicit network identity

`EvmCertificateNetworkOptions` binds a positive chain ID, HTTPS RPC endpoint,
nonzero registry address and public bytes32 issuer identity, expected deployed
runtime SHA-256, timeout/response limits and finality policy. Defaults propose
Sepolia11155111 with `Finalized`; empty deployment settings are invalid.
Sepolia cannot silently fall back to a confirmation count. Other EVM networks
may explicitly select a positive confirmation policy.

The code digest is SHA-256 of **actual deployed runtime bytes**. Compiler output
containing immutable placeholders is not the expected deployment runtime. Review
the deployment and its configured issuer before recording this digest. This is
distinct from Ethereum's Keccak code hash and the SHA-256 certificate digest.

`AddEvmCertificateVerification` explicitly registers the transport/verifier. Its
named client disables redirects/cookies and default request loggers, and uses
the configured per-request deadline. Configuration and exception messages never
echo the endpoint, provider error text/data or inner transport exceptions.
No private signing key is part of these read-service settings. Global tracing
configuration must also protect any credential-bearing RPC URL.

## Proof checks

For each observation the verifier:

1. Checks `eth_chainId` and chooses an explicit policy-qualified block. A
   confirmation policy reads the confirmed height, including for revocation
   state; it does not read unconfirmed latest state as confirmed evidence.
2. Pins code and contract reads to that block's hash using EIP-1898 with
   `requireCanonical=true`; validates deployed code, protocol1 and issuer.
3. Requires the claimed receipt and transaction to match the requested hash,
   direct registry target, expected exact calldata, zero value, signer and block.
   A provided transaction chain ID must also match. Both successful receipts and
   terminal reverts must meet canonical inclusion/finality policy.
4. Checks the canonical block hash and the exact event digest, issuer, signer,
   block, transaction and removal status. Missing/duplicate events cannot verify.
5. Reads the registry state at the same pinned block. Original issuance survives
   signer rotation; current signer grants do not invalidate historical evidence.
   Finalized revocation invalidates an anchoring proof. A revocation proof must
   match its own receipt/event and the stored revoker/block.

Missing receipts remain pending. Unqualified inclusion is `NotFinalized`; a
divergent/disappearing canonical block is `Reorged`; malformed or inconsistent
data is rejected. Transport/identity failures cannot produce verified evidence.
The default proof status is `InvalidEvidence`.

The v1 publisher targets direct registry transactions from its issuer signer.
The verifier does not infer internal calls from account-abstraction/multicall
transactions. It is an RPC-based verifier, not an Ethereum consensus/light client.

## Evidence and remaining work

Unit cases cover typed configuration, correlated/bounded JSON-RPC, safe errors,
deadlines/cancellation, network/code/issuer identity, canonical block pinning,
receipt/event/state mismatch, finality, revocation and rotated signers. RPC/HTTP
test doubles prove these component decisions; they do not establish real-chain
publication. The Solidity registry is separately compiled/executed by hosted
Forge tests. Native interoperability and actual Sepolia acceptance remain
separate evidence layers.

Next stages must preserve these ports while implementing durable idempotent
operation/nonce storage, signer references and fee limits, publication/recovery,
host composition and finalized anchor/revoke transactions on the configured
testnet. No local-provider record may be relabeled as real-chain evidence.

Protocol sources: [JSON-RPC2.0](https://www.jsonrpc.org/specification),
[Ethereum RPC conventions](https://ethereum.org/developers/docs/apis/json-rpc/),
[EIP-1898](https://eips.ethereum.org/EIPS/eip-1898), and the
[registry source](../../contracts/certificate-anchoring/src/CertificateAnchorRegistry.sol).
