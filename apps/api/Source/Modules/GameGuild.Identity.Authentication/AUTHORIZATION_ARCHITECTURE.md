# Authorization Architecture Documentation

## 2026-10-05 Web3 backend identity and session boundary — #292 / #291

Web3 nonce generation now accounts for the actual bounded host cache and requires
both nonce/challenge and wallet bindings to be retained. Trusted-clock expiry,
canonical SIWE/chain checks, mixed-case checksum validation and atomic local
consumption precede account lookup and credential issuance. Unknown/evicted or
already consumed nonces fail closed; shared-store/replica acceptance is separate.

The verified insert-only provider key resolves a persisted user. Wallet possession
does not verify or automatically link an email account. Stored account status and
active tenant membership precede the existing hashed refresh generator and required
session binding. JWT claims use the stored user version and actual session GUID.
The command transaction rolls back required identity/credential writes on failure;
an already consumed nonce cannot be retried. Expected authentication denials use
the existing sanitized 401 exception mapping. The native five-criterion scope and
execution boundaries are documented in
[Web3 backend reconciliation](../../../../../docs/architecture/web3-backend-authentication-reconciliation.md).
The parent retains provider/UI, linking and complete product journey requirements.

## 2026-10-05 unavailable-account token boundary — #262 / #263

Otherwise active refresh issuance requires a user returned by the existing live-user repository,
before tenant provisioning, token generation or session mutation. Missing/deleted
accounts receive generic invalid-refresh denial before issuance; revoked/replaced
replay still commits containment of extant tokens/sessions without inventing a
profile or version update. Fallback email/version identities
are no longer minted. A production user JWT with a valid `token_version` also
requires a current live-user version; null lookup results reject the bearer and
clear its identity through the same protected/public boundary as revoked tokens.

Versionless legacy tokens and service-account tokens retain their existing flow.
The production service token generator carries `actor_kind=Service` and no user
version; no user profile is invented for that machine identity. This correction
does not change JWT claims, keys, schema, TTL policy or public response contracts.
Actual PostgreSQL/production-JWT deleted-account failures, successful controls
and whole-endpoint concurrent refresh acceptance are recorded in
[lifecycle reconciliation](../../../../../docs/architecture/refresh-token-lifecycle-reconciliation.md).

## 2026-10-05 active bearer revocation — #262 / #263

The active host calls `UseTokenRevocation` after authentication and before tenant,
actor and authorization construction. Existing configured JTI/user revocation and
stored user token-version checks now govern protected requests. A rejected identity
is cleared; protected responses use generic 401 Problem Details and a Bearer challenge.
Explicit anonymous endpoints continue with an anonymous identity, preserving public
sign-in/recovery/health when the client still carries an old revoked JWT.

Five signed-JWT relational baseline failures and three passing controls are retained.
Whole-flow acceptance for the bounded #262 requirement is being executed; #263 still
requires persisted family lineage, full issuance/race acceptance, session-specific
revocation, actor guards, alerts/audit and scheduled cleanup/metrics. Legacy tokens
without a version claim retain the existing compatibility behavior. Configured cache
provider/distributed acceptance is separate from persisted replay version invalidation.
See [the acceptance record](../../../../../docs/architecture/bearer-revocation-reconciliation.md).

**Module:** GameGuild.Identity.Authentication  
**Date:** November 10, 2025  
**Version:** 1.0

---

## Table of Contents
1. [Overview](#overview)
2. [Authorization Layers](#authorization-layers)
3. [Role-Based Access Control (RBAC)](#1-role-based-access-control-rbac)
4. [Direct Permission Assignment](#2-direct-permission-assignment)
5. [Attribute-Based Access Control (ABAC)](#3-attribute-based-access-control-abac)
6. [Conditional Access Policies](#4-conditional-access-policies)
7. [Access Review & Compliance](#5-access-review--compliance)
8. [Authorization Decision Flow](#authorization-decision-flow)
9. [Real-World Examples](#real-world-examples)
10. [Design Benefits](#design-benefits)
11. [Implementation Status](#implementation-status)
12. [Best Practices](#best-practices)

---

## Overview

The GameGuild Authentication module implements a **sophisticated, multi-layered hybrid authorization system** that combines multiple access control paradigms to provide maximum flexibility, security, and compliance capabilities.

### Key Features
- **Multiple Authorization Models**: RBAC, ABAC, Permission-based, Conditional
- **Defense in Depth**: Multiple layers of security checks
- **Context-Aware**: Considers user attributes, environment, time, location, device
- **Compliance-Ready**: Built-in access review and audit capabilities
- **Performance Optimized**: Caching layer for authorization decisions
- **Flexible & Extensible**: Mix and match approaches based on requirements

### Architecture Philosophy
The system follows a **layered evaluation model** where each request passes through multiple authorization checks, from most restrictive (conditional policies) to most granular (direct permissions). This ensures both security and flexibility.

---

## Authorization Layers

The authorization system is structured in **five distinct layers**, evaluated in a specific order:

```
┌─────────────────────────────────────────────────────────────┐
│                    Authorization Request                     │
│              "Can User X do Action Y on Resource Z?"         │
└────────────────────────────┬────────────────────────────────┘
                             │
                ┌────────────┴────────────┐
                │  Layer 1: Conditional   │  ← Security & Context
                │  Access Policies        │     (MFA, Location, Time)
                └────────────┬────────────┘
                             │
                ┌────────────┴────────────┐
                │  Layer 2: ABAC          │  ← Attribute-Based
                │  Policies               │     (User/Resource Attributes)
                └────────────┬────────────┘
                             │
                ┌────────────┴────────────┐
                │  Layer 3: Direct        │  ← Fine-Grained
                │  Permissions            │     (Tenant/Resource/Type)
                └────────────┬────────────┘
                             │
                ┌────────────┴────────────┐
                │  Layer 4: Role-Based    │  ← Hierarchical
                │  Permissions (RBAC)     │     (Role → Permissions)
                └────────────┬────────────┘
                             │
                             ▼
                    ✅ Allow / ❌ Deny
```

**Evaluation Order**:
1. **Conditional Policies** - Can block immediately (security checks)
2. **ABAC Policies** - Dynamic attribute-based rules
3. **Direct Permissions** - Explicit permission grants
4. **Role Permissions** - Inherited from roles
5. **Default Deny** - If no match, deny access

---

## 1. Role-Based Access Control (RBAC)

### Status: ⚠️ **PLANNED - NOT YET IMPLEMENTED**

### Concept
Traditional hierarchical permission management where users are assigned **roles**, and roles contain sets of **permissions**.

### Architecture

```
┌──────────┐     assigned to    ┌──────────┐    contains    ┌──────────────┐
│   User   │ ─────────────────> │   Role   │ ────────────> │  Permission  │
└──────────┘                     └──────────┘                └──────────────┘
     │                                │                              │
     │                                │                              │
  User A                         "Admin"                    "users:write"
  User B                         "Editor"                   "posts:write"
  User C                         "Viewer"                   "posts:read"
```

### Benefits
- **Simple to Understand**: Easy mental model for administrators
- **Easy to Manage**: Group permissions into meaningful roles
- **Hierarchical**: Roles can inherit from other roles
- **Audit-Friendly**: Clear role assignments

### Typical Roles Structure
```
Role: "Administrator"
  Permissions:
    - users:*           (all user operations)
    - posts:*           (all post operations)
    - settings:*        (all settings operations)
    - audit:read        (view audit logs)

Role: "Editor"
  Permissions:
    - posts:read
    - posts:write
    - posts:publish
    - media:upload

Role: "Viewer"
  Permissions:
    - posts:read
    - comments:read
```

### Permission Format
```
<resource>:<action>

Examples:
  - users:read
  - users:write
  - users:delete
  - posts:*          (wildcard for all actions)
  - *:read           (read all resources)
```

### PermissionRegistry (Auto-Discovery)

The `PermissionRegistry` class provides **automatic discovery** of all permission scopes at startup. Instead of manually registering permissions, simply create a class that inherits from `Permission`:

```csharp
// 1. Define your permission class (auto-discovered at startup)
public sealed class MyFeaturePermission : Permission
{
    // Compile-time constants for use in attributes
    public static class Keys
    {
        public const string Read = "myfeature:read";
        public const string Write = "myfeature:write";
    }
    
    // Runtime permission instances with metadata
    public static readonly MyFeaturePermission Read = new(Keys.Read, "Read my feature data");
    public static readonly MyFeaturePermission Write = new(Keys.Write, "Write my feature data");
    
    private MyFeaturePermission(string key, string description) : base(key, description) { }
}

// 2. Use the registry for validation
if (!PermissionRegistry.IsValidKey(permissionKey))
    throw new ArgumentException($"Unknown permission: {permissionKey}");

// 3. Get all permissions for a resource
var userPermissions = PermissionRegistry.GetByResource("users");

// 4. Get all registered scopes (for documentation/admin UI)
foreach (var scope in PermissionRegistry.Scopes)
{
    Console.WriteLine($"Resource: {scope.Resource}");
    Console.WriteLine($"  Wildcard: {scope.Wildcard}");
    foreach (var perm in scope.Permissions)
        Console.WriteLine($"  - {perm.Key}: {perm.Description}");
}
```

**Key Benefits:**
- **OCP Compliant**: Add new permissions without modifying existing code
- **Single Source of Truth**: All permissions discovered from code
- **Compile-Time Safety**: Use `Keys` constants in attributes (e.g., `[Authorize(Policy = Policies.HasPermission, ...)]`)
- **Runtime Validation**: `IsValidKey()` validates permission strings including wildcards
- **Self-Documenting**: Each permission carries its description for admin UIs

See [PermissionRegistry.cs](../GameGuild.Identity.Authorization/PermissionRegistry.cs) for implementation.

### Use Cases
- **Common Access Patterns**: Define standard roles for common job functions
- **Department-Based Access**: Engineering, Marketing, Sales roles
- **Hierarchical Organizations**: Manager, Team Lead, Individual Contributor
- **Multi-Tenant Systems**: Tenant Admin, Tenant Member, Guest

### Planned Implementation
See [Section 2: Roles Management](./IMPLEMENTATION_STATUS.md#2-roles-management) in IMPLEMENTATION_STATUS.md for detailed requirements.

---

## 2. Direct Permission Assignment

### Status: ✅ **FULLY IMPLEMENTED**

### Concept
Fine-grained access control through **direct permission grants** at three distinct levels, independent of roles.

### 2.1 Tenant Permissions

**Scope**: Organization/tenant-wide capabilities

**Entity**: `TenantPermission`
```csharp
{
    UserId: Guid,
    TenantId: Guid,
    Permission: string,     // e.g., "billing:manage"
    GrantedAt: DateTime,
    GrantedBy: Guid,
    ExpiresAt: DateTime?    // Optional: temporary access
}
```

**Use Cases**:
- Billing administration for entire organization
- Tenant-wide settings management
- Organization analytics access
- Workspace administration

**Example**:
```csharp
// Grant user ability to manage billing for Tenant ABC
GrantTenantPermission(
    userId: "user-123",
    tenantId: "tenant-abc",
    permission: "billing:manage"
)
```

**Endpoints**:
- `POST /v1/permissions/tenant/grant`
- `POST /v1/permissions/tenant/revoke`
- `POST /v1/permissions/tenant/check`
- `POST /v1/permissions/tenant/list`
- `POST /v1/permissions/tenant/bulk-grant`
- `POST /v1/permissions/tenant/bulk-revoke`

### 2.2 Resource Permissions

**Scope**: Specific resource instances

**Entity**: `ResourcePermission`
```csharp
{
    UserId: Guid,
    ResourceId: Guid,       // Specific document, project, etc.
    ResourceType: string,   // "Document", "Project", etc.
    Permission: string,     // e.g., "edit", "share", "delete"
    GrantedAt: DateTime,
    GrantedBy: Guid
}
```

**Use Cases**:
- Document sharing (user can edit Document #123)
- Project collaboration (user can manage Project #456)
- Record-level access control
- Delegation of specific items

**Example**:
```csharp
// Allow user to edit specific document
GrantResourcePermission(
    userId: "user-123",
    resourceId: "doc-456",
    resourceType: "Document",
    permission: "edit"
)
```

**Endpoints**:
- `POST /v1/permissions/resource/grant`
- `POST /v1/permissions/resource/revoke`
- `POST /v1/permissions/resource/bulk-grant`

### 2.3 Content-Type Permissions

**Scope**: All resources of a specific type

**Entity**: `ContentTypePermission`
```csharp
{
    UserId: Guid,
    ContentType: string,    // "BlogPost", "Comment", etc.
    Permission: string,     // e.g., "create", "publish"
    TenantId: Guid?,
    GrantedAt: DateTime
}
```

**Use Cases**:
- Allow user to create all Blog Posts
- Grant moderation access to all Comments
- Enable publishing capability for all Articles
- Type-based bulk permissions

**Example**:
```csharp
// Allow user to create any blog post
GrantContentTypePermission(
    userId: "user-123",
    contentType: "BlogPost",
    permission: "create"
)
```

**Endpoints**:
- `POST /v1/permissions/content-type/grant`
- `POST /v1/permissions/content-type/revoke`

### 2.4 Permission Templates

**Purpose**: Pre-defined permission sets for common scenarios

**Features**:
- **Template Library**: Reusable permission configurations
- **Bulk Application**: Apply multiple permissions at once
- **Consistency**: Ensure standard access patterns
- **Quick Setup**: Onboard users faster

**Example Template**:
```json
{
    "name": "Content Creator Bundle",
    "description": "Standard permissions for content creators",
    "permissions": [
        { "type": "ContentType", "contentType": "BlogPost", "permission": "create" },
        { "type": "ContentType", "contentType": "BlogPost", "permission": "edit" },
        { "type": "ContentType", "contentType": "Media", "permission": "upload" },
        { "type": "Tenant", "permission": "analytics:view" }
    ]
}
```

**Endpoint**:
- `POST /v1/permissions/template/apply`

### 2.5 Permission Caching

**Purpose**: Performance optimization for permission checks

**Features**:
- **Hybrid Cache**: Per-instance L1 memory with optional shared Redis-backed L2; misses continue to the database source of truth.
- **TTL and Capacity**: Separate configurable lifetimes for policies, permissions, ACLs, and rulesets, with bounded L1 size.
- **Versioned Invalidation**: Tenant/user security versions, key tracking, bulk invalidation, and Redis Pub/Sub propagation keep grants and revocations coherent across instances.
- **Bulk Decision Cache**: When the hybrid permission cache and tenant security-version store are registered, bulk decisions are cached across calls. Keys include the user, tenant, permission, content type, resource, and both tenant and global security versions. The service re-reads the versions after evaluation and retries if they changed, so a result evaluated across a permission mutation is not returned or cached under the earlier version. If three consecutive snapshots change, the batch fails instead of returning a potentially stale decision. Without both collaborators, checks use the database-backed batch path without decision caching.
- **Bounded Bulk Evaluation**: Collection-based checks process at most 256 requests per database batch and preserve input order. Streaming checks default to 128 requests per batch; callers may select any size from 1 to 256 and process decisions as they are yielded.
- **Warmup and Metrics**: Manual and popularity-based warmup plus hit, miss, eviction, and latency metrics.
- **Health Alerts**: A periodic monitor logs cache health and emits structured warnings for low hit rate or high average latency after the configured minimum sample size.

**Administrative endpoints** (system-admin authorization applies to statistics and warmup):
- `GET /v1/permissions/cache/stats`
- `POST /v1/permissions/cache:clear`
- `POST /v1/permissions/cache:warm`

Metrics use the `Authorization:Cache` options, including `EnableMetrics`, `MetricsLoggingIntervalSeconds`, `MinimumHitRateWarningThreshold`, `MinimumRequestsForPerformanceWarning`, and `LookupLatencyWarningThresholdMilliseconds`.

---

## 3. Attribute-Based Access Control (ABAC)

### Status: ✅ **FULLY IMPLEMENTED**

### Concept
Dynamic, context-aware authorization based on **attributes** of users, resources, and environment, evaluated at runtime using **policy expressions**.

### Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      ABAC Policy Engine                      │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │    User      │  │   Resource   │  │ Environment  │      │
│  │  Attributes  │  │  Attributes  │  │  Attributes  │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
│         │                  │                  │             │
│         └──────────────────┴──────────────────┘             │
│                          │                                  │
│                          ▼                                  │
│              ┌─────────────────────┐                        │
│              │  Policy Expression  │                        │
│              │  Evaluation Engine  │                        │
│              └─────────────────────┘                        │
│                          │                                  │
│                          ▼                                  │
│                  Allow or Deny                              │
└─────────────────────────────────────────────────────────────┘
```

### Attribute Sources

#### User Attributes
```csharp
{
    "department": "Engineering",
    "seniority": "Senior",
    "location": "US-West",
    "clearanceLevel": 3,
    "employmentType": "FullTime",
    "costCenter": "CC-1234",
    "certifications": ["AWS", "Azure"]
}
```

#### Resource Attributes
```csharp
{
    "classification": "Confidential",
    "owner": "user-456",
    "department": "Finance",
    "createdAt": "2025-01-15",
    "dataResidency": "EU",
    "tags": ["sensitive", "pii"]
}
```

#### Environmental Attributes
```csharp
{
    "currentTime": "2025-11-10T14:30:00Z",
    "dayOfWeek": "Monday",
    "ipAddress": "192.168.1.100",
    "ipLocation": "US-CA",
    "deviceType": "Desktop",
    "networkType": "Corporate"
}
```

#### Contextual Attributes
```csharp
{
    "mfaVerified": true,
    "sessionRiskScore": 0.2,
    "trustedDevice": true,
    "requestOrigin": "WebApp"
}
```

### Policy Structure

```csharp
public class AbacPolicy
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    
    // Expression using attributes
    public string Expression { get; set; }
    
    // Effect of the policy
    public PolicyEffect Effect { get; set; }  // Allow, Deny
    
    // Resources this applies to
    public string[] ResourceTypes { get; set; }
    
    // Actions covered
    public string[] Actions { get; set; }
    
    // Priority for conflict resolution
    public int Priority { get; set; }
    
    public bool IsActive { get; set; }
    public Guid TenantId { get; set; }
}
```

### Expression Language

Supports complex boolean logic:

```javascript
// Example 1: Department and Seniority
user.department == "Engineering" AND user.seniority >= "Senior"

// Example 2: Time-Based Access
user.department == "Support" AND 
environment.currentTime >= "09:00" AND 
environment.currentTime <= "18:00"

// Example 3: Data Classification
resource.classification == "Public" OR 
(resource.classification == "Internal" AND user.employmentType == "FullTime")

// Example 4: Complex Conditions
(user.clearanceLevel >= resource.requiredClearance) AND
(user.location == resource.dataResidency OR user.hasOverride == true) AND
context.mfaVerified == true

// Example 5: Ownership Check
resource.owner == user.id OR 
user.department == resource.department AND user.role == "Manager"
```

### Operators Supported
- **Comparison**: `==`, `!=`, `>`, `<`, `>=`, `<=`
- **Logical**: `AND`, `OR`, `NOT`
- **Membership**: `IN`, `NOT IN`
- **Pattern**: `MATCHES`, `CONTAINS`
- **Null Check**: `IS NULL`, `IS NOT NULL`

### Policy Examples

#### Example 1: Deployment Restrictions
```json
{
    "name": "Production Deploy Restrictions",
    "expression": "user.department == 'Engineering' AND user.seniority >= 'Senior' AND resource.environment == 'production' AND context.mfaVerified == true",
    "effect": "Allow",
    "resourceTypes": ["Deployment"],
    "actions": ["deploy", "rollback"],
    "priority": 10
}
```

#### Example 2: Data Access Control
```json
{
    "name": "PII Data Access",
    "expression": "resource.containsPII == true AND (user.certifications CONTAINS 'DataPrivacy' OR user.clearanceLevel >= 4)",
    "effect": "Allow",
    "resourceTypes": ["CustomerRecord", "EmployeeRecord"],
    "actions": ["read", "export"],
    "priority": 20
}
```

#### Example 3: Geographic Restrictions
```json
{
    "name": "EU Data Residency",
    "expression": "resource.dataResidency == 'EU' AND user.location IN ['EU-West', 'EU-Central']",
    "effect": "Allow",
    "resourceTypes": ["*"],
    "actions": ["read", "write"],
    "priority": 5
}
```

### Endpoints

**Policy Management**:
- `POST /v1/abac/policies` - Create policy
- `GET /v1/abac/policies` - List policies
- `GET /v1/abac/policies/{id}` - Get policy details
- `PUT /v1/abac/policies/{id}` - Update policy
- `DELETE /v1/abac/policies/{id}` - Delete policy
- `POST /v1/abac/policies/{id}/activate` - Activate policy
- `POST /v1/abac/policies/{id}/deactivate` - Deactivate policy
- `POST /v1/abac/policies/{id}/clone` - Clone policy
- `POST /v1/abac/policies/from-template` - Create from template

**Policy Evaluation**:
- `POST /v1/abac/evaluate` - Evaluate if action is allowed
- `POST /v1/abac/bulk-evaluate` - Evaluate multiple actions
- `POST /v1/abac/test-expression` - Test expression logic
- `POST /v1/abac/validate` - Validate policy syntax

### Use Cases

1. **Department-Based Access**:
   - Finance users can access financial reports
   - HR users can view employee records

2. **Seniority-Based Operations**:
   - Only senior engineers can deploy to production
   - Managers can approve expenses over $10,000

3. **Data Classification**:
   - Confidential data requires higher clearance
   - Public data accessible to all

4. **Time-Based Access**:
   - Support team access only during business hours
   - After-hours access requires special approval

5. **Location-Based Restrictions**:
   - EU data only accessible from EU locations
   - Sensitive operations require corporate network

6. **Ownership & Delegation**:
   - Users can edit their own resources
   - Department managers can access team resources

---

## 4. Conditional Access Policies

### Status: ✅ **FULLY IMPLEMENTED**

### Concept
Real-time access control based on **runtime conditions** with the ability to **require additional verification** or **block access** based on security context.

### Differences from ABAC
| Aspect | ABAC | Conditional Policies |
|--------|------|---------------------|
| **Focus** | User/Resource attributes | Access conditions & security context |
| **Evaluation** | Allow/Deny | Allow/Deny/RequireVerification |
| **Priority** | Policy-based | Explicit priority order |
| **Use Case** | Business logic | Security enforcement |
| **Examples** | "Senior engineers only" | "Require MFA for sensitive data" |

### Policy Structure

```csharp
public class ConditionalPolicy
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    
    // Priority (lower = evaluated first)
    public int Priority { get; set; }
    
    // Conditions to evaluate
    public PolicyCondition[] Conditions { get; set; }
    
    // Action to take
    public PolicyAction Action { get; set; }  // Block, Allow, RequireMfa, RequireApproval
    
    // Effect
    public PolicyEffect Effect { get; set; }  // Allow, Deny
    
    // Resources affected
    public string[] ResourceTypes { get; set; }
    
    public bool IsActive { get; set; }
}

public class PolicyCondition
{
    public PolicyConditionType Type { get; set; }
    public string Operator { get; set; }  // Equals, NotEquals, Contains, etc.
    public string Value { get; set; }
}
```

### Condition Types

```csharp
public enum PolicyConditionType
{
    // Location-based
    IpAddress,
    IpLocation,
    Country,
    
    // Time-based
    TimeOfDay,
    DayOfWeek,
    DateRange,
    
    // Device-based
    DeviceType,
    DeviceTrusted,
    OperatingSystem,
    
    // Security-based
    MfaVerified,
    RiskScore,
    SessionAge,
    
    // Resource-based
    ResourceClassification,
    ResourceOwner,
    ResourceAge,
    
    // User-based
    UserRole,
    UserDepartment,
    AccountAge
}
```

### Policy Actions

```csharp
public enum PolicyAction
{
    Block,              // Immediately deny access
    Allow,              // Allow with no additional requirements
    RequireMfa,         // Require MFA verification
    RequireApproval,    // Require manager approval
    RequireJustification, // Require access justification
    StepUpAuth          // Require re-authentication
}
```

### Policy Examples

#### Example 1: MFA for Sensitive Data
```json
{
    "name": "Require MFA for Confidential Data",
    "priority": 1,
    "conditions": [
        {
            "type": "ResourceClassification",
            "operator": "Equals",
            "value": "Confidential"
        },
        {
            "type": "MfaVerified",
            "operator": "Equals",
            "value": "false"
        }
    ],
    "action": "RequireMfa",
    "effect": "Deny",
    "resourceTypes": ["Document", "Report", "CustomerData"]
}
```

#### Example 2: Geographic Restrictions
```json
{
    "name": "Block Access from High-Risk Countries",
    "priority": 5,
    "conditions": [
        {
            "type": "Country",
            "operator": "In",
            "value": "CN,RU,KP"
        }
    ],
    "action": "Block",
    "effect": "Deny",
    "resourceTypes": ["*"]
}
```

#### Example 3: Business Hours Only
```json
{
    "name": "Restrict After-Hours Access",
    "priority": 10,
    "conditions": [
        {
            "type": "TimeOfDay",
            "operator": "NotBetween",
            "value": "09:00-18:00"
        },
        {
            "type": "DayOfWeek",
            "operator": "In",
            "value": "Monday,Tuesday,Wednesday,Thursday,Friday"
        },
        {
            "type": "UserRole",
            "operator": "NotEquals",
            "value": "Administrator"
        }
    ],
    "action": "Block",
    "effect": "Deny"
}
```

#### Example 4: Untrusted Device Restriction
```json
{
    "name": "Untrusted Device Limitations",
    "priority": 3,
    "conditions": [
        {
            "type": "DeviceTrusted",
            "operator": "Equals",
            "value": "false"
        }
    ],
    "action": "Block",
    "effect": "Deny",
    "resourceTypes": ["PaymentInfo", "BankAccount"]
}
```

#### Example 5: High-Risk Score
```json
{
    "name": "Elevated Risk Score",
    "priority": 2,
    "conditions": [
        {
            "type": "RiskScore",
            "operator": "GreaterThan",
            "value": "0.7"
        }
    ],
    "action": "StepUpAuth",
    "effect": "Deny"
}
```

### Endpoints

**Policy Management**:
- `POST /v1/conditional/policies` - Create policy
- `GET /v1/conditional/policies` - List policies
- `GET /v1/conditional/policies/{id}` - Get policy
- `PUT /v1/conditional/policies/{id}` - Update policy
- `DELETE /v1/conditional/policies/{id}` - Delete policy
- `POST /v1/conditional/policies/{id}/activate` - Activate
- `POST /v1/conditional/policies/{id}/deactivate` - Deactivate
- `PUT /v1/conditional/policies/{id}/priority` - Update priority
- `POST /v1/conditional/policies/{id}/clone` - Clone policy
- `POST /v1/conditional/policies/from-template` - Create from template

**Policy Evaluation**:
- `POST /v1/conditional/evaluate` - Evaluate policies
- `POST /v1/conditional/bulk-evaluate` - Bulk evaluate
- `POST /v1/conditional/simulate` - Simulate policy impact
- `POST /v1/conditional/validate` - Validate policy
- `POST /v1/conditional/validate-condition` - Validate condition
- `POST /v1/conditional/test-rule` - Test policy rule

### Use Cases

1. **Zero Trust Security**:
   - Require MFA for sensitive operations
   - Verify device trust status
   - Continuous authentication

2. **Compliance Requirements**:
   - Geographic data restrictions
   - Time-based access controls
   - Audit trail requirements

3. **Risk-Based Access**:
   - Block high-risk sessions
   - Require additional verification for anomalies
   - Step-up authentication for sensitive actions

4. **Device Management**:
   - Corporate device requirements
   - OS version compliance
   - Trusted device registration

5. **Incident Response**:
   - Emergency access restrictions
   - Rapid policy deployment
   - Temporary access controls

---

## 5. Access Review & Compliance

### Status: ✅ **FULLY IMPLEMENTED**

### Concept
Periodic review and audit of access permissions to ensure compliance with security policies and regulations.

### Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                  Access Review Workflow                      │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  1. Create Campaign                                          │
│     ├─ Define scope (users, resources, permissions)         │
│     ├─ Assign reviewers                                      │
│     └─ Set deadline                                          │
│                                                              │
│  2. Generate Review Items                                    │
│     ├─ Collect all permissions                               │
│     ├─ Group by user/resource/type                           │
│     └─ Create review tasks                                   │
│                                                              │
│  3. Review Process                                           │
│     ├─ Reviewers evaluate access                             │
│     ├─ Approve or revoke permissions                         │
│     ├─ Add justifications                                    │
│     └─ Escalate if needed                                    │
│                                                              │
│  4. Execute Actions                                          │
│     ├─ Revoke denied permissions                             │
│     ├─ Update audit trail                                    │
│     └─ Notify affected users                                 │
│                                                              │
│  5. Generate Reports                                         │
│     ├─ Compliance status                                     │
│     ├─ Review statistics                                     │
│     └─ Remediation actions                                   │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

### Campaign Types

#### 1. One-Time Campaign
Manual access review for specific purpose:
```json
{
    "name": "Q4 2025 Access Review",
    "startDate": "2025-11-01",
    "endDate": "2025-11-30",
    "scope": {
        "userGroups": ["Engineering", "Finance"],
        "resourceTypes": ["FinancialReport", "CustomerData"],
        "permissionTypes": ["Admin", "Write"]
    },
    "reviewers": ["manager-1", "security-lead"]
}
```

#### 2. Periodic Campaign
Recurring automated reviews:
```json
{
    "name": "Quarterly Admin Review",
    "frequency": "Quarterly",
    "autoStart": true,
    "scope": {
        "permissionTypes": ["Admin", "Owner"]
    },
    "reviewers": ["security-team"],
    "reminderSettings": {
        "enabled": true,
        "frequency": "Weekly"
    }
}
```

### Review Item Structure

```csharp
public class AccessReviewItem
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    
    // What's being reviewed
    public Guid UserId { get; set; }
    public string Permission { get; set; }
    public Guid? ResourceId { get; set; }
    public string ResourceType { get; set; }
    
    // Review details
    public ReviewStatus Status { get; set; }  // Pending, Approved, Revoked
    public Guid? ReviewerId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string Justification { get; set; }
    public string Comments { get; set; }
    
    // Context
    public DateTime PermissionGrantedAt { get; set; }
    public DateTime LastUsed { get; set; }
    public int UsageCount { get; set; }
}
```

### Compliance Features

#### Compliance Status Tracking
```csharp
public class ComplianceStatus
{
    public int TotalReviews { get; set; }
    public int CompletedReviews { get; set; }
    public int PendingReviews { get; set; }
    public int OverdueReviews { get; set; }
    
    public int PermissionsRevoked { get; set; }
    public int PermissionsApproved { get; set; }
    
    public double ComplianceScore { get; set; }  // 0-100
    public List<ComplianceFlag> Flags { get; set; }
}

public class ComplianceFlag
{
    public string Type { get; set; }      // "StalePermission", "UnusedAccess", etc.
    public string Severity { get; set; }  // "Critical", "High", "Medium", "Low"
    public string Description { get; set; }
    public Guid[] AffectedUsers { get; set; }
}
```

### Analytics & Reporting

#### Review Analytics
- Review completion rates
- Average review time
- Permissions revoked vs approved
- Most reviewed resource types
- Top reviewers by volume

#### Access Patterns
- Permission usage statistics
- Last access timestamps
- Stale permission identification
- Privilege creep detection

### Endpoints

**Campaign Management** (7 endpoints):
- `POST /v1/access-review/campaigns`
- `GET /v1/access-review/campaigns`
- `GET /v1/access-review/campaigns/{id}`
- `PUT /v1/access-review/campaigns/{id}`
- `DELETE /v1/access-review/campaigns/{id}`
- `POST /v1/access-review/campaigns/{id}/start`
- `POST /v1/access-review/campaigns/{id}/complete`

**Review Items** (4 endpoints):
- `GET /v1/access-review/campaigns/{campaignId}/items`
- `GET /v1/access-review/items/{itemId}`
- `POST /v1/access-review/items/{itemId}/review`
- `POST /v1/access-review/items/bulk-review`

**Periodic Reviews** (4 endpoints):
- `POST /v1/access-review/periodic`
- `PUT /v1/access-review/periodic/{id}`
- `DELETE /v1/access-review/periodic/{id}`
- `POST /v1/access-review/periodic/{id}/trigger`

**Revocation & Compliance** (4 endpoints):
- `POST /v1/access-review/revoke`
- `POST /v1/access-review/bulk-revoke`
- `GET /v1/access-review/compliance`
- `PUT /v1/access-review/compliance/flags`

**Reports & Analytics** (3 endpoints):
- `POST /v1/access-review/reports/generate`
- `GET /v1/access-review/analytics`
- `GET /v1/access-review/history`

**Templates & Reminders** (4 endpoints):
- `GET /v1/access-review/templates`
- `POST /v1/access-review/campaigns/from-template`
- `POST /v1/access-review/reminders/send`
- `PUT /v1/access-review/reminders/configure`

### Use Cases

1. **Regulatory Compliance**:
   - SOX: Annual access reviews for financial systems
   - HIPAA: Quarterly reviews for healthcare data access
   - GDPR: Regular reviews of personal data access

2. **Security Audits**:
   - Privilege escalation detection
   - Stale permission cleanup
   - Orphaned account identification

3. **Organizational Changes**:
   - Department transfers
   - Role changes
   - Employee departures

4. **Risk Management**:
   - High-privilege access monitoring
   - Third-party access reviews
   - Temporary access expiration

---

## Authorization Decision Flow

### Complete Evaluation Process

```
┌─────────────────────────────────────────────────────────────┐
│                 AUTHORIZATION REQUEST                        │
│         Can User X perform Action Y on Resource Z?          │
└────────────────────────────┬────────────────────────────────┘
                             │
                             ▼
           ┌─────────────────────────────────┐
           │  Gather Context & Attributes    │
           │  ├─ User attributes             │
           │  ├─ Resource attributes         │
           │  ├─ Environment context         │
           │  └─ Session metadata            │
           └─────────────────┬───────────────┘
                             │
                             ▼
┌────────────────────────────────────────────────────────────┐
│  LAYER 1: CONDITIONAL ACCESS POLICIES (Priority Order)     │
│  ├─ Check MFA requirement                                  │
│  ├─ Verify device trust                                    │
│  ├─ Validate IP/Location                                   │
│  ├─ Check time restrictions                                │
│  └─ Evaluate risk score                                    │
└────────────────────────────┬───────────────────────────────┘
                             │
                    ┌────────┴────────┐
                    │   BLOCK?        │
                    └────────┬────────┘
                             │
                 ┌───────────┴───────────┐
                 │                       │
                YES                     NO
                 │                       │
                 ▼                       ▼
           ❌ DENY                 Continue
         (Security Block)               │
                                        │
                                        ▼
┌────────────────────────────────────────────────────────────┐
│  LAYER 2: ABAC POLICIES                                    │
│  ├─ Match user attributes                                  │
│  ├─ Match resource attributes                              │
│  ├─ Evaluate policy expressions                            │
│  └─ Apply highest priority matching policy                 │
└────────────────────────────┬───────────────────────────────┘
                             │
                    ┌────────┴────────┐
                    │   EXPLICIT      │
                    │   DENY?         │
                    └────────┬────────┘
                             │
                 ┌───────────┴───────────┐
                 │                       │
                YES                     NO
                 │                       │
                 ▼                       ▼
           ❌ DENY                 Continue
         (ABAC Deny)                    │
                                        │
                                        ▼
┌────────────────────────────────────────────────────────────┐
│  LAYER 3: DIRECT PERMISSIONS                               │
│  ├─ Check Resource Permission (specific item)              │
│  ├─ Check Content-Type Permission (all of type)            │
│  └─ Check Tenant Permission (org-wide)                     │
└────────────────────────────┬───────────────────────────────┘
                             │
                    ┌────────┴────────┐
                    │   FOUND?        │
                    └────────┬────────┘
                             │
                 ┌───────────┴───────────┐
                 │                       │
                YES                     NO
                 │                       │
                 ▼                       ▼
            ✅ ALLOW               Continue
         (Direct Grant)                 │
                                        │
                                        ▼
┌────────────────────────────────────────────────────────────┐
│  LAYER 4: ROLE-BASED PERMISSIONS (✅ Implemented)          │
│  ├─ Get user's roles (GetUserRolesQuery)                   │
│  ├─ Aggregate permissions from all roles                   │
│  └─ Check if permission exists in any role                 │
└────────────────────────────┬───────────────────────────────┘
                             │
                    ┌────────┴────────┐
                    │   FOUND?        │
                    └────────┬────────┘
                             │
                 ┌───────────┴───────────┐
                 │                       │
                YES                     NO
                 │                       │
                 ▼                       ▼
            ✅ ALLOW              ❌ DENY
         (Role Grant)        (No Permission)
                                        │
                                        ▼
                            ┌───────────────────┐
                            │   Log Decision    │
                            │   Audit Trail     │
                            │   Metrics         │
                            └───────────────────┘
```

### Decision Priority

**Explicit Deny > Explicit Allow > Default Deny**

1. **Conditional Policy Block**: Highest priority - immediate denial
2. **ABAC Explicit Deny**: Second priority - policy-based denial
3. **ABAC Explicit Allow**: Third priority - policy-based allow
4. **Direct Permission**: Fourth priority - explicit grant
5. **Role Permission**: Fifth priority - inherited grant
6. **Default Deny**: If no allow found, deny by default

### Caching Strategy

```
┌─────────────────────────────────────────────────────────────┐
│                    Authorization Cache                       │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  Cache Key: user:{id}:resource:{id}:action:{action}         │
│  TTL: 5 minutes (configurable)                              │
│                                                              │
│  Invalidation Triggers:                                      │
│  ├─ Permission grant/revoke                                  │
│  ├─ Role assignment change                                   │
│  ├─ Policy activation/deactivation                           │
│  └─ User logout                                              │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

---

## Real-World Examples

### Example 1: Document Access

**Scenario**: User trying to edit a confidential document

```csharp
AuthorizationRequest request = new()
{
    UserId = "user-123",
    Action = "edit",
    ResourceType = "Document",
    ResourceId = "doc-456"
};

// Step 1: Conditional Policy Check
ConditionalPolicy mfaPolicy = "Require MFA for Confidential Documents";
if (document.Classification == "Confidential" && !user.MfaVerified)
{
    return new AuthorizationResult
    {
        Allowed = false,
        Reason = "MFA verification required",
        RequiredAction = "PerformMfa"
    };
}

// Step 2: ABAC Policy Check
AbacPolicy departmentPolicy = "Department Restricted Documents";
string expression = "resource.department == user.department OR user.role == 'Manager'";
if (!EvaluateExpression(expression, user, document))
{
    return AuthorizationResult.Deny("Department restriction");
}

// Step 3: Direct Permission Check
if (HasResourcePermission(userId: "user-123", resourceId: "doc-456", permission: "edit"))
{
    return AuthorizationResult.Allow("Direct permission grant");
}

// Step 4: Role Permission Check (when implemented)
if (UserHasRoleWithPermission(userId: "user-123", permission: "documents:edit"))
{
    return AuthorizationResult.Allow("Role-based permission");
}

// Step 5: Default Deny
return AuthorizationResult.Deny("No permission found");
```

### Example 2: Production Deployment

**Scenario**: Engineer attempting to deploy to production

```csharp
// Context
User user = new()
{
    Id = "eng-789",
    Department = "Engineering",
    Seniority = "Senior",
    MfaVerified = true,
    Location = "US-West"
};

Resource resource = new()
{
    Type = "Deployment",
    Environment = "Production",
    Application = "PaymentService"
};

// Layer 1: Conditional Policies
if (environment.CurrentTime.Hour < 9 || environment.CurrentTime.Hour > 17)
{
    return Deny("Production deploys only allowed during business hours (9am-5pm)");
}

if (environment.DayOfWeek == DayOfWeek.Friday)
{
    return Deny("No Friday production deploys (change freeze)");
}

// Layer 2: ABAC
string expression = @"
    user.department == 'Engineering' AND 
    user.seniority >= 'Senior' AND 
    context.mfaVerified == true AND
    (resource.application NOT IN criticalApps OR user.hasEmergencyAccess == true)
";

if (!EvaluateAbacPolicy(expression))
{
    return Deny("ABAC policy: Only senior engineers with MFA can deploy");
}

// Layer 3: Direct Permissions
if (HasContentTypePermission(userId, "Deployment", "execute"))
{
    // Log the deployment
    AuditLog.Record(new DeploymentEvent
    {
        User = user.Id,
        Environment = "Production",
        Application = resource.Application,
        Timestamp = DateTime.UtcNow
    });
    
    return Allow("Direct deployment permission");
}

// Layer 4: Roles (when implemented)
if (UserHasRole(userId, "DevOps Engineer"))
{
    return Allow("DevOps Engineer role");
}

return Deny("Insufficient permissions for production deployment");
```

### Example 3: Sensitive Data Export

**Scenario**: User trying to export customer PII data

```csharp
// Context
User user = new()
{
    Id = "analyst-456",
    Department = "Analytics",
    DataPrivacyCertified = true,
    RiskScore = 0.15
};

Resource resource = new()
{
    Type = "CustomerData",
    Classification = "PII",
    RecordCount = 50000
};

// Layer 1: Conditional - Risk Assessment
if (user.RiskScore > 0.5)
{
    return Deny("High risk score detected - access suspended");
}

if (!user.MfaVerified)
{
    return RequireMfa("MFA required for PII data export");
}

if (!session.FromTrustedDevice)
{
    return Deny("PII exports only allowed from trusted devices");
}

// Layer 2: ABAC - Data Privacy Compliance
string expression = @"
    resource.classification == 'PII' AND
    user.dataPrivacyCertified == true AND
    user.department IN ['Legal', 'Compliance', 'Analytics'] AND
    resource.recordCount <= 100000
";

if (!EvaluateAbacPolicy(expression))
{
    return Deny("Data privacy policy violation");
}

// Require justification for large exports
if (resource.RecordCount > 10000)
{
    return RequireJustification("Justification required for large data exports");
}

// Layer 3: Direct Permissions
if (HasTenantPermission(userId, "data:export-pii"))
{
    // Create access request for approval
    AccessRequest request = CreateAccessRequest(new()
    {
        UserId = user.Id,
        ResourceType = resource.Type,
        Action = "export",
        RecordCount = resource.RecordCount,
        RequiresApproval = true,
        ApproverRoles = ["Data Protection Officer"]
    });
    
    return Pending("Access request created - awaiting approval", request.Id);
}

return Deny("No PII export permission");
```

---

## Design Benefits

### 1. **Flexibility**

**Multiple Approaches**: Choose the right tool for each use case
- Simple scenarios → Roles
- Fine-grained → Direct Permissions
- Complex logic → ABAC
- Security requirements → Conditional Policies

**Gradual Adoption**: Start simple, add complexity as needed
```
Phase 1: Roles + Direct Permissions (traditional)
Phase 2: Add ABAC for complex rules
Phase 3: Layer Conditional Policies for security
Phase 4: Implement Access Reviews for compliance
```

### 2. **Security**

**Defense in Depth**: Multiple layers of protection
- Conditional policies block insecure contexts
- ABAC enforces business rules
- Direct permissions provide granular control
- Regular reviews catch privilege creep

**Context-Aware**: Decisions based on full context
- Who is making the request
- What they're trying to access
- When and where they're accessing from
- How they're accessing (device, network)
- Why (justification, approval)

**Zero Trust Principles**:
- Never trust, always verify
- Least privilege access
- Assume breach mentality
- Continuous verification

### 3. **Compliance**

**Audit Trail**: Complete visibility
- Every decision logged
- Who approved what, when
- Changes tracked over time
- Access patterns analyzed

**Regular Reviews**: Automated compliance
- Periodic access certification
- Stale permission cleanup
- Privilege escalation detection
- Compliance reporting

**Regulatory Support**:
- SOX: Financial system access controls
- HIPAA: Healthcare data protection
- GDPR: Personal data access management
- ISO 27001: Information security

### 4. **Performance**

**Caching Layer**: Fast authorization decisions
- In-memory permission cache
- TTL-based invalidation
- Distributed cache support
- Cache hit rate monitoring

**Bulk Operations**: Efficient at scale
- Bulk permission grants
- Batch policy evaluation
- Parallel processing
- Optimized database queries

**Lazy Evaluation**: Only compute what's needed
- Short-circuit on deny
- Skip unnecessary checks
- Conditional layer first
- Cache before compute

### 5. **Maintainability**

**Clear Separation**: Each layer has distinct purpose
- Roles: Common patterns
- Permissions: Exceptions & fine-tuning
- ABAC: Complex business logic
- Conditional: Security enforcement

**Template-Based**: Reusable configurations
- Permission templates
- Policy templates
- Campaign templates
- Best practices codified

**Self-Service**: Reduce admin burden
- Delegated administration
- Request/approval workflows
- Automated reviews
- Policy simulation

---

## Implementation Status

### ✅ Fully Implemented (95% Complete)

1. **Direct Permissions** ✅
   - Tenant permissions
   - Resource permissions
   - Content-type permissions
   - Permission templates
   - Caching system
   - 13 endpoints

2. **ABAC Policies** ✅
   - Policy CRUD operations
   - Expression evaluation engine
   - Policy templates
   - Bulk evaluation
   - 13 endpoints

3. **Conditional Policies** ✅
   - Policy management
   - Priority-based evaluation
   - Condition validation
   - Policy simulation
   - 16 endpoints

4. **Access Reviews** ✅
   - Campaign management
   - Review workflows
   - Compliance tracking
   - Analytics & reporting
   - 26 endpoints

### ⚠️ Planned - Not Implemented (5% Remaining)

1. **Role-Based Access Control** ❌
   - Role entity
   - Role repository
   - Role commands/handlers
   - Role queries
   - Role-user assignments
   - Role-permission mappings
   - 5 endpoints (placeholders exist)

**Missing Components**:
```
- Role.cs entity
- IRoleRepository interface
- RoleRepository implementation
- CreateRoleCommand + Handler
- UpdateRoleCommand + Handler
- DeleteRoleCommand + Handler
- AssignRoleToUserCommand + Handler
- RemoveRoleFromUserCommand + Handler
- GetRolesQuery + Handler
- GetRoleByIdQuery + Handler
- GetUserRolesQuery + Handler
- RoleDto, CreateRoleRequest, UpdateRoleRequest
```

**Estimated Effort**: 3-5 days for full RBAC implementation

### Current Capabilities

**Without RBAC**, the system still provides:
- ✅ Fine-grained direct permissions
- ✅ Complex attribute-based rules
- ✅ Security-focused conditional policies
- ✅ Compliance & access reviews

**With RBAC** (once implemented), adds:
- Simplified administration for common patterns
- Hierarchical permission management
- Traditional role-based workflows
- Role templates for new users

---

## Best Practices

### 1. **Layer Usage Guidelines**

**Use Conditional Policies for**:
- Security requirements (MFA, device trust)
- Geographic restrictions
- Time-based access
- Risk-based decisions

**Use ABAC for**:
- Complex business logic
- Dynamic attribute-based rules
- Cross-cutting concerns
- Temporary/contextual access

**Use Direct Permissions for**:
- Specific exceptions
- Fine-grained control
- Individual resource access
- Temporary grants

**Use Roles for** (when implemented):
- Common access patterns
- Job function definitions
- Department-based access
- Standard user onboarding

### 2. **Performance Optimization**

**Cache Aggressively**:
```csharp
// Cache permission checks
CacheKey = $"auth:{userId}:{resourceId}:{action}";
TTL = 5 minutes;

// Invalidate on changes
OnPermissionGrant() => ClearCache(userId);
OnPolicyUpdate() => ClearPolicyCache();
```

**Batch Operations**:
```csharp
// Instead of checking one by one
foreach (resource in resources)
{
    await CheckPermission(userId, resource.Id, action);
}

// Bulk check
var results = await BulkCheckPermissions(userId, resourceIds, action);
```

**Short-Circuit Evaluation**:
```csharp
// Stop on first deny
if (ConditionalPolicyBlocks()) return Deny;
if (AbacPolicyDenies()) return Deny;
// Continue to next layer
```

### 3. **Security Best Practices**

**Default Deny**:
```csharp
// Always end with deny
return AuthorizationResult.Deny("No permission found");
```

**Explicit Over Implicit**:
```csharp
// Good: Explicit permission
GrantPermission(userId, "documents:edit", documentId);

// Avoid: Wildcard for everything
GrantPermission(userId, "*:*");  // Too broad
```

**Least Privilege**:
```csharp
// Good: Minimal permission
GrantPermission(userId, "reports:read");

// Avoid: Excessive permission
GrantPermission(userId, "reports:*");  // Includes delete
```

**Regular Reviews**:
```csharp
// Quarterly review of admin access
CreatePeriodicReview(
    frequency: "Quarterly",
    scope: { permissionTypes: ["Admin", "Owner"] }
);
```

### 4. **Policy Design**

**Keep Policies Simple**:
```javascript
// Good: Simple, readable
user.department == "Engineering"

// Avoid: Overly complex
(user.department == "Engineering" OR user.role == "Contractor") 
AND (resource.classification != "Secret" OR user.clearance >= 5)
AND (environment.time >= "09:00" AND environment.time <= "17:00")
// Split into multiple policies
```

**Use Descriptive Names**:
```csharp
// Good
"Require MFA for Confidential Data Access"

// Avoid
"Policy_123"
```

**Document Policies**:
```csharp
Policy.Description = @"
    This policy requires MFA verification when accessing 
    confidential classified data. Applies to all users 
    except administrators with emergency access override.
    
    Compliance: SOC 2 requirement A.1.2.3
";
```

### 5. **Monitoring & Alerting**

**Track Key Metrics**:
- Authorization decision latency
- Cache hit rate
- Policy evaluation count
- Permission grant/revoke rate
- Failed authorization attempts

**Alert on Anomalies**:
- Spike in denials
- Unusual access patterns
- Policy conflicts
- Performance degradation

**Audit Everything**:
```csharp
AuditLog.Record(new AuthorizationEvent
{
    UserId = user.Id,
    Action = action,
    Resource = resource.Id,
    Decision = decision,
    Reason = reason,
    Policies = evaluatedPolicies,
    Timestamp = DateTime.UtcNow,
    IpAddress = context.IpAddress,
    UserAgent = context.UserAgent
});
```

### 6. **Testing Strategy**

**Unit Test Policies**:
```csharp
[Test]
public void SeniorEngineers_CanDeployToProduction()
{
    var user = new User { Department = "Engineering", Seniority = "Senior" };
    var resource = new Resource { Environment = "Production" };
    
    var result = policyEngine.Evaluate("Production Deploy", user, resource);
    
    Assert.That(result.Allowed, Is.True);
}
```

**Integration Test Layers**:
```csharp
[Test]
public async Task Authorization_ChecksAllLayers()
{
    // Setup conditional policy (MFA required)
    // Setup ABAC policy (department restriction)
    // Setup direct permission
    
    var result = await authService.CheckAccess(userId, resourceId, action);
    
    // Verify correct layer granted/denied
}
```

**Load Test Performance**:
```csharp
[Test]
public async Task Authorization_PerformsUnderLoad()
{
    // Simulate 1000 concurrent authorization checks
    var tasks = Enumerable.Range(0, 1000)
        .Select(_ => authService.CheckAccess(userId, resourceId, action));
    
    await Task.WhenAll(tasks);
    
    Assert.That(averageLatency, Is.LessThan(50 milliseconds));
}
```

---

## Platform Authorization Hardening

### Refresh-token lifecycle audit and metrics

The identity module emits credential-free `RefreshTokenLifecycleEvent` records through
`IRefreshTokenLifecycleRecorder`. The host stores issuance, rotation, explicit revocation,
account-wide revocation and completed replay containment in the same database transaction
as the token and session mutations. A failed transaction retains neither these audit rows
nor a committed-operation metric. Pure rejections use a separate scoped context so a
denied command cannot erase their audit evidence. Replay containment still returns the
server-only commit-on-denial result; it never returns credentials.

Audit records contain opaque owner/token/parent/session identifiers and bounded reasons.
An owner identifier recovered from storage does not authenticate the requester. Tenant
context comes from resolved membership or an already authenticated owner, never an
unverified requested tenant. Raw credentials, hashes, emails and request headers are
excluded from lifecycle metadata. Audit storage failures propagate; the existing
best-effort authentication transport cannot erase this transactional evidence.

`GameGuild.Identity.Authentication.RefreshTokens` is registered in the host's OpenTelemetry
meter provider. The attempt counter counts entry to an operation. The persisted-outcome
counter is emitted only after `SaveChanges` has committed independently or the owning EF
transaction has committed. Rollback and transaction failure discard staged counts.
Tags are limited to operation, outcome and reason enums; no account, tenant, session,
credential or IP appears in metric dimensions. Metrics describe the current process;
the database audit is the authoritative history, including across restarts. A failing
telemetry listener cannot reverse a successful database commit.

### Security email acceptance

Security email rows require a rendered message and an `IConfirmedEmailSender`
receipt whose `Accepted` flag is true before the dispatcher marks them Sent.
A disabled sender, missing message or sender without that capability follows the
existing retry and dead-letter path. Provider acceptance is distinct from its
optional message identifier; a successful response without an identifier is valid.
SMTP, SES and SendGrid expose that distinction without changing `IEmailSender.SendAsync`.

The registered Security renderer HTML-encodes the message, excludes persisted
metadata and directs the recipient to the ordinary trusted application address.
Provider acceptance does not prove inbox placement or that the recipient read it.
The durable replay producer, retry/idempotence integration and the full #263
acceptance remain under validation; this delivery boundary alone does not close it.

### Persisted refresh-token parent and session lineage

Refresh tokens have nullable `ParentTokenId` and `SessionId` foreign keys to the
persisted predecessor and session. Existing rows retain unknown metadata as null;
the migration does not guess bindings from timestamps, users or token hashes.
New sessions bind their stored refresh token to the real session after creation.
Successful refresh rotation records the predecessor ID only after winning the
existing atomic revocation claim. Session creation/refresh and rotation require
`IRefreshTokenLineageRepository` from the same configured token repository;
custom stores must implement this capability. Manual sessions created without a
refresh credential retain their existing behavior. An authentication session
with a supplied credential must successfully bind its persisted token; a missing
row fails before success audit or returned credentials.

Binding validates token/session ownership, matching hashes, active state, expiry
and immutable existing bindings. Rotation additionally requires the claimed
parent's matching successor hash and compatible owned session. The repository
reads the parent outside the change tracker because SQL atomic claims bypass it;
adding metadata must not overwrite the persisted revocation with stale state.
An incomplete lineage write cannot return successful authentication credentials.
Credential login retains its generic failure response; refresh failures and
cancellation propagate. The host command transaction rolls back unsuccessful
database mutations. Legacy parents can be
bound only by an observed successful rotation with the owned persisted session.

Retention deletes eligible leaves before predecessors. An active or otherwise
retained descendant keeps its complete known chain; sessions referenced by any
retained token are kept. Restrictive foreign keys reject dangling references and
prevent deletion from silently breaking lineage. Downgrading the migration drops
these metadata columns and their links while retaining token rows, hashes and
revocation state; newly written lineage is consequently lost on downgrade.

This covers credential/session flows and local refresh rotation. It does not
establish complete issuance metadata for every external authentication provider,
nor all original #263 audit, alert, scheduled cleanup and metrics requirements.
Those original criteria remain open until their separate evidence is accepted.

### Refresh credential expiration and session limits

`Jwt:RefreshTokenSlidingExpiration` defaults to `true`, retaining renewal on each
successful refresh. With `false`, rotation preserves the predecessor's deadline
or a shorter configured TTL. Both modes retain `SessionOptions.AbsoluteTimeoutMinutes`
measured from the original session creation; rotation cannot reset that boundary.
The existing idle timeout and active-session checks remain mandatory.

Session binding and rotation cap the stored refresh credential at the actual
persisted session deadline. Credential sign-in, sign-up, OAuth/Discord and Web3
report that deadline in both refresh expiration response fields. A null, foreign,
inactive, expired or wrongly bound session cannot return successful credentials.
The provider issuer for magic-link/WebAuthn retains its persisted-token/session
checks. `RefreshTokenExpirationDays` remains the canonical TTL configuration;
the historical `RefreshTokenExpiryInDays` fallback remains supported.

This boundary is validated by real HTTP/PostgreSQL cases for both sliding modes
and one-day/thirty-day absolute limits. Complete #263 acceptance remains separate
from those four expiration cases.

### Authenticated self revocation across all sessions

`POST /v1/auth/sessions:terminate-all` binds `RevokeAllUserTokensCommand`.
The handler requires an authenticated User actor with a nonempty GUID subject;
the command carries no user selector. It revokes that user's active refresh
tokens, terminates their sessions with the existing logout reason, advances
their stored token version once and records a user revocation cutoff through
the existing distributed revocation service. The IP comes from the host
connection. The route and response shape are preserved.

The host command transaction covers the database mutations. Persistence,
revocation-store failures and cancellation propagate; the endpoint must not
report successful logout after an incomplete write. The cache and PostgreSQL
do not share a transaction: a cutoff already written before a later database
commit failure remains a denial of earlier tokens. This is a conservative
failure outcome, and callers must sign in again or retry after the failure.
The cutoff records the minimum token version advanced by that operation. A
signed token at or above that version is not rejected solely because its
second-granular iat precedes the cutoff's fractional second. Tokens without a
version and ordinary time-only cutoffs retain the earlier timestamp rule;
the stored database version and session validity checks still run. Both
configured stores implement the required typed version boundary. A custom
store must implement that capability for the guarded self-revocation command.
The session-only command remains separate for ending other sessions, MFA
containment and refresh-replay containment; those paths retain their own
existing version/security policy. No raw credentials are added to events.

All 19 original #263 criteria remain authoritative. This increment covers the
existing all-session entry point; explicit parent/session token lineage,
complete token-operation audit/alerts, scheduled retention and metrics still
require separate acceptance.

### Explicit Refresh-Token Revocation Ownership

The self-service revoke command requires an authenticated `User` actor with a
nonempty GUID subject from `IActorContextAccessor`. Anonymous, service, system,
webhook and external actors are rejected before token hashing or repository reads.
Stored refresh-token ownership must match that subject before the existing revoke
service can mutate a token or its linked session. Request/command `UserId`, tenant
administration and system-administrator roles do not grant cross-user access to
this self-service operation. The HTTP controller retains its host-observed IP;
request-body IP is not audit evidence.

Unknown tokens keep the existing invalid-token contract. Successful own-token
revocation still marks the token revoked and terminates its linked session with
`UserLogout`, without revoking unrelated users or advancing the account version.
This ownership guard does not establish all #263 lineage, cleanup, telemetry or
alert-delivery requirements. Original issue criteria remain authoritative.

Session-bound access tokens additionally require a single, valid nonempty
`session_id` claim, an existing active/unexpired session without termination, and
a session owner matching the token subject. The production revocation middleware
reads stored session state on each authenticated session-bound request. Missing,
expired, terminated, malformed or cross-user sessions clear identity and reject
protected requests with generic401. Explicit anonymous endpoints continue with
no stale actor. Legacy user and service tokens without a session claim retain
their existing JTI/user-version compatibility; this does not invent session
binding for those historical tokens. No session state is changed by validation.

### Refresh Token Repository Predicates

Active-token listing and user-wide revocation use mapped `IsRevoked` and
`ExpiresAt` columns and one captured UTC timestamp. The ignored computed
`IsActive` property must not appear in SQL predicates. Queries stay in the
database and retain user isolation, strict expiry and current active-row behavior.
This repository correction does not establish family lineage, complete service
rotation atomicity or revocation consumption in the host's bearer pipeline.

Refresh replay containment returns a server-only denial only after required token,
session and applicable account-version writes succeed. Its explicit `ICommitOnFailureOutcome`
preserves those mutations through the command transaction while the endpoint still
returns generic 401. Business failure classification remains unchanged. Ordinary
failed outcomes and exceptions keep rollback behavior; request data cannot supply
the commit contract. Profile mapping preserves the internal denial without fetching
or exposing an account. Separate PostgreSQL HTTP and transaction cases verify
containment persistence and ordinary failure/exception rollback.

`Jwt:RefreshTokenReplayContainmentScope` selects `Family` (default) or `Account`.
Family containment uses the persisted, owner-checked session binding, terminates
that session, and revokes its descendants without changing the account token
version. Signed bearers from that family are denied by the session guard; other
owned sessions remain usable. Account containment retains the existing all-session
revocation and version increment. A legacy token without a provable persisted
family falls back to account containment. Explicit revoke-all is independent.
Session metadata updates cannot clear committed termination state or reactivate a
family from a stale tracked object. Family termination locks its session before
querying descendants; a waiting refresh must pass the current persisted binding
checks and cannot acknowledge an orphan replacement. Unknown policy values fail
configuration validation.

[Scope and remaining acceptance](../../../../../docs/architecture/refresh-token-rotation-reconciliation.md).

### MFA Recovery State

New recovery codes use salted, versioned PBKDF2-HMAC-SHA256 (600,000 iterations)
and default to 12 characters. Legacy SHA-256 codes remain verifiable until used or
regenerated. Both formats use constant-time hash comparison. Issued-set metadata
preserves the original count; legacy total/used counts are explicitly unknown.
The v1 counters keep their numeric types. For unknown legacy history they return
lower bounds and `areUsageCountsKnown=false`; configuration's issued count is null.
Status endpoints disclose counts without hashes or plaintext.

MFA row updates compare the original backup set, failure counter, lockout,
enablement, setup completion and encrypted TOTP secret. A conflicting write reloads
the row before a bounded retry. Success requires a committed consumption; stale
failed attempts cannot restore consumed codes. Pending setup codes cannot complete
enrollment. Successful TOTP confirmation records setup completion. Cancellation
propagates through the verifier and orchestrator.
New enrollments have a fixed persisted expiration which failed attempts cannot
extend; legacy pending rows use the prior timestamp fallback.

### Polymorphic Password Entry Point

`POST /v1/auth/polymorphic` is explicitly anonymous and reviewed in the host
allowlist, with the existing authentication rate limit. The CQRS command has a
durable use-case event contract which excludes credentials and issued tokens.
The Users module performs bounded two-candidate lookup without depending on the
Authentication module. Zero or multiple undeleted matches are denied; no first
match or email fallback can authenticate an unresolved identifier.

Resolved account identity is server-only, reloaded before password verification
and cannot be supplied through JSON. The existing local flow still performs
password verification, generic denial/timing/attempt recording, risk analysis,
tenant membership resolution, JWT issuance and persisted session creation.
Tenant in the anonymous request selects a membership to validate; it is not an
authenticated tenant claim. IP and user agent come from the existing HTTP context.
Device fingerprint is an observational hint passed to risk and session storage,
not independent proof of device trust. High-risk results retain step-up handling
without issuing a completed login session.

Phone lookup uses the stored canonical international string, without silently
rewriting legacy data or asserting phone ownership. Unsupported password
identifier types use generic denial. [Requirement and execution map](../../../../../docs/architecture/polymorphic-signin-reconciliation.md).

### Password History Acceptance

Current-password and five previous-hash checks are retained in both change and
reset. Expected-current-hash updates and EF concurrency tokens protect one
committed history/version transition across competing requests. Mixed-format
history remains bounded and excluded from serialized users and responses.
Fresh migrated PostgreSQL/HTTP definitions and their controlled race timing are
recorded in the [#251 acceptance map](../../../../../docs/architecture/password-history-reconciliation.md).
Fixture-principal/token-service execution does not certify external email
delivery or real bearer identity proofing; those acceptance boundaries stay open.

### Password Hash Boundaries

Password writes retain policy checks, history rejection and the original-hash
concurrency guard. Validated BCrypt cost remains 12 by default, configurable from
10 to 16. New inputs beyond 72 UTF-8 bytes use explicitly identified salted
PBKDF2-HMAC-SHA256 with 600,000 iterations and full-input verification. Long input
cannot authenticate against a truncated legacy BCrypt hash; recovery creates a
full-length hash. History conservatively rejects reuse of an ambiguous legacy
prefix. Legacy suffixes cannot be reconstructed or certified from stored hashes.
See [the requirement and compatibility map](../../../../../docs/architecture/password-hashing-reconciliation.md).

### Authentication Response Projection

Authentication response conversion preserves server-issued tokens, explicit
expirations, tenant/session identity, challenge flags and risk metadata. Repository
profile lookup uses the server response's user ID; an embedded response profile
cannot override the persisted identity or verified-email assertion. Complete names
are projected without changing the entity. A stored phone is disclosed only when
authentication succeeds with a nonempty access token and neither MFA nor step-up
is still required. Phone possession is not inferred as phone verification.

The public legacy refresh converter preserves supplied expiry/duration/profile;
it never extends a supplied expired timestamp. Only a genuinely missing expiry
may be derived from a positive duration. Conversion copies mutable containers and
does not mutate sources, write accounts or bypass authentication/authorization.

This section documents the authorization-hardening invariants of the common platform
modules. Everything below applies to the platform modules that are shared verbatim
across products; product-specific behavior hooks in exclusively through the documented
extension points.

### New-User Handle Assignment

New user factories assign a lowercase ASCII handle independently of the display
name. Accents are removed, separators become hyphens, and compatible dots and
underscores remain supported. Existing stored handles, including legacy nulls,
are preserved on reads and display-name updates. Local signup validates the
canonical candidate before persistence or token issuance and passes the chosen
handle separately from the display name.

The user repository reserves handles across persisted users (including deleted
rows) and unsaved batch members. Automatically generated collisions receive a
bounded user-ID suffix; an explicitly chosen collision returns a Username
validation error without silently renaming the choice. PostgreSQL's existing
unique index remains the final concurrent-write guard. Only its username-specific
unique violation can trigger one generated-handle save retry. The retry preserves
entity versions and durable-event capture; email and other persistence failures
retain their existing failure path. No user schema or authorization claim changes
are required.

### JWT Algorithm and Additional-Claim Boundaries

Access-token issuance, all `JwtTokenService` validation paths and the API's active
JWT bearer registration use the recorded HS256 policy. Each validator explicitly
restricts `ValidAlgorithms` to HS256; a valid signature under another algorithm
does not satisfy this token contract. `GetPrincipalFromExpiredToken` bypasses
lifetime validation only, retaining signature, algorithm, issuer and audience checks.

The public additional-claims overload preserves legitimate custom values, repeated
claims and their JSON value types. It rejects null/blank claim entries and reserved
protocol, identity, tenant, session, role, permission, MFA and actor claims, including
aliases used by current extractors and configured authorization claim names.
Callers must use typed issuance parameters for server-owned identity and authorization
data; arbitrary additional claims cannot replace or extend those security assertions.
Existing public method signatures and HTTP response contracts are preserved.

`JwtGenerationValidationPolicyTests` and `JwtBearerValidationHttpTests` exercise
issuance, unsupported algorithms, tampering, wrong keys/issuer/audience, time validity,
custom claims and reserved-claim rejection. The bearer fixture invokes the actual
host authentication registration over HTTP; it does not certify session/account
revocation or external-provider acceptance tracked by separate requirements.

### Fail-Closed Permission Mapping (CQRS `AuthorizationBehavior`)

- The behavior resolves authorization requirements through the **typed**
  `AuthorizeRequestAttribute` only. Attributes that merely share the *name*
  `AuthorizeRequestAttribute` (different type/namespace) are ignored — no
  name-based reflection matching.
- Resource-level checks map the permission string to an `AccessLevel`
  (`manage/admin/delete/remove` → Admin; `write/edit/update/create` → Write;
  `read/view/get/list` → Read). **Unknown permission patterns throw
  `UnauthorizedAccessException`** — they are never silently defaulted to `Write`.
  A typo can only fail closed.

### Permission Template Application (`ApplyPermissionTemplateCommand`)

Applying a template mutates another user's permissions, so the handler enforces
(defense-in-depth beyond controller attributes):

1. the actor must be authenticated;
2. **system templates** require `system:manage-global-defaults` (or SystemAdmin);
3. **tenant templates** require tenant-admin or `permissions:manage`, and the target
   tenant must equal the actor's tenant (SystemAdmin excepted);
4. every successful apply **bumps the tenant security version** (cache invalidation) and
   **writes a `PermissionAuditLog` entry**.

### Permission Cache Clearing (`ClearPermissionCacheCommand`)

The command has a real handler: it bumps the tenant security version through
`ITenantSecurityVersionStore` and evicts local L1 entries through
`ICacheInvalidationService`. Authorization: tenant-scoped clears require SystemAdmin or
tenant-admin of the target tenant; unscoped (global) clears require SystemAdmin. A
user-scoped clear without a tenant fails closed.

### Bulk Permission Operations

`BulkGrantTenantPermissionsCommand`, `BulkRevokeTenantPermissionsCommand`, and
`BulkGrantResourcePermissionsCommand` all have guarded handlers that delegate to the
Authorization-module permission services (which bump versions and audit each mutation).
The guards mirror single grants: tenant-admin within the actor's own tenant or
SystemAdmin; `Guid.Empty` tenant IDs (global defaults) additionally require
`system:manage-global-defaults`. Per-user failures are collected into the
`BulkPermissionResult`, never swallowed.

Single tenant grant, by-ID revoke, revoke, deny, deny-removal, and tenant-default commands
apply the same authenticated same-tenant check (SystemAdmin may cross tenants). Their
actor IDs come from `IActorContextAccessor`; legacy `GrantedBy`/`RevokedBy` fields in
command payloads are not trusted. Permission queries are tenant-bound too, and reading
another user's permissions requires tenant-admin or user-read access. Tenant permission
rows are soft-deleted, excluded from normal queries, and retained as history; the
active-only unique index permits a later grant to create a new row without erasing the
deleted record.

Only **one command type exists per operation** — permission-grant endpoints bind the
guarded Authorization-module commands, and acting-user identity (`GrantedBy`,
`RevokedBy`, `CreatedByUserId`, …) always comes from the authenticated actor context,
never from the request body.

### Automatic Permission Audit Hooks

`ApplicationDbContext` captures added, modified, and deleted EF-tracked entities whose
type or changed property represents permissions. It stores the actor, tenant, target user,
correlation ID, command type, and before/after property snapshots, then sends grouped
permission events to the centralized `IAuditService`. CQRS commands flush those records
after the owning transaction commits; a rollback discards the pending snapshots. Audit
storage and extension-hook failures are logged and do not fail an already-committed
permission mutation.

The default filter audits all permission entities. Hosts may selectively exclude entity
types or EF operation kinds under `Audit:PermissionHooks:ExcludedEntityTypes` and
`Audit:PermissionHooks:ExcludedOperations`. Product-specific behavior can register an
`IPermissionAuditHook`; each hook receives the committed changes and the centralized
audit request. Permission mutations should use the EF unit of work and CQRS operation
pipeline so snapshots are captured and emitted only after successful persistence.

### Per-Product Policy Seed Extension Point

The common `PolicyDefinitionSeeder` seeds **platform-generic policies only**. A product
adds its domain policies by implementing:

```csharp
public interface IPolicySeedContributor
{
    string Name { get; }
    IEnumerable<PolicyDefinitionEntity> BuildPolicies();
}
```

and registering it from the host (or a domain module) with
`services.AddPolicySeedContributor<TContributor>()`. Contributor policies use the same
seeding semantics (create-if-missing, refresh when the stored policy version is older
than the seeder's current version). This product's domain policy gates — role
admissions and permission-based access rules specific to this product — are seeded
through this extension point from the host; the common seeder contains no domain
identifiers.

### Hardened Platform Modules

- **Features** (`GameGuild.Features`): the management controller requires
  authentication plus the `Features.Read` policy (reads) or `Features.Manage` policy
  (mutations). No feature-flag endpoint is public; anonymous callers can never mutate
  flags. Feature *evaluation* for callers happens through the evaluation/SDK surfaces.
- **Ledgers** (`GameGuild.Finance.Ledgers`): controllers require authentication plus
  `Ledgers.Read` / `Ledgers.Write`. The effective tenant is the **actor's tenant** — a
  route-supplied tenant is honored only for SystemAdmin; cross-tenant reads fail
  closed. `CreatedByUserId` is always taken from the actor, never the request body.
- **Assets** (`GameGuild.Assets`): object access validates that the asset's **own
  tenant** matches the request tenant (`TenantMismatch` denial) — membership in the
  request tenant alone is not sufficient. The only escape hatch is the explicit
  `permitCrossTenant` path reserved for SystemAdmin surfaces.
- **Content Pages** (`GameGuild.Content.Pages`): get-by-id and public listing surfaces
  gate unpublished content behind the `content:read`/`content:write`/`content:admin`
  permissions; the anonymous slug/sitemap/catalog surfaces return published content
  only.

### Legacy Permission Facade

The backward-compatible three-layer permission facade bumps the tenant security version
and writes an audit entry on **every mutation path** (grants, revokes, defaults,
content-type and resource grants, expired-permission cleanup). Invalidation failures
propagate — a mutation whose cache invalidation failed is never reported as successful.

### Scheduled refresh-token retention

The host registers an enabled `RefreshTokenCleanupWorker` with a fresh scoped
operation per cycle. `Authentication:RefreshTokenCleanup` validates retention
(default 30 days), batch size (default 500), batches per store (default 10), startup
delay (default two minutes), interval (default one hour), and execution deadline
(default one minute). Cycles run sequentially; failures and timeouts retry at the
next interval, and shutdown cancels both database work and the timer.

Token deletion requires both the original expiration and any revocation timestamp
to precede the retention cutoff. A predecessor survives while a child survives;
an unexpired revoked leaf remains evidence for reuse detection. Sessions survive
while any stored token references them. Each cycle bounds writes in both stores.
Deletion and a redacted system audit row commit in one database transaction.
Audit/storage/cancellation failure rolls the cycle back. Cleanup counters report
committed row counts only after commit, with bounded resource/outcome dimensions.
The scheduler is an internal system operation and exposes no public cleanup API.
Tests disable automatic scheduling explicitly and exercise the worker separately.

### Architecture Rules (enforced by build/tests)

### Verified provider session issuance

Magic-link consumption and WebAuthn authentication completion delegate credential
issuance to `IAuthenticatedSessionIssuer` after verifying the provider identity and
loading an available stored account. Issuance requires an active membership in an
active tenant; pending or cancelled invitations and revoked memberships grant no
access. Login does not provision or reactivate memberships. Requested foreign tenants
are rejected before writing credentials. Roles come from the existing tenant resolver.

The issuer persists a hashed root refresh token, creates its owned session, and checks
the stored binding before generating an access token carrying that session and tenant.
The refresh deadline is capped to the persisted session's absolute deadline. Response
lifetimes come from the actual JWT and stored deadlines. Provider commands run inside
the host command transaction: a binding, persistence, cancellation or issuance failure
must escape that transaction so its writes roll back. Credentials are returned only
after the transaction succeeds. Missing issuer configuration fails closed.

These invariants cover initial session issuance. They do not certify provider delivery,
browser authenticator ceremonies, enterprise federation, or the complete refresh-token
lifecycle requirements; those retain their separate acceptance evidence.

### Architecture enforcement

- **Controller authorization (GGARCH008 + `ControllerAuthorizationArchitectureTests`)**:
  every MVC endpoint must carry `[Authorize]` (class or action) or an explicit
  `[AllowAnonymous]`. The Roslyn analyzer fails the build on unguarded endpoints; the
  unit tests additionally require every anonymous endpoint (action-level, or any endpoint
  of a controller whose class is `[AllowAnonymous]`) to appear in the HOST-side reviewed
  allowlist registry (`AnonymousEndpointRegistry` in the API host's `Security` folder)
  with a one-line justification. The registry is per-product content; the tests and
  analyzer carry no product-specific entries and mirror verbatim.
- **Dependency direction (`ModuleDependencyDirectionTests`)**: platform modules
  (`GameGuild.Identity.*`, `GameGuild.Resources*`, `GameGuild.SharedKernel`,
  `GameGuild.Assets`, `GameGuild.Features`, `GameGuild.Finance.Ledgers`,
  `GameGuild.Content.Pages`) must not reference **domain modules** — every module under
  `Source/Modules` that is not in the platform set is a domain module (the test derives
  this set dynamically; there is no per-product hardcoding) — via project references
  **or** namespace usages. The reverse direction is allowed. Product-specific bridges
  live in the host composition root (e.g. the commerce-backed order-validation adapter
  and the tenant payment-history bridge). Known debt is explicitly listed in the test
  with a removal plan; anything not listed fails the tests.

### Subscription Quota Sync

Plan-change quota sync never swallows failures: each per-quota failure is logged as an
error, and if any quota failed to apply the handler throws an aggregate failure so a
downgrade can never silently skip its new (lower) limits.

---

## Conclusion

The GameGuild Authorization Architecture provides a **comprehensive, enterprise-grade access control system** that balances:

- **Security**: Multi-layered defense with conditional policies
- **Flexibility**: Multiple authorization models for different needs
- **Performance**: Caching and optimization for scale
- **Compliance**: Built-in review and audit capabilities
- **Usability**: Templates and self-service for ease of use

**Role-Based Access Control (RBAC)** is now fully implemented with complete role management (Role entity, RoleController, CQRS handlers for Create/Update/Delete/Assign/Remove roles). The system provides traditional role-based workflows alongside the advanced ABAC and conditional policy systems.

**For More Information**:
- [Implementation Status](./IMPLEMENTATION_STATUS.md)
- [API Documentation](./API_DOCUMENTATION.md)
- [Security Best Practices](./SECURITY.md)
- [Compliance Guide](./COMPLIANCE.md)

---

**Last Updated**: January 13, 2026  
**Version**: 1.1  
**Status**: Complete - All authorization layers implemented including RBAC

┌─────────────────────────────────────────────────────────────┐
│                    Authorization Request                     │
│              "Can User X do Action Y on Resource Z?"         │
└────────────────────────────┬────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────┐
│  Step 1: Check Conditional Policies (by priority)           │
│  ├─ Location allowed?                                        │
│  ├─ Time window allowed?                                     │
│  ├─ Device trusted?                                          │
│  ├─ MFA verified? (if required)                             │
│  └─ Risk score acceptable?                                   │
└────────────────────────────┬────────────────────────────────┘
                             │ (If BLOCK → Deny immediately)
                             ▼
┌─────────────────────────────────────────────────────────────┐
│  Step 2: Check ABAC Policies                                │
│  ├─ Evaluate attribute-based rules                          │
│  ├─ Match user/resource/context attributes                  │
│  └─ Apply policy effect (Allow/Deny)                        │
└────────────────────────────┬────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────┐
│  Step 3: Check Direct Permissions                           │
│  ├─ Resource permission? (specific to this resource)        │
│  ├─ Content-type permission? (for this type)                │
│  └─ Tenant permission? (organization-wide)                  │
└────────────────────────────┬────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────┐
│  Step 4: Check Role Permissions (✅ Implemented)             │
│  ├─ Get user's roles (GetUserRolesQuery)                     │
│  ├─ Aggregate permissions from all roles                    │
│  └─ Check if action is allowed                              │
└────────────────────────────┬────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────┐
│                    Authorization Decision                    │
│                    ✅ Allow  or  ❌ Deny                     │
└─────────────────────────────────────────────────────────────┘
