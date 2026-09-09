using FluentAssertions;
using GameGuild.Learning.Courses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Learning.Assessments.Tests;

[CollectionDefinition(nameof(SystemClockCollection), DisableParallelization = true)]
public sealed class SystemClockCollection;

[Collection(nameof(SystemClockCollection))]
public sealed class InteractiveVideoCueServiceTests
{
    private static readonly SemaphoreSlim ClockGate = new(1, 1);

    [Fact]
    public async Task SubmitAsync_AtDueBoundary_UsesOneCapturedTimestampForEligibilityAndPersistence()
    {
        await ClockGate.WaitAsync();
        try
        {
            await using var db = CreateContext();
            var dueAt = DateTime.UtcNow.AddHours(1);
            var assessment = Assessment.Create(Guid.NewGuid(), "Boundary", AssessmentType.Assignment, Score(10));
            assessment.SetDeliverySchedule(null, dueAt, dueAt, false, null);
            var submission = AssessmentSubmission.Start(assessment.Id, Guid.NewGuid(), Guid.NewGuid(), 1);
            db.AddRange(assessment, submission);
            await db.SaveChangesAsync();
            SystemClock.SetProvider(new SequenceTimeProvider(dueAt, dueAt.AddTicks(1)));
            var service = new AssessmentService(db, Mock.Of<IProgramContentService>(), new RubricService(db, NullLogger<RubricService>.Instance), NullLogger<AssessmentService>.Instance);

            var result = await service.SubmitAsync(submission.Id);

            result.IsSuccess.Should().BeTrue();
            result.Value.SubmittedAt.Should().Be(dueAt);
            result.Value.Status.Should().Be(SubmissionStatus.Submitted);
        }
        finally
        {
            SystemClock.Reset();
            ClockGate.Release();
        }
    }
    [Fact]
    public async Task GetUserSubmissionsAsync_FiltersByEnrollmentAndActorUser()
    {
        await using var db = CreateContext();
        var enrollmentId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        db.Set<AssessmentSubmission>().AddRange(
            AssessmentSubmission.Start(Guid.NewGuid(), enrollmentId, actorUserId, 1),
            AssessmentSubmission.Start(Guid.NewGuid(), enrollmentId, Guid.NewGuid(), 1),
            AssessmentSubmission.Start(Guid.NewGuid(), Guid.NewGuid(), actorUserId, 1));
        await db.SaveChangesAsync();
        var service = new AssessmentService(db, Mock.Of<IProgramContentService>(), new RubricService(db, NullLogger<RubricService>.Instance), NullLogger<AssessmentService>.Instance);

        var submissions = await service.GetUserSubmissionsAsync(enrollmentId, actorUserId);

        submissions.Should().ContainSingle()
            .Which.UserId.Should().Be(actorUserId);
    }

    [Fact]
    public async Task LinkInteractiveVideoCueAsync_WhenContentDoesNotResolve_ShouldReturnNotFound()
    {
        await using var db = CreateContext();
        var assessment = Assessment.Create(Guid.NewGuid(), "Video checkpoint", AssessmentType.Quiz, Score(10));
        db.Set<Assessment>().Add(assessment);
        await db.SaveChangesAsync();
        var contents = new Mock<IProgramContentService>();
        contents.Setup(service => service.GetContentByIdAsync(It.IsAny<Guid>())).ReturnsAsync((ProgramContent?)null);
        var service = new AssessmentService(db, contents.Object, new RubricService(db, NullLogger<RubricService>.Instance), NullLogger<AssessmentService>.Instance);

        var result = await service.LinkInteractiveVideoCueAsync(
            assessment.Id,
            new LinkInteractiveVideoCueRequest(Guid.NewGuid(), "chapter-1"));

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task LinkInteractiveVideoCueAsync_WhenDeletedContentDoesNotResolve_ShouldReturnNotFound()
    {
        await using var db = CreateContext();
        var assessment = Assessment.Create(Guid.NewGuid(), "Video checkpoint", AssessmentType.Quiz, Score(10));
        db.Set<Assessment>().Add(assessment);
        await db.SaveChangesAsync();
        var contents = new Mock<IProgramContentService>();
        contents.Setup(service => service.GetContentByIdAsync(It.IsAny<Guid>())).ReturnsAsync((ProgramContent?)null);
        var service = new AssessmentService(db, contents.Object, new RubricService(db, NullLogger<RubricService>.Instance), NullLogger<AssessmentService>.Instance);

        var result = await service.LinkInteractiveVideoCueAsync(
            assessment.Id,
            new LinkInteractiveVideoCueRequest(Guid.NewGuid(), "deleted-cue"));

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task LinkInteractiveVideoCueAsync_WhenContentIsInAnotherCourse_ShouldReturnValidationError()
    {
        await using var db = CreateContext();
        var assessment = Assessment.Create(Guid.NewGuid(), "Video checkpoint", AssessmentType.Quiz, Score(10));
        db.Set<Assessment>().Add(assessment);
        await db.SaveChangesAsync();
        var content = CreateVideoLesson(Guid.NewGuid());
        var contents = new Mock<IProgramContentService>();
        contents.Setup(service => service.GetContentByIdAsync(content.Id)).ReturnsAsync(content);
        var service = new AssessmentService(db, contents.Object, new RubricService(db, NullLogger<RubricService>.Instance), NullLogger<AssessmentService>.Instance);

        var result = await service.LinkInteractiveVideoCueAsync(
            assessment.Id,
            new LinkInteractiveVideoCueRequest(content.Id, "other-course"));

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task LinkInteractiveVideoCueAsync_WhenContentIsNotVideoLesson_ShouldReturnValidationError()
    {
        await using var db = CreateContext();
        var courseId = Guid.NewGuid();
        var assessment = Assessment.Create(courseId, "Video checkpoint", AssessmentType.Quiz, Score(10));
        db.Set<Assessment>().Add(assessment);
        await db.SaveChangesAsync();
        var content = CreateVideoLesson(courseId);
        content.LessonFormat = LessonContentFormat.Markdown;
        var contents = new Mock<IProgramContentService>();
        contents.Setup(service => service.GetContentByIdAsync(content.Id)).ReturnsAsync(content);
        var service = new AssessmentService(db, contents.Object, new RubricService(db, NullLogger<RubricService>.Instance), NullLogger<AssessmentService>.Instance);

        var result = await service.LinkInteractiveVideoCueAsync(
            assessment.Id,
            new LinkInteractiveVideoCueRequest(content.Id, "not-video"));

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task LinkInteractiveVideoCueAsync_WithVideoLessonInAssessmentCourse_ShouldPersistStableCue()
    {
        await using var db = CreateContext();
        var courseId = Guid.NewGuid();
        var assessment = Assessment.Create(courseId, "Video checkpoint", AssessmentType.Quiz, Score(10));
        db.Set<Assessment>().Add(assessment);
        await db.SaveChangesAsync();
        var content = CreateVideoLesson(courseId);
        var contents = new Mock<IProgramContentService>();
        contents.Setup(service => service.GetContentByIdAsync(content.Id)).ReturnsAsync(content);
        var service = new AssessmentService(db, contents.Object, new RubricService(db, NullLogger<RubricService>.Instance), NullLogger<AssessmentService>.Instance);

        var result = await service.LinkInteractiveVideoCueAsync(
            assessment.Id,
            new LinkInteractiveVideoCueRequest(content.Id, " chapter-1 "));

        result.IsSuccess.Should().BeTrue();
        result.Value.CueId.Should().Be("chapter-1");
        (await db.Set<InteractiveVideoAssessmentCue>().SingleAsync()).CueId.Should().Be("chapter-1");
    }

    [Fact]
    public async Task GetInteractiveVideoCuesAsync_WhenLinkedContentNoLongerResolves_ShouldFilterStaleCue()
    {
        await using var db = CreateContext();
        var assessment = Assessment.Create(Guid.NewGuid(), "Video checkpoint", AssessmentType.Quiz, Score(10));
        var cue = assessment.AddInteractiveVideoCue(Guid.NewGuid(), "chapter-1");
        db.AddRange(assessment, cue);
        await db.SaveChangesAsync();
        var contents = new Mock<IProgramContentService>();
        contents.Setup(service => service.GetContentByIdAsync(cue.ContentId)).ReturnsAsync((ProgramContent?)null);
        var service = new AssessmentService(db, contents.Object, new RubricService(db, NullLogger<RubricService>.Instance), NullLogger<AssessmentService>.Instance);

        var cues = await service.GetInteractiveVideoCuesAsync(assessment.Id);

        cues.Should().BeEmpty();
    }

    [Fact]
    public async Task UnlinkInteractiveVideoCueAsync_HardDeletesAndAllowsTheStableCueToBeRelinked()
    {
        await using var db = CreateContext();
        var courseId = Guid.NewGuid();
        var assessment = Assessment.Create(courseId, "Video checkpoint", AssessmentType.Quiz, Score(10));
        db.Set<Assessment>().Add(assessment);
        await db.SaveChangesAsync();
        var content = CreateVideoLesson(courseId);
        var contents = new Mock<IProgramContentService>();
        contents.Setup(service => service.GetContentByIdAsync(content.Id)).ReturnsAsync(content);
        var service = new AssessmentService(db, contents.Object, new RubricService(db, NullLogger<RubricService>.Instance), NullLogger<AssessmentService>.Instance);

        var linked = await service.LinkInteractiveVideoCueAsync(
            assessment.Id,
            new LinkInteractiveVideoCueRequest(content.Id, "checkpoint"));
        var unlinked = await service.UnlinkInteractiveVideoCueAsync(assessment.Id, linked.Value.Id);
        var relinked = await service.LinkInteractiveVideoCueAsync(
            assessment.Id,
            new LinkInteractiveVideoCueRequest(content.Id, "checkpoint"));

        linked.IsSuccess.Should().BeTrue();
        unlinked.IsSuccess.Should().BeTrue();
        relinked.IsSuccess.Should().BeTrue();
        (await db.Set<InteractiveVideoAssessmentCue>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task DeleteAssessmentAsync_SoftDeletesActiveCueLinksAndRemovesOperationalReads()
    {
        await using var db = CreateContext();
        var courseId = Guid.NewGuid();
        var assessment = Assessment.Create(courseId, "Video checkpoint", AssessmentType.Quiz, Score(10));
        assessment.Version = 1;
        var cue = assessment.AddInteractiveVideoCue(Guid.NewGuid(), "chapter-1");
        cue.Version = 1;
        db.AddRange(assessment, cue);
        await db.SaveChangesAsync();
        var content = CreateVideoLesson(courseId);
        content.Id = cue.ContentId;
        var contents = new Mock<IProgramContentService>();
        contents.Setup(service => service.GetContentByIdAsync(content.Id)).ReturnsAsync(content);
        var service = new AssessmentService(db, contents.Object, new RubricService(db, NullLogger<RubricService>.Instance), NullLogger<AssessmentService>.Instance);

        var deleted = await service.DeleteAssessmentAsync(assessment.Id);
        var loaded = await service.GetAssessmentByIdAsync(assessment.Id);
        var cues = await service.GetInteractiveVideoCuesAsync(assessment.Id);

        deleted.IsSuccess.Should().BeTrue();
        loaded.Should().BeNull();
        cues.Should().BeEmpty();
        (await db.Set<InteractiveVideoAssessmentCue>().SingleAsync()).DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GradeSubmissionAsync_WhenScoreExceedsAssessmentMaximum_ReturnsValidationError()
    {
        await using var db = CreateContext();
        var courseId = Guid.NewGuid();
        var assessment = Assessment.Create(courseId, "Quiz", AssessmentType.Quiz, Score(100));
        var submission = AssessmentSubmission.Start(assessment.Id, Guid.NewGuid(), Guid.NewGuid(), 1);
        submission.Submit();
        var program = new Program { Id = courseId, PassingScore = Percent(60) };
        db.AddRange(program, assessment, submission);
        await db.SaveChangesAsync();
        var service = new AssessmentService(db, Mock.Of<IProgramContentService>(), new RubricService(db, NullLogger<RubricService>.Instance), NullLogger<AssessmentService>.Instance);

        var result = await service.GradeSubmissionAsync(submission.Id, new GradeSubmissionRequest(Score(101)));

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task GradeSubmissionAsync_LoadsProgramPassingScore_ComputesAbsolutePassing()
    {
        await using var db = CreateContext();
        var courseId = Guid.NewGuid();
        var assessment = Assessment.Create(courseId, "Quiz", AssessmentType.Quiz, Score(100));
        assessment.SetPassingScore(Score(60));
        var program = new Program { Id = courseId, PassingScore = Percent(60) };
        var passing = AssessmentSubmission.Start(assessment.Id, Guid.NewGuid(), Guid.NewGuid(), 1);
        passing.Submit();
        var failing = AssessmentSubmission.Start(assessment.Id, Guid.NewGuid(), Guid.NewGuid(), 2);
        failing.Submit();
        db.AddRange(program, assessment, passing, failing);
        await db.SaveChangesAsync();
        var service = new AssessmentService(db, Mock.Of<IProgramContentService>(), new RubricService(db, NullLogger<RubricService>.Instance), NullLogger<AssessmentService>.Instance);

        var passResult = await service.GradeSubmissionAsync(passing.Id, new GradeSubmissionRequest(Score(70)));
        var failResult = await service.GradeSubmissionAsync(failing.Id, new GradeSubmissionRequest(Score(50)));

        passResult.IsSuccess.Should().BeTrue();
        passResult.Value.Passed.Should().BeTrue();
        passResult.Value.Score.Should().Be(Score(70));
        failResult.IsSuccess.Should().BeTrue();
        failResult.Value.Passed.Should().BeFalse();
        failResult.Value.Score.Should().Be(Score(50));
    }

    [Fact]
    public async Task UpdateAssessmentAsync_WhenNewMaximumIsBelowAssignedScore_ReturnsValidationError()
    {
        await using var db = CreateContext();
        var assessment = Assessment.Create(Guid.NewGuid(), "Quiz", AssessmentType.Quiz, Score(100));
        var submission = AssessmentSubmission.Start(assessment.Id, Guid.NewGuid(), Guid.NewGuid(), 1);
        submission.Submit();
        submission.Grade(Score(80), Score(60), assessment.MaxScore);
        db.AddRange(assessment, submission);
        await db.SaveChangesAsync();
        var service = new AssessmentService(db, Mock.Of<IProgramContentService>(), new RubricService(db, NullLogger<RubricService>.Instance), NullLogger<AssessmentService>.Instance);

        var result = await service.UpdateAssessmentAsync(
            assessment.Id,
            new UpdateAssessmentRequest(assessment.Version, MaxScore: Score(70)));

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    private static ProgramContent CreateVideoLesson(Guid courseId) => new()
    {
        Id = Guid.NewGuid(),
        ProgramId = courseId,
        Title = "Video lesson",
        Type = ProgramContentType.Lesson,
        LessonFormat = LessonContentFormat.Video,
    };

    private static TestAssessmentDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestAssessmentDbContext>()
            .UseInMemoryDatabase($"AssessmentCue_{Guid.NewGuid()}")
            .Options;
        return new TestAssessmentDbContext(options);
    }

    private sealed class TestAssessmentDbContext(DbContextOptions<TestAssessmentDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            new AssessmentsModelConfiguration().Configure(modelBuilder);
            // ponytail: minimal Program mapping for GradeSubmissionAsync rewire (loads Program to read PassingScore).
            // Ignore navigations — full mapping lives in ApplicationDbContext; unit tests don't traverse them.
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
            throw new NotSupportedException("Transactions are not required for cue tests.");
        }
    }

    private sealed class SequenceTimeProvider(params DateTime[] timestamps) : TimeProvider
    {
        private int _index;

        public override DateTimeOffset GetUtcNow()
        {
            var timestamp = timestamps[Math.Min(_index++, timestamps.Length - 1)];
            return new DateTimeOffset(timestamp, TimeSpan.Zero);
        }
    }
}
