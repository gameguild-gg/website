using GameGuild.Identity.Context.Actors;

namespace GameGuild.TestingLab;

/// <summary>
/// Service implementation for testing session operations.
/// Extracted from the monolithic TestService for focused responsibility.
/// </summary>
public class TestingSessionOperationsService(
    IApplicationDbContext context,
    IActorContextAccessor actorContextAccessor) : ITestingSessionOperations
{
    public async Task<IEnumerable<TestingSession>> GetAllTestingSessionsAsync()
    {
        var tenantId = RequireTenantId();
        return await context.Set<TestingSession>()
            .Where(ts => ts.TenantId == tenantId && ts.DeletedAt == null)
            .Include(ts => ts.TestingRequest)
            .Include(ts => ts.Location)
            .OrderByDescending(ts => ts.SessionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TestingSession>> GetTestingSessionsAsync(int skip = 0, int take = 50)
    {
        var tenantId = RequireTenantId();
        return await context.Set<TestingSession>()
            .Where(ts => ts.TenantId == tenantId && ts.DeletedAt == null)
            .Include(ts => ts.TestingRequest)
            .Include(ts => ts.Location)
            .OrderByDescending(ts => ts.SessionDate)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<TestingSession?> GetTestingSessionByIdAsync(Guid id)
    {
        var tenantId = RequireTenantId();
        return await context.Set<TestingSession>()
            .Where(ts => ts.Id == id && ts.TenantId == tenantId && ts.DeletedAt == null)
            .Include(ts => ts.TestingRequest)
            .Include(ts => ts.Location)
            .FirstOrDefaultAsync();
    }

    public async Task<TestingSession?> GetTestingSessionByIdWithDetailsAsync(Guid id)
    {
        var tenantId = RequireTenantId();
        return await context.Set<TestingSession>()
            .Where(ts => ts.Id == id && ts.TenantId == tenantId && ts.DeletedAt == null)
            .Include(ts => ts.TestingRequest)
            .Include(ts => ts.Location)
            .FirstOrDefaultAsync();
    }

    public async Task<TestingSession> CreateTestingSessionAsync(TestingSession testingSession)
    {
        var tenantId = RequireTenantId();
        var requestBelongsToTenant = await context.Set<TestingRequest>()
            .AnyAsync(request => request.Id == testingSession.TestingRequestId &&
                                 request.TenantId == tenantId &&
                                 request.DeletedAt == null);
        var locationBelongsToTenant = await context.Set<TestingLocation>()
            .AnyAsync(location => location.Id == testingSession.LocationId &&
                                  location.TenantId == tenantId &&
                                  location.DeletedAt == null);
        if (!requestBelongsToTenant || !locationBelongsToTenant)
        {
            throw new UnauthorizedAccessException("Testing Lab sessions can only reference requests and locations from the current tenant.");
        }

        testingSession.TenantId = tenantId;
        testingSession.Id = Guid.NewGuid();
        testingSession.Touch();

        context.Set<TestingSession>().Add(testingSession);
        await context.SaveChangesAsync().ConfigureAwait(false);

        return testingSession;
    }

    public async Task<TestingSession> UpdateTestingSessionAsync(TestingSession testingSession)
    {
        var tenantId = RequireTenantId();
        var existingSession = await context.Set<TestingSession>()
            .FirstOrDefaultAsync(session => session.Id == testingSession.Id &&
                                            session.TenantId == tenantId &&
                                            session.DeletedAt == null)
            .ConfigureAwait(false);

        if (existingSession == null)
        {
            throw new InvalidOperationException($"Testing session with ID {testingSession.Id} not found.");
        }

        existingSession.SessionName = testingSession.SessionName;
        existingSession.SessionDate = testingSession.SessionDate;
        existingSession.StartTime = testingSession.StartTime;
        existingSession.EndTime = testingSession.EndTime;
        existingSession.MaxTesters = testingSession.MaxTesters;
        existingSession.Status = testingSession.Status;
        existingSession.ManagerUserId = testingSession.ManagerUserId;
        existingSession.Touch();

        await context.SaveChangesAsync().ConfigureAwait(false);

        return (await GetTestingSessionByIdAsync(existingSession.Id).ConfigureAwait(false)) ?? existingSession;
    }

    public async Task<bool> DeleteTestingSessionAsync(Guid id)
    {
        var tenantId = RequireTenantId();
        var testingSession = await context.Set<TestingSession>()
            .FirstOrDefaultAsync(session => session.Id == id && session.TenantId == tenantId && session.DeletedAt == null)
            .ConfigureAwait(false);

        if (testingSession == null)
        {
            return false;
        }

        testingSession.SoftDelete();
        await context.SaveChangesAsync().ConfigureAwait(false);

        return true;
    }

    public async Task<bool> RestoreTestingSessionAsync(Guid id)
    {
        var tenantId = RequireTenantId();
        var testingSession = await context.Set<TestingSession>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(ts => ts.Id == id && ts.TenantId == tenantId);

        if (testingSession == null)
        {
            return false;
        }

        testingSession.Restore();
        testingSession.Touch();
        await context.SaveChangesAsync().ConfigureAwait(false);

        return true;
    }

    public async Task<IEnumerable<TestingSession>> GetTestingSessionsByRequestAsync(Guid testingRequestId)
    {
        var tenantId = RequireTenantId();
        return await context.Set<TestingSession>()
            .Where(ts => ts.TestingRequestId == testingRequestId && ts.TenantId == tenantId && ts.DeletedAt == null)
            .Include(ts => ts.TestingRequest)
            .Include(ts => ts.Location)
            .OrderByDescending(ts => ts.SessionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TestingSession>> GetTestingSessionsByLocationAsync(Guid locationId)
    {
        var tenantId = RequireTenantId();
        return await context.Set<TestingSession>()
            .Where(ts => ts.LocationId == locationId && ts.TenantId == tenantId && ts.DeletedAt == null)
            .Include(ts => ts.TestingRequest)
            .Include(ts => ts.Location)
            .OrderByDescending(ts => ts.SessionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TestingSession>> GetTestingSessionsByStatusAsync(SessionStatus status)
    {
        var tenantId = RequireTenantId();
        return await context.Set<TestingSession>()
            .Where(ts => ts.Status == status && ts.TenantId == tenantId && ts.DeletedAt == null)
            .Include(ts => ts.TestingRequest)
            .Include(ts => ts.Location)
            .OrderByDescending(ts => ts.SessionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TestingSession>> GetTestingSessionsByManagerAsync(Guid managerId)
    {
        var tenantId = RequireTenantId();
        return await context.Set<TestingSession>()
            .Where(ts => ts.ManagerUserId == managerId && ts.TenantId == tenantId && ts.DeletedAt == null)
            .Include(ts => ts.TestingRequest)
            .Include(ts => ts.Location)
            .OrderByDescending(ts => ts.SessionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TestingSession>> SearchTestingSessionsAsync(string searchTerm)
    {
        var tenantId = RequireTenantId();
        var lowerSearchTerm = searchTerm.ToLower();

        return await context.Set<TestingSession>()
            .Where(ts => ts.TenantId == tenantId && ts.DeletedAt == null && ts.SessionName.ToLower().Contains(lowerSearchTerm))
            .Include(ts => ts.TestingRequest)
            .Include(ts => ts.Location)
            .OrderByDescending(ts => ts.SessionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TestingSession>> GetPublicTestingSessionsAsync(int take = 100)
    {
        var tenantId = RequireTenantId();
        var now = SystemClock.UtcNow;
        var graceWindow = now.AddHours(-2);

        return await context.Set<TestingSession>()
            .Where(ts => ts.TenantId == tenantId && ts.DeletedAt == null &&
                (ts.Status == SessionStatus.Scheduled || ts.Status == SessionStatus.Active) &&
                ts.EndTime >= graceWindow)
            .Include(ts => ts.Location)
            .OrderBy(ts => ts.StartTime)
            .Take(Math.Min(take, 200))
            .ToListAsync();
    }

    public async Task<object> GetTestingSessionStatisticsAsync(Guid testingSessionId)
    {
        var tenantId = RequireTenantId();
        var session = await context.Set<TestingSession>()
            .FirstOrDefaultAsync(candidate => candidate.Id == testingSessionId && candidate.TenantId == tenantId && candidate.DeletedAt == null)
            .ConfigureAwait(false);

        if (session == null)
        {
            return new { };
        }

        var registrationCount = await context.Set<SessionRegistration>().CountAsync(sr =>
            sr.SessionId == testingSessionId && sr.TenantId == tenantId && sr.DeletedAt == null);
        var waitlistCount = await context.Set<SessionWaitlist>().CountAsync(sw =>
            sw.SessionId == testingSessionId && sw.TenantId == tenantId && sw.DeletedAt == null);
        var feedbackCount = await context.Set<TestingFeedback>().CountAsync(tf =>
            tf.SessionId == testingSessionId && tf.TenantId == tenantId && tf.DeletedAt == null);

        return new
        {
            session.MaxTesters,
            RegisteredCount = registrationCount,
            WaitlistCount = waitlistCount,
            FeedbackCount = feedbackCount,
            AvailableSlots = Math.Max(0, session.MaxTesters - registrationCount)
        };
    }

    public async Task<object> GetSessionAttendanceReportAsync()
    {
        var tenantId = RequireTenantId();
        var sessions = await context.Set<TestingSession>()
            .Where(ts => ts.TenantId == tenantId && ts.DeletedAt == null)
            .Include(ts => ts.Location)
            .Select(ts => new
            {
                ts.Id,
                ts.SessionName,
                Date = ts.SessionDate.ToString("yyyy-MM-dd"),
                Location = ts.Location.Name,
                TotalCapacity = ts.Location.MaxTestersCapacity,
                StudentsRegistered = ts.RegisteredTesterCount,
                StudentsAttended = ts.RegisteredTesterCount,
                AttendanceRate = ts.RegisteredTesterCount > 0 ? (double)ts.RegisteredTesterCount / ts.RegisteredTesterCount * 100 : 0,
                GamesTested = 1,
            })
            .ToListAsync();

        return sessions;
    }

    public async Task UpdateSessionAttendanceAsync(Guid sessionId, Guid userId, AttendanceStatus status, Guid updatedByUserId)
    {
        var tenantId = RequireTenantId();
        var sessionBelongsToTenant = await context.Set<TestingSession>()
            .AnyAsync(session => session.Id == sessionId && session.TenantId == tenantId && session.DeletedAt == null);
        if (!sessionBelongsToTenant)
        {
            throw new UnauthorizedAccessException("Testing session is outside the current tenant.");
        }

        var registration = await context.Set<SessionRegistration>().FirstOrDefaultAsync(sr =>
            sr.SessionId == sessionId && sr.UserId == userId && sr.TenantId == tenantId && sr.DeletedAt == null);

        if (registration == null) { throw new ArgumentException("Registration not found"); }

        registration.AttendanceStatus = status;

        if (status == AttendanceStatus.Completed) { registration.AttendedAt = SystemClock.UtcNow; }

        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    private Guid RequireTenantId()
    {
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || actor.SubjectIdAsGuid == null)
        {
            throw new AuthenticationRequiredException("Testing Lab session access requires an authenticated actor.");
        }

        if (actor.TenantId == null)
        {
            throw new AccessDeniedException("Testing Lab session access requires an active tenant membership.");
        }

        return actor.TenantId.Value;
    }
}
