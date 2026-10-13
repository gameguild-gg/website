using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace GameGuild.API.Endpoints;

internal sealed class CacheEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // Cache management mutates shared infrastructure state (clear-by-pattern,
        // connectivity tests, statistics); it is an admin-only surface and is never
        // listed in the anonymous endpoint registry. Issue #386: gated by the
        // system-configuration permission tier (SystemAdmin role or a delegated
        // system:manage-configuration grant) - platform-scoped, not tenant-clamped.
        var group = app.MapGroup("/cache")
            .WithTags("Cache")
            .WithDescription("Cache management operations")
            .RequireAuthorization(Policies.SystemConfigurationManage);

        group.MapGet("/health", GetCacheHealth)
            .WithName("GetCacheHealth")
            .WithSummary("Check cache health")
            .WithDescription("Returns the health status of the configured cache provider");

        group.MapPost("/test", TestCacheOperations)
            .WithName("TestCacheOperations")
            .WithSummary("Test cache operations")
            .WithDescription("Test basic cache set/get/delete operations");

        group.MapDelete("/clear/{pattern}", CacheEndpointHandlers.ClearCacheByPattern)
            .WithName("ClearCacheByPattern")
            .WithSummary("Clear cache by pattern")
            .WithDescription("Clear cache entries matching the specified pattern");

        group.MapGet("/stats", GetCacheStats)
            .WithName("GetCacheStats")
            .WithSummary("Get cache statistics")
            .WithDescription("Returns cache provider and connectivity statistics");
    }

    private static async Task<IResult> GetCacheHealth(
        [FromServices] ICacheService cacheService,
        CancellationToken cancellationToken)
    {
        try
        {
            var testKey = $"health_check_{Guid.NewGuid()}";
            const string TestValue = "health_check_value";

            await cacheService.SetAsync(testKey, TestValue, TimeSpan.FromSeconds(10), cancellationToken)
                .ConfigureAwait(false);
            var retrievedValue = await cacheService.GetAsync<string>(testKey, cancellationToken)
                .ConfigureAwait(false);
            await cacheService.RemoveAsync(testKey, cancellationToken).ConfigureAwait(false);

            var isHealthy = retrievedValue == TestValue;
            return Results.Ok(new
            {
                Status = isHealthy ? "Healthy" : "Unhealthy",
                Message = isHealthy ? "Cache operations successful" : "Cache operations failed",
                Timestamp = DateTimeOffset.UtcNow
            });
        }
        catch (InvalidOperationException exception)
        {
            return Results.Ok(new
            {
                Status = "Unhealthy",
                Message = $"Cache configuration error: {exception.Message}",
                Timestamp = DateTimeOffset.UtcNow
            });
        }
        catch (TimeoutException exception)
        {
            return Results.Ok(new
            {
                Status = "Unhealthy",
                Message = $"Cache timeout: {exception.Message}",
                Timestamp = DateTimeOffset.UtcNow
            });
        }
    }

    private static async Task<IResult> TestCacheOperations(
        [FromServices] ICacheService cacheService,
        CancellationToken cancellationToken)
    {
        try
        {
            var results = new List<object>();
            var testKey = $"test_{Guid.NewGuid()}";
            var testData = new { Message = "Test cache data", Timestamp = DateTimeOffset.UtcNow };

            await cacheService.SetAsync(testKey, testData, TimeSpan.FromMinutes(5), cancellationToken)
                .ConfigureAwait(false);
            results.Add(new { Operation = "Set", Status = "Success", Key = testKey });

            var retrievedData = await cacheService.GetAsync<object>(testKey, cancellationToken)
                .ConfigureAwait(false);
            results.Add(new
            {
                Operation = "Get",
                Status = retrievedData is not null ? "Success" : "Failed",
                Data = retrievedData
            });

            await cacheService.RemoveAsync(testKey, cancellationToken).ConfigureAwait(false);
            var existsAfterRemove = await cacheService.GetAsync<object>(testKey, cancellationToken)
                .ConfigureAwait(false) is not null;
            results.Add(new
            {
                Operation = "Remove",
                Status = !existsAfterRemove ? "Success" : "Failed",
                Exists = existsAfterRemove
            });

            return Results.Ok(new
            {
                Message = "Cache operations test completed",
                Results = results,
                Timestamp = DateTimeOffset.UtcNow
            });
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new
            {
                Message = "Cache operations test failed",
                Error = exception.Message,
                Timestamp = DateTimeOffset.UtcNow
            });
        }
    }

    private static Task<IResult> GetCacheStats(
        [FromServices] IConfiguration configuration,
        [FromServices] IServiceProvider services)
    {
        var redisEnabled = configuration.GetValue<bool>("Redis:Enabled");
        var multiplexer = services.GetService<IConnectionMultiplexer>();
        var redisConnected = multiplexer?.IsConnected;
        var provider = redisEnabled ? "Redis" : "Memory";
        var status = redisEnabled && redisConnected != true ? "Unavailable" : "Available";

        return Task.FromResult(Results.Ok(new
        {
            Message = "Cache statistics",
            Stats = new
            {
                Status = status,
                Type = provider,
                RedisEnabled = redisEnabled,
                RedisConnected = redisConnected,
                InstanceName = configuration["Redis:InstanceName"]
            },
            Timestamp = DateTimeOffset.UtcNow
        }));
    }
}

public static class CacheEndpointHandlers
{
    public static async Task<IResult> ClearCacheByPattern(
        string pattern,
        [FromServices] ICacheService cacheService,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return Results.BadRequest(new
            {
                Message = "Cache clear pattern is required",
                Pattern = pattern,
                Timestamp = DateTimeOffset.UtcNow
            });
        }

        var containsWildcard = pattern.Contains('*', StringComparison.Ordinal)
                               || pattern.Contains('?', StringComparison.Ordinal);

        if (cacheService is not IPatternCacheService patternCacheService)
        {
            if (containsWildcard)
            {
                return Results.Json(
                    new
                    {
                        Message = "The configured cache service does not support pattern-based cache clearing.",
                        Pattern = pattern,
                        Timestamp = DateTimeOffset.UtcNow
                    },
                    statusCode: StatusCodes.Status501NotImplemented);
            }

            await cacheService.RemoveAsync(pattern, cancellationToken).ConfigureAwait(false);
            return Results.Ok(new
            {
                Message = "Cache key cleared",
                Pattern = pattern,
                RemovedCount = 1,
                Timestamp = DateTimeOffset.UtcNow
            });
        }

        try
        {
            var removedCount = await patternCacheService
                .RemoveByPatternAsync(pattern, cancellationToken)
                .ConfigureAwait(false);

            return Results.Ok(new
            {
                Message = "Cache entries cleared",
                Pattern = pattern,
                RemovedCount = removedCount,
                Timestamp = DateTimeOffset.UtcNow
            });
        }
        catch (NotSupportedException exception)
        {
            return Results.Json(
                new
                {
                    Message = "Cache pattern clearing is not supported by the configured provider.",
                    Pattern = pattern,
                    Error = exception.Message,
                    Timestamp = DateTimeOffset.UtcNow
                },
                statusCode: StatusCodes.Status501NotImplemented);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new
            {
                Message = "Failed to clear cache",
                Pattern = pattern,
                Error = exception.Message,
                Timestamp = DateTimeOffset.UtcNow
            });
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new
            {
                Message = "Failed to clear cache",
                Pattern = pattern,
                Error = exception.Message,
                Timestamp = DateTimeOffset.UtcNow
            });
        }
    }
}
