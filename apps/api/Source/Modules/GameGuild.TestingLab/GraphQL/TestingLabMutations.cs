using HotChocolate;
using HotChocolate.Types;
using System.Security.Claims;

namespace GameGuild.TestingLab;

/// <summary> GraphQL input types for testing location operations </summary>
public class CreateTestingLocationInput {
  public string Name { get; set; } = string.Empty;

  public string? Description { get; set; }

  public string? Address { get; set; }

  public int MaxTestersCapacity { get; set; }

  public int MaxProjectsCapacity { get; set; }

  public string? EquipmentAvailable { get; set; }

  public LocationStatus Status { get; set; } = LocationStatus.Active;
}

public class UpdateTestingLocationInput {
  public string? Name { get; set; }

  public string? Description { get; set; }

  public string? Address { get; set; }

  public int? MaxTestersCapacity { get; set; }

  public int? MaxProjectsCapacity { get; set; }

  public string? EquipmentAvailable { get; set; }

  public LocationStatus? Status { get; set; }
}

/// <summary> GraphQL mutations for TestingLab module using CQRS pattern </summary>
[ExtendObjectType("Mutation")]
public class TestingLabMutations {
  /// <summary> Create a new testing request </summary>
  public async Task<TestingRequest> CreateTestingRequest([Service] ITestService testService, ClaimsPrincipal claimsPrincipal, CreateTestingRequestDto input) {
    // Get the current authenticated user's ID
    var userIdClaim = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("User ID not found in token");
        }

        var request = input.ToTestingRequest(userId);

    return await testService.CreateTestingRequestAsync(request).ConfigureAwait(false);
  }

  /// <summary> Update an existing testing request </summary>
  public async Task<TestingRequest> UpdateTestingRequest([Service] ITestService testService, Guid id, UpdateTestingRequestDto input) {
    var existingRequest = await testService.GetTestingRequestByIdAsync(id).ConfigureAwait(false);

    if (existingRequest == null)
        {
            throw new ArgumentException($"Testing request with ID {id} not found");
        }

        // Update properties using the DTO
        input.UpdateTestingRequest(existingRequest);

    return await testService.UpdateTestingRequestAsync(existingRequest).ConfigureAwait(false);
  }

  /// <summary> Delete a testing request </summary>
  public async Task<bool> DeleteTestingRequest([Service] ITestService testService, Guid id) { return await testService.DeleteTestingRequestAsync(id).ConfigureAwait(false); }

  /// <summary> Create a new testing session </summary>
  public async Task<TestingSession> CreateTestingSession([Service] ITestService testService, ClaimsPrincipal claimsPrincipal, CreateTestingSessionDto input) {
    // Get the current authenticated user's ID
    var userIdClaim = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("User ID not found in token");
        }

        var session = input.ToTestingSession(userId);

    return await testService.CreateTestingSessionAsync(session).ConfigureAwait(false);
  }

  /// <summary> Update an existing testing session </summary>
  public async Task<TestingSession> UpdateTestingSession([Service] ITestService testService, Guid id, CreateTestingSessionDto input) {
    var existingSession = await testService.GetTestingSessionByIdAsync(id).ConfigureAwait(false);

    if (existingSession == null)
        {
            throw new ArgumentException($"Testing session with ID {id} not found");
        }

        // Update properties
        existingSession.TestingRequestId = input.TestingRequestId;
    existingSession.LocationId = input.LocationId;
    existingSession.SessionName = input.SessionName;
    existingSession.SessionDate = input.SessionDate;
    existingSession.StartTime = input.StartTime;
    existingSession.EndTime = input.EndTime;
    existingSession.MaxTesters = input.MaxTesters;
    existingSession.Status = input.Status;
    existingSession.ManagerUserId = input.ManagerUserId;

    return await testService.UpdateTestingSessionAsync(existingSession).ConfigureAwait(false);
  }

  /// <summary> Delete a testing session </summary>
  public async Task<bool> DeleteTestingSession([Service] ITestService testService, Guid id) { return await testService.DeleteTestingSessionAsync(id).ConfigureAwait(false); }

  /// <summary> Create a new testing location </summary>
  public async Task<TestingLocation> CreateTestingLocation([Service] ITestService testService, CreateTestingLocationInput input) {
    var location = new TestingLocation {
      Name = input.Name,
      Description = input.Description,
      Address = input.Address,
      MaxTestersCapacity = input.MaxTestersCapacity,
      MaxProjectsCapacity = input.MaxProjectsCapacity,
      EquipmentAvailable = input.EquipmentAvailable,
      Status = input.Status,
    };

    return await testService.CreateTestingLocationAsync(location).ConfigureAwait(false);
  }

  /// <summary> Update an existing testing location </summary>
  public async Task<TestingLocation> UpdateTestingLocation([Service] ITestService testService, Guid id, UpdateTestingLocationInput input) {
    var existingLocation = await testService.GetTestingLocationByIdAsync(id).ConfigureAwait(false);

    if (existingLocation == null)
        {
            throw new ArgumentException($"Testing location with ID {id} not found");
        }

        // Update properties
        if (!string.IsNullOrEmpty(input.Name))
        {
            existingLocation.Name = input.Name;
        }

        if (input.Description != null)
        {
            existingLocation.Description = input.Description;
        }

        if (input.Address != null)
        {
            existingLocation.Address = input.Address;
        }

        if (input.MaxTestersCapacity.HasValue)
        {
            existingLocation.MaxTestersCapacity = input.MaxTestersCapacity.Value;
        }

        if (input.MaxProjectsCapacity.HasValue)
        {
            existingLocation.MaxProjectsCapacity = input.MaxProjectsCapacity.Value;
        }

        if (input.EquipmentAvailable != null)
        {
            existingLocation.EquipmentAvailable = input.EquipmentAvailable;
        }

        if (input.Status.HasValue)
        {
            existingLocation.Status = input.Status.Value;
        }

        return await testService.UpdateTestingLocationAsync(existingLocation).ConfigureAwait(false);
  }

  /// <summary> Delete a testing location </summary>
  public async Task<bool> DeleteTestingLocation([Service] ITestService testService, Guid id) { return await testService.DeleteTestingLocationAsync(id).ConfigureAwait(false); }
}
