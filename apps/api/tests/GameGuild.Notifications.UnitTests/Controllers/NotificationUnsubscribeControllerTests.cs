using GameGuild.Notifications.Services.Email;
using GameGuild.Notifications.UnitTests.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameGuild.Notifications.UnitTests.Controllers;

public class NotificationUnsubscribeControllerTests
{
    private readonly NotificationsTestDbContext _context = CreateContext();
    private readonly UnsubscribeTokenService _tokenService = new(new EphemeralDataProtectionProvider());
    private readonly NotificationUnsubscribeController _controller;

    public NotificationUnsubscribeControllerTests()
    {
        var configuration = new Mock<IConfiguration>();
        configuration.SetupGet(c => c["App:BaseUrl"]).Returns("https://app.example.com/");
        _controller = new NotificationUnsubscribeController(new HandlerSender(new UnsubscribeNotificationCommandHandler(
            _tokenService,
            new NotificationPreferenceService(new ApplicationDbContextAdapter(_context)),
            configuration.Object,
            NullLogger<NotificationUnsubscribeController>.Instance)));
    }

    [Fact]
    public async Task Unsubscribe_Type_Scope_Should_Mute_Type_And_Return_Success()
    {
        var userId = Guid.NewGuid();
        var token = _tokenService.Generate(userId, "type", "MonthlyStatement");

        var actionResult = await _controller.Unsubscribe(token);

        var ok = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<UnsubscribeResponse>().Subject;
        response.Status.Should().Be("unsubscribed");
        response.Scope.Should().Be("type");
        response.Value.Should().Be("MonthlyStatement");
        response.ManageUrl.Should().Be("https://app.example.com/workspace/settings/notifications");

        var preferences = await GetPreferences(userId);
        preferences.GetMutedTypeNames().Should().Contain("MonthlyStatement");
    }

    [Fact]
    public async Task Unsubscribe_Category_Scope_Should_Disable_Category_Only()
    {
        var userId = Guid.NewGuid();
        var token = _tokenService.Generate(userId, "category", "marketing");

        var actionResult = await _controller.Unsubscribe(token);

        actionResult.Should().BeOfType<OkObjectResult>();
        var preferences = await GetPreferences(userId);
        preferences.MarketingEnabled.Should().BeFalse();
        preferences.SocialEnabled.Should().BeTrue();
        preferences.LearningEnabled.Should().BeTrue();
        preferences.AchievementsEnabled.Should().BeTrue();
        preferences.EmailEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Unsubscribe_All_Scope_Should_Disable_Email()
    {
        var userId = Guid.NewGuid();
        var token = _tokenService.Generate(userId, "all", null);

        var actionResult = await _controller.Unsubscribe(token);

        actionResult.Should().BeOfType<OkObjectResult>();
        var preferences = await GetPreferences(userId);
        preferences.EmailEnabled.Should().BeFalse();
        preferences.PushEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Unsubscribe_Should_Be_Idempotent_On_Repeated_Clicks()
    {
        var userId = Guid.NewGuid();
        var token = _tokenService.Generate(userId, "type", "Billing");

        var first = await _controller.Unsubscribe(token);
        var second = await _controller.Unsubscribe(token);

        first.Should().BeOfType<OkObjectResult>();
        second.Should().BeOfType<OkObjectResult>();
        var preferences = await GetPreferences(userId);
        preferences.GetMutedTypeNames().Should().HaveCount(1).And.Contain("Billing");
    }

    [Theory]
    [InlineData("EmailVerification")]
    [InlineData("PasswordReset")]
    [InlineData("MagicLink")]
    [InlineData("TenantInvite")]
    public async Task Unsubscribe_Should_Reject_Transactional_Types_Without_State_Change(string transactionalType)
    {
        var userId = Guid.NewGuid();
        var token = _tokenService.Generate(userId, "type", transactionalType);

        var actionResult = await _controller.Unsubscribe(token);

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Detail.Should().MatchEquivalentOf("*cannot unsubscribe from transactional emails*");
        _context.NotificationPreferences.Should().BeEmpty();
    }

    [Fact]
    public async Task Unsubscribe_Should_Reject_Unknown_Type_In_Signed_Token()
    {
        var token = _tokenService.Generate(Guid.NewGuid(), "type", "NotARealType");

        var actionResult = await _controller.Unsubscribe(token);

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task Unsubscribe_Should_Reject_Unknown_Category_In_Signed_Token()
    {
        var token = _tokenService.Generate(Guid.NewGuid(), "category", "nope");

        var actionResult = await _controller.Unsubscribe(token);

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("!!!")]
    public async Task Unsubscribe_Should_Return_400_For_Invalid_Token_Without_Creating_State(string badToken)
    {
        var actionResult = await _controller.Unsubscribe(badToken);

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Detail.Should().NotContain("user");
        _context.NotificationPreferences.Should().BeEmpty();
    }

    [Fact]
    public async Task Unsubscribe_Should_Return_400_For_Missing_Token()
    {
        var actionResult = await _controller.Unsubscribe(null);

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Unsubscribe_Should_Return_400_For_Tampered_Token()
    {
        var token = _tokenService.Generate(Guid.NewGuid(), "all", null);
        var tampered = token[..^1] + (token[^1] == 'A' ? 'B' : 'A');

        var actionResult = await _controller.Unsubscribe(tampered);

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    private async Task<NotificationPreference> GetPreferences(Guid userId)
    {
        var preferences = await _context.NotificationPreferences.SingleAsync(p => p.UserId == userId);
        return preferences;
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
