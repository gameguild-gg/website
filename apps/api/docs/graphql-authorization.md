# GraphQL Authorization Framework (issue #335)

This document describes the GameGuild GraphQL authorization framework: the general
`@authorize` directive, field-level enforcement, permission-aware schema visibility and
introspection control, and how they integrate with the platform permission model
(the effective-permission engine, owner override, durable evaluation logging).

The GraphQL endpoint itself remains opt-in (`GraphQL:EnableGraphQL`, default `false`) and
requires authentication at the transport boundary (`MapGraphQL(...).RequireAuthorization()`).
The framework below adds authorization *inside* the schema.

## The general `@authorize` directive

The general directive is the cross-domain authorization contract for GraphQL fields. It is
repeatable and applies to field definitions (queries, mutations and nested fields):

```graphql
type Query {
  projects: [Project!]!
  projectById(id: ID!): Project                     @authorize(permissions: ["Read"], resourceType: "Project", resourceIdArgument: "id")
  auditTrail: [AuditEntry!]!                        @authorize(permissions: ["Audit"], maskUnauthorized: false)
}
```

In resolver code, use `GraphQLAuthorizeAttribute` from `GameGuild.Identity.Authorization.GraphQL`
(any module referencing the authorization module can adopt it):

```csharp
using GameGuild.Identity.Authorization.GraphQL;

[ExtendObjectType("Query")]
public class ProjectQueries
{
    [GraphQLAuthorize(PermissionType.Read)]                       // permission variant (tenant-scoped)
    public string? Guarded() => ...;

    [GraphQLAuthorize(PermissionType.Edit, PermissionType.Delete,
        Mode = GraphQLAuthorizationMode.Any)]                      // OR composition (Any); default is All (AND)
    public string? AnyOf() => ...;

    [GraphQLAuthorize(Policy = "RequireAdminRole")]               // ASP.NET Core policy variant
    public string? PolicyGuarded() => ...;

    [GraphQLAuthorize(Roles = ["Admin"])]                          // role variant (any-of)
    public string? RoleGuarded() => ...;

    [GraphQLAuthorize(PermissionType.Read, MaskUnauthorized = true)] // deny -> null instead of error
    public string? Masked() => ...;

    [GraphQLAuthorize(PermissionType.Read,
        ResourceType = "Project",                                  // resource-scoped: activates
        ResourceIdArgumentName = "id")]                            // owner override + expiry checks
    public string? Resource(Guid id) => ...;

    [GraphQLAuthorize(PermissionType.Read,
        ResourceType = "Project",
        ResourceIdParentPropertyName = "ProjectId")]               // nested field: id from parent value
    public string? NestedGuarded([Parent] Nested resource) => ...;
}
```

### Directive arguments

| Argument | Meaning |
|---|---|
| `permissions` | Typed permission keys (`PermissionType` names). Composed with `mode`. |
| `mode` | `all` (AND, default) or `any` (OR) for `permissions`. |
| `policy` | ASP.NET Core authorization policy the caller must satisfy. |
| `roles` | Role claims; the caller must hold at least one. |
| `maskUnauthorized` | Resolve denied fields to `null` instead of a GraphQL error. |
| `resourceType` | DAC resource type; enables resource-scoped evaluation. |
| `resourceIdArgument` | Dotted argument path holding the resource id (e.g. `input.projectId`). |
| `resourceIdParentProperty` | Parent-value property path holding the resource id (nested fields). |
| `contentType` | DAC content type (issues #360/#365/#320); enables content-type scoped evaluation through the engine's content-type grant layer. Cannot be combined with `resourceType`. |

### Enforcement semantics (deny-by-default)

Enforcement is centralized in the host's GraphQL authorization field middleware
(`GraphQLAuthorizeFieldMiddleware`, registered by `SetupGraphQL`), not in each resolver:

- A field **without** the directive is not protected by this framework (the transport
  authentication requirement still applies).
- A field **with** one or more `@authorize` applications is protected: the resolver only
  runs after **every** application passes (repeatable applications AND together; deny wins).
- Within one application, `permissions` (per `mode`), `roles` and `policy` all apply (AND).
- Permission checks run through `IEffectivePermissionResolver` — the canonical
  effective-permission engine (DENY-WINS, deny-by-default). Tenant-scoped rules resolve a
  tenant context; content-type scoped rules (`contentType`, no resource pair) resolve a
  content-type context served by the engine's content-type grant layer — each permission
  key is satisfied by the content-type qualified name (`{ContentType}.{Permission}`) or the
  unqualified tenant name (inheritance: a tenant-layer grant satisfies a content-type
  scoped rule); resource-scoped rules (`resourceType` + a resolved id) resolve an exact
  resource context, which is what activates **owner override** (#372) and
  **expiration-aware precedence** (#363, `OwnerOverrideEvaluation.EffectiveThroughUtc`):
  a lapsed ownership bound fails closed inside the engine and simply mints nothing.
- Roles are evaluated against the trusted actor context; policies against the ASP.NET Core
  `IAuthorizationService` principal. Actor/tenant always come from the request context,
  never from GraphQL input.
- **Fail-closed everywhere**: anonymous actors, unresolvable actor context, unresolvable
  resource ids, unknown permission keys and evaluation exceptions all deny. Evaluation
  exceptions surface the bounded `AUTHORIZATION_UNAVAILABLE` error (never store details).
- Denials produce the bounded error `AUTHORIZATION_DENIED` ("Not authorized to access this
  resource.") — or `null` when every application requests `maskUnauthorized`. The resolver
  is never invoked on denial.
- Effective-permission resolution and role attribution are cached per request, so
  multi-field queries pay once per (user, tenant, resource).

The project-specific `@projectAuthorize` directive (PR #585) remains active for the
Projects domain and is unaffected; new cross-domain fields should use `@authorize`.

HotChocolate's own `@authorize` middleware registration (`AddAuthorization()`) is replaced
by the general directive: the policy/role checks are enforced natively through the ASP.NET
Core authorization services inside the same middleware.

### Evaluation logging

Every field-level decision (Allow / Deny / Error) and every introspection decision is
recorded through `IPermissionEvaluationLogService` (source `graphql`, operation = field
name, role attribution with `(inherited)` markers per #359/#330) and persisted durably by
the audit sink. Recording never changes the decision and never throws.

## Schema visibility and introspection control

**Strategy decision (v1).** HotChocolate builds one static schema per service, and its
introspection resolvers bypass field middleware, so per-user *partial* schema responses are
not constructible without a per-permission-view schema build. The framework therefore
takes the **fail-closed visibility** approach:

- **From query results**: unauthorized fields never resolve (error or `null` mask) —
  values are hidden on every execution.
- **From introspection** (`GraphQL:SchemaAuthorization`):

  | Setting | Default | Behavior |
  |---|---|---|
  | `FilterSchema` | `true` | Introspection is allowed only for callers that can see **every** `@authorize`-protected field in the schema (depth-first walk over all protected fields; resource-scoped rules are treated as not visible — they cannot be proven without a concrete resource). Callers that cannot see everything are denied introspection entirely rather than served a partial schema: protected operations never leak through introspection metadata. |
  | `RestrictIntrospectionToSchemaAdminsInProduction` | `true` | In a Production environment, only schema administrators may introspect, regardless of visibility. |
  | `SchemaAdminPermission` | `SystemAdmin` | Typed permission key identifying schema administrators. Holders bypass per-field visibility checks. Unknown/blank keys fail closed (nobody is a schema admin). |

```json
"GraphQL": {
  "EnableGraphQL": true,
  "Endpoint": "/graphql",
  "SchemaAuthorization": {
    "FilterSchema": true,
    "RestrictIntrospectionToSchemaAdminsInProduction": true,
    "SchemaAdminPermission": "SystemAdmin"
  }
}
```

Introspection denials use the same bounded `AUTHORIZATION_DENIED` error; normal queries
and `__typename` continue to work for restricted callers, and schema admins always
introspect the full schema.

## Testing utilities

The test suite exercises the framework through `WebApplication` + `TestServer` hosts with
fake `IEffectivePermissionResolver` implementations (see
`GameGuild.API.UnitTests/GraphQL/GraphQLAuthorizeDirectiveTests.cs` and
`GraphQLSchemaAuthorizationTests.cs`): allow/deny/mask matrix, Any/All composition,
repeatable applications, role and policy variants, resource-scoped and owner-override
paths (in-force vs lapsed ownership), anonymous denial, per-request caching, evaluation
logging (allow + deny + error, with role attribution), introspection gating matrix
(visibility, production restriction, schema admin bypass, fail-closed on errors) and
configuration validation.

## References

- Effective-permission resolution contract: `apps/api/docs/effective-permission-resolution.md`
- Evaluation logging (#359): `IPermissionEvaluationLogService` / `PermissionEvaluationRecord`
- Owner override (#372) and temporal precedence (#363): `OwnerOverrideOptions`,
  `OwnerOverrideEvaluation`
- Projects directive (#585): `ProjectAuthorizationDirective`
