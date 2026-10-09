namespace GameGuild.Identity.Authorization;

/// <summary>
///     Permission layer in the 3-layer DAC (Discretionary Access Control) system
///     Matches the platform authorization model
/// </summary>
public enum PermissionLayer
{
    /// <summary>
    ///     Auto-detect permission layer (try tenant -> content-type -> resource)
    /// </summary>
    Auto = 0,

    /// <summary>
    ///     Tenant-wide permissions - applies to all content types within a tenant
    /// </summary>
    Tenant = 1,

    /// <summary>
    ///     Content-type permissions - applies to all entries of a specific content type
    /// </summary>
    ContentType = 2,

    /// <summary>
    ///     Resource-level permissions - applies to specific content entries
    /// </summary>
    Resource = 3
}

/// <summary>
///     Type of permission operation for audit
/// </summary>
// ReSharper disable once InconsistentNaming - JIT is a standard abbreviation for Just-In-Time
public enum PermissionOperationType
{
    None = 0,
    Grant = 1,
    Revoke = 2,
    Update = 3,
    Delete = 4,
    Delegate = 5,
    // ReSharper disable once InconsistentNaming - JIT is a standard abbreviation for Just-In-Time
    ElevateJIT = 6,
    Review = 7,
    Deny = 8,

    /// <summary>A previously removed permission state was restored (issue #358).</summary>
    Restore = 9,
    /// <summary>Permissions were changed by an external-system synchronization import (issue #358).</summary>
    SyncImport = 10,

    /// <summary>
    ///     A permission evaluation/decision (as opposed to a permission mutation).
    ///     Recorded when an endpoint-level permission attribute is checked, so that
    ///     sensitive permission-gated operations (e.g. monetization) leave a durable
    ///     decision trail (issue #346).
    /// </summary>
    Check = 11
}

/// <summary>
///     ABAC policy effect
/// </summary>
public enum AbacPolicyEffect
{
    None = 0,
    Allow = 1,
    Deny = 2
}

/// <summary>
///     Template change type for versioning
/// </summary>
public enum TemplateChangeType
{
    None = 0,
    Major = 1,    // Breaking changes, incompatible with previous
    Minor = 2,    // New features, backwards compatible
    Patch = 3,    // Bug fixes, no new features
    Hotfix = 4    // Critical security/bug fixes
}

/// <summary>
///     JIT elevation request status
/// </summary>
public enum JitRequestStatus
{
    None = 0,
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Expired = 4,
    Revoked = 5
}

/// <summary>
///     Permission delegation status
/// </summary>
public enum DelegationStatus
{
    None = 0,
    Active = 1,
    Expired = 2,
    Revoked = 3
}

/// <summary>
///     Access review campaign status
/// </summary>
public enum AccessReviewStatus
{
    None = 0,
    Draft = 1,
    Active = 2,
    InProgress = 3,
    Completed = 4,
    Expired = 5
}

/// <summary>
///     SoD rule action to take on violation
/// </summary>
public enum SoDViolationAction
{
    None = 0,
    Warn = 1,
    Block = 2,
    Notify = 3,
    RequireApproval = 4
}

/// <summary>
///     Comprehensive enumeration of permission types in the GameGuild system
///     Represents the various operations that can be controlled through permissions
///     Defines the complete platform permission system
/// </summary>
public enum PermissionType
{
    #region Interaction GameGuild.Permissions

    /// <summary>
    ///     Permission to read/view content
    /// </summary>
    Read = 1,

    /// <summary>
    ///     Permission to comment on content
    /// </summary>
    Comment = 2,

    /// <summary>
    ///     Permission to reply to comments
    /// </summary>
    Reply = 3,

    /// <summary>
    ///     Permission to vote on content
    /// </summary>
    Vote = 4,

    /// <summary>
    ///     Permission to share content
    /// </summary>
    Share = 5,

    /// <summary>
    ///     Permission to report content
    /// </summary>
    Report = 6,

    /// <summary>
    ///     Permission to follow users or content
    /// </summary>
    Follow = 7,

    /// <summary>
    ///     Permission to bookmark content
    /// </summary>
    Bookmark = 8,

    /// <summary>
    ///     Permission to react to content
    /// </summary>
    React = 9,

    /// <summary>
    ///     Permission to subscribe to content or notifications
    /// </summary>
    Subscribe = 10,

    /// <summary>
    ///     Permission to mention users
    /// </summary>
    Mention = 11,

    /// <summary>
    ///     Permission to tag content
    /// </summary>
    Tag = 12,

    #endregion

    #region Curation GameGuild.Permissions

    /// <summary>
    ///     Permission to categorize content
    /// </summary>
    Categorize = 13,

    /// <summary>
    ///     Permission to create and manage collections
    /// </summary>
    Collection = 14,

    /// <summary>
    ///     Permission to create and manage series
    /// </summary>
    Series = 15,

    /// <summary>
    ///     Permission to create cross-references between content
    /// </summary>
    CrossReference = 16,

    /// <summary>
    ///     Permission to translate content
    /// </summary>
    Translate = 17,

    /// <summary>
    ///     Permission to create versions of content
    /// </summary>
    Version = 18,

    /// <summary>
    ///     Permission to create and use templates
    /// </summary>
    Template = 19,

    #endregion

    #region Lifecycle GameGuild.Permissions

    /// <summary>
    ///     Permission to create new content
    /// </summary>
    Create = 20,

    /// <summary>
    ///     Permission to save content as draft
    /// </summary>
    Draft = 21,

    /// <summary>
    ///     Permission to submit content for review/publication
    /// </summary>
    Submit = 22,

    /// <summary>
    ///     Permission to withdraw submitted content
    /// </summary>
    Withdraw = 23,

    /// <summary>
    ///     Permission to archive content
    /// </summary>
    Archive = 24,

    /// <summary>
    ///     Permission to restore archived content
    /// </summary>
    Restore = 25,

    /// <summary>
    ///     Permission to soft delete content (Delete is an alias for SoftDelete)
    /// </summary>
    Delete = 26,

    /// <summary>
    ///     Permission to soft delete content (same as Delete)
    ///     Only the owners of a resource can soft delete it at resource level,
    ///     it still can be deleted by admins at tenant or content type level
    /// </summary>
    SoftDelete = 26,

    /// <summary>
    ///     Permission to permanently delete content
    /// </summary>
    HardDelete = 27,

    /// <summary>
    ///     Permission to backup content
    /// </summary>
    Backup = 28,

    /// <summary>
    ///     Permission to migrate content
    /// </summary>
    Migrate = 29,

    /// <summary>
    ///     Permission to clone/duplicate content
    /// </summary>
    Clone = 30,

    #endregion

    #region Editorial GameGuild.Permissions

    /// <summary>
    ///     Permission to edit content
    /// </summary>
    Edit = 31,

    /// <summary>
    ///     Permission to proofread content
    /// </summary>
    Proofread = 32,

    /// <summary>
    ///     Permission to fact-check content
    /// </summary>
    FactCheck = 33,

    /// <summary>
    ///     Permission to apply style guide rules
    /// </summary>
    StyleGuide = 34,

    /// <summary>
    ///     Permission to check for plagiarism
    /// </summary>
    Plagiarism = 35,

    /// <summary>
    ///     Permission to optimize for SEO
    /// </summary>
    Seo = 36,

    /// <summary>
    ///     Permission to ensure accessibility compliance
    /// </summary>
    Accessibility = 37,

    /// <summary>
    ///     Permission to review for legal compliance
    /// </summary>
    Legal = 38,

    /// <summary>
    ///     Permission to ensure brand compliance
    /// </summary>
    Brand = 39,

    /// <summary>
    ///     Permission to enforce content guidelines
    /// </summary>
    Guidelines = 40,

    #endregion

    #region Approval GameGuild.Permissions

    /// <summary>
    ///     Permission to approve content for publication
    /// </summary>
    Approve = 41,

    /// <summary>
    ///     Permission to reject submitted content
    /// </summary>
    Reject = 42,

    /// <summary>
    ///     Permission to request revisions to content
    /// </summary>
    RequestRevision = 43,

    /// <summary>
    ///     Permission to escalate approval decisions
    /// </summary>
    Escalate = 44,

    /// <summary>
    ///     Permission to override approval decisions
    /// </summary>
    Override = 45,

    /// <summary>
    ///     Permission to delegate approval authority
    /// </summary>
    Delegate = 46,

    /// <summary>
    ///     Permission to fast-track approval process
    /// </summary>
    FastTrack = 47,

    /// <summary>
    ///     Permission to batch approve multiple items
    /// </summary>
    BatchApprove = 48,

    /// <summary>
    ///     Permission to conditionally approve content
    /// </summary>
    ConditionalApprove = 49,

    /// <summary>
    ///     Permission to require additional review
    /// </summary>
    RequireReview = 50,

    #endregion

    #region Publishing GameGuild.Permissions

    /// <summary>
    ///     Permission to publish content
    /// </summary>
    Publish = 51,

    /// <summary>
    ///     Permission to unpublish content
    /// </summary>
    Unpublish = 52,

    /// <summary>
    ///     Permission to schedule publication
    /// </summary>
    Schedule = 53,

    /// <summary>
    ///     Permission to set publication dates
    /// </summary>
    SetPublishDate = 54,

    /// <summary>
    ///     Permission to control content visibility
    /// </summary>
    Visibility = 55,

    /// <summary>
    ///     Permission to feature content prominently
    /// </summary>
    Feature = 56,

    /// <summary>
    ///     Permission to pin content to top
    /// </summary>
    Pin = 57,

    /// <summary>
    ///     Permission to sticky content (keep at top)
    /// </summary>
    Sticky = 58,

    /// <summary>
    ///     Permission to highlight content
    /// </summary>
    Highlight = 59,

    /// <summary>
    ///     Permission to promote content
    /// </summary>
    Promote = 60,

    #endregion

    #region Moderation GameGuild.Permissions

    /// <summary>
    ///     Permission to moderate content
    /// </summary>
    Moderate = 61,

    /// <summary>
    ///     Permission to hide content from public view
    /// </summary>
    Hide = 62,

    /// <summary>
    ///     Permission to flag content for review
    /// </summary>
    Flag = 63,

    /// <summary>
    ///     Permission to warn users about content issues
    /// </summary>
    Warn = 64,

    /// <summary>
    ///     Permission to suspend content temporarily
    /// </summary>
    Suspend = 65,

    /// <summary>
    ///     Permission to ban content permanently
    /// </summary>
    Ban = 66,

    /// <summary>
    ///     Permission to quarantine suspicious content
    /// </summary>
    Quarantine = 67,

    /// <summary>
    ///     Permission to review flagged content
    /// </summary>
    Review = 68,

    /// <summary>
    ///     Permission to investigate content issues
    /// </summary>
    Investigate = 69,

    /// <summary>
    ///     Permission to escalate moderation issues
    /// </summary>
    EscalateModeration = 70,

    #endregion

    #region Collaboration GameGuild.Permissions

    /// <summary>
    ///     Permission to invite collaborators
    /// </summary>
    Invite = 71,

    /// <summary>
    ///     Permission to assign tasks to team members
    /// </summary>
    Assign = 72,

    /// <summary>
    ///     Permission to collaborate on content
    /// </summary>
    Collaborate = 73,

    /// <summary>
    ///     Permission to co-author content
    /// </summary>
    CoAuthor = 74,

    /// <summary>
    ///     Permission to contribute to content
    /// </summary>
    Contribute = 75,

    /// <summary>
    ///     Permission to provide suggestions
    /// </summary>
    Suggest = 76,

    /// <summary>
    ///     Permission to track changes in content
    /// </summary>
    Track = 77,

    /// <summary>
    ///     Permission to merge changes from contributors
    /// </summary>
    Merge = 78,

    /// <summary>
    ///     Permission to resolve conflicts in collaborative editing
    /// </summary>
    Resolve = 79,

    /// <summary>
    ///     Permission to coordinate team activities
    /// </summary>
    Coordinate = 80,

    #endregion

    #region Quality Control GameGuild.Permissions

    /// <summary>
    ///     Permission to score content quality
    /// </summary>
    Score = 82,

    /// <summary>
    ///     Permission to rate content
    /// </summary>
    Rate = 83,

    /// <summary>
    ///     Permission to benchmark content against standards
    /// </summary>
    Benchmark = 84,

    /// <summary>
    ///     Permission to collect metrics on content
    /// </summary>
    Metrics = 85,

    /// <summary>
    ///     Permission to access analytics data
    /// </summary>
    Analytics = 86,

    /// <summary>
    ///     Permission to monitor performance
    /// </summary>
    Performance = 87,

    /// <summary>
    ///     Permission to provide feedback
    /// </summary>
    Feedback = 88,

    /// <summary>
    ///     Permission to audit content and processes
    /// </summary>
    Audit = 89,

    /// <summary>
    ///     Permission to enforce quality standards
    /// </summary>
    Standards = 90,

    /// <summary>
    ///     Permission to implement improvements
    /// </summary>
    Improvement = 91,

    #endregion

    #region Commerce Permissions

    /// <summary>
    ///     Permission to monetize content (enable revenue generation)
    /// </summary>
    Monetize = 92,

    /// <summary>
    ///     Permission to set pricing for content
    /// </summary>
    Pricing = 93,

    /// <summary>
    ///     Permission to add paywall to content
    /// </summary>
    Paywall = 94,

    #endregion

    #region Administrative GameGuild.Permissions

    /// <summary>
    ///     Permission to manage resources and settings
    /// </summary>
    Manage = 100,

    /// <summary>
    ///     Administrative permission for general admin tasks
    /// </summary>
    Admin = 101,

    /// <summary>
    ///     Permission to execute system operations
    /// </summary>
    Execute = 110,

    /// <summary>
    ///     Permission to export data
    /// </summary>
    Export = 111,

    /// <summary>
    ///     Permission to import data
    /// </summary>
    Import = 112,

    /// <summary>
    ///     System administrator with full system access
    /// </summary>
    SystemAdmin = 200,

    /// <summary>
    ///     Tenant administrator with full tenant access
    /// </summary>
    TenantAdmin = 201,

    /// <summary>
    ///     Permission to manage users and user accounts
    /// </summary>
    UserManagement = 202,

    /// <summary>
    ///     Permission to configure system settings and integrations
    /// </summary>
    Configure = 203,

    #endregion
}

/// <summary>
///     Data masking level
/// </summary>
public enum DataMaskingLevel
{
    None = 0,
    Partial = 1,
    Full = 2,
    Redacted = 3
}

/// <summary>
///     Status of JIT elevation request
/// </summary>
public enum ElevationRequestStatus
{
    None = 0,
    Pending = 1,
    Approved = 2,
    Denied = 3,
    Active = 4,
    Expired = 5,
    Revoked = 6
}

/// <summary>
///     Type of access review/certification campaign
/// </summary>
public enum AccessReviewType
{
    None = 0,
    PermissionReview = 1,
    RoleReview = 2,
    ResourceAccessReview = 3,
    UserAccessReview = 4,
    ComplianceAttestation = 5
}

/// <summary>
///     Scope of access review campaign
/// </summary>
public enum AccessReviewScope
{
    None = 0,
    AllUsers = 1,
    Department = 2,
    Team = 3,
    Role = 4,
    Resource = 5,
    HighPrivilege = 6,
    External = 7,
    Custom = 99
}

/// <summary>
///     Status of individual review item
/// </summary>
public enum AccessReviewItemStatus
{
    None = 0,
    Pending = 1,
    Reviewed = 2,
    Approved = 3,
    Revoked = 4,
    Expired = 5
}

/// <summary>
///     Decision made on review item
/// </summary>
public enum AccessReviewDecision
{
    None = 0,
    Approve = 1,
    Revoke = 2,
    ModifyAndApprove = 3
}

/// <summary>
///     Type of SoD rule
/// </summary>
public enum SoDRuleType
{
    None = 0,
    PermissionConflict = 1,
    RoleConflict = 2,
    ResourceConflict = 3,
    BusinessProcessConflict = 4,
    FunctionalConflict = 5
}

/// <summary>
///     Severity of SoD rule
/// </summary>
public enum SoDSeverity
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

/// <summary>
///     Status of SoD violation
/// </summary>
public enum SoDViolationStatus
{
    None = 0,
    Active = 1,
    Acknowledged = 2,
    Mitigated = 3,
    Resolved = 4,
    Excepted = 5,
    FalsePositive = 6
}

/// <summary>
///     Action taken to resolve SoD violation
/// </summary>
public enum SoDResolutionAction
{
    None = 0,
    RevokePermission = 1,
    RevokeRole = 2,
    GrantException = 3,
    ImplementCompensatingControl = 4,
    TransferOwnership = 5,
    NoAction = 6
}

/// <summary>
///     Type of delegated admin scope
/// </summary>
public enum DelegatedAdminScopeType
{
    None = 0,
    Department = 1,
    Team = 2,
    Role = 3,
    Resource = 4,
    Custom = 5
}

/// <summary>
///     Type of policy condition
/// </summary>
public enum PolicyConditionType
{
    None = 0,
    Time = 1,
    Environment = 2,
    Location = 3,
    Device = 4,
    RiskScore = 5,
    Custom = 99
}

/// <summary>
///     Action to take when policy condition matches
/// </summary>
public enum PolicyAction
{
    None = 0,
    Allow = 1,
    Deny = 2,
    Require2Fa = 3,
    RequireApproval = 4,
    Log = 5,
    Throttle = 6
}

/// <summary>
///     Type of data masking to apply
/// </summary>
public enum MaskingType
{
    None = 0,
    Full = 1,
    Partial = 2,
    Hash = 3,
    Custom = 4,
    PatternMask = 5,
    Redact = 6
}

/// <summary>
///     Migration status
/// </summary>
public enum MigrationStatus
{
    None = 0,
    Planned = 1,
    Scheduled = 2,
    InProgress = 3,
    Completed = 4,
    Failed = 5,
    RolledBack = 6,
    Cancelled = 7
}

/// <summary>
///     Migration strategy
/// </summary>
public enum MigrationStrategy
{
    None = 0,
    Immediate = 1,
    Phased = 2,
    Manual = 3,
    Scheduled = 4
}

/// <summary>
///     Policy bundle type
/// </summary>
public enum PolicyBundleType
{
    None = 0,
    Permission = 1,
    Conditional = 2,
    DataMasking = 3,
    SoD = 4,
    AccessReview = 5,
    Composite = 6
}

/// <summary>
///     Policy bundle status
/// </summary>
public enum PolicyBundleStatus
{
    None = 0,
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Active = 4,
    Deprecated = 5,
    Revoked = 6
}

/// <summary>
///     Policy deployment status
/// </summary>
public enum PolicyDeploymentStatus
{
    None = 0,
    Pending = 1,
    Deploying = 2,
    Active = 3,
    Failed = 4,
    RolledBack = 5
}

/// <summary>
///     Policy registry action types
/// </summary>
public enum PolicyRegistryAction
{
    None = 0,
    Create = 1,
    Update = 2,
    Sign = 3,
    Approve = 4,
    Deploy = 5,
    Activate = 6,
    Deprecate = 7,
    Revoke = 8,
    Rollback = 9,
    Verify = 10,
    Export = 11,
    Import = 12
}

/// <summary>
///     Graph export formats
/// </summary>
// ReSharper disable InconsistentNaming - DOT, JSON, and GraphML are standard format names
public enum GraphExportFormat
{
    None = 0,
    DOT = 1,
    JSON = 2,
    GraphML = 3
}
// ReSharper restore InconsistentNaming

/// <summary>
///     Impact severity level
/// </summary>
public enum ImpactSeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}
