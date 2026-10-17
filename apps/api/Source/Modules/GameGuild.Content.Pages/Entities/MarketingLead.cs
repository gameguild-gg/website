using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Content.Pages;

public static class MarketingLeadSources
{
    public const string Contact = "contact";
    public const string Newsletter = "newsletter";
    public const string CourseProposal = "course-proposal";

    private static readonly HashSet<string> ValidValues =
    [Contact, Newsletter, CourseProposal];

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && ValidValues.Contains(value.Trim().ToLowerInvariant());
}

public static class MarketingLeadStatuses
{
    public const string New = "new";
    public const string Reviewed = "reviewed";
    public const string Archived = "archived";

    private static readonly HashSet<string> ValidValues =
    [New, Reviewed, Archived];

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && ValidValues.Contains(value.Trim().ToLowerInvariant());
}

public static class MarketingLeadTopics
{
    public const string Sales = "sales";
    public const string Support = "support";
    public const string Partnership = "partnership";
    public const string Other = "other";

    private static readonly HashSet<string> ValidValues =
    [Sales, Support, Partnership, Other];

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && ValidValues.Contains(value.Trim().ToLowerInvariant());
}

/// <summary>
///     Captures inbound marketing/contact requests from the public website.
/// </summary>
[Table("marketing_leads")]
[Index(nameof(Email))]
[Index(nameof(Source), nameof(CreatedAt))]
[Index(nameof(Status), nameof(CreatedAt))]
public class MarketingLead : EntityBase
{
    [Required]
    [MaxLength(40)]
    public string Source { get; set; } = MarketingLeadSources.Contact;

    [Required]
    [MaxLength(40)]
    public string Status { get; set; } = MarketingLeadStatuses.New;

    [MaxLength(120)]
    public string? Name { get; set; }

    [Required]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Company { get; set; }

    [MaxLength(40)]
    public string? Topic { get; set; }

    [MaxLength(60)]
    public string? Plan { get; set; }

    [MaxLength(4000)]
    public string? Message { get; set; }

    [MaxLength(10)]
    public string? Locale { get; set; }

    [MaxLength(300)]
    public string? PagePath { get; set; }

    [MaxLength(2000)]
    public string? Referrer { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }
}