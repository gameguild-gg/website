using GameGuild.Modules.User.Queries;
using GameGuild.Modules.User.Services;
using MediatR;

namespace GameGuild.Modules.User.GraphQL;

public class Query
{    /// <summary>
    /// Gets all active users using CQRS pattern with MediatR.
    /// </summary>
    public async Task<IEnumerable<Models.User>> GetUsers([Service] IMediator mediator)
    {
        var query = new GetAllUsersQuery { IncludeDeleted = false };
        return await mediator.Send(query);
    }

    /// <summary>
    /// Gets all users using traditional service pattern (for comparison).
    /// </summary>
    public async Task<IEnumerable<Models.User>> GetUsersLegacy([Service] IUserService userService)
    {
        return await userService.GetAllUsersAsync();
    }

    /// <summary>
    /// Gets a user by their unique identifier (UUID).
    /// </summary>
    public async Task<Models.User?> GetUserById(Guid id, [Service] IUserService userService)
    {
        return await userService.GetUserByIdAsync(id);
    }

    /// <summary>
    /// Gets active users only.
    /// </summary>
    public async Task<IEnumerable<Models.User>> GetActiveUsers([Service] IUserService userService)
    {
        var users = await userService.GetAllUsersAsync();

        return users.Where(u => u.IsActive);
    }

    /// <summary>
    /// Gets soft-deleted users.
    /// </summary>
    public async Task<IEnumerable<Models.User>> GetDeletedUsers([Service] IUserService userService)
    {
        return await userService.GetDeletedUsersAsync();
    }
}
