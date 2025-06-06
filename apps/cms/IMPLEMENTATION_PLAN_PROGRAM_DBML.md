# Implementation Plan for program.dbml

This document outlines the plan for implementing the schema defined in `program.dbml` within the `apps/cms` project, incorporating the **11 modular permission types** and **three-level capability-based DAC strategy** defined in `DAC-STRATEGY.md`, along with Entity Framework Core best practices.

## Core Design Principles

This implementation follows the **four core design principles** outlined in `DAC-STRATEGY.md`:

### 1. Modular Permission Architecture
Replace traditional role-based systems with **composable permission types** that can be combined and configured independently. Each permission type provides specific capabilities rather than broad role definitions.

### 2. Capability-Based Security Model
Focus on **what users can do** (capabilities) rather than **who they are** (roles). This enables fine-grained control and flexible permission combinations.

### 3. Three-Level Permission Application
Apply permissions at three distinct levels:
- **Tenant-wide**: Tenant-wide permissions for a user across all content types within a tenant
- **Content-type-wide**: Category-specific permission rules for a user across all entries of a specific content type within a tenant  
- **Content-entry**: Individual resource-specific permissions for a user on a specific content entry within a tenant

### 4. TPC Entity Design Pattern
Utilize **Table-Per-Concrete** hierarchy for optimal performance, clean separation of concerns, and scalable content type management.

## 1. Analyze `program.dbml` and Existing Entities:

*   Thoroughly review the `program.dbml` file to understand the complete schema, including tables, columns, relationships, and enums.
*   Examine existing C# entity classes in `/Users/tolstenko/projects/gameguild/apps/cms/src/Modules` and `/Users/tolstenko/projects/gameguild/apps/cms/src/Data` to understand the current data model.
*   Identify gaps between the `program.dbml` schema and the existing entities.

## 2. Define Core Entities and Relationships:

*   Based on `program.dbml` and existing code, define the core C# entities. This will involve creating new classes or modifying existing ones.
*   Pay close attention to relationships (one-to-one, one-to-many, many-to-many) and ensure they are correctly implemented using Entity Framework Core conventions.
*   Enums defined in `program.dbml` will be translated into C# enums.

## 4. Implement Modular Permission Type Architecture:

### 4.1 Eleven Specialized Permission Types

Implement the **11 modular permission types** as defined in `DAC-STRATEGY.md`, each providing specific capabilities that can be combined independently:

| Permission Type | Implementation Focus | Key Capabilities |
|--------|---------|------------------|
| **Content Interaction** | Social and community features | Comment, Vote, Share, Follow, Bookmark, React |
| **Content Curation** | Content organization & taxonomy | Tag, Categorize, Feature, Organize |
| **Moderation** | Community safety and standards | Review, Approve, Flag, Hide, Suspend, Ban |
| **Content Lifecycle** | Content workflow control | Draft, Publish, Archive, Schedule |
| **Publishing** | Content distribution strategy | Distribute, Syndicate, Promote |
| **Monetization** | Business model enablement | Paywall, Subscription, Ads, Commerce |
| **Editorial** | Content excellence and standards | Edit, Review, Approve, Fact-check |
| **Promotion** | Content marketing and discovery | Boost, Feature, Recommend, Highlight |
| **Quality Control** | Quality assurance and compliance | Audit, Validate, Certify, Analyze |
| **Business Logic** | Organization-specific operations | Execute, Process, Calculate, Transform |

### 4.2 Permission Flag Structure

Instead of using a UnifiedPermissionContext object, implement permissions as **individual boolean flag columns** for optimal query performance and database efficiency:

```csharp
// Example permission entity structure
public class UserTenantPermission
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    
    // Content Interaction Permissions
    public bool CanComment { get; set; }
    public bool CanVote { get; set; }
    public bool CanShare { get; set; }
    public bool CanFollow { get; set; }
    public bool CanBookmark { get; set; }
    public bool CanReact { get; set; }
    
    // Content Curation Permissions
    public bool CanTag { get; set; }
    public bool CanCategorize { get; set; }
    public bool CanFeature { get; set; }
    public bool CanOrganize { get; set; }
    
    // Moderation Permissions
    public bool CanReview { get; set; }
    public bool CanApprove { get; set; }
    public bool CanFlag { get; set; }
    public bool CanHide { get; set; }
    public bool CanSuspend { get; set; }
    public bool CanBan { get; set; }
    
    // Content Lifecycle Permissions
    public bool CanDraft { get; set; }
    public bool CanPublish { get; set; }
    public bool CanArchive { get; set; }
    public bool CanSchedule { get; set; }
    
    // Publishing Permissions
    public bool CanDistribute { get; set; }
    public bool CanSyndicate { get; set; }
    public bool CanPromote { get; set; }
    
    // Monetization Permissions
    public bool CanPaywall { get; set; }
    public bool CanSubscription { get; set; }
    public bool CanAds { get; set; }
    public bool CanCommerce { get; set; }
    
    // Editorial Permissions
    public bool CanEdit { get; set; }
    public bool CanEditorialReview { get; set; }
    public bool CanEditorialApprove { get; set; }
    public bool CanFactCheck { get; set; }
    
    // Promotion Permissions
    public bool CanBoost { get; set; }
    public bool CanHighlight { get; set; }
    public bool CanRecommend { get; set; }
    
    // Quality Control Permissions
    public bool CanAudit { get; set; }
    public bool CanValidate { get; set; }
    public bool CanCertify { get; set; }
    public bool CanAnalyze { get; set; }
    
    // Business Logic Permissions
    public bool CanExecute { get; set; }
    public bool CanProcess { get; set; }
    public bool CanCalculate { get; set; }
    public bool CanTransform { get; set; }
    
    // Navigation properties
    public virtual User User { get; set; }
    public virtual Tenant Tenant { get; set; }
}
```

### 4.3 Content-Type-Specific Permission Tables

**Each content type requires different permission tables** because each content type may need different permission types. For example:
- Comments need **Moderation** permissions (Review, Approve, Flag)
- Articles need **Editorial** permissions (Edit, Review, Fact-check) 
- Products need **Monetization** permissions (Paywall, Commerce)
- Programs need **Business Logic** permissions (Execute, Process)

The permission table for each content type will include only the relevant permission type columns.

## 5. Implement Three-Level Permission Application:

### 5.1 Permission Level Architecture

The permission system applies controls at **three distinct levels** providing granular inheritance and override capabilities:

```
Level 1: Tenant-wide Permissions
│   └── A user has permissions that apply to all content types within a specific tenant domain
│
Level 2: Content-type-wide Permissions  
│   └── A user has permissions that apply to all content entries of a specific content type within a tenant domain
│
Level 3: Content-entry Permissions
│   └── A user can have specific permissions for individual content entries within a content type and tenant domain
```

### 5.2 Permission Level Characteristics

| Level | Scope | Design Purpose | Inheritance Pattern |
|-------|-------|----------------|-------------------|
| **Tenant-wide** | Organization-level | Global policies & defaults | Foundation for all content |
| **Content-type-wide** | Category-specific | Content behavior patterns | Inherits tenant + adds type rules |
| **Content-entry** | Individual resources | Specific item exceptions | Inherits type + item overrides |

### 5.3 Permission Entities Structure

*   **`UserTenantPermission`**: For tenant-wide permissions across all content types
*   **`UserContentTypePermission`**: For permissions on all items of a specific content type within a tenant
*   **`ResourcePermission`**: For specific permissions on individual entity instances

Each permission entity will contain **separate boolean flag columns** for the relevant permission types, avoiding JSON for better query performance and database optimization. This allows for:
- Direct SQL queries with WHERE conditions on specific permissions
- Efficient indexing on individual permission flags
- Simple joins and aggregations for permission checking
- Better performance for complex permission inheritance logic

## 6. Implement TPC Entity Design Pattern:

### 6.1 TPC Hierarchy for Content Types

Utilize **Table-Per-Concrete (TPC)** hierarchy for optimal performance and clean separation. Each concrete content type maintains its own optimized table structure while sharing common permission interfaces.

```
ContentBase (Abstract Permission Interface)
├── Program (Learning programs with Business Logic permissions)
├── ProgramContent (Course materials with Editorial permissions)  
├── Product (E-commerce items with Monetization permissions)
├── Certificate (Credentials with Quality Control permissions)
├── Comment (User discussions with Moderation permissions)
├── Article (Content with Editorial + Publishing permissions)
├── Video (Media with Lifecycle + Promotion permissions)
└── Document (Files with Curation permissions)
```

### 6.2 TPC Design Benefits

| Benefit | Description | Impact |
|---------|-------------|---------|
| **Performance** | No joins required for concrete type queries | Fast content retrieval and listing |
| **Scalability** | Each table optimized for specific content patterns | Efficient storage and indexing |
| **Flexibility** | Easy addition of new content types | Extensible without schema changes |
| **Permission Alignment** | Each type defines its relevant permission types | Clean permission-to-content mapping |

### 6.3 Financial Transactions TPC Implementation

As per `program.dbml` notes, implement TPC for different transaction types:
*   Base `FinancialTransaction` class (abstract)
*   Derived classes: `PurchaseTransaction`, `WithdrawalTransaction`, `RefundTransaction`, `SubscriptionPaymentTransaction`
*   Each derived class has transaction-type-specific fields and relevant permission types

## 9. Database Context (`ApplicationDbContext`):

*   Update `ApplicationDbContext.cs` to include `DbSet` properties for all new and modified entities.
*   Configure TPC inheritance using `modelBuilder` for content type hierarchies.
*   Define relationships, keys, and constraints using Fluent API.
*   Configure individual boolean flag columns for permission types instead of JSON for optimal query performance.
*   Set up proper indexing for permission queries across the three levels.

### 10.2 Hierarchical Permission Resolution

The service will resolve permissions in this order:
1. **Resource-specific permissions** (highest priority)
2. **Content-type-wide permissions** within tenant
3. **Tenant-wide permissions**
4. **Global content-type permissions**

