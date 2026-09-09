using System.Security.Claims;
using FluentAssertions;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Moq;
using Xunit;

namespace GameGuild.Learning.Courses.UnitTests;

public sealed class CourseContentAccessRuleEvaluatorTests
{
    private readonly Mock<IProgramCrudService> _programService = new();
    private readonly Mock<IActorContextAccessor> _actorContextAccessor = new();
    private readonly Mock<IAuthorizationSinglePermissionChecker> _permissionChecker = new();

    public CourseContentAccessRuleEvaluatorTests()
    {
        _actorContextAccessor.SetupGet(accessor => accessor.ActorContext).Returns(ActorContext.Anonymous);
    }

    [Fact]
    public async Task PublicOutline_AllowsAnonymousOnlyForPublishedPublicProgram()
    {
        var program = new Program
        {
            Status = ContentStatus.Published,
            Visibility = ContentVisibility.Public
        };

        var result = await EvaluateAsync(program, "PublicOutline", AnonymousUser());

        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(ContentStatus.Draft, ContentVisibility.Public)]
    [InlineData(ContentStatus.Published, ContentVisibility.Private)]
    [InlineData(ContentStatus.Archived, ContentVisibility.Public)]
    public async Task PublicOutline_DeniesProgramsOutsideThePublicCatalog(
        ContentStatus status,
        ContentVisibility visibility)
    {
        var program = new Program { Status = status, Visibility = visibility };

        var result = await EvaluateAsync(program, "PublicOutline", AnonymousUser());

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Learner_AllowsAuthenticatedUserWithExistingEnrollmentProgress()
    {
        var userId = Guid.NewGuid();
        var program = new Program { Id = Guid.NewGuid() };
        _programService
            .Setup(service => service.GetUserProgressDtoAsync(program.Id, userId))
            .ReturnsAsync(new UserProgressDto(
                Guid.NewGuid(),
                program.Id,
                userId,
                PercentValue.Zero,
                null,
                null,
                null,
                []));

        var result = await EvaluateAsync(program, "Learner", AuthenticatedUser(userId));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Learner_AllowsJwtSubjectClaimWithExistingEnrollmentProgress()
    {
        var userId = Guid.NewGuid();
        var program = new Program { Id = Guid.NewGuid() };
        _programService
            .Setup(service => service.GetUserProgressDtoAsync(program.Id, userId))
            .ReturnsAsync(new UserProgressDto(
                Guid.NewGuid(),
                program.Id,
                userId,
                PercentValue.Zero,
                null,
                null,
                null,
                []));
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", userId.ToString())],
            "Bearer"));

        var result = await EvaluateAsync(program, "Learner", user);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Learner_DeniesAuthenticatedUserWithoutEnrollmentProgress()
    {
        var userId = Guid.NewGuid();
        var program = new Program { Id = Guid.NewGuid() };
        _programService
            .Setup(service => service.GetUserProgressDtoAsync(program.Id, userId))
            .ReturnsAsync((UserProgressDto?)null);

        var result = await EvaluateAsync(program, "Learner", AuthenticatedUser(userId));

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ViewAll_AllowsCreatorWithoutChangingCodingAssignmentManagementAccess()
    {
        var creatorId = Guid.NewGuid();
        var program = new Program { CreatorId = creatorId };

        var viewAll = await EvaluateAsync(program, "Manage", AuthenticatedUser(creatorId), allowCreator: true);
        var manageCodingAssignment = await EvaluateAsync(program, "Manage", AuthenticatedUser(creatorId));

        viewAll.IsSuccess.Should().BeTrue();
        manageCodingAssignment.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Manage_AllowsSystemAdministratorWithoutTenantPermissionLookup()
    {
        var userId = Guid.NewGuid();
        _actorContextAccessor.SetupGet(accessor => accessor.ActorContext).Returns(Actor(
            userId,
            tenantId: null,
            roles: new HashSet<string> { "SystemAdmin" }));

        var result = await EvaluateAsync(new Program(), "Manage", AuthenticatedUser(userId));

        result.IsSuccess.Should().BeTrue();
        _permissionChecker.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Manage_AllowsAnyExistingDynamicProgramPermissionInCurrentTenant()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var program = new Program { Id = Guid.NewGuid(), TenantId = tenantId };
        _actorContextAccessor.SetupGet(accessor => accessor.ActorContext).Returns(Actor(userId, tenantId));
        _permissionChecker
            .Setup(checker => checker.HasPermissionAsync(
                userId,
                tenantId,
                $"Program.{program.Id}.Read",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await EvaluateAsync(program, "Manage", AuthenticatedUser(userId, tenantId));

        result.IsSuccess.Should().BeTrue();
        _permissionChecker.Verify(checker => checker.HasPermissionAsync(
            userId,
            tenantId,
            $"Program.{program.Id}.Read",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Manage_DeniesPermissionLookupWithoutTenantContext()
    {
        var userId = Guid.NewGuid();
        _actorContextAccessor.SetupGet(accessor => accessor.ActorContext).Returns(Actor(userId, tenantId: null));

        var result = await EvaluateAsync(new Program(), "Manage", AuthenticatedUser(userId));

        result.IsSuccess.Should().BeFalse();
        _permissionChecker.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Evaluator_FailsClosedForWrongResourceOrUnknownAccessMode()
    {
        var user = AuthenticatedUser(Guid.NewGuid());

        var wrongResource = await EvaluateAsync(new object(), "Learner", user);
        var unknownMode = await EvaluateAsync(new Program(), "Unknown", user);

        wrongResource.IsSuccess.Should().BeFalse();
        unknownMode.IsSuccess.Should().BeFalse();
    }

    private async Task<RuleEvaluationResult> EvaluateAsync(
        object resource,
        string access,
        ClaimsPrincipal user,
        bool allowCreator = false)
    {
        var evaluator = new CourseContentAccessRuleEvaluator(
            _programService.Object,
            _actorContextAccessor.Object,
            _permissionChecker.Object);
        var context = new AuthorizationHandlerContext([], user, resource);
        var parameters = RuleParameters.FromJson($$"""
        {
          "access": "{{access}}",
          "allowCreator": {{allowCreator.ToString().ToLowerInvariant()}}
        }
        """);

        return await evaluator.EvaluateAsync(context, parameters);
    }

    private static ClaimsPrincipal AnonymousUser() => new(new ClaimsIdentity());

    private static ClaimsPrincipal AuthenticatedUser(Guid userId, Guid? tenantId = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
        if (tenantId.HasValue)
        {
            claims.Add(new Claim("tenant_id", tenantId.Value.ToString()));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private static ActorContext Actor(
        Guid userId,
        Guid? tenantId,
        IReadOnlySet<string>? roles = null)
    {
        return new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = userId.ToString(),
            TenantId = tenantId,
            Roles = roles ?? new HashSet<string>(),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            IsAuthenticated = true
        };
    }
}
