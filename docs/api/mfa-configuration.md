# Multi-Factor Authentication (MFA) configuration and recovery

Covers the MFA surface implemented for issues #244 (SMS OTP, method management, metrics),
#234 (distributed WebAuthn challenge store), #242 (login-forensics geolocation) and #259
(push approval challenges, v1).

## Factors

| Factor | Enrollment | Verification |
|---|---|---|
| Authenticator app (TOTP) | `POST /v1/auth/mfa/totp:setup` → `totp:complete` | `POST /v1/auth/mfa/verify` with `Method=Totp` |
| SMS OTP | `POST /v1/auth/mfa/sms:setup` → `sms:complete` | `sms:challenge` then `verify` with `Method=Sms` |
| Email OTP | Existing email-code flow | `verify` with `Method=Email` |
| Push approval (v1) | `POST /v1/auth/mfa/push:register-device` | `push:challenge` → device `push:respond` → `verify` with `Method=Push` |
| Recovery codes | Generated at TOTP enrollment or via `backup-codes:regenerate` | `verify` with `Method=BackupCode` |

## SMS delivery

SMS codes are delivered through the Notifications infrastructure as transactional
`SmsMfaCode` rows on the SMS channel; the `SmsDispatcherService` background sweep delivers
them through the configured provider (claim → send → retry/backoff → staleness → deadletter).

The provider port (`ISmsSender`, SharedKernel) defaults to a fail-closed no-op: with no
provider configured, rows retry and deadletter with an observable reason — codes are never
silently dropped. Configure the Twilio REST adapter in the API host:

```json
{
  "SmsDelivery": {
    "Enabled": true,
    "Provider": "Twilio",
    "From": "+15550000000",
    "Twilio": { "AccountSid": "...", "AuthToken": "..." }
  }
}
```

Codes are six cryptographically random digits, stored only as SHA-256 digests, single-use,
valid ten minutes, five verification attempts maximum, one request per user per minute.

## Push approval challenges (v1)

Register the device once (`push:register-device`, authenticated). A sign-in flow issues a
challenge (`push:challenge`), the enrolled device answers (`push:respond`, approve/deny),
and verification consumes an approved, unexpired challenge. Challenge state lives in
`IDistributedCache` (Redis in production) with a 120-second TTL; unanswered challenges time
out and the verification response carries an explicit fallback-to-TOTP signal. Push delivery
rides the Notifications push channel through the `IPushSender` port (FCM HTTP v1 adapter,
config-gated with the same fail-closed no-op default under `PushDelivery`).

## Recovery flow

1. Recovery codes are issued when TOTP is enrolled (shown in the enrollment response) and can
   be regenerated any time from **Settings → Security → Multi-factor authentication**
   (`POST /v1/auth/mfa/backup-codes:regenerate`). Regeneration invalidates every previous code.
2. Codes are displayed exactly once — the API never returns stored codes. Store them offline.
3. At sign-in, any unused code verifies via `POST /v1/auth/mfa/verify` with `Method=BackupCode`.
   Each code is single-use; consumption is concurrency-safe (EF concurrency tokens + retry).
4. The status endpoint (`GET /v1/auth/mfa/backup-codes`) reports remaining/used counts; when
   counts are unknown for legacy sets the response says so rather than guessing.

## Disabling MFA

`POST /v1/auth/mfa:disable` requires the **account password** (not an MFA code). Passwordless
(OAuth-only) accounts cannot disable MFA through this endpoint. Disabling removes every
enrolled factor and invalidates all recovery codes; sessions are terminated when
`Session:TerminateSessionsOnMfaDisable` is set.

## WebAuthn challenge store

WebAuthn ceremony sessions (registration and assertion) live in `IDistributedCache` — the same
pattern as the token revocation store — keyed by user (plus a single guest slot for
discoverable-credential flows), with a five-minute TTL enforced against the domain clock.
Ceremonies started on one instance complete on any other.

## Login-forensics geolocation

When `Geolocation:MaxMind:DatabasePath` points at a GeoLite2-City database, every recorded
login attempt is enriched with `Country`, `City` and a display `Location`, and the SIEM audit
event carries the same fields. Missing configuration or database, private/loopback addresses
and lookup failures all resolve to Unknown — enrichment never blocks the attempt.

```json
{
  "Geolocation": { "MaxMind": { "DatabasePath": "/var/geoip/GeoLite2-City.mmdb" } }
}
```

## Metrics

OpenTelemetry counters under the meter `GameGuild.Identity.Authentication.Mfa`:

- `authentication.mfa.enrollments` — dimensions `method`, `outcome`
  (`started`, `enrolled`, `verify-failed`, `disabled`, `disable-refused`, `device-registered`)
- `authentication.mfa.verifications` — dimensions `method`, `outcome`
  (`succeeded`, `failed`, `timeout`, `approved`, `denied`, `challenge-issued`, ...)

Adoption per method is the `enrolled` outcome series of the enrollment counter.
