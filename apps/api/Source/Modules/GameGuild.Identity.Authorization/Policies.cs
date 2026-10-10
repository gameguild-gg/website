namespace GameGuild.Identity.Authorization;

/// <summary>
///     Central registry of all policy names used in the system.
///     Use these constants instead of magic strings in [Authorize(Policy = "...")] attributes.
/// </summary>
/// <remarks>
///     <para>
///         All policy names are registered in <see cref="All"/> for validation.
///         Use <see cref="IsValid"/> to check if a policy name is valid at runtime.
///     </para>
/// </remarks>
public static class Policies
{
    // ========================
    // AUTHENTICATION POLICIES
    // ========================

    /// <summary>Requires authenticated user</summary>
    public const string Authenticated = "Authenticated";

    /// <summary>Allows anonymous access</summary>
    public const string Anonymous = "Anonymous";

    // ========================
    // TENANT-SCOPED POLICIES
    // ========================

    /// <summary>Requires authenticated user with matching tenant context</summary>
    public const string TenantMember = "TenantMember";

    /// <summary>Tenant administrator with full tenant access</summary>
    public const string TenantAdmin = "TenantAdmin";

    // ========================
    // PROJECT POLICIES (DAC)
    // ========================

    /// <summary>Read access to projects</summary>
    public const string ProjectRead = "Project.Read";

    /// <summary>Edit access to projects</summary>
    public const string ProjectEdit = "Project.Edit";

    /// <summary>Delete access to projects</summary>
    public const string ProjectDelete = "Project.Delete";

    /// <summary>Full owner access to projects</summary>
    public const string ProjectOwner = "Project.Owner";

    // ========================
    // CONTENT POLICIES (DAC)
    // ========================

    /// <summary>Read access to content items</summary>
    public const string ContentRead = "Content.Read";

    /// <summary>Edit access to content items</summary>
    public const string ContentEdit = "Content.Edit";

    // ========================
    // COURSE POLICIES (DAC)
    // ========================

    /// <summary>Read access to courses</summary>
    public const string CourseRead = "Course.Read";

    /// <summary>Management access to courses</summary>
    public const string CourseManage = "Course.Manage";

    public const string CourseContentPublicOutline = "Course.Content.PublicOutline";

    public const string CourseContentLearner = "Course.Content.Learner";

    public const string CourseContentViewAll = "Course.Content.ViewAll";

    public const string CourseContentManage = "Course.Content.Manage";

    // ========================
    // FEATURE FLAG POLICIES
    // ========================

    /// <summary>Read access to feature flag definitions (feature-flag management surface)</summary>
    public const string FeaturesRead = "Features.Read";

    /// <summary>Create, update, delete, enable, disable and toggle feature flags</summary>
    public const string FeaturesManage = "Features.Manage";

    // ========================
    // LEDGER POLICIES
    // ========================

    /// <summary>Read access to ledgers, entries, balances and rollups</summary>
    public const string LedgersRead = "Ledgers.Read";

    /// <summary>Create ledgers and post, update, reverse or delete ledger entries</summary>
    public const string LedgersWrite = "Ledgers.Write";

    // ========================
    // DOCUMENT POLICIES
    // ========================

    /// <summary>Edit documents user owns or has ACL access to</summary>
    public const string DocumentEdit = "Document.Edit";

    // ========================
    // ADMIN POLICIES
    // ========================

    /// <summary>Administrator role with full access</summary>
    public const string Admin = "Admin";

    /// <summary>System administrator with cross-tenant platform authority</summary>
    public const string SystemAdmin = "SystemAdmin";

    /// <summary>Admin operations requiring MFA</summary>
    public const string SecureAdmin = "SecureAdmin";

    // ========================
    // PROVISIONING POLICIES
    // ========================

    /// <summary>Issue, rotate and revoke SCIM provisioning tokens</summary>
    public const string ProvisioningManageTokens = "Provisioning.ManageTokens";

    // ========================
    // USER POLICIES - Collection Operations
    // ========================

    /// <summary>Read access to user data (list users, search)</summary>
    public const string UsersRead = "Users.Read";

    /// <summary>Permission to create new users</summary>
    public const string UsersCreate = "Users.Create";

    /// <summary>Permission to update existing users</summary>
    public const string UsersUpdate = "Users.Update";

    /// <summary>Permission to soft-delete users</summary>
    public const string UsersDelete = "Users.Delete";

    /// <summary>Administrative user operations (activate, deactivate, suspend, restore)</summary>
    public const string UsersAdmin = "Users.Admin";

    /// <summary>Dangerous: Permission to permanently delete users (irreversible)</summary>
    public const string UsersPurge = "Users.Purge";

    // ========================
    // EMPLOYEE POLICIES
    // ========================

    /// <summary>Read access to employee records</summary>
    public const string EmployeesRead = "Employees.Read";

    /// <summary>Permission to create employee records</summary>
    public const string EmployeesCreate = "Employees.Create";

    /// <summary>Permission to update employee records</summary>
    public const string EmployeesUpdate = "Employees.Update";

    /// <summary>Permission to delete employee records</summary>
    public const string EmployeesDelete = "Employees.Delete";

    // ========================
    // USER POLICIES - Self/Single-User Operations
    // ========================

    /// <summary>Read own user data OR manage any user (SelfOrPermission)</summary>
    public const string UsersReadSelf = "Users.ReadSelf";

    /// <summary>Edit own profile OR manage other users (SelfOrPermission)</summary>
    public const string UsersEditSelf = "Users.EditSelf";

    /// <summary>Delete own account OR manage other users (SelfOrPermission)</summary>
    public const string UsersDeleteSelf = "Users.DeleteSelf";

    // ========================
    // POLICY REGISTRY & VALIDATION
    // ========================

    /// <summary>
    ///     All registered policy names for validation.
    ///     Use this to validate policy names at startup or runtime.
    /// </summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        // Authentication
        Authenticated, Anonymous,
        // Tenant
        TenantMember, TenantAdmin,
        // Project
        ProjectRead, ProjectEdit, ProjectDelete, ProjectOwner,
        // Content
        ContentRead, ContentEdit,
        // Course
        CourseRead, CourseManage,
        CourseContentPublicOutline, CourseContentLearner, CourseContentViewAll, CourseContentManage,
        // Document
        DocumentEdit,
        // Feature flags
        FeaturesRead, FeaturesManage,
        // Ledgers
        LedgersRead, LedgersWrite,
        // Admin
        Admin, SystemAdmin, SecureAdmin,
        // Provisioning
        ProvisioningManageTokens,
        // Users - Collection
        UsersRead, UsersCreate, UsersUpdate, UsersDelete, UsersAdmin, UsersPurge,
        // Employees
        EmployeesRead, EmployeesCreate, EmployeesUpdate, EmployeesDelete,
        // Users - Self
        UsersReadSelf, UsersEditSelf, UsersDeleteSelf
    };

    /// <summary>
    ///     Validates if a policy name is registered in the system.
    /// </summary>
    /// <param name="policyName">The policy name to validate.</param>
    /// <returns>True if the policy name is valid and registered.</returns>
    public static bool IsValid(string policyName) =>
        All.Contains(policyName, StringComparer.Ordinal);

    /// <summary>
    ///     Gets all policies matching a prefix (e.g., "Users." returns all user policies).
    /// </summary>
    /// <param name="prefix">The prefix to match.</param>
    /// <returns>All policies starting with the prefix.</returns>
    public static IEnumerable<string> GetByPrefix(string prefix) =>
        All.Where(p => p.StartsWith(prefix, StringComparison.Ordinal));
}
