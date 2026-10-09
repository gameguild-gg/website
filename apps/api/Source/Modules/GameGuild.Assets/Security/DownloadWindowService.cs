using Microsoft.Extensions.Logging;

namespace GameGuild.Assets.Security;

/// <summary>
/// Options for download window enforcement.
/// Mitigates: Download Window Bypass (#12)
/// </summary>
public class DownloadWindowOptions
{
    public const string SectionName = "Assets:DownloadWindow";

    /// <summary>
    /// Default download window duration in hours.
    /// </summary>
    public int DefaultWindowHours { get; set; } = 48;

    /// <summary>
    /// Maximum download window duration in hours.
    /// </summary>
    public int MaxWindowHours { get; set; } = 168; // 7 days

    /// <summary>
    /// Whether to enforce download windows strictly.
    /// </summary>
    public bool StrictEnforcement { get; set; } = true;

    /// <summary>
    /// Grace period in minutes after window expiry.
    /// </summary>
    public int GracePeriodMinutes { get; set; } = 5;
}

/// <summary>
/// Result of download window validation.
/// </summary>
public sealed record DownloadWindowValidationResult(
    bool IsValid,
    string? Error = null,
    DateTime? ExpiresAt = null,
    Guid? OrderId = null);

/// <summary>
/// Service for managing paid content download windows.
/// </summary>
public interface IDownloadWindowService
{
    /// <summary>
    /// Validates that a download window is still valid.
    /// Checks order status server-side (not just timestamp).
    /// </summary>
    Task<DownloadWindowValidationResult> ValidateDownloadWindowAsync(
        Guid assetReferenceId,
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Grants a download window for an asset after purchase.
    /// </summary>
    Task<DownloadWindowValidationResult> GrantDownloadWindowAsync(
        Guid assetReferenceId,
        Guid userId,
        Guid orderId,
        TimeSpan? customDuration = null,
        CancellationToken ct = default);

    /// <summary>
    /// Revokes a download window (e.g., refund).
    /// </summary>
    Task RevokeDownloadWindowAsync(
        Guid assetReferenceId,
        Guid orderId,
        string reason,
        CancellationToken ct = default);
}

/// <summary>
/// Order status for download window validation.
/// </summary>
public enum OrderStatus
{
    Pending = 0,
    Paid = 1,
    Fulfilled = 2,
    Refunded = 3,
    Cancelled = 4,
    Disputed = 5
}

/// <summary>
/// Interface for order validation (to be implemented by Commerce module).
/// </summary>
public interface IOrderValidationService
{
    /// <summary>
    /// Gets the current status of an order.
    /// </summary>
    Task<OrderStatus?> GetOrderStatusAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// Checks if an order is valid for download access.
    /// </summary>
    Task<bool> IsOrderValidForDownloadAsync(Guid orderId, CancellationToken ct = default);
}

/// <summary>
/// Implementation of download window service with server-side order validation.
/// </summary>
public class DownloadWindowService : IDownloadWindowService
{
    private readonly IAssetReferenceRepository _referenceRepository;
    private readonly IOrderValidationService _orderValidation;
    private readonly DownloadWindowOptions _options;
    private readonly ILogger<DownloadWindowService> _logger;

    public DownloadWindowService(
        IAssetReferenceRepository referenceRepository,
        IOrderValidationService orderValidation,
        Microsoft.Extensions.Options.IOptions<DownloadWindowOptions> options,
        ILogger<DownloadWindowService> logger)
    {
        _referenceRepository = referenceRepository;
        _orderValidation = orderValidation;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DownloadWindowValidationResult> ValidateDownloadWindowAsync(
        Guid assetReferenceId,
        Guid userId,
        CancellationToken ct = default)
    {
        var reference = await _referenceRepository.GetByIdAsync(assetReferenceId, ct).ConfigureAwait(false);
        if (reference == null)
        {
            return new DownloadWindowValidationResult(false, "Asset not found");
        }

        // Check if this is paid content
        if (reference.AccessPolicy != AssetAccessPolicy.PaidContent)
        {
            // Not paid content, no download window required
            return new DownloadWindowValidationResult(true);
        }

        // Check if there's a download window
        if (!reference.DownloadWindowExpiresAt.HasValue)
        {
            return new DownloadWindowValidationResult(false, "No download window granted");
        }

        // Check if window has expired
        var now = SystemClock.UtcNow;
        var expiry = reference.DownloadWindowExpiresAt.Value;
        var gracePeriod = TimeSpan.FromMinutes(_options.GracePeriodMinutes);

        if (now > expiry.Add(gracePeriod))
        {
            return new DownloadWindowValidationResult(
                false,
                "Download window has expired",
                expiry,
                reference.GrantedByOrderId);
        }

        // CRITICAL: Server-side order validation
        // Prevents manipulation of order status on client side
        if (reference.GrantedByOrderId.HasValue)
        {
            var isOrderValid = await _orderValidation.IsOrderValidForDownloadAsync(
                reference.GrantedByOrderId.Value, ct).ConfigureAwait(false);

            if (!isOrderValid)
            {
                _logger.LogWarning(
                    "Download window validation failed: Order {OrderId} is no longer valid",
                    reference.GrantedByOrderId);

                return new DownloadWindowValidationResult(
                    false,
                    "Order is no longer valid for download",
                    expiry,
                    reference.GrantedByOrderId);
            }
        }

        return new DownloadWindowValidationResult(
            true,
            null,
            expiry,
            reference.GrantedByOrderId);
    }

    public async Task<DownloadWindowValidationResult> GrantDownloadWindowAsync(
        Guid assetReferenceId,
        Guid userId,
        Guid orderId,
        TimeSpan? customDuration = null,
        CancellationToken ct = default)
    {
        var reference = await _referenceRepository.GetByIdAsync(assetReferenceId, ct).ConfigureAwait(false);
        if (reference == null)
        {
            return new DownloadWindowValidationResult(false, "Asset not found");
        }

        // Validate order first
        var orderStatus = await _orderValidation.GetOrderStatusAsync(orderId, ct).ConfigureAwait(false);
        if (orderStatus != OrderStatus.Paid && orderStatus != OrderStatus.Fulfilled)
        {
            return new DownloadWindowValidationResult(
                false,
                $"Order status '{orderStatus}' is not valid for granting download access");
        }

        // Calculate window duration
        var duration = customDuration ?? TimeSpan.FromHours(_options.DefaultWindowHours);
        if (duration > TimeSpan.FromHours(_options.MaxWindowHours))
        {
            duration = TimeSpan.FromHours(_options.MaxWindowHours);
        }

        var expiresAt = SystemClock.UtcNow.Add(duration);

        // Update reference with download window
        reference.DownloadWindowExpiresAt = expiresAt;
        reference.GrantedByOrderId = orderId;

        await _referenceRepository.UpdateAsync(reference, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Download window granted for asset {AssetId} to user {UserId} via order {OrderId}, expires {Expiry}",
            assetReferenceId, userId, orderId, expiresAt);

        return new DownloadWindowValidationResult(true, null, expiresAt, orderId);
    }

    public async Task RevokeDownloadWindowAsync(
        Guid assetReferenceId,
        Guid orderId,
        string reason,
        CancellationToken ct = default)
    {
        var reference = await _referenceRepository.GetByIdAsync(assetReferenceId, ct).ConfigureAwait(false);
        if (reference == null)
        {
            return;
        }

        // Only revoke if the order matches
        if (reference.GrantedByOrderId == orderId)
        {
            reference.DownloadWindowExpiresAt = null;
            reference.GrantedByOrderId = null;

            await _referenceRepository.UpdateAsync(reference, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Download window revoked for asset {AssetId}, order {OrderId}: {Reason}",
                assetReferenceId, orderId, reason);
        }
    }
}

/// <summary>
/// Fail-closed default order validation. Returns "no valid order" for everything so that
/// paid-download windows stay locked unless a commerce adapter is registered from the
/// composition root (the host knows the commerce stack; this module stays platform-generic).
/// </summary>
public sealed class DenyByDefaultOrderValidationService : IOrderValidationService
{
    public Task<OrderStatus?> GetOrderStatusAsync(Guid orderId, CancellationToken ct = default)
        => Task.FromResult<OrderStatus?>(null);

    public Task<bool> IsOrderValidForDownloadAsync(Guid orderId, CancellationToken ct = default)
        => Task.FromResult(false);
}
