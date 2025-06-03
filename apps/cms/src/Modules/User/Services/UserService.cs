using cms.Data;
using Microsoft.EntityFrameworkCore;

namespace cms.Modules.User.Services;

public interface IUserService
{
    Task<IEnumerable<Models.User>> GetAllUsersAsync();

    Task<Models.User?> GetUserByIdAsync(Guid id);

    Task<Models.User> CreateUserAsync(Models.User user);

    Task<Models.User?> UpdateUserAsync(Guid id, Models.User user);

    Task<bool> DeleteUserAsync(Guid id);

    Task<bool> SoftDeleteUserAsync(Guid id);

    Task<bool> RestoreUserAsync(Guid id);

    Task<IEnumerable<Models.User>> GetDeletedUsersAsync();
}

public class UserService : IUserService
{
    private readonly ApplicationDbContext _context;

    public UserService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Models.User>> GetAllUsersAsync()
    {
        return await _context.Users.ToListAsync();
    }

    public async Task<Models.User?> GetUserByIdAsync(Guid id)
    {
        return await _context.Users.FindAsync(id);
    }

    public async Task<Models.User> CreateUserAsync(Models.User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return user;
    }

    public async Task<Models.User?> UpdateUserAsync(Guid id, Models.User user)
    {
        Models.User? existingUser = await _context.Users.FindAsync(id);

        if (existingUser == null)
            return null;

        existingUser.Name = user.Name;
        existingUser.Email = user.Email;
        existingUser.IsActive = user.IsActive;

        await _context.SaveChangesAsync();

        return existingUser;
    }

    public async Task<bool> DeleteUserAsync(Guid id)
    {
        Models.User? user = await _context.Users.FindAsync(id);

        if (user == null)
            return false;

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> SoftDeleteUserAsync(Guid id)
    {
        Models.User? user = await _context.Users.FindAsync(id);

        if (user == null)
            return false;

        user.SoftDelete();
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> RestoreUserAsync(Guid id)
    {
        // Need to include deleted entities to find soft-deleted user
        Models.User? user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null || !user.IsDeleted)
            return false;

        user.Restore();
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<IEnumerable<Models.User>> GetDeletedUsersAsync()
    {
        return await _context.Users
            .IgnoreQueryFilters()
            .Where(u => u.DeletedAt != null)
            .ToListAsync();
    }
}
