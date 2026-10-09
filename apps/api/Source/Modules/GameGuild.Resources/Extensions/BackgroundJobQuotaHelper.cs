namespace GameGuild.Resources;

/// <summary>
///     Helper for enforcing resource quotas in background jobs.
///     <para>
///     <b>CRITICAL:</b> Background jobs that create resources MUST use this helper
///     to ensure quotas are properly enforced. The CQRS pipeline behavior does NOT
///     intercept direct repository calls.
///     </para>
/// </summary>
/// <remarks>
///     <para>
///     <b>Why this is needed:</b><br/>
///     When a background job creates resources using repositories directly (bypassing
///     the CQRS command pipeline), the <see cref="ResourceQuotaBehavior{TRequest,TResponse}"/>
///     is not invoked. This could lead to quota violations.
///     </para>
///     <para>
///     <b>Usage pattern:</b>
///     <code>
///     public class MyBackgroundJob(
///         IResourceQuotaService quotaService,
///         IMyRepository repository)
///     {
///         public async Task ExecuteAsync(Guid tenantId)
///         {
///             // Use the helper for quota-controlled resource creation
///             await quotaService.WithQuotaEnforcementAsync(
///                 tenantId,
///                 ResourceUsageType.Users,
///                 amount: 10,
///                 async () => await repository.CreateUsersAsync(users),
///                 source: "MyBackgroundJob"
///             );
///         }
///     }
///     </code>
///     </para>
/// </remarks>
public static class BackgroundJobQuotaHelper
{
    /// <summary>
    ///     Executes an action with quota enforcement, rolling back on failure.
    ///     <para>
    ///     This method:
    ///     <list type="number">
    ///         <item>Atomically consumes the requested quota amount</item>
    ///         <item>Executes the provided action</item>
    ///         <item>Rolls back the quota if the action throws an exception</item>
    ///     </list>
    ///     </para>
    /// </summary>
    /// <param name="quotaService">The quota service</param>
    /// <param name="tenantId">Tenant ID for quota enforcement</param>
    /// <param name="resourceType">Type of resource being created</param>
    /// <param name="amount">Number of resources being created</param>
    /// <param name="action">The action to execute (e.g., repository call)</param>
    /// <param name="source">Optional source identifier for audit trail</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <exception cref="QuotaExceededException">Thrown when the quota would be exceeded</exception>
    /// <exception cref="InvalidOperationException">Thrown when the quota service fails</exception>
    public static async Task WithQuotaEnforcementAsync(
        this IResourceQuotaService quotaService,
        Guid tenantId,
        ResourceUsageType resourceType,
        long amount,
        Func<Task> action,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(quotaService);
        ArgumentNullException.ThrowIfNull(action);

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be greater than zero");
        }

        // Step 1: Atomically consume quota BEFORE executing action
        var (success, currentUsage, hardLimit) = await quotaService.TryAtomicConsumeAsync(
            tenantId,
            resourceType,
            amount,
            cancellationToken).ConfigureAwait(false);

        if (!success)
        {
            throw new QuotaExceededException(
                $"Resource quota exceeded for {resourceType}. " +
                $"Current usage: {currentUsage}, Hard limit: {hardLimit}, Requested: {amount}",
                resourceType,
                currentUsage,
                hardLimit ?? 0,
                tenantId);
        }

        try
        {
            // Step 2: Execute the action
            await action().ConfigureAwait(false);
        }
        catch
        {
            // Step 3: Rollback quota on failure
            await quotaService.DecrementUsageAsync(
                tenantId,
                resourceType,
                amount,
                source: source ?? "BackgroundJobQuotaHelper:Rollback",
                cancellationToken: cancellationToken).ConfigureAwait(false);

            throw;
        }
    }

    /// <summary>
    ///     Executes an action with quota enforcement, returning a result.
    ///     <para>
    ///     This is the generic version that returns a value from the action.
    ///     </para>
    /// </summary>
    /// <typeparam name="T">The return type of the action</typeparam>
    /// <param name="quotaService">The quota service</param>
    /// <param name="tenantId">Tenant ID for quota enforcement</param>
    /// <param name="resourceType">Type of resource being created</param>
    /// <param name="amount">Number of resources being created</param>
    /// <param name="action">The action to execute (e.g., repository call)</param>
    /// <param name="source">Optional source identifier for audit trail</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The result of the action</returns>
    /// <exception cref="QuotaExceededException">Thrown when the quota would be exceeded</exception>
    public static async Task<T> WithQuotaEnforcementAsync<T>(
        this IResourceQuotaService quotaService,
        Guid tenantId,
        ResourceUsageType resourceType,
        long amount,
        Func<Task<T>> action,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(quotaService);
        ArgumentNullException.ThrowIfNull(action);

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be greater than zero");
        }

        // Step 1: Atomically consume quota BEFORE executing action
        var (success, currentUsage, hardLimit) = await quotaService.TryAtomicConsumeAsync(
            tenantId,
            resourceType,
            amount,
            cancellationToken).ConfigureAwait(false);

        if (!success)
        {
            throw new QuotaExceededException(
                $"Resource quota exceeded for {resourceType}. " +
                $"Current usage: {currentUsage}, Hard limit: {hardLimit}, Requested: {amount}",
                resourceType,
                currentUsage,
                hardLimit ?? 0,
                tenantId);
        }

        try
        {
            // Step 2: Execute the action
            return await action().ConfigureAwait(false);
        }
        catch
        {
            // Step 3: Rollback quota on failure
            await quotaService.DecrementUsageAsync(
                tenantId,
                resourceType,
                amount,
                source: source ?? "BackgroundJobQuotaHelper:Rollback",
                cancellationToken: cancellationToken).ConfigureAwait(false);

            throw;
        }
    }

    /// <summary>
    ///     Executes a batch operation with quota enforcement and partial success handling.
    ///     <para>
    ///     This method handles scenarios where a batch operation might partially succeed.
    ///     It consumes the full quota upfront, then adjusts based on actual success count.
    ///     </para>
    /// </summary>
    /// <typeparam name="TInput">Type of items being processed</typeparam>
    /// <typeparam name="TResult">Type of result per item</typeparam>
    /// <param name="quotaService">The quota service</param>
    /// <param name="tenantId">Tenant ID for quota enforcement</param>
    /// <param name="resourceType">Type of resource being created</param>
    /// <param name="items">Items to process (quota = count of items)</param>
    /// <param name="processor">Function to process each item, returning success/failure</param>
    /// <param name="source">Optional source identifier for audit trail</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    ///     A tuple containing:
    ///     <list type="bullet">
    ///         <item>Successful: List of successfully processed results</item>
    ///         <item>Failed: List of items that failed to process</item>
    ///     </list>
    /// </returns>
    /// <exception cref="QuotaExceededException">Thrown when the quota would be exceeded</exception>
    public static async Task<(IReadOnlyList<TResult> Successful, IReadOnlyList<TInput> Failed)> 
        WithBatchQuotaEnforcementAsync<TInput, TResult>(
            this IResourceQuotaService quotaService,
            Guid tenantId,
            ResourceUsageType resourceType,
            IReadOnlyList<TInput> items,
            Func<TInput, Task<(bool Success, TResult? Result)>> processor,
            string? source = null,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(quotaService);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(processor);

        if (items.Count == 0)
        {
            return (Array.Empty<TResult>(), Array.Empty<TInput>());
        }

        // Step 1: Atomically consume quota for the entire batch
        var batchSize = items.Count;
        var (success, currentUsage, hardLimit) = await quotaService.TryAtomicConsumeAsync(
            tenantId,
            resourceType,
            batchSize,
            cancellationToken).ConfigureAwait(false);

        if (!success)
        {
            throw new QuotaExceededException(
                $"Resource quota exceeded for batch of {batchSize} {resourceType}. " +
                $"Current usage: {currentUsage}, Hard limit: {hardLimit}",
                resourceType,
                currentUsage,
                hardLimit ?? 0,
                tenantId);
        }

        var successful = new List<TResult>();
        var failed = new List<TInput>();

        try
        {
            // Step 2: Process each item
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var (itemSuccess, result) = await processor(item).ConfigureAwait(false);
                    if (itemSuccess && result is not null)
                    {
                        successful.Add(result);
                    }
                    else
                    {
                        failed.Add(item);
                    }
                }
                catch
                {
                    failed.Add(item);
                }
            }
        }
        finally
        {
            // Step 3: Release unused quota for failed items
            if (failed.Count > 0)
            {
                await quotaService.DecrementUsageAsync(
                    tenantId,
                    resourceType,
                    failed.Count,
                    source: source ?? "BackgroundJobQuotaHelper:BatchPartialRelease",
                    cancellationToken: CancellationToken.None).ConfigureAwait(false); // Don't cancel quota release
            }
        }

        return (successful, failed);
    }
}
