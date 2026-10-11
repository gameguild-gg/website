using System.ComponentModel;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Product type enumeration
/// </summary>
public enum ProductType
{
    /// <summary>Educational content organized into lessons</summary>
    [Description("Educational content organized into lessons")]
    Program = 0,

    /// <summary>Course product type</summary>
    [Description("Single course")]
    Course = 1,

    /// <summary>Collection of multiple products</summary>
    [Description("Collection of multiple products")]
    Bundle = 2,

    /// <summary>Recurring access to content</summary>
    [Description("Recurring access to content")]
    Subscription = 3,

    /// <summary>Live interactive sessions</summary>
    [Description("Live interactive sessions")]
    Workshop = 4,

    /// <summary>One-on-one coaching</summary>
    [Description("One-on-one coaching")]
    Mentorship = 5,

    /// <summary>Digital publications</summary>
    [Description("Digital publications")]
    Ebook = 6,

    /// <summary>Downloadable assets</summary>
    [Description("Downloadable assets")]
    ResourcePack = 7,

    /// <summary>Access to forums/communities</summary>
    [Description("Access to forums/communities")]
    Community = 8,

    /// <summary>Industry credentials</summary>
    [Description("Industry credentials")]
    Certification = 9,

    /// <summary>Physical product</summary>
    [Description("Physical product")]
    Physical = 10,

    /// <summary>Service or consulting</summary>
    [Description("Service or consulting")]
    Service = 11,

    /// <summary>Curated sequence of programs</summary>
    [Description("Curated sequence of programs")]
    LearningPathway = 12,

    /// <summary>Other product categories</summary>
    [Description("Other")]
    Other = 99
}

/// <summary>
/// Represents how a user acquired access to a product
/// </summary>
public enum ProductAcquisitionType
{
    /// <summary>Direct purchase</summary>
    [Description("Product acquired through direct payment")]
    Purchase = 0,

    /// <summary>Access via subscription</summary>
    [Description("Product access via recurring subscription")]
    Subscription = 1,

    /// <summary>Granted by administrator or instructor</summary>
    [Description("Granted by administrator")]
    Grant = 2,

    /// <summary>Access via promotional code</summary>
    [Description("Access via promotional code")]
    PromoCode = 3,

    /// <summary>Access via bundle purchase</summary>
    [Description("Access via bundle purchase")]
    Bundle = 4,

    /// <summary>Trial or free access</summary>
    [Description("Trial or free access")]
    Trial = 5,

    /// <summary>Access via affiliate/referral</summary>
    [Description("Access via affiliate/referral")]
    Referral = 6,

    /// <summary>Product provided at no cost</summary>
    [Description("Product provided at no cost")]
    Free = 7,

    /// <summary>Product received as a gift</summary>
    [Description("Product received as a gift from another user")]
    Gift = 8
}

/// <summary>
/// Represents the current access status for a user's product
/// </summary>
public enum ProductAccessStatus
{
    /// <summary>Access is active and valid</summary>
    [Description("User has full access to the product")]
    Active = 0,

    /// <summary>Access has expired</summary>
    [Description("Access period has ended")]
    Expired = 1,

    /// <summary>Access has been revoked</summary>
    [Description("Access manually removed by admin")]
    Revoked = 2,

    /// <summary>Access is suspended</summary>
    [Description("Temporary hold on access that may be restored")]
    Suspended = 3,

    /// <summary>Access is pending activation</summary>
    [Description("Access is pending activation")]
    Pending = 4,

    /// <summary>Access was cancelled</summary>
    [Description("Access was cancelled")]
    Cancelled = 5
}

/// <summary>
/// Represents the type of promo code discount
/// </summary>
public enum PromoCodeType
{
    /// <summary>Percentage discount (e.g., 20% off)</summary>
    [Description("Percentage discount")]
    PercentageOff = 0,

    /// <summary>Fixed amount discount (e.g., $10 off)</summary>
    [Description("Fixed amount discount")]
    FixedAmountOff = 1,

    /// <summary>Free trial period</summary>
    [Description("Free trial period")]
    FreeTrial = 2,

    /// <summary>Buy one get one free</summary>
    [Description("Buy one get one free")]
    BuyOneGetOne = 3,

    /// <summary>Free shipping</summary>
    [Description("Free shipping")]
    FreeShipping = 4,

    /// <summary>Extends the trial period by a number of days (FreeTrialExtensionDays)</summary>
    [Description("Free trial extension in days")]
    FreeTrialExtension = 5,

    /// <summary>Grants a number of free billing periods (DurationMonths)</summary>
    [Description("Free months (skipped billing periods)")]
    FreeMonths = 6,

    /// <summary>Overrides the final price to a specific amount (DiscountAmount holds the set price)</summary>
    [Description("Set price override")]
    SetPrice = 7
}
