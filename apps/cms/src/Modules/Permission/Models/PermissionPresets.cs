// namespace cms.Common.Entities;
//
// /// <summary>
// /// Helper class for common permission combinations using the existing PermissionType enum
// ///
// /// DEPRECATED: This class uses the old mixed permission system.
// /// For new implementations, use:
// /// - CorePermissionPresets for CRUD operations
// /// - BusinessLogicPermissionPresets for business logic
// /// - ReputationPermissionMapping for reputation-based permissions
// ///
// /// This class is maintained for backward compatibility but will be phased out.
// /// </summary>
// [Obsolete("Use CorePermissionPresets and BusinessLogicPermissionPresets instead. This class will be removed in a future version.")]
// public static class PermissionPresets
// {
//     // TODO: Migrate to new permission system:
//     // 1. Replace CRUD combinations with CorePermissionPresets
//     // 2. Replace business logic combinations with BusinessLogicPermissionPresets
//     // 3. Implement reputation-based business logic permissions using ReputationSystem
//     // 4. Remove this deprecated class
//     /// <summary>
//     /// Full administrative permissions - can do everything
//     /// </summary>
//     public static readonly PermissionType Admin =
//         PermissionType.Read | PermissionType.Create | PermissionType.Update | PermissionType.Delete |
//         PermissionType.Moderate | PermissionType.Share | PermissionType.Archive | PermissionType.Publish;
//
//     /// <summary>
//     /// Editor permissions - can create, read, update and publish content
//     /// </summary>
//     public static readonly PermissionType Editor =
//         PermissionType.Read | PermissionType.Create | PermissionType.Update |
//         PermissionType.Comment | PermissionType.Publish | PermissionType.Share;
//
//     /// <summary>
//     /// Moderator permissions - can moderate, delete and archive content
//     /// </summary>
//     public static readonly PermissionType Moderator =
//         PermissionType.Read | PermissionType.Moderate | PermissionType.Delete |
//         PermissionType.Archive | PermissionType.Comment;
//
//     /// <summary>
//     /// Author permissions - can create and update their own content
//     /// </summary>
//     public static readonly PermissionType Author =
//         PermissionType.Read | PermissionType.Create | PermissionType.Update |
//         PermissionType.Comment | PermissionType.Share;
//
//     /// <summary>
//     /// Viewer permissions - basic read access with social features
//     /// </summary>
//     public static readonly PermissionType Viewer =
//         PermissionType.Read | PermissionType.Comment | PermissionType.Vote;
//
//     /// <summary>
//     /// All possible permissions - equivalent to super admin
//     /// </summary>
//     public static readonly PermissionType All =
//         (PermissionType)1023; // Binary: 1111111111 (all 10 permissions)
//
//     /// <summary>
//     /// No permissions - used for denied access
//     /// </summary>
//     public static readonly PermissionType None = 0;
// }



