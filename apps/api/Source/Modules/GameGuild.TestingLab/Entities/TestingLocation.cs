namespace GameGuild.TestingLab;

/// <summary>
/// Represents a testing location where QA sessions can be conducted
/// </summary>
[Table("testing_locations")]
[Index(nameof(Name))]
[Index(nameof(City))]
[Index(nameof(Status))]
[Index(nameof(Capacity))]
[Index(nameof(IsVirtual))]
[Index(nameof(TenantId))]
public class TestingLocation : EntityBase
{
    /// <summary>
    /// Location name
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Location description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Street address
    /// </summary>
    [MaxLength(500)]
    public string? Address { get; set; }

    /// <summary>
    /// City
    /// </summary>
    [MaxLength(100)]
    public string? City { get; set; }

    /// <summary>
    /// State/Province
    /// </summary>
    [MaxLength(100)]
    public string? State { get; set; }

    /// <summary>
    /// Postal code
    /// </summary>
    [MaxLength(20)]
    public string? PostalCode { get; set; }

    /// <summary>
    /// Country
    /// </summary>
    [MaxLength(100)]
    public string? Country { get; set; }

    /// <summary>
    /// Maximum capacity for testers
    /// </summary>
    public int? Capacity { get; set; }

    public int MaxTestersCapacity {
        get => Capacity ?? 0;
        set => Capacity = value;
    }

    public int MaxProjectsCapacity { get; set; }

    /// <summary>
    /// Available equipment/resources
    /// </summary>
    public string? Equipment { get; set; }

    public string? EquipmentAvailable {
        get => Equipment;
        set => Equipment = value;
    }

    /// <summary>
    /// Whether this is a virtual location (online testing)
    /// </summary>
    public bool IsVirtual { get; set; } = false;

    /// <summary>
    /// Virtual meeting URL or platform info
    /// </summary>
    [MaxLength(500)]
    public string? VirtualUrl { get; set; }

    /// <summary>
    /// Location status
    /// </summary>
    public LocationStatus Status { get; set; } = LocationStatus.Active;

    /// <summary>
    /// Contact email for this location
    /// </summary>
    [MaxLength(255)]
    public string? ContactEmail { get; set; }

    /// <summary>
    /// Contact phone for this location
    /// </summary>
    [MaxLength(50)]
    public string? ContactPhone { get; set; }

    // Navigation Properties
    /// <summary>
    /// Testing sessions at this location
    /// </summary>
    public virtual ICollection<TestingSession> Sessions { get; set; } = new List<TestingSession>();

    // Computed Properties
    /// <summary>
    /// Whether this location is global (tenant-independent)
    /// </summary>
    public override bool IsGlobal => TenantId == null;

    /// <summary>
    /// Whether this location is currently available
    /// </summary>
    public bool IsAvailable => Status == LocationStatus.Active;

    /// <summary>
    /// Full address as string
    /// </summary>
    public string FullAddress => string.Join(", ", new[] { Address, City, State, PostalCode, Country }.Where(x => !string.IsNullOrWhiteSpace(x)));

    /// <summary>
    /// Number of active sessions
    /// </summary>
    public int ActiveSessionCount => Sessions?.Count(s => s.Status == SessionStatus.Active) ?? 0;

    // Domain Methods
    /// <summary>
    /// Activates the location
    /// </summary>
    public void Activate()
    {
        Status = LocationStatus.Active;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Deactivates the location
    /// </summary>
    public void Deactivate()
    {
        Status = LocationStatus.Inactive;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Puts location under maintenance
    /// </summary>
    public void SetMaintenance()
    {
        Status = LocationStatus.Maintenance;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Updates capacity
    /// </summary>
    public void SetCapacity(int capacity)
    {
        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity cannot be negative");
        }

        Capacity = capacity;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Updates virtual meeting information
    /// </summary>
    public void SetVirtualInfo(string url)
    {
        IsVirtual = true;
        VirtualUrl = url;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Checks if location can accommodate a session
    /// </summary>
    public bool CanAccommodate(int requiredCapacity)
    {
        return IsAvailable && (Capacity == null || Capacity >= requiredCapacity);
    }
}
