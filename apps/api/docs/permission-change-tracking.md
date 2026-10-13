# Permission change tracking and history (issues #188/#189)

This document describes the governance-audit surfaces added for permission change
tracking (#188) and permission grant/revoke history (#189).

## What is captured, and where

| Mutation | Central EF audit (`PermissionAuditChange`) | PermissionAuditLog entry | Version history |
|---|---|---|---|
| Tenant/resource permission grant/revoke/deny | yes (existing) | yes (existing `GrantTenantPermission`-style handlers) | — |
| `DynamicRole` hierarchy edge change (`ParentRoleId`, `AdditionalParentRoleIds`, `BlockedInheritedPermissions`) | yes (hook extension) | `InheritanceChange` on `RoleHierarchy` **with cascade payload** | — |
| `DynamicRoleAssignment` add/remove | yes (hook extension) | `InheritanceChange` on `RoleAssignment` with the role's expanded effective permissions | — |
| `ConditionalPolicy` mutation | yes (hook extension) | `PolicyChange` | `PolicyVersionHistory` row (append-only) |
| `PolicyDefinitionEntity` mutation | yes (hook extension) | `PolicyChange` | `PolicyVersionHistory` row (append-only) |
| Multi-stage approval stage decision | — | `ApprovalStage` per stage + terminal `Grant` via the guarded flow | — |

The EF change-capture hook (`ApplicationDbContext.IsPermissionChange`) now matches the
governance entity types listed above; previously role-hierarchy edits, dynamic-role
assignments and conditional-policy mutations escaped every audit path. The
`PermissionGovernanceAuditHook` (host) feeds the captured changes into
`IRoleHierarchyAuditService`, which writes the `PermissionAuditLog` entries post-commit
with cascade visibility:

- affected descendant roles (recursive expansion of the inheritance graph),
- affected user count (valid assignments on affected roles),
- the mutated role's inherited-permission delta (before/after parent sets).

`PolicyVersionHistory` rows are appended **inside the mutating repository's unit of
work** (never updated or deleted): either the policy change and its snapshot commit
together, or neither does.

## Multi-stage approvals (#188)

`POST /api/v{version}/authorization/permission-approvals` creates a workflow with one or
more ordered stages; `POST .../{id}/decisions` decides the **current** stage only.

- Every stage decision writes its own `ApprovalStage` audit entry (stage name, decision,
  notes, workflow status after the decision).
- The permission grant executes only after the final stage approves, through the existing
  guarded-mutation flow (`IPermissionGrantService`: versioned, cache-invalidating,
  audited).
- Fail closed: stages decide strictly in order; a stage pinned to a required approver
  accepts that approver only; a rejection terminates the workflow with no grant; a
  completed workflow rejects further decisions.

## Point-in-time reconstruction (#189)

`GET /api/v{version}/authorization/permissions/history/at?timestamp=...&userId=...`
reconstructs a subject's effective permission state at a past instant (self or system
admin only; tenant from request context).

Data sources and their guarantees:

- `ResourceUserPermission` — soft-revoked (`RevokedAt`) and bounded (`ExpiresAt`), so the
  table alone answers "effective at T".
- `TenantPermission` — revocations rewrite the permission array, so the subject's
  `PermissionAuditLog` stream (ResourceType `TenantPermission`) is replayed instead
  (grant and deny channels separated by operation type).
- `DynamicRoleAssignment` — removals hard-delete rows, so surviving rows plus
  `RoleAssignment` audit entries (written by the governance hook) are replayed.
- `DynamicRole` hierarchy — parent edges are replayed backwards from the current graph
  through `RoleHierarchy` audit entries.

**Fail closed:** whenever the requested instant cannot be proven from the available data
(grant predating audit coverage, non-empty pre-audit initial state, role assignments or
hierarchy edges predating their audit coverage), the endpoint returns `422` with the
concrete coverage gaps — it never guesses.

`GET .../permissions/history/inheritance?fromTimestamp=&toTimestamp=` lists
role-inheritance change events (system admin), each carrying the pre/post hierarchy state
and the cascade recorded at mutation time — the recursive expansion of role-to-role grants
over time.

`GET /api/v{version}/authorization/permission-policies/{policyId}/versions?kind=` lists a
policy's append-only version history (system admin).
