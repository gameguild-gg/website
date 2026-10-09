using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;

namespace GameGuild.TestingLab;

public sealed record StudentAttendanceReportRow(
    string Id,
    string Name,
    string Email,
    string? Team,
    int Block1Sessions,
    int Block2Sessions,
    int Block3Sessions,
    int Block4Sessions,
    int TotalSessions,
    int GamesTested,
    string Status);

/// <summary>
/// Service implementation for participant management, registration, and waitlist operations.
/// Extracted from the monolithic TestService for focused responsibility.
/// </summary>
public class TestingParticipantOperationsService(
    IApplicationDbContext context,
    IActorContextAccessor actorContextAccessor) : ITestingParticipantOperations
{
    private Guid TenantId => actorContextAccessor.ActorContext.TenantId
        ?? throw new UnauthorizedAccessException("A selected tenant is required for Testing Lab participation.");

    #region Participant Management

    public async Task<TestingParticipant> AddParticipantAsync(Guid testingRequestId, Guid userId)
    {
        var tenantId = TenantId;
        var requestExists = await context.Set<TestingRequest>().AsNoTracking().AnyAsync(request =>
            request.Id == testingRequestId && request.TenantId == tenantId && request.DeletedAt == null);
        if (!requestExists)
        {
            throw new KeyNotFoundException("Testing request not found.");
        }

        if (!await context.Set<TenantMember>().AsNoTracking().AnyAsync(member =>
                member.UserId == userId && member.TenantId == tenantId && member.IsActive && member.DeletedAt == null))
        {
            throw new InvalidOperationException("The participant must be an active member of the selected tenant.");
        }

        var existingParticipant = await context.Set<TestingParticipant>()
            .FirstOrDefaultAsync(tp => tp.TestingRequestId == testingRequestId && tp.UserId == userId &&
                                       tp.TenantId == tenantId && tp.DeletedAt == null);

        if (existingParticipant != null)
        {
            return existingParticipant;
        }

        var participant = new TestingParticipant
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TestingRequestId = testingRequestId,
            UserId = userId
        };

        context.Set<TestingParticipant>().Add(participant);
        await context.SaveChangesAsync().ConfigureAwait(false);

        return await context.Set<TestingParticipant>()
            .Include(tp => tp.TestingRequest)
            .Include(tp => tp.User)
            .FirstAsync(tp => tp.Id == participant.Id && tp.TenantId == tenantId);
    }

    public async Task<bool> RemoveParticipantAsync(Guid testingRequestId, Guid userId)
    {
        var tenantId = TenantId;
        var participant = await context.Set<TestingParticipant>()
            .FirstOrDefaultAsync(tp => tp.TestingRequestId == testingRequestId && tp.UserId == userId &&
                                       tp.TenantId == tenantId && tp.DeletedAt == null);

        if (participant == null)
        {
            return false;
        }

        context.Set<TestingParticipant>().Remove(participant);
        await context.SaveChangesAsync().ConfigureAwait(false);

        return true;
    }

    public async Task<IEnumerable<TestingParticipant>> GetTestingRequestParticipantsAsync(Guid testingRequestId)
    {
        var tenantId = TenantId;
        return await context.Set<TestingParticipant>()
            .Where(tp => tp.TestingRequestId == testingRequestId && tp.TenantId == tenantId && tp.DeletedAt == null &&
                         tp.TestingRequest.TenantId == tenantId)
            .Include(tp => tp.User)
            .OrderBy(tp => tp.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> IsUserParticipantAsync(Guid testingRequestId, Guid userId)
    {
        var tenantId = TenantId;
        return await context.Set<TestingParticipant>()
            .AnyAsync(tp => tp.TestingRequestId == testingRequestId && tp.UserId == userId &&
                            tp.TenantId == tenantId && tp.DeletedAt == null && tp.TestingRequest.TenantId == tenantId);
    }

    #endregion

    #region Session Registration

    public async Task<SessionRegistration> RegisterForSessionAsync(Guid sessionId, Guid userId, RegistrationType registrationType, string? notes = null)
    {
        await EnsureActorCanParticipateAsync(userId).ConfigureAwait(false);
        var tenantId = TenantId;
        var session = await context.Set<TestingSession>()
            .FirstOrDefaultAsync(candidate => candidate.Id == sessionId && candidate.TenantId == tenantId && candidate.DeletedAt == null)
            .ConfigureAwait(false);
        if (session == null)
        {
            throw new KeyNotFoundException("Testing session not found.");
        }

        var existingRegistration = await context.Set<SessionRegistration>()
            .FirstOrDefaultAsync(sr => sr.SessionId == sessionId && sr.UserId == userId &&
                                       sr.TenantId == tenantId && sr.DeletedAt == null);

        if (existingRegistration != null)
        {
            return existingRegistration;
        }

        if (session.Status != SessionStatus.Scheduled)
        {
            throw new InvalidOperationException("Only scheduled sessions accept registrations.");
        }

        if (registrationType == RegistrationType.Tester && session.RegisteredTesterCount >= session.MaxTesters)
        {
            throw new InvalidOperationException("The testing session is at capacity. Join its waitlist instead.");
        }

        var registration = new SessionRegistration
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SessionId = sessionId,
            UserId = userId,
            RegistrationType = registrationType,
            RegistrationNotes = notes
        };

        context.Set<SessionRegistration>().Add(registration);
        if (registrationType == RegistrationType.Tester)
        {
            session.RegisteredTesterCount++;
        }
        else if (registrationType == RegistrationType.ProjectMember)
        {
            session.RegisteredProjectMemberCount++;
        }

        var waiting = await context.Set<SessionWaitlist>().FirstOrDefaultAsync(entry =>
            entry.SessionId == sessionId && entry.UserId == userId && entry.TenantId == tenantId && entry.DeletedAt == null)
            .ConfigureAwait(false);
        if (waiting != null)
        {
            context.Set<SessionWaitlist>().Remove(waiting);
        }

        await context.SaveChangesAsync().ConfigureAwait(false);

        return await context.Set<SessionRegistration>()
            .Include(sr => sr.Session)
            .Include(sr => sr.User)
            .FirstAsync(sr => sr.Id == registration.Id && sr.TenantId == tenantId);
    }

    public async Task<bool> UnregisterFromSessionAsync(Guid sessionId, Guid userId)
    {
        await EnsureActorCanParticipateAsync(userId).ConfigureAwait(false);
        var tenantId = TenantId;
        var registration = await context.Set<SessionRegistration>()
            .FirstOrDefaultAsync(sr => sr.SessionId == sessionId && sr.UserId == userId &&
                                       sr.TenantId == tenantId && sr.DeletedAt == null);

        if (registration == null)
        {
            return false;
        }

        var session = await context.Set<TestingSession>()
            .FirstOrDefaultAsync(candidate => candidate.Id == sessionId && candidate.TenantId == tenantId && candidate.DeletedAt == null)
            .ConfigureAwait(false);

        if (session != null)
        {
            if (registration.RegistrationType == RegistrationType.Tester)
            {
                session.RegisteredTesterCount = Math.Max(0, session.RegisteredTesterCount - 1);
            }
            else if (registration.RegistrationType == RegistrationType.ProjectMember)
            {
                session.RegisteredProjectMemberCount = Math.Max(0, session.RegisteredProjectMemberCount - 1);
            }
        }

        context.Set<SessionRegistration>().Remove(registration);
        if (session != null && registration.RegistrationType == RegistrationType.Tester)
        {
            await PromoteOldestWaitlistedTesterAsync(session, tenantId).ConfigureAwait(false);
        }

        await context.SaveChangesAsync().ConfigureAwait(false);

        return true;
    }

    public async Task<IEnumerable<SessionRegistration>> GetSessionRegistrationsAsync(Guid sessionId)
    {
        var tenantId = TenantId;
        return await context.Set<SessionRegistration>()
            .Where(sr => sr.SessionId == sessionId && sr.TenantId == tenantId && sr.DeletedAt == null &&
                         sr.Session.TenantId == tenantId && sr.Session.DeletedAt == null)
            .Include(sr => sr.User)
            .OrderBy(sr => sr.CreatedAt)
            .ToListAsync();
    }

    #endregion

    #region Waitlist

    public async Task<SessionWaitlist> AddToWaitlistAsync(Guid sessionId, Guid userId, RegistrationType registrationType, string? notes = null)
    {
        await EnsureActorCanParticipateAsync(userId).ConfigureAwait(false);
        var tenantId = TenantId;
        var sessionExists = await context.Set<TestingSession>().AsNoTracking().AnyAsync(session =>
            session.Id == sessionId && session.TenantId == tenantId && session.DeletedAt == null &&
            session.Status == SessionStatus.Scheduled).ConfigureAwait(false);
        if (!sessionExists)
        {
            throw new KeyNotFoundException("Testing session not found or not open for registration.");
        }

        if (await context.Set<SessionRegistration>().AnyAsync(registration =>
                registration.SessionId == sessionId && registration.UserId == userId &&
                registration.TenantId == tenantId && registration.DeletedAt == null).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The user is already registered for this session.");
        }

        var existingWaitlist = await context.Set<SessionWaitlist>()
            .FirstOrDefaultAsync(sw => sw.SessionId == sessionId && sw.UserId == userId &&
                                       sw.TenantId == tenantId && sw.DeletedAt == null);

        if (existingWaitlist != null)
        {
            return existingWaitlist;
        }

        var maxPosition = await context.Set<SessionWaitlist>()
            .Where(sw => sw.SessionId == sessionId && sw.TenantId == tenantId && sw.DeletedAt == null)
            .MaxAsync(sw => (int?)sw.Position) ?? 0;

        var waitlistEntry = new SessionWaitlist
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SessionId = sessionId,
            UserId = userId,
            RegistrationType = registrationType,
            Position = maxPosition + 1,
            RegistrationNotes = notes,
        };

        context.Set<SessionWaitlist>().Add(waitlistEntry);
        await context.SaveChangesAsync().ConfigureAwait(false);

        return await context.Set<SessionWaitlist>()
            .Include(sw => sw.Session)
            .Include(sw => sw.User)
            .FirstAsync(sw => sw.Id == waitlistEntry.Id && sw.TenantId == tenantId);
    }

    public async Task<bool> RemoveFromWaitlistAsync(Guid sessionId, Guid userId)
    {
        await EnsureActorCanParticipateAsync(userId).ConfigureAwait(false);
        var tenantId = TenantId;
        var waitlistEntry = await context.Set<SessionWaitlist>()
            .FirstOrDefaultAsync(sw => sw.SessionId == sessionId && sw.UserId == userId &&
                                       sw.TenantId == tenantId && sw.DeletedAt == null);

        if (waitlistEntry == null)
        {
            return false;
        }

        var removedPosition = waitlistEntry.Position;

        context.Set<SessionWaitlist>().Remove(waitlistEntry);

        var remainingEntries = await context.Set<SessionWaitlist>()
            .Where(sw => sw.SessionId == sessionId && sw.TenantId == tenantId && sw.DeletedAt == null &&
                         sw.Position > removedPosition)
            .ToListAsync();

        foreach (var entry in remainingEntries)
        {
            entry.Position--;
        }

        await context.SaveChangesAsync().ConfigureAwait(false);

        return true;
    }

    public async Task<IEnumerable<SessionWaitlist>> GetSessionWaitlistAsync(Guid sessionId)
    {
        var tenantId = TenantId;
        return await context.Set<SessionWaitlist>()
            .Where(sw => sw.SessionId == sessionId && sw.TenantId == tenantId && sw.DeletedAt == null &&
                         sw.Session.TenantId == tenantId && sw.Session.DeletedAt == null)
            .Include(sw => sw.User)
            .OrderBy(sw => sw.Position)
            .ToListAsync();
    }

    #endregion

    #region User Activity & Attendance

    public async Task<object> GetUserTestingActivityAsync(Guid userId)
    {
        var tenantId = TenantId;
        var participationCount = await context.Set<TestingParticipant>().CountAsync(tp => tp.UserId == userId && tp.TenantId == tenantId && tp.DeletedAt == null);
        var sessionRegistrationCount = await context.Set<SessionRegistration>().CountAsync(sr => sr.UserId == userId && sr.TenantId == tenantId && sr.DeletedAt == null);
        var feedbackCount = await context.Set<TestingFeedback>().CountAsync(tf => tf.UserId == userId && tf.TenantId == tenantId && tf.DeletedAt == null);
        var managedSessionCount = await context.Set<TestingSession>().CountAsync(ts => ts.ManagerUserId == userId && ts.TenantId == tenantId && ts.DeletedAt == null);
        var createdRequestCount = await context.Set<TestingRequest>().CountAsync(tr => tr.CreatedById == userId && tr.TenantId == tenantId && tr.DeletedAt == null);

        return new
        {
            ParticipationCount = participationCount,
            SessionRegistrationCount = sessionRegistrationCount,
            FeedbackCount = feedbackCount,
            ManagedSessionCount = managedSessionCount,
            CreatedRequestCount = createdRequestCount
        };
    }

    public async Task<object> GetStudentAttendanceReportAsync()
    {
        var tenantId = TenantId;
        var registrations = await context.Set<SessionRegistration>()
            .AsNoTracking()
            .Include(registration => registration.User)
            .Include(registration => registration.Session)
            .Where(registration => registration.TenantId == tenantId && registration.DeletedAt == null &&
                                   registration.Session.TenantId == tenantId && registration.Session.DeletedAt == null)
            .ToListAsync()
            .ConfigureAwait(false);

        var participants = await context.Set<TestingParticipant>()
            .AsNoTracking()
            .Include(participant => participant.TestingRequest)
            .Where(participant => participant.TenantId == tenantId && participant.DeletedAt == null &&
                                  participant.TestingRequest.TenantId == tenantId)
            .ToListAsync()
            .ConfigureAwait(false);

        var feedback = await context.Set<TestingFeedback>()
            .AsNoTracking()
            .Where(entry => entry.TenantId == tenantId && entry.DeletedAt == null)
            .ToListAsync()
            .ConfigureAwait(false);

        var userIds = registrations.Select(registration => registration.UserId)
            .Concat(participants.Select(participant => participant.UserId))
            .Concat(feedback.Select(entry => entry.UserId))
            .Distinct()
            .ToArray();

        if (userIds.Length == 0)
        {
            return new List<StudentAttendanceReportRow>();
        }

        var usersById = await context.Set<User>()
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id)
            .ConfigureAwait(false);

        var rows = userIds
            .Select(userId => BuildAttendanceRow(
                userId,
                usersById.GetValueOrDefault(userId),
                registrations.Where(registration => registration.UserId == userId),
                participants.Where(participant => participant.UserId == userId),
                feedback.Where(entry => entry.UserId == userId)))
            .OrderBy(row => row.Name)
            .ThenBy(row => row.Email)
            .ToList();

        return rows;
    }

    #endregion

    private async Task EnsureActorCanParticipateAsync(Guid userId)
    {
        var actor = actorContextAccessor.ActorContext;
        if (actor.SubjectIdAsGuid != userId && !actor.IsTenantAdmin)
        {
            throw new UnauthorizedAccessException("Users may only manage their own Testing Lab participation.");
        }

        if (!await TestingLabActorAccess.IsActiveTenantActorAsync(context, actor, CancellationToken.None).ConfigureAwait(false))
        {
            throw new UnauthorizedAccessException("An active tenant membership is required for Testing Lab participation.");
        }
    }

    private async Task PromoteOldestWaitlistedTesterAsync(TestingSession session, Guid tenantId)
    {
        var next = await context.Set<SessionWaitlist>()
            .Where(entry => entry.SessionId == session.Id && entry.TenantId == tenantId &&
                            entry.DeletedAt == null && entry.RegistrationType == RegistrationType.Tester)
            .OrderBy(entry => entry.Position)
            .ThenBy(entry => entry.CreatedAt)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (next == null || session.RegisteredTesterCount >= session.MaxTesters)
        {
            return;
        }

        context.Set<SessionWaitlist>().Remove(next);
        context.Set<SessionRegistration>().Add(new SessionRegistration
        {
            TenantId = tenantId,
            SessionId = session.Id,
            UserId = next.UserId,
            RegistrationType = next.RegistrationType,
            RegistrationNotes = next.RegistrationNotes,
        });
        session.RegisteredTesterCount++;
        var remaining = await context.Set<SessionWaitlist>()
            .Where(entry => entry.SessionId == session.Id && entry.TenantId == tenantId &&
                            entry.DeletedAt == null && entry.Position > next.Position)
            .ToListAsync()
            .ConfigureAwait(false);
        foreach (var entry in remaining)
        {
            entry.Position--;
        }
    }

    private static StudentAttendanceReportRow BuildAttendanceRow(
        Guid userId,
        User? user,
        IEnumerable<SessionRegistration> userRegistrations,
        IEnumerable<TestingParticipant> userParticipants,
        IEnumerable<TestingFeedback> userFeedback)
    {
        var registrations = userRegistrations.ToList();
        var participants = userParticipants.ToList();
        var feedback = userFeedback.ToList();
        var attendedRegistrations = registrations.Where(IsAttended).ToList();
        var noShowCount = registrations.Count(IsNoShow);

        var blockCounts = attendedRegistrations
            .GroupBy(registration => GetCalendarBlock(registration.Session.SessionDate))
            .ToDictionary(group => group.Key, group => group.Count());

        var gamesTested = attendedRegistrations.Select(registration => (Guid?)registration.Session.TestingRequestId)
            .Concat(participants.Where(IsCompletedParticipation).Select(participant => (Guid?)participant.TestingRequestId))
            .Concat(feedback.Select(entry => entry.TestingRequestId ?? entry.ApplicationId))
            .Where(identifier => identifier.HasValue)
            .Distinct()
            .Count();

        var totalSessions = attendedRegistrations.Count;
        var status = noShowCount > 0 || totalSessions == 0
            ? "atRisk"
            : totalSessions >= 2 || feedback.Count > 0
                ? "onTrack"
                : "monitor";

        return new StudentAttendanceReportRow(
            userId.ToString(),
            user?.Name ?? "Unknown user",
            user?.Email ?? string.Empty,
            ResolveTeam(registrations, participants),
            blockCounts.GetValueOrDefault(1),
            blockCounts.GetValueOrDefault(2),
            blockCounts.GetValueOrDefault(3),
            blockCounts.GetValueOrDefault(4),
            totalSessions,
            gamesTested,
            status);
    }

    private static bool IsAttended(SessionRegistration registration)
        => registration.AttendanceStatus is AttendanceStatus.Present or AttendanceStatus.Completed ||
           registration.Status == RegistrationStatus.Attended ||
           registration.CheckedInAt.HasValue;

    private static bool IsNoShow(SessionRegistration registration)
        => registration.AttendanceStatus == AttendanceStatus.NoShow ||
           registration.Status == RegistrationStatus.NoShow;

    private static bool IsCompletedParticipation(TestingParticipant participant)
        => participant.Status == ParticipationStatus.Completed || participant.CompletedAt.HasValue;

    private static int GetCalendarBlock(DateTime sessionDate)
        => sessionDate.Month switch
        {
            <= 3 => 1,
            <= 6 => 2,
            <= 9 => 3,
            _ => 4
        };

    private static string? ResolveTeam(IEnumerable<SessionRegistration> registrations, IEnumerable<TestingParticipant> participants)
        => registrations.Select(registration => registration.RegistrationNotes)
               .FirstOrDefault(note => !string.IsNullOrWhiteSpace(note))
           ?? participants.Select(participant => participant.TestingRequest.Title)
               .FirstOrDefault(title => !string.IsNullOrWhiteSpace(title));
}
