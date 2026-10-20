# Issue278: trusted devices and session identity

## Original requirements

The original issue retains32 functional checklist items,6 technical requirements,
and the explicit IP reputation, VPN/proxy detection and malicious-IP blocking
requirements in its prose. Completing a subset does not close the whole issue.

This increment addresses the existing requirements to trust the current device,
show that trust in the session list, and revoke it without affecting another owner.
The initial source review found two disconnected boundaries:

- Trust creation hashes the API request's IP/User-Agent, while login persists
  another fingerprint supplied during authentication or generated for that session.
  A server action can also have different transport metadata from the browser.
- Session listing reads a legacy session flag. Trust creation/revocation updates
  the separate trusted-device records and does not maintain that flag. Expiry and
  revocation therefore cannot reliably drive the session badge.

## Regression candidate

`TrustedDeviceSessionPostgreSqlHttpTests` defines18 cases through the actual
HTTP routes, registered JWT handler/services and migrated PostgreSQL. No
authentication, command, session or trusted-device collaborator is replaced.

| Required behavior | Cases |
| --- | --- |
| Trust uses the signed session's persisted fingerprint; conflicting body/query/header/transport identifiers are ignored | Named and omitted display name |
| Listing derives current trust from owner-scoped records, even when the legacy flag says the opposite | Active, expired, revoked, foreign owner |
| Revocation immediately changes the projection, preserving sessions and another owner's same fingerprint | Fresh database and HTTP assertions |
| Missing session binding cannot invent a device identity | Sessionless bearer |
| Authentication required | Anonymous caller |
| Invalid signed session binding denied | Missing, foreign owner, expired, terminated |
| Empty persisted fingerprint rejected | Bound session without a device identity |
| Disabled fingerprinting does not create or project trust | Policy-configured real-authentication host |
| Existing trust renewed instead of creating another identity | Expired and revoked record |
| Other owner's trust cannot be revoked | Same fingerprint across owners |

This first candidate adds regression definitions while preserving production
source, to obtain hosted evidence of the current failures before correction.
It has not been compiled or executed locally. No passing native result,
implementation acceptance or issue closure is claimed from source inspection.

## Remaining acceptance

After correction, require all current-source CI and these native cases to pass.
Then exercise the actual security settings page and server action, verifying the
existing trust control and badge through create/revoke/expiry behavior.

Separate prepared password-revocation and configured MaxMind acceptance cases
remain outside this first regression candidate. Other issue278 requirements,
including the explicit reputation/VPN/proxy/blocking scope, remain to be reconciled.
