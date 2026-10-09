using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using GameGuild.Learning.Courses;
using GameGuild.Learning.Enrollments;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Assessments.Grading.Runtime;
using GameGuild.Learning.Grading.Contracts;
using GameGuild.Identity.Context.Actors;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Learning.Assessments;

/// <summary>
/// Service implementation for assessment management and submission processing
/// </summary>
public class AssessmentService : IAssessmentService
{
    private readonly IApplicationDbContext _context;
    private readonly IProgramContentService _programContentService;
    private readonly IRubricService _rubricService;
    private readonly ILogger<AssessmentService> _logger;
    private readonly ILtiScorePassback? _ltiScorePassback;
    private readonly INotificationService? _notifications;
    private readonly IAssessmentGradebookProjectionService? _gradebookProjection;
    private readonly IActorContextAccessor? _actorContextAccessor;

    public AssessmentService(
        IApplicationDbContext context,
        IProgramContentService programContentService,
        IRubricService rubricService,
        ILogger<AssessmentService> logger,
        ILtiScorePassback? ltiScorePassback = null,
        INotificationService? notifications = null,
        IAssessmentGradebookProjectionService? gradebookProjection = null,
        IActorContextAccessor? actorContextAccessor = null)
    {
        _context = context;
        _programContentService = programContentService;
        _rubricService = rubricService;
        _logger = logger;
        _ltiScorePassback = ltiScorePassback;
        _notifications = notifications;
        _gradebookProjection = gradebookProjection;
        _actorContextAccessor = actorContextAccessor;
    }

    // ===== ASSESSMENT MANAGEMENT =====

    public async Task<Result<Assessment>> CreateAssessmentAsync(CreateAssessmentRequest request)
    {
        try
        {
            if (request.ContentId.HasValue)
            {
                return Result.Failure<Assessment>(Error.Validation(
                    "Assessment.ContentOwnership",
                    "A content-linked assessment must be created through its content authoring workflow."));
            }

            var course = await _context.Set<GameGuild.Learning.Courses.Program>()
                .AsNoTracking()
                .FirstOrDefaultAsync(value => value.Id == request.CourseId && value.DeletedAt == null)
                .ConfigureAwait(false);
            if (course is null)
            {
                return Result.Failure<Assessment>(Error.NotFound("Program", "Course not found"));
            }

            var assessment = Assessment.Create(
                request.CourseId,
                request.Title,
                request.Type,
                request.MaxScore,
                request.IsRequired,
                request.AssessmentGroupId,
                request.ContentId,
                request.ReviewMethods,
                request.Slug);
            assessment.TenantId = course.TenantId;

            // Set optional properties using internal setters
            assessment.SetDescription(request.Description);
            assessment.SetTimeLimit(request.TimeLimitMinutes);
            assessment.SetMaxAttempts(request.MaxAttempts);
            assessment.SetPassingScore(request.PassingScore ?? ScoreValue.Zero);
            assessment.SetDeliveryContract(request.SubmissionModalities, request.PresentationMode);
            assessment.SetDeliverySchedule(
                request.AvailableFrom,
                request.AvailableUntil,
                request.DueAt,
                request.AllowLateSubmissions,
                request.LateSubmissionDeadline);

            var groupValidation = await EnsureGroupMatchesCourseAsync(request.AssessmentGroupId, request.CourseId).ConfigureAwait(false);
            if (!groupValidation.IsSuccess)
            {
                return Result.Failure<Assessment>(groupValidation.Error);
            }

            _context.Set<Assessment>().Add(assessment);
            await _context.SaveChangesAsync().ConfigureAwait(false);

            _logger.LogInformation("Assessment created: {AssessmentId} for course {CourseId}", assessment.Id, request.CourseId);

            return Result.Success(assessment);
        }
        catch (Exception ex) when (ex is ArgumentException or JsonException)
        {
            return Result.Failure<Assessment>(Error.Validation("Assessment.Invalid", ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating assessment for course {CourseId}", request.CourseId);
            return Result.Failure<Assessment>(Error.Failure("CreateAssessment", "Failed to create assessment"));
        }
    }

    public async Task<Assessment?> GetAssessmentByIdAsync(Guid id)
    {
        return await _context.Set<Assessment>()
            .Include(a => a.AssessmentGroup)
            .FirstOrDefaultAsync(a => a.Id == id && a.DeletedAt == null).ConfigureAwait(false);
    }

    public async Task<Assessment?> GetAssessmentByContentIdAsync(Guid contentId)
    {
        if (contentId == Guid.Empty) return null;

        return await _context.Set<Assessment>()
            .Include(a => a.AssessmentGroup)
            .SingleOrDefaultAsync(a => a.ContentId == contentId && a.DeletedAt == null)
            .ConfigureAwait(false);
    }

    public async Task<Assessment?> GetAssessmentByIdIncludingDeletedAsync(Guid id)
    {
        return await _context.Set<Assessment>()
            .Include(a => a.AssessmentGroup)
            .FirstOrDefaultAsync(a => a.Id == id).ConfigureAwait(false);
    }

    public async Task<IEnumerable<Assessment>> GetCourseAssessmentsAsync(Guid courseId)
    {
        return await _context.Set<Assessment>()
            .Include(a => a.AssessmentGroup)
            .Where(a => a.CourseId == courseId && a.DeletedAt == null)
            .OrderBy(a => a.AssessmentGroup == null ? int.MaxValue : a.AssessmentGroup.Order)
            .ThenBy(a => a.Order)
            .ToListAsync().ConfigureAwait(false);
    }

    public async Task<CourseAssessmentAnalyticsDto> GetCourseAssessmentAnalyticsAsync(Guid courseId)
    {
        var assessments = (await _context.Set<Assessment>()
            .Include(a => a.AssessmentGroup)
            .Where(a => a.CourseId == courseId &&
                        a.DeletedAt == null &&
                        a.AssessmentGroup != null)
            .ToListAsync().ConfigureAwait(false))
            .Where(a => a.AssessmentGroup!.WeightPercent.CompareTo(PercentValue.Zero) > 0)
            .ToList();

        var assessmentIds = assessments.Select(a => a.Id).ToArray();
        var submissions = assessmentIds.Length == 0
            ? new List<AssessmentSubmission>()
            : await _context.Set<AssessmentSubmission>()
                .Where(s => assessmentIds.Contains(s.AssessmentId) && s.DeletedAt == null)
                .ToListAsync().ConfigureAwait(false);

        var scored = BuildScoreFacts(assessments, submissions);
        var groups = assessments
            .GroupBy(a => new
            {
                GroupId = a.AssessmentGroupId,
                GroupName = a.AssessmentGroup?.Name ?? "Ungrouped",
                a.AssessmentGroup?.WeightPercent,
                GroupOrder = a.AssessmentGroup?.Order ?? int.MaxValue
            })
            .OrderBy(g => g.Key.GroupOrder)
            .ThenBy(g => g.Key.GroupName)
            .Select(g =>
            {
                var groupAssessmentIds = g.Select(a => a.Id).ToHashSet();
                var groupScored = scored.Where(f => groupAssessmentIds.Contains(f.AssessmentId)).ToList();
                return new AssessmentGroupAnalyticsDto(
                    g.Key.GroupId,
                    g.Key.GroupName,
                    g.Key.WeightPercent,
                    g.Count(),
                    groupScored.Count,
                    g.Count(a => !groupScored.Any(f => f.AssessmentId == a.Id)),
                    AveragePercent(groupScored),
                    PassRate(groupScored),
                    BuildDistribution(groupScored));
            })
            .ToArray();

        return new CourseAssessmentAnalyticsDto(
            courseId,
            assessments.Count,
            scored.Count,
            assessments.Count(a => !scored.Any(f => f.AssessmentId == a.Id)),
            AveragePercent(scored),
            PassRate(scored),
            BuildDistribution(scored),
            groups);
    }

    public async Task<Result<Assessment>> UpdateAssessmentAsync(Guid id, UpdateAssessmentRequest request)
    {
        try
        {
            var assessment = await GetAssessmentByIdAsync(id).ConfigureAwait(false);
            if (assessment == null)
            {
                return Result.Failure<Assessment>(Error.NotFound("Assessment", "Assessment not found"));
            }

            if (assessment.Version != request.ExpectedVersion)
            {
                return Result.Failure<Assessment>(Error.Conflict(
                    "Assessment.ConcurrentWrite",
                    "Assessment changed before it was saved."));
            }

            if (assessment.ContentId.HasValue &&
                (request.Title is not null || request.Description is not null || request.ClearDescription ||
                 request.MaxScore.HasValue || request.IsRequired.HasValue || request.ContentId.HasValue ||
                 request.ClearContentId || request.Slug is not null))
            {
                return Result.Failure<Assessment>(Error.Validation(
                    "Assessment.ContentOwnership",
                    "Content-owned assessment fields must be changed through their content authoring workflow."));
            }

            if (request.MaxScore.HasValue &&
                (await _context.Set<AssessmentSubmission>()
                    .Where(submission => submission.AssessmentId == id && submission.Score.HasValue)
                    .Select(submission => submission.Score)
                    .ToListAsync().ConfigureAwait(false))
                .Any(score => score!.Value.CompareTo(request.MaxScore.Value) > 0))
            {
                return Result.Failure<Assessment>(Error.Validation(
                    "Assessment.ScoreRange",
                    "Maximum score cannot be lower than an assigned submission score."));
            }

            var previousGroupId = assessment.AssessmentGroupId;
            var previousWeight = assessment.AssessmentGroup?.WeightPercent;
            assessment.Update(
                request.Title,
                request.Description,
                request.ClearDescription,
                request.MaxScore,
                request.PassingScore,
                request.TimeLimitMinutes,
                request.ClearTimeLimitMinutes,
                request.MaxAttempts,
                request.IsRequired,
                request.AvailableFrom,
                request.ClearAvailableFrom,
                request.AvailableUntil,
                request.ClearAvailableUntil,
                request.ContentId,
                request.ClearContentId,
                request.AssessmentGroupId,
                request.ClearAssessmentGroupId,
                request.SubmissionModalities,
                request.PresentationMode,
                request.DueAt,
                request.ClearDueAt,
                request.AllowLateSubmissions,
                request.LateSubmissionDeadline,
                request.ClearLateSubmissionDeadline,
                request.ReviewMethods,
                groupSetId: request.GroupSetId,
                clearGroupSetId: request.ClearGroupSetId,
                reviewConfigurationCanonicalJson: request.ReviewConfigurationCanonicalJson,
                attemptContributionMode: request.AttemptContributionMode,
                contentCompletionMode: request.ContentCompletionMode,
                resultReleaseMode: request.ResultReleaseMode,
                resultReleaseScheduledFor: request.ResultReleaseScheduledFor,
                slug: request.Slug);

            var groupValidation = await EnsureGroupMatchesCourseAsync(assessment.AssessmentGroupId, assessment.CourseId).ConfigureAwait(false);
            if (!groupValidation.IsSuccess)
            {
                return Result.Failure<Assessment>(groupValidation.Error);
            }

            var groupSetValidation = await EnsureGroupSetMatchesCourseAsync(
                request.ClearGroupSetId ? null : assessment.GroupSetId,
                assessment.CourseId).ConfigureAwait(false);
            if (!groupSetValidation.IsSuccess)
            {
                return Result.Failure<Assessment>(groupSetValidation.Error);
            }

            _context.Set<Assessment>().Update(assessment);
            if (previousGroupId != assessment.AssessmentGroupId && TryGetProjectionActor(out var projectionActor))
            {
                await _gradebookProjection!.ReprojectAssessmentPlacementAsync(
                    assessment.Id,
                    projectionActor,
                    previousGroupId,
                    previousWeight).ConfigureAwait(false);
            }
            await _context.SaveChangesAsync().ConfigureAwait(false);

            _logger.LogInformation("Assessment updated: {AssessmentId}", id);

            return Result.Success(assessment);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<Assessment>(Error.Conflict(
                "Assessment.ConcurrentWrite",
                "Assessment changed before it was saved."));
        }
        catch (Exception ex) when (ex is ArgumentException or JsonException)
        {
            return Result.Failure<Assessment>(Error.Validation("Assessment.Invalid", ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating assessment {AssessmentId}", id);
            return Result.Failure<Assessment>(Error.Failure("UpdateAssessment", "Failed to update assessment"));
        }
    }

    public async Task<Result> DeleteAssessmentAsync(Guid id)
    {
        try
        {
            async Task<Result> DeleteCoreAsync()
            {
                await using var lifecycleTransaction = await AssessmentLifecycleDatabaseLock
                    .AcquireAsync(_context, id)
                    .ConfigureAwait(false);
                var assessment = await GetAssessmentByIdAsync(id).ConfigureAwait(false);
                if (assessment == null)
                {
                    return Result.Failure(Error.NotFound("Assessment", "Assessment not found"));
                }

                if (assessment.ContentId.HasValue)
                {
                    return Result.Failure(Error.Validation(
                        "Assessment.ContentOwnership",
                        "Disable grading through the content authoring workflow instead of deleting its assessment independently."));
                }

                assessment.SoftDelete();
                var activeCues = await _context.Set<InteractiveVideoAssessmentCue>()
                    .Where(cue => cue.AssessmentId == id && cue.DeletedAt == null)
                    .ToListAsync()
                    .ConfigureAwait(false);
                await using var contentLifecycleTransaction = await ProgramContentLifecycleDatabaseLock
                    .AcquireAsync(_context, activeCues.Select(cue => cue.ContentId))
                    .ConfigureAwait(false);
                foreach (var cue in activeCues)
                {
                    cue.SoftDelete();
                }

                _context.Set<Assessment>().Update(assessment);
                _context.Set<InteractiveVideoAssessmentCue>().UpdateRange(activeCues);
                await _context.SaveChangesAsync().ConfigureAwait(false);
                await AssessmentLifecycleDatabaseLock.CommitAsync(lifecycleTransaction).ConfigureAwait(false);

                return Result.Success();
            }

            var result = _context is DbContext dbContext
                ? await dbContext.Database.CreateExecutionStrategy()
                    .ExecuteAsync(DeleteCoreAsync)
                    .ConfigureAwait(false)
                : await DeleteCoreAsync().ConfigureAwait(false);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Assessment deleted: {AssessmentId}", id);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting assessment {AssessmentId}", id);
            return Result.Failure(Error.Failure("DeleteAssessment", "Failed to delete assessment"));
        }
    }

    public async Task<Result> RestoreAssessmentAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            async Task<Result> RestoreCoreAsync()
            {
                await using var lifecycleTransaction = await AssessmentLifecycleDatabaseLock
                    .AcquireAsync(_context, id, ct)
                    .ConfigureAwait(false);
                var assessment = await GetAssessmentByIdIncludingDeletedAsync(id).ConfigureAwait(false);
                if (assessment == null)
                {
                    return Result.Failure(Error.NotFound("Assessment", "Assessment not found"));
                }

                if (assessment.ContentId.HasValue)
                {
                    return Result.Failure(Error.Validation(
                        "Assessment.ContentOwnership",
                        "Content-linked assessments are restored only by their content authoring workflow."));
                }

                // ponytail: delete cascades SoftDelete to InteractiveVideoAssessmentCues, but restore does not
                // cascade-restore them. Acceptable for now: managers re-link cues via the cue endpoint.
                assessment.Restore();

                _context.Set<Assessment>().Update(assessment);
                await _context.SaveChangesAsync(ct).ConfigureAwait(false);
                await AssessmentLifecycleDatabaseLock.CommitAsync(lifecycleTransaction, ct).ConfigureAwait(false);

                return Result.Success();
            }

            var result = _context is DbContext dbContext
                ? await dbContext.Database.CreateExecutionStrategy()
                    .ExecuteAsync(RestoreCoreAsync)
                    .ConfigureAwait(false)
                : await RestoreCoreAsync().ConfigureAwait(false);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Assessment restored: {AssessmentId}", id);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring assessment {AssessmentId}", id);
            return Result.Failure(Error.Failure("RestoreAssessment", "Failed to restore assessment"));
        }
    }

    public async Task<IEnumerable<AssessmentGroup>> GetCourseAssessmentGroupsAsync(Guid courseId)
    {
        return await _context.Set<AssessmentGroup>()
            .Where(g => g.CourseId == courseId && g.DeletedAt == null)
            .OrderBy(g => g.Order)
            .ThenBy(g => g.Name)
            .ToListAsync().ConfigureAwait(false);
    }

    public async Task<AssessmentGroup?> GetAssessmentGroupByIdAsync(Guid id)
    {
        return await _context.Set<AssessmentGroup>()
            .FirstOrDefaultAsync(group => group.Id == id && group.DeletedAt == null)
            .ConfigureAwait(false);
    }

    public async Task<Result<AssessmentGroup>> CreateAssessmentGroupAsync(CreateAssessmentGroupRequest request)
    {
        try
        {
            var course = await _context.Set<GameGuild.Learning.Courses.Program>()
                .AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == request.CourseId && value.DeletedAt == null)
                .ConfigureAwait(false);
            if (course is null)
                return Result.Failure<AssessmentGroup>(Error.NotFound("Program", "Course not found"));
            var configuredWeights = await _context.Set<AssessmentGroup>()
                .Where(value => value.CourseId == request.CourseId && value.DeletedAt == null)
                .Select(value => value.WeightPercent)
                .ToArrayAsync()
                .ConfigureAwait(false);
            var configuredWeight = configuredWeights.Sum(value => value.Units);
            if (checked(configuredWeight + request.WeightPercent.Units) > PercentValue.MaximumUnits)
            {
                return Result.Failure<AssessmentGroup>(Error.Validation(
                    "AssessmentGroup.TotalWeight",
                    "Assessment group weights cannot exceed 100 percent."));
            }

            var group = AssessmentGroup.Create(
                request.CourseId,
                request.Name,
                request.WeightPercent,
                request.Order,
                request.Description);
            group.TenantId = course.TenantId;

            _context.Set<AssessmentGroup>().Add(group);
            await _context.SaveChangesAsync().ConfigureAwait(false);

            _logger.LogInformation("Assessment group created: {AssessmentGroupId} for course {CourseId}", group.Id, request.CourseId);

            return Result.Success(group);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<AssessmentGroup>(Error.Validation("AssessmentGroup.Invalid", ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating assessment group for course {CourseId}", request.CourseId);
            return Result.Failure<AssessmentGroup>(Error.Failure("CreateAssessmentGroup", "Failed to create assessment group"));
        }
    }

    public async Task<Result<AssessmentGroup>> UpdateAssessmentGroupAsync(Guid id, UpdateAssessmentGroupRequest request)
    {
        try
        {
            var group = await _context.Set<AssessmentGroup>().FirstOrDefaultAsync(g => g.Id == id && g.DeletedAt == null).ConfigureAwait(false);
            if (group == null)
            {
                return Result.Failure<AssessmentGroup>(Error.NotFound("AssessmentGroup", "Assessment group not found"));
            }

            var previousWeight = group.WeightPercent;
            if (request.WeightPercent is { } nextWeight)
            {
                var otherWeights = await _context.Set<AssessmentGroup>()
                    .Where(value => value.CourseId == group.CourseId && value.Id != group.Id && value.DeletedAt == null)
                    .Select(value => value.WeightPercent)
                    .ToArrayAsync()
                    .ConfigureAwait(false);
                var otherWeight = otherWeights.Sum(value => value.Units);
                if (checked(otherWeight + nextWeight.Units) > PercentValue.MaximumUnits)
                {
                    return Result.Failure<AssessmentGroup>(Error.Validation(
                        "AssessmentGroup.TotalWeight",
                        "Assessment group weights cannot exceed 100 percent."));
                }
            }
            group.Update(request.Name, request.Description, request.WeightPercent, request.Order);
            _context.Set<AssessmentGroup>().Update(group);
            if (previousWeight != group.WeightPercent && TryGetProjectionActor(out var projectionActor))
            {
                await _gradebookProjection!.ReprojectGroupWeightAsync(group.Id, projectionActor, previousWeight)
                    .ConfigureAwait(false);
            }
            await _context.SaveChangesAsync().ConfigureAwait(false);

            _logger.LogInformation("Assessment group updated: {AssessmentGroupId}", id);

            return Result.Success(group);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<AssessmentGroup>(Error.Validation("AssessmentGroup.Invalid", ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating assessment group {AssessmentGroupId}", id);
            return Result.Failure<AssessmentGroup>(Error.Failure("UpdateAssessmentGroup", "Failed to update assessment group"));
        }
    }

    public async Task<Result> DeleteAssessmentGroupAsync(Guid id)
    {
        try
        {
            var group = await _context.Set<AssessmentGroup>().FirstOrDefaultAsync(g => g.Id == id && g.DeletedAt == null).ConfigureAwait(false);
            if (group == null)
            {
                return Result.Failure(Error.NotFound("AssessmentGroup", "Assessment group not found"));
            }

            var groupedAssessments = await _context.Set<Assessment>()
                .Where(a => a.AssessmentGroupId == id)
                .ToListAsync().ConfigureAwait(false);
            var previousWeight = group.WeightPercent;

            foreach (var assessment in groupedAssessments)
            {
                assessment.AssignToGroup(null);
            }

            group.SoftDelete();
            if (TryGetProjectionActor(out var projectionActor))
            {
                await _gradebookProjection!.RemoveAssessmentGroupPlacementAsync(group.Id, projectionActor, previousWeight)
                    .ConfigureAwait(false);
            }
            _context.Set<Assessment>().UpdateRange(groupedAssessments);
            _context.Set<AssessmentGroup>().Update(group);
            await _context.SaveChangesAsync().ConfigureAwait(false);

            _logger.LogInformation("Assessment group deleted: {AssessmentGroupId}", id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting assessment group {AssessmentGroupId}", id);
            return Result.Failure(Error.Failure("DeleteAssessmentGroup", "Failed to delete assessment group"));
        }
    }

    public async Task<Result<Assessment>> AssignAssessmentToGroupAsync(Guid assessmentId, AssignAssessmentGroupRequest request)
    {
        try
        {
            var assessment = await GetAssessmentByIdAsync(assessmentId).ConfigureAwait(false);
            if (assessment == null)
            {
                return Result.Failure<Assessment>(Error.NotFound("Assessment", "Assessment not found"));
            }

            if (!request.ClearAssessmentGroup && !request.AssessmentGroupId.HasValue)
            {
                return Result.Failure<Assessment>(Error.Validation("AssessmentGroup.Required", "Assessment group is required unless the assignment is being cleared."));
            }

            var nextGroupId = request.ClearAssessmentGroup ? null : request.AssessmentGroupId;
            var groupValidation = await EnsureGroupMatchesCourseAsync(nextGroupId, assessment.CourseId).ConfigureAwait(false);
            if (!groupValidation.IsSuccess)
            {
                return Result.Failure<Assessment>(groupValidation.Error);
            }

            var previousGroupId = assessment.AssessmentGroupId;
            var previousWeight = assessment.AssessmentGroup?.WeightPercent;
            assessment.AssignToGroup(nextGroupId);
            _context.Set<Assessment>().Update(assessment);
            if (previousGroupId != nextGroupId && TryGetProjectionActor(out var projectionActor))
            {
                await _gradebookProjection!.ReprojectAssessmentPlacementAsync(
                    assessment.Id,
                    projectionActor,
                    previousGroupId,
                    previousWeight).ConfigureAwait(false);
            }
            await _context.SaveChangesAsync().ConfigureAwait(false);

            _logger.LogInformation("Assessment {AssessmentId} assigned to group {AssessmentGroupId}", assessment.Id, nextGroupId);

            return Result.Success(assessment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning assessment {AssessmentId} to group", assessmentId);
            return Result.Failure<Assessment>(Error.Failure("AssignAssessmentGroup", "Failed to assign assessment group"));
        }
    }

    public async Task<Result<InteractiveVideoAssessmentCue>> LinkInteractiveVideoCueAsync(
        Guid assessmentId,
        LinkInteractiveVideoCueRequest request)
    {
        try
        {
            // Assessment locks are acquired before content locks in both cue-link and assessment-delete flows.
            await using var assessmentLifecycleTransaction = await AssessmentLifecycleDatabaseLock
                .AcquireAsync(_context, assessmentId)
                .ConfigureAwait(false);
            var assessment = await GetAssessmentByIdAsync(assessmentId).ConfigureAwait(false);
            if (assessment == null)
            {
                return Result.Failure<InteractiveVideoAssessmentCue>(Error.NotFound("Assessment", "Assessment not found"));
            }

            await using var lifecycleTransaction = await ProgramContentLifecycleDatabaseLock
                .AcquireAsync(_context, [request.ContentId])
                .ConfigureAwait(false);
            var content = await _programContentService.GetContentByIdAsync(request.ContentId).ConfigureAwait(false);
            if (content == null)
            {
                return Result.Failure<InteractiveVideoAssessmentCue>(Error.NotFound("ProgramContent", "Interactive-video lesson content not found"));
            }

            if (content.ProgramId != assessment.CourseId)
            {
                return Result.Failure<InteractiveVideoAssessmentCue>(
                    Error.Validation("AssessmentCue.CourseMismatch", "Interactive-video content must belong to the assessment course."));
            }

            if (content.Type != ProgramContentType.Lesson || content.LessonFormat != LessonContentFormat.Video)
            {
                return Result.Failure<InteractiveVideoAssessmentCue>(
                    Error.Validation("AssessmentCue.NotVideoLesson", "Interactive-video content must be a video lesson."));
            }

            var cueId = request.CueId ?? string.Empty;
            var normalizedCueId = cueId.Trim();
            var duplicate = await _context.Set<InteractiveVideoAssessmentCue>()
                .AnyAsync(cue =>
                    cue.AssessmentId == assessmentId &&
                    cue.ContentId == request.ContentId &&
                    cue.CueId == normalizedCueId &&
                    cue.DeletedAt == null)
                .ConfigureAwait(false);
            if (duplicate)
            {
                return Result.Failure<InteractiveVideoAssessmentCue>(
                    Error.Validation("AssessmentCue.Duplicate", "The interactive-video cue is already linked to this assessment."));
            }

            var cue = assessment.AddInteractiveVideoCue(request.ContentId, cueId, request.CuePositionSeconds);
            _context.Set<InteractiveVideoAssessmentCue>().Add(cue);
            await _context.SaveChangesAsync().ConfigureAwait(false);
            await AssessmentLifecycleDatabaseLock.CommitAsync(assessmentLifecycleTransaction).ConfigureAwait(false);

            return Result.Success(cue);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<InteractiveVideoAssessmentCue>(Error.Validation("AssessmentCue.Invalid", ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking interactive-video cue to assessment {AssessmentId}", assessmentId);
            return Result.Failure<InteractiveVideoAssessmentCue>(Error.Failure("LinkAssessmentCue", "Failed to link interactive-video assessment cue"));
        }
    }

    public async Task<IEnumerable<InteractiveVideoAssessmentCue>> GetInteractiveVideoCuesAsync(Guid assessmentId)
    {
        var assessment = await GetAssessmentByIdAsync(assessmentId).ConfigureAwait(false);
        if (assessment == null) return Array.Empty<InteractiveVideoAssessmentCue>();

        var cues = await _context.Set<InteractiveVideoAssessmentCue>()
            .Where(cue => cue.AssessmentId == assessmentId && cue.DeletedAt == null)
            .OrderBy(cue => cue.CuePositionSeconds)
            .ThenBy(cue => cue.CueId)
            .ToListAsync().ConfigureAwait(false);

        var activeCues = new List<InteractiveVideoAssessmentCue>();
        foreach (var cue in cues)
        {
            var content = await _programContentService.GetContentByIdAsync(cue.ContentId).ConfigureAwait(false);
            if (content?.ProgramId == assessment.CourseId &&
                content.Type == ProgramContentType.Lesson &&
                content.LessonFormat == LessonContentFormat.Video)
            {
                activeCues.Add(cue);
            }
        }

        return activeCues;
    }

    public async Task<IEnumerable<InteractiveVideoAssessmentCue>> GetInteractiveVideoCuesForContentAsync(
        Guid assessmentId,
        Guid contentId)
    {
        var cues = await GetInteractiveVideoCuesAsync(assessmentId).ConfigureAwait(false);
        return cues.Where(cue => cue.ContentId == contentId).ToList();
    }

    public async Task<Result> UnlinkInteractiveVideoCueAsync(Guid assessmentId, Guid cueId)
    {
        var assessment = await GetAssessmentByIdAsync(assessmentId).ConfigureAwait(false);
        if (assessment == null)
        {
            return Result.Failure(Error.NotFound("Assessment", "Assessment not found"));
        }

        var cue = await _context.Set<InteractiveVideoAssessmentCue>()
            .FirstOrDefaultAsync(candidate => candidate.Id == cueId &&
                                              candidate.AssessmentId == assessmentId &&
                                              candidate.DeletedAt == null)
            .ConfigureAwait(false);
        if (cue == null)
        {
            return Result.Failure(Error.NotFound("AssessmentCue", "Interactive-video assessment cue not found"));
        }

        _context.Set<InteractiveVideoAssessmentCue>().Remove(cue);
        await _context.SaveChangesAsync().ConfigureAwait(false);
        return Result.Success();
    }

    private async Task<Result> EnsureGroupMatchesCourseAsync(Guid? groupId, Guid courseId)
    {
        if (!groupId.HasValue)
        {
            return Result.Success();
        }

        var group = await _context.Set<AssessmentGroup>()
            .FirstOrDefaultAsync(g => g.Id == groupId.Value && g.DeletedAt == null)
            .ConfigureAwait(false);

        if (group == null)
        {
            return Result.Failure(Error.NotFound("AssessmentGroup", "Assessment group not found"));
        }

        return group.CourseId == courseId
            ? Result.Success()
            : Result.Failure(Error.Validation("AssessmentGroup.CourseMismatch", "Assessment group belongs to another course"));
    }

    private async Task<Result> EnsureGroupSetMatchesCourseAsync(Guid? groupSetId, Guid courseId)
    {
        if (!groupSetId.HasValue)
        {
            return Result.Success();
        }

        var groupSet = await _context.Set<CourseGroupSet>()
            .FirstOrDefaultAsync(set => set.Id == groupSetId.Value && set.DeletedAt == null)
            .ConfigureAwait(false);

        if (groupSet == null)
        {
            return Result.Failure(Error.NotFound("CourseGroupSet", "Group set not found"));
        }

        return groupSet.CourseId == courseId
            ? Result.Success()
            : Result.Failure(Error.Validation("Assessment.GroupSetCourseMismatch", "Group set belongs to another course"));
    }

    private static List<AssessmentScoreFact> BuildScoreFacts(
        IReadOnlyCollection<Assessment> assessments,
        IReadOnlyCollection<AssessmentSubmission> submissions)
    {
        var assessmentsById = assessments.ToDictionary(a => a.Id);
        return submissions
            .Where(s => s.Score.HasValue && assessmentsById.ContainsKey(s.AssessmentId))
            .Select(s =>
            {
                var assessment = assessmentsById[s.AssessmentId];
                var percent = PercentValue.FromScores(s.Score!.Value, assessment.MaxScore);

                return new AssessmentScoreFact(
                    assessment.Id,
                    percent,
                    s.Passed ?? percent.CompareTo(DefaultPassingPercent) >= 0);
            })
            .ToList();
    }

    // Course-level passing policy is authoritative when no submission decision was captured.
    private static readonly PercentValue DefaultPassingPercent = PercentValue.FromPercentage("60");

    private static PercentValue AveragePercent(IReadOnlyCollection<AssessmentScoreFact> facts)
    {
        return PercentValue.Average(facts.Select(f => f.Percent));
    }

    private static PercentValue PassRate(IReadOnlyCollection<AssessmentScoreFact> facts)
    {
        return facts.Count == 0
            ? PercentValue.Zero
            : PercentValue.FromRatio(facts.Count(f => f.Passed), facts.Count);
    }

    private static IReadOnlyCollection<AssessmentScoreBucketDto> BuildDistribution(IReadOnlyCollection<AssessmentScoreFact> facts)
    {
        return new[]
        {
            BuildBucket("0-59", 0, 59, facts),
            BuildBucket("60-69", 60, 69, facts),
            BuildBucket("70-79", 70, 79, facts),
            BuildBucket("80-89", 80, 89, facts),
            BuildBucket("90-100", 90, 100, facts)
        };
    }

    private static AssessmentScoreBucketDto BuildBucket(
        string label,
        int minPercent,
        int maxPercent,
        IReadOnlyCollection<AssessmentScoreFact> facts)
    {
        var minimum = PercentValue.FromPercentage(minPercent.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var maximum = PercentValue.FromPercentage(maxPercent.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var count = facts.Count(f => f.Percent.CompareTo(minimum) >= 0 && f.Percent.CompareTo(maximum) <= 0);
        return new AssessmentScoreBucketDto(label, minPercent, maxPercent, count);
    }

    private sealed record AssessmentScoreFact(Guid AssessmentId, PercentValue Percent, bool Passed);

    public Task<Guid?> GetProgramEnrollmentUserIdAsync(Guid courseId, Guid enrollmentId, CancellationToken cancellationToken) =>
        ProgramEnrollmentAssessmentMembership.ActiveForCourse(_context, courseId)
            .Where(value => value.Id == enrollmentId)
            .Select(value => (Guid?)value.UserId)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<Guid?> GetProgramEnrollmentIdAsync(Guid courseId, Guid userId, CancellationToken cancellationToken) =>
        ProgramEnrollmentAssessmentMembership.ActiveForCourse(_context, courseId)
            .Where(value => value.UserId == userId)
            .Select(value => (Guid?)value.Id)
            .SingleOrDefaultAsync(cancellationToken);

    // ===== SUBMISSION MANAGEMENT =====

    public async Task<Result<AssessmentSubmission>> StartSubmissionAsync(Guid assessmentId, Guid enrollmentId, Guid userId)
    {
        try
        {
            if (_context is DbContext dbContext &&
                dbContext.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL" &&
                dbContext.Database.CurrentTransaction is null)
            {
                var executionStrategy = dbContext.Database.CreateExecutionStrategy();
                return await executionStrategy.ExecuteAsync(
                        () => StartSubmissionCoreAsync(assessmentId, enrollmentId, userId))
                    .ConfigureAwait(false);
            }

            return await StartSubmissionCoreAsync(assessmentId, enrollmentId, userId).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrent submission start detected for assessment {AssessmentId}", assessmentId);
            return Result.Failure<AssessmentSubmission>(Error.Conflict("AssessmentSubmission.AttemptConflict", "A concurrent submission attempt was detected. Please retry."));
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _logger.LogWarning(ex, "Duplicate submission attempt detected for assessment {AssessmentId}", assessmentId);
            return Result.Failure<AssessmentSubmission>(Error.Conflict("AssessmentSubmission.AttemptConflict", "A concurrent submission attempt was detected. Please retry."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting submission for assessment {AssessmentId}", assessmentId);
            return Result.Failure<AssessmentSubmission>(Error.Failure("StartSubmission", "Failed to start submission"));
        }
    }

    private async Task<Result<AssessmentSubmission>> StartSubmissionCoreAsync(
        Guid assessmentId,
        Guid enrollmentId,
        Guid userId)
    {
        await using var attemptTransaction = await AssessmentSubmissionDatabaseLock
            .AcquireAsync(_context, assessmentId, enrollmentId)
            .ConfigureAwait(false);
        var assessment = await GetAssessmentByIdAsync(assessmentId).ConfigureAwait(false);
        if (assessment == null)
        {
            return Result.Failure<AssessmentSubmission>(Error.NotFound("Assessment", "Assessment not found"));
        }

        if (!assessment.IsAvailable())
        {
            return Result.Failure<AssessmentSubmission>(Error.Validation("Assessment", "Assessment is not currently available"));
        }

        if (UsesGradingRuntime(assessment))
        {
            return Result.Failure<AssessmentSubmission>(Error.Conflict(
                "Assessment.OfficialRuntimeUnavailable",
                "Content-backed graded assessments cannot start through the generic submission endpoint."));
        }

        if (assessment.GroupSetId.HasValue)
        {
            return Result.Failure<AssessmentSubmission>(Error.Conflict(
                "Assessment.CollectiveRuntimeRequired",
                "Collective assessments must start through the grading runtime."));
        }

        Guid? courseGroupId = null;
        if (assessment.GroupSetId.HasValue)
        {
            courseGroupId = await ResolveActorCourseGroupIdAsync(assessment.GroupSetId.Value, userId).ConfigureAwait(false);
            if (courseGroupId is null)
            {
                return Result.Failure<AssessmentSubmission>(Error.Validation(
                    "Submission.GroupRequired",
                    "Join a group before attempting this assessment"));
            }
        }

        var attemptCount = await GetAttemptCountAsync(assessmentId, enrollmentId).ConfigureAwait(false);
        if (attemptCount >= assessment.MaxAttempts)
        {
            return Result.Failure<AssessmentSubmission>(Error.Validation("Assessment.MaxAttemptsReached", "Maximum attempts reached"));
        }

        var highestAttemptNumber = courseGroupId.HasValue
            ? await GetHighestGroupAttemptNumberAsync(assessmentId, courseGroupId.Value).ConfigureAwait(false)
            : await GetHighestAttemptNumberAsync(assessmentId, enrollmentId).ConfigureAwait(false);
        var submission = AssessmentSubmission.Start(assessmentId, enrollmentId, userId, highestAttemptNumber + 1);
        if (courseGroupId.HasValue)
        {
            submission.StampCourseGroup(courseGroupId.Value);
        }

        _context.Set<AssessmentSubmission>().Add(submission);
        await _context.SaveChangesAsync().ConfigureAwait(false);
        await AssessmentSubmissionDatabaseLock.CommitAsync(attemptTransaction).ConfigureAwait(false);

        _logger.LogInformation("Submission started: {SubmissionId} for assessment {AssessmentId}", submission.Id, assessmentId);
        return Result.Success(submission);
    }
    public async Task<Result<AssessmentSubmission>> SubmitAsync(Guid submissionId, SubmitAssessmentRequest? request = null)
    {
        try
        {
            var submission = await GetSubmissionByIdAsync(submissionId).ConfigureAwait(false);
            if (submission == null)
            {
                return Result.Failure<AssessmentSubmission>(Error.NotFound("Submission", "Submission not found"));
            }

            if (submission.Status != SubmissionStatus.InProgress)
            {
                return Result.Failure<AssessmentSubmission>(Error.Validation("Submission", "Submission is not in progress"));
            }

            var assessment = await GetAssessmentByIdAsync(submission.AssessmentId).ConfigureAwait(false);
            if (assessment == null)
            {
                return Result.Failure<AssessmentSubmission>(Error.NotFound("Assessment", "Assessment not found"));
            }

            if (UsesGradingRuntime(assessment))
            {
                return Result.Failure<AssessmentSubmission>(Error.Conflict(
                    "Assessment.OfficialRuntimeUnavailable",
                    "Content-backed graded assessments cannot submit through the generic submission endpoint."));
            }

            var submittedAt = SystemClock.UtcNow;
            if (!assessment.TryGetSubmissionTiming(submittedAt, out var isLate))
            {
                return Result.Failure<AssessmentSubmission>(Error.Validation("Submission.Unavailable", "Assessment is not accepting submissions at this time"));
            }

            if (request != null)
            {
                submission.SetPayload(request, assessment.SubmissionModalities);
            }

            submission.Submit(isLate, submittedAt);

            _context.Set<AssessmentSubmission>().Update(submission);
            await _context.SaveChangesAsync().ConfigureAwait(false);

            _logger.LogInformation("Submission submitted: {SubmissionId}", submissionId);

            if (_notifications is not null)
            {
                try
                {
                    await NotifyManagersOfPendingSubmissionsAsync(assessment).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Grading notification failed for assessment {AssessmentId}", assessment.Id);
                }
            }

            return Result.Success(submission);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<AssessmentSubmission>(Error.Validation("Submission.Invalid", ex.Message));
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // ponytail: first-writer-wins via UX_AssessmentSubmissions_Assessment_Enrollment_Attempt — two members
            // submitting the same group attempt concurrently is out of scope v1; the index makes the loser retry.
            _logger.LogWarning(ex, "Concurrent group submit detected for submission {SubmissionId}", submissionId);
            return Result.Failure<AssessmentSubmission>(Error.Conflict(
                "Submission.GroupConcurrentSubmit",
                "Groupmate is submitting concurrently, try again"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting {SubmissionId}", submissionId);
            return Result.Failure<AssessmentSubmission>(Error.Failure("Submit", "Failed to submit"));
        }
    }

    /// <summary>
    ///     Notifies every course manager once per submit event with the assessment's current
    ///     pending-grade target count (same dedup as /me/tasks grade items).
    /// </summary>
    private async Task NotifyManagersOfPendingSubmissionsAsync(Assessment assessment)
    {
        var managers = await CourseManagers.GetManagerUserIdsAsync(_context, assessment.CourseId).ConfigureAwait(false);
        if (managers.Count == 0)
        {
            return;
        }

        var rows = await _context.Set<AssessmentSubmission>()
            .Where(s => s.AssessmentId == assessment.Id && s.DeletedAt == null)
            .ToListAsync().ConfigureAwait(false);
        var pending = TasksService.CountPendingGradeTargets(rows);

        // Plain text interpolation of the instructor-authored title — no markup, in-app channel only.
        foreach (var manager in managers)
        {
            await _notifications!.SendAsync(
                    manager,
                    NotificationType.System,
                    "Submissions awaiting grading",
                    $"{pending} submissions awaiting grading on {assessment.Title}",
                    NotificationChannel.InApp,
                    actionUrl: "/dashboard/tasks")
                .ConfigureAwait(false);
        }
    }

    public async Task<AssessmentSubmission?> GetSubmissionByIdAsync(Guid id)
    {
        return await _context.Set<AssessmentSubmission>()
            .FirstOrDefaultAsync(s => s.Id == id).ConfigureAwait(false);
    }

    public async Task<IEnumerable<AssessmentSubmission>> GetAssessmentSubmissionsAsync(Guid assessmentId)
    {
        return await _context.Set<AssessmentSubmission>()
            .Where(s => s.AssessmentId == assessmentId)
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync().ConfigureAwait(false);
    }

    public async Task<IEnumerable<AssessmentSubmission>> GetUserSubmissionsAsync(Guid enrollmentId)
    {
        return await _context.Set<AssessmentSubmission>()
            .Where(s => s.EnrollmentId == enrollmentId)
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync().ConfigureAwait(false);
    }

    public async Task<IEnumerable<AssessmentSubmission>> GetUserSubmissionsAsync(Guid enrollmentId, Guid userId)
    {
        return await _context.Set<AssessmentSubmission>()
            .Where(submission =>
                (submission.EnrollmentId == enrollmentId && submission.UserId == userId) ||
                _context.Set<Grading.Persistence.AssessmentSubmissionParticipant>().Any(participant =>
                    participant.SubmissionId == submission.Id &&
                    participant.EnrollmentId == enrollmentId &&
                    participant.UserId == userId))
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync().ConfigureAwait(false);
    }

    public async Task<int> GetAttemptCountAsync(Guid assessmentId, Guid enrollmentId)
    {
        return await _context.Set<AssessmentSubmission>()
            .CountAsync(s => s.AssessmentId == assessmentId && s.EnrollmentId == enrollmentId).ConfigureAwait(false);
    }

    private async Task<int> GetHighestAttemptNumberAsync(Guid assessmentId, Guid enrollmentId)
    {
        return await _context.Set<AssessmentSubmission>()
            .Where(s => s.AssessmentId == assessmentId && s.EnrollmentId == enrollmentId)
            .Select(s => (int?)s.AttemptNumber)
            .MaxAsync()
            .ConfigureAwait(false) ?? 0;
    }

    // Generic submissions do not implement collective grading. Collective attempts are owned by
    // the grading runtime and therefore always have one submission and one execution.
    private async Task<int> GetHighestGroupAttemptNumberAsync(Guid assessmentId, Guid courseGroupId)
    {
        return await _context.Set<AssessmentSubmission>()
            .Where(s => s.AssessmentId == assessmentId &&
                        s.CourseGroupId == courseGroupId &&
                        s.Status != SubmissionStatus.InProgress)
            .Select(s => (int?)s.AttemptNumber)
            .MaxAsync()
            .ConfigureAwait(false) ?? 0;
    }

    private async Task<Guid?> ResolveActorCourseGroupIdAsync(Guid groupSetId, Guid userId)
    {
        var setGroupIds = await _context.Set<CourseGroup>()
            .Where(g => g.GroupSetId == groupSetId && g.DeletedAt == null)
            .Select(g => g.Id)
            .ToListAsync().ConfigureAwait(false);

        return await _context.Set<CourseGroupMember>()
            .Where(m => m.UserId == userId && m.DeletedAt == null && setGroupIds.Contains(m.GroupId))
            .Select(m => (Guid?)m.GroupId)
            .FirstOrDefaultAsync().ConfigureAwait(false);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (string.Equals(current.GetType().GetProperty("SqlState")?.GetValue(current) as string, "23505", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public async Task<Result<bool>> CanAttemptAsync(Guid assessmentId, Guid enrollmentId)
    {
        try
        {
            var assessment = await GetAssessmentByIdAsync(assessmentId).ConfigureAwait(false);
            if (assessment == null)
            {
                return Result.Failure<bool>(Error.NotFound("Assessment", "Assessment not found"));
            }

            if (!assessment.IsAvailable())
            {
                return Result.Success(false);
            }

            if (UsesGradingRuntime(assessment)) return Result.Success(false);
            var attemptCount = await GetAttemptCountAsync(assessmentId, enrollmentId).ConfigureAwait(false);
            return Result.Success(attemptCount < assessment.MaxAttempts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking attempt eligibility for assessment {AssessmentId}", assessmentId);
            return Result.Failure<bool>(Error.Failure("CanAttempt", "Failed to check attempt eligibility"));
        }
    }

    private static bool UsesGradingRuntime(Assessment assessment) =>
        assessment.ContentId.HasValue && assessment.ReviewMethods != ReviewMethods.None;

    private bool TryGetProjectionActor(out Guid actorId)
    {
        actorId = _actorContextAccessor?.ActorContext.SubjectIdAsGuid ?? Guid.Empty;
        return _gradebookProjection is not null && actorId != Guid.Empty;
    }
}
