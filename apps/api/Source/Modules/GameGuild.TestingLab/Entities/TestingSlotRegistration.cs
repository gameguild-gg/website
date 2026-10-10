using GameGuild.Identity.Users;

namespace GameGuild.TestingLab;

[Table("testing_slot_registrations")]
public sealed class TestingSlotRegistration : EntityBase
{
    public Guid EventId { get; private set; }

    public TestingEvent Event { get; private set; } = null!;

    public Guid SlotId { get; private set; }

    public TestingEventSlot Slot { get; private set; } = null!;

    public Guid UserId { get; private set; }

    public User User { get; private set; } = null!;

    public TestingSlotRegistrationStatus Status { get; private set; }

    public int? WaitlistPosition { get; private set; }

    [MaxLength(1000)]
    public string? Notes { get; private set; }

    public string? RegistrationResponseJson { get; private set; }

    [NotMapped]
    public QuestionnaireResponse? RegistrationResponse => string.IsNullOrWhiteSpace(RegistrationResponseJson)
        ? null
        : QuestionnaireResponse.FromJson(RegistrationResponseJson);

    public DateTime? RulesAcceptedAt { get; private set; }

    public DateTime? EventConfigurationFrozenAt { get; private set; }

    public DateTime RegisteredAt { get; private set; }

    public DateTime? PromotedAt { get; private set; }

    public DateTime? CheckedInAt { get; private set; }

    public DateTime? CheckedOutAt { get; private set; }

    public DateTime? CompletedAt { get; private set; }

    public bool ConsumesCapacity => Status is
        TestingSlotRegistrationStatus.Registered or
        TestingSlotRegistrationStatus.CheckedIn or
        TestingSlotRegistrationStatus.Attended or
        TestingSlotRegistrationStatus.Completed or
        TestingSlotRegistrationStatus.NoShow;

    private TestingSlotRegistration()
    {
    }

    public static TestingSlotRegistration Register(
        Guid eventId,
        Guid slotId,
        Guid userId,
        string? notes,
        Guid? tenantId)
        => Register(eventId, slotId, userId, notes, tenantId, new QuestionnaireResponse([]),
            SystemClock.UtcNow, SystemClock.UtcNow);

    public static TestingSlotRegistration Register(
        Guid eventId,
        Guid slotId,
        Guid userId,
        string? notes,
        Guid? tenantId,
        QuestionnaireResponse registrationResponse,
        DateTime rulesAcceptedAt,
        DateTime eventConfigurationFrozenAt)
        => Create(eventId, slotId, userId, TestingSlotRegistrationStatus.Registered, null, notes, tenantId,
            registrationResponse, rulesAcceptedAt, eventConfigurationFrozenAt);

    public static TestingSlotRegistration Waitlist(
        Guid eventId,
        Guid slotId,
        Guid userId,
        int position,
        string? notes,
        Guid? tenantId)
        => Waitlist(eventId, slotId, userId, position, notes, tenantId, new QuestionnaireResponse([]),
            SystemClock.UtcNow, SystemClock.UtcNow);

    public static TestingSlotRegistration Waitlist(
        Guid eventId,
        Guid slotId,
        Guid userId,
        int position,
        string? notes,
        Guid? tenantId,
        QuestionnaireResponse registrationResponse,
        DateTime rulesAcceptedAt,
        DateTime eventConfigurationFrozenAt)
    {
        if (position <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        return Create(eventId, slotId, userId, TestingSlotRegistrationStatus.Waitlisted, position, notes, tenantId,
            registrationResponse, rulesAcceptedAt, eventConfigurationFrozenAt);
    }

    public void Promote()
    {
        if (Status != TestingSlotRegistrationStatus.Waitlisted)
        {
            throw new InvalidOperationException("Only waitlisted registrations can be promoted.");
        }

        Status = TestingSlotRegistrationStatus.Registered;
        WaitlistPosition = null;
        PromotedAt = SystemClock.UtcNow;
        Touch();
    }

    public void Reposition(int position)
    {
        if (Status != TestingSlotRegistrationStatus.Waitlisted)
        {
            throw new InvalidOperationException("Only waitlisted registrations can be repositioned.");
        }

        if (position <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        WaitlistPosition = position;
        Touch();
    }

    public void Cancel(Guid actorUserId, bool managerOverride)
    {
        if (!managerOverride && actorUserId != UserId)
        {
            throw new UnauthorizedAccessException("Only the tester or event manager can cancel this registration.");
        }

        if (Status is TestingSlotRegistrationStatus.Cancelled or TestingSlotRegistrationStatus.Completed)
        {
            throw new InvalidOperationException("This registration can no longer be cancelled.");
        }

        Status = TestingSlotRegistrationStatus.Cancelled;
        WaitlistPosition = null;
        Touch();
    }

    public void CheckIn()
    {
        if (Status != TestingSlotRegistrationStatus.Registered)
        {
            throw new InvalidOperationException("Only registered testers can check in.");
        }

        Status = TestingSlotRegistrationStatus.CheckedIn;
        CheckedInAt = SystemClock.UtcNow;
        Touch();
    }

    public void CheckOut()
    {
        if (Status != TestingSlotRegistrationStatus.CheckedIn)
        {
            throw new InvalidOperationException("Only checked-in testers can check out.");
        }

        Status = TestingSlotRegistrationStatus.Attended;
        CheckedOutAt = SystemClock.UtcNow;
        Touch();
    }

    public void MarkNoShow()
    {
        if (Status != TestingSlotRegistrationStatus.Registered)
        {
            throw new InvalidOperationException("Only registered testers can be marked as no-show.");
        }

        Status = TestingSlotRegistrationStatus.NoShow;
        Touch();
    }

    public void Complete()
    {
        if (Status != TestingSlotRegistrationStatus.Attended)
        {
            throw new InvalidOperationException("Only attended registrations can be completed.");
        }

        Status = TestingSlotRegistrationStatus.Completed;
        CompletedAt = SystemClock.UtcNow;
        Touch();
    }

    private static TestingSlotRegistration Create(
        Guid eventId,
        Guid slotId,
        Guid userId,
        TestingSlotRegistrationStatus status,
        int? waitlistPosition,
        string? notes,
        Guid? tenantId,
        QuestionnaireResponse registrationResponse,
        DateTime rulesAcceptedAt,
        DateTime eventConfigurationFrozenAt)
    {
        if (eventId == Guid.Empty || slotId == Guid.Empty || userId == Guid.Empty)
        {
            throw new ArgumentException("Event, slot, and user are required.");
        }

        return new TestingSlotRegistration
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            SlotId = slotId,
            UserId = userId,
            Status = status,
            WaitlistPosition = waitlistPosition,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            RegistrationResponseJson = registrationResponse.ToJson(),
            RulesAcceptedAt = rulesAcceptedAt,
            EventConfigurationFrozenAt = eventConfigurationFrozenAt,
            RegisteredAt = SystemClock.UtcNow,
            TenantId = tenantId,
        };
    }
}
