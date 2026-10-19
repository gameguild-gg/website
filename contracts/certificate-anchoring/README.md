# Certificate anchoring EVM contract — issue #224

Status: source candidate for the first delivery stage. This contract is not
deployed and does not enable an EVM backend provider. The issue stays open.

## Protocol v1

`CertificateAnchorRegistry` binds one immutable, public `bytes32 issuerId` to one
deployment. A deployment has a configured chain ID and contract address; a
certificate digest alone is not a globally unique proof. The backend must bind
chain, contract, protocol version, issuer and digest when storing/verifying proof.

Only authorized issuer signers can call `anchor(bytes32)` or `revoke(bytes32)`.
The administrator grants/removes signers and transfers administration through
`beginOwnershipTransfer(address)` followed by `acceptOwnership()` from the proposed
owner. Administrator status does not implicitly grant signing rights. A signer
cannot grant other signers or change ownership. Governance can supersede a pending
ownership proposal. There is no renunciation or proxy upgrade mechanism.

Publishing a digest records the original signer and block. Publishing it again
returns `false`, leaves the original evidence unchanged and emits no event.
Revocation records the authorized revoker and block permanently. Repeated
revocation is also a no-op; publishing a revoked digest cannot revive it. All
duplicate calls still require a currently authorized signer. Revoking an unknown
digest reverts. The all-zero digest and zero role addresses are rejected.

`getAnchor(bytes32)` returns:

| Field | Meaning |
| --- | --- |
| `anchorSigner` | Original signer; zero means this digest was not anchored |
| `revokeSigner` | Original revoker; nonzero means permanently revoked |
| `anchoredAtBlock` | Block number of first anchoring |
| `revokedAtBlock` | Block number of first revocation |

Events `CertificateAnchored` and `CertificateRevoked` index digest, issuer and
acting signer, and include the block number. `PROTOCOL_VERSION()` returns `1`.
Only SHA-256 digests of the existing canonical certificate payload are intended
for publication. No user IDs, payloads, claims, certificates or revocation reasons
belong in transaction data or events. The issuer identifier must be public metadata.
An application database record or successful transaction submission is not proof
of receipt success, canonical inclusion, finality or current revocation state.

## Reproducible hosted verification

The workflow `certificate-anchoring-contract.yml` pins Foundry `v1.8.5`, Solidity
`0.8.37`, the toolchain action commit, compiler options and Paris EVM bytecode.
It has no signing keys, RPC secrets or deployment step. It builds the actual
contract and executes it inside Forge's EVM, retaining JUnit, ABI/bytecode and the
exact checked-out commit/tree. Tests use real proxy actors to exercise `msg.sender`,
plus Foundry's documented block/log/revert test interfaces. They have no external
Solidity library dependency and include a 256-run digest isolation fuzz case.

Relevant commands (for hosted CI; no local tool installation is required):

```sh
forge build --sizes
forge test --junit
```

Sources: [Solidity contracts](https://docs.soliditylang.org/en/v0.8.37/contracts.html),
[pinned Foundry action](https://github.com/foundry-rs/foundry-toolchain/blob/908c540300062bd5a7e473851cdb4282204cee09/action.yml),
[Foundry test interface](https://github.com/foundry-rs/forge-std/blob/master/src/Vm.sol).
`private` Solidity fields are still public blockchain data; no secret is stored.

## Remaining work for the actual EVM provider

The [issue proposal](https://github.com/gameguild-gg/gameguild/issues/224#issuecomment-6266492261)
remains the complete acceptance scope. Next stages must provide typed configuration
and startup checks for chain ID, expected runtime code/protocol/issuer; durable
operation storage and nonce coordination; signer reference and fee limits; recovery
of submitted transactions; canonical receipt/event/finality verification; and
finalized revocation verification. Preserve existing `none`/`local` records and
providers. Do not treat their deterministic synthetic hashes as blockchain receipts.

Initial real acceptance is proposed on Sepolia, chain ID `11155111`. It requires a
configured RPC, funded test signer, deployment with reviewed owner/signer/issuer,
and independently verified finalized anchor and revoke transactions. Chain state,
receipts and generated ABI/bytecode must match the reviewed complete source. Forge
tests do not substitute for this external acceptance. No transaction was sent as
part of this source increment. Anchoring also does not implement W3C credential
signing. No mainnet deployment or payment is configured.
