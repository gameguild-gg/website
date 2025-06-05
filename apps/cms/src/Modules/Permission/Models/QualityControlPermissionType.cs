using System;

namespace cms.Common.Entities;

/// <summary>
/// Permissions for quality control and content assessment
/// </summary>
[Flags]
public enum QualityControlPermissionType
{
    None = 0,
    Score = 1,               // Assign quality scores
    Rate = 2,                // Rate content quality
    Benchmark = 4,           // Set quality benchmarks
    Metrics = 8,             // Access quality metrics
    Analytics = 16,          // View detailed analytics
    Performance = 32,        // Monitor content performance
    Feedback = 64,           // Collect quality feedback
    Audit = 128,             // Conduct quality audits
    Standards = 256,         // Define quality standards
    Improvement = 512,       // Suggest improvements
    
    // Convenience combinations
    BasicQuality = Score | Rate | Feedback,
    QualityAnalytics = Metrics | Analytics | Performance,
    QualityManagement = Benchmark | Standards | Audit | Improvement,
    All = Score | Rate | Benchmark | Metrics | Analytics | Performance | Feedback | Audit | Standards | Improvement
}