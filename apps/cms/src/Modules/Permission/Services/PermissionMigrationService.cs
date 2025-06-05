using System;
using System.Collections.Generic;
using cms.Common.Entities;

namespace cms.Modules.Permission.Services;

/// <summary>
/// Service to migrate from legacy PermissionType to new modular permission system
/// </summary>
public class PermissionMigrationService
{
    /// <summary>
    /// Migrates legacy PermissionType to UnifiedPermissionContext
    /// </summary>
#pragma warning disable CS0618 // Type or member is obsolete
    public UnifiedPermissionContext MigrateFromLegacy(PermissionType legacyPermission)
    {
        var context = new UnifiedPermissionContext();
        
        // Map core CRUD permissions
        if (legacyPermission.HasFlag(PermissionType.Read))
            context.CorePermissions |= CorePermissionType.Read;
        if (legacyPermission.HasFlag(PermissionType.Create))
            context.CorePermissions |= CorePermissionType.Create;
        if (legacyPermission.HasFlag(PermissionType.Update))
            context.CorePermissions |= CorePermissionType.Update;
        if (legacyPermission.HasFlag(PermissionType.Delete))
            context.CorePermissions |= CorePermissionType.Delete;
            
        return context;
    }
    
    /// <summary>
    /// Validates that all legacy permissions can be migrated
    /// </summary>
    public IEnumerable<string> ValidateMigration(PermissionType legacyPermission)
    {
        var issues = new List<string>();
        
        // Currently only core permissions are mapped
        var mappedFlags = PermissionType.Read | PermissionType.Create | PermissionType.Update | PermissionType.Delete;
        var unmappedFlags = legacyPermission & ~mappedFlags;
        
        if (unmappedFlags != PermissionType.None)
        {
            issues.Add($"Unmapped legacy permissions detected: {unmappedFlags}");
        }
        
        return issues;
    }
    
    /// <summary>
    /// Converts UnifiedPermissionContext back to legacy PermissionType for backward compatibility
    /// </summary>
    public PermissionType ConvertToLegacy(UnifiedPermissionContext context)
    {
        var legacyPermission = PermissionType.None;
        
        // Map core permissions back
        if (context.CorePermissions.HasFlag(CorePermissionType.Read))
            legacyPermission |= PermissionType.Read;
        if (context.CorePermissions.HasFlag(CorePermissionType.Create))
            legacyPermission |= PermissionType.Create;
        if (context.CorePermissions.HasFlag(CorePermissionType.Update))
            legacyPermission |= PermissionType.Update;
        if (context.CorePermissions.HasFlag(CorePermissionType.Delete))
            legacyPermission |= PermissionType.Delete;
        
        return legacyPermission;
    }
    
    /// <summary>
    /// Provides a migration report showing what permissions would be mapped
    /// </summary>
    public MigrationReport GenerateMigrationReport(PermissionType legacyPermission)
    {
        var report = new MigrationReport
        {
            OriginalPermission = legacyPermission,
            MigratedContext = MigrateFromLegacy(legacyPermission),
            ValidationIssues = ValidateMigration(legacyPermission)
        };
        
        return report;
    }
#pragma warning restore CS0618 // Type or member is obsolete
}

/// <summary>
/// Report showing the results of a permission migration
/// </summary>
public class MigrationReport
{
#pragma warning disable CS0618 // Type or member is obsolete
    public PermissionType OriginalPermission { get; set; }
#pragma warning restore CS0618 // Type or member is obsolete
    public UnifiedPermissionContext MigratedContext { get; set; } = new();
    public IEnumerable<string> ValidationIssues { get; set; } = new List<string>();
    
    public bool HasIssues => ValidationIssues.Any();
}