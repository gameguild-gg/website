using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Learning.Abstractions;

/// <summary>
/// Base controller class for Learning module controllers.
/// Provides common functionality including authenticated user context access and standard response helpers.
/// Inherits from <see cref="BaseApiController"/> for standardized Result-to-ActionResult mapping.
/// </summary>
/// <remarks>
/// This base class addresses DRY violations by centralizing:
/// - Actor context extraction pattern (GetRequiredUserId, GetRequiredActorContext)
/// - Null check + NotFound response pattern (NotFoundIfNull)
/// - Common authorization helpers
/// - Result-to-ActionResult mapping (via BaseApiController)
/// </remarks>
public abstract class LearningControllerBase : BaseApiController
{
    /// <summary>
    /// Provides access to the authenticated user's context
    /// </summary>
    protected IActorContextAccessor ActorContextAccessor { get; }

    /// <summary>
    /// Initializes the base controller with actor context accessor
    /// </summary>
    /// <param name="actorContextAccessor">The actor context accessor for authentication</param>
    protected LearningControllerBase(IActorContextAccessor actorContextAccessor)
    {
        ActorContextAccessor = actorContextAccessor ?? throw new ArgumentNullException(nameof(actorContextAccessor));
    }

    /// <summary>
    /// Gets the authenticated user's GUID from the actor context
    /// </summary>
    /// <returns>The authenticated user's GUID</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when user is not authenticated</exception>
    protected Guid GetRequiredUserId()
    {
        var actor = ActorContextAccessor.ActorContext;
        if (!actor.SubjectIdAsGuid.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        return actor.SubjectIdAsGuid.Value;
    }

    /// <summary>
    /// Gets the authenticated user's GUID if available, otherwise null
    /// </summary>
    /// <returns>The authenticated user's GUID or null if not authenticated</returns>
    protected Guid? GetOptionalUserId()
    {
        return ActorContextAccessor.ActorContext?.SubjectIdAsGuid;
    }

    /// <summary>
    /// Gets the full actor context for the authenticated user
    /// </summary>
    /// <returns>The actor context</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when user is not authenticated</exception>
    protected ActorContext GetRequiredActorContext()
    {
        var actor = ActorContextAccessor.ActorContext;
        if (actor == null || !actor.SubjectIdAsGuid.HasValue)
        {
            throw new UnauthorizedAccessException("User must be authenticated");
        }

        return actor;
    }

    /// <summary>
    /// Gets the current tenant ID from the actor context
    /// </summary>
    /// <returns>The tenant ID or null if not in a tenant context</returns>
    protected Guid? GetCurrentTenantId()
    {
        return ActorContextAccessor.ActorContext?.TenantId;
    }

    /// <summary>
    /// Returns NotFound if the entity is null, otherwise returns Ok with the entity
    /// </summary>
    /// <typeparam name="T">The type of the entity</typeparam>
    /// <param name="entity">The entity to check</param>
    /// <param name="notFoundMessage">Optional message for the NotFound response</param>
    /// <returns>Ok with entity or NotFound</returns>
    protected ActionResult<T> OkOrNotFound<T>(T? entity, string? notFoundMessage = null) where T : class
    {
        if (entity == null)
        {
            return NotFound(notFoundMessage ?? "Resource not found");
        }

        return Ok(entity);
    }

    /// <summary>
    /// Returns NotFound if the entity is null, otherwise executes the success action
    /// </summary>
    /// <typeparam name="T">The type of the entity</typeparam>
    /// <typeparam name="TResult">The type of the result</typeparam>
    /// <param name="entity">The entity to check</param>
    /// <param name="onSuccess">Action to execute if entity is not null</param>
    /// <param name="notFoundMessage">Optional message for the NotFound response</param>
    /// <returns>The result of onSuccess or NotFound</returns>
    protected ActionResult<TResult> MapOrNotFound<T, TResult>(T? entity, Func<T, TResult> onSuccess, string? notFoundMessage = null) where T : class
    {
        if (entity == null)
        {
            return NotFound(notFoundMessage ?? "Resource not found");
        }

        return Ok(onSuccess(entity));
    }

    /// <summary>
    /// Validates that the user has access to the specified resource (same user or admin)
    /// </summary>
    /// <param name="resourceUserId">The user ID associated with the resource</param>
    /// <returns>True if access is allowed</returns>
    protected bool CanAccessUserResource(Guid resourceUserId)
    {
        var currentUserId = GetOptionalUserId();
        if (!currentUserId.HasValue)
        {
            return false;
        }

        // Allow access if same user
        if (currentUserId.Value == resourceUserId)
        {
            return true;
        }

        // Admin role check: admins and tenant admins can access any user's resources
        var actor = ActorContextAccessor.ActorContext;
        if (actor.IsSystemAdmin || actor.IsTenantAdmin)
        {
            return true;
        }

        return false;
    }
}
