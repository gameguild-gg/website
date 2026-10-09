using GameGuild.API;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using GameGuild;
using GameGuild.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GameGuild.API.Core.Middleware;

/// <summary>
/// Adds shared Redis limits after the in-process limiter has admitted a request.
/// </summary>
public sealed class RedisEndpointRateLimitingMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        IDistributedRateLimiter distributedRateLimiter,
        RateLimitingOptions options,
        RateLimitAccessOptions accessOptions)
    {
        try
        {
            if (accessOptions.IsAllowlisted(context))
            {
                await _next(context).ConfigureAwait(false);
                return;
            }

            var endpoint = context.GetEndpoint();
            if (endpoint?.Metadata.GetMetadata<DisableRateLimitingAttribute>() is not null)
            {
                await _next(context).ConfigureAwait(false);
                return;
            }

            var penaltyKey = $"api:penalty:{RateLimitingServiceCollectionExtensions.GetUserOrIpPartitionKey(context)}";
            if (options.EnableProgressivePenalties)
            {
                var activePenalty = await distributedRateLimiter.GetActivePenaltyAsync(penaltyKey, context.RequestAborted)
                    .ConfigureAwait(false);
                if (activePenalty is { } remaining && remaining > TimeSpan.Zero)
                {
                    await WritePenaltyRejectionAsync(context, options.Limit, remaining).ConfigureAwait(false);
                    return;
                }
            }

            if (!RateLimitingServiceCollectionExtensions.IsExemptPath(context.Request.Path, options.ExemptPaths))
            {
                var globalKey = $"api:global:{RateLimitingServiceCollectionExtensions.GetUserOrIpPartitionKey(context)}";
                if (!await EnforceAsync(
                        context,
                        distributedRateLimiter,
                        globalKey,
                        "global",
                        options.Limit,
                        options.Period,
                        fixedWindow: true,
                        options: options,
                        penaltyKey: penaltyKey)
                        .ConfigureAwait(false))
                {
                    return;
                }
            }

            var policyName = endpoint?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
            if (TryGetDistributedPolicy(context, policyName, options, out var policy))
            {
                var policyKey = $"api:policy:{policyName}:{policy.PartitionKey}";
                if (!await EnforceAsync(
                        context,
                        distributedRateLimiter,
                        policyKey,
                        policyName!,
                        policy.Limit,
                        policy.Window,
                        policy.FixedWindow,
                        policy.TokensPerPeriod,
                        options,
                        penaltyKey)
                        .ConfigureAwait(false))
                {
                    return;
                }
            }

            IAsyncDisposable? concurrencyLease = null;
            if (policyName == RateLimitPolicies.ExpensiveOperations)
            {
                var concurrencyKey = $"api:concurrency:{policyName}:{RateLimitingServiceCollectionExtensions.GetUserPartitionKey(context)}";
                var decision = await distributedRateLimiter.TryAcquireConcurrencyLeaseAsync(
                    concurrencyKey,
                    options.MaxConcurrentRequests,
                    options.ConcurrencyLeaseDuration,
                    context.RequestAborted).ConfigureAwait(false);
                if (!decision.IsAllowed)
                {
                    var retryAfter = decision.RetryAfter;
                    if (options.EnableProgressivePenalties)
                    {
                        var penalty = await distributedRateLimiter.RecordRateLimitViolationAsync(
                            penaltyKey,
                            options.PenaltyViolationThreshold,
                            options.PenaltyDecayWindow,
                            options.PenaltyBaseDuration,
                            options.PenaltyMaxDuration,
                            context.RequestAborted).ConfigureAwait(false);
                        if (penalty is { } penaltyDuration && penaltyDuration > retryAfter)
                        {
                            retryAfter = penaltyDuration;
                        }
                    }

                    await WriteConcurrencyRejectionAsync(context, options.MaxConcurrentRequests, retryAfter)
                        .ConfigureAwait(false);
                    return;
                }

                concurrencyLease = decision.Lease;
            }

            try
            {
                await _next(context).ConfigureAwait(false);
            }
            finally
            {
                if (concurrencyLease is not null)
                {
                    await concurrencyLease.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
        catch (RateLimitBackendUnavailableException)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await RateLimitProblemDetailsWriter.WriteAsync(
                context,
                new ProblemDetails
                {
                    Type = "urn:problem-type:rate-limit-store-unavailable",
                    Title = "Rate Limiter Unavailable",
                    Status = StatusCodes.Status503ServiceUnavailable,
                    Detail = "Request admission cannot be verified while the rate-limit store is unavailable.",
                    Instance = context.Request.Path,
                    Extensions = { ["errorCode"] = "rate_limit_store_unavailable" }
                },
                context.RequestAborted).ConfigureAwait(false);
        }
    }

    private static bool TryGetDistributedPolicy(
        HttpContext context,
        string? policyName,
        RateLimitingOptions options,
        out DistributedPolicy policy)
    {
        policy = policyName switch
        {
            RateLimitPolicies.Authentication => new(
                RateLimitingServiceCollectionExtensions.GetAuthenticationPartitionKey(context),
                options.AuthenticationRequestsPerMinute,
                options.AuthenticationWindow,
                FixedWindow: true),
            RateLimitPolicies.Authorization => new(
                RateLimitingServiceCollectionExtensions.GetUserTenantPartitionKey(context),
                options.AuthorizationRequestsPerMinute,
                options.AuthorizationWindow,
                FixedWindow: true),
            RateLimitPolicies.Internal => new(
                RateLimitingServiceCollectionExtensions.GetUserPartitionKey(context),
                options.InternalRequestsPerMinute,
                options.InternalWindow,
                FixedWindow: true),
            RateLimitPolicies.Api => new(
                RateLimitingServiceCollectionExtensions.GetUserOrIpPartitionKey(context),
                options.ApiRequestsPerMinute,
                options.ApiWindow,
                FixedWindow: false),
            RateLimitPolicies.PerTenant => new(
                RateLimitingServiceCollectionExtensions.GetTenantPartitionKey(context),
                options.TenantRequestsPerMinute,
                options.TenantWindow,
                FixedWindow: false),
            RateLimitPolicies.PerUser => new(
                RateLimitingServiceCollectionExtensions.GetUserPartitionKey(context),
                options.UserRequestsPerMinute,
                options.UserWindow,
                FixedWindow: false),
            RateLimitPolicies.PerIp => new(
                RateLimitingServiceCollectionExtensions.GetIpPartitionKey(context),
                options.IpRequestsPerMinute,
                options.IpWindow,
                FixedWindow: false),
            RateLimitPolicies.Bursty => new(
                RateLimitingServiceCollectionExtensions.GetUserOrIpPartitionKey(context),
                options.TokenBucketLimit,
                options.TokenReplenishmentPeriod,
                FixedWindow: false,
                TokensPerPeriod: options.TokensPerPeriod),
            RateLimitPolicies.ApiKey => GetApiKeyPolicy(context, options),
            _ => default
        };

        if (policy.PartitionKey is not null)
        {
            return true;
        }

        if (policyName is null || !options.Policies.TryGetValue(policyName, out var configuredPolicy))
        {
            return false;
        }

        policy = new DistributedPolicy(
            RateLimitingServiceCollectionExtensions.GetPartitionKey(context, configuredPolicy.PartitionBy),
            configuredPolicy.PermitLimit,
            configuredPolicy.Window,
            FixedWindow: configuredPolicy.Algorithm == RateLimitingAlgorithm.FixedWindow,
            TokensPerPeriod: configuredPolicy.Algorithm == RateLimitingAlgorithm.TokenBucket
                ? configuredPolicy.TokensPerPeriod
                : null);

        return true;
    }

    private static DistributedPolicy GetApiKeyPolicy(HttpContext context, RateLimitingOptions options)
    {
        var partitionKey = RateLimitingServiceCollectionExtensions.GetApiKeyPartitionKey(context);
        var premium = partitionKey.StartsWith("premium:", StringComparison.Ordinal);
        var requestLimit = premium ? options.PremiumApiKeyRequestsPerMinute : options.StandardApiKeyRequestsPerMinute;
        return new DistributedPolicy(
            partitionKey,
            requestLimit,
            options.ApiKeyWindow,
            FixedWindow: false,
            TokensPerPeriod: requestLimit);
    }

    private static async Task<bool> EnforceAsync(
        HttpContext context,
        IDistributedRateLimiter limiter,
        string key,
        string policyName,
        int limit,
        TimeSpan window,
        bool fixedWindow,
        int? tokensPerPeriod = null,
        RateLimitingOptions? options = null,
        string? penaltyKey = null)
    {
        bool allowed;
        TimeSpan retryAfter;
        if (tokensPerPeriod.HasValue)
        {
            var decision = await limiter.TryAcquireTokenBucketAsync(
                key,
                limit,
                tokensPerPeriod.Value,
                window,
                context.RequestAborted).ConfigureAwait(false);
            allowed = decision.IsAllowed;
            retryAfter = decision.RetryAfter;
        }
        else
        {
            allowed = fixedWindow
                ? await limiter.IsAllowedFixedWindowAsync(key, limit, window, context.RequestAborted).ConfigureAwait(false)
                : await limiter.IsAllowedAsync(key, limit, window, context.RequestAborted).ConfigureAwait(false);
            retryAfter = TimeSpan.Zero;
        }

        if (allowed)
        {
            return true;
        }

        if (!tokensPerPeriod.HasValue)
        {
            retryAfter = fixedWindow
                ? GetTimeUntilFixedWindowReset(window)
                : await limiter.GetTimeUntilResetAsync(key, window, context.RequestAborted).ConfigureAwait(false) ?? window;
        }

        if (options?.EnableProgressivePenalties == true && penaltyKey is not null)
        {
            var penalty = await limiter.RecordRateLimitViolationAsync(
                penaltyKey,
                options.PenaltyViolationThreshold,
                options.PenaltyDecayWindow,
                options.PenaltyBaseDuration,
                options.PenaltyMaxDuration,
                context.RequestAborted).ConfigureAwait(false);
            if (penalty is { } penaltyDuration && penaltyDuration > retryAfter)
            {
                retryAfter = penaltyDuration;
            }
        }
        var retryAfterSeconds = Math.Max(0, retryAfter.TotalSeconds);

        RateLimitingMetrics.RecordRejection(policyName, "redis");
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        RateLimitingServiceCollectionExtensions.SetRateLimitHeaders(context.Response, limit, retryAfterSeconds);
        context.RequestServices.GetService<ILoggerFactory>()?
            .CreateLogger("GameGuild.API.RateLimiting")
            .LogWarning(
                "Distributed rate limit exceeded for {Path} under policy {Policy}",
                LogRedaction.Sanitize(context.Request.Path),
                LogRedaction.Sanitize(policyName));

        await RateLimitProblemDetailsWriter.WriteAsync(
            context,
            new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too Many Requests",
                Detail = $"Rate limit exceeded. Please retry after {retryAfterSeconds:F0} seconds.",
                Instance = context.Request.Path
            },
            context.RequestAborted).ConfigureAwait(false);

        return false;
    }

    private static TimeSpan GetTimeUntilFixedWindowReset(TimeSpan window)
    {
        var windowMilliseconds = checked((long)Math.Ceiling(window.TotalMilliseconds));
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return TimeSpan.FromMilliseconds(windowMilliseconds - now % windowMilliseconds);
    }

    private static async Task WriteConcurrencyRejectionAsync(
        HttpContext context,
        int limit,
        TimeSpan retryAfter)
    {
        var retryAfterSeconds = Math.Max(1, Math.Ceiling(retryAfter.TotalSeconds));
        RateLimitingMetrics.RecordRejection(RateLimitPolicies.ExpensiveOperations, "redis");
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        RateLimitingServiceCollectionExtensions.SetRateLimitHeaders(context.Response, limit, retryAfterSeconds);
        await RateLimitProblemDetailsWriter.WriteAsync(
            context,
            new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too Many Requests",
                Detail = $"Concurrent operation limit reached. Please retry after {retryAfterSeconds:F0} seconds.",
                Instance = context.Request.Path
            },
            context.RequestAborted).ConfigureAwait(false);
    }

    private static async Task WritePenaltyRejectionAsync(
        HttpContext context,
        int limit,
        TimeSpan retryAfter)
    {
        var retryAfterSeconds = Math.Max(1, Math.Ceiling(retryAfter.TotalSeconds));
        RateLimitingMetrics.RecordRejection("penalty", "redis");
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        RateLimitingServiceCollectionExtensions.SetRateLimitHeaders(context.Response, limit, retryAfterSeconds);
        await RateLimitProblemDetailsWriter.WriteAsync(
            context,
            new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Temporarily Blocked",
                Detail = $"Repeated rate-limit violations triggered a temporary block. Please retry after {retryAfterSeconds:F0} seconds.",
                Instance = context.Request.Path
            },
            context.RequestAborted).ConfigureAwait(false);
    }

    private readonly record struct DistributedPolicy(
        string PartitionKey,
        int Limit,
        TimeSpan Window,
        bool FixedWindow,
        int? TokensPerPeriod = null);
}
