using GameGuild.Identity.Authorization.Models;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Strongly-typed permissions for admin operations.
///     Provides compile-time safety for permission checks.
/// </summary>
/// <remarks>
///     <para>
///         Use <see cref="Keys"/> for attribute usage: [RequirePermission(AdminPermission.Keys.Wildcard)]
///     </para>
///     <para>
///         Use the static readonly fields for runtime checks: actor.HasPermission(AdminPermission.Wildcard)
///     </para>
/// </remarks>
public sealed class AdminPermission : Permission
{
    private AdminPermission(string key, string description)
        : base(
            resource: key.Split(':')[0],
            action: key.Contains(':') ? key.Split(':')[1] : key,
            scope: key.Split(':').Length > 2 ? key.Split(':')[2] : null,
            description: description)
    {
    }

    /// <summary>
    ///     Permission key constants for use in attributes.
    /// </summary>
    /// <example>
    ///     [RequirePermission(AdminPermission.Keys.Wildcard)]
    ///     public IActionResult AdminAction() { }
    /// </example>
    public static class Keys
    {
        /// <summary>Full admin access (wildcard)</summary>
        public const string Wildcard = "admin:*";

        /// <summary>Admin permission</summary>
        public const string Admin = "admin:admin";

        /// <summary>Tenant admin permission</summary>
        public const string TenantAdmin = "tenant:admin";
    }

    /// <summary>Full admin access (wildcard)</summary>
    public static readonly AdminPermission Wildcard = new(Keys.Wildcard, "Full admin access (wildcard)");

    /// <summary>Admin permission</summary>
    public static readonly AdminPermission Admin = new(Keys.Admin, "Admin permission");

    /// <summary>Tenant admin permission</summary>
    public static readonly AdminPermission TenantAdmin = new(Keys.TenantAdmin, "Tenant admin permission");
}

/// <summary>
///     Strongly-typed permissions for SCIM provisioning administration.
/// </summary>
/// <remarks>
///     Use <see cref="Keys"/> for attribute usage: [RequirePermission(ProvisioningPermission.Keys.ManageTokens)]
///     and the static fields for runtime checks: actor.HasPermission(ProvisioningPermission.ManageTokens).
/// </remarks>
public sealed class ProvisioningPermission : Permission
{
    private ProvisioningPermission(string key, string description)
        : base(
            resource: key.Split(':')[0],
            action: key.Contains(':') ? key.Split(':')[1] : key,
            scope: key.Split(':').Length > 2 ? key.Split(':')[2] : null,
            description: description)
    {
    }

    /// <summary>Permission key constants for use in attributes.</summary>
    public static class Keys
    {
        /// <summary>Issue, rotate and revoke SCIM provisioning tokens.</summary>
        public const string ManageTokens = "provisioning:manage-tokens";
    }

    /// <summary>Issue, rotate and revoke SCIM provisioning tokens.</summary>
    public static readonly ProvisioningPermission ManageTokens =
        new(Keys.ManageTokens, "Manage SCIM provisioning tokens");
}

/// <summary>
///     Strongly-typed permissions for user operations.
///     Provides compile-time safety for permission checks.
/// </summary>
/// <remarks>
///     <para>
///         Use <see cref="Keys"/> for attribute usage: [RequirePermission(UsersPermission.Keys.Read)]
///     </para>
///     <para>
///         Use the static readonly fields for runtime checks: actor.HasPermission(UsersPermission.Read)
///     </para>
/// </remarks>
public sealed class UsersPermission : Permission
{
    private UsersPermission(string key, string description)
        : base(
            resource: key.Split(':')[0],
            action: key.Split(':')[1],
            scope: key.Split(':').Length > 2 ? key.Split(':')[2] : null,
            description: description)
    {
    }

    /// <summary>
    ///     Permission key constants for use in attributes.
    /// </summary>
    /// <example>
    ///     [RequirePermission(UsersPermission.Keys.Read)]
    ///     public IActionResult GetUsers() { }
    /// </example>
    public static class Keys
    {
        /// <summary>Read user data</summary>
        public const string Read = "users:read";

        /// <summary>Create new users</summary>
        public const string Create = "users:create";

        /// <summary>Update existing users</summary>
        public const string Update = "users:update";

        /// <summary>Soft-delete users</summary>
        public const string Delete = "users:delete";

        /// <summary>Administrative operations on users</summary>
        public const string Admin = "users:admin";

        /// <summary>Permanently delete users (dangerous)</summary>
        public const string Purge = "users:purge";

        /// <summary>Edit own profile</summary>
        public const string EditSelf = "users:edit:self";

        /// <summary>Delete own account</summary>
        public const string DeleteSelf = "users:delete:self";

        /// <summary>Read own data</summary>
        public const string ReadSelf = "users:read:self";

        /// <summary>Manage any user</summary>
        public const string Manage = "users:manage";
    }

    // CRUD Operations
    /// <summary>Read user data</summary>
    public static readonly UsersPermission Read = new(Keys.Read, "Read user data");

    /// <summary>Create new users</summary>
    public static readonly UsersPermission Create = new(Keys.Create, "Create new users");

    /// <summary>Update existing users</summary>
    public static readonly UsersPermission Update = new(Keys.Update, "Update existing users");

    /// <summary>Soft-delete users</summary>
    public static readonly UsersPermission Delete = new(Keys.Delete, "Soft-delete users");

    /// <summary>Administrative operations on users</summary>
    public static readonly UsersPermission Admin = new(Keys.Admin, "Administrative operations on users");

    /// <summary>Permanently delete users (dangerous)</summary>
    public static readonly UsersPermission Purge = new(Keys.Purge, "Permanently delete users (dangerous)");

    // Self Operations
    /// <summary>Edit own profile</summary>
    public static readonly UsersPermission EditSelf = new(Keys.EditSelf, "Edit own profile");

    /// <summary>Delete own account</summary>
    public static readonly UsersPermission DeleteSelf = new(Keys.DeleteSelf, "Delete own account");

    /// <summary>Read own data</summary>
    public static readonly UsersPermission ReadSelf = new(Keys.ReadSelf, "Read own data");

    /// <summary>Manage any user</summary>
    public static readonly UsersPermission Manage = new(Keys.Manage, "Manage any user");
}

/// <summary>
///     Strongly-typed permissions for content operations.
///     Provides compile-time safety for permission checks.
/// </summary>
public sealed class ContentPermission : Permission
{
    private ContentPermission(string key, string description)
        : base(
            resource: key.Split(':')[0],
            action: key.Split(':')[1],
            scope: key.Split(':').Length > 2 ? key.Split(':')[2] : null,
            description: description)
    {
    }

    /// <summary>
    ///     Permission key constants for use in attributes.
    /// </summary>
    public static class Keys
    {
        /// <summary>Read content</summary>
        public const string Read = "content:read";

        /// <summary>Write/edit content</summary>
        public const string Write = "content:write";

        /// <summary>Admin access to content</summary>
        public const string Admin = "content:admin";

        /// <summary>Edit existing editorial content (editorial permission)</summary>
        public const string Edit = "content:edit";

        /// <summary>Create and maintain content drafts (editorial permission)</summary>
        public const string Draft = "content:draft";

        /// <summary>Schedule and cancel scheduled publication (editorial permission)</summary>
        public const string Schedule = "content:schedule";
    }

    /// <summary>Read content</summary>
    public static readonly ContentPermission Read = new(Keys.Read, "Read content");

    /// <summary>Write/edit content</summary>
    public static readonly ContentPermission Write = new(Keys.Write, "Write/edit content");

    /// <summary>Admin access to content</summary>
    public static readonly ContentPermission Admin = new(Keys.Admin, "Admin access to content");

    /// <summary>Edit existing editorial content (editorial permission)</summary>
    public static readonly ContentPermission Edit = new(Keys.Edit, "Edit existing editorial content");

    /// <summary>Create and maintain content drafts (editorial permission)</summary>
    public static readonly ContentPermission Draft = new(Keys.Draft, "Create and maintain content drafts");

    /// <summary>Schedule and cancel scheduled publication (editorial permission)</summary>
    public static readonly ContentPermission Schedule = new(Keys.Schedule, "Schedule and cancel scheduled publication");
}

/// <summary>
///     Strongly-typed permissions for project operations.
///     Provides compile-time safety for permission checks.
/// </summary>
public sealed class ProjectPermission : Permission
{
    private ProjectPermission(string key, string description)
        : base(
            resource: key.Split(':')[0],
            action: key.Split(':')[1],
            scope: key.Split(':').Length > 2 ? key.Split(':')[2] : null,
            description: description)
    {
    }

    /// <summary>
    ///     Permission key constants for use in attributes.
    /// </summary>
    public static class Keys
    {
        /// <summary>Read projects</summary>
        public const string Read = "project:read";

        /// <summary>Write/edit projects</summary>
        public const string Write = "project:write";

        /// <summary>Admin access to projects</summary>
        public const string Admin = "project:admin";
    }

    /// <summary>Read projects</summary>
    public static readonly ProjectPermission Read = new(Keys.Read, "Read projects");

    /// <summary>Write/edit projects</summary>
    public static readonly ProjectPermission Write = new(Keys.Write, "Write/edit projects");

    /// <summary>Admin access to projects</summary>
    public static readonly ProjectPermission Admin = new(Keys.Admin, "Admin access to projects");
}

/// <summary>
///     Strongly-typed permissions for course operations.
///     Provides compile-time safety for permission checks.
/// </summary>
public sealed class CoursePermission : Permission
{
    private CoursePermission(string key, string description)
        : base(
            resource: key.Split(':')[0],
            action: key.Split(':')[1],
            scope: key.Split(':').Length > 2 ? key.Split(':')[2] : null,
            description: description)
    {
    }

    /// <summary>
    ///     Permission key constants for use in attributes.
    /// </summary>
    public static class Keys
    {
        /// <summary>Read courses</summary>
        public const string Read = "course:read";

        /// <summary>Manage courses</summary>
        public const string Manage = "course:manage";
    }

    /// <summary>Read courses</summary>
    public static readonly CoursePermission Read = new(Keys.Read, "Read courses");

    /// <summary>Manage courses</summary>
    public static readonly CoursePermission Manage = new(Keys.Manage, "Manage courses");
}

/// <summary>
///     Strongly-typed permissions for product operations.
///     Provides compile-time safety for permission checks.
/// </summary>
public sealed class ProductsPermission : Permission
{
    private ProductsPermission(string key, string description)
        : base(
            resource: key.Split(':')[0],
            action: key.Split(':')[1],
            scope: key.Split(':').Length > 2 ? key.Split(':')[2] : null,
            description: description)
    {
    }

    /// <summary>
    ///     Permission key constants for use in attributes.
    /// </summary>
    public static class Keys
    {
        /// <summary>Read product data</summary>
        public const string Read = "products:read";

        /// <summary>Create new products</summary>
        public const string Create = "products:create";

        /// <summary>Update existing products</summary>
        public const string Update = "products:update";

        /// <summary>Delete products</summary>
        public const string Delete = "products:delete";

        /// <summary>Full management access to products</summary>
        public const string Manage = "products:manage";

        /// <summary>Manage product pricing</summary>
        public const string PricingManage = "products:pricing:manage";
    }

    /// <summary>Read product data</summary>
    public static readonly ProductsPermission Read = new(Keys.Read, "Read product data");

    /// <summary>Create new products</summary>
    public static readonly ProductsPermission Create = new(Keys.Create, "Create new products");

    /// <summary>Update existing products</summary>
    public static readonly ProductsPermission Update = new(Keys.Update, "Update existing products");

    /// <summary>Delete products</summary>
    public static readonly ProductsPermission Delete = new(Keys.Delete, "Delete products");

    /// <summary>Full management access to products</summary>
    public static readonly ProductsPermission Manage = new(Keys.Manage, "Full management access to products");

    /// <summary>Manage product pricing</summary>
    public static readonly ProductsPermission PricingManage = new(Keys.PricingManage, "Manage product pricing");
}

/// <summary>
///     Strongly-typed permissions for promo code operations.
///     Provides compile-time safety for permission checks.
/// </summary>
public sealed class PromoCodesPermission : Permission
{
    private PromoCodesPermission(string key, string description)
        : base(
            resource: key.Split(':')[0],
            action: key.Split(':')[1],
            scope: key.Split(':').Length > 2 ? key.Split(':')[2] : null,
            description: description)
    {
    }

    /// <summary>
    ///     Permission key constants for use in attributes.
    /// </summary>
    public static class Keys
    {
        /// <summary>Read promo codes</summary>
        public const string Read = "promocodes:read";

        /// <summary>Create promo codes</summary>
        public const string Create = "promocodes:create";

        /// <summary>Update promo codes</summary>
        public const string Update = "promocodes:update";

        /// <summary>Delete promo codes</summary>
        public const string Delete = "promocodes:delete";

        /// <summary>Full management access to promo codes</summary>
        public const string Manage = "promocodes:manage";
    }

    /// <summary>Read promo codes</summary>
    public static readonly PromoCodesPermission Read = new(Keys.Read, "Read promo codes");

    /// <summary>Create promo codes</summary>
    public static readonly PromoCodesPermission Create = new(Keys.Create, "Create promo codes");

    /// <summary>Update promo codes</summary>
    public static readonly PromoCodesPermission Update = new(Keys.Update, "Update promo codes");

    /// <summary>Delete promo codes</summary>
    public static readonly PromoCodesPermission Delete = new(Keys.Delete, "Delete promo codes");

    /// <summary>Full management access to promo codes</summary>
    public static readonly PromoCodesPermission Manage = new(Keys.Manage, "Full management access to promo codes");
}

/// <summary>
///     Strongly-typed permissions for order operations.
///     Provides compile-time safety for permission checks.
/// </summary>
public sealed class OrdersPermission : Permission
{
    private OrdersPermission(string key, string description)
        : base(
            resource: key.Split(':')[0],
            action: key.Split(':')[1],
            scope: key.Split(':').Length > 2 ? key.Split(':')[2] : null,
            description: description)
    {
    }

    /// <summary>
    ///     Permission key constants for use in attributes.
    /// </summary>
    public static class Keys
    {
        /// <summary>Read orders (own orders)</summary>
        public const string Read = "orders:read";

        /// <summary>Read all orders (admin)</summary>
        public const string ReadAll = "orders:read:all";

        /// <summary>Create orders</summary>
        public const string Create = "orders:create";

        /// <summary>Update orders</summary>
        public const string Update = "orders:update";

        /// <summary>Delete/cancel orders</summary>
        public const string Delete = "orders:delete";

        /// <summary>Capture payment for orders</summary>
        public const string Capture = "orders:capture";

        /// <summary>Place orders on hold</summary>
        public const string Hold = "orders:hold";

        /// <summary>Release held orders</summary>
        public const string Release = "orders:release";

        /// <summary>Process refunds</summary>
        public const string Refund = "orders:refund";

        /// <summary>Full management access to orders</summary>
        public const string Manage = "orders:manage";
    }

    /// <summary>Read orders (own orders)</summary>
    public static readonly OrdersPermission Read = new(Keys.Read, "Read orders (own orders)");

    /// <summary>Read all orders (admin)</summary>
    public static readonly OrdersPermission ReadAll = new(Keys.ReadAll, "Read all orders (admin)");

    /// <summary>Create orders</summary>
    public static readonly OrdersPermission Create = new(Keys.Create, "Create orders");

    /// <summary>Update orders</summary>
    public static readonly OrdersPermission Update = new(Keys.Update, "Update orders");

    /// <summary>Delete/cancel orders</summary>
    public static readonly OrdersPermission Delete = new(Keys.Delete, "Delete/cancel orders");

    /// <summary>Capture payment for orders</summary>
    public static readonly OrdersPermission Capture = new(Keys.Capture, "Capture payment for orders");

    /// <summary>Place orders on hold</summary>
    public static readonly OrdersPermission Hold = new(Keys.Hold, "Place orders on hold");

    /// <summary>Release held orders</summary>
    public static readonly OrdersPermission Release = new(Keys.Release, "Release held orders");

    /// <summary>Process refunds</summary>
    public static readonly OrdersPermission Refund = new(Keys.Refund, "Process refunds");

    /// <summary>Full management access to orders</summary>
    public static readonly OrdersPermission Manage = new(Keys.Manage, "Full management access to orders");
}

/// <summary>
///     Strongly-typed permissions for entitlement operations.
///     Provides compile-time safety for permission checks.
/// </summary>
public sealed class EntitlementsPermission : Permission
{
    private EntitlementsPermission(string key, string description)
        : base(
            resource: key.Split(':')[0],
            action: key.Split(':')[1],
            scope: key.Split(':').Length > 2 ? key.Split(':')[2] : null,
            description: description)
    {
    }

    /// <summary>
    ///     Permission key constants for use in attributes.
    /// </summary>
    public static class Keys
    {
        /// <summary>View own entitlements</summary>
        public const string ReadSelf = "entitlements:read:self";

        /// <summary>View all entitlements (admin)</summary>
        public const string ReadAll = "entitlements:read:all";

        /// <summary>Grant entitlements</summary>
        public const string Grant = "entitlements:grant";

        /// <summary>Revoke entitlements</summary>
        public const string Revoke = "entitlements:revoke";

        /// <summary>Full management access to entitlements</summary>
        public const string Manage = "entitlements:manage";
    }

    /// <summary>View own entitlements</summary>
    public static readonly EntitlementsPermission ReadSelf = new(Keys.ReadSelf, "View own entitlements");

    /// <summary>View all entitlements (admin)</summary>
    public static readonly EntitlementsPermission ReadAll = new(Keys.ReadAll, "View all entitlements (admin)");

    /// <summary>Grant entitlements</summary>
    public static readonly EntitlementsPermission Grant = new(Keys.Grant, "Grant entitlements");

    /// <summary>Revoke entitlements</summary>
    public static readonly EntitlementsPermission Revoke = new(Keys.Revoke, "Revoke entitlements");

    /// <summary>Full management access to entitlements</summary>
    public static readonly EntitlementsPermission Manage = new(Keys.Manage, "Full management access to entitlements");
}

/// <summary>
///     Strongly-typed permissions for asset operations.
///     Provides compile-time safety for permission checks.
/// </summary>
/// <remarks>
///     <para>
///         Use <see cref="Keys"/> for attribute usage: [RequirePermission(AssetsPermission.Keys.Read)]
///     </para>
///     <para>
///         Use the static readonly fields for runtime checks: actor.HasPermission(AssetsPermission.Read)
///     </para>
/// </remarks>
public sealed class AssetsPermission : Permission
{
    private AssetsPermission(string key, string description)
        : base(
            resource: key.Split(':')[0],
            action: key.Split(':')[1],
            scope: key.Split(':').Length > 2 ? key.Split(':')[2] : null,
            description: description)
    {
    }

    /// <summary>
    ///     Permission key constants for use in attributes.
    /// </summary>
    /// <example>
    ///     [RequirePermission(AssetsPermission.Keys.Read)]
    ///     public IActionResult GetAssets() { }
    /// </example>
    public static class Keys
    {
        /// <summary>Read/download assets</summary>
        public const string Read = "assets:read";

        /// <summary>Upload new assets</summary>
        public const string Create = "assets:create";

        /// <summary>Update asset metadata</summary>
        public const string Update = "assets:update";

        /// <summary>Delete assets</summary>
        public const string Delete = "assets:delete";

        /// <summary>Admin operations (GC, undeletable marks)</summary>
        public const string Admin = "assets:admin";

        /// <summary>Moderate content</summary>
        public const string Moderate = "assets:moderate";

        /// <summary>Apply transformations</summary>
        public const string Transform = "assets:transform";

        /// <summary>Generate access URLs</summary>
        public const string GenerateUrl = "assets:generate-url";

        /// <summary>Report assets for moderation</summary>
        public const string Report = "assets:report";
    }

    /// <summary>Read/download assets</summary>
    public static readonly AssetsPermission Read = new(Keys.Read, "Read/download assets");

    /// <summary>Upload new assets</summary>
    public static readonly AssetsPermission Create = new(Keys.Create, "Upload new assets");

    /// <summary>Update asset metadata</summary>
    public static readonly AssetsPermission Update = new(Keys.Update, "Update asset metadata");

    /// <summary>Delete assets</summary>
    public static readonly AssetsPermission Delete = new(Keys.Delete, "Delete assets");

    /// <summary>Admin operations (GC, undeletable marks)</summary>
    public static readonly AssetsPermission Admin = new(Keys.Admin, "Admin operations (GC, undeletable marks)");

    /// <summary>Moderate content</summary>
    public static readonly AssetsPermission Moderate = new(Keys.Moderate, "Moderate content");

    /// <summary>Apply transformations</summary>
    public static readonly AssetsPermission Transform = new(Keys.Transform, "Apply transformations");

    /// <summary>Generate access URLs</summary>
    public static readonly AssetsPermission GenerateUrl = new(Keys.GenerateUrl, "Generate access URLs");

    /// <summary>Report assets for moderation</summary>
    public static readonly AssetsPermission Report = new(Keys.Report, "Report assets for moderation");
}

/// <summary>
///     Strongly-typed permissions for moderation operations.
///     Covers the cross-cutting moderation duties (moderate content, flag for
///     review, ban members, warn members) that apply across content resources.
/// </summary>
/// <remarks>
///     <para>
///         Use <see cref="Keys"/> for attribute usage: [RequirePermission(ModerationPermission.Keys.Moderate)]
///     </para>
///     <para>
///         Use the static readonly fields for runtime checks: actor.HasPermission(ModerationPermission.Moderate)
///     </para>
/// </remarks>
public sealed class ModerationPermission : Permission
{
    private ModerationPermission(string key, string description)
        : base(
            resource: key.Split(':')[0],
            action: key.Split(':')[1],
            scope: key.Split(':').Length > 2 ? key.Split(':')[2] : null,
            description: description)
    {
    }

    /// <summary>
    ///     Permission key constants for use in attributes.
    /// </summary>
    /// <example>
    ///     [RequirePermission(ModerationPermission.Keys.Moderate)]
    ///     public IActionResult ReviewQueue() { }
    /// </example>
    public static class Keys
    {
        /// <summary>Moderate content (edit, hide, restore)</summary>
        public const string Moderate = "moderation:moderate";

        /// <summary>Flag content for review</summary>
        public const string Flag = "moderation:flag";

        /// <summary>Ban members</summary>
        public const string Ban = "moderation:ban";

        /// <summary>Warn members</summary>
        public const string Warn = "moderation:warn";
    }

    /// <summary>Moderate content (edit, hide, restore)</summary>
    public static readonly ModerationPermission Moderate = new(Keys.Moderate, "Moderate content (edit, hide, restore)");

    /// <summary>Flag content for review</summary>
    public static readonly ModerationPermission Flag = new(Keys.Flag, "Flag content for review");

    /// <summary>Ban members</summary>
    public static readonly ModerationPermission Ban = new(Keys.Ban, "Ban members");

    /// <summary>Warn members</summary>
    public static readonly ModerationPermission Warn = new(Keys.Warn, "Warn members");
}

/// <summary>Strongly-typed permission for platform-level wallet administration.</summary>
public sealed class WalletsPermission : Permission
{
    private WalletsPermission(string key, string description)
        : base(
            resource: key.Split(':')[0],
            action: key.Split(':')[1],
            scope: null,
            description: description)
    {
    }

    public static class Keys
    {
        public const string Admin = "wallets:admin";
    }

    public static readonly WalletsPermission Admin = new(Keys.Admin, "Administer wallets across the platform");
}

/// <summary>Segregated permissions for Economy control-plane and operational duties.</summary>
public sealed class EconomyPermission : Permission
{
    private EconomyPermission(string key, string description)
        : base(
            resource: key.Split(':')[0],
            action: key.Split(':')[1],
            scope: key.Split(':').Length > 2 ? key.Split(':')[2] : null,
            description: description)
    {
    }

    public static class Keys
    {
        public const string ReadOperations = "economy:read:operations";
        public const string ReviewPayouts = "economy:review:payouts";
        public const string OperatePayouts = "economy:operate:payouts";
        public const string OperateCompliance = "economy:operate:compliance";
        public const string ManagePolicies = "economy:manage:policies";
        public const string ManageReserves = "economy:manage:reserves";
        public const string OperateLedger = "economy:operate:ledger";
        public const string ManageKillSwitches = "economy:manage:kill-switches";
        public const string OperateAdRewards = "economy:operate:ad-rewards";
        public const string OperateMarketplace = "economy:operate:marketplace";
        public const string OperateBounties = "economy:operate:bounties";
        public const string OperateTreasury = "economy:operate:treasury";
        public const string ManageLegacyMigration = "economy:manage:legacy-migration";
    }

    public static readonly EconomyPermission ReadOperations = new(Keys.ReadOperations, "Read Economy operational state");
    public static readonly EconomyPermission ReviewPayouts = new(Keys.ReviewPayouts, "Review tenant payout requests");
    public static readonly EconomyPermission OperatePayouts = new(Keys.OperatePayouts, "Reserve, dispatch, and reconcile approved tenant payouts");
    public static readonly EconomyPermission OperateCompliance = new(Keys.OperateCompliance, "Operate Economy compliance cases and evidence");
    public static readonly EconomyPermission ManagePolicies = new(Keys.ManagePolicies, "Propose and approve Economy policies");
    public static readonly EconomyPermission ManageReserves = new(Keys.ManageReserves, "Manage Economy reserves and custody observations");
    public static readonly EconomyPermission OperateLedger = new(Keys.OperateLedger, "Operate Economy journal, projections and anchors");
    public static readonly EconomyPermission ManageKillSwitches = new(Keys.ManageKillSwitches, "Activate and release Economy kill switches");
    public static readonly EconomyPermission OperateAdRewards = new(Keys.OperateAdRewards, "Operate AdRewards reports and reconciliation");
    public static readonly EconomyPermission OperateMarketplace = new(Keys.OperateMarketplace, "Operate Marketplace settlements and refunds");
    public static readonly EconomyPermission OperateBounties = new(Keys.OperateBounties, "Operate Bounty expiration and conflicts");
    public static readonly EconomyPermission OperateTreasury = new(Keys.OperateTreasury, "Operate Treasury withdrawals and reconciliation");
    public static readonly EconomyPermission ManageLegacyMigration = new(Keys.ManageLegacyMigration, "Manage tenant-scoped Economy legacy migration and cutover");
}

/// <summary>
///     Strongly-typed permissions for monetization and analytics authorization
///     (issue #346: Monetize, ViewAnalytics, Configure).
/// </summary>
/// <remarks>
///     <para>
///         These permissions control access to revenue-generation features and
///         platform business-intelligence data:
///         <list type="bullet">
///             <item><see cref="Monetize"/> — enable revenue generation, set pricing, manage monetization settings.</item>
///             <item><see cref="ViewAnalytics"/> — access revenue reports, financial dashboards and performance metrics.</item>
///             <item><see cref="Configure"/> — manage payment settings, subscription tiers and pricing models.</item>
///         </list>
///     </para>
///     <para>
///         The keys are ordinary tenant-level permission strings, so they flow through the
///         existing grant/inheritance/precedence machinery (global defaults → tenant defaults →
///         direct grants → role providers, deny-wins) without any special casing.
///     </para>
/// </remarks>
public sealed class MonetizationPermission : Permission
{
    private MonetizationPermission(string key, string description)
        : base(
            resource: key.Split(':')[0],
            action: key.Split(':')[1],
            scope: key.Split(':').Length > 2 ? key.Split(':')[2] : null,
            description: description)
    {
    }

    /// <summary>
    ///     Permission key constants for use in attributes.
    /// </summary>
    /// <example>
    ///     [RequirePermission(MonetizationPermission.Keys.ViewAnalytics)]
    ///     public IActionResult GetMetrics() { }
    /// </example>
    public static class Keys
    {
        /// <summary>Enable revenue generation, set pricing, manage monetization settings</summary>
        public const string Monetize = "monetization:monetize";

        /// <summary>Access revenue reports, financial dashboards, performance metrics</summary>
        public const string ViewAnalytics = "monetization:view-analytics";

        /// <summary>Manage payment settings, subscription tiers, pricing models</summary>
        public const string Configure = "monetization:configure";
    }

    /// <summary>Enable revenue generation, set pricing, manage monetization settings</summary>
    public static readonly MonetizationPermission Monetize = new(Keys.Monetize, "Enable revenue generation, set pricing and manage monetization settings");

    /// <summary>Access revenue reports, financial dashboards, performance metrics</summary>
    public static readonly MonetizationPermission ViewAnalytics = new(Keys.ViewAnalytics, "Access revenue reports, financial dashboards and performance metrics");

    /// <summary>Manage payment settings, subscription tiers, pricing models</summary>
    public static readonly MonetizationPermission Configure = new(Keys.Configure, "Manage payment settings, subscription tiers and pricing models");
}
