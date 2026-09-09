

using Microsoft.EntityFrameworkCore;


namespace GameGuild.Learning.Courses;

/// <summary> Service implementation for ProgramContent management with full DAC permission support Handles CRUD operations, hierarchical content structure, and content ordering </summary>
public class ProgramContentService(
  IApplicationDbContext context,
  IProgramContentScheduleGuard scheduleGuard,
  IProgramContentLifecycleGuard lifecycleGuard,
  IEnumerable<IProgramContentAcademicMutationGuard>? academicGuards = null) : IProgramContentService {
  private readonly IEnumerable<IProgramContentAcademicMutationGuard> academicMutationGuards = academicGuards ?? [];
  public async Task<ProgramContent> CreateContentAsync(ProgramContent content) {
    content.NormalizeLearningContract();
    ProgramContentAcademicMutationGuard.EnsureAllowed(academicMutationGuards, content, ProgramContentAcademicMutation.Authoring);

    // Set creation timestamp
    content.Touch();

    // If no sort order is specified, put it at the end
    if (content.SortOrder == 0) {
      var maxOrder = await context.Set<ProgramContent>().Where(pc => pc.ProgramId == content.ProgramId && pc.ParentId == content.ParentId && pc.DeletedAt == null).MaxAsync(pc => (int?)pc.SortOrder) ?? 0;
      content.SortOrder = maxOrder + 1;
    }

    context.Set<ProgramContent>().Add(content);
    await context.SaveChangesAsync().ConfigureAwait(false);

    return content;
  }

  public async Task<ProgramContent?> GetContentByIdAsync(Guid id) {
    return await context.Set<ProgramContent>().Include(pc => pc.Program).Include(pc => pc.Parent).Include(pc => pc.Children.Where(c => c.DeletedAt == null)).Where(pc => pc.DeletedAt == null).FirstOrDefaultAsync(pc => pc.Id == id);
  }

  public async Task<IEnumerable<ProgramContent>> GetContentByProgramAsync(Guid programId) {
    return await context.Set<ProgramContent>().Include(pc => pc.Parent).Include(pc => pc.Children.Where(c => c.DeletedAt == null)).Where(pc => pc.ProgramId == programId && pc.DeletedAt == null).OrderBy(pc => pc.SortOrder).ToListAsync();
  }

  public async Task<IEnumerable<ProgramContent>> GetContentByParentAsync(Guid parentId) {
    return await context.Set<ProgramContent>().Include(pc => pc.Children.Where(c => c.DeletedAt == null)).Where(pc => pc.ParentId == parentId && pc.DeletedAt == null).OrderBy(pc => pc.SortOrder).ToListAsync();
  }

  public async Task<IEnumerable<ProgramContent>> GetTopLevelContentAsync(Guid programId) {
    return await context.Set<ProgramContent>().Include(pc => pc.Children.Where(c => c.DeletedAt == null)).Where(pc => pc.ProgramId == programId && pc.ParentId == null && pc.DeletedAt == null).OrderBy(pc => pc.SortOrder).ToListAsync();
  }

  public async Task<ProgramContent> UpdateContentAsync(ProgramContent content) => await ExecuteInContentLifecycleStrategyAsync(async () => {
    await using var lifecycleTransaction = await ProgramContentLifecycleDatabaseLock
      .AcquireAsync(context, [content.Id])
      .ConfigureAwait(false);
    var existingContent = await context.Set<ProgramContent>().FirstOrDefaultAsync(pc => pc.Id == content.Id && pc.DeletedAt == null);

    if (existingContent == null) throw new InvalidOperationException($"ProgramContent with ID {content.Id} not found or has been deleted");

    // Update properties
    content.NormalizeLearningContract();
    ProgramContentAcademicMutationGuard.EnsureAllowed(academicMutationGuards, content, ProgramContentAcademicMutation.Authoring);
    if (await lifecycleGuard.HasBlockingIncompatibleUpdateReference(
            existingContent.Id,
            content.Type,
            content.LessonFormat).ConfigureAwait(false)) {
      throw new GameGuild.CQRS.RequestValidationException(
        "Content linked to an assessment cue must remain a video lesson. Remove the assessment cue first.");
    }
    existingContent.Title = content.Title;
    existingContent.Description = content.Description;
    existingContent.Type = content.Type;
    existingContent.Body = content.Body;
    existingContent.LessonFormat = content.LessonFormat;
    existingContent.SortOrder = content.SortOrder;
    existingContent.IsRequired = content.IsRequired;
    existingContent.EstimatedMinutes = content.EstimatedMinutes;
    existingContent.Visibility = content.Visibility;
    existingContent.NormalizeLearningContract();
    existingContent.Touch();

    await context.SaveChangesAsync().ConfigureAwait(false);
    await ProgramContentLifecycleDatabaseLock.CommitAsync(lifecycleTransaction).ConfigureAwait(false);

    return existingContent;
  }).ConfigureAwait(false);

  public async Task<bool> DeleteContentAsync(Guid id) => await ExecuteInContentLifecycleStrategyAsync(async () => {
    var content = await context.Set<ProgramContent>().FirstOrDefaultAsync(pc => pc.Id == id && pc.DeletedAt == null);

    if (content == null) return false;

    var contents = await context.Set<ProgramContent>().Where(pc => pc.ProgramId == content.ProgramId && pc.DeletedAt == null).ToListAsync();
    var contentTreeIds = ProgramContentTree.GetIds(id, contents);
    await using var lifecycleTransaction = await ProgramContentLifecycleDatabaseLock
      .AcquireAsync(context, contentTreeIds)
      .ConfigureAwait(false);
    contents = await context.Set<ProgramContent>().Where(pc => pc.ProgramId == content.ProgramId && pc.DeletedAt == null).ToListAsync();
    contentTreeIds = ProgramContentTree.GetIds(id, contents);
    foreach (var contentId in contentTreeIds) {
      if (await scheduleGuard.HasActiveScheduleReference(contentId).ConfigureAwait(false)) {
        throw new GameGuild.CQRS.RequestValidationException(
          "Content used by an active class schedule cannot be deleted. Remove or replace its schedule entry first.");
      }

      if (await lifecycleGuard.HasBlockingDeleteReference(contentId).ConfigureAwait(false)) {
        throw new GameGuild.CQRS.RequestValidationException(
          "Content linked to an assessment cue cannot be deleted. Remove the assessment cue first.");
      }
    }

    SoftDeleteContentTree(id, contents);

    await context.SaveChangesAsync().ConfigureAwait(false);
    await ProgramContentLifecycleDatabaseLock.CommitAsync(lifecycleTransaction).ConfigureAwait(false);

    return true;
  }).ConfigureAwait(false);

  private async Task<T> ExecuteInContentLifecycleStrategyAsync<T>(Func<Task<T>> operation)
  {
    if (context is not DbContext dbContext ||
        dbContext.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL")
    {
      return await operation().ConfigureAwait(false);
    }

    var strategy = dbContext.Database.CreateExecutionStrategy();
    return await strategy.ExecuteAsync(operation).ConfigureAwait(false);
  }

  private static void SoftDeleteContentTree(Guid rootId, IEnumerable<ProgramContent> contents) {
    var contentById = contents.ToDictionary(content => content.Id);
    var childrenByParentId = contents
      .Where(content => content.ParentId.HasValue)
      .GroupBy(content => content.ParentId!.Value)
      .ToDictionary(group => group.Key, group => group.ToList());
    var pending = new Stack<Guid>();
    var visited = new HashSet<Guid>();

    pending.Push(rootId);
    while (pending.Count > 0) {
      var contentId = pending.Pop();
      if (!visited.Add(contentId) || !contentById.TryGetValue(contentId, out var content)) continue;

      content.SoftDelete();

      if (!childrenByParentId.TryGetValue(contentId, out var children)) continue;
      foreach (var child in children) pending.Push(child.Id);
    }
  }

  public async Task<bool> ReorderContentAsync(Guid programId, List<(Guid contentId, int sortOrder)> newOrder) {
    // Get all content items to reorder
    var contentIds = newOrder.Select(x => x.contentId).ToList();
    var contentItems = await context.Set<ProgramContent>().Where(pc => contentIds.Contains(pc.Id) && pc.ProgramId == programId && pc.DeletedAt == null).ToListAsync();

    if (contentItems.Count != newOrder.Count) return false; // Some content items not found

    // Update sort orders
    foreach (var (contentId, sortOrder) in newOrder) {
      var content = contentItems.First(c => c.Id == contentId);
      content.SortOrder = sortOrder;
      content.Touch();
    }

    await context.SaveChangesAsync().ConfigureAwait(false);

    return true;
  }

  public async Task<IEnumerable<ProgramContent>> GetRequiredContentAsync(Guid programId) {
    return await context.Set<ProgramContent>().Where(pc => pc.ProgramId == programId && pc.IsRequired && pc.DeletedAt == null).OrderBy(pc => pc.SortOrder).ToListAsync();
  }

  public async Task<IEnumerable<ProgramContent>> GetContentByTypeAsync(Guid programId, ProgramContentType type) {
    return await context.Set<ProgramContent>().Where(pc => pc.ProgramId == programId && pc.Type == type && pc.DeletedAt == null).OrderBy(pc => pc.SortOrder).ToListAsync();
  }

  public async Task<IEnumerable<ProgramContent>> GetContentByVisibilityAsync(Guid programId, Visibility visibility) {
    return await context.Set<ProgramContent>().Where(pc => pc.ProgramId == programId && pc.Visibility == visibility && pc.DeletedAt == null).OrderBy(pc => pc.SortOrder).ToListAsync();
  }

  public async Task<bool> MoveContentAsync(Guid contentId, Guid? newParentId, int newSortOrder) {
    var content = await context.Set<ProgramContent>().FirstOrDefaultAsync(pc => pc.Id == contentId && pc.DeletedAt == null);

    if (content == null) return false;

    // Update parent and sort order
    content.ParentId = newParentId;
    content.SortOrder = newSortOrder;
    content.Touch();

    await context.SaveChangesAsync().ConfigureAwait(false);

    return true;
  }

  public async Task<int> GetContentCountAsync(Guid programId) { return await context.Set<ProgramContent>().CountAsync(pc => pc.ProgramId == programId && pc.DeletedAt == null); }

  public async Task<int> GetRequiredContentCountAsync(Guid programId) { return await context.Set<ProgramContent>().CountAsync(pc => pc.ProgramId == programId && pc.IsRequired && pc.DeletedAt == null); }

  public async Task<IEnumerable<ProgramContent>> SearchContentAsync(Guid programId, string searchTerm) {
    return await context.Set<ProgramContent>().Where(pc => pc.ProgramId == programId && pc.DeletedAt == null && (pc.Title.Contains(searchTerm) || (pc.Description != null && pc.Description.Contains(searchTerm)))).OrderBy(pc => pc.SortOrder).ToListAsync();
  }
}
