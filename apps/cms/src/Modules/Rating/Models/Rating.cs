using System.ComponentModel.DataAnnotations;
using cms.Common.Entities;

namespace cms.Modules.Rating.Models;

/// <summary>
/// Represents a rating (e.g., 1-5 stars) on a rateable entity.
/// </summary>
public class Rating : BaseEntity
{
    /// <summary>
    /// The rating value (e.g., 1-5 stars)
    /// </summary>
    [Range(1, 5)]
    public int Value
    {
        get;
        set;
    }

    /// <summary>
    /// Navigation property to the user who provided this rating
    /// Entity Framework will automatically create the UserId foreign key
    /// </summary>
    [Required]
    public virtual cms.Modules.User.Models.User User
    {
        get;
        set;
    } = null!;

    /// <summary>
    /// Foreign key for the entity being rated
    /// </summary>
    public Guid EntityId { get; set; }

    /// <summary>
    /// The type of entity being rated (for polymorphic relationships)
    /// </summary>
    [MaxLength(255)]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Optional comment/review text associated with the rating
    /// </summary>
    [MaxLength(1000)]
    public string? Comment { get; set; }
}
