# DAC Attributes and the Declarative Authorization Family (issues #318/#320/#321)

Status: implemented. This document is the usage contract for the Discretionary Access
Control (DAC) attribute family — the declarative surface over the canonical
effective-permission engine — and the `IDacAttribute` data contract.

## The 3-layer model

Every declarative requirement is evaluated at one DAC layer, resolved through
`IDacPermissionResolver` and the canonical effective-permission engine
(`apps/api/docs/effective-permission-resolution.md`, DENY-WINS, deny-by-default,
fail-closed):

| Layer | Qualified name | Satisfied by |
|---|---|---|
| Tenant | `{Permission}` | RBAC roles, global/tenant defaults, direct grants, JIT elevation |
| Content-type | `{ContentType}.{Permission}` | `ContentTypePermissionGrant` rows for the type; tenant-layer grants under `Auto` |
| Resource | `{ResourceType}.{ResourceId}.{Permission}` | The exact resource's owner override; content-type grants of the type under `Auto` |

Inheritance flows **tenant → content-type → resource** under `PermissionLayer.Auto`
(the default): broader grants satisfy narrower checks, while scoped grants mint only
their qualified names and never widen upward (see the resolution contract).

## The declarative attribute family

The endpoint filter `ResourcePermissionAuthorizationFilter` is **enabled by default**
(`Controllers:EnablePermissionAuthorizationFilter`, default `true`). It enforces every
attribute below before the action runs; the CQRS `[AuthorizeRequest]` behavior remains
the second, independent gate on dispatched commands.

| Attribute | Layer | Notes |
|---|---|---|
| `RequireDacPermissionAttribute("perm")` | Tenant | AND logic across multiple entries. |
| `RequireContentTypeDacPermissionAttribute(typeof(T), "Read")` | Content-type | Content type = `T.Name`. |
| `RequireResourceDacPermissionAttribute(typeof(T), "id", "Read")` | Resource | Route id extraction (default). |
| `RequireResourcePermissionAttribute<TResource>(["Read"], "id")` | Resource | Single-generic variant (issue #321). |
| `RequireResourcePermissionAttribute<TPermission, TResource>(perm, "id")` | Resource | Typed enum variant (legacy). |
| `RequireContentTypePermissionAttribute<TResource>(perm)` | Content-type | Legacy marker, now resolver-backed. |
| `RequiresPermissionAttribute("perm")` / `RequirePermissionAttribute` | Tenant | Simple permission check through the engine. |

The **legacy marker attributes** (`RequireResourcePermissionAttribute<TPermission,TResource>`
and `RequireContentTypePermissionAttribute<TResource>`) predate the filter and were inert
while it was disabled; they are enforced — through the same scoped-store paths — only
when `Controllers:EnforceLegacyPermissionMarkers` is enabled (default off, because their
surfaces are governed by their own capability checks).

Multiple attributes on one action (or across action + controller) compose with **AND
logic** — the stricter conjunction always governs (most-specific wins). Denials produce a
structured `403` ProblemDetails with a stable machine `reason`; the failing attribute and
its declared permissions are included **only in the Development environment**
(issue #321 diagnostics; production responses never leak which permission was missing).

### Flexible resource-id binding

Resource attributes bind the resource id from the declared parameter name across four
sources (`DacResourceIdSource`): `RouteOrQuery` (default, backward compatible), `Route`,
`Query`, `Header` and `Claims`:

```csharp
[RequireResourceDacPermission(typeof(Invoice), "X-Invoice-Id", "Read", DacResourceIdSource.Header)]
public IActionResult Download() { ... }
```

### Extensible attribute framework (custom validation)

Custom attributes derive from `DacPermissionAttributeBase`, declaring layer, permissions
and scope; the shared filter enforces them through the same resolver path with zero new
filter code:

```csharp
public sealed class RequireSelfOrAdminAttribute()
    : DacPermissionAttributeBase(PermissionLayer.Tenant, "users:manage")
{
    public override bool ShouldApply(HttpContext httpContext)
        => !httpContext.User.HasClaim(claim => claim.Type == "role" && claim.Value == "Self");
}
```

`ShouldApply` enables **conditional application** based on runtime context. It is a
requirement-narrowing hook only in the sense that an attribute that does not apply
removes its own requirement — overrides must be independently justified, because the
fail-closed default applies the requirement always. Derived classes can also extend
`Validate()` to reject invalid declarations at enforcement time (invalid declarations
deny the request; they are never skipped).

### API documentation discovery

The `DacPermissionOperationFilter` publishes every DAC requirement declared on an
operation into the OpenAPI document under the `x-gameguild-required-permissions`
extension (layer + permission names + scope), so the generated client and Swagger UI
surface the enforced authorization contract.

## The `IDacAttribute` data contract (issue #318)

Stored DAC grants implement one standard shape:

```csharp
public interface IDacAttribute
{
    Guid? Owner { get; }                     // DAC owner (null = scope default)
    string? OwnerGroup { get; }              // optional co-governing group
    IReadOnlyList<string> PermissionList { get; }
    DacInheritancePolicy InheritancePolicy { get; }
}
```

| Carrier | Owner | Inheritance policy |
|---|---|---|
| `ResourceUserPermission` (owner row, `IsOwner = true`) | the grant subject | `InheritAndPropagate` — the engine's owner override honors the set at tenant parity and on the exact resource (issues #372/#363) |
| `ResourceUserPermission` (plain row) | the grant subject | `None` — exact resource only |
| `ContentTypePermissionGrant` | the grant subject (null for the tenant default) | `PropagateToChildren` — applies to every resource of the type |
| `DacAttributeInfo` | as constructed | as constructed (tests, tooling, ad-hoc composition) |

The interface is a read-only projection over stored grants; it is never an authorization
input by itself — every decision still runs through the engine.

## Owner override on declarative paths

Resource-scoped declarative checks (the resource markers and `RequireResource*`
attributes) resolve through the engine's resource context, which activates the
configurable owner override (`Authorization:OwnerOverride`, default
`AllowUnlessDenied`) and the temporal-precedence bound — see the resolution contract's
[Owner override (issue #372)](effective-permission-resolution.md#owner-override-issue-372)
section. The GraphQL `@authorize` directive exposes the same behavior through its
`resourceType` binding, and content-type scoped rules through `contentType`.

## GraphQL

The general `@authorize` directive accepts `contentType` for content-type scoped rules
(see `apps/api/docs/graphql-authorization.md`); permission keys are satisfied by the
content-type qualified name or the unqualified tenant name (inheritance).

## Content-type grant management

Content-type grants are managed through the guarded, versioned and audited REST surface
`/api/v{version}/authorization/content-types` (grant / deny / revoke / delete / list /
has-permission), backed by `ContentTypePermissionGrant` rows and the
`ContentTypePermissionGrantService` mutation contract (tenant security-version bump +
`PermissionAuditLog` entry per change, soft delete for permission-history preservation).
