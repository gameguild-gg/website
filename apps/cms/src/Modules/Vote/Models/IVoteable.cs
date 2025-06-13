namespace GameGuild.Modules.Voting.Models;

/// <summary>
/// Interface for entities that support voting (upvote/downvote).
/// </summary>
public interface IVoteable
{
    /// <summary>
    /// Gets the collection of votes for this entity.
    /// </summary>
    ICollection<Vote> Votes
    {
        get;
        set;
    }
}
