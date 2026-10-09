using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Authorization;
using GameGuild.Learning.Assessments;
using GameGuild.Learning.Assessments.Grading.Authoring;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Assessments.Grading.Runtime;
using GameGuild.Learning.Courses;
using GameGuild.Learning.Enrollments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Learning.Assessments.Tests;

public class ControllerAndModuleTests
{
    private readonly Mock<IAssessmentService> _svc = new();
    private readonly Mock<IActorContextAccessor> _actor = new();
    private readonly Mock<IProgramCrudService> _programs = new();
    private readonly Mock<IProgramReadService> _programReads = new();
    private readonly Mock<IProgramEnrollmentService> _programEnrollments = new();
    private readonly Mock<IEnrollmentService> _enrollments = new();
    private readonly Mock<IPermissionQueryService> _permissions = new();
    private readonly Mock<IGradingQueueService> _gradingQueue = new();
    private readonly Mock<IAssessmentAuthoringService> _authoring = new();
    private readonly Mock<IAssessmentGradingRuntimeService> _runtime = new();
    private readonly Mock<ILogger<AssessmentsController>> _log = new();
    private AssessmentEndpointTestSender? _endpointSender;

    private AssessmentsController CreateController(Guid? userId = null, bool isSystemAdmin = false, Guid? tenantId = null)
    {
        var uid = userId ?? Guid.NewGuid();
        var effectiveTenantId = tenantId ?? Guid.NewGuid();
        _actor.Setup(a => a.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = uid.ToString(),
            TenantId = effectiveTenantId,
            IsAuthenticated = true,
            Roles = isSystemAdmin ? new HashSet<string> { "SystemAdmin" } : new HashSet<string>(),
            Permissions = new HashSet<string>()
        });
        _permissions.Setup(service => service.IsUserInTenantAsync(
                uid,
                effectiveTenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _programReads.Setup(service => service.GetProgramByIdAsync(It.IsAny<Guid>()))
            .Returns<Guid>(courseId => _programs.Object.GetProgramByIdAsync(courseId));
        var courseAccess = new CourseAccessEvaluator(
            _programReads.Object,
            new CourseEnrollmentAccessReader(_programEnrollments.Object, _enrollments.Object),
            _actor.Object,
            _permissions.Object);
        _endpointSender = new AssessmentEndpointTestSender(assessmentService: _svc.Object);
        return new AssessmentsController(
            _svc.Object,
            _actor.Object,
            _programs.Object,
             _enrollments.Object,
             courseAccess,
             _gradingQueue.Object,
             _authoring.Object,
            _log.Object,
            _endpointSender,
             _runtime.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

    [Fact] public void Ctor_Creates() => CreateController().Should().NotBeNull();

    [Fact]
    public async Task GetAssessment_Found_ReturnsOk()
    {
        var id = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        _svc.Setup(s => s.GetAssessmentByIdAsync(id))
            .ReturnsAsync(Assessment.Create(courseId, "T", AssessmentType.Quiz, Score(100)));
        _programs.Setup(service => service.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, CreatorId = Guid.NewGuid() });
        var r = await CreateController(isSystemAdmin: true).GetAssessment(id);
        r.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetAssessment_NotFound_Returns404()
    {
        _svc.Setup(s => s.GetAssessmentByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Assessment?)null);
        var r = await CreateController().GetAssessment(Guid.NewGuid());
        r.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetCourseAssessments_ReturnsOk()
    {
        var courseId = Guid.NewGuid();
        _programs.Setup(s => s.GetProgramByIdAsync(courseId)).ReturnsAsync(new Program { Id = courseId });
        _svc.Setup(s => s.GetCourseAssessmentsAsync(courseId)).ReturnsAsync(new List<Assessment>());
        var r = await CreateController(isSystemAdmin: true).GetCourseAssessments(courseId);
        r.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void AssessmentService_ShouldExposeGroupManagement()
    {
        typeof(IAssessmentService).GetMethod("GetCourseAssessmentGroupsAsync").Should().NotBeNull();
        typeof(IAssessmentService).GetMethod("CreateAssessmentGroupAsync").Should().NotBeNull();
        typeof(IAssessmentService).GetMethod("AssignAssessmentToGroupAsync").Should().NotBeNull();
    }

    [Fact]
    public void AssessmentService_ShouldExposeCourseAnalytics()
    {
        typeof(IAssessmentService).GetMethod("GetCourseAssessmentAnalyticsAsync").Should().NotBeNull();
        typeof(AssessmentsController).GetMethod("GetCourseAssessmentAnalytics").Should().NotBeNull();
    }

    [Fact]
    public async Task GetCourseAssessmentAnalytics_ReturnsOk()
    {
        var courseId = Guid.NewGuid();
        var analytics = new CourseAssessmentAnalyticsDto(
            courseId,
            AssessmentCount: 1,
            GradedCount: 1,
            UngradedCount: 0,
            AveragePercent: Percent(80),
            PassRate: Percent(100),
            Distribution: Array.Empty<AssessmentScoreBucketDto>(),
            Groups: Array.Empty<AssessmentGroupAnalyticsDto>());
        _svc.Setup(s => s.GetCourseAssessmentAnalyticsAsync(courseId)).ReturnsAsync(analytics);
        _programs.Setup(service => service.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, CreatorId = Guid.NewGuid() });

        var r = await CreateController(isSystemAdmin: true).GetCourseAssessmentAnalytics(courseId);

        r.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task DeleteAssessment_Success_Returns204()
    {
        var assessmentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        _svc.Setup(s => s.GetAssessmentByIdAsync(assessmentId))
            .ReturnsAsync(Assessment.Create(courseId, "T", AssessmentType.Assignment, Score(100)));
        _programs.Setup(service => service.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, CreatorId = Guid.NewGuid() });
        _svc.Setup(s => s.DeleteAssessmentAsync(assessmentId)).ReturnsAsync(Result.Success());
        var r = await CreateController(isSystemAdmin: true).DeleteAssessment(assessmentId);
        r.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task CanAttempt_ReturnsOk()
    {
        var aId = Guid.NewGuid(); var eId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        _svc.Setup(s => s.GetAssessmentByIdAsync(aId)).ReturnsAsync(Assessment.Create(courseId, "T", AssessmentType.Quiz, Score(100)));
        _enrollments.Setup(s => s.GetAsync(eId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrollmentDto(eId, courseId, userId, null, GameGuild.Learning.Enrollments.EnrollmentStatus.Active, DateTime.UtcNow, null, null, 0, null));
        _enrollments.Setup(s => s.GetUserEnrollmentsAsync(
                userId,
                GameGuild.Learning.Enrollments.EnrollmentStatus.Active,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([new EnrollmentDto(eId, courseId, userId, null, GameGuild.Learning.Enrollments.EnrollmentStatus.Active, DateTime.UtcNow, null, null, 0, null)]);
        _programs.Setup(service => service.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, CreatorId = Guid.NewGuid() });
        _svc.Setup(s => s.CanAttemptAsync(aId, eId)).ReturnsAsync(Result.Success(true));
        _svc.Setup(s => s.GetAttemptCountAsync(aId, eId)).ReturnsAsync(2);
        var r = await CreateController(userId).CanAttempt(aId, eId);
        r.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetSubmission_Found_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var enrollmentId = Guid.NewGuid();
        var assessment = Assessment.Create(courseId, "T", AssessmentType.Quiz, Score(100));
        _svc.Setup(s => s.GetSubmissionByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(AssessmentSubmission.Start(assessment.Id, enrollmentId, userId, 1));
        _svc.Setup(s => s.GetAssessmentByIdAsync(assessment.Id)).ReturnsAsync(assessment);
        _programs.Setup(service => service.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, CreatorId = Guid.NewGuid() });
        _enrollments.Setup(service => service.GetUserEnrollmentsAsync(
                userId,
                GameGuild.Learning.Enrollments.EnrollmentStatus.Active,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([new EnrollmentDto(enrollmentId, courseId, userId, null, GameGuild.Learning.Enrollments.EnrollmentStatus.Active, DateTime.UtcNow, null, null, 0, null)]);
        var r = await CreateController(userId).GetSubmission(Guid.NewGuid());
        r.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<LearnerAssessmentSubmissionDto>();
    }

    [Fact]
    public async Task GetSubmission_WhenOwnerIsInAnotherProgramTenant_ReturnsForbidden()
    {
        var actorId = Guid.NewGuid();
        var actorTenantId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var assessment = Assessment.Create(courseId, "T", AssessmentType.Quiz, Score(100));
        var submission = AssessmentSubmission.Start(assessment.Id, Guid.NewGuid(), actorId, 1);
        _svc.Setup(service => service.GetSubmissionByIdAsync(submission.Id)).ReturnsAsync(submission);
        _svc.Setup(service => service.GetAssessmentByIdAsync(assessment.Id)).ReturnsAsync(assessment);
        _programs.Setup(service => service.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, TenantId = Guid.NewGuid(), CreatorId = actorId });

        var result = await CreateController(actorId, tenantId: actorTenantId).GetSubmission(submission.Id);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetSubmission_WhenAssessmentIsSoftDeleted_ReturnsNotFoundForOwner()
    {
        var actorId = Guid.NewGuid();
        var submission = AssessmentSubmission.Start(Guid.NewGuid(), Guid.NewGuid(), actorId, 1);
        _svc.Setup(service => service.GetSubmissionByIdAsync(submission.Id)).ReturnsAsync(submission);
        _svc.Setup(service => service.GetAssessmentByIdAsync(submission.AssessmentId))
            .ReturnsAsync((Assessment?)null);

        var result = await CreateController(actorId).GetSubmission(submission.Id);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetSubmission_NotFound_Returns404()
    {
        _svc.Setup(s => s.GetSubmissionByIdAsync(It.IsAny<Guid>())).ReturnsAsync((AssessmentSubmission?)null);
        var r = await CreateController().GetSubmission(Guid.NewGuid());
        r.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetSubmission_WhenSubmissionBelongsToAnotherLearner_ReturnsForbidden()
    {
        var actorId = Guid.NewGuid();
        var submission = AssessmentSubmission.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1);
        _svc.Setup(s => s.GetSubmissionByIdAsync(submission.Id)).ReturnsAsync(submission);
        _svc.Setup(s => s.GetAssessmentByIdAsync(submission.AssessmentId))
            .ReturnsAsync(Assessment.Create(Guid.NewGuid(), "T", AssessmentType.Assignment, Score(100)));

        var result = await CreateController(actorId).GetSubmission(submission.Id);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetSubmission_WithProgramReviewPermission_ReturnsManagerPayload()
    {
        var actorId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var assessment = Assessment.Create(courseId, "T", AssessmentType.Assignment, Score(100));
        var submission = AssessmentSubmission.Start(assessment.Id, Guid.NewGuid(), Guid.NewGuid(), 1);
        submission.SetPayload(new SubmitAssessmentRequest(TextPayload: "Persisted learner response"), SubmissionModality.Text);
        submission.Submit();
        var graderId = Guid.NewGuid();
        submission.Grade(Score(80), Score(60), assessment.MaxScore, graderId, "Reviewed");
        _svc.Setup(s => s.GetSubmissionByIdAsync(submission.Id)).ReturnsAsync(submission);
        _svc.Setup(s => s.GetAssessmentByIdAsync(assessment.Id)).ReturnsAsync(assessment);
        _programs.Setup(service => service.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, CreatorId = Guid.NewGuid() });
        _permissions.Setup(service => service.HasTenantPermissionAsync(
                actorId,
                It.IsAny<Guid?>(),
                $"{nameof(Program)}.{courseId}.{PermissionType.Review}"))
            .ReturnsAsync(true);

        var result = await CreateController(actorId).GetSubmission(submission.Id);

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<AssessmentSubmissionDto>()
            .Which.Should().BeEquivalentTo(AssessmentSubmissionDto.FromEntity(submission));
    }

    [Fact]
    public async Task GetSubmission_WithProgramReadPermission_ReturnsForbidden()
    {
        var actorId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var assessment = Assessment.Create(courseId, "T", AssessmentType.Assignment, Score(100));
        var submission = AssessmentSubmission.Start(assessment.Id, Guid.NewGuid(), Guid.NewGuid(), 1);
        _svc.Setup(s => s.GetSubmissionByIdAsync(submission.Id)).ReturnsAsync(submission);
        _svc.Setup(s => s.GetAssessmentByIdAsync(assessment.Id)).ReturnsAsync(assessment);
        _programs.Setup(service => service.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, CreatorId = Guid.NewGuid() });
        _permissions.Setup(service => service.HasTenantPermissionAsync(
                actorId,
                It.IsAny<Guid?>(),
                $"{nameof(Program)}.{courseId}.{PermissionType.Read}"))
            .ReturnsAsync(true);

        var result = await CreateController(actorId).GetSubmission(submission.Id);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Theory]
    [InlineData(PermissionType.Create)]
    [InlineData(PermissionType.Edit)]
    [InlineData(PermissionType.Delete)]
    public async Task GetSubmission_WithNonReviewPermission_ReturnsForbidden(PermissionType permission)
    {
        var actorId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var assessment = Assessment.Create(courseId, "T", AssessmentType.Assignment, Score(100));
        var submission = AssessmentSubmission.Start(assessment.Id, Guid.NewGuid(), Guid.NewGuid(), 1);
        _svc.Setup(service => service.GetSubmissionByIdAsync(submission.Id)).ReturnsAsync(submission);
        _svc.Setup(service => service.GetAssessmentByIdAsync(assessment.Id)).ReturnsAsync(assessment);
        _programs.Setup(service => service.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, CreatorId = Guid.NewGuid() });
        _permissions.Setup(service => service.HasTenantPermissionAsync(
                actorId,
                It.IsAny<Guid?>(),
                $"{nameof(Program)}.{courseId}.{permission}"))
            .ReturnsAsync(true);

        var result = await CreateController(actorId).GetSubmission(submission.Id);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetSubmission_WhenActorIsProgramCreatorWithoutReview_ReturnsManagerPayload()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var assessment = Assessment.Create(courseId, "T", AssessmentType.Assignment, Score(100));
        var submission = AssessmentSubmission.Start(assessment.Id, Guid.NewGuid(), Guid.NewGuid(), 1);
        _svc.Setup(service => service.GetSubmissionByIdAsync(submission.Id)).ReturnsAsync(submission);
        _svc.Setup(service => service.GetAssessmentByIdAsync(assessment.Id)).ReturnsAsync(assessment);
        _programs.Setup(service => service.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, TenantId = tenantId, CreatorId = actorId });

        var result = await CreateController(actorId, tenantId: tenantId).GetSubmission(submission.Id);

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<AssessmentSubmissionDto>();
    }

    [Fact]
    public async Task GetAssessmentSubmissions_WithProgramReviewPermission_ReturnsOk()
    {
        var actorId = Guid.NewGuid();
        var assessmentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        _svc.Setup(s => s.GetAssessmentByIdAsync(assessmentId)).ReturnsAsync(Assessment.Create(courseId, "T", AssessmentType.Quiz, Score(100)));
        _programs.Setup(service => service.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, CreatorId = Guid.NewGuid() });
        _permissions.Setup(service => service.HasTenantPermissionAsync(
                actorId,
                It.IsAny<Guid?>(),
                $"{nameof(Program)}.{courseId}.{PermissionType.Review}"))
            .ReturnsAsync(true);
        _svc.Setup(s => s.GetAssessmentSubmissionsAsync(assessmentId)).ReturnsAsync(new List<AssessmentSubmission>());
        var r = await CreateController(actorId).GetAssessmentSubmissions(assessmentId);
        r.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetAssessmentSubmissions_WhenLearnerIsNotManager_ReturnsForbidden()
    {
        var assessmentId = Guid.NewGuid();
        _svc.Setup(s => s.GetAssessmentByIdAsync(assessmentId))
            .ReturnsAsync(Assessment.Create(Guid.NewGuid(), "T", AssessmentType.Quiz, Score(100)));

        var result = await CreateController().GetAssessmentSubmissions(assessmentId);

        result.Result.Should().BeOfType<ForbidResult>();
        _svc.Verify(s => s.GetAssessmentSubmissionsAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Theory]
    [InlineData(PermissionType.Create)]
    [InlineData(PermissionType.Edit)]
    [InlineData(PermissionType.Delete)]
    public async Task GetAssessmentSubmissions_WithNonReviewPermission_ReturnsForbidden(PermissionType permission)
    {
        var actorId = Guid.NewGuid();
        var assessmentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        _svc.Setup(s => s.GetAssessmentByIdAsync(assessmentId))
            .ReturnsAsync(Assessment.Create(courseId, "T", AssessmentType.Quiz, Score(100)));
        _programs.Setup(service => service.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, CreatorId = Guid.NewGuid() });
        _svc.Setup(s => s.GetAssessmentSubmissionsAsync(assessmentId)).ReturnsAsync(new List<AssessmentSubmission>());
        _permissions.Setup(service => service.HasTenantPermissionAsync(
                actorId,
                It.IsAny<Guid?>(),
                $"{nameof(Program)}.{courseId}.{permission}"))
            .ReturnsAsync(true);

        var result = await CreateController(actorId).GetAssessmentSubmissions(assessmentId);

        result.Result.Should().BeOfType<ForbidResult>();
        _svc.Verify(service => service.GetAssessmentSubmissionsAsync(assessmentId), Times.Never);
    }

    [Fact]
    public async Task GetAssessmentSubmissions_WhenActorIsProgramCreatorWithoutReview_ReturnsOk()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var assessmentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        _svc.Setup(service => service.GetAssessmentByIdAsync(assessmentId))
            .ReturnsAsync(Assessment.Create(courseId, "T", AssessmentType.Quiz, Score(100)));
        _programs.Setup(service => service.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, TenantId = tenantId, CreatorId = actorId });

        var result = await CreateController(actorId, tenantId: tenantId).GetAssessmentSubmissions(assessmentId);

        result.Result.Should().BeOfType<OkObjectResult>();
        _svc.Verify(service => service.GetAssessmentSubmissionsAsync(assessmentId), Times.Once);
    }

    [Fact]
    public async Task GetMySubmissions_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        _svc.Setup(s => s.GetUserSubmissionsAsync(It.IsAny<Guid>(), userId)).ReturnsAsync(new List<AssessmentSubmission>());
        var r = await CreateController(userId).GetMySubmissions(Guid.NewGuid());
        r.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetMySubmissions_WhenSameUserIdBelongsToAnotherProgramTenant_ExcludesSubmission()
    {
        var userId = Guid.NewGuid();
        var actorTenantId = Guid.NewGuid();
        var enrollmentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var assessment = Assessment.Create(courseId, "Other tenant", AssessmentType.Quiz, Score(100));
        var submission = AssessmentSubmission.Start(assessment.Id, enrollmentId, userId, 1);
        _svc.Setup(service => service.GetUserSubmissionsAsync(enrollmentId, userId))
            .ReturnsAsync([submission]);
        _svc.Setup(service => service.GetAssessmentByIdAsync(assessment.Id)).ReturnsAsync(assessment);
        _programs.Setup(service => service.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, TenantId = Guid.NewGuid(), CreatorId = userId });

        var result = await CreateController(userId, tenantId: actorTenantId).GetMySubmissions(enrollmentId);

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<IEnumerable<LearnerAssessmentSubmissionDto>>()
            .Which.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMySubmissions_WhenAssessmentIsSoftDeleted_ExcludesSubmission()
    {
        var userId = Guid.NewGuid();
        var enrollmentId = Guid.NewGuid();
        var submission = AssessmentSubmission.Start(Guid.NewGuid(), enrollmentId, userId, 1);
        _svc.Setup(service => service.GetUserSubmissionsAsync(enrollmentId, userId))
            .ReturnsAsync([submission]);
        _svc.Setup(service => service.GetAssessmentByIdAsync(submission.AssessmentId))
            .ReturnsAsync((Assessment?)null);

        var result = await CreateController(userId).GetMySubmissions(enrollmentId);

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<IEnumerable<LearnerAssessmentSubmissionDto>>()
            .Which.Should().BeEmpty();
    }

    [Fact]
    public async Task StartSubmission_WhenEnrollmentBelongsToAnotherLearner_ReturnsForbidden()
    {
        var actorId = Guid.NewGuid();
        var assessmentId = Guid.NewGuid();
        var enrollmentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        _svc.Setup(s => s.GetAssessmentByIdAsync(assessmentId))
            .ReturnsAsync(Assessment.Create(courseId, "T", AssessmentType.Quiz, Score(100)));
        _enrollments.Setup(s => s.GetAsync(enrollmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrollmentDto(enrollmentId, courseId, Guid.NewGuid(), null, GameGuild.Learning.Enrollments.EnrollmentStatus.Active, DateTime.UtcNow, null, null, 0, null));

        var result = await CreateController(actorId).StartSubmission(assessmentId, new StartSubmissionRequest(enrollmentId));

        result.Result.Should().BeOfType<ForbidResult>();
        _svc.Verify(s => s.StartSubmissionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task CanAttempt_WhenEnrollmentBelongsToAnotherLearner_ReturnsForbidden()
    {
        var actorId = Guid.NewGuid();
        var assessmentId = Guid.NewGuid();
        var enrollmentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        _svc.Setup(s => s.GetAssessmentByIdAsync(assessmentId))
            .ReturnsAsync(Assessment.Create(courseId, "T", AssessmentType.Quiz, Score(100)));
        _enrollments.Setup(s => s.GetAsync(enrollmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrollmentDto(enrollmentId, courseId, Guid.NewGuid(), null, GameGuild.Learning.Enrollments.EnrollmentStatus.Active, DateTime.UtcNow, null, null, 0, null));

        var result = await CreateController(actorId).CanAttempt(assessmentId, enrollmentId);

        result.Result.Should().BeOfType<ForbidResult>();
        _svc.Verify(s => s.CanAttemptAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public void LegacyDirectGradeAuthority_IsNotExposed()
    {
        typeof(AssessmentsController).GetMethod("GradeSubmission").Should().BeNull();
        typeof(IAssessmentService).GetMethod("GradeSubmissionAsync").Should().BeNull();
    }

    [Fact]
    public async Task ReleaseRuntimeSubmission_WhenActorCannotReviewCourse_ReturnsForbiddenBeforeRelease()
    {
        var actorId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var assessment = Assessment.Create(courseId, "Quiz", AssessmentType.Quiz, Score(100));
        var submission = AssessmentSubmission.Start(assessment.Id, Guid.NewGuid(), Guid.NewGuid(), 1);
        _svc.Setup(service => service.GetSubmissionByIdAsync(submissionId)).ReturnsAsync(submission);
        _svc.Setup(service => service.GetAssessmentByIdAsync(assessment.Id)).ReturnsAsync(assessment);
        _programs.Setup(service => service.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, CreatorId = Guid.NewGuid() });

        var result = await CreateController(actorId).ReleaseRuntimeSubmission(
            submissionId,
            new ReleaseGradeResultCommand(Guid.NewGuid(), 1, "release-denied"),
            CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
        _endpointSender!.SentRequests.Should().NotContain(request =>
            request is ReleaseRuntimeSubmissionEndpointCommand);
    }

    [Fact]
    public void AddAssessmentsModule_Registers()
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddScoped<IApplicationDbContext>(_ => Mock.Of<IApplicationDbContext>());
        sc.AddScoped<IProgramContentService>(_ => Mock.Of<IProgramContentService>());
        sc.AddAssessmentsModule();
        sc.BuildServiceProvider().GetService<IAssessmentService>().Should().NotBeNull();
    }

    [Fact]
    public void AddAssessmentsModule_PrioritizesNumericReviewMethodsJsonContract()
    {
        var services = new ServiceCollection();
        services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddAssessmentsModule();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>()
            .Value.JsonSerializerOptions;
        var methods = ReviewMethods.AutomatedReview | ReviewMethods.InstructorReview;

        JsonSerializer.Serialize(methods, options).Should().Be("12");
        JsonSerializer.Deserialize<ReviewMethods>("12", options).Should().Be(methods);
        var deserializeString = () => JsonSerializer.Deserialize<ReviewMethods>(
            "\"AutomatedReview, InstructorReview\"",
            options);
        deserializeString.Should().Throw<JsonException>();
    }

    [Fact]
    public void CoursesAndAssessmentsModules_ResolveTheRealAssessmentLifecycleGuard()
    {
        var sc = new ServiceCollection();
        sc.AddScoped<IApplicationDbContext>(_ => Mock.Of<IApplicationDbContext>());
        sc.AddCoursesModule();
        sc.AddAssessmentsModule();

        using var provider = sc.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IProgramContentLifecycleGuard>()
            .Should().BeOfType<AssessmentProgramContentLifecycleGuard>();
    }

    [Fact]
    public void MapAssessmentsEndpoints_ReturnsSameEndpointBuilder()
    {
        var endpoints = Mock.Of<IEndpointRouteBuilder>();

        endpoints.MapAssessmentsEndpoints().Should().BeSameAs(endpoints);
    }

    [Fact]
    public void AssessmentsModelConfiguration_ConfiguresAssessmentEntities()
    {
        var modelBuilder = new ModelBuilder(new ConventionSet());

        new AssessmentsModelConfiguration().Configure(modelBuilder);
        var model = modelBuilder.FinalizeModel();

        model.FindEntityType(typeof(Assessment))!.GetTableName().Should().Be("Assessments");
        model.FindEntityType(typeof(AssessmentSubmission))!.GetTableName().Should().Be("AssessmentSubmissions");
        model.GetEntityTypes().Select(e => e.GetTableName()).Should().Contain("AssessmentGroups");
    }

    [Fact]
    public async Task CreateAssessment_Success_Returns201()
    {
        var req = new CreateAssessmentRequest(
            Guid.NewGuid(),
            "T",
            "D",
            AssessmentType.Quiz,
            Score(100),
            TimeLimitMinutes: 30,
            MaxAttempts: 1,
            IsRequired: true);
        _svc.Setup(s => s.CreateAssessmentAsync(req))
            .ReturnsAsync(Result.Success(Assessment.Create(req.CourseId, "T", AssessmentType.Quiz, Score(100))));
        _programs.Setup(service => service.GetProgramByIdAsync(req.CourseId))
            .ReturnsAsync(new Program { Id = req.CourseId, CreatorId = Guid.NewGuid() });
        var r = await CreateController(isSystemAdmin: true).CreateAssessment(req);
        r.Result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task CreateAssessment_WhenActorCannotManagePersistedProgram_ReturnsForbiddenBeforeCallingService()
    {
        var actorId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var request = new CreateAssessmentRequest(courseId, "T", null, AssessmentType.Assignment, Score(100), TimeLimitMinutes: 60);
        _programs.Setup(service => service.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, CreatorId = Guid.NewGuid() });

        var result = await CreateController(actorId).CreateAssessment(request);

        result.Result.Should().BeOfType<ForbidResult>();
        _svc.Verify(service => service.CreateAssessmentAsync(It.IsAny<CreateAssessmentRequest>()), Times.Never);
    }

    [Fact]
    public async Task CreateAssessment_WhenActorIsPersistedProgramCreator_ReturnsCreated()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var request = new CreateAssessmentRequest(courseId, "T", null, AssessmentType.Assignment, Score(100), TimeLimitMinutes: 60);
        _programs.Setup(service => service.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, TenantId = tenantId, CreatorId = actorId });
        _svc.Setup(service => service.CreateAssessmentAsync(request))
            .ReturnsAsync(Result.Success(Assessment.Create(courseId, "T", AssessmentType.Assignment, Score(100))));

        var result = await CreateController(actorId, tenantId: tenantId).CreateAssessment(request);

        result.Result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task CreateAssessment_WhenActorHasProgramPermission_ReturnsCreated()
    {
        var actorId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var request = new CreateAssessmentRequest(courseId, "T", null, AssessmentType.Assignment, Score(100), TimeLimitMinutes: 60);
        _programs.Setup(service => service.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, CreatorId = Guid.NewGuid() });
        _permissions.Setup(service => service.HasTenantPermissionAsync(
                actorId, It.IsAny<Guid?>(), $"{nameof(Program)}.{courseId}.{PermissionType.Edit}"))
            .ReturnsAsync(true);
        _svc.Setup(service => service.CreateAssessmentAsync(request))
            .ReturnsAsync(Result.Success(Assessment.Create(courseId, "T", AssessmentType.Assignment, Score(100))));

        var result = await CreateController(actorId).CreateAssessment(request);

        result.Result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task CreateAssessment_WhenCreatorIsInAnotherTenant_ReturnsForbidden()
    {
        var actorId = Guid.NewGuid();
        var actorTenantId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var request = new CreateAssessmentRequest(courseId, "T", null, AssessmentType.Assignment, Score(100), TimeLimitMinutes: 60);
        _programs.Setup(service => service.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, CreatorId = actorId, TenantId = Guid.NewGuid() });

        var result = await CreateController(actorId, tenantId: actorTenantId).CreateAssessment(request);

        result.Result.Should().BeOfType<ForbidResult>();
        _svc.Verify(service => service.CreateAssessmentAsync(It.IsAny<CreateAssessmentRequest>()), Times.Never);
    }

    [Fact]
    public async Task UnlinkInteractiveVideoCue_WhenLearnerIsNotManager_ReturnsForbidden()
    {
        var assessmentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        _svc.Setup(service => service.GetAssessmentByIdAsync(assessmentId))
            .ReturnsAsync(Assessment.Create(courseId, "Video", AssessmentType.Assignment, Score(100)));
        _programs.Setup(service => service.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, CreatorId = Guid.NewGuid() });

        var result = await CreateController().UnlinkInteractiveVideoCue(assessmentId, Guid.NewGuid());

        result.Should().BeOfType<ForbidResult>();
        _svc.Verify(service => service.UnlinkInteractiveVideoCueAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GetLearnerInteractiveVideoCues_WhenEnrollmentBelongsToAnotherLearner_ReturnsForbidden()
    {
        var actorId = Guid.NewGuid();
        var assessmentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var enrollmentId = Guid.NewGuid();
        _svc.Setup(service => service.GetAssessmentByIdAsync(assessmentId))
            .ReturnsAsync(Assessment.Create(courseId, "Video", AssessmentType.Assignment, Score(100)));
        _enrollments.Setup(service => service.GetAsync(enrollmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrollmentDto(enrollmentId, courseId, Guid.NewGuid(), null, GameGuild.Learning.Enrollments.EnrollmentStatus.Active, DateTime.UtcNow, null, null, 0, null));

        var result = await CreateController(actorId)
            .GetLearnerInteractiveVideoCues(assessmentId, Guid.NewGuid(), enrollmentId);

        result.Result.Should().BeOfType<ForbidResult>();
        _svc.Verify(service => service.GetInteractiveVideoCuesForContentAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GetCourseAssessmentAnalytics_WhenActorCannotManageProgram_ReturnsForbidden()
    {
        var courseId = Guid.NewGuid();
        _programs.Setup(service => service.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, CreatorId = Guid.NewGuid() });

        var result = await CreateController().GetCourseAssessmentAnalytics(courseId);

        result.Result.Should().BeOfType<ForbidResult>();
        _svc.Verify(service => service.GetCourseAssessmentAnalyticsAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAssessment_WhenActorCannotManageAssessmentCourse_ReturnsForbidden()
    {
        var assessmentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        _svc.Setup(service => service.GetAssessmentByIdAsync(assessmentId))
            .ReturnsAsync(Assessment.Create(courseId, "T", AssessmentType.Assignment, Score(100)));
        _programs.Setup(service => service.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, CreatorId = Guid.NewGuid() });

        var result = await CreateController().UpdateAssessment(assessmentId, new UpdateAssessmentRequest(0, Title: "updated"));

        result.Result.Should().BeOfType<ForbidResult>();
        _svc.Verify(service => service.UpdateAssessmentAsync(It.IsAny<Guid>(), It.IsAny<UpdateAssessmentRequest>()), Times.Never);
    }

    [Fact]
    public async Task CreateAssessmentGroup_WhenActorCannotManagePersistedProgram_ReturnsForbidden()
    {
        var courseId = Guid.NewGuid();
        _programs.Setup(service => service.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, CreatorId = Guid.NewGuid() });

        var result = await CreateController().CreateAssessmentGroup(new CreateAssessmentGroupRequest(courseId, "Quizzes", Percent(20)));

        result.Result.Should().BeOfType<ForbidResult>();
        _svc.Verify(service => service.CreateAssessmentGroupAsync(It.IsAny<CreateAssessmentGroupRequest>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAssessment_Success_ReturnsOk()
    {
        var id = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var req = new UpdateAssessmentRequest(0, Title: "U");
        _svc.Setup(s => s.GetAssessmentByIdAsync(id))
            .ReturnsAsync(Assessment.Create(courseId, "T", AssessmentType.Assignment, Score(100)));
        _svc.Setup(s => s.UpdateAssessmentAsync(id, req))
            .ReturnsAsync(Result.Success(Assessment.Create(courseId, "U", AssessmentType.Quiz, Score(100))));
        _programs.Setup(service => service.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, CreatorId = Guid.NewGuid() });
        var r = await CreateController(isSystemAdmin: true).UpdateAssessment(id, req);
        r.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SubmitAssessment_WithoutBody_ForCurrentLearner_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var assessmentId = Guid.NewGuid();
        var enrollmentId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();
        var submission = AssessmentSubmission.Start(assessmentId, enrollmentId, userId, 1);
        _svc.Setup(s => s.GetSubmissionByIdAsync(submissionId))
            .ReturnsAsync(submission);
        _svc.Setup(s => s.GetAssessmentByIdAsync(assessmentId))
            .ReturnsAsync(Assessment.Create(courseId, "T", AssessmentType.Quiz, Score(100)));
        _programs.Setup(service => service.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, TenantId = tenantId, CreatorId = Guid.NewGuid() });
        _enrollments.Setup(service => service.GetUserEnrollmentsAsync(
                userId,
                GameGuild.Learning.Enrollments.EnrollmentStatus.Active,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([new EnrollmentDto(
                enrollmentId,
                courseId,
                userId,
                tenantId,
                GameGuild.Learning.Enrollments.EnrollmentStatus.Active,
                DateTime.UtcNow,
                null,
                null,
                0,
                null)]);
        _svc.Setup(s => s.SubmitAsync(submissionId, null))
            .ReturnsAsync(Result.Success(submission));
        var r = await CreateController(userId, tenantId: tenantId).SubmitAssessment(submissionId);
        r.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SubmitAssessment_WhenEnrollmentIsNotActive_ReturnsForbidden()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var assessmentId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();
        _svc.Setup(service => service.GetSubmissionByIdAsync(submissionId))
            .ReturnsAsync(AssessmentSubmission.Start(assessmentId, Guid.NewGuid(), userId, 1));
        _svc.Setup(service => service.GetAssessmentByIdAsync(assessmentId))
            .ReturnsAsync(Assessment.Create(courseId, "T", AssessmentType.Quiz, Score(100)));
        _programs.Setup(service => service.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, TenantId = tenantId, CreatorId = Guid.NewGuid() });

        var result = await CreateController(userId, tenantId: tenantId).SubmitAssessment(submissionId);

        result.Result.Should().BeOfType<ForbidResult>();
        _svc.Verify(
            service => service.SubmitAsync(It.IsAny<Guid>(), It.IsAny<SubmitAssessmentRequest?>()),
            Times.Never);
    }

    [Fact]
    public async Task SubmitAssessment_WhenSubmissionBelongsToAnotherLearner_ReturnsForbidden()
    {
        var submissionId = Guid.NewGuid();
        _svc.Setup(s => s.GetSubmissionByIdAsync(submissionId))
            .ReturnsAsync(AssessmentSubmission.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1));

        var result = await CreateController().SubmitAssessment(submissionId);

        result.Result.Should().BeOfType<ForbidResult>();
        _svc.Verify(s => s.SubmitAsync(It.IsAny<Guid>(), It.IsAny<SubmitAssessmentRequest?>()), Times.Never);
    }

    [Fact]
    public async Task StartSubmission_WithProgramUserButNoActiveEnrollment_ReturnsForbidden()
    {
        var assessmentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var programUserId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _svc.Setup(service => service.GetAssessmentByIdAsync(assessmentId))
            .ReturnsAsync(Assessment.Create(courseId, "T", AssessmentType.Quiz, Score(100)));
        _programs.Setup(service => service.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, CreatorId = Guid.NewGuid() });
        _enrollments.Setup(service => service.GetAsync(programUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EnrollmentDto?)null);
        _programs.Setup(service => service.GetUserProgressDtoAsync(courseId, userId))
            .ReturnsAsync(new UserProgressDto(programUserId, courseId, userId, Percent(0), null, null, null, []));
        _svc.Setup(service => service.StartSubmissionAsync(assessmentId, programUserId, userId))
            .ReturnsAsync(Result.Success(AssessmentSubmission.Start(assessmentId, programUserId, userId, 1)));

        var result = await CreateController(userId).StartSubmission(assessmentId, new StartSubmissionRequest(programUserId));

        result.Result.Should().BeOfType<ForbidResult>();
        _svc.Verify(
            service => service.StartSubmissionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()),
            Times.Never);
    }

    [Fact]
    public async Task StartSubmission_Success_Returns201()
    {
        var aId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var enrollmentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var assessment = Assessment.Create(courseId, "T", AssessmentType.Quiz, Score(100));
        _svc.Setup(s => s.GetAssessmentByIdAsync(aId)).ReturnsAsync(assessment);
        _enrollments.Setup(s => s.GetAsync(enrollmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrollmentDto(enrollmentId, courseId, userId, null, GameGuild.Learning.Enrollments.EnrollmentStatus.Active, DateTime.UtcNow, null, null, 0, null));
        _enrollments.Setup(s => s.GetUserEnrollmentsAsync(
                userId,
                GameGuild.Learning.Enrollments.EnrollmentStatus.Active,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([new EnrollmentDto(enrollmentId, courseId, userId, null, GameGuild.Learning.Enrollments.EnrollmentStatus.Active, DateTime.UtcNow, null, null, 0, null)]);
        _programs.Setup(service => service.GetProgramByIdAsync(courseId))
            .ReturnsAsync(new Program { Id = courseId, CreatorId = Guid.NewGuid() });
        _svc.Setup(s => s.StartSubmissionAsync(aId, enrollmentId, userId))
            .ReturnsAsync(Result.Success(AssessmentSubmission.Start(aId, enrollmentId, userId, 1)));
        var r = await CreateController(userId).StartSubmission(aId, new StartSubmissionRequest(enrollmentId));
        r.Result.Should().BeOfType<CreatedAtActionResult>()
            .Which.Value.Should().BeOfType<LearnerAssessmentAttemptDto>();
    }

}
