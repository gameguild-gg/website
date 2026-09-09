using System.Text.Json;
using FluentAssertions;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
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

public class RubricServiceTests
{
    // ===== PUT (SaveAsync) =====

    [Fact]
    public async Task Save_ValidRubric_PersistsRubricCriteriaAndAssociation()
    {
        await using var db = CreateContext();
        var assessment = await SeedAssessmentAsync(db);
        var service = CreateRubricService(db);

        var result = await service.SaveAsync(assessment.Id, StandardRubric());

        result.IsSuccess.Should().BeTrue();
        var rubric = await db.Set<AssessmentRubric>().SingleAsync(r => r.Id == result.Value.Id);
        rubric.Title.Should().Be("Essay Rubric");
        var criteria = await db.Set<RubricCriterion>().Where(c => c.RubricId == rubric.Id).ToListAsync();
        criteria.Should().HaveCount(2);
        ScoreValue.Sum(criteria.Select(c => c.Points)).Should().Be(Score(100));
        (await db.Set<Assessment>().SingleAsync(a => a.Id == assessment.Id)).RubricId
            .Should().Be(rubric.Id);
    }

    [Fact]
    public async Task Save_SumMismatch_FailsWithExactMessage()
    {
        await using var db = CreateContext();
        var assessment = await SeedAssessmentAsync(db);
        var service = CreateRubricService(db);

        var result = await service.SaveAsync(assessment.Id, new SaveRubricRequest("Broken", [
            new SaveRubricCriterionRequest("Correctness", Score(60), 1),
            new SaveRubricCriterionRequest("Style", Score(30), 2)
        ]));

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Description.Should().Be("Rubric points must sum to assessment max score");
        (await db.Set<AssessmentRubric>().AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Save_EmptyCriteria_ReturnsValidationFailure()
    {
        await using var db = CreateContext();
        var assessment = await SeedAssessmentAsync(db);
        var service = CreateRubricService(db);

        var result = await service.SaveAsync(assessment.Id, new SaveRubricRequest("Empty", []));

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Save_EmptyTitle_ReturnsValidationFailure()
    {
        await using var db = CreateContext();
        var assessment = await SeedAssessmentAsync(db);
        var service = CreateRubricService(db);

        var result = await service.SaveAsync(assessment.Id, new SaveRubricRequest("   ", [
            new SaveRubricCriterionRequest("Only", Score(100), 1)
        ]));

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Save_SecondPut_ReplacesCriteriaKeepingRubricId()
    {
        await using var db = CreateContext();
        var assessment = await SeedAssessmentAsync(db);
        var service = CreateRubricService(db);
        var first = await service.SaveAsync(assessment.Id, StandardRubric());
        first.IsSuccess.Should().BeTrue();

        var result = await service.SaveAsync(assessment.Id, new SaveRubricRequest("Essay Rubric v2", [
            new SaveRubricCriterionRequest("Correctness", Score(30), 1),
            new SaveRubricCriterionRequest("Style", Score(30), 2),
            new SaveRubricCriterionRequest("Depth", Score(40), 3)
        ]));

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(first.Value.Id, "PUT replaces in place — same rubric id");
        result.Value.Title.Should().Be("Essay Rubric v2");
        var criteria = await db.Set<RubricCriterion>().Where(c => c.RubricId == first.Value.Id).ToListAsync();
        criteria.Should().HaveCount(3);
        criteria.Select(c => c.Description).Should().BeEquivalentTo(["Correctness", "Style", "Depth"]);
        (await db.Set<Assessment>().SingleAsync(a => a.Id == assessment.Id)).RubricId
            .Should().Be(first.Value.Id);
    }

    [Fact]
    public async Task Save_AfterAnyGradedSubmission_ReturnsConflict()
    {
        await using var db = CreateContext();
        var assessment = await SeedAssessmentAsync(db);
        await SaveStandardRubricAsync(db, assessment);
        await SeedGradedRowAsync(db, assessment);
        var service = CreateRubricService(db);

        var result = await service.SaveAsync(assessment.Id, StandardRubric());

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Description.Should().Be("Rubric locked after grading started");
    }

    // ===== GET / DELETE =====

    [Fact]
    public async Task Get_WhenAssessmentHasRubric_ReturnsCriteriaOrdered()
    {
        await using var db = CreateContext();
        var assessment = await SeedAssessmentAsync(db);
        await SaveStandardRubricAsync(db, assessment);

        var result = await CreateRubricService(db).GetAsync(assessment.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Essay Rubric");
        result.Value.Criteria.Select(c => c.Order).Should().BeInAscendingOrder();
        result.Value.Criteria.Select(c => c.Description).Should().Equal(["Correctness", "Style"]);
    }

    [Fact]
    public async Task Get_WhenNoRubric_ReturnsNotFound()
    {
        await using var db = CreateContext();
        var assessment = await SeedAssessmentAsync(db);

        var result = await CreateRubricService(db).GetAsync(assessment.Id);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Delete_RemovesRubricCriteriaAndAssociation()
    {
        await using var db = CreateContext();
        var assessment = await SeedAssessmentAsync(db);
        var rubric = await SaveStandardRubricAsync(db, assessment);

        var result = await CreateRubricService(db).DeleteAsync(assessment.Id);

        result.IsSuccess.Should().BeTrue();
        (await db.Set<AssessmentRubric>().AnyAsync(r => r.Id == rubric.Id)).Should().BeFalse();
        (await db.Set<RubricCriterion>().AnyAsync(c => c.RubricId == rubric.Id)).Should().BeFalse();
        (await db.Set<Assessment>().SingleAsync(a => a.Id == assessment.Id)).RubricId.Should().BeNull();
    }

    [Fact]
    public async Task Delete_WhenAnyGradedSubmission_ReturnsConflict()
    {
        await using var db = CreateContext();
        var assessment = await SeedAssessmentAsync(db);
        await SaveStandardRubricAsync(db, assessment);
        await SeedGradedRowAsync(db, assessment);

        var result = await CreateRubricService(db).DeleteAsync(assessment.Id);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Description.Should().Be("Rubric locked after grading started");
        (await db.Set<Assessment>().SingleAsync(a => a.Id == assessment.Id)).RubricId.Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_WhenNoRubric_ReturnsNotFound()
    {
        await using var db = CreateContext();
        var assessment = await SeedAssessmentAsync(db);

        var result = await CreateRubricService(db).DeleteAsync(assessment.Id);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    // ===== RUBRIC-AWARE GRADING (through AssessmentService.GradeSubmissionAsync) =====

    [Fact]
    public async Task Grade_WithRubricPartialCredit_PersistsScoresOnAllGroupRows()
    {
        await using var db = CreateContext();
        var (assessment, submissionId) = await SeedSubmittedGroupAttemptAsync(db);
        var rubric = await SaveStandardRubricAsync(db, assessment);
        var scores = Scores((rubric.Criteria[0].Id, 35, "mostly right"), (rubric.Criteria[1].Id, 5, null));

        var result = await CreateGradingService(db).GradeSubmissionAsync(
            submissionId, new GradeSubmissionRequest(Score(40), GradedBy: Guid.NewGuid(), RubricScores: scores));

        result.IsSuccess.Should().BeTrue();
        var rows = await db.Set<AssessmentSubmission>()
            .Where(s => s.AssessmentId == assessment.Id).ToListAsync();
        rows.Should().HaveCount(3, "group attempt fan-out");
        rows.Should().OnlyContain(r =>
            r.Status == SubmissionStatus.Graded &&
            r.Score == Score(40) &&
            r.RubricScoresPayload == scores);
    }

    [Fact]
    public async Task Grade_WithRubric_MissingRubricScores_FailsWithMessage()
    {
        await using var db = CreateContext();
        var (assessment, submissionId) = await SeedSubmittedAttemptAsync(db);
        await SaveStandardRubricAsync(db, assessment);

        var result = await CreateGradingService(db).GradeSubmissionAsync(
            submissionId, new GradeSubmissionRequest(Score(40), GradedBy: Guid.NewGuid()));

        result.IsSuccess.Should().BeFalse();
        result.Error.Description.Should().Be("A rubric score is required for rubric-graded assessments");
        (await db.Set<AssessmentSubmission>().SingleAsync(s => s.Id == submissionId)).Status
            .Should().Be(SubmissionStatus.Submitted, "grading must not partially apply");
    }

    [Fact]
    public async Task Grade_CriterionAboveItsMax_FailsNamingCriterion()
    {
        await using var db = CreateContext();
        var (assessment, submissionId) = await SeedSubmittedAttemptAsync(db);
        var rubric = await SaveStandardRubricAsync(db, assessment);
        var scores = Scores((rubric.Criteria[0].Id, 61, null), (rubric.Criteria[1].Id, 40, null));

        var result = await CreateGradingService(db).GradeSubmissionAsync(
            submissionId, new GradeSubmissionRequest(Score(101), GradedBy: Guid.NewGuid(), RubricScores: scores));

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Description.Should().Contain("Correctness");
    }

    [Fact]
    public async Task Grade_MissingCriterionId_FailsNamingCriterion()
    {
        await using var db = CreateContext();
        var (assessment, submissionId) = await SeedSubmittedAttemptAsync(db);
        var rubric = await SaveStandardRubricAsync(db, assessment);
        var scores = Scores((rubric.Criteria[0].Id, 40, null));

        var result = await CreateGradingService(db).GradeSubmissionAsync(
            submissionId, new GradeSubmissionRequest(Score(40), GradedBy: Guid.NewGuid(), RubricScores: scores));

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Description.Should().Contain("Style");
    }

    [Fact]
    public async Task Grade_SumNotEqualToScore_FailsWithMessage()
    {
        await using var db = CreateContext();
        var (assessment, submissionId) = await SeedSubmittedAttemptAsync(db);
        var rubric = await SaveStandardRubricAsync(db, assessment);
        var scores = Scores((rubric.Criteria[0].Id, 35, null), (rubric.Criteria[1].Id, 5, null));

        var result = await CreateGradingService(db).GradeSubmissionAsync(
            submissionId, new GradeSubmissionRequest(Score(50), GradedBy: Guid.NewGuid(), RubricScores: scores));

        result.IsSuccess.Should().BeFalse();
        result.Error.Description.Should().Be("Rubric scores must sum to the submitted score");
    }

    [Fact]
    public async Task Grade_MalformedRubricScores_ReturnsValidationNotCrash()
    {
        await using var db = CreateContext();
        var (assessment, submissionId) = await SeedSubmittedAttemptAsync(db);
        await SaveStandardRubricAsync(db, assessment);

        var result = await CreateGradingService(db).GradeSubmissionAsync(
            submissionId, new GradeSubmissionRequest(Score(40), GradedBy: Guid.NewGuid(), RubricScores: "not json"));

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Grade_UnknownCriterionIdInPayload_Fails()
    {
        await using var db = CreateContext();
        var (assessment, submissionId) = await SeedSubmittedAttemptAsync(db);
        var rubric = await SaveStandardRubricAsync(db, assessment);
        var unknownId = Guid.NewGuid();
        var scores = Scores((rubric.Criteria[0].Id, 20, null), (unknownId, 20, null));

        var result = await CreateGradingService(db).GradeSubmissionAsync(
            submissionId, new GradeSubmissionRequest(Score(40), GradedBy: Guid.NewGuid(), RubricScores: scores));

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Description.Should().Contain(unknownId.ToString());
    }

    [Fact]
    public async Task Grade_NoRubricWithRubricScores_FailsWithMessage()
    {
        await using var db = CreateContext();
        var (_, submissionId) = await SeedSubmittedAttemptAsync(db);

        var result = await CreateGradingService(db).GradeSubmissionAsync(
            submissionId, new GradeSubmissionRequest(Score(40), GradedBy: Guid.NewGuid(), RubricScores: Scores((Guid.NewGuid(), 40, null))));

        result.IsSuccess.Should().BeFalse();
        result.Error.Description.Should().Be("This assessment is not rubric-graded");
    }

    // ===== CONTROLLER TESTS (mock pattern from GroupSetServiceTests) =====

    private readonly Mock<IRubricService> _rubrics = new();
    private readonly Mock<IAssessmentService> _assessments = new();
    private readonly Mock<IActorContextAccessor> _actor = new();
    private readonly Mock<IProgramCrudService> _programs = new();
    private readonly Mock<IPermissionQueryService> _permissions = new();
    private readonly Mock<ILogger<RubricsController>> _log = new();

    private RubricsController CreateController(Guid? userId = null)
    {
        var uid = userId ?? Guid.NewGuid();
        _actor.Setup(a => a.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = uid.ToString(),
            TenantId = Guid.NewGuid(),
            IsAuthenticated = true,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>()
        });
        return new RubricsController(
            _rubrics.Object,
            _assessments.Object,
            _actor.Object,
            _programs.Object,
            _permissions.Object,
            _log.Object);
    }

    [Fact]
    public async Task Put_WhenActorCannotManageCourse_ReturnsForbidden()
    {
        var assessmentId = Guid.NewGuid();
        _assessments.Setup(s => s.GetAssessmentByIdAsync(assessmentId))
            .ReturnsAsync(Assessment.Create(Guid.NewGuid(), "Essay", AssessmentType.Assignment, Score(100)));
        _programs.Setup(s => s.GetProgramByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new Program { Id = Guid.NewGuid(), CreatorId = Guid.NewGuid() });

        var result = await CreateController().PutRubric(assessmentId, StandardRubric());

        result.Result.Should().BeOfType<ForbidResult>();
        _rubrics.Verify(s => s.SaveAsync(It.IsAny<Guid>(), It.IsAny<SaveRubricRequest>()), Times.Never);
    }

    [Fact]
    public async Task Put_WhenRubricLocked_ReturnsConflict()
    {
        var actorId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var assessmentId = Guid.NewGuid();
        var assessment = Assessment.Create(courseId, "Essay", AssessmentType.Assignment, Score(100));
        _assessments.Setup(s => s.GetAssessmentByIdAsync(assessmentId)).ReturnsAsync(assessment);
        _programs.Setup(s => s.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, CreatorId = actorId });
        _rubrics.Setup(s => s.SaveAsync(assessmentId, It.IsAny<SaveRubricRequest>()))
            .ReturnsAsync(Result.Failure<RubricDto>(Error.Conflict("Rubric.Locked", "Rubric locked after grading started")));

        var result = await CreateController(actorId).PutRubric(assessmentId, StandardRubric());

        result.Result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task ControllerGetRubric_WhenNoRubric_ReturnsNotFound()
    {
        var actorId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var assessmentId = Guid.NewGuid();
        _assessments.Setup(s => s.GetAssessmentByIdAsync(assessmentId))
            .ReturnsAsync(Assessment.Create(courseId, "Essay", AssessmentType.Assignment, Score(100)));
        _programs.Setup(s => s.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, CreatorId = actorId });
        _rubrics.Setup(s => s.GetAsync(assessmentId))
            .ReturnsAsync(Result.Failure<RubricDto>(Error.NotFound("Rubric", "No rubric is assigned to this assessment.")));

        var result = await CreateController(actorId).GetRubric(assessmentId);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ===== FIXTURE =====

    private static RubricService CreateRubricService(TestRubricDbContext db) =>
        new(db, NullLogger<RubricService>.Instance);

    private static AssessmentService CreateGradingService(TestRubricDbContext db) =>
        new(db, Mock.Of<IProgramContentService>(), CreateRubricService(db), NullLogger<AssessmentService>.Instance);

    private static SaveRubricRequest StandardRubric() => new("Essay Rubric", [
        new SaveRubricCriterionRequest("Correctness", Score(60), 1),
        new SaveRubricCriterionRequest("Style", Score(40), 2)
    ]);

    private static string Scores(params (Guid CriterionId, int Points, string? Comment)[] entries) =>
        JsonSerializer.Serialize(
            entries.ToDictionary(
                e => e.CriterionId.ToString(),
                e => new { points = Score(e.Points), comment = e.Comment }));

    private static async Task<Assessment> SeedAssessmentAsync(TestRubricDbContext db, int maxScore = 100)
    {
        var courseId = Guid.NewGuid();
        var assessment = Assessment.Create(courseId, "Essay", AssessmentType.Assignment, Score(maxScore));
        db.AddRange(new Program { Id = courseId, PassingScore = Percent(60) }, assessment);
        await db.SaveChangesAsync();
        return assessment;
    }

    private static async Task<RubricDto> SaveStandardRubricAsync(TestRubricDbContext db, Assessment assessment)
    {
        var result = await CreateRubricService(db).SaveAsync(assessment.Id, StandardRubric());
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    private static async Task<(Assessment Assessment, Guid SubmissionId)> SeedSubmittedAttemptAsync(TestRubricDbContext db)
    {
        var assessment = await SeedAssessmentAsync(db);
        var enrollment = Enrollment.Create(assessment.CourseId, Guid.NewGuid());
        db.Add(enrollment);
        await db.SaveChangesAsync();
        var service = CreateGradingService(db);
        var start = await service.StartSubmissionAsync(assessment.Id, enrollment.Id, enrollment.UserId);
        var submit = await service.SubmitAsync(start.Value.Id, new SubmitAssessmentRequest(TextPayload: "answer"));
        submit.IsSuccess.Should().BeTrue();
        return (assessment, submit.Value.Id);
    }

    private static async Task<(Assessment Assessment, Guid SubmissionId)> SeedSubmittedGroupAttemptAsync(TestRubricDbContext db)
    {
        var courseId = Guid.NewGuid();
        var set = CourseGroupSet.Create(courseId, "Project Groups");
        var group = CourseGroup.Create(set.Id, "Team A", 3);
        var assessment = Assessment.Create(courseId, "Group Essay", AssessmentType.Assignment, Score(100));
        assessment.AssignToGroupSet(set.Id);
        db.AddRange(new Program { Id = courseId, PassingScore = Percent(60) }, set, group, assessment);
        var members = Enumerable.Range(0, 3).Select(_ =>
        {
            var user = Guid.NewGuid();
            var enrollment = Enrollment.Create(courseId, user);
            db.AddRange(enrollment, CourseGroupMember.Create(group.Id, user));
            return enrollment;
        }).ToArray();
        await db.SaveChangesAsync();
        var service = CreateGradingService(db);
        var start = await service.StartSubmissionAsync(assessment.Id, members[0].Id, members[0].UserId);
        var submit = await service.SubmitAsync(start.Value.Id, new SubmitAssessmentRequest(TextPayload: "group answer"));
        submit.IsSuccess.Should().BeTrue();
        return (assessment, submit.Value.Id);
    }

    private static async Task SeedGradedRowAsync(TestRubricDbContext db, Assessment assessment)
    {
        var enrollment = Enrollment.Create(assessment.CourseId, Guid.NewGuid());
        var row = AssessmentSubmission.Start(assessment.Id, enrollment.Id, enrollment.UserId, 1);
        row.SetPayload(new SubmitAssessmentRequest(TextPayload: "answer"), SubmissionModality.Text);
        row.Submit();
        row.Grade(Score(80), Score(60), Score(100));
        db.AddRange(enrollment, row);
        await db.SaveChangesAsync();
    }

    private static TestRubricDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestRubricDbContext>()
            .UseInMemoryDatabase($"Rubrics_{Guid.NewGuid()}")
            .Options;
        return new TestRubricDbContext(options);
    }

    private sealed class TestRubricDbContext(DbContextOptions<TestRubricDbContext> options)
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
            throw new NotSupportedException("Transactions are not required for rubric tests.");
        }
    }
}
