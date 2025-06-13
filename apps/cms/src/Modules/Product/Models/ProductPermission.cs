using GameGuild.Common.Entities;

namespace GameGuild.Modules.Product.Models;

/// <summary>
/// Resource-level permissions for Product entity
/// Inherits from ResourcePermission with Product as the generic type
/// Enables granular access control for individual product instances
/// </summary>
public class ProductPermission : ResourcePermission<Product>
{
    // Inherits all properties from ResourcePermission<Product>:
    // - Id, UserId, TenantId from WithPermissions base
    // - ResourceId and ResourceType from ResourcePermission<T>
    // - ResourceType automatically set to "Product"
    // - PermissionFlags1, PermissionFlags2 for storing permission types
    // - CreatedAt, UpdatedAt timestamps
}
