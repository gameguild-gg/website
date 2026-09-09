using Microsoft.EntityFrameworkCore;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Courses;

/// <summary>
/// Composite service that delegates to focused services.
/// Kept for backward compatibility — new code should depend on
/// <see cref="IProgramCrudService"/> or <see cref="IProgramLifecycleService"/> directly.
/// </summary>
public class ProgramService(IProgramCrudService crud, IProgramLifecycleService lifecycle) : IProgramService
{
  // ── IProgramCrudService delegation ──────────────────────────────────

  public Task<Program?> GetProgramByIdAsync(Guid id) => crud.GetProgramByIdAsync(id);
  public Task<Program?> GetProgramBySlugAsync(string slug) => crud.GetProgramBySlugAsync(slug);
  public Task<Program?> GetPublishedProgramBySlugAsync(string slug) => crud.GetPublishedProgramBySlugAsync(slug);
  public Task<Program?> GetProgramWithContentAsync(Guid id) => crud.GetProgramWithContentAsync(id);
  public Task<IEnumerable<Program>> GetProgramsAsync(int skip = 0, int take = 50) => crud.GetProgramsAsync(skip, take);
  public Task<Program> CreateProgramAsync(Program program) => crud.CreateProgramAsync(program);
  public Task<Program> UpdateProgramAsync(Program program) => crud.UpdateProgramAsync(program);
  public Task DeleteProgramAsync(Guid id) => crud.DeleteProgramAsync(id);
  public Task<Program> CloneProgramAsync(Guid id, string newTitle) => crud.CloneProgramAsync(id, newTitle);
  public Task<bool> ProgramExistsAsync(Guid id) => crud.ProgramExistsAsync(id);
  public Task<Program> CreateProgramAsync(CreateProgramDto createDto) => crud.CreateProgramAsync(createDto);
  public Task<Program?> UpdateProgramAsync(Guid id, UpdateProgramDto updateDto) => crud.UpdateProgramAsync(id, updateDto);
  public Task<ProgramContent?> AddContentAsync(Guid programId, CreateContentDto contentDto) => crud.AddContentAsync(programId, contentDto);
  public Task<ProgramContent?> UpdateContentAsync(Guid programId, Guid contentId, UpdateContentDto contentDto) => crud.UpdateContentAsync(programId, contentId, contentDto);
  public Task<bool> RemoveContentAsync(Guid programId, Guid contentId) => crud.RemoveContentAsync(programId, contentId);
  public Task<ProgramContent> AddContentAsync(Guid programId, ProgramContent content) => crud.AddContentAsync(programId, content);
  public Task<ProgramContent> UpdateContentAsync(ProgramContent content) => crud.UpdateContentAsync(content);
  public Task DeleteContentAsync(Guid contentId) => crud.DeleteContentAsync(contentId);
  public Task<Program> ReorderContentAsync(Guid programId, List<Guid> contentIds) => crud.ReorderContentAsync(programId, contentIds);
  public Task<IEnumerable<ProgramContent>> GetProgramContentAsync(Guid programId) => crud.GetProgramContentAsync(programId);
  public Task<ProgramUser> AddUserAsync(Guid programId, Guid userId) => crud.AddUserAsync(programId, userId);
  public Task<ProgramUser> RemoveUserAsync(Guid programId, Guid userId) => crud.RemoveUserAsync(programId, userId);
  public Task<IEnumerable<ProgramUser>> GetProgramUsersAsync(Guid programId) => crud.GetProgramUsersAsync(programId);
  public Task<IEnumerable<Program>> GetUserProgramsAsync(Guid userId) => crud.GetUserProgramsAsync(userId);
  public Task<bool> IsUserInProgramAsync(Guid programId, Guid userId) => crud.IsUserInProgramAsync(programId, userId);
  public Task<UserProgressDto?> AddUserToProgramAsync(Guid programId, Guid userId) => crud.AddUserToProgramAsync(programId, userId);
  public Task<bool> RemoveUserFromProgramAsync(Guid programId, Guid userId) => crud.RemoveUserFromProgramAsync(programId, userId);
  public Task<IEnumerable<UserProgressDto>> GetProgramUsersAsync(Guid programId, int skip = 0, int take = 50) => crud.GetProgramUsersAsync(programId, skip, take);
  public Task<PercentValue> GetUserProgressAsync(Guid programId, Guid userId) => crud.GetUserProgressAsync(programId, userId);
  public Task<UserProgressDto?> GetUserProgressDtoAsync(Guid programId, Guid userId) => crud.GetUserProgressDtoAsync(programId, userId);
  public Task<IEnumerable<ContentInteraction>> GetUserInteractionsAsync(Guid programId, Guid userId) => crud.GetUserInteractionsAsync(programId, userId);
  public Task<Program> UpdateUserProgressAsync(Guid programId, Guid userId, Guid contentId, ProgressStatus status) => crud.UpdateUserProgressAsync(programId, userId, contentId, status);
  public Task<UserProgressDto?> UpdateUserProgressAsync(Guid programId, Guid userId, UpdateProgressDto progressDto) => crud.UpdateUserProgressAsync(programId, userId, progressDto);
  public Task<ContentInteraction?> SubmitUserContentAsync(Guid programId, Guid userId, Guid contentId, string submissionData) => crud.SubmitUserContentAsync(programId, userId, contentId, submissionData);
  public Task<bool> MarkContentCompletedAsync(Guid programId, Guid userId, Guid contentId) => crud.MarkContentCompletedAsync(programId, userId, contentId);
  public Task<bool> ResetUserProgressAsync(Guid programId, Guid userId) => crud.ResetUserProgressAsync(programId, userId);
  public Task<IEnumerable<Program>> SearchProgramsAsync(string searchTerm, int skip = 0, int take = 50) => crud.SearchProgramsAsync(searchTerm, skip, take);
  public Task<IEnumerable<Program>> GetProgramsByCreatorAsync(Guid creatorId, int skip = 0, int take = 50) => crud.GetProgramsByCreatorAsync(creatorId, skip, take);
  public Task<IEnumerable<Program>> GetFeaturedProgramsAsync(int count = 10) => crud.GetFeaturedProgramsAsync(count);
  public Task<IEnumerable<Program>> GetRecentProgramsAsync(int count = 10) => crud.GetRecentProgramsAsync(count);
  public Task<IEnumerable<Program>> GetPopularProgramsAsync(int count = 10) => crud.GetPopularProgramsAsync(count);
  public Task<IEnumerable<Program>> GetProgramsByCategoryAsync(ProgramCategory category, int skip = 0, int take = 50) => crud.GetProgramsByCategoryAsync(category, skip, take);
  public Task<IEnumerable<Program>> GetProgramsByDifficultyAsync(ProgramDifficulty difficulty, int skip = 0, int take = 50) => crud.GetProgramsByDifficultyAsync(difficulty, skip, take);
  public Task<IEnumerable<Program>> GetPublishedProgramsAsync(int skip = 0, int take = 50) => crud.GetPublishedProgramsAsync(skip, take);
  public Task<IEnumerable<Program>> GetPublicPublishedProgramsAsync(int skip = 0, int take = 50) => crud.GetPublicPublishedProgramsAsync(skip, take);
  public Task<int> GetProgramCountAsync(ContentStatus? status = null, ContentVisibility? visibility = null) => crud.GetProgramCountAsync(status, visibility);
  public Task<int> GetUserCountForProgramAsync(Guid programId) => crud.GetUserCountForProgramAsync(programId);
  public Task<PercentValue> GetAverageCompletionRateAsync(Guid programId) => crud.GetAverageCompletionRateAsync(programId);
  public Task<Dictionary<string, object>> GetProgramStatisticsAsync(Guid programId) => crud.GetProgramStatisticsAsync(programId);
  public Task<ProgramAnalyticsDto?> GetProgramAnalyticsAsync(Guid id) => crud.GetProgramAnalyticsAsync(id);
  public Task<CompletionRatesDto?> GetCompletionRatesAsync(Guid id) => crud.GetCompletionRatesAsync(id);
  public Task<EngagementMetricsDto?> GetEngagementMetricsAsync(Guid id) => crud.GetEngagementMetricsAsync(id);
  public Task<RevenueAnalyticsDto?> GetRevenueAnalyticsAsync(Guid id) => crud.GetRevenueAnalyticsAsync(id);
  public Task<Program?> EnableMonetizationAsync(Guid id, MonetizationDto monetizationDto) => crud.EnableMonetizationAsync(id, monetizationDto);
  public Task<Program?> DisableMonetizationAsync(Guid id) => crud.DisableMonetizationAsync(id);
  public Task<PricingDto?> GetProgramPricingAsync(Guid id) => crud.GetProgramPricingAsync(id);
  public Task<PricingDto?> UpdateProgramPricingAsync(Guid id, UpdatePricingDto pricingDto) => crud.UpdateProgramPricingAsync(id, pricingDto);
  public Task<Guid?> CreateProductFromProgramAsync(Guid programId, CreateProductFromProgramDto productDto) => crud.CreateProductFromProgramAsync(programId, productDto);
  public Task<bool> LinkProgramToProductAsync(Guid programId, Guid productId) => crud.LinkProgramToProductAsync(programId, productId);
  public Task<bool> UnlinkProgramFromProductAsync(Guid programId, Guid productId) => crud.UnlinkProgramFromProductAsync(programId, productId);
  public Task<IEnumerable<Guid>> GetLinkedProductsAsync(Guid programId) => crud.GetLinkedProductsAsync(programId);

  // ── IProgramLifecycleService delegation ─────────────────────────────

  public Task<Program> CreateDraftAsync(Program program) => lifecycle.CreateDraftAsync(program);
  public Task<Program> SubmitForReviewAsync(Guid id) => lifecycle.SubmitForReviewAsync(id);
  public Task<Program> ApproveAsync(Guid id) => lifecycle.ApproveAsync(id);
  public Task<Program> RejectAsync(Guid id, string reason) => lifecycle.RejectAsync(id, reason);
  public Task<Program> ArchiveAsync(Guid id) => lifecycle.ArchiveAsync(id);
  public Task<Program> RestoreAsync(Guid id) => lifecycle.RestoreAsync(id);
  public Task<Program> PublishAsync(Guid id) => lifecycle.PublishAsync(id);
  public Task<Program> SetVisibilityAsync(Guid id, ContentVisibility visibility) => lifecycle.SetVisibilityAsync(id, visibility);
  public Task<Program> PublishProgramAsync(Guid id) => lifecycle.PublishProgramAsync(id);
  public Task<Program> UnpublishProgramAsync(Guid id) => lifecycle.UnpublishProgramAsync(id);
  public Task<Program> SchedulePublishAsync(Guid id, DateTime publishAt) => lifecycle.SchedulePublishAsync(id, publishAt);
  public Task<Program?> SubmitProgramAsync(Guid id) => lifecycle.SubmitProgramAsync(id);
  public Task<Program?> ApproveProgramAsync(Guid id) => lifecycle.ApproveProgramAsync(id);
  public Task<Program?> RejectProgramAsync(Guid id, string reason) => lifecycle.RejectProgramAsync(id, reason);
  public Task<Program?> WithdrawProgramAsync(Guid id) => lifecycle.WithdrawProgramAsync(id);
  public Task<Program?> ArchiveProgramAsync(Guid id) => lifecycle.ArchiveProgramAsync(id);
  public Task<Program?> RestoreProgramAsync(Guid id) => lifecycle.RestoreProgramAsync(id);
  public Task<Program?> ScheduleProgramAsync(Guid id, DateTime publishAt) => lifecycle.ScheduleProgramAsync(id, publishAt);
}
