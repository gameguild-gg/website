using GameGuild.TestingLab;

namespace GameGuild.API.Database;

/// <summary>
/// Creates persistent, development-only Testing Lab events for exercising both enrollment paths.
/// </summary>
public static class TestingLabLocalEventSeedDataSeeder
{
    public static readonly Guid GameJamSprintEventId = Guid.Parse("490dc4af-4480-41cb-89a1-41dd5e555d62");
    public static readonly Guid OpenTesterEventId = Guid.Parse("490dc4af-4480-41cb-89a1-41dd5e555d63");
    public static readonly Guid OpenDeveloperEventId = Guid.Parse("490dc4af-4480-41cb-89a1-41dd5e555d64");

    public static async Task SeedAsync(
        ApplicationDbContext context,
        Guid managerUserId,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (managerUserId == Guid.Empty)
        {
            throw new ArgumentException("A local seed event manager is required.", nameof(managerUserId));
        }

        var now = SystemClock.UtcNow;
        var historicalStart = new DateTime(2026, 9, 22, 2, 43, 0, DateTimeKind.Utc);
        var historicalEnd = historicalStart.AddHours(3);
        var testerStart = now.Date.AddDays(7).AddHours(18);
        var developerStart = now.Date.AddDays(21).AddHours(18);

        var seededEvents = new[]
        {
            CreateHistoricalEvent(managerUserId, tenantId, historicalStart, historicalEnd),
            CreateOpenTesterEvent(managerUserId, tenantId, testerStart),
            CreateOpenDeveloperEvent(managerUserId, tenantId, developerStart),
        };

        foreach (var candidate in seededEvents)
        {
            var existing = await context.Set<TestingEvent>()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(item => item.Id == candidate.Id, cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                continue;
            }

            context.Set<TestingEvent>().Add(candidate);
            context.Set<TestingEventSlot>().Add(CreateSlot(candidate, maxTesters: 20, maxProjects: 3));
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static TestingEvent CreateHistoricalEvent(
        Guid managerUserId,
        Guid? tenantId,
        DateTime startsAt,
        DateTime endsAt)
    {
        var eventEntity = CreateEvent(
            GameJamSprintEventId,
            "Game Jam Sprint Playtest",
            "Community playtest of the latest game jam builds.",
            managerUserId,
            tenantId,
            startsAt.AddDays(-14),
            startsAt.AddMinutes(-1),
            startsAt,
            endsAt,
            "Archived local playtest rules are retained for layout review.");
        eventEntity.OpenApplications();
        eventEntity.CloseApplications();
        eventEntity.Schedule();
        eventEntity.Activate();
        eventEntity.Complete();
        return eventEntity;
    }

    private static TestingEvent CreateOpenTesterEvent(Guid managerUserId, Guid? tenantId, DateTime startsAt)
    {
        var eventEntity = CreateEvent(
            OpenTesterEventId,
            "Open tester sign-up",
            "Try a new game build and share useful feedback with its creator.",
            managerUserId,
            tenantId,
            startsAt.AddDays(-30),
            startsAt.AddDays(-1),
            startsAt,
            startsAt.AddHours(3),
            "Join on time, follow the game instructions, and keep unreleased builds private.");
        eventEntity.OpenApplications();
        eventEntity.CloseApplications();
        eventEntity.Schedule();
        return eventEntity;
    }

    private static TestingEvent CreateOpenDeveloperEvent(Guid managerUserId, Guid? tenantId, DateTime startsAt)
    {
        var eventEntity = CreateEvent(
            OpenDeveloperEventId,
            "Open game submissions",
            "Add one of your projects to an upcoming community playtest.",
            managerUserId,
            tenantId,
            SystemClock.UtcNow.AddDays(-1),
            startsAt.AddDays(-1),
            startsAt,
            startsAt.AddHours(3),
            "Submit a project you can represent and provide a playable build for the selected session.");
        eventEntity.OpenApplications();
        return eventEntity;
    }

    private static TestingEvent CreateEvent(
        Guid id,
        string name,
        string description,
        Guid managerUserId,
        Guid? tenantId,
        DateTime applicationsOpenAt,
        DateTime applicationsCloseAt,
        DateTime startsAt,
        DateTime endsAt,
        string testerInstructions)
    {
        var eventEntity = TestingEvent.Create(
            name,
            TestingEventMode.Online,
            managerUserId,
            applicationsOpenAt,
            applicationsCloseAt,
            startsAt,
            endsAt,
            requiresFeedback: true,
            approvalMode: TestingEventApprovalMode.ManagerOnly,
            tenantId,
            description,
            timeZoneId: "UTC");
        eventEntity.Id = id;
        eventEntity.Configure(
            "Be respectful, share clear feedback, and do not publish private playtest material.",
            "Choose a game you own or have permission to submit, then provide a build testers can run.",
            testerInstructions,
            CreateDeveloperQuestionnaire(),
            CreateTesterQuestionnaire());
        return eventEntity;
    }

    private static TestingEventSlot CreateSlot(TestingEvent eventEntity, int maxTesters, int maxProjects) =>
        TestingEventSlot.Create(
            eventEntity.Id,
            eventEntity.Mode,
            eventEntity.StartsAt,
            eventEntity.EndsAt,
            maxTesters,
            maxProjects,
            campusName: null,
            roomName: null,
            meetingUrl: $"http://localhost:3000/testing-lab/events/{eventEntity.Id}",
            eventEntity.TenantId);

    private static QuestionnaireSchema CreateTesterQuestionnaire() => new(
        "Tester sign-up",
        [
            new QuestionnaireQuestion(
                "playtest-platform",
                "Which platform will you use?",
                QuestionnaireQuestionType.SingleChoice,
                Required: true,
                [
                    new QuestionnaireOption("windows", "Windows"),
                    new QuestionnaireOption("macos", "macOS"),
                    new QuestionnaireOption("browser", "Browser"),
                ]),
            new QuestionnaireQuestion(
                "tester-notes",
                "Anything the creator should know before the session?",
                QuestionnaireQuestionType.FreeText,
                Required: false),
        ]);

    private static QuestionnaireSchema CreateDeveloperQuestionnaire() => new(
        "About your game",
        [
            new QuestionnaireQuestion(
                "testing-focus",
                "What should testers focus on?",
                QuestionnaireQuestionType.FreeText,
                Required: true),
            new QuestionnaireQuestion(
                "known-issues",
                "Any known issues or setup notes?",
                QuestionnaireQuestionType.FreeText,
                Required: false),
        ]);
}
