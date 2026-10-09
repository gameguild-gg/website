# Authorization options configuration

The API binds the `Authorization` configuration section to the presentation authorization options. It supports a system-account identifier, static policies, and role inheritance. Database-backed policy definitions remain authoritative for names registered in `GameGuild.Identity.Authorization.Policies`.

```json
{
  "Authorization": {
    "SystemAccountId": "11111111-2222-3333-4444-555555555555",
    "FallbackPolicyName": "CanAccessApi",
    "RoleHierarchy": {
      "Administrator": [ "Moderator", "Editor" ],
      "SystemAdministrator": [ "Administrator" ]
    },
    "Policies": {
      "CanAccessApi": {
        "RequireAuthenticatedUser": true
      },
      "CanModerate": {
        "RequireAuthenticatedUser": true,
        "Roles": [ "Moderator" ],
        "Claims": [
          {
            "Type": "tenant_access",
            "AllowedValues": [ "write" ]
          }
        ],
        "AuthenticationSchemes": [ "Bearer" ]
      }
    }
  }
}
```

For a configured static policy, every configured requirement must pass. Any configured role can satisfy the role requirement; a role inherits the lower-level roles listed under its name. Every claim requirement must be present, and `AllowedValues` is an any-of list. An empty `AllowedValues` list requires the claim type with any value. `AuthenticationSchemes` names schemes registered by the authentication setup.

`FallbackPolicyName` selects a registered built-in or static policy for endpoints that have no authorization metadata. An unknown name fails authorization setup instead of silently leaving the endpoint unprotected. Leave it unset to preserve ASP.NET Core's existing fallback behavior; endpoints that intentionally allow anonymous access should continue to declare that explicitly with `[AllowAnonymous]`.

Policy names already owned by the database-backed policy registry cannot be overridden in configuration. Invalid or cyclic role hierarchies fail validation. A policy that disables `RequireAuthenticatedUser` must still require a role or claim, so an empty policy cannot accidentally allow every request.

Existing database-backed policies continue to support resource permissions, tenant overrides, ABAC rules, time-window and IP rules, compiled-policy caching, and authorization auditing. The static options are an additional host configuration path; they do not replace those services or define external OAuth/SAML providers. Static policies may reference an authentication scheme after that provider has been registered.

External **authorization decisions** (distinct from external authentication providers) integrate through the `Authorization:ExternalDecision` section: an OAuth2 client-credentials decision endpoint consulted by the effective-permission resolver after every local layer, veto-only (an external deny overrides a local allow; an external allow never overrides a local deny and never grants on its own) and disabled by default. See `apps/api/docs/effective-permission-resolution.md` for the full contract, fail modes and the deferred SAML authorization-assertion integration.

## Migration

Existing deployments need no configuration change. Keep current named policies in the database. Add a policy to this section only when it is intended to be a host-owned static policy; move policy names only after checking controller attributes and the database seed. Register external authentication providers separately, then use their registered scheme names in `AuthenticationSchemes`.
