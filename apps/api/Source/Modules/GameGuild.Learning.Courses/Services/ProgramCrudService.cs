using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Courses;

/// <summary>
/// Thin facade that delegates to <see cref="IProgramWriteService"/> and <see cref="IProgramReadService"/>.
/// Kept for backward compatibility — new code should depend on the focused interfaces directly.
/// </summary>
public class ProgramCrudService(IProgramReadService read, IProgramWriteService write) : IProgramCrudService
{
  // ── Read-side delegation (IProgramReadService) ──────────────────────

  public Task<Program?> GetProgramByIdAsync(Guid id) => read.GetProgramByIdAsync(id);
  public Task<Program?> GetProgramBySlugAsync(string slug) => read.GetProgramBySlugAsync(slug);
  public Task<Program?> GetPublishedProgramBySlugAsync(string slug) => read.GetPublishedProgramBySlugAsync(slug);
  public Task<Program?> GetProgramWithContentAsync(Guid id) => read.GetProgramWithContentAsync(id);
  public Task<bool> ProgramExistsAsync(Guid id) => read.ProgramExistsAsync(id);
  public Task<IEnumerable<Program>> GetProgramsAsync(int skip = 0, int take = 50) => read.GetProgramsAsync(skip, take);
  public Task<IEnumerable<ProgramContent>> GetProgramContentAsync(Guid programId) => read.GetProgramContentAsync(programId);
  public Task<IEnumerable<Program>> GetPublishedProgramsAsync(int skip = 0, int take = 50) => read.GetPublishedProgramsAsync(skip, take);
  public Task<IEnumerable<Program>> GetPublicPublishedProgramsAsync(int skip = 0, int take = 50) => read.GetPublicPublishedProgramsAsync(skip, take);
  public Task<IEnumerable<Program>> SearchProgramsAsync(string searchTerm, int skip = 0, int take = 50) => read.SearchProgramsAsync(searchTerm, skip, take);
  public Task<IEnumerable<Program>> GetProgramsByCreatorAsync(Guid creatorId, int skip = 0, int take = 50) => read.GetProgramsByCreatorAsync(creatorId, skip, take);
  public Task<IEnumerable<Program>> GetFeaturedProgramsAsync(int count = 10) => read.GetFeaturedProgramsAsync(count);
  public Task<IEnumerable<Program>> GetRecentProgramsAsync(int count = 10) => read.GetRecentProgramsAsync(count);
  public Task<IEnumerable<Program>> GetPopularProgramsAsync(int count = 10) => read.GetPopularProgramsAsync(count);
  public Task<IEnumerable<Program>> GetProgramsByCategoryAsync(ProgramCategory category, int skip = 0, int take = 50) => read.GetProgramsByCategoryAsync(category, skip, take);
  public Task<IEnumerable<Program>> GetProgramsByDifficultyAsync(ProgramDifficulty difficulty, int skip = 0, int take = 50) => read.GetProgramsByDifficultyAsync(difficulty, skip, take);
  public Task<IEnumerable<ProgramUser>> GetProgramUsersAsync(Guid programId) => read.GetProgramUsersAsync(programId);
  public Task<IEnumerable<Program>> GetUserProgramsAsync(Guid userId) => read.GetUserProgramsAsync(userId);
  public Task<bool> IsUserInProgramAsync(Guid programId, Guid userId) => read.IsUserInProgramAsync(programId, userId);
  public Task<IEnumerable<UserProgressDto>> GetProgramUsersAsync(Guid programId, int skip = 0, int take = 50) => read.GetProgramUsersAsync(programId, skip, take);
  public Task<PercentValue> GetUserProgressAsync(Guid programId, Guid userId) => read.GetUserProgressAsync(programId, userId);
  public Task<UserProgressDto?> GetUserProgressDtoAsync(Guid programId, Guid userId) => read.GetUserProgressDtoAsync(programId, userId);
  public Task<IEnumerable<ContentInteraction>> GetUserInteractionsAsync(Guid programId, Guid userId) => read.GetUserInteractionsAsync(programId, userId);
  public Task<int> GetProgramCountAsync(ContentStatus? status = null, ContentVisibility? visibility = null) => read.GetProgramCountAsync(status, visibility);
  public Task<int> GetUserCountForProgramAsync(Guid programId) => read.GetUserCountForProgramAsync(programId);
  public Task<PercentValue> GetAverageCompletionRateAsync(Guid programId) => read.GetAverageCompletionRateAsync(programId);
  public Task<Dictionary<string, object>> GetProgramStatisticsAsync(Guid programId) => read.GetProgramStatisticsAsync(programId);
  public Task<ProgramAnalyticsDto?> GetProgramAnalyticsAsync(Guid id) => read.GetProgramAnalyticsAsync(id);
  public Task<CompletionRatesDto?> GetCompletionRatesAsync(Guid id) => read.GetCompletionRatesAsync(id);
  public Task<EngagementMetricsDto?> GetEngagementMetricsAsync(Guid id) => read.GetEngagementMetricsAsync(id);
  public Task<RevenueAnalyticsDto?> GetRevenueAnalyticsAsync(Guid id) => read.GetRevenueAnalyticsAsync(id);
  public Task<PricingDto?> GetProgramPricingAsync(Guid id) => read.GetProgramPricingAsync(id);
  public Task<IEnumerable<Guid>> GetLinkedProductsAsync(Guid programId) => read.GetLinkedProductsAsync(programId);

  // ── Write-side delegation (IProgramWriteService) ────────────────────

  public Task<Program> CreateProgramAsync(Program program) => write.CreateProgramAsync(program);
  public Task<Program> UpdateProgramAsync(Program program) => write.UpdateProgramAsync(program);
  public Task DeleteProgramAsync(Guid id) => write.DeleteProgramAsync(id);
  public Task<Program> CloneProgramAsync(Guid id, string newTitle) => write.CloneProgramAsync(id, newTitle);
  public Task<Program> CreateProgramAsync(CreateProgramDto createDto) => write.CreateProgramAsync(createDto);
  public Task<Program?> UpdateProgramAsync(Guid id, UpdateProgramDto updateDto) => write.UpdateProgramAsync(id, updateDto);
  public Task<ProgramContent> AddContentAsync(Guid programId, ProgramContent content) => write.AddContentAsync(programId, content);
  public Task<ProgramContent> UpdateContentAsync(ProgramContent content) => write.UpdateContentAsync(content);
  public Task DeleteContentAsync(Guid contentId) => write.DeleteContentAsync(contentId);
  public Task<Program> ReorderContentAsync(Guid programId, List<Guid> contentIds) => write.ReorderContentAsync(programId, contentIds);
  public Task<ProgramContent?> AddContentAsync(Guid programId, CreateContentDto contentDto) => write.AddContentAsync(programId, contentDto);
  public Task<ProgramContent?> UpdateContentAsync(Guid programId, Guid contentId, UpdateContentDto contentDto) => write.UpdateContentAsync(programId, contentId, contentDto);
  public Task<bool> RemoveContentAsync(Guid programId, Guid contentId) => write.RemoveContentAsync(programId, contentId);
  public Task<ProgramUser> AddUserAsync(Guid programId, Guid userId) => write.AddUserAsync(programId, userId);
  public Task<ProgramUser> RemoveUserAsync(Guid programId, Guid userId) => write.RemoveUserAsync(programId, userId);
  public Task<UserProgressDto?> AddUserToProgramAsync(Guid programId, Guid userId) => write.AddUserToProgramAsync(programId, userId);
  public Task<bool> RemoveUserFromProgramAsync(Guid programId, Guid userId) => write.RemoveUserFromProgramAsync(programId, userId);
  public Task<Program> UpdateUserProgressAsync(Guid programId, Guid userId, Guid contentId, ProgressStatus status) => write.UpdateUserProgressAsync(programId, userId, contentId, status);
  public Task<UserProgressDto?> UpdateUserProgressAsync(Guid programId, Guid userId, UpdateProgressDto progressDto) => write.UpdateUserProgressAsync(programId, userId, progressDto);
  public Task<ContentInteraction?> SubmitUserContentAsync(Guid programId, Guid userId, Guid contentId, string submissionData) => write.SubmitUserContentAsync(programId, userId, contentId, submissionData);
  public Task<bool> MarkContentCompletedAsync(Guid programId, Guid userId, Guid contentId) => write.MarkContentCompletedAsync(programId, userId, contentId);
  public Task<bool> ResetUserProgressAsync(Guid programId, Guid userId) => write.ResetUserProgressAsync(programId, userId);
  public Task<Program?> EnableMonetizationAsync(Guid id, MonetizationDto monetizationDto) => write.EnableMonetizationAsync(id, monetizationDto);
  public Task<Program?> DisableMonetizationAsync(Guid id) => write.DisableMonetizationAsync(id);
  public Task<PricingDto?> UpdateProgramPricingAsync(Guid id, UpdatePricingDto pricingDto) => write.UpdateProgramPricingAsync(id, pricingDto);
  public Task<Guid?> CreateProductFromProgramAsync(Guid programId, CreateProductFromProgramDto productDto) => write.CreateProductFromProgramAsync(programId, productDto);
  public Task<bool> LinkProgramToProductAsync(Guid programId, Guid productId) => write.LinkProgramToProductAsync(programId, productId);
  public Task<bool> UnlinkProgramFromProductAsync(Guid programId, Guid productId) => write.UnlinkProgramFromProductAsync(programId, productId);
}
