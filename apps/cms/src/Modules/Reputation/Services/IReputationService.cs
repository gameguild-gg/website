using cms.Modules.Reputation.Models;

namespace cms.Modules.Reputation.Services;

/// <summary>
/// Service interface for managing user reputation
/// </summary>
public interface IReputationService
{
    /// <summary>
    /// Get a user's reputation in a specific tenant (or global if tenantId is null)
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="tenantId">The tenant ID (null for global reputation)</param>
    /// <returns>The user's reputation or null if not found</returns>
    Task<IReputation?> GetUserReputationAsync(Guid userId, Guid? tenantId = null);

    /// <summary>
    /// Update a user's reputation score
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="scoreChange">The change in reputation score (can be negative)</param>
    /// <param name="tenantId">The tenant ID (null for global reputation)</param>
    /// <param name="reason">Optional reason for the reputation change</param>
    /// <returns>The updated reputation</returns>
    Task<IReputation> UpdateReputationAsync(Guid userId, int scoreChange, Guid? tenantId = null, string? reason = null);

    /// <summary>
    /// Get all users with a specific reputation level or higher
    /// </summary>
    /// <param name="minimumLevel">The minimum reputation level</param>
    /// <param name="tenantId">The tenant ID (null for global reputation)</param>
    /// <returns>List of users meeting the reputation criteria</returns>
    Task<IEnumerable<IReputation>> GetUsersByReputationLevelAsync(ReputationLevel minimumLevel, Guid? tenantId = null);

}
