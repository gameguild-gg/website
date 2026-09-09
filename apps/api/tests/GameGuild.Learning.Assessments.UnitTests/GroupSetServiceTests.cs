using FluentAssertions;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Users;
using GameGuild.Learning.Courses;
using GameGuild.Learning.Enrollments;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Learning.Assessments.Tests;

public class GroupSetServiceTests
{
    // ===== SERVICE TESTS (in-memory DbContext, fixture pattern from InteractiveVideoCueServiceTests) =====

    [Fact]
    public async Task Join_HappyPath_PersistsMembershipRow()
    {
        await using var db = CreateContext();
        var (courseId, setId, groupId, userId) = await SeedSetWithGroupAsync(db, capacity: 4);
        await SeedEnrollmentAsync(db, courseId, userId);
        var service = CreateService(db);

        var result = await service.JoinAsync(courseId, groupId, userId);

        result.IsSuccess.Should().BeTrue();
        (await db.Set<CourseGroupMember>().CountAsync(m => m.GroupId == groupId && m.UserId == userId))
            .Should().Be(1);
    }

    [Fact]
    public async Task Join_FullGroup_ReturnsFailure()
    {
        await using var db = CreateContext();
        var (courseId, setId, groupId, userId) = await SeedSetWithGroupAsync(db, capacity: 2);
        await SeedEnrollmentAsync(db, courseId, userId);
        await SeedFullGroupAsync(db, groupId);
        var service = CreateService(db);

        var result = await service.JoinAsync(courseId, groupId, userId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Join_SecondGroupInSameSet_ReturnsFailure()
    {
        await using var db = CreateContext();
        var (courseId, setId, groupId, userId) = await SeedSetWithGroupAsync(db, capacity: 4);
        var secondGroup = CourseGroup.Create(setId, "Team B", 4);
        db.Add(secondGroup);
        await SeedEnrollmentAsync(db, courseId, userId);
        db.Add(CourseGroupMember.Create(secondGroup.Id, userId));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.JoinAsync(courseId, groupId, userId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Description.Should().Be("You are already in a group in this set.");
    }

    [Fact]
    public async Task Join_AfterAssessmentDueDate_ReturnsFailure()
    {
        await using var db = CreateContext();
        var (courseId, setId, groupId, userId) = await SeedSetWithGroupAsync(db, capacity: 4);
        await SeedEnrollmentAsync(db, courseId, userId);
        await SeedLockedAssessmentAsync(db, courseId, setId, dueAt: DateTime.UtcNow.AddHours(-1), lateDeadline: DateTime.UtcNow.AddHours(24));
        var service = CreateService(db);

        var result = await service.JoinAsync(courseId, groupId, userId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Join_WhenOnlyAvailableUntilPassed_ReturnsFailure()
    {
        await using var db = CreateContext();
        var (courseId, setId, groupId, userId) = await SeedSetWithGroupAsync(db, capacity: 4);
        await SeedEnrollmentAsync(db, courseId, userId);
        var assessment = Assessment.Create(courseId, "Project", AssessmentType.Project, Score(100));
        assessment.AssignToGroupSet(setId);
        assessment.SetDeliverySchedule(null, DateTime.UtcNow.AddHours(-1), null, false, null);
        db.Add(assessment);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.JoinAsync(courseId, groupId, userId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Join_WithoutActiveEnrollment_ReturnsFailure()
    {
        await using var db = CreateContext();
        var (courseId, setId, groupId, userId) = await SeedSetWithGroupAsync(db, capacity: 4);
        var dropped = Enrollment.Create(courseId, userId);
        dropped.Drop();
        db.Add(dropped);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.JoinAsync(courseId, groupId, userId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Join_GroupInAnotherCourse_ReturnsNotFound()
    {
        await using var db = CreateContext();
        var (courseId, setId, groupId, userId) = await SeedSetWithGroupAsync(db, capacity: 4);
        await SeedEnrollmentAsync(db, courseId, userId);
        var service = CreateService(db);

        var result = await service.JoinAsync(Guid.NewGuid(), groupId, userId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task InstructorAdd_AfterDueDate_BypassesLockAndSucceeds()
    {
        await using var db = CreateContext();
        var (courseId, setId, groupId, userId) = await SeedSetWithGroupAsync(db, capacity: 4);
        await SeedEnrollmentAsync(db, courseId, userId);
        await SeedLockedAssessmentAsync(db, courseId, setId, dueAt: DateTime.UtcNow.AddHours(-1), lateDeadline: null);
        var service = CreateService(db);

        var result = await service.AddMemberAsync(courseId, groupId, userId);

        result.IsSuccess.Should().BeTrue();
        (await db.Set<CourseGroupMember>().AnyAsync(m => m.GroupId == groupId && m.UserId == userId))
            .Should().BeTrue();
    }

    [Fact]
    public async Task InstructorAdd_FullGroup_ReturnsFailure()
    {
        await using var db = CreateContext();
        var (courseId, setId, groupId, userId) = await SeedSetWithGroupAsync(db, capacity: 2);
        await SeedEnrollmentAsync(db, courseId, userId);
        await SeedFullGroupAsync(db, groupId);
        var service = CreateService(db);

        var result = await service.AddMemberAsync(courseId, groupId, userId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task InstructorAdd_WithoutActiveEnrollment_ReturnsFailure()
    {
        await using var db = CreateContext();
        var (courseId, setId, groupId, userId) = await SeedSetWithGroupAsync(db, capacity: 4);
        var service = CreateService(db);

        var result = await service.AddMemberAsync(courseId, groupId, userId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Leave_HappyPath_RemovesMembership()
    {
        await using var db = CreateContext();
        var (courseId, setId, groupId, userId) = await SeedSetWithGroupAsync(db, capacity: 4);
        await SeedEnrollmentAsync(db, courseId, userId);
        db.Add(CourseGroupMember.Create(groupId, userId));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.LeaveAsync(courseId, groupId, userId);

        result.IsSuccess.Should().BeTrue();
        (await db.Set<CourseGroupMember>().CountAsync(m => m.GroupId == groupId && m.UserId == userId))
            .Should().Be(0);
    }

    [Fact]
    public async Task Leave_WhenLocked_ReturnsFailure()
    {
        await using var db = CreateContext();
        var (courseId, setId, groupId, userId) = await SeedSetWithGroupAsync(db, capacity: 4);
        await SeedEnrollmentAsync(db, courseId, userId);
        await SeedLockedAssessmentAsync(db, courseId, setId, dueAt: DateTime.UtcNow.AddHours(-1), lateDeadline: null);
        db.Add(CourseGroupMember.Create(groupId, userId));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.LeaveAsync(courseId, groupId, userId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task InstructorRemove_AfterDueDate_Succeeds()
    {
        await using var db = CreateContext();
        var (courseId, setId, groupId, userId) = await SeedSetWithGroupAsync(db, capacity: 4);
        await SeedEnrollmentAsync(db, courseId, userId);
        await SeedLockedAssessmentAsync(db, courseId, setId, dueAt: DateTime.UtcNow.AddHours(-1), lateDeadline: null);
        db.Add(CourseGroupMember.Create(groupId, userId));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.RemoveMemberAsync(courseId, groupId, userId);

        result.IsSuccess.Should().BeTrue();
        (await db.Set<CourseGroupMember>().AnyAsync(m => m.GroupId == groupId && m.UserId == userId))
            .Should().BeFalse();
    }

    [Fact]
    public async Task RemoveMembership_ThatDoesNotExist_ReturnsNotFound()
    {
        await using var db = CreateContext();
        var (courseId, setId, groupId, userId) = await SeedSetWithGroupAsync(db, capacity: 4);
        var service = CreateService(db);

        var result = await service.RemoveMemberAsync(courseId, groupId, userId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task GetGroupSetGroups_ReturnsMemberDisplayNamesAndCounts()
    {
        await using var db = CreateContext();
        var (courseId, setId, groupId, userId) = await SeedSetWithGroupAsync(db, capacity: 3);
        var namedUser = new User { Id = userId, Name = "Ada Lovelace", Email = "ada@example.com" };
        var orphanUserId = Guid.NewGuid();
        db.AddRange(namedUser, CourseGroupMember.Create(groupId, userId), CourseGroupMember.Create(groupId, orphanUserId));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.GetGroupSetGroupsAsync(courseId, setId);

        result.IsSuccess.Should().BeTrue();
        var group = result.Value.Should().ContainSingle().Subject;
        group.MemberCount.Should().Be(2);
        group.Members.Should().Contain(m => m.DisplayName == "Ada Lovelace")
            .And.Contain(m => m.DisplayName == orphanUserId.ToString());
    }

    [Fact]
    public async Task GetCourseGroupSets_ReturnsSetsWithGroupSummaries()
    {
        await using var db = CreateContext();
        var (courseId, setId, groupId, userId) = await SeedSetWithGroupAsync(db, capacity: 3);
        db.Add(CourseGroupMember.Create(groupId, userId));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var sets = await service.GetCourseGroupSetsAsync(courseId);

        var set = sets.Should().ContainSingle().Subject;
        set.Name.Should().Be("Project Groups");
        set.Groups.Should().ContainSingle()
            .Which.Should().Match<GroupSummaryDto>(g => g.Capacity == 3 && g.MemberCount == 1);
    }

    [Fact]
    public async Task GetGroupSetGroups_WhenSetInAnotherCourse_ReturnsNotFound()
    {
        await using var db = CreateContext();
        var (courseId, setId, groupId, userId) = await SeedSetWithGroupAsync(db, capacity: 3);
        var service = CreateService(db);

        var result = await service.GetGroupSetGroupsAsync(Guid.NewGuid(), setId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task CreateGroupSet_WithEmptyName_ReturnsValidationFailure()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        var result = await service.CreateGroupSetAsync(Guid.NewGuid(), "   ");

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task CreateGroup_WithCapacityBelowTwo_ReturnsValidationFailure()
    {
        await using var db = CreateContext();
        var (courseId, setId, groupId, userId) = await SeedSetWithGroupAsync(db, capacity: 2);
        var service = CreateService(db);

        var result = await service.CreateGroupAsync(courseId, setId, "Solo", 1);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task HasActiveEnrollmentAsync_DistinguishesActiveFromDropped()
    {
        await using var db = CreateContext();
        var courseId = Guid.NewGuid();
        var activeUser = Guid.NewGuid();
        var droppedUser = Guid.NewGuid();
        var active = Enrollment.Create(courseId, activeUser);
        var dropped = Enrollment.Create(courseId, droppedUser);
        dropped.Drop();
        db.AddRange(active, dropped);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        (await service.HasActiveEnrollmentAsync(courseId, activeUser)).Should().BeTrue();
        (await service.HasActiveEnrollmentAsync(courseId, droppedUser)).Should().BeFalse();
    }

    // ===== CONTROLLER TESTS (mock pattern from ControllerAndModuleTests) =====

    private readonly Mock<IGroupSetService> _svc = new();
    private readonly Mock<IActorContextAccessor> _actor = new();
    private readonly Mock<IProgramCrudService> _programs = new();
    private readonly Mock<IPermissionQueryService> _permissions = new();
    private readonly Mock<ILogger<GroupSetsController>> _log = new();

    private GroupSetsController CreateController(Guid? userId = null, bool isSystemAdmin = false, Guid? tenantId = null)
    {
        var uid = userId ?? Guid.NewGuid();
        _actor.Setup(a => a.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = uid.ToString(),
            TenantId = tenantId ?? Guid.NewGuid(),
            IsAuthenticated = true,
            Roles = isSystemAdmin ? new HashSet<string> { "SystemAdmin" } : new HashSet<string>(),
            Permissions = new HashSet<string>()
        });
        return new GroupSetsController(
            _svc.Object,
            _actor.Object,
            _programs.Object,
            _permissions.Object,
            _log.Object);
    }

    [Fact]
    public async Task CreateGroupSet_WhenActorCannotManageCourse_ReturnsForbidden()
    {
        var courseId = Guid.NewGuid();
        _programs.Setup(service => service.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, CreatorId = Guid.NewGuid() });

        var result = await CreateController().CreateGroupSet(courseId, new CreateGroupSetRequest("Sets"));

        result.Result.Should().BeOfType<ForbidResult>();
        _svc.Verify(service => service.CreateGroupSetAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CreateGroupSet_WhenActorIsProgramCreator_ReturnsCreated()
    {
        var actorId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        _programs.Setup(service => service.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, CreatorId = actorId });
        _svc.Setup(service => service.CreateGroupSetAsync(courseId, "Sets"))
            .ReturnsAsync(Result.Success(CourseGroupSet.Create(courseId, "Sets")));

        var result = await CreateController(actorId).CreateGroupSet(courseId, new CreateGroupSetRequest("Sets"));

        result.Result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task GetGroupSets_WhenActorHasActiveEnrollment_ReturnsOk()
    {
        var actorId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        _programs.Setup(service => service.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, CreatorId = Guid.NewGuid() });
        _svc.Setup(service => service.HasActiveEnrollmentAsync(courseId, actorId)).ReturnsAsync(true);
        _svc.Setup(service => service.GetCourseGroupSetsAsync(courseId))
            .ReturnsAsync(new List<GroupSetSummaryDto>());

        var result = await CreateController(actorId).GetGroupSets(courseId);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetGroupSets_WhenActorIsOutsideCourse_ReturnsForbidden()
    {
        var actorId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        _programs.Setup(service => service.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, CreatorId = Guid.NewGuid() });
        _svc.Setup(service => service.HasActiveEnrollmentAsync(courseId, actorId)).ReturnsAsync(false);

        var result = await CreateController(actorId).GetGroupSets(courseId);

        result.Result.Should().BeOfType<ForbidResult>();
        _svc.Verify(service => service.GetCourseGroupSetsAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task JoinGroup_WhenServiceRejects_ReturnsProblemDetails()
    {
        var actorId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        _svc.Setup(service => service.JoinAsync(courseId, groupId, actorId))
            .ReturnsAsync(Result.Failure<CourseGroupMember>(
                Error.Validation("GroupMembership.AlreadyInSet", "You are already in a group in this set.")));

        var result = await CreateController(actorId).JoinGroup(courseId, groupId);

        result.Result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().BeOfType<ProblemDetails>()
            .Which.Detail.Should().Be("You are already in a group in this set.");
    }

    [Fact]
    public async Task AddMember_WhenActorCannotManageCourse_ReturnsForbidden()
    {
        var courseId = Guid.NewGuid();
        _programs.Setup(service => service.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, CreatorId = Guid.NewGuid() });

        var result = await CreateController().AddMember(courseId, Guid.NewGuid(), Guid.NewGuid());

        result.Result.Should().BeOfType<ForbidResult>();
        _svc.Verify(service => service.AddMemberAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    // ===== FIXTURE =====

    private static GroupSetService CreateService(TestGroupDbContext db) =>
        new(db, NullLogger<GroupSetService>.Instance);

    private static async Task<(Guid CourseId, Guid SetId, Guid GroupId, Guid UserId)> SeedSetWithGroupAsync(
        TestGroupDbContext db, int capacity)
    {
        var courseId = Guid.NewGuid();
        var set = CourseGroupSet.Create(courseId, "Project Groups");
        var group = CourseGroup.Create(set.Id, "Team A", capacity);
        db.AddRange(set, group);
        await db.SaveChangesAsync();
        return (courseId, set.Id, group.Id, Guid.NewGuid());
    }

    private static async Task SeedEnrollmentAsync(TestGroupDbContext db, Guid courseId, Guid userId)
    {
        db.Add(Enrollment.Create(courseId, userId));
        await db.SaveChangesAsync();
    }

    private static async Task SeedFullGroupAsync(TestGroupDbContext db, Guid groupId)
    {
        db.AddRange(CourseGroupMember.Create(groupId, Guid.NewGuid()), CourseGroupMember.Create(groupId, Guid.NewGuid()));
        await db.SaveChangesAsync();
    }

    private static async Task SeedLockedAssessmentAsync(
        TestGroupDbContext db, Guid courseId, Guid setId, DateTime dueAt, DateTime? lateDeadline)
    {
        var assessment = Assessment.Create(courseId, "Project", AssessmentType.Project, Score(100));
        assessment.AssignToGroupSet(setId);
        assessment.SetDeliverySchedule(null, null, dueAt, lateDeadline.HasValue, lateDeadline);
        db.Add(assessment);
        await db.SaveChangesAsync();
    }

    private static TestGroupDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestGroupDbContext>()
            .UseInMemoryDatabase($"GroupSets_{Guid.NewGuid()}")
            .Options;
        return new TestGroupDbContext(options);
    }

    private sealed class TestGroupDbContext(DbContextOptions<TestGroupDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            new AssessmentsModelConfiguration().Configure(modelBuilder);
            // ponytail: minimal cross-module mappings for membership rules; full mapping lives in ApplicationDbContext.
            modelBuilder.Entity<Enrollment>().HasKey(e => e.Id);
            modelBuilder.Entity<User>(b =>
            {
                b.HasKey(u => u.Id);
                b.Ignore(u => u.Profile);
                b.Ignore(u => u.Metadata);
                b.Ignore(u => u.Preferences);
                b.Ignore(u => u.Notifications);
                b.Ignore(u => u.TenantMemberships);
            });
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Transactions are not required for group set tests.");
        }
    }
}
