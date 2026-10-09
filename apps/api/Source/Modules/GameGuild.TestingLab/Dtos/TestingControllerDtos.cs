namespace GameGuild.TestingLab;

// DTOs for request bodies (extracted from TestingController.cs)

public class UpdateAttendanceDto
{
    public Guid UserId { get; set; }
    public AttendanceStatus AttendanceStatus { get; set; }
}

public class ReportFeedbackDto
{
    public string Reason { get; set; } = string.Empty;
}

public class RateFeedbackQualityDto
{
    public FeedbackQuality Quality { get; set; }
}

public class CreateTestingLocationDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public int MaxTestersCapacity { get; set; }
    public int MaxProjectsCapacity { get; set; }
    public string? EquipmentAvailable { get; set; }
    public bool IsVirtual { get; set; }
    public string? VirtualUrl { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public LocationStatus Status { get; set; } = LocationStatus.Active;

    public TestingLocation ToTestingLocation()
    {
        return new TestingLocation
        {
            Name = Name,
            Description = Description,
            Address = Address,
            City = City,
            State = State,
            PostalCode = PostalCode,
            Country = Country,
            MaxTestersCapacity = MaxTestersCapacity,
            MaxProjectsCapacity = MaxProjectsCapacity,
            EquipmentAvailable = EquipmentAvailable,
            IsVirtual = IsVirtual,
            VirtualUrl = VirtualUrl,
            ContactEmail = ContactEmail,
            ContactPhone = ContactPhone,
            Status = Status
        };
    }
}

public class UpdateTestingLocationDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public int? MaxTestersCapacity { get; set; }
    public int? MaxProjectsCapacity { get; set; }
    public string? EquipmentAvailable { get; set; }
    public bool? IsVirtual { get; set; }
    public string? VirtualUrl { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public LocationStatus? Status { get; set; }

    public void UpdateTestingLocation(TestingLocation location)
    {
        if (!string.IsNullOrEmpty(Name))
        {
            location.Name = Name;
        }

        if (Description != null)
        {
            location.Description = Description;
        }

        if (Address != null)
        {
            location.Address = Address;
        }

        if (City != null)
        {
            location.City = City;
        }

        if (State != null)
        {
            location.State = State;
        }

        if (PostalCode != null)
        {
            location.PostalCode = PostalCode;
        }

        if (Country != null)
        {
            location.Country = Country;
        }

        if (MaxTestersCapacity.HasValue)
        {
            location.MaxTestersCapacity = MaxTestersCapacity.Value;
        }

        if (MaxProjectsCapacity.HasValue)
        {
            location.MaxProjectsCapacity = MaxProjectsCapacity.Value;
        }

        if (EquipmentAvailable != null)
        {
            location.EquipmentAvailable = EquipmentAvailable;
        }

        if (IsVirtual.HasValue)
        {
            location.IsVirtual = IsVirtual.Value;
        }

        if (VirtualUrl != null)
        {
            location.VirtualUrl = VirtualUrl;
        }

        if (ContactEmail != null)
        {
            location.ContactEmail = ContactEmail;
        }

        if (ContactPhone != null)
        {
            location.ContactPhone = ContactPhone;
        }

        if (Status.HasValue)
        {
            location.Status = Status.Value;
        }
    }
}

// Module Permission DTOs
public class TestingLabActionPermissions
{
    public bool CanCreateSessions { get; set; }
    public bool CanDeleteSessions { get; set; }
    public bool CanManageTesters { get; set; }
    public bool CanViewReports { get; set; }
    public bool CanExportData { get; set; }
}

public class AssignTestingLabRoleDto
{
    public Guid UserId { get; set; }
    public Guid? TenantId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public List<PermissionConstraint>? Constraints { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
