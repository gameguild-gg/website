using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Content.Pages;

public static class CourseProposalStatuses
{
    public const string Submitted = "submitted";
    public const string InReview = "in-review";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Published = "published";
    public const string Archived = "archived";

    private static readonly HashSet<string> ValidValues =
    [Submitted, InReview, Approved, Rejected, Published, Archived];

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && ValidValues.Contains(value.Trim().ToLowerInvariant());
}

/// <summary>
///     A proposed course submitted through the public creator pipeline (issue #105).
///     Public visitors submit proposals; the detail page at <c>/proposals/[slug]</c>
///     renders <see cref="CourseProposalStatuses.Published" /> proposals only.
/// </summary>
[Table("course_proposals")]
[Index(nameof(Slug))]
[Index(nameof(Status), nameof(CreatedAt))]
public class CourseProposal : EntityBase
{
    /// <summary>
    ///     URL-safe identifier for the public detail page. Generated from the course
    ///     title plus a short random suffix so concurrent submissions cannot collide.
    /// </summary>
    [Required]
    [MaxLength(160)]
    public string Slug { get; set; } = string.Empty;

    [Required]
    [MaxLength(40)]
    public string Status { get; set; } = CourseProposalStatuses.Submitted;

    [Required]
    [MaxLength(200)]
    public string CourseTitle { get; set; } = string.Empty;

    /// <summary>What the course covers and why it matters.</summary>
    [MaxLength(4000)]
    public string? Pitch { get; set; }

    /// <summary>Who the course is for and what they will be able to do after it.</summary>
    [MaxLength(2000)]
    public string? Audience { get; set; }

    [MaxLength(120)]
    public string? ProposerName { get; set; }

    [MaxLength(200)]
    public string? ProposerEmail { get; set; }

    /// <summary>Free-form JSON metadata (topics, links, estimates). Never secrets.</summary>
    [MaxLength(4000)]
    public string? Metadata { get; set; }

    /// <summary>UTC moment the proposal became publicly visible; null while unpublished.</summary>
    public DateTime? PublishedAt { get; set; }
}
