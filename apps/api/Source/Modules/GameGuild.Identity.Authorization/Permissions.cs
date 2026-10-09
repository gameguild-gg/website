using GameGuild.Identity.Authorization.Models;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Central registry providing convenient access to all permission key constants.
///     Use these constants in attributes where compile-time constants are required.
/// </summary>
/// <remarks>
///     <para>
///         This class provides backward-compatible access to permission keys.
///         For runtime permission checks, prefer using the strongly-typed permission classes
///         (e.g., <see cref="UsersPermission.Read"/>) which provide IntelliSense and refactoring safety.
///     </para>
///     <para>
///         For attributes, use either this class or the nested Keys class in each permission type:
///         <code>
///         // Option 1: Using this facade
///         [RequirePermission(Permissions.UsersRead)]
///         
///         // Option 2: Using the nested Keys class (preferred for discoverability)
///         [RequirePermission(UsersPermission.Keys.Read)]
///         </code>
///     </para>
/// </remarks>
public static class Permissions
{
    // ========================
    // ADMIN PERMISSIONS
    // ========================

    /// <summary>Full admin access (wildcard)</summary>
    public const string AdminWildcard = AdminPermission.Keys.Wildcard;

    /// <summary>Admin permission</summary>
    public const string Admin = AdminPermission.Keys.Admin;

    /// <summary>Tenant admin permission</summary>
    public const string TenantAdmin = AdminPermission.Keys.TenantAdmin;

    // ========================
    // USER PERMISSIONS - CRUD Operations
    // ========================

    /// <summary>Read user data</summary>
    public const string UsersRead = UsersPermission.Keys.Read;

    /// <summary>Create new users</summary>
    public const string UsersCreate = UsersPermission.Keys.Create;

    /// <summary>Update existing users</summary>
    public const string UsersUpdate = UsersPermission.Keys.Update;

    /// <summary>Soft-delete users</summary>
    public const string UsersDelete = UsersPermission.Keys.Delete;

    /// <summary>Administrative operations on users</summary>
    public const string UsersAdmin = UsersPermission.Keys.Admin;

    /// <summary>Permanently delete users (dangerous)</summary>
    public const string UsersPurge = UsersPermission.Keys.Purge;

    // ========================
    // USER PERMISSIONS - Self Operations
    // ========================

    /// <summary>Edit own profile</summary>
    public const string UsersEditSelf = UsersPermission.Keys.EditSelf;

    /// <summary>Delete own account</summary>
    public const string UsersDeleteSelf = UsersPermission.Keys.DeleteSelf;

    /// <summary>Read own data</summary>
    public const string UsersReadSelf = UsersPermission.Keys.ReadSelf;

    /// <summary>Manage any user</summary>
    public const string UsersManage = UsersPermission.Keys.Manage;

    // ========================
    // CONTENT PERMISSIONS
    // ========================

    /// <summary>Read content</summary>
    public const string ContentRead = ContentPermission.Keys.Read;

    /// <summary>Write/edit content</summary>
    public const string ContentWrite = ContentPermission.Keys.Write;

    /// <summary>Admin access to content</summary>
    public const string ContentAdmin = ContentPermission.Keys.Admin;

    /// <summary>Edit existing editorial content</summary>
    public const string ContentEdit = ContentPermission.Keys.Edit;

    /// <summary>Create and maintain content drafts</summary>
    public const string ContentDraft = ContentPermission.Keys.Draft;

    /// <summary>Schedule and cancel scheduled content publication</summary>
    public const string ContentSchedule = ContentPermission.Keys.Schedule;

    // ========================
    // PROJECT PERMISSIONS
    // ========================

    /// <summary>Read projects</summary>
    public const string ProjectRead = ProjectPermission.Keys.Read;

    /// <summary>Write/edit projects</summary>
    public const string ProjectWrite = ProjectPermission.Keys.Write;

    /// <summary>Admin access to projects</summary>
    public const string ProjectAdmin = ProjectPermission.Keys.Admin;

    // ========================
    // COURSE PERMISSIONS
    // ========================

    /// <summary>Read courses</summary>
    public const string CourseRead = CoursePermission.Keys.Read;

    /// <summary>Manage courses</summary>
    public const string CourseManage = CoursePermission.Keys.Manage;

    // ========================
    // PRODUCTS PERMISSIONS
    // ========================

    /// <summary>Read product data</summary>
    public const string ProductsRead = ProductsPermission.Keys.Read;

    /// <summary>Create new products</summary>
    public const string ProductsCreate = ProductsPermission.Keys.Create;

    /// <summary>Update existing products</summary>
    public const string ProductsUpdate = ProductsPermission.Keys.Update;

    /// <summary>Delete products</summary>
    public const string ProductsDelete = ProductsPermission.Keys.Delete;

    /// <summary>Full management access to products</summary>
    public const string ProductsManage = ProductsPermission.Keys.Manage;

    /// <summary>Manage product pricing</summary>
    public const string ProductsPricingManage = ProductsPermission.Keys.PricingManage;

    // ========================
    // PROMO CODES PERMISSIONS
    // ========================

    /// <summary>Read promo codes</summary>
    public const string PromoCodesRead = PromoCodesPermission.Keys.Read;

    /// <summary>Create promo codes</summary>
    public const string PromoCodesCreate = PromoCodesPermission.Keys.Create;

    /// <summary>Update promo codes</summary>
    public const string PromoCodesUpdate = PromoCodesPermission.Keys.Update;

    /// <summary>Delete promo codes</summary>
    public const string PromoCodesDelete = PromoCodesPermission.Keys.Delete;

    /// <summary>Full management access to promo codes</summary>
    public const string PromoCodesManage = PromoCodesPermission.Keys.Manage;

    // ========================
    // ORDERS PERMISSIONS
    // ========================

    /// <summary>Read orders (own orders)</summary>
    public const string OrdersRead = OrdersPermission.Keys.Read;

    /// <summary>Read all orders (admin)</summary>
    public const string OrdersReadAll = OrdersPermission.Keys.ReadAll;

    /// <summary>Create orders</summary>
    public const string OrdersCreate = OrdersPermission.Keys.Create;

    /// <summary>Process refunds</summary>
    public const string OrdersRefund = OrdersPermission.Keys.Refund;

    /// <summary>Full management access to orders</summary>
    public const string OrdersManage = OrdersPermission.Keys.Manage;

    // ========================
    // ENTITLEMENTS PERMISSIONS
    // ========================

    /// <summary>View own entitlements</summary>
    public const string EntitlementsReadSelf = EntitlementsPermission.Keys.ReadSelf;

    /// <summary>View all entitlements (admin)</summary>
    public const string EntitlementsReadAll = EntitlementsPermission.Keys.ReadAll;

    /// <summary>Grant entitlements</summary>
    public const string EntitlementsGrant = EntitlementsPermission.Keys.Grant;

    /// <summary>Revoke entitlements</summary>
    public const string EntitlementsRevoke = EntitlementsPermission.Keys.Revoke;

    /// <summary>Full management access to entitlements</summary>
    public const string EntitlementsManage = EntitlementsPermission.Keys.Manage;

    // ========================
    // SYSTEM PERMISSIONS
    // ========================

    /// <summary>Manage global default permissions (tenantId=null)</summary>
    public const string SystemManageGlobalDefaults = SystemPermission.Keys.ManageGlobalDefaults;

    /// <summary>Full system administration</summary>
    public const string SystemAdmin = SystemPermission.Keys.Admin;

    /// <summary>System wildcard (all permissions)</summary>
    public const string SystemWildcard = SystemPermission.Keys.Wildcard;

    // ========================
    // ASSETS PERMISSIONS
    // ========================

    /// <summary>Read/download assets</summary>
    public const string AssetsRead = AssetsPermission.Keys.Read;

    /// <summary>Upload new assets</summary>
    public const string AssetsCreate = AssetsPermission.Keys.Create;

    /// <summary>Update asset metadata</summary>
    public const string AssetsUpdate = AssetsPermission.Keys.Update;

    /// <summary>Delete assets</summary>
    public const string AssetsDelete = AssetsPermission.Keys.Delete;

    /// <summary>Admin operations (GC, undeletable marks)</summary>
    public const string AssetsAdmin = AssetsPermission.Keys.Admin;

    /// <summary>Moderate asset content</summary>
    public const string AssetsModerate = AssetsPermission.Keys.Moderate;

    /// <summary>Apply asset transformations</summary>
    public const string AssetsTransform = AssetsPermission.Keys.Transform;

    /// <summary>Generate asset access URLs</summary>
    public const string AssetsGenerateUrl = AssetsPermission.Keys.GenerateUrl;

    /// <summary>Report assets for moderation</summary>
    public const string AssetsReport = AssetsPermission.Keys.Report;
}

/// <summary>
///     System-level permissions for managing global defaults and system-wide settings.
/// </summary>
/// <remarks>
///     <para>
///         These permissions control access to system-level operations that affect
///         all tenants or global defaults. They should only be granted to system administrators.
///     </para>
/// </remarks>
public sealed class SystemPermission : Permission
{
    private SystemPermission(string key, string description)
        : base(
            resource: key.Split(':')[0],
            action: key.Contains(':') ? key.Split(':')[1] : key,
            scope: key.Split(':').Length > 2 ? key.Split(':')[2] : null,
            description: description)
    {
    }

    /// <summary>Permission to manage global default permissions (tenantId=null)</summary>
    public static readonly SystemPermission ManageGlobalDefaults = new(Keys.ManageGlobalDefaults, "Manage global default permissions");

    /// <summary>Permission to manage tenant permission grants and templates</summary>
    public static readonly SystemPermission ManagePermissions = new(Keys.ManagePermissions, "Manage tenant permission grants and templates");

    /// <summary>Full system administration permission</summary>
    public static readonly SystemPermission Admin = new(Keys.Admin, "Full system administration");

    /// <summary>System wildcard - grants all permissions</summary>
    public static readonly SystemPermission Wildcard = new(Keys.Wildcard, "System wildcard - grants all permissions");

    /// <summary>
    ///     Compile-time constant keys for use in attributes.
    /// </summary>
    public static class Keys
    {
        public const string ManageGlobalDefaults = "system:manage-global-defaults";
        public const string ManagePermissions = "permissions:manage";
        public const string Admin = "system:admin";
        public const string Wildcard = "system:*";
    }
}
