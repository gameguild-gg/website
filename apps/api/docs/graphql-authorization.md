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
  resource.") — or `null` when every application requests `maskUnauthorized` (see
  [Conditional field exposure](#conditional-field-exposure-masking)). The resolver
  is never invoked on denial.
- Effective-permission resolution and role attribution are cached per request, so
  multi-field queries pay once per (user, tenant, resource).

### Computed-field authorization from related-entity permissions

A protected field's authorization can be derived from a RELATED entity's permission
instead of the parent type's: declare a resource-scoped rule whose resource id is bound
to a parent property path. Dotted paths resolve nested related entities, and the
resource type may differ from the parent type:

```csharp
[ExtendObjectType(nameof(Course))]
public sealed class CourseResolvers
{
    // Authorized by the RELATED author's Read permission — id from the nested
    // parent path Author.Id.
    [GraphQLAuthorize(PermissionType.Read,
        ResourceType = "User",
        ResourceIdParentPropertyName = "Author.Id")]
    public string? AuthorBiography() => ...;

    // Authorized by the related category entity — id from the parent's scalar property.
    [GraphQLAuthorize(PermissionType.Read,
        ResourceType = "CourseCategory",
        ResourceIdParentPropertyName = "CategoryId")]
    public string? CategoryLabel() => ...;
}
```

Computed resolvers that resolve the related entity themselves (or derive it from
arguments) use the imperative helper, which routes through the same engine (owner
override, expiration-aware precedence), the per-request permission cache and evaluation
logging, and never throws (fail-closed outcomes):

```csharp
public async Task<bool> CanEditCategoryAsync(
    [Parent] Course course,
    IResolverContext resolverContext)
{
    var outcome = await GraphQLAuthorizationSupport.AuthorizeRelatedEntityAsync(
        resolverContext,
        resourceType: "CourseCategory",
        relatedEntityId: course.CategoryId,
        permissions: [PermissionType.Edit]).ConfigureAwait(false);
    return outcome.Allowed; // Deny/Unavailable reasons are machine-readable server detail
}
```

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
    "SchemaAdminPermission": "SystemAdmin",
    "HideUnauthorizedOperations": true,
    "MaskUnauthorizedFieldsByDefault": false
  }
}
```

Introspection denials use the same bounded `AUTHORIZATION_DENIED` error; normal queries
and `__typename` continue to work for restricted callers, and schema admins always
introspect the full schema.

## Dynamic schema filtering for unauthorized operations (operation hiding)

HotChocolate compiles one static schema per process, so per-user *schema documents* are
not constructible without forking the platform. The framework instead hides unauthorized
operations at the **operation-planning stage**: a request-pipeline middleware
(`GraphQLUnauthorizedOperationMiddleware`, registered between document validation and
operation compilation) walks every root field referenced by the request — directly,
through inline fragments or fragment spreads — and evaluates each protected operation's
planning-decidable `@authorize` rules (tenant-scoped and content-type scoped permissions,
roles, policies) against the caller's resolved permission set.

- Any referenced unauthorized operation fails the **whole request closed**, before
  execution: the response carries only the error HotChocolate produces for a nonexistent
  field (`The field \`X\` does not exist on the type \`Query\`.`), with **no `data`
  payload, no path and no authorization code** — indistinguishable from querying an
  unknown field, so protected operations never reveal their existence.
- **Resource-scoped rules are not planning-decidable** (they need the concrete resource
  and its owner-override state); they remain enforced during execution by the field
  middleware, and their execution-time denial errors never carry a path (see error
  hygiene below).
- Schema administrators (see `SchemaAdminPermission`) bypass the gate; they receive the
  honest execution-time denial instead.
- Anonymous callers, unknown permission keys and evaluation failures deny (fail-closed).
- Fields referenced under `@skip`/`@include` are still gated: the directives are not
  evaluated at planning time (variables are not coerced yet), so conditionally skipped
  protected operations are conservatively hidden. Split such queries into separate
  documents.

| Setting | Default | Behavior |
|---|---|---|
| `HideUnauthorizedOperations` | `true` | Enables the operation-planning gate. When `false`, unauthorized operations fail at execution time with the bounded `AUTHORIZATION_DENIED` error (path stripped). |

## Error hygiene (non-revealing authorization errors)

The `GraphQLAuthorizationErrorFilter` normalizes execution-time authorization failures:
`AUTHORIZATION_DENIED` / `AUTHORIZATION_UNAVAILABLE` errors keep their generic message
and code, but their response `path` is stripped, so an attacker probing a protected field
learns only that *something* was denied — never which field. Combined with the
planning-time gate above, both the existence and the identity of protected fields stay
hidden from unauthorized callers.

## Conditional field exposure (masking)

Per-request field masking on results adapts the response shape to the caller's
permission set: a denied field resolves to `null` instead of erroring, while sibling
public fields keep resolving.

| Setting | Default | Behavior |
|---|---|---|
| `MaskUnauthorizedFieldsByDefault` | `false` | When `true`, every denied protected field with a **nullable** result type resolves to `null` (unless a client error is required). Per-field `maskUnauthorized: true` on the directive remains the explicit opt-in and composes with the host default. |

- Nullability is static in GraphQL: a **non-nullable** field cannot be nulled without
  producing HotChocolate's non-null violation error (which names the field), so
  non-nullable denied fields keep the bounded `AUTHORIZATION_DENIED` error (path
  stripped). Design protected fields as nullable when they should mask.
- Masked responses are the honest approximation of "conditional field exposure": the
  query shape is stable and the caller's effective values depend on their permission
  set.

## Permission-level schema versioning

The `schemaVersion` query field returns the caller's effective schema surface version
(`GraphQLSchemaVersionEvaluator`):

```graphql
{ schemaVersion { revision visibilityTier introspectionAllowed } }
```

- `revision` — deterministic SHA-256 tag (16 hex characters) over the schema's
  protected-surface manifest plus the caller's visibility tier and introspection
  eligibility. Callers on the same permission level resolve the same revision; the
  revision changes when the protected surface changes or the caller's level changes.
- `visibilityTier` — `Full` (schema administrators, or callers whose permission set
  satisfies every planning-decidable protected rule) or `Restricted`.
- `introspectionAllowed` — whether this caller may introspect (includes the Production
  restriction).

**Documented limitation.** HotChocolate builds one static schema per process and its
introspection resolvers bypass field middleware; true per-user *compiled* schemas and
partial introspection responses are not constructible without forking the platform. The
framework's contract is therefore: values hidden at execution (masking/errors),
operations hidden at planning (nonexistent-field shape), introspection all-or-nothing
per visibility, and a per-level revision marker for clients to detect surface changes.
The revision response never exposes a field inventory.

## Testing utilities

The test suite exercises the framework through `WebApplication` + `TestServer` hosts with
fake `IEffectivePermissionResolver` implementations (see
`GameGuild.API.UnitTests/GraphQL/GraphQLAuthorizeDirectiveTests.cs`,
`GraphQLSchemaAuthorizationTests.cs`, `GraphQLUnauthorizedOperationTests.cs`,
`GraphQLSchemaVersionTests.cs` and `GraphQLRelatedEntityAuthorizationTests.cs`):
allow/deny/mask matrix, Any/All composition, repeatable applications, role and policy
variants, resource-scoped and owner-override paths (in-force vs lapsed ownership),
anonymous denial, per-request caching, evaluation logging (allow + deny + error, with
role attribution), introspection gating matrix (visibility, production restriction,
schema admin bypass, fail-closed on errors), configuration validation, operation-hiding
equivalence with nonexistent-field errors (query and mutation roots, fragment-wrapped
selections), execution-time denial path stripping, default and per-field masking
(nullable vs non-nullable), permission-level schema versioning (stable per level,
distinct across levels and surface changes, no inventory leak) and related-entity
computed-field authorization (nested and scalar parent paths, imperative helper).

## References

- Effective-permission resolution contract: `apps/api/docs/effective-permission-resolution.md`
- Evaluation logging (#359): `IPermissionEvaluationLogService` / `PermissionEvaluationRecord`
- Owner override (#372) and temporal precedence (#363): `OwnerOverrideOptions`,
  `OwnerOverrideEvaluation`
- Projects directive (#585): `ProjectAuthorizationDirective`
