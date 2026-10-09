using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Social.Profiles;

public enum ProfileVisibility
{
    Private,
    Connections,
    Public
}

public enum ProfileAvailabilityStatus
{
    NotSet,
    OpenToWork,
    OpenToCollaborate,
    Busy,
    Hidden
}

public enum ProfileSkillProficiency
{
    Beginner,
    Intermediate,
    Advanced,
    Expert
}

[Table("social_profiles")]
[Index(nameof(UserId), IsUnique = true)]
[Index(nameof(Handle), IsUnique = true)]
[Index(nameof(Visibility))]
public class SocialProfile : EntityBase
{
    public Guid UserId { get; set; }

    [MaxLength(80)]
    public string Handle { get; set; } = string.Empty;

    [MaxLength(180)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Bio { get; set; }

    [MaxLength(500)]
    public string? AvatarUrl { get; set; }

    [MaxLength(500)]
    public string? BannerUrl { get; set; }

    [MaxLength(120)]
    public string? Headline { get; set; }

    [MaxLength(120)]
    public string? Location { get; set; }

    [MaxLength(80)]
    public string? TimeZone { get; set; }

    [MaxLength(500)]
    public string? WebsiteUrl { get; set; }

    public string SocialLinksJson { get; set; } = "{}";

    public ProfileVisibility Visibility { get; set; } = ProfileVisibility.Public;

    public ProfileAvailabilityStatus AvailabilityStatus { get; set; } = ProfileAvailabilityStatus.NotSet;

    public bool ShowActivity { get; set; } = true;

    public bool ShowPortfolio { get; set; } = true;

    public bool ShowSkills { get; set; } = true;

    public DateTime? VerifiedAt { get; set; }

    public int CompletenessScore { get; set; }

    public int FollowerCount { get; set; }

    public int FollowingCount { get; set; }

    public int PostCount { get; set; }

    public int ProjectCount { get; set; }

    public ICollection<ProfileSkill> Skills { get; set; } = new List<ProfileSkill>();

    public ICollection<ProfilePortfolioItem> PortfolioItems { get; set; } = new List<ProfilePortfolioItem>();

    public void UpdateProfile(UpdateSocialProfileCommand command)
    {
        Handle = NormalizeHandle(command.Handle);
        DisplayName = command.DisplayName;
        Bio = command.Bio;
        AvatarUrl = command.AvatarUrl;
        BannerUrl = command.BannerUrl;
        Headline = command.Headline;
        Location = command.Location;
        TimeZone = command.TimeZone;
        WebsiteUrl = command.WebsiteUrl;
        SocialLinksJson = command.SocialLinksJson;
        AvailabilityStatus = command.AvailabilityStatus;
        RecalculateCompleteness();
    }

    public void UpdatePrivacy(ProfileVisibility visibility, bool showActivity, bool showPortfolio, bool showSkills)
    {
        Visibility = visibility;
        ShowActivity = showActivity;
        ShowPortfolio = showPortfolio;
        ShowSkills = showSkills;
        Touch();
    }

    public void UpdateStats(int followerCount, int followingCount, int postCount, int projectCount)
    {
        FollowerCount = Math.Max(0, followerCount);
        FollowingCount = Math.Max(0, followingCount);
        PostCount = Math.Max(0, postCount);
        ProjectCount = Math.Max(0, projectCount);
        Touch();
    }

    public int CalculateCompleteness()
    {
        var score = 20;
        if (!string.IsNullOrWhiteSpace(DisplayName))
        {
            score += 15;
        }

        if (!string.IsNullOrWhiteSpace(Handle))
        {
            score += 15;
        }

        if (!string.IsNullOrWhiteSpace(Bio))
        {
            score += 15;
        }

        if (!string.IsNullOrWhiteSpace(AvatarUrl))
        {
            score += 10;
        }

        if (!string.IsNullOrWhiteSpace(Headline))
        {
            score += 10;
        }

        if (!string.IsNullOrWhiteSpace(WebsiteUrl))
        {
            score += 5;
        }

        if (Skills.Count > 0)
        {
            score += 5;
        }

        if (PortfolioItems.Count > 0)
        {
            score += 5;
        }

        return Math.Min(100, score);
    }

    public void RecalculateCompleteness()
    {
        CompletenessScore = CalculateCompleteness();
        Touch();
    }

    public SocialProfileDto ToDto() => new(
        Id,
        UserId,
        Handle,
        DisplayName,
        Bio,
        AvatarUrl,
        BannerUrl,
        Headline,
        Location,
        TimeZone,
        WebsiteUrl,
        SocialLinksJson,
        Visibility,
        AvailabilityStatus,
        ShowActivity,
        ShowPortfolio,
        ShowSkills,
        VerifiedAt,
        CompletenessScore,
        FollowerCount,
        FollowingCount,
        PostCount,
        ProjectCount,
        Skills.OrderBy(skill => skill.DisplayOrder).Select(skill => skill.ToDto()).ToList(),
        PortfolioItems.OrderByDescending(item => item.IsPinned).ThenBy(item => item.DisplayOrder).Select(item => item.ToDto()).ToList());

    public static string NormalizeHandle(string handle) => handle.Trim().TrimStart('@').ToLowerInvariant();
}

[Table("social_profile_skills")]
[Index(nameof(ProfileId))]
[Index(nameof(ProfileId), nameof(Name), IsUnique = true)]
public class ProfileSkill : EntityBase
{
    public Guid ProfileId { get; set; }

    [ForeignKey(nameof(ProfileId))]
    public SocialProfile? Profile { get; set; }

    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    public ProfileSkillProficiency Proficiency { get; set; } = ProfileSkillProficiency.Intermediate;

    public int DisplayOrder { get; set; }

    public void Update(ProfileSkillProficiency proficiency, int displayOrder)
    {
        Proficiency = proficiency;
        DisplayOrder = displayOrder;
        Touch();
    }

    public ProfileSkillDto ToDto() => new(Id, ProfileId, Name, Proficiency, DisplayOrder);
}

[Table("social_profile_portfolio_items")]
[Index(nameof(ProfileId))]
[Index(nameof(ProjectId))]
public class ProfilePortfolioItem : EntityBase
{
    public Guid ProfileId { get; set; }

    [ForeignKey(nameof(ProfileId))]
    public SocialProfile? Profile { get; set; }

    public Guid? ProjectId { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? Url { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public bool IsPinned { get; set; }

    public int DisplayOrder { get; set; }

    public void Update(string title, string? description, string? url, string? imageUrl, bool isPinned, int displayOrder)
    {
        Title = title;
        Description = description;
        Url = url;
        ImageUrl = imageUrl;
        IsPinned = isPinned;
        DisplayOrder = displayOrder;
        Touch();
    }

    public ProfilePortfolioItemDto ToDto() => new(Id, ProfileId, ProjectId, Title, Description, Url, ImageUrl, IsPinned, DisplayOrder);
}

public sealed record SocialProfileDto(
    Guid Id,
    Guid UserId,
    string Handle,
    string DisplayName,
    string? Bio,
    string? AvatarUrl,
    string? BannerUrl,
    string? Headline,
    string? Location,
    string? TimeZone,
    string? WebsiteUrl,
    string SocialLinksJson,
    ProfileVisibility Visibility,
    ProfileAvailabilityStatus AvailabilityStatus,
    bool ShowActivity,
    bool ShowPortfolio,
    bool ShowSkills,
    DateTime? VerifiedAt,
    int CompletenessScore,
    int FollowerCount,
    int FollowingCount,
    int PostCount,
    int ProjectCount,
    List<ProfileSkillDto> Skills,
    List<ProfilePortfolioItemDto> PortfolioItems);

public sealed record ProfileSkillDto(
    Guid Id,
    Guid ProfileId,
    string Name,
    ProfileSkillProficiency Proficiency,
    int DisplayOrder);

public sealed record ProfilePortfolioItemDto(
    Guid Id,
    Guid ProfileId,
    Guid? ProjectId,
    string Title,
    string? Description,
    string? Url,
    string? ImageUrl,
    bool IsPinned,
    int DisplayOrder);
