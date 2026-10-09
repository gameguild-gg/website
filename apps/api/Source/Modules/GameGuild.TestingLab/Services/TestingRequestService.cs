namespace GameGuild.TestingLab;

/// <summary>
/// Backward-compatible adapter for the legacy request service contract. All data
/// access is delegated to the canonical tenant-scoped operations services.
/// </summary>
public sealed class TestingRequestService(
    ITestingRequestOperations requests,
    ITestingParticipantOperations participants) : ITestingRequestService
{
    public Task<IEnumerable<TestingRequest>> GetAllAsync() => requests.GetAllTestingRequestsAsync();

    public Task<IEnumerable<TestingRequest>> GetWithPaginationAsync(int skip = 0, int take = 50) =>
        requests.GetTestingRequestsAsync(skip, take);

    public Task<TestingRequest?> GetByIdAsync(Guid id) => requests.GetTestingRequestByIdAsync(id);

    public Task<TestingRequest?> GetByIdWithDetailsAsync(Guid id) => requests.GetTestingRequestByIdWithDetailsAsync(id);

    public Task<TestingRequest> CreateAsync(TestingRequest testingRequest) =>
        requests.CreateTestingRequestAsync(testingRequest);

    public Task<TestingRequest> UpdateAsync(TestingRequest testingRequest) =>
        requests.UpdateTestingRequestAsync(testingRequest);

    public Task<bool> DeleteAsync(Guid id) => requests.DeleteTestingRequestAsync(id);

    public Task<bool> RestoreAsync(Guid id) => requests.RestoreTestingRequestAsync(id);

    public Task<IEnumerable<TestingRequest>> GetByProjectVersionAsync(Guid projectVersionId) =>
        requests.GetTestingRequestsByProjectVersionAsync(projectVersionId);

    public Task<IEnumerable<TestingRequest>> GetByStatusAsync(TestingRequestStatus status) =>
        requests.GetTestingRequestsByStatusAsync(status);

    public Task<IEnumerable<TestingRequest>> GetActiveRequestsAsync() => requests.GetActiveTestingRequestsAsync();

    public async Task<IEnumerable<TestingRequest>> GetRequestsNeedingClosureAsync()
    {
        var now = SystemClock.UtcNow;
        return (await requests.GetAllTestingRequestsAsync().ConfigureAwait(false))
            .Where(request => (request.Status is TestingRequestStatus.Open or TestingRequestStatus.InProgress) &&
                              request.EndDate < now)
            .OrderBy(request => request.EndDate)
            .ToArray();
    }

    public async Task<bool> CanUserJoinTestingAsync(Guid userId, Guid testingRequestId)
    {
        var request = await requests.GetTestingRequestByIdAsync(testingRequestId).ConfigureAwait(false);
        if (request == null || request.Status != TestingRequestStatus.Open)
        {
            return false;
        }

        if (await participants.IsUserParticipantAsync(testingRequestId, userId).ConfigureAwait(false))
        {
            return false;
        }

        return !request.MaxTesters.HasValue || request.CurrentTesterCount < request.MaxTesters.Value;
    }

    public async Task<TestingRequest> JoinTestingAsync(Guid userId, Guid testingRequestId)
    {
        if (!await CanUserJoinTestingAsync(userId, testingRequestId).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Testing request is unavailable, full, or already joined.");
        }

        var request = await requests.GetTestingRequestByIdAsync(testingRequestId).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Testing request not found.");
        await participants.AddParticipantAsync(testingRequestId, userId).ConfigureAwait(false);
        request.CurrentTesterCount++;
        return await requests.UpdateTestingRequestAsync(request).ConfigureAwait(false);
    }

    public async Task<TestingRequest> LeaveTestingAsync(Guid userId, Guid testingRequestId)
    {
        var request = await requests.GetTestingRequestByIdAsync(testingRequestId).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Testing request not found.");
        if (!await participants.RemoveParticipantAsync(testingRequestId, userId).ConfigureAwait(false))
        {
            throw new InvalidOperationException("User is not participating in this testing request.");
        }

        request.CurrentTesterCount = Math.Max(0, request.CurrentTesterCount - 1);
        return await requests.UpdateTestingRequestAsync(request).ConfigureAwait(false);
    }

    public async Task<TestingRequest> CloseTestingRequestAsync(Guid testingRequestId)
    {
        var request = await requests.GetTestingRequestByIdAsync(testingRequestId).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Testing request not found.");
        request.Status = TestingRequestStatus.Completed;
        return await requests.UpdateTestingRequestAsync(request).ConfigureAwait(false);
    }

    public Task<IEnumerable<TestingRequest>> SearchAsync(string searchTerm) =>
        requests.SearchTestingRequestsAsync(searchTerm);
}
