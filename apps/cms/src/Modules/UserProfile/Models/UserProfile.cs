using System.ComponentModel.DataAnnotations;
using GameGuild.Common.Entities;

namespace GameGuild.Modules.UserProfile.Models;

/// <summary>
/// Represents a user profile, which is a resource and can be localized and permissioned.
/// </summary>
public class UserProfile : ResourceBase
{
    [MaxLength(100)]
    public string? GivenName
    {
        get;
        set;
    }

    [MaxLength(100)]
    public string? FamilyName
    {
        get;
        set;
    }

    [MaxLength(100)]
    public string? DisplayName
    {
        get;
        set;
    }
}
