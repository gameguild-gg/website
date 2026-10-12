using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Users;

/// <summary>
///     User profile entity for storing profile information, avatar, banner, and social links
/// </summary>
[Table("UserProfiles")]
[Index(nameof(UserId), IsUnique = true)]
public class UserProfile : EntityBase
{
    /// <summary>
    ///     Default constructor
    /// </summary>
    public UserProfile() { }

    /// <summary>
    ///     Constructor for partial initialization
    /// </summary>
    /// <param name="partial">Partial user profile data</param>
    public UserProfile(object partial) : base(partial) { }

    /// <summary>
    ///     ID of the user this profile belongs to
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    ///     Navigation property to the user
    /// </summary>
    public virtual User User { get; set; } = null!;

    /// <summary>
    ///     Display name for the profile (can be different from User.Name)
    /// </summary>
    [MaxLength(100)]
    public string? DisplayName { get; set; }

    /// <summary>
    ///     User biography or description
    /// </summary>
    [MaxLength(1000)]
    public string? Bio { get; set; }

    /// <summary>
    ///     User's location
    /// </summary>
    [MaxLength(100)]
    public string? Location { get; set; }

    /// <summary>
    ///     User's website URL
    /// </summary>
    [MaxLength(255)]
    public string? Website { get; set; }

    /// <summary>
    ///     User's job title or profession
    /// </summary>
    [MaxLength(100)]
    public string? JobTitle { get; set; }

    /// <summary>
    ///     User's company or organization
    /// </summary>
    [MaxLength(100)]
    public string? Company { get; set; }

    /// <summary>
    ///     Avatar image URL or file path
    /// </summary>
    [MaxLength(500)]
    public string? AvatarUrl { get; set; }

    /// <summary>
    ///     Banner/cover image URL or file path
    /// </summary>
    [MaxLength(500)]
    public string? BannerUrl { get; set; }

    /// <summary>
    ///     Date of birth
    /// </summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>
    ///     User's gender
    /// </summary>
    [MaxLength(20)]
    public string? Gender { get; set; }

    /// <summary>
    ///     Profile visibility level
    /// </summary>
    public ProfileVisibility Visibility { get; set; } = ProfileVisibility.Public;

    /// <summary>
    ///     Whether the profile is verified
    /// </summary>
    public bool IsVerified { get; set; }

    /// <summary>
    ///     Update basic profile information
    /// </summary>
    public void UpdateBasicInfo(string? displayName = null, string? bio = null, string? location = null, string? website = null)
    {
        if (displayName != null)
        {
            DisplayName = displayName;
        }

        if (bio != null)
        {
            Bio = bio;
        }

        if (location != null)
        {
            Location = location;
        }

        if (website != null)
        {
            Website = website;
        }

        Touch();
    }

    /// <summary>
    ///     Update professional information
    /// </summary>
    public void UpdateProfessionalInfo(string? jobTitle = null, string? company = null)
    {
        if (jobTitle != null)
        {
            JobTitle = jobTitle;
        }

        if (company != null)
        {
            Company = company;
        }

        Touch();
    }

    /// <summary>
    ///     Update avatar URL
    /// </summary>
    public void UpdateAvatar(string? avatarUrl)
    {
        AvatarUrl = avatarUrl;
        Touch();
    }

    /// <summary>
    ///     Update banner URL
    /// </summary>
    public void UpdateBanner(string? bannerUrl)
    {
        BannerUrl = bannerUrl;
        Touch();
    }

    /// <summary>
    ///     Update profile visibility
    /// </summary>
    public void UpdateVisibility(ProfileVisibility visibility)
    {
        Visibility = visibility;
        Touch();
    }

    /// <summary>
    ///     Set profile verification status
    /// </summary>
    public void SetVerificationStatus(bool isVerified)
    {
        IsVerified = isVerified;
        Touch();
    }

    /// <summary>
    ///     Factory method to create user profile
    /// </summary>
    public static UserProfile Create(Guid userId, string? displayName = null) { return new UserProfile { UserId = userId, DisplayName = displayName }; }
}

/// <summary>
///     Profile visibility options
/// </summary>
public enum ProfileVisibility { Private = 0, FriendsOnly = 1, Public = 2 }
