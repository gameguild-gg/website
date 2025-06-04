using System.ComponentModel.DataAnnotations;
using cms.Common.Entities;

namespace cms.Modules.Voting.Models;

/// <summary>
/// Represents a vote (upvote/downvote) on a voteable entity.
/// </summary>
public class Vote : BaseEntity
{
    /// <summary>
    /// Navigation property to the user who cast this vote
    /// Entity Framework will automatically create the UserId foreign key
    /// </summary>
    [Required]
    public virtual cms.Modules.User.Models.User User
    {
        get;
        set;
    } = null!;

    /// <summary>
    /// Type of the vote (Upvote or Downvote)
    /// </summary>
    [Required]
    public VoteType Type
    {
        get;
        set;
    }

    /// <summary>
    /// Weight of the vote (allows for weighted voting systems)
    /// Default is 1 (standard weight)
    /// </summary>
    [Required]
    public int Weight
    {
        get;
        set;
    } = 1;

    /// <summary>
    /// Calculated value of the vote based on type and weight
    /// Returns positive weight for upvotes and negative weight for downvotes
    /// </summary>
    public int Value
    {
        get => Type == VoteType.Upvote ? Weight : -Weight;
    }

    /// <summary>
    /// Foreign key for the entity being voted on
    /// </summary>
    public Guid EntityId
    {
        get;
        set;
    }

    /// <summary>
    /// The type of entity being voted on (for polymorphic relationships)
    /// </summary>
    [MaxLength(255)]
    public string EntityType
    {
        get;
        set;
    } = string.Empty;
}
