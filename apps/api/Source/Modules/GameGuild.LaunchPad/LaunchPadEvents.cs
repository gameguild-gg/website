using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using GameGuild.Identity.Users;
using GameGuild.Projects;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.LaunchPad;

public enum LaunchPadEventStatus
{
    Draft = 0,
    ApplicationsOpen = 1,
    ApplicationsClosed = 2,
    Scheduled = 3,
    Active = 4,
    Completed = 5,
    Cancelled = 6,
    Archived = 7
}

public enum LaunchPadApplicationStatus
{
    Draft = 0,
    Submitted = 1,
    UnderReview = 2,
    Waitlisted = 3,
    Approved = 4,
    Rejected = 5,
    Withdrawn = 6
}

public enum LaunchPadParticipantStatus
{
    Registered = 0,
    Waitlisted = 1,
    CheckedIn = 2,
    Attended = 3,
    Completed = 4,
    Cancelled = 5,
    NoShow = 6
}

public enum LaunchPadParticipantRole
{
    Participant = 0,
    Mentor = 1,
    Audience = 2,
    Presenter = 3
}

[Table("launch_pad_events")]
[Index(nameof(TenantId), nameof(Status), nameof(StartsAt))]
public sealed class LaunchPadEvent : EntityBase<Guid>
{
    [Required, MaxLength(200)]
    public string Name { get; private set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; private set; }

    public DateTime StartsAt { get; private set; }
    public DateTime EndsAt { get; private set; }
    public DateTime? ApplicationsOpenAt { get; private set; }
    public DateTime? ApplicationsCloseAt { get; private set; }
    public LaunchPadEventStatus Status { get; private set; } = LaunchPadEventStatus.Draft;

    public ICollection<LaunchPadApplication> Applications { get; private set; } = new List<LaunchPadApplication>();
    public ICollection<LaunchPadParticipantSlot> Slots { get; private set; } = new List<LaunchPadParticipantSlot>();

    private LaunchPadEvent() { }

    public static LaunchPadEvent Create(Guid tenantId, string name, DateTime startsAt, DateTime endsAt, string? description = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant is required.", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        if (endsAt <= startsAt)
        {
            throw new ArgumentException("Event end must be after start.", nameof(endsAt));
        }

        return new LaunchPadEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            Description = description?.Trim(),
            StartsAt = startsAt,
            EndsAt = endsAt
        };
    }

    public void ConfigureApplicationWindow(DateTime opensAt, DateTime closesAt)
    {
        EnsureState(LaunchPadEventStatus.Draft);
        if (closesAt <= opensAt || closesAt > StartsAt)
        {
            throw new InvalidOperationException("Application window must close after opening and before the event starts.");
        }

        ApplicationsOpenAt = opensAt;
        ApplicationsCloseAt = closesAt;
        Touch();
    }

    public void Update(string name, string? description, DateTime startsAt, DateTime endsAt,
        DateTime? applicationsOpenAt, DateTime? applicationsCloseAt)
    {
        if (Status is not (LaunchPadEventStatus.Draft or LaunchPadEventStatus.ApplicationsOpen or LaunchPadEventStatus.ApplicationsClosed))
        {
            throw new InvalidOperationException("A scheduled, active, completed or archived event can no longer be edited.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        if (endsAt <= startsAt)
        {
            throw new ArgumentException("Event end must be after start.", nameof(endsAt));
        }

        if (applicationsOpenAt.HasValue != applicationsCloseAt.HasValue)
        {
            throw new ArgumentException("Both application window dates are required.");
        }

        if (applicationsOpenAt.HasValue &&
            (applicationsCloseAt <= applicationsOpenAt || applicationsCloseAt > startsAt))
        {
            throw new InvalidOperationException("Application window must close after opening and before the event starts.");
        }

        Name = name.Trim();
        Description = description?.Trim();
        StartsAt = startsAt;
        EndsAt = endsAt;
        ApplicationsOpenAt = applicationsOpenAt;
        ApplicationsCloseAt = applicationsCloseAt;
        Touch();
    }

    public void OpenApplications() => Transition(LaunchPadEventStatus.Draft, LaunchPadEventStatus.ApplicationsOpen);
    public void CloseApplications() => Transition(LaunchPadEventStatus.ApplicationsOpen, LaunchPadEventStatus.ApplicationsClosed);
    public void Schedule() => Transition(LaunchPadEventStatus.ApplicationsClosed, LaunchPadEventStatus.Scheduled);
    public void Activate() => Transition(LaunchPadEventStatus.Scheduled, LaunchPadEventStatus.Active);
    public void Complete() => Transition(LaunchPadEventStatus.Active, LaunchPadEventStatus.Completed);

    public void Cancel()
    {
        if (Status is LaunchPadEventStatus.Completed or LaunchPadEventStatus.Archived)
        {
            throw new InvalidOperationException("A completed or archived event cannot be cancelled.");
        }

        Status = LaunchPadEventStatus.Cancelled;
        Touch();
    }

    public void Archive()
    {
        if (Status is not (LaunchPadEventStatus.Completed or LaunchPadEventStatus.Cancelled))
        {
            throw new InvalidOperationException("Only completed or cancelled events can be archived.");
        }

        Status = LaunchPadEventStatus.Archived;
        Touch();
    }

    private void Transition(LaunchPadEventStatus expected, LaunchPadEventStatus next)
    {
        EnsureState(expected);
        Status = next;
        Touch();
    }

    private void EnsureState(LaunchPadEventStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"Expected event status {expected}, but was {Status}.");
        }
    }
}

[Table("launch_pad_applications")]
[Index(nameof(LaunchPadEventId), nameof(ProjectId), IsUnique = true)]
public sealed class LaunchPadApplication : EntityBase<Guid>
{
    public Guid LaunchPadEventId { get; private set; }
    public LaunchPadEvent LaunchPadEvent { get; private set; } = null!;
    public Guid ProjectId { get; private set; }
    public Project Project { get; private set; } = null!;
    public Guid ProjectVersionId { get; private set; }
    public ProjectVersion ProjectVersion { get; private set; } = null!;
    public Guid SubmittedByUserId { get; private set; }
    public User SubmittedByUser { get; private set; } = null!;
    public Guid? ReviewedByUserId { get; private set; }
    public DateTime SubmittedAt { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public LaunchPadApplicationStatus Status { get; private set; } = LaunchPadApplicationStatus.Submitted;
    public VersionSubmissionPolicy SubmissionVersionPolicy { get; private set; } = VersionSubmissionPolicy.ReleasedImmutable;

    [MaxLength(2000)]
    public string? Pitch { get; private set; }

    [MaxLength(10000)]
    public string? SubmittedAssetReferenceIdsJson { get; private set; }

    [NotMapped]
    public IReadOnlyList<Guid> SubmittedAssetReferenceIds => string.IsNullOrWhiteSpace(SubmittedAssetReferenceIdsJson)
        ? []
        : JsonSerializer.Deserialize<Guid[]>(SubmittedAssetReferenceIdsJson) ?? [];

    private LaunchPadApplication() { }

    public static LaunchPadApplication Submit(
        Guid tenantId,
        Guid launchPadEventId,
        Guid projectId,
        Guid projectVersionId,
        Guid submittedByUserId,
        string? pitch,
        IReadOnlyCollection<Guid>? submittedAssetReferenceIds = null,
        VersionSubmissionPolicy submissionVersionPolicy = VersionSubmissionPolicy.ReleasedImmutable)
    {
        if (new[] { tenantId, launchPadEventId, projectId, projectVersionId, submittedByUserId }.Any(id => id == Guid.Empty))
        {
            throw new ArgumentException("Tenant, event, project, version and submitter are required.");
        }

        return new LaunchPadApplication
        {
            Id = Guid.NewGuid(), TenantId = tenantId, LaunchPadEventId = launchPadEventId,
            ProjectId = projectId, ProjectVersionId = projectVersionId,
            SubmittedByUserId = submittedByUserId, SubmittedAt = SystemClock.UtcNow,
            Pitch = pitch?.Trim(),
            SubmittedAssetReferenceIdsJson = SerializeAssetIds(submittedAssetReferenceIds),
            Status = LaunchPadApplicationStatus.Submitted,
            SubmissionVersionPolicy = submissionVersionPolicy
        };
    }

    private static string? SerializeAssetIds(IReadOnlyCollection<Guid>? assetReferenceIds)
    {
        var normalized = assetReferenceIds?.Where(id => id != Guid.Empty).Distinct().Take(100).ToArray();
        return normalized is { Length: > 0 } ? JsonSerializer.Serialize(normalized) : null;
    }

    public void Update(Guid projectVersionId, string? pitch, IReadOnlyCollection<Guid>? submittedAssetReferenceIds = null)
    {
        if (Status != LaunchPadApplicationStatus.Submitted)
        {
            throw new InvalidOperationException("The application can no longer be edited.");
        }

        if (projectVersionId != ProjectVersionId &&
            !ProjectVersionEligibility.CanReplaceAfterSubmission(SubmissionVersionPolicy))
        {
            throw new InvalidOperationException("The submitted project version is immutable under this application's policy.");
        }

        ProjectVersionId = projectVersionId;
        Pitch = pitch?.Trim();
        if (submittedAssetReferenceIds != null)
        {
            SubmittedAssetReferenceIdsJson = SerializeAssetIds(submittedAssetReferenceIds);
        }

        Touch();
    }

    public void StartReview() => Transition(LaunchPadApplicationStatus.Submitted, LaunchPadApplicationStatus.UnderReview);
    public void Waitlist(Guid reviewerId) => ReviewTransition(LaunchPadApplicationStatus.Waitlisted, reviewerId);
    public void Approve(Guid reviewerId) => ReviewTransition(LaunchPadApplicationStatus.Approved, reviewerId);
    public void Reject(Guid reviewerId) => ReviewTransition(LaunchPadApplicationStatus.Rejected, reviewerId);

    public void Withdraw()
    {
        if (Status is LaunchPadApplicationStatus.Approved or LaunchPadApplicationStatus.Rejected or LaunchPadApplicationStatus.Withdrawn)
        {
            throw new InvalidOperationException("The application cannot be withdrawn.");
        }

        Status = LaunchPadApplicationStatus.Withdrawn;
        Touch();
    }

    private void ReviewTransition(LaunchPadApplicationStatus next, Guid reviewerId)
    {
        if (Status is not (LaunchPadApplicationStatus.Submitted or LaunchPadApplicationStatus.UnderReview or LaunchPadApplicationStatus.Waitlisted))
        {
            throw new InvalidOperationException("The application is not reviewable.");
        }

        Status = next;
        ReviewedByUserId = reviewerId;
        ReviewedAt = SystemClock.UtcNow;
        Touch();
    }

    private void Transition(LaunchPadApplicationStatus expected, LaunchPadApplicationStatus next)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"Expected application status {expected}, but was {Status}.");
        }

        Status = next;
        Touch();
    }
}

[Table("launch_pad_participant_slots")]
[Index(nameof(LaunchPadEventId), nameof(Role))]
public sealed class LaunchPadParticipantSlot : EntityBase<Guid>
{
    public Guid LaunchPadEventId { get; private set; }
    public LaunchPadEvent LaunchPadEvent { get; private set; } = null!;
    [Required, MaxLength(120)] public string Name { get; private set; } = string.Empty;
    public LaunchPadParticipantRole Role { get; private set; }
    public int Capacity { get; private set; }
    public int ReservedCount { get; private set; }
    public DateTime StartsAt { get; private set; }
    public DateTime EndsAt { get; private set; }
    public ICollection<LaunchPadParticipantRegistration> Registrations { get; private set; } = new List<LaunchPadParticipantRegistration>();

    private LaunchPadParticipantSlot() { }

    public static LaunchPadParticipantSlot Create(Guid tenantId, Guid eventId, string name, LaunchPadParticipantRole role, int capacity, DateTime startsAt, DateTime endsAt)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        if (endsAt <= startsAt)
        {
            throw new ArgumentException("Slot end must be after start.");
        }

        return new LaunchPadParticipantSlot
        {
            Id = Guid.NewGuid(), TenantId = tenantId, LaunchPadEventId = eventId, Name = name.Trim(),
            Role = role, Capacity = capacity, StartsAt = startsAt, EndsAt = endsAt
        };
    }

    public bool HasCapacity => ReservedCount < Capacity;

    public void Update(string name, LaunchPadParticipantRole role, int capacity, DateTime startsAt, DateTime endsAt)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        if (capacity <= 0 || capacity < ReservedCount)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        if (endsAt <= startsAt)
        {
            throw new ArgumentException("Slot end must be after start.");
        }

        Name = name.Trim();
        Role = role;
        Capacity = capacity;
        StartsAt = startsAt;
        EndsAt = endsAt;
        Touch();
    }

    public void Reserve()
    {
        if (!HasCapacity)
        {
            throw new InvalidOperationException("The participant slot is full.");
        }

        ReservedCount++;
        Touch();
    }

    public void Release()
    {
        if (ReservedCount > 0)
        {
            ReservedCount--;
        }

        Touch();
    }
}

[Table("launch_pad_participant_registrations")]
[Index(nameof(LaunchPadParticipantSlotId), nameof(UserId), IsUnique = true)]
public sealed class LaunchPadParticipantRegistration : EntityBase<Guid>
{
    public Guid LaunchPadParticipantSlotId { get; private set; }
    public LaunchPadParticipantSlot LaunchPadParticipantSlot { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public LaunchPadParticipantStatus Status { get; private set; }
    public DateTime RegisteredAt { get; private set; }
    public DateTime? CheckedInAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private LaunchPadParticipantRegistration() { }

    public static LaunchPadParticipantRegistration Register(Guid tenantId, Guid slotId, Guid userId, bool waitlisted)
        => new()
        {
            Id = Guid.NewGuid(), TenantId = tenantId, LaunchPadParticipantSlotId = slotId, UserId = userId,
            Status = waitlisted ? LaunchPadParticipantStatus.Waitlisted : LaunchPadParticipantStatus.Registered,
            RegisteredAt = SystemClock.UtcNow
        };

    public void Promote()
    {
        Ensure(LaunchPadParticipantStatus.Waitlisted);
        Status = LaunchPadParticipantStatus.Registered;
        Touch();
    }

    public void CheckIn()
    {
        Ensure(LaunchPadParticipantStatus.Registered);
        Status = LaunchPadParticipantStatus.CheckedIn;
        CheckedInAt = SystemClock.UtcNow;
        Touch();
    }

    public void MarkAttended()
    {
        Ensure(LaunchPadParticipantStatus.CheckedIn);
        Status = LaunchPadParticipantStatus.Attended;
        Touch();
    }

    public void Complete()
    {
        Ensure(LaunchPadParticipantStatus.Attended);
        Status = LaunchPadParticipantStatus.Completed;
        CompletedAt = SystemClock.UtcNow;
        Touch();
    }

    public void Cancel()
    {
        if (Status is LaunchPadParticipantStatus.Completed or LaunchPadParticipantStatus.Cancelled or LaunchPadParticipantStatus.NoShow)
        {
            throw new InvalidOperationException("Registration cannot be cancelled.");
        }

        Status = LaunchPadParticipantStatus.Cancelled;
        Touch();
    }

    public void MarkNoShow()
    {
        if (Status is not (LaunchPadParticipantStatus.Registered or LaunchPadParticipantStatus.CheckedIn))
        {
            throw new InvalidOperationException("Registration cannot be marked as no-show.");
        }

        Status = LaunchPadParticipantStatus.NoShow;
        Touch();
    }

    private void Ensure(LaunchPadParticipantStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"Expected participant status {expected}, but was {Status}.");
        }
    }
}
