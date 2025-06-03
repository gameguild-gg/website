using cms.Modules.User.Services;
using cms.Modules.User.Models;
using HotChocolate;

namespace cms.Modules.User.GraphQL;

public class Query
{
    /// <summary>
    /// Gets all active users from the database.
    /// </summary>
    public async Task<IEnumerable<Models.User>> GetUsers([Service] IUserService userService)
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
