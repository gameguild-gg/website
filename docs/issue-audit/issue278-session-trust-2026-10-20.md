# Issue 278: trusted devices and session identity

## Original requirements

The original issue retains 32 functional checklist items, 6 technical requirements,
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

`TrustedDeviceSessionPostgreSqlHttpTests` defines 18 cases through the actual
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

## Hosted diagnosis and repair candidate

The unchanged-production candidate 9c7814a2 completed hosted integration execution:
688 existing cases passed. Of the 18 new results, one demonstrates a product defect:
with fingerprinting disabled, the configuration assertion and POST 400 pass, but
GET sessions still returns the old true trust flag (line 197). The other 17 stop
at the fixture's duplicate tenant name and do not qualify product behavior.
The fixture now uses a unique tenant name. Evidence:
[API job 39451266705/118726620353](https://github.com/gameguild-gg/gameguild/actions/runs/39451266705/job/118726620353).
The actual tested checkout was e1168d7eeced869e93724514ef80045b2c5eaceb, tree
126ac10c033e27ef89fea3ee634e6686a4462f4c, with develop 4acb0d44 and source 9c7814a2 parents.

The correction candidate changes the existing command to receive the authenticated
session ID. Inside its handler, the session must exist, belong to the user, remain
valid and unterminated, and contain a persisted fingerprint. The session projection
uses current owner-scoped active/unexpired trusted records and the configured
fingerprinting policy. Transport metadata and the legacy session flag do not choose
trust. Public route/request/response shapes remain unchanged; the existing Web
action sends an empty display name when omitted, matching the API's non-null default.

There are 12 new handler guard cases and 3 server-action payload unit cases. The
existing hosted browser harness adds 4 trust scenarios through the real security
page, encrypted cookie, Next server actions, Kestrel and migrated PostgreSQL.
It seeds expired own trust and active foreign trust with the same fingerprint,
renews/revokes using the existing controls, checks the session badge and reload,
and independently reads the final records from a fresh database context. The
original 7 session-state scenarios remain required. Next runs in development mode;
this does not assert E2E acceptance of a production build.

This is a correction candidate pending matching-source CI, not an accepted delivery.
Issue 278 is reopened on the one qualified native defect. The full original scope
remains pending; no new closure or full recertification is claimed.
