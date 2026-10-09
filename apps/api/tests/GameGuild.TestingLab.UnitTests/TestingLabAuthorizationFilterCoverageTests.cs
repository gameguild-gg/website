using FluentAssertions;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using GameGuild.Projects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using Xunit;

namespace GameGuild.TestingLab.UnitTests;

public sealed class TestingLabAuthorizationFilterCoverageTests
{
    [Fact]
    public void Attribute_PreservesTheRequestedPermissionAndNormalizesTheOptionalParameter()
    {
        var withoutParameter = new RequireTestingLabPermissionAttribute(
            TestingLabActions.Read, TestingLabResourceTypes.Event);
        var withParameter = new RequireTestingLabPermissionAttribute(
            TestingLabActions.Edit, TestingLabResourceTypes.Event, "eventId");

        withoutParameter.Arguments.Should().Equal(
            TestingLabActions.Read, TestingLabResourceTypes.Event, string.Empty);
        withParameter.Arguments.Should().Equal(
            TestingLabActions.Edit, TestingLabResourceTypes.Event, "eventId");
    }

    [Fact]
    public async Task Filter_RejectsAnonymousMissingTenantAndInactiveTenantActors()
    {
        await using var context = Context();
        var anonymous = await AuthorizeAsync(context, ActorContext.Anonymous);
        var withoutTenant = await AuthorizeAsync(
            context, ActorContextBuilder.ForUser(Guid.NewGuid()).Build());

        var inactiveId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        AddIdentity(context, inactiveId, tenantId, userActive: false);
        await context.SaveChangesAsync();
        var inactive = await AuthorizeAsync(context, Actor(inactiveId, tenantId));

        anonymous.Result.Should().BeOfType<UnauthorizedResult>();
        withoutTenant.Result.Should().BeOfType<ForbidResult>();
        inactive.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Filter_RejectsMissingMalformedAndUnknownResourceIds()
    {
        await using var context = Context();
        var (actor, _, _) = await ActiveActor(context);

        var missing = await AuthorizeAsync(
            context, actor, TestingLabActions.Read, TestingLabResourceTypes.Event, "eventId");
        var malformed = await AuthorizeAsync(
            context, actor, TestingLabActions.Read, TestingLabResourceTypes.Event, "eventId",
            routeValue: "not-a-guid");
        var malformedQuery = await AuthorizeAsync(
            context, actor, TestingLabActions.Read, TestingLabResourceTypes.Event, "eventId",
            queryValue: "not-a-guid");
        var unknown = await AuthorizeAsync(
            context, actor, TestingLabActions.Read, "unknown", "resourceId",
            routeValue: Guid.NewGuid().ToString());

        missing.Result.Should().BeOfType<ForbidResult>();
        malformed.Result.Should().BeOfType<ForbidResult>();
        malformedQuery.Result.Should().BeOfType<ForbidResult>();
        unknown.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Filter_ResolvesResourceIdFromQueryAndUsesExplicitPermissionFallback()
    {
        await using var context = Context();
        var (actor, actorId, tenantId) = await ActiveActor(context);
        var request = Request(Guid.NewGuid(), tenantId);
        context.Set<TestingRequest>().Add(request);
        await context.SaveChangesAsync();

        var permission = PermissionService(allowed: true);
        var result = await AuthorizeAsync(
            context, actor, TestingLabActions.Edit, TestingLabResourceTypes.Request, "requestId",
            queryValue: request.Id.ToString(), permissions: permission.Object);

        result.Result.Should().BeNull();
        permission.Verify(service => service.HasPermissionAsync(
            actorId, tenantId, TestingLabActions.Edit, TestingLabResourceTypes.Request, request.Id), Times.Once);
    }

    [Fact]
    public void ResourceIdResolver_TreatsAnExplicitNullRouteValueAsMissing()
    {
        var context = FilterContext(null, null, null);
        context.RouteData.Values["eventId"] = null;
        var method = typeof(TestingLabPermissionAuthorizationFilter).GetMethod(
            "ResolveResourceId",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        method.Invoke(null, [context, "eventId"]).Should().BeNull();
    }

    [Fact]
    public async Task Filter_AllowsEventAndRequestOwnersWithoutConsultingPermissionService()
    {
        await using var context = Context();
        var (actor, actorId, tenantId) = await ActiveActor(context);
        var schedule = Schedule();
        var testingEvent = TestingEvent.Create(
            "Owned event", TestingEventMode.Online, actorId,
            schedule.Open, schedule.Close, schedule.Start, schedule.End,
            true, TestingEventApprovalMode.ManagerOnly, tenantId);
        var request = Request(actorId, tenantId);
        context.Set<TestingEvent>().Add(testingEvent);
        context.Set<TestingRequest>().Add(request);
        await context.SaveChangesAsync();
        var permission = PermissionService(allowed: false);

        var eventResult = await AuthorizeAsync(
            context, actor, TestingLabActions.Edit, TestingLabResourceTypes.Event, "eventId",
            routeValue: testingEvent.Id.ToString(), permissions: permission.Object);
        var requestResult = await AuthorizeAsync(
            context, actor, TestingLabActions.Edit, TestingLabResourceTypes.Request, "requestId",
            routeValue: request.Id.ToString(), permissions: permission.Object);

        eventResult.Result.Should().BeNull();
        requestResult.Result.Should().BeNull();
        permission.Verify(service => service.HasPermissionAsync(
            It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>()),
            Times.Never);
    }

    [Fact]
    public async Task Filter_AllowsEventCollectionReadForAnEventManager()
    {
        await using var context = Context();
        var (actor, actorId, tenantId) = await ActiveActor(context);
        var schedule = Schedule();
        context.Set<TestingEvent>().Add(TestingEvent.Create(
            "Managed event", TestingEventMode.Online, actorId,
            schedule.Open, schedule.Close, schedule.Start, schedule.End,
            true, TestingEventApprovalMode.ManagerOnly, tenantId));
        await context.SaveChangesAsync();

        var result = await AuthorizeAsync(
            context, actor, TestingLabActions.Read, TestingLabResourceTypes.Event);

        result.Result.Should().BeNull();
    }

    [Theory]
    [InlineData(TestingLabResourceTypes.Feedback, TestingLabActions.Read, true)]
    [InlineData(TestingLabResourceTypes.Feedback, TestingLabActions.Edit, false)]
    [InlineData(TestingLabResourceTypes.Participant, TestingLabActions.Read, true)]
    [InlineData(TestingLabResourceTypes.Participant, TestingLabActions.Edit, false)]
    [InlineData(TestingLabResourceTypes.Location, TestingLabActions.Read, false)]
    public async Task Filter_ResolvesFeedbackParticipantAndLocationOwnership(
        string resourceType, string action, bool ownerBypass)
    {
        await using var context = Context();
        var (actor, actorId, tenantId) = await ActiveActor(context);
        var resourceId = AddSimpleResource(context, resourceType, actorId, tenantId);
        await context.SaveChangesAsync();
        var permission = PermissionService(allowed: false);

        var result = await AuthorizeAsync(
            context, actor, action, resourceType, "id", resourceId.ToString(), permissions: permission.Object);

        if (ownerBypass)
        {
            result.Result.Should().BeNull();
        }
        else
        {
            result.Result.Should().BeOfType<ForbidResult>();
        }
    }

    [Theory]
    [InlineData(TestingLabActions.Read, PermissionType.Read)]
    [InlineData(TestingLabActions.Edit, PermissionType.Edit)]
    public async Task Filter_DelegatesApplicationOwnershipToProjectAuthorization(
        string action, PermissionType expectedPermission)
    {
        await using var context = Context();
        var (actor, _, tenantId) = await ActiveActor(context);
        var projectId = Guid.NewGuid();
        var application = TestingProjectApplication.Submit(
            Guid.NewGuid(), projectId, null, Guid.NewGuid(), null, tenantId);
        context.Set<TestingProjectApplication>().Add(application);
        await context.SaveChangesAsync();
        var projects = new Mock<IProjectAuthorizationService>();
        projects.Setup(service => service.HasPermissionAsync(
                projectId, expectedPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await AuthorizeAsync(
            context, actor, action, TestingLabResourceTypes.Application, "applicationId",
            application.Id.ToString(), projects: projects.Object);

        result.Result.Should().BeNull();
        projects.Verify(service => service.HasPermissionAsync(
            projectId, expectedPermission, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("manager")]
    [InlineData("creator")]
    public async Task Filter_AllowsSessionManagerOrCreator(string ownerField)
    {
        await using var context = Context();
        var (actor, actorId, tenantId) = await ActiveActor(context);
        var session = Session(tenantId);
        session.ManagerId = ownerField == "manager" ? actorId : Guid.NewGuid();
        session.CreatedById = ownerField == "creator" ? actorId : Guid.NewGuid();
        context.Set<TestingSession>().Add(session);
        await context.SaveChangesAsync();

        var result = await AuthorizeAsync(
            context, actor, TestingLabActions.Edit, TestingLabResourceTypes.Session, "sessionId",
            session.Id.ToString());

        result.Result.Should().BeNull();
    }

    [Theory]
    [InlineData("SystemAdmin")]
    [InlineData("TenantAdmin")]
    public async Task Filter_AllowsAdministratorsAfterActiveTenantValidation(string role)
    {
        await using var context = Context();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        AddIdentity(context, userId, tenantId);
        await context.SaveChangesAsync();
        var actor = ActorContextBuilder.ForUser(userId).WithTenantId(tenantId).WithRole(role).Build();

        var result = await AuthorizeAsync(context, actor);

        result.Result.Should().BeNull();
    }

    private static async Task<AuthorizationFilterContext> AuthorizeAsync(
        FilterDbContext context,
        ActorContext actor,
        string action = TestingLabActions.Read,
        string resourceType = TestingLabResourceTypes.Event,
        string? parameter = null,
        string? routeValue = null,
        string? queryValue = null,
        ITestingLabPermissionService? permissions = null,
        IProjectAuthorizationService? projects = null)
    {
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(value => value.ActorContext).Returns(actor);
        var filter = new TestingLabPermissionAuthorizationFilter(
            action,
            resourceType,
            parameter,
            accessor.Object,
            permissions ?? PermissionService(allowed: false).Object,
            projects ?? Mock.Of<IProjectAuthorizationService>(),
            context);
        var authorizationContext = FilterContext(parameter, routeValue, queryValue);
        await filter.OnAuthorizationAsync(authorizationContext);
        return authorizationContext;
    }

    private static AuthorizationFilterContext FilterContext(
        string? parameter, string? routeValue, string? queryValue)
    {
        var http = new DefaultHttpContext();
        var route = new RouteData();
        if (parameter != null && routeValue != null)
        {
            route.Values[parameter] = routeValue;
        }

        if (parameter != null && queryValue != null)
        {
            http.Request.QueryString = QueryString.Create(parameter, queryValue);
        }

        var action = new ActionContext(http, route, new ActionDescriptor(), new ModelStateDictionary());
        return new AuthorizationFilterContext(action, []);
    }

    private static Mock<ITestingLabPermissionService> PermissionService(bool allowed)
    {
        var service = new Mock<ITestingLabPermissionService>();
        service.Setup(value => value.HasPermissionAsync(
                It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>()))
            .ReturnsAsync(allowed);
        return service;
    }

    private static async Task<(ActorContext Actor, Guid UserId, Guid TenantId)> ActiveActor(FilterDbContext context)
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        AddIdentity(context, userId, tenantId);
        await context.SaveChangesAsync();
        return (Actor(userId, tenantId), userId, tenantId);
    }

    private static ActorContext Actor(Guid userId, Guid tenantId)
        => ActorContextBuilder.ForUser(userId).WithTenantId(tenantId).Build();

    private static void AddIdentity(
        IApplicationDbContext context, Guid userId, Guid tenantId, bool userActive = true)
    {
        context.Set<User>().Add(new User
        {
            Id = userId,
            Email = $"{userId:N}@example.com",
            Name = "Testing Lab actor",
            IsActive = userActive
        });
        context.Set<TenantMember>().Add(new TenantMember
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            Role = "Member",
            IsActive = true
        });
    }

    private static Guid AddSimpleResource(
        IApplicationDbContext context, string resourceType, Guid actorId, Guid tenantId)
    {
        if (resourceType == TestingLabResourceTypes.Feedback)
        {
            var feedback = new TestingFeedback
            {
                Id = Guid.NewGuid(), TenantId = tenantId, UserId = actorId,
                FeedbackData = "Useful", TestingContext = TestingContext.Online
            };
            context.Set<TestingFeedback>().Add(feedback);
            return feedback.Id;
        }
        if (resourceType == TestingLabResourceTypes.Participant)
        {
            var participant = new TestingParticipant
            {
                Id = Guid.NewGuid(), TenantId = tenantId, UserId = actorId,
                TestingRequestId = Guid.NewGuid()
            };
            context.Set<TestingParticipant>().Add(participant);
            return participant.Id;
        }

        var location = new TestingLocation
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Name = "Lab", MaxTestersCapacity = 10,
            MaxProjectsCapacity = 2
        };
        context.Set<TestingLocation>().Add(location);
        return location.Id;
    }

    private static TestingRequest Request(Guid creatorId, Guid tenantId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        CreatedById = creatorId,
        Title = "Testing request",
        StartDate = SystemClock.UtcNow,
        EndDate = SystemClock.UtcNow.AddDays(2)
    };

    private static TestingSession Session(Guid tenantId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        TestingRequestId = Guid.NewGuid(),
        LocationId = Guid.NewGuid(),
        SessionName = "Session",
        SessionDate = SystemClock.UtcNow.AddDays(1),
        StartTime = SystemClock.UtcNow.AddDays(1),
        EndTime = SystemClock.UtcNow.AddDays(1).AddHours(1),
        ManagerId = Guid.NewGuid(),
        ManagerUserId = Guid.NewGuid(),
        CreatedById = Guid.NewGuid()
    };

    private static (DateTime Open, DateTime Close, DateTime Start, DateTime End) Schedule()
    {
        var start = SystemClock.UtcNow.AddDays(4);
        return (start.AddDays(-3), start.AddDays(-1), start, start.AddHours(2));
    }

    private static FilterDbContext Context()
        => new(new DbContextOptionsBuilder<FilterDbContext>()
            .UseInMemoryDatabase($"testing-lab-filter-{Guid.NewGuid():N}")
            .Options);

    private sealed class FilterDbContext(DbContextOptions<FilterDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<TenantMember> TenantMembers => Set<TenantMember>();
        public DbSet<TestingEvent> TestingEvents => Set<TestingEvent>();
        public DbSet<TestingProjectApplication> TestingProjectApplications => Set<TestingProjectApplication>();
        public DbSet<TestingRequest> TestingRequests => Set<TestingRequest>();
        public DbSet<TestingSession> TestingSessions => Set<TestingSession>();
        public DbSet<TestingLocation> TestingLocations => Set<TestingLocation>();
        public DbSet<TestingFeedback> TestingFeedback => Set<TestingFeedback>();
        public DbSet<TestingParticipant> TestingParticipants => Set<TestingParticipant>();

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
