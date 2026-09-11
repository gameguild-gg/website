using System.Security.Claims;
using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Learning.Courses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace GameGuild.Learning.Courses.UnitTests;

public sealed class ProgramContentVisibilityTests
{
    private readonly Guid programId = Guid.NewGuid();
    private readonly Guid privateItemId = Guid.NewGuid();
    private readonly Guid privateChildId = Guid.NewGuid();
    private readonly Guid publicItemId = Guid.NewGuid();
    private readonly Guid creatorId = Guid.NewGuid();
    private readonly Guid viewerId = Guid.NewGuid();

    private ProgramContent BuildContent()
    {
        var privateItem = new ProgramContent
        {
            Id = privateItemId,
            ProgramId = programId,
            Title = "Private report",
            Type = ProgramContentType.Lesson,
            Visibility = Visibility.Private,
            Body = "secret body",
            Version = 1,
            Children = new List<ProgramContent>
            {
                new()
                {
                    Id = privateChildId,
                    ProgramId = programId,
                    Title = "Public child of private parent",
                    Type = ProgramContentType.Lesson,
                    Visibility = Visibility.Public,
                    Body = "child body",
                    Version = 1,
                },
            },
        };
        var publicItem = new ProgramContent
        {
            Id = publicItemId,
            ProgramId = programId,
            Title = "Public lesson",
            Type = ProgramContentType.Lesson,
            Visibility = Visibility.Public,
            Body = "public body",
            Version = 1,
        };

        return new ProgramContent
        {
            Id = Guid.NewGuid(),
            ProgramId = programId,
            Title = "Module",
            Type = ProgramContentType.Module,
            Visibility = Visibility.Public,
            Version = 1,
            Children = new List<ProgramContent> { privateItem, publicItem },
        };
    }

    private ProgramContentController BuildController(
        Mock<IProgramCrudService> programMock,
        Mock<IProgramContentService> contentMock,
        Guid? userId,
        bool enrolled,
        bool manager,
        Program? program = null)
    {
        program ??= new Program
        {
            Id = programId,
            CreatorId = creatorId,
            Title = "Course",
            Status = ContentStatus.Published,
            Visibility = ContentVisibility.Public,
            Version = 1,
        };

        programMock.Setup(s => s.GetProgramByIdAsync(programId)).ReturnsAsync(program);
        var authorizationMock = new Mock<IAuthorizationService>();
        authorizationMock
            .Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<object>(),
                It.IsAny<string>()))
            .ReturnsAsync((ClaimsPrincipal _, object resource, string policy) =>
            {
                var course = (Program)resource;
                var authorized = policy switch
                {
                    Policies.CourseContentViewAll => manager || userId.HasValue && course.CreatorId == userId,
                    Policies.CourseContentLearner => enrolled,
                    Policies.CourseContentPublicOutline => course.Status == ContentStatus.Published
                                                           && course.Visibility == ContentVisibility.Public,
                    _ => false
                };

                return authorized ? AuthorizationResult.Success() : AuthorizationResult.Failed();
            });

        var controller = new ProgramContentController(
            contentMock.Object,
            programMock.Object,
            new Mock<ICodingAssignmentContentService>().Object,
            authorizationMock.Object,
            Mock.Of<ISender>());

        if (userId.HasValue)
        {
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()),
                    })),
                },
            };
        }
        else
        {
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity()),
                },
            };
        }

        return controller;
    }

    private Mock<IProgramContentService> SetupContent()
    {
        var contentMock = new Mock<IProgramContentService>();
        var root = BuildContent();
        contentMock.Setup(s => s.GetContentByProgramAsync(programId)).ReturnsAsync(new[] { root });
        contentMock.Setup(s => s.GetContentByIdAsync(privateItemId)).ReturnsAsync(root.Children.First(c => c.Id == privateItemId));
        contentMock.Setup(s => s.GetContentByIdAsync(publicItemId)).ReturnsAsync(root.Children.First(c => c.Id == publicItemId));
        return contentMock;
    }

    [Fact]
    public async Task GetProgramContent_EnrolledStudent_ExcludesPrivateSubtreeAndKeepsBodies()
    {
        var programMock = new Mock<IProgramCrudService>();
        var contentMock = SetupContent();
        var controller = BuildController(programMock, contentMock, viewerId, enrolled: true, manager: false);

        var result = await controller.GetProgramContent(programId);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var items = ok.Value.Should().BeAssignableTo<IEnumerable<ProgramContentDto>>().Subject.ToList();
        var module = items.Single();
        module.Title.Should().Be("Module");
        module.Children.Select(c => c.Title).Should().ContainSingle().Which.Should().Be("Public lesson");
        module.Children.Single().Body.Should().Be("public body");
    }

    [Fact]
    public async Task GetProgramContent_Manager_SeesPrivateContent()
    {
        var programMock = new Mock<IProgramCrudService>();
        var contentMock = SetupContent();
        var controller = BuildController(programMock, contentMock, viewerId, enrolled: false, manager: true);

        var result = await controller.GetProgramContent(programId);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var items = ok.Value.Should().BeAssignableTo<IEnumerable<ProgramContentDto>>().Subject.ToList();
        var module = items.Single();
        module.Children.Select(c => c.Title).Should().Contain("Private report").And.Contain("Public lesson");
        module.Children.Single(c => c.Title == "Private report").Children.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetProgramContent_Creator_SeesPrivateContent()
    {
        var programMock = new Mock<IProgramCrudService>();
        var contentMock = SetupContent();
        var controller = BuildController(programMock, contentMock, creatorId, enrolled: false, manager: false);

        var result = await controller.GetProgramContent(programId);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var module = ok.Value.Should().BeAssignableTo<IEnumerable<ProgramContentDto>>().Subject.ToList().Single();
        module.Children.Select(c => c.Title).Should().Contain("Private report");
    }

    [Fact]
    public async Task GetProgramContent_Anonymous_PublishedPublicCourse_GetsPublicOutlineOnly()
    {
        var programMock = new Mock<IProgramCrudService>();
        var contentMock = SetupContent();
        var controller = BuildController(programMock, contentMock, userId: null, enrolled: false, manager: false);

        var result = await controller.GetProgramContent(programId);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var module = ok.Value.Should().BeAssignableTo<IEnumerable<ProgramContentDto>>().Subject.ToList().Single();
        module.Children.Select(c => c.Title).Should().ContainSingle().Which.Should().Be("Public lesson");
        module.Children.Single().Body.Should().BeNull();
    }

    [Fact]
    public async Task GetContent_Student_PrivateItem_ReturnsNotFound()
    {
        var programMock = new Mock<IProgramCrudService>();
        var contentMock = SetupContent();
        var controller = BuildController(programMock, contentMock, viewerId, enrolled: true, manager: false);

        var result = await controller.GetContent(programId, privateItemId);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetContent_Student_PublicItem_ReturnsBody()
    {
        var programMock = new Mock<IProgramCrudService>();
        var contentMock = SetupContent();
        var controller = BuildController(programMock, contentMock, viewerId, enrolled: true, manager: false);

        var result = await controller.GetContent(programId, publicItemId);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<ProgramContentDto>().Which.Body.Should().Be("public body");
    }

    [Fact]
    public async Task GetContentByVisibility_Student_RequestingPrivate_ReturnsEmpty()
    {
        var programMock = new Mock<IProgramCrudService>();
        var contentMock = SetupContent();
        var controller = BuildController(programMock, contentMock, viewerId, enrolled: true, manager: false);

        var result = await controller.GetContentByVisibility(programId, Visibility.Private);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<IEnumerable<ProgramContentDto>>().Which.Should().BeEmpty();
        contentMock.Verify(s => s.GetContentByVisibilityAsync(It.IsAny<Guid>(), It.IsAny<Visibility>()), Times.Never);
    }

    [Fact]
    public async Task GetProgramContent_NotEnrolledNotPublished_ReturnsNotFound()
    {
        var programMock = new Mock<IProgramCrudService>();
        var contentMock = SetupContent();
        var controller = BuildController(
            programMock, contentMock, viewerId, enrolled: false, manager: false,
            program: new Program
            {
                Id = programId,
                CreatorId = creatorId,
                Title = "Draft course",
                Status = ContentStatus.Draft,
                Visibility = ContentVisibility.Private,
                Version = 1,
            });

        var result = await controller.GetProgramContent(programId);

        result.Result.Should().BeOfType<NotFoundResult>();
    }
}
