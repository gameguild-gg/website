using GameGuild.Notifications.Services;
using GameGuild.Notifications.UnitTests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameGuild.Notifications.UnitTests.Controllers;

public class NotificationsControllerPreferencesTests
{
    private readonly NotificationsTestDbContext _context = CreateContext();
    private readonly NotificationsController _controller;

    public NotificationsControllerPreferencesTests()
    {
        var actorUserId = Guid.NewGuid();
        _controller = CreateController(new NotificationPreferenceService(new ApplicationDbContextAdapter(_context)), actorUserId);
        ActorUserId = actorUserId;
    }

    private Guid ActorUserId { get; }

    [Fact]
    public async Task SetMutedTypes_Should_Replace_The_Full_Mute_List()
    {
        await SeedPreferencesAsync(ActorUserId, muted: """["Billing","Marketing"]""");

        var actionResult = await _controller.SetMutedTypes(new UpdateMutedTypesRequest(["monthlystatement", "SocialInteraction"]));

        var ok = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<MutedTypesResponse>().Subject;
        response.MutedTypes.Should().BeEquivalentTo(["MonthlyStatement", "SocialInteraction"]);

        var preferences = await GetPreferences(ActorUserId);
        preferences.GetMutedTypeNames().Should().BeEquivalentTo(["MonthlyStatement", "SocialInteraction"]);
    }

    [Fact]
    public async Task SetMutedTypes_With_Empty_List_Should_Clear_Mutes()
    {
        await SeedPreferencesAsync(ActorUserId, muted: """["Billing"]""");

        var actionResult = await _controller.SetMutedTypes(new UpdateMutedTypesRequest([]));

        actionResult.Should().BeOfType<OkObjectResult>();
        var preferences = await GetPreferences(ActorUserId);
        preferences.GetMutedTypeNames().Should().BeEmpty();
        preferences.MutedTypes.Should().BeNull();
    }

    [Fact]
    public async Task SetMutedTypes_Should_Return_400_For_Unknown_Type_Name()
    {
        await SeedPreferencesAsync(ActorUserId, muted: null);

        var actionResult = await _controller.SetMutedTypes(new UpdateMutedTypesRequest(["Billing", "NotAType"]));

        var badRequest = actionResult.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().BeOfType<Error>().Which.Code.Should().Be("Notifications.MutedTypes.UnknownType");
        var preferences = await GetPreferences(ActorUserId);
        preferences.GetMutedTypeNames().Should().BeEmpty();
    }

    [Fact]
    public async Task SetMutedTypes_Should_Return_400_For_Null_List()
    {
        var actionResult = await _controller.SetMutedTypes(new UpdateMutedTypesRequest(null!));

        actionResult.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SetDigestFrequency_Should_Accept_Known_Values_Case_Insensitively()
    {
        await SeedPreferencesAsync(ActorUserId, muted: null);

        var weekly = await _controller.SetDigestFrequency(new UpdateDigestFrequencyRequest("weekly"));
        var cleared = await _controller.SetDigestFrequency(new UpdateDigestFrequencyRequest(null));

        var weeklyResponse = weekly.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<DigestFrequencyResponse>().Subject;
        weeklyResponse.EmailDigestFrequency.Should().Be("Weekly");

        var clearedResponse = cleared.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<DigestFrequencyResponse>().Subject;
        clearedResponse.EmailDigestFrequency.Should().BeNull();

        var preferences = await GetPreferences(ActorUserId);
        preferences.EmailDigestFrequency.Should().BeNull();
    }

    [Fact]
    public async Task SetDigestFrequency_Should_Return_400_For_Unknown_Value()
    {
        var actionResult = await _controller.SetDigestFrequency(new UpdateDigestFrequencyRequest("Hourly"));

        var badRequest = actionResult.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().BeOfType<Error>().Which.Code.Should().Be("Notifications.DigestFrequency.Invalid");
    }

    [Fact]
    public void GetTypesCatalog_Should_List_Every_Enum_Value_Except_Custom_With_Classification()
    {
        var actionResult = _controller.GetTypesCatalog();

        var ok = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var catalog = ok.Value.Should().BeAssignableTo<IEnumerable<NotificationTypeCatalogEntry>>().Subject.ToList();

        var expectedCount = Enum.GetValues<NotificationType>().Length - 1; // Custom excluded
        catalog.Should().HaveCount(expectedCount);
        catalog.Should().NotContain(e => e.Type == "Custom");

        var knownCategories = new[] { "Marketing", "Social", "Learning", "Achievements", "Billing", "Transactional", "System" };
        catalog.Should().OnlyContain(e => knownCategories.Contains(e.Category));

        var transactional = catalog.Where(e => !e.Suppressible).Select(e => e.Type).ToList();
        transactional.Should().BeEquivalentTo(["EmailVerification", "PasswordReset", "MagicLink", "TenantInvite"]);

        var monthlyStatement = catalog.Single(e => e.Type == "MonthlyStatement");
        monthlyStatement.Category.Should().Be("Billing");
        monthlyStatement.Suppressible.Should().BeTrue();
        monthlyStatement.DisplayName.Should().Be("Monthly Statement");

        var courseEnrollment = catalog.Single(e => e.Type == "CourseEnrollment");
        courseEnrollment.Category.Should().Be("Learning");
    }

    private async Task SeedPreferencesAsync(Guid userId, string? muted)
    {
        var preferences = NotificationPreference.CreateDefault(userId);
        if (muted is not null)
        {
            preferences.SetMutedTypes(muted);
        }

        _context.NotificationPreferences.Add(preferences);
        await _context.SaveChangesAsync();
    }

    private async Task<NotificationPreference> GetPreferences(Guid userId)
        => await _context.NotificationPreferences.SingleAsync(p => p.UserId == userId);

    private NotificationsController CreateController(INotificationPreferenceService preferenceService, Guid userId)
    {
        var actorAccessor = new Mock<IActorContextAccessor>();
        actorAccessor.SetupGet(accessor => accessor.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = userId.ToString(),
            TenantId = null,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            AuthScheme = "Bearer",
            IsAuthenticated = true
        });

        var notificationService = new Mock<INotificationService>().Object;
        var handler = new NotificationMutationCommandHandler(notificationService, preferenceService);
        return new NotificationsController(notificationService, actorAccessor.Object, new NotificationCommandTestSender(handler));
    }

    private static NotificationsTestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<NotificationsTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new NotificationsTestDbContext(options);
    }

    private sealed class ApplicationDbContextAdapter(NotificationsTestDbContext inner) : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => inner.Set<T>();

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => inner.SaveChangesAsync(cancellationToken);

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Mock.Of<IDbContextTransaction>());
    }
}
