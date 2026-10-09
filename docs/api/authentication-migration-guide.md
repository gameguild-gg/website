# Authentication migration guide

This guide migrates an existing authentication system onto the GameGuild API: concept mapping
from common prior systems, account/credential data migration, configuration relocation, and a
staged cutover. Configuration syntax and the typed-options contract are documented separately in
[Authentication options configuration](authentication-options-configuration.md); MFA and session
runtime wiring is documented in [Authentication configuration](../../apps/api/docs/authentication-configuration.md).

This is a deployment procedure with the code support each step actually has. It does not claim
that a production migration, an external-provider sandbox exercise, or a legacy revocation has
been executed in this repository.

## 1. Inventory the source system first

Record, before changing anything: the deployed API and consumer revisions, enabled schemes,
token issuer/audience/algorithm/lifetimes, refresh-token storage and rotation semantics, cookie
name and Data Protection key ring, OAuth provider registrations (client IDs, redirect URIs,
scopes), password-hash formats and work factors, MFA enrollment state, session policy, and the
tenant/role vocabulary. Keep secret values in the deployment secret store; the inventory holds
references only.

Take a restorable database backup and preserve the current deployment, configuration and
Data Protection key ring. Rehearse restoration in the migration environment before touching
schema or traffic.

## 2. Map the source system onto GameGuild services

The authentication module is `GameGuild.Identity.Authentication`. The composite
`IAuthService` interface (`Abstractions/IAuthService.cs`) exposes the operations of
`ILocalAuthService` (sign-in, sign-up, refresh, revoke), `IOAuthAuthService`, `IPasswordService`
and `IWeb3AuthService`; individual services are resolvable directly. The mapping below uses the
REST surface clients normally consume.

| Concept in the source system | GameGuild implementation | Endpoint / entry point |
| --- | --- | --- |
| Register user | `LocalAuthService.LocalSignUpAsync` | `POST /v1/auth/sign-up` |
| Password sign-in | `LocalAuthService.LocalSignInAsync` | `POST /v1/auth/sign-in`; identifier-flexible variant `POST /v1/auth/polymorphic` (email, username or E.164 phone) |
| Issue access token | `JwtTokenService.GenerateAccessTokenAsync` — claims `sub`, `email`, `jti`, `iat`, `auth_time`, `token_version`, `role`, `tenant_id`, `sid` (session) | returned by every sign-in route |
| Refresh token + rotation | `LocalAuthService.RefreshTokenAsync`; atomic rotation claim (`TryRevokeForRotationAsync`), token-lineage recording, replay containment on inactive-session or concurrent-rotation reuse | `POST /v1/auth/tokens:refresh` |
| Revoke refresh token | `LocalAuthService.RevokeRefreshTokenAsync` | `POST /v1/auth/tokens:revoke` |
| Server-side sessions | `SessionManagementService`, `UserSession` entity; idle/absolute limits, concurrent-session eviction, terminate on password change/MFA disable | `GET /v1/auth/sessions`, `DELETE /v1/auth/sessions/{sessionId}`, `POST /v1/auth/sessions:terminate-others`, `:terminate-all`, `:refresh`, `GET /v1/auth/sessions:analyze-security` |
| Risk-based step-up | `IAuthenticationAnomalyDetectionService`; high-risk sign-in returns `RequiresStepUp` + 5-minute `StepUpToken` instead of tokens | `POST /v1/auth/step-up` |
| Flow-orchestrated sign-in with MFA | `AuthenticationOrchestrationService` (risk score, challenge steps at threshold 0.5, TOTP/backup codes, MFA lockout) | internal service consumed by sign-in flows |
| TOTP / backup codes / MFA status | `IMfaService`, `ITotpMfaService` (secrets encrypted via `IEncryptionService`), `IBackupCodeMfaService` | `/v1/auth/mfa`, `/v1/auth/mfa/totp:setup`, `totp:complete`, `/v1/auth/mfa/verify`, `/v1/auth/mfa/backup-codes`, `:regenerate`, `/v1/auth/mfa/methods`, `/v1/auth/mfa/disable` |
| External providers (OAuth 2.0) | `IOAuthService` — authorization URL, callback with mandatory `state` CSRF validation, profile fetch, token revocation; google, github, discord, microsoft | `GET /v1/auth/github:authorize` + `GET /v1/auth/github:callback`; `POST /v1/auth/discord:sign-in-authorize` + `:sign-in-callback`; Google ID-token bridge `POST /v1/auth/google:sign-in` |
| Account linking to providers | `ExternalLogin` entity — one row per `(Provider, ProviderKey)` | `POST /v1/auth/external-logins/google`, `POST /v1/auth/external-logins/discord:link-authorize` + `:link-callback`, `GET /v1/auth/external-logins`, `DELETE /v1/auth/external-logins/{provider}` |
| Password lifecycle | `IPasswordService` (reset request/consume, change, strength policy) | `POST /v1/auth/password:reset-request`, `:reset`, `:change` |
| Email verification | `IEmailVerificationService` | `POST /v1/auth/email:send-verification`, `:verify` |
| Passwordless | magic-link and Web3 wallet flows | `POST /v1/auth/magic-link:request` + `:consume`; `POST /v1/auth/web3/challenge` + `/v1/auth/web3:verify` |

Anonymous endpoints are registered in `apps/api/Source/GameGuild.API/Security/AnonymousEndpointRegistry.cs`;
any gateway or sidecar that fronts these routes must replicate that allow list, and nothing else
should be reachable without credentials.

### 2.1 From ASP.NET Core Identity

`SignInManager`/`UserManager` calls map onto the services above (password sign-in →
`POST /v1/auth/sign-in`; lockout → the `AuthenticationSecurity` throttling and MFA-attempt
lockout; roles → `role` claims issued per tenant membership). The Identity persistence stack
(`AspNetUsers`, `AspNetRoles`, `AspNetUserLogins`, …) is **not** a second stack inside GameGuild:
migrate rows into the GameGuild schema (Section 3) and retire the Identity database. The
`ExternalLogin` table replaces `AspNetUserLogins` with the same `(Provider, ProviderKey)`
uniqueness semantics. `SecurityStamp` has no direct counterpart — `token_version` on the user
record serves the same invalidation purpose (bumping it invalidates issued access tokens).

The Identity v3 password-hash format (binary PBKDF2 with embedded PRF/iteration count) is not
readable by the GameGuild `PasswordHasher`; see Section 3.2 for the two supported formats and
the re-hash-on-first-login strategy.

### 2.2 From NextAuth / Auth.js

The web app already demonstrates the supported bridge: it runs NextAuth v5 and exchanges the
Google ID token for GameGuild tokens through `POST /v1/auth/google:sign-in` (the endpoint is
explicitly provided for NextAuth.js integration). Two migration shapes:

1. **Keep NextAuth as the front door (bridge mode).** The NextAuth Google provider keeps its own
   client credentials; after the NextAuth callback, the server action posts the ID token to
   `/v1/auth/google:sign-in`, stores the returned `AccessToken`/`RefreshToken`, and refreshes via
   `POST /v1/auth/tokens:refresh` before expiry. NextAuth's session/JWT remains a short-lived
   convenience wrapper; authorization over API data uses the GameGuild bearer token.
2. **Adopt the API-native OAuth flows (replace mode).** Move provider credentials into
   `PresentationLayer:Authentication:ExternalProviders:Providers:{provider}` (google, microsoft,
   github, discord) and use the authorize/callback endpoints listed above with their mandatory
   `state` validation; then remove the NextAuth provider entry. Redirect URIs registered with
   each provider must be updated in the same change.

In both shapes, map NextAuth users to GameGuild users through `ExternalLogin` rows
(`Provider = "google"`, `ProviderKey = provider subject/sub claim`), never by email alone.

### 2.3 From a raw JWT implementation

| Source concept | GameGuild destination |
| --- | --- |
| Signing secret, issuer, audience | `PresentationLayer:Authentication:JwtSecretKey`, `JwtIssuer`, `JwtAudience` (preserve the old values to keep unexpired tokens valid across cutover) |
| Access-token lifetime | `PresentationLayer:Authentication:JwtExpiration` (`TimeSpan` string, e.g. `00:15:00` for 15 minutes) |
| Refresh-token lifetime | `PresentationLayer:Authentication:RefreshTokenExpirationDays`; sliding expiry, clock skew and replay containment stay in the `Jwt`/`JwtSettings` resolver fields (`RefreshTokenSlidingExpiration`, `ClockSkewSeconds`, `RefreshTokenReplayContainmentScope` = `Family` or `Account`) |
| Custom claims in the token | Only the claim set of `JwtTokenService` (`sub`, `email`, `role`, `token_version`, `auth_time`, `tenant_id`, `sid`, …) is issued and validated; move any additional identity data into the profile/authorization APIs instead of the token |
| Opaque refresh tokens | Cannot be imported: GameGuild refresh tokens are stored hashed, session-bound, lineage-tracked and atomically rotated. Cut users over with a fresh sign-in and revoke the old system's tokens there |
| Token revocation | `POST /v1/auth/tokens:revoke` plus per-session termination endpoints; `token_version` bump forces global re-auth |

Legacy key locations (`Jwt:Secret`, `Jwt:SecretKey`, `JwtSettings:*`, root `Authentication:JwtSecretKey`)
remain readable by `JwtOptionsResolver` as aliases; the nested
`PresentationLayer:Authentication` values take precedence, so move values into the nested section
explicitly and delete the aliases after cutover.

## 3. Data migration

Run data migration as reviewed, idempotent scripts against the GameGuild schema (standard EF
migrations apply for any schema change). Preserve source user IDs in an audit mapping table;
authorization, audit and billing rows reference GameGuild `Guid` user IDs, so allocate those once
and reuse them across retries.

### 3.1 Accounts, roles and tenants

Import users with email/username, verification state and disabled/locked status preserved.
Never mark an unverified source account as verified, and never infer elevated privilege from an
unverified email. Roles require a tenant: map source roles into tenant memberships so that
sign-in resolves an active `tenantAccessContext` (sign-in fails closed when no active tenant
access exists). Record the source `SecurityStamp`-equivalent (if any) as `token_version = 1`
unless you intentionally want to invalidate nothing.

### 3.2 Password hashes: supported formats and re-hash-on-first-login

`PasswordHasher.VerifyPassword` (`Services/PasswordHasher.cs`) accepts exactly two stored formats:

| Source format | Directly verifiable? | Action |
| --- | --- | --- |
| BCrypt `$2a/$2b/$2x/$2y$` with work factor 04–16 | Yes | Import as-is; users keep their passwords |
| Versioned `pbkdf2-sha256$600000$<salt>$<hash>` (16-byte salt, 32-byte key, exactly 600 000 iterations) | Yes | Import as-is |
| ASP.NET Core Identity PBKDF2 (binary v3 format, arbitrary iteration count) | No | Use re-hash-on-first-login (below) or verified password reset |
| Any other PBKDF2/Argon2/scrypt/LDAP format | No | Same as above |

**Re-hash-on-first-login.** For formats in the "No" rows, import the legacy hash alongside the
account (a dedicated column or an import table) and wrap verification at the application layer:

1. On `POST /v1/auth/sign-in` for an account flagged as `pendingLegacyHash`, the wrapper first
   verifies against the legacy hash with a scoped, tested adapter.
2. On success it re-hashes the submitted password with `IPasswordHasher.HashPasswordAsync`
   (BCrypt at the configured `PasswordPolicy:BCryptWorkFactor`, default 12), stores it as the
   account's `PasswordHash`, and clears the legacy marker. Subsequent sign-ins use the normal
   path.
3. On failure it records the attempt through the existing lockout/throttling path exactly like a
   normal failed sign-in (do not build a second failure pipeline).

Code support versus orchestration: `IPasswordHasher` exposes `NeedsRehashAsync`/
`NeedsUpgrade` (flags hashes below the configured BCrypt work factor or malformed), but
`LocalAuthService.LocalSignInAsync` does not invoke it and no legacy-format verifier or import
adapter ships in this repository. The dual-read wrapper in steps 1–3 is application-level
orchestration you write and test around the existing services; the supported alternative is the
built-in verified password-reset flow (`POST /v1/auth/password:reset-request` → `:reset`) for
the affected population, which needs no custom code. Do not truncate passwords, rehash an
existing hash as if it were a password, or relax the password policy to make imports pass.

### 3.3 External login linkage

Create one `ExternalLogin` row per source `AspNetUserLogins`-equivalent record
(`Provider`, `ProviderKey` = the provider's stable subject identifier, `UserId` = the allocated
GameGuild user). The unique index on `(Provider, ProviderKey)` rejects duplicates — resolve
collisions before import by keeping exactly one row per pair. Email match is not linkage proof:
two local accounts sharing an email with one Google subject must not be silently merged.
Post-cutover, users link additional providers through the authenticated
`/v1/auth/external-logins/*` endpoints rather than by import.

### 3.4 Session cutover and dual-token windows

What the code supports:

- Both systems can run alongside: GameGuild issues its own tokens and sessions; the legacy stack
  keeps serving its existing sessions until you decommission it. Nothing in this repository
  validates legacy tokens.
- Bounded invalidation on the new stack: per-token `POST /v1/auth/tokens:revoke`, per-session and
  bulk session termination, `Session:TerminateSessionsOnPasswordChange/MfaDisable`, and the
  `token_version` bump for global re-auth.
- Safe refresh during the window: rotation is atomic and replay of an already-rotated token
  triggers containment (session invalidation) per the configured
  `RefreshTokenReplayContainmentScope`.

What is application-level orchestration (the dual-token window itself):

1. **Alongside.** Deploy the GameGuild API with schemes and providers configured. New logins use
   the new endpoints; existing sessions stay on the legacy system. Gate access per consumer.
2. **Migrate.** Import accounts, supported password hashes, `ExternalLogin` rows and MFA state
   (Section 3.5) while both stacks run. Imported users still holding legacy sessions are not yet
   authenticated against GameGuild.
3. **Cutover with dual acceptance.** For a window of at least the legacy token's maximum
   lifetime, have clients present either credential: the legacy token to the legacy validator,
   the GameGuild bearer token to the new API (an edge proxy or the client SDK picks by issuer or
   a marker claim). Users appearing with only a legacy session get a fresh GameGuild sign-in
   (password — which transparently completes re-hash-on-first-login — or provider flow).
4. **Revoke legacy.** After the window and the agreed monitoring shows no legacy-token traffic,
   revoke remaining legacy tokens in the source system, remove its endpoints from the proxy, and
   delete the legacy credential store only after the rehearsed rollback window closes.

Never copy opaque legacy refresh tokens into GameGuild `RefreshToken` rows (they are stored
hashed, session-bound and lineage-tracked) and never widen token validation (extra issuer,
audience or algorithm) to accept legacy tokens inside the GameGuild pipeline.

### 3.5 MFA state

Importing TOTP secrets requires the GameGuild encryption context: secrets are encrypted with
`IEncryptionService` (`Encryption:EncryptionKey`/`Encryption:Key`), so either decrypt-and-re-encrypt
through that service during import or force re-enrollment via `/v1/auth/mfa/totp:setup`.
Backup codes cannot be imported usefully (they are shown once as plaintext); import users as
enrolled-without-backup-codes and let them regenerate via `/v1/auth/mfa/backup-codes:regenerate`.
Do not clear MFA enrollment to make a sign-in succeed; a user you cannot enroll compatibly keeps
access through the verified password-reset flow.

## 4. Configuration migration

Move values into the typed sections; the nested presentation section wins over the legacy root
aliases. Validation runs at startup and fails closed — a missing JWT secret/issuer/audience or a
missing/weak encryption key aborts startup (`OperationalStartupConfiguration` throws
"Unsafe operational startup configuration"; `EncryptionService` refuses to operate without a
32-byte key). Never resolve a configuration failure by disabling authentication or authorization.

| Old key / location | New location |
| --- | --- |
| `Jwt:Secret` / `Jwt:SecretKey` / `JwtSettings:SecretKey` / `Authentication:JwtSecretKey` | `PresentationLayer:Authentication:JwtSecretKey` |
| `Jwt:Issuer` / `JwtSettings:Issuer`; `Jwt:Audience` / `JwtSettings:Audience` | `PresentationLayer:Authentication:JwtIssuer` / `JwtAudience` |
| `Jwt:AccessTokenExpirationMinutes` / `JwtSettings:AccessTokenExpirationMinutes` | `PresentationLayer:Authentication:JwtExpiration` (TimeSpan string; whole minutes are used) |
| `Jwt:RefreshTokenExpirationDays` / `JwtSettings:RefreshTokenExpirationDays` | `PresentationLayer:Authentication:RefreshTokenExpirationDays` |
| `Jwt:RefreshTokenSlidingExpiration`, `Jwt:ClockSkewSeconds`, `Jwt:Validate*`, `Jwt:RefreshTokenReplayContainmentScope` | Keep in `Jwt` / `JwtSettings` (read by `JwtOptionsResolver`; the nested section does not replace these) |
| `Authentication:PasswordPolicy:*` / `PasswordPolicy:*` | `PresentationLayer:Authentication:PasswordPolicy:*` (adds `BCryptWorkFactor`) |
| `OAuth:{provider}:*` | `PresentationLayer:Authentication:ExternalProviders:Providers:{provider}` with explicit `Enabled` |
| Legacy session limits | root `Session` (`IdleTimeoutMinutes`, `AbsoluteTimeoutMinutes`, `MaxConcurrentSessions`, `TrustedDeviceDurationDays`, `MaxTrustedDevices`, `TerminateSessionsOnPasswordChange`, `TerminateSessionsOnMfaDisable`, `EnableDeviceFingerprinting`, `EnableLocationTracking`); module-level `SessionPolicy` (`AccessTokenExpirationMinutes`, `AllowMultipleSessions`, `IdleTimeoutMinutes`, `ExtendOnActivity`, `BindToIpAddress`, `BindToDeviceFingerprint`) composes the runtime session policy |
| Legacy lockout/throttling | root `AuthenticationSecurity` (`MaxFailedAttemptsPerHour`/`PerDay`, `MaxAttemptsPerIpPerHour`, `AccountLockoutDurationMinutes`, `EnableIpThrottling`, `EnableUserEnumerationProtection`) |
| Legacy MFA switches | root `Mfa` (`Enabled`, `RequireMfaByDefault`, `MaxFailedAttempts`, `LockoutDurationMinutes`, backup-code counts, TOTP parameters) |
| Legacy headers | `PresentationLayer:SecurityHeaders` (CSP, X-Frame-Options; production HSTS comes from the pipeline) |
| Legacy API-key source | `EnableApiKeyAuthentication` + `ApiKeyHeaderName` (+ optional HTTPS-only query source, or the programmatic `ApiKeyCustomKeyResolver`) |
| Legacy Basic auth | `EnableBasicAuthentication` + `Basic` (HTTPS-only; can never bypass MFA) |
| Legacy cookie | `EnableCookieAuthentication` + `Cookie` (HttpOnly, `SecurePolicy.Always`, validated SameSite) |

**`Encryption:Key` fail-closed requirement.** TOTP secret encryption resolves
`Encryption:EncryptionKey` first, then `Encryption:Key`, and throws (refuses to encrypt or
decrypt) when the key is absent or shorter than 32 bytes — there is no fallback plaintext mode.
Operational startup validation independently requires a strong value. Before the first deployment
that enables MFA, provision the key in the secret store (for example
`Encryption__EncryptionKey`), and if you are replacing an older key, move the old value into
`Encryption:PreviousKeys` so previously encrypted TOTP secrets remain decryptable during the
rotation window.

Minimum cutover configuration (secrets via the secret store):

```json
{
  "PresentationLayer": {
    "Authentication": {
      "EnableAuthentication": true,
      "EnableAuthorization": true,
      "JwtIssuer": "legacy-issuer",
      "JwtAudience": "legacy-audience",
      "JwtExpiration": "00:15:00",
      "RefreshTokenExpirationDays": 30,
      "PasswordPolicy": {
        "MinPasswordLength": 12,
        "BCryptWorkFactor": 12
      },
      "ExternalProviders": {
        "Providers": {
          "google": { "Enabled": true, "ClientId": "<from legacy OAuth:google>", "Scopes": ["openid", "email", "profile"] }
        }
      }
    }
  },
  "Session": {
    "IdleTimeoutMinutes": 30,
    "AbsoluteTimeoutMinutes": 1440,
    "MaxConcurrentSessions": 5
  },
  "Mfa": { "Enabled": true, "RequireMfaByDefault": false },
  "AuthenticationSecurity": { "EnableUserEnumerationProtection": true }
}
```

## 5. Rollout

1. **Rehearse.** Deploy to the migration environment with the Section 4 configuration. Exercise
   the regression surface in Section 6 plus the provider sandbox flows (real callback, rejected
   `state`, replayed `state`) for every `Enabled` provider. Record release SHA, effective
   non-secret configuration and results.
2. **Alongside.** Deploy the configured API beside the legacy system (Section 3.4 step 1).
   Confirm health, startup validation and that no anonymous route beyond
   `AnonymousEndpointRegistry` is reachable through the edge.
3. **Migrate.** Run the Section 3 import scripts idempotently; verify row counts, unique-index
   collisions resolved, tenant/role mapping and spot-check password sign-ins for each imported
   hash format (including one legacy-format re-hash-on-first-login if that adapter is used).
4. **Cutover.** Switch consumers to GameGuild endpoints with the dual-token window (Section 3.4
   step 3). Watch authentication failure rate, MFA/step-up completion, refresh rotation and
   containment events, and authorization denials; roll back consumers to the legacy endpoints on
   the rehearsed triggers rather than widening validation.
5. **Revoke legacy.** Revoke remaining legacy tokens, remove legacy endpoints from the edge,
   delete the legacy credential store after the rollback window, and remove the legacy
   configuration aliases (`Jwt:*`, `JwtSettings:*`, root `Authentication:JwtSecretKey`) from all
   environments.

Rollback contract: reverting the deployment must not resurrect revoked sessions or bypass
enforced policy — the imported accounts, token versions and revocation records stay. If the
previous API version cannot read a schema change applied during migration, use the rehearsed
restore procedure with a maintenance window; do not improvise destructive SQL.

## 6. Verification surface

| Check | Existing regression tests |
| --- | --- |
| Scheme registration, typed options, startup validation | `GameGuild.API.UnitTests` `SecurityServiceCollectionExtensionsTests`; SharedKernel authentication-options tests |
| Password hashing, long passwords, legacy-format rejection | `GameGuild.Identity.Authentication.UnitTests` `PasswordHasherTests` and sign-in security tests |
| JWT claims/validation, tenant isolation, token version | JWT and authorization unit suites; PostgreSQL HTTP regressions |
| Refresh rotation, replay containment, revocation | `RefreshTokenPostgreSqlFlowTests`, `RefreshTokenRotationPostgreSqlTests`, bearer-revocation HTTP tests |
| MFA setup/verify/disable, lockout, backup codes | MFA unit suites and MFA PostgreSQL HTTP tests |
| Sessions, concurrent limits, termination | session-management unit and HTTP tests |
| OAuth URL/callback/profile, state validation | OAuth service tests plus the provider sandbox exercise |
| Endpoint anonymity | `AnonymousEndpointRegistry` consistency tests |

A migration is accepted when every enabled scheme, provider, imported credential format, MFA
path, session policy and the rollback rehearsal have recorded results for the deployed release.

## Implementation references

- [Composite auth interface](../../apps/api/Source/Modules/GameGuild.Identity.Authentication/Abstractions/IAuthService.cs);
  [local sign-in/refresh](../../apps/api/Source/Modules/GameGuild.Identity.Authentication/Abstractions/ILocalAuthService.cs);
  [OAuth provider contract](../../apps/api/Source/Modules/GameGuild.Identity.Authentication/Abstractions/IOAuthService.cs).
- [REST surface](../../apps/api/Source/Modules/GameGuild.Identity.Authentication/Controllers/AuthController.cs),
  [sessions](../../apps/api/Source/Modules/GameGuild.Identity.Authentication/Controllers/SessionController.cs),
  [MFA](../../apps/api/Source/Modules/GameGuild.Identity.Authentication/Controllers/MfaController.cs),
  [external logins](../../apps/api/Source/Modules/GameGuild.Identity.Authentication/Controllers/ExternalLoginsController.cs).
- [Token issuance and claims](../../apps/api/Source/Modules/GameGuild.Identity.Authentication/Services/JwtTokenService.cs);
  [sign-in, rotation and replay containment](../../apps/api/Source/Modules/GameGuild.Identity.Authentication/Services/LocalAuthService.cs);
  [orchestrated flows](../../apps/api/Source/Modules/GameGuild.Identity.Authentication/Services/AuthenticationOrchestrationService.cs).
- [Password hashing and rehash detection](../../apps/api/Source/Modules/GameGuild.Identity.Authentication/Services/PasswordHasher.cs);
  [versioned long-password format](../../apps/api/Source/Modules/GameGuild.Identity.Authentication/Services/LongPasswordHash.cs).
- [Provider dispatch and state validation](../../apps/api/Source/Modules/GameGuild.Identity.Authentication/Services/OAuthService.cs);
  [external-login entity](../../apps/api/Source/Modules/GameGuild.Identity.Authentication/Entities/ExternalLogin.cs).
- [Typed authentication options](../../apps/api/Source/Modules/GameGuild.SharedKernel/Configuration/PresentationLayer/Authentication/AuthenticationOptions.cs);
  [legacy JWT key aliases](../../apps/api/Source/Modules/GameGuild.SharedKernel/Configuration/ApplicationLayer/JwtOptionsResolver.cs);
  [session options](../../apps/api/Source/Modules/GameGuild.SharedKernel/Configuration/ApplicationLayer/SessionOptions.cs);
  [module session policy](../../apps/api/Source/Modules/GameGuild.Identity.Authentication/Models/Configuration/SessionPolicy.cs).
- [Fail-closed encryption key](../../apps/api/Source/Modules/GameGuild.Identity.Authentication/Services/EncryptionService.cs);
  [startup secret validation](../../apps/api/Source/GameGuild.API/Core/Setup/OperationalStartupConfiguration.cs).
- [Scheme registration](../../apps/api/Source/GameGuild.API/Core/Extensions/SecurityServiceCollectionExtensions.cs);
  [anonymous endpoint allow list](../../apps/api/Source/GameGuild.API/Security/AnonymousEndpointRegistry.cs).
