using GameGuild.Identity.Context.Actors;

namespace GameGuild.TestingLab;

/// <summary>
/// Service implementation for testing location operations.
/// Extracted from the monolithic TestService for focused responsibility.
/// </summary>
public class TestingLocationOperationsService(
    IApplicationDbContext context,
    IActorContextAccessor actorContextAccessor) : ITestingLocationOperations
{
    public async Task<IEnumerable<TestingLocation>> GetAllTestingLocationsAsync()
    {
        var tenantId = RequireTenantId();
        return await context.Set<TestingLocation>()
            .Where(tl => tl.TenantId == tenantId && tl.DeletedAt == null)
            .OrderBy(tl => tl.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<TestingLocation>> GetTestingLocationsAsync(int skip = 0, int take = 50, bool includeArchived = false)
    {
        var tenantId = RequireTenantId();
        var query = context.Set<TestingLocation>().AsQueryable();
        if (includeArchived)
        {
            query = query.IgnoreQueryFilters();
        }

        return await query
            .Where(tl => tl.TenantId == tenantId && (includeArchived || tl.DeletedAt == null))
            .OrderBy(tl => tl.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<TestingLocation?> GetTestingLocationByIdAsync(Guid id)
    {
        var tenantId = RequireTenantId();
        return await context.Set<TestingLocation>()
            .Where(tl => tl.Id == id && tl.TenantId == tenantId && tl.DeletedAt == null)
            .FirstOrDefaultAsync();
    }

    public async Task<TestingLocation> CreateTestingLocationAsync(TestingLocation location)
    {
        location.TenantId = RequireTenantId();
        location.Id = Guid.NewGuid();
        location.Touch();

        context.Set<TestingLocation>().Add(location);
        await context.SaveChangesAsync().ConfigureAwait(false);

        return location;
    }

    public async Task<TestingLocation> UpdateTestingLocationAsync(TestingLocation location)
    {
        var tenantId = RequireTenantId();
        var existingLocation = await context.Set<TestingLocation>()
            .FirstOrDefaultAsync(tl => tl.Id == location.Id && tl.TenantId == tenantId && tl.DeletedAt == null);

        if (existingLocation == null)
        {
            throw new InvalidOperationException($"Testing location with ID {location.Id} not found");
        }

        existingLocation.Name = location.Name;
        existingLocation.Description = location.Description;
        existingLocation.Address = location.Address;
        existingLocation.City = location.City;
        existingLocation.State = location.State;
        existingLocation.PostalCode = location.PostalCode;
        existingLocation.Country = location.Country;
        existingLocation.MaxTestersCapacity = location.MaxTestersCapacity;
        existingLocation.MaxProjectsCapacity = location.MaxProjectsCapacity;
        existingLocation.EquipmentAvailable = location.EquipmentAvailable;
        existingLocation.IsVirtual = location.IsVirtual;
        existingLocation.VirtualUrl = location.VirtualUrl;
        existingLocation.ContactEmail = location.ContactEmail;
        existingLocation.ContactPhone = location.ContactPhone;
        existingLocation.Status = location.Status;
        existingLocation.Touch();

        await context.SaveChangesAsync().ConfigureAwait(false);

        return existingLocation;
    }

    public async Task<bool> DeleteTestingLocationAsync(Guid id)
    {
        var tenantId = RequireTenantId();
        var location = await context.Set<TestingLocation>()
            .FirstOrDefaultAsync(tl => tl.Id == id && tl.TenantId == tenantId && tl.DeletedAt == null);

        if (location == null)
        {
            return false;
        }

        var hasUpcomingSessions = await context.Set<TestingSession>()
            .AnyAsync(session =>
                session.TenantId == tenantId &&
                session.LocationId == id &&
                session.DeletedAt == null &&
                session.Status != SessionStatus.Cancelled &&
                session.Status != SessionStatus.Completed &&
                session.EndTime >= SystemClock.UtcNow);
        if (hasUpcomingSessions)
        {
            throw new InvalidOperationException("Move or cancel upcoming sessions before archiving this location.");
        }

        location.SoftDelete();
        await context.SaveChangesAsync().ConfigureAwait(false);

        return true;
    }

    public async Task<bool> RestoreTestingLocationAsync(Guid id)
    {
        var tenantId = RequireTenantId();
        var location = await context.Set<TestingLocation>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(tl => tl.Id == id && tl.TenantId == tenantId && tl.DeletedAt != null);

        if (location == null)
        {
            return false;
        }

        location.Restore();
        location.Touch();
        await context.SaveChangesAsync().ConfigureAwait(false);

        return true;
    }

    private Guid RequireTenantId()
    {
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || actor.SubjectIdAsGuid == null)
        {
            throw new AuthenticationRequiredException("Testing Lab location access requires an authenticated actor.");
        }

        if (actor.TenantId == null)
        {
            throw new AccessDeniedException("Testing Lab location access requires an active tenant membership.");
        }

        return actor.TenantId.Value;
    }
}
