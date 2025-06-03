using cms.Data;
using cms.Models;
using Microsoft.EntityFrameworkCore;

namespace cms.GraphQL;

public class Query
{
    /// <summary>
    /// Gets all users from the database.
    /// </summary>
    public async Task<IEnumerable<User>> GetUsers(ApplicationDbContext context)
    {
        return await context.Users.ToListAsync();
    }
    
    /// <summary>
    /// Gets a user by their unique identifier.
    /// </summary>
    public async Task<User?> GetUserById(Guid id, ApplicationDbContext context)
    {
        return await context.Users.FirstOrDefaultAsync(u => u.Id == id);
    }
    
    /// <summary>
    /// Gets a user by their username.
    /// </summary>
    public async Task<User?> GetUserByUsername(string username, ApplicationDbContext context)
    {
        return await context.Users.FirstOrDefaultAsync(u => u.Username == username);
    }
    
    /// <summary>
    /// Gets active users only.
    /// </summary>
    public async Task<IEnumerable<User>> GetActiveUsers(ApplicationDbContext context)
    {
        return await context.Users.Where(u => u.IsActive).ToListAsync();
    }
}
