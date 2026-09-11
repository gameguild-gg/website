using System.Security.Claims;
using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace GameGuild.Learning.Courses.UnitTests;

/// <summary>
/// DAC scoping for GET /courses (workspace catalog): manage-capable actors see the full
/// catalog, regular creators only their own courses. Mirrors AssessmentsController
/// permission-name conventions (Program.Manage content-type grant / SystemAdmin role).
/// </summary>
public sealed class ProgramCrudControllerCatalogScopeTests
{
  private readonly Guid _actorId = Guid.NewGuid();
  private readonly Guid _tenantId = Guid.NewGuid();
  private readonly Guid _otherCreatorId = Guid.NewGuid();

  private readonly Mock<IProgramCrudService> _service = new();
  private readonly Mock<IPermissionQueryService> _permissions = new();

  [Fact]
  public async Task GetPrograms_SystemAdmin_ReturnsFullCatalog()
  {
    var catalog = MixedCatalog();
    _service.Setup(s => s.GetProgramsAsync(0, 50)).ReturnsAsync(catalog);
    var controller = CreateController(isSystemAdmin: true);

    var result = await controller.GetPrograms();

    var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
    var dtos = ok.Value.Should().BeAssignableTo<IEnumerable<ProgramDto>>().Subject.ToList();
    dtos.Should().HaveCount(5);
    _service.Verify(s => s.GetProgramsAsync(0, 50), Times.Once);
    _permissions.Verify(
      p => p.HasTenantPermissionAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  [Fact]
  public async Task GetPrograms_PlainCreator_ReturnsOnlyOwnCourses()
  {
    var ownCourses = MixedCatalog().Where(p => p.CreatorId == _actorId).ToList();
    _service.Setup(s => s.GetProgramsByCreatorAsync(_actorId, 0, 50)).ReturnsAsync(ownCourses);
    var controller = CreateController(isSystemAdmin: false);

    var result = await controller.GetPrograms();

    var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
    var dtos = ok.Value.Should().BeAssignableTo<IEnumerable<ProgramDto>>().Subject.ToList();
    dtos.Should().HaveCount(2);
    dtos.Should().OnlyContain(dto => dto.CreatorId == _actorId);
    _service.Verify(s => s.GetProgramsAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
  }

  [Fact]
  public async Task GetPrograms_WithProgramManageGrant_ReturnsFullCatalog()
  {
    var catalog = MixedCatalog();
    _service.Setup(s => s.GetProgramsAsync(0, 50)).ReturnsAsync(catalog);
    _permissions
      .Setup(p => p.HasTenantPermissionAsync(_actorId, _tenantId, "Program.Manage", It.IsAny<CancellationToken>()))
      .ReturnsAsync(true);
    var controller = CreateController(isSystemAdmin: false);

    var result = await controller.GetPrograms();

    var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
    var dtos = ok.Value.Should().BeAssignableTo<IEnumerable<ProgramDto>>().Subject.ToList();
    dtos.Should().HaveCount(5);
    _permissions.Verify(
      p => p.HasTenantPermissionAsync(_actorId, _tenantId, "Program.Manage", It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Fact]
  public async Task GetPrograms_NonManageActorProbingOtherCreator_GetsOwnCoursesOnly()
  {
    var ownCourses = MixedCatalog().Where(p => p.CreatorId == _actorId).ToList();
    _service.Setup(s => s.GetProgramsByCreatorAsync(_actorId, 0, 50)).ReturnsAsync(ownCourses);
    var controller = CreateController(isSystemAdmin: false);

    var result = await controller.GetPrograms(creatorId: _otherCreatorId);

    var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
    var dtos = ok.Value.Should().BeAssignableTo<IEnumerable<ProgramDto>>().Subject.ToList();
    dtos.Should().HaveCount(2);
    dtos.Should().OnlyContain(dto => dto.CreatorId == _actorId);
    _service.Verify(s => s.GetProgramsByCreatorAsync(_otherCreatorId, It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    _service.Verify(s => s.GetProgramsByCreatorAsync(_actorId, 0, 50), Times.Once);
  }

  [Fact]
  public async Task GetPrograms_NonManageActorSearching_GetsOnlyOwnSubset()
  {
    _service.Setup(s => s.SearchProgramsAsync("course", 0, 50)).ReturnsAsync(MixedCatalog());
    var controller = CreateController(isSystemAdmin: false);

    var result = await controller.GetPrograms(q: "course");

    var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
    var dtos = ok.Value.Should().BeAssignableTo<IEnumerable<ProgramDto>>().Subject.ToList();
    dtos.Should().HaveCount(2);
    dtos.Should().OnlyContain(dto => dto.CreatorId == _actorId);
  }

  // ── fixtures ──────────────────────────────────────────────────────────────────

  private List<Program> MixedCatalog() => new()
  {
    new Program { Id = Guid.NewGuid(), Title = "Mine 1", Slug = "mine-1", CreatorId = _actorId },
    new Program { Id = Guid.NewGuid(), Title = "Theirs 1", Slug = "theirs-1", CreatorId = _otherCreatorId },
    new Program { Id = Guid.NewGuid(), Title = "Mine 2", Slug = "mine-2", CreatorId = _actorId },
    new Program { Id = Guid.NewGuid(), Title = "Theirs 2", Slug = "theirs-2", CreatorId = _otherCreatorId },
    new Program { Id = Guid.NewGuid(), Title = "Theirs 3", Slug = "theirs-3", CreatorId = Guid.NewGuid() },
  };

  private ProgramCrudController CreateController(bool isSystemAdmin)
  {
    var actor = new ActorContext
    {
      ActorKind = ActorKind.User,
      IsAuthenticated = true,
      SubjectId = _actorId.ToString(),
      TenantId = _tenantId,
      Roles = new HashSet<string>(isSystemAdmin ? new[] { "SystemAdmin" } : Array.Empty<string>()),
      Permissions = new HashSet<string>()
    };
    var actorAccessor = new Mock<IActorContextAccessor>();
    actorAccessor.Setup(a => a.ActorContext).Returns(actor);

    var controller = new ProgramCrudController(_service.Object, actorAccessor.Object, _permissions.Object, Mock.Of<ISender>())
    {
      ControllerContext = new ControllerContext
      {
        HttpContext = new DefaultHttpContext
        {
          User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, _actorId.ToString()) }, "test"))
        }
      }
    };
    return controller;
  }
}
