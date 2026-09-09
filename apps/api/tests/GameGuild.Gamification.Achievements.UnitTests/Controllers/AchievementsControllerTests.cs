using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Reflection;
using Xunit;

namespace GameGuild.Gamification.Achievements.Tests;

public class AchievementsControllerTests
{
    private readonly Mock<IAchievementService> _serviceMock = new();
    private readonly Mock<IActorContextAccessor> _actorMock = new();
    private readonly Mock<ISender> _senderMock = new();
    private readonly AchievementsController _sut;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    public static IEnumerable<object[]> AdminMutationPermissions =>
    [
        [nameof(AchievementsController.CreateAchievement), "achievements:create"],
        [nameof(AchievementsController.UpdateAchievement), "achievements:update"],
        [nameof(AchievementsController.DeleteAchievement), "achievements:delete"],
        [nameof(AchievementsController.AwardAchievement), "achievements:award"]
    ];

    public AchievementsControllerTests()
    {
        var actorContext = new ActorContext
        {
            SubjectId = _userId.ToString(),
            TenantId = _tenantId,
            ActorKind = ActorKind.User,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            IsAuthenticated = true
        };
        _actorMock.Setup(a => a.ActorContext).Returns(actorContext);
        _sut = new AchievementsController(_serviceMock.Object, _actorMock.Object, _senderMock.Object);
    }

    [Theory]
    [MemberData(nameof(AdminMutationPermissions))]
    public void AdminMutationActions_ShouldRequireExplicitAchievementPermission(string actionName, string permissionName)
    {
        var method = typeof(AchievementsController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Single(method => method.Name == actionName);

        method.GetCustomAttributes<RequirePermissionAttribute>(inherit: true)
            .Should()
            .ContainSingle(attribute => attribute.PermissionName == permissionName);
    }

    // ── GetMyAchievements ──

    [Fact]
    public async Task GetMyAchievements_ReturnsOk_WithDtos()
    {
        var achievement = Achievement.Create("Test", "social", "badge", 10, "desc");
        var ua = UserAchievement.Create(_userId, achievement.Id, 10, "ctx");
        ua.GetType().GetProperty(nameof(UserAchievement.Achievement))!.SetValue(ua, achievement);

        _serviceMock.Setup(s => s.GetUserAchievementsAsync(_userId, null, _tenantId))
            .ReturnsAsync(new[] { ua });

        var result = await _sut.GetMyAchievements();

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var items = okResult.Value.Should().BeAssignableTo<IEnumerable<UserAchievementDto>>().Subject;
        items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetMyAchievements_Unauthorized_WhenNoSubject()
    {
        SetupAnonymousActor();
        var result = await _sut.GetMyAchievements();
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    // ── GetMyTotalPoints ──

    [Fact]
    public async Task GetMyTotalPoints_ReturnsOk()
    {
        _serviceMock.Setup(s => s.GetUserTotalPointsAsync(_userId, _tenantId))
            .ReturnsAsync(42);

        var result = await _sut.GetMyTotalPoints();

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetMyTotalPoints_Unauthorized_WhenNoSubject()
    {
        SetupAnonymousActor();
        var result = await _sut.GetMyTotalPoints();
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    // ── GetUnnotifiedAchievements ──

    [Fact]
    public async Task GetUnnotifiedAchievements_ReturnsOk_OnSuccess()
    {
        var ua = UserAchievement.Create(_userId, Guid.NewGuid(), 5);
        _serviceMock.Setup(s => s.GetUnnotifiedAchievementsAsync(_userId, _tenantId))
            .ReturnsAsync(Result.Success(new List<UserAchievement> { ua }));

        var result = await _sut.GetUnnotifiedAchievements();

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetUnnotifiedAchievements_Returns500_OnFailure()
    {
        _serviceMock.Setup(s => s.GetUnnotifiedAchievementsAsync(_userId, _tenantId))
            .ReturnsAsync(Result.Failure<List<UserAchievement>>(Error.Failure("GeneralError", "fail")));

        var result = await _sut.GetUnnotifiedAchievements();

        var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task GetUnnotifiedAchievements_Unauthorized_WhenNoSubject()
    {
        SetupAnonymousActor();
        var result = await _sut.GetUnnotifiedAchievements();
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    // ── MarkAsNotified ──

    [Fact]
    public async Task MarkAsNotified_ReturnsNoContent_OnSuccess()
    {
        var uaId = Guid.NewGuid();
        _senderMock.Setup(s => s.Send(It.Is<MarkAchievementNotifiedCommand>(command => command.UserAchievementId == uaId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await _sut.MarkAsNotified(uaId);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task MarkAsNotified_ReturnsNotFound_WhenErrorCodeNotFound()
    {
        var uaId = Guid.NewGuid();
        _senderMock.Setup(s => s.Send(It.Is<MarkAchievementNotifiedCommand>(command => command.UserAchievementId == uaId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.NotFound("NotFound", "not found")));

        var result = await _sut.MarkAsNotified(uaId);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task MarkAsNotified_Returns500_WhenOtherError()
    {
        var uaId = Guid.NewGuid();
        _senderMock.Setup(s => s.Send(It.Is<MarkAchievementNotifiedCommand>(command => command.UserAchievementId == uaId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Failure("ServerError", "server error")));

        var result = await _sut.MarkAsNotified(uaId);

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(500);
    }

    // ── GetEligibleAchievements ──

    [Fact]
    public async Task GetEligibleAchievements_ReturnsOk_OnSuccess()
    {
        var a = Achievement.Create("Eligible", "cat");
        _serviceMock.Setup(s => s.GetEligibleAchievementsAsync(_userId, _tenantId))
            .ReturnsAsync(Result.Success(new List<Achievement> { a }));

        var result = await _sut.GetEligibleAchievements();

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetEligibleAchievements_Returns500_OnFailure()
    {
        _serviceMock.Setup(s => s.GetEligibleAchievementsAsync(_userId, _tenantId))
            .ReturnsAsync(Result.Failure<List<Achievement>>(Error.Failure("GeneralError", "fail")));

        var result = await _sut.GetEligibleAchievements();

        var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task GetEligibleAchievements_Unauthorized_WhenNoSubject()
    {
        SetupAnonymousActor();
        var result = await _sut.GetEligibleAchievements();
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    // ── GetAchievements ──

    [Fact]
    public async Task GetAchievements_ReturnsOk()
    {
        var a = Achievement.Create("Test", "cat");
        _serviceMock.Setup(s => s.GetAchievementsAsync(null, true, false, _tenantId))
            .ReturnsAsync(new[] { a });

        var result = await _sut.GetAchievements();

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    // ── GetAchievement ──

    [Fact]
    public async Task GetAchievement_ReturnsOk_WhenFound()
    {
        var a = Achievement.Create("Test", "cat");
        _serviceMock.Setup(s => s.GetAchievementByIdAsync(a.Id))
            .ReturnsAsync(a);

        var result = await _sut.GetAchievement(a.Id);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetAchievement_ReturnsNotFound_WhenNull()
    {
        var id = Guid.NewGuid();
        _serviceMock.Setup(s => s.GetAchievementByIdAsync(id))
            .ReturnsAsync((Achievement?)null);

        var result = await _sut.GetAchievement(id);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    // ── CreateAchievement ──

    [Fact]
    public async Task CreateAchievement_ReturnsCreated_OnSuccess()
    {
        var created = Achievement.Create("New", "cat");
        _senderMock.Setup(s => s.Send(It.IsAny<CreateAchievementCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(created));

        var request = new CreateAchievementRequest
        {
            Name = "New",
            Category = "cat",
            Points = 10,
            IconUrl = "/icon.png",
            Color = "#FF0000",
            IsSecret = true,
            IsRepeatable = true,
            DisplayOrder = 5
        };

        var result = await _sut.CreateAchievement(request);

        result.Result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task CreateAchievement_Returns500_OnFailure()
    {
        _senderMock.Setup(s => s.Send(It.IsAny<CreateAchievementCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<Achievement>(Error.Failure("GeneralError", "fail")));

        var request = new CreateAchievementRequest { Name = "New", Points = 10 };

        var result = await _sut.CreateAchievement(request);

        var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(500);
    }

    // ── UpdateAchievement ──

    [Fact]
    public async Task UpdateAchievement_ReturnsOk_OnSuccess()
    {
        var a = Achievement.Create("Old", "cat", "badge", 10);
        _senderMock.Setup(s => s.Send(It.IsAny<UpdateAchievementCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<Achievement>(a));

        var request = new UpdateAchievementRequest
        {
            Name = "Updated",
            Points = 20,
            IsActive = false,
            IsSecret = true,
            IsRepeatable = true,
            DisplayOrder = 3
        };

        var result = await _sut.UpdateAchievement(a.Id, request);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdateAchievement_ReturnsNotFound_WhenMissing()
    {
        var id = Guid.NewGuid();
        _senderMock.Setup(s => s.Send(It.IsAny<UpdateAchievementCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<Achievement>(Error.NotFound("NotFound", "not found")));

        var result = await _sut.UpdateAchievement(id, new UpdateAchievementRequest());

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task UpdateAchievement_Returns500_OnUpdateFailure()
    {
        var a = Achievement.Create("Old", "cat");
        _senderMock.Setup(s => s.Send(It.IsAny<UpdateAchievementCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<Achievement>(Error.Failure("GeneralError", "fail")));

        var result = await _sut.UpdateAchievement(a.Id, new UpdateAchievementRequest { Name = "X" });

        var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(500);
    }

    // ── DeleteAchievement ──

    [Fact]
    public async Task DeleteAchievement_ReturnsNoContent_OnSuccess()
    {
        var id = Guid.NewGuid();
        _senderMock.Setup(s => s.Send(It.Is<DeleteAchievementCommand>(command => command.AchievementId == id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await _sut.DeleteAchievement(id);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteAchievement_ReturnsNotFound_WhenNotFoundError()
    {
        var id = Guid.NewGuid();
        _senderMock.Setup(s => s.Send(It.Is<DeleteAchievementCommand>(command => command.AchievementId == id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.NotFound("NotFound", "not found")));

        var result = await _sut.DeleteAchievement(id);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DeleteAchievement_Returns500_WhenOtherError()
    {
        var id = Guid.NewGuid();
        _senderMock.Setup(s => s.Send(It.Is<DeleteAchievementCommand>(command => command.AchievementId == id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Failure("ServerError", "server error")));

        var result = await _sut.DeleteAchievement(id);

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(500);
    }

    // ── AwardAchievement ──

    [Fact]
    public async Task AwardAchievement_ReturnsOk_OnSuccess()
    {
        var achievementId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var ua = UserAchievement.Create(targetUserId, achievementId, 10);

        _senderMock.Setup(s => s.Send(It.Is<AwardAchievementCommand>(command =>
                command.UserId == targetUserId && command.AchievementId == achievementId && command.Context == "manual"),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<UserAchievement>(ua));

        var request = new AwardAchievementRequest { UserId = targetUserId, Context = "manual" };

        var result = await _sut.AwardAchievement(achievementId, request);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task AwardAchievement_ReturnsNotFound_WhenNotFoundError()
    {
        var achievementId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _senderMock.Setup(s => s.Send(It.IsAny<AwardAchievementCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<UserAchievement>(Error.NotFound("NotFound", "not found")));

        var result = await _sut.AwardAchievement(achievementId, new AwardAchievementRequest { UserId = userId });

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AwardAchievement_ReturnsConflict_WhenAlreadyEarned()
    {
        var achievementId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _senderMock.Setup(s => s.Send(It.IsAny<AwardAchievementCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<UserAchievement>(Error.Conflict("Conflict", "already earned")));

        var result = await _sut.AwardAchievement(achievementId, new AwardAchievementRequest { UserId = userId });

        result.Result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task AwardAchievement_ReturnsBadRequest_WhenValidationError()
    {
        var achievementId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _senderMock.Setup(s => s.Send(It.IsAny<AwardAchievementCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<UserAchievement>(Error.Validation("Validation", "invalid")));

        var result = await _sut.AwardAchievement(achievementId, new AwardAchievementRequest { UserId = userId });

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── Helpers ──

    private void SetupAnonymousActor()
    {
        var anonymousContext = new ActorContext
        {
            SubjectId = "not-a-guid",
            TenantId = _tenantId,
            ActorKind = ActorKind.Anonymous,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            IsAuthenticated = false
        };
        _actorMock.Setup(a => a.ActorContext).Returns(anonymousContext);
    }
}
