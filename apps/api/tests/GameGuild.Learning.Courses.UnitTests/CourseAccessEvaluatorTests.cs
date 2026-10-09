using FluentAssertions;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Moq;
using Xunit;

namespace GameGuild.Learning.Courses.UnitTests;

public sealed class CourseAccessEvaluatorTests
{
    private readonly Mock<IProgramReadService> _programs = new();
    private readonly Mock<ICourseEnrollmentAccessReader> _enrollments = new();
    private readonly Mock<IActorContextAccessor> _actors = new();
    private readonly Mock<IPermissionQueryService> _permissions = new();

    [Fact]
    public async Task MissingCourse_ReturnsDeniedProjection()
    {
        var courseId = Guid.NewGuid();
        _programs.Setup(service => service.GetProgramByIdAsync(courseId)).ReturnsAsync((Program?)null);

        var result = await CreateEvaluator().GetCapabilitiesAsync(courseId);

        result.Should().Be(CourseAccessCapabilities.Denied(courseId));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task MissingAuthenticationOrTenant_FailsClosed(bool authenticated, bool hasTenant)
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var course = Course(tenantId, userId);
        SetActor(authenticated ? userId : null, hasTenant ? tenantId : null);

        var result = await CreateEvaluator().GetCapabilitiesAsync(course);

        result.CanAccessWorkspace.Should().BeFalse();
        result.CanLearn.Should().BeFalse();
        _permissions.VerifyNoOtherCalls();
        _enrollments.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CrossTenantCourse_FailsClosedBeforeMembershipOrGrantLookups()
    {
        var userId = Guid.NewGuid();
        SetActor(userId, Guid.NewGuid());

        var result = await CreateEvaluator().GetCapabilitiesAsync(Course(Guid.NewGuid(), userId));

        result.CourseExists.Should().BeFalse();
        result.CanAccessWorkspace.Should().BeFalse();
        _permissions.VerifyNoOtherCalls();
        _enrollments.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task NonMember_CannotUseOwnershipEnrollmentOrGrants()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var course = Course(tenantId, userId);
        SetActor(userId, tenantId);
        SetMembership(userId, tenantId, false);

        var result = await CreateEvaluator().GetCapabilitiesAsync(course);

        result.CourseExists.Should().BeTrue();
        result.IsTenantMember.Should().BeFalse();
        result.IsOwner.Should().BeFalse();
        result.CanAccessWorkspace.Should().BeFalse();
        _enrollments.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Owner_ReceivesAuthoringCapabilitiesButLearnStillRequiresEnrollment()
    {
        var ownerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var course = Course(tenantId, ownerId);
        SetActor(ownerId, tenantId);
        SetMembership(ownerId, tenantId, true);
        SetEnrollment(course.Id, ownerId, false);

        var result = await CreateEvaluator().GetCapabilitiesAsync(course);

        result.IsOwner.Should().BeTrue();
        result.CanEdit.Should().BeTrue();
        result.CanPublish.Should().BeTrue();
        result.CanReviewAsStaff.Should().BeTrue();
        result.CanLearn.Should().BeFalse();
        _permissions.Verify(service => service.HasTenantPermissionAsync(
            It.IsAny<Guid?>(),
            It.IsAny<Guid?>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreatorOfGlobalCourse_IsNotAContextualTenantOwner()
    {
        var creatorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var course = new Program
        {
            Id = Guid.NewGuid(),
            TenantId = null,
            CreatorId = creatorId
        };
        SetActor(creatorId, tenantId);
        SetMembership(creatorId, tenantId, true);
        SetEnrollment(course.Id, creatorId, false);

        var result = await CreateEvaluator().GetCapabilitiesAsync(course);

        result.IsOwner.Should().BeFalse();
        result.CanAccessWorkspace.Should().BeFalse();
    }

    [Fact]
    public async Task EnrolledMember_ReceivesOnlyLearnCapability()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var course = Course(tenantId, Guid.NewGuid());
        SetActor(userId, tenantId);
        SetMembership(userId, tenantId, true);
        SetEnrollment(course.Id, userId, true);

        var result = await CreateEvaluator().GetCapabilitiesAsync(course);

        result.HasActiveEnrollment.Should().BeTrue();
        result.CanLearn.Should().BeTrue();
        result.CanAccessWorkspace.Should().BeFalse();
    }

    [Theory]
    [InlineData(CourseCapability.Edit)]
    [InlineData(CourseCapability.Publish)]
    [InlineData(CourseCapability.StaffReview)]
    public async Task ExplicitGrant_GrantsOnlyItsNamedCapability(CourseCapability granted)
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var course = Course(tenantId, Guid.NewGuid());
        SetActor(userId, tenantId);
        SetMembership(userId, tenantId, true);
        SetEnrollment(course.Id, userId, false);
        SetGrant(userId, tenantId, course.Id, granted);

        var result = await CreateEvaluator().GetCapabilitiesAsync(course);

        result.CanEdit.Should().Be(granted == CourseCapability.Edit);
        result.CanPublish.Should().Be(granted == CourseCapability.Publish);
        result.CanReviewAsStaff.Should().Be(granted == CourseCapability.StaffReview);
        result.CanLearn.Should().BeFalse();
        result.CanAccessWorkspace.Should().BeTrue();
    }

    [Fact]
    public async Task OutsiderMember_ReceivesNoCourseCapability()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var course = Course(tenantId, Guid.NewGuid());
        SetActor(userId, tenantId);
        SetMembership(userId, tenantId, true);
        SetEnrollment(course.Id, userId, false);

        var result = await CreateEvaluator().GetCapabilitiesAsync(course);

        result.CanLearn.Should().BeFalse();
        result.CanAccessWorkspace.Should().BeFalse();
    }

    [Fact]
    public async Task SystemAdministrator_DoesNotReceiveLearnerAccessWithoutEnrollment()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var course = Course(tenantId, Guid.NewGuid());
        SetActor(userId, tenantId, isSystemAdmin: true);
        SetEnrollment(course.Id, userId, false);

        var result = await CreateEvaluator().GetCapabilitiesAsync(course);

        result.CanAccessWorkspace.Should().BeTrue();
        result.CanLearn.Should().BeFalse();
        _permissions.Verify(service => service.IsUserInTenantAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void PermissionNames_AreExactAndNonGrantableCapabilitiesAreRejected()
    {
        var courseId = Guid.NewGuid();

        CoursePermissionNames.For(courseId, CourseCapability.Edit)
            .Should().Be($"Program.{courseId}.Edit");
        CoursePermissionNames.For(courseId, CourseCapability.Publish)
            .Should().Be($"Program.{courseId}.Publish");
        CoursePermissionNames.For(courseId, CourseCapability.StaffReview)
            .Should().Be($"Program.{courseId}.Review");
        FluentActions.Invoking(() => CoursePermissionNames.For(courseId, CourseCapability.Learn))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    private CourseAccessEvaluator CreateEvaluator() => new(
        _programs.Object,
        _enrollments.Object,
        _actors.Object,
        _permissions.Object);

    private static Program Course(Guid tenantId, Guid creatorId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        CreatorId = creatorId
    };

    private void SetActor(Guid? userId, Guid? tenantId, bool isSystemAdmin = false)
    {
        _actors.SetupGet(accessor => accessor.ActorContext).Returns(new ActorContext
        {
            ActorKind = userId.HasValue ? ActorKind.User : ActorKind.Anonymous,
            SubjectId = userId?.ToString(),
            TenantId = tenantId,
            Roles = isSystemAdmin ? new HashSet<string> { "SystemAdmin" } : new HashSet<string>(),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            IsAuthenticated = userId.HasValue
        });
    }

    private void SetMembership(Guid userId, Guid tenantId, bool isMember) =>
        _permissions
            .Setup(service => service.IsUserInTenantAsync(
                userId,
                tenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(isMember);

    private void SetEnrollment(Guid courseId, Guid userId, bool isActive) =>
        _enrollments
            .Setup(reader => reader.HasActiveEnrollmentAsync(
                courseId,
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(isActive);

    private void SetGrant(
        Guid userId,
        Guid tenantId,
        Guid courseId,
        CourseCapability granted)
    {
        foreach (var capability in new[]
                 {
                     CourseCapability.Edit,
                     CourseCapability.Publish,
                     CourseCapability.StaffReview
                 })
        {
            _permissions
                .Setup(service => service.HasTenantPermissionAsync(
                    userId,
                    tenantId,
                    CoursePermissionNames.For(courseId, capability),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(capability == granted);
        }
    }
}
