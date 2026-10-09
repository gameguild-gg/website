# Blockchain certificate anchoring (safe-default configuration)

Implements the verifier-approved scope of issue #224: a **config-gated** provider abstraction for
anchoring certificates, **disabled by default**. No personal data is ever published, and no external
network is contacted by the shipped providers.

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

## Wiring

- Learning.Certificates (domain module) exposes the port `ICertificateAnchoring` with a default
  no-op (`NoOpCertificateAnchoring`) registered by `AddCertificatesModule`. `CertificateService`
  invokes the port after issuance and after revocation; anchoring failures are logged and never
  break the certificate lifecycle.
- The API host bridges the port to the platform service via
  `GameGuild.API.Core.Integration.BlockchainCertificateAnchoringAdapter`, registered in
  `ApiProductComposition` **only when the provider is not `none`**. The adapter reduces the
  recipient name to a digest before anything leaves the module.

## Deferred operations decisions (deliberately out of scope)

The following require explicit product/ops decisions before any real chain is used. The
configuration and persistence contracts above are designed so a future provider can be added
without breaking existing anchors:

1. **Target chain and test network** — which chain, which testnet, mainnet policy.
2. **Anchoring protocol** — on-chain contract/event format for publishing the digest.
3. **Issuer key custody** — who holds signing keys and how they are rotated (this also gates
   `GenerateVerifiableCredentialAsync`, which currently throws `NotSupportedException`).
4. **Transaction funding** — wallet management and gas budgeting.
5. **Revocation publication semantics** — whether revocations must also be on-chain for external
   verifiers, and with what finality/confirmation policy.

Until these are decided, `Provider: "local"` is the only enabled mode, and it is local-only by
construction (no RPC endpoints, no keys, no external calls).

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
