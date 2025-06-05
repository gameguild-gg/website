using System;

namespace cms.Common.Entities;

/// <summary>
/// Permissions for content moderation activities
/// </summary>
[Flags]
public enum ModerationPermissionType
{
    None = 0,
    Review = 1,              // Review submitted content
    Approve = 2,             // Approve pending content
    Reject = 4,              // Reject content submissions
    Hide = 8,                // Hide content from public view
    Quarantine = 16,         // Restrict content pending review
    Flag = 32,               // Flag content for attention
    Warning = 64,            // Issue warnings to users
    Suspend = 128,           // Suspend user accounts
    Ban = 256,               // Ban users permanently
    Escalate = 512,          // Escalate to higher authorities
    
    // Convenience combinations
    BasicModeration = Review | Approve | Reject | Flag,
    ContentControl = Hide | Quarantine | Warning,
    UserControl = Suspend | Ban | Escalate,
    All = Review | Approve | Reject | Hide | Quarantine | Flag | Warning | Suspend | Ban | Escalate
}
