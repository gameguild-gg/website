# PII Redaction & Token Vault (issue #192)

Operational documentation for the PII redaction and tokenization pipeline that protects structured
audit logs. Implementation: `apps/api/Source/Modules/GameGuild.Compliance.Audit/Services/PiiRedaction/`.

## What the pipeline does

Configurable rules (`PiiRedactionRules` table) detect PII in audit records and structured-log
payloads and transform matches before persistence:

| Rule shape | Options |
|---|---|
| Pattern type | `regex` (custom organization pattern) or `preset`: `email`, `phone`, `national-id` (US SSN shape), `credit-card` (Luhn-validated) |
| Action | `redact` (replace with the rule replacement, default `[PII-REDACTED]`) or `tokenize` (replace with a deterministic token) |
| Scope | `field-name` (only the listed fields, e.g. `Description`, `Metadata`, `UserAgent`, `ResourceId`) or `global` (every scanned value) |
| Lifecycle | `enabled` flag, integer priority, per-tenant or platform-global (`TenantId` null) |

Rule application order: field-name rules before global rules, then ascending priority; the first
matching rule wins per value (no double transformation). Phone matches require 10–15 digits;
credit-card matches must pass the Luhn checksum — both validations exist to suppress false positives.

## Where the pipeline runs

1. **Central audit sink** — `AuditLogPiiRedactionInterceptor` (EF `SaveChanges` interceptor) applies
   tenant + global rules to every added/modified `AuditLog` entity across all write paths
   (AuditService, SecurityEventLogger command and retry paths, alert resolution). Token vault rows
   and `PiiRedactionOperations` trail rows are staged on the same `DbContext`, so record + vault +
   trail persist in one save and share the caller's transaction.
2. **Structured logging** — `PiiRedactionLogSink` wraps the NDJSON file sink configured in
   `SerilogExtensions` and rewrites string-valued log-event properties using the platform-global
   rules (tenant rules do not apply there: generic log events carry no tenant context). Structural
   properties (`Application`, `Environment`, `MachineName`, `SourceContext`, ...) are exempt.
3. **Durable fallbacks** — the critical-log fallback payload in `AuditService` and the local spool
   payloads in `SecurityEventLogger` are sanitized through the pipeline before serialization, so the
   failure paths cannot become the leak the pipeline exists to close.

## Fail-closed behavior

- **Rule store unavailable, no cached snapshot**: protectable fields are replaced wholesale with
  `[PII-REDACTED-FAILEDCLOSED]`, the failure is logged, and a `PiiRedactionOperations` row with
  `FailClosed = true` records the event. The audit record is never dropped and never written raw.
- **Stale-if-error**: a previously loaded rule snapshot keeps being served while refreshes fail;
  the pipeline only fails closed when no snapshot was ever loaded for the scope.
- **Per-value matcher failure** (e.g. regex timeout): the whole value is transformed rather than
  passed through.
- **Tokenizer without a configured key**: tokenization fails closed exactly like the platform
  `EncryptionService` — no key, no tokens (and vault writes are skipped with a warning).

## Token vault and key management

- **Tokens** are `pii_tok_v1_` + 16 bytes of `HMAC-SHA256(value, key)` in base64url. They are
  deterministic: the same value yields the same token across processes and restarts, which is what
  keeps redacted records joinable during investigations.
- **Key**: derived from the platform encryption configuration (`Encryption:EncryptionKey`, falling
  back to `Encryption:Key`) through the purpose string `gameguild:pii-tokenization:v1:`. The key is
  cached for 10 minutes and never logged; `GET /api/audit/redaction-pipeline` exposes only a
  non-reversible fingerprint for operational correlation.
- **Vault** (`PiiTokenVaultEntries` table): for every value tokenized through the audit sink, the
  token is mapped back to the original encrypted with the platform `IEncryptionService`
  (AES-256-GCM). Scope key is the tenant id (or `global`); `(ScopeKey, Token)` is unique.
- **Authorized detokenization**: `GET /api/audit/redaction-pipeline/tokens/{token}?reason=...`
  resolves a token for tenant administrators. A free-text investigation reason of at least 10
  characters is mandatory; every resolution writes a critical-risk audit entry recording the token,
  use count, and reason.
- **Key rotation caveat**: rotating `Encryption:EncryptionKey` changes both future tokens and the
  ability to decrypt existing vault rows. Rotations re-tokenize historical data under new tokens and
  leave old vault rows undecryptable; treat key rotation as a breaking compliance event and snapshot
  the vault beforehand.

## Administration

All endpoints are under `/api/audit/redaction-pipeline` (and the versioned
`/v1/audit/redaction-pipeline`), require `[Authorize]`, and additionally enforce an authenticated
tenant administrator with tenant context in the service layer (denials are audit-logged through the
permission-deny channel):

| Endpoint | Purpose |
|---|---|
| `GET` | Pipeline status: snapshot cache, enabled rule counts, vault size, last refresh error |
| `GET rules` / `GET rules/{id}` | List/read rules (tenant + inherited global rules) |
| `POST rules` / `PUT rules/{id}?expectedRevision=N` / `DELETE rules/{id}?expectedRevision=N` | Guarded, versioned (409 on stale revision), audited CRUD |
| `POST test` | Dry-run current rules against sample field values; persists nothing |
| `GET tokens/{token}?reason=...` | Authorized detokenization (see above) |

Every mutation writes an audit entry (`PiiRedactionRuleCreated`/`Updated`/`Deleted`,
`PiiTokenResolved`) and invalidates the rule cache so changes apply within one cache TTL (30 s) or
immediately after the mutation.

## Testing

`apps/api/tests/GameGuild.Audit.UnitTests/Services/PiiRedactionPipelineTests.cs` covers the rule
matching matrix (presets, custom regex, Luhn/digit-count validation, scope precedence, priority),
redact vs tokenize, deterministic tokenization + fail-closed tokenizer, audit-record application
including nested metadata JSON, vault encryption, fail-closed pipeline behavior, the EF interceptor
integration at the central audit sink, admin CRUD authorization/versioning/auditing, and the
structured-log sink (redaction and fail-closed modes).
