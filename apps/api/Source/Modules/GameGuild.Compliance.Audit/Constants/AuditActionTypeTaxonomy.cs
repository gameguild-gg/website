using System.Reflection;

namespace GameGuild.Compliance.Audit;

/// <summary>Stable taxonomy for known action types, with derived groups for common investigations.</summary>
public static class AuditActionTypeTaxonomy
{
    private static readonly string[] KnownGroups = ["CRUD", "Security", "Admin"];

    private static readonly IReadOnlyList<AuditActionTypeDescriptor> Descriptors =
        typeof(AuditActionTypes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
            .Select(field => field.GetRawConstantValue() as string)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => Describe(value!))
            .OrderBy(item => string.Join('.', item.CategoryPath), StringComparer.Ordinal)
            .ThenBy(item => item.ActionType, StringComparer.Ordinal)
            .ToArray();

    public static AuditActionTypeTaxonomyResponse GetTaxonomy() => new()
    {
        ActionTypes = Descriptors,
        Groups =
        [
            new AuditActionTypeGroupDescriptor("CRUD", "Create, update, delete, add, and remove operations."),
            new AuditActionTypeGroupDescriptor("Security", "Authentication, authorization, session, and threat events."),
            new AuditActionTypeGroupDescriptor("Admin", "Administrative and system configuration events.")
        ]
    };

    public static bool IsKnownGroup(string? group) =>
        KnownGroups.Contains(group?.Trim(), StringComparer.OrdinalIgnoreCase);

    public static bool IsKnownCategory(string? category) =>
        Descriptors.Any(item => string.Equals(string.Join('.', item.CategoryPath), category?.Trim(), StringComparison.OrdinalIgnoreCase));

    internal static string NormalizeGroup(string group) =>
        KnownGroups.Single(value => string.Equals(value, group.Trim(), StringComparison.OrdinalIgnoreCase));

    internal static string NormalizeCategory(string category) =>
        string.Join('.', Descriptors.First(item => string.Equals(
            string.Join('.', item.CategoryPath), category.Trim(), StringComparison.OrdinalIgnoreCase)).CategoryPath);

    internal static string[] GetActionTypesForGroup(string group) => Descriptors
        .Where(item => item.Groups.Contains(NormalizeGroup(group), StringComparer.Ordinal))
        .Select(item => item.ActionType)
        .ToArray();

    internal static string[] GetActionTypesForCategory(string category) => Descriptors
        .Where(item => string.Equals(string.Join('.', item.CategoryPath), NormalizeCategory(category), StringComparison.Ordinal))
        .Select(item => item.ActionType)
        .ToArray();

    internal static AuditCategory[] GetAuditCategoriesForGroup(string group) => NormalizeGroup(group) switch
    {
        "Security" => [AuditCategory.Authentication, AuditCategory.Authorization, AuditCategory.Permission, AuditCategory.Security],
        "Admin" => [AuditCategory.Admin, AuditCategory.System],
        _ => []
    };

    internal static AuditCategory[] GetAuditCategoriesForTaxonomyCategory(string category) => NormalizeCategory(category) switch
    {
        "Security.Authentication" => [AuditCategory.Authentication],
        "Security.Authorization" => [AuditCategory.Authorization, AuditCategory.Permission],
        "Security.Sessions" => [],
        "Security.ThreatDetection" => [AuditCategory.Security],
        // The persisted user category also represents username events, so these leaves use
        // their explicitly classified action types instead of that broad category.
        "Identity.UserManagement" => [],
        "Identity.Usernames" => [],
        "Administration.Platform" => [AuditCategory.Admin, AuditCategory.System],
        "Data.Movement" => [AuditCategory.Data],
        "Data.Privacy" => [AuditCategory.Privacy],
        "Tenancy.Management" => [AuditCategory.Tenant],
        "General.Other" => [AuditCategory.General],
        _ => []
    };

    private static AuditActionTypeDescriptor Describe(string actionType)
    {
        var path = GetCategoryPath(actionType);
        var groups = new List<string>();
        if (path[0] == "Security")
        {
            groups.Add("Security");
        }
        if (path.SequenceEqual(["Administration", "Platform"]) ||
            actionType is AuditActionTypes.DataExported or AuditActionTypes.DataImported)
        {
            groups.Add("Admin");
        }
        if (IsCrudAction(actionType))
        {
            groups.Add("CRUD");
        }
        return new AuditActionTypeDescriptor(actionType, path, groups);
    }

    private static string[] GetCategoryPath(string actionType)
    {
        if (actionType.StartsWith("Login", StringComparison.Ordinal) ||
            actionType.StartsWith("Logout", StringComparison.Ordinal) ||
            actionType.StartsWith("Mfa", StringComparison.Ordinal) ||
            actionType.StartsWith("Password", StringComparison.Ordinal))
        {
            return ["Security", "Authentication"];
        }

        if (actionType.StartsWith("Permission", StringComparison.Ordinal) ||
            actionType.StartsWith("Role", StringComparison.Ordinal) ||
            actionType == AuditActionTypes.AccessDenied)
        {
            return ["Security", "Authorization"];
        }

        if (actionType.StartsWith("Session", StringComparison.Ordinal) || actionType.StartsWith("Device", StringComparison.Ordinal))
        {
            return ["Security", "Sessions"];
        }

        if (actionType is AuditActionTypes.SecurityViolation or AuditActionTypes.RateLimitExceeded or
            AuditActionTypes.SuspiciousActivity or AuditActionTypes.PolicyViolation or AuditActionTypes.TenantIsolationBypassed)
        {
            return ["Security", "ThreatDetection"];
        }

        if (actionType.StartsWith("Username", StringComparison.Ordinal))
        {
            return ["Identity", "Usernames"];
        }
        if (actionType.StartsWith("User", StringComparison.Ordinal))
        {
            return ["Identity", "UserManagement"];
        }
        if (actionType.StartsWith("Tenant", StringComparison.Ordinal))
        {
            return ["Tenancy", "Management"];
        }
        if (actionType.StartsWith("Privacy", StringComparison.Ordinal))
        {
            return ["Data", "Privacy"];
        }

        if (actionType is AuditActionTypes.DataExported or AuditActionTypes.DataImported)
        {
            return ["Data", "Movement"];
        }

        if (actionType is AuditActionTypes.AdminAction or AuditActionTypes.SystemConfigChanged or AuditActionTypes.BulkOperation)
        {
            return ["Administration", "Platform"];
        }

        return ["General", "Other"];
    }

    private static bool IsCrudAction(string actionType) =>
        actionType.EndsWith("Created", StringComparison.Ordinal) ||
        actionType.EndsWith("Updated", StringComparison.Ordinal) ||
        actionType.EndsWith("Deleted", StringComparison.Ordinal) ||
        actionType.EndsWith("Added", StringComparison.Ordinal) ||
        actionType.EndsWith("Removed", StringComparison.Ordinal);
}
