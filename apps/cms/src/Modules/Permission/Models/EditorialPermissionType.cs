using System;

namespace cms.Common.Entities;

/// <summary>
/// Permissions for editorial oversight and content quality
/// </summary>
[Flags]
public enum EditorialPermissionType
{
    None = 0,
    Edit = 1,                // Edit content for quality
    Proofread = 2,           // Proofread for errors
    FactCheck = 4,           // Verify factual accuracy
    StyleGuide = 8,          // Enforce style guidelines
    Plagiarism = 16,         // Check for plagiarism
    SEO = 32,                // Optimize for search engines
    Accessibility = 64,      // Ensure accessibility compliance
    Legal = 128,             // Review for legal compliance
    Brand = 256,             // Ensure brand consistency
    Guidelines = 512,        // Enforce editorial guidelines
    
    // Convenience combinations
    BasicEditorial = Edit | Proofread | StyleGuide,
    QualityControl = FactCheck | Plagiarism | Guidelines,
    ComplianceReview = Legal | Accessibility | Brand,
    All = Edit | Proofread | FactCheck | StyleGuide | Plagiarism | SEO | Accessibility | Legal | Brand | Guidelines
}
