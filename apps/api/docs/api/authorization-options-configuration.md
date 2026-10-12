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

## SAML 2.0 — explicitly deferred

SAML 2.0 federation is intentionally not implemented. A safe default does not exist yet because it requires a product decision on the .NET SAML library (Sustainsys.Saml2 vs ITfoxtec.Identity.Saml2 — differing posture on metadata refresh, EncryptedAssertion key handling, and logout bindings) and on tenant-held X.509 certificate lifecycle (rotation, revocation lists). Enterprise IdPs that support OIDC can be connected today through the generic OIDC federation above. Reopen with the library and certificate-handling decisions to implement the SAML profile. (Trigger note: PR Verify synchronize events for this PR head were dropped by the platform on 2026-10-10; this comment is unchanged in substance.)

## Migration

Bind and validate the `Authorization` section with `AuthorizationOptionsBuilder.Build(configuration)` or pass it to `SetupAuthorization`. Existing built-in policies and database-backed tenant policies keep their current names and sources. Move only host-owned static policies into this section; continue to declare resource checks with the Identity Authorization permission attributes/services so resource identifiers are resolved from the request and checked against the permission store.

When adding an external provider, register its authentication scheme through the host's authentication configuration, then list that scheme in the policy's `AuthenticationSchemes`. Keep provider credentials in the application's secret provider, not in the `Authorization` section. Claim mappings are opt-in and allowlisted; review any mapping to a role or permission claim as an access grant.
