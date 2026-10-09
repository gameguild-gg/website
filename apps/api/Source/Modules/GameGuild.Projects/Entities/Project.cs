using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using GameGuild.Identity.Users;

namespace GameGuild.Projects;

/// <summary> Represents a project (game, tool, art, etc.) Enhanced with improved validation, indexing, and relationships </summary>
[Table("Projects")]
[Microsoft.EntityFrameworkCore.Index(nameof(Title))]
[Microsoft.EntityFrameworkCore.Index(nameof(Status))]
[Microsoft.EntityFrameworkCore.Index(nameof(Visibility))]
[Microsoft.EntityFrameworkCore.Index(nameof(CreatedById))]
[Microsoft.EntityFrameworkCore.Index(nameof(CategoryId))]
[Microsoft.EntityFrameworkCore.Index(nameof(CreatedAt))]
[Microsoft.EntityFrameworkCore.Index(nameof(UpdatedAt))]
public sealed class Project : EntityBase {
    /// <summary> Project title </summary>
    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    /// <summary> URL-friendly slug for the project </summary>
    [Required]
    [MaxLength(500)]
    public string Slug { get; set; } = string.Empty;

    /// <summary> Short description (max 500 chars) </summary>
    [MaxLength(500)]
    public string? ShortDescription { get; set; }

    /// <summary> Full description or content body (HTML or Markdown) </summary>
    [Column(TypeName = "text")]
    public string? Description { get; set; }

    /// <summary> Project image/logo URL </summary>
    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    /// <summary> Project type (Game, Tool, Art, etc.) </summary>
    public ProjectType Type { get; set; } = ProjectType.Game;

    /// <summary> Development status </summary>
    public DevelopmentStatus DevelopmentStatus { get; set; } = DevelopmentStatus.Planning;

    /// <summary> Current status of the project </summary>
    [Required]
    public ContentStatus Status { get; set; } = ContentStatus.Draft;

    /// <summary> Visibility/access level for the project </summary>
    [Required]
    public ContentVisibility Visibility { get; set; } = ContentVisibility.Private;

    /// <summary> Project category (entity) </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProjectCategory? Category { get; set; }

    public Guid? CategoryId { get; set; }

    /// <summary> Website URL </summary>
    [MaxLength(500)]
    public string? WebsiteUrl { get; set; }

    /// <summary> Repository URL </summary>
    [MaxLength(500)]
    public string? RepositoryUrl { get; set; }

    /// <summary> Social links (JSON string) </summary>
    public string? SocialLinks { get; set; }

    /// <summary> Download URL or platform links </summary>
    [MaxLength(500)]
    public string? DownloadUrl { get; set; }

    /// <summary> Project tags (JSON array) </summary>
    public string? Tags { get; set; }

    /// <summary> Featured image or thumbnail URL </summary>
    [MaxLength(1000)]
    public string? FeaturedImageUrl { get; set; }

    /// <summary> License type for the project </summary>
    [MaxLength(200)]
    public string? License { get; set; }

    /// <summary> Copyright information </summary>
    [MaxLength(500)]
    public string? Copyright { get; set; }

    /// <summary> When the project was published </summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary> Project metadata and statistics </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProjectMetadata? ProjectMetadata { get; set; }

    /// <summary> Navigation property to project versions </summary>
    public ICollection<ProjectVersion> Versions { get; set; } = new List<ProjectVersion>();

    /// <summary> Navigation property to project collaborators </summary>
    public ICollection<ProjectCollaborator> Collaborators { get; set; } = new List<ProjectCollaborator>();

    /// <summary> Navigation property to project releases </summary>
    public ICollection<ProjectRelease> Releases { get; set; } = new List<ProjectRelease>();

    /// <summary> Navigation property to project teams </summary>
    public ICollection<ProjectTeam> Teams { get; set; } = new List<ProjectTeam>();

    public ICollection<ProjectMemberAllocation> Allocations { get; set; } = new List<ProjectMemberAllocation>();

    public ICollection<ProjectTeamAgreement> TeamAgreements { get; set; } = new List<ProjectTeamAgreement>();

    /// <summary> Navigation property to project followers </summary>
    public ICollection<ProjectFollower> Followers { get; set; } = new List<ProjectFollower>();

    /// <summary> Navigation property to project feedback/reviews </summary>
    public ICollection<ProjectFeedback> Feedbacks { get; set; } = new List<ProjectFeedback>();

    /// <summary> Navigation property to jam submissions </summary>
    public ICollection<ProjectJamSubmission> JamSubmissions { get; set; } = new List<ProjectJamSubmission>();

    /// <summary> User who created the project </summary>
    [JsonIgnore]
    public User? CreatedBy { get; set; }

    public Guid? CreatedById { get; set; }

    /// <summary> Computed property: Is the project active </summary>
    [NotMapped]
    public bool IsActive => Status == ContentStatus.Published && !IsDeleted;

    /// <summary> Computed property: Latest version </summary>
    [NotMapped]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProjectVersion? LatestVersion => Versions.OrderByDescending(v => v.CreatedAt).FirstOrDefault();

    /// <summary> Computed property: Number of followers </summary>
    [NotMapped]
    public int FollowerCount => Followers.Count;

    /// <summary> Computed property: Average rating from feedback </summary>
    [NotMapped]
    public decimal? AverageRating => Feedbacks.Count != 0 ? (decimal?)Feedbacks.Where(f => f.Status == ContentStatus.Published).Average(f => f.Rating) : null;

    /// <summary> Computed property: Total feedback count </summary>
    [NotMapped]
    public int FeedbackCount => Feedbacks.Count(f => f.Status == ContentStatus.Published);

    /// <summary> Computed property: Whether the project is part of active jams </summary>
    [NotMapped]
    public bool IsInJam => JamSubmissions.Count != 0;

    /// <summary> Computed property: Number of teams working on this project </summary>
    [NotMapped]
    public int TeamCount => Teams.Count(team => team.IsActive);

    public ProjectTeam SetOwnerTeam(Guid teamId)
    {
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("An owner team is required.", nameof(teamId));
        }

        foreach (var owner in Teams.Where(team => team.IsActive && team.Role == ProjectTeamRole.Owner && team.TeamId != teamId))
        {
            owner.Role = ProjectTeamRole.CoOwner;
        }

        var projectTeam = Teams.SingleOrDefault(team => team.TeamId == teamId && team.DeletedAt == null);
        if (projectTeam == null)
        {
            projectTeam = new ProjectTeam
            {
                TenantId = TenantId,
                ProjectId = Id,
                TeamId = teamId
            };
            Teams.Add(projectTeam);
        }

        projectTeam.Role = ProjectTeamRole.Owner;
        projectTeam.ParticipationMode = ProjectTeamParticipationMode.AllMembers;
        projectTeam.IsActive = true;
        projectTeam.EndedAt = null;
        Touch();
        return projectTeam;
    }

    public ProjectTeam AddParticipatingTeam(Guid teamId, ProjectTeamRole role)
    {
        if (role == ProjectTeamRole.Owner)
        {
            throw new ArgumentException("Use SetOwnerTeam to transfer ownership.", nameof(role));
        }

        if (Teams.Any(team => team.TeamId == teamId && team.IsActive && team.DeletedAt == null))
        {
            throw new InvalidOperationException("This team already participates in the project.");
        }

        var projectTeam = new ProjectTeam
        {
            TenantId = TenantId,
            ProjectId = Id,
            TeamId = teamId,
            Role = role,
            ParticipationMode = ProjectTeamParticipationMode.SelectedMembers
        };
        Teams.Add(projectTeam);
        Touch();
        return projectTeam;
    }

    public ProjectMemberAllocation AddAllocation(
        Guid projectTeamId,
        Guid userId,
        string function,
        decimal capacityPercentage,
        DateTime? startsAt = null,
        DateTime? endsAt = null)
    {
        if (capacityPercentage is <= 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(capacityPercentage));
        }

        var projectTeam = Teams.SingleOrDefault(team => team.Id == projectTeamId && team.IsActive && team.DeletedAt == null)
            ?? throw new InvalidOperationException("The project team is not active.");
        var start = startsAt ?? SystemClock.UtcNow;
        if (endsAt.HasValue && endsAt <= start)
        {
            throw new ArgumentException("Allocation end must be after its start.", nameof(endsAt));
        }

        var allocation = new ProjectMemberAllocation
        {
            TenantId = TenantId,
            ProjectId = Id,
            ProjectTeamId = projectTeam.Id,
            UserId = userId,
            Function = function.Trim(),
            CapacityPercentage = capacityPercentage,
            StartsAt = start,
            EndsAt = endsAt
        };
        projectTeam.Allocations.Add(allocation);
        Allocations.Add(allocation);
        Touch();
        return allocation;
    }

    /// <summary> Generate URL-friendly slug from title </summary>
    public static string GenerateSlug(string title) {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        return title.ToLowerInvariant()
                    .Replace(" ", "-")
                    .Replace("_", "-")
                    .Replace(".", "-")
                    .Replace("(", "")
                    .Replace(")", "")
                    .Replace("[", "")
                    .Replace("]", "")
                    .Replace("{", "")
                    .Replace("}", "")
                    .Replace(",", "")
                    .Replace(";", "")
                    .Replace(":", "")
                    .Replace("'", "")
                    .Replace("\"", "")
                    .Replace("!", "")
                    .Replace("?", "")
                    .Replace("@", "")
                    .Replace("#", "")
                    .Replace("$", "")
                    .Replace("%", "")
                    .Replace("^", "")
                    .Replace("&", "")
                    .Replace("*", "")
                    .Replace("+", "")
                    .Replace("=", "")
                    .Replace("|", "")
                    .Replace("\\", "")
                    .Replace("/", "")
                    .Replace("<", "")
                    .Replace(">", "");
    }
}
