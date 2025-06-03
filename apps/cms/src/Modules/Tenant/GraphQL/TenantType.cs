using cms.Modules.Tenant.Models;
using cms.Modules.User.GraphQL;

namespace cms.Modules.Tenant.GraphQL;

/// <summary>
/// GraphQL type for Tenant entity
/// </summary>
public class TenantType : ObjectType<Models.Tenant>
{
    protected override void Configure(IObjectTypeDescriptor<Models.Tenant> descriptor)
    {
        descriptor.Name("Tenant");
        descriptor.Description("A tenant represents an organization or group within the system");

        // Basic fields
        descriptor.Field(t => t.Id)
            .Type<NonNullType<UuidType>>()
            .Description("The unique identifier of the tenant");

        descriptor.Field(t => t.Name)
            .Type<NonNullType<StringType>>()
            .Description("The name of the tenant");

        descriptor.Field(t => t.Description)
            .Type<StringType>()
            .Description("The description of the tenant");

        descriptor.Field(t => t.IsActive)
            .Type<NonNullType<BooleanType>>()
            .Description("Whether the tenant is active");

        // BaseEntity fields
        descriptor.Field(t => t.CreatedAt)
            .Type<NonNullType<DateTimeType>>()
            .Description("The date and time when the tenant was created");

        descriptor.Field(t => t.UpdatedAt)
            .Type<DateTimeType>()
            .Description("The date and time when the tenant was last updated");

        descriptor.Field(t => t.Version)
            .Type<NonNullType<IntType>>()
            .Description("The version number for optimistic concurrency control");

        // Navigation properties
        descriptor.Field(t => t.UserTenants)
            .Type<ListType<UserTenantType>>()
            .Description("The users associated with this tenant");

        descriptor.Field(t => t.TenantRoles)
            .Type<ListType<TenantRoleType>>()
            .Description("The roles defined for this tenant");
    }
}

/// <summary>
/// GraphQL type for TenantRole entity
/// </summary>
public class TenantRoleType : ObjectType<TenantRole>
{
    protected override void Configure(IObjectTypeDescriptor<TenantRole> descriptor)
    {
        descriptor.Name("TenantRole");
        descriptor.Description("A role definition within a tenant context");

        descriptor.Field(r => r.Id)
            .Type<NonNullType<UuidType>>()
            .Description("The unique identifier of the tenant role");

        descriptor.Field(r => r.Name)
            .Type<NonNullType<StringType>>()
            .Description("The name of the role");

        descriptor.Field(r => r.Description)
            .Type<StringType>()
            .Description("The description of the role");

        descriptor.Field(r => r.Permissions)
            .Type<StringType>()
            .Description("The permissions assigned to this role (JSON format)");

        descriptor.Field(r => r.IsActive)
            .Type<NonNullType<BooleanType>>()
            .Description("Whether the role is active");

        // Navigation properties
        descriptor.Field(r => r.TenantId)
            .Type<NonNullType<UuidType>>()
            .Description("The tenant this role belongs to");

        descriptor.Field(r => r.Tenant)
            .Type<TenantType>()
            .Description("The tenant this role belongs to");

        descriptor.Field(r => r.UserTenantRoles)
            .Type<ListType<UserTenantRoleType>>()
            .Description("The user role assignments for this role");
    }
}

/// <summary>
/// GraphQL type for UserTenant junction entity
/// </summary>
public class UserTenantType : ObjectType<UserTenant>
{
    protected override void Configure(IObjectTypeDescriptor<UserTenant> descriptor)
    {
        descriptor.Name("UserTenant");
        descriptor.Description("Represents the relationship between a user and a tenant");

        descriptor.Field(ut => ut.Id)
            .Type<NonNullType<UuidType>>()
            .Description("The unique identifier of the user-tenant relationship");

        descriptor.Field(ut => ut.UserId)
            .Type<NonNullType<UuidType>>()
            .Description("The user identifier");

        descriptor.Field(ut => ut.TenantId)
            .Type<NonNullType<UuidType>>()
            .Description("The tenant identifier");

        descriptor.Field(ut => ut.JoinedAt)
            .Type<NonNullType<DateTimeType>>()
            .Description("The date and time when the user joined the tenant");

        // Navigation properties
        descriptor.Field(ut => ut.User)
            .Type<UserType>()
            .Description("The user in this relationship");

        descriptor.Field(ut => ut.Tenant)
            .Type<TenantType>()
            .Description("The tenant in this relationship");

        descriptor.Field(ut => ut.UserTenantRoles)
            .Type<ListType<UserTenantRoleType>>()
            .Description("The roles assigned to this user within the tenant");
    }
}

/// <summary>
/// GraphQL type for UserTenantRole entity
/// </summary>
public class UserTenantRoleType : ObjectType<UserTenantRole>
{
    protected override void Configure(IObjectTypeDescriptor<UserTenantRole> descriptor)
    {
        descriptor.Name("UserTenantRole");
        descriptor.Description("Represents a role assignment for a user within a tenant");

        descriptor.Field(utr => utr.Id)
            .Type<NonNullType<UuidType>>()
            .Description("The unique identifier of the user tenant role assignment");

        descriptor.Field(utr => utr.UserTenantId)
            .Type<NonNullType<UuidType>>()
            .Description("The user-tenant relationship identifier");

        descriptor.Field(utr => utr.TenantRoleId)
            .Type<NonNullType<UuidType>>()
            .Description("The tenant role identifier");

        descriptor.Field(utr => utr.AssignedAt)
            .Type<NonNullType<DateTimeType>>()
            .Description("The date and time when the role was assigned");

        descriptor.Field(utr => utr.ExpiresAt)
            .Type<DateTimeType>()
            .Description("The date and time when the role assignment expires");

        // Navigation properties
        descriptor.Field(utr => utr.UserTenant)
            .Type<UserTenantType>()
            .Description("The user-tenant relationship");

        descriptor.Field(utr => utr.TenantRole)
            .Type<TenantRoleType>()
            .Description("The assigned tenant role");
    }
}
