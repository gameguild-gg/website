using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace GameGuild.Social.Profiles.UnitTests.Controllers;

public sealed class SocialProfilesControllerTests
{
    private readonly Mock<ISender> _sender = new();
    private readonly Mock<ISocialProfileRepository> _profiles = new();
    private readonly Mock<IProfileSkillRepository> _skills = new();
    private readonly Mock<IProfilePortfolioRepository> _portfolio = new();

    [Fact]
    public async Task Upsert_SameUser_DispatchesCommand()
    {
        var userId = Guid.NewGuid();
        var body = new UpdateSocialProfileBody("creator", "Creator");
        _sender.Setup(sender => sender.Send(
                It.Is<UpdateSocialProfileCommand>(command => command.UserId == userId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProfileDto(userId));
        var controller = CreateController(userId);

        var result = await controller.Upsert(userId, body, default);

        result.Result.Should().BeOfType<OkObjectResult>();
        _sender.VerifyAll();
    }

    [Fact]
    public async Task Upsert_OtherUser_ReturnsForbidWithoutDispatch()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.Upsert(Guid.NewGuid(), new UpdateSocialProfileBody("creator", "Creator"), default);

        result.Result.Should().BeOfType<ForbidResult>();
        _sender.Verify(sender => sender.Send(It.IsAny<UpdateSocialProfileCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddSkill_OtherUsersProfile_ReturnsForbidWithoutDispatch()
    {
        var actorId = Guid.NewGuid();
        var profile = new SocialProfile
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Handle = "other",
            DisplayName = "Other"
        };
        _profiles.Setup(repository => repository.GetByIdAsync(profile.Id, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        var controller = CreateController(actorId);

        var result = await controller.AddSkill(profile.Id, new AddProfileSkillBody("C#"), default);

        result.Result.Should().BeOfType<ForbidResult>();
        _sender.Verify(sender => sender.Send(It.IsAny<AddProfileSkillCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private SocialProfilesController CreateController(Guid userId)
    {
        var actors = new Mock<IActorContextAccessor>();
        actors.SetupGet(value => value.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = userId.ToString(),
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            IsAuthenticated = true
        });
        return new SocialProfilesController(_sender.Object, actors.Object, _profiles.Object, _skills.Object, _portfolio.Object);
    }

    private static SocialProfileDto ProfileDto(Guid userId)
        => new(
            Guid.NewGuid(),
            userId,
            "creator",
            "Creator",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            "{}",
            ProfileVisibility.Public,
            ProfileAvailabilityStatus.NotSet,
            true,
            true,
            true,
            null,
            0,
            0,
            0,
            0,
            0,
            [],
            []);
}
