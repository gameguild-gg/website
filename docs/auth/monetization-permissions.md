# Monetization & Analytics Permissions (Monetize, ViewAnalytics, Configure)

Implementation of issue #346. These three tenant-level permissions control access to
revenue-generation features, business-intelligence data and monetization configuration.

## Permission keys

| Permission | Key | Scope |
|---|---|---|
| Monetize | `monetization:monetize` | Enable revenue generation, set pricing, manage monetization settings |
| ViewAnalytics | `monetization:view-analytics` | Access revenue reports, financial dashboards, performance metrics |
| Configure | `monetization:configure` | Manage payment settings, subscription tiers, pricing models |

The keys are ordinary tenant permission strings defined in
`apps/api/Source/Modules/GameGuild.Identity.Authorization/Models/TypedPermissions.cs`
(`MonetizationPermission`) and surfaced through the `Permissions` facade
(`Permissions.MonetizationMonetize`, `Permissions.MonetizationViewAnalytics`,
`Permissions.MonetizationConfigure`). They are auto-discovered by `PermissionRegistry`,
so `monetization:*` wildcards validate as well.

## Where they are enforced

### ViewAnalytics — business-intelligence data

- `GameGuild.Analytics.ProductMetricsController` — `GET /api/metrics/product` and
  `GET /api/metrics/product/export` carry `[RequirePermission(MonetizationPermission.Keys.ViewAnalytics)]`.
- `GetProductMetricsQuery` / `ExportProductMetricsQuery` carry
  `[AuthorizeRequest(MonetizationPermission.Keys.ViewAnalytics)]`, so **every** dispatch
  path (REST, GraphQL, in-process senders) is gated by the CQRS `AuthorizationBehavior`.

### Configure — monetization settings, subscription tiers, pricing models

- Subscription plan lifecycle endpoints in `SubscriptionPlansCrudController`
  (create / full update / delete) and `SubscriptionPlanOperationsController`
  (details / pricing / limits / features / activate / deactivate / archive / clone /
  featured / external-id) carry `[RequirePermission(MonetizationPermission.Keys.Configure)]`.
- The underlying commands (`CreateSubscriptionPlanCommand`, `FullUpdateSubscriptionPlanCommand`,
  `UpdateSubscriptionPlanCommand`, `UpdateSubscriptionPlanPricingCommand`,
  `UpdateSubscriptionPlanLimitsCommand`, `UpdateSubscriptionPlanFeaturesCommand`,
  `Activate/Deactivate/Archive/Clone/DeleteSubscriptionPlanCommand`,
  `SetSubscriptionPlanFeaturedCommand`, `SetSubscriptionPlanExternalIdCommand`) carry
  `[AuthorizeRequest(MonetizationPermission.Keys.Configure)]` so non-HTTP dispatch is gated too.
- Read-only plan discovery (list/get/compare/usage/pricing calculation/limit validation) is
  intentionally **not** gated — it is public catalog data.
- Self-service subscription operations (upgrade/downgrade/change billing cycle on your own
  subscription) are **not** gated — they are end-user actions, not monetization configuration.

### Monetize — revenue generation / pricing

- `SetProductPricingCommand` carries `[AuthorizeRequest(MonetizationPermission.Keys.Monetize)]`,
  gating pricing changes on every dispatch path. The REST action
  (`PUT /v1/products/{id}/pricing`) additionally keeps its existing controller-level
  `products:pricing:manage` gate — both layers must authorize.

## Integration with the DAC framework

These permissions require no special handling: they are resolved through the standard
tenant permission machinery (`IPermissionQueryService` / `Actor.HasPermission`) with the
platform's inheritance and precedence rules:

1. **Allow sources (union):** global defaults → tenant defaults → direct user grants →
   delegable role-provider permissions.
2. **Deny wins:** an explicit deny at any layer removes the permission from the effective set.
3. **Fail closed:** no tenant context ⇒ no permission; unknown permissions are rejected,
   never defaulted.
4. **System admins** bypass permission checks (`Actor.IsSystemAdmin`).

Grant examples (stored on `TenantPermissions.Permissions`):

```text
monetization:monetize
monetization:view-analytics
monetization:configure
monetization:*            (family wildcard — validates against the registry)
```

## Audit logging

- Every endpoint-level permission check performed by
  `ResourcePermissionAuthorizationFilter` (i.e. every `[RequirePermission]` /
  `[RequiresPermission]` attribute, granted **and** denied) records a
  `PermissionAuditLog` entry with the new `PermissionOperationType.Check` operation type,
  including permission key, controller action, tenant and actor.
- `AuditService` no longer swallows persistence failures silently: `TryLogAsync` retries
  the write once, and a permanent failure escalates to a **critical structured-log entry
  that carries the full audit payload**, so the record survives in the durable logging
  pipeline for reconciliation (and returns `false` so observant callers can react).
  Business operations still never break because of the audit layer.

## Best practices

- Prefer the typed constants (`MonetizationPermission.Keys.*` or the `Permissions.*`
  facade) over raw strings — typos fail closed.
- Gate the **command/query**, not only the controller, whenever the operation can be
  dispatched from non-HTTP transports (GraphQL, background jobs).
- Deny by default: do not add `monetization:*` to tenant defaults unless every user in the
  tenant should access revenue data; grant per-user or via a dedicated role instead.
- When adding a new monetization-sensitive surface, add both the gate and a wiring test
  (see the `*MonetizationWiringTests` test classes for the pattern).

## Tests

- `GameGuild.API.IntegrationTests/MonetizationPermissionHttpTests.cs` — end-to-end HTTP
  coverage against real PostgreSQL: `GET /api/metrics/product` (ViewAnalytics),
  `POST /v1/subscription-plans` (Configure) and `PUT /v1/products/{id}/pricing`
  (Monetize, both the endpoint and command gates), asserting 200-with-grant,
  403-without, cross-tenant and expired-grant fail-closed behavior, SystemAdmin
  bypass, no mutation on denial, and `PermissionOperationType.Check` decision-audit rows.
- `GameGuild.Identity.Authorization.UnitTests/MonetizationPermissionTests.cs` — registry,
  facade, wildcard, and CQRS `AuthorizationBehavior` enforcement (allow / deny /
  unauthenticated / system-admin bypass).
- `GameGuild.Identity.Authorization.UnitTests/Handlers/ResourcePermissionAuthorizationFilterTests.cs`
  — controller-level enforcement plus decision audit (granted, denied, audit failure does
  not change the decision).
- `GameGuild.Analytics.UnitTests/MonetizationAnalyticsWiringTests.cs` — metrics surfaces gated.
- `GameGuild.Commerce.Subscriptions.UnitTests/SubscriptionPlanMonetizationWiringTests.cs` —
  plan configuration gated; read-only and self-service endpoints intentionally not gated.
- `GameGuild.Commerce.Products.UnitTests/SetProductPricingMonetizationWiringTests.cs` —
  pricing command gated; controller gate preserved.
- `GameGuild.Audit.UnitTests/Services/AuditServiceDurabilityTests.cs` — retry-once,
  critical durable fallback, transient-recovery behavior.
