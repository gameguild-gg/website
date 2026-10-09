using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Resources.Contents;

/// <summary>
/// Represents a version of content for revision history.
/// This is a polymorphic versioning system that can track versions of any entity.
/// </summary>
[Table("content_versions")]
[Index(nameof(EntityId), nameof(EntityType))]
[Index(nameof(EntityId), nameof(EntityType), nameof(VersionNumber))]
[Index(nameof(Status))]
[Index(nameof(CreatedAt))]
[Index(nameof(ScheduledPublishAt))]
public class ContentVersion : EntityBase
{
    /// <summary>The ID of the entity this version belongs to</summary>
    public Guid EntityId { get; private set; }

    /// <summary>The type of entity (e.g., "Course", "Project", "Document")</summary>
    [Required]
    [MaxLength(100)]
    public string EntityType { get; private set; } = string.Empty;

    /// <summary>Version number (1, 2, 3...)</summary>
    public int VersionNumber { get; private set; }

    /// <summary>Title at this version</summary>
    [Required]
    [MaxLength(500)]
    public string Title { get; private set; } = string.Empty;

    /// <summary>Summary/description at this version</summary>
    [MaxLength(2000)]
    public string? Summary { get; private set; }

    /// <summary>Main content body at this version (stored as text/JSON)</summary>
    [Column(TypeName = "text")]
    public string? Body { get; private set; }

    /// <summary>Content metadata snapshot as JSON</summary>
    [Column(TypeName = "jsonb")]
    public string? Metadata { get; private set; }

    /// <summary>Current status of this version</summary>
    public ContentVersionStatus Status { get; private set; } = ContentVersionStatus.Draft;

    /// <summary>Who created this version</summary>
    public Guid CreatedBy { get; private set; }

    /// <summary>Change notes describing what changed</summary>
    [MaxLength(1000)]
    public string? ChangeNotes { get; private set; }

    /// <summary>When submitted for review</summary>
    public DateTime? SubmittedForReviewAt { get; private set; }

    /// <summary>Who submitted for review</summary>
    public Guid? SubmittedBy { get; private set; }

    /// <summary>Who approved/rejected this version</summary>
    public Guid? ReviewedBy { get; private set; }

    /// <summary>When reviewed</summary>
    public DateTime? ReviewedAt { get; private set; }

    /// <summary>Review notes from the reviewer</summary>
    [MaxLength(2000)]
    public string? ReviewNotes { get; private set; }

    /// <summary>When this version was published</summary>
    public DateTime? PublishedAt { get; private set; }

    /// <summary>Who published this version</summary>
    public Guid? PublishedBy { get; private set; }

    /// <summary>Scheduled publish date (for scheduled publishing)</summary>
    public DateTime? ScheduledPublishAt { get; private set; }

    /// <summary>Whether this is the currently active/published version</summary>
    public bool IsCurrentVersion { get; private set; }

    private ContentVersion() { } // EF Core

    public static ContentVersion Create(
        Guid entityId,
        string entityType,
        int versionNumber,
        string title,
        Guid createdBy,
        string? summary = null,
        string? body = null,
        string? metadata = null,
        string? changeNotes = null)
    {
        return new ContentVersion
        {
            Id = Guid.NewGuid(),
            EntityId = entityId,
            EntityType = entityType.Trim(),
            VersionNumber = versionNumber,
            Title = title.Trim(),
            Summary = summary?.Trim(),
            Body = body,
            Metadata = metadata,
            CreatedBy = createdBy,
            ChangeNotes = changeNotes?.Trim(),
            Status = ContentVersionStatus.Draft,
            IsCurrentVersion = false
        };
    }

    public void UpdateDraft(string? title = null, string? summary = null, string? body = null, string? metadata = null, string? changeNotes = null)
    {
        if (Status != ContentVersionStatus.Draft)
        {
            throw new InvalidOperationException("Can only update draft versions");
        }

        if (title != null)
        {
            Title = title.Trim();
        }

        if (summary != null)
        {
            Summary = summary.Trim();
        }

        if (body != null)
        {
            Body = body;
        }

        if (metadata != null)
        {
            Metadata = metadata;
        }

        if (changeNotes != null)
        {
            ChangeNotes = changeNotes.Trim();
        }

        UpdatedAt = SystemClock.UtcNow;
    }

    public void SubmitForReview(Guid submittedBy)
    {
        if (Status != ContentVersionStatus.Draft)
        {
            throw new InvalidOperationException("Can only submit drafts for review");
        }

        Status = ContentVersionStatus.PendingReview;
        SubmittedBy = submittedBy;
        SubmittedForReviewAt = SystemClock.UtcNow;
        UpdatedAt = SystemClock.UtcNow;
    }

    public void Approve(Guid reviewedBy, string? reviewNotes = null)
    {
        if (Status != ContentVersionStatus.PendingReview)
        {
            throw new InvalidOperationException("Can only approve versions pending review");
        }

        Status = ContentVersionStatus.Approved;
        ReviewedBy = reviewedBy;
        ReviewedAt = SystemClock.UtcNow;
        ReviewNotes = reviewNotes?.Trim();
        UpdatedAt = SystemClock.UtcNow;
    }

    public void Reject(Guid reviewedBy, string? reviewNotes = null)
    {
        if (Status != ContentVersionStatus.PendingReview)
        {
            throw new InvalidOperationException("Can only reject versions pending review");
        }

        Status = ContentVersionStatus.Rejected;
        ReviewedBy = reviewedBy;
        ReviewedAt = SystemClock.UtcNow;
        ReviewNotes = reviewNotes?.Trim();
        UpdatedAt = SystemClock.UtcNow;
    }

    public void Publish(Guid publishedBy)
    {
        if (Status != ContentVersionStatus.Approved && Status != ContentVersionStatus.Scheduled)
        {
            throw new InvalidOperationException("Can only publish approved or scheduled versions");
        }

        Status = ContentVersionStatus.Published;
        PublishedBy = publishedBy;
        PublishedAt = SystemClock.UtcNow;
        IsCurrentVersion = true;
        UpdatedAt = SystemClock.UtcNow;
    }

    public void SchedulePublish(DateTime scheduledAt, Guid scheduledBy)
    {
        if (Status != ContentVersionStatus.Approved)
        {
            throw new InvalidOperationException("Can only schedule approved versions");
        }

        Status = ContentVersionStatus.Scheduled;
        ScheduledPublishAt = scheduledAt;
        PublishedBy = scheduledBy;
        UpdatedAt = SystemClock.UtcNow;
    }

    public void Archive()
    {
        Status = ContentVersionStatus.Archived;
        IsCurrentVersion = false;
        UpdatedAt = SystemClock.UtcNow;
    }

    public void SetAsCurrent(bool isCurrent)
    {
        IsCurrentVersion = isCurrent;
        UpdatedAt = SystemClock.UtcNow;
    }
}

/// <summary>
/// Status of a content version
/// </summary>
public enum ContentVersionStatus
{
    /// <summary>Work in progress, not yet submitted</summary>
    Draft = 0,

    /// <summary>Submitted and awaiting review</summary>
    PendingReview = 1,

    /// <summary>Approved by reviewer, ready to publish</summary>
    Approved = 2,

    /// <summary>Rejected by reviewer, needs changes</summary>
    Rejected = 3,

    /// <summary>Scheduled for future publishing</summary>
    Scheduled = 4,

    /// <summary>Currently live/published</summary>
    Published = 5,

    /// <summary>Previously published, now archived</summary>
    Archived = 6
}

/// <summary>
/// Interface for entities that support versioning
/// </summary>
public interface IVersionable
{
    Guid Id { get; }
    string GetVersionableEntityType();
    int CurrentVersion { get; }
}

/// <summary>
/// Represents a review comment on a content version
/// </summary>
[Table("content_version_reviews")]
[Index(nameof(ContentVersionId))]
[Index(nameof(ReviewerId))]
public class ContentVersionReview : EntityBase
{
    public Guid ContentVersionId { get; private set; }
    public Guid ReviewerId { get; private set; }
    public ContentReviewDecision Decision { get; private set; }
    
    [MaxLength(2000)]
    public string? Feedback { get; private set; }
    
    /// <summary>Specific line-by-line or section suggestions as JSON</summary>
    [Column(TypeName = "jsonb")]
    public string? Suggestions { get; private set; }

    private ContentVersionReview() { } // EF Core

    public static ContentVersionReview Create(
        Guid contentVersionId,
        Guid reviewerId,
        ContentReviewDecision decision,
        string? feedback = null,
        string? suggestions = null)
    {
        return new ContentVersionReview
        {
            Id = Guid.NewGuid(),
            ContentVersionId = contentVersionId,
            ReviewerId = reviewerId,
            Decision = decision,
            Feedback = feedback?.Trim(),
            Suggestions = suggestions
        };
    }
}

/// <summary>
/// Review decision for content
/// </summary>
public enum ContentReviewDecision
{
    /// <summary>Review pending, no decision yet</summary>
    Pending = 0,

    /// <summary>Content is approved</summary>
    Approve = 1,

    /// <summary>Content needs minor changes</summary>
    RequestChanges = 2,

    /// <summary>Content is rejected</summary>
    Reject = 3
}

/// <summary>
/// Represents a diff between two content versions
/// </summary>
public record ContentVersionDiff(
    Guid Version1Id,
    Guid Version2Id,
    int Version1Number,
    int Version2Number,
    bool TitleChanged,
    bool SummaryChanged,
    bool BodyChanged,
    bool MetadataChanged,
    string? TitleDiff,
    string? SummaryDiff,
    string? BodyDiff
);
