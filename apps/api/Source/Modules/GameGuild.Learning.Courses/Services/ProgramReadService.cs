using Microsoft.EntityFrameworkCore;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Courses;

/// <summary>
/// Read-side service for Programs: lookups, listings, search, filtering,
/// analytics, statistics, and read-only user/progress queries.
/// </summary>
public class ProgramReadService(IApplicationDbContext context) : IProgramReadService
{
  private static decimal Percentage(int numerator, int denominator) => denominator <= 0 ? 0m : Math.Round((decimal)numerator / denominator * 100m, 2);

  private static TimeSpan AverageDuration(IEnumerable<TimeSpan> durations)
  {
    var validDurations = durations.Where(duration => duration > TimeSpan.Zero).ToList();

    return validDurations.Count == 0
      ? TimeSpan.Zero
      : new TimeSpan(Convert.ToInt64(validDurations.Average(duration => duration.Ticks)));
  }

  // ── Single-entity Lookups ───────────────────────────────────────────

  public async Task<Program?> GetProgramByIdAsync(Guid id) { return await context.Set<Program>().Where(p => p.DeletedAt == null).FirstOrDefaultAsync(p => p.Id == id); }

  public async Task<Program?> GetProgramBySlugAsync(string slug) { return await context.Set<Program>().Where(p => p.DeletedAt == null).FirstOrDefaultAsync(p => p.Slug == slug); }

  public async Task<Program?> GetPublishedProgramBySlugAsync(string slug)
  {
    return await context.Set<Program>().Where(p => p.DeletedAt == null && p.Status == ContentStatus.Published && p.Visibility == ContentVisibility.Public).FirstOrDefaultAsync(p => p.Slug == slug);
  }

  public async Task<Program?> GetProgramWithContentAsync(Guid id)
  {
    return await context.Set<Program>().Include(p => p.ProgramContents.Where(pc => pc.DeletedAt == null)).Include(p => p.ProgramUsers.Where(pu => pu.DeletedAt == null)).Where(p => p.DeletedAt == null).FirstOrDefaultAsync(p => p.Id == id);
  }

  public async Task<bool> ProgramExistsAsync(Guid id) { return await context.Set<Program>().Where(p => p.DeletedAt == null).AnyAsync(p => p.Id == id); }

  // ── Listings ────────────────────────────────────────────────────────

  public async Task<IEnumerable<Program>> GetProgramsAsync(int skip = 0, int take = 50) { return await context.Set<Program>().Where(p => p.DeletedAt == null).OrderByDescending(p => p.CreatedAt).Skip(skip).Take(take).ToListAsync(); }

  public async Task<IEnumerable<ProgramContent>> GetProgramContentAsync(Guid programId) { return await context.Set<ProgramContent>().Where(pc => pc.DeletedAt == null && pc.ProgramId == programId).OrderBy(pc => pc.SortOrder).ToListAsync(); }

  public async Task<IEnumerable<Program>> GetPublishedProgramsAsync(int skip = 0, int take = 50)
  {
    return await context.Set<Program>().Where(p => p.DeletedAt == null && p.Status == ContentStatus.Published).OrderByDescending(p => p.CreatedAt).Skip(skip).Take(take).ToListAsync();
  }

  public async Task<IEnumerable<Program>> GetPublicPublishedProgramsAsync(int skip = 0, int take = 50)
  {
    return await context.Set<Program>()
      .Where(p => p.DeletedAt == null && p.Status == ContentStatus.Published && p.Visibility == ContentVisibility.Public)
      .OrderByDescending(p => p.CreatedAt)
      .Skip(skip)
      .Take(take)
      .ToListAsync();
  }

  // ── Search & Discovery ──────────────────────────────────────────────

  public async Task<IEnumerable<Program>> SearchProgramsAsync(string searchTerm, int skip = 0, int take = 50)
  {
    if (string.IsNullOrWhiteSpace(searchTerm)) return await GetProgramsAsync(skip, take).ConfigureAwait(false);

    return await context.Set<Program>().Where(p => p.DeletedAt == null && (p.Title.Contains(searchTerm) || (p.Description != null && p.Description.Contains(searchTerm)))).OrderByDescending(p => p.CreatedAt).Skip(skip).Take(take).ToListAsync();
  }

  public async Task<IEnumerable<Program>> GetProgramsByCreatorAsync(Guid creatorId, int skip = 0, int take = 50)
  {
    return await context.Set<Program>().Where(p => p.DeletedAt == null).OrderByDescending(p => p.CreatedAt).Skip(skip).Take(take).ToListAsync();
  }

  public async Task<IEnumerable<Program>> GetFeaturedProgramsAsync(int count = 10)
  {
    return await context.Set<Program>().Where(p => p.DeletedAt == null && p.Status == ContentStatus.Published).OrderByDescending(p => p.ProgramUsers.Count(pu => pu.DeletedAt == null && pu.IsActive)).Take(count).ToListAsync();
  }

  public async Task<IEnumerable<Program>> GetRecentProgramsAsync(int count = 10)
  {
    return await context.Set<Program>().Where(p => p.DeletedAt == null && p.Status == ContentStatus.Published).OrderByDescending(p => p.CreatedAt).Take(count).ToListAsync();
  }

  public async Task<IEnumerable<Program>> GetPopularProgramsAsync(int count = 10)
  {
    return await context.Set<Program>().Where(p => p.DeletedAt == null && p.Status == ContentStatus.Published).OrderByDescending(p => p.ProgramUsers.Count(pu => pu.DeletedAt == null && pu.IsActive)).ThenByDescending(p => p.CreatedAt).Take(count).ToListAsync();
  }

  public async Task<IEnumerable<Program>> GetProgramsByCategoryAsync(ProgramCategory category, int skip = 0, int take = 50)
  {
    return await context.Set<Program>().Where(p => p.DeletedAt == null && p.Category == category).OrderByDescending(p => p.CreatedAt).Skip(skip).Take(take).ToListAsync();
  }

  public async Task<IEnumerable<Program>> GetProgramsByDifficultyAsync(ProgramDifficulty difficulty, int skip = 0, int take = 50)
  {
    return await context.Set<Program>().Where(p => p.DeletedAt == null && p.Difficulty == difficulty).OrderByDescending(p => p.CreatedAt).Skip(skip).Take(take).ToListAsync();
  }

  // ── User & Progress Queries ─────────────────────────────────────────

  public async Task<IEnumerable<ProgramUser>> GetProgramUsersAsync(Guid programId)
  {
    return await context.Set<ProgramUser>().Include(pu => pu.User).Where(pu => pu.DeletedAt == null && pu.ProgramId == programId && pu.IsActive).OrderBy(pu => pu.JoinedAt).ToListAsync();
  }

  public async Task<IEnumerable<Program>> GetUserProgramsAsync(Guid userId)
  {
    return await context.Set<ProgramUser>().Include(pu => pu.Program).Where(pu => pu.DeletedAt == null && pu.UserId == userId && pu.IsActive).Select(pu => pu.Program).Where(p => p.DeletedAt == null).OrderByDescending(p => p.CreatedAt).ToListAsync();
  }

  public async Task<bool> IsUserInProgramAsync(Guid programId, Guid userId) { return await context.Set<ProgramUser>().Where(pu => pu.DeletedAt == null && pu.ProgramId == programId && pu.UserId == userId && pu.IsActive).AnyAsync(); }

  public async Task<IEnumerable<UserProgressDto>> GetProgramUsersAsync(Guid programId, int skip = 0, int take = 50)
  {
    var programUsers = await context.Set<ProgramUser>().Where(pu => pu.ProgramId == programId && pu.DeletedAt == null).Skip(skip).Take(take).ToListAsync();

    var result = new List<UserProgressDto>();

    foreach (var pu in programUsers)
    {
      var progress = await GetUserProgressDtoAsync(programId, pu.UserId).ConfigureAwait(false);
      if (progress != null) result.Add(progress);
    }

    return result;
  }

  public async Task<PercentValue> GetUserProgressAsync(Guid programId, Guid userId)
  {
    var programUser = await context.Set<ProgramUser>().Where(pu => pu.DeletedAt == null && pu.ProgramId == programId && pu.UserId == userId).FirstOrDefaultAsync();

    return programUser?.CompletionPercentage ?? PercentValue.Zero;
  }

  public async Task<UserProgressDto?> GetUserProgressDtoAsync(Guid programId, Guid userId)
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

  public async Task<IEnumerable<ContentInteraction>> GetUserInteractionsAsync(Guid programId, Guid userId)
  {
    var programUser = await context.Set<ProgramUser>().Where(pu => pu.DeletedAt == null && pu.ProgramId == programId && pu.UserId == userId).FirstOrDefaultAsync();

    if (programUser == null) return [];

    return await context.Set<ContentInteraction>().Include(ci => ci.Content).Where(ci => ci.DeletedAt == null && ci.ProgramUserId == programUser.Id).OrderBy(ci => ci.Content.SortOrder).ToListAsync();
  }

  // ── Analytics & Statistics ──────────────────────────────────────────

  public async Task<int> GetProgramCountAsync(ContentStatus? status = null, ContentVisibility? visibility = null)
  {
    var query = context.Set<Program>().Where(p => p.DeletedAt == null);

    if (status.HasValue) query = query.Where(p => p.Status == status.Value);

    if (visibility.HasValue) query = query.Where(p => p.Visibility == visibility.Value);

    return await query.CountAsync().ConfigureAwait(false);
  }

  public async Task<int> GetUserCountForProgramAsync(Guid programId) { return await context.Set<ProgramUser>().Where(pu => pu.DeletedAt == null && pu.ProgramId == programId && pu.IsActive).CountAsync(); }

  public async Task<PercentValue> GetAverageCompletionRateAsync(Guid programId)
  {
    var values = await context.Set<ProgramUser>()
      .Where(pu => pu.DeletedAt == null && pu.ProgramId == programId && pu.IsActive)
      .Select(pu => pu.CompletionPercentage)
      .ToListAsync()
      .ConfigureAwait(false);
    return PercentValue.Average(values);
  }

  public async Task<Dictionary<string, object>> GetProgramStatisticsAsync(Guid programId)
  {
    var userCount = await GetUserCountForProgramAsync(programId).ConfigureAwait(false);
    var averageCompletion = await GetAverageCompletionRateAsync(programId).ConfigureAwait(false);
    var completedCount = await context.Set<ProgramUser>().Where(pu => pu.DeletedAt == null && pu.ProgramId == programId && pu.IsActive && pu.CompletedAt != null).CountAsync();

    return new Dictionary<string, object>
    {
      ["totalUsers"] = userCount,
      ["averageCompletion"] = averageCompletion,
      ["completedUsers"] = completedCount,
      ["completionRate"] = userCount > 0 ? PercentValue.FromRatio(completedCount, userCount) : PercentValue.Zero,
    };
  }

  public async Task<ProgramAnalyticsDto?> GetProgramAnalyticsAsync(Guid id)
  {
    var program = await GetProgramByIdAsync(id).ConfigureAwait(false);

    if (program == null) return null;

    var programUsers = await context.Set<ProgramUser>()
      .Where(pu => pu.ProgramId == id && pu.DeletedAt == null)
      .Select(pu => new
      {
        pu.Id,
        pu.IsActive,
        pu.JoinedAt,
        pu.StartedAt,
        pu.CompletedAt,
        pu.CompletionPercentage,
      })
      .ToListAsync()
      .ConfigureAwait(false);

    var contentIds = await context.Set<ProgramContent>()
      .Where(pc => pc.ProgramId == id && pc.DeletedAt == null)
      .Select(pc => pc.Id)
      .ToListAsync()
      .ConfigureAwait(false);

    var interactions = await context.Set<ContentInteraction>()
      .Where(ci => ci.DeletedAt == null && contentIds.Contains(ci.ContentId))
      .Select(ci => new
      {
        ci.LastAccessedAt,
        ci.TimeSpentMinutes,
      })
      .ToListAsync()
      .ConfigureAwait(false);

    var totalUsers = programUsers.Count;
    var activeUsers = programUsers.Count(pu => pu.IsActive);
    var completedUsers = programUsers.Count(pu => pu.CompletedAt.HasValue);
    var averageCompletionTime = AverageDuration(programUsers
      .Where(pu => pu.CompletedAt.HasValue)
      .Select(pu => pu.CompletedAt!.Value - (pu.StartedAt ?? pu.JoinedAt)));
    var lastActivity = interactions
      .Select(interaction => interaction.LastAccessedAt)
      .Concat(programUsers.Select(pu => pu.CompletedAt))
      .Where(activityAt => activityAt.HasValue)
      .Max();

    return new ProgramAnalyticsDto(
      id,
      program.Title,
      totalUsers,
      activeUsers,
      completedUsers,
      Percentage(completedUsers, totalUsers),
      averageCompletionTime,
      interactions.Count,
      lastActivity,
      new Dictionary<string, object>
      {
        ["averageProgress"] = PercentValue.Average(programUsers.Select(pu => pu.CompletionPercentage)),
        ["contentItems"] = contentIds.Count,
        ["totalTimeSpentMinutes"] = interactions.Sum(interaction => interaction.TimeSpentMinutes ?? 0),
      }
    );
  }

  public async Task<CompletionRatesDto?> GetCompletionRatesAsync(Guid id)
  {
    var program = await GetProgramByIdAsync(id).ConfigureAwait(false);

    if (program == null) return null;

    var programUsers = await context.Set<ProgramUser>()
      .Where(pu => pu.ProgramId == id && pu.DeletedAt == null)
      .Select(pu => new { pu.UserId, pu.CompletedAt })
      .ToListAsync()
      .ConfigureAwait(false);
    var contentItems = await context.Set<ProgramContent>()
      .Where(pc => pc.ProgramId == id && pc.DeletedAt == null)
      .Select(pc => pc.Id)
      .ToListAsync()
      .ConfigureAwait(false);
    var interactions = await context.Set<ContentInteraction>()
      .Where(ci => ci.DeletedAt == null && contentItems.Contains(ci.ContentId))
      .Select(ci => new { ci.ContentId, ci.UserId, ci.IsCompleted })
      .ToListAsync()
      .ConfigureAwait(false);

    var enrolledUserIds = programUsers.Select(pu => pu.UserId).ToHashSet();
    var contentCompletionRates = contentItems.ToDictionary(
      contentId => contentId,
      contentId => Percentage(
        interactions
          .Where(interaction => interaction.ContentId == contentId && interaction.IsCompleted && enrolledUserIds.Contains(interaction.UserId))
          .Select(interaction => interaction.UserId)
          .Distinct()
          .Count(),
        programUsers.Count));

    var trends = new List<CompletionTrendDto>();
    var cumulativeCompleted = 0;
    foreach (var completionGroup in programUsers.Where(pu => pu.CompletedAt.HasValue).GroupBy(pu => pu.CompletedAt!.Value.Date).OrderBy(group => group.Key))
    {
      cumulativeCompleted += completionGroup.Count();
      trends.Add(new CompletionTrendDto(completionGroup.Key, cumulativeCompleted, programUsers.Count, Percentage(cumulativeCompleted, programUsers.Count)));
    }

    return new CompletionRatesDto(
      id,
      Percentage(programUsers.Count(pu => pu.CompletedAt.HasValue), programUsers.Count),
      contentCompletionRates,
      trends);
  }

  public async Task<EngagementMetricsDto?> GetEngagementMetricsAsync(Guid id)
  {
    var program = await GetProgramByIdAsync(id).ConfigureAwait(false);

    if (program == null) return null;

    var enrolledUserIds = await context.Set<ProgramUser>()
      .Where(pu => pu.ProgramId == id && pu.DeletedAt == null && pu.IsActive)
      .Select(pu => pu.UserId)
      .ToListAsync()
      .ConfigureAwait(false);
    var contentIds = await context.Set<ProgramContent>()
      .Where(pc => pc.ProgramId == id && pc.DeletedAt == null)
      .Select(pc => pc.Id)
      .ToListAsync()
      .ConfigureAwait(false);
    var interactions = await context.Set<ContentInteraction>()
      .Where(ci => ci.DeletedAt == null && contentIds.Contains(ci.ContentId))
      .Select(ci => new
      {
        ci.UserId,
        ci.ContentId,
        ci.TimeSpentMinutes,
        ci.StartedAt,
        ci.CompletedAt,
        ci.FirstAccessedAt,
        ci.LastAccessedAt,
      })
      .ToListAsync()
      .ConfigureAwait(false);
    var enrolledUserSet = enrolledUserIds.ToHashSet();
    var enrolledInteractions = interactions
      .Where(interaction => enrolledUserSet.Contains(interaction.UserId))
      .ToList();
    var now = SystemClock.UtcNow;
    var today = now.Date;
    var weekStart = today.AddDays(-6);
    var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    var activityRows = enrolledInteractions
      .Select(interaction => new
      {
        interaction.UserId,
        interaction.ContentId,
        ActivityAt = interaction.LastAccessedAt ?? interaction.FirstAccessedAt ?? interaction.StartedAt ?? interaction.CompletedAt,
        Duration = interaction.TimeSpentMinutes is > 0
          ? TimeSpan.FromMinutes(interaction.TimeSpentMinutes.Value)
          : interaction.StartedAt.HasValue && interaction.CompletedAt.HasValue
            ? interaction.CompletedAt.Value - interaction.StartedAt.Value
            : TimeSpan.Zero,
      })
      .Where(activity => activity.ActivityAt.HasValue)
      .ToList();
    var monthlyActiveUsers = activityRows
      .Where(activity => activity.ActivityAt!.Value >= monthStart)
      .Select(activity => activity.UserId)
      .Distinct()
      .Count();

    return new EngagementMetricsDto(
      id,
      activityRows.Where(activity => activity.ActivityAt!.Value >= today).Select(activity => activity.UserId).Distinct().Count(),
      activityRows.Where(activity => activity.ActivityAt!.Value >= weekStart).Select(activity => activity.UserId).Distinct().Count(),
      monthlyActiveUsers,
      AverageDuration(activityRows.Select(activity => activity.Duration)),
      enrolledInteractions.Count,
      monthlyActiveUsers == 0
        ? 0m
        : Percentage(activityRows.Where(activity => activity.ActivityAt!.Value >= weekStart).Select(activity => activity.UserId).Distinct().Count(), monthlyActiveUsers),
      enrolledInteractions.GroupBy(interaction => interaction.ContentId.ToString()).ToDictionary(group => group.Key, group => group.Count())
    );
  }

  public async Task<RevenueAnalyticsDto?> GetRevenueAnalyticsAsync(Guid id)
  {
    var program = await GetProgramByIdAsync(id).ConfigureAwait(false);

    if (program == null) return null;

    return new RevenueAnalyticsDto(
      id,
      0,
      0,
      0,
      0,
      0,
      0,
      []
    );
  }

  // ── Pricing & Product Queries ───────────────────────────────────────

  public async Task<PricingDto?> GetProgramPricingAsync(Guid id)
  {
    var program = await GetProgramByIdAsync(id).ConfigureAwait(false);

    if (program == null) return null;

    return ProgramPricingMetadata.Read(program);
  }

  public async Task<IEnumerable<Guid>> GetLinkedProductsAsync(Guid programId)
  {
    var programExists = await context.Set<Program>()
      .Where(p => p.DeletedAt == null)
      .AnyAsync(p => p.Id == programId)
      .ConfigureAwait(false);

    if (!programExists) return [];

    return await context.Set<ProductProgram>()
      .Where(pp => pp.DeletedAt == null && pp.ProgramId == programId)
      .OrderBy(pp => pp.SortOrder)
      .Select(pp => pp.ProductId)
      .ToListAsync()
      .ConfigureAwait(false);
  }
}
