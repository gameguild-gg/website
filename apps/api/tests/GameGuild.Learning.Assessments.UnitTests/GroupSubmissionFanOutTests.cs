using FluentAssertions;
using GameGuild.Learning.Courses;
using GameGuild.Learning.Enrollments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Learning.Assessments.Tests;

public class GroupSubmissionFanOutTests
{
    [Fact]
    public async Task Submit_FansOutToAllGroupMembers()
    {
        await using var db = CreateContext();
        var fixture = await SeedGroupAsync(db);
        var (aUser, aEnrollment) = fixture.Members[0];
        var service = CreateService(db);

        var start = await service.StartSubmissionAsync(fixture.Assessment.Id, aEnrollment.Id, aUser);
        start.IsSuccess.Should().BeTrue();
        start.Value.CourseGroupId.Should().Be(fixture.GroupId);
        start.Value.AttemptNumber.Should().Be(1);

        var submit = await service.SubmitAsync(start.Value.Id, new SubmitAssessmentRequest(TextPayload: "group answer"));

        submit.IsSuccess.Should().BeTrue();
        var rows = await db.Set<AssessmentSubmission>()
            .Where(s => s.AssessmentId == fixture.Assessment.Id)
            .ToListAsync();
        rows.Should().HaveCount(3);
        rows.Select(r => r.UserId).Should().BeEquivalentTo(fixture.Members.Select(m => m.UserId));
        rows.Should().OnlyContain(r =>
            r.CourseGroupId == fixture.GroupId &&
            r.AttemptNumber == 1 &&
            r.Status == SubmissionStatus.Submitted &&
            r.TextPayload == "group answer");
        rows.Select(r => r.SubmittedAt).Distinct().Should().ContainSingle();
    }

    [Fact]
    public async Task Grade_FansOutIdenticalGradeToAllMembersAndPostsAgsPerMember()
    {
        await using var db = CreateContext();
        var fixture = await SeedGroupAsync(db);
        var (aUser, aEnrollment) = fixture.Members[0];
        await StartAndSubmitAsync(db, fixture, memberIndex: 0);
        var lti = new Mock<ILtiScorePassback>();
        var service = new AssessmentService(db, Mock.Of<IProgramContentService>(), new RubricService(db, NullLogger<RubricService>.Instance), NullLogger<AssessmentService>.Instance, lti.Object);
        var gradedRow = await db.Set<AssessmentSubmission>()
            .SingleAsync(s => s.AssessmentId == fixture.Assessment.Id && s.UserId == aUser);
        var gradedBy = Guid.NewGuid();

        var result = await service.GradeSubmissionAsync(
            gradedRow.Id, new GradeSubmissionRequest(Score(85), gradedBy, "Solid work"));

        result.IsSuccess.Should().BeTrue();
        var rows = await db.Set<AssessmentSubmission>()
            .Where(s => s.AssessmentId == fixture.Assessment.Id)
            .ToListAsync();
        rows.Should().HaveCount(3);
        rows.Should().OnlyContain(r =>
            r.Status == SubmissionStatus.Graded &&
            r.Score == Score(85) &&
            r.Passed == true &&
            r.Feedback == "Solid work" &&
            r.GradedBy == gradedBy &&
            r.RubricScoresPayload == null);
        foreach (var member in fixture.Members)
        {
            lti.Verify(p => p.PostScoreIfMappedAsync(fixture.Assessment.Id, member.UserId, Score(85), Score(100)), Times.Once);
        }
    }

    [Fact]
    public async Task Submit_WhenSiblingStartedFirst_ReusesInProgressRowWithoutDuplicate()
    {
        await using var db = CreateContext();
        var fixture = await SeedGroupAsync(db);
        var (_, bEnrollment) = fixture.Members[1];
        var (aUser, aEnrollment) = fixture.Members[0];
        var service = CreateService(db);

        var bStart = await service.StartSubmissionAsync(fixture.Assessment.Id, bEnrollment.Id, fixture.Members[1].UserId);
        bStart.Value.AttemptNumber.Should().Be(1);

        var aStart = await service.StartSubmissionAsync(fixture.Assessment.Id, aEnrollment.Id, aUser);
        // Members starting the same group attempt concurrently share one attempt number;
        // a per-member bump would fragment the group attempt (UX unique index forbids a second row).
        aStart.Value.AttemptNumber.Should().Be(1);

        var submit = await service.SubmitAsync(aStart.Value.Id, new SubmitAssessmentRequest(TextPayload: "group answer"));

        submit.IsSuccess.Should().BeTrue();
        var rows = await db.Set<AssessmentSubmission>()
            .Where(s => s.AssessmentId == fixture.Assessment.Id)
            .ToListAsync();
        rows.Should().HaveCount(3);
        var bRow = rows.Single(r => r.UserId == fixture.Members[1].UserId);
        bRow.Id.Should().Be(bStart.Value.Id, "the sibling's InProgress row must be reused, not duplicated");
        bRow.Status.Should().Be(SubmissionStatus.Submitted);
        bRow.TextPayload.Should().Be("group answer");
    }

    [Fact]
    public async Task Submit_WhenSiblingRowAlreadySubmitted_SkipsSiblingWithoutFailure()
    {
        await using var db = CreateContext();
        var fixture = await SeedGroupAsync(db);
        var (aUser, aEnrollment) = fixture.Members[0];
        var (cUser, cEnrollment) = fixture.Members[2];
        var aStart = await CreateService(db).StartSubmissionAsync(fixture.Assessment.Id, aEnrollment.Id, aUser);
        var cRow = AssessmentSubmission.Start(fixture.Assessment.Id, cEnrollment.Id, cUser, 1);
        cRow.SetPayload(new SubmitAssessmentRequest(TextPayload: "c solo"), SubmissionModality.Text);
        cRow.Submit();
        cRow.StampCourseGroup(fixture.GroupId);
        db.Add(cRow);
        await db.SaveChangesAsync();
        var cSubmittedAt = cRow.SubmittedAt;

        var submit = await CreateService(db).SubmitAsync(aStart.Value.Id, new SubmitAssessmentRequest(TextPayload: "group answer"));

        submit.IsSuccess.Should().BeTrue();
        var rows = await db.Set<AssessmentSubmission>()
            .Where(s => s.AssessmentId == fixture.Assessment.Id)
            .ToListAsync();
        rows.Should().HaveCount(3);
        var cAfter = rows.Single(r => r.UserId == cUser);
        cAfter.Id.Should().Be(cRow.Id);
        cAfter.TextPayload.Should().Be("c solo");
        cAfter.SubmittedAt.Should().Be(cSubmittedAt);
        rows.Should().OnlyContain(r => r.Status == SubmissionStatus.Submitted);
    }

    [Fact]
    public async Task Submit_DoesNotCloneForMemberWithDroppedEnrollment()
    {
        await using var db = CreateContext();
        var fixture = await SeedGroupAsync(db);
        var droppedUser = Guid.NewGuid();
        var dropped = Enrollment.Create(fixture.CourseId, droppedUser);
        dropped.Drop();
        db.AddRange(dropped, CourseGroupMember.Create(fixture.GroupId, droppedUser));
        await db.SaveChangesAsync();
        var (aUser, aEnrollment) = fixture.Members[0];
        var start = await CreateService(db).StartSubmissionAsync(fixture.Assessment.Id, aEnrollment.Id, aUser);

        var submit = await CreateService(db).SubmitAsync(start.Value.Id, new SubmitAssessmentRequest(TextPayload: "group answer"));

        submit.IsSuccess.Should().BeTrue();
        (await db.Set<AssessmentSubmission>().CountAsync(s => s.AssessmentId == fixture.Assessment.Id))
            .Should().Be(3);
        (await db.Set<AssessmentSubmission>().AnyAsync(s => s.UserId == droppedUser))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Start_WithoutGroupMembership_ReturnsValidationFailure()
    {
        await using var db = CreateContext();
        var fixture = await SeedGroupAsync(db);
        var lonerUser = Guid.NewGuid();
        var lonerEnrollment = Enrollment.Create(fixture.CourseId, lonerUser);
        db.Add(lonerEnrollment);
        await db.SaveChangesAsync();

        var result = await CreateService(db).StartSubmissionAsync(fixture.Assessment.Id, lonerEnrollment.Id, lonerUser);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Description.Should().Be("Join a group before attempting this assessment");
        (await db.Set<AssessmentSubmission>().AnyAsync(s => s.AssessmentId == fixture.Assessment.Id))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Start_AfterGroupAttemptSubmitted_RejectsSecondAttemptUntilContributionPolicyExists()
    {
        await using var db = CreateContext();
        var fixture = await SeedGroupAsync(db);
        var (aUser, aEnrollment) = fixture.Members[0];
        await StartAndSubmitAsync(db, fixture, memberIndex: 0);
        var service = CreateService(db);

        var secondStart = await service.StartSubmissionAsync(fixture.Assessment.Id, aEnrollment.Id, aUser);

        secondStart.IsSuccess.Should().BeFalse();
        secondStart.Error.Code.Should().Be("Assessment.MaxAttemptsReached");
        var rows = await db.Set<AssessmentSubmission>()
            .Where(s => s.AssessmentId == fixture.Assessment.Id)
            .ToListAsync();
        rows.Should().HaveCount(3);
        rows.Count(r => r.AttemptNumber == 1).Should().Be(3);
        rows.Should().NotContain(r => r.AttemptNumber == 2);
    }

    // ===== FIXTURE =====

    private static AssessmentService CreateService(TestGroupFanOutDbContext db) =>
        new(db, Mock.Of<IProgramContentService>(), new RubricService(db, NullLogger<RubricService>.Instance), NullLogger<AssessmentService>.Instance);

    private static async Task<AssessmentSubmission> StartAndSubmitAsync(
        TestGroupFanOutDbContext db, GroupFanOutFixture fixture, int memberIndex)
    {
        var service = CreateService(db);
        var (user, enrollment) = fixture.Members[memberIndex];
        var start = await service.StartSubmissionAsync(fixture.Assessment.Id, enrollment.Id, user);
        var submit = await service.SubmitAsync(start.Value.Id, new SubmitAssessmentRequest(TextPayload: "group answer"));
        submit.IsSuccess.Should().BeTrue();
        return submit.Value;
    }

    private sealed record GroupFanOutFixture(
        Guid CourseId,
        Guid GroupId,
        Assessment Assessment,
        (Guid UserId, Enrollment Enrollment)[] Members);

    private static async Task<GroupFanOutFixture> SeedGroupAsync(TestGroupFanOutDbContext db, int memberCount = 3)
    {
        var courseId = Guid.NewGuid();
        var set = CourseGroupSet.Create(courseId, "Project Groups");
        var group = CourseGroup.Create(set.Id, "Team A", Math.Max(2, memberCount));
        var assessment = Assessment.Create(courseId, "Group Project", AssessmentType.Project, Score(100));
        assessment.AssignToGroupSet(set.Id);
        db.AddRange(new Program { Id = courseId, PassingScore = Percent(60) }, set, group, assessment);
        var members = Enumerable.Range(0, memberCount).Select(_ =>
        {
            var user = Guid.NewGuid();
            var enrollment = Enrollment.Create(courseId, user);
            db.AddRange(enrollment, CourseGroupMember.Create(group.Id, user));
            return (user, enrollment);
        }).ToArray();
        await db.SaveChangesAsync();
        return new GroupFanOutFixture(courseId, group.Id, assessment, members);
    }

    private static TestGroupFanOutDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestGroupFanOutDbContext>()
            .UseInMemoryDatabase($"GroupFanOut_{Guid.NewGuid()}")
            .Options;
        return new TestGroupFanOutDbContext(options);
    }

    private sealed class TestGroupFanOutDbContext(DbContextOptions<TestGroupFanOutDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            new AssessmentsModelConfiguration().Configure(modelBuilder);
            // ponytail: minimal cross-module mappings; full mapping lives in ApplicationDbContext.
            modelBuilder.Entity<Enrollment>().HasKey(e => e.Id);
            modelBuilder.Entity<Program>(b =>
            {
                b.HasKey(p => p.Id);
                b.Ignore(p => p.ProgramContents);
                b.Ignore(p => p.ProgramUsers);
                b.Ignore(p => p.ProgramRatings);
                b.Ignore(p => p.ProgramWishlists);
            });
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Transactions are not required for fan-out tests.");
        }
    }
}
