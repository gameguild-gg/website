using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Commerce.Products;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Users;
using GameGuild.LaunchPad;
using GameGuild.Projects;
using GameGuild.TestingLab;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Xunit;

namespace GameGuild.API.UnitTests.Database;

[Collection(PostgreSqlTestCollection.Name)]
public sealed class ProjectChannelPostgreSqlRaceTests : IAsyncLifetime
{
    private EconomyPostgreSqlTestDatabase _container = null!;

    private DbContextOptions<ApplicationDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        _container = await EconomyPostgreSqlTestDatabase.CreateAsync("project_channel_races");
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_container.ConnectionString)
            .Options;
        await using var context = new ApplicationDbContext(_options);
        await context.Database.EnsureCreatedAsync();
    }

    [Fact]
    public async Task ConcurrentDeleteAndStoreLink_CannotLeaveActiveLinkOnDeletedProject()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var project = NewProject(tenantId, ContentStatus.Published, ContentVisibility.Public);
        var product = Product.Create("Race product", creatorId: actorId, tenantId: tenantId);
        product.IsPublished = true;
        await SeedAsync(NewUser(actorId), project, product);
        var actorAccessor = ActorAccessor(actorId, tenantId);

        await using var gateContext = new ApplicationDbContext(_options);
        await using var gate = await new ProjectLifecycleLock(gateContext).AcquireAsync(project.Id);
        await using var observer = new NpgsqlConnection(_container.ConnectionString);
        await observer.OpenAsync();

        var linkTask = LinkStoreAsync(project.Id, product.Id, actorAccessor);
        await WaitForWaitingAdvisoryLocksAsync(observer, 1);
        var deleteTask = DeleteAsync(project.Id);
        await WaitForWaitingAdvisoryLocksAsync(observer, 2);

        await gate.CommitAsync();
        (await linkTask).IsSuccess.Should().BeTrue();
        (await deleteTask).Should().BeTrue();
        await using var verify = new ApplicationDbContext(_options);
        (await verify.Set<Project>().IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == project.Id))
            .DeletedAt.Should().NotBeNull();
        (await verify.Set<ProjectStoreProduct>().AnyAsync(link =>
            link.ProjectId == project.Id && link.DeletedAt == null)).Should().BeFalse();
    }

    [Fact]
    public async Task ConcurrentDeleteAndStoreLink_DeleteFirstRejectsLink()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var project = NewProject(tenantId, ContentStatus.Published, ContentVisibility.Public);
        var product = Product.Create("Delete-first product", creatorId: actorId, tenantId: tenantId);
        product.IsPublished = true;
        await SeedAsync(NewUser(actorId), project, product);
        var actorAccessor = ActorAccessor(actorId, tenantId);

        await using var gateContext = new ApplicationDbContext(_options);
        await using var gate = await new ProjectLifecycleLock(gateContext).AcquireAsync(project.Id);
        await using var observer = new NpgsqlConnection(_container.ConnectionString);
        await observer.OpenAsync();

        var deleteTask = DeleteAsync(project.Id);
        await WaitForWaitingAdvisoryLocksAsync(observer, 1);
        var linkTask = LinkStoreAsync(project.Id, product.Id, actorAccessor);
        await WaitForWaitingAdvisoryLocksAsync(observer, 2);

        await gate.CommitAsync();
        (await deleteTask).Should().BeTrue();
        (await linkTask).IsFailure.Should().BeTrue();
        await using var verify = new ApplicationDbContext(_options);
        (await verify.Set<ProjectStoreProduct>().AnyAsync(link =>
            link.ProjectId == project.Id && link.DeletedAt == null)).Should().BeFalse();
    }

    [Fact]
    public async Task ConcurrentDeleteAndSessionLink_CannotLeaveActiveLinkOnDeletedProject()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var user = NewUser(actorId);
        var project = NewProject(tenantId, ContentStatus.Draft, ContentVisibility.Private);
        var request = new TestingRequest
        {
            Title = "Race request",
            InstructionsType = InstructionType.Text,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(1),
            CreatedById = actorId,
            TenantId = tenantId
        };
        var location = new TestingLocation { Name = "Race lab", TenantId = tenantId };
        var session = new TestingSession
        {
            TestingRequestId = request.Id,
            LocationId = location.Id,
            SessionName = "Race session",
            SessionDate = DateTime.UtcNow,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(1),
            MaxTesters = 8,
            ManagerId = actorId,
            ManagerUserId = actorId,
            CreatedById = actorId,
            TenantId = tenantId
        };
        await SeedAsync(user, project, request, location, session);
        var actorAccessor = ActorAccessor(actorId, tenantId);

        await using var gateContext = new ApplicationDbContext(_options);
        await using var gate = await new ProjectLifecycleLock(gateContext).AcquireAsync(project.Id);
        await using var observer = new NpgsqlConnection(_container.ConnectionString);
        await observer.OpenAsync();

        var linkTask = LinkSessionAsync(session.Id, project.Id, actorAccessor);
        await WaitForWaitingAdvisoryLocksAsync(observer, 1);
        var deleteTask = DeleteAsync(project.Id);
        await WaitForWaitingAdvisoryLocksAsync(observer, 2);

        await gate.CommitAsync();
        (await linkTask).IsSuccess.Should().BeTrue();
        (await deleteTask).Should().BeTrue();
        await using var verify = new ApplicationDbContext(_options);
        (await verify.Set<SessionProject>().AnyAsync(link =>
            link.ProjectId == project.Id && link.IsActive && link.DeletedAt == null)).Should().BeFalse();
        (await verify.Set<TestingSession>().SingleAsync(candidate => candidate.Id == session.Id))
            .RegisteredProjectCount.Should().Be(0);
    }

    [Fact]
    public async Task ConcurrentDeleteAndSessionLink_DeleteFirstRejectsLink()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var project = NewProject(tenantId, ContentStatus.Draft, ContentVisibility.Private);
        var request = new TestingRequest
        {
            Title = "Delete-first request",
            InstructionsType = InstructionType.Text,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(1),
            CreatedById = actorId,
            TenantId = tenantId
        };
        var location = new TestingLocation { Name = "Delete-first lab", TenantId = tenantId };
        var session = new TestingSession
        {
            TestingRequestId = request.Id,
            LocationId = location.Id,
            SessionName = "Delete-first session",
            SessionDate = DateTime.UtcNow,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(1),
            MaxTesters = 8,
            ManagerId = actorId,
            ManagerUserId = actorId,
            CreatedById = actorId,
            TenantId = tenantId
        };
        await SeedAsync(NewUser(actorId), project, request, location, session);
        var actorAccessor = ActorAccessor(actorId, tenantId);

        await using var gateContext = new ApplicationDbContext(_options);
        await using var gate = await new ProjectLifecycleLock(gateContext).AcquireAsync(project.Id);
        await using var observer = new NpgsqlConnection(_container.ConnectionString);
        await observer.OpenAsync();

        var deleteTask = DeleteAsync(project.Id);
        await WaitForWaitingAdvisoryLocksAsync(observer, 1);
        var linkTask = LinkSessionAsync(session.Id, project.Id, actorAccessor);
        await WaitForWaitingAdvisoryLocksAsync(observer, 2);

        await gate.CommitAsync();
        (await deleteTask).Should().BeTrue();
        (await linkTask).IsFailure.Should().BeTrue();
        await using var verify = new ApplicationDbContext(_options);
        (await verify.Set<SessionProject>().AnyAsync(link =>
            link.ProjectId == project.Id && link.IsActive && link.DeletedAt == null)).Should().BeFalse();
    }

    [Fact]
    public async Task ConcurrentDeleteAndLaunchPlanCreate_CannotLeaveActivePlanOnDeletedProject()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var project = NewProject(tenantId, ContentStatus.Draft, ContentVisibility.Private);
        await SeedAsync(NewUser(actorId), project);
        var actorAccessor = ActorAccessor(actorId, tenantId);

        await using var gateContext = new ApplicationDbContext(_options);
        await using var gate = await new ProjectLifecycleLock(gateContext).AcquireAsync(project.Id);
        await using var observer = new NpgsqlConnection(_container.ConnectionString);
        await observer.OpenAsync();

        var createTask = CreateLaunchPlanAsync(project.Id, actorAccessor);
        await WaitForWaitingAdvisoryLocksAsync(observer, 1);
        var deleteTask = DeleteAsync(project.Id);
        await WaitForWaitingAdvisoryLocksAsync(observer, 2);

        await gate.CommitAsync();
        (await createTask).IsSuccess.Should().BeTrue();
        (await deleteTask).Should().BeTrue();
        await using var verify = new ApplicationDbContext(_options);
        (await verify.Set<LaunchPlan>().AnyAsync(plan =>
            plan.ProjectId == project.Id && plan.DeletedAt == null)).Should().BeFalse();
    }

    [Fact]
    public async Task ConcurrentDeleteAndLaunchPlanCreate_DeleteFirstRejectsCreate()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var project = NewProject(tenantId, ContentStatus.Draft, ContentVisibility.Private);
        await SeedAsync(NewUser(actorId), project);
        var actorAccessor = ActorAccessor(actorId, tenantId);

        await using var gateContext = new ApplicationDbContext(_options);
        await using var gate = await new ProjectLifecycleLock(gateContext).AcquireAsync(project.Id);
        await using var observer = new NpgsqlConnection(_container.ConnectionString);
        await observer.OpenAsync();

        var deleteTask = DeleteAsync(project.Id);
        await WaitForWaitingAdvisoryLocksAsync(observer, 1);
        var createTask = CreateLaunchPlanAsync(project.Id, actorAccessor);
        await WaitForWaitingAdvisoryLocksAsync(observer, 2);

        await gate.CommitAsync();
        (await deleteTask).Should().BeTrue();
        (await createTask).IsFailure.Should().BeTrue();
        await using var verify = new ApplicationDbContext(_options);
        (await verify.Set<LaunchPlan>().AnyAsync(plan =>
            plan.ProjectId == project.Id && plan.DeletedAt == null)).Should().BeFalse();
    }

    [Fact]
    public async Task ConcurrentDeleteAndTestingRequestCreate_CreateFirstClosesRequest()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var project = NewProject(tenantId, ContentStatus.Draft, ContentVisibility.Private);
        await SeedAsync(NewUser(actorId), project);
        var actorAccessor = ActorAccessor(actorId, tenantId);

        await using var gateContext = new ApplicationDbContext(_options);
        await using var gate = await new ProjectLifecycleLock(gateContext).AcquireAsync(project.Id);
        await using var observer = new NpgsqlConnection(_container.ConnectionString);
        await observer.OpenAsync();

        var createTask = CreateTestingRequestAsync(project.Id, actorId, actorAccessor);
        await WaitForWaitingAdvisoryLocksAsync(observer, 1);
        var deleteTask = DeleteAsync(project.Id);
        await WaitForWaitingAdvisoryLocksAsync(observer, 2);

        await gate.CommitAsync();
        var request = await createTask;
        (await deleteTask).Should().BeTrue();
        await using var verify = new ApplicationDbContext(_options);
        (await verify.Set<TestingRequest>().IgnoreQueryFilters()
            .SingleAsync(candidate => candidate.Id == request.Id)).DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ConcurrentDeleteAndTestingRequestCreate_DeleteFirstRejectsCreate()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var project = NewProject(tenantId, ContentStatus.Draft, ContentVisibility.Private);
        await SeedAsync(NewUser(actorId), project);
        var actorAccessor = ActorAccessor(actorId, tenantId);

        await using var gateContext = new ApplicationDbContext(_options);
        await using var gate = await new ProjectLifecycleLock(gateContext).AcquireAsync(project.Id);
        await using var observer = new NpgsqlConnection(_container.ConnectionString);
        await observer.OpenAsync();

        var deleteTask = DeleteAsync(project.Id);
        await WaitForWaitingAdvisoryLocksAsync(observer, 1);
        var createTask = CreateTestingRequestAsync(project.Id, actorId, actorAccessor);
        await WaitForWaitingAdvisoryLocksAsync(observer, 2);

        await gate.CommitAsync();
        (await deleteTask).Should().BeTrue();
        var waitForCreate = async () => await createTask;
        await waitForCreate.Should().ThrowAsync<InvalidOperationException>();
        await using var verify = new ApplicationDbContext(_options);
        (await verify.Set<TestingRequest>().AnyAsync()).Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentDeleteAndPublicTestingRequestCreate_CreateFirstClosesRequest(bool useCommandHandler)
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var project = NewProject(tenantId, ContentStatus.Draft, ContentVisibility.Private);
        var version = NewProjectVersion(project, actorId, tenantId);
        await SeedAsync(NewUser(actorId), project, version);
        var actorAccessor = ActorAccessor(actorId, tenantId);

        await using var gateContext = new ApplicationDbContext(_options);
        await using var gate = await new ProjectLifecycleLock(gateContext).AcquireAsync(project.Id);
        await using var observer = new NpgsqlConnection(_container.ConnectionString);
        await observer.OpenAsync();

        var createTask = CreatePublicTestingRequestAsync(version.Id, actorAccessor, useCommandHandler);
        await WaitForWaitingAdvisoryLocksAsync(observer, 1);
        var deleteTask = DeleteAsync(project.Id);
        await WaitForWaitingAdvisoryLocksAsync(observer, 2);

        await gate.CommitAsync();
        var request = await createTask;
        (await deleteTask).Should().BeTrue();
        await using var verify = new ApplicationDbContext(_options);
        (await verify.Set<TestingRequest>().IgnoreQueryFilters()
            .SingleAsync(candidate => candidate.Id == request.Id)).DeletedAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentDeleteAndPublicTestingRequestCreate_DeleteFirstRejectsCreate(bool useCommandHandler)
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var project = NewProject(tenantId, ContentStatus.Draft, ContentVisibility.Private);
        var version = NewProjectVersion(project, actorId, tenantId);
        await SeedAsync(NewUser(actorId), project, version);
        var actorAccessor = ActorAccessor(actorId, tenantId);

        await using var gateContext = new ApplicationDbContext(_options);
        await using var gate = await new ProjectLifecycleLock(gateContext).AcquireAsync(project.Id);
        await using var observer = new NpgsqlConnection(_container.ConnectionString);
        await observer.OpenAsync();

        var deleteTask = DeleteAsync(project.Id);
        await WaitForWaitingAdvisoryLocksAsync(observer, 1);
        var createTask = CreatePublicTestingRequestAsync(version.Id, actorAccessor, useCommandHandler);
        await WaitForWaitingAdvisoryLocksAsync(observer, 2);

        await gate.CommitAsync();
        (await deleteTask).Should().BeTrue();
        var waitForCreate = async () => await createTask;
        await waitForCreate.Should().ThrowAsync<InvalidOperationException>();
        await using var verify = new ApplicationDbContext(_options);
        (await verify.Set<TestingRequest>().AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task ConcurrentDeleteAndTestingRequestRestore_RestoreFirstClosesRequest()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var project = NewProject(tenantId, ContentStatus.Draft, ContentVisibility.Private);
        var version = NewProjectVersion(project, actorId, tenantId);
        var request = NewDeletedTestingRequest(version.Id, actorId, tenantId);
        await SeedAsync(NewUser(actorId), project, version, request);
        var actorAccessor = ActorAccessor(actorId, tenantId);

        await using var gateContext = new ApplicationDbContext(_options);
        await using var gate = await new ProjectLifecycleLock(gateContext).AcquireAsync(project.Id);
        await using var observer = new NpgsqlConnection(_container.ConnectionString);
        await observer.OpenAsync();

        var restoreTask = RestoreTestingRequestAsync(request.Id, actorAccessor);
        await WaitForWaitingAdvisoryLocksAsync(observer, 1);
        var deleteTask = DeleteAsync(project.Id);
        await WaitForWaitingAdvisoryLocksAsync(observer, 2);

        await gate.CommitAsync();
        (await restoreTask).Should().BeTrue();
        (await deleteTask).Should().BeTrue();
        await using var verify = new ApplicationDbContext(_options);
        (await verify.Set<TestingRequest>().IgnoreQueryFilters()
            .SingleAsync(candidate => candidate.Id == request.Id)).DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ConcurrentDeleteAndTestingRequestRestore_DeleteFirstRejectsRestore()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var project = NewProject(tenantId, ContentStatus.Draft, ContentVisibility.Private);
        var version = NewProjectVersion(project, actorId, tenantId);
        var request = NewDeletedTestingRequest(version.Id, actorId, tenantId);
        await SeedAsync(NewUser(actorId), project, version, request);
        var actorAccessor = ActorAccessor(actorId, tenantId);

        await using var gateContext = new ApplicationDbContext(_options);
        await using var gate = await new ProjectLifecycleLock(gateContext).AcquireAsync(project.Id);
        await using var observer = new NpgsqlConnection(_container.ConnectionString);
        await observer.OpenAsync();

        var deleteTask = DeleteAsync(project.Id);
        await WaitForWaitingAdvisoryLocksAsync(observer, 1);
        var restoreTask = RestoreTestingRequestAsync(request.Id, actorAccessor);
        await WaitForWaitingAdvisoryLocksAsync(observer, 2);

        await gate.CommitAsync();
        (await deleteTask).Should().BeTrue();
        var waitForRestore = async () => await restoreTask;
        await waitForRestore.Should().ThrowAsync<InvalidOperationException>();
        await using var verify = new ApplicationDbContext(_options);
        (await verify.Set<TestingRequest>().IgnoreQueryFilters()
            .SingleAsync(candidate => candidate.Id == request.Id)).DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task HardDelete_RemovesRestrictiveChannelRowsAndPreservesSafeTestingHistory()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var project = NewProject(tenantId, ContentStatus.Draft, ContentVisibility.Private);
        project.Id = Guid.NewGuid();
        project.CreatedById = actorId;
        var remainingProject = NewProject(tenantId, ContentStatus.Draft, ContentVisibility.Private);
        remainingProject.Id = Guid.NewGuid();
        var version = NewProjectVersion(project, actorId, tenantId);
        var request = NewTestingRequest(version.Id);
        request.CreatedById = actorId;
        request.TenantId = tenantId;
        var location = new TestingLocation { Id = Guid.NewGuid(), Name = "Hard-delete lab", TenantId = tenantId };
        var session = new TestingSession
        {
            Id = Guid.NewGuid(),
            TestingRequestId = request.Id,
            LocationId = location.Id,
            SessionName = "Hard-delete session",
            SessionDate = SystemClock.UtcNow,
            StartTime = SystemClock.UtcNow,
            EndTime = SystemClock.UtcNow.AddHours(1),
            MaxTesters = 8,
            RegisteredProjectCount = 99,
            ManagerId = actorId,
            ManagerUserId = actorId,
            CreatedById = actorId,
            TenantId = tenantId
        };
        var product = Product.Create("Hard-delete product", creatorId: actorId, tenantId: tenantId);
        var activeStoreLink = new ProjectStoreProduct
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            ProductId = product.Id,
            TenantId = tenantId
        };
        var historicalStoreLink = new ProjectStoreProduct
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            ProductId = product.Id,
            TenantId = tenantId,
            DeletedAt = SystemClock.UtcNow.AddDays(-1)
        };
        var activeSessionLink = NewSessionProject(session.Id, project.Id, actorId, tenantId);
        var historicalSessionLink = NewSessionProject(session.Id, project.Id, actorId, tenantId);
        historicalSessionLink.IsActive = false;
        historicalSessionLink.DeletedAt = SystemClock.UtcNow.AddDays(-1);
        var remainingSessionLink = NewSessionProject(session.Id, remainingProject.Id, actorId, tenantId);
        var launchPlan = new LaunchPlan
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            TenantId = tenantId,
            Name = "Hard-delete launch"
        };
        await SeedAsync(
            NewUser(actorId),
            project,
            remainingProject,
            version,
            request,
            location,
            session,
            product,
            activeStoreLink,
            historicalStoreLink,
            activeSessionLink,
            historicalSessionLink,
            remainingSessionLink,
            launchPlan);

        (await DeleteAsync(project.Id, softDelete: false)).Should().BeTrue();

        await using var verify = new ApplicationDbContext(_options);
        (await verify.Set<Project>().IgnoreQueryFilters().AnyAsync(candidate => candidate.Id == project.Id))
            .Should().BeFalse();
        (await verify.Set<ProjectStoreProduct>().IgnoreQueryFilters().AnyAsync(link => link.ProjectId == project.Id))
            .Should().BeFalse();
        (await verify.Set<SessionProject>().IgnoreQueryFilters().AnyAsync(link => link.ProjectId == project.Id))
            .Should().BeFalse();
        (await verify.Set<TestingSession>().SingleAsync(candidate => candidate.Id == session.Id))
            .RegisteredProjectCount.Should().Be(1);
        (await verify.Set<LaunchPlan>().IgnoreQueryFilters().AnyAsync(plan => plan.ProjectId == project.Id))
            .Should().BeFalse();
        var preservedRequest = await verify.Set<TestingRequest>().IgnoreQueryFilters()
            .SingleAsync(candidate => candidate.Id == request.Id);
        preservedRequest.DeletedAt.Should().BeNull();
        preservedRequest.ProjectVersionId.Should().BeNull();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    private async Task<Result<ProjectStoreProductProjection>> LinkStoreAsync(
        Guid projectId,
        Guid productId,
        IActorContextAccessor actorAccessor)
    {
        await using var context = new ApplicationDbContext(_options);
        return await new ProjectStoreProductHandlers(
                context,
                actorAccessor,
                new ProjectChannelAvailabilityService(context),
                new AllowAllProjectAuthorizationService(),
                NullLogger<ProjectStoreProductHandlers>.Instance,
                new ProjectLifecycleLock(context))
            .Handle(new LinkProjectStoreProductCommand(projectId, productId), default);
    }

    private async Task<Result<SessionProjectProjection>> LinkSessionAsync(
        Guid sessionId,
        Guid projectId,
        IActorContextAccessor actorAccessor)
    {
        await using var context = new ApplicationDbContext(_options);
        return await new SessionProjectHandlers(
                context,
                actorAccessor,
                new ProjectChannelAvailabilityService(context),
                new AllowAllProjectAuthorizationService(),
                NullLogger<SessionProjectHandlers>.Instance,
                new ProjectLifecycleLock(context))
            .Handle(new LinkSessionProjectCommand(sessionId, projectId), default);
    }

    private async Task<Result<LaunchPlan>> CreateLaunchPlanAsync(
        Guid projectId,
        IActorContextAccessor actorAccessor)
    {
        await using var context = new ApplicationDbContext(_options);
        return await new LaunchPadHandlers(
                context,
                actorAccessor,
                new ProjectChannelAvailabilityService(context),
                new AllowAllProjectAuthorizationService(),
                NullLogger<LaunchPadHandlers>.Instance,
                new ProjectLifecycleLock(context))
            .Handle(new CreateLaunchPlanCommand { ProjectId = projectId, Name = "Race plan" }, default);
    }

    private async Task<TestingRequest> CreateTestingRequestAsync(
        Guid projectId,
        Guid actorId,
        IActorContextAccessor actorAccessor)
    {
        await using var context = new ApplicationDbContext(_options);
        return await new TestingRequestOperationsService(
                context,
                new ProjectChannelAvailabilityService(context),
                new AllowAllProjectAuthorizationService(),
                actorAccessor,
                new ProjectLifecycleLock(context))
            .CreateSimpleTestingRequestAsync(new CreateSimpleTestingRequestDto
            {
                ProjectId = projectId,
                Title = "Race request",
                VersionNumber = "1.0.0",
                InstructionsType = InstructionType.Text,
                InstructionsContent = "Exercise the project lifecycle."
            }, actorId);
    }

    private async Task<TestingRequest> CreatePublicTestingRequestAsync(
        Guid projectVersionId,
        IActorContextAccessor actorAccessor,
        bool useCommandHandler)
    {
        await using var context = new ApplicationDbContext(_options);
        var operations = new TestingRequestOperationsService(
            context,
            new ProjectChannelAvailabilityService(context),
            new AllowAllProjectAuthorizationService(),
            actorAccessor,
            new ProjectLifecycleLock(context));
        if (!useCommandHandler)
        {
            return await operations.CreateTestingRequestAsync(NewTestingRequest(projectVersionId));
        }

        var mediator = new Mock<IMediator>();
        mediator.Setup(candidate => candidate.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return await new CreateTestingRequestCommandHandler(
                Mock.Of<ITestingRequestRepository>(),
                new TestingRequestService(operations, Mock.Of<ITestingParticipantOperations>()),
                mediator.Object)
            .Handle(new CreateTestingRequestCommand(
                projectVersionId,
                "Public race request",
                null,
                null,
                InstructionType.Text,
                "Exercise lifecycle serialization.",
                null,
                null,
                null,
                4,
                SystemClock.UtcNow,
                SystemClock.UtcNow.AddDays(1)), default);
    }

    private async Task<bool> RestoreTestingRequestAsync(
        Guid requestId,
        IActorContextAccessor actorAccessor)
    {
        await using var context = new ApplicationDbContext(_options);
        return await new TestingRequestOperationsService(
                context,
                new ProjectChannelAvailabilityService(context),
                new AllowAllProjectAuthorizationService(),
                actorAccessor,
                new ProjectLifecycleLock(context))
            .RestoreTestingRequestAsync(requestId);
    }

    private async Task<bool> DeleteAsync(Guid projectId, bool softDelete = true)
    {
        await using var context = new ApplicationDbContext(_options);
        return await new ProjectLifecycleCoordinator(
                context,
                [
                    new ProjectStoreProductLifecycleParticipant(context),
                    new TestingLabProjectLifecycleParticipant(context),
                    new LaunchPadProjectLifecycleParticipant(context)
                ],
                new ProjectLifecycleLock(context))
            .DeleteAsync(projectId, softDelete);
    }

    private async Task SeedAsync(params object[] entities)
    {
        await using var context = new ApplicationDbContext(_options);
        context.AddRange(entities);
        await context.SaveChangesAsync();
    }

    private static Project NewProject(
        Guid tenantId,
        ContentStatus status,
        ContentVisibility visibility) => new()
    {
        Title = "Race project",
        Slug = $"race-project-{Guid.NewGuid():N}",
        TenantId = tenantId,
        Status = status,
        Visibility = visibility
    };

    private static ProjectVersion NewProjectVersion(Project project, Guid actorId, Guid tenantId) =>
        ProjectVersion.Create(project.Id, "1.0.0", releaseNotes: null, actorId, tenantId);

    private static TestingRequest NewTestingRequest(Guid projectVersionId) => new()
    {
        Id = Guid.NewGuid(),
        ProjectVersionId = projectVersionId,
        Title = "Public race request",
        InstructionsType = InstructionType.Text,
        StartDate = SystemClock.UtcNow,
        EndDate = SystemClock.UtcNow.AddDays(1)
    };

    private static TestingRequest NewDeletedTestingRequest(
        Guid projectVersionId,
        Guid actorId,
        Guid tenantId)
    {
        var request = NewTestingRequest(projectVersionId);
        request.CreatedById = actorId;
        request.TenantId = tenantId;
        request.DeletedAt = SystemClock.UtcNow.AddDays(-1);
        return request;
    }

    private static SessionProject NewSessionProject(
        Guid sessionId,
        Guid projectId,
        Guid actorId,
        Guid tenantId) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = sessionId,
        ProjectId = projectId,
        RegisteredById = actorId,
        TenantId = tenantId,
        IsActive = true
    };

    private static User NewUser(Guid userId) => new()
    {
        Id = userId,
        Email = $"{userId:N}@example.com",
        Name = "Project channel race actor",
        IsActive = true
    };

    private static IActorContextAccessor ActorAccessor(Guid actorId, Guid tenantId)
    {
        var accessor = new ActorContextAccessor();
        accessor.SetActorContext(ActorContextBuilder.ForUser(actorId).WithTenantId(tenantId).Build());
        return accessor;
    }

    private static async Task WaitForWaitingAdvisoryLocksAsync(
        NpgsqlConnection connection,
        int minimumCount)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            await using var command = new NpgsqlCommand(
                "SELECT COUNT(*) FROM pg_locks WHERE locktype = 'advisory' AND NOT granted AND database = (SELECT oid FROM pg_database WHERE datname = current_database())",
                connection);
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) >= minimumCount)
            {
                return;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException($"Timed out waiting for {minimumCount} advisory lock waiters.");
    }

    private sealed class AllowAllProjectAuthorizationService : IProjectAuthorizationService
    {
        public Task<bool> IsActorActiveTenantMemberAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> HasPermissionAsync(
            Guid projectId,
            PermissionType permission,
            CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }
}
