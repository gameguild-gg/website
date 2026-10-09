# Discretionary Access Control (DAC) & Permissions

Consolidated summary (replaces prior DAC strategy & related docs).

> **See Also**: [Permission Evaluation Policy](../security/PERMISSION_EVALUATION_POLICY.md) for complete multi-layer conflict resolution rules.

## Hierarchy

1. Tenant Level – global tenant capabilities
2. Content-Type Level – entity type capabilities (Program, Post, etc.)
3. Resource Level – specific entity overrides

Resolution order: Resource → Content-Type → Tenant (first explicit grant wins).

## Permission Entity Hierarchy (PermissionBase) — issues #352 / #357

Every persisted permission-grant entity derives from the abstract `PermissionBase`
(`apps/api/Source/Modules/GameGuild.Identity.Authorization/Entities/PermissionBase.cs`):

```
PermissionBase (abstract; Id, UserId, TenantId, IsActive, ExpiresAt, GrantedAt,
                CreatedAt/UpdatedAt/DeletedAt/Version, validation, expiration,
                audit views, tenant-scope guards, fail-closed parsing)
├── TenantPermission                    (tenant-wide allow/deny grants)
├── ResourceUserPermission              (direct per-resource grants, revocation-state)
└── WithPermissions                     (comma-separated PermissionType payload)
    ├── ContentTypePermission           (layer 2 above)
    └── ResourcePermission<TResource>   (layer 3 above)
        └── GenericResourcePermission   (mapped table: genericresourcepermission)
```

- **Table-per-type without a mapped hierarchy root**: each entity keeps its own table and
  maps the inherited common column block (`Id, UserId, TenantId, IsActive, ExpiresAt,
  GrantedAt, CreatedAt, UpdatedAt, DeletedAt, Version`) into it — no destructive merge.
- **Fail closed**: raw payload values (numeric or name form) are validated with
  `Enum.IsDefined` and undefined values are dropped, never cast; inactive, expired or
  soft-deleted grants convey nothing (`IsEffective()`/`Grants()`).
- **Audited mutations**: `ApplicationDbContext` captures Added/Modified/Deleted entries of
  permission entities (including `UserRole`) and flushes them as centralized
  `PermissionAuditLog` records; role-assignment removal is a soft delete
  (`DeletedAt`) so permission history is preserved and re-assignment restores the row.

## Conflict Resolution (DAC Layer)

**Policy: Allow-Wins (Additive)**

DAC permissions are merged from all sources:
- Global defaults + Tenant defaults + Direct grants = Effective permissions

```
EffectivePermissions = GlobalDefaults ∪ TenantDefaults ∪ DirectGrants
```

**No explicit deny**: Revoking a permission removes the grant; there is no "deny" entry.

> ⚠️ **Note**: DAC is evaluated AFTER Rule-Based and ABAC layers. A deny from those layers overrides any DAC grant. See [Permission Evaluation Policy](../security/PERMISSION_EVALUATION_POLICY.md).

## Permission Types

Composable flags (Create, Read, Update, Delete, Review, Moderate, ManageMembers, etc.) combined per scope.

## Generic Attributes

```csharp
[RequireTenantPermission(PermissionType.ManageMembers)]
[RequireContentTypePermission<Program>(PermissionType.Create)]
[RequireResourcePermission<Program>(PermissionType.Update)]
// Legacy compatibility: [RequireResourcePermission<Program>(PermissionType.Read)]
```

## ProgramContent Inheritance

ProgramContent inherits Program permissions; dedicated ProgramContent rows removed to simplify reasoning.

## Seeding Essentials

Initial seed: tenant admin + baseline content-type grants to avoid early 401/403 responses.

## Adding a Secured Entity

1. Define entity + repository
2. Register content-type permission mapping
3. Decorate endpoints with attributes
4. Extend seeding (optional defaults)
5. Add tests (tenant / content-type / resource scenarios)
