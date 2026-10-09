# Authentication configuration

The API binds the `Mfa`, `Session`, and `ThreatIntelligence` sections when the Authentication module is registered. All sections are validated at startup; invalid values stop application startup with a configuration error. Values can come from `appsettings.json`, environment-specific files, environment variables, or another ASP.NET Core configuration provider.

## Example

```json
{
  "Mfa": {
    "Enabled": true,
    "RequireMfaByDefault": false,
    "MaxFailedAttempts": 5,
    "LockoutDurationMinutes": 15,
    "BackupCodesCount": 10,
    "BackupCodeLength": 8,
    "TotpIssuer": "GameGuild",
    "TotpTimeStepSeconds": 30,
    "TotpClockSkew": 1,
    "SetupSessionDurationMinutes": 10
  },
  "Session": {
    "IdleTimeoutMinutes": 30,
    "AbsoluteTimeoutMinutes": 1440,
    "MaxConcurrentSessions": 5,
    "TrustedDeviceDurationDays": 30,
    "MaxTrustedDevices": 10,
    "TerminateSessionsOnPasswordChange": true,
    "TerminateSessionsOnMfaDisable": true,
    "EnableDeviceFingerprinting": true,
    "EnableLocationTracking": true
  }
}
```

## MFA settings

| Setting | Runtime behavior |
| --- | --- |
| `Enabled` | Enables or disables TOTP and backup-code setup and verification. When disabled, MFA status and policy queries report that MFA is not required. |
| `RequireMfaByDefault` | Makes the MFA policy service require MFA by default for users when MFA is enabled. Elevated roles also require MFA. Automatic enforcement in every sign-in flow remains a separate integration requirement. |
| `MaxFailedAttempts`, `LockoutDurationMinutes` | Set the failed-code threshold and lockout duration for TOTP and backup-code verification. |
| `BackupCodesCount`, `BackupCodeLength` | Set how many codes setup produces and the length of each generated backup code. Codes are returned once and stored as hashes. |
| `TotpIssuer`, `TotpTimeStepSeconds`, `TotpClockSkew` | Set the authenticator-app issuer, the `otpauth` period, and the accepted time-step window. |
| `SetupSessionDurationMinutes` | Expires an unfinished TOTP setup. A late verification clears its pending secret and backup codes. |

## Session settings

| Setting | Runtime behavior |
| --- | --- |
| `IdleTimeoutMinutes` | Rejects a session that has had no activity for this long. |
| `AbsoluteTimeoutMinutes` | Caps session expiration from creation, including when a session is refreshed. |
| `MaxConcurrentSessions` | Deactivates the least recently used sessions before creating a session above this limit. |
| `TrustedDeviceDurationDays`, `MaxTrustedDevices` | Set the trusted-device lifetime and maximum active device count. Re-trusting a device renews its expiry. |
| `TerminateSessionsOnPasswordChange` | Terminates the user's sessions after a password change or reset. |
| `TerminateSessionsOnMfaDisable` | Terminates the user's sessions when MFA is disabled. |
| `EnableDeviceFingerprinting` | Controls fingerprint generation and trusted-device operations. When disabled, new sessions store no fingerprint. |
| `EnableLocationTracking` | Controls whether the session stores the supplied IP address; when disabled it stores `unknown`. |

`RequireTrustedDeviceForSensitiveOps` is present in the options model but is not yet enforced by sensitive-operation authorization. Do not rely on it as a security control until that integration is implemented.

This configuration documents the MFA and session runtime wiring only. Scheme registration, typed
authentication options, and provider configuration are covered in
[Authentication options configuration](../../../docs/api/authentication-options-configuration.md), and moving
an existing authentication system onto this platform — service and endpoint mapping, account and
credential migration, configuration relocation, and staged cutover — is covered in the
[Authentication migration guide](../../../docs/api/authentication-migration-guide.md).

## Threat intelligence settings

The `ThreatIntelligence` section configures credential-stuffing threat intelligence used by the login risk analysis (`ILoginAttemptAnalysisService.AnalyzeLoginAttemptAsync`). The safe default ships a **local-file provider**: the operator supplies a feed file, and the platform makes **zero external calls**. An unreadable or missing feed **fails open** (no matches are reported, sign-in is unaffected) and emits a security event through the central security event pipeline.

```json
{
  "ThreatIntelligence": {
    "Provider": "LocalFile",
    "EnforcementMode": "Observation",
    "MaliciousIpRiskScore": 60,
    "BreachedPasswordRiskScore": 60,
    "LocalFile": {
      "FilePath": "config/threat-intelligence.json",
      "ReloadInterval": "00:05:00",
      "ReloadOnFileChange": true
    }
  }
}
```

| Setting | Runtime behavior |
| --- | --- |
| `Provider` | `LocalFile` (default) or `None` (disables threat intelligence). Any other value stops startup. External feed providers are deliberately deferred behind this switch. |
| `EnforcementMode` | `Observation` (default): matches are logged as detected anomalies and security events but do not raise the risk score — this guards against false positives while feed quality is validated. `Enforce`: matches raise the risk score and flow into the existing step-up machinery (a score of 60 or more from a single match triggers step-up). |
| `MaliciousIpRiskScore`, `BreachedPasswordRiskScore` | Risk-score bump applied per matching signal in `Enforce` mode (0–100). |
| `LocalFile:FilePath` | Operator-supplied feed JSON (see below). Relative paths resolve against the content root. |
| `LocalFile:ReloadInterval` | Minimum time between feed reload attempts (`TimeSpan` string). |
| `LocalFile:ReloadOnFileChange` | Additionally reload whenever the feed file's last-write time changes. |

### Local feed file format

The feed is a single JSON document with two optional arrays:

```json
{
  "maliciousIpCidrs": [
    "203.0.113.0/24",
    "198.51.100.7/32",
    "2001:db8::/32"
  ],
  "breachedPasswordSha256": [
    "ef92b778bafe771e89245b89ecbc08a44a4e166c06659911881f383d4473e94f",
    "5e884898da28047151d0e56f8dc6292773603d0d6aabbdd62a11ef721d1542d8"
  ]
}
```

- `maliciousIpCidrs`: IPv4 and IPv6 CIDR blocks. Host bits set in an entry are masked off (`203.0.113.37/24` behaves like `203.0.113.0/24`). Invalid entries are skipped with a warning; the rest of the feed still loads.
- `breachedPasswordSha256`: **full** SHA-256 hex digests of known-breached passwords (for example from a licensed corpus export). Only full digests are supported — never plaintext passwords and never k-anonymized prefixes. Comparison is case-insensitive. The submitted password's digest is computed in memory for comparison only; it is never persisted or logged.

Behavior when the feed is missing, unreadable, or unparseable: checks fail open (no match), the last successfully loaded feed keeps being served while retries fail, and one security event per outage is emitted through `Authentication.ThreatIntelligenceFeedUnavailable`. A feed outage never blocks or slows a decision to lock out users: this signal is defense-in-depth.
