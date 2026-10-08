# Policy Bundle Signatures (Central Policy Registry)

Signed policy bundles let the central policy registry ship authorization policies whose
integrity is cryptographically verifiable. A bundle that is not signed by a trusted key — or
whose content changed after signing — can never be approved, deployed or served to the
dynamic authorization policy provider.

## Signing contract

| Property | Value |
|---|---|
| Algorithm | ECDSA P-256 with SHA-256 (`ES256`) |
| Signature format | P1363 (IEEE `r`\|\|`s`, 64 bytes), base64 in the envelope |
| Content hash | SHA-256 (hex) over the canonical signed payload |
| Envelope | Versioned JSON stored in `PolicyBundle.DigitalSignature` |

The canonical signed payload binds, in a fixed property order with a shared serializer:

- bundle identity (`Id`, `Name`) and semantic `Version`,
- tenant/global scope (`IsGlobal` / `TenantId`),
- bundle type, `PolicyData` and `Metadata`,
- version lineage (`PreviousVersionId`),
- effective dates (`EffectiveFrom` / `EffectiveUntil`),
- creator (`CreatedBy`).

The signature additionally binds the signer **key id** and the **signing time**, so a
signature cannot be replayed as a different key or bundle. Envelope versions other than `1`
and algorithms other than `ES256` fail closed during verification.

## Input validation (fail closed)

`IPolicyBundleSignatureService.ValidateBundleInputs` rejects a bundle when:

- `PolicyData` or `Metadata` is not valid JSON,
- any JSON object contains **duplicate property names**,
- the scope is inconsistent (global bundle with a tenant id, or tenant bundle without one),
- `EffectiveFrom >= EffectiveUntil`,
- combined `PolicyData` + `Metadata` size exceeds `MaxBundleSizeBytes` (default 1 MiB),
- `Name`, `Version` or `CreatedBy` is missing.

Invalid inputs cannot be signed, and verification of a signed bundle whose current content
violates the contract fails.

## Trusted keys, rotation and revocation

Trusted keys are configured under `Authorization:PolicyBundleSigning` (see
`PolicyBundleSigningOptions`):

```json
{
  "Authorization": {
    "PolicyBundleSigning": {
      "EnforceSignatureVerification": true,
      "MaxBundleSizeBytes": 1048576,
      "TrustedKeys": [
        {
          "KeyId": "policy-signing-2026-q3",
          "PublicKeyPem": "-----BEGIN PUBLIC KEY-----\n...\n-----END PUBLIC KEY-----",
          "NotBefore": "2026-07-01T00:00:00Z"
        },
        {
          "KeyId": "policy-signing-2026-q4",
          "PublicKeyPem": "-----BEGIN PUBLIC KEY-----\n...\n-----END PUBLIC KEY-----",
          "NotBefore": "2026-10-01T00:00:00Z"
        }
      ],
      "ActiveKey": {
        "KeyId": "policy-signing-2026-q4",
        "PrivateKeyPem": "-----BEGIN PRIVATE KEY-----\n...\n-----END PRIVATE KEY-----"
      }
    }
  }
}
```

- **Rotation:** several trusted keys may coexist. Each signature records the key that
  produced it; verification uses that key, so bundles signed before a rotation keep
  verifying while new signatures use the new active key.
- **Validity windows:** `NotBefore` / `NotAfter` are evaluated against the recorded signing
  time.
- **Immediate revocation:** setting `RevokedAt` (to a past or current instant) makes every
  verification with that key fail immediately, regardless of when the signature was made.
- The **active private key** must match one of the trusted public keys, otherwise signing
  fails closed.

`EnforceSignatureVerification` defaults to **true**. Disabling it is a local-development
escape hatch only; in any shared environment published bundles without valid signatures are
unreadable.

## Lifecycle (guards)

| Operation | Who | Signature requirement |
|---|---|---|
| Create draft | System admin (any scope) or tenant admin (own tenant) | Inputs must pass contract validation |
| **Sign** | **System admin only** | Active key trusted and in its validity window |
| Approve | System admin only | Signature must verify (fail closed) |
| Deploy | System admin only | Bundle `Approved` **and** signature must verify again (fail closed) |
| Rollback deployment | System admin only | — |

Every operation writes a `PolicyRegistryAuditLog` entry (`Create`, `Sign`, `Approve`,
`Deploy`, `Rollback`).

## HTTP API

`api/v{version}/authorization/policy-bundles` (authenticated, hidden from the generated
OpenAPI client):

- `POST /` — create draft bundle
- `GET /?tenantId=` — list bundles for a scope
- `POST /{bundleId}:sign` — sign (system admin)
- `POST /{bundleId}:approve` — approve (system admin, fail-closed verification)
- `POST /{bundleId}:deploy` — deploy (system admin; body `{ "environment": "Production" }`)
- `POST /deployments/{deploymentId}:rollback` — roll back (body `{ "reason": "..." }`)

## Provider integration

Deploying a bundle materializes its `PolicyData` (a JSON array of policy definitions:
`policyName`, `requiredPermissions`, `requiredRoles`, `requireAuthentication`,
`requireAccessControlListAccess`, `resourceType`, `minimumAccessLevel`) into
`PolicyDefinitions` rows annotated with `policy-bundle:{bundleId}`. Each definition's
`PolicyVersion` is bumped and the tenant (plus global) security versions are incremented so
the `DbAuthorizationPolicyProvider`'s version-keyed caches invalidate immediately. Rolling
back a deployment removes exactly the definitions carrying that bundle's marker.

Bundle `PolicyData` example:

```json
[
  {
    "policyName": "content.approve",
    "requireAuthentication": true,
    "requiredPermissions": ["content:write"],
    "requireAccessControlListAccess": true,
    "resourceType": "Content"
  }
]
```

## Provisioning production keys

1. Generate a P-256 key pair and register the **public** key in `TrustedKeys` with a stable
   `KeyId` (include the quarter/date so rotation is obvious).
2. Store the private key in the secret store available to operators with the system-admin
   signing duty; configure `ActiveKey` only where signing is performed.
3. Rotate by adding the next public key, switching `ActiveKey`, and later setting
   `RevokedAt` on the retired key only after every bundle it signed has been re-signed or
   retired.
