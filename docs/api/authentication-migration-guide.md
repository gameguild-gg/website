# Authentication migration guide

This guide covers migration from scattered GameGuild authentication configuration
and adaptation of an existing authentication system to the current .NET API. It
addresses the migration-guide requirement of issue #145. Configuration reference:
[Authentication options configuration](authentication-options-configuration.md).

The steps below are a deployment procedure. Adding this document does not claim
that a production migration or an external-provider sandbox has been executed.
The MFA sign-in changes in PR #704 must pass the real instructor/learner Code
cycle before that candidate is integrated or deployed.

## 1. Record the source system and choose a compatibility window

Record the currently deployed API and consumer revisions, effective configuration
section names, enabled schemes, token issuer/audience/algorithm and lifetimes,
cookie name/scheme, OAuth redirect URIs and scopes, password-hash formats, MFA
enrollment state, session policy, and tenant/role identifiers. Keep secret values
in the deployment secret store; the migration record should contain references.

Take a recoverable database backup and preserve the current deployment/configuration
and any cookie Data Protection key ring before applying schema changes. Rehearse
restoration in the supported migration environment. Use the repository's normal
EF migration process; moving options does not require creating a second account
database or another ASP.NET Core Identity persistence stack.

Decide whether existing access tokens can remain valid until expiration. This is
possible only if their signature, claims, issuer/audience, current account/token
version, session and tenant checks all satisfy the new host. Otherwise require
fresh sign-in at cutover. Record the decision and its effect on clients; matching
only the signing secret does not establish compatibility.

## 2. Move configuration to the host's actual sections

The normal API host passes `PresentationLayer:Authentication` to
`SetupAuthentication`. Its overload without an options argument reads the root
`Authentication` section; that overload is for explicitly composed hosts. Adding
root settings alone does not replace the standard host's nested configuration.

| Existing setting or behavior | Destination in the standard host | Migration action |
| --- | --- | --- |
| `Jwt:Secret`, `Jwt:SecretKey`, `JwtSettings:SecretKey`, or root `Authentication:JwtSecretKey` | `PresentationLayer:Authentication:JwtSecretKey` | Preserve the current key through a secret-store reference when compatibility is required. Do not rely on a legacy fallback overriding nested options. |
| `Jwt:Issuer` / `JwtSettings:Issuer` | `PresentationLayer:Authentication:JwtIssuer` | Preserve the current issuer initially. |
| `Jwt:Audience` / `JwtSettings:Audience` | `PresentationLayer:Authentication:JwtAudience` | Preserve the audience expected by current consumers. |
| `Jwt:AccessTokenExpirationMinutes` / `JwtSettings:AccessTokenExpirationMinutes` | `PresentationLayer:Authentication:JwtExpiration` | Convert minutes to a .NET TimeSpan, e.g. 60 minutes to `01:00:00`. The host converts this back to whole minutes using ceiling. |
| Legacy refresh-token lifetime | `PresentationLayer:Authentication:RefreshTokenExpirationDays` | Preserve the intended lifetime in days; access-token and refresh-token expirations are separate. |
| Refresh sliding expiry, replay containment, clock skew, validation settings | Existing `Jwt` / `JwtSettings` fields read by `JwtOptionsResolver` | Keep these settings explicitly. Nested authentication options replace key/issuer/audience/access lifetime/refresh days, not every resolver field. |
| API-key header/query source | `PresentationLayer:Authentication:EnableApiKeyAuthentication`, `ApiKeyHeaderName`, `AllowApiKeyInQueryString`, `ApiKeyQueryStringParameterName` | Prefer the existing configured header. Leave query support off unless the integration requires it and uses HTTPS. |
| Custom API-key source | `AuthenticationOptions.ApiKeyCustomKeyResolver` in host code | Port the delegate in the composition root; it is not a JSON expression. Reject multiple simultaneous credential sources. |
| Legacy HTTP Basic | `PresentationLayer:Authentication:EnableBasicAuthentication` and `Basic` | Opt in only for the required endpoint/scheme and keep HTTPS. Basic does not bypass MFA. |
| Cookie authentication | `PresentationLayer:Authentication:EnableCookieAuthentication` and `Cookie` | Preserve intended name/scheme, expiration, sliding behavior and validated SameSite policy. The registered cookie is Secure and HttpOnly. |
| `OAuth:{provider}:ClientId/ClientSecret/Scopes/...` | `PresentationLayer:Authentication:ExternalProviders:Providers:{provider}` | Copy values into the typed provider entry, explicitly set `Enabled`, and verify provider redirect registration. |
| Password requirements | `PresentationLayer:Authentication:PasswordPolicy` | Preserve length/complexity requirements, then validate signup/reset/change with the target release. |
| MFA settings | Root `Mfa` | Preserve enrollment/backup-code policy and failed-attempt limits. Explicitly review `RequireMfaByDefault` rather than enabling it accidentally. |
| Session settings | Root `Session` | Preserve idle/absolute/concurrent-session limits and revocation rules. |
| Account/IP lockout and enumeration policy | Root `AuthenticationSecurity` | Preserve throttling thresholds, lockout duration and user-enumeration protection. |
| CSP and frame/content headers | `PresentationLayer:SecurityHeaders` | Preserve the intended policies. Production HSTS comes from the API pipeline. |
| Additional schemes | `configureAdditionalSchemes` callback of `SetupAuthentication` | Port registration in host code and retain explicit endpoint scheme selection; JWT bearer stays the default. |

For example, an installation currently issuing 60-minute access tokens and
30-day refresh tokens can move those non-secret values as follows. Replace the
issuer and audience with the recorded source values, rather than copying these
example names into a live environment.

```json
{
  "PresentationLayer": {
    "Authentication": {
      "EnableAuthentication": true,
      "EnableAuthorization": true,
      "JwtIssuer": "existing-issuer",
      "JwtAudience": "existing-audience",
      "JwtExpiration": "01:00:00",
      "RefreshTokenExpirationDays": 30,
      "EnableApiKeyAuthentication": false,
      "EnableBasicAuthentication": false,
      "EnableCookieAuthentication": false
    }
  },
  "Mfa": {
    "Enabled": true,
    "RequireMfaByDefault": false
  }
}
```

Supply the preserved signing key through
`PresentationLayer__Authentication__JwtSecretKey` in the deployment environment
or equivalent secret provider. Provider secrets use, for example,
`PresentationLayer__Authentication__ExternalProviders__Providers__google__ClientSecret`.
Account for environment-specific files and environment overrides when reviewing
the effective values. Startup validation must pass before admitting traffic;
never resolve a configuration failure by disabling authentication/authorization.

## 3. Preserve identities and adapt incompatible credential storage

For a configuration-only migration, keep existing account, credential, tenant,
membership, role, session and token-version rows. Do not reset identifiers or
recreate accounts: authorization, audit and billing references depend on them.

For another identity system, define a reviewed mapping from its stable subject
identifier to the GameGuild user ID and from its verified tenant/role membership
to current records. Preserve disabled/locked accounts and verification state.
Do not infer verified identity or elevated privileges from an unverified email
or client-supplied claim. OAuth identity association must preserve provider plus
provider subject; an email match alone is not migration proof.

The current local password verifier supports valid BCrypt hashes and this
implementation's versioned `pbkdf2-sha256$` representation. The latter uses the
format and parameters in `LongPasswordHash`; the prefix does not make arbitrary
PBKDF2 hashes compatible. BCrypt cannot authenticate an input exceeding 72 UTF-8
bytes. Do not truncate it or rehash an existing hash as if it were a password.

There is no universal legacy-account/hash importer in this guide. If the source
format or identity mapping is unsupported, keep cutover pending until a scoped
adapter has real compatibility tests, or use the normal verified password-reset
flow for affected users. Do not mark imported accounts verified or clear MFA to
make migration succeed. Retain password-history and revocation behavior when
mapping supported credentials.

Existing TOTP secrets, backup-code hashes, trusted-device state and encrypted
credential material require their compatible encryption/storage context. Preserve
the necessary deployment keys through the secret store and test verification.
Where compatibility cannot be established, use the supported recovery/enrollment
process rather than silently accepting an unenrolled account as fully authenticated.

## 4. Adapt tokens, sessions, consumers and providers

1. Validate claims against the current token service and public boundary. Current
   issuance uses `sub`, `role`, `token_version`, `auth_time`, `tenant_id` when
   present, and `sid` for session-bound issuance. The host disables inbound claim
   mapping and uses `sub`/`role` for name/role. Do not translate a legacy token
   into elevated or cross-tenant claims in the consumer.
2. Do not copy opaque external refresh tokens into GameGuild refresh-token rows.
   They require the current family/session/hash/revocation lifecycle. If the
   source lifecycle is incompatible, revoke it through that system and require
   fresh sign-in. Test rotation, reuse detection and the configured
   `RefreshTokenReplayContainmentScope` (`Family` or `Account`) before cutover.
3. Update consumers to use `AccessTokenExpiresAt` and `RefreshTokenExpiresAt`
   separately. `ExpiresAt` is a backward-compatible legacy field with historically
   conflated meaning; it is not the access-token expiration contract.
4. Treat `RequiresMfa` as a pending sign-in. Do not establish a normal session or
   store empty access/refresh values as authenticated credentials. Use
   `/v1/auth/mfa/sign-in/enrollment` when enrollment is required and
   `/v1/auth/mfa/sign-in/complete` to submit the supported proof, then install the
   credentials returned by successful completion. Preserve the client binding
   associated with the challenge. The authenticated `/v1/auth/mfa/verify` endpoint
   is not a substitute for completing a pending sign-in.
5. Cookie name/scheme changes or an incompatible Data Protection key ring require
   fresh cookie issuance. Do not assume an old cookie remains valid. Check the
   intended browser origin and SameSite behavior without weakening Secure/HttpOnly.
6. For each enabled OAuth provider, verify client ID/secret references, scopes,
   redirect URI, provider subject mapping and callback state validation. Complete
   a sandbox sign-in and rejection of invalid/replayed state before enabling that
   provider. Legacy fallback support in `OAuthService` does not prove the target
   typed configuration or callback registration is correct.
7. Keep JWT bearer as the default while adding an optional scheme. Test the actual
   endpoint's scheme selection, missing/invalid credentials, and MFA policy; merely
   registering Basic, cookie or API-key handlers does not select them for every route.

## 5. Rehearse acceptance before cutover

Run the checks in the configured migration/sandbox environment, following resource
coordination and repository test commands. This guide is not authorization to start
a local database during an active containment hold. Record the release SHA,
effective non-secret configuration, individual results and evidence links.

| Check | Required result | Existing regression surface |
| --- | --- | --- |
| Configuration and scheme registration | Target sections bind; malformed settings fail startup; default and optional schemes are correct. | API `SecurityServiceCollectionExtensionsTests`; SharedKernel configuration tests. |
| Existing-account credentials | Allowed credentials still work; unsupported/malformed hashes, disabled/locked accounts and incorrect passwords are rejected. | Authentication `PasswordHasherTests`, long-password and sign-in security tests. |
| JWT and tenant isolation | Correct claims and issuer/audience/lifetime work; invalid signature, stale token version, revoked session and another tenant are rejected. | JWT/session/authorization unit and PostgreSQL HTTP regressions. |
| Refresh lifecycle | Successful rotation replaces the old token; repeated use and revocation follow the configured family/account policy. | `RefreshTokenPostgreSqlFlowTests`, `RefreshTokenRotationPostgreSqlTests`, API refresh-token HTTP regressions. |
| MFA and recovery | Optional and required MFA, enrollment, TOTP and backup codes work; pending sign-in issues no normal credentials; expired/replayed/forged challenges fail. | API `MfaSignInCompletionSecurityPostgreSqlTests`, MFA policy and limited-enrollment HTTP tests. |
| OAuth | Each enabled provider completes the registered callback; invalid state and wrong subject association fail. | Provider unit/integration checks plus the configured provider sandbox. |
| Cookies, Basic and API keys | Intended endpoints authenticate the selected scheme over HTTPS; invalid/multiple key sources fail; browser cookie attributes are preserved. | Scheme-registration and handler tests plus the actual consuming integration. |
| Security policies and audit | Lockout/rate limits, signup/reset/change policy, headers and non-sensitive authentication audit events remain effective. | Authentication security and API header/audit tests. |
| Consumer workflows | Sign-in, required MFA, refresh, logout and protected navigation work for existing users. | Real instructor/learner coding assessment cycle and applicable browser workflows. |

Passing test definitions or historical CI links do not replace an executed rehearsal
of deployment-specific providers and migrated identities. Retain failed attempts
and unsupported cases as pending acceptance.

## 6. Cut over in stages and retain a safe rollback

Deploy the validated API and compatible consumers together to the rehearsal
environment first. Then direct a bounded subset of production traffic to the
candidate using the existing deployment mechanism. Observe authentication failure,
MFA completion, refresh reuse/revocation and authorization errors before expanding.
Do not log passwords, tokens, provider secrets or MFA codes while comparing results.

Remove obsolete configuration aliases only after every deployment and consumer
uses the target sections and the agreed compatibility window has elapsed. Retain
the reviewed mapping and migration evidence for future incident analysis.

If acceptance fails, stop expansion and return traffic to the preserved compatible
deployment/configuration. Preserve account/token versions, revoked credentials,
audit records and completed MFA changes; reverting a deployment must not resurrect
a revoked session or bypass an enforced policy. If the previous version cannot
read the new schema, follow the rehearsed schema rollback/recovery procedure with
the appropriate maintenance window. Do not improvise destructive SQL or restore
an old credential snapshot over active writes.

The migration is accepted only when the chosen identity/token compatibility path,
all enabled schemes, MFA/recovery, tenant authorization, consumers and rollback
rehearsal have recorded results. A migration-guide artifact satisfies the documentation
deliverable; it does not by itself close the complete authentication issue.

## Implementation references

- [Host scheme registration and JWT options projection](../../apps/api/Source/GameGuild.API/Core/Extensions/SecurityServiceCollectionExtensions.cs).
- [Presentation-layer host composition](../../apps/api/Source/GameGuild.API/Core/Setup/PresentationLayerExtensions.cs).
- [JWT legacy aliases and replay policy](../../apps/api/Source/Modules/GameGuild.SharedKernel/Configuration/ApplicationLayer/JwtOptionsResolver.cs).
- [Typed authentication options](../../apps/api/Source/Modules/GameGuild.SharedKernel/Configuration/PresentationLayer/Authentication/AuthenticationOptions.cs).
- [Credential verification](../../apps/api/Source/Modules/GameGuild.Identity.Authentication/Services/PasswordHasher.cs) and [versioned long-password format](../../apps/api/Source/Modules/GameGuild.Identity.Authentication/Services/LongPasswordHash.cs).
- [Token claims](../../apps/api/Source/Modules/GameGuild.Identity.Authentication/Services/JwtTokenService.cs) and [sign-in response contract](../../apps/api/Source/Modules/GameGuild.Identity.Authentication/DTOs/SignInResponse.cs).
- [Authentication and pending-MFA endpoints](../../apps/api/Source/Modules/GameGuild.Identity.Authentication/Controllers/AuthController.cs).
- [OAuth configuration and callback handling](../../apps/api/Source/Modules/GameGuild.Identity.Authentication/Services/OAuthService.cs).
- [Authentication/MFA evidence and outstanding acceptance](../architecture/authentication-mfa-policy-reconciliation.md).

## Source-system mapping supplement

[Source-system migration mappings](authentication-migration-source-systems.md) preserves the
ASP.NET Core Identity, NextAuth/Auth.js, raw JWT and credential-import guidance delivered
by PR #742. Use this guide for the current MFA challenge, session and token contracts.
No legacy-format importer, live cutover or provider sandbox execution is implied.
