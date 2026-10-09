namespace GameGuild.TestingLab;

/// <summary>
/// Backward-compatible adapter for the legacy session service contract. The
/// canonical operations services own tenant resolution, authorization scope and
/// persistence.
/// </summary>
public sealed class TestingSessionService(
    ITestingSessionOperations sessions,
    ITestingParticipantOperations participants) : ITestingSessionService
{
    public Task<IEnumerable<TestingSession>> GetAllAsync() => sessions.GetAllTestingSessionsAsync();

    public Task<IEnumerable<TestingSession>> GetWithPaginationAsync(int skip = 0, int take = 50) =>
        sessions.GetTestingSessionsAsync(skip, take);

    public Task<TestingSession?> GetByIdAsync(Guid id) => sessions.GetTestingSessionByIdAsync(id);

    public Task<TestingSession?> GetByIdWithDetailsAsync(Guid id) => sessions.GetTestingSessionByIdWithDetailsAsync(id);

    public Task<TestingSession> CreateAsync(TestingSession testingSession) =>
        sessions.CreateTestingSessionAsync(testingSession);

    public Task<TestingSession> UpdateAsync(TestingSession testingSession) =>
        sessions.UpdateTestingSessionAsync(testingSession);

    public Task<bool> DeleteAsync(Guid id) => sessions.DeleteTestingSessionAsync(id);

    public Task<bool> RestoreAsync(Guid id) => sessions.RestoreTestingSessionAsync(id);

    public Task<IEnumerable<TestingSession>> GetByTestingRequestAsync(Guid testingRequestId) =>
        sessions.GetTestingSessionsByRequestAsync(testingRequestId);

    public Task<IEnumerable<TestingSession>> GetByStatusAsync(SessionStatus status) =>
        sessions.GetTestingSessionsByStatusAsync(status);

    public async Task<IEnumerable<TestingSession>> GetUpcomingSessionsAsync()
    {
        var now = SystemClock.UtcNow;
        return (await sessions.GetTestingSessionsByStatusAsync(SessionStatus.Scheduled).ConfigureAwait(false))
            .Where(session => session.StartTime > now)
            .OrderBy(session => session.StartTime)
            .ToArray();
    }

    public async Task<IEnumerable<TestingSession>> GetActiveSessionsAsync()
    {
        var now = SystemClock.UtcNow;
        return (await sessions.GetTestingSessionsByStatusAsync(SessionStatus.Active).ConfigureAwait(false))
            .Where(session => session.StartTime <= now && session.EndTime >= now)
            .OrderBy(session => session.StartTime)
            .ToArray();
    }

    public Task<IEnumerable<TestingSession>> GetByLocationAsync(Guid locationId) =>
        sessions.GetTestingSessionsByLocationAsync(locationId);

    public async Task<IEnumerable<TestingSession>> GetByDateRangeAsync(DateTime startDate, DateTime endDate) =>
        (await sessions.GetAllTestingSessionsAsync().ConfigureAwait(false))
        .Where(session => session.SessionDate >= startDate && session.SessionDate <= endDate)
        .OrderBy(session => session.SessionDate)
        .ToArray();

    public async Task<bool> CanUserJoinSessionAsync(Guid userId, Guid testingSessionId)
    {
        var session = await sessions.GetTestingSessionByIdAsync(testingSessionId).ConfigureAwait(false);
        if (session == null || !session.AllowsRegistration)
        {
            return false;
        }

        return !(await participants.GetSessionRegistrationsAsync(testingSessionId).ConfigureAwait(false))
            .Any(registration => registration.UserId == userId && registration.DeletedAt == null);
    }

    public async Task<TestingSession> JoinSessionAsync(Guid userId, Guid testingSessionId)
    {
        await participants.RegisterForSessionAsync(testingSessionId, userId, RegistrationType.Tester).ConfigureAwait(false);
        return await sessions.GetTestingSessionByIdAsync(testingSessionId).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Testing session not found after registration.");
    }

    public async Task<TestingSession> LeaveSessionAsync(Guid userId, Guid testingSessionId)
    {
        if (!await participants.UnregisterFromSessionAsync(testingSessionId, userId).ConfigureAwait(false))
        {
            throw new InvalidOperationException("User is not registered for this session.");
        }

        return await sessions.GetTestingSessionByIdAsync(testingSessionId).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Testing session not found after cancellation.");
    }

    public Task<TestingSession> StartSessionAsync(Guid testingSessionId) =>
        TransitionAsync(testingSessionId, session => session.Start());

    public Task<TestingSession> EndSessionAsync(Guid testingSessionId) =>
        TransitionAsync(testingSessionId, session => session.Complete());

    public Task<TestingSession> CancelSessionAsync(Guid testingSessionId) =>
        TransitionAsync(testingSessionId, session => session.Cancel());

    private async Task<TestingSession> TransitionAsync(Guid sessionId, Action<TestingSession> transition)
    {
        var session = await sessions.GetTestingSessionByIdAsync(sessionId).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Testing session not found.");
        transition(session);
        return await sessions.UpdateTestingSessionAsync(session).ConfigureAwait(false);
    }
}
