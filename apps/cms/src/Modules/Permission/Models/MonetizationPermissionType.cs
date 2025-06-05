using System;

namespace cms.Common.Entities;

/// <summary>
/// Permissions for monetization and business operations
/// </summary>
[Flags]
public enum MonetizationPermissionType
{
    None = 0,
    Monetize = 1,            // Enable monetization features
    Paywall = 2,             // Add content behind paywall
    Subscription = 4,        // Manage subscription content
    Advertisement = 8,       // Add advertisements
    Sponsorship = 16,        // Mark as sponsored content
    Affiliate = 32,          // Add affiliate links
    Commission = 64,         // Manage commission structures
    License = 128,           // Manage content licensing
    Pricing = 256,           // Set content pricing
    Revenue = 512,           // View revenue analytics
    
    // Convenience combinations
    BasicMonetization = Monetize | Paywall | Advertisement,
    AdvancedMonetization = Subscription | Sponsorship | License,
    BusinessIntelligence = Revenue | Commission | Pricing,
    All = Monetize | Paywall | Subscription | Advertisement | Sponsorship | Affiliate | Commission | License | Pricing | Revenue
}
