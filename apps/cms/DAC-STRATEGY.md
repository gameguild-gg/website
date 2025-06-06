# DAC Permission System Design Strategy

## Overview

This document outlines the **design principles** and **architectural patterns** for a modern pure three-layer **Discretionary Access Control (DAC)** system. The design emphasizes **modular permission types**, **capability-based access control**, and **hierarchical permission inheritance** through the **Table-Per-Concrete (TPC)** entity pattern.

## Core Design Principles

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

## Permission Type Architecture

### Modular Permission Design

The system organizes permissions into **11 specialized permission types**, each providing specific capabilities that can be combined independently:

### Permission Type Capability Matrix

| Permission Type | Purpose | Key Capabilities | Design Intent |
|--------|---------|------------------|---------------|
| **Core** | Basic operations | Read, Create, Update, Delete | Foundation CRUD operations |
| **Content Interaction** | User engagement | Comment, Vote, Share, Follow, Bookmark, React | Social and community features |
| **Content Curation** | Organization & taxonomy | Tag, Categorize, Feature, Organize | Content discovery and structure |
| **Moderation** | Content oversight | Review, Approve, Flag, Hide, Suspend, Ban | Community safety and standards |
| **Content Lifecycle** | State management | Draft, Publish, Archive, Schedule | Content workflow control |
| **Publishing** | Publication control | Distribute, Syndicate, Promote | Content distribution strategy |  
| **Monetization** | Revenue features | Paywall, Subscription, Ads, Commerce | Business model enablement |
| **Editorial** | Content quality | Edit, Review, Approve, Fact-check | Content excellence and standards |
| **Promotion** | Marketing & visibility | Boost, Feature, Recommend, Highlight | Content marketing and discovery |
| **Quality Control** | Standards enforcement | Audit, Validate, Certify, Analyze | Quality assurance and compliance |
| **Business Logic** | Custom workflows | Execute, Process, Calculate, Transform | Organization-specific operations |

### Permission Type Relationships

The permission types are designed to work **independently** and **in combination**:

- **Core** permission type provides the foundational CRUD operations required by all other types
- **Content Interaction** and **Content Curation** complement each other for community-driven platforms
- **Moderation** and **Quality Control** work together for comprehensive content oversight
- **Publishing** and **Promotion** combine for full content marketing capabilities
- **Editorial** and **Content Lifecycle** ensure quality content progression

### Permission Context Design Pattern

The **UnifiedPermissionContext** serves as the container for all permission types, enabling **modular composition**:

```
UnifiedPermissionContext
├── CorePermissions          (Foundation capabilities)
├── InteractionPermissions   (Social engagement)
├── CurationPermissions      (Content organization)
├── ModerationPermissions    (Community oversight)
├── LifecyclePermissions     (Workflow management)
├── PublishingPermissions    (Distribution control)
├── MonetizationPermissions  (Revenue features)
├── EditorialPermissions     (Quality management)
├── PromotionPermissions     (Marketing capabilities)
├── QualityPermissions       (Standards enforcement)
└── BusinessLogicPermissions (Custom workflows)
```

### Permission Composition Patterns

Rather than predefined roles, the system supports **flexible permission composition**:

| Composition Pattern | Use Case | Permission Type Combination |
|-------------------|----------|-------------------|
| **Basic Consumption** | Content browsing & engagement | Interaction (Basic) |
| **Content Creation** | User-generated content | Lifecycle (Basic) + Curation (Tag) |
| **Community Management** | Forum/social moderation | Moderation (All) + Quality (Audit) + Editorial (Review) |
| **Content Editing** | Professional content work | Editorial (All) + Lifecycle (Publish) + Quality (Validate) |
| **Business Operations** | Revenue & analytics | Monetization (All) + Promotion (Analytics) + Quality (Reports) |
| **Platform Administration** | System-wide management | All types with appropriate capabilities |
## Three-Level Permission Application

Each content type requires different permission tables because each content type may need different permission types. The system applies permissions at three distinct levels, allowing for granular inheritance and override capabilities. For example: comments need to be moderated, but not all content types require moderation capabilities, so the permission table for comments will include moderation permission flags, while the permission table for basic articles may not include this column.

### Design Architecture

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

Each level provides a different scope of control, allowing for **cascading inheritance** and **contextual overrides**. A user can have permissions that apply globally within a tenant domain, at the content type level, or specifically for individual content entries.

### Permission Level Characteristics

| Level | Scope | Design Purpose | Inheritance Pattern |
|-------|-------|----------------|-------------------|
| **Tenant-wide** | Organization-level | Global policies & defaults | Foundation for all content |
| **Content-type-wide** | Category-specific | Content behavior patterns | Inherits tenant + adds type rules |
| **Content-entry** | Individual resources | Specific item exceptions | Inherits type + item overrides |

## TPC Entity Design Strategy

### Table-Per-Concrete Design Philosophy

The system utilizes **Table-Per-Concrete (TPC)** hierarchy for maximum performance and clean separation. Each concrete content type maintains its own optimized table structure while sharing common permission interfaces.

### TPC Hierarchy Architecture

Example of TPC hierarchy for content types:
```
ContentBase (Abstract Permission Interface)
├── Article (News, Blogs, Documentation)
├── Video (Streaming, Tutorials, Presentations)  
├── Image (Photos, Graphics, Diagrams)
├── Document (PDFs, Spreadsheets, Reports)
├── Discussion (Forums, Comments, Q&A)
├── Event (Webinars, Conferences, Meetings)
├── Course (Training, Education, Certification)
├── Product (E-commerce, Catalog, Reviews)
├── Project (Tasks, Milestones, Collaboration)
└── Resource (Files, Links, References)
```

### TPC Design Benefits

| Benefit | Description | Impact |
|---------|-------------|---------|
| **Performance** | No joins required for concrete type queries | Fast content retrieval and listing |
| **Scalability** | Each table optimized for specific content patterns | Efficient storage and indexing |
| **Flexibility** | Easy addition of new content types | Extensible without schema changes |
| **Isolation** | Content types evolve independently | Reduced coupling and maintenance |
| **Permission Alignment** | Each type defines its relevant permission types | Clean permission-to-content mapping |

### Concrete Entity Examples

#### Article Entity Design
```
Article Table (Concrete)
├── Id (Primary Key)
├── TenantId (Multi-tenancy)
├── Title (Content-specific)
├── Body (Content-specific)
├── SEO_Keywords (Content-specific)
├── Word_Count (Content-specific)
├── Reading_Time (Content-specific)
├── Author_Id (Ownership)
├── Published_At (Lifecycle)
├── Permission_Context (JSON column)
└── [Navigation Properties]

Primary Permission Types:
- Editorial (Review, Edit, Fact-check)
- Publishing (Schedule, Distribute)
- Promotion (SEO, Feature, Recommend)
- Quality (Grammar, Plagiarism, Standards)
```

#### Video Entity Design
```
Video Table (Concrete)
├── Id (Primary Key)
├── TenantId (Multi-tenancy)
├── Title (Content-specific)
├── Duration (Content-specific)
├── Video_URL (Content-specific)
├── Transcript (Content-specific)
├── Thumbnail_URL (Content-specific)
├── Resolution (Content-specific)
├── Creator_Id (Ownership)
├── Upload_Date (Lifecycle)
├── Permission_Context (JSON column)
└── [Navigation Properties]

Primary Permission Types:
- Lifecycle (Upload, Process, Archive)
- Monetization (Ads, Subscriptions, Pay-per-view)
- Publishing (Stream, Download, Syndicate)
- Quality (Content rating, Transcription)
```

#### Discussion Entity Design
```
Discussion Table (Concrete)
├── Id (Primary Key)
├── TenantId (Multi-tenancy)
├── Topic (Content-specific)
├── Category (Content-specific)
├── Reply_Count (Content-specific)
├── Last_Activity (Content-specific)
├── Is_Pinned (Content-specific)
├── Is_Locked (Content-specific)
├── Creator_Id (Ownership)
├── Created_At (Lifecycle)
├── Permission_Context (JSON column)
└── [Navigation Properties]

Primary Permission Types:
- Interaction (Reply, Vote, React)
- Moderation (Lock, Pin, Remove, Ban)
- Curation (Tag, Categorize, Feature)
- Quality (Flag, Report, Review)
```

### TPC Permission Interface Design

Each concrete entity implements the **IPermissionEnabledContent** interface:

```
IPermissionEnabledContent Interface
├── GetRelevantPermissionTypes() → List<PermissionType>
├── GetDefaultPermissions() → UnifiedPermissionContext  
├── ValidatePermissionContext(context) → ValidationResult
├── GetPermissionRequirements(operation) → PermissionContext
└── ApplyPermissionConstraints(user, operation) → ConstraintResult
```

This design ensures that:
- Each content type defines its **relevant permission types**
- **Default permissions** align with content type behavior
- **Validation rules** prevent invalid permission combinations
- **Operation requirements** are content-type aware
- **Constraints** apply contextually based on content characteristics

## Service Architecture Design

### Permission Service Interface Design

The permission service architecture follows **capability-based design principles** with clear separation of concerns:

```
Permission Service Architecture
├── Permission Resolution Engine
│   ├── Three-Level Inheritance Calculator
│   ├── Permission Type Capability Evaluator
│   └── Context-Aware Filter
├── Permission Management Layer
│   ├── Capability Assignment Service
│   ├── Permission Validation Service
│   └── Constraint Application Service
├── Audit & Compliance Layer
│   ├── Permission Change Tracker
│   ├── Access Log Generator
│   └── Compliance Validator
└── Performance Optimization Layer
    ├── Multi-Level Caching Strategy
    ├── Bulk Permission Operations
    └── Query Optimization
```

### Service Design Patterns

| Pattern | Purpose | Design Approach |
|---------|---------|-----------------|
| **Permission Resolution** | Calculate effective permissions | Cascading inheritance with permission-type-specific rules |
| **Capability Validation** | Ensure permission consistency | Cross-permission-type dependency validation |
| **Performance Optimization** | Scale to large user bases | Strategic caching and bulk operations |
| **Audit Compliance** | Track permission changes | Event sourcing with immutable audit trails |
| **Context Awareness** | Dynamic permission adaptation | User relationship and ownership evaluation |

## Multi-Tenancy Design Architecture

### Tenant Isolation Strategy

The system implements **tenant-aware permissions** with complete data and permission isolation:

```
Multi-Tenant Permission Architecture
├── Global Level (System-wide defaults)
│   ├── Universal policies and constraints
│   └── Cross-tenant permission baselines
├── Tenant Level (Organization-specific)  
│   ├── Tenant policy overrides and additions
│   └── Organization-specific permission defaults
├── Content-Type Level (Category within tenant)
│   ├── Content behavior patterns per tenant
│   └── Category-specific permission rules
└── Resource Level (Individual items)
    ├── Item-specific permission overrides
    └── Content-entry access controls
```

### Tenant Permission Design Patterns

| Level | Scope | Design Purpose | Inheritance Flow |
|-------|-------|----------------|------------------|
| **Global** | Cross-tenant | System defaults and universal policies | Base for all tenant operations |
| **Tenant** | Organization | Tenant-specific overrides and additions | Inherits global + adds org rules |
| **Content-Type** | Category in tenant | Content behavior within org context | Inherits tenant + adds type rules |
| **Resource** | Individual items | Item-specific permissions and exceptions | Inherits type + adds item overrides |

## Design Implementation Principles

### 1. **Modular Permission Type Architecture Implementation**

#### Core Design Requirements
- **Permission Type Independence**: Each permission type should function independently without requiring others
- **Capability Composition**: Permission types should combine cleanly without conflicts or overlaps
- **Extensibility**: New permission types can be added without modifying existing permission type logic
- **Performance**: Permission type resolution should be optimized for fast capability checking

#### Implementation Design Patterns
```
Permission Type Service Architecture:
├── Permission Type Registry (manages available permission types)
├── Capability Resolver (maps operations to permission type requirements)
├── Permission Composer (combines permission type capabilities)
├── Constraint Validator (enforces permission type rules)
└── Context Builder (creates permission contexts)
```

### 2. **Three-Layer Permission Architecture Design**

#### Layer Design Principles
1. **Tenant Layer**: Organizational defaults and baseline capabilities
2. **Content-Type Layer**: Category-specific permission type combinations
3. **Resource Layer**: Individual content capability overrides

#### Resolution Design Algorithm
```
Permission Resolution Design:
1. Initialize with empty capability set
2. Apply Tenant-level permission type capabilities (additive)
3. Apply Content-Type permission type capabilities (additive)
4. Apply Resource-level permission type capabilities (additive)
5. Validate capability constraints and dependencies
6. Return final composed permission context
```

### 3. **TPC Entity Design Implementation**

#### Entity Architecture Principles
- **Content-Type Tables**: Each content type gets its own optimized table
- **Permission Interface**: Common permission interface across all content types
- **Permission Type Mapping**: Each content type defines its relevant permission types
- **Inheritance Design**: No table joins required for type-specific operations

#### Performance Design Considerations
- **Direct Table Access**: Query specific content type tables directly
- **Permission Type Indexing**: Index permission contexts for fast permission type capability lookups
- **Capability Caching**: Cache frequently accessed permission combinations
- **Constraint Optimization**: Pre-compute permission type constraint validations