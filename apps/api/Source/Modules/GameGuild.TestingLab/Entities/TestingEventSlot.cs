namespace GameGuild.TestingLab;

[Table("testing_event_slots")]
public sealed class TestingEventSlot : EntityBase
{
    public Guid EventId { get; private set; }

    public TestingEvent Event { get; private set; } = null!;

    public Guid? LocationId { get; private set; }

    public TestingLocation? Location { get; private set; }

    public TestingEventMode Mode { get; private set; }

    public DateTime StartsAt { get; private set; }

    public DateTime EndsAt { get; private set; }

    public int? MaxTesters { get; private set; }

    public int? MaxProjects { get; private set; }

    [MaxLength(200)]
    public string? CampusName { get; private set; }

    [MaxLength(200)]
    public string? RoomName { get; private set; }

    [MaxLength(1000)]
    public string? MeetingUrl { get; private set; }

    public bool IsTesterCapacityUnlimited => MaxTesters is null;

    public bool IsProjectCapacityUnlimited => MaxProjects is null;

    private TestingEventSlot()
    {
    }

    public static TestingEventSlot Create(
        Guid eventId,
        TestingEventMode mode,
        DateTime startsAt,
        DateTime endsAt,
        int? maxTesters,
        int? maxProjects,
        string? campusName,
        string? roomName,
        string? meetingUrl,
        Guid? tenantId,
        Guid? locationId = null)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("Event is required.", nameof(eventId));
        }

        if (endsAt <= startsAt)
        {
            throw new ArgumentException("Slot must end after it starts.");
        }

        if (maxTesters is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTesters));
        }

        if (maxProjects is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxProjects));
        }

        if (mode == TestingEventMode.InPerson &&
            (string.IsNullOrWhiteSpace(campusName) || string.IsNullOrWhiteSpace(roomName)))
        {
            throw new ArgumentException("In-person slots require campus and room.");
        }

        if (mode == TestingEventMode.Online && string.IsNullOrWhiteSpace(meetingUrl))
        {
            throw new ArgumentException("Online slots require a meeting URL.", nameof(meetingUrl));
        }

        return new TestingEventSlot
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Mode = mode,
            StartsAt = startsAt,
            EndsAt = endsAt,
            MaxTesters = maxTesters,
            MaxProjects = maxProjects,
            CampusName = string.IsNullOrWhiteSpace(campusName) ? null : campusName.Trim(),
            RoomName = string.IsNullOrWhiteSpace(roomName) ? null : roomName.Trim(),
            MeetingUrl = string.IsNullOrWhiteSpace(meetingUrl) ? null : meetingUrl.Trim(),
            LocationId = locationId,
            TenantId = tenantId,
        };
    }

    public void Update(
        TestingEventMode mode,
        DateTime startsAt,
        DateTime endsAt,
        int? maxTesters,
        int? maxProjects,
        string? campusName,
        string? roomName,
        string? meetingUrl,
        Guid? locationId)
    {
        if (endsAt <= startsAt)
        {
            throw new ArgumentException("Slot must end after it starts.");
        }

        if (maxTesters is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTesters));
        }

        if (maxProjects is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxProjects));
        }

        if (mode == TestingEventMode.InPerson &&
            (string.IsNullOrWhiteSpace(campusName) || string.IsNullOrWhiteSpace(roomName)))
        {
            throw new ArgumentException("In-person slots require campus and room.");
        }

        if (mode == TestingEventMode.Online && string.IsNullOrWhiteSpace(meetingUrl))
        {
            throw new ArgumentException("Online slots require a meeting URL.", nameof(meetingUrl));
        }

        Mode = mode;
        StartsAt = startsAt;
        EndsAt = endsAt;
        MaxTesters = maxTesters;
        MaxProjects = maxProjects;
        CampusName = string.IsNullOrWhiteSpace(campusName) ? null : campusName.Trim();
        RoomName = string.IsNullOrWhiteSpace(roomName) ? null : roomName.Trim();
        MeetingUrl = string.IsNullOrWhiteSpace(meetingUrl) ? null : meetingUrl.Trim();
        LocationId = locationId;
        Touch();
    }}
