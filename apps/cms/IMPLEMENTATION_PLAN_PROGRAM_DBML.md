# Implementation Plan for program.dbml

This document outlines the plan for implementing the schema defined in `program.dbml` within the `apps/cms` project, incorporating the existing DAC (Discretionary Access Control) strategy and Entity Framework Core best practices.

## 1. Analyze `program.dbml` and Existing Entities:

*   Thoroughly review the `program.dbml` file to understand the complete schema, including tables, columns, relationships, and enums.
*   Examine existing C# entity classes in `/Users/tolstenko/projects/gameguild/apps/cms/src/Modules` and `/Users/tolstenko/projects/gameguild/apps/cms/src/Data` to understand the current data model.
*   Identify gaps between the `program.dbml` schema and the existing entities.

## 2. Define Core Entities and Relationships:

*   Based on `program.dbml` and existing code, define the core C# entities. This will involve creating new classes or modifying existing ones.
*   Pay close attention to relationships (one-to-one, one-to-many, many-to-many) and ensure they are correctly implemented using Entity Framework Core conventions.
*   Enums defined in `program.dbml` will be translated into C# enums.

## 3. Implement TPC Inheritance:

*   Where appropriate, use Table-Per-Concrete-Type (TPC) inheritance. This is particularly relevant for entities that have distinct subtypes with their own specific fields, while also sharing common base fields. The `program.dbml` notes on "PostgreSQL Table Inheritance Alternative" for `financial_transactions` suggests this pattern might be suitable for transaction types (e.g., `PurchaseTransaction`, `WithdrawalTransaction` inheriting from a base `FinancialTransaction`).
*   Evaluate other hierarchies in `program.dbml` to see if TPC is a good fit, for example, potentially for different types of `ProgramContent` or other extensible entities.

## 4. Integrate the Three-Layer DAC Permission Model:

*   Adhere to the "pure three-layer DAC" model described in `DAC-STRATEGY.md`.
*   **`PermissionableResource`**: Entities in `program.dbml` that require granular access control (e.g., `programs`, `program_contents`, `products`, `certificates`) will need to inherit from or be associated with a concept similar to `PermissionableResource`.
    *   **Content vs. Resource**:
        *   "Content" generally refers to items that are created and managed within the system, often with their own lifecycle and specific attributes (e.g., a `Post`, a `Document`, a `ProgramPage`, a `CourseActivity`). These are prime candidates to be `PermissionableResource`s.
        *   "Resource" in the DAC context is the protectable entity. So, a specific `Post` *is* a resource. A `ContentType` like "Post" is a *type* of resource, for which permissions can be set that apply to all resources of that type.
*   **Permission Entities**:
    *   `UserTenantPermission`: For global or tenant-wide permissions.
    *   `UserContentTypePermission`: For permissions on all items of a specific type (e.g., all "Courses", all "Quizzes") within a tenant or globally. The `program_content_type` enum in `program.dbml` will be crucial here.
    *   `ResourcePermission`: For specific permissions on individual entity instances (e.g., permission for User A to edit Course X).
*   **User and Tenant Entities**: Ensure `User` and `Tenant` entities are structured to support these permission relationships as outlined in `DAC-STRATEGY.md`. The `program.dbml` already defines a `users` table. A `tenants` table might need to be explicitly added if not already present and if the multi-tenancy model requires it beyond just nullable `TenantId` fields.
*   **`PermissionService`**: Implement or extend a `PermissionService` to handle permission checks and assignments, incorporating the hierarchical logic (Global -> Tenant -> ContentType Global -> ContentType Tenant -> Resource-specific).

## 5. Entity Implementation - Key Tables from `program.dbml` (Illustrative Examples):

*   **`users`**: Will map to the `User` class, incorporating fields for authentication and DAC relationships.
*   **`programs`**: Will be a `PermissionableResource`.
*   **`program_contents`**: Each item can be a `PermissionableResource`. The `program_content_type` enum will be used for `UserContentTypePermission`.
*   **`products`**: Will be a `PermissionableResource`.
*   **`certificates`**: Will be a `PermissionableResource`.
*   **`financial_transactions`**:
    *   As per `program.dbml` notes, consider TPC if different transaction types (`purchase`, `withdrawal`, `refund`, etc.) have significantly different fields.
    *   Base `FinancialTransaction` class.
    *   Derived classes like `PurchaseTransaction`, `SubscriptionPaymentTransaction`, etc.
*   **`content_interactions`**: This table merges progress and submissions. It will link to `program_users` and `program_contents`. Access to view or modify interactions might be controlled via permissions on the related `program_content` or `program`.
*   **`tags`**: Will be a standard entity. Relationships like `tag_relationships` and `certificate_tags` will be mapped.
*   **Supporting Tables**: Entities for `promo_codes`, `user_subscriptions`, `user_financial_methods`, `user_kyc_verifications`, etc., will be created, and permissions applied as needed (e.g., who can create a `promo_code`, who can view `financial_transactions`).

## 6. Database Context (`ApplicationDbContext`):

*   Update `ApplicationDbContext.cs` to include `DbSet` properties for all new and modified entities.
*   Configure TPC inheritance using `modelBuilder` if not using conventions.
*   Define relationships, keys, and constraints using Fluent API or attributes.

## 7. Migrations:

*   Generate Entity Framework Core migrations to apply the schema changes to the database.

## 8. Service Layer:

*   Develop or update services to manage the new entities and business logic (e.g., `ProgramService`, `ProductService`, `TransactionService`).
*   Ensure services utilize the `PermissionService` for authorization.

## 9. Testing:

*   Write unit and integration tests for:
    *   Entity mappings and relationships.
    *   TPC inheritance behavior.
    *   DAC permission logic (all three layers).
    *   Service layer business logic.

## Detailed Steps for DAC Integration:

1.  **Base Permissionable Entities**:
    *   Ensure `PermissionableResource` (as per `DAC-STRATEGY.md`) is a base class or an interface implemented by entities like `Program`, `Product`, `ProgramContent`, `Certificate`.
    *   Add navigation properties for `ResourcePermission` to these entities.

2.  **User and Tenant Setup**:
    *   Modify the existing `User` entity to include collections for `GrantedResourcePermissions`, and potentially `UserTenantPermissions` and `UserContentTypePermissions` if direct navigation is desired, or manage these through the `PermissionService`.
    *   Define a `Tenant` entity if it doesn't exist and is required by the multi-tenancy strategy.

3.  **Permission Entities Implementation**:
    *   Create `BasePermission`, `ResourcePermission`, `UserTenantPermission`, and `UserContentTypePermission` classes as described in `DAC-STRATEGY.md`.
    *   Ensure foreign keys and navigation properties are correctly set up (e.g., `UserId`, `ResourceId`, `TenantId`, `ContentType`).
    *   The `ContentType` in `UserContentTypePermission` will likely be a string or enum that maps to types like "Program", "Product", or specific `program_content_type` values.

4.  **`PermissionService` Enhancement**:
    *   Implement the methods outlined in `DAC-STRATEGY.md` (e.g., `HasPermissionAsync`, `AssignUserToTenantAsync`, `AssignContentTypePermissionAsync`, `GrantResourcePermissionAsync`).
    *   The core logic will involve querying these permission tables in the specified hierarchical order.

## Considerations from `program.dbml`:

*   **`jsonb` fields**: For fields like `metadata` in `user_kyc_verifications`, EF Core can map these to complex types or handle them as strings.
*   **Partitioning/Inheritance for `financial_transactions`**: The plan includes TPC as a primary approach for `financial_transactions` based on the DBML notes.
*   **Enums**: All enums listed (e.g., `visibility`, `certificate_status`, `product_type`) will be created as C# enums.
*   **Relationships**: All `Ref` entries in `program.dbml` will be translated into EF Core relationships.
