using cms.Modules.Rating.Models;
using cms.Modules.User.Models;

namespace cms.Modules.Rating.Services;

/// <summary>
/// Interface for rating-related operations
/// </summary>
public interface IRatingService
{
    /// <summary>
    /// Adds a rating to a rateable entity
    /// </summary>
    /// <param name="entityId">The ID of the entity to rate</param>
    /// <param name="entityType">The type name of the entity</param>
    /// <param name="value">Rating value (typically 1-5)</param>
    /// <param name="user">The user providing the rating</param>
    /// <param name="comment">Optional comment with the rating</param>
    /// <returns>The created rating</returns>
    Task<Models.Rating> AddRatingAsync(Guid entityId, string entityType, int value, User.Models.User user, string? comment = null);

    /// <summary>
    /// Updates an existing rating
    /// </summary>
    /// <param name="ratingId">The ID of the rating to update</param>
    /// <param name="value">The new rating value</param>
    /// <param name="user">The user updating the rating (must be the original rater)</param>
    /// <param name="comment">Optional updated comment</param>
    /// <returns>The updated rating</returns>
    Task<Models.Rating> UpdateRatingAsync(Guid ratingId, int value, User.Models.User user, string? comment = null);

    /// <summary>
    /// Deletes a rating
    /// </summary>
    /// <param name="ratingId">The ID of the rating to delete</param>
    /// <param name="user">The user deleting the rating (must be the original rater or have admin permissions)</param>
    /// <returns>True if deletion was successful</returns>
    Task<bool> DeleteRatingAsync(Guid ratingId, User.Models.User user);

    /// <summary>
    /// Gets ratings for a specific entity
    /// </summary>
    /// <param name="entityId">The ID of the entity</param>
    /// <param name="entityType">The type name of the entity</param>
    /// <returns>Collection of ratings</returns>
    Task<IEnumerable<Models.Rating>> GetRatingsForEntityAsync(Guid entityId, string entityType);

    /// <summary>
    /// Gets the average rating for a specific entity
    /// </summary>
    /// <param name="entityId">The ID of the entity</param>
    /// <param name="entityType">The type name of the entity</param>
    /// <returns>Average rating value</returns>
    Task<double> GetAverageRatingForEntityAsync(Guid entityId, string entityType);

    /// <summary>
    /// Gets a specific rating by ID
    /// </summary>
    /// <param name="ratingId">The ID of the rating to retrieve</param>
    /// <returns>The rating if found, otherwise null</returns>
    Task<Models.Rating?> GetRatingByIdAsync(Guid ratingId);

    /// <summary>
    /// Checks if a user has already rated an entity
    /// </summary>
    /// <param name="entityId">The ID of the entity</param>
    /// <param name="entityType">The type name of the entity</param>
    /// <param name="userId">The ID of the user</param>
    /// <returns>True if the user has already rated the entity</returns>
    Task<bool> HasUserRatedEntityAsync(Guid entityId, string entityType, Guid userId);
}
