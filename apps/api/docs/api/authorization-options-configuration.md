# Authorization options configuration

The API host reads static policy definitions from the `Authorization` section. Database-backed policies remain owned by the Identity Authorization module and must not be redefined as static policies.

```json
{
  "Authorization": {
    "DefaultPolicy": "SignedIn",
    "RequireAuthenticatedUser": true,
    "Policies": {
      "ManageProjects": {
        "RequireAuthenticatedUser": true,
        "Roles": ["ProjectManager"],
        "Claims": [
          { "Type": "tenant_id", "AllowedValues": ["current-tenant"] }
        ],
        "AuthenticationSchemes": ["Bearer", "EnterpriseOidc"]
      }
    },
    "RoleHierarchy": {
      "PlatformAdmin": ["ProjectManager"],
      "ProjectManager": ["ProjectEditor"]
    },
    "ClaimTransformations": [
      {
        "SourceClaimType": "groups",
        "TargetClaimType": "role",
        "ValueMappings": {
          "gameguild-project-admins": "ProjectManager"
        }
      }
    ]
  }
}
```

Each configured policy combines its requirements: it can require an authenticated principal, one of its configured roles, every configured claim, and one or more authentication schemes. Role hierarchies expand accepted roles transitively. The options validator rejects empty policy requirements, invalid claim definitions, role hierarchy cycles, and incomplete claim transformations during host setup.

Claim transformations run after authentication and before authorization. They only add claims when the source value has an explicit mapping; unmapped values are ignored and repeated transformation calls do not duplicate claims. Treat mappings as trusted security configuration: a mapping into a role or permission claim can grant access to matching policies.

Dynamic ABAC and resource checks, policy caching and invalidation, and authorization decision audit events remain implemented by the Identity Authorization module. Conditional policies support location conditions, while environment requirements enforce configured IP ranges, time windows, device types, and HTTPS. Tenant context and conditional policy inputs are resolved there at evaluation time. Configure cache behavior through `Authorization:Cache` and tenant behavior through `Tenancy`; the API host does not create a second cache or policy store.

Authentication provider registration and credential settings are owned by the Authentication module. A policy can name any scheme registered by the host in `AuthenticationSchemes`, including multiple schemes. OAuth, OIDC, and SAML provider setup, callbacks, secrets, and identity provisioning remain configured in the Authentication module rather than in authorization policy definitions.

## Federated enterprise SSO (generic OIDC)

Enterprise identity providers federate through the config-gated generic OIDC surface under `Authentication:ExternalProviders:Oidc:<slug>`. Every provider defaults to disabled and fails closed (unknown or disabled slugs are refused; misconfigured entries are rejected at host startup):

```json
{
  "Authentication": {
    "ExternalProviders": {
      "Oidc": {
        "corp-idp": {
          "Enabled": true,
          "Authority": "https://login.corp.example.com",
          "ClientId": "gameguild-web",
          "ClientSecret": "<from the deployment secret store>",
          "DisplayName": "Corp SSO",
          "Scopes": ["openid", "profile", "email"],
          "ClaimMapping": {
            "sub": "sub",
            "email": "email",
            "name": "name",
            "email_verified": "email_verified"
          },
          "EmailDomains": ["corp.example.com"]
        }
      }
    }
  }
}
```

- `Authority` must be absolute HTTPS without credentials, query, or fragment; the discovery document at `{authority}/.well-known/openid-configuration` is fetched and cached (15 minutes), and its `issuer` must equal the configured authority.
- `Scopes` must include `openid` (default `openid profile email`).
- `ClaimMapping` remaps the logical claims `sub`, `email`, `name`, and `email_verified` onto the provider's claim types; unmapped logical claims use their own names.
- `EmailDomains` (optional) restricts sign-in to the listed domains and powers domain-to-provider discovery via `POST /v1/auth/oidc:discover-provider`.
- Sign-in endpoints: `POST /v1/auth/oidc/{slug}:sign-in-authorize` and `POST /v1/auth/oidc/{slug}:sign-in-callback`. The ID token is validated cryptographically (RS256 via the provider JWKS, issuer, audience, lifetime) — no new package dependency. Accounts link/JIT through the same external-login policy as the social providers, keyed `oidc-<slug>`.
- MFA policy is enforced fail-closed: when the platform requires MFA for the resolved user, the provider must attest it (`amr` containing `mfa`), otherwise sign-in is refused. The attested `amr`/`acr` values are surfaced on the sign-in response for the step-up/conditional-policy surface.
- Logout forwarding: `GET /v1/auth/oidc/{slug}:end-session-url` returns the provider's `end_session_endpoint` (with `post_logout_redirect_uri`) when the discovery document advertises one; local refresh-token revocation is unchanged.

## Federated enterprise SSO (SAML 2.0)

Enterprise identity providers that only speak SAML 2.0 federate through the config-gated service-provider surface under `Authentication:ExternalProviders:Saml:<slug>`. Every provider defaults to disabled and fails closed (unknown or disabled slugs are refused; misconfigured entries are rejected at host startup). The identity provider is described statically by configuration — no IdP metadata is fetched over the network, so the trust base is exactly what the deployment supplies:

```json
{
  "Authentication": {
    "ExternalProviders": {
      "Saml": {
        "corp-idp": {
          "Enabled": true,
          "DisplayName": "Corp SSO",
          "SpEntityId": "https://gameguild.example.com/auth/saml",
          "AssertionConsumerServiceUrl": "https://gameguild.example.com/auth/saml/corp-idp/callback",
          "IdpEntityId": "https://sts.corp.example.com",
          "IdpSigningCertificates": ["<base64 DER certificate>"],
          "IdpSignInServiceUrl": "https://sts.corp.example.com/sso/redirect",
          "IdpSingleLogoutServiceUrl": "https://sts.corp.example.com/slo/redirect",
          "NameIdFormat": "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
          "ClaimMapping": { "email": "email", "name": "displayName" },
          "TrustIdpEmailVerification": false,
          "MfaAuthnContextClassRefs": ["urn:oasis:names:tc:SAML:2.0:ac:classes:TimeSyncToken"],
          "AllowedClockSkewSeconds": 90,
          "AllowIdpInitiated": false,
          "EmailDomains": ["corp.example.com"],
          "TenantIds": ["00000000-0000-0000-0000-000000000001"]
        }
      }
    }
  }
}
```

- `IdpSigningCertificates` is a list of Base64 DER certificates (more than one supports rotation overlap). Every response must be signed (at the assertion or response level, XML-DSig) and verify against at least one currently-valid configured certificate; expired-only or unknown certificates are rejected. Unsigned responses are always refused. Encrypted assertions are rejected with an explicit error (not supported).
- Response validation fails closed on every Web SSO obligation: issuer must equal `IdpEntityId`, `Destination` must be present and equal `AssertionConsumerServiceUrl`, exactly one assertion per response (signature-wrapping posture), audience restriction must contain `SpEntityId`, `NotOnOrAfter` is required and clock-skew-bounded (`AllowedClockSkewSeconds`, default 90, max 600), the bearer subject confirmation's `Recipient`/`NotOnOrAfter` are enforced, and assertion IDs are single-use (IDistributedCache-backed replay protection) for as long as the assertion could be valid.
- Correlation: `POST /v1/auth/saml/{slug}:sign-in-authorize` issues an AuthnRequest whose ID is stored server-side (10-minute window); a posted response must carry a matching, unconsumed `InResponseTo`. Responses without `InResponseTo` (IdP-initiated SSO) are refused unless the provider is explicitly configured with `AllowIdpInitiated: true`.
- `ClaimMapping` maps the logical claims `email` and `name` onto the provider's attribute names (the subject is always the assertion `NameID`). `TrustIdpEmailVerification` (default false) decides whether the provider's email assertion counts as verified for the auto-link policy — by default SAML emails are unverified, so an account is never linked by address alone (provider-key linking and JIT creation still work), keyed `saml-<slug>`.
- MFA policy is enforced fail-closed: when the platform requires MFA for the resolved user, the assertion's `AuthnContextClassRef` must be one of the provider's configured `MfaAuthnContextClassRefs` (defaults: the standard smartcard and one-time-password context classes), otherwise sign-in is refused. The resolved class ref and the provider MFA verdict are surfaced on the sign-in response.
- Tenant isolation: a non-empty `TenantIds` allowlist binds the provider — both a requested tenant and the session's resolved tenant must be inside it, or sign-in is refused.
- `EmailDomains` (optional) restricts sign-in to the listed domains and joins domain-to-provider discovery: `GET /v1/auth/oidc:discover-provider` lists SAML providers alongside OIDC ones, each entry carrying the `protocol` it serves (`oidc` or `saml`) so the login page can route to `/v1/auth/saml/{slug}:sign-in-authorize`.
- Endpoints: `GET /v1/auth/saml/{slug}:metadata` (SP metadata XML for IdP registration), `POST /v1/auth/saml/{slug}:sign-in-authorize`, `POST /v1/auth/saml/{slug}:assertion-consumer` (the API side of the ACS: the web app forwards the posted Base64 `SAMLResponse` and `RelayState`).
- Logout: `GET /v1/auth/saml/{slug}:logout-url` returns the provider's single-logout redirect (an unsigned `LogoutRequest` via the redirect binding, with the post-logout return carried as `RelayState`) when `IdpSingleLogoutServiceUrl` is configured; null keeps logout local. Local refresh-token/session revocation always stays with the existing logout endpoints. Limitations by design: the `LogoutRequest` is unsigned (IdPs requiring signed SLO will reject it, and logout then stays local), and IdP-initiated single logout (a `LogoutRequest` inbound to this service) is not implemented.
- Scope note: this surface implements the SP-initiated Web SSO profile (redirect binding out, POST binding in) as a minimal audited in-repo subset on the in-box `System.Security.Cryptography.Xml` verifier — no new package dependency. Assertion-consumed authorization statements are not decision inputs (see `apps/api/Source/Modules/GameGuild.Identity.Authorization/Abstractions/ExternalAuthorizationOptions.cs`); roles continue to come from tenant memberships.

## Migration

Bind and validate the `Authorization` section with `AuthorizationOptionsBuilder.Build(configuration)` or pass it to `SetupAuthorization`. Existing built-in policies and database-backed tenant policies keep their current names and sources. Move only host-owned static policies into this section; continue to declare resource checks with the Identity Authorization permission attributes/services so resource identifiers are resolved from the request and checked against the permission store.

When adding an external provider, register its authentication scheme through the host's authentication configuration, then list that scheme in the policy's `AuthenticationSchemes`. Keep provider credentials in the application's secret provider, not in the `Authorization` section. Claim mappings are opt-in and allowlisted; review any mapping to a role or permission claim as an access grant.
