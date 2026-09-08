using System.Reflection;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging;

namespace GameGuild.Resources;

/// <summary>
///     Pipeline behavior that automatically validates and enforces resource quotas
///     for commands decorated with the [RequiresQuota] attribute.
/// </summary>
/// <typeparam name="TRequest">The request type (command)</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public class ResourceQuotaBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequestBase
{
    private readonly IResourceQuotaService _quotaService;
    private readonly IActorContextAccessor _actorContextAccessor;
    private readonly ILogger<ResourceQuotaBehavior<TRequest, TResponse>> _logger;

    private ActorContext Actor => _actorContextAccessor.ActorContext;

    public ResourceQuotaBehavior(
        IResourceQuotaService quotaService,
        IActorContextAccessor actorContextAccessor,
        ILogger<ResourceQuotaBehavior<TRequest, TResponse>> logger)
    {
        _quotaService = quotaService;
        _actorContextAccessor = actorContextAccessor;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Check if the command requires quota validation
        var quotaAttribute = typeof(TRequest).GetCustomAttribute<RequiresQuotaAttribute>();

        if (quotaAttribute == null)
        {
            // No quota required, proceed normally
            return await next().ConfigureAwait(false);
        }

        // Ensure we have a tenant context - FAIL-CLOSED: reject if missing
        if (!Actor.TenantId.HasValue)
        {
            _logger.LogError(
                "Command {CommandType} requires quota validation but no tenant context is available. " +
                "Rejecting request to prevent quota bypass. Ensure X-Tenant-Id header is provided.",
                typeof(TRequest).Name
            );
            throw new InvalidOperationException(
                $"Quota-controlled command {typeof(TRequest).Name} requires tenant context. " +
                "Ensure X-Tenant-Id header is provided for multi-tenant operations.");
        }

        var tenantId = Actor.TenantId.Value;
        var resourceType = quotaAttribute.ResourceType;
        var amount = quotaAttribute.Amount;
        var source = quotaAttribute.Source ?? typeof(TRequest).Name;

        _logger.LogDebug(
            "Checking resource quota for tenant {TenantId}, resource {ResourceType}, amount {Amount}",
            tenantId,
            resourceType,
            amount
        );

        // Track whether we successfully consumed quota (for rollback on failure)
        var quotaConsumed = false;

        try
        {
            // ATOMIC APPROACH: Reserve quota BEFORE executing command
            // This prevents race conditions where multiple concurrent requests
            // could each pass the check but together exceed the limit
            if (quotaAttribute.RecordUsage)
            {
                var (success, currentUsage, hardLimit) = await _quotaService.TryAtomicConsumeAsync(
                    tenantId,
                    resourceType,
                    amount,
                    cancellationToken
                ).ConfigureAwait(false);

                if (!success)
                {
                    var errorMessage = $"Resource quota exceeded for {resourceType}. " +
                                       $"Current usage: {currentUsage}, " +
                                       $"Hard limit: {hardLimit}, " +
                                       $"Requested: {amount}";

                    _logger.LogError(
                        "Quota exceeded for tenant {TenantId}, resource {ResourceType}. " +
                        "Current: {CurrentUsage}, Limit: {HardLimit}, Requested: {Amount}",
                        tenantId,
                        resourceType,
                        currentUsage,
                        hardLimit,
                        amount
                    );

                    throw new QuotaExceededException(
                        errorMessage,
                        resourceType,
                        currentUsage,
                        hardLimit ?? 0,
                        tenantId
                    );
                }

                quotaConsumed = true;

                // Check soft limit for warning purposes
                var quota = await _quotaService.GetQuotaAsync(tenantId, resourceType, cancellationToken).ConfigureAwait(false);
                if (quota?.SoftLimit.HasValue == true && currentUsage > quota.SoftLimit.Value)
                {
                    _logger.LogWarning(
                        "Tenant {TenantId} is approaching resource quota limit for {ResourceType}. " +
                        "Current: {CurrentUsage}, Soft Limit: {SoftLimit}, Hard Limit: {HardLimit}",
                        tenantId,
                        resourceType,
                        currentUsage,
                        quota.SoftLimit,
                        hardLimit
                    );
                }

                _logger.LogDebug(
                    "Reserved {Amount} {ResourceType} quota for tenant {TenantId}",
                    amount,
                    resourceType,
                    tenantId
                );
            }
            else
            {
                // If not recording usage, just do an advisory check
                var limitCheck = await _quotaService.CheckLimitsAsync(
                    tenantId,
                    resourceType,
                    amount,
                    cancellationToken
                ).ConfigureAwait(false);

                if (!limitCheck.CanProceed)
                {
                    throw new QuotaExceededException(
                        $"Resource quota exceeded for {resourceType}.",
                        resourceType,
                        limitCheck.CurrentUsage,
                        limitCheck.HardLimit ?? 0,
                        tenantId
                    );
                }
            }

            // Execute the command
            var response = await next().ConfigureAwait(false);

            // CQRS handlers commonly report expected failures through Result instead of throwing.
            // A failed result did not create the resource, so release the reservation as well.
            if (quotaConsumed && CommandOutcome.IsFailure(response))
            {
                await _quotaService.DecrementUsageAsync(
                    tenantId,
                    resourceType,
                    amount,
                    cancellationToken: cancellationToken
                ).ConfigureAwait(false);
                quotaConsumed = false;
                return response;
            }

            _logger.LogInformation(
                "Successfully completed {CommandType} with {Amount} {ResourceType} quota for tenant {TenantId}",
                typeof(TRequest).Name,
                amount,
                resourceType,
                tenantId
            );

            return response;
        }
        catch (QuotaExceededException)
        {
            // Re-throw quota exceptions (quota was not consumed)
            throw;
        }
        catch (Exception ex) when (quotaConsumed)
        {
            // Command failed AFTER quota was consumed - rollback the quota
            _logger.LogWarning(
                ex,
                "Command {CommandType} failed after consuming quota. Rolling back {Amount} {ResourceType} for tenant {TenantId}",
                typeof(TRequest).Name,
                amount,
                resourceType,
                tenantId
            );

            try
            {
                await _quotaService.DecrementUsageAsync(
                    tenantId,
                    resourceType,
                    amount,
                    cancellationToken: cancellationToken
                ).ConfigureAwait(false);
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(
                    rollbackEx,
                    "Failed to rollback quota after command failure. Quota may be inconsistent for tenant {TenantId}, resource {ResourceType}",
                    tenantId,
                    resourceType
                );
            }

            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error checking or recording quota for tenant {TenantId}, resource {ResourceType}. " +
                "Rejecting request to prevent potential quota bypass.",
                tenantId,
                resourceType
            );

            // FAIL-CLOSED: Don't allow operations when quota service fails
            // This prevents quota bypass attacks when the quota service is unavailable
            throw new InvalidOperationException(
                $"Unable to verify resource quota for {resourceType}. " +
                "Request rejected for safety. Please try again later.", ex);
        }
    }

    /// <summary>
    ///     Attempts to extract user ID from the response object
    /// </summary>
    private static Guid? TryExtractUserId(TResponse response)
    {
        if (response == null)
            return null;

        // Try to get Id or UserId property via reflection
        var responseType = response.GetType();
        var idProperty = responseType.GetProperty("Id") ?? responseType.GetProperty("UserId");

        if (idProperty != null && idProperty.PropertyType == typeof(Guid))
        {
            return (Guid?)idProperty.GetValue(response);
        }

        return null;
    }
}
