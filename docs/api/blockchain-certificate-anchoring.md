# Blockchain certificate anchoring (safe-default configuration)

Certificate evidence is **disabled by default**. The legacy `none`/`local` providers contact no external
network. The separately enabled EVM host composes the durable certificate outbox, PostgreSQL journal,
signer and publication worker. Full issue #224 acceptance still requires the authenticated status/proof
surface, recovery acceptance and a verified deployment on a public testnet.

## Configuration

Section: `BlockchainCertificates` (options type: `GameGuild.Configuration.ApplicationLayer.BlockchainCertificateOptions`, registered by `AddBlockchainCertificateAnchoring`).

```json
"BlockchainCertificates": {
  "Provider": "none",
  "Issuer": "gameguild",
  "LocalNetworkName": "local"
}
```

| Option | Default | Meaning |
| --- | --- | --- |
| `Provider` | `none` | `none` = disabled no-op (safe default); `local` = deterministic local/dev provider. Unknown values fail startup (fail closed). |
| `Issuer` | `gameguild` | Issuer identity embedded in the canonical payload. Organization identity, never a person. |
| `LocalNetworkName` | `local` | Network label recorded on anchors produced by the local provider. |

Activation is opt-in, following the same philosophy as `TenantAuthConfiguration.AllowWeb3Auth`:
existing deployments that do nothing keep exactly their previous behavior (`Provider: "none"`
registers `DisabledBlockchainCertificateService`, which persists and publishes nothing).

## What the local provider does

`GameGuild.Identity.Authentication.LocalBlockchainCertificateService` (`Provider: "local"`):

- Persists anchors via the existing `BlockchainCertificateAnchor` entity/table (EF configuration
  already existed; no schema change, no migration).
- Publishes **only a SHA-256 hash** of a canonicalized, PII-free payload. The canonical payload is
  a JSON object with exactly these keys, sorted:
  `id`, `issued-at`, `issuer`, `recipient-hash`, `version`.
  Unknown input fields — names, e-mails, any personal data — are dropped by the canonicalizer
  (`BlockchainCertificateCanonicalizer.CanonicalizeIssuancePayload`), which is the enforcement point
  for PII exclusion. The recipient is represented only by `recipient-hash` (SHA-256 digest).
- Is deterministic: transaction hashes and block numbers are derived from the canonical payload, so
  identical inputs produce identical anchors (idempotent anchoring and revocation) and results are
  reproducible in development and tests. It submits no transaction to any network.
- Treats **revocation as a second anchor record linking the original**: the original anchor row is
  marked revoked locally, and a second `BlockchainCertificateAnchor` row is appended whose
  `CertificateType` is `revocation:<original certificate hash>` and whose payload links back to the
  original anchor. The revocation *reason* stays in local storage only — it is never anchored.
- `VerifyCertificateAsync` resolves the **latest anchor state**: a certificate is valid only while
  no revocation anchor referencing it exists and the stored canonical payload still hashes to the
  anchored digest.

## On-chain verification of anchored digests

`ICertificateAnchoring.VerifyAnchoringAsync` (implemented by `BlockchainCertificateAnchoringAdapter`,
mirroring the anchoring call path) queries the anchoring backend to **prove an anchor**:

1. Recomputes the canonical SHA-256 digest exactly as the anchoring path did (same canonicalizer,
   same payload version, same issuer).
2. Loads the user's anchors (`GetUserCertificatesAsync`) and looks for a `learning-certificate`
   anchor whose `CertificateHash` equals the recomputed digest.
3. Confirms the chain record with the platform's `VerifyCertificateAsync(digest, transactionHash)`
   — payload integrity, latest revocation state and anchor validity.

The result is fail-closed and reported as one of:

| Status | Meaning |
| --- | --- |
| `Verified` | Digest matches an anchor and the chain record corroborates it (network, transaction hash, block number and anchor time returned as evidence). |
| `DigestMismatch` | An anchor claims this certificate but its recorded digest differs from the recomputed one (or the chain record failed to corroborate it). |
| `Revoked` | An anchor exists but a revocation record invalidates it. |
| `NotAnchored` | No anchor exists for the recomputed digest (e.g. anchoring was disabled at issuance — `NoOpCertificateAnchoring` always reports this). |
| `Unreachable` | The anchoring backend could not be queried. Never reported as verified. |

Public endpoint: `GET /api/certificates/verify/{certificateNumber}/anchor` (anonymous, registered
in `AnonymousEndpointRegistry`). The response carries only PII-free evidence — digests,
transaction/network identifiers, block number and anchor timestamp; recipient names and revocation
reasons are never included.

## Wiring

- Learning.Certificates (domain module) exposes the port `ICertificateAnchoring` with a default
  no-op (`NoOpCertificateAnchoring`) registered by `AddCertificatesModule`. `CertificateService`
  invokes the port after issuance and after revocation; anchoring failures are logged and never
  break the certificate lifecycle.
- The API host bridges the port to the platform service via
  `GameGuild.API.Core.Integration.BlockchainCertificateAnchoringAdapter`, registered in
  `ApiProductComposition` **only when the provider is not `none`**. The adapter reduces the
  recipient name to a digest before anything leaves the module.

## Explicit EVM host configuration

`ApiProductComposition` calls `AddCertificateEvmHost` after the certificate module. An absent or false
`BlockchainCertificates:Evm:Enabled` registers no EVM key source, transport, journal or worker. Invalid
enabled settings fail before those services are registered. Settings are captured once; a reload
cannot silently retarget existing operations. One publication profile is supported per host.

Keep the legacy `Provider` at `none` when enabling EVM. Selecting `local` and EVM together is rejected.
Legacy `Certificate.BlockchainTxHash` and local-provider hashes are not asynchronous EVM operation IDs
or receipts. A committed certificate/outbox intent does not imply that a transaction was submitted or
finalized.

The following is an **inactive template**, not a deployed/funded network. Supply the actual registry
address, public bytes32 issuer identifier, SHA-256 of the deployed runtime (including immutables),
HTTPS RPC endpoint and environment key reference before activating it. Never commit a key value or
credential-bearing RPC endpoint. Only the final certificate digest and contract operation are sent
on chain; owner/tenant IDs and revocation reasons remain off chain.

```json
"BlockchainCertificates": {
  "Provider": "none",
  "Evm": {
    "Enabled": false,
    "Profile": "sepolia-certificates-v1",
    "CanonicalIssuer": "gameguild-testnet",
    "Network": {
      "ChainId": 11155111,
      "RpcEndpoint": "",
      "ContractAddress": "",
      "IssuerId": "",
      "ExpectedRuntimeCodeSha256": "",
      "FinalityMode": "Finalized",
      "MinimumConfirmations": 0,
      "RequestTimeout": "00:00:10",
      "MaximumResponseBytes": 1048576
    },
    "SignerReference": "certificates-testnet-v1",
    "PrivateKeyEnvironmentVariable": "GAMEGUILD_CERTIFICATE_TESTNET_KEY",
    "GasLimit": 200000,
    "MaximumFeePerGasWei": "30000000000",
    "MaximumTransactionFeeWei": "6000000000000000",
    "InitialFeePerGasWei": "20000000000",
    "InitialPriorityFeePerGasWei": "1000000000",
    "CheckDelay": "00:00:30",
    "LeaseDuration": "00:02:00",
    "ReplacementDelay": "00:02:00",
    "MaximumAttempts": 3
  }
}
```

The sample fee values are explicit **caps, not gas-price or funding estimates**. The worker preserves
each operation's original caps and nonce. Replacements must fit that budget; exhausted attempts,
unavailable keys or profile mismatches remain visible for operator review. The software key adapter
is opt-in and reads only its allowlisted process environment variable when signing is required.
Registration does not read the key or contact RPC. A missing key cannot be represented as successful
publication. Sepolia requires `Finalized`; there is no confirmation-count fallback. Sepolia is the
proposed first public network because Ethereum recommends it for application testing:
[Ethereum networks](https://ethereum.org/developers/docs/networks/#sepolia).

### Database startup and roles

The EVM journal remains an independent four-table model with its own
`gameguild.authentication.__EF_EvmCertificateJournal` history. When the existing database startup
initializer runs, its EVM migration prerequisite uses that initializer's **migration connection**;
the runtime factory uses the normal **runtime connection**. Existing production migration-role
validation and post-migration grants remain in effect. No elevated connection is registered with the
publication worker. Operators must provision the runtime/migration roles and database as documented
by the application's startup configuration.

If startup initialization is disabled, operators must apply the dedicated journal migration and grant
its runtime permissions before starting the enabled host. A lifecycle readiness check reads the
dedicated history and all four tables, without performing DDL. An absent, unexpected or unreadable
schema aborts host startup before any hosted service starts, including the outbox and publisher.
Database/provider error details are removed from EVM startup failures. Default `none`/`local`
deployments do not register this check or apply these journal migrations.

## Remaining operations and acceptance

The EVM protocol, constrained signer, durable journal and worker have separate implementation
evidence; configuration alone does not establish deployment or acceptance. Remaining gates are:

1. **Public testnet deployment** — provision the proposed Sepolia profile with its actual deployed
   registry, authorized issuer/signer and pinned runtime-code digest. No mainnet deployment is implied.
2. **Key custody and funding** — supply a separate testnet signer environment reference, fund its
   address and approve explicit spending caps. Secret values do not belong in issues or source files.
3. **Authenticated status and fresh proof** — the public digest-verification surface above covers
   the legacy/local providers; owner/tenant-scoped asynchronous EVM state and canonical chain proof
   (without leaking endpoints, keys or raw transactions) remain open.
4. **Operator recovery** — accept original-profile availability, quarantined nonce gaps and discovery
   after journal loss without inventing a new original identity or receipt.
5. **External evidence** — record actual anchor/revoke transactions and finalized block/state proof.
   Private Anvil fixtures and local hashes are not public-testnet acceptance.

`GenerateVerifiableCredentialAsync` remains a separate unsupported operation. The learning-certificate
EVM pipeline does not claim to implement W3C credential issuance.

## Verification (local mode)

```sql
-- issuance anchors
SELECT "certificate_type", "certificate_hash", "transaction_hash", "blockchain_network", "anchored_at"
FROM gameguild.authentication.blockchaincertificateanchor
WHERE "certificate_type" = 'learning-certificate';

-- revocation anchors (link back to the revoked hash)
SELECT "certificate_type", "certificate_hash", "transaction_hash"
FROM gameguild.authentication.blockchaincertificateanchor
WHERE "certificate_type" LIKE 'revocation:%';
```
