using FluentAssertions;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Users;
using GameGuild.Learning.Courses;
using GameGuild.Learning.Enrollments;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using NotificationPriority = GameGuild.Notifications.NotificationPriority;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Learning.Assessments.Tests;

/// <summary>
/// Cross-course task aggregation (/me/tasks: grade/do/review items) and the notification
/// hooks on submit (managers), peer-review submit (all group row owners, anonymous),
/// and grade fan-out (each graded member).
/// </summary>
public class TasksAggregationTests
{
    // ===== AGGREGATION =====

    [Fact]
    public async Task Instructor_SeesGradeTasks_WithCounts_GroupAssessmentCountsGroupAttempts()
    {
        await using var db = CreateContext();
        var courseId = Guid.NewGuid();
        var instructorId = Guid.NewGuid();
        await SeedCourseAsync(db, courseId, "Physics", instructorId);

        var individual = await SeedAssessmentAsync(db, courseId, "Homework");
        await SeedRowAsync(db, individual.Id, "Alice", 1, SubmissionStatus.Submitted);
        await SeedRowAsync(db, individual.Id, "Bob", 1, SubmissionStatus.Late, isLate: true);
        await SeedRowAsync(db, individual.Id, "Carol", 1, SubmissionStatus.InProgress); // never counts
        await SeedRowAsync(db, individual.Id, "Dave", 1, SubmissionStatus.Graded, score: 90); // already graded
        var (eveId, _) = await SeedRowAsync(db, individual.Id, "Eve", 1, SubmissionStatus.Submitted);
        await SeedUserRowAsync(db, individual.Id, eveId, "Eve", 2, SubmissionStatus.Graded, score: 50); // latest graded

        var groupAssessment = await SeedAssessmentAsync(db, courseId, "Group Project");
        var group = await SeedGroupAsync(db, groupAssessment, "Alice", "Bob", "Carol");
        foreach (var (userId, _) in group.Members)
        {
            await SeedUserRowAsync(db, groupAssessment.Id, userId, "member", 1, SubmissionStatus.Submitted, groupId: group.GroupId);
        }

        var gradedOut = await SeedAssessmentAsync(db, courseId, "Old Quiz");
        await SeedRowAsync(db, gradedOut.Id, "Alice", 1, SubmissionStatus.Graded, score: 100);

        var dto = await CreateService(db, instructorId).GetTasksAsync(instructorId, TenantId, isSystemAdmin: false);

        var gradeItems = dto.Items.Where(i => i.Type == "grade").ToList();
        gradeItems.Should().HaveCount(2, "fully-graded assessments carry no grade task");
        var homework = gradeItems.Single(i => i.AssessmentTitle == "Homework");
        homework.CourseId.Should().Be(courseId);
        homework.CourseTitle.Should().Be("Physics");
        homework.CountSubmitted.Should().Be(2, "InProgress never counts, Graded is done, and Eve's stale attempt-1 submission under her graded attempt 2 does not count either");
        homework.DueAt.Should().Be(individual.DueAt);
        var project = gradeItems.Single(i => i.AssessmentTitle == "Group Project");
        project.CountSubmitted.Should().Be(1, "three member rows of one group attempt count as one target");
    }

    [Fact]
    public async Task Student_SeesDoAndReviewTasks()
    {
        await using var db = CreateContext();
        var courseId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        await SeedCourseAsync(db, courseId, "Biology", creatorId: Guid.NewGuid());
        var enrollment = Enrollment.Create(courseId, studentId);
        db.Add(enrollment);
        await db.SaveChangesAsync();

        var todo = await SeedAssessmentAsync(db, courseId, "Lab Report", dueAt: FixedDueAt);
        var inProgress = await SeedAssessmentAsync(db, courseId, "Draft Essay");
        await SeedUserRowAsync(db, inProgress.Id, studentId, "student", 1, SubmissionStatus.InProgress, enrollmentId: enrollment.Id);

        var peer = await SeedAssessmentAsync(db, courseId, "Peer Essay", dueAt: FixedDueAt,
            reviewMethods: ReviewMethods.PeerReview);
        ConfigurePeerReview(peer, 2);
        await db.SaveChangesAsync();

        var dto = await CreateService(db, studentId).GetTasksAsync(studentId, TenantId, isSystemAdmin: false);

        dto.Items.Where(i => i.Type == "grade").Should().BeEmpty("student manages nothing");
        var doItems = dto.Items.Where(i => i.Type == "do").ToList();
        doItems.Select(i => i.AssessmentTitle)
            .Should().Contain(["Lab Report", "Draft Essay", "Peer Essay"], "open assessments with no final attempt are all do tasks");
        doItems.Single(i => i.AssessmentTitle == "Lab Report").DueAt.Should().Be(FixedDueAt);
        var review = dto.Items.Single(i => i.Type == "review");
        review.AssessmentTitle.Should().Be("Peer Essay");
        review.DueAt.Should().Be(FixedDueAt, "review due date is the assessment close");
        review.ReviewsCompleted.Should().Be(0);
        review.ReviewsRequired.Should().Be(2);
    }

    [Fact]
    public async Task ReviewTask_Disappears_WhenQuotaMet()
    {
        await using var db = CreateContext();
        var courseId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        await SeedCourseAsync(db, courseId, "Chemistry", creatorId: Guid.NewGuid());
        db.Add(Enrollment.Create(courseId, studentId));
        await db.SaveChangesAsync();

        var peer = await SeedAssessmentAsync(db, courseId, "Peer Lab",
            reviewMethods: ReviewMethods.PeerReview);
        ConfigurePeerReview(peer, 1);
        var (victimId, targetRow) = await SeedRowAsync(db, peer.Id, "Victim", 1, SubmissionStatus.Submitted);
        var review = AssessmentPeerReview.Create(peer.Id, targetRow.Id, studentId);
        review.SubmitReview(Score(80), "solid work", null);
        db.Add(review);
        await db.SaveChangesAsync();

        var dto = await CreateService(db, studentId).GetTasksAsync(studentId, TenantId, isSystemAdmin: false);

        dto.Items.Should().Contain(i => i.Type == "do" && i.AssessmentTitle == "Peer Lab",
            "the peer assessment itself is still open to do");
        dto.Items.Should().NotContain(i => i.Type == "review", "quota met means no review task");
    }

    [Fact]
    public async Task Student_DoAndReviewItems_CarryRealCourseTitle_NotGuid()
    {
        await using var db = CreateContext();
        var courseId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        await SeedCourseAsync(db, courseId, "Organic Chemistry", creatorId: Guid.NewGuid());
        db.Add(Enrollment.Create(courseId, studentId));
        await db.SaveChangesAsync();

        var todo = await SeedAssessmentAsync(db, courseId, "Lab Report");
        var peer = await SeedAssessmentAsync(db, courseId, "Peer Essay",
            reviewMethods: ReviewMethods.PeerReview);
        ConfigurePeerReview(peer, 2);
        await db.SaveChangesAsync();

        var dto = await CreateService(db, studentId).GetTasksAsync(studentId, TenantId, isSystemAdmin: false);

        dto.Items.Should().HaveCount(3, "the peer assessment yields both a do and a review item");
        dto.Items.Should().OnlyContain(i => i.CourseTitle == "Organic Chemistry",
            "enrolled-but-not-managed courses resolve titles via the Program lookup, not a GUID fallback");
    }

    [Fact]
    public async Task ClosedAssessment_ExcludedFromDoTasks()
    {
        await using var db = CreateContext();
        var courseId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        await SeedCourseAsync(db, courseId, "History", creatorId: Guid.NewGuid());
        db.Add(Enrollment.Create(courseId, studentId));
        await db.SaveChangesAsync();

        var closed = await SeedAssessmentAsync(db, courseId, "Archived Exam");
        closed.SetAvailability(availableFrom: null, availableUntil: SystemClock.UtcNow.AddDays(-1));
        await db.SaveChangesAsync();

        var dto = await CreateService(db, studentId).GetTasksAsync(studentId, TenantId, isSystemAdmin: false);

        dto.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task NonEnrolled_NonManager_GetsEmptyTasks_NotError()
    {
        await using var db = CreateContext();
        var courseId = Guid.NewGuid();
        await SeedCourseAsync(db, courseId, "Private Course", creatorId: Guid.NewGuid());
        var assessment = await SeedAssessmentAsync(db, courseId, "Hidden Work");
        await SeedRowAsync(db, assessment.Id, "Alice", 1, SubmissionStatus.Submitted);

        var outsider = Guid.NewGuid();
        var actor = new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = outsider.ToString(),
            TenantId = TenantId,
            IsAuthenticated = true,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>()
        };
        var actorMock = new Mock<IActorContextAccessor>();
        actorMock.Setup(a => a.ActorContext).Returns(actor);

        var controller = new TasksController(
            CreateService(db, outsider),
            actorMock.Object,
            NullLogger<TasksController>.Instance);

        var result = await controller.GetTasks();

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<TasksDto>().Subject;
        dto.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GradeTask_RequiresManagePermission_NotJustEnrollment()
    {
        await using var db = CreateContext();
        var courseId = Guid.NewGuid();
        await SeedCourseAsync(db, courseId, "Cooking", creatorId: Guid.NewGuid());
        var assessment = await SeedAssessmentAsync(db, courseId, "Souffle");
        await SeedRowAsync(db, assessment.Id, "Alice", 1, SubmissionStatus.Submitted);
        db.Add(Enrollment.Create(courseId, Guid.NewGuid()));
        await db.SaveChangesAsync();

        var enrolledStudent = (await db.Set<Enrollment>().FirstAsync()).UserId;
        var dto = await CreateService(db, enrolledStudent).GetTasksAsync(enrolledStudent, TenantId, isSystemAdmin: false);

        dto.Items.Should().NotContain(i => i.Type == "grade", "plain enrollees never see grade tasks");
    }

    // ===== NOTIFICATIONS =====

    [Fact]
    public async Task Submit_GroupSubmit_NotifiesEachCourseManagerOnce()
    {
        await using var db = CreateContext();
        var courseId = Guid.NewGuid();
        var creatorManager = Guid.NewGuid();
        await SeedCourseAsync(db, courseId, "Physics", creatorId: creatorManager);
        var grantedManager = Guid.NewGuid();
        db.Add(new TenantPermission
        {
            UserId = grantedManager,
            Permissions = [$"Program.{courseId}.{PermissionType.Edit}"]
        });

        var assessment = await SeedAssessmentAsync(db, courseId, "Group Project");
        var group = await SeedGroupAsync(db, assessment, "Alice", "Bob", "Carol");
        var aliceRow = default(AssessmentSubmission);
        foreach (var (userId, _) in group.Members)
        {
            db.Add(Enrollment.Create(courseId, userId));
            var row = await SeedUserRowAsync(db, assessment.Id, userId, "member", 1, SubmissionStatus.InProgress, groupId: group.GroupId);
            if (aliceRow == null) aliceRow = row.Row;
        }

        await db.SaveChangesAsync();

        var notifier = new RecordingNotifier();
        var service = CreateAssessmentService(db, notifier);
        var result = await service.SubmitAsync(aliceRow!.Id, new SubmitAssessmentRequest(TextPayload: "our work"));

        result.IsSuccess.Should().BeTrue();
        notifier.Sent.Should().HaveCount(2, "one notification per manager per submit event");
        notifier.Sent.Select(s => s.Recipient).Should().BeEquivalentTo([creatorManager, grantedManager]);
        notifier.Sent.Should().OnlyContain(s =>
            s.Message.Contains("awaiting grading", StringComparison.OrdinalIgnoreCase) &&
            s.Message.Contains("1 submissions", StringComparison.Ordinal) &&
            s.Message.Contains(assessment.Title));
    }

    [Fact]
    public async Task Submit_Individual_NotifiesManagers()
    {
        await using var db = CreateContext();
        var courseId = Guid.NewGuid();
        var manager = Guid.NewGuid();
        await SeedCourseAsync(db, courseId, "Math", creatorId: manager);
        var assessment = await SeedAssessmentAsync(db, courseId, "Problem Set");
        var (studentId, row) = await SeedRowAsync(db, assessment.Id, "Alice", 1, SubmissionStatus.InProgress);

        var notifier = new RecordingNotifier();
        var service = CreateAssessmentService(db, notifier);
        var result = await service.SubmitAsync(row.Id, new SubmitAssessmentRequest(TextPayload: "answers"));

        result.IsSuccess.Should().BeTrue();
        notifier.Sent.Should().ContainSingle().Which.Recipient.Should().Be(manager);
        notifier.Sent[0].Message.Should().Contain("1 submissions");
    }

    [Fact]
    public async Task SubmitReview_NotifiesAllGroupMemberRowOwners_Anonymously()
    {
        await using var db = CreateContext();
        var courseId = Guid.NewGuid();
        await SeedCourseAsync(db, courseId, "Art", creatorId: Guid.NewGuid());
        var assessment = await SeedAssessmentAsync(db, courseId, "Group Critique",
            reviewMethods: ReviewMethods.PeerReview);
        var group = await SeedGroupAsync(db, assessment, "Alice", "Bob", "Carol");
        var rows = new List<(Guid UserId, AssessmentSubmission Row)>();
        foreach (var (userId, _) in group.Members)
        {
            rows.Add(await SeedUserRowAsync(db, assessment.Id, userId, "member", 1, SubmissionStatus.Submitted, groupId: group.GroupId));
        }

        var reviewerId = Guid.NewGuid();
        db.Add(new User { Id = reviewerId, Name = "Eve Mallory", Email = "eve@example.com" });
        var review = AssessmentPeerReview.Create(assessment.Id, rows.Min(r => r.Row.Id), reviewerId);
        db.Add(review);
        await db.SaveChangesAsync();

        var notifier = new RecordingNotifier();
        var service = new PeerReviewAssignmentService(db, NullLogger<PeerReviewAssignmentService>.Instance, notifier);
        var result = await service.SubmitReviewAsync(review, Score(85), "clear thesis, tight argument", null);

        result.IsSuccess.Should().BeTrue();
        notifier.Sent.Should().HaveCount(3, "every owner of a row sharing the reviewed (CourseGroupId, AttemptNumber) is notified");
        notifier.Sent.Select(s => s.Recipient).Should().BeEquivalentTo(group.Members.Select(m => m.UserId));
        notifier.Sent.Should().OnlyContain(s =>
            s.Message.Contains("peer feedback", StringComparison.OrdinalIgnoreCase) &&
            s.Message.Contains(assessment.Title));
        notifier.Sent.Should().OnlyContain(s =>
            !s.Title.Contains("Eve") && !s.Message.Contains("Eve"),
            "reviewer identity must never appear in student-facing notification payloads");
    }

    [Fact]
    public async Task GradeFanOut_NotifiesEachMember()
    {
        await using var db = CreateContext();
        var courseId = Guid.NewGuid();
        await SeedCourseAsync(db, courseId, "Music", creatorId: Guid.NewGuid());
        var assessment = await SeedAssessmentAsync(db, courseId, "Group Performance");
        var group = await SeedGroupAsync(db, assessment, "Alice", "Bob", "Carol");
        var gradedRow = default(AssessmentSubmission);
        foreach (var (userId, _) in group.Members)
        {
            var row = await SeedUserRowAsync(db, assessment.Id, userId, "member", 1, SubmissionStatus.Submitted, groupId: group.GroupId);
            if (gradedRow == null) gradedRow = row.Row;
        }

        var notifier = new RecordingNotifier();
        var service = CreateAssessmentService(db, notifier);
        var result = await service.GradeSubmissionAsync(gradedRow!.Id, new GradeSubmissionRequest(Score(90), GradedBy: Guid.NewGuid(), Feedback: "bravo"));

        result.IsSuccess.Should().BeTrue();
        notifier.Sent.Should().HaveCount(3, "grade fan-out notifies every graded member");
        notifier.Sent.Select(s => s.Recipient).Should().BeEquivalentTo(group.Members.Select(m => m.UserId));
        notifier.Sent.Should().OnlyContain(s =>
            s.Type == NotificationType.AssessmentGraded &&
            s.Message.Contains(assessment.Title) &&
            s.Message.Contains("90"));
    }

    [Fact]
    public async Task NotificationFailure_DoesNotBreakSubmit()
    {
        await using var db = CreateContext();
        var courseId = Guid.NewGuid();
        await SeedCourseAsync(db, courseId, "Drama", creatorId: Guid.NewGuid());
        var assessment = await SeedAssessmentAsync(db, courseId, "Monologue");
        var (_, row) = await SeedRowAsync(db, assessment.Id, "Alice", 1, SubmissionStatus.InProgress);

        var notifier = new RecordingNotifier { ThrowOnSend = true };
        var service = CreateAssessmentService(db, notifier);
        var result = await service.SubmitAsync(row.Id, new SubmitAssessmentRequest(TextPayload: "lines"));

        result.IsSuccess.Should().BeTrue("notification failures must never break the submit flow");
        (await db.Set<AssessmentSubmission>().SingleAsync(s => s.Id == row.Id)).Status
            .Should().Be(SubmissionStatus.Submitted);
    }

    // ===== WIRING =====

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTime FixedDueAt = SystemClock.UtcNow.AddDays(7);

    private static TasksService CreateService(TestTasksDbContext db, Guid actorId)
    {
        var permissions = new Mock<IPermissionQueryService>();
        permissions.Setup(p => p.GetEffectivePermissionsAsync(actorId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());
        return new TasksService(db, permissions.Object, NullLogger<TasksService>.Instance);
    }

    private static AssessmentService CreateAssessmentService(TestTasksDbContext db, RecordingNotifier notifier) =>
        new(
            db,
            null!,
            new RubricService(db, NullLogger<RubricService>.Instance),
            NullLogger<AssessmentService>.Instance,
            notifications: notifier);

    private sealed class RecordingNotifier : INotificationService
    {
        public List<(Guid Recipient, NotificationType Type, string Title, string Message)> Sent { get; } = [];
        public bool ThrowOnSend { get; set; }

        public Task<Result<Notification>> SendAsync(
            Guid? recipientId, NotificationType type, string title, string message,
            NotificationChannel channel = NotificationChannel.InApp, Guid? tenantId = null, string? actionUrl = null,
            NotificationPriority priority = NotificationPriority.Normal, Guid? referenceEntityId = null,
            string? referenceEntityType = null, string? metadata = null, string? recipientEmail = null,
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnSend) throw new InvalidOperationException("notification sink down");
            Sent.Add((recipientId!.Value, type, title, message));
            return Task.FromResult(Result.Success<Notification>(null!));
        }

        public Task<Result<Notification>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<IEnumerable<Notification>>> GetUserNotificationsAsync(Guid userId, int skip = 0, int take = 20, bool? isRead = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<int>> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<Notification>> SendFromTemplateAsync(Guid recipientId, string templateCode, Dictionary<string, string> placeholders, Guid? tenantId = null, Guid? referenceEntityId = null, string? referenceEntityType = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<IEnumerable<Notification>>> SendBulkAsync(IEnumerable<Guid> recipientIds, NotificationType type, string title, string message, NotificationChannel channel = NotificationChannel.InApp, Guid? tenantId = null, string? actionUrl = null, NotificationPriority priority = NotificationPriority.Normal, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<Notification>> ScheduleAsync(Guid recipientId, NotificationType type, string title, string message, DateTime scheduledAt, NotificationChannel channel = NotificationChannel.InApp, Guid? tenantId = null, string? actionUrl = null, NotificationPriority priority = NotificationPriority.Normal, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> MarkAsUnreadAsync(Guid notificationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> DeleteAsync(Guid notificationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<int>> DeleteReadNotificationsAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<NotificationPreference>> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<NotificationPreference>> UpdatePreferencesAsync(Guid userId, bool? emailEnabled = null, bool? pushEnabled = null, bool? inAppEnabled = null, bool? smsEnabled = null, bool? marketingEnabled = null, bool? socialEnabled = null, bool? learningEnabled = null, bool? achievementsEnabled = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result> SetQuietHoursAsync(Guid userId, TimeOnly? start, TimeOnly? end, string? timezone = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<NotificationTemplate>> GetTemplateByCodeAsync(string code, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<IEnumerable<NotificationTemplate>>> GetTemplatesAsync(string? category = null, bool? isActive = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<NotificationTemplate>> CreateTemplateAsync(string code, string name, NotificationType type, NotificationChannel channel, string titleTemplate, string messageTemplate, string? description = null, string? actionUrlTemplate = null, string? category = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<NotificationTemplate>> UpdateTemplateAsync(Guid templateId, string? titleTemplate = null, string? messageTemplate = null, string? actionUrlTemplate = null, bool? isActive = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    // ===== FIXTURE =====

    private static async Task SeedCourseAsync(TestTasksDbContext db, Guid courseId, string title, Guid creatorId)
    {
        db.Add(new Program { Id = courseId, Title = title, CreatorId = creatorId, TenantId = null });
        await db.SaveChangesAsync();
    }

    private static async Task<Assessment> SeedAssessmentAsync(
        TestTasksDbContext db,
        Guid courseId,
        string title,
        DateTime? dueAt = null,
        ReviewMethods reviewMethods = ReviewMethods.InstructorReview)
    {
        var assessment = Assessment.Create(courseId, title, AssessmentType.Assignment, Score(100), reviewMethods: reviewMethods);
        if (dueAt.HasValue)
        {
            assessment.SetDeliverySchedule(null, null, dueAt, false, null);
        }

        db.Add(assessment);
        await db.SaveChangesAsync();
        return assessment;
    }

    private static void ConfigurePeerReview(Assessment assessment, int requiredReviews)
    {
        var configuration = $$"""
            {"peer":{"aggregation":"mean","claimLeaseMinutes":30,"evidenceWindowMinutes":60,"minimumReviewsToFinalize":{{requiredReviews}},"onInsufficientEvidence":"await-instructor-resolution","reviewsPerReviewer":{{requiredReviews}},"reviewsRequiredPerSubmission":{{requiredReviews}}},"schemaVersion":1}
            """;
        assessment.SetReviewPolicy(
            assessment.ReviewMethods,
            configuration,
            null,
            ContentCompletionMode.OnReleaseAndPass,
            ResultReleaseMode.Manual,
            null);
    }

    private sealed record GroupFixture(Guid GroupId, (Guid UserId, string Name)[] Members);

    private static async Task<GroupFixture> SeedGroupAsync(
        TestTasksDbContext db, Assessment assessment, params string[] memberNames)
    {
        var set = CourseGroupSet.Create(assessment.CourseId, "Project Groups");
        var group = CourseGroup.Create(set.Id, "Team A", Math.Max(2, memberNames.Length));
        assessment.AssignToGroupSet(set.Id);
        db.AddRange(set, group);
        var members = memberNames.Select(name =>
        {
            var user = Guid.NewGuid();
            db.AddRange(
                new User { Id = user, Name = name, Email = $"{name.ToLowerInvariant()}@example.com" },
                CourseGroupMember.Create(group.Id, user));
            return (user, name);
        }).ToArray();
        await db.SaveChangesAsync();
        return new GroupFixture(group.Id, members);
    }

    private static async Task<(Guid UserId, AssessmentSubmission Row)> SeedRowAsync(
        TestTasksDbContext db,
        Guid assessmentId,
        string displayName,
        int attempt,
        SubmissionStatus status,
        Guid? groupId = null,
        int? score = null,
        bool isLate = false,
        Guid? enrollmentId = null) => await SeedUserRowAsync(db, assessmentId, Guid.NewGuid(), displayName, attempt, status, groupId, score, isLate, enrollmentId);

    private static async Task<(Guid UserId, AssessmentSubmission Row)> SeedUserRowAsync(
        TestTasksDbContext db,
        Guid assessmentId,
        Guid userId,
        string displayName,
        int attempt,
        SubmissionStatus status,
        Guid? groupId = null,
        int? score = null,
        bool isLate = false,
        Guid? enrollmentId = null)
    {
        if (await db.Set<User>().AllAsync(u => u.Id != userId))
        {
            db.Add(new User { Id = userId, Name = displayName, Email = $"{displayName.ToLowerInvariant()}@example.com" });
        }

        var row = AssessmentSubmission.Start(assessmentId, enrollmentId ?? Guid.NewGuid(), userId, attempt);
        if (status != SubmissionStatus.InProgress)
        {
            row.SetPayload(new SubmitAssessmentRequest(TextPayload: "work"), SubmissionModality.Text);
            row.Submit(isLate);
            if (status == SubmissionStatus.Graded)
            {
                row.Grade(Score(score ?? 0), Score(60), Score(100), Guid.NewGuid(), "graded");
            }
        }

        if (groupId.HasValue)
        {
            row.StampCourseGroup(groupId.Value);
        }

        db.Add(row);
        await db.SaveChangesAsync();
        return (userId, row);
    }

    private static TestTasksDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestTasksDbContext>()
            .UseInMemoryDatabase($"Tasks_{Guid.NewGuid()}")
            .Options;
        return new TestTasksDbContext(options);
    }

    private sealed class TestTasksDbContext(DbContextOptions<TestTasksDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            new AssessmentsModelConfiguration().Configure(modelBuilder);
            // ponytail: minimal cross-module mapping; full mapping lives in ApplicationDbContext.
            modelBuilder.Entity<User>(b =>
            {
                b.HasKey(u => u.Id);
                b.Ignore(u => u.Profile);
                b.Ignore(u => u.Metadata);
                b.Ignore(u => u.Preferences);
                b.Ignore(u => u.Notifications);
                b.Ignore(u => u.TenantMemberships);
            });
            modelBuilder.Entity<Program>(b =>
            {
                b.HasKey(p => p.Id);
                b.Ignore(p => p.ProgramContents);
                b.Ignore(p => p.ProgramUsers);
                b.Ignore(p => p.ProgramRatings);
                b.Ignore(p => p.ProgramWishlists);
            });
            modelBuilder.Entity<Enrollment>(b => b.HasKey(e => e.Id));
            modelBuilder.Entity<TenantPermission>(b =>
            {
                b.HasKey(p => p.Id);
                b.Ignore(p => p.Metadata);
            });
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Transactions are not required for tasks aggregation tests.");
        }
    }
}
