using GameGuild.Modules.Voting.Models;

namespace GameGuild.Modules.Voting.Services;

/// <summary>
/// Interface for the service that manages votes and voting operations
/// </summary>
public interface IVotingService
{
    /// <summary>
    /// Cast a vote on an entity
    /// </summary>
    /// <param name="entityId">The ID of the entity to vote on</param>
    /// <param name="entityType">The type of entity being voted on</param>
    /// <param name="userId">The ID of the user casting the vote</param>
    /// <param name="voteType">The type of vote (upvote or downvote)</param>
    /// <param name="weight">The weight of the vote (defaults to 1)</param>
    /// <returns>The newly created vote</returns>
    Task<Vote> CastVoteAsync(Guid entityId, string entityType, Guid userId, VoteType voteType, int weight = 1);

    /// <summary>
    /// Remove a vote from an entity
    /// </summary>
    /// <param name="entityId">The ID of the entity</param>
    /// <param name="entityType">The type of entity</param>
    /// <param name="userId">The ID of the user who cast the vote</param>
    /// <returns>True if the vote was removed, false if no vote existed</returns>
    Task<bool> RemoveVoteAsync(Guid entityId, string entityType, Guid userId);

    /// <summary>
    /// Get all votes for an entity
    /// </summary>
    /// <param name="entityId">The ID of the entity</param>
    /// <param name="entityType">The type of entity</param>
    /// <returns>Collection of votes</returns>
    Task<IEnumerable<Vote>> GetVotesForEntityAsync(Guid entityId, string entityType);

    /// <summary>
    /// Calculate the total score for an entity (sum of all vote values)
    /// </summary>
    /// <param name="entityId">The ID of the entity</param>
    /// <param name="entityType">The type of entity</param>
    /// <returns>The total score</returns>
    Task<int> GetTotalScoreAsync(Guid entityId, string entityType);
}
