using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Repository interface for PromoCode entities
/// </summary>
public interface IPromoCodeRepository
{
    /// <summary>
    /// Get a promo code by ID
    /// </summary>
    Task<PromoCode?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a promo code by code string. The input is normalized (trim + uppercase)
    /// and compared case-insensitively so lookups behave the same regardless of input casing.
    /// </summary>
    Task<PromoCode?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get promo codes matching a set of code strings (normalized, case-insensitive).
    /// </summary>
    Task<List<PromoCode>> GetByCodesAsync(IEnumerable<string> codes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all active promo codes
    /// </summary>
    Task<IEnumerable<PromoCode>> GetActiveCodesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all promo codes for a specific product
    /// </summary>
    Task<IEnumerable<PromoCode>> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all promo codes associated with a campaign
    /// </summary>
    Task<List<PromoCode>> GetByCampaignAsync(string campaignId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a paginated list of promo codes
    /// </summary>
    Task<(IEnumerable<PromoCode> Items, int TotalCount)> GetPagedAsync(
        bool? isActive = null,
        PromoCodeType? type = null,
        Guid? productId = null,
        string? searchTerm = null,
        string? campaignId = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get usage count for a promo code
    /// </summary>
    Task<int> GetUsageCountAsync(Guid promoCodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get usage count for a promo code by a specific user
    /// </summary>
    Task<int> GetUserUsageCountAsync(Guid promoCodeId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically increment the global usage counter of a promo code. Fails closed
    /// (returns <c>false</c>) when the code is missing, inactive, soft-deleted, or already
    /// at its <c>MaxUses</c> limit. The conditional update runs as a single guarded
    /// statement, so concurrent redemptions cannot exceed the limit.
    /// </summary>
    Task<bool> TryIncrementTotalUsageAsync(Guid promoCodeId, int? maxUses, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically increment the per-user usage counter of a promo code. Fails closed
    /// (returns <c>false</c>) when the user is already at the <c>MaxUsesPerUser</c> limit.
    /// Concurrency-safe via guarded update plus unique-key insert.
    /// </summary>
    Task<bool> TryIncrementUserUsageAsync(Guid promoCodeId, Guid userId, int? maxUsesPerUser, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find a previously recorded usage by idempotency key (replay protection).
    /// </summary>
    Task<PromoCodeUse?> FindUsageByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a new promo code
    /// </summary>
    Task AddAsync(PromoCode promoCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add multiple promo codes (bulk generation)
    /// </summary>
    Task AddRangeAsync(IEnumerable<PromoCode> promoCodes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing promo code
    /// </summary>
    Task UpdateAsync(PromoCode promoCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a promo code
    /// </summary>
    Task DeleteAsync(PromoCode promoCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Record a promo code usage
    /// </summary>
    Task RecordUsageAsync(PromoCodeUse usage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persist a validation attempt (issue #418 conversion tracking). The caller supplies
    /// the resolved <paramref name="promoCodeId"/> when the submitted code matched one.
    /// </summary>
    Task RecordValidationAttemptAsync(
        string code,
        Guid? promoCodeId,
        Guid? userId,
        PromoCodeValidationOutcome outcome,
        string? errorMessage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Load promo codes by identifiers (issue #418 subscription application).
    /// </summary>
    Task<List<PromoCode>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Promo usages linked to a subscription that no invoice has consumed yet
    /// (issue #418 invoice application).
    /// </summary>
    Task<IReadOnlyList<PromoCodeUse>> GetUninvoicedUsesForSubscriptionAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stamp promo usage rows as settled on an invoice (idempotent: rows already
    /// invoiced are skipped). Returns the number of rows stamped.
    /// </summary>
    Task<int> MarkUsesInvoicedAsync(
        IReadOnlyList<Guid> usageIds,
        Guid invoiceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a promo code exists
    /// </summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a promo code string is already in use (normalized, case-insensitive)
    /// </summary>
    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check which of the given (normalized) codes already exist
    /// </summary>
    Task<HashSet<string>> GetExistingCodesAsync(IEnumerable<string> codes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get usage statistics for a promo code
    /// </summary>
    Task<PromoCodeUsageDto> GetUsageStatsAsync(Guid promoCodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get aggregated performance for a campaign within an optional window
    /// </summary>
    Task<CampaignPerformanceDto> GetCampaignPerformanceAsync(
        string campaignId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Begin a database transaction (used by the atomic usage recorder)
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Save changes to the database
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
