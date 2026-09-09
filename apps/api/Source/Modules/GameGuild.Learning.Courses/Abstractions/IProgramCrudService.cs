using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Courses;

/// <summary>
/// Service interface for Program CRUD operations, search, filtering, analytics, monetization, and product integration.
/// </summary>
public interface IProgramCrudService
{
  // Basic CRUD
  Task<Program?> GetProgramByIdAsync(Guid id);
  Task<Program?> GetProgramBySlugAsync(string slug);
  Task<Program?> GetPublishedProgramBySlugAsync(string slug);
  Task<Program?> GetProgramWithContentAsync(Guid id);
  Task<IEnumerable<Program>> GetProgramsAsync(int skip = 0, int take = 50);
  Task<Program> CreateProgramAsync(Program program);
  Task<Program> UpdateProgramAsync(Program program);
  Task DeleteProgramAsync(Guid id);
  Task<Program> CloneProgramAsync(Guid id, string newTitle);
  Task<bool> ProgramExistsAsync(Guid id);

  // CRUD with DTOs
  Task<Program> CreateProgramAsync(CreateProgramDto createDto);
  Task<Program?> UpdateProgramAsync(Guid id, UpdateProgramDto updateDto);

  // Content management (DTO-based, delegates to IProgramContentService internally)
  Task<ProgramContent?> AddContentAsync(Guid programId, CreateContentDto contentDto);
  Task<ProgramContent?> UpdateContentAsync(Guid programId, Guid contentId, UpdateContentDto contentDto);
  Task<bool> RemoveContentAsync(Guid programId, Guid contentId);

  // Legacy content methods kept for backward compatibility
  Task<ProgramContent> AddContentAsync(Guid programId, ProgramContent content);
  Task<ProgramContent> UpdateContentAsync(ProgramContent content);
  Task DeleteContentAsync(Guid contentId);
  Task<Program> ReorderContentAsync(Guid programId, List<Guid> contentIds);
  Task<IEnumerable<ProgramContent>> GetProgramContentAsync(Guid programId);

  // User management (DTO-based, co-located here because ProgramController calls them)
  Task<ProgramUser> AddUserAsync(Guid programId, Guid userId);
  Task<ProgramUser> RemoveUserAsync(Guid programId, Guid userId);
  Task<IEnumerable<ProgramUser>> GetProgramUsersAsync(Guid programId);
  Task<IEnumerable<Program>> GetUserProgramsAsync(Guid userId);
  Task<bool> IsUserInProgramAsync(Guid programId, Guid userId);
  Task<UserProgressDto?> AddUserToProgramAsync(Guid programId, Guid userId);
  Task<bool> RemoveUserFromProgramAsync(Guid programId, Guid userId);
  Task<IEnumerable<UserProgressDto>> GetProgramUsersAsync(Guid programId, int skip = 0, int take = 50);

  // Progress
  Task<PercentValue> GetUserProgressAsync(Guid programId, Guid userId);
  Task<UserProgressDto?> GetUserProgressDtoAsync(Guid programId, Guid userId);
  Task<IEnumerable<ContentInteraction>> GetUserInteractionsAsync(Guid programId, Guid userId);
  Task<Program> UpdateUserProgressAsync(Guid programId, Guid userId, Guid contentId, ProgressStatus status);
  Task<UserProgressDto?> UpdateUserProgressAsync(Guid programId, Guid userId, UpdateProgressDto progressDto);
  Task<ContentInteraction?> SubmitUserContentAsync(Guid programId, Guid userId, Guid contentId, string submissionData);
  Task<bool> MarkContentCompletedAsync(Guid programId, Guid userId, Guid contentId);
  Task<bool> ResetUserProgressAsync(Guid programId, Guid userId);

  // Search & Discovery
  Task<IEnumerable<Program>> SearchProgramsAsync(string searchTerm, int skip = 0, int take = 50);
  Task<IEnumerable<Program>> GetProgramsByCreatorAsync(Guid creatorId, int skip = 0, int take = 50);
  Task<IEnumerable<Program>> GetFeaturedProgramsAsync(int count = 10);
  Task<IEnumerable<Program>> GetRecentProgramsAsync(int count = 10);
  Task<IEnumerable<Program>> GetPopularProgramsAsync(int count = 10);
  Task<IEnumerable<Program>> GetProgramsByCategoryAsync(ProgramCategory category, int skip = 0, int take = 50);
  Task<IEnumerable<Program>> GetProgramsByDifficultyAsync(ProgramDifficulty difficulty, int skip = 0, int take = 50);
  Task<IEnumerable<Program>> GetPublishedProgramsAsync(int skip = 0, int take = 50);
  Task<IEnumerable<Program>> GetPublicPublishedProgramsAsync(int skip = 0, int take = 50);

  // Analytics & Statistics
  Task<int> GetProgramCountAsync(ContentStatus? status = null, ContentVisibility? visibility = null);
  Task<int> GetUserCountForProgramAsync(Guid programId);
  Task<PercentValue> GetAverageCompletionRateAsync(Guid programId);
  Task<Dictionary<string, object>> GetProgramStatisticsAsync(Guid programId);
  Task<ProgramAnalyticsDto?> GetProgramAnalyticsAsync(Guid id);
  Task<CompletionRatesDto?> GetCompletionRatesAsync(Guid id);
  Task<EngagementMetricsDto?> GetEngagementMetricsAsync(Guid id);
  Task<RevenueAnalyticsDto?> GetRevenueAnalyticsAsync(Guid id);

  // Monetization
  Task<Program?> EnableMonetizationAsync(Guid id, MonetizationDto monetizationDto);
  Task<Program?> DisableMonetizationAsync(Guid id);
  Task<PricingDto?> GetProgramPricingAsync(Guid id);
  Task<PricingDto?> UpdateProgramPricingAsync(Guid id, UpdatePricingDto pricingDto);

  // Product Integration
  Task<Guid?> CreateProductFromProgramAsync(Guid programId, CreateProductFromProgramDto productDto);
  Task<bool> LinkProgramToProductAsync(Guid programId, Guid productId);
  Task<bool> UnlinkProgramFromProductAsync(Guid programId, Guid productId);
  Task<IEnumerable<Guid>> GetLinkedProductsAsync(Guid programId);
}
