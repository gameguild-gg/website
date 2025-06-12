using cms.Data;
using Microsoft.EntityFrameworkCore;

namespace cms.Modules.UserProfile.Services;

public interface IUserProfileService
{
    Task<IEnumerable<Models.UserProfile>> GetAllUserProfilesAsync();

    Task<Models.UserProfile?> GetUserProfileByIdAsync(Guid id);

    Task<Models.UserProfile?> GetUserProfileByUserIdAsync(Guid userId);

    Task<Models.UserProfile> CreateUserProfileAsync(Models.UserProfile userProfile);

    Task<Models.UserProfile?> UpdateUserProfileAsync(Guid id, Models.UserProfile userProfile);

    Task<bool> DeleteUserProfileAsync(Guid id);

    Task<bool> SoftDeleteUserProfileAsync(Guid id);

    Task<bool> RestoreUserProfileAsync(Guid id);

    Task<IEnumerable<Models.UserProfile>> GetDeletedUserProfilesAsync();
}

public class UserProfileService : IUserProfileService
{
    private readonly ApplicationDbContext _context;

    public UserProfileService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Models.UserProfile>> GetAllUserProfilesAsync()
    {
        return await _context.Resources
            .OfType<Models.UserProfile>()
            .Where(up => !up.IsDeleted)
            .Include(up => up.Metadata)
            .ToListAsync();
    }

    public async Task<Models.UserProfile?> GetUserProfileByIdAsync(Guid id)
    {
        return await _context.Resources
            .OfType<Models.UserProfile>()
            .Include(up => up.Metadata)
            .FirstOrDefaultAsync(up => up.Id == id && !up.IsDeleted);
    }

    public async Task<Models.UserProfile?> GetUserProfileByUserIdAsync(Guid userId)
    {
        return await _context.Resources
            .OfType<Models.UserProfile>()
            .Include(up => up.Metadata)
            .FirstOrDefaultAsync(up => up.Id == userId && !up.IsDeleted);
    }

    public async Task<Models.UserProfile> CreateUserProfileAsync(Models.UserProfile userProfile)
    {
        _context.Resources.Add(userProfile);
        await _context.SaveChangesAsync();

        return userProfile;
    }

    public async Task<Models.UserProfile?> UpdateUserProfileAsync(Guid id, Models.UserProfile userProfile)
    {
        Models.UserProfile? existingProfile = await _context.Resources
            .OfType<Models.UserProfile>()
            .FirstOrDefaultAsync(up => up.Id == id);

        if (existingProfile == null || existingProfile.IsDeleted)
            return null;

        existingProfile.GivenName = userProfile.GivenName;
        existingProfile.FamilyName = userProfile.FamilyName;
        existingProfile.DisplayName = userProfile.DisplayName;
        existingProfile.Title = userProfile.Title;
        existingProfile.Description = userProfile.Description;

        await _context.SaveChangesAsync();

        return existingProfile;
    }

    public async Task<bool> DeleteUserProfileAsync(Guid id)
    {
        Models.UserProfile? userProfile = await _context.Resources
            .OfType<Models.UserProfile>()
            .FirstOrDefaultAsync(up => up.Id == id);

        if (userProfile == null)
            return false;

        _context.Resources.Remove(userProfile);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> SoftDeleteUserProfileAsync(Guid id)
    {
        Models.UserProfile? userProfile = await _context.Resources
            .OfType<Models.UserProfile>()
            .FirstOrDefaultAsync(up => up.Id == id);

        if (userProfile == null || userProfile.IsDeleted)
            return false;

        userProfile.SoftDelete();
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> RestoreUserProfileAsync(Guid id)
    {
        Models.UserProfile? userProfile = await _context.Resources
            .OfType<Models.UserProfile>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(up => up.Id == id);

        if (userProfile == null || !userProfile.IsDeleted)
            return false;

        userProfile.Restore();
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<IEnumerable<Models.UserProfile>> GetDeletedUserProfilesAsync()
    {
        return await _context.Resources
            .OfType<Models.UserProfile>()
            .IgnoreQueryFilters()
            .Where(up => up.IsDeleted)
            .Include(up => up.Metadata)
            .ToListAsync();
    }
}
