namespace GameGuild.Resources;

/// <summary>
///     Types of resource usage that can be tracked and limited.
///     Each type represents a quota-controlled resource in the system.
/// </summary>
public enum ResourceUsageType
{
    /// <summary>User accounts per tenant</summary>
    Users = 1,

    /// <summary>Projects created per tenant</summary>
    Projects = 2,

    /// <summary>Storage usage in bytes</summary>
    Storage = 3,

    /// <summary>API calls per period</summary>
    ApiCalls = 4,

    /// <summary>Programs (learning paths) per tenant</summary>
    Programs = 5,

    /// <summary>Courses per tenant</summary>
    Courses = 6,

    /// <summary>Feature flags per tenant</summary>
    FeatureFlags = 7,

    /// <summary>Subscription plans per tenant</summary>
    SubscriptionPlans = 8,

    /// <summary>Products in catalog per tenant</summary>
    Products = 9,

    /// <summary>Testing sessions per tenant</summary>
    TestingSessions = 10,

    /// <summary>Roles per tenant</summary>
    Roles = 11,

    // ========== NEW RESOURCE TYPES (Issue #6, #7 - Expanded quota coverage) ==========

    /// <summary>Tenants created (for platform-level quotas)</summary>
    Tenants = 12,

    /// <summary>Active subscriptions per tenant</summary>
    Subscriptions = 13,

    /// <summary>Service Level Objectives per tenant</summary>
    SLOs = 14,

    /// <summary>Access review campaigns per tenant</summary>
    AccessReviewCampaigns = 15,

    /// <summary>Separation of Duties rules per tenant</summary>
    SoDRules = 16,

    /// <summary>ABAC policies per tenant</summary>
    AbacPolicies = 17,

    /// <summary>Conditional access policies per tenant</summary>
    ConditionalPolicies = 18,

    /// <summary>Crypto wallets per tenant</summary>
    Wallets = 19,

    /// <summary>Payment disputes per tenant</summary>
    Disputes = 20,

    /// <summary>Promotional codes per tenant</summary>
    PromoCodes = 21,

    /// <summary>Orders per tenant (commerce)</summary>
    Orders = 22,

    /// <summary>Audit log entries (for compliance modules)</summary>
    AuditEntries = 23,

    // ========== ASSETS MODULE RESOURCE TYPES (Architecture Doc D.2) ==========

    /// <summary>Total asset files per tenant</summary>
    Assets = 24,

    /// <summary>Total storage consumed by assets in bytes</summary>
    AssetStorage = 25,

    /// <summary>Asset download count per period</summary>
    AssetDownloads = 26,

    /// <summary>Asset transformation operations per period</summary>
    AssetTransformations = 27,

    /// <summary>AI completion requests per tenant</summary>
    AiRequests = 28,

    /// <summary>AI tokens consumed across providers</summary>
    AiTokens = 29,

    /// <summary>Teams created per tenant</summary>
    Teams = 30,

    Properties = 31
}
