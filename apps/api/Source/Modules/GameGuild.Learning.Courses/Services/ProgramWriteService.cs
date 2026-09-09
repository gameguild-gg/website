using GameGuild.Commerce.Products;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Learning.Grading.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace GameGuild.Learning.Courses;

/// <summary>
/// Write-side service for Programs: create, update, delete, clone,
/// content management, user management, progress mutations, monetization, and product integration.
/// </summary>
public class ProgramWriteService(
  IApplicationDbContext context,
  IProgramContentLifecycleGuard? lifecycleReferenceGuard = null,
  IRequestContextAccessor? requestContextAccessor = null,
  IPermissionQueryService? permissionQueryService = null,
  IEnumerable<IProgramContentAcademicMutationGuard>? academicGuards = null) : IProgramWriteService
{
  private readonly IProgramContentLifecycleGuard lifecycleGuard = lifecycleReferenceGuard ?? new NullProgramContentLifecycleGuard();
  private readonly IEnumerable<IProgramContentAcademicMutationGuard> academicMutationGuards = academicGuards ?? [];
  // ── Program CRUD ────────────────────────────────────────────────────

  public async Task<Program> CreateProgramAsync(Program program)
  {
    program.Status = ContentStatus.Draft;
    program.Visibility = ContentVisibility.Private;

    context.Set<Program>().Add(program);
    await context.SaveChangesAsync().ConfigureAwait(false);

    return program;
  }

  public async Task<Program> UpdateProgramAsync(Program program)
  {
    program.Touch();
    context.Set<Program>().Update(program);
    await context.SaveChangesAsync().ConfigureAwait(false);

    return program;
  }

  public async Task DeleteProgramAsync(Guid id)
  {
    var program = await context.Set<Program>().FindAsync(id).ConfigureAwait(false);

    if (program != null)
    {
      program.SoftDelete();
      await context.SaveChangesAsync().ConfigureAwait(false);
    }
  }

  public async Task<Program> CloneProgramAsync(Guid id, string newTitle)
  {
    var originalProgram = await context.Set<Program>()
      .Include(p => p.ProgramContents.Where(pc => pc.DeletedAt == null))
      .Include(p => p.ProgramUsers.Where(pu => pu.DeletedAt == null))
      .Where(p => p.DeletedAt == null)
      .FirstOrDefaultAsync(p => p.Id == id);

    if (originalProgram == null) throw new ArgumentException("Program not found", nameof(id));

    var clonedProgram = new Program
    {
      CreatorId = originalProgram.CreatorId,
      Title = newTitle,
      Description = originalProgram.Description,
      Slug = GenerateSlug(newTitle),
      Thumbnail = originalProgram.Thumbnail,
      Status = ContentStatus.Draft,
      Visibility = ContentVisibility.Private,
    };

    context.Set<Program>().Add(clonedProgram);
    await context.SaveChangesAsync().ConfigureAwait(false);

    // Clone content
    foreach (var content in originalProgram.ProgramContents.OrderBy(pc => pc.SortOrder))
    {
      var clonedContent = new ProgramContent
      {
        ProgramId = clonedProgram.Id,
        Title = content.Title,
        Description = content.Description,
        Type = content.Type,
        Body = content.Body,
        LessonFormat = content.LessonFormat,
        SortOrder = content.SortOrder,
        IsRequired = content.IsRequired,
        EstimatedMinutes = content.EstimatedMinutes,
        Visibility = content.Visibility,
      };

      clonedContent.NormalizeLearningContract();
      if (content.GetActivitySettings() is { } activitySettings)
        clonedContent.SetActivitySettings(activitySettings);

      context.Set<ProgramContent>().Add(clonedContent);
    }

    await context.SaveChangesAsync().ConfigureAwait(false);

    return clonedProgram;
  }

  // ── CRUD with DTOs ──────────────────────────────────────────────────

  public async Task<Program> CreateProgramAsync(CreateProgramDto createDto)
  {
    var program = new Program
    {
      Id = Guid.NewGuid(),
      CreatorId = createDto.CreatorId,
      Title = createDto.Title,
      Description = createDto.Description,
      Slug = createDto.Slug,
      Thumbnail = createDto.Thumbnail,
      PassingScore = createDto.PassingScore,
      Status = ContentStatus.Draft,
      Visibility = ContentVisibility.Private,
    };

    context.Set<Program>().Add(program);
    await context.SaveChangesAsync().ConfigureAwait(false);

    return program;
  }

  public async Task<Program?> UpdateProgramAsync(Guid id, UpdateProgramDto updateDto)
  {
    var program = await context.Set<Program>().Where(p => p.DeletedAt == null).FirstOrDefaultAsync(p => p.Id == id).ConfigureAwait(false);

    if (program == null) return null;

    if (updateDto.Title != null) program.Title = updateDto.Title;
    if (updateDto.Description != null) program.Description = updateDto.Description;
    if (updateDto.Metadata != null) program.Metadata = updateDto.Metadata;
    if (updateDto.Slug != null) program.Slug = updateDto.Slug;
    if (updateDto.Thumbnail != null) program.Thumbnail = updateDto.Thumbnail;
    if (updateDto.VideoShowcaseUrl != null) program.VideoShowcaseUrl = updateDto.VideoShowcaseUrl;
    if (updateDto.EstimatedHours.HasValue) program.EstimatedHours = updateDto.EstimatedHours.Value;
    if (updateDto.Visibility.HasValue) program.Visibility = updateDto.Visibility.Value;
    if (updateDto.Category.HasValue) program.Category = updateDto.Category.Value;
    if (updateDto.Difficulty.HasValue) program.Difficulty = updateDto.Difficulty.Value;
    if (updateDto.SkillsRequired != null) program.SkillsRequired = updateDto.SkillsRequired;
    if (updateDto.SkillsProvided != null) program.SkillsProvided = updateDto.SkillsProvided;
    if (updateDto.CreatorId.HasValue) program.CreatorId = updateDto.CreatorId.Value;
    if (updateDto.EnrollmentStatus.HasValue) program.EnrollmentStatus = updateDto.EnrollmentStatus.Value;
    if (updateDto.ClearMaxEnrollments) program.MaxEnrollments = null;
    else if (updateDto.MaxEnrollments.HasValue) program.MaxEnrollments = updateDto.MaxEnrollments.Value;
    if (updateDto.ClearEnrollmentDeadline) program.EnrollmentDeadline = null;
    else if (updateDto.EnrollmentDeadline.HasValue) program.EnrollmentDeadline = updateDto.EnrollmentDeadline.Value;
    if (updateDto.PassingScore.HasValue) program.PassingScore = updateDto.PassingScore.Value;

    program.Touch();
    await context.SaveChangesAsync().ConfigureAwait(false);

    return program;
  }

  // ── Content Management ──────────────────────────────────────────────

  public async Task<ProgramContent> AddContentAsync(Guid programId, ProgramContent content)
  {
    var program = await context.Set<Program>().Where(p => p.DeletedAt == null).AnyAsync(p => p.Id == programId).ConfigureAwait(false);

    if (!program) throw new ArgumentException("Program not found", nameof(programId));

    content.ProgramId = programId;
    content.NormalizeLearningContract();
    ProgramContentAcademicMutationGuard.EnsureAllowed(academicMutationGuards, content, ProgramContentAcademicMutation.Authoring);

    if (content.SortOrder == 0)
    {
      var maxOrder = await context.Set<ProgramContent>().Where(pc => pc.DeletedAt == null && pc.ProgramId == programId).MaxAsync(pc => (int?)pc.SortOrder) ?? 0;
      content.SortOrder = maxOrder + 1;
    }

    context.Set<ProgramContent>().Add(content);
    await context.SaveChangesAsync().ConfigureAwait(false);

    return content;
  }

  public async Task<ProgramContent> UpdateContentAsync(ProgramContent content)
  {
    await using var lifecycleTransaction = await ProgramContentLifecycleDatabaseLock
      .AcquireAsync(context, [content.Id])
      .ConfigureAwait(false);
    var existingContent = await context.Set<ProgramContent>()
      .FirstOrDefaultAsync(candidate => candidate.Id == content.Id && candidate.DeletedAt == null)
      .ConfigureAwait(false);
    if (existingContent == null) throw new InvalidOperationException($"ProgramContent with ID {content.Id} not found or has been deleted");

    content.NormalizeLearningContract();
    ProgramContentAcademicMutationGuard.EnsureAllowed(academicMutationGuards, content, ProgramContentAcademicMutation.Authoring);
    if (await lifecycleGuard.HasBlockingIncompatibleUpdateReference(
          content.Id,
          content.Type,
          content.LessonFormat).ConfigureAwait(false))
    {
      throw new GameGuild.CQRS.RequestValidationException(
        "Content linked to an assessment cue must remain a video lesson. Remove the assessment cue first.");
    }
    content.Touch();
    context.Set<ProgramContent>().Update(content);
    await context.SaveChangesAsync().ConfigureAwait(false);
    await ProgramContentLifecycleDatabaseLock.CommitAsync(lifecycleTransaction).ConfigureAwait(false);

    return content;
  }

  public async Task DeleteContentAsync(Guid contentId)
  {
    await using var lifecycleTransaction = await ProgramContentLifecycleDatabaseLock
      .AcquireAsync(context, [contentId])
      .ConfigureAwait(false);
    var content = await context.Set<ProgramContent>().FindAsync(contentId).ConfigureAwait(false);

    if (content != null)
    {
      ProgramContentAcademicMutationGuard.EnsureAllowed(academicMutationGuards, content, ProgramContentAcademicMutation.Delete);
      if (await lifecycleGuard.HasBlockingDeleteReference(content.Id).ConfigureAwait(false))
      {
        throw new GameGuild.CQRS.RequestValidationException(
          "Content linked to an assessment cue cannot be deleted. Remove the assessment cue first.");
      }
      content.SoftDelete();
      await context.SaveChangesAsync().ConfigureAwait(false);
      await ProgramContentLifecycleDatabaseLock.CommitAsync(lifecycleTransaction).ConfigureAwait(false);
    }
  }

  public async Task<Program> ReorderContentAsync(Guid programId, List<Guid> contentIds)
  {
    var program = await context.Set<Program>().Where(p => p.DeletedAt == null).FirstOrDefaultAsync(p => p.Id == programId).ConfigureAwait(false);

    if (program == null) throw new ArgumentException("Program not found", nameof(programId));

    var contents = await context.Set<ProgramContent>().Where(pc => pc.DeletedAt == null && pc.ProgramId == programId && contentIds.Contains(pc.Id)).ToListAsync();

    for (var i = 0; i < contentIds.Count; i++)
    {
      var content = contents.FirstOrDefault(c => c.Id == contentIds[i]);

      if (content != null)
      {
        content.SortOrder = i + 1;
        content.Touch();
      }
    }

    await context.SaveChangesAsync().ConfigureAwait(false);

    return program;
  }

  public async Task<ProgramContent?> AddContentAsync(Guid programId, CreateContentDto contentDto)
  {
    var program = await context.Set<Program>().Where(p => p.DeletedAt == null).FirstOrDefaultAsync(p => p.Id == programId).ConfigureAwait(false);

    if (program == null) return null;

    var content = new ProgramContent
    {
      Id = Guid.NewGuid(),
      ProgramId = programId,
      Title = contentDto.Title,
      Description = contentDto.Description,
      Type = contentDto.Type,
      Body = contentDto.Body,
      SortOrder = contentDto.SortOrder ?? 0,
      IsRequired = contentDto.IsRequired,
      EstimatedMinutes = contentDto.EstimatedMinutes,
    };

    content.NormalizeLearningContract();
    ProgramContentAcademicMutationGuard.EnsureAllowed(academicMutationGuards, content, ProgramContentAcademicMutation.Authoring);

    context.Set<ProgramContent>().Add(content);
    await context.SaveChangesAsync().ConfigureAwait(false);

    return content;
  }

  public async Task<ProgramContent?> UpdateContentAsync(Guid programId, Guid contentId, UpdateContentDto contentDto)
  {
    await using var lifecycleTransaction = await ProgramContentLifecycleDatabaseLock
      .AcquireAsync(context, [contentId])
      .ConfigureAwait(false);
    var content = await context.Set<ProgramContent>().FirstOrDefaultAsync(c => c.Id == contentId && c.ProgramId == programId && c.DeletedAt == null);

    if (content == null) return null;

    if (contentDto.Title != null) content.Title = contentDto.Title;
    if (contentDto.Description != null) content.Description = contentDto.Description;
    if (contentDto.Body != null)
    {
      content.Body = contentDto.Body;
      if (ProgramContentMappingExtensions.NormalizeProfessorFacingType(content.Type) == ProgramContentType.Lesson &&
          !content.LessonFormat.HasValue)
      {
        content.LessonFormat = LessonContentFormatInference.FromBody(contentDto.Body);
      }
    }
    if (contentDto.SortOrder != null) content.SortOrder = contentDto.SortOrder.Value;
    if (contentDto.IsRequired != null) content.IsRequired = contentDto.IsRequired.Value;
    if (contentDto.EstimatedMinutes != null) content.EstimatedMinutes = contentDto.EstimatedMinutes;

    content.NormalizeLearningContract();
    ProgramContentAcademicMutationGuard.EnsureAllowed(academicMutationGuards, content, ProgramContentAcademicMutation.Authoring);
    if (await lifecycleGuard.HasBlockingIncompatibleUpdateReference(
          content.Id,
          content.Type,
          content.LessonFormat).ConfigureAwait(false))
    {
      throw new GameGuild.CQRS.RequestValidationException(
        "Content linked to an assessment cue must remain a video lesson. Remove the assessment cue first.");
    }
    content.Touch();
    await context.SaveChangesAsync().ConfigureAwait(false);
    await ProgramContentLifecycleDatabaseLock.CommitAsync(lifecycleTransaction).ConfigureAwait(false);

    return content;
  }

  public async Task<bool> RemoveContentAsync(Guid programId, Guid contentId)
  {
    await using var lifecycleTransaction = await ProgramContentLifecycleDatabaseLock
      .AcquireAsync(context, [contentId])
      .ConfigureAwait(false);
    var content = await context.Set<ProgramContent>().FirstOrDefaultAsync(c => c.Id == contentId && c.ProgramId == programId && c.DeletedAt == null);

    if (content == null) return false;

    if (await lifecycleGuard.HasBlockingDeleteReference(content.Id).ConfigureAwait(false))
    {
      throw new GameGuild.CQRS.RequestValidationException(
        "Content linked to an assessment cue cannot be deleted. Remove the assessment cue first.");
    }

    content.SoftDelete();
    await context.SaveChangesAsync().ConfigureAwait(false);
    await ProgramContentLifecycleDatabaseLock.CommitAsync(lifecycleTransaction).ConfigureAwait(false);

    return true;
  }

  // ── User Management ─────────────────────────────────────────────────

  public async Task<ProgramUser> AddUserAsync(Guid programId, Guid userId)
  {
    var existingUser = await context.Set<ProgramUser>().Where(pu => pu.DeletedAt == null && pu.ProgramId == programId && pu.UserId == userId).FirstOrDefaultAsync();

    if (existingUser != null)
    {
      if (!existingUser.IsActive)
      {
        existingUser.IsActive = true;
        existingUser.JoinedAt = SystemClock.UtcNow;
        existingUser.Touch();
        await context.SaveChangesAsync().ConfigureAwait(false);
      }

      return existingUser;
    }

    var programUser = new ProgramUser { ProgramId = programId, UserId = userId, IsActive = true, JoinedAt = SystemClock.UtcNow };

    context.Set<ProgramUser>().Add(programUser);
    await context.SaveChangesAsync().ConfigureAwait(false);

    return programUser;
  }

  public async Task<ProgramUser> RemoveUserAsync(Guid programId, Guid userId)
  {
    var programUser = await context.Set<ProgramUser>().Where(pu => pu.DeletedAt == null && pu.ProgramId == programId && pu.UserId == userId).FirstOrDefaultAsync();

    if (programUser != null)
    {
      programUser.IsActive = false;
      programUser.Touch();
      await context.SaveChangesAsync().ConfigureAwait(false);
    }

    return programUser!;
  }

  public async Task<UserProgressDto?> AddUserToProgramAsync(Guid programId, Guid userId)
  {
    var program = await context.Set<Program>().Where(p => p.DeletedAt == null).FirstOrDefaultAsync(p => p.Id == programId).ConfigureAwait(false);

    if (program == null) return null;

    var now = SystemClock.UtcNow;
    var programUser = await context.Set<ProgramUser>()
      .FirstOrDefaultAsync(pu => pu.ProgramId == programId && pu.UserId == userId);
    if (programUser == null)
    {
      programUser = new ProgramUser
      {
        Id = Guid.NewGuid(),
        ProgramId = programId,
        UserId = userId,
        JoinedAt = now,
        LastAccessedAt = now,
        CompletionPercentage = PercentValue.Zero,
        IsActive = true,
        TenantId = program.TenantId,
      };
      context.Set<ProgramUser>().Add(programUser);
    }
    else
    {
      if (programUser.DeletedAt.HasValue) programUser.Restore();
      programUser.IsActive = true;
      programUser.TenantId ??= program.TenantId;
    }

    var canonicalEnrollment = await context.Set<ProgramEnrollment>()
      .FirstOrDefaultAsync(enrollment => enrollment.ProgramId == programId && enrollment.UserId == userId);
    if (canonicalEnrollment == null)
    {
      canonicalEnrollment = new ProgramEnrollment
      {
        Id = Guid.NewGuid(),
        ProgramId = programId,
        UserId = userId,
        EnrollmentSource = EnrollmentSource.Manual,
        EnrollmentStatus = EnrollmentStatus.Active,
        EnrolledAt = programUser.JoinedAt == default ? now : programUser.JoinedAt,
        StartDate = programUser.JoinedAt == default ? now : programUser.JoinedAt,
        ProgressPercentage = programUser.CompletionPercentage,
        FinalGrade = programUser.FinalGrade,
        TenantId = program.TenantId,
      };
      context.Set<ProgramEnrollment>().Add(canonicalEnrollment);
    }
    else
    {
      if (canonicalEnrollment.DeletedAt.HasValue) canonicalEnrollment.Restore();
      canonicalEnrollment.EnrollmentStatus = EnrollmentStatus.Active;
      canonicalEnrollment.TenantId ??= program.TenantId;
      canonicalEnrollment.Touch();
    }

    await context.SaveChangesAsync().ConfigureAwait(false);
    return await GetUserProgressDtoInternalAsync(programId, userId).ConfigureAwait(false);
  }

  public async Task<bool> RemoveUserFromProgramAsync(Guid programId, Guid userId)
  {
    var programUser = await context.Set<ProgramUser>().FirstOrDefaultAsync(pu => pu.ProgramId == programId && pu.UserId == userId && pu.DeletedAt == null);

    if (programUser == null) return false;

    programUser.IsActive = false;
    programUser.SoftDelete();
    var canonicalEnrollment = await context.Set<ProgramEnrollment>()
      .FirstOrDefaultAsync(enrollment => enrollment.ProgramId == programId && enrollment.UserId == userId);
    if (canonicalEnrollment != null)
    {
      canonicalEnrollment.EnrollmentStatus = EnrollmentStatus.Cancelled;
      canonicalEnrollment.Touch();
    }
    await context.SaveChangesAsync().ConfigureAwait(false);

    return true;
  }

  // ── Progress Mutations ──────────────────────────────────────────────

  public async Task<Program> UpdateUserProgressAsync(Guid programId, Guid userId, Guid contentId, ProgressStatus status)
  {
    var program = await context.Set<Program>().Where(p => p.DeletedAt == null).FirstOrDefaultAsync(p => p.Id == programId).ConfigureAwait(false);

    if (program == null) throw new ArgumentException("Program not found", nameof(programId));

    var programUser = await context.Set<ProgramUser>().Where(pu => pu.DeletedAt == null && pu.ProgramId == programId && pu.UserId == userId).FirstOrDefaultAsync();

    if (programUser == null) throw new ArgumentException("User not enrolled in program");

    var interaction = await context.Set<ContentInteraction>()
      .Where(ci => ci.DeletedAt == null && ci.ProgramUserId == programUser.Id && ci.ContentId == contentId)
      .OrderBy(ci => ci.SubmittedAt.HasValue)
      .ThenByDescending(ci => ci.CreatedAt)
      .FirstOrDefaultAsync();

    var isNewInteraction = interaction == null;
    if (isNewInteraction)
    {
      interaction = new ContentInteraction { ProgramUserId = programUser.Id, UserId = userId, ContentId = contentId, FirstAccessedAt = SystemClock.UtcNow, LastAccessedAt = SystemClock.UtcNow, };
    }

    ApplyProgressStatus(interaction!, status);

    if (isNewInteraction)
      interaction = await SaveNewActiveAttemptAsync(
          interaction!,
          winner => ApplyProgressStatus(winner, status))
        .ConfigureAwait(false);
    else
      await context.SaveChangesAsync().ConfigureAwait(false);
    await RecalculateUserProgressAsync(programUser.Id).ConfigureAwait(false);
    await context.SaveChangesAsync().ConfigureAwait(false);

    return program;
  }

  public async Task<UserProgressDto?> UpdateUserProgressAsync(Guid programId, Guid userId, UpdateProgressDto progressDto)
  {
    var programUser = await context.Set<ProgramUser>().FirstOrDefaultAsync(pu => pu.ProgramId == programId && pu.UserId == userId && pu.DeletedAt == null);

    if (programUser == null) return null;

    if (progressDto.LastAccessedAt != null) programUser.LastAccessedAt = progressDto.LastAccessedAt.Value;
    programUser.Touch();

    await context.SaveChangesAsync().ConfigureAwait(false);

    return await GetUserProgressDtoInternalAsync(programId, userId).ConfigureAwait(false);
  }

  public async Task<ContentInteraction?> SubmitUserContentAsync(Guid programId, Guid userId, Guid contentId, string submissionData)
  {
    var actorId = requestContextAccessor?.CurrentUserId;
    if (requestContextAccessor?.IsAuthenticated != true || !actorId.HasValue)
      throw new RequestValidationException("An authenticated actor is required to submit course content.");
    if (actorId.Value != userId && !await HasProgramEditAccessAsync(programId, actorId.Value).ConfigureAwait(false))
      throw new RequestValidationException("Program management permission is required to submit content for another learner.");

    var programUser = await context.Set<ProgramUser>()
      .FirstOrDefaultAsync(pu => pu.ProgramId == programId && pu.UserId == userId && pu.DeletedAt == null && pu.IsActive)
      .ConfigureAwait(false);

    if (programUser == null) return null;

    var initialContentType = await context.Set<ProgramContent>()
      .AsNoTracking()
      .Where(pc => pc.Id == contentId && pc.ProgramId == programId && pc.DeletedAt == null)
      .Select(pc => (ProgramContentType?)pc.Type)
      .FirstOrDefaultAsync()
      .ConfigureAwait(false);

    if (!initialContentType.HasValue) return null;

    var currentContentType = await context.Set<ProgramContent>()
      .AsNoTracking()
      .Where(pc => pc.Id == contentId && pc.ProgramId == programId && pc.DeletedAt == null)
      .Select(pc => (ProgramContentType?)pc.Type)
      .FirstOrDefaultAsync()
      .ConfigureAwait(false);
    if (!currentContentType.HasValue) return null;

    await using var submissionPolicyTransaction = LearningActivityContract.RequiresSubmissionPolicyLock(currentContentType.Value)
      ? await ProgramContentLifecycleDatabaseLock.AcquireAsync(context, [contentId]).ConfigureAwait(false)
      : null;
    var content = await context.Set<ProgramContent>()
      .AsNoTracking()
      .FirstOrDefaultAsync(pc => pc.Id == contentId && pc.ProgramId == programId && pc.DeletedAt == null)
      .ConfigureAwait(false);
    if (content == null) return null;
    ProgramContentAcademicMutationGuard.EnsureAllowed(academicMutationGuards, content, ProgramContentAcademicMutation.Submit);

    var response = LearningActivityContract.IsActivityType(content.Type)
      ? ActivityResponseContract.Parse(content.Type, submissionData, content.GetActivitySettings())
      : null;
    if (response is not null)
      await LearningActivityContract.ValidateDiscussionThreadRootAsync(context, programId, contentId, response).ConfigureAwait(false);

    var now = SystemClock.UtcNow;
    var interaction = await context.Set<ContentInteraction>()
      .Where(ci => ci.ProgramUserId == programUser.Id && ci.ContentId == contentId && ci.DeletedAt == null)
      .OrderBy(ci => ci.SubmittedAt.HasValue)
      .ThenByDescending(ci => ci.CreatedAt)
      .FirstOrDefaultAsync()
      .ConfigureAwait(false);

    if (content.Type == ProgramContentType.Survey && !LearningActivityContract.AllowsMultipleResponses(content))
    {
      var existingResponse = await context.Set<ContentInteraction>()
        .Where(ci => ci.ProgramUserId == programUser.Id && ci.ContentId == contentId && ci.SubmittedAt != null && ci.DeletedAt == null)
        .OrderByDescending(ci => ci.SubmittedAt)
        .FirstOrDefaultAsync()
        .ConfigureAwait(false);
      if (existingResponse is not null) {
        await ProgramContentLifecycleDatabaseLock.CommitAsync(submissionPolicyTransaction).ConfigureAwait(false);
        return existingResponse;
      }
    }
    else if (content.Type != ProgramContentType.Survey && interaction?.SubmittedAt != null)
    {
      await ProgramContentLifecycleDatabaseLock.CommitAsync(submissionPolicyTransaction).ConfigureAwait(false);
      return interaction;
    }

    var isNewInteraction = interaction == null || interaction.SubmittedAt != null;
    if (isNewInteraction)
    {
      interaction = new ContentInteraction
      {
        Id = content.Type == ProgramContentType.Survey && LearningActivityContract.AllowsMultipleResponses(content)
          ? Guid.NewGuid()
          : interaction is null ? CreateDirectSubmissionAttemptId(programUser.Id, contentId) : Guid.NewGuid(),
        ProgramUserId = programUser.Id,
        UserId = userId,
        ContentId = contentId,
        Status = ProgressStatus.InProgress,
        FirstAccessedAt = now,
        LastAccessedAt = now,
        StartedAt = now,
        CompletionPercentage = PercentValue.Zero,
      };

    }

    SubmitInteraction(interaction!, userId, submissionData, now);

    programUser.LastAccessedAt = now;
    programUser.Touch();

    if (isNewInteraction)
    {
      var saveResult = await SaveDirectSubmissionAsync(interaction!, submissionData, now)
        .ConfigureAwait(false);
      interaction = saveResult.Interaction;
      if (saveResult.IsConcurrentReplay) {
        await ProgramContentLifecycleDatabaseLock.CommitAsync(submissionPolicyTransaction).ConfigureAwait(false);
        return interaction;
      }
    }
    else
      await context.SaveChangesAsync().ConfigureAwait(false);
    await RecalculateUserProgressAsync(programUser.Id).ConfigureAwait(false);
    await context.SaveChangesAsync().ConfigureAwait(false);
    await ProgramContentLifecycleDatabaseLock.CommitAsync(submissionPolicyTransaction).ConfigureAwait(false);

    return interaction;
  }

  private Task<bool> HasProgramEditAccessAsync(Guid programId, Guid actorId)
  {
    if (!requestContextAccessor!.CurrentTenantId.HasValue || permissionQueryService is null) return Task.FromResult(false);
    return permissionQueryService.HasTenantPermissionAsync(
      actorId,
      requestContextAccessor.CurrentTenantId,
      $"{nameof(Program)}.{programId}.{PermissionType.Edit}");
  }

  public async Task<bool> MarkContentCompletedAsync(Guid programId, Guid userId, Guid contentId)
  {
    var programUser = await context.Set<ProgramUser>().FirstOrDefaultAsync(pu => pu.ProgramId == programId && pu.UserId == userId && pu.DeletedAt == null);

    if (programUser == null) return false;

    var content = await context.Set<ProgramContent>()
      .AsNoTracking()
      .FirstOrDefaultAsync(pc => pc.Id == contentId && pc.ProgramId == programId && pc.DeletedAt == null)
      .ConfigureAwait(false);

    if (content is null) return false;
    ProgramContentAcademicMutationGuard.EnsureAllowed(academicMutationGuards, content, ProgramContentAcademicMutation.Complete);

    var now = SystemClock.UtcNow;
    var interaction = await context.Set<ContentInteraction>()
      .Where(ci => ci.ProgramUserId == programUser.Id && ci.ContentId == contentId && ci.DeletedAt == null)
      .OrderBy(ci => ci.SubmittedAt.HasValue)
      .ThenByDescending(ci => ci.CreatedAt)
      .FirstOrDefaultAsync()
      .ConfigureAwait(false);

    var isNewInteraction = interaction == null;
    if (isNewInteraction)
    {
      interaction = new ContentInteraction
      {
        ProgramUserId = programUser.Id,
        UserId = userId,
        ContentId = contentId,
        Status = ProgressStatus.Completed,
        FirstAccessedAt = now,
        LastAccessedAt = now,
        StartedAt = now,
        CompletedAt = now,
        CompletionPercentage = PercentValue.Hundred,
        IsCompleted = true,
        AttemptCount = 1,
      };
    }
    else
    {
      CompleteInteraction(interaction!, userId, now);
    }

    programUser.LastAccessedAt = now;
    programUser.Touch();

    if (isNewInteraction)
      interaction = await SaveNewActiveAttemptAsync(
          interaction!,
          winner => CompleteInteraction(winner, userId, now))
        .ConfigureAwait(false);
    else
      await context.SaveChangesAsync().ConfigureAwait(false);
    await RecalculateUserProgressAsync(programUser.Id).ConfigureAwait(false);
    await context.SaveChangesAsync().ConfigureAwait(false);

    return true;
  }

  public async Task<bool> ResetUserProgressAsync(Guid programId, Guid userId)
  {
    var programUser = await context.Set<ProgramUser>().FirstOrDefaultAsync(pu => pu.ProgramId == programId && pu.UserId == userId && pu.DeletedAt == null);

    if (programUser == null) return false;

    programUser.CompletionPercentage = PercentValue.Zero;
    programUser.CompletedAt = null;
    programUser.LastAccessedAt = SystemClock.UtcNow;
    programUser.Touch();

    await context.SaveChangesAsync().ConfigureAwait(false);

    return true;
  }

  // ── Monetization ────────────────────────────────────────────────────

  public async Task<Program?> EnableMonetizationAsync(Guid id, MonetizationDto monetizationDto)
  {
    var program = await context.Set<Program>().Where(p => p.DeletedAt == null).FirstOrDefaultAsync(p => p.Id == id).ConfigureAwait(false);

    if (program == null) return null;

    ProgramPricingMetadata.Enable(program, monetizationDto);
    program.Touch();
    await context.SaveChangesAsync().ConfigureAwait(false);

    return program;
  }

  public async Task<Program?> DisableMonetizationAsync(Guid id)
  {
    var program = await context.Set<Program>().Where(p => p.DeletedAt == null).FirstOrDefaultAsync(p => p.Id == id).ConfigureAwait(false);

    if (program == null) return null;

    ProgramPricingMetadata.Disable(program);
    program.Touch();
    await context.SaveChangesAsync().ConfigureAwait(false);

    return program;
  }

  public async Task<PricingDto?> UpdateProgramPricingAsync(Guid id, UpdatePricingDto pricingDto)
  {
    var program = await context.Set<Program>().Where(p => p.DeletedAt == null).FirstOrDefaultAsync(p => p.Id == id).ConfigureAwait(false);

    if (program == null) return null;

    var pricing = ProgramPricingMetadata.Update(program, pricingDto);
    program.Touch();
    await context.SaveChangesAsync().ConfigureAwait(false);

    return pricing;
  }

  // ── Product Integration ─────────────────────────────────────────────

  public async Task<Guid?> CreateProductFromProgramAsync(Guid programId, CreateProductFromProgramDto productDto)
  {
    var program = await context.Set<Program>().Where(p => p.DeletedAt == null).FirstOrDefaultAsync(p => p.Id == programId).ConfigureAwait(false);

    if (program == null) return null;

    var name = string.IsNullOrWhiteSpace(productDto.Name) ? program.Title : productDto.Name.Trim();
    var description = string.IsNullOrWhiteSpace(productDto.Description) ? program.Description : productDto.Description.Trim();
    var product = Product.Create(
      name,
      ProductType.Course,
      description,
      program.Description,
      program.Thumbnail,
      program.CreatorId,
      tenantId: program.TenantId);

    var (pricing, initialVersion) = ProductPricing.CreateWithVersion(
      product.Id,
      "Course access",
      productDto.BasePrice,
      productDto.Currency,
      salePrice: null,
      saleStartDate: null,
      saleEndDate: null,
      isDefault: true,
      createdByUserId: program.CreatorId,
      tenantId: program.TenantId);

    context.Set<Product>().Add(product);
    context.Set<ProductPricing>().Add(pricing);
    context.Set<ProductPricingVersion>().Add(initialVersion);

    var sortOrder = await GetNextProductProgramSortOrderAsync(product.Id).ConfigureAwait(false);
    context.Set<ProductProgram>().Add(new ProductProgram
    {
      ProductId = product.Id,
      ProgramId = program.Id,
      SortOrder = sortOrder,
      TenantId = program.TenantId,
    });

    await context.SaveChangesAsync().ConfigureAwait(false);

    return product.Id;
  }

  public async Task<bool> LinkProgramToProductAsync(Guid programId, Guid productId)
  {
    var program = await context.Set<Program>().Where(p => p.DeletedAt == null).FirstOrDefaultAsync(p => p.Id == programId).ConfigureAwait(false);

    if (program == null) return false;

    var productExists = await context.Set<Product>()
      .Where(p => p.DeletedAt == null)
      .AnyAsync(p => p.Id == productId)
      .ConfigureAwait(false);

    if (!productExists) return false;

    var existingLink = await context.Set<ProductProgram>()
      .Where(pp => pp.DeletedAt == null)
      .FirstOrDefaultAsync(pp => pp.ProgramId == programId && pp.ProductId == productId)
      .ConfigureAwait(false);

    if (existingLink != null) return true;

    var sortOrder = await GetNextProductProgramSortOrderAsync(productId).ConfigureAwait(false);
    context.Set<ProductProgram>().Add(new ProductProgram
    {
      ProductId = productId,
      ProgramId = programId,
      SortOrder = sortOrder,
      TenantId = program.TenantId,
    });

    await context.SaveChangesAsync().ConfigureAwait(false);

    return true;
  }

  public async Task<bool> UnlinkProgramFromProductAsync(Guid programId, Guid productId)
  {
    var link = await context.Set<ProductProgram>()
      .Where(pp => pp.DeletedAt == null)
      .FirstOrDefaultAsync(pp => pp.ProgramId == programId && pp.ProductId == productId)
      .ConfigureAwait(false);

    if (link == null) return false;

    context.Set<ProductProgram>().Remove(link);
    await context.SaveChangesAsync().ConfigureAwait(false);

    return true;
  }

  // ── Private Helpers ─────────────────────────────────────────────────

  private static string GenerateSlug(string title) { return title.ToLowerInvariant().Replace(" ", "-").Replace("'", "").Replace("\"", ""); }

  private async Task<int> GetNextProductProgramSortOrderAsync(Guid productId)
  {
    var currentMax = await context.Set<ProductProgram>()
      .Where(pp => pp.DeletedAt == null && pp.ProductId == productId)
      .MaxAsync(pp => (int?)pp.SortOrder)
      .ConfigureAwait(false);

    return (currentMax ?? -1) + 1;
  }

  private async Task RecalculateUserProgressAsync(Guid programUserId)
  {
    var programUser = await context.Set<ProgramUser>().Where(pu => pu.Id == programUserId).FirstOrDefaultAsync();

    if (programUser == null) return;

    var requiredContentIds = await context.Set<ProgramContent>()
      .Where(pc => pc.DeletedAt == null && pc.ProgramId == programUser.ProgramId && pc.IsRequired)
      .Select(pc => pc.Id)
      .ToListAsync()
      .ConfigureAwait(false);
    var totalContent = requiredContentIds.Count;

    if (totalContent == 0)
    {
      programUser.CompletionPercentage = PercentValue.Zero;

      return;
    }

    var completedContent = await context.Set<ContentInteraction>()
      .Where(ci =>
        ci.DeletedAt == null &&
        ci.ProgramUserId == programUserId &&
        requiredContentIds.Contains(ci.ContentId) &&
        (ci.IsCompleted || ci.Status == ProgressStatus.Completed))
      .Select(ci => ci.ContentId)
      .Distinct()
      .CountAsync()
      .ConfigureAwait(false);

    programUser.CompletionPercentage = PercentValue.FromRatio(completedContent, totalContent);

    if (programUser.CompletionPercentage.CompareTo(PercentValue.Hundred) == 0 && programUser.CompletedAt is null)
      programUser.CompletedAt = SystemClock.UtcNow;
    else if (programUser.CompletionPercentage.CompareTo(PercentValue.Hundred) < 0)
      programUser.CompletedAt = null;

    programUser.Touch();
  }

  private static void ApplyProgressStatus(ContentInteraction interaction, ProgressStatus status)
  {
    if (status == ProgressStatus.Completed)
    {
      interaction.Complete();
      return;
    }

    if (interaction.IsCompleted) return;

    interaction.Status = status;
    interaction.LastAccessedAt = SystemClock.UtcNow;
    interaction.Touch();
  }

  private static void CompleteInteraction(ContentInteraction interaction, Guid userId, DateTime now)
  {
    interaction.UserId = userId;
    interaction.FirstAccessedAt ??= now;
    interaction.StartedAt ??= now;
    interaction.Complete();
    interaction.LastAccessedAt = now;
    interaction.AttemptCount = Math.Max(1, interaction.AttemptCount);
    interaction.Touch();
  }

  private static void SubmitInteraction(
    ContentInteraction interaction,
    Guid userId,
    string submissionData,
    DateTime now)
  {
    interaction.UserId = userId;
    interaction.SubmissionData = submissionData;
    interaction.SubmittedAt = now;
    interaction.FirstAccessedAt ??= now;
    interaction.StartedAt ??= now;
    interaction.Complete();
    interaction.LastAccessedAt = now;
    interaction.AttemptCount = Math.Max(1, interaction.AttemptCount + 1);
    interaction.Touch();
  }

  private static Guid CreateDirectSubmissionAttemptId(Guid programUserId, Guid contentId)
  {
    Span<byte> source = stackalloc byte[32];
    programUserId.TryWriteBytes(source[..16]);
    contentId.TryWriteBytes(source[16..]);
    Span<byte> hash = stackalloc byte[32];
    SHA256.HashData(source, hash);
    return new Guid(hash[..16]);
  }

  private async Task<(ContentInteraction Interaction, bool IsConcurrentReplay)> SaveDirectSubmissionAsync(
    ContentInteraction newInteraction,
    string submissionData,
    DateTime now)
  {
    context.Set<ContentInteraction>().Add(newInteraction);
    try
    {
      await context.SaveChangesAsync().ConfigureAwait(false);
      return (newInteraction, false);
    }
    catch (DbUpdateException)
    {
      context.Set<ContentInteraction>().Remove(newInteraction);
      var winningInteraction = await context.Set<ContentInteraction>()
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(interaction => interaction.Id == newInteraction.Id)
        .ConfigureAwait(false);
      if (winningInteraction is null) throw;

      if (winningInteraction.DeletedAt is not null)
      {
        winningInteraction.Restore();
        SubmitInteraction(winningInteraction, newInteraction.UserId, submissionData, now);
        return (winningInteraction, false);
      }

      return (winningInteraction, true);
    }
  }

  private async Task<ContentInteraction> SaveNewActiveAttemptAsync(
    ContentInteraction newInteraction,
    Action<ContentInteraction> reconcileWinner)
  {
    context.Set<ContentInteraction>().Add(newInteraction);
    try
    {
      await context.SaveChangesAsync().ConfigureAwait(false);
      return newInteraction;
    }
    catch (DbUpdateException)
    {
      context.Set<ContentInteraction>().Remove(newInteraction);
      var winningInteraction = await context.Set<ContentInteraction>()
        .Where(interaction =>
          interaction.ProgramUserId == newInteraction.ProgramUserId &&
          interaction.UserId == newInteraction.UserId &&
          interaction.ContentId == newInteraction.ContentId &&
          interaction.SubmittedAt == null &&
          interaction.DeletedAt == null)
        .OrderByDescending(interaction => interaction.CreatedAt)
        .FirstOrDefaultAsync()
        .ConfigureAwait(false);
      if (winningInteraction is null) throw;

      reconcileWinner(winningInteraction);
      await context.SaveChangesAsync().ConfigureAwait(false);
      return winningInteraction;
    }
  }

  /// <summary>Internal helper to build a <see cref="UserProgressDto"/> without depending on the read service.</summary>
  private async Task<UserProgressDto?> GetUserProgressDtoInternalAsync(Guid programId, Guid userId)
  {
    var programUser = await context.Set<ProgramUser>().FirstOrDefaultAsync(pu => pu.ProgramId == programId && pu.UserId == userId && pu.DeletedAt == null);

    if (programUser == null) return null;

    var interactions = await context.Set<ContentInteraction>()
      .Include(ci => ci.Content)
      .Where(ci => ci.ProgramUserId == programUser.Id && ci.DeletedAt == null)
      .ToListAsync()
      .ConfigureAwait(false);
    var contentProgress = ContentInteractionAttemptSelection.CurrentPerContent(interactions)
      .Select(ci => new ContentProgressDto(
        ci.ContentId,
        ci.Content.Title,
        ci.Status,
        ci.CompletionPercentage,
        ci.FirstAccessedAt,
        ci.LastAccessedAt,
        ci.CompletedAt))
      .ToList();

    return new UserProgressDto(
      programUser.Id,
      programUser.ProgramId,
      programUser.UserId,
      programUser.CompletionPercentage,
      programUser.LastAccessedAt,
      programUser.StartedAt,
      programUser.CompletedAt,
      contentProgress
    );
  }
}
