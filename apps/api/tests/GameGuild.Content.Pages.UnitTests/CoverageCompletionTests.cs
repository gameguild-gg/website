using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Moq;
using Xunit;

namespace GameGuild.Content.Pages.UnitTests;

public class MarketingLeadCoverageCompletionTests
{
    [Fact]
    public void MarketingLeadValidationHelpers_ShouldAcceptAndRejectExpectedValues()
    {
        MarketingLeadSources.IsValid(" CONTACT ").Should().BeTrue();
        MarketingLeadSources.IsValid("newsletter").Should().BeTrue();
        MarketingLeadSources.IsValid(null).Should().BeFalse();
        MarketingLeadSources.IsValid("unknown").Should().BeFalse();

        MarketingLeadStatuses.IsValid(" NEW ").Should().BeTrue();
        MarketingLeadStatuses.IsValid("reviewed").Should().BeTrue();
        MarketingLeadStatuses.IsValid("archived").Should().BeTrue();
        MarketingLeadStatuses.IsValid(" ").Should().BeFalse();
        MarketingLeadStatuses.IsValid("closed").Should().BeFalse();

        MarketingLeadTopics.IsValid(" SALES ").Should().BeTrue();
        MarketingLeadTopics.IsValid("support").Should().BeTrue();
        MarketingLeadTopics.IsValid("partnership").Should().BeTrue();
        MarketingLeadTopics.IsValid("other").Should().BeTrue();
        MarketingLeadTopics.IsValid(null).Should().BeFalse();
        MarketingLeadTopics.IsValid("billing").Should().BeFalse();
    }

    [Fact]
    public void MarketingLeadEntity_ShouldExposeAllProperties()
    {
        var lead = new MarketingLead
        {
            Id = Guid.NewGuid(),
            Source = MarketingLeadSources.Contact,
            Status = MarketingLeadStatuses.Reviewed,
            Name = "Ada",
            Email = "ada@example.com",
            Company = "Modu",
            Topic = MarketingLeadTopics.Sales,
            Plan = "pro",
            Message = "I want to talk with sales.",
            Locale = "en",
            PagePath = "/pricing",
            Referrer = "https://example.test",
            UserAgent = "UnitTest"
        };

        lead.Source.Should().Be(MarketingLeadSources.Contact);
        lead.Status.Should().Be(MarketingLeadStatuses.Reviewed);
        lead.Name.Should().Be("Ada");
        lead.Email.Should().Be("ada@example.com");
        lead.Company.Should().Be("Modu");
        lead.Topic.Should().Be(MarketingLeadTopics.Sales);
        lead.Plan.Should().Be("pro");
        lead.Message.Should().Contain("sales");
        lead.Locale.Should().Be("en");
        lead.PagePath.Should().Be("/pricing");
        lead.Referrer.Should().Be("https://example.test");
        lead.UserAgent.Should().Be("UnitTest");
    }

    [Fact]
    public void MarketingLeadMapping_ShouldMapSingleAndCollection()
    {
        var leads = new List<MarketingLead>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Source = MarketingLeadSources.Contact,
                Status = MarketingLeadStatuses.New,
                Name = "Ada",
                Email = "ada@example.com",
                Company = "Modu",
                Topic = MarketingLeadTopics.Support,
                Plan = "pro",
                Message = "Need support with billing.",
                Locale = "en",
                PagePath = "/contact",
                Referrer = "ref",
                UserAgent = "agent"
            }
        };

        var dto = leads[0].ToDto();
        var dtos = leads.ToDtos().ToList();

        dto.Id.Should().Be(leads[0].Id);
        dto.Source.Should().Be(MarketingLeadSources.Contact);
        dto.Email.Should().Be("ada@example.com");
        dto.UserAgent.Should().Be("agent");
        dtos.Should().ContainSingle().Which.Email.Should().Be("ada@example.com");
    }
}

public class MarketingLeadControllerCoverageTests
{
    [Fact]
    public async Task GetLeadsAndGetLead_ShouldReturnMappedResultsAndNotFound()
    {
        var lead = CreateLead();
        var service = new Mock<IMarketingLeadService>();
        service
            .Setup(current => current.ListAsync("contact", "new", "sales", "ada", 1, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync([lead]);
        service.Setup(current => current.GetByIdAsync(lead.Id, It.IsAny<CancellationToken>())).ReturnsAsync(lead);
        service.Setup(current => current.GetByIdAsync(Guid.Empty, It.IsAny<CancellationToken>())).ReturnsAsync((MarketingLead?)null);
        var controller = new MarketingLeadController(service.Object, new CommandHandlerSender(service.Object));

        var listResult = await controller.GetLeads("contact", "new", "sales", "ada", 1, 2);
        var getResult = await controller.GetLead(lead.Id);
        var missingResult = await controller.GetLead(Guid.Empty);

        listResult.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeAssignableTo<IEnumerable<MarketingLeadDto>>();
        getResult.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<MarketingLeadDto>().Which.Email.Should().Be(lead.Email);
        missingResult.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CreateLead_ShouldReturnBadRequest_WhenModelStateIsInvalid()
    {
        var controller = new MarketingLeadController(Mock.Of<IMarketingLeadService>(), Mock.Of<ISender>());
        controller.ModelState.AddModelError("Email", "Email is required.");

        var result = await controller.CreateLead(new CreateMarketingLeadDto(), CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateLead_ShouldReturnValidationProblem_ForInvalidSourceTopicAndContactRequirements()
    {
        var controller = new MarketingLeadController(Mock.Of<IMarketingLeadService>(), Mock.Of<ISender>());
        var dto = new CreateMarketingLeadDto
        {
            Source = "contact",
            Name = "A",
            Email = "ada@example.com",
            Topic = "billing",
            Message = "short"
        };

        var invalidTopic = await controller.CreateLead(dto, CancellationToken.None);
        var invalidSource = await controller.CreateLead(dto with { Source = "unknown", Topic = MarketingLeadTopics.Sales }, CancellationToken.None);

        var topicProblem = invalidTopic.Result.Should().BeOfType<BadRequestObjectResult>().Subject.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        topicProblem.Errors.Should().ContainKey(nameof(CreateMarketingLeadDto.Topic));
        topicProblem.Errors.Should().ContainKey(nameof(CreateMarketingLeadDto.Name));
        topicProblem.Errors.Should().ContainKey(nameof(CreateMarketingLeadDto.Message));
        invalidSource.Result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().BeOfType<ValidationProblemDetails>()
            .Which.Errors.Should().ContainKey(nameof(CreateMarketingLeadDto.Source));
    }

    [Fact]
    public async Task CreateLead_ShouldRequireContactTopicAndNonBlankNameAndMessage()
    {
        var controller = new MarketingLeadController(Mock.Of<IMarketingLeadService>(), Mock.Of<ISender>());

        var result = await controller.CreateLead(new CreateMarketingLeadDto
        {
            Source = MarketingLeadSources.Contact,
            Name = " ",
            Email = "ada@example.com",
            Topic = null,
            Message = " "
        }, CancellationToken.None);

        var problem = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        problem.Errors.Should().ContainKey(nameof(CreateMarketingLeadDto.Name));
        problem.Errors.Should().ContainKey(nameof(CreateMarketingLeadDto.Topic));
        problem.Errors.Should().ContainKey(nameof(CreateMarketingLeadDto.Message));
    }

    [Fact]
    public async Task CreateLead_ShouldCreateNewsletterAndContactLeads()
    {
        var service = new Mock<IMarketingLeadService>();
        service.Setup(current => current.CreateAsync(It.IsAny<CreateMarketingLeadDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateMarketingLeadDto dto, CancellationToken _) => new MarketingLead
            {
                Id = Guid.NewGuid(),
                Source = dto.Source.Trim().ToLowerInvariant(),
                Status = MarketingLeadStatuses.New,
                Name = dto.Name,
                Email = dto.Email,
                Topic = dto.Topic,
                Message = dto.Message
            });
        var controller = new MarketingLeadController(service.Object, new CommandHandlerSender(service.Object));

        var newsletter = await controller.CreateLead(new CreateMarketingLeadDto
        {
            Source = MarketingLeadSources.Newsletter,
            Email = "news@example.com"
        }, CancellationToken.None);
        var contact = await controller.CreateLead(new CreateMarketingLeadDto
        {
            Source = MarketingLeadSources.Contact,
            Name = "Ada",
            Email = "ada@example.com",
            Topic = MarketingLeadTopics.Sales,
            Message = "I want to talk with sales."
        }, CancellationToken.None);

        newsletter.Result.Should().BeOfType<CreatedAtActionResult>().Which.Value.Should().BeOfType<MarketingLeadDto>().Which.Source.Should().Be(MarketingLeadSources.Newsletter);
        contact.Result.Should().BeOfType<CreatedAtActionResult>().Which.Value.Should().BeOfType<MarketingLeadDto>().Which.Source.Should().Be(MarketingLeadSources.Contact);
    }

    private static MarketingLead CreateLead()
    {
        return new MarketingLead
        {
            Id = Guid.NewGuid(),
            Source = MarketingLeadSources.Contact,
            Status = MarketingLeadStatuses.New,
            Name = "Ada",
            Email = "ada@example.com",
            Topic = MarketingLeadTopics.Sales,
            Message = "I want to talk with sales."
        };
    }
}

public class ContentPagesConfigurationCoverageTests
{
    [Fact]
    public void AddContentPagesModule_ShouldRegisterAllServices()
    {
        var services = new ServiceCollection();

        var returned = services.AddContentPagesModule();

        returned.Should().BeSameAs(services);
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IPageService) && descriptor.ImplementationType == typeof(PageService));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IContentResourceService) && descriptor.ImplementationType == typeof(ContentResourceService));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IMarketingLeadService) && descriptor.ImplementationType == typeof(MarketingLeadService));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IOpenGraphService) && descriptor.ImplementationType == typeof(OpenGraphService));
    }

    [Fact]
    public void PagesModelConfiguration_ShouldApplyAllEntityConfigurations()
    {
        var modelBuilder = new ModelBuilder();

        new PagesModelConfiguration().Configure(modelBuilder);
        var page = modelBuilder.Model.FindEntityType(typeof(Page));
        var section = modelBuilder.Model.FindEntityType(typeof(PageSection));
        var resource = modelBuilder.Model.FindEntityType(typeof(ContentResource));
        var lead = modelBuilder.Model.FindEntityType(typeof(MarketingLead));

        page.Should().NotBeNull();
        page!.FindProperty(nameof(Page.Slug))!.GetMaxLength().Should().Be(500);
        page.FindProperty(nameof(Page.Title))!.GetMaxLength().Should().Be(300);
        page.GetIndexes().Should().Contain(index => index.IsUnique);

        section.Should().NotBeNull();
        section!.FindProperty(nameof(PageSection.SectionType))!.GetMaxLength().Should().Be(50);
        section.FindProperty(nameof(PageSection.CssClasses))!.GetMaxLength().Should().Be(500);

        resource.Should().NotBeNull();
        resource!.FindProperty(nameof(ContentResource.Slug))!.GetMaxLength().Should().Be(500);
        resource.FindProperty(nameof(ContentResource.Tags))!.GetMaxLength().Should().Be(1000);
        resource.GetIndexes().Should().Contain(index => index.IsUnique);

        lead.Should().NotBeNull();
        lead!.FindProperty(nameof(MarketingLead.Source))!.GetMaxLength().Should().Be(40);
        lead.FindProperty(nameof(MarketingLead.Status))!.GetDefaultValue().Should().Be(MarketingLeadStatuses.New);
        lead.FindProperty(nameof(MarketingLead.UserAgent))!.GetMaxLength().Should().Be(500);
    }
}

public class ContentPagesSmokeCoverageTests
{
    [Fact]
    public void ControllersAndServices_ShouldConstruct()
    {
        var actorAccessor = new Mock<IActorContextAccessor>();
        actorAccessor.Setup(current => current.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            IsAuthenticated = true,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>()
        });

        var pageController = new PageController(Mock.Of<IPageService>(), Mock.Of<ISender>(), actorAccessor.Object);
        var resourceController = new ContentResourceController(Mock.Of<IContentResourceService>(), Mock.Of<ISender>(), actorAccessor.Object);
        var ogController = new OpenGraphController(Mock.Of<IOpenGraphService>());
        var db = Mock.Of<IApplicationDbContext>();

        var pageService = new PageService(db);
        var resourceService = new ContentResourceService(db);
        var ogService = new OpenGraphService(db);

        pageController.Should().NotBeNull();
        resourceController.Should().NotBeNull();
        ogController.Should().NotBeNull();
        pageService.Should().NotBeNull();
        resourceService.Should().NotBeNull();
        ogService.Should().NotBeNull();
    }

    [Fact]
    public async Task OpenGraphController_ShouldReturnBadRequestNotFoundAndOk()
    {
        var metadata = new OpenGraphMetadataDto { Slug = "home", Title = "Home" };
        var service = new Mock<IOpenGraphService>();
        service.Setup(current => current.ResolveAsync("missing", It.IsAny<CancellationToken>())).ReturnsAsync((OpenGraphMetadataDto?)null);
        service.Setup(current => current.ResolveAsync("home", It.IsAny<CancellationToken>())).ReturnsAsync(metadata);
        var controller = new OpenGraphController(service.Object);

        var badRequest = await controller.Resolve(" ");
        var notFound = await controller.Resolve("missing");
        var ok = await controller.Resolve("home");

        badRequest.Result.Should().BeOfType<BadRequestObjectResult>();
        notFound.Result.Should().BeOfType<NotFoundResult>();
        ok.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(metadata);
    }
}

public class PublishCommandHandlerCoverageTests
{
    [Fact]
    public async Task PublishPageCommandHandler_ShouldValidateAndMapResult()
    {
        var page = new Page { Id = Guid.NewGuid(), Slug = "home", Title = "Home", Status = PageStatus.Published };
        var service = new Mock<IPageService>();
        service.Setup(current => current.PublishAsync(page.Id, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(page);
        service.Setup(current => current.PublishAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Page?)null);
        var handler = new PublishPageCommandHandler(service.Object);
        var userId = Guid.NewGuid();

        await handler.Invoking(current => current.Handle(null!, CancellationToken.None)).Should().ThrowAsync<ArgumentNullException>();
        await handler.Invoking(current => current.Handle(new PublishPageCommand(Guid.Empty, userId), CancellationToken.None)).Should().ThrowAsync<ArgumentException>();
        await handler.Invoking(current => current.Handle(new PublishPageCommand(page.Id, Guid.Empty), CancellationToken.None)).Should().ThrowAsync<ArgumentException>();
        var dto = await handler.Handle(new PublishPageCommand(page.Id, userId), CancellationToken.None);
        var missing = await handler.Handle(new PublishPageCommand(Guid.Parse("11111111-1111-1111-1111-111111111111"), userId), CancellationToken.None);

        dto.Should().NotBeNull();
        dto!.Slug.Should().Be("home");
        missing.Should().BeNull();
    }

    [Fact]
    public async Task PublishContentResourceCommandHandler_ShouldValidateAndMapResult()
    {
        var resource = new ContentResource { Id = Guid.NewGuid(), Slug = "guide", Title = "Guide", Status = ContentResourceStatus.Published };
        var service = new Mock<IContentResourceService>();
        service.Setup(current => current.PublishAsync(resource.Id, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(resource);
        service.Setup(current => current.PublishAsync(Guid.Parse("22222222-2222-2222-2222-222222222222"), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((ContentResource?)null);
        var handler = new PublishContentResourceCommandHandler(service.Object);
        var userId = Guid.NewGuid();

        await handler.Invoking(current => current.Handle(null!, CancellationToken.None)).Should().ThrowAsync<ArgumentNullException>();
        await handler.Invoking(current => current.Handle(new PublishContentResourceCommand(Guid.Empty, userId), CancellationToken.None)).Should().ThrowAsync<ArgumentException>();
        await handler.Invoking(current => current.Handle(new PublishContentResourceCommand(resource.Id, Guid.Empty), CancellationToken.None)).Should().ThrowAsync<ArgumentException>();
        var dto = await handler.Handle(new PublishContentResourceCommand(resource.Id, userId), CancellationToken.None);
        var missing = await handler.Handle(new PublishContentResourceCommand(Guid.Parse("22222222-2222-2222-2222-222222222222"), userId), CancellationToken.None);

        dto.Should().NotBeNull();
        dto!.Slug.Should().Be("guide");
        missing.Should().BeNull();
    }
}

public class MarketingLeadServiceCompletionTests
{
    [Fact]
    public async Task CreateAsync_ShouldNormalizeRequiredAndOptionalFields()
    {
        await using var db = CreateDbContext();
        var service = new MarketingLeadService(db);

        var lead = await service.CreateAsync(new CreateMarketingLeadDto
        {
            Source = " CONTACT ",
            Name = " Ada ",
            Email = " ADA@EXAMPLE.COM ",
            Company = " Modu ",
            Topic = " SALES ",
            Plan = " Pro ",
            Message = " Need a sales conversation. ",
            Locale = " en ",
            PagePath = " /pricing ",
            Referrer = " https://ref.example ",
            UserAgent = " Agent "
        });

        lead.Source.Should().Be(MarketingLeadSources.Contact);
        lead.Email.Should().Be("ada@example.com");
        lead.Name.Should().Be("Ada");
        lead.Company.Should().Be("Modu");
        lead.Topic.Should().Be("SALES");
        lead.Plan.Should().Be("Pro");
        lead.Message.Should().Be("Need a sales conversation.");
        lead.Locale.Should().Be("en");
        lead.PagePath.Should().Be("/pricing");
        lead.Referrer.Should().Be("https://ref.example");
        lead.UserAgent.Should().Be("Agent");
    }

    private static TestPagesDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TestPagesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestPagesDbContext(options);
    }

    private sealed class TestPagesDbContext(DbContextOptions<TestPagesDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MarketingLead>();
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
