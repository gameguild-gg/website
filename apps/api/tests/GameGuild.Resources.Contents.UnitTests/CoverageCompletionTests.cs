using FluentAssertions;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable.Moq;
using GameGuild.Identity.Context.Actors;
using GameGuild.CQRS;
using GameGuild.Resources.Contents.Configuration;
using Moq;
using Xunit;

namespace GameGuild.Resources.Contents.UnitTests;

public class ContentDomainCoverageCompletionTests
{
    [Fact]
    public void DocumentTemplate_ShouldNormalizeValidateAndUpdateAllFields()
    {
        Action missingKey = () => DocumentTemplate.Create(" ", "Name");
        Action missingName = () => DocumentTemplate.Create("key", " ");

        missingKey.Should().Throw<ArgumentException>();
        missingName.Should().Throw<ArgumentException>();

        var template = DocumentTemplate.Create(
            " lease ",
            " Lease Agreement ",
            description: " ",
            category: " legal ",
            supportedEntityType: " lease ",
            placeholderSchema: " {\"fields\":[]} ",
            isSystemTemplate: true);

        template.TemplateKey.Should().Be("lease");
        template.Name.Should().Be("Lease Agreement");
        template.Description.Should().BeNull();
        template.Category.Should().Be("legal");
        template.SupportedEntityType.Should().Be("lease");
        template.PlaceholderSchema.Should().Be("{\"fields\":[]}");
        template.IsSystemTemplate.Should().BeTrue();

        template.Update(
            " Updated ",
            description: " Desc ",
            category: null,
            supportedEntityType: " property ",
            placeholderSchema: null,
            isSystemTemplate: false);

        template.Name.Should().Be("Updated");
        template.Description.Should().Be("Desc");
        template.Category.Should().BeNull();
        template.SupportedEntityType.Should().Be("property");
        template.PlaceholderSchema.Should().BeNull();
        template.IsSystemTemplate.Should().BeFalse();

        template.Update(" Still Updated ");
        template.IsSystemTemplate.Should().BeFalse();
    }

    [Fact]
    public void ContentVersion_ShouldCoverRemainingNullableBranches()
    {
        var version = ContentVersion.Create(Guid.NewGuid(), "Page", 1, "Title", Guid.NewGuid());
        version.UpdateDraft(metadata: "{\"seo\":true}");
        version.Metadata.Should().Be("{\"seo\":true}");

        var rejected = ContentVersion.Create(Guid.NewGuid(), "Page", 1, "Title", Guid.NewGuid());
        rejected.SubmitForReview(Guid.NewGuid());
        rejected.Reject(Guid.NewGuid());

        rejected.Status.Should().Be(ContentVersionStatus.Rejected);
        rejected.ReviewNotes.Should().BeNull();
    }

    [Fact]
    public void ContractAndTemplateRecords_ShouldExposeValues()
    {
        var template = DocumentTemplate.Create("lease", "Lease");
        var draft = ContentVersion.Create(template.Id, DocumentTemplate.VersionEntityType, 1, "Lease", Guid.NewGuid());
        var input = new GenerateContractInput(
            template.Id,
            "Lease",
            null,
            "Generated Lease",
            new Dictionary<string, string?> { ["name"] = "Ada" },
            "summary",
            Publish: true,
            AllowMissingVariables: true);
        var generated = new GeneratedContractResult(
            Guid.NewGuid(),
            Guid.NewGuid(),
            template.Id,
            template.TemplateKey,
            "Lease",
            Guid.NewGuid(),
            2,
            "Generated Lease",
            "Body",
            ["missing"],
            true,
            new DateTime(2026, 1, 1));
        var create = new CreateDocumentTemplateInput(
            "lease",
            "Lease",
            "desc",
            "legal",
            "Lease",
            "{}",
            "hello {{name}}",
            true);
        var update = new UpdateDocumentTemplateInput("Updated", "desc", "category", "Entity", "{}", false);
        var created = new DocumentTemplateCreatedResult(template, draft);
        var published = new PublishedDocumentTemplateResult(template, draft);
        var rendered = new RenderedDocumentTemplateResult(template, draft, "hello Ada", ["missing"]);

        input.DocumentTemplateId.Should().Be(template.Id);
        input.Publish.Should().BeTrue();
        generated.TemplateKey.Should().Be("lease");
        generated.Published.Should().BeTrue();
        create.InitialContent.Should().Be("hello {{name}}");
        update.IsSystemTemplate.Should().BeFalse();
        created.Template.Should().BeSameAs(template);
        created.DraftVersion.Should().BeSameAs(draft);
        published.Version.Should().BeSameAs(draft);
        rendered.Content.Should().Be("hello Ada");
        rendered.MissingVariables.Should().ContainSingle("missing");
    }

    [Fact]
    public void ControllerDtoRecords_ShouldMapEntitiesAndExposeValues()
    {
        var version = ContentVersion.Create(
            Guid.NewGuid(),
            "Page",
            3,
            "Title",
            Guid.NewGuid(),
            "Summary",
            "Body",
            "{}",
            "Notes");
        version.SubmitForReview(Guid.NewGuid());
        version.Approve(Guid.NewGuid(), "Approved");
        version.Publish(Guid.NewGuid());
        var review = ContentVersionReview.Create(version.Id, Guid.NewGuid(), ContentReviewDecision.RequestChanges, "Feedback", "{}");
        var draftRequest = new CreateDraftRequest(version.EntityId, version.EntityType, "Draft", version.CreatedBy, "S", "B", "M", "N");
        var updateRequest = new UpdateDraftRequest("Title", "Summary", "Body", "{}", "Notes");
        var reviewRequest = new ReviewRequest("Looks good");
        var scheduleRequest = new ScheduleRequest(new DateTime(2026, 2, 1));
        var rollbackRequest = new RollbackRequest(2, "mistake");
        var addReviewRequest = new AddReviewRequest(ContentReviewDecision.Approve, "ok", "{}");
        var generatedResponse = GeneratedContractResponse.FromResult(new GeneratedContractResult(
            version.EntityId,
            version.Id,
            Guid.NewGuid(),
            "template",
            version.EntityType,
            version.EntityId,
            version.VersionNumber,
            version.Title,
            version.Body,
            [],
            true,
            version.CreatedAt));

        var dto = ContentVersionDto.FromEntity(version);
        var reviewDto = ContentVersionReviewDto.FromEntity(review);

        dto.Id.Should().Be(version.Id);
        dto.Status.Should().Be(ContentVersionStatus.Published);
        reviewDto.ContentVersionId.Should().Be(version.Id);
        reviewDto.Decision.Should().Be(ContentReviewDecision.RequestChanges);
        draftRequest.Metadata.Should().Be("M");
        updateRequest.ChangeNotes.Should().Be("Notes");
        reviewRequest.ReviewNotes.Should().Be("Looks good");
        scheduleRequest.ScheduledAt.Should().Be(new DateTime(2026, 2, 1));
        rollbackRequest.TargetVersionNumber.Should().Be(2);
        addReviewRequest.Suggestions.Should().Be("{}");
        generatedResponse.ContentVersionId.Should().Be(version.Id);
    }
}

public class ContentsModuleAndConfigurationCoverageTests
{
    [Fact]
    public void ContentsModule_ShouldRegisterServicesAndReturnInputs()
    {
        var module = new ContentsModule();
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var routes = Mock.Of<IEndpointRouteBuilder>();

        var returnedServices = module.ConfigureServices(services, configuration);
        var returnedRoutes = module.MapEndpoints(routes);

        module.Name.Should().Be("Contents");
        returnedServices.Should().BeSameAs(services);
        returnedRoutes.Should().BeSameAs(routes);
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IDocumentTemplateService) && descriptor.ImplementationType == typeof(DocumentTemplateService));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IContractGenerationService) && descriptor.ImplementationType == typeof(ContractGenerationService));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(ICommandHandler<GenerateContractCommand, Result<GeneratedContractResult>>) && descriptor.ImplementationType == typeof(GenerateContractCommandHandler));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(ICommandHandler<BulkGenerateContractsCommand, BulkGeneratedContractsResult>) && descriptor.ImplementationType == typeof(BulkGenerateContractsCommandHandler));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IContentDraftService) && descriptor.ImplementationType == typeof(ContentDraftService));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IContentReviewPublishingService) && descriptor.ImplementationType == typeof(ContentReviewPublishingService));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IContentVersionQueryService) && descriptor.ImplementationType == typeof(ContentVersionQueryService));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IContentVersioningService) && descriptor.ImplementationType == typeof(ContentVersioningService));
    }

    [Fact]
    public void EfConfigurations_ShouldApplyExpectedTablesIndexesAndProperties()
    {
        var modelBuilder = new ModelBuilder();

        new ContentsModelConfiguration().Configure(modelBuilder);
        var model = modelBuilder.Model;

        var contentVersion = model.FindEntityType(typeof(ContentVersion))!;
        var review = model.FindEntityType(typeof(ContentVersionReview))!;
        var template = model.FindEntityType(typeof(DocumentTemplate))!;

        contentVersion.Should().NotBeNull();
        contentVersion.GetTableName().Should().Be("content_versions");
        contentVersion.FindProperty(nameof(ContentVersion.EntityType))!.GetMaxLength().Should().Be(100);
        contentVersion.FindProperty(nameof(ContentVersion.Body))!.GetColumnType().Should().Be("text");
        contentVersion.FindProperty(nameof(ContentVersion.Metadata))!.GetColumnType().Should().Be("jsonb");
        contentVersion.GetIndexes().Should().Contain(index => index.Properties.Any(property => property.Name == nameof(ContentVersion.ScheduledPublishAt)));

        review.Should().NotBeNull();
        review.GetTableName().Should().Be("content_version_reviews");
        review.FindProperty(nameof(ContentVersionReview.Feedback))!.GetMaxLength().Should().Be(2000);
        review.FindProperty(nameof(ContentVersionReview.Suggestions))!.GetColumnType().Should().Be("jsonb");

        template.Should().NotBeNull();
        template.GetTableName().Should().Be("document_templates");
        template.FindProperty(nameof(DocumentTemplate.TemplateKey))!.GetMaxLength().Should().Be(160);
        template.FindProperty(nameof(DocumentTemplate.PlaceholderSchema))!.GetColumnType().Should().Be("jsonb");
        template.GetIndexes().Should().Contain(index => index.IsUnique);
    }
}

public class ContractGenerationControllerCoverageTests
{
    [Fact]
    public async Task Generate_ShouldReturnUnauthorized_WhenActorHasNoGuid()
    {
        var controller = CreateController(ActorContext.Anonymous);

        var result = await controller.Generate(CreateRequest(), CancellationToken.None);

        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Generate_ShouldReturnCreated_WhenServiceSucceeds()
    {
        var actorId = Guid.NewGuid();
        var request = CreateRequest();
        var generated = new GeneratedContractResult(
            request.EntityId!.Value,
            Guid.NewGuid(),
            request.DocumentTemplateId,
            "lease",
            request.EntityType,
            request.EntityId.Value,
            1,
            request.Title,
            "Body",
            [],
            true,
            new DateTime(2026, 1, 1));
        var sender = new Mock<ISender>();
        sender
            .Setup(mock => mock.Send(It.Is<IRequest<Result<GeneratedContractResult>>>(command =>
                MatchesGenerateCommand(command, request, actorId)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(generated));
        var controller = CreateController(CreateActor(actorId), sender.Object);

        var result = await controller.Generate(request, CancellationToken.None);

        var created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(ContractGenerationController.Generate));
        created.Value.Should().BeOfType<GeneratedContractResponse>().Which.ContractId.Should().Be(generated.ContractId);
    }

    [Fact]
    public async Task Generate_ShouldMapServiceFailuresToNotFoundAndBadRequest()
    {
        var actorId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender
            .SetupSequence(mock => mock.Send(It.IsAny<IRequest<Result<GeneratedContractResult>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<GeneratedContractResult>(Error.NotFound("Template.NotFound", "missing")))
            .ReturnsAsync(Result.Failure<GeneratedContractResult>(Error.Validation("Template.Invalid", "invalid")));
        var controller = CreateController(CreateActor(actorId), sender.Object);

        var notFound = await controller.Generate(CreateRequest(), CancellationToken.None);
        var badRequest = await controller.Generate(CreateRequest(), CancellationToken.None);

        notFound.Result.Should().BeOfType<NotFoundObjectResult>();
        badRequest.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GenerateBulk_ShouldReturnUnauthorized_WhenActorHasNoGuid()
    {
        var controller = CreateController(ActorContext.Anonymous);

        var result = await controller.GenerateBulk(new BulkGenerateContractsRequest([CreateRequest()]), CancellationToken.None);

        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GenerateBulk_ShouldReturnBadRequest_WhenContractsAreEmpty()
    {
        var controller = CreateController(CreateActor(Guid.NewGuid()));

        var result = await controller.GenerateBulk(new BulkGenerateContractsRequest([]), CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GenerateBulk_ShouldReturnPerItemSuccessAndFailure()
    {
        var actorId = Guid.NewGuid();
        var first = CreateRequest();
        var second = CreateRequest() with { Title = "Second Lease" };
        var generated = new GeneratedContractResult(
            first.EntityId!.Value,
            Guid.NewGuid(),
            first.DocumentTemplateId,
            "lease",
            first.EntityType,
            first.EntityId.Value,
            1,
            first.Title,
            "Body",
            [],
            true,
            new DateTime(2026, 1, 1));
        var bulk = new BulkGeneratedContractsResult(
            2,
            1,
            1,
            [
                BulkGeneratedContractItemResult.Succeeded(0, generated),
                BulkGeneratedContractItemResult.FailedItem(1, Error.Validation("Contract.Invalid", "invalid"))
            ]);
        var sender = new Mock<ISender>();
        sender
            .Setup(mock => mock.Send(It.Is<IRequest<BulkGeneratedContractsResult>>(command =>
                MatchesBulkGenerateCommand(command, second.Title, actorId)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(bulk);
        var controller = CreateController(CreateActor(actorId), sender.Object);

        var result = await controller.GenerateBulk(new BulkGenerateContractsRequest([first, second]), CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<BulkGeneratedContractsResponse>().Subject;
        response.TotalRequested.Should().Be(2);
        response.Successful.Should().Be(1);
        response.Failed.Should().Be(1);
        response.HasFailures.Should().BeTrue();
        response.Items[0].Contract!.ContractId.Should().Be(generated.ContractId);
        response.Items[1].Error!.Code.Should().Be("Contract.Invalid");
    }

    private static ContractGenerationController CreateController(
        ActorContext actorContext,
        ISender? sender = null)
    {
        var actorAccessor = new Mock<IActorContextAccessor>();
        actorAccessor.Setup(accessor => accessor.ActorContext).Returns(actorContext);

        return new ContractGenerationController(sender ?? Mock.Of<ISender>(), actorAccessor.Object);
    }

    private static GenerateContractRequest CreateRequest()
    {
        return new GenerateContractRequest(
            Guid.NewGuid(),
            "Lease",
            Guid.NewGuid(),
            "Lease Agreement",
            new Dictionary<string, string?> { ["name"] = "Ada" },
            "Summary",
            Publish: true,
            AllowMissingVariables: true);
    }

    private static bool MatchesGenerateCommand(
        IRequest<Result<GeneratedContractResult>> command,
        GenerateContractRequest request,
        Guid actorId)
    {
        if (command is not GenerateContractCommand generate)
        {
            return false;
        }

        return generate.CreatedBy == actorId &&
               generate.Input.DocumentTemplateId == request.DocumentTemplateId &&
               generate.Input.Variables.ContainsKey("name") &&
               generate.Input.Publish &&
               generate.Input.AllowMissingVariables;
    }

    private static bool MatchesBulkGenerateCommand(
        IRequest<BulkGeneratedContractsResult> command,
        string expectedSecondTitle,
        Guid actorId)
    {
        if (command is not BulkGenerateContractsCommand bulkCommand)
        {
            return false;
        }

        return bulkCommand.CreatedBy == actorId &&
               bulkCommand.ContinueOnError &&
               bulkCommand.Inputs.Count == 2 &&
               bulkCommand.Inputs[1].Title == expectedSecondTitle;
    }

    private static ActorContext CreateActor(Guid userId)
    {
        return new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = userId.ToString(),
            IsAuthenticated = true,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>()
        };
    }
}

public class ContractGenerationCommandHandlerTests
{
    [Fact]
    public async Task GenerateContractCommandHandler_ShouldDelegateToGenerationService()
    {
        var createdBy = Guid.NewGuid();
        var input = CreateInput("Lease");
        var generated = CreateGenerated(input, "lease");
        var service = new Mock<IContractGenerationService>();
        service
            .Setup(mock => mock.GenerateAsync(input, createdBy, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(generated));
        var handler = new GenerateContractCommandHandler(service.Object);

        var result = await handler.Handle(new GenerateContractCommand(input, createdBy), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ContractId.Should().Be(generated.ContractId);
        service.Verify(mock => mock.GenerateAsync(input, createdBy, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BulkGenerateContractsCommandHandler_ShouldContinueAndReportPartialFailures()
    {
        var createdBy = Guid.NewGuid();
        var first = CreateInput("Lease");
        var second = CreateInput("Renewal");
        var generated = CreateGenerated(first, "lease");
        var service = new Mock<IContractGenerationService>();
        service
            .Setup(mock => mock.GenerateAsync(first, createdBy, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(generated));
        service
            .Setup(mock => mock.GenerateAsync(second, createdBy, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<GeneratedContractResult>(Error.Validation("Contract.Invalid", "invalid")));
        var handler = new BulkGenerateContractsCommandHandler(service.Object);

        var result = await handler.Handle(
            new BulkGenerateContractsCommand([first, second], createdBy, ContinueOnError: true),
            CancellationToken.None);

        result.TotalRequested.Should().Be(2);
        result.Successful.Should().Be(1);
        result.Failed.Should().Be(1);
        result.HasFailures.Should().BeTrue();
        result.Items[0].Contract!.ContractId.Should().Be(generated.ContractId);
        result.Items[1].Error!.Code.Should().Be("Contract.Invalid");
    }

    [Fact]
    public async Task BulkGenerateContractsCommandHandler_ShouldStopOnFirstFailure_WhenContinueOnErrorIsFalse()
    {
        var createdBy = Guid.NewGuid();
        var first = CreateInput("Lease");
        var second = CreateInput("Renewal");
        var service = new Mock<IContractGenerationService>();
        service
            .Setup(mock => mock.GenerateAsync(first, createdBy, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<GeneratedContractResult>(Error.Validation("Contract.Invalid", "invalid")));
        var handler = new BulkGenerateContractsCommandHandler(service.Object);

        var result = await handler.Handle(
            new BulkGenerateContractsCommand([first, second], createdBy, ContinueOnError: false),
            CancellationToken.None);

        result.TotalRequested.Should().Be(2);
        result.Successful.Should().Be(0);
        result.Failed.Should().Be(1);
        result.Items.Should().ContainSingle();
        service.Verify(mock => mock.GenerateAsync(second, createdBy, It.IsAny<CancellationToken>()), Times.Never);
    }

    private static GenerateContractInput CreateInput(string title)
        => new(
            Guid.NewGuid(),
            "Lease",
            Guid.NewGuid(),
            title,
            new Dictionary<string, string?> { ["name"] = "Ada" },
            "Summary",
            Publish: true,
            AllowMissingVariables: true);

    private static GeneratedContractResult CreateGenerated(GenerateContractInput input, string templateKey)
        => new(
            input.EntityId!.Value,
            Guid.NewGuid(),
            input.DocumentTemplateId,
            templateKey,
            input.EntityType,
            input.EntityId.Value,
            1,
            input.Title,
            "Body",
            [],
            true,
            new DateTime(2026, 1, 1));
}

public class VersioningControllerCoverageCompletionTests
{
    [Fact]
    public async Task VersioningController_ShouldReturnExpectedResultKinds()
    {
        var service = new Mock<IContentVersioningService>();
        var entityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var draft = ContentVersion.Create(entityId, "Page", 1, "Draft", userId);
        var pending = ContentVersion.Create(entityId, "Page", 2, "Pending", userId);
        pending.SubmitForReview(userId);
        var approved = ContentVersion.Create(entityId, "Page", 3, "Approved", userId);
        approved.SubmitForReview(userId);
        approved.Approve(userId);
        var review = ContentVersionReview.Create(draft.Id, userId, ContentReviewDecision.Approve, "ok", "{}");
        var diff = new ContentVersionDiff(draft.Id, approved.Id, 1, 3, true, false, true, false, "Title", null, "Body");
        var notFound = Error.NotFound("ContentVersioning.NotFound", "missing");
        var invalid = Error.Validation("ContentVersioning.Invalid", "invalid");
        var controller = new VersioningController(service.Object, new CommandHandlerSender(service.Object));

        service.Setup(mock => mock.CreateDraftAsync(entityId, "Page", "Draft", userId, "S", "B", "M", "N", It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(draft));
        service.Setup(mock => mock.UpdateDraftAsync(draft.Id, "Updated", null, null, null, null, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(draft));
        service.Setup(mock => mock.UpdateDraftAsync(Guid.Empty, null, null, null, null, null, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<ContentVersion>(notFound));
        service.Setup(mock => mock.UpdateDraftAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"), null, null, null, null, null, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<ContentVersion>(invalid));
        service.Setup(mock => mock.GetVersionAsync(draft.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(draft));
        service.Setup(mock => mock.GetVersionAsync(Guid.Empty, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<ContentVersion>(notFound));
        service.Setup(mock => mock.GetVersionHistoryAsync(entityId, "Page", It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success<IEnumerable<ContentVersion>>([draft]));
        service.Setup(mock => mock.GetVersionHistoryAsync(Guid.Empty, "Page", It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<IEnumerable<ContentVersion>>(invalid));
        service.Setup(mock => mock.GetCurrentVersionAsync(entityId, "Page", It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(approved));
        service.Setup(mock => mock.GetCurrentVersionAsync(Guid.Empty, "Page", It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<ContentVersion>(notFound));
        service.Setup(mock => mock.GetVersionByNumberAsync(entityId, "Page", 1, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(draft));
        service.Setup(mock => mock.GetVersionByNumberAsync(Guid.Empty, "Page", 1, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<ContentVersion>(notFound));
        service.Setup(mock => mock.SubmitForReviewAsync(draft.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(pending));
        service.Setup(mock => mock.SubmitForReviewAsync(Guid.Empty, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<ContentVersion>(notFound));
        service.Setup(mock => mock.SubmitForReviewAsync(Guid.Parse("22222222-2222-2222-2222-222222222222"), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<ContentVersion>(invalid));
        service.Setup(mock => mock.GetPendingReviewAsync("Page", 0, 10, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success<IEnumerable<ContentVersion>>([pending]));
        service.Setup(mock => mock.GetPendingReviewAsync("Fail", 0, 10, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<IEnumerable<ContentVersion>>(invalid));
        service.Setup(mock => mock.ApproveAsync(pending.Id, "ok", It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(approved));
        service.Setup(mock => mock.ApproveAsync(Guid.Empty, null, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<ContentVersion>(notFound));
        service.Setup(mock => mock.ApproveAsync(Guid.Parse("33333333-3333-3333-3333-333333333333"), null, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<ContentVersion>(invalid));
        service.Setup(mock => mock.RejectAsync(pending.Id, "no", It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(pending));
        service.Setup(mock => mock.RejectAsync(Guid.Empty, null, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<ContentVersion>(notFound));
        service.Setup(mock => mock.RejectAsync(Guid.Parse("44444444-4444-4444-4444-444444444444"), null, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<ContentVersion>(invalid));
        service.Setup(mock => mock.PublishAsync(approved.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(approved));
        service.Setup(mock => mock.PublishAsync(Guid.Empty, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<ContentVersion>(notFound));
        service.Setup(mock => mock.PublishAsync(Guid.Parse("55555555-5555-5555-5555-555555555555"), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<ContentVersion>(invalid));
        service.Setup(mock => mock.SchedulePublishAsync(approved.Id, new DateTime(2026, 2, 1), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(approved));
        service.Setup(mock => mock.SchedulePublishAsync(Guid.Empty, new DateTime(2026, 2, 1), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<ContentVersion>(notFound));
        service.Setup(mock => mock.SchedulePublishAsync(Guid.Parse("66666666-6666-6666-6666-666666666666"), new DateTime(2026, 2, 1), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<ContentVersion>(invalid));
        service.Setup(mock => mock.CancelScheduledPublishAsync(approved.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(approved));
        service.Setup(mock => mock.CancelScheduledPublishAsync(Guid.Empty, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<ContentVersion>(notFound));
        service.Setup(mock => mock.CancelScheduledPublishAsync(Guid.Parse("77777777-7777-7777-7777-777777777777"), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<ContentVersion>(invalid));
        service.Setup(mock => mock.CompareVersionsAsync(draft.Id, approved.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(diff));
        service.Setup(mock => mock.CompareVersionsAsync(Guid.Empty, approved.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<ContentVersionDiff>(notFound));
        service.Setup(mock => mock.CompareVersionsAsync(Guid.Parse("88888888-8888-8888-8888-888888888888"), approved.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<ContentVersionDiff>(invalid));
        service.Setup(mock => mock.RollbackAsync(entityId, "Page", 1, "reason", It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(draft));
        service.Setup(mock => mock.RollbackAsync(Guid.Empty, "Page", 1, null, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<ContentVersion>(notFound));
        service.Setup(mock => mock.RollbackAsync(Guid.Parse("99999999-9999-9999-9999-999999999999"), "Page", 1, null, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<ContentVersion>(invalid));
        service.Setup(mock => mock.AddReviewAsync(draft.Id, ContentReviewDecision.Approve, "ok", "{}", It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(review));
        service.Setup(mock => mock.AddReviewAsync(Guid.Empty, ContentReviewDecision.Approve, null, null, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<ContentVersionReview>(notFound));
        service.Setup(mock => mock.AddReviewAsync(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), ContentReviewDecision.Approve, null, null, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<ContentVersionReview>(invalid));

        (await controller.CreateDraft(new CreateDraftRequest(entityId, "Page", "Draft", userId, "S", "B", "M", "N"), CancellationToken.None)).Should().BeOfType<CreatedAtActionResult>();
        (await controller.UpdateDraft(draft.Id, new UpdateDraftRequest("Updated"), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await controller.UpdateDraft(Guid.Empty, new UpdateDraftRequest(), CancellationToken.None)).Should().BeOfType<NotFoundObjectResult>();
        (await controller.UpdateDraft(Guid.Parse("11111111-1111-1111-1111-111111111111"), new UpdateDraftRequest(), CancellationToken.None)).Should().BeOfType<BadRequestObjectResult>();
        (await controller.GetVersion(draft.Id, CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await controller.GetVersion(Guid.Empty, CancellationToken.None)).Should().BeOfType<NotFoundObjectResult>();
        (await controller.GetVersionHistory("Page", entityId, CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await controller.GetVersionHistory("Page", Guid.Empty, CancellationToken.None)).Should().BeOfType<BadRequestObjectResult>();
        (await controller.GetCurrentVersion("Page", entityId, CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await controller.GetCurrentVersion("Page", Guid.Empty, CancellationToken.None)).Should().BeOfType<NotFoundObjectResult>();
        (await controller.GetVersionByNumber("Page", entityId, 1, CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await controller.GetVersionByNumber("Page", Guid.Empty, 1, CancellationToken.None)).Should().BeOfType<NotFoundObjectResult>();
        (await controller.SubmitForReview(draft.Id, CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await controller.SubmitForReview(Guid.Empty, CancellationToken.None)).Should().BeOfType<NotFoundObjectResult>();
        (await controller.SubmitForReview(Guid.Parse("22222222-2222-2222-2222-222222222222"), CancellationToken.None)).Should().BeOfType<BadRequestObjectResult>();
        (await controller.GetPendingReview("Page", 0, 10, CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await controller.GetPendingReview("Fail", 0, 10, CancellationToken.None)).Should().BeOfType<BadRequestObjectResult>();
        (await controller.Approve(pending.Id, new ReviewRequest("ok"), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await controller.Approve(Guid.Empty, null, CancellationToken.None)).Should().BeOfType<NotFoundObjectResult>();
        (await controller.Approve(Guid.Parse("33333333-3333-3333-3333-333333333333"), null, CancellationToken.None)).Should().BeOfType<BadRequestObjectResult>();
        (await controller.Reject(pending.Id, new ReviewRequest("no"), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await controller.Reject(Guid.Empty, null, CancellationToken.None)).Should().BeOfType<NotFoundObjectResult>();
        (await controller.Reject(Guid.Parse("44444444-4444-4444-4444-444444444444"), null, CancellationToken.None)).Should().BeOfType<BadRequestObjectResult>();
        (await controller.Publish(approved.Id, CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await controller.Publish(Guid.Empty, CancellationToken.None)).Should().BeOfType<NotFoundObjectResult>();
        (await controller.Publish(Guid.Parse("55555555-5555-5555-5555-555555555555"), CancellationToken.None)).Should().BeOfType<BadRequestObjectResult>();
        (await controller.SchedulePublish(approved.Id, new ScheduleRequest(new DateTime(2026, 2, 1)), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await controller.SchedulePublish(Guid.Empty, new ScheduleRequest(new DateTime(2026, 2, 1)), CancellationToken.None)).Should().BeOfType<NotFoundObjectResult>();
        (await controller.SchedulePublish(Guid.Parse("66666666-6666-6666-6666-666666666666"), new ScheduleRequest(new DateTime(2026, 2, 1)), CancellationToken.None)).Should().BeOfType<BadRequestObjectResult>();
        (await controller.CancelSchedule(approved.Id, CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await controller.CancelSchedule(Guid.Empty, CancellationToken.None)).Should().BeOfType<NotFoundObjectResult>();
        (await controller.CancelSchedule(Guid.Parse("77777777-7777-7777-7777-777777777777"), CancellationToken.None)).Should().BeOfType<BadRequestObjectResult>();
        (await controller.Compare(draft.Id, approved.Id, CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await controller.Compare(Guid.Empty, approved.Id, CancellationToken.None)).Should().BeOfType<NotFoundObjectResult>();
        (await controller.Compare(Guid.Parse("88888888-8888-8888-8888-888888888888"), approved.Id, CancellationToken.None)).Should().BeOfType<BadRequestObjectResult>();
        (await controller.Rollback("Page", entityId, new RollbackRequest(1, "reason"), CancellationToken.None)).Should().BeOfType<CreatedAtActionResult>();
        (await controller.Rollback("Page", Guid.Empty, new RollbackRequest(1), CancellationToken.None)).Should().BeOfType<NotFoundObjectResult>();
        (await controller.Rollback("Page", Guid.Parse("99999999-9999-9999-9999-999999999999"), new RollbackRequest(1), CancellationToken.None)).Should().BeOfType<BadRequestObjectResult>();
        (await controller.AddReview(draft.Id, new AddReviewRequest(ContentReviewDecision.Approve, "ok", "{}"), CancellationToken.None)).Should().BeOfType<CreatedAtActionResult>();
        (await controller.AddReview(Guid.Empty, new AddReviewRequest(ContentReviewDecision.Approve), CancellationToken.None)).Should().BeOfType<NotFoundObjectResult>();
        (await controller.AddReview(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), new AddReviewRequest(ContentReviewDecision.Approve), CancellationToken.None)).Should().BeOfType<BadRequestObjectResult>();
    }
}

public class ContentServiceFallbackBranchCoverageTests
{
    [Fact]
    public async Task ContentDraftRollback_ShouldUseEmptyUserId_WhenActorSubjectIsNotGuid()
    {
        var entityId = Guid.NewGuid();
        var target = ContentVersion.Create(entityId, "Page", 1, "Original", Guid.NewGuid(), "S", "B", "M");
        var versions = new List<ContentVersion> { target };
        var db = CreateDbMock(versions, []);
        var service = new ContentDraftService(db.Object, CreateActorAccessor("system"), NullLogger<ContentDraftService>.Instance);

        var result = await service.RollbackAsync(entityId, "Page", 1, "reason");

        result.IsSuccess.Should().BeTrue();
        result.Value.CreatedBy.Should().Be(Guid.Empty);
        result.Value.ChangeNotes.Should().Be("Rollback to v1: reason");
    }

    [Fact]
    public async Task ContentReviewAddReview_ShouldUseEmptyReviewerId_WhenActorSubjectIsNotGuid()
    {
        var version = ContentVersion.Create(Guid.NewGuid(), "Page", 1, "Draft", Guid.NewGuid());
        var reviews = new List<ContentVersionReview>();
        var db = CreateDbMock([version], reviews);
        var service = new ContentReviewPublishingService(db.Object, CreateActorAccessor("system"), NullLogger<ContentReviewPublishingService>.Instance);

        var result = await service.AddReviewAsync(version.Id, ContentReviewDecision.Approve, "ok", "{}");

        result.IsSuccess.Should().BeTrue();
        result.Value.ReviewerId.Should().Be(Guid.Empty);
        reviews.Should().ContainSingle();
    }

    [Fact]
    public async Task ContractGenerationService_ShouldReturnValidationFailuresBeforeRendering()
    {
        var service = new ContractGenerationService(
            Mock.Of<IApplicationDbContext>(),
            Mock.Of<IDocumentTemplateService>());
        var validTemplateId = Guid.NewGuid();

        var missingEntityType = await service.GenerateAsync(new GenerateContractInput(
            validTemplateId,
            " ",
            null,
            "Title",
            new Dictionary<string, string?>()),
            Guid.NewGuid());
        var missingTitle = await service.GenerateAsync(new GenerateContractInput(
            validTemplateId,
            "Lease",
            null,
            " ",
            new Dictionary<string, string?>()),
            Guid.NewGuid());

        missingEntityType.IsFailure.Should().BeTrue();
        missingEntityType.Error.Code.Should().Be("ContractGeneration.EntityTypeRequired");
        missingTitle.IsFailure.Should().BeTrue();
        missingTitle.Error.Code.Should().Be("ContractGeneration.TitleRequired");
    }

    [Fact]
    public async Task ContractGenerationService_ShouldGenerateDraftContractAndUseJsonOptions()
    {
        var template = DocumentTemplate.Create("lease", "Lease");
        var templateVersion = ContentVersion.Create(template.Id, DocumentTemplate.VersionEntityType, 1, "Lease", Guid.NewGuid(), body: "Hello {{name}}");
        var entityId = Guid.NewGuid();
        var existingVersion = ContentVersion.Create(entityId, "Lease", 1, "Existing", Guid.NewGuid());
        var versions = new List<ContentVersion> { existingVersion };
        var versionSet = versions.AsQueryable().BuildMockDbSet();
        versionSet.Setup(set => set.Add(It.IsAny<ContentVersion>()))
            .Callback<ContentVersion>(versions.Add);
        var db = new Mock<IApplicationDbContext>();
        db.Setup(database => database.Set<ContentVersion>()).Returns(versionSet.Object);
        db.Setup(database => database.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var templates = new Mock<IDocumentTemplateService>();
        templates
            .Setup(service => service.RenderPublishedAsync(
                template.Id,
                It.IsAny<IReadOnlyDictionary<string, string?>>(),
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new RenderedDocumentTemplateResult(template, templateVersion, "Hello Ada", [])));
        var service = new ContractGenerationService(db.Object, templates.Object);

        var result = await service.GenerateAsync(new GenerateContractInput(
            template.Id,
            " Lease ",
            entityId,
            "Generated",
            new Dictionary<string, string?> { ["name"] = "Ada" }),
            Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        result.Value.VersionNumber.Should().Be(2);
        result.Value.Content.Should().Be("Hello Ada");
        result.Value.Published.Should().BeFalse();
        versions.Should().HaveCount(2);
        versions.Last().Metadata.Should().Contain("contractGenerated");
    }

    [Fact]
    public async Task DocumentTemplateService_ShouldExposeErrorFactories()
    {
        var db = Mock.Of<IApplicationDbContext>();
        var service = new DocumentTemplateService(db);

        var notFound = DocumentTemplateErrors.NotFound;
        var duplicate = DocumentTemplateErrors.DuplicateKey;
        var publishedMissing = DocumentTemplateErrors.PublishedVersionNotFound;

        service.Should().NotBeNull();
        notFound.Code.Should().Be("DocumentTemplate.NotFound");
        duplicate.Code.Should().Be("DocumentTemplate.DuplicateKey");
        publishedMissing.Code.Should().Be("DocumentTemplate.PublishedVersionNotFound");
        await Task.CompletedTask;
    }

    [Fact]
    public void ContractGenerationController_ToInput_ShouldUseEmptyVariablesWhenRequestVariablesAreNull()
    {
        var request = new GenerateContractRequest(
            Guid.NewGuid(),
            "Lease",
            Guid.NewGuid(),
            "Title",
            Variables: null,
            Summary: "summary",
            Publish: true,
            AllowMissingVariables: true);

        var input = typeof(ContractGenerationController)
            .GetMethod("ToInput", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [request])
            .Should()
            .BeOfType<GenerateContractInput>()
            .Subject;

        input.Variables.Should().BeEmpty();
        input.Publish.Should().BeTrue();
        input.AllowMissingVariables.Should().BeTrue();
    }

    private static Mock<IApplicationDbContext> CreateDbMock(
        List<ContentVersion> versions,
        List<ContentVersionReview> reviews)
    {
        var versionSet = versions.AsQueryable().BuildMockDbSet();
        versionSet.Setup(set => set.Add(It.IsAny<ContentVersion>()))
            .Callback<ContentVersion>(versions.Add);
        var reviewSet = reviews.AsQueryable().BuildMockDbSet();
        reviewSet.Setup(set => set.Add(It.IsAny<ContentVersionReview>()))
            .Callback<ContentVersionReview>(reviews.Add);
        var db = new Mock<IApplicationDbContext>();
        db.Setup(database => database.Set<ContentVersion>()).Returns(versionSet.Object);
        db.Setup(database => database.Set<ContentVersionReview>()).Returns(reviewSet.Object);
        db.Setup(database => database.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        return db;
    }

    private static IActorContextAccessor CreateActorAccessor(string subjectId)
    {
        var accessor = new Mock<IActorContextAccessor>();
        accessor.Setup(current => current.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.System,
            SubjectId = subjectId,
            IsAuthenticated = true,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>()
        });
        return accessor.Object;
    }
}
