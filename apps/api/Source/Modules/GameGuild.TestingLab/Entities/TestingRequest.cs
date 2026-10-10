using GameGuild.Identity.Users;
using GameGuild.Projects;


namespace GameGuild.TestingLab;

/// <summary>
/// Represents a request for testing and quality assurance
/// </summary>
[Table("testing_requests")]
[Index(nameof(ProjectVersionId))]
[Index(nameof(Status))]
[Index(nameof(StartDate))]
[Index(nameof(EndDate))]
[Index(nameof(CreatedById))]
[Index(nameof(InstructionsType))]
[Index(nameof(TenantId))]
public class TestingRequest : EntityBase
{
    /// <summary>
    /// Foreign key to the project version (optional - can be standalone testing)
    /// </summary>
    public Guid? ProjectVersionId { get; set; }

    public virtual ProjectVersion? ProjectVersion { get; set; }

    /// <summary>
    /// Request title
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Request description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// URL to download the product/game build
    /// </summary>
    [MaxLength(1000)]
    public string? DownloadUrl { get; set; }

    /// <summary>
    /// Type of instructions provided
    /// </summary>
    [Required]
    public InstructionType InstructionsType { get; set; }

    /// <summary>
    /// Instructions content (text format)
    /// </summary>
    public string? InstructionsContent { get; set; }

    /// <summary>
    /// URL for instructions
    /// </summary>
    [MaxLength(500)]
    public string? InstructionsUrl { get; set; }

    /// <summary>
    /// File ID for instruction documents
    /// </summary>
    public Guid? InstructionsFileId { get; set; }

    /// <summary>
    /// Simple feedback form content (plain text questions)
    /// </summary>
    public string? FeedbackFormContent { get; set; }

    /// <summary>
    /// Maximum number of testers
    /// </summary>
    public int? MaxTesters { get; set; }

    /// <summary>
    /// Current number of testers
    /// </summary>
    public int CurrentTesterCount { get; set; } = 0;

    /// <summary>
    /// Testing start date
    /// </summary>
    [Required]
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Testing end date
    /// </summary>
    [Required]
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Testing request status
    /// </summary>
    [Required]
    public TestingRequestStatus Status { get; set; } = TestingRequestStatus.Draft;

    /// <summary>
    /// Foreign key to the user who created this request
    /// </summary>
    [Required]
    public Guid CreatedById { get; set; }

    /// <summary>
    /// Navigation property to the user who created this request
    /// </summary>
    public virtual User CreatedBy { get; set; } = null!;

    /// <summary>
    /// Priority level of this testing request
    /// </summary>
    public TestingPriority Priority { get; set; } = TestingPriority.Medium;

    /// <summary>
    /// Estimated testing duration in hours
    /// </summary>
    public int? EstimatedDurationHours { get; set; }

    /// <summary>
    /// Testing mode (manual, automated, mixed)
    /// </summary>
    public TestingMode Mode { get; set; } = TestingMode.Online;

    // Navigation Properties
    /// <summary>
    /// Testing sessions for this request
    /// </summary>
    public virtual ICollection<TestingSession> Sessions { get; set; } = new List<TestingSession>();

    /// <summary>
    /// Participants in this testing request
    /// </summary>
    public virtual ICollection<TestingParticipant> Participants { get; set; } = new List<TestingParticipant>();

    /// <summary>
    /// Feedback collected for this request
    /// </summary>
    public virtual ICollection<TestingFeedback> Feedback { get; set; } = new List<TestingFeedback>();

    /// <summary>
    /// Feedback forms associated with this request
    /// </summary>
    public virtual ICollection<TestingFeedbackForm> FeedbackForms { get; set; } = new List<TestingFeedbackForm>();

    // Computed Properties
    /// <summary>
    /// Whether this request is global (tenant-independent)
    /// </summary>
    public override bool IsGlobal => TenantId == null;

    /// <summary>
    /// Whether this request is currently active
    /// </summary>
    public bool IsActive => Status == TestingRequestStatus.Active && SystemClock.UtcNow >= StartDate && SystemClock.UtcNow <= EndDate;

    /// <summary>
    /// Whether this request accepts new testers
    /// </summary>
    public bool AcceptsNewTesters => IsActive && (MaxTesters == null || CurrentTesterCount < MaxTesters);

    /// <summary>
    /// Available tester spots
    /// </summary>
    public int? AvailableSpots => MaxTesters.HasValue ? Math.Max(0, MaxTesters.Value - CurrentTesterCount) : null;

    /// <summary>
    /// Testing duration
    /// </summary>
    public TimeSpan Duration => EndDate - StartDate;

    /// <summary>
    /// Days remaining for testing
    /// </summary>
    public int? DaysRemaining => IsActive ? (int?)Math.Max(0, (EndDate - SystemClock.UtcNow).Days) : null;

    // Domain Methods
    /// <summary>
    /// Activates the testing request
    /// </summary>
    public void Activate()
    {
        if (Status != TestingRequestStatus.Draft && Status != TestingRequestStatus.Paused)
        {
            throw new InvalidOperationException("Only draft or paused requests can be activated");
        }

        Status = TestingRequestStatus.Active;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Pauses the testing request
    /// </summary>
    public void Pause()
    {
        if (Status != TestingRequestStatus.Active)
        {
            throw new InvalidOperationException("Only active requests can be paused");
        }

        Status = TestingRequestStatus.Paused;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Completes the testing request
    /// </summary>
    public void Complete()
    {
        if (Status == TestingRequestStatus.Completed)
        {
            return;
        }

        Status = TestingRequestStatus.Completed;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Cancels the testing request
    /// </summary>
    public void Cancel()
    {
        if (Status == TestingRequestStatus.Completed)
        {
            throw new InvalidOperationException("Completed requests cannot be cancelled");
        }

        Status = TestingRequestStatus.Cancelled;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Increments tester count
    /// </summary>
    public void AddTester()
    {
        if (MaxTesters.HasValue && CurrentTesterCount >= MaxTesters.Value)
        {
            throw new InvalidOperationException("Maximum testers reached");
        }

        CurrentTesterCount++;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Decrements tester count
    /// </summary>
    public void RemoveTester()
    {
        CurrentTesterCount = Math.Max(0, CurrentTesterCount - 1);
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Sets priority level
    /// </summary>
    public void SetPriority(TestingPriority priority)
    {
        Priority = priority;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Updates estimated duration
    /// </summary>
    public void SetEstimatedDuration(int hours)
    {
        EstimatedDurationHours = hours;
        UpdatedAt = SystemClock.UtcNow;
    }
}
