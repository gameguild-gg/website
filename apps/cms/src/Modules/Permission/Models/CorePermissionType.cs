using System;

namespace cms.Common.Entities;

/// <summary>
/// Core CRUD permissions for basic resource access control
/// </summary>
[Flags]
public enum CorePermissionType
{
    None = 0,
    Read = 1,
    Create = 2,
    Update = 4,
    Delete = 8,
    
    // Convenience combinations
    ReadWrite = Read | Create | Update,
    All = Read | Create | Update | Delete
}