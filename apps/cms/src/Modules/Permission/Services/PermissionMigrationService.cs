using System;
using System.Collections.Generic;
using cms.Common.Entities;

namespace cms.Modules.Permission.Services;

/// <summary>
/// Service to handle permission-related operations
/// </summary>
public class PermissionService
{
    /// <summary>
    /// Creates a default permission context for new users
    /// </summary>
    public UnifiedPermissionContext CreateDefaultContext()
    {
        return new UnifiedPermissionContext
        {
            InteractionPermissions = InteractionPerm.BasicInteraction,
            // Add other default permissions as needed
        };
    }
}