using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Courses;

/// <summary>
/// Read-side operations for Programs: lookups, listings, search, filtering,
/// analytics, statistics, and read-only user/progress queries.
/// </summary>
public interface IProgramReadService
{
  // ── Single-entity Lookups ───────────────────────────────────────────
  Task<Program?> GetProgramByIdAsync(Guid id);
  Task<Program?> GetProgramBySlugAsync(string slug);
  Task<Program?> GetPublishedProgramBySlugAsync(string slug);
  Task<Program?> GetProgramWithContentAsync(Guid id);
  Task<bool> ProgramExistsAsync(Guid id);

  // ── Listings ────────────────────────────────────────────────────────
  Task<IEnumerable<Program>> GetProgramsAsync(int skip = 0, int take = 50);
  Task<IEnumerable<ProgramContent>> GetProgramContentAsync(Guid programId);
  Task<IEnumerable<Program>> GetPublishedProgramsAsync(int skip = 0, int take = 50);
  Task<IEnumerable<Program>> GetPublicPublishedProgramsAsync(int skip = 0, int take = 50);

  // ── Search & Discovery ──────────────────────────────────────────────
  Task<IEnumerable<Program>> SearchProgramsAsync(string searchTerm, int skip = 0, int take = 50);
  Task<IEnumerable<Program>> GetProgramsByCreatorAsync(Guid creatorId, int skip = 0, int take = 50);
  Task<IEnumerable<Program>> GetFeaturedProgramsAsync(int count = 10);
  Task<IEnumerable<Program>> GetRecentProgramsAsync(int count = 10);
  Task<IEnumerable<Program>> GetPopularProgramsAsync(int count = 10);
  Task<IEnumerable<Program>> GetProgramsByCategoryAsync(ProgramCategory category, int skip = 0, int take = 50);
  Task<IEnumerable<Program>> GetProgramsByDifficultyAsync(ProgramDifficulty difficulty, int skip = 0, int take = 50);

  // ── User & Progress Queries ─────────────────────────────────────────
  Task<IEnumerable<ProgramUser>> GetProgramUsersAsync(Guid programId);
  Task<IEnumerable<Program>> GetUserProgramsAsync(Guid userId);
  Task<bool> IsUserInProgramAsync(Guid programId, Guid userId);
  Task<IEnumerable<UserProgressDto>> GetProgramUsersAsync(Guid programId, int skip = 0, int take = 50);
  Task<PercentValue> GetUserProgressAsync(Guid programId, Guid userId);
  Task<UserProgressDto?> GetUserProgressDtoAsync(Guid programId, Guid userId);
  Task<IEnumerable<ContentInteraction>> GetUserInteractionsAsync(Guid programId, Guid userId);

  // ── Analytics & Statistics ──────────────────────────────────────────
  Task<int> GetProgramCountAsync(ContentStatus? status = null, ContentVisibility? visibility = null);
  Task<int> GetUserCountForProgramAsync(Guid programId);
  Task<PercentValue> GetAverageCompletionRateAsync(Guid programId);
  Task<Dictionary<string, object>> GetProgramStatisticsAsync(Guid programId);
  Task<ProgramAnalyticsDto?> GetProgramAnalyticsAsync(Guid id);
  Task<CompletionRatesDto?> GetCompletionRatesAsync(Guid id);
  Task<EngagementMetricsDto?> GetEngagementMetricsAsync(Guid id);
  Task<RevenueAnalyticsDto?> GetRevenueAnalyticsAsync(Guid id);

  // ── Pricing & Product Queries ───────────────────────────────────────
  Task<PricingDto?> GetProgramPricingAsync(Guid id);
  Task<IEnumerable<Guid>> GetLinkedProductsAsync(Guid programId);
}
