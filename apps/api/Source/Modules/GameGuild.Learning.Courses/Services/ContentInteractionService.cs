using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Learning.Grading.Contracts;
using Microsoft.EntityFrameworkCore;


namespace GameGuild.Learning.Courses;

/// <summary>
///   Service for managing content interactions following the permission inheritance pattern ContentInteraction inherits permissions from Program -> ProgramContent -> ContentInteraction Once submitted, interactions become immutable but
///   users can create new interactions
/// </summary>
public class ContentInteractionService(
  IApplicationDbContext context,
  IRequestContextAccessor requestContextAccessor,
  IPermissionQueryService? permissionQueryService = null,
  IEnumerable<IProgramContentAcademicMutationGuard>? academicGuards = null) : IContentInteractionService {
  private readonly IEnumerable<IProgramContentAcademicMutationGuard> academicMutationGuards = academicGuards ?? [];
  /// <summary> Start a new content interaction (or resume existing one if not submitted) </summary>
  public async Task<ContentInteraction> StartContentAsync(Guid programUserId, Guid contentId) {
    if (context is DbContext dbContext &&
        dbContext.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL" &&
        dbContext.Database.CurrentTransaction is null) {
      var executionStrategy = dbContext.Database.CreateExecutionStrategy();
      return await executionStrategy.ExecuteAsync(() => StartContentCoreAsync(programUserId, contentId)).ConfigureAwait(false);
    }

    return await StartContentCoreAsync(programUserId, contentId).ConfigureAwait(false);
  }

  private async Task<ContentInteraction> StartContentCoreAsync(Guid programUserId, Guid contentId) {
    var currentUserId = requestContextAccessor.CurrentUserId;
    if (!currentUserId.HasValue)
      throw new RequestValidationException("Active course enrollment was not found.");

    var programUser = await context.Set<ProgramUser>()
      .AsNoTracking()
      .FirstOrDefaultAsync(item =>
        item.Id == programUserId &&
        item.UserId == currentUserId.Value &&
        item.DeletedAt == null &&
        item.IsActive)
      .ConfigureAwait(false);
    if (programUser == null) throw new RequestValidationException("Active course enrollment was not found.");

    var content = await context.Set<ProgramContent>()
      .AsNoTracking()
      .FirstOrDefaultAsync(item =>
        item.Id == contentId &&
        item.ProgramId == programUser.ProgramId &&
        item.DeletedAt == null)
      .ConfigureAwait(false);
    if (content is null)
      throw new InvalidOperationException("Content does not belong to the enrolled course.");
    ProgramContentAcademicMutationGuard.EnsureAllowed(academicMutationGuards, content, ProgramContentAcademicMutation.Start);

    await using var surveyPolicyTransaction = LearningActivityContract.RequiresSurveyPolicyLock(content.Type)
      ? await ProgramContentLifecycleDatabaseLock.AcquireAsync(context, [contentId]).ConfigureAwait(false)
      : null;
    content = LearningActivityContract.RequiresSurveyPolicyLock(content.Type)
      ? await context.Set<ProgramContent>()
        .AsNoTracking()
        .FirstOrDefaultAsync(item => item.Id == contentId && item.ProgramId == programUser.ProgramId && item.DeletedAt == null)
        .ConfigureAwait(false)
      : content;
    if (content is null)
      throw new InvalidOperationException("Content does not belong to the enrolled course.");

    if (content?.Type == ProgramContentType.Survey && !LearningActivityContract.AllowsMultipleResponses(content)) {
      var alreadySubmitted = await context.Set<ContentInteraction>()
        .AnyAsync(item => item.ProgramUserId == programUserId && item.ContentId == contentId && item.SubmittedAt != null && item.DeletedAt == null)
        .ConfigureAwait(false);
      if (alreadySubmitted) throw new InvalidOperationException("This survey accepts only one response.");
    }

    // Check if there's already an interaction for this user/content
    var existingInteraction = await context.Set<ContentInteraction>()
      .Where(ci => ci.ProgramUserId == programUserId && ci.ContentId == contentId)
      .OrderBy(ci => ci.SubmittedAt.HasValue)
      .ThenByDescending(ci => ci.CreatedAt)
      .FirstOrDefaultAsync()
      .ConfigureAwait(false);

    if (existingInteraction != null) {
      existingInteraction.UserId = programUser.UserId;

      // If already submitted, create a new interaction based on the last one
      if (existingInteraction.SubmittedAt.HasValue) {
        var next = await CreateNewInteractionFromPreviousAsync(existingInteraction).ConfigureAwait(false);
        await ProgramContentLifecycleDatabaseLock.CommitAsync(surveyPolicyTransaction).ConfigureAwait(false);
        return next;
      }

      // Otherwise, resume the existing interaction
      existingInteraction.FirstAccessedAt ??= SystemClock.UtcNow;
      existingInteraction.LastAccessedAt = SystemClock.UtcNow;
      if (!existingInteraction.IsCompleted)
        existingInteraction.Status = ProgressStatus.InProgress;

      await context.SaveChangesAsync().ConfigureAwait(false);

      await ProgramContentLifecycleDatabaseLock.CommitAsync(surveyPolicyTransaction).ConfigureAwait(false);
      return existingInteraction;
    }

    // Create new interaction
    var newInteraction = new ContentInteraction { ProgramUserId = programUserId, UserId = programUser.UserId, ContentId = contentId, Status = ProgressStatus.InProgress, FirstAccessedAt = SystemClock.UtcNow, LastAccessedAt = SystemClock.UtcNow, CompletionPercentage = PercentValue.Zero };

    var created = await SaveNewActiveAttemptAsync(newInteraction).ConfigureAwait(false);
    await ProgramContentLifecycleDatabaseLock.CommitAsync(surveyPolicyTransaction).ConfigureAwait(false);
    return created;
  }

  /// <summary> Update progress for an interaction (only if not submitted) </summary>
  public async Task<ContentInteraction> UpdateProgressAsync(Guid interactionId, PercentValue completionPercentage) {
    var interaction = await GetInteractionByIdAsync(interactionId).ConfigureAwait(false);
    ProgramContentAcademicMutationGuard.EnsureAllowed(academicMutationGuards, interaction.Content, ProgramContentAcademicMutation.UpdateProgress);

    if (interaction.SubmittedAt.HasValue) throw new InvalidOperationException("Cannot update progress on submitted interaction. Create a new interaction to continue work.");

    interaction.UpdateProgress(completionPercentage);
    if (!interaction.IsCompleted && interaction.Status == ProgressStatus.NotStarted)
      interaction.Status = ProgressStatus.InProgress;

    await context.SaveChangesAsync().ConfigureAwait(false);

    return interaction;
  }

  /// <summary> Submit content interaction (makes it immutable) </summary>
  public async Task<ContentInteraction> SubmitContentAsync(Guid interactionId, string submissionData) {
    if (context is DbContext dbContext &&
        dbContext.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL" &&
        dbContext.Database.CurrentTransaction is null) {
      var executionStrategy = dbContext.Database.CreateExecutionStrategy();
      return await executionStrategy.ExecuteAsync(() => SubmitContentCoreAsync(interactionId, submissionData)).ConfigureAwait(false);
    }

    return await SubmitContentCoreAsync(interactionId, submissionData).ConfigureAwait(false);
  }

  private async Task<ContentInteraction> SubmitContentCoreAsync(Guid interactionId, string submissionData) {
    var submissionTarget = await GetSubmissionTargetAsync(interactionId).ConfigureAwait(false);
    var currentContentType = await context.Set<ProgramContent>()
      .AsNoTracking()
      .Where(content => content.Id == submissionTarget.ContentId && content.DeletedAt == null)
      .Select(content => (ProgramContentType?)content.Type)
      .FirstOrDefaultAsync()
      .ConfigureAwait(false);
    if (!currentContentType.HasValue)
      throw new RequestValidationException("Content interaction was not found.");

    await using var submissionPolicyTransaction = LearningActivityContract.RequiresSubmissionPolicyLock(currentContentType.Value)
      ? await ProgramContentLifecycleDatabaseLock.AcquireAsync(context, [submissionTarget.ContentId]).ConfigureAwait(false)
      : null;
    if (LearningActivityContract.RequiresSubmissionPolicyLock(currentContentType.Value))
      DetachTrackedSubmissionTarget(interactionId, submissionTarget.ContentId);
    var interaction = await GetInteractionForSubmissionAsync(interactionId).ConfigureAwait(false);
    ProgramContentAcademicMutationGuard.EnsureAllowed(academicMutationGuards, interaction.Content, ProgramContentAcademicMutation.Submit);

    if (interaction.SubmittedAt.HasValue) throw new InvalidOperationException("Interaction has already been submitted and cannot be changed.");

    if (interaction.Content.Type == ProgramContentType.Survey && !LearningActivityContract.AllowsMultipleResponses(interaction.Content)) {
      var anotherResponseExists = await context.Set<ContentInteraction>()
        .AnyAsync(item =>
          item.Id != interaction.Id &&
          item.ProgramUserId == interaction.ProgramUserId &&
          item.ContentId == interaction.ContentId &&
          item.SubmittedAt != null &&
          item.DeletedAt == null)
        .ConfigureAwait(false);
      if (anotherResponseExists) throw new InvalidOperationException("This survey accepts only one response.");
    }

    var response = LearningActivityContract.IsActivityType(interaction.Content.Type)
      ? ActivityResponseContract.Parse(interaction.Content.Type, submissionData, interaction.Content.GetActivitySettings())
      : null;
    if (response is not null)
      await LearningActivityContract.ValidateDiscussionThreadRootAsync(
          context,
          interaction.Content.ProgramId,
          interaction.ContentId,
          response).ConfigureAwait(false);

    interaction.SubmissionData = submissionData;
    interaction.SubmittedAt = SystemClock.UtcNow;
    interaction.Complete();

    await SaveInteractionOnlyAsync(interaction).ConfigureAwait(false);
    await ProgramContentLifecycleDatabaseLock.CommitAsync(submissionPolicyTransaction).ConfigureAwait(false);

    return interaction;
  }

  /// <summary> Mark content as completed </summary>
  public async Task<ContentInteraction> CompleteContentAsync(Guid interactionId) {
    var interaction = await GetInteractionByIdAsync(interactionId).ConfigureAwait(false);
    ProgramContentAcademicMutationGuard.EnsureAllowed(academicMutationGuards, interaction.Content, ProgramContentAcademicMutation.Complete);

    if (interaction.SubmittedAt.HasValue) throw new InvalidOperationException("Cannot modify submitted interaction. Create a new interaction to continue work.");

    interaction.Complete();

    await context.SaveChangesAsync().ConfigureAwait(false);

    return interaction;
  }

  /// <summary> Get interaction for a specific user and content </summary>
  public async Task<ContentInteraction?> GetInteractionAsync(Guid programUserId, Guid contentId) {
    var currentUserId = requestContextAccessor.CurrentUserId;
    if (!currentUserId.HasValue) return null;

    return await context.Set<ContentInteraction>().Include(ci => ci.ProgramUser).Include(ci => ci.Content).Include(ci => ci.ActivityGrades)
      .OrderBy(ci => ci.SubmittedAt.HasValue)
      .ThenByDescending(ci => ci.CreatedAt)
      .FirstOrDefaultAsync(ci =>
        ci.ProgramUserId == programUserId &&
        ci.ContentId == contentId &&
        ci.UserId == currentUserId.Value)
      .ConfigureAwait(false);
  }

  /// <summary> Get all interactions for a user </summary>
  public async Task<IEnumerable<ContentInteraction>> GetUserInteractionsAsync(Guid programUserId) {
    var currentUserId = requestContextAccessor.CurrentUserId;
    if (!currentUserId.HasValue) return [];

    return await context.Set<ContentInteraction>().Include(ci => ci.Content).Include(ci => ci.ActivityGrades)
      .Where(ci => ci.ProgramUserId == programUserId && ci.UserId == currentUserId.Value)
      .OrderByDescending(ci => ci.LastAccessedAt)
      .ToListAsync()
      .ConfigureAwait(false);
  }

  /// <summary>Gets survey result projections without exposing learner or enrollment identity.</summary>
  public async Task<IEnumerable<SurveyResponseResultDto>> GetSurveyResponsesAsync(Guid expectedProgramId, Guid contentId) {
    var actorId = requestContextAccessor.CurrentUserId;
    if (!requestContextAccessor.IsAuthenticated || !actorId.HasValue)
      throw new RequestValidationException("Program management permission is required.");

    var program = await GetTenantScopedProgramAsync(expectedProgramId, "Program management permission is required.").ConfigureAwait(false);
    var content = await context.Set<ProgramContent>()
      .FirstOrDefaultAsync(item => item.Id == contentId && item.ProgramId == expectedProgramId && item.DeletedAt == null)
      .ConfigureAwait(false);
    if (content is null || content.Type != ProgramContentType.Survey)
      throw new RequestValidationException("Survey content was not found for the specified program.");

    if (!await HasProgramReviewAccessAsync(program.Id, actorId.Value).ConfigureAwait(false))
      throw new RequestValidationException("Program review permission is required.");

    var interactions = await context.Set<ContentInteraction>()
      .Where(item => item.ContentId == contentId && item.SubmittedAt != null && item.DeletedAt == null)
      .OrderBy(item => item.SubmittedAt)
      .ToListAsync()
      .ConfigureAwait(false);

    return interactions.Select(interaction => SurveyResponseResultDto.FromInteraction(
      interaction,
      !LearningActivityContract.IsAnonymousSurvey(content))).ToList();
  }

  public async Task<IEnumerable<SurveyResponseResultDto>> GetVisibleSurveyResponsesAsync(Guid expectedProgramId, Guid contentId) {
    var learnerId = requestContextAccessor.CurrentUserId;
    if (!requestContextAccessor.IsAuthenticated || !learnerId.HasValue)
      throw new RequestValidationException("Active course enrollment is required.");

    await GetTenantScopedProgramAsync(expectedProgramId, "Active course enrollment is required.").ConfigureAwait(false);
    var content = await context.Set<ProgramContent>()
      .FirstOrDefaultAsync(item => item.Id == contentId && item.ProgramId == expectedProgramId && item.DeletedAt == null)
      .ConfigureAwait(false);
    if (content is null || content.Type != ProgramContentType.Survey)
      throw new RequestValidationException("Survey content was not found for the specified program.");

    var enrollment = await context.Set<ProgramUser>()
      .FirstOrDefaultAsync(item => item.ProgramId == expectedProgramId && item.UserId == learnerId.Value && item.DeletedAt == null && item.IsActive)
      .ConfigureAwait(false);
    if (enrollment is null)
      throw new RequestValidationException("Active course enrollment is required.");

    var settings = content.GetActivitySettings() as SurveyActivitySettings ?? new SurveyActivitySettings();
    var learnerSubmitted = await context.Set<ContentInteraction>()
      .AnyAsync(item => item.ProgramUserId == enrollment.Id && item.ContentId == contentId && item.SubmittedAt != null && item.DeletedAt == null)
      .ConfigureAwait(false);
    var courseClosed = await context.Set<Program>()
      .Where(program => program.Id == expectedProgramId && program.DeletedAt == null)
      .Select(program => program.EnrollmentStatus != EnrollmentStatus.Open)
      .FirstOrDefaultAsync()
      .ConfigureAwait(false);
    var visible = settings.ResultsVisibility switch {
      SurveyResultsVisibility.AfterSubmission => learnerSubmitted,
      SurveyResultsVisibility.AfterClose => courseClosed,
      SurveyResultsVisibility.Never => false,
      _ => false,
    };
    if (!visible) throw new RequestValidationException("Survey results are not available to learners.");

    var interactions = await context.Set<ContentInteraction>()
      .Where(item => item.ContentId == contentId && item.SubmittedAt != null && item.DeletedAt == null)
      .OrderBy(item => item.SubmittedAt)
      .ToListAsync()
      .ConfigureAwait(false);
    return interactions.Select(interaction => SurveyResponseResultDto.FromInteraction(interaction)).ToList();
  }

  public async Task<IEnumerable<ReflectionResponseResultDto>> GetReflectionResponsesAsync(Guid expectedProgramId, Guid contentId) {
    var actorId = requestContextAccessor.CurrentUserId;
    if (!requestContextAccessor.IsAuthenticated || !actorId.HasValue)
      throw new RequestValidationException("Program management permission is required.");
    var program = await GetTenantScopedProgramAsync(expectedProgramId, "Program management permission is required.").ConfigureAwait(false);
    if (!await HasProgramReviewAccessAsync(program.Id, actorId.Value).ConfigureAwait(false))
      throw new RequestValidationException("Program review permission is required.");
    var content = await GetReflectionContentAsync(program.Id, contentId).ConfigureAwait(false);
    var interactions = await SubmittedInteractionsAsync(content.Id).ConfigureAwait(false);
    return interactions.Select(interaction => ReflectionResponseResultDto.FromInteraction(interaction, true)).ToList();
  }

  public async Task<IEnumerable<ReflectionResponseResultDto>> GetVisibleReflectionResponsesAsync(Guid expectedProgramId, Guid contentId) {
    var learnerId = requestContextAccessor.CurrentUserId;
    if (!requestContextAccessor.IsAuthenticated || !learnerId.HasValue)
      throw new RequestValidationException("Active course enrollment is required.");
    var program = await GetTenantScopedProgramAsync(expectedProgramId, "Active course enrollment is required.").ConfigureAwait(false);
    var enrollment = await context.Set<ProgramUser>()
      .FirstOrDefaultAsync(item => item.ProgramId == program.Id && item.UserId == learnerId.Value && item.DeletedAt == null && item.IsActive)
      .ConfigureAwait(false);
    if (enrollment is null) throw new RequestValidationException("Active course enrollment is required.");
    var content = await GetReflectionContentAsync(program.Id, contentId).ConfigureAwait(false);
    var interactions = await SubmittedInteractionsAsync(content.Id).ConfigureAwait(false);
    if (content.GetActivitySettings() is ReflectionActivitySettings { PrivateToInstructors: true })
      interactions = interactions.Where(interaction => interaction.ProgramUserId == enrollment.Id).ToList();
    return interactions.Select(interaction => ReflectionResponseResultDto.FromInteraction(interaction)).ToList();
  }

  private async Task<Program> GetTenantScopedProgramAsync(Guid programId, string failureMessage) {
    var tenantId = requestContextAccessor.CurrentTenantId;
    if (!tenantId.HasValue) throw new RequestValidationException(failureMessage);
    // Global programs are intentionally visible in every tenant; tenant-owned programs are not.
    var program = await context.Set<Program>()
      .FirstOrDefaultAsync(item => item.Id == programId && item.DeletedAt == null && (item.TenantId == null || item.TenantId == tenantId.Value))
      .ConfigureAwait(false);
    return program ?? throw new RequestValidationException(failureMessage);
  }

  private async Task<ProgramContent> GetReflectionContentAsync(Guid programId, Guid contentId) {
    var content = await context.Set<ProgramContent>()
      .FirstOrDefaultAsync(item => item.Id == contentId && item.ProgramId == programId && item.Type == ProgramContentType.Reflection && item.DeletedAt == null)
      .ConfigureAwait(false);
    return content ?? throw new RequestValidationException("Reflection content was not found for the specified program.");
  }

  private Task<List<ContentInteraction>> SubmittedInteractionsAsync(Guid contentId) =>
    context.Set<ContentInteraction>()
      .Where(item => item.ContentId == contentId && item.SubmittedAt != null && item.DeletedAt == null)
      .OrderBy(item => item.SubmittedAt)
      .ToListAsync();

  private Task<bool> HasProgramReviewAccessAsync(Guid programId, Guid actorId) {
    if (!requestContextAccessor.CurrentTenantId.HasValue || permissionQueryService is null) return Task.FromResult(false);
    return permissionQueryService.HasTenantPermissionAsync(
      actorId,
      requestContextAccessor.CurrentTenantId,
      $"{nameof(Program)}.{programId}.{PermissionType.Review}");
  }

  /// <summary> Update time spent on content </summary>
  public async Task<ContentInteraction> UpdateTimeSpentAsync(Guid interactionId, int additionalMinutes) {
    var interaction = await GetInteractionByIdAsync(interactionId).ConfigureAwait(false);

    if (interaction.SubmittedAt.HasValue) throw new InvalidOperationException("Cannot update time spent on submitted interaction.");

    interaction.AddTimeSpent(additionalMinutes);

    await context.SaveChangesAsync().ConfigureAwait(false);

    return interaction;
  }

  /// <summary> Get interaction by ID with proper error handling </summary>
  private async Task<ContentInteraction> GetInteractionByIdAsync(Guid interactionId) {
    var currentUserId = requestContextAccessor.CurrentUserId;
    if (!currentUserId.HasValue)
      throw new RequestValidationException("Content interaction was not found.");

    var interaction = await context.Set<ContentInteraction>().Include(ci => ci.ProgramUser).Include(ci => ci.Content).Include(ci => ci.ActivityGrades)
      .FirstOrDefaultAsync(ci => ci.Id == interactionId && ci.UserId == currentUserId.Value)
      .ConfigureAwait(false);

    if (interaction == null) throw new RequestValidationException("Content interaction was not found.");

    return interaction;
  }

  private async Task<ContentInteraction> GetInteractionForSubmissionAsync(Guid interactionId, bool track = true) {
    var currentUserId = requestContextAccessor.CurrentUserId;
    if (!currentUserId.HasValue)
      throw new RequestValidationException("Content interaction was not found.");

    IQueryable<ContentInteraction> interactions = context.Set<ContentInteraction>().Include(ci => ci.Content);
    if (!track) interactions = interactions.AsNoTracking();
    var interaction = await interactions
      .FirstOrDefaultAsync(ci => ci.Id == interactionId && ci.UserId == currentUserId.Value)
      .ConfigureAwait(false);
    if (interaction == null) throw new RequestValidationException("Content interaction was not found.");

    return interaction;
  }

  private async Task<SubmissionTarget> GetSubmissionTargetAsync(Guid interactionId) {
    var currentUserId = requestContextAccessor.CurrentUserId;
    if (!currentUserId.HasValue)
      throw new RequestValidationException("Content interaction was not found.");

    var target = await context.Set<ContentInteraction>()
      .AsNoTracking()
      .Where(interaction => interaction.Id == interactionId && interaction.UserId == currentUserId.Value)
      .Select(interaction => new SubmissionTarget(interaction.ContentId))
      .FirstOrDefaultAsync()
      .ConfigureAwait(false);
    if (target is null) throw new RequestValidationException("Content interaction was not found.");

    return target;
  }

  private async Task SaveInteractionOnlyAsync(ContentInteraction interaction) {
    if (context is not DbContext dbContext) {
      await context.SaveChangesAsync().ConfigureAwait(false);
      return;
    }

    var pendingEntries = dbContext.ChangeTracker.Entries()
      .Where(entry => !ReferenceEquals(entry.Entity, interaction) && entry.State is not EntityState.Unchanged and not EntityState.Detached)
      .Select(entry => new PendingEntry(
        entry.Entity,
        entry.State,
        entry.CurrentValues.Clone(),
        entry.OriginalValues.Clone(),
        entry.Properties.Where(property => property.IsModified).Select(property => property.Metadata.Name).ToHashSet()))
      .ToList();
    foreach (var pending in pendingEntries) dbContext.Entry(pending.Entity).State = EntityState.Unchanged;

    try {
      await context.SaveChangesAsync().ConfigureAwait(false);
    }
    finally {
      foreach (var pending in pendingEntries) {
        var entry = dbContext.Entry(pending.Entity);
        entry.CurrentValues.SetValues(pending.CurrentValues);
        entry.OriginalValues.SetValues(pending.OriginalValues);
        entry.State = pending.State;
        if (pending.State == EntityState.Modified)
          foreach (var property in entry.Properties)
            property.IsModified = pending.ModifiedPropertyNames.Contains(property.Metadata.Name);
      }
    }
  }

  private void DetachTrackedSubmissionTarget(Guid interactionId, Guid contentId) {
    if (context is not DbContext dbContext) return;

    foreach (var entry in dbContext.ChangeTracker.Entries<ContentInteraction>()
               .Where(entry => entry.Entity.Id == interactionId)
               .ToList())
      entry.State = EntityState.Detached;
    foreach (var entry in dbContext.ChangeTracker.Entries<ProgramContent>()
               .Where(entry => entry.Entity.Id == contentId)
               .ToList())
      entry.State = EntityState.Detached;
  }

  private sealed record PendingEntry(
    object Entity,
    EntityState State,
    Microsoft.EntityFrameworkCore.ChangeTracking.PropertyValues CurrentValues,
    Microsoft.EntityFrameworkCore.ChangeTracking.PropertyValues OriginalValues,
    HashSet<string> ModifiedPropertyNames);

  private sealed record SubmissionTarget(Guid ContentId);

  /// <summary> Create a new interaction based on previous submission data This allows users to continue working after submitting </summary>
  private async Task<ContentInteraction> CreateNewInteractionFromPreviousAsync(ContentInteraction previousInteraction) {
    var newInteraction = new ContentInteraction {
      ProgramUserId = previousInteraction.ProgramUserId,
      UserId = previousInteraction.UserId,
      ContentId = previousInteraction.ContentId,
      Status = ProgressStatus.InProgress,
      FirstAccessedAt = SystemClock.UtcNow,
      LastAccessedAt = SystemClock.UtcNow,
      CompletionPercentage = PercentValue.Zero,
      // Initialize with previous submission data as starting point
      SubmissionData = previousInteraction.SubmissionData,
    };

    return await SaveNewActiveAttemptAsync(newInteraction).ConfigureAwait(false);
  }

  private async Task<ContentInteraction> SaveNewActiveAttemptAsync(ContentInteraction newInteraction) {
    context.Set<ContentInteraction>().Add(newInteraction);
    try {
      await context.SaveChangesAsync().ConfigureAwait(false);
      return newInteraction;
    }
    catch (DbUpdateException) {
      context.Set<ContentInteraction>().Remove(newInteraction);
      var winningInteraction = await context.Set<ContentInteraction>()
        .Where(ci =>
          ci.ProgramUserId == newInteraction.ProgramUserId &&
          ci.UserId == newInteraction.UserId &&
          ci.ContentId == newInteraction.ContentId &&
          ci.SubmittedAt == null &&
          ci.DeletedAt == null)
        .OrderByDescending(ci => ci.CreatedAt)
        .FirstOrDefaultAsync()
        .ConfigureAwait(false);
      if (winningInteraction is not null) return winningInteraction;

      throw;
    }
  }

}
