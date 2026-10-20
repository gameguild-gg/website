using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using GameGuild.CQRS;
using Moq;
using Xunit;

namespace GameGuild.Content.Pages.UnitTests;

public class CourseProposalCoverageTests
{
    [Fact]
    public void CourseProposalValidationHelpers_ShouldAcceptAndRejectExpectedValues()
    {
        CourseProposalStatuses.IsValid(" SUBMITTED ").Should().BeTrue();
        CourseProposalStatuses.IsValid("in-review").Should().BeTrue();
        CourseProposalStatuses.IsValid("approved").Should().BeTrue();
        CourseProposalStatuses.IsValid("rejected").Should().BeTrue();
        CourseProposalStatuses.IsValid("published").Should().BeTrue();
        CourseProposalStatuses.IsValid("archived").Should().BeTrue();
        CourseProposalStatuses.IsValid(null).Should().BeFalse();
        CourseProposalStatuses.IsValid("unknown").Should().BeFalse();
    }

    [Fact]
    public void CourseProposalEntity_ShouldExposeAllProperties()
    {
        var proposal = new CourseProposal
        {
            Id = Guid.NewGuid(),
            Slug = "shader-basics-ab12cd34",
            Status = CourseProposalStatuses.Published,
            CourseTitle = "Shader Basics",
            Pitch = "Hands-on shader authoring for 2D games.",
            Audience = "Intermediate Unity developers.",
            ProposerName = "Ada",
            ProposerEmail = "ada@example.com",
            Metadata = """{"topics":["shaders"]}""",
            PublishedAt = DateTime.UtcNow,
        };

        proposal.Slug.Should().Be("shader-basics-ab12cd34");
        proposal.Status.Should().Be(CourseProposalStatuses.Published);
        proposal.CourseTitle.Should().Be("Shader Basics");
        proposal.Pitch.Should().Contain("shader");
        proposal.Audience.Should().Contain("Unity");
        proposal.ProposerName.Should().Be("Ada");
        proposal.ProposerEmail.Should().Be("ada@example.com");
        proposal.Metadata.Should().Contain("shaders");
        proposal.PublishedAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public void CourseProposalMapping_ShouldMapSingleAndCollection()
    {
        var proposals = new List<CourseProposal>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Slug = "shader-basics-ab12cd34",
                Status = CourseProposalStatuses.Submitted,
                CourseTitle = "Shader Basics",
                ProposerName = "Ada",
                ProposerEmail = "ada@example.com",
            },
        };

        var dto = proposals[0].ToDto();
        var dtos = proposals.ToDtos().ToList();

        dto.Id.Should().Be(proposals[0].Id);
        dto.Slug.Should().Be("shader-basics-ab12cd34");
        dto.CourseTitle.Should().Be("Shader Basics");
        dtos.Should().ContainSingle().Which.CourseTitle.Should().Be("Shader Basics");
    }

    [Fact]
    public void GenerateSlug_ShouldSlugifyTitlesAndGuaranteeUniqueness()
    {
        CourseProposalService.GenerateSlug("Shader Basics: Lights & Color!")
            .Should().StartWith("shader-basics-lights-color-");
        CourseProposalService.GenerateSlug("   ").Should().StartWith("proposal-");

        var first = CourseProposalService.GenerateSlug("Same Title");
        var second = CourseProposalService.GenerateSlug("Same Title");
        first.Should().NotBe(second);
        first.Length.Should().BeLessThanOrEqualTo(160);
    }
}

public class CourseProposalServiceTests
{
    [Fact]
    public async Task CreateAsync_ShouldSubmitWithGeneratedSlugAndAuditEvent()
    {
        await using var db = CreateDbContext();
        var sink = new RecordingContentPagesAuditEventSink();
        var service = new CourseProposalService(db, sink);

        var proposal = await service.CreateAsync(new CreateCourseProposalDto
        {
            CourseTitle = "Shader Basics",
            Pitch = "Hands-on shaders.",
            ProposerName = "Ada",
            ProposerEmail = "Ada@Example.com ",
            Metadata = """{"level":"intermediate"}""",
        });

        proposal.Status.Should().Be(CourseProposalStatuses.Submitted);
        proposal.Slug.Should().StartWith("shader-basics-");
        proposal.ProposerEmail.Should().Be("ada@example.com");
        sink.Events.Should().ContainSingle().Which.ActionType.Should().Be("Content.Pages.CourseProposalCreated");
    }

    [Fact]
    public async Task GetPublishedBySlugAsync_ShouldReturnOnlyPublishedProposals()
    {
        await using var db = CreateDbContext();
        var sink = new RecordingContentPagesAuditEventSink();
        var service = new CourseProposalService(db, sink);

        var published = await service.CreateAsync(new CreateCourseProposalDto
        {
            CourseTitle = "Published One",
            ProposerName = "A",
            ProposerEmail = "a@example.com",
        });
        await service.UpdateAsync(published.Id, new UpdateCourseProposalDto { Status = CourseProposalStatuses.Published });

        var draft = await service.CreateAsync(new CreateCourseProposalDto
        {
            CourseTitle = "Draft Two",
            ProposerName = "B",
            ProposerEmail = "b@example.com",
        });

        var found = await service.GetPublishedBySlugAsync(published.Slug);
        var missing = await service.GetPublishedBySlugAsync(draft.Slug);

        found.Should().NotBeNull();
        found!.CourseTitle.Should().Be("Published One");
        found.PublishedAt.Should().NotBeNull();
        missing.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ShouldTransitionStatusesAndClearPublishedAtOnUnpublish()
    {
        await using var db = CreateDbContext();
        var sink = new RecordingContentPagesAuditEventSink();
        var service = new CourseProposalService(db, sink);

        var proposal = await service.CreateAsync(new CreateCourseProposalDto
        {
            CourseTitle = "Lifecycle",
            ProposerName = "A",
            ProposerEmail = "a@example.com",
        });
        await service.UpdateAsync(proposal.Id, new UpdateCourseProposalDto { Status = " PUBLISHED " });

        var published = await service.GetByIdAsync(proposal.Id);
        published!.Status.Should().Be(CourseProposalStatuses.Published);
        published.PublishedAt.Should().NotBeNull();

        await service.UpdateAsync(proposal.Id, new UpdateCourseProposalDto { Status = CourseProposalStatuses.Archived });

        var archived = await service.GetByIdAsync(proposal.Id);
        archived!.Status.Should().Be(CourseProposalStatuses.Archived);
        archived.PublishedAt.Should().BeNull();

        sink.Events.Select(auditEvent => auditEvent.ActionType).Should().Equal(
            "Content.Pages.CourseProposalCreated",
            "Content.Pages.CourseProposalUpdated",
            "Content.Pages.CourseProposalUpdated");
    }

    [Fact]
    public async Task ListAsync_ShouldFilterByStatusAndSearch()
    {
        await using var db = CreateDbContext();
        var sink = new RecordingContentPagesAuditEventSink();
        var service = new CourseProposalService(db, sink);

        await service.CreateAsync(new CreateCourseProposalDto
        {
            CourseTitle = "Shader Basics",
            ProposerName = "Ada",
            ProposerEmail = "ada@example.com",
        });
        await service.CreateAsync(new CreateCourseProposalDto
        {
            CourseTitle = "Level Design",
            ProposerName = "Grace",
            ProposerEmail = "grace@example.com",
        });

        var byStatus = await service.ListAsync(" SUBMITTED ", null, 0, 10);
        byStatus.Should().HaveCount(2);

        var bySearch = await service.ListAsync(null, "grace", 0, 10);
        bySearch.Should().ContainSingle().Which.ProposerEmail.Should().Be("grace@example.com");
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveAndReportMissing()
    {
        await using var db = CreateDbContext();
        var sink = new RecordingContentPagesAuditEventSink();
        var service = new CourseProposalService(db, sink);

        var proposal = await service.CreateAsync(new CreateCourseProposalDto
        {
            CourseTitle = "Removed",
            ProposerName = "A",
            ProposerEmail = "a@example.com",
        });

        (await service.DeleteAsync(proposal.Id)).Should().BeTrue();
        (await service.DeleteAsync(proposal.Id)).Should().BeFalse();
        (await service.GetByIdAsync(proposal.Id)).Should().BeNull();
    }

    private static TestCourseProposalDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TestCourseProposalDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestCourseProposalDbContext(options);
    }

    private sealed class TestCourseProposalDbContext(DbContextOptions<TestCourseProposalDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CourseProposal>();
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}

public class CourseProposalControllerCoverageTests
{
    [Fact]
    public async Task GetProposalsAndGetProposal_ShouldReturnMappedResultsAndNotFound()
    {
        var proposal = new CourseProposal
        {
            Id = Guid.NewGuid(),
            Slug = "shader-basics-ab12cd34",
            Status = CourseProposalStatuses.Submitted,
            CourseTitle = "Shader Basics",
            ProposerName = "Ada",
            ProposerEmail = "ada@example.com",
        };
        var service = new Mock<ICourseProposalService>();
        service
            .Setup(current => current.ListAsync("submitted", "shader", 1, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync([proposal]);
        service.Setup(current => current.GetByIdAsync(proposal.Id, It.IsAny<CancellationToken>())).ReturnsAsync(proposal);
        service.Setup(current => current.GetByIdAsync(Guid.Empty, It.IsAny<CancellationToken>())).ReturnsAsync((CourseProposal?)null);
        service.Setup(current => current.GetPublishedBySlugAsync(proposal.Slug, It.IsAny<CancellationToken>())).ReturnsAsync(proposal);
        var controller = new CourseProposalsController(service.Object, new CommandHandlerSender(service.Object));

        var listResult = await controller.GetProposals("submitted", "shader", 1, 2);
        var getResult = await controller.GetProposal(proposal.Id);
        var missingResult = await controller.GetProposal(Guid.Empty);
        var slugResult = await controller.GetProposalBySlug(proposal.Slug);

        listResult.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeAssignableTo<IEnumerable<CourseProposalDto>>();
        getResult.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<CourseProposalDto>().Which.Slug.Should().Be(proposal.Slug);
        missingResult.Result.Should().BeOfType<NotFoundResult>();
        slugResult.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<CourseProposalDto>().Which.Slug.Should().Be(proposal.Slug);
    }

    [Fact]
    public async Task CreateProposal_ShouldReturnBadRequest_WhenModelStateIsInvalid()
    {
        var controller = new CourseProposalsController(Mock.Of<ICourseProposalService>(), Mock.Of<ISender>());
        controller.ModelState.AddModelError("CourseTitle", "Course title is required.");

        var result = await controller.CreateProposal(new CreateCourseProposalDto(), CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateProposal_ShouldCreateSubmissionAndReturnCreatedAtAction()
    {
        var service = new Mock<ICourseProposalService>();
        service.Setup(current => current.CreateAsync(It.IsAny<CreateCourseProposalDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateCourseProposalDto dto, CancellationToken _) => new CourseProposal
            {
                Id = Guid.NewGuid(),
                Slug = "shader-basics-ab12cd34",
                Status = CourseProposalStatuses.Submitted,
                CourseTitle = dto.CourseTitle,
                ProposerName = dto.ProposerName,
                ProposerEmail = dto.ProposerEmail,
            });
        var controller = new CourseProposalsController(service.Object, new CommandHandlerSender(service.Object));

        var result = await controller.CreateProposal(new CreateCourseProposalDto
        {
            CourseTitle = "Shader Basics",
            ProposerName = "Ada",
            ProposerEmail = "ada@example.com",
        });

        var created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(CourseProposalsController.GetProposal));
        created.Value.Should().BeOfType<CourseProposalDto>().Which.Status.Should().Be(CourseProposalStatuses.Submitted);
    }

    [Fact]
    public async Task UpdateProposal_ShouldRejectUnknownStatuses()
    {
        var controller = new CourseProposalsController(Mock.Of<ICourseProposalService>(), Mock.Of<ISender>());

        var result = await controller.UpdateProposal(Guid.NewGuid(), new UpdateCourseProposalDto { Status = "cooked" });

        var problem = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        problem.Errors.Should().ContainKey(nameof(UpdateCourseProposalDto.Status));
    }

    [Fact]
    public async Task UpdateAndDeleteProposal_ShouldMapNotFound()
    {
        var id = Guid.NewGuid();
        var service = new Mock<ICourseProposalService>();
        service.Setup(current => current.UpdateAsync(id, It.IsAny<UpdateCourseProposalDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CourseProposal?)null);
        service.Setup(current => current.DeleteAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var controller = new CourseProposalsController(service.Object, new CommandHandlerSender(service.Object));

        (await controller.UpdateProposal(id, new UpdateCourseProposalDto { Status = CourseProposalStatuses.Approved }))
            .Result.Should().BeOfType<NotFoundResult>();
        (await controller.DeleteProposal(id)).Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void MarketingLeadSources_ShouldAcceptCourseProposalSource()
    {
        // The /proposals/[slug] interest form posts a lead with the
        // course-proposal source (issue #105 / #53 pipeline).
        MarketingLeadSources.IsValid(" COURSE-PROPOSAL ").Should().BeTrue();
    }
}
