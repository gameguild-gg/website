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
    }
}
