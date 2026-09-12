using System.Security.Claims;
using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Users;
using GameGuild.Learning.Courses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace GameGuild.Learning.Courses.UnitTests;

public sealed class ProgramCrudControllerLearnerTests
{
  [Fact]
  public async Task GetMyPrograms_ReturnsCurrentUsersPrograms()
  {
    var userId = Guid.NewGuid();
    var programs = new[]
    {
      new GameGuild.Learning.Courses.Program { Id = Guid.NewGuid(), Title = "Private cohort", Slug = "private-cohort" }
    };
    var service = new Mock<IProgramCrudService>();
    service.Setup(candidate => candidate.GetUserProgramsAsync(userId)).ReturnsAsync(programs);
    var controller = CreateController(service.Object, userId);

    var result = await controller.GetMyPrograms();

    var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
    ok.Value.Should().BeAssignableTo<IEnumerable<ProgramDto>>()
      .Which.Should().ContainSingle(program => program.Title == "Private cohort");
  }

  [Fact]
  public async Task GetMyPrograms_ReturnsUnauthorizedWithoutCurrentUser()
  {
    var service = new Mock<IProgramCrudService>();
    var controller = CreateController(service.Object, null);

    var result = await controller.GetMyPrograms();

    result.Result.Should().BeOfType<UnauthorizedResult>();
    service.Verify(candidate => candidate.GetUserProgramsAsync(It.IsAny<Guid>()), Times.Never);
  }

  [Fact]
  public async Task AddUserToProgramByReference_ResolvesTenantUserAndEnrollsThem()
  {
    var ownerId = Guid.NewGuid();
    var courseId = Guid.NewGuid();
    var studentId = Guid.NewGuid();
    var enrollmentId = Guid.NewGuid();
    var sender = new Mock<ISender>();
    sender
      .Setup(candidate => candidate.Send(
        It.Is<GetUsersQuery>(query => query.Email == "student@example.com" && query.Limit == 5),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(new PagedResult<UserDto>(
        [new UserDto(studentId, "student@example.com", "Student", DateTime.UtcNow, null)],
        1,
        0,
        5));
    sender
      .Setup(candidate => candidate.Send(
        It.Is<AddUserToProgramEndpointCommand>(command =>
          command.ProgramId == courseId && command.UserId == studentId),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(new UserProgressDto(
        enrollmentId,
        courseId,
        studentId,
        0,
        null,
        null,
        null,
        []));
    var controller = CreateController(new Mock<IProgramCrudService>().Object, ownerId, sender.Object);

    var result = await controller.AddUserToProgramByReference(
      courseId,
      new EnrollProgramUserRequest("student@example.com"),
      CancellationToken.None);

    var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
    ok.Value.Should().BeOfType<UserProgressDto>()
      .Which.UserId.Should().Be(studentId);
  }

  private static ProgramCrudController CreateController(
    IProgramCrudService service,
    Guid? userId,
    ISender? sender = null)
  {
    var claims = userId.HasValue
      ? new[] { new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()) }
      : Array.Empty<Claim>();
    var actorAccessor = new Mock<GameGuild.Identity.Context.Actors.IActorContextAccessor>();
    actorAccessor.Setup(a => a.ActorContext).Returns(new GameGuild.Identity.Context.Actors.ActorContext
    {
      ActorKind = GameGuild.Identity.Context.Actors.ActorKind.User,
      IsAuthenticated = userId.HasValue,
      SubjectId = userId?.ToString(),
      Roles = new HashSet<string>(),
      Permissions = new HashSet<string>()
    });
    return new ProgramCrudController(
      service,
      actorAccessor.Object,
      new Mock<GameGuild.Identity.Authorization.IPermissionQueryService>().Object,
      sender ?? Mock.Of<ISender>())
    {
      ControllerContext = new ControllerContext
      {
        HttpContext = new DefaultHttpContext
        {
          User = new ClaimsPrincipal(new ClaimsIdentity(claims, userId.HasValue ? "test" : null))
        }
      }
    };
  }
}
