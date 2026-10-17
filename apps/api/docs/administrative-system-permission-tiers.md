# Administrative and system permission tiers (issues #294, #386)

This document defines the two permission tiers introduced by issues #294 and #386:
the **administrative tier** (tenant-scoped management capabilities) and the **system
tier** (platform-scoped operational capabilities). Both are ordinary tenant-level
permission keys — they flow through the standard grant/inheritance/precedence machinery
documented in [effective-permission-resolution.md](effective-permission-resolution.md)
(global defaults → tenant defaults → direct grants → role providers, DENY-WINS) with no
special casing.

## Administrative tier (issue #294)

Typed classes live in
`Modules/GameGuild.Identity.Authorization/Models/TypedPermissions.cs`; policy names in
`Policies.cs`; database-backed policy definitions in `Services/PolicyDefinitionSeeder.cs`.

| Capability | Key | Typed class | Policy | Granted to tenant admins by role providers |
|---|---|---|---|---|
| Manage members | `members:manage` | `ManageMembersPermission` | `Members.Manage` | Yes |
| Manage roles | `roles:manage` | `ManageRolesPermission` | `Roles.Manage` | **No** (delegation-only) |
| Read settings | `settings:read` | `ManageSettingsPermission` | `Settings.Read` | Yes |
| Manage settings | `settings:manage` | `ManageSettingsPermission` | `Settings.Manage` | Yes |
| Read audit data | `audit:read` | `AuditPermission` | `Audit.Read` | Yes (self-service tier) |
| Export audit data | `audit:export` | `AuditPermission` | `Audit.Export` | **No** (delegation-only) |

Policy shape (administrative tier): `TenantMatch` + `AnyOf(RequireAnyRole([...]),
RequireAnyPermission([key]))`. The role fallback preserves the previous blanket
`TenantAdmin`/`SystemAdmin` access so existing administrators keep working; the granular
key makes each capability independently delegable (principle of least privilege).
System administrators additionally bypass every permission rule (platform-wide
authority, see `PermissionRuleEvaluators`).

### Endpoint mapping (issue #294)

- **Member management** — `UserMembershipsController` mutations (add / re-role /
  deactivate / activate / invite resend / invite cancel) require `Members.Manage`.
  Membership reads keep the `Users.ReadSelf` self-service tier. The controller's
  in-code guard additionally honors `members:manage` within the actor's own tenant.
- **Role administration** — every `RolesController` action requires `Roles.Manage`.
  Non-system actors are clamped to their own tenant's roles: creating a global role
  (`TenantId = null`) or mutating a foreign/global role fails closed with 403.
  `Roles.Manage` is deliberately **not** granted to tenant membership roles, so tenant
  owners cannot edit global roles without an explicit delegation.
- **Tenant settings** — `TenantSettingsController` reads require `Settings.Read` and
  mutations `Settings.Manage`. (Previously the whole surface was reachable by any
  authenticated user; it now fails closed for non-administrative callers.)
- **Audit reads (self-service tier)** — `AuditController` reads/searches/statistics,
  `SecurityAuditController` reads and `AuditQueryController` require `Audit.Read`.
  **Tenant isolation:** non-system callers are clamped to their own tenant's audit rows
  by the controllers (`TryClampToTenantScope`); an explicit cross-tenant request or a
  missing tenant context fails closed with 403. `AuditQueryController` achieves the
  same with its built-in `ResolveTenantScope` clamp.
- **Audit exports** — CSV/JSON exports, action-type exports, export progress and the
  scheduled-export lifecycle require `Audit.Export` (system-tier shape, see below);
  they are not part of the tenant-admin self-service tier.

## System tier (issue #386)

| Capability | Key | Typed class | Policy |
|---|---|---|---|
| System configuration | `system:manage-configuration` | `SystemConfigurationPermission` | `SystemConfiguration.Manage` |
| Backup / restore | `system:backup-restore` | `BackupRestorePermission` | `System.BackupRestore` |
| Data migration | `system:data-migration` | `DataMigrationPermission` | `System.DataMigration` |

Policy shape (system tier): `AnyOf(RequireAnyRole(["SystemAdmin"]),
RequireAnyPermission([key]))` — **no `TenantMatch` rule**, matching the previous blanket
`SystemAdmin` policy shape for platform-wide operations.

### Tenant isolation decisions (system-level vs tenant-scoped per key)

- **`system:manage-configuration` — system-level, not tenant-clamped.** Gates the
  `/cache` management group (health, connectivity tests, clear-by-pattern, statistics),
  signing-key rotation (`/auth/signing-keys`) and external billing provider
  configuration (`/billing/external-providers` list/get/enable/disable). These
  operations mutate state that affects every tenant, so there is no per-tenant scope to
  clamp to.
- **`system:backup-restore` — system-level, not tenant-clamped.** Gates audit snapshot
  capture/replay (`/admin/audit-logs/replay`) and event-transport dead-letter recovery
  (`/admin/events`). Recovery operations protect the whole platform. (The audit
  snapshot capture itself is already bound to the caller's authenticated tenant by the
  `CaptureAuditReplayCommand` semantics.)
- **`system:data-migration` — system-level.** Gates the billing provider migration
  dry-run (`/billing/external-providers/migration:dry-run`), the HTTP-exposed
  data-migration assessment surface. Database schema migrations run in the host startup
  pipeline and have no HTTP surface; this key is the operational delegation point for
  migration surfaces that do. **Tenant-scoped legacy-data migration keeps its dedicated
  domain key** `economy:manage:legacy-migration`
  (`EconomyLegacyMigrationAdministrationController`): it migrates one tenant's economy
  data and therefore stays tenant-scoped rather than system-scoped.
- **Delegation model.** System-tier keys are granted through the ordinary permission
  machinery — typically the global-defaults row (`UserId = null, TenantId = null`,
  modifiable with `system:manage-global-defaults`) for platform operators, or direct
  tenant grants when an operator's authority should be scoped to the tenant whose
  context they work in. The permission evaluators bypass the `SystemAdmin` role and
  fail closed for everyone else.

## Registry and enforcement summary

- All nine keys are strongly typed (`TypedPermissions.cs`), exposed via the `Permissions`
  facade, members of `PermissionType` (204–210), auto-discovered by `PermissionRegistry`
  (wildcards `members:*`, `roles:*`, `settings:*`, `audit:*`, `system:*` validate), and
  seeded as database-backed policies by `PolicyDefinitionSeeder`.
- The two `TenantMembershipRolePermissionProvider` implementations grant administrative
  membership roles (owner, admin, TenantAdmin) `members:manage`, `settings:read`,
  `settings:manage` and `audit:read` — mirroring the existing `users:*` projection.
  `roles:manage` and `audit:export` are delegation-only by design.
- Endpoint gating is covered by the matrix tests in
  `GameGuild.API.UnitTests/Core/AuthorizationRoleContractTests.cs`, per-tier permission
  tests in `GameGuild.Identity.Authorization.UnitTests/AdminSystemPermissionsTests.cs`,
  and tenant-clamp tests in `GameGuild.Audit.UnitTests/Services/AuditTenantClampTests.cs`.
