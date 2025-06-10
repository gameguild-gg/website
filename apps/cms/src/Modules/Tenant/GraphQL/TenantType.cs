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
        descriptor.Field(t => t.TenantPermissions)
            .Type<ListType<TenantPermissionType>>()
            .Description("The users and their permissions associated with this tenant");
    }
}

/// <summary>
/// GraphQL type for TenantPermission entity
/// </summary>
public class TenantPermissionType : ObjectType<TenantPermission>
{
    protected override void Configure(IObjectTypeDescriptor<TenantPermission> descriptor)
    {
        descriptor.Name("TenantPermission");
        descriptor.Description("Represents the permissions and relationship between a user and a tenant");

        descriptor.Field(tp => tp.Id)
            .Type<NonNullType<UuidType>>()
            .Description("The unique identifier of the tenant permission");

        descriptor.Field(tp => tp.UserId)
            .Type<NonNullType<UuidType>>()
            .Description("The user identifier");

        descriptor.Field(tp => tp.TenantId)
            .Type<NonNullType<UuidType>>()
            .Description("The tenant identifier");

        descriptor.Field(tp => tp.CreatedAt)
            .Type<NonNullType<DateTimeType>>()
            .Description("The date and time when the user joined the tenant");

        descriptor.Field(tp => tp.ExpiresAt)
            .Type<DateTimeType>()
            .Description("The date and time when the permission expires");

        descriptor.Field(tp => tp.PermissionFlags1)
            .Type<NonNullType<LongType>>()
            .Description("Permission flags for bits 0-63");

        descriptor.Field(tp => tp.PermissionFlags2)
            .Type<NonNullType<LongType>>()
            .Description("Permission flags for bits 64-127");

        // Navigation properties
        descriptor.Field(tp => tp.User)
            .Type<UserType>()
            .Description("The user in this relationship");

        descriptor.Field(tp => tp.Tenant)
            .Type<TenantType>()
            .Description("The tenant in this relationship");
    }
}
