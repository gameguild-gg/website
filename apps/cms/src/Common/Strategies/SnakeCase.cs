using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Serialization;

/// <summary>
/// Provides utilities for converting strings and type names to snake_case format.
/// </summary>
public static class SnakeCase
{
    private static readonly SnakeCaseNamingStrategy _namingStrategy = new();
    private static IMemoryCache _cache;

    static SnakeCase()
    {
        var options = new MemoryCacheOptions
        {
            SizeLimit = 1000, // Maximum 1000 entries
            CompactionPercentage = 0.25 // Remove 25% of entries when limit is reached
        };
        _cache = new MemoryCache(options);
    }

    /// <summary>
    /// Converts a string to snake_case.
    /// </summary>
    /// <param name="name">The string to convert.</param>
    /// <returns>The converted string in snake_case.</returns>
    /// <exception cref="ArgumentException">Thrown when name is null or empty.</exception>
    public static string Convert(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Name cannot be null or empty.", nameof(name));
        }

        var cacheKey = $"string:{name}";
        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.Size = 1; // Each entry counts as 1 towards the size limit
            entry.SlidingExpiration = TimeSpan.FromMinutes(30); // Expire after 30 minutes of inactivity
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2); // Absolute expiration after 2 hours
            return _namingStrategy.GetPropertyName(name, false);
        }) ?? string.Empty;
    }

    /// <summary>
    /// Converts a type name to snake_case.
    /// </summary>
    /// <typeparam name="T">The type whose name to convert.</typeparam>
    /// <returns>The converted type name in snake_case.</returns>
    public static string Convert<T>()
    {
        return Convert(typeof(T));
    }

    /// <summary>
    /// Converts a type name to snake_case.
    /// </summary>
    /// <param name="type">The type whose name to convert.</param>
    /// <returns>The converted type name in snake_case.</returns>
    /// <exception cref="ArgumentNullException">Thrown when type is null.</exception>
    public static string Convert(Type type)
    {
        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        var cacheKey = $"type:{type.FullName}";
        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.Size = 1; // Each entry counts as 1 towards the size limit
            entry.SlidingExpiration = TimeSpan.FromMinutes(30); // Expire after 30 minutes of inactivity
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2); // Absolute expiration after 2 hours
            return _namingStrategy.GetPropertyName(type.Name, false);
        }) ?? string.Empty;
    }

    /// <summary>
    /// Converts a string to snake_case without caching (useful for one-time conversions).
    /// </summary>
    /// <param name="name">The string to convert.</param>
    /// <returns>The converted string in snake_case.</returns>
    public static string ConvertUncached(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return string.Empty;
        }

        return _namingStrategy.GetPropertyName(name, false);
    }

    /// <summary>
    /// Converts multiple strings to snake_case.
    /// </summary>
    /// <param name="names">The strings to convert.</param>
    /// <returns>An array of converted strings in snake_case.</returns>
    public static string[] ConvertMany(params string[] names)
    {
        if (names == null || names.Length == 0)
        {
            return Array.Empty<string>();
        }

        var result = new string[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            result[i] = Convert(names[i]);
        }
        return result;
    }

    /// <summary>
    /// Converts multiple type names to snake_case.
    /// </summary>
    /// <param name="types">The types whose names to convert.</param>
    /// <returns>An array of converted type names in snake_case.</returns>
    public static string[] ConvertMany(params Type[] types)
    {
        if (types == null || types.Length == 0)
        {
            return Array.Empty<string>();
        }

        var result = new string[types.Length];
        for (int i = 0; i < types.Length; i++)
        {
            result[i] = Convert(types[i]);
        }
        return result;
    }

    /// <summary>
    /// Clears the internal cache by disposing and recreating it. Useful for memory management in long-running applications.
    /// </summary>
    public static void ClearCache()
    {
        // IMemoryCache doesn't have a Clear method, so we need to dispose and recreate
        _cache.Dispose();
        var options = new MemoryCacheOptions
        {
            SizeLimit = 1000, // Maximum 1000 entries
            CompactionPercentage = 0.25 // Remove 25% of entries when limit is reached
        };
        _cache = new MemoryCache(options);
    }

    /// <summary>
    /// Disposes the memory cache. Call this method when the application is shutting down.
    /// </summary>
    public static void Dispose()
    {
        _cache?.Dispose();
    }
}