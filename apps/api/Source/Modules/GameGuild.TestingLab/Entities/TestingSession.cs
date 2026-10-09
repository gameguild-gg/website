using GameGuild.Identity.Users;


namespace GameGuild.TestingLab;

/// <summary>
/// Represents a testing session for quality assurance and user feedback collection
/// </summary>
[Table("testing_sessions")]
[Index(nameof(TestingRequestId))]
[Index(nameof(LocationId))]
[Index(nameof(SessionDate))]
[Index(nameof(Status))]
[Index(nameof(ManagerId))]
[Index(nameof(TenantId))]
public class TestingSession : EntityBase
{
    /// <summary>
    /// Optional event slot that scheduled this operational session.
    /// </summary>
    public Guid? EventSlotId { get; set; }

    /// <summary>
    /// Event slot that scheduled this operational session.
    /// </summary>
    public virtual TestingEventSlot? EventSlot { get; set; }
    /// <summary>
    /// Foreign key to the testing request
    /// </summary>
    [Required]
    public Guid TestingRequestId { get; set; }

    /// <summary>
    /// Navigation property to the testing request
    /// </summary>
    public virtual TestingRequest TestingRequest { get; set; } = null!;

    /// <summary>
    /// Foreign key to the testing location
    /// </summary>
    [Required]
    public Guid LocationId { get; set; }

    /// <summary>
    /// Navigation property to the testing location
    /// </summary>
    public virtual TestingLocation Location { get; set; } = null!;

    /// <summary>
    /// Session name
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string SessionName { get; set; } = string.Empty;

    /// <summary>
    /// Session date
    /// </summary>
    [Required]
    public DateTime SessionDate { get; set; }

    /// <summary>
    /// Session start time
    /// </summary>
    [Required]
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Session end time
    /// </summary>
    [Required]
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Maximum number of testers
    /// </summary>
    [Required]
    public int MaxTesters { get; set; }

    /// <summary>
    /// Maximum number of projects
    /// </summary>
    [Required]
    public int MaxProjects { get; set; } = 1;

    /// <summary>
    /// Number of registered testers
    /// </summary>
    public int RegisteredTesterCount { get; set; } = 0;

    /// <summary>
    /// Number of registered project members
    /// </summary>
    public int RegisteredProjectMemberCount { get; set; } = 0;

    /// <summary>
    /// Number of registered projects
    /// </summary>
    public int RegisteredProjectCount { get; set; } = 0;

    /// <summary>
    /// Session status
    /// </summary>
    [Required]
    public SessionStatus Status { get; set; } = SessionStatus.Scheduled;

    /// <summary>
    /// Foreign key to the session manager
    /// </summary>
    [Required]
    public Guid ManagerId { get; set; }

    /// <summary>
    /// Navigation property to the session manager
    /// </summary>
    public virtual User Manager { get; set; } = null!;

    /// <summary>
    /// Additional foreign key to the session manager (for backward compatibility)
    /// </summary>
    public Guid ManagerUserId { get; set; }

    /// <summary>
    /// Foreign key to the user who created this session
    /// </summary>
    [Required]
    public Guid CreatedById { get; set; }

    /// <summary>
    /// Navigation property to the user who created this session
    /// </summary>
    public virtual User CreatedBy { get; set; } = null!;

    // Navigation Properties
    /// <summary>
    /// Session registrations
    /// </summary>
    public virtual ICollection<SessionRegistration> Registrations { get; set; } = new List<SessionRegistration>();

    /// <summary>
    /// Testing feedback for this session
    /// </summary>
    public virtual ICollection<TestingFeedback> Feedback { get; set; } = new List<TestingFeedback>();

    // Session waitlist entries are modeled through SessionWaitlist.SessionId.

    // Computed Properties
    /// <summary>
    /// Whether this session is global (tenant-independent)
    /// </summary>
    public override bool IsGlobal => TenantId == null;

    /// <summary>
    /// Whether the session is currently active
    /// </summary>
    public bool IsActive => Status == SessionStatus.Active;

    /// <summary>
    /// Whether the session is completed
    /// </summary>
    public bool IsCompleted => Status == SessionStatus.Completed;

    /// <summary>
    /// Whether the session allows new registrations
    /// </summary>
    public bool AllowsRegistration => Status == SessionStatus.Scheduled && RegisteredTesterCount < MaxTesters;

    /// <summary>
    /// Available spots for testers
    /// </summary>
    public int AvailableSpots => Math.Max(0, MaxTesters - RegisteredTesterCount);

    /// <summary>
    /// Session duration
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;

    // Domain Methods
    /// <summary>
    /// Starts the testing session
    /// </summary>
    public void Start()
    {
        if (Status != SessionStatus.Scheduled)
        {
            throw new InvalidOperationException("Only scheduled sessions can be started");
        }

        Status = SessionStatus.Active;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Completes the testing session
    /// </summary>
    public void Complete()
    {
        if (Status != SessionStatus.Active)
        {
            throw new InvalidOperationException("Only active sessions can be completed");
        }

        Status = SessionStatus.Completed;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Cancels the testing session
    /// </summary>
    public void Cancel()
    {
        if (Status == SessionStatus.Completed)
        {
            throw new InvalidOperationException("Completed sessions cannot be cancelled");
        }

        Status = SessionStatus.Cancelled;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Checks if a user can register for this session
    /// </summary>
    public bool CanUserRegister(Guid userId)
    {
        return AllowsRegistration && !Registrations.Any(r => r.UserId == userId);
    }

    /// <summary>
    /// Increments tester count
    /// </summary>
    public void IncrementTesterCount()
    {
        RegisteredTesterCount++;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Decrements tester count
    /// </summary>
    public void DecrementTesterCount()
    {
        RegisteredTesterCount = Math.Max(0, RegisteredTesterCount - 1);
        UpdatedAt = SystemClock.UtcNow;
    }
}
