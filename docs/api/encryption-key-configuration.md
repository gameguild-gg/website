# Encryption key configuration

Credentials that the API persists in recoverable form — TOTP MFA secrets and other
authentication material handled by `EncryptionService` — are encrypted with AES-256-GCM.
The AES key is derived from configured key material with HKDF-SHA256; no key is compiled
into the application.

Two configuration settings supply the key material, with the same precedence as the host
startup guard:

1. `Encryption:EncryptionKey` (primary)
2. `Encryption:Key` (accepted alias)

```json
{
  "Encryption": {
    "EncryptionKey": "<secret with at least 32 bytes of key material>"
  }
}
```

## Fail-closed behavior

`EncryptionService` validates the resolved key on every encrypt/decrypt operation:

- **Missing or whitespace-only key** — `InvalidOperationException`. The service never
  falls back to a built-in or shared key.
- **Key shorter than 32 bytes** — `InvalidOperationException`. Weak key material is
  rejected instead of silently stretching a short secret.

The API host additionally validates the setting at startup through
`OperationalStartupConfiguration`: outside the Development, Test and Testing environments,
startup fails when the encryption key is missing, shorter than 32 characters, or still set
to a `CHANGE_THIS` placeholder. `appsettings.json` ships such a placeholder so local
development works without extra setup; every other environment must override it.

Supply the production key through the deployment secret store or the .NET environment
configuration provider, for example:

```text
Encryption__EncryptionKey=<deployment-secret-with-at-least-32-bytes>
```

A suitable key can be generated with:

```bash
openssl rand -base64 32
```

## Deployment implications

- **Store the key in a secret manager** (for example AWS Secrets Manager, Azure Key Vault
  or the platform's deployment secret store). Do not commit production keys, do not log
  them, and do not pass them on the command line.
- **Losing the key makes existing ciphertext unreadable.** Data encrypted with a previous
  key cannot be recovered without that key; users whose MFA secret was encrypted with it
  must re-enroll MFA. Keep backups of the key according to the platform's key-management
  policy.
- **Rotating the key invalidates existing ciphertext.** Plan a rotation window: records
  written under the old key become undecryptable after the switch. MFA recovery codes are
  the supported user path back in; affected users re-enroll.
- **Records encrypted by the removed built-in fallback key must be migrated.** Earlier
  builds encrypted credentials with a shared hard-coded key when no key was configured.
  If such records exist, set the encryption key to that former value first, decrypt and
  re-encrypt persisted secrets with the production key (or have affected users re-enroll
  MFA) before rotating.
- **Keep the key stable across replicas.** All API instances must resolve the same
  encryption key, otherwise ciphertext written by one replica cannot be read by another.

The related `Encryption` section settings (`Algorithm`, `CipherMode`, `PaddingMode`,
`EnableKeyRotation`, `KeyRotationIntervalDays`, `PreviousKeys`) document the intended key
lifecycle but are not consumed by `EncryptionService`; the effective algorithm is always
AES-256-GCM with a fresh 96-bit nonce per operation, and the effective derivation is
HKDF-SHA256.

## Verification

`GameGuild.Identity.Authentication.UnitTests` covers the key policy:

- `EncryptionServiceKeyPolicyTests` — missing, whitespace and short keys fail closed on
  both `Encrypt` and `Decrypt`; the 32-byte boundary is accepted; both setting names are
  honored with the documented precedence.
- `GenerateSecureRandomString` selection is uniform over the alphanumeric alphabet
  (`RandomNumberGenerator.GetItems`), with no modulo bias.
